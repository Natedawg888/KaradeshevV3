using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class AnimalSimulationController : MonoBehaviour
{
    [SerializeField] private bool debugAnimalSpawning = false;
    [SerializeField] private bool debugAnimalSpawnSummary = true;

    private struct SpawnTileCandidate
    {
        public TileCoord coord;
        public EnvironmentType envType;
        public EnvironmentTileType tileType;
        public List<AnimalDefinition> matchingSpecies;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void DebugLogSpawn(
        string phase,
        AnimalDefinition def,
        TileCoord coord,
        int size,
        EnvironmentType envType,
        EnvironmentTileType tileType,
        int spawnedSoFar,
        int capForPhase,
        int overallLiveCap)
    {
        if (!debugAnimalSpawning)
            return;

        string defName = def != null ? def.name : "NULL";
        string phaseName = string.IsNullOrWhiteSpace(phase) ? "Unknown" : phase;

        Debug.Log(
            $"[AnimalSpawn:{phaseName}] '{defName}' x{size} @ {coord} | " +
            $"env={envType}/{tileType} | phaseCount={spawnedSoFar}/{capForPhase} | liveCap={overallLiveCap}");
    }

    private void DebugLogSupportedSpeciesPool(List<AnimalDefinition> supportedSpecies)
    {
        if (!debugSupportedSpeciesSelection)
            return;

        if (supportedSpecies == null || supportedSpecies.Count == 0)
        {
            Debug.Log("[AnimalSpawn:SupportedPool] No supported species selected.");
            return;
        }

        var parts = new List<string>(supportedSpecies.Count);

        for (int i = 0; i < supportedSpecies.Count; i++)
        {
            var def = supportedSpecies[i];
            if (def == null)
                continue;

            string speciesName = !string.IsNullOrWhiteSpace(def.displayName)
                ? def.displayName
                : def.name;

            parts.Add($"{speciesName} [{def.diet}/{def.sizeCategory}]");
        }

        Debug.Log(
            $"[AnimalSpawn:SupportedPool] Selected {supportedSpecies.Count}/{Mathf.Max(1, maxSupportedSpeciesOnMap)}: " +
            string.Join(", ", parts));
    }
#endif

    private void HandleTurnEnded()
    {
        RefreshPlayerBuildingTiles();
        RefreshHumanUnitGroups();

        _attackingBuildingByGroup.Clear();
        _buildingAttackCounts.Clear();

        _unitAttackCountNext.Clear();
        _animalToUnitTargetNext.Clear();

        _currentTurnBeingTicked = TurnSystem.GetCurrentTurn();

        _simulation.BeginTurnTick(_currentTurnBeingTicked);
        _isTickingTurn = true;
        MarkWorldSimSaveCacheDirty();
    }

    private void HandleTilesActivated()
    {
        BuildTileUiLookup();

        var presetManager = EnvironmentPresetManager.Instance;
        if (presetManager == null)
            return;

        var currentPreset = presetManager.GetCurrentPreset();
        if (currentPreset == null)
            return;

        _hasCompletedInitialAnimalSpawn = false;

        if (_spawnRoutine != null)
            StopCoroutine(_spawnRoutine);

        _spawnRoutine = StartCoroutine(SpawnInitialAnimalsFromPreset(currentPreset));
    }

    private Coroutine _markerRebuildRoutine;

    private void RequestDeferredMarkerRebuild()
    {
        if (_markerRebuildRoutine != null)
            StopCoroutine(_markerRebuildRoutine);

        _markerRebuildRoutine = StartCoroutine(DeferredMarkerRebuildCoroutine());
    }

    private IEnumerator DeferredMarkerRebuildCoroutine()
    {
        const int maxFrames = 60;

        for (int frame = 0; frame < maxFrames; frame++)
        {
            BuildTileUiLookup();

            bool anyReady = false;

            foreach (var kvp in _tileUIs)
            {
                TileAnimalUI tileUI = kvp.Value;
                if (tileUI == null)
                    continue;

                tileUI.ResolveNow();

                if (tileUI.ContentRoot != null)
                {
                    anyReady = true;
                    break;
                }
            }

            if (anyReady)
                break;

            yield return null;
        }

        yield return null;

        RebuildAnimalMarkersFromLoadedState();
        _markerRebuildRoutine = null;
    }

    private IEnumerator SpawnInitialAnimalsFromPreset(EnvironmentPreset preset)
    {
        if (_simulation == null || preset == null || preset.animalsForThisPreset == null || preset.animalsForThisPreset.Count == 0)
        {
            _spawnRoutine = null;
            yield break;
        }

        if (_simulation != null)
            _simulation.ClearPendingSpawns();

        var allSpecies = new List<AnimalDefinition>();

        for (int i = 0; i < preset.animalsForThisPreset.Count; i++)
        {
            var def = preset.animalsForThisPreset[i];
            if (def == null)
                continue;

            if (!allSpecies.Contains(def))
                allSpecies.Add(def);
        }

        if (allSpecies.Count == 0)
        {
            _spawnRoutine = null;
            yield break;
        }

        // NEW: choose the subset this map will support.
        var supportedSpecies = BuildSupportedSpeciesPool(allSpecies);

        if (supportedSpecies.Count == 0)
        {
            _spawnRoutine = null;
            yield break;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    DebugLogSupportedSpeciesPool(supportedSpecies);
#endif

        bool hasCarnivores = false;
        bool hasNonCarnivores = false;

        for (int i = 0; i < supportedSpecies.Count; i++)
        {
            var def = supportedSpecies[i];
            if (def == null)
                continue;

            if (def.diet == AnimalDiet.Carnivore)
                hasCarnivores = true;
            else
                hasNonCarnivores = true;
        }

        var envSource = envDataSource as MonoEnvironmentDataSource;
        if (envSource == null)
        {
            _spawnRoutine = null;
            yield break;
        }

        var uniqueTiles = BuildUniqueSpawnTileSnapshot(envSource);
        ShuffleInPlace(uniqueTiles);

        int initialSpawnCap = Mathf.Clamp(maxInitialSpawnGroups, 0, maxTotalGroups);
        int groupsSpawned = 0;
        int processedThisFrame = 0;

        int minGroupsPerTile = Mathf.Max(1, initialGroupsPerTileRange.x);
        int maxGroupsPerTile = Mathf.Max(minGroupsPerTile, initialGroupsPerTileRange.y);

        for (int i = 0; i < uniqueTiles.Count; i++)
        {
            if (groupsSpawned >= initialSpawnCap || _simulation.GetTotalGroupCount() >= maxTotalGroups)
                break;

            if (UnityEngine.Random.value > initialSpawnChancePerTile)
                continue;

            var kvp = uniqueTiles[i];
            var coord = kvp.Key;
            var env = kvp.Value;

            if (env == null)
                continue;

            // IMPORTANT: only match against the chosen subset, not the full preset list.
            var matchingSpecies = GetStrictSpawnMatches(supportedSpecies, env.environmentType, env.environmentTileType);
            if (matchingSpecies.Count == 0)
                continue;

            int groupsForThisTile = UnityEngine.Random.Range(minGroupsPerTile, maxGroupsPerTile + 1);

            for (int g = 0; g < groupsForThisTile; g++)
            {
                if (groupsSpawned >= initialSpawnCap || _simulation.GetTotalGroupCount() >= maxTotalGroups)
                    break;

                bool preferCarnivore =
                    hasCarnivores &&
                    (!hasNonCarnivores || UnityEngine.Random.value < initialCarnivoreChanceRelativeToHerbivores);

                AnimalDefinition chosen = PickInitialSpeciesSimple(matchingSpecies, preferCarnivore);
                if (chosen == null)
                    break;

                int minSize = Mathf.Max(1, chosen.minGroupSize);
                int maxSize = Mathf.Max(minSize, chosen.maxGroupSize);
                int size = UnityEngine.Random.Range(minSize, maxSize + 1);

                int startingAgeTurns = GetInitialSpawnAgeTurns(chosen);
                _simulation.SpawnGroup(chosen, coord, size, startingAgeTurns);

                groupsSpawned++;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                DebugLogSpawn(
                    "Initial",
                    chosen,
                    coord,
                    size,
                    env.environmentType,
                    env.environmentTileType,
                    groupsSpawned,
                    initialSpawnCap,
                    maxTotalGroups);
#endif
            }

            processedThisFrame++;
            if (processedThisFrame >= tilesProcessedPerFrame)
            {
                processedThisFrame = 0;
                yield return null;
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    if (debugAnimalSpawnSummary)
    {
        Debug.Log(
            $"[AnimalSpawn:Initial] Finished. " +
            $"SupportedSpecies={supportedSpecies.Count}/{allSpecies.Count}, " +
            $"InitialSpawned={groupsSpawned}/{initialSpawnCap}, " +
            $"CandidateTiles={uniqueTiles.Count}, " +
            $"LiveGroupsNow={_simulation.GetTotalGroupCount()}/{maxTotalGroups}");
    }
#endif
        _hasCompletedInitialAnimalSpawn = true;
        _spawnRoutine = null;
    }

    private AnimalDefinition PickInitialSpeciesSimple(List<AnimalDefinition> pool, bool preferCarnivore)
    {
        if (pool == null || pool.Count == 0)
            return null;

        var carnivores = new List<AnimalDefinition>();
        var nonCarnivores = new List<AnimalDefinition>();

        for (int i = 0; i < pool.Count; i++)
        {
            var def = pool[i];
            if (def == null)
                continue;

            if (def.diet == AnimalDiet.Carnivore)
                carnivores.Add(def);
            else
                nonCarnivores.Add(def);
        }

        List<AnimalDefinition> chosenPool = null;

        if (preferCarnivore && carnivores.Count > 0)
            chosenPool = carnivores;
        else if (!preferCarnivore && nonCarnivores.Count > 0)
            chosenPool = nonCarnivores;
        else if (nonCarnivores.Count > 0)
            chosenPool = nonCarnivores;
        else if (carnivores.Count > 0)
            chosenPool = carnivores;

        if (chosenPool == null || chosenPool.Count == 0)
            return null;

        return chosenPool[UnityEngine.Random.Range(0, chosenPool.Count)];
    }

    private List<KeyValuePair<TileCoord, EnvironmentControl>> BuildUniqueSpawnTileSnapshot(MonoEnvironmentDataSource envSource)
    {
        var tilesSnapshot = new List<KeyValuePair<TileCoord, EnvironmentControl>>(envSource.AllTiles);
        var uniqueTiles = new List<KeyValuePair<TileCoord, EnvironmentControl>>(tilesSnapshot.Count);
        var processedEnvironmentTiles = new HashSet<EnvironmentControl>();

        for (int i = 0; i < tilesSnapshot.Count; i++)
        {
            var kvp = tilesSnapshot[i];
            if (kvp.Value == null)
                continue;

            if (!processedEnvironmentTiles.Add(kvp.Value))
                continue;

            uniqueTiles.Add(kvp);
        }

        return uniqueTiles;
    }

    private List<AnimalDefinition> GetStrictSpawnMatches(
        List<AnimalDefinition> source,
        EnvironmentType envType,
        EnvironmentTileType tileType)
    {
        var results = new List<AnimalDefinition>();

        if (source == null || source.Count == 0)
            return results;

        for (int i = 0; i < source.Count; i++)
        {
            var def = source[i];
            if (MatchesSpawnHabitatStrict(def, envType, tileType))
                results.Add(def);
        }

        return results;
    }

    private bool MatchesSpawnHabitatStrict(
        AnimalDefinition def,
        EnvironmentType envType,
        EnvironmentTileType tileType)
    {
        if (def == null)
            return false;

        if (ContainsEnvironment(def.avoidedEnvironments, envType))
            return false;

        if (ContainsTileType(def.avoidedTileTypes, tileType))
            return false;

        if (def.preferredEnvironments != null &&
            def.preferredEnvironments.Length > 0 &&
            !ContainsEnvironment(def.preferredEnvironments, envType))
        {
            return false;
        }

        if (def.preferredTileTypes != null &&
            def.preferredTileTypes.Length > 0 &&
            !ContainsTileType(def.preferredTileTypes, tileType))
        {
            return false;
        }

        return true;
    }

    private bool ContainsEnvironment(EnvironmentType[] array, EnvironmentType value)
    {
        if (array == null || array.Length == 0)
            return false;

        for (int i = 0; i < array.Length; i++)
        {
            if (array[i] == value)
                return true;
        }

        return false;
    }

    private bool ContainsTileType(EnvironmentTileType[] array, EnvironmentTileType value)
    {
        if (array == null || array.Length == 0)
            return false;

        for (int i = 0; i < array.Length; i++)
        {
            if (array[i] == value)
                return true;
        }

        return false;
    }

    private void ShuffleInPlace<T>(IList<T> list)
    {
        if (list == null || list.Count <= 1)
            return;

        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }

    private IEnumerator ProcessReproductionSpawns()
    {
        while (_simulation != null && _simulation.HasPendingSpawns)
        {
            _simulation.ProcessPendingSpawns(newGroupsPerFrame);
            yield return null;
        }

        _reproductionSpawnRoutine = null;
    }

    private int GetInitialSpawnAgeTurns(AnimalDefinition species)
    {
        if (species == null)
            return 0;

        int maxAge = Mathf.Max(0, species.maxAgeInTurns);
        if (maxAge <= 1)
            return 0;

        int softMax = Mathf.Max(1, Mathf.FloorToInt(maxAge * 0.85f));
        return UnityEngine.Random.Range(0, softMax);
    }

    private struct SpeciesBucketKey : IEquatable<SpeciesBucketKey>
    {
        public AnimalDiet diet;
        public AnimalSizeCategory sizeCategory;

        public SpeciesBucketKey(AnimalDiet diet, AnimalSizeCategory sizeCategory)
        {
            this.diet = diet;
            this.sizeCategory = sizeCategory;
        }

        public bool Equals(SpeciesBucketKey other)
            => diet == other.diet && sizeCategory == other.sizeCategory;

        public override bool Equals(object obj)
            => obj is SpeciesBucketKey other && Equals(other);

        public override int GetHashCode()
            => ((int)diet * 397) ^ (int)sizeCategory;

        public override string ToString()
            => $"{diet}/{sizeCategory}";
    }

    private List<AnimalDefinition> BuildSupportedSpeciesPool(List<AnimalDefinition> source)
    {
        var selected = new List<AnimalDefinition>();

        if (source == null || source.Count == 0)
            return selected;

        int totalCap = Mathf.Max(1, maxSupportedSpeciesOnMap);
        int perBucketCap = Mathf.Max(1, maxSpeciesPerDietSizeBucket);

        var buckets = new Dictionary<SpeciesBucketKey, List<AnimalDefinition>>();
        var bucketOrder = new List<SpeciesBucketKey>();

        for (int i = 0; i < source.Count; i++)
        {
            var def = source[i];
            if (def == null)
                continue;

            var key = new SpeciesBucketKey(def.diet, def.sizeCategory);

            if (!buckets.TryGetValue(key, out var list))
            {
                list = new List<AnimalDefinition>();
                buckets[key] = list;
                bucketOrder.Add(key);
            }

            if (!list.Contains(def))
                list.Add(def);
        }

        if (bucketOrder.Count == 0)
            return selected;

        for (int i = 0; i < bucketOrder.Count; i++)
        {
            var key = bucketOrder[i];
            ShuffleInPlace(buckets[key]);
        }

        ShuffleInPlace(bucketOrder);

        var takenPerBucket = new Dictionary<SpeciesBucketKey, int>(bucketOrder.Count);

        while (selected.Count < totalCap)
        {
            bool addedAnyThisPass = false;

            for (int i = 0; i < bucketOrder.Count; i++)
            {
                var key = bucketOrder[i];
                var list = buckets[key];

                int alreadyTaken = takenPerBucket.TryGetValue(key, out var count) ? count : 0;
                if (alreadyTaken >= perBucketCap)
                    continue;

                if (alreadyTaken >= list.Count)
                    continue;

                selected.Add(list[alreadyTaken]);
                takenPerBucket[key] = alreadyTaken + 1;
                addedAnyThisPass = true;

                if (selected.Count >= totalCap)
                    break;
            }

            if (!addedAnyThisPass)
                break;
        }

        return selected;
    }

    private Vector2Int GetSeasonalTopUpGroupRangeForBucket(AnimalDefinition species)
    {
        if (species == null)
            return new Vector2Int(1, 1);

        return (species.diet, species.sizeCategory) switch
        {
            (AnimalDiet.Herbivore, AnimalSizeCategory.Small) => new Vector2Int(2, 4),
            (AnimalDiet.Herbivore, AnimalSizeCategory.Medium) => new Vector2Int(2, 3),
            (AnimalDiet.Herbivore, AnimalSizeCategory.Large) => new Vector2Int(1, 2),
            (AnimalDiet.Herbivore, AnimalSizeCategory.Giant) => new Vector2Int(1, 1),

            (AnimalDiet.Omnivore, AnimalSizeCategory.Small) => new Vector2Int(2, 3),
            (AnimalDiet.Omnivore, AnimalSizeCategory.Medium) => new Vector2Int(1, 2),
            (AnimalDiet.Omnivore, AnimalSizeCategory.Large) => new Vector2Int(1, 2),
            (AnimalDiet.Omnivore, AnimalSizeCategory.Giant) => new Vector2Int(1, 1),

            (AnimalDiet.Carnivore, AnimalSizeCategory.Small) => new Vector2Int(1, 2),
            (AnimalDiet.Carnivore, AnimalSizeCategory.Medium) => new Vector2Int(1, 2),
            (AnimalDiet.Carnivore, AnimalSizeCategory.Large) => new Vector2Int(1, 1),
            (AnimalDiet.Carnivore, AnimalSizeCategory.Giant) => new Vector2Int(1, 1),

            _ => new Vector2Int(1, 1)
        };
    }

    private IEnumerator SeasonalTopUpMissingSpeciesFromPreset()
    {
        if (_simulation == null)
        {
            _seasonalTopUpRoutine = null;
            yield break;
        }

        var presetManager = EnvironmentPresetManager.Instance;
        if (presetManager == null)
        {
            _seasonalTopUpRoutine = null;
            yield break;
        }

        var preset = presetManager.GetCurrentPreset();
        if (preset == null || preset.animalsForThisPreset == null || preset.animalsForThisPreset.Count == 0)
        {
            _seasonalTopUpRoutine = null;
            yield break;
        }

        var envSource = envDataSource as MonoEnvironmentDataSource;
        if (envSource == null)
        {
            _seasonalTopUpRoutine = null;
            yield break;
        }

        var allPresetSpecies = new List<AnimalDefinition>();
        for (int i = 0; i < preset.animalsForThisPreset.Count; i++)
        {
            var def = preset.animalsForThisPreset[i];
            if (def == null)
                continue;

            if (!allPresetSpecies.Contains(def))
                allPresetSpecies.Add(def);
        }

        if (allPresetSpecies.Count == 0)
        {
            _seasonalTopUpRoutine = null;
            yield break;
        }

        int targetVariety = Mathf.Min(maxSupportedSpeciesOnMap, allPresetSpecies.Count);
        if (targetVariety <= 0)
        {
            _seasonalTopUpRoutine = null;
            yield break;
        }

        var liveGroupCounts = new Dictionary<AnimalDefinition, int>();
        _simulation.GetLiveSpeciesCounts(liveGroupCounts, null);

        var liveSpecies = new HashSet<AnimalDefinition>();
        foreach (var kvp in liveGroupCounts)
        {
            if (kvp.Key != null && kvp.Value > 0)
                liveSpecies.Add(kvp.Key);
        }

        int liveSpeciesCount = liveSpecies.Count;
        int varietyGap = targetVariety - liveSpeciesCount;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (debugAnimalSpawnSummary)
        {
            Debug.Log(
                $"[AnimalSpawn:SeasonTopUp] Start. " +
                $"LiveSpecies={liveSpeciesCount}, " +
                $"TargetVariety={targetVariety}, " +
                $"VarietyGap={varietyGap}, " +
                $"LiveGroups={_simulation.GetTotalGroupCount()}/{maxTotalGroups}");
        }
#endif

        if (varietyGap <= 0)
        {
            _seasonalTopUpRoutine = null;
            yield break;
        }

        var uniqueTiles = BuildUniqueSpawnTileSnapshot(envSource);
        ShuffleInPlace(uniqueTiles);

        var candidateSpecies = new List<AnimalDefinition>();
        for (int i = 0; i < allPresetSpecies.Count; i++)
        {
            var def = allPresetSpecies[i];
            if (def == null)
                continue;

            if (liveSpecies.Contains(def))
                continue;

            if (!HasAnyValidSpawnTile(def, uniqueTiles))
                continue;

            candidateSpecies.Add(def);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (debugAnimalSpawnSummary)
        {
            Debug.Log(
                $"[AnimalSpawn:SeasonTopUp] Candidate missing species after filtering alive/invalid = {candidateSpecies.Count}");
        }
#endif

        if (candidateSpecies.Count == 0)
        {
            _seasonalTopUpRoutine = null;
            yield break;
        }

        var supportedTopUpPool = BuildSupportedSpeciesPool(candidateSpecies);
        if (supportedTopUpPool.Count == 0)
        {
            _seasonalTopUpRoutine = null;
            yield break;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DebugLogSupportedSpeciesPool(supportedTopUpPool);
#endif

        int speciesToRecover = Mathf.Min(varietyGap, supportedTopUpPool.Count);
        var selectedMissingSpecies = new List<AnimalDefinition>(speciesToRecover);

        for (int i = 0; i < speciesToRecover; i++)
            selectedMissingSpecies.Add(supportedTopUpPool[i]);

        var remainingGroupsBySpecies = new Dictionary<AnimalDefinition, int>();
        var seasonalTargetGroupsBySpecies = new Dictionary<AnimalDefinition, int>();
        bool hasCarnivores = false;
        bool hasNonCarnivores = false;

        for (int i = 0; i < selectedMissingSpecies.Count; i++)
        {
            var species = selectedMissingSpecies[i];
            if (species == null)
                continue;

            int maxGroupsForSpecies = Mathf.Max(1, _simulation.GetSeasonalTopUpGroupTarget(species));

            // Random total groups for this seasonal top-up:
            // 1 means only the guaranteed intro group
            // maxGroupsForSpecies means fill all the way to the species seasonal cap
            int seasonalTargetGroups = UnityEngine.Random.Range(1, maxGroupsForSpecies + 1);

            seasonalTargetGroupsBySpecies[species] = seasonalTargetGroups;
            remainingGroupsBySpecies[species] = seasonalTargetGroups;

            if (species.diet == AnimalDiet.Carnivore)
                hasCarnivores = true;
            else
                hasNonCarnivores = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    if (debugAnimalSpawning)
    {
        Debug.Log(
            $"[AnimalSpawn:SeasonTopUp-Target] '{species.name}' seasonalTarget={seasonalTargetGroups} maxAllowed={maxGroupsForSpecies}");
    }
#endif
        }

        int spawnedSpecies = 0;
        int spawnedGroups = 0;
        int processedThisFrame = 0;

        int minGroupsPerTile = Mathf.Max(1, initialGroupsPerTileRange.x);
        int maxGroupsPerTile = Mathf.Max(minGroupsPerTile, initialGroupsPerTileRange.y);

        // Phase 1: guarantee one live group for each selected missing species.
        for (int i = 0; i < selectedMissingSpecies.Count; i++)
        {
            if (_simulation.GetTotalGroupCount() >= maxTotalGroups)
                break;

            var species = selectedMissingSpecies[i];
            if (species == null)
                continue;

            bool spawnedGuaranteed = TrySpawnGuaranteedMissingSpeciesGroup(species, uniqueTiles);
            if (!spawnedGuaranteed)
                continue;

            spawnedSpecies++;
            spawnedGroups++;
            liveGroupCounts[species] = 1;

            if (remainingGroupsBySpecies.TryGetValue(species, out int remaining))
                remainingGroupsBySpecies[species] = Mathf.Max(0, remaining - 1);

            processedThisFrame++;
            if (processedThisFrame >= newGroupsPerFrame)
            {
                processedThisFrame = 0;
                yield return null;
            }
        }

        // Phase 2: add the remaining requested groups for those reintroduced species.
        for (int i = 0; i < uniqueTiles.Count; i++)
        {
            if (_simulation.GetTotalGroupCount() >= maxTotalGroups)
                break;

            if (!HasRemainingSeasonalGroups(remainingGroupsBySpecies))
                break;

            if (UnityEngine.Random.value > initialSpawnChancePerTile)
                continue;

            var kvp = uniqueTiles[i];
            var coord = kvp.Key;
            var env = kvp.Value;

            if (env == null)
                continue;

            var matchingSpecies = GetStrictSpawnMatches(selectedMissingSpecies, env.environmentType, env.environmentTileType);
            if (matchingSpecies.Count == 0)
                continue;

            int groupsForThisTile = UnityEngine.Random.Range(minGroupsPerTile, maxGroupsPerTile + 1);

            for (int g = 0; g < groupsForThisTile; g++)
            {
                if (_simulation.GetTotalGroupCount() >= maxTotalGroups)
                    break;

                bool preferCarnivore =
                    hasCarnivores &&
                    (!hasNonCarnivores || UnityEngine.Random.value < initialCarnivoreChanceRelativeToHerbivores);

                AnimalDefinition chosen = PickSeasonalSpeciesWithRemainingGroups(
    matchingSpecies,
    remainingGroupsBySpecies,
    preferCarnivore);

                if (chosen == null)
                    break;

                int targetGroupsForChosen = seasonalTargetGroupsBySpecies.TryGetValue(chosen, out int target)
                    ? Mathf.Max(1, target)
                    : 1;

                if (_simulation.GetLiveGroupCountForSpecies(chosen) >= targetGroupsForChosen)
                {
                    remainingGroupsBySpecies[chosen] = 0;
                    continue;
                }

                int minSize = Mathf.Max(1, chosen.minGroupSize);
                int maxSize = Mathf.Max(minSize, chosen.maxGroupSize);
                int size = UnityEngine.Random.Range(minSize, maxSize + 1);
                int startingAgeTurns = GetInitialSpawnAgeTurns(chosen);

                int beforeCount = _simulation.GetTotalGroupCount();
                _simulation.SpawnGroup(chosen, coord, size, startingAgeTurns);
                int afterCount = _simulation.GetTotalGroupCount();

                if (afterCount <= beforeCount)
                    continue;

                spawnedGroups++;

                if (remainingGroupsBySpecies.TryGetValue(chosen, out int remaining))
                    remainingGroupsBySpecies[chosen] = Mathf.Max(0, remaining - 1);


#if UNITY_EDITOR || DEVELOPMENT_BUILD
                DebugLogSpawn(
                    "SeasonTopUp",
                    chosen,
                    coord,
                    size,
                    env.environmentType,
                    env.environmentTileType,
                    spawnedGroups,
                    selectedMissingSpecies.Count,
                    maxTotalGroups);
#endif
            }

            processedThisFrame++;
            if (processedThisFrame >= tilesProcessedPerFrame)
            {
                processedThisFrame = 0;
                yield return null;
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (debugAnimalSpawnSummary)
        {
            Debug.Log(
                $"[AnimalSpawn:SeasonTopUp] Finished. " +
                $"SelectedMissingSpecies={selectedMissingSpecies.Count}, " +
                $"SpawnedMissingSpecies={spawnedSpecies}, " +
                $"SpawnedGroups={spawnedGroups}, " +
                $"LiveSpeciesNow={liveSpeciesCount + spawnedSpecies}/{targetVariety}, " +
                $"LiveGroupsNow={_simulation.GetTotalGroupCount()}/{maxTotalGroups}");
        }
#endif

        _seasonalTopUpRoutine = null;
    }

    private bool HasRemainingSeasonalGroups(Dictionary<AnimalDefinition, int> remainingGroupsBySpecies)
    {
        if (remainingGroupsBySpecies == null || remainingGroupsBySpecies.Count == 0)
            return false;

        foreach (var kvp in remainingGroupsBySpecies)
        {
            if (kvp.Key != null && kvp.Value > 0)
                return true;
        }

        return false;
    }

    private AnimalDefinition PickSeasonalSpeciesWithRemainingGroups(
    List<AnimalDefinition> pool,
    Dictionary<AnimalDefinition, int> remainingGroupsBySpecies,
    bool preferCarnivore)
    {
        if (pool == null || pool.Count == 0 || remainingGroupsBySpecies == null)
            return null;

        var carnivores = new List<AnimalDefinition>();
        var nonCarnivores = new List<AnimalDefinition>();

        for (int i = 0; i < pool.Count; i++)
        {
            var def = pool[i];
            if (def == null)
                continue;

            if (!remainingGroupsBySpecies.TryGetValue(def, out int remaining) || remaining <= 0)
                continue;

            if (def.diet == AnimalDiet.Carnivore)
                carnivores.Add(def);
            else
                nonCarnivores.Add(def);
        }

        List<AnimalDefinition> chosenPool = null;

        if (preferCarnivore && carnivores.Count > 0)
            chosenPool = carnivores;
        else if (!preferCarnivore && nonCarnivores.Count > 0)
            chosenPool = nonCarnivores;
        else if (nonCarnivores.Count > 0)
            chosenPool = nonCarnivores;
        else if (carnivores.Count > 0)
            chosenPool = carnivores;

        if (chosenPool == null || chosenPool.Count == 0)
            return null;

        return chosenPool[UnityEngine.Random.Range(0, chosenPool.Count)];
    }

    private bool HasAnyValidSpawnTile(
    AnimalDefinition species,
    List<KeyValuePair<TileCoord, EnvironmentControl>> uniqueTiles)
    {
        if (species == null || uniqueTiles == null || uniqueTiles.Count == 0)
            return false;

        for (int i = 0; i < uniqueTiles.Count; i++)
        {
            var kvp = uniqueTiles[i];
            var env = kvp.Value;

            if (env == null)
                continue;

            if (MatchesSpawnHabitatStrict(species, env.environmentType, env.environmentTileType))
                return true;
        }

        return false;
    }

    private bool TrySpawnGuaranteedMissingSpeciesGroup(
    AnimalDefinition species,
    List<KeyValuePair<TileCoord, EnvironmentControl>> uniqueTiles)
    {
        if (_simulation == null || species == null || uniqueTiles == null || uniqueTiles.Count == 0)
            return false;

        var validTiles = new List<KeyValuePair<TileCoord, EnvironmentControl>>();

        for (int i = 0; i < uniqueTiles.Count; i++)
        {
            var kvp = uniqueTiles[i];
            var env = kvp.Value;

            if (env == null)
                continue;

            if (MatchesSpawnHabitatStrict(species, env.environmentType, env.environmentTileType))
                validTiles.Add(kvp);
        }

        if (validTiles.Count == 0)
            return false;

        ShuffleInPlace(validTiles);

        for (int i = 0; i < validTiles.Count; i++)
        {
            if (_simulation.GetTotalGroupCount() >= maxTotalGroups)
                return false;

            var chosenTile = validTiles[i];

            int minSize = Mathf.Max(1, species.minGroupSize);
            int maxSize = Mathf.Max(minSize, species.maxGroupSize);
            int size = UnityEngine.Random.Range(minSize, maxSize + 1);

            int startingAgeTurns = GetInitialSpawnAgeTurns(species);

            int beforeCount = _simulation.GetTotalGroupCount();
            _simulation.SpawnGroup(species, chosenTile.Key, size, startingAgeTurns);
            int afterCount = _simulation.GetTotalGroupCount();

            if (afterCount > beforeCount)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (debugAnimalSpawning)
            {
                Debug.Log(
                    $"[AnimalSpawn:SeasonTopUp-Guaranteed] '{species.name}' x{size} @ {chosenTile.Key} | " +
                    $"env={chosenTile.Value.environmentType}/{chosenTile.Value.environmentTileType}");
            }
#endif
                return true;
            }
        }

        return false;
    }
}