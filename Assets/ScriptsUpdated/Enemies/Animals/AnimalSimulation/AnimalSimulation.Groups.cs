using System;
using System.Collections.Generic;
using UnityEngine;

public partial class AnimalSimulation
{
    private readonly Dictionary<TileCoord, int> _turnIncomingAnimalsByTile = new();
    private readonly Dictionary<TileCoord, int> _turnIncomingGroupsByTile = new();

    private readonly List<int> _tmpSpeciesCapGroupIds = new(64);
    private readonly HashSet<AnimalDefinition> _tmpSpeciesCapSpecies = new();

    private readonly Dictionary<AnimalDefinition, HashSet<int>> _liveGroupIdsBySpecies = new();
    private readonly Dictionary<int, AnimalDefinition> _liveSpeciesByGroupId = new();
    private readonly List<int> _staleRegistryGroupIds = new(16);

    public void SpawnGroup(AnimalDefinition species, TileCoord tile, int size)
    {
        SpawnGroup(species, tile, size, 0);
    }

    public void SpawnGroup(AnimalDefinition species, TileCoord tile, int size, int startingAgeInTurns)
    {
        if (species == null || size <= 0)
            return;

        if (HasReachedGroupCap())
            return;

        int maxAge = Mathf.Max(0, species.maxAgeInTurns);
        int clampedAge = Mathf.Clamp(startingAgeInTurns, 0, maxAge);

        var group = new AnimalGroupState
        {
            id = _nextGroupId++,
            species = species,
            size = size,
            ageInTurns = clampedAge,
            currentHealth = -1,
            hunger = 0f,
            thirst = 0f,
            tile = tile,
            lastAction = AnimalActionType.Idle,
            nextUpdateTurn = 0,

            isLeader = false,
            herdId = 0,
            leaderGroupId = 0,

            isHunting = false,
            huntingTargetGroupId = -1,
            isTargetedByPredator = false,
            targetedByPredatorGroupId = -1,
            huntingEscapeCount = 0,

            nextReproductionTurn = 0,
            isOnReproductionCooldown = false,

            isInPredatorConflict = false,
            predatorConflictTargetGroupId = -1,

            isFleeingFromThreat = false,
            fleeFromPredatorGroupId = -1,
            fleeUntilDistanceTiles = 0,
            fleeThreatLastKnownTile = tile,
            fleeStepsRemaining = 0,

            hasWaterSearchMemory = false,
            lastWaterSearchPreviousTile = tile,
            secondLastWaterSearchPreviousTile = tile,
            waterSearchBacktrackAvoidanceTurns = 0,

            isRaidingPlayerTile = false,
            raidTargetTile = tile,

            isHuntingHumanUnits = false,
            huntingHumanUnitGroupId = null
        };

        RollGroupCoreStats(ref group);
        group.EnsureHealthValid();

        _groups[group.id] = group;
        AddToTileIndex(group.id, group.tile);
        RegisterGroupInSpeciesRegistry(group.id, group.species);

        // This may remove surplus groups immediately if species cap is exceeded.
        EnforceSpeciesGroupCapFor(species);

        // Only notify if this newly spawned group still exists after cap enforcement.
        if (_groups.ContainsKey(group.id))
        {
            OnGroupCreated?.Invoke(group);
            NotifySimulationStateChanged();
        }
    }

    private void AddToTileIndex(int id, TileCoord tile)
    {
        if (!_tileIndex.TryGetValue(tile, out var list))
        {
            list = new List<int>();
            _tileIndex[tile] = list;
        }
        list.Add(id);
    }

    private void RemoveFromTileIndex(int id, TileCoord tile)
    {
        if (_tileIndex.TryGetValue(tile, out var list))
        {
            list.Remove(id);
            if (list.Count == 0)
                _tileIndex.Remove(tile);
        }
    }

    private void MoveGroupInTileIndex(int groupId, TileCoord oldTile, TileCoord newTile)
    {
        if (oldTile.Equals(newTile))
            return;

        RemoveFromTileIndex(groupId, oldTile);
        AddToTileIndex(groupId, newTile);
    }

