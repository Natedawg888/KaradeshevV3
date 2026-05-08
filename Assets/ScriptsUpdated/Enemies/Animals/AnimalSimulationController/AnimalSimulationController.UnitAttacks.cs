using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class AnimalSimulationController : MonoBehaviour
{
    private void RefreshHumanUnitGroups()
    {
        if (_grid == null)
            _grid = FindObjectOfType<GridManager>();

        _humanUnitsById.Clear();
        _humanInfosBuffer.Clear();

        var controls = FindAllFast<TileUnitGroupControl>();

        for (int c = 0; c < controls.Length; c++)
        {
            var ctrl = controls[c];
            if (ctrl == null || ctrl.Groups == null) continue;

            var gp = _grid.GetGridPosition(ctrl.transform.position);
            var coord = new TileCoord { x = gp.x, y = gp.y };

            for (int i = 0; i < ctrl.Groups.Count; i++)
            {
                var g = ctrl.Groups[i];
                if (g == null || g.unitType == null) continue;
                if (!g.unitType.isHuman) continue;
                if (g.unitCount <= 0 || g.currentHealth <= 0) continue;

                _humanUnitsById[g.groupId] = (ctrl, g);

                _humanInfosBuffer.Add(new AnimalSimulation.HumanUnitGroupInfo
                {
                    groupId = g.groupId,
                    tile = coord,
                    unitCount = g.unitCount
                });
            }
        }

        InvalidateUnitMarkerCache();

        _simulation.SetHumanUnitGroups(_humanInfosBuffer);
    }

    private void HandleGroupAttackedPlayerUnitGroup(int animalGroupId, string unitGroupId, TileCoord tile)
    {
        if (_simulation == null) return;

        if (!_humanUnitsById.TryGetValue(unitGroupId, out var entry))
            return;

        var owner = entry.owner;
        var target = entry.group;

        if (owner == null || target == null || target.unitType == null)
            return;

        if (target.unitCount <= 0 || target.currentHealth <= 0)
            return;

        if (!_simulation.TryGetGroup(animalGroupId, out var animal) || animal == null || animal.species == null || !animal.isAlive)
            return;

        bool isNewAttacker = !_animalToUnitTargetNext.ContainsKey(animalGroupId) ||
                             _animalToUnitTargetNext[animalGroupId] != unitGroupId;

        RegisterUnitAttackThisTurn(animalGroupId, unitGroupId);

        if (isNewAttacker)
            PostAnimalTargetedUnitNotification(target, animal.species, owner);

        var species = animal.species;

        float hitChance = GetAnimalHitChanceVsUnit(species, target);
        if (UnityEngine.Random.value > hitChance)
        {
            owner.RefreshMarker(target);
            return;
        }

        float baseDamage = Mathf.Max(1f, animal.size * baseDamagePerAnimal);

        float strengthMult = Mathf.Lerp(0.6f, 2.0f, Mathf.Clamp01(species.strength));

        float sizeMult = species.sizeCategory switch
        {
            AnimalSizeCategory.Small => 0.6f,
            AnimalSizeCategory.Medium => 1.0f,
            AnimalSizeCategory.Large => 1.6f,
            AnimalSizeCategory.Giant => 2.4f,
            _ => 1.0f
        };

        float rawDamage = baseDamage * strengthMult * sizeMult;

        int defDefense = target.unitType.defense + target.bonusDefense;
        float defenseMult = 1f / (1f + defDefense * animalVsUnitDefenseMitigationPerPoint);
        defenseMult = Mathf.Max(minAnimalDamageMultAfterDefense, defenseMult);

        int damage = Mathf.Clamp(
            Mathf.RoundToInt(rawDamage * defenseMult),
            minUnitDamagePerAttack,
            maxUnitDamagePerAttack
        );

        int perUnitHp = Mathf.Max(1, target.unitType.maxHealth + target.bonusHealth);

        int oldHealth = Mathf.Max(0, target.currentHealth);
        int newHealth = Mathf.Max(0, oldHealth - damage);

        int beforeUnitsAlive = oldHealth <= 0 ? 0 : Mathf.CeilToInt(oldHealth / (float)perUnitHp);
        int afterUnitsAlive = newHealth <= 0 ? 0 : Mathf.CeilToInt(newHealth / (float)perUnitHp);
        int unitsKilled = Mathf.Max(0, beforeUnitsAlive - afterUnitsAlive);

        target.currentHealth = newHealth;

        if (unitsKilled > 0)
        {
            // ✅ reduce total population for population-backed unit groups
            if (!string.IsNullOrEmpty(target.populationReservationId))
            {
                var pop = PlayersPopulationManager.Instance;
                pop?.ApplyPenaltyFromReservation(target.populationReservationId, unitsKilled);

                // keep this in sync so the fatality cleanup can be robust
                target.reservedPopulation = Mathf.Max(0, target.reservedPopulation - unitsKilled);
            }

            // your existing logic
            target.unitCount = Mathf.Max(0, target.unitCount - unitsKilled);
            target.maxHealth = perUnitHp * Mathf.Max(0, target.unitCount);
            target.currentHealth = Mathf.Clamp(target.currentHealth, 0, Mathf.Max(0, target.maxHealth));
        }

        if (target.unitCount <= 0 || target.currentHealth <= 0)
        {
            owner.RemoveGroupDueToFatalities(target);
            return;
        }

        owner.RefreshMarker(target);
    }

    private static T[] FindAllFastIncludeInactive<T>() where T : UnityEngine.Object
    {
    #if UNITY_2022_2_OR_NEWER
        return UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    #else
        return UnityEngine.Object.FindObjectsOfType<T>(true);
    #endif
    }

    private void InvalidateUnitMarkerCache() => _unitMarkerCacheDirty = true;

    private void RebuildUnitMarkerCacheIfNeeded()
    {
        if (!_unitMarkerCacheDirty) return;
        _unitMarkerCacheDirty = false;

        _unitMarkerByGroupId.Clear();

        var markers = FindAllFastIncludeInactive<UnitGroupMarker>();
        for (int i = 0; i < markers.Length; i++)
        {
            var m = markers[i];
            if (m == null) continue;

            var g = m.BoundGroup;
            if (g == null) continue;

            if (string.IsNullOrEmpty(g.groupId)) continue;
            _unitMarkerByGroupId[g.groupId] = m;
        }
    }

    private void SetUnitMarkerUnderAttack(string unitGroupId, bool on)
    {
        if (string.IsNullOrEmpty(unitGroupId)) return;

        RebuildUnitMarkerCacheIfNeeded();

        if (_unitMarkerByGroupId.TryGetValue(unitGroupId, out var marker) && marker != null)
            marker.SetUnderAttack(on);
    }

    private static void Inc(Dictionary<string, int> dict, string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        dict.TryGetValue(key, out int v);
        dict[key] = v + 1;
    }

    private static int Dec(Dictionary<string, int> dict, string key)
    {
        if (string.IsNullOrEmpty(key)) return 0;
        if (!dict.TryGetValue(key, out int v)) return 0;

        v -= 1;
        if (v <= 0)
        {
            dict.Remove(key);
            return 0;
        }

        dict[key] = v;
        return v;
    }

    private void RegisterUnitAttackThisTurn(int animalGroupId, string unitGroupId)
    {
        if (string.IsNullOrEmpty(unitGroupId)) return;

        // If this animal was already counted attacking a different unit this turn, move its "slot"
        if (_animalToUnitTargetNext.TryGetValue(animalGroupId, out var oldUnitId) &&
            !string.IsNullOrEmpty(oldUnitId) &&
            oldUnitId != unitGroupId)
        {
            int remaining = Dec(_unitAttackCountNext, oldUnitId);
            if (remaining == 0)
                SetUnitMarkerUnderAttack(oldUnitId, false);
        }

        _animalToUnitTargetNext[animalGroupId] = unitGroupId;
        Inc(_unitAttackCountNext, unitGroupId);

        // Show immediately
        SetUnitMarkerUnderAttack(unitGroupId, true);
    }

    private void UnregisterAnimalAttackIfAny(int animalGroupId)
    {
        if (!_animalToUnitTargetNext.TryGetValue(animalGroupId, out var unitGroupId) ||
            string.IsNullOrEmpty(unitGroupId))
            return;

        _animalToUnitTargetNext.Remove(animalGroupId);

        int remaining = Dec(_unitAttackCountNext, unitGroupId);
        if (remaining == 0)
        {
            // animal died and it was the last attacker => hide immediately
            SetUnitMarkerUnderAttack(unitGroupId, false);
        }
    }

    private void CommitUnitUnderAttackIconsAfterTurn()
    {
        // copy next -> current
        _unitAttackCountCurrent.Clear();
        foreach (var kvp in _unitAttackCountNext)
            _unitAttackCountCurrent[kvp.Key] = kvp.Value;

        // Apply to all known markers
        RebuildUnitMarkerCacheIfNeeded();

        foreach (var kvp in _unitMarkerByGroupId)
        {
            var unitId = kvp.Key;
            var marker = kvp.Value;
            if (marker == null) continue;

            bool isAttacked = _unitAttackCountCurrent.ContainsKey(unitId);
            marker.SetUnderAttack(isAttacked);
        }
    }

    private void EnsureUnitControlsCache()
    {
        if (Time.unscaledTime < _nextUnitControlsRescanAt && _unitControlsCache.Count > 0)
            return;

        _unitControlsCache.Clear();

        // FindAllFast uses FindObjectsSortMode.None on Unity 2022.2+ — avoids sort overhead
        var controls = FindAllFast<TileUnitGroupControl>();
        for (int i = 0; i < controls.Length; i++)
            if (controls[i] != null)
                _unitControlsCache.Add(controls[i]);

        _nextUnitControlsRescanAt = Time.unscaledTime + unitControlsCacheRescanInterval;
    }

    private void RefreshPlayerTargetedAnimalIcons()
    {
        // Build a set of animal ids currently targeted by player unit actions
        _playerTargetedAnimalIds.Clear();
        EnsureUnitControlsCache();

        for (int c = 0; c < _unitControlsCache.Count; c++)
        {
            var ctrl = _unitControlsCache[c];
            if (ctrl == null || ctrl.Groups == null) continue;

            for (int i = 0; i < ctrl.Groups.Count; i++)
            {
                var g = ctrl.Groups[i];
                if (g == null || g.unitType == null) continue;

                // Only player humans (matches your earlier filtering style)
                if (!g.unitType.isHuman) continue;

                // ✅ This is what your melee + ranged actions use for animal targeting
                if (g.activeMeleeTargetType == MeleeTargetType.Animal && g.activeMeleeTargetAnimalId >= 0)
                    _playerTargetedAnimalIds.Add(g.activeMeleeTargetAnimalId);
            }
        }

        // Apply to animal markers
        foreach (var kv in _markerViews)
        {
            var animalId = kv.Key;
            var marker = kv.Value;
            if (marker == null) continue;

            marker.SetPlayerTargeted(_playerTargetedAnimalIds.Contains(animalId));
        }
    }

    private static void PostAnimalTargetedUnitNotification(TileUnitGroupData target, AnimalDefinition species, TileUnitGroupControl owner)
    {
        if (NotificationManager.Instance == null) return;
        string groupName   = !string.IsNullOrWhiteSpace(target.groupName) ? target.groupName : "Unit Group";
        string unitName    = target.unitType != null && !string.IsNullOrWhiteSpace(target.unitType.unitName)
            ? target.unitType.unitName : "Unit";
        string speciesName = species != null && !string.IsNullOrWhiteSpace(species.displayName)
            ? species.displayName : "Animal";
        Vector3 pos = owner != null ? owner.transform.position : default;
        string title, message;
        if (NotificationMessageCrafterManager.Instance != null)
            (title, message) = NotificationMessageCrafterManager.Instance.CraftUnitTargetedByAnimal(groupName, unitName, speciesName);
        else
            (title, message) = ("Under Attack!", $"{groupName} is being attacked by {speciesName}!");
        NotificationManager.Instance.AddNotification(NotificationType.UnitTargetedByAnimal, title, message, pos);
    }

    private float GetAnimalHitChanceVsUnit(AnimalDefinition species, TileUnitGroupData target)
    {
        if (species == null || target == null || target.unitType == null)
            return baseAnimalHitChance;

        int unitAgility = target.unitType.agility + target.bonusAgility;
        int unitSkill   = Mathf.Max(0, target.skillLevel);

        float avoid = (unitAgility * unitAgilityAvoidPerPoint) + (unitSkill * unitSkillAvoidPerLevel);
        float hit   = baseAnimalHitChance + (Mathf.Clamp01(species.speed) * animalSpeedHitBonus) - avoid;

        return Mathf.Clamp(hit, minAnimalHitChance, maxAnimalHitChance);
    }
}
