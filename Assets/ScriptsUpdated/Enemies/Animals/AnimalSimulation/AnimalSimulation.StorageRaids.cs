using System.Collections.Generic;
using UnityEngine;

public partial class AnimalSimulation
{
    private readonly List<TileCoord> _storageAdjBuffer = new List<TileCoord>(8);

    private bool HandleStorageRaiding(ref AnimalGroupState predator, float hungerPct)
    {
        var species = predator.species;
        if (species == null || !species.raidsStorageForFood)
            return false;

        if (hungerPct < species.storageRaidHungerThreshold)
            return false;

        if (_storageFoodByTile.Count == 0)
            return false;

        // Check tiles adjacent to the predator for an immediately attackable storage
        _env.GetNeighbourTilesNonAlloc(predator.tile, 1, _storageAdjBuffer, includeCenter: false);
        for (int i = 0; i < _storageAdjBuffer.Count; i++)
        {
            TileCoord n = _storageAdjBuffer[i];
            if (!_storageFoodByTile.TryGetValue(n, out int foodAmt) || foodAmt <= 0)
                continue;

            int requestedAmount = Mathf.Max(1, predator.size * species.foodStolenPerRaidAction);
            predator.isHunting = true;
            predator.lastAction = AnimalActionType.AttackPlayer;
            OnGroupAttemptedStorageRaid?.Invoke(predator.id, n, requestedAmount);
            return true;
        }

        // Find nearest edible storage tile within range, avoiding repelled tiles
        bool found = false;
        TileCoord best = default;
        int bestDist = int.MaxValue;

        foreach (var kvp in _storageFoodByTile)
        {
            if (kvp.Value <= 0) continue;

            int dist = Manhattan(predator.tile, kvp.Key);
            if (dist <= 0 || dist > species.storageRaidRangeTiles) continue;
            if (IsTileRepelled(kvp.Key)) continue;

            if (!found || dist < bestDist)
            {
                found = true;
                best = kvp.Key;
                bestDist = dist;
            }
        }

        if (!found)
            return false;

        TileCoord next = StepTowards(predator.tile, best, allowStepOnGoalEvenIfBuilding: false);
        if (next.Equals(predator.tile))
            return false;

        predator.tile = next;
        predator.isHunting = true;
        predator.lastAction = AnimalActionType.Move;
        return true;
    }
}