    public void RemoveGroup(int groupId, TileCoord tile, string eventName = "REMOVE", string reason = null)
    {
        if (_groups.TryGetValue(groupId, out var group))
        {
            CleanupTargetsOnDeath(ref group);

            LogAnimalEvent(
                eventName,
                group,
                string.IsNullOrWhiteSpace(reason)
                    ? $"RemoveGroup called at tile {tile}."
                    : reason);
        }

        DeregisterGroupFromSpeciesRegistry(groupId);
        _groups.Remove(groupId);
        RemoveFromTileIndex(groupId, tile);

        OnGroupRemoved?.Invoke(groupId);
        NotifySimulationStateChanged();
    }

    private void EnforceAllSpeciesGroupCaps()
    {
        _tmpSpeciesCapSpecies.Clear();

        foreach (var kvp in _groups)
        {
            var group = kvp.Value;
            if (group == null || !group.isAlive || group.species == null)
                continue;

            _tmpSpeciesCapSpecies.Add(group.species);
        }

        foreach (var species in _tmpSpeciesCapSpecies)
            EnforceSpeciesGroupCapFor(species);

        _tmpSpeciesCapSpecies.Clear();
    }

    private void EnforceSpeciesGroupCapFor(AnimalDefinition species)
    {
        if (species == null)
            return;

        if (IsSpeciesCapSuppressedThisTurn(species))
            return;

        int baseCap = Mathf.Max(0, species.maxLiveGroupsOnMap);
        int cap = GetEffectiveSpeciesGroupCap(species);

        if (cap <= 0)
            return;

        _tmpSpeciesCapGroupIds.Clear();

        foreach (var kvp in _groups)
        {
            var group = kvp.Value;
            if (group == null || !group.isAlive || group.species != species)
                continue;

            _tmpSpeciesCapGroupIds.Add(kvp.Key);
        }

        int liveCount = _tmpSpeciesCapGroupIds.Count;
        if (liveCount <= cap)
            return;

        _tmpSpeciesCapGroupIds.Sort(CompareGroupsOldestFirst);

        int removeCount = liveCount - cap;

        for (int i = 0; i < removeCount; i++)
        {
            int id = _tmpSpeciesCapGroupIds[i];

            if (!_groups.TryGetValue(id, out var group) || group == null || !group.isAlive)
                continue;

            string speciesName = !string.IsNullOrWhiteSpace(species.displayName)
                ? species.displayName
                : species.name;

            RemoveGroup(
                id,
                group.tile,
                eventName: "MIGRATE-DESPAWN",
                reason:
                    $"Species group cap exceeded for {speciesName}. " +
                    $"liveGroups={liveCount}, baseCap={baseCap}, worldMultiplier={_worldSpeciesGroupCapMultiplier:F2}, effectiveCap={cap}. " +
                    $"Oldest group migrated out first. age={group.ageInTurns}/{species.maxAgeInTurns}, size={group.size}, tile={group.tile}");
        }

        _tmpSpeciesCapGroupIds.Clear();
    }

    private int CompareGroupsOldestFirst(int a, int b)
    {
        bool hasA = _groups.TryGetValue(a, out var groupA);
        bool hasB = _groups.TryGetValue(b, out var groupB);

        if (!hasA && !hasB) return a.CompareTo(b);
        if (!hasA) return 1;
        if (!hasB) return -1;

        // Older age first
        int ageCompare = groupB.ageInTurns.CompareTo(groupA.ageInTurns);
        if (ageCompare != 0)
            return ageCompare;

        // Tie-breaker: older id first
        return a.CompareTo(b);
    }

    public IReadOnlyList<int> GetGroupsOnTile(TileCoord tile)
    {
        if (_tileIndex.TryGetValue(tile, out var list))
            return list;

        return Array.Empty<int>();
    }

    public bool TryGetGroup(int id, out AnimalGroupState group)
        => _groups.TryGetValue(id, out group);

    public HuntResult PlayerHuntGroup(int groupId, int huntersPower)
    {
        if (!_groups.TryGetValue(groupId, out var group))
            return HuntResult.Invalid;

        // TODO: implement later
        return HuntResult.Invalid;
    }

    public int GetTotalGroupCount()
    {
        return TotalGroupCount;
    }

    private void RegisterGroupInSpeciesRegistry(int groupId, AnimalDefinition species)
    {
        if (groupId <= 0 || species == null)
            return;

        if (!_liveGroupIdsBySpecies.TryGetValue(species, out var ids))
        {
            ids = new HashSet<int>();
            _liveGroupIdsBySpecies[species] = ids;
        }

        ids.Add(groupId);
        _liveSpeciesByGroupId[groupId] = species;
    }

    private void DeregisterGroupFromSpeciesRegistry(int groupId)
    {
        if (!_liveSpeciesByGroupId.TryGetValue(groupId, out var species) || species == null)
            return;

        if (_liveGroupIdsBySpecies.TryGetValue(species, out var ids))
        {
            ids.Remove(groupId);

            if (ids.Count == 0)
                _liveGroupIdsBySpecies.Remove(species);
        }

        _liveSpeciesByGroupId.Remove(groupId);
    }

    public int GetLiveGroupCountForSpecies(AnimalDefinition species)
    {
        if (species == null)
            return 0;

        if (!_liveGroupIdsBySpecies.TryGetValue(species, out var ids) || ids == null || ids.Count == 0)
            return 0;

        _staleRegistryGroupIds.Clear();

        int liveCount = 0;

        foreach (int id in ids)
        {
            if (_groups.TryGetValue(id, out var group) &&
                group != null &&
                group.isAlive &&
                group.size > 0 &&
                group.species == species)
            {
                liveCount++;
            }
            else
            {
                _staleRegistryGroupIds.Add(id);
            }
        }

        for (int i = 0; i < _staleRegistryGroupIds.Count; i++)
            ids.Remove(_staleRegistryGroupIds[i]);

        if (ids.Count == 0)
            _liveGroupIdsBySpecies.Remove(species);

        _staleRegistryGroupIds.Clear();
        return liveCount;
    }

    public bool IsSpeciesAliveOnMap(AnimalDefinition species)
    {
        return GetLiveGroupCountForSpecies(species) > 0;
    }

    public int GetLiveSpeciesCountOnMap()
    {
        var speciesSnapshot = new List<AnimalDefinition>(_liveGroupIdsBySpecies.Keys);
        int liveSpeciesCount = 0;

        for (int i = 0; i < speciesSnapshot.Count; i++)
        {
            if (GetLiveGroupCountForSpecies(speciesSnapshot[i]) > 0)
                liveSpeciesCount++;
        }

        return liveSpeciesCount;
    }

    public void GetLiveSpeciesOnMap(HashSet<AnimalDefinition> results)
    {
        if (results == null)
            return;

        results.Clear();

        var speciesSnapshot = new List<AnimalDefinition>(_liveGroupIdsBySpecies.Keys);

        for (int i = 0; i < speciesSnapshot.Count; i++)
        {
            var species = speciesSnapshot[i];
            if (GetLiveGroupCountForSpecies(species) > 0)
                results.Add(species);
        }
    }

    public int GetSeasonalTopUpGroupTarget(AnimalDefinition species)
    {
        if (species == null)
            return 0;

        int effectiveCap = GetEffectiveSpeciesGroupCap(species);

        // 0 means uncapped on the definition, but seasonal top-up should not try
        // to fill infinitely, so fall back to 1 when uncapped.
        if (effectiveCap <= 0)
            return 1;

        return Mathf.Max(1, effectiveCap);
    }


    public void GetLiveSpeciesCounts(
        Dictionary<AnimalDefinition, int> groupCounts,
        Dictionary<AnimalDefinition, int> animalCounts)
    {
        if (groupCounts != null)
            groupCounts.Clear();

        if (animalCounts != null)
            animalCounts.Clear();

        foreach (var kvp in _groups)
        {
            var group = kvp.Value;

            if (group.species == null || !group.isAlive || group.size <= 0)
                continue;

            if (groupCounts != null)
            {
                if (!groupCounts.ContainsKey(group.species))
                    groupCounts[group.species] = 0;

                groupCounts[group.species]++;
            }

            if (animalCounts != null)
            {
                if (!animalCounts.ContainsKey(group.species))
                    animalCounts[group.species] = 0;

                animalCounts[group.species] += group.size;
            }
        }
    }

    public int FreeGroupSlotsForSeasonalTopUp(int slotsNeeded)
    {
        if (slotsNeeded <= 0)
            return 0;

        var liveCountsBySpecies = new Dictionary<AnimalDefinition, int>();
        foreach (var kvp in _groups)
        {
            var group = kvp.Value;
            if (group == null || !group.isAlive || group.species == null || group.size <= 0)
                continue;

            if (!liveCountsBySpecies.ContainsKey(group.species))
                liveCountsBySpecies[group.species] = 0;

            liveCountsBySpecies[group.species]++;
        }

        if (liveCountsBySpecies.Count == 0)
            return 0;

        var removableGroupIds = new List<int>();

        foreach (var kvp in _groups)
        {
            var group = kvp.Value;
            if (group == null || !group.isAlive || group.species == null || group.size <= 0)
                continue;

            if (!liveCountsBySpecies.TryGetValue(group.species, out int liveCountForSpecies))
                continue;

            // Preserve at least one live group per currently alive species.
            if (liveCountForSpecies <= 1)
                continue;

            removableGroupIds.Add(kvp.Key);
        }

        removableGroupIds.Sort((a, b) =>
        {
            bool hasA = _groups.TryGetValue(a, out var groupA);
            bool hasB = _groups.TryGetValue(b, out var groupB);

            if (!hasA && !hasB) return a.CompareTo(b);
            if (!hasA) return 1;
            if (!hasB) return -1;

            int countA = liveCountsBySpecies.TryGetValue(groupA.species, out int ca) ? ca : 0;
            int countB = liveCountsBySpecies.TryGetValue(groupB.species, out int cb) ? cb : 0;

            // Remove from the most overrepresented species first.
            int countCompare = countB.CompareTo(countA);
            if (countCompare != 0)
                return countCompare;

            // Then remove oldest first.
            int ageCompare = groupB.ageInTurns.CompareTo(groupA.ageInTurns);
            if (ageCompare != 0)
                return ageCompare;

            return a.CompareTo(b);
        });

        int removed = 0;

        for (int i = 0; i < removableGroupIds.Count && removed < slotsNeeded; i++)
        {
            int id = removableGroupIds[i];

            if (!_groups.TryGetValue(id, out var group) || group == null || !group.isAlive || group.species == null)
                continue;

            if (!liveCountsBySpecies.TryGetValue(group.species, out int liveCountForSpecies))
                continue;

            if (liveCountForSpecies <= 1)
                continue;

            liveCountsBySpecies[group.species] = liveCountForSpecies - 1;

            string speciesName = !string.IsNullOrWhiteSpace(group.species.displayName)
                ? group.species.displayName
                : group.species.name;

            RemoveGroup(
                id,
                group.tile,
                eventName: "SEASONAL-TOPUP-MAKE-ROOM",
                reason:
                    $"Removed surplus live group to make room for seasonal top-up species. " +
                    $"species={speciesName}, speciesLiveGroupsBefore={liveCountForSpecies}, " +
                    $"age={group.ageInTurns}/{group.species.maxAgeInTurns}, size={group.size}, tile={group.tile}");

            removed++;
        }

        return removed;
    }

    public void GetAllGroupsNonAlloc(List<AnimalGroupState> results)
    {
        if (results == null)
            return;

        results.Clear();

        foreach (var kvp in _groups)
        {
            AnimalGroupState group = kvp.Value;
            if (group == null || !group.isAlive || group.size <= 0 || group.species == null)
                continue;

            results.Add(group);
        }
    }
}