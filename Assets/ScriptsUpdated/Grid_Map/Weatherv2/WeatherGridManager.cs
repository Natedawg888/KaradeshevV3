using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight per-cell weather data owned by WeatherGridManager.
/// For v2 this only tracks climate-derived values.
/// More fields can be added later without turning this manager into a gameplay system.
/// </summary>
[Serializable]
public struct WeatherCellState
{
    public float temperatureC;
    public float humidity01;
    public bool isValid;

    public static WeatherCellState Invalid => new WeatherCellState
    {
        temperatureC = 0f,
        humidity01 = 0f,
        isValid = false
    };
}

/// <summary>
/// Aggregated weather sample across a footprint.
/// Useful for future readers without forcing them to re-scan cells every time.
/// </summary>
[Serializable]
public struct WeatherAreaSample
{
    public int coveredCellCount;
    public int validCellCount;
    public float averageTemperatureC;
    public float averageHumidity01;
    public bool hasAnyValidCell;
}

/// <summary>
/// WeatherGridManager v2
///
/// Responsibility:
/// - Own and maintain weather-grid state
/// - Mirror climate values from ClimateManager
/// - Cache environment -> weather-cell coverage
/// - Cache building -> weather-cell coverage
/// - Expose fast read APIs
///
/// Non-responsibilities:
/// - No gameplay damage/effects
/// - No visuals
/// - No storms/tornadoes/fire/volcanic systems
/// - No animal/unit/building consequence logic
/// </summary>
public class WeatherGridManager : MonoBehaviour
{
    public static WeatherGridManager Instance { get; private set; }

    [Header("World References")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private ClimateManager climateManager;
    [SerializeField] private MonoEnvironmentDataSource environmentDataSource;

    [Header("Late Runtime References")]
    [SerializeField] private WorldBuildingManager worldBuildingManager;

    [Header("Weather Space")]
    [SerializeField] private float weatherGridBaseHeight = 6f;

    public float WeatherGridBaseHeight => weatherGridBaseHeight;

    [Header("Startup")]
    [SerializeField] private bool buildOnStart = true;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = false;
    [SerializeField] private bool warnOnCellOverlap = false;

    public event Action OnWeatherGridInitialized;
    public event Action OnWeatherStateRefreshed;
    public event Action OnEnvironmentCoverageRebuilt;
    public event Action OnBuildingCoverageRebuilt;
    public event Action<EnvironmentControl> OnEnvironmentCoverageChanged;
    public event Action<string> OnBuildingCoverageChanged;

    public int Columns => _cols;
    public int Rows => _rows;
    public bool IsInitialized => _isInitialized;

    private int _cols;
    private int _rows;
    private bool _isInitialized;

    private WeatherCellState[,] _cellStates;
    private EnvironmentControl[,] _environmentByCell;
    private WorldBuildingManager.Record[,] _buildingByCell;

    private readonly Dictionary<EnvironmentControl, EnvironmentCoverage> _environmentCoverageByEnv =
        new Dictionary<EnvironmentControl, EnvironmentCoverage>();

    private readonly Dictionary<TileCoord, EnvironmentCoverage> _environmentCoverageByPrimaryCoord =
        new Dictionary<TileCoord, EnvironmentCoverage>();

    private readonly Dictionary<string, BuildingCoverage> _buildingCoverageById =
        new Dictionary<string, BuildingCoverage>();

    private readonly List<TileCoord> _coordScratch = new List<TileCoord>(8);

    // Track the exact objects we are currently subscribed to so late rebinding is safe.
    private ClimateManager _subscribedClimateManager;
    private MonoEnvironmentDataSource _subscribedEnvironmentDataSource;
    private WorldBuildingManager _subscribedWorldBuildingManager;

    [Header("Gizmos")]
    [SerializeField] private bool drawWeatherGridGizmos = true;
    [SerializeField] private bool drawOnlyWhenSelected = true;
    [SerializeField] private bool drawCellFill = true;
    [SerializeField] private bool tintFillByHumidity = true;
    [SerializeField] private float gizmoY = 0.9f;
    [SerializeField] private float fillInset = 0.08f;

    [SerializeField] private Color gizmoGridColor = new Color(0.15f, 0.8f, 1f, 0.9f);
    [SerializeField] private Color gizmoValidCellColor = new Color(0.1f, 0.7f, 1f, 0.10f);
    [SerializeField] private Color gizmoInvalidCellColor = new Color(1f, 0.2f, 0.2f, 0.08f);

    [Header("Ownership Gizmos")]
    [SerializeField] private bool drawEnvironmentOwnership = true;
    [SerializeField] private bool drawBuildingOwnership = true;
    [SerializeField] private bool drawOwnershipAboveWeather = true;
    [SerializeField] private float ownershipYOffset = 0.08f;
    [SerializeField] private float ownershipInset = 0.22f;
    [SerializeField] private bool drawEnvironmentWire = true;
    [SerializeField] private bool drawBuildingWire = true;

    [SerializeField] private Color environmentOwnershipFillColor = new Color(0.2f, 1f, 0.35f, 0.22f);
    [SerializeField] private Color buildingOwnershipFillColor = new Color(1f, 0.85f, 0.1f, 0.28f);
    [SerializeField] private Color environmentOwnershipWireColor = new Color(0.1f, 0.8f, 0.2f, 0.95f);
    [SerializeField] private Color buildingOwnershipWireColor = new Color(1f, 0.7f, 0.05f, 0.95f);

    [Serializable]
    private sealed class EnvironmentCoverage
    {
        public TileCoord primaryCoord;
        public EnvironmentControl environment;
        public readonly List<TileCoord> cells = new List<TileCoord>(4);
    }

    [Serializable]
    private sealed class BuildingCoverage
    {
        public string instanceId;
        public WorldBuildingManager.Record record;
        public readonly List<TileCoord> cells = new List<TileCoord>(4);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolveWorldReferences();
    }

    private void OnEnable()
    {
        ResolveWorldReferences();
        RebindSubscriptions();
    }

    private void Start()
    {
        if (buildOnStart)
            FullRebuild();
    }

    private void OnDisable()
    {
        UnbindAllSubscriptions();
    }

    private void OnDestroy()
    {
        UnbindAllSubscriptions();

        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// Optional explicit world-side install.
    /// Useful if you want BootstrapLoader / installers to inject refs directly
    /// instead of relying on singleton resolution.
    /// </summary>
    public void InstallWorldRuntimeRefs(
        GridManager newGridManager,
        ClimateManager newClimateManager,
        MonoEnvironmentDataSource newEnvironmentDataSource,
        bool rebuildNow = true)
    {
        if (newGridManager != null)
            gridManager = newGridManager;

        if (newClimateManager != null)
            climateManager = newClimateManager;

        if (newEnvironmentDataSource != null)
            environmentDataSource = newEnvironmentDataSource;

        RebindSubscriptions();

        if (rebuildNow)
        {
            InitializeWeatherGridFromGridManager();
            RefreshClimateFromClimateManager();
            RebuildEnvironmentFootprintCoverage();
        }
    }

    /// <summary>
    /// Late-bind the WorldBuildingManager after the player/world setup scenes are loaded.
    /// This is the important hook for your bootstrap flow.
    /// </summary>
    public void SetWorldBuildingManager(WorldBuildingManager newWorldBuildingManager, bool rebuildCoverage = true)
    {
        if (ReferenceEquals(worldBuildingManager, newWorldBuildingManager) &&
            ReferenceEquals(_subscribedWorldBuildingManager, newWorldBuildingManager))
        {
            return;
        }

        worldBuildingManager = newWorldBuildingManager;
        RebindSubscriptions();

        if (rebuildCoverage)
            RebuildBuildingFootprintCoverage();

        if (debugLogging)
        {
            string n = worldBuildingManager != null ? worldBuildingManager.name : "null";
            Debug.Log($"[WeatherGridManager] SetWorldBuildingManager -> {n}");
        }
    }

    /// <summary>
    /// Initializes the weather grid to match GridManager dimensions.
    /// Does not automatically populate climate or coverage caches.
    /// </summary>
    public bool InitializeWeatherGridFromGridManager()
    {
        ResolveWorldReferences();

        if (gridManager == null)
        {
            Debug.LogWarning("[WeatherGridManager] Cannot initialize: GridManager is missing.");
            return false;
        }

        int newCols = Mathf.Max(0, gridManager.columns);
        int newRows = Mathf.Max(0, gridManager.rows);

        if (newCols <= 0 || newRows <= 0)
        {
            Debug.LogWarning("[WeatherGridManager] Cannot initialize: grid dimensions are invalid.");
            return false;
        }

        bool sizeChanged = !_isInitialized || newCols != _cols || newRows != _rows;

        _cols = newCols;
        _rows = newRows;

        if (sizeChanged)
        {
            _cellStates = new WeatherCellState[_cols, _rows];
            _environmentByCell = new EnvironmentControl[_cols, _rows];
            _buildingByCell = new WorldBuildingManager.Record[_cols, _rows];
        }
        else
        {
            ClearAllCellState();
            ClearEnvironmentCellOwnership();
            ClearBuildingCellOwnership();
        }

        _environmentCoverageByEnv.Clear();
        _environmentCoverageByPrimaryCoord.Clear();
        _buildingCoverageById.Clear();

        _isInitialized = true;
        OnWeatherGridInitialized?.Invoke();

        if (debugLogging)
            Debug.Log($"[WeatherGridManager] Initialized weather grid {_cols}x{_rows}");

        return true;
    }

    /// <summary>
    /// Convenience entry point:
    /// - initialize grid
    /// - refresh climate data
    /// - rebuild environment coverage
    /// - rebuild building coverage (if PlayerBuildingManager is available)
    /// </summary>
    public bool FullRebuild()
    {
        if (!InitializeWeatherGridFromGridManager())
            return false;

        RefreshClimateFromClimateManager();
        RebuildEnvironmentFootprintCoverage();
        RebuildBuildingFootprintCoverage();
        return true;
    }

    /// <summary>
    /// Pulls current per-cell temperature/humidity from ClimateManager.
    /// Invalidates cells if climate is unavailable.
    /// </summary>
    public void RefreshClimateFromClimateManager()
    {
        if (!EnsureInitialized())
            return;

        ResolveWorldReferences();

        if (climateManager == null || !climateManager.IsClimateReady())
        {
            ClearAllCellState();

            if (debugLogging)
                Debug.Log("[WeatherGridManager] Climate refresh skipped: ClimateManager missing or not ready.");

            OnWeatherStateRefreshed?.Invoke();
            return;
        }

        for (int x = 0; x < _cols; x++)
        {
            for (int y = 0; y < _rows; y++)
            {
                WeatherCellState state = WeatherCellState.Invalid;

                bool hasTemp = climateManager.TryGetTemperatureAtCell(x, y, out float temp);
                bool hasHum = climateManager.TryGetHumidityAtCell(x, y, out float hum);

                if (hasTemp && hasHum)
                {
                    state.temperatureC = temp;
                    state.humidity01 = hum;
                    state.isValid = true;
                }

                _cellStates[x, y] = state;
            }
        }

        if (debugLogging)
            Debug.Log("[WeatherGridManager] Refreshed climate values from ClimateManager.");

        OnWeatherStateRefreshed?.Invoke();
    }

    /// <summary>
    /// Rebuilds all environment footprint coverage from MonoEnvironmentDataSource.
    /// </summary>
    public void RebuildEnvironmentFootprintCoverage()
    {
        if (!EnsureInitialized())
            return;

        ResolveWorldReferences();

        ClearEnvironmentCellOwnership();
        _environmentCoverageByEnv.Clear();
        _environmentCoverageByPrimaryCoord.Clear();

        if (environmentDataSource == null)
        {
            if (debugLogging)
                Debug.Log("[WeatherGridManager] Environment coverage rebuild skipped: source missing.");

            OnEnvironmentCoverageRebuilt?.Invoke();
            return;
        }

        foreach (KeyValuePair<TileCoord, EnvironmentControl> kvp in environmentDataSource.AllTiles)
        {
            TileCoord primaryCoord = kvp.Key;
            EnvironmentControl env = kvp.Value;

            if (env == null)
                continue;

            RegisterOrUpdateEnvironmentCoverage(primaryCoord, env, raiseChangedEvent: false);
        }

        if (debugLogging)
            Debug.Log($"[WeatherGridManager] Rebuilt environment coverage. Count={_environmentCoverageByEnv.Count}");

        OnEnvironmentCoverageRebuilt?.Invoke();
    }

    /// <summary>
    /// Rebuilds all building footprint coverage from PlayerBuildingManager.
    /// Safe to call before PlayerBuildingManager exists; it will just no-op cleanly.
    /// </summary>
    public void RebuildBuildingFootprintCoverage()
    {
        if (!EnsureInitialized())
            return;

        ClearBuildingCellOwnership();
        _buildingCoverageById.Clear();

        // Important for additive loading:
        // allow a late singleton resolve if the world building manager is now available.
        if (worldBuildingManager == null)
            worldBuildingManager = WorldBuildingManager.Instance;

        RebindSubscriptions();

        if (worldBuildingManager == null)
        {
            if (debugLogging)
                Debug.Log("[WeatherGridManager] Building coverage rebuild skipped: WorldBuildingManager not available yet.");

            OnBuildingCoverageRebuilt?.Invoke();
            return;
        }

        IReadOnlyList<WorldBuildingManager.Record> records = worldBuildingManager.GetAll();
        if (records != null)
        {
            for (int i = 0; i < records.Count; i++)
            {
                WorldBuildingManager.Record record = records[i];
                if (record == null)
                    continue;

                RegisterOrUpdateBuildingCoverage(record, raiseChangedEvent: false);
            }
        }

        if (debugLogging)
            Debug.Log($"[WeatherGridManager] Rebuilt building coverage. Count={_buildingCoverageById.Count}");

        OnBuildingCoverageRebuilt?.Invoke();
    }

    public void RefreshEnvironmentCoverage(TileCoord primaryCoord, EnvironmentControl env)
    {
        if (!EnsureInitialized())
            return;

        RegisterOrUpdateEnvironmentCoverage(primaryCoord, env, raiseChangedEvent: true);
    }

    public void RefreshBuildingCoverage(WorldBuildingManager.Record record)
    {
        if (!EnsureInitialized())
            return;

        RegisterOrUpdateBuildingCoverage(record, raiseChangedEvent: true);
    }

    public void RemoveEnvironmentCoverage(EnvironmentControl env)
    {
        RemoveEnvironmentCoverageInternal(env, raiseChangedEvent: true);
    }

    public void RemoveBuildingCoverage(string instanceId)
    {
        RemoveBuildingCoverageInternal(instanceId, raiseChangedEvent: true);
    }

    // ---------------------------------------------------------------------
    // Cell queries
    // ---------------------------------------------------------------------

    public bool TryGetCellState(int x, int y, out WeatherCellState state)
    {
        state = WeatherCellState.Invalid;

        if (!EnsureInitialized() || !IsInBounds(x, y))
            return false;

        state = _cellStates[x, y];
        return state.isValid;
    }

    public bool TryGetCellState(Vector3 worldPosition, out WeatherCellState state)
    {
        state = WeatherCellState.Invalid;

        if (!EnsureInitialized() || gridManager == null)
            return false;

        Vector2Int gridPos = gridManager.GetGridPosition(worldPosition);
        return TryGetCellState(gridPos.x, gridPos.y, out state);
    }

    public WeatherCellState GetCellStateOrDefault(int x, int y)
    {
        return TryGetCellState(x, y, out WeatherCellState state)
            ? state
            : WeatherCellState.Invalid;
    }

    public bool TryGetEnvironmentAtCell(int x, int y, out EnvironmentControl environment)
    {
        environment = null;

        if (!EnsureInitialized() || !IsInBounds(x, y))
            return false;

        environment = _environmentByCell[x, y];
        return environment != null;
    }

    public bool TryGetBuildingAtCell(int x, int y, out WorldBuildingManager.Record building)
    {
        building = null;

        bool initialized = EnsureInitialized();
        bool inBounds = initialized && IsInBounds(x, y);

        if (!initialized || !inBounds)
        {
            if (debugLogging)
            {
                Debug.Log(
                    $"[WeatherGridManager] TryGetBuildingAtCell MISS early | " +
                    $"Cell=({x},{y}) Initialized={initialized} InBounds={inBounds}");
            }

            return false;
        }

        building = _buildingByCell[x, y];

        if (debugLogging)
        {
            Debug.Log(
                $"[WeatherGridManager] TryGetBuildingAtCell | Cell=({x},{y}) " +
                $"Hit={(building != null)} " +
                $"Record={(building != null ? building.instanceId : "null")}");
        }

        return building != null;
    }

    // ---------------------------------------------------------------------
    // Environment coverage queries
    // ---------------------------------------------------------------------

    public bool TryGetEnvironmentCoveredCells(EnvironmentControl env, List<TileCoord> results)
    {
        if (results == null)
            return false;

        results.Clear();

        if (!EnsureInitialized() || env == null)
            return false;

        if (!_environmentCoverageByEnv.TryGetValue(env, out EnvironmentCoverage coverage))
            return false;

        CopyCoords(coverage.cells, results);
        return results.Count > 0;
    }

    public bool TryGetEnvironmentCoveredCells(TileCoord primaryCoord, List<TileCoord> results)
    {
        if (results == null)
            return false;

        results.Clear();

        if (!EnsureInitialized())
            return false;

        if (!_environmentCoverageByPrimaryCoord.TryGetValue(primaryCoord, out EnvironmentCoverage coverage))
            return false;

        CopyCoords(coverage.cells, results);
        return results.Count > 0;
    }

    public bool TryGetEnvironmentWeatherSample(EnvironmentControl env, out WeatherAreaSample sample)
    {
        sample = default;

        if (!EnsureInitialized() || env == null)
            return false;

        if (!_environmentCoverageByEnv.TryGetValue(env, out EnvironmentCoverage coverage))
            return false;

        return TryBuildAreaSample(coverage.cells, out sample);
    }

    public bool TryGetEnvironmentWeatherSample(TileCoord primaryCoord, out WeatherAreaSample sample)
    {
        sample = default;

        if (!EnsureInitialized())
            return false;

        if (!_environmentCoverageByPrimaryCoord.TryGetValue(primaryCoord, out EnvironmentCoverage coverage))
            return false;

        return TryBuildAreaSample(coverage.cells, out sample);
    }

    // ---------------------------------------------------------------------
    // Building coverage queries
    // ---------------------------------------------------------------------

    public bool TryGetBuildingCoveredCells(WorldBuildingManager.Record record, List<TileCoord> results)
    {
        if (results == null)
            return false;

        results.Clear();

        if (record == null || string.IsNullOrEmpty(record.instanceId))
            return false;

        return TryGetBuildingCoveredCells(record.instanceId, results);
    }

    public bool TryGetBuildingCoveredCells(string instanceId, List<TileCoord> results)
    {
        if (results == null)
            return false;

        results.Clear();

        if (!EnsureInitialized() || string.IsNullOrEmpty(instanceId))
            return false;

        if (!_buildingCoverageById.TryGetValue(instanceId, out BuildingCoverage coverage))
            return false;

        CopyCoords(coverage.cells, results);
        return results.Count > 0;
    }

    public bool TryGetBuildingWeatherSample(WorldBuildingManager.Record record, out WeatherAreaSample sample)
    {
        sample = default;

        if (record == null || string.IsNullOrEmpty(record.instanceId))
            return false;

        return TryGetBuildingWeatherSample(record.instanceId, out sample);
    }

    public bool TryGetBuildingWeatherSample(string instanceId, out WeatherAreaSample sample)
    {
        sample = default;

        if (!EnsureInitialized() || string.IsNullOrEmpty(instanceId))
            return false;

        if (!_buildingCoverageById.TryGetValue(instanceId, out BuildingCoverage coverage))
            return false;

        return TryBuildAreaSample(coverage.cells, out sample);
    }

    // ---------------------------------------------------------------------
    // Internal event handlers
    // ---------------------------------------------------------------------

    private void HandleClimateRebuilt()
    {
        RefreshClimateFromClimateManager();
    }

    private void HandleEnvironmentRegisteredOrUpdated(TileCoord primaryCoord, EnvironmentControl env)
    {
        RefreshEnvironmentCoverage(primaryCoord, env);
    }

    private void HandleEnvironmentUnregistered(TileCoord primaryCoord, EnvironmentControl env)
    {
        RemoveEnvironmentCoverageInternal(env, raiseChangedEvent: true);
    }

    private void HandleBuildingPlaced(WorldBuildingManager.Record record)
    {
        RefreshBuildingCoverage(record);
    }

    private void HandleBuildingRemoved(WorldBuildingManager.Record record)
    {
        if (record == null)
            return;

        RemoveBuildingCoverageInternal(record.instanceId, raiseChangedEvent: true);
    }

    // ---------------------------------------------------------------------
    // Internal registration / rebuild helpers
    // ---------------------------------------------------------------------

    private void RegisterOrUpdateEnvironmentCoverage(TileCoord primaryCoord, EnvironmentControl env, bool raiseChangedEvent)
    {
        if (env == null)
            return;

        RemoveEnvironmentCoverageInternal(env, raiseChangedEvent: false);

        _coordScratch.Clear();

        bool gotFootprint = environmentDataSource != null &&
                            environmentDataSource.TryGetFootprintCoords(primaryCoord, _coordScratch);

        if (!gotFootprint)
            _coordScratch.Add(primaryCoord);

        EnvironmentCoverage coverage = new EnvironmentCoverage
        {
            primaryCoord = primaryCoord,
            environment = env
        };

        for (int i = 0; i < _coordScratch.Count; i++)
        {
            TileCoord coord = _coordScratch[i];
            if (!IsInBounds(coord.x, coord.y))
                continue;

            if (warnOnCellOverlap &&
                _environmentByCell[coord.x, coord.y] != null &&
                _environmentByCell[coord.x, coord.y] != env)
            {
                Debug.LogWarning(
                    $"[WeatherGridManager] Environment cell overlap at {coord}. " +
                    $"Replacing {_environmentByCell[coord.x, coord.y].name} with {env.name}.");
            }

            _environmentByCell[coord.x, coord.y] = env;
            coverage.cells.Add(coord);
        }

        if (coverage.cells.Count == 0 && IsInBounds(primaryCoord.x, primaryCoord.y))
        {
            _environmentByCell[primaryCoord.x, primaryCoord.y] = env;
            coverage.cells.Add(primaryCoord);
        }

        _environmentCoverageByEnv[env] = coverage;
        _environmentCoverageByPrimaryCoord[coverage.primaryCoord] = coverage;

        if (raiseChangedEvent)
            OnEnvironmentCoverageChanged?.Invoke(env);
    }

    private void RegisterOrUpdateBuildingCoverage(WorldBuildingManager.Record record, bool raiseChangedEvent)
    {
        if (record == null || string.IsNullOrEmpty(record.instanceId))
            return;

        RemoveBuildingCoverageInternal(record.instanceId, raiseChangedEvent: false);

        BuildingCoverage coverage = new BuildingCoverage
        {
            instanceId = record.instanceId,
            record = record
        };

        _coordScratch.Clear();
        BuildBuildingFootprint(record, _coordScratch);

        for (int i = 0; i < _coordScratch.Count; i++)
        {
            TileCoord coord = _coordScratch[i];
            if (!IsInBounds(coord.x, coord.y))
                continue;

            if (warnOnCellOverlap &&
                _buildingByCell[coord.x, coord.y] != null &&
                _buildingByCell[coord.x, coord.y] != record)
            {
                string previousId = _buildingByCell[coord.x, coord.y].instanceId;
                Debug.LogWarning(
                    $"[WeatherGridManager] Building cell overlap at {coord}. " +
                    $"Replacing {previousId} with {record.instanceId}.");
            }

            _buildingByCell[coord.x, coord.y] = record;
            coverage.cells.Add(coord);
        }

        if (coverage.cells.Count == 0)
            return;

        _buildingCoverageById[record.instanceId] = coverage;

        if (raiseChangedEvent)
            OnBuildingCoverageChanged?.Invoke(record.instanceId);
    }

    private void RemoveEnvironmentCoverageInternal(EnvironmentControl env, bool raiseChangedEvent)
    {
        if (!EnsureInitialized() || env == null)
            return;

        if (!_environmentCoverageByEnv.TryGetValue(env, out EnvironmentCoverage coverage))
            return;

        for (int i = 0; i < coverage.cells.Count; i++)
        {
            TileCoord coord = coverage.cells[i];
            if (IsInBounds(coord.x, coord.y) && _environmentByCell[coord.x, coord.y] == env)
                _environmentByCell[coord.x, coord.y] = null;
        }

        _environmentCoverageByEnv.Remove(env);
        _environmentCoverageByPrimaryCoord.Remove(coverage.primaryCoord);

        if (raiseChangedEvent)
            OnEnvironmentCoverageChanged?.Invoke(env);
    }

    private void RemoveBuildingCoverageInternal(string instanceId, bool raiseChangedEvent)
    {
        if (!EnsureInitialized() || string.IsNullOrEmpty(instanceId))
            return;

        if (!_buildingCoverageById.TryGetValue(instanceId, out BuildingCoverage coverage))
            return;

        for (int i = 0; i < coverage.cells.Count; i++)
        {
            TileCoord coord = coverage.cells[i];
            if (IsInBounds(coord.x, coord.y) && _buildingByCell[coord.x, coord.y] == coverage.record)
                _buildingByCell[coord.x, coord.y] = null;
        }

        _buildingCoverageById.Remove(instanceId);

        if (raiseChangedEvent)
            OnBuildingCoverageChanged?.Invoke(instanceId);
    }

    /// <summary>
    /// Builds a building footprint from instance bounds if possible.
    /// Falls back to record.worldPos if no bounds are available.
    /// </summary>
    private void BuildBuildingFootprint(WorldBuildingManager.Record record, List<TileCoord> results)
    {
        results.Clear();

        if (record == null || gridManager == null)
            return;

        GameObject instance = record.instance;
        if (instance == null)
            return;

        // 1) Explicit provider should win.
        BuildingFootprintProvider footprintProvider = instance.GetComponent<BuildingFootprintProvider>();
        if (footprintProvider == null)
            footprintProvider = instance.GetComponentInChildren<BuildingFootprintProvider>(true);

        if (footprintProvider != null && footprintProvider.TryGetCoveredCells(gridManager, results))
            return;

        // 2) Then try dedicated box-collider footprint inference.
        if (TryGetFootprintBoundsFromBoxCollider(instance.transform, out Bounds footprintBounds))
        {
            AddGridCellsCoveredByBounds(footprintBounds, results);
            if (results.Count > 0)
                return;
        }

        // 3) General fallback from any collider / renderer bounds.
        if (TryGetWorldBounds(instance.transform, out Bounds bounds))
        {
            AddGridCellsCoveredByBounds(bounds, results);
            if (results.Count > 0)
                return;
        }

        // 4) Last fallback: single anchor cell.
        Vector2Int pos = gridManager.GetGridPosition(record.worldPos);
        if (IsInBounds(pos.x, pos.y))
            results.Add(new TileCoord(pos.x, pos.y));
    }

    private bool TryGetFootprintBoundsFromBoxCollider(Transform root, out Bounds bounds)
    {
        if (root == null)
        {
            bounds = default;
            return false;
        }

        BoxCollider ownBox = root.GetComponent<BoxCollider>();
        if (ownBox != null)
        {
            bounds = ownBox.bounds;
            return true;
        }

        BoxCollider[] childBoxes = root.GetComponentsInChildren<BoxCollider>(true);
        if (childBoxes != null && childBoxes.Length > 0)
        {
            bounds = childBoxes[0].bounds;
            for (int i = 1; i < childBoxes.Length; i++)
                bounds.Encapsulate(childBoxes[i].bounds);

            return true;
        }

        bounds = default;
        return false;
    }

    private void AddGridCellsCoveredByBounds(Bounds bounds, List<TileCoord> results)
    {
        if (gridManager == null || results == null)
            return;

        float epsilon = gridManager.cellSize * 0.10f;

        Vector2Int min = gridManager.GetGridPosition(new Vector3(bounds.min.x + epsilon, 0f, bounds.min.z + epsilon));
        Vector2Int max = gridManager.GetGridPosition(new Vector3(bounds.max.x - epsilon, 0f, bounds.max.z - epsilon));

        int startX = Mathf.Max(0, min.x);
        int endX = Mathf.Min(_cols - 1, max.x);
        int startY = Mathf.Max(0, min.y);
        int endY = Mathf.Min(_rows - 1, max.y);

        for (int x = startX; x <= endX; x++)
        {
            for (int y = startY; y <= endY; y++)
            {
                TileCoord coord = new TileCoord(x, y);
                if (!results.Contains(coord))
                    results.Add(coord);
            }
        }
    }

    private bool TryBuildAreaSample(List<TileCoord> coords, out WeatherAreaSample sample)
    {
        sample = default;

        if (coords == null || coords.Count == 0 || !EnsureInitialized())
            return false;

        float tempSum = 0f;
        float humSum = 0f;
        int validCount = 0;

        for (int i = 0; i < coords.Count; i++)
        {
            TileCoord coord = coords[i];
            if (!IsInBounds(coord.x, coord.y))
                continue;

            WeatherCellState state = _cellStates[coord.x, coord.y];
            if (!state.isValid)
                continue;

            tempSum += state.temperatureC;
            humSum += state.humidity01;
            validCount++;
        }

        sample.coveredCellCount = coords.Count;
        sample.validCellCount = validCount;
        sample.hasAnyValidCell = validCount > 0;
        sample.averageTemperatureC = validCount > 0 ? tempSum / validCount : 0f;
        sample.averageHumidity01 = validCount > 0 ? humSum / validCount : 0f;

        return true;
    }

    private bool TryGetWorldBounds(Transform root, out Bounds bounds)
    {
        if (root == null)
        {
            bounds = default;
            return false;
        }

        Collider ownCollider = root.GetComponent<Collider>();
        if (ownCollider != null)
        {
            bounds = ownCollider.bounds;
            return true;
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        if (colliders != null && colliders.Length > 0)
        {
            bounds = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++)
                bounds.Encapsulate(colliders[i].bounds);

            return true;
        }

        Renderer ownRenderer = root.GetComponent<Renderer>();
        if (ownRenderer != null)
        {
            bounds = ownRenderer.bounds;
            return true;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers != null && renderers.Length > 0)
        {
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            return true;
        }

        bounds = default;
        return false;
    }

    private void ResolveWorldReferences()
    {
        if (gridManager == null)
            gridManager = GridManager.Instance;

        if (climateManager == null)
            climateManager = ClimateManager.Instance;

        if (environmentDataSource == null)
            environmentDataSource = MonoEnvironmentDataSource.Instance;
    }

    private void RebindSubscriptions()
    {
        RebindClimateSubscription();
        RebindEnvironmentSubscription();
        RebindBuildingSubscription();
    }

    private void RebindClimateSubscription()
    {
        if (_subscribedClimateManager == climateManager)
            return;

        if (_subscribedClimateManager != null)
            _subscribedClimateManager.OnClimateRebuilt -= HandleClimateRebuilt;

        _subscribedClimateManager = climateManager;

        if (_subscribedClimateManager != null)
            _subscribedClimateManager.OnClimateRebuilt += HandleClimateRebuilt;
    }

    private void RebindEnvironmentSubscription()
    {
        if (_subscribedEnvironmentDataSource == environmentDataSource)
            return;

        if (_subscribedEnvironmentDataSource != null)
        {
            _subscribedEnvironmentDataSource.OnEnvironmentRegisteredOrUpdated -= HandleEnvironmentRegisteredOrUpdated;
            _subscribedEnvironmentDataSource.OnEnvironmentUnregistered -= HandleEnvironmentUnregistered;
        }

        _subscribedEnvironmentDataSource = environmentDataSource;

        if (_subscribedEnvironmentDataSource != null)
        {
            _subscribedEnvironmentDataSource.OnEnvironmentRegisteredOrUpdated += HandleEnvironmentRegisteredOrUpdated;
            _subscribedEnvironmentDataSource.OnEnvironmentUnregistered += HandleEnvironmentUnregistered;
        }
    }

    private void RebindBuildingSubscription()
    {
        if (_subscribedWorldBuildingManager == worldBuildingManager)
            return;

        if (_subscribedWorldBuildingManager != null)
        {
            _subscribedWorldBuildingManager.OnBuildingPlaced -= HandleBuildingPlaced;
            _subscribedWorldBuildingManager.OnBuildingRemoved -= HandleBuildingRemoved;
        }

        _subscribedWorldBuildingManager = worldBuildingManager;

        if (_subscribedWorldBuildingManager != null)
        {
            _subscribedWorldBuildingManager.OnBuildingPlaced += HandleBuildingPlaced;
            _subscribedWorldBuildingManager.OnBuildingRemoved += HandleBuildingRemoved;
        }
    }

    private void UnbindAllSubscriptions()
    {
        if (_subscribedClimateManager != null)
            _subscribedClimateManager.OnClimateRebuilt -= HandleClimateRebuilt;

        if (_subscribedEnvironmentDataSource != null)
        {
            _subscribedEnvironmentDataSource.OnEnvironmentRegisteredOrUpdated -= HandleEnvironmentRegisteredOrUpdated;
            _subscribedEnvironmentDataSource.OnEnvironmentUnregistered -= HandleEnvironmentUnregistered;
        }

        if (_subscribedWorldBuildingManager != null)
        {
            _subscribedWorldBuildingManager.OnBuildingPlaced -= HandleBuildingPlaced;
            _subscribedWorldBuildingManager.OnBuildingRemoved -= HandleBuildingRemoved;
        }

        _subscribedClimateManager = null;
        _subscribedEnvironmentDataSource = null;
        _subscribedWorldBuildingManager = null;
    }

    private bool EnsureInitialized()
    {
        if (_isInitialized)
            return true;

        return InitializeWeatherGridFromGridManager();
    }

    private bool IsInBounds(int x, int y)
    {
        return x >= 0 && x < _cols && y >= 0 && y < _rows;
    }

    private void ClearAllCellState()
    {
        if (_cellStates == null)
            return;

        for (int x = 0; x < _cols; x++)
        {
            for (int y = 0; y < _rows; y++)
                _cellStates[x, y] = WeatherCellState.Invalid;
        }
    }

    private void ClearEnvironmentCellOwnership()
    {
        if (_environmentByCell != null)
            Array.Clear(_environmentByCell, 0, _environmentByCell.Length);
    }

    private void ClearBuildingCellOwnership()
    {
        if (_buildingByCell != null)
            Array.Clear(_buildingByCell, 0, _buildingByCell.Length);
    }

    private static void CopyCoords(List<TileCoord> source, List<TileCoord> destination)
    {
        destination.Clear();

        if (source == null)
            return;

        for (int i = 0; i < source.Count; i++)
            destination.Add(source[i]);
    }

    public bool TryGetWeatherCellCornerWorldPosition(int x, int y, out Vector3 worldPosition)
    {
        worldPosition = Vector3.zero;

        if (!EnsureInitialized())
            return false;

        GridManager gm = gridManager != null ? gridManager : GridManager.Instance;
        if (gm == null || !IsInBounds(x, y))
            return false;

        Vector3 basePos = gm.GetWorldPosition(x, y);
        worldPosition = new Vector3(basePos.x, weatherGridBaseHeight, basePos.z);
        return true;
    }

    public bool TryGetWeatherCellCenterWorldPosition(int x, int y, out Vector3 worldPosition)
    {
        worldPosition = Vector3.zero;

        if (!EnsureInitialized())
            return false;

        GridManager gm = gridManager != null ? gridManager : GridManager.Instance;
        if (gm == null || !IsInBounds(x, y))
            return false;

        Vector3 basePos = gm.GetWorldPosition(x, y);
        worldPosition = new Vector3(
            basePos.x + (gm.cellSize * 0.5f),
            weatherGridBaseHeight,
            basePos.z + (gm.cellSize * 0.5f));

        return true;
    }

    private void OnDrawGizmos()
    {
        if (drawOnlyWhenSelected)
            return;

        DrawWeatherGridGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawOnlyWhenSelected)
            return;

        DrawWeatherGridGizmos();
    }

    private void DrawWeatherGridGizmos()
    {
        if (!drawWeatherGridGizmos)
            return;

        GridManager gm = gridManager != null ? gridManager : GridManager.Instance;
        if (gm == null)
            return;

        int cols = _isInitialized ? _cols : gm.columns;
        int rows = _isInitialized ? _rows : gm.rows;

        if (cols <= 0 || rows <= 0 || gm.cellSize <= 0f)
            return;

        Vector3 yOffset = new Vector3(0f, weatherGridBaseHeight + gizmoY, 0f);

        // Draw grid lines.
        Gizmos.color = gizmoGridColor;

        for (int x = 0; x <= cols; x++)
        {
            Vector3 from = gm.GetWorldPosition(x, 0) + yOffset;
            Vector3 to = gm.GetWorldPosition(x, rows) + yOffset;
            Gizmos.DrawLine(from, to);
        }

        for (int y = 0; y <= rows; y++)
        {
            Vector3 from = gm.GetWorldPosition(0, y) + yOffset;
            Vector3 to = gm.GetWorldPosition(cols, y) + yOffset;
            Gizmos.DrawLine(from, to);
        }

        if (drawCellFill)
        {
            float inset = Mathf.Clamp01(fillInset) * gm.cellSize;
            float size = Mathf.Max(0.01f, gm.cellSize - inset * 2f);

            Vector3 cubeSize = new Vector3(size, 0.02f, size);

            for (int x = 0; x < cols; x++)
            {
                for (int y = 0; y < rows; y++)
                {
                    Vector3 basePos = gm.GetWorldPosition(x, y);
                    Vector3 center = basePos + new Vector3(
                        gm.cellSize * 0.5f,
                        weatherGridBaseHeight + gizmoY,
                        gm.cellSize * 0.5f);

                    Color fillColor = gizmoInvalidCellColor;

                    if (_isInitialized && _cellStates != null && x < _cols && y < _rows)
                    {
                        WeatherCellState state = _cellStates[x, y];

                        if (state.isValid)
                        {
                            if (tintFillByHumidity)
                            {
                                fillColor = Color.Lerp(
                                    new Color(0.95f, 0.35f, 0.2f, gizmoValidCellColor.a),
                                    new Color(0.1f, 0.7f, 1f, gizmoValidCellColor.a),
                                    Mathf.Clamp01(state.humidity01));
                            }
                            else
                            {
                                fillColor = gizmoValidCellColor;
                            }
                        }
                    }

                    Gizmos.color = fillColor;
                    Gizmos.DrawCube(center, cubeSize);
                }
            }
        }

        DrawOwnershipGizmos(gm, cols, rows);
    }

    public WeatherGridManagerSaveData SaveState()
    {
        return new WeatherGridManagerSaveData
        {
            gridWasInitialized = _isInitialized
        };
    }

    public void LoadState(WeatherGridManagerSaveData data)
    {
        if (data == null)
            return;

        FullRebuild();
    }

    private void DrawOwnershipGizmos(GridManager gm, int cols, int rows)
    {
        if (!_isInitialized)
            return;

        if (_environmentByCell == null && _buildingByCell == null)
            return;

        float inset = Mathf.Clamp01(ownershipInset) * gm.cellSize;
        float size = Mathf.Max(0.01f, gm.cellSize - inset * 2f);

        float y = weatherGridBaseHeight + gizmoY;
        if (drawOwnershipAboveWeather)
            y += ownershipYOffset;

        Vector3 cubeSize = new Vector3(size, 0.03f, size);

        for (int x = 0; x < cols; x++)
        {
            for (int yCell = 0; yCell < rows; yCell++)
            {
                Vector3 basePos = gm.GetWorldPosition(x, yCell);
                Vector3 center = basePos + new Vector3(
                    gm.cellSize * 0.5f,
                    y,
                    gm.cellSize * 0.5f);

                bool hasEnvironment =
                    _environmentByCell != null &&
                    x < _cols && yCell < _rows &&
                    _environmentByCell[x, yCell] != null;

                bool hasBuilding =
                    _buildingByCell != null &&
                    x < _cols && yCell < _rows &&
                    _buildingByCell[x, yCell] != null;

                if (drawEnvironmentOwnership && hasEnvironment)
                {
                    Gizmos.color = environmentOwnershipFillColor;
                    Gizmos.DrawCube(center, cubeSize);

                    if (drawEnvironmentWire)
                    {
                        Gizmos.color = environmentOwnershipWireColor;
                        Gizmos.DrawWireCube(center, cubeSize);
                    }
                }

                if (drawBuildingOwnership && hasBuilding)
                {
                    Vector3 buildingCenter = center;
                    if (drawEnvironmentOwnership && hasEnvironment)
                        buildingCenter.y += 0.035f;

                    Gizmos.color = buildingOwnershipFillColor;
                    Gizmos.DrawCube(buildingCenter, cubeSize);

                    if (drawBuildingWire)
                    {
                        Gizmos.color = buildingOwnershipWireColor;
                        Gizmos.DrawWireCube(buildingCenter, cubeSize);
                    }
                }
            }
        }
    }
}