using System;
using System.Collections.Generic;
using UnityEngine;

public partial class AnimalSimulation
{
    private readonly List<TileCoord> _lavaFleeCandidateTiles = new List<TileCoord>(8);

    public bool TryApplyLavaThreatToGroup(
        int groupId,
        float fleeChance01,
        bool instantKillIfFleeFails,
        int damageIfFleeFails,
        int fleeSearchDistance,
        Func<TileCoord, bool> isLavaOrIncomingLavaAtTile,
        Func<TileCoord, bool> isValidFleeTile,
        bool debugLogging)
    {
        if (!_groups.TryGetValue(groupId, out AnimalGroupState group) || group == null)
            return false;

        if (!group.isAlive || group.size <= 0)
            return false;

        TileCoord originalTile = group.tile;

        fleeChance01 = Mathf.Clamp01(fleeChance01);
        fleeSearchDistance = Mathf.Max(1, fleeSearchDistance);

        bool canAttemptFlee =
            fleeChance01 > 0f &&
            UnityEngine.Random.value <= fleeChance01;

        if (canAttemptFlee &&
            TryFindLavaFleeTile(
                group,
                originalTile,
                fleeSearchDistance,
                isLavaOrIncomingLavaAtTile,
                isValidFleeTile,
                out TileCoord fleeTile) &&
            !fleeTile.Equals(originalTile))
        {
            group.tile = fleeTile;
            group.lastAction = AnimalActionType.Move;

            // Clear target states because lava forcibly displaced the group.
            group.isHunting = false;
            group.huntingTargetGroupId = -1;
            group.isTargetedByPredator = false;
            group.targetedByPredatorGroupId = -1;

            group.isInPredatorConflict = false;
            group.predatorConflictTargetGroupId = -1;

            group.isRaidingPlayerTile = false;
            group.isHuntingHumanUnits = false;

            MoveGroupInTileIndex(groupId, originalTile, fleeTile);

            group.EnsureHealthValid();
            _groups[groupId] = group;

            OnGroupUpdated?.Invoke(group);
            OnSimulationStateChanged?.Invoke();

            if (debugLogging)
            {
                Debug.Log(
                    $"[AnimalSimulation] Lava forced animal group {groupId} to flee " +
                    $"from {originalTile} to {fleeTile}.");
            }

            return true;
        }

        if (instantKillIfFleeFails)
        {
            if (debugLogging)
            {
                Debug.Log(
                    $"[AnimalSimulation] Lava killed animal group {groupId} at {originalTile}. " +
                    $"Flee failed.");
            }

            CleanupTargetsOnDeath(ref group);
            RemoveGroup(groupId, originalTile);
            OnSimulationStateChanged?.Invoke();
            return true;
        }

        int finalDamage = Mathf.Max(0, damageIfFleeFails);
        if (finalDamage <= 0)
            return false;

        group.currentHealth = Mathf.Max(0, group.currentHealth - finalDamage);

        int healthPerAnimal = GetResolvedHealthPerAnimalFor(group);
        int newSize = Mathf.Max(0, Mathf.CeilToInt(group.currentHealth / (float)healthPerAnimal));

        if (newSize != group.size)
            group.size = newSize;

        if (group.currentHealth <= 0 || group.size <= 0)
        {
            if (debugLogging)
            {
                Debug.Log(
                    $"[AnimalSimulation] Lava damage killed animal group {groupId} at {originalTile}. " +
                    $"Damage={finalDamage}");
            }

            CleanupTargetsOnDeath(ref group);
            RemoveGroup(groupId, originalTile);
            OnSimulationStateChanged?.Invoke();
            return true;
        }

        group.EnsureHealthValid();
        _groups[groupId] = group;

        OnGroupUpdated?.Invoke(group);
        OnSimulationStateChanged?.Invoke();

        if (debugLogging)
        {
            Debug.Log(
                $"[AnimalSimulation] Lava damaged animal group {groupId} at {originalTile}. " +
                $"Damage={finalDamage} RemainingSize={group.size}");
        }

        return true;
    }

    private bool TryFindLavaFleeTile(
        AnimalGroupState group,
        TileCoord blockedTile,
        int maxDistance,
        Func<TileCoord, bool> isLavaOrIncomingLavaAtTile,
        Func<TileCoord, bool> isValidFleeTile,
        out TileCoord bestTile)
    {
        bestTile = blockedTile;

        AnimalDefinition species = group.species;
        if (species == null || _env == null)
            return false;

        bool found = false;
        float bestScore = float.NegativeInfinity;

        _lavaFleeCandidateTiles.Clear();

        List<TileCoord> neighbours = GetNeighbourTilesCached(blockedTile, Mathf.Max(1, maxDistance));

        for (int i = 0; i < neighbours.Count; i++)
        {
            TileCoord coord = neighbours[i];

            if (coord.Equals(blockedTile))
                continue;

            if (isValidFleeTile != null && !isValidFleeTile(coord))
                continue;

            if (isLavaOrIncomingLavaAtTile != null && isLavaOrIncomingLavaAtTile(coord))
                continue;

            if (IsPlayerBuildingTile(coord))
                continue;

            if (ShouldAvoidHumans(species) && IsPlayerBuildingTile(coord))
                continue;

            TileEnvironmentData data = _env.GetTileData(coord);

            if (!IsTileSuitableForForcedRelocation(species, data))
                continue;

            float habitatScore = GetHabitatSuitability(species, data);

            float hungerPct = species.maxHunger > 0f ? group.hunger / species.maxHunger : 0f;
            float thirstPct = species.maxThirst > 0f ? group.thirst / species.maxThirst : 0f;

            float waterBonus = data.hasWater ? thirstPct * 2f : 0f;
            float foodBonus = data.plantFood > 0f ? hungerPct * 1.25f : 0f;

            int occupantCount = _tileIndex.TryGetValue(coord, out List<int> occupants) && occupants != null
                ? occupants.Count
                : 0;

            float crowdPenalty = occupantCount * 0.2f;

            float lavaDangerPenalty = 0f;
            if (isLavaOrIncomingLavaAtTile != null && isLavaOrIncomingLavaAtTile(coord))
                lavaDangerPenalty = 999f;

            float score =
                habitatScore +
                waterBonus +
                foodBonus -
                data.dangerLevel * 0.5f -
                crowdPenalty -
                lavaDangerPenalty;

            if (!found || score > bestScore)
            {
                found = true;
                bestScore = score;
                bestTile = coord;
            }
        }

        return found;
    }
}