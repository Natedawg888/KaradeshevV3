using System;
using UnityEngine;

public partial class AnimalSimulation
{
    public bool TryApplyEarthquakeThreatToGroup(
        int groupId,
        float fleeChance01,
        bool instantKillIfFleeFails,
        int damageIfFleeFails,
        int fleeSearchDistance,
        Func<TileCoord, bool> isDangerousEarthquakeTile,
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
            TryFindFireFleeTile(
                group,
                originalTile,
                fleeSearchDistance,
                isDangerousEarthquakeTile,
                isValidFleeTile,
                out TileCoord fleeTile) &&
            !fleeTile.Equals(originalTile))
        {
            group.tile = fleeTile;
            group.lastAction = AnimalActionType.Move;

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
                    $"[AnimalSimulation] Earthquake forced animal group {groupId} to flee " +
                    $"from {originalTile} to {fleeTile}."
                );
            }

            return true;
        }

        if (instantKillIfFleeFails)
        {
            if (debugLogging)
            {
                Debug.Log(
                    $"[AnimalSimulation] Earthquake killed animal group {groupId} at {originalTile}. " +
                    $"Flee failed."
                );
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
                    $"[AnimalSimulation] Earthquake damage killed animal group {groupId} at {originalTile}. " +
                    $"Damage={finalDamage}"
                );
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
                $"[AnimalSimulation] Earthquake damaged animal group {groupId} at {originalTile}. " +
                $"Damage={finalDamage} RemainingSize={group.size}"
            );
        }

        return true;
    }
}