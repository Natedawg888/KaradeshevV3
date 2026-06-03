using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class AnimalSimulationController : MonoBehaviour
{
    private void UpdateBuildingUnderAttackFromGroup(AnimalGroupState group)
    {
        bool isAttackingBuilding =
            group.lastAction == AnimalActionType.AttackPlayerTile &&
            group.isRaidingPlayerTile;

        bool hadOld = _attackingBuildingByGroup.TryGetValue(group.id, out var oldTile);

        if (isAttackingBuilding)
        {
            var targetTile = group.raidTargetTile;

            // If target tile isn't a known building tile, don't toggle anything.
            if (!_buildingByTile.ContainsKey(targetTile))
            {
                if (hadOld)
                {
                    _attackingBuildingByGroup.Remove(group.id);
                    DecrementBuildingAttack(oldTile);
                }
                return;
            }

            if (!hadOld)
            {
                _attackingBuildingByGroup[group.id] = targetTile;
                IncrementBuildingAttack(targetTile);
                return;
            }

            if (oldTile != targetTile)
            {
                DecrementBuildingAttack(oldTile);
                _attackingBuildingByGroup[group.id] = targetTile;
                IncrementBuildingAttack(targetTile);
                return;
            }

            // same tile, ensure it’s on
            SetBuildingUnderAttack(targetTile, true);
            return;
        }

        // not attacking now
        if (hadOld)
        {
            _attackingBuildingByGroup.Remove(group.id);
            DecrementBuildingAttack(oldTile);
        }
    }

    private void RemoveBuildingAttacker(int groupId)
    {
        if (_attackingBuildingByGroup.TryGetValue(groupId, out var tile))
        {
            _attackingBuildingByGroup.Remove(groupId);
            DecrementBuildingAttack(tile);
        }
    }

    private void IncrementBuildingAttack(TileCoord tile)
    {
        _buildingAttackCounts.TryGetValue(tile, out int count);
        count++;
        _buildingAttackCounts[tile] = count;

        SetBuildingUnderAttack(tile, true);
    }

    private void DecrementBuildingAttack(TileCoord tile)
    {
        if (!_buildingAttackCounts.TryGetValue(tile, out int count))
        {
            // failsafe
            SetBuildingUnderAttack(tile, false);
            return;
        }

        count--;
        if (count <= 0)
        {
            _buildingAttackCounts.Remove(tile);
            SetBuildingUnderAttack(tile, false);
        }
        else
        {
            _buildingAttackCounts[tile] = count;
        }
    }

    private void SetBuildingUnderAttack(TileCoord buildingTile, bool underAttack)
    {
        if (!_attackIconsByTile.TryGetValue(buildingTile, out var icons) || icons == null)
        {
            if (!_buildingByTile.TryGetValue(buildingTile, out var building) || building == null)
                return;

            var tile = building.GetComponentInParent<TileControl>();
            Transform searchRoot = tile != null ? tile.transform : building.transform;

            // ✅ Only icons under BuildingTileCanvas* parents
            icons = CollectBuildingAttackIcons(searchRoot);
            _attackIconsByTile[buildingTile] = icons;
        }

        if (icons == null || icons.Length == 0) return;

        for (int i = 0; i < icons.Length; i++)
        {
            var v = icons[i];
            if (v != null)
                v.SetUnderAttack(underAttack);
        }
    }

    // -------- Habitat helpers --------

    private void ResetBuildingAttackIcons()
    {
        foreach (var kvp in _attackIconsByTile)
        {
            var icons = kvp.Value;
            if (icons == null) continue;
            for (int i = 0; i < icons.Length; i++)
                if (icons[i] != null) icons[i].SetUnderAttack(false);
        }
    }

    private void RefreshPlayerBuildingTiles()
    {
        if (_grid == null)
            _grid = FindObjectOfType<GridManager>();

        _buildingByTile.Clear();
        _attackIconsByTile.Clear();

        // reset counts each turn so icons reflect CURRENT attacks only
        _attackingBuildingByGroup.Clear();
        _buildingAttackCounts.Clear();

        var buildings = FindAllFast<BuildingControl>();

        for (int i = 0; i < buildings.Length; i++)
        {
            var b = buildings[i];
            if (b == null) continue;

            Vector2Int gp;
            var tile = b.GetComponentInParent<TileControl>();
            if (tile != null)
                gp = _grid.GetGridPosition(tile.transform.position);
            else
                gp = _grid.GetGridPosition(b.transform.position);

            var coord = new TileCoord(gp.x, gp.y);

            bool destroyed = false;
            if (b.TryGetComponent(out BuildingStatus status))
                destroyed = status.CurrentState == BuildingState.Destroyed;

            // Always cache + hide icons (even if destroyed), so they don't get stuck on
            Transform searchRoot = tile != null ? tile.transform : b.transform;
            var icons = CollectBuildingAttackIcons(searchRoot);
            if (icons != null && icons.Length > 0)
            {
                _attackIconsByTile[coord] = icons;
                for (int k = 0; k < icons.Length; k++)
                    if (icons[k] != null)
                        icons[k].SetUnderAttack(false);
            }

            if (destroyed)
                continue;

            _buildingByTile[coord] = b;
        }

        _simulation.SetPlayerBuildingTiles(_buildingByTile.Keys);
    }

    private void HandleGroupAttackedPlayerTile(int animalGroupId, TileCoord tile)
    {
        if (_simulation == null) return;

        if (!_buildingByTile.TryGetValue(tile, out var building) || building == null)
            return;

        if (!_simulation.TryGetGroup(animalGroupId, out var animal) || animal == null || animal.species == null || !animal.isAlive)
            return;

        // ✅ Register immediately from the ATTACK EVENT (don’t rely on TryGetGroup state being updated yet)
        bool isNewRaid = !_attackingBuildingByGroup.TryGetValue(animalGroupId, out var existingTile)
                         || !existingTile.Equals(tile);

        RegisterBuildingAttackerFromAttackEvent(animalGroupId, tile);

        if (isNewRaid)
            PostAnimalRaidNotification(animal.species, building);

        var species = animal.species;

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

        int damage = Mathf.Clamp(
            Mathf.RoundToInt(baseDamage * strengthMult * sizeMult),
            minBuildingDamagePerAttack,
            maxBuildingDamagePerAttack
        );

        building.ApplyDamage(damage);

        // ✅ If it got destroyed, force-hide the icon now
        if (building.TryGetComponent(out BuildingStatus st) && st.CurrentState == BuildingState.Destroyed)
        {
            SetBuildingUnderAttack(tile, false);
            _buildingByTile.Remove(tile);
        }
        else
        {
            SetBuildingUnderAttack(tile, true);
        }
    }

    private static bool HasAncestorNameContaining(Transform t, string contains)
    {
        while (t != null)
        {
            if (!string.IsNullOrEmpty(t.name) && t.name.Contains(contains))
                return true;
            t = t.parent;
        }
        return false;
    }

    private static BuildingUnderAttackIconView[] CollectBuildingAttackIcons(Transform tileRoot)
    {
        if (tileRoot == null) return null;

        // Grab all icons, then filter to only those living under a BuildingTileCanvas* parent.
        var all = tileRoot.GetComponentsInChildren<BuildingUnderAttackIconView>(true);
        if (all == null || all.Length == 0) return all;

        int keep = 0;
        for (int i = 0; i < all.Length; i++)
        {
            var v = all[i];
            if (v == null) continue;

            // ✅ Supports: BuildingTileCanvasStage00, BuildingTileCanvasStage01, etc
            if (HasAncestorNameContaining(v.transform, "BuildingTileCanvas"))
                all[keep++] = v;
        }

        if (keep == all.Length) return all;
        if (keep == 0) return System.Array.Empty<BuildingUnderAttackIconView>();

        var trimmed = new BuildingUnderAttackIconView[keep];
        for (int i = 0; i < keep; i++) trimmed[i] = all[i];
        return trimmed;
    }

    private static void PostAnimalRaidNotification(AnimalDefinition species, BuildingControl building)
    {
        if (NotificationManager.Instance == null) return;
        string speciesName   = species != null && !string.IsNullOrWhiteSpace(species.displayName)
            ? species.displayName : "Animals";
        string buildingName  = building != null && !string.IsNullOrWhiteSpace(building.buildingName)
            ? building.buildingName : "a building";
        Vector3 pos = building != null ? building.transform.position : default;
        string title, message;
        if (NotificationMessageCrafterManager.Instance != null)
            (title, message) = NotificationMessageCrafterManager.Instance.CraftAnimalRaidingBuilding(speciesName, buildingName);
        else
            (title, message) = ("Building Under Raid!", $"{speciesName} are attacking {buildingName}!");
        NotificationManager.Instance.AddNotification(NotificationType.AnimalRaidingBuilding, title, message, pos);
    }

    private void RegisterBuildingAttackerFromAttackEvent(int attackerGroupId, TileCoord buildingTile)
    {
        if (attackerGroupId < 0) return;

        // only if this tile is a currently-known building tile
        if (!_buildingByTile.ContainsKey(buildingTile))
            return;

        if (_attackingBuildingByGroup.TryGetValue(attackerGroupId, out var oldTile))
        {
            if (oldTile.Equals(buildingTile))
            {
                // already registered
                SetBuildingUnderAttack(buildingTile, true);
                return;
            }

            // switched target
            _attackingBuildingByGroup[attackerGroupId] = buildingTile;
            DecrementBuildingAttack(oldTile);
            IncrementBuildingAttack(buildingTile);
            return;
        }

        _attackingBuildingByGroup[attackerGroupId] = buildingTile;
        IncrementBuildingAttack(buildingTile);
    }
}
