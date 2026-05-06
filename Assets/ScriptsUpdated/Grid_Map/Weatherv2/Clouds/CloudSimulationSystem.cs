using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cloud-only simulation layered on top of WeatherGridManager.
/// Reads humidity from WeatherGridManager, simulates cloud density/wind,
/// and owns pooled cloud visuals.
/// </summary>
public class CloudSimulationSystem : MonoBehaviour
{
    public enum CloudDensity
    {
        None = 0,
        Low = 1,
        Mid = 2,
        High = 3
    }

    public enum WindDirection8
    {
        North = 0,
        NorthEast = 1,
        East = 2,
        SouthEast = 3,
        South = 4,
        SouthWest = 5,
        West = 6,
        NorthWest = 7
    }

    public static CloudSimulationSystem Instance { get; private set; }

    [Header("References")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private WeatherGridManager weatherGridManager;
    [SerializeField] private Transform cloudVisualRoot;
    [SerializeField] private CloudVisualPool cloudPool;

    [Header("Lifecycle")]
    [SerializeField] private bool initializeOnStart = true;
    [SerializeField] private bool seedCloudsOnInitialize = true;
    [SerializeField] private bool topUpCloudsOnWeatherStateRefreshed = true;
    [SerializeField] private bool advanceWithTurnSystem = true;
    [SerializeField] private int simulationStepsPerAdvance = 1;

    [Header("Humidity -> Cloud Formation")]
    [Range(0f, 1f)][SerializeField] private float lowCloudHumidityThreshold = 0.45f;
    [Range(0f, 1f)][SerializeField] private float midCloudHumidityThreshold = 0.65f;
    [Range(0f, 1f)][SerializeField] private float highCloudHumidityThreshold = 0.82f;

    [Range(0f, 1f)][SerializeField] private float baseFormationChance = 0.08f;
    [Range(0f, 1f)][SerializeField] private float neighbourFormationBonus = 0.12f;
    [SerializeField] private bool forceSeedCloudsInHumidCells = false;

    [Header("Humidity -> Dissipation / Growth")]
    [Range(0f, 1f)][SerializeField] private float dryDissipationChanceMultiplier = 0.35f;
    [Range(0f, 1f)][SerializeField] private float humidGrowthChanceMultiplier = 0.15f;

    [Header("Wind")]
    [SerializeField] private WindDirection8 windDirection = WindDirection8.East;
    [Min(0)][SerializeField] private int windSpeedTilesPerStep = 1;
    [Range(0f, 1f)][SerializeField] private float lateralShuffleChance = 0.15f;
    [Range(0f, 1f)][SerializeField] private float windDirectionChangeChancePerTurn = 0.2f;
    [SerializeField] private bool allowWindDirectionToStaySame = false;

    [Header("Cloud Visuals")]
    [SerializeField] private GameObject lowCloudPrefab;
    [SerializeField] private GameObject midCloudPrefab;
    [SerializeField] private GameObject highCloudPrefab;
    [SerializeField] private bool randomizeCloudRotation = true;
    [SerializeField] private Vector3 minCloudRotation = Vector3.zero;
    [SerializeField] private Vector3 maxCloudRotation = new Vector3(360f, 360f, 360f);

    [Header("Cloud Shading")]
    [SerializeField] private Color cloudBrightColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color cloudDarkColor = new Color(0.55f, 0.55f, 0.6f, 1f);

    [Header("Volcanic Clouds")]
    [SerializeField] private bool enableVolcanicSootInput = true;

    [SerializeField] private CloudDensity minimumVolcanicCloudDensity = CloudDensity.Mid;

    [Tooltip("How much soot visually darkens clouds.")]
    [Range(0f, 1f)]
    [SerializeField] private float volcanicSootDarknessStrength = 0.75f;

    [Tooltip("How much soot remains after each cloud advance. 0.92 = slow fade, 0.5 = fast fade.")]
    [Range(0f, 1f)]
    [SerializeField] private float volcanicSootRetentionPerCloudStep = 0.92f;

    [Tooltip("If true, soot can create cloud visuals even in cells that had no normal cloud.")]
    [SerializeField] private bool volcanicSootCanCreateClouds = true;

    [Tooltip("Final tint color for volcanic soot clouds. Use black for ash/soot.")]
    [SerializeField] private Color volcanicSootCloudColor = Color.black;

    [Tooltip("How strongly soot pushes the cloud tint toward black.")]
    [Range(0f, 1f)]
    [SerializeField] private float volcanicSootBlackTintStrength = 1f;

    private float[,] _volcanicSootGrid;
    private float[,] _nextVolcanicSootGrid;

    [Header("Pool Warmup")]
    [SerializeField] private bool prewarmPoolsOnInitialize = true;
    [Min(0)][SerializeField] private int prewarmLowCloudCount = 16;
    [Min(0)][SerializeField] private int prewarmMidCloudCount = 16;
    [Min(0)][SerializeField] private int prewarmHighCloudCount = 16;
    [Min(1)][SerializeField] private int maxCreatesPerPrewarmCall = 24;

    [Header("Visual Refresh Performance")]
    [SerializeField] private bool enableQueuedVisualRefresh = true;
    [Min(1)][SerializeField] private int visualRefreshesPerFrame = 12;
    [Min(0f)][SerializeField] private float visualRefreshIntervalSeconds = 0f;

    [Header("Cloud Height Variation")]
    [SerializeField] private bool randomizeCloudHeight = true;
    [SerializeField] private float minCloudHeightOffset = -0.75f;
    [SerializeField] private float maxCloudHeightOffset = 1.25f;

    [Header("Cloud Row Batching")]
    [SerializeField] private bool batchCloudStateOverFrames = true;
    [Min(1)][SerializeField] private int cloudRowsPerFrame = 8;

    private Coroutine _cloudStepCoroutine;
    private bool _cloudAdvanceQueued;
    private bool _isAdvancingClouds;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = false;

    public event Action OnCloudGridInitialized;
    public event Action OnCloudStateChanged;

    public int Columns => _cols;
    public int Rows => _rows;
    public bool IsInitialized => _isInitialized;

    private int _cols;
    private int _rows;
    private bool _isInitialized;

    private CloudDensity[,] _cloudGrid;
    private CloudDensity[,] _nextCloudGrid;

    private float[,] _cloudHeightOffsets;
    private bool[,] _cloudHeightOffsetAssigned;

    private GameObject[,] _cloudVisuals;
    private GameObject[,] _cloudVisualPrefabs;

    private float[,] _externalCloudDarkness01;
    private float[,] _stormCloudDarkness01;

    private readonly Queue<Vector2Int> _pendingVisualRefreshes = new Queue<Vector2Int>();
    private readonly HashSet<int> _pendingVisualKeys = new HashSet<int>();
    private Coroutine _visualRefreshCoroutine;
    private WaitForSeconds _cachedRefreshWait;
    private float _cachedRefreshWaitSeconds = -1f;

    private Coroutine _waitForWeatherReadyCoroutine;
    private bool _seededFromValidWeather;

    private MaterialPropertyBlock _cloudTintBlock;

    private WeatherGridManager _subscribedWeatherGridManager;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsureLinks();
        EnsurePool();
    }

    private void OnEnable()
    {
        EnsureLinks();
        EnsurePool();
        RebindWeatherGridEvents();

        TurnSystem.SubscribeToStartOfTurn(HandleStartOfTurn);
        BeginWaitingForWeatherReady();
    }

    private void Start()
    {
        if (initializeOnStart)
            BeginWaitingForWeatherReady();
    }

    private void OnDisable()
    {
        TurnSystem.UnsubscribeFromStartOfTurn(HandleStartOfTurn);
        UnbindWeatherGridEvents();
        StopVisualRefreshRoutine();

        if (_waitForWeatherReadyCoroutine != null)
        {
            StopCoroutine(_waitForWeatherReadyCoroutine);
            _waitForWeatherReadyCoroutine = null;
        }

        if (_cloudStepCoroutine != null)
        {
            StopCoroutine(_cloudStepCoroutine);
            _cloudStepCoroutine = null;
        }

        _cloudAdvanceQueued = false;
        _isAdvancingClouds = false;
    }

    private void OnDestroy()
    {
        UnbindWeatherGridEvents();
        StopVisualRefreshRoutine();

        if (Instance == this)
            Instance = null;
    }

    private void BeginWaitingForWeatherReady()
    {
        if (_waitForWeatherReadyCoroutine != null)
            return;

        _waitForWeatherReadyCoroutine = StartCoroutine(WaitForWeatherReadyRoutine());
    }

    private IEnumerator WaitForWeatherReadyRoutine()
    {
        while (true)
        {
            EnsureLinks();
            EnsurePool();
            RebindWeatherGridEvents();

            if (TryInitializeGrid() && HasAnyValidWeatherCell())
            {
                if (seedCloudsOnInitialize && !_seededFromValidWeather)
                {
                    ReseedAllCloudsFromHumidity();
                    _seededFromValidWeather = true;
                }

                if (debugLogging)
                    Debug.Log("[CloudSimulationSystem] Weather is ready. Cloud system initialized.");

                _waitForWeatherReadyCoroutine = null;
                yield break;
            }

            yield return null;
        }
    }

    private bool HasAnyValidWeatherCell()
    {
        if (weatherGridManager == null || !weatherGridManager.IsInitialized)
            return false;

        int cols = weatherGridManager.Columns;
        int rows = weatherGridManager.Rows;

        for (int x = 0; x < cols; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                if (weatherGridManager.TryGetCellState(x, y, out WeatherCellState state) && state.isValid)
                    return true;
            }
        }

        return false;
    }

    public void InstallRuntimeRefs(
        GridManager newGridManager,
        WeatherGridManager newWeatherGridManager,
        Transform newCloudVisualRoot = null,
        CloudVisualPool newCloudPool = null,
        bool initializeNow = true)
    {
        if (newGridManager != null)
            gridManager = newGridManager;

        if (newWeatherGridManager != null)
            weatherGridManager = newWeatherGridManager;

        if (newCloudVisualRoot != null)
            cloudVisualRoot = newCloudVisualRoot;

        if (newCloudPool != null)
            cloudPool = newCloudPool;

        EnsurePool();
        RebindWeatherGridEvents();

        if (initializeNow)
            TryInitializeGrid();
    }

    public bool TryInitializeGrid()
    {
        EnsureLinks();
        EnsurePool();

        if (weatherGridManager == null || !weatherGridManager.IsInitialized)
            return false;

        if (gridManager == null)
            return false;

        int newCols = weatherGridManager.Columns;
        int newRows = weatherGridManager.Rows;

        if (newCols <= 0 || newRows <= 0)
            return false;

        bool wasInitialized = _isInitialized;
        bool sizeChanged = !wasInitialized || newCols != _cols || newRows != _rows;

        if (sizeChanged)
            ClearAllVisuals();

        _cols = newCols;
        _rows = newRows;

        if (sizeChanged)
        {
            _cloudGrid = new CloudDensity[_cols, _rows];
            _nextCloudGrid = new CloudDensity[_cols, _rows];
            _cloudVisuals = new GameObject[_cols, _rows];
            _cloudVisualPrefabs = new GameObject[_cols, _rows];
            _cloudHeightOffsets = new float[_cols, _rows];
            _cloudHeightOffsetAssigned = new bool[_cols, _rows];
            _externalCloudDarkness01 = new float[_cols, _rows];
            _stormCloudDarkness01 = new float[_cols, _rows];

            _volcanicSootGrid = new float[_cols, _rows];
            _nextVolcanicSootGrid = new float[_cols, _rows];
        }

        _isInitialized = true;

        if (sizeChanged && prewarmPoolsOnInitialize)
            PrewarmCloudPools();

        if (sizeChanged)
            OnCloudGridInitialized?.Invoke();

        if (debugLogging && sizeChanged)
            Debug.Log($"[CloudSimulationSystem] Initialized {_cols}x{_rows}");

        return true;
    }

    public void AdvanceCloudsOneStep()
    {
        if (!batchCloudStateOverFrames)
        {
            AdvanceCloudsOneStepImmediate();
            return;
        }

        if (_cloudStepCoroutine != null)
        {
            _cloudAdvanceQueued = true;
            return;
        }

        _cloudStepCoroutine = StartCoroutine(AdvanceCloudsOneStepBatchedRoutine());
    }

    private void BeginCloudStepBuffers()
    {
        Array.Clear(_nextCloudGrid, 0, _nextCloudGrid.Length);

        if (_nextVolcanicSootGrid != null)
            Array.Clear(_nextVolcanicSootGrid, 0, _nextVolcanicSootGrid.Length);
    }

    private void ProcessCloudCarryRows(int startY, int endY)
    {
        for (int y = startY; y < endY; y++)
        {
            for (int x = 0; x < _cols; x++)
            {
                CloudDensity current = _cloudGrid[x, y];
                if (current == CloudDensity.None)
                    continue;

                float currentHumidity = GetHumidity01(x, y);
                CloudDensity movedDensity = MaybeDissipate(current, currentHumidity);

                if (movedDensity == CloudDensity.None)
                    continue;

                Vector2Int target = GetWindTarget(x, y);
                if (!IsInBounds(target.x, target.y))
                    continue;

                float targetHumidity = GetHumidity01(target.x, target.y);
                movedDensity = AdjustDensityForTargetHumidity(movedDensity, targetHumidity);

                if (movedDensity == CloudDensity.None)
                    continue;

                MergeCloudInto(_nextCloudGrid, target.x, target.y, movedDensity);
            }
        }
    }

    private void ProcessCloudFormationRows(int startY, int endY)
    {
        for (int y = startY; y < endY; y++)
        {
            for (int x = 0; x < _cols; x++)
            {
                if (_nextCloudGrid[x, y] != CloudDensity.None)
                    continue;

                float humidity = GetHumidity01(x, y);
                if (humidity < lowCloudHumidityThreshold)
                    continue;

                bool hasNeighbour = HasCloudNeighbour(_nextCloudGrid, x, y) || HasCloudNeighbour(_cloudGrid, x, y);
                bool shouldForm = forceSeedCloudsInHumidCells ||
                                  UnityEngine.Random.value <= GetFormationChance(humidity, hasNeighbour);

                if (!shouldForm)
                    continue;

                _nextCloudGrid[x, y] = GetDensityFromHumidity(humidity);
            }
        }
    }

    private bool ApplyCloudStateRows(int startY, int endY)
    {
        bool anyChanged = false;

        for (int y = startY; y < endY; y++)
        {
            for (int x = 0; x < _cols; x++)
            {
                CloudDensity oldDensity = _cloudGrid[x, y];
                CloudDensity newDensity = _nextCloudGrid[x, y];

                if (oldDensity == newDensity)
                    continue;

                _cloudGrid[x, y] = newDensity;
                QueueCloudVisualRefresh(x, y);
                anyChanged = true;
            }
        }

        return anyChanged;
    }

    private void AdvanceCloudsOneStepImmediate()
    {
        if (_isAdvancingClouds)
            return;

        if (!TryInitializeGrid())
            return;

        _isAdvancingClouds = true;
        try
        {
            TryChangeWindDirection();

            int steps = Mathf.Max(1, simulationStepsPerAdvance);

            for (int i = 0; i < steps; i++)
            {
                BeginCloudStepBuffers();

                ProcessCloudCarryRows(0, _rows);
                ProcessVolcanicSootCarryRows(0, _rows);

                ProcessCloudFormationRows(0, _rows);

                ApplyCloudStateRows(0, _rows);
                ApplyVolcanicSootStateRows(0, _rows);
            }

            OnCloudStateChanged?.Invoke();

            if (debugLogging)
                Debug.Log("[CloudSimulationSystem] Advanced clouds one step.");
        }
        finally
        {
            _isAdvancingClouds = false;
        }
    }

    private IEnumerator AdvanceCloudsOneStepBatchedRoutine()
    {
        if (_isAdvancingClouds || !TryInitializeGrid())
        {
            _cloudStepCoroutine = null;
            yield break;
        }

        _isAdvancingClouds = true;
        int rowsPerFrame = Mathf.Max(1, cloudRowsPerFrame);

        try
        {
            TryChangeWindDirection();

            int steps = Mathf.Max(1, simulationStepsPerAdvance);

            for (int step = 0; step < steps; step++)
            {
                BeginCloudStepBuffers();

                for (int startY = 0; startY < _rows; startY += rowsPerFrame)
                {
                    ProcessCloudCarryRows(startY, Mathf.Min(startY + rowsPerFrame, _rows));

                    if (startY + rowsPerFrame < _rows)
                        yield return null;
                }

                for (int startY = 0; startY < _rows; startY += rowsPerFrame)
                {
                    ProcessCloudFormationRows(startY, Mathf.Min(startY + rowsPerFrame, _rows));

                    if (startY + rowsPerFrame < _rows)
                        yield return null;
                }

                for (int startY = 0; startY < _rows; startY += rowsPerFrame)
                {
                    ApplyCloudStateRows(startY, Mathf.Min(startY + rowsPerFrame, _rows));

                    if (startY + rowsPerFrame < _rows)
                        yield return null;
                }

                for (int startY = 0; startY < _rows; startY += rowsPerFrame)
                {
                    ProcessVolcanicSootCarryRows(startY, Mathf.Min(startY + rowsPerFrame, _rows));

                    if (startY + rowsPerFrame < _rows)
                        yield return null;
                }

                for (int startY = 0; startY < _rows; startY += rowsPerFrame)
                {
                    ApplyVolcanicSootStateRows(startY, Mathf.Min(startY + rowsPerFrame, _rows));

                    if (startY + rowsPerFrame < _rows)
                        yield return null;
                }
            }

            OnCloudStateChanged?.Invoke();

            if (debugLogging)
                Debug.Log("[CloudSimulationSystem] Advanced clouds one batched step.");
        }
        finally
        {
            _isAdvancingClouds = false;
            _cloudStepCoroutine = null;

            if (_cloudAdvanceQueued && isActiveAndEnabled)
            {
                _cloudAdvanceQueued = false;
                _cloudStepCoroutine = StartCoroutine(AdvanceCloudsOneStepBatchedRoutine());
            }
            else
            {
                _cloudAdvanceQueued = false;
            }
        }
    }

    private bool ProcessVolcanicSootCarryRows(int startY, int endY)
    {
        if (_volcanicSootGrid == null || _nextVolcanicSootGrid == null)
            return false;

        if (!enableVolcanicSootInput)
            return false;

        bool changedAny = false;
        float retention = Mathf.Clamp01(volcanicSootRetentionPerCloudStep);

        for (int y = startY; y < endY; y++)
        {
            for (int x = 0; x < _cols; x++)
            {
                float oldSoot = _volcanicSootGrid[x, y];

                if (oldSoot <= 0.001f)
                    continue;

                Vector2Int target = GetVolcanicSootWindTarget(x, y);

                float movedSoot = oldSoot * retention;

                if (movedSoot <= 0.01f)
                    continue;

                float before = _nextVolcanicSootGrid[target.x, target.y];
                float after = Mathf.Clamp01(before + movedSoot);

                if (Mathf.Abs(before - after) <= 0.001f)
                    continue;

                _nextVolcanicSootGrid[target.x, target.y] = after;
                changedAny = true;
            }
        }

        return changedAny;
    }

    private bool ApplyVolcanicSootStateRows(int startY, int endY)
    {
        if (_volcanicSootGrid == null || _nextVolcanicSootGrid == null)
            return false;

        bool changedAny = false;

        for (int y = startY; y < endY; y++)
        {
            for (int x = 0; x < _cols; x++)
            {
                float oldSoot = _volcanicSootGrid[x, y];
                float newSoot = _nextVolcanicSootGrid[x, y];

                if (newSoot <= 0.01f)
                    newSoot = 0f;

                if (Mathf.Abs(oldSoot - newSoot) <= 0.001f)
                    continue;

                _volcanicSootGrid[x, y] = newSoot;

                if (newSoot > 0.001f && volcanicSootCanCreateClouds && _cloudGrid != null)
                {
                    CloudDensity current = _cloudGrid[x, y];

                    if ((int)current < (int)minimumVolcanicCloudDensity)
                        _cloudGrid[x, y] = minimumVolcanicCloudDensity;
                }

                QueueCloudVisualRefresh(x, y);
                changedAny = true;
            }
        }

        return changedAny;
    }

    private Vector2Int GetVolcanicSootWindTarget(int x, int y)
    {
        Vector2Int dir = GetWindOffset(windDirection);

        if (dir == Vector2Int.zero || windSpeedTilesPerStep <= 0)
            return new Vector2Int(x, y);

        Vector2Int target = new Vector2Int(
            x + dir.x * windSpeedTilesPerStep,
            y + dir.y * windSpeedTilesPerStep);

        if (!IsInBounds(target.x, target.y))
            return new Vector2Int(x, y);

        return target;
    }

    public void ReseedAllCloudsFromHumidity()
    {
        if (!TryInitializeGrid())
            return;

        Array.Clear(_nextCloudGrid, 0, _nextCloudGrid.Length);

        int seededCount = 0;

        for (int x = 0; x < _cols; x++)
        {
            for (int y = 0; y < _rows; y++)
            {
                float humidity = GetHumidity01(x, y);
                if (humidity < lowCloudHumidityThreshold)
                    continue;

                if (!forceSeedCloudsInHumidCells)
                {
                    float chance = GetFormationChance(humidity, false);
                    if (UnityEngine.Random.value > chance)
                        continue;
                }

                CloudDensity density = GetDensityFromHumidity(humidity);
                _nextCloudGrid[x, y] = density;

                if (density != CloudDensity.None)
                    seededCount++;
            }
        }

        ApplyCloudState(_nextCloudGrid);

        if (debugLogging)
            Debug.Log($"[CloudSimulationSystem] Reseeded clouds. Seeded={seededCount}");
    }

    public void TopUpCloudsFromHumidity()
    {
        if (!TryInitializeGrid())
            return;

        Array.Clear(_nextCloudGrid, 0, _nextCloudGrid.Length);

        for (int x = 0; x < _cols; x++)
        {
            for (int y = 0; y < _rows; y++)
                _nextCloudGrid[x, y] = _cloudGrid[x, y];
        }

        int added = 0;

        for (int x = 0; x < _cols; x++)
        {
            for (int y = 0; y < _rows; y++)
            {
                if (_nextCloudGrid[x, y] != CloudDensity.None)
                    continue;

                float humidity = GetHumidity01(x, y);
                if (humidity < lowCloudHumidityThreshold)
                    continue;

                bool hasNeighbour = HasCloudNeighbour(_nextCloudGrid, x, y) || HasCloudNeighbour(_cloudGrid, x, y);

                if (!forceSeedCloudsInHumidCells)
                {
                    float chance = GetFormationChance(humidity, hasNeighbour);
                    if (UnityEngine.Random.value > chance)
                        continue;
                }

                CloudDensity density = GetDensityFromHumidity(humidity);
                _nextCloudGrid[x, y] = density;

                if (density != CloudDensity.None)
                    added++;
            }
        }

        ApplyCloudState(_nextCloudGrid);

        if (debugLogging)
            Debug.Log($"[CloudSimulationSystem] Top-up added {added} clouds.");
    }

    public void ClearAllClouds()
    {
        if (!TryInitializeGrid())
            return;

        Array.Clear(_nextCloudGrid, 0, _nextCloudGrid.Length);
        ApplyCloudState(_nextCloudGrid);

        if (debugLogging)
            Debug.Log("[CloudSimulationSystem] Cleared all clouds.");
    }

    public CloudDensity GetCloudDensityAtCell(int x, int y)
    {
        if (!_isInitialized || !IsInBounds(x, y) || _cloudGrid == null)
            return CloudDensity.None;

        return _cloudGrid[x, y];
    }

    public bool HasCloudAtCell(int x, int y)
    {
        return GetCloudDensityAtCell(x, y) != CloudDensity.None;
    }

    public float GetCloudCoverage01AtCell(int x, int y)
    {
        switch (GetCloudDensityAtCell(x, y))
        {
            case CloudDensity.Low: return 0.33f;
            case CloudDensity.Mid: return 0.66f;
            case CloudDensity.High: return 1f;
            default: return 0f;
        }
    }

    public Vector2Int GetCurrentWindOffset()
    {
        return GetWindOffset(windDirection);
    }

    public bool TryGetCloudWorldPosition(int x, int y, out Vector3 worldPosition)
    {
        worldPosition = Vector3.zero;

        if (!_isInitialized || !IsInBounds(x, y) || gridManager == null || weatherGridManager == null)
            return false;

        Vector3 cellCorner = gridManager.GetWorldPosition(x, y);

        float height = weatherGridManager.WeatherGridBaseHeight + GetOrCreateCloudHeightOffset(x, y);

        worldPosition = new Vector3(
            cellCorner.x + (gridManager.cellSize * 0.5f),
            height,
            cellCorner.z + (gridManager.cellSize * 0.5f));

        return true;
    }

    private void HandleStartOfTurn()
    {
        if (!advanceWithTurnSystem)
            return;

        AdvanceCloudsOneStep();
    }

    private void HandleWeatherGridInitialized()
    {
        TryInitializeGrid();
        BeginWaitingForWeatherReady();
    }

    private void HandleWeatherStateRefreshed()
    {
        if (!TryInitializeGrid())
            return;

        if (!HasAnyValidWeatherCell())
            return;

        if (seedCloudsOnInitialize && !_seededFromValidWeather)
        {
            ReseedAllCloudsFromHumidity();
            _seededFromValidWeather = true;
            return;
        }

        if (topUpCloudsOnWeatherStateRefreshed)
            TopUpCloudsFromHumidity();
    }

    private void SimulateSingleStep()
    {
        Array.Clear(_nextCloudGrid, 0, _nextCloudGrid.Length);

        // Pass 1: move and transform existing clouds.
        for (int x = 0; x < _cols; x++)
        {
            for (int y = 0; y < _rows; y++)
            {
                CloudDensity current = _cloudGrid[x, y];
                if (current == CloudDensity.None)
                    continue;

                float currentHumidity = GetHumidity01(x, y);
                CloudDensity movedDensity = MaybeDissipate(current, currentHumidity);

                if (movedDensity == CloudDensity.None)
                    continue;

                Vector2Int target = GetWindTarget(x, y);
                if (!IsInBounds(target.x, target.y))
                    continue;

                float targetHumidity = GetHumidity01(target.x, target.y);
                movedDensity = AdjustDensityForTargetHumidity(movedDensity, targetHumidity);

                if (movedDensity == CloudDensity.None)
                    continue;

                MergeCloudInto(_nextCloudGrid, target.x, target.y, movedDensity);
            }
        }

        // Pass 2: form new clouds in humid empty cells.
        for (int x = 0; x < _cols; x++)
        {
            for (int y = 0; y < _rows; y++)
            {
                if (_nextCloudGrid[x, y] != CloudDensity.None)
                    continue;

                float humidity = GetHumidity01(x, y);
                if (humidity < lowCloudHumidityThreshold)
                    continue;

                bool hasNeighbour = HasCloudNeighbour(_nextCloudGrid, x, y) || HasCloudNeighbour(_cloudGrid, x, y);
                bool shouldForm = forceSeedCloudsInHumidCells ||
                                  UnityEngine.Random.value <= GetFormationChance(humidity, hasNeighbour);

                if (!shouldForm)
                    continue;

                _nextCloudGrid[x, y] = GetDensityFromHumidity(humidity);
            }
        }

        ApplyCloudState(_nextCloudGrid);
    }

    private void ApplyCloudState(CloudDensity[,] nextGrid)
    {
        for (int x = 0; x < _cols; x++)
        {
            for (int y = 0; y < _rows; y++)
            {
                CloudDensity oldDensity = _cloudGrid[x, y];
                CloudDensity newDensity = nextGrid[x, y];

                if (oldDensity == newDensity)
                    continue;

                _cloudGrid[x, y] = newDensity;
                QueueCloudVisualRefresh(x, y);
            }
        }
    }

    private void EnsureVisualRefreshRoutine()
    {
        if (_visualRefreshCoroutine != null || !isActiveAndEnabled)
            return;

        _visualRefreshCoroutine = StartCoroutine(VisualRefreshRoutine());
    }

    private IEnumerator VisualRefreshRoutine()
    {
        while (_pendingVisualRefreshes.Count > 0)
        {
            int refreshedThisBatch = 0;
            int maxRefreshes = Mathf.Max(1, visualRefreshesPerFrame);

            while (_pendingVisualRefreshes.Count > 0 && refreshedThisBatch < maxRefreshes)
            {
                Vector2Int cell = _pendingVisualRefreshes.Dequeue();
                int key = GetCellKey(cell.x, cell.y);
                _pendingVisualKeys.Remove(key);

                if (!_isInitialized || !IsInBounds(cell.x, cell.y))
                    continue;

                RefreshCloudVisualAtCell(cell.x, cell.y, _cloudGrid[cell.x, cell.y]);
                refreshedThisBatch++;
            }

            if (_pendingVisualRefreshes.Count > 0)
            {
                float interval = Mathf.Max(0f, visualRefreshIntervalSeconds);

                if (interval > 0f)
                {
                    if (_cachedRefreshWait == null || Mathf.Abs(_cachedRefreshWaitSeconds - interval) > 0.0001f)
                    {
                        _cachedRefreshWait = new WaitForSeconds(interval);
                        _cachedRefreshWaitSeconds = interval;
                    }

                    yield return _cachedRefreshWait;
                }
                else
                {
                    yield return null;
                }
            }
        }

        _visualRefreshCoroutine = null;
        _cachedRefreshWait = null;
        _cachedRefreshWaitSeconds = -1f;
    }

    private void RefreshCloudVisualAtCell(int x, int y, CloudDensity density)
    {
        EnsurePool();

        GameObject desiredPrefab = GetCloudPrefab(density);

        if (_cloudVisuals[x, y] != null && _cloudVisualPrefabs[x, y] != desiredPrefab)
            ReturnCloudVisualAtCell(x, y);

        if (desiredPrefab == null)
            return;

        if (cloudVisualRoot == null)
        {
            GameObject root = new GameObject("Cloud Visual Root");
            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            cloudVisualRoot = root.transform;
        }

        if (!TryGetCloudWorldPosition(x, y, out Vector3 worldPos))
            return;

        if (_cloudVisuals[x, y] == null)
        {
            Quaternion rotation = Quaternion.identity;

            if (randomizeCloudRotation)
            {
                rotation = Quaternion.Euler(
                    UnityEngine.Random.Range(minCloudRotation.x, maxCloudRotation.x),
                    UnityEngine.Random.Range(minCloudRotation.y, maxCloudRotation.y),
                    UnityEngine.Random.Range(minCloudRotation.z, maxCloudRotation.z));
            }

            GameObject instance = cloudPool.Get(desiredPrefab, cloudVisualRoot, worldPos, rotation);
            instance.name = $"Cloud_{density}_{x}_{y}";

            _cloudVisuals[x, y] = instance;
            _cloudVisualPrefabs[x, y] = desiredPrefab;
        }
        else
        {
            _cloudVisuals[x, y].name = $"Cloud_{density}_{x}_{y}";
        }

        Transform tr = _cloudVisuals[x, y].transform;
        tr.SetParent(cloudVisualRoot, true);
        tr.position = worldPos;

        float soot01 = GetVolcanicSoot01AtCell(x, y);

        float normalDarkness = Mathf.Clamp01(
            GetCloudDarkness01(density) +
            GetExternalCloudDarkness01AtCell(x, y) +
            GetStormCloudDarkness01AtCell(x, y));

        Color tint = Color.Lerp(cloudBrightColor, cloudDarkColor, normalDarkness);

        // Volcanic soot should not just make clouds grey.
        // It should push them toward black ash clouds.
        if (soot01 > 0.001f)
        {
            float sootBlackT = Mathf.Clamp01(soot01 * volcanicSootDarknessStrength * volcanicSootBlackTintStrength);
            tint = Color.Lerp(tint, volcanicSootCloudColor, sootBlackT);
        }

        ApplyCloudTint(_cloudVisuals[x, y], tint);
    }

    private void ReturnCloudVisualAtCell(int x, int y)
    {
        if (_cloudVisuals == null || _cloudVisualPrefabs == null)
            return;

        GameObject instance = _cloudVisuals[x, y];
        GameObject prefab = _cloudVisualPrefabs[x, y];

        if (instance != null && cloudPool != null)
            cloudPool.Return(prefab, instance);

        _cloudVisuals[x, y] = null;
        _cloudVisualPrefabs[x, y] = null;

        ClearCloudHeightOffset(x, y);
    }

    private void ClearAllVisuals()
    {
        StopVisualRefreshRoutine();

        if (_cloudVisuals == null)
            return;

        for (int x = 0; x < _cloudVisuals.GetLength(0); x++)
        {
            for (int y = 0; y < _cloudVisuals.GetLength(1); y++)
                ReturnCloudVisualAtCell(x, y);
        }
    }

    private void StopVisualRefreshRoutine()
    {
        _pendingVisualRefreshes.Clear();
        _pendingVisualKeys.Clear();

        if (_visualRefreshCoroutine != null)
        {
            StopCoroutine(_visualRefreshCoroutine);
            _visualRefreshCoroutine = null;
        }

        _cachedRefreshWait = null;
        _cachedRefreshWaitSeconds = -1f;
    }

    private void PrewarmCloudPools()
    {
        EnsurePool();

        Transform parent = cloudVisualRoot != null ? cloudVisualRoot : transform;

        if (lowCloudPrefab != null)
            cloudPool.Prewarm(lowCloudPrefab, prewarmLowCloudCount, parent, maxCreatesPerPrewarmCall);

        if (midCloudPrefab != null)
            cloudPool.Prewarm(midCloudPrefab, prewarmMidCloudCount, parent, maxCreatesPerPrewarmCall);

        if (highCloudPrefab != null)
            cloudPool.Prewarm(highCloudPrefab, prewarmHighCloudCount, parent, maxCreatesPerPrewarmCall);
    }

    private void EnsurePool()
    {
        if (cloudPool != null)
            return;

        GameObject go = new GameObject("Cloud Visual Pool");
        go.transform.SetParent(transform, false);
        cloudPool = go.AddComponent<CloudVisualPool>();
    }

    private void EnsureLinks()
    {
        if (gridManager == null)
            gridManager = GridManager.Instance;

        if (weatherGridManager == null)
            weatherGridManager = WeatherGridManager.Instance;
    }

    private void RebindWeatherGridEvents()
    {
        if (_subscribedWeatherGridManager == weatherGridManager)
            return;

        UnbindWeatherGridEvents();

        _subscribedWeatherGridManager = weatherGridManager;

        if (_subscribedWeatherGridManager != null)
        {
            _subscribedWeatherGridManager.OnWeatherGridInitialized += HandleWeatherGridInitialized;
            _subscribedWeatherGridManager.OnWeatherStateRefreshed += HandleWeatherStateRefreshed;
        }
    }

    private void UnbindWeatherGridEvents()
    {
        if (_subscribedWeatherGridManager == null)
            return;

        _subscribedWeatherGridManager.OnWeatherGridInitialized -= HandleWeatherGridInitialized;
        _subscribedWeatherGridManager.OnWeatherStateRefreshed -= HandleWeatherStateRefreshed;
        _subscribedWeatherGridManager = null;
    }

    private float GetHumidity01(int x, int y)
    {
        if (weatherGridManager == null)
            return 0f;

        return weatherGridManager.TryGetCellState(x, y, out WeatherCellState state)
            ? Mathf.Clamp01(state.humidity01)
            : 0f;
    }

    private float GetFormationChance(float humidity01, bool hasNeighbour)
    {
        if (humidity01 < lowCloudHumidityThreshold)
            return 0f;

        float t = Mathf.InverseLerp(lowCloudHumidityThreshold, 1f, humidity01);
        float chance = baseFormationChance * t;

        if (hasNeighbour)
            chance += neighbourFormationBonus * t;

        return Mathf.Clamp01(chance);
    }

    private CloudDensity GetDensityFromHumidity(float humidity01)
    {
        if (humidity01 >= highCloudHumidityThreshold)
            return CloudDensity.High;

        if (humidity01 >= midCloudHumidityThreshold)
            return CloudDensity.Mid;

        if (humidity01 >= lowCloudHumidityThreshold)
            return CloudDensity.Low;

        return CloudDensity.None;
    }

    private CloudDensity MaybeDissipate(CloudDensity current, float humidity01)
    {
        if (current == CloudDensity.None)
            return CloudDensity.None;

        if (humidity01 >= lowCloudHumidityThreshold)
            return current;

        float dryness = Mathf.InverseLerp(lowCloudHumidityThreshold, 0f, humidity01);
        float densityFactor = (int)current / 3f;
        float chance = dryness * densityFactor * dryDissipationChanceMultiplier;

        if (UnityEngine.Random.value <= chance)
            return StepDown(current);

        return current;
    }

    private CloudDensity AdjustDensityForTargetHumidity(CloudDensity current, float humidity01)
    {
        if (current == CloudDensity.None)
            return CloudDensity.None;

        if (humidity01 >= highCloudHumidityThreshold && UnityEngine.Random.value <= humidGrowthChanceMultiplier)
            current = StepUp(current);

        if (humidity01 < lowCloudHumidityThreshold * 0.85f && UnityEngine.Random.value <= 0.25f)
            current = StepDown(current);

        return current;
    }

    private void TryChangeWindDirection()
    {
        if (UnityEngine.Random.value > windDirectionChangeChancePerTurn)
            return;

        WindDirection8 oldDirection = windDirection;
        windDirection = GetRandomWindDirection(allowWindDirectionToStaySame ? (WindDirection8?)null : oldDirection);

        if (debugLogging)
            Debug.Log($"[CloudSimulationSystem] Wind direction changed: {oldDirection} -> {windDirection}");
    }

    private WindDirection8 GetRandomWindDirection(WindDirection8? excludeDirection = null)
    {
        WindDirection8[] validDirections =
        {
            WindDirection8.North,
            WindDirection8.NorthEast,
            WindDirection8.East,
            WindDirection8.SouthEast,
            WindDirection8.South,
            WindDirection8.SouthWest,
            WindDirection8.West,
            WindDirection8.NorthWest
        };

        if (excludeDirection == null)
            return validDirections[UnityEngine.Random.Range(0, validDirections.Length)];

        WindDirection8 chosen;
        do
        {
            chosen = validDirections[UnityEngine.Random.Range(0, validDirections.Length)];
        }
        while (chosen == excludeDirection.Value);

        return chosen;
    }

    private Vector2Int GetWindTarget(int x, int y)
    {
        Vector2Int dir = GetWindOffset(windDirection);

        if (dir == Vector2Int.zero || windSpeedTilesPerStep <= 0)
            return new Vector2Int(x, y);

        Vector2Int target = new Vector2Int(
            x + dir.x * windSpeedTilesPerStep,
            y + dir.y * windSpeedTilesPerStep);

        if (UnityEngine.Random.value <= lateralShuffleChance)
        {
            Vector2Int leftPerp = new Vector2Int(-dir.y, dir.x);
            Vector2Int rightPerp = new Vector2Int(dir.y, -dir.x);
            Vector2Int lateral = UnityEngine.Random.value < 0.5f ? leftPerp : rightPerp;
            lateral.x = Mathf.Clamp(lateral.x, -1, 1);
            lateral.y = Mathf.Clamp(lateral.y, -1, 1);

            target += lateral;
        }

        return target;
    }

    private Vector2Int GetWindOffset(WindDirection8 direction)
    {
        switch (direction)
        {
            case WindDirection8.North: return new Vector2Int(0, 1);
            case WindDirection8.NorthEast: return new Vector2Int(1, 1);
            case WindDirection8.East: return new Vector2Int(1, 0);
            case WindDirection8.SouthEast: return new Vector2Int(1, -1);
            case WindDirection8.South: return new Vector2Int(0, -1);
            case WindDirection8.SouthWest: return new Vector2Int(-1, -1);
            case WindDirection8.West: return new Vector2Int(-1, 0);
            case WindDirection8.NorthWest: return new Vector2Int(-1, 1);
            default: return Vector2Int.zero;
        }
    }

    private void MergeCloudInto(CloudDensity[,] targetGrid, int x, int y, CloudDensity incoming)
    {
        CloudDensity existing = targetGrid[x, y];

        if (existing == CloudDensity.None)
        {
            targetGrid[x, y] = incoming;
            return;
        }

        if (existing == incoming)
            targetGrid[x, y] = StepUp(existing);
        else
            targetGrid[x, y] = (CloudDensity)Mathf.Max((int)existing, (int)incoming);
    }

    private CloudDensity StepUp(CloudDensity density)
    {
        int value = Mathf.Clamp((int)density + 1, 0, (int)CloudDensity.High);
        return (CloudDensity)value;
    }

    private CloudDensity StepDown(CloudDensity density)
    {
        int value = Mathf.Clamp((int)density - 1, 0, (int)CloudDensity.High);
        return (CloudDensity)value;
    }

    private bool HasCloudNeighbour(CloudDensity[,] grid, int x, int y)
    {
        for (int ox = -1; ox <= 1; ox++)
        {
            for (int oy = -1; oy <= 1; oy++)
            {
                if (ox == 0 && oy == 0)
                    continue;

                int nx = x + ox;
                int ny = y + oy;

                if (!IsInBounds(nx, ny))
                    continue;

                if (grid[nx, ny] != CloudDensity.None)
                    return true;
            }
        }

        return false;
    }

    private GameObject GetCloudPrefab(CloudDensity density)
    {
        switch (density)
        {
            case CloudDensity.Low: return lowCloudPrefab;
            case CloudDensity.Mid: return midCloudPrefab;
            case CloudDensity.High: return highCloudPrefab;
            default: return null;
        }
    }

    private float GetCloudDarkness01(CloudDensity density)
    {
        switch (density)
        {
            case CloudDensity.Low: return 0.20f;
            case CloudDensity.Mid: return 0.35f;
            case CloudDensity.High: return 0.50f;
            default: return 0f;
        }
    }

    private void ApplyCloudTint(GameObject cloudInstance, Color tint)
    {
        if (cloudInstance == null)
            return;

        SpriteRenderer[] spriteRenderers = cloudInstance.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < spriteRenderers.Length; i++)
            spriteRenderers[i].color = tint;

        Renderer[] renderers = cloudInstance.GetComponentsInChildren<Renderer>(true);

        if (_cloudTintBlock == null)
            _cloudTintBlock = new MaterialPropertyBlock();

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer is SpriteRenderer)
                continue;

            renderer.GetPropertyBlock(_cloudTintBlock);
            _cloudTintBlock.SetColor("_Color", tint);
            _cloudTintBlock.SetColor("_BaseColor", tint);
            renderer.SetPropertyBlock(_cloudTintBlock);
        }
    }

    private int GetCellKey(int x, int y)
    {
        return x + (y * Mathf.Max(1, _cols));
    }

    private bool IsInBounds(int x, int y)
    {
        return x >= 0 && x < _cols && y >= 0 && y < _rows;
    }

    private float GetOrCreateCloudHeightOffset(int x, int y)
    {
        if (!randomizeCloudHeight || !IsInBounds(x, y))
            return 0f;

        if (!_cloudHeightOffsetAssigned[x, y])
        {
            _cloudHeightOffsets[x, y] = UnityEngine.Random.Range(minCloudHeightOffset, maxCloudHeightOffset);
            _cloudHeightOffsetAssigned[x, y] = true;
        }

        return _cloudHeightOffsets[x, y];
    }

    private void ClearCloudHeightOffset(int x, int y)
    {
        if (_cloudHeightOffsets == null || _cloudHeightOffsetAssigned == null || !IsInBounds(x, y))
            return;

        _cloudHeightOffsets[x, y] = 0f;
        _cloudHeightOffsetAssigned[x, y] = false;
    }

    public float GetExternalCloudDarkness01AtCell(int x, int y)
    {
        if (_externalCloudDarkness01 == null || !IsInBounds(x, y))
            return 0f;

        if (_externalCloudDarkness01.GetLength(0) != _cols || _externalCloudDarkness01.GetLength(1) != _rows)
            return 0f;

        return Mathf.Clamp01(_externalCloudDarkness01[x, y]);
    }

    public void SetExternalCloudDarkness01AtCell(int x, int y, float value)
    {
        if (_externalCloudDarkness01 == null || !IsInBounds(x, y))
            return;

        if (_externalCloudDarkness01.GetLength(0) != _cols || _externalCloudDarkness01.GetLength(1) != _rows)
            return;

        float clamped = Mathf.Clamp01(value);
        if (Mathf.Abs(_externalCloudDarkness01[x, y] - clamped) <= 0.01f)
            return;

        _externalCloudDarkness01[x, y] = clamped;
        QueueCloudVisualRefresh(x, y);
    }

    public void ClearAllExternalCloudDarkness()
    {
        if (_externalCloudDarkness01 == null)
            return;

        if (_externalCloudDarkness01.GetLength(0) != _cols || _externalCloudDarkness01.GetLength(1) != _rows)
            return;

        for (int x = 0; x < _cols; x++)
        {
            for (int y = 0; y < _rows; y++)
            {
                if (_externalCloudDarkness01[x, y] <= 0.01f)
                    continue;

                _externalCloudDarkness01[x, y] = 0f;
                QueueCloudVisualRefresh(x, y);
            }
        }
    }

    public bool TrySetCloudDensityAtCell(int x, int y, CloudDensity density)
    {
        if (!_isInitialized || !IsInBounds(x, y) || _cloudGrid == null)
            return false;

        CloudDensity clamped = (CloudDensity)Mathf.Clamp((int)density, 0, (int)CloudDensity.High);
        if (_cloudGrid[x, y] == clamped)
            return false;

        _cloudGrid[x, y] = clamped;

        if (clamped == CloudDensity.None)
            ClearCloudHeightOffset(x, y);

        QueueCloudVisualRefresh(x, y);
        return true;
    }

    public Vector2Int GetWindTargetForCell(int x, int y)
    {
        if (!_isInitialized || !IsInBounds(x, y))
            return new Vector2Int(x, y);

        return GetWindTarget(x, y);
    }

    public float GetStormCloudDarkness01AtCell(int x, int y)
    {
        if (_stormCloudDarkness01 == null || !IsInBounds(x, y))
            return 0f;

        if (_stormCloudDarkness01.GetLength(0) != _cols || _stormCloudDarkness01.GetLength(1) != _rows)
            return 0f;

        return Mathf.Clamp01(_stormCloudDarkness01[x, y]);
    }

    public void SetStormCloudDarkness01AtCell(int x, int y, float value)
    {
        if (_stormCloudDarkness01 == null || !IsInBounds(x, y))
            return;

        if (_stormCloudDarkness01.GetLength(0) != _cols || _stormCloudDarkness01.GetLength(1) != _rows)
            return;

        float clamped = Mathf.Clamp01(value);
        if (Mathf.Abs(_stormCloudDarkness01[x, y] - clamped) <= 0.01f)
            return;

        _stormCloudDarkness01[x, y] = clamped;
        QueueCloudVisualRefresh(x, y);
    }

    public void ClearAllStormCloudDarkness()
    {
        if (_stormCloudDarkness01 == null)
            return;

        if (_stormCloudDarkness01.GetLength(0) != _cols || _stormCloudDarkness01.GetLength(1) != _rows)
            return;

        for (int x = 0; x < _cols; x++)
        {
            for (int y = 0; y < _rows; y++)
            {
                if (_stormCloudDarkness01[x, y] <= 0.01f)
                    continue;

                _stormCloudDarkness01[x, y] = 0f;
                QueueCloudVisualRefresh(x, y);
            }
        }
    }

    private bool ShouldShowCloudVisualAtCell(int x, int y)
    {
        if (!_isInitialized || !IsInBounds(x, y))
            return false;

        return GetCloudPrefab(_cloudGrid[x, y]) != null;
    }

    private void QueueCloudVisualRefresh(int x, int y)
    {
        if (!IsInBounds(x, y))
            return;

        bool shouldShow = ShouldShowCloudVisualAtCell(x, y);
        bool hasVisual = _cloudVisuals != null && _cloudVisuals[x, y] != null;

        if (!shouldShow && !hasVisual)
            return;

        if (!enableQueuedVisualRefresh)
        {
            RefreshCloudVisualAtCell(x, y, _cloudGrid[x, y]);
            return;
        }

        int key = GetCellKey(x, y);
        if (!_pendingVisualKeys.Add(key))
            return;

        _pendingVisualRefreshes.Enqueue(new Vector2Int(x, y));
        EnsureVisualRefreshRoutine();
    }

    public bool AddVolcanicSootPlume(int originX, int originY, float amount, int plumeLength)
    {
        if (!enableVolcanicSootInput)
            return false;

        if (amount <= 0f)
            return false;

        if (!TryInitializeGrid())
            return false;

        if (_volcanicSootGrid == null)
            _volcanicSootGrid = new float[_cols, _rows];

        int length = Mathf.Max(1, plumeLength);

        // Use cloud wind direction so volcanic ash/soot follows your existing cloud system.
        Vector2Int wind = GetCurrentWindOffset();
        if (wind == Vector2Int.zero)
            wind = Vector2Int.up;

        bool changedAny = false;

        for (int i = 0; i < length; i++)
        {
            int x = originX + wind.x * i;
            int y = originY + wind.y * i;

            if (!IsInBounds(x, y))
                continue;

            float falloff = 1f - (i / (float)length);
            float cellAmount = amount * Mathf.Clamp01(falloff);

            if (cellAmount <= 0f)
                continue;

            AddVolcanicSootAtCell(x, y, cellAmount);
            changedAny = true;
        }

        if (changedAny)
            OnCloudStateChanged?.Invoke();

        return changedAny;
    }

    public bool AddVolcanicSootStamp(
    IReadOnlyList<TileCoord> originCells,
    float totalSootAmount,
    int stampRadius,
    float maxAddPerCell,
    float falloffPerCell)
    {
        if (!enableVolcanicSootInput)
            return false;

        if (originCells == null || originCells.Count == 0)
            return false;

        if (totalSootAmount <= 0f)
            return false;

        if (!TryInitializeGrid())
            return false;

        if (_volcanicSootGrid == null)
            _volcanicSootGrid = new float[_cols, _rows];

        int radius = Mathf.Max(0, stampRadius);
        float maxCellAdd = Mathf.Clamp01(maxAddPerCell);
        float falloff = Mathf.Clamp01(falloffPerCell);

        bool changedAny = false;

        float baseAmountPerOrigin = totalSootAmount / Mathf.Max(1, originCells.Count);

        for (int i = 0; i < originCells.Count; i++)
        {
            TileCoord origin = originCells[i];

            for (int ox = -radius; ox <= radius; ox++)
            {
                for (int oy = -radius; oy <= radius; oy++)
                {
                    int manhattan = Mathf.Abs(ox) + Mathf.Abs(oy);

                    if (manhattan > radius)
                        continue;

                    int x = origin.x + ox;
                    int y = origin.y + oy;

                    if (!IsInBounds(x, y))
                        continue;

                    float ringMultiplier = Mathf.Clamp01(1f - manhattan * falloff);

                    if (ringMultiplier <= 0f)
                        continue;

                    float amountForCell = baseAmountPerOrigin * ringMultiplier;
                    amountForCell = Mathf.Min(amountForCell, maxCellAdd);

                    if (AddVolcanicSootAtCellCapped(x, y, amountForCell))
                        changedAny = true;
                }
            }
        }

        if (changedAny)
            OnCloudStateChanged?.Invoke();

        return changedAny;
    }

    private bool AddVolcanicSootAtCellCapped(int x, int y, float amount)
    {
        if (!enableVolcanicSootInput)
            return false;

        if (amount <= 0f)
            return false;

        if (!IsInBounds(x, y))
            return false;

        if (_volcanicSootGrid == null)
            _volcanicSootGrid = new float[_cols, _rows];

        float oldSoot = _volcanicSootGrid[x, y];
        float newSoot = Mathf.Clamp01(oldSoot + amount);

        if (Mathf.Abs(oldSoot - newSoot) <= 0.001f)
            return false;

        _volcanicSootGrid[x, y] = newSoot;

        if (volcanicSootCanCreateClouds)
        {
            CloudDensity current = _cloudGrid[x, y];

            if ((int)current < (int)minimumVolcanicCloudDensity)
                _cloudGrid[x, y] = minimumVolcanicCloudDensity;
        }

        QueueCloudVisualRefresh(x, y);
        return true;
    }

    public void AddVolcanicSootAtCell(int x, int y, float amount)
    {
        if (!enableVolcanicSootInput)
            return;

        if (amount <= 0f)
            return;

        if (!TryInitializeGrid())
            return;

        if (!IsInBounds(x, y))
            return;

        if (_volcanicSootGrid == null)
            _volcanicSootGrid = new float[_cols, _rows];

        float oldSoot = _volcanicSootGrid[x, y];
        float newSoot = Mathf.Clamp01(oldSoot + amount);

        if (Mathf.Abs(oldSoot - newSoot) <= 0.001f)
            return;

        _volcanicSootGrid[x, y] = newSoot;

        if (volcanicSootCanCreateClouds)
        {
            CloudDensity current = _cloudGrid[x, y];

            if ((int)current < (int)minimumVolcanicCloudDensity)
                _cloudGrid[x, y] = minimumVolcanicCloudDensity;
        }

        QueueCloudVisualRefresh(x, y);
    }

    public float GetVolcanicSoot01AtCell(int x, int y)
    {
        if (_volcanicSootGrid == null || !IsInBounds(x, y))
            return 0f;

        return Mathf.Clamp01(_volcanicSootGrid[x, y]);
    }

    public bool HasVolcanicSootAtCell(int x, int y, float threshold = 0.05f)
    {
        return GetVolcanicSoot01AtCell(x, y) >= threshold;
    }

    public void ClearVolcanicSootAtCell(int x, int y)
    {
        if (_volcanicSootGrid == null || !IsInBounds(x, y))
            return;

        if (_volcanicSootGrid[x, y] <= 0f)
            return;

        _volcanicSootGrid[x, y] = 0f;
        QueueCloudVisualRefresh(x, y);
    }

    public void ClearAllVolcanicSoot()
    {
        if (_volcanicSootGrid == null)
            return;

        for (int x = 0; x < _cols; x++)
        {
            for (int y = 0; y < _rows; y++)
            {
                if (_volcanicSootGrid[x, y] <= 0f)
                    continue;

                _volcanicSootGrid[x, y] = 0f;
                QueueCloudVisualRefresh(x, y);
            }
        }

        OnCloudStateChanged?.Invoke();
    }

    public CloudSimulationSaveData SaveState()
    {
        CloudSimulationSaveData data = new CloudSimulationSaveData
        {
            windDirectionValue = (int)windDirection,
            windSpeedTilesPerStep = windSpeedTilesPerStep,
            seededFromValidWeather = _seededFromValidWeather
        };

        if (!_isInitialized || _cloudGrid == null)
            return data;

        for (int x = 0; x < _cols; x++)
        {
            for (int y = 0; y < _rows; y++)
            {
                CloudDensity density = _cloudGrid[x, y];

                float volcanicSoot = _volcanicSootGrid != null
                    ? Mathf.Clamp01(_volcanicSootGrid[x, y])
                    : 0f;

                float externalDarkness = _externalCloudDarkness01 != null
                    ? Mathf.Clamp01(_externalCloudDarkness01[x, y])
                    : 0f;

                float stormDarkness = _stormCloudDarkness01 != null
                    ? Mathf.Clamp01(_stormCloudDarkness01[x, y])
                    : 0f;

                bool heightAssigned =
                    _cloudHeightOffsetAssigned != null &&
                    _cloudHeightOffsetAssigned[x, y];

                bool hasSomethingToSave =
                    density != CloudDensity.None ||
                    volcanicSoot > 0.001f ||
                    externalDarkness > 0.001f ||
                    stormDarkness > 0.001f ||
                    heightAssigned;

                if (!hasSomethingToSave)
                    continue;

                data.clouds.Add(new CloudCellSaveData
                {
                    x = x,
                    y = y,

                    densityValue = Mathf.Clamp((int)density, 0, (int)CloudDensity.High),
                    density01 = GetCloudCoverage01AtCell(x, y),

                    heightOffsetAssigned = heightAssigned,
                    heightOffset = heightAssigned && _cloudHeightOffsets != null
                        ? _cloudHeightOffsets[x, y]
                        : 0f,

                    volcanicSoot01 = volcanicSoot,
                    externalDarkness01 = externalDarkness,
                    stormDarkness01 = stormDarkness
                });
            }
        }

        return data;
    }

    public void LoadState(CloudSimulationSaveData data)
    {
        if (data == null)
            return;

        if (!TryInitializeGrid())
        {
            if (debugLogging)
                Debug.LogWarning("[CloudSimulationSystem] Could not load cloud state because grid is not initialized yet.");

            return;
        }

        // Stop startup reseeding/carry simulation while we restore the saved cloud layer.
        if (_waitForWeatherReadyCoroutine != null)
        {
            StopCoroutine(_waitForWeatherReadyCoroutine);
            _waitForWeatherReadyCoroutine = null;
        }

        if (_cloudStepCoroutine != null)
        {
            StopCoroutine(_cloudStepCoroutine);
            _cloudStepCoroutine = null;
        }

        _cloudAdvanceQueued = false;
        _isAdvancingClouds = false;

        StopVisualRefreshRoutine();
        ClearAllVisuals();

        Array.Clear(_cloudGrid, 0, _cloudGrid.Length);
        Array.Clear(_nextCloudGrid, 0, _nextCloudGrid.Length);

        if (_cloudHeightOffsets != null)
            Array.Clear(_cloudHeightOffsets, 0, _cloudHeightOffsets.Length);

        if (_cloudHeightOffsetAssigned != null)
            Array.Clear(_cloudHeightOffsetAssigned, 0, _cloudHeightOffsetAssigned.Length);

        if (_externalCloudDarkness01 != null)
            Array.Clear(_externalCloudDarkness01, 0, _externalCloudDarkness01.Length);

        if (_stormCloudDarkness01 != null)
            Array.Clear(_stormCloudDarkness01, 0, _stormCloudDarkness01.Length);

        if (_volcanicSootGrid != null)
            Array.Clear(_volcanicSootGrid, 0, _volcanicSootGrid.Length);

        if (_nextVolcanicSootGrid != null)
            Array.Clear(_nextVolcanicSootGrid, 0, _nextVolcanicSootGrid.Length);

        windDirection = IsValidWindDirectionValue(data.windDirectionValue)
            ? (WindDirection8)data.windDirectionValue
            : windDirection;

        if (data.windSpeedTilesPerStep >= 0)
            windSpeedTilesPerStep = data.windSpeedTilesPerStep;

        int restoredCells = 0;

        if (data.clouds != null)
        {
            for (int i = 0; i < data.clouds.Count; i++)
            {
                CloudCellSaveData saved = data.clouds[i];

                if (!IsInBounds(saved.x, saved.y))
                    continue;

                CloudDensity density = RestoreCloudDensityFromSave(saved);

                _cloudGrid[saved.x, saved.y] = density;

                if (_cloudHeightOffsets != null && _cloudHeightOffsetAssigned != null)
                {
                    _cloudHeightOffsetAssigned[saved.x, saved.y] = saved.heightOffsetAssigned;
                    _cloudHeightOffsets[saved.x, saved.y] = saved.heightOffset;
                }

                if (_volcanicSootGrid != null)
                    _volcanicSootGrid[saved.x, saved.y] = Mathf.Clamp01(saved.volcanicSoot01);

                if (_externalCloudDarkness01 != null)
                    _externalCloudDarkness01[saved.x, saved.y] = Mathf.Clamp01(saved.externalDarkness01);

                if (_stormCloudDarkness01 != null)
                    _stormCloudDarkness01[saved.x, saved.y] = Mathf.Clamp01(saved.stormDarkness01);

                // If soot exists and your settings allow soot to create clouds,
                // ensure the cell still has at least the minimum soot cloud density.
                if (_volcanicSootGrid != null &&
                    _volcanicSootGrid[saved.x, saved.y] > 0.001f &&
                    volcanicSootCanCreateClouds)
                {
                    CloudDensity current = _cloudGrid[saved.x, saved.y];

                    if ((int)current < (int)minimumVolcanicCloudDensity)
                        _cloudGrid[saved.x, saved.y] = minimumVolcanicCloudDensity;
                }

                QueueCloudVisualRefresh(saved.x, saved.y);
                restoredCells++;
            }
        }

        // Prevent the startup/weather-ready routine from reseeding over loaded clouds.
        _seededFromValidWeather = true;

        OnCloudStateChanged?.Invoke();

        if (debugLogging)
        {
            Debug.Log(
                $"[CloudSimulationSystem] Loaded cloud state. " +
                $"Cells={restoredCells}, Wind={windDirection}, Speed={windSpeedTilesPerStep}");
        }
    }

    private bool IsValidWindDirectionValue(int value)
    {
        return value >= (int)WindDirection8.North &&
               value <= (int)WindDirection8.NorthWest;
    }

    private CloudDensity RestoreCloudDensityFromSave(CloudCellSaveData saved)
    {
        if (saved == null)
            return CloudDensity.None;

        if (saved.densityValue >= (int)CloudDensity.None &&
            saved.densityValue <= (int)CloudDensity.High)
        {
            return (CloudDensity)saved.densityValue;
        }

        // Old fallback if you had density01 only.
        if (saved.density01 >= 0.90f)
            return CloudDensity.High;

        if (saved.density01 >= 0.55f)
            return CloudDensity.Mid;

        if (saved.density01 > 0.01f)
            return CloudDensity.Low;

        return CloudDensity.None;
    }

    public void ApplyPresetSettings(CloudPresetSettings settings)
    {
        if (settings == null || !settings.overrideClouds)
            return;

        lowCloudHumidityThreshold = settings.lowCloudHumidityThreshold;
        midCloudHumidityThreshold = settings.midCloudHumidityThreshold;
        highCloudHumidityThreshold = settings.highCloudHumidityThreshold;

        baseFormationChance = settings.baseFormationChance;
        neighbourFormationBonus = settings.neighbourFormationBonus;
        dryDissipationChanceMultiplier = settings.dryDissipationChanceMultiplier;
        humidGrowthChanceMultiplier = settings.humidGrowthChanceMultiplier;

        windSpeedTilesPerStep = settings.windSpeedTilesPerStep;
        lateralShuffleChance = settings.lateralShuffleChance;
        windDirectionChangeChancePerTurn = settings.windDirectionChangeChancePerTurn;

        if (debugLogging)
            Debug.Log("[CloudSimulationSystem] Applied cloud preset settings.");
    }
}