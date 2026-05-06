using System;

public partial class AnimalSimulation
{
    // batching tick state
    private int[] _tickOrder = Array.Empty<int>();
    private int _tickOrderCount = 0;
    private int _tickCursor = 0;
    private int _tickTurn = -1;

    private void EnsureTickOrderCapacity(int needed)
    {
        if (_tickOrder == null || _tickOrder.Length < needed)
            _tickOrder = new int[needed];
    }

    public void BeginTurnTick(int currentTurn)
    {
        EnsureTickOrderCapacity(_groups.Count);
        PurgeInvalidGroups();

        _tickOrderCount = 0;
        foreach (var id in _groups.Keys)
            _tickOrder[_tickOrderCount++] = id;

        _tickCursor = 0;
        _tickTurn = currentTurn;

        _turnIncomingAnimalsByTile.Clear();
        _turnIncomingGroupsByTile.Clear();
        _speciesCapSuppressedThisTurn.Clear();
    }

    private int GetTurnIncomingAnimalsOnTile(TileCoord tile)
    {
        return _turnIncomingAnimalsByTile.TryGetValue(tile, out int value) ? value : 0;
    }

    private int GetTurnIncomingGroupsOnTile(TileCoord tile)
    {
        return _turnIncomingGroupsByTile.TryGetValue(tile, out int value) ? value : 0;
    }

    private void RegisterTurnIncomingMove(TileCoord oldTile, TileCoord newTile, int movedAnimals)
    {
        if (oldTile == newTile || movedAnimals <= 0)
            return;

        if (_turnIncomingAnimalsByTile.TryGetValue(newTile, out int animals))
            _turnIncomingAnimalsByTile[newTile] = animals + movedAnimals;
        else
            _turnIncomingAnimalsByTile[newTile] = movedAnimals;

        if (_turnIncomingGroupsByTile.TryGetValue(newTile, out int groups))
            _turnIncomingGroupsByTile[newTile] = groups + 1;
        else
            _turnIncomingGroupsByTile[newTile] = 1;
    }

    public bool TickSomeAnimals(int maxGroupsToProcess)
    {
        if (_tickTurn < 0)
            return true;

        if (_tickOrderCount == 0)
            return true;

        int processed = 0;
        bool anyStateChangedThisBatch = false;

        while (_tickCursor < _tickOrderCount && processed < maxGroupsToProcess)
        {
            int id = _tickOrder[_tickCursor++];

            if (!_groups.TryGetValue(id, out var group))
                continue;

            if (_tickTurn < group.nextUpdateTurn)
                continue;

            var oldTile = group.tile;

            TickGroup(id, ref group, _tickTurn);
            anyStateChangedThisBatch = true;

            if (!group.isAlive)
            {
                RemoveGroup(id, oldTile);
            }
            else
            {
                if (group.tile != oldTile)
                {
                    MoveGroupInTileIndex(id, oldTile, group.tile);
                    RegisterTurnIncomingMove(oldTile, group.tile, group.size);
                }

                _groups[id] = group;
                OnGroupUpdated?.Invoke(group);
            }

            processed++;
        }

        bool done = _tickCursor >= _tickOrderCount;

        if (done)
        {
            ProcessPendingGroupSplits();
            EnforceAllSpeciesGroupCaps();

            _tickTurn = -1;
            _tickOrderCount = 0;
            _tickCursor = 0;
        }

        if (anyStateChangedThisBatch)
            NotifySimulationStateChanged();

        return done;
    }

    public void PurgeInvalidGroups()
    {
        _tmpSpeciesCapGroupIds.Clear();

        foreach (var kvp in _groups)
        {
            var group = kvp.Value;

            if (group == null || !group.isAlive || group.size <= 0 || group.currentHealth <= 0 || group.species == null)
                _tmpSpeciesCapGroupIds.Add(kvp.Key);
        }

        for (int i = 0; i < _tmpSpeciesCapGroupIds.Count; i++)
        {
            int id = _tmpSpeciesCapGroupIds[i];

            if (_groups.TryGetValue(id, out var group))
            {
                RemoveGroup(
                    id,
                    group.tile,
                    eventName: "PURGE-INVALID",
                    reason: $"Purged invalid group. size={group.size}, hp={group.currentHealth}, speciesNull={group.species == null}");
            }
        }

        _tmpSpeciesCapGroupIds.Clear();
    }
}