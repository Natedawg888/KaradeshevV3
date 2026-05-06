using System;
using System.Collections.Generic;
using UnityEngine;

public partial class AnimalSimulation
{
    private readonly List<TileCoord> _tornadoPushCandidateTiles = new List<TileCoord>(8);

    public bool HasGroupsAtTile(TileCoord tile)
    {
        return _tileIndex.TryGetValue(tile, out List<int> list) &&
               list != null &&
               list.Count > 0;
    }

    public int GetGroupIdsAtTileNonAlloc(TileCoord tile, List<int> results)
    {
        if (results == null)
            return 0;

        results.Clear();

        if (!_tileIndex.TryGetValue(tile, out List<int> list) || list == null || list.Count == 0)
            return 0;

        for (int i = 0; i < list.Count; i++)
            results.Add(list[i]);

        return results.Count;
    }

    public bool TryApplyTornadoEffectToGroup(
        int groupId,
        int damagePerTurn,
        bool canThrowGroup,
        float throwChance,
        Func<TileCoord, bool> isTornadoAtTile,
        Func<TileCoord, bool> isValidPushTile,
        bool debugLogging)
    {
        if (!_groups.TryGetValue(groupId, out AnimalGroupState group) || group == null)
            return false;

        if (!group.isAlive || group.size <= 0)
            return false;

        TileCoord originalTile = group.tile;
        bool anyChanged = false;

        if (damagePerTurn > 0)
        {
            int finalDamage = Mathf.Max(0, damagePerTurn);
            if (finalDamage > 0)
            {
                group.currentHealth = Mathf.Max(0, group.currentHealth - finalDamage);

                int healthPerAnimal = GetResolvedHealthPerAnimalFor(group);
                int newSize = Mathf.Max(0, Mathf.CeilToInt(group.currentHealth / (float)healthPerAnimal));

                if (newSize != group.size)
                    group.size = newSize;

                anyChanged = true;

                if (group.currentHealth <= 0 || group.size <= 0)
                {
                    if (debugLogging)
                    {
                        Debug.Log(
                            $"[AnimalSimulation] Tornado killed animal group {groupId} at {originalTile}. " +
                            $"Damage={finalDamage}");
                    }

                    CleanupTargetsOnDeath(ref group);
                    RemoveGroup(groupId, originalTile);
                    OnSimulationStateChanged?.Invoke();
                    return true;
                }
            }
        }

        if (canThrowGroup &&
            throwChance > 0f &&
            UnityEngine.Random.value <= throwChance &&
            TryFindTornadoPushTile(group, originalTile, isTornadoAtTile, isValidPushTile, out TileCoord pushedTile) &&
            !pushedTile.Equals(originalTile))
        {
            group.tile = pushedTile;
            group.lastAction = AnimalActionType.Move;

            // Clear target states because the tornado forcibly displaced the group.
            group.isHunting = false;
            group.huntingTargetGroupId = -1;
            group.isTargetedByPredator = false;
            group.targetedByPredatorGroupId = -1;

            group.isInPredatorConflict = false;
            group.predatorConflictTargetGroupId = -1;

            group.isRaidingPlayerTile = false;
            group.isHuntingHumanUnits = false;

            MoveGroupInTileIndex(groupId, originalTile, pushedTile);
            anyChanged = true;

            if (debugLogging)
            {
                Debug.Log(
                    $"[AnimalSimulation] Tornado pushed animal group {groupId} from {originalTile} to {pushedTile}.");
            }
        }

        if (!anyChanged)
            return false;

        group.EnsureHealthValid();
        _groups[groupId] = group;
        OnGroupUpdated?.Invoke(group);
        OnSimulationStateChanged?.Invoke();
        return true;
    }

    private bool TryFindTornadoPushTile(
        AnimalGroupState group,
        TileCoord blockedTile,
        Func<TileCoord, bool> isTornadoAtTile,
        Func<TileCoord, bool> isValidPushTile,
        out TileCoord bestTile)
    {
        bestTile = blockedTile;

        AnimalDefinition species = group.species;
        if (species == null || _env == null)
            return false;

        bool found = false;
        float bestScore = float.NegativeInfinity;

        _tornadoPushCandidateTiles.Clear();

        List<TileCoord> neighbours = GetNeighbourTilesCached(blockedTile, 1);
        for (int i = 0; i < neighbours.Count; i++)
        {
            TileCoord coord = neighbours[i];

            if (coord.Equals(blockedTile))
                continue;

            if (isValidPushTile != null && !isValidPushTile(coord))
                continue;

            if (isTornadoAtTile != null && isTornadoAtTile(coord))
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

            float score = habitatScore
                        + waterBonus
                        + foodBonus
                        - data.dangerLevel * 0.5f
                        - crowdPenalty;

            if (!found || score > bestScore)
            {
                found = true;
                bestScore = score;
                bestTile = coord;
            }
        }

        return found;
    }

    private int GetResolvedHealthPerAnimalFor(AnimalGroupState group)
    {
        if (group == null)
            return 1;

        if (group.resolvedHealthPerAnimal > 0)
            return group.resolvedHealthPerAnimal;

        if (group.species != null && group.species.healthPerAnimal > 0)
            return group.species.healthPerAnimal;

        return 1;
    }
}