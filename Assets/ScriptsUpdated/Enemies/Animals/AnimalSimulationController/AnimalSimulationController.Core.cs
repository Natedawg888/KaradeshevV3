using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class AnimalSimulationController : MonoBehaviour
{
    [Header("Simulation")]
    [SerializeField] private TurnSystem turnSystem;
    [SerializeField] private MonoEnvironmentDataSource envDataSource;
    [SerializeField] private TileActivator tileActivator;

    [Header("Tile UI")]
    [SerializeField] private AnimalGroupMarkerView markerPrefab;

    [Header("Initial World Spawn")]
    private float initialSpawnChancePerTile => CurrentAnimalSpawnSettings.initialSpawnChancePerTile;
    private float initialCarnivoreChanceRelativeToHerbivores => CurrentAnimalSpawnSettings.initialCarnivoreChanceRelativeToHerbivores;
    private int maxInitialSpawnGroups => CurrentAnimalSpawnSettings.maxInitialSpawnGroups;
    private Vector2Int initialGroupsPerTileRange => CurrentAnimalSpawnSettings.initialGroupsPerTileRange;

    private int maxSupportedSpeciesOnMap => CurrentAnimalSpawnSettings.maxSupportedSpeciesOnMap;
    private int maxSpeciesPerDietSizeBucket => CurrentAnimalSpawnSettings.maxSpeciesPerDietSizeBucket;
    [SerializeField] private bool debugSupportedSpeciesSelection = true;

    private float worldSpeciesGroupCapMultiplier => CurrentAnimalSpawnSettings.worldSpeciesGroupCapMultiplier;
    private int maxTotalGroups => CurrentAnimalSpawnSettings.maxTotalGroups;

    [Header("Batching")]
    [SerializeField] private int tilesProcessedPerFrame = 16;
    [SerializeField] private int groupsTickedPerFrame = 32;
    [SerializeField] private int newGroupsPerFrame = 8;

    [Header("Human Hunter Rules")]
    [SerializeField] private bool humanHuntersAvoidPlayerBuildingsAtStart = true;

    [Header("Human Hunter Damage")]
    [SerializeField] private float baseDamagePerAnimal = 0.5f;

    [SerializeField] private int minBuildingDamagePerAttack = 2;
    [SerializeField] private int maxBuildingDamagePerAttack = 12;

    [SerializeField] private float animalVsUnitDefenseMitigationPerPoint = 0.15f;
    [SerializeField] private float minAnimalDamageMultAfterDefense = 0.25f;
    [SerializeField] private int minUnitDamagePerAttack = 1;
    [SerializeField] private int maxUnitDamagePerAttack = 25;

    [Header("Player Targeting (Animal Markers)")]
    [SerializeField] private bool refreshPlayerAnimalTargetIconsRealtime = true;

    [SerializeField, Min(0.05f)] private float playerAnimalTargetRefreshInterval = 0.15f;
    [SerializeField, Min(0.25f)] private float unitControlsCacheRescanInterval = 1.0f;

    [Header("Animal vs Unit Hit Chance")]
    [SerializeField, Range(0f, 1f)] private float baseAnimalHitChance = 0.85f;
    [SerializeField, Range(0f, 1f)] private float minAnimalHitChance = 0.10f;
    [SerializeField, Range(0f, 1f)] private float maxAnimalHitChance = 0.98f;

    [SerializeField] private float animalSpeedHitBonus = 0.20f;
    [SerializeField] private float unitAgilityAvoidPerPoint = 0.03f;
    [SerializeField] private float unitSkillAvoidPerLevel = 0.02f;

    private float _nextPlayerTargetRefreshAt;
    private float _nextUnitControlsRescanAt;

    private readonly List<TileUnitGroupControl> _unitControlsCache = new(128);
    private readonly HashSet<int> _playerTargetedAnimalIds = new();

    private readonly Dictionary<TileCoord, BuildingControl> _buildingByTile = new();
    private GridManager _grid;

    private readonly Dictionary<int, AnimalGroupMarkerView> _markerViews = new();
    private readonly Dictionary<TileCoord, TileAnimalUI> _tileUIs = new();

    private readonly Dictionary<string, (TileUnitGroupControl owner, TileUnitGroupData group)> _humanUnitsById = new();

    private readonly List<AnimalSimulation.HumanUnitGroupInfo> _humanInfosBuffer =
        new List<AnimalSimulation.HumanUnitGroupInfo>(128);

    public AnimalSimulation Simulation => _simulation;

    private bool _isTickingTurn = false;
    private int _currentTurnBeingTicked = -1;

    private Coroutine _spawnRoutine;
    private Coroutine _reproductionSpawnRoutine;
    private Coroutine _seasonalTopUpRoutine;
    private AnimalSimulation _simulation;

    private readonly Dictionary<TileCoord, BuildingUnderAttackIconView[]> _attackIconsByTile = new();
    private readonly Dictionary<int, TileCoord> _attackingBuildingByGroup = new();
    private readonly Dictionary<TileCoord, int> _buildingAttackCounts = new();
    private readonly List<AnimalGroupState> _loadedGroupsBuffer = new(256);
    private readonly List<int> _tmpIntBuffer = new(32);

    private bool _worldSimSaveCacheDirty = true;

    private bool _hasCompletedInitialAnimalSpawn = false;

    private readonly Dictionary<string, UnitGroupMarker> _unitMarkerByGroupId =
        new Dictionary<string, UnitGroupMarker>(System.StringComparer.Ordinal);

    private bool _unitMarkerCacheDirty = true;

    private readonly Dictionary<string, int> _unitAttackCountCurrent =
        new Dictionary<string, int>(System.StringComparer.Ordinal);

    private readonly Dictionary<string, int> _unitAttackCountNext =
        new Dictionary<string, int>(System.StringComparer.Ordinal);

    private readonly Dictionary<int, string> _animalToUnitTargetNext = new Dictionary<int, string>();

    private static T[] FindAllFast<T>(bool includeInactive = false) where T : UnityEngine.Object
    {
#if UNITY_2022_2_OR_NEWER
        return UnityEngine.Object.FindObjectsByType<T>(
            includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
#else
        return UnityEngine.Object.FindObjectsOfType<T>(includeInactive);
#endif
    }

    private void Awake()
    {
        if (envDataSource == null)
            envDataSource = FindObjectOfType<MonoEnvironmentDataSource>();

        if (tileActivator == null)
            tileActivator = FindObjectOfType<TileActivator>();

        if (_grid == null)
            _grid = FindObjectOfType<GridManager>();

        _simulation = new AnimalSimulation(envDataSource);

        // Overall live population cap for initial spawn + reproduction.
        _simulation.MaxTotalGroups = maxTotalGroups;
        ApplyWorldSpeciesGroupCapMultiplier();

        TurnSystem.SubscribeToEndOfTurn(HandleTurnEnded);

        if (SeasonManager.Instance != null)
            SeasonManager.Instance.OnSeasonChanged += HandleSeasonChanged;

        _simulation.OnGroupCreated += HandleGroupCreated;
        _simulation.OnGroupUpdated += HandleGroupUpdated;
        _simulation.OnGroupRemoved += HandleGroupRemoved;

        _simulation.HumanHuntersAvoidPlayerBuildings = humanHuntersAvoidPlayerBuildingsAtStart;
        _simulation.OnGroupAttackedPlayerTile += HandleGroupAttackedPlayerTile;
        _simulation.OnGroupAttackedPlayerUnitGroup += HandleGroupAttackedPlayerUnitGroup;
        _simulation.OnGroupAttemptedStorageRaid += HandleGroupAttemptedStorageRaid;

        _simulation.DebugHumanTargeting = true;
        _simulation.DebugHumanStepping = true;

        AnimalSimulationAccess.Current = _simulation;

        FinalSetupInstaller installer = FindFirstObjectByType<FinalSetupInstaller>(FindObjectsInactive.Include);
        if (installer != null)
            installer.RegisterAnimalSimulationController(this);

        if (tileActivator != null)
            tileActivator.OnTilesActivated += HandleTilesActivated;

        AnimalRepellerRegistry.OnChanged += RefreshRepelledTiles;
        RefreshRepelledTiles();

        BuildTileUiLookup();
    }

    private void SubscribeSimulationEvents()
    {
        if (_simulation == null)
            return;

        _simulation.OnGroupCreated += HandleGroupCreated;
        _simulation.OnGroupUpdated += HandleGroupUpdated;
        _simulation.OnGroupRemoved += HandleGroupRemoved;
        _simulation.OnSimulationStateChanged += HandleSimulationStateChanged;
    }

    private void UnsubscribeSimulationEvents()
    {
        if (_simulation == null)
            return;

        _simulation.OnGroupCreated -= HandleGroupCreated;
        _simulation.OnGroupUpdated -= HandleGroupUpdated;
        _simulation.OnGroupRemoved -= HandleGroupRemoved;
        _simulation.OnSimulationStateChanged -= HandleSimulationStateChanged;
    }

    private void HandleSimulationStateChanged()
    {
        MarkWorldSimSaveCacheDirty();
    }

    private void OnDestroy()
    {
        TurnSystem.UnsubscribeFromEndOfTurn(HandleTurnEnded);

        if (SeasonManager.Instance != null)
            SeasonManager.Instance.OnSeasonChanged -= HandleSeasonChanged;

        if (tileActivator != null)
            tileActivator.OnTilesActivated -= HandleTilesActivated;

        AnimalRepellerRegistry.OnChanged -= RefreshRepelledTiles;

        if (_spawnRoutine != null)
            StopCoroutine(_spawnRoutine);

        if (_reproductionSpawnRoutine != null)
            StopCoroutine(_reproductionSpawnRoutine);

        if (_seasonalTopUpRoutine != null)
            StopCoroutine(_seasonalTopUpRoutine);

        if (_simulation != null)
        {
            _simulation.OnGroupCreated -= HandleGroupCreated;
            _simulation.OnGroupUpdated -= HandleGroupUpdated;
            _simulation.OnGroupRemoved -= HandleGroupRemoved;
            _simulation.OnGroupAttackedPlayerTile -= HandleGroupAttackedPlayerTile;
            _simulation.OnGroupAttackedPlayerUnitGroup -= HandleGroupAttackedPlayerUnitGroup;
            _simulation.OnGroupAttemptedStorageRaid -= HandleGroupAttemptedStorageRaid;
        }

        if (AnimalSimulationAccess.Current == _simulation)
            AnimalSimulationAccess.Current = null;
    }

    private void HandleSeasonChanged(SeasonDefinition newSeason)
    {
        //Debug.Log("Season change top up attempt");

        // Do not run seasonal top-up before the initial world spawn has completed.
        if (!_hasCompletedInitialAnimalSpawn)
            return;

        //Debug.Log("Season change top up Initial Animal Spawn complete");

        if (_simulation != null &&
            _seasonalTopUpRoutine == null &&
            _simulation.GetTotalGroupCount() < maxTotalGroups)
        {
            _seasonalTopUpRoutine = StartCoroutine(SeasonalTopUpMissingSpeciesFromPreset());
            //Debug.Log("Starting Top up");
        }
    }

    private void Update()
    {
        if (_isTickingTurn && _simulation != null)
        {
            bool done = _simulation.TickSomeAnimals(groupsTickedPerFrame);
            if (done)
            {
                _isTickingTurn = false;
                CommitUnitUnderAttackIconsAfterTurn();
                MarkWorldSimSaveCacheDirty();
            }
        }

        if (!_isTickingTurn && _simulation != null && _worldSimSaveCacheDirty)
        {
            _simulation.RebuildCachedSaveState();
            _worldSimSaveCacheDirty = false;
        }

        if (_simulation != null &&
            _simulation.HasPendingSpawns &&
            _reproductionSpawnRoutine == null)
        {
            _reproductionSpawnRoutine = StartCoroutine(ProcessReproductionSpawns());
        }

        if (refreshPlayerAnimalTargetIconsRealtime && Time.unscaledTime >= _nextPlayerTargetRefreshAt)
        {
            RefreshPlayerTargetedAnimalIcons();
            _nextPlayerTargetRefreshAt = Time.unscaledTime + playerAnimalTargetRefreshInterval;
        }
    }

    private int GetRemainingSeasonalGroupCount(Dictionary<AnimalDefinition, int> remainingGroupsBySpecies)
    {
        if (remainingGroupsBySpecies == null || remainingGroupsBySpecies.Count == 0)
            return 0;

        int total = 0;

        foreach (var kvp in remainingGroupsBySpecies)
        {
            if (kvp.Key != null && kvp.Value > 0)
                total += kvp.Value;
        }

        return total;
    }

    private readonly AnimalSpawnPresetSettings _fallbackAnimalSpawnSettings = new();

    private AnimalSpawnPresetSettings CurrentAnimalSpawnSettings
    {
        get
        {
            if (EnvironmentPresetManager.Instance != null)
                return EnvironmentPresetManager.Instance.GetAnimalSpawnSettings();

            return _fallbackAnimalSpawnSettings;
        }
    }

    private void ApplyWorldSpeciesGroupCapMultiplier()
    {
        if (_simulation != null)
            _simulation.SetWorldSpeciesGroupCapMultiplier(worldSpeciesGroupCapMultiplier);
    }

    public AnimalSimulationSaveData SaveState()
    {
        AnimalSimulationSaveData data = _simulation != null
            ? _simulation.SaveState()
            : new AnimalSimulationSaveData();

        data.hasCompletedInitialAnimalSpawn = _hasCompletedInitialAnimalSpawn;
        return data;
    }

    private void MarkWorldSimSaveCacheDirty()
    {
        _worldSimSaveCacheDirty = true;
        SaveSystem.MarkSectionDirty(SaveSectionKeys.WorldSim);
    }

    public void LoadState(AnimalSimulationSaveData data)
    {
        if (_simulation == null)
            return;

        _isTickingTurn = false;
        _currentTurnBeingTicked = -1;

        if (_spawnRoutine != null)
        {
            StopCoroutine(_spawnRoutine);
            _spawnRoutine = null;
        }

        if (_reproductionSpawnRoutine != null)
        {
            StopCoroutine(_reproductionSpawnRoutine);
            _reproductionSpawnRoutine = null;
        }

        if (_seasonalTopUpRoutine != null)
        {
            StopCoroutine(_seasonalTopUpRoutine);
            _seasonalTopUpRoutine = null;
        }

        _simulation.LoadState(data);
        _simulation.RebuildCachedSaveState();
        _worldSimSaveCacheDirty = false;

        _hasCompletedInitialAnimalSpawn =
            data != null && (data.hasCompletedInitialAnimalSpawn || (data.groups != null && data.groups.Count > 0));

        RequestDeferredMarkerRebuild();

        if (_simulation.HasPendingSpawns && _reproductionSpawnRoutine == null)
            _reproductionSpawnRoutine = StartCoroutine(ProcessReproductionSpawns());

        RefreshPlayerTargetedAnimalIcons();
    }

    public void InstallRuntimeRefs(
    TurnSystem newTurnSystem = null,
    MonoEnvironmentDataSource newEnvDataSource = null,
    TileActivator newTileActivator = null)
    {
        if (newTurnSystem != null)
            turnSystem = newTurnSystem;

        if (newEnvDataSource != null)
            envDataSource = newEnvDataSource;

        if (newTileActivator != null && tileActivator != newTileActivator)
        {
            if (tileActivator != null)
                tileActivator.OnTilesActivated -= HandleTilesActivated;

            tileActivator = newTileActivator;
            tileActivator.OnTilesActivated += HandleTilesActivated;
        }
    }

    private void ClearAllAnimalMarkers()
    {
        foreach (var kvp in _markerViews)
        {
            AnimalGroupMarkerView marker = kvp.Value;
            if (marker != null)
                Destroy(marker.gameObject);
        }

        _markerViews.Clear();
    }

    private void RebuildAnimalMarkersFromLoadedState()
    {
        if (_simulation == null)
            return;

        ClearAllAnimalMarkers();
        BuildTileUiLookup();

        _simulation.GetAllGroupsNonAlloc(_loadedGroupsBuffer);

        for (int i = 0; i < _loadedGroupsBuffer.Count; i++)
        {
            AnimalGroupState group = _loadedGroupsBuffer[i];
            if (group == null)
                continue;

            HandleGroupCreated(group);
        }

        _loadedGroupsBuffer.Clear();
        RefreshPlayerTargetedAnimalIcons();
    }
}
