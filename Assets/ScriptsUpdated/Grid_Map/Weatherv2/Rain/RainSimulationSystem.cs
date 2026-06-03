using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CloudDensity = CloudSimulationSystem.CloudDensity;


/// <summary>
/// Rain-only simulation layered on top of WeatherGridManager + CloudSimulationSystem.
/// Owns rain charge, raining state, pooled rain visuals, and cloud darkening from rain charge.
/// </summary>
public class RainSimulationSystem : MonoBehaviour
{
    public enum RainVisualKind
    {
        None = 0,
        NormalRain = 1,
        AcidRain = 2,
        AshFall = 3
    }

    public enum RainIntensityLevel
    {
        None = 0,
        Light = 1,
        Normal = 2,
        Heavy = 3
    }

    public static RainSimulationSystem Instance { get; private set; }

    [Header("References")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private WeatherGridManager weatherGridManager;
    [SerializeField] private CloudSimulationSystem cloudSimulationSystem;
    [SerializeField] private Transform rainVisualRoot;
    [SerializeField] private RainVisualPool rainPool;

    [Header("Lifecycle")]
    [SerializeField] private bool initializeOnStart = true;
    [SerializeField] private bool advanceOnCloudStateChanged = true;
    [SerializeField] private bool advanceOnWeatherStateRefreshed = true;

    [Header("Rain Visuals")]
    [SerializeField] private GameObject rainParticlePrefab;
    [SerializeField] private GameObject acidRainParticlePrefab;
    [SerializeField] private GameObject ashParticlePrefab;

    [Range(0.1f, 1f)][SerializeField] private float rainTileCoverage = 0.8f;
    [Min(0.01f)][SerializeField] private float rainShapeHeight = 0.25f;
    [SerializeField] private bool onlyShowRainForMidOrHighClouds = false;

    [Header("Volcanic Rain / Ash")]
    [SerializeField] private bool enableVolcanicRainVisuals = true;

    [Tooltip("Soot level needed before rain switches to acid rain.")]
    [Range(0f, 1f)]
    [SerializeField] private float acidRainSootThreshold = 0.20f;

    [Tooltip("Soot level needed before dry cells show ash fall.")]
    [Range(0f, 1f)]
    [SerializeField] private float ashFallSootThreshold = 0.15f;

    [Tooltip("Humidity at or above this value uses acid rain when soot is present.")]
    [Range(0f, 1f)]
    [SerializeField] private float acidRainHumidityThreshold = 0.45f;

    [Tooltip("Humidity at or below this value uses ash fall when soot is present.")]
    [Range(0f, 1f)]
    [SerializeField] private float ashFallMaxHumidity = 0.40f;

    [Tooltip("If true, ash can show even when the rain grid is not actively raining.")]
    [SerializeField] private bool ashCanShowWithoutRain = true;

    [Header("Rain Charge")]
    [Range(0f, 1f)][SerializeField] private float rainHumidityThreshold = 0.45f;
    [Range(0f, 1f)][SerializeField] private float initialRainChargeFromHumidity = 0.10f;
    [Range(0f, 1f)][SerializeField] private float rainChargeGainPerStep = 0.20f;
    [Range(0f, 1f)][SerializeField] private float rainChargeLossPerStepWhenRaining = 0.35f;
    [Range(0f, 1f)][SerializeField] private float rainChargePassiveLossPerStep = 0.02f;
    [Range(0f, 1f)][SerializeField] private float rainStartChargeThreshold = 0.80f;
    [Range(0f, 1f)][SerializeField] private float rainStopChargeThreshold = 0.45f;

    [Header("Rain Start Limits")]
    [SerializeField] private bool allowLowCloudsToStartRain = false;
    [Min(0)][SerializeField] private int maxNewRainCellsPerStep = 12;

    [Header("Cloud Response To Moisture")]
    [Range(0f, 1f)][SerializeField] private float thickenHumidityThreshold = 0.75f;
    [Range(0f, 1f)][SerializeField] private float thickenChancePerStep = 0.15f;
    [Range(0f, 1f)][SerializeField] private float disperseHumidityThreshold = 0.30f;
    [Range(0f, 1f)][SerializeField] private float disperseChancePerStep = 0.20f;

    [Header("Cloud Darkening")]
    [Range(0f, 1f)][SerializeField] private float cloudDarkenFromRainChargeStrength = 0.5f;

    [Header("Rain Intensity")]
    [SerializeField] private bool useRainIntensity = true;

    [Tooltip("Minimum intensity for any active rain cell.")]
    [Range(0f, 1f)]
    [SerializeField] private float minimumActiveRainIntensity = 0.25f;

    [Tooltip("Below this is light rain.")]
    [Range(0f, 1f)]
    [SerializeField] private float lightRainMaxIntensity = 0.45f;

    [Tooltip("At or above this is heavy rain.")]
    [Range(0f, 1f)]
    [SerializeField] private float heavyRainMinIntensity = 0.75f;

    [Tooltip("How strongly rain charge controls rain intensity.")]
    [Range(0f, 2f)]
    [SerializeField] private float rainChargeIntensityWeight = 0.65f;

    [Tooltip("How strongly humidity controls rain intensity.")]
    [Range(0f, 2f)]
    [SerializeField] private float humidityIntensityWeight = 0.25f;

    [Tooltip("How strongly cloud density controls rain intensity.")]
    [Range(0f, 2f)]
    [SerializeField] private float cloudDensityIntensityWeight = 0.35f;

    [SerializeField] private float lowCloudIntensityMultiplier = 0.65f;
    [SerializeField] private float midCloudIntensityMultiplier = 0.9f;
    [SerializeField] private float highCloudIntensityMultiplier = 1.2f;

    [Header("Rain Particle Intensity")]
    [SerializeField] private bool driveParticleSettingsFromIntensity = true;

    [Min(0f)][SerializeField] private float lightRainEmissionRate = 8f;
    [Min(0f)][SerializeField] private float normalRainEmissionRate = 25f;
    [Min(0f)][SerializeField] private float heavyRainEmissionRate = 50f;

    [Min(0f)][SerializeField] private float lightRainStartSpeed = 4f;
    [Min(0f)][SerializeField] private float normalRainStartSpeed = 7f;
    [Min(0f)][SerializeField] private float heavyRainStartSpeed = 11f;

    [Min(0f)][SerializeField] private float lightRainStartSize = 0.025f;
    [Min(0f)][SerializeField] private float normalRainStartSize = 0.04f;
    [Min(0f)][SerializeField] private float heavyRainStartSize = 0.06f;

    [SerializeField] private float lightRainShapeHeight = 0.18f;
    [SerializeField] private float normalRainShapeHeight = 0.25f;
    [SerializeField] private float heavyRainShapeHeight = 0.35f;

    [Range(0.1f, 1.5f)][SerializeField] private float lightRainTileCoverage = 0.65f;
    [Range(0.1f, 1.5f)][SerializeField] private float normalRainTileCoverage = 0.8f;
    [Range(0.1f, 1.5f)][SerializeField] private float heavyRainTileCoverage = 1f;

    [Header("Pool Warmup")]
    [SerializeField] private bool prewarmPoolOnInitialize = true;
    [Min(0)][SerializeField] private int prewarmRainCount = 12;
    [Min(1)][SerializeField] private int maxCreatesPerPrewarmCall = 12;

    [Header("Rain Visual Performance")]
    [SerializeField] private bool enableQueuedRainVisualRefresh = true;
    [Min(1)][SerializeField] private int rainVisualRefreshesPerFrame = 3;
    [Min(0f)][SerializeField] private float rainVisualRefreshIntervalSeconds = 0f;

    private readonly Queue<Vector2Int> _pendingRainVisualRefreshes = new Queue<Vector2Int>();
    private readonly HashSet<int> _pendingRainVisualRefreshKeys = new HashSet<int>();
    private Coroutine _rainVisualRefreshCoroutine;
    private WaitForSeconds _cachedRainVisualRefreshWait;
    private float _cachedRainVisualRefreshWaitSeconds = -1f;

    [Header("Rain Row Batching")]
    [SerializeField] private bool batchRainStateOverFrames = true;
    [Min(1)][SerializeField] private int rainRowsPerFrame = 8;

    private Coroutine _rainStepCoroutine;
    private bool _rainAdvanceQueued;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = false;

    public event Action OnRainGridInitialized;
    public event Action OnRainStateChanged;

    public int Columns => _cols;
    public int Rows => _rows;
    public bool IsInitialized => _isInitialized;

    private int _cols;
    private int _rows;
    private bool _isInitialized;

    private float[,] _rainChargeGrid;
    private float[,] _nextRainChargeGrid;

    private float[,] _rainIntensityGrid;
    private float[,] _nextRainIntensityGrid;

    private bool[,] _rainActiveGrid;
    private bool[,] _nextRainActiveGrid;

    private GameObject[,] _rainVisuals;
    private GameObject[,] _rainVisualPrefabs;

    private Coroutine _waitForSourcesReadyCoroutine;

    private WeatherGridManager _subscribedWeatherGridManager;
    private CloudSimulationSystem _subscribedCloudSimulationSystem;

    private bool _isAdvancingRain;

    private readonly List<RainStartCandidate> _rainStartCandidates = new List<RainStartCandidate>(64);
    private readonly List<DensityChange> _pendingDensityChanges = new List<DensityChange>(64);
    private readonly HashSet<TileCoord> _activeRainCells = new HashSet<TileCoord>();

    private struct RainStartCandidate
    {
        public int x;
        public int y;
        public float score;
        public float chargeAfterStart;
    }

    private struct DensityChange
    {
        public int x;
        public int y;
        public CloudDensity density;
    }

    public struct VolcanicPrecipitationCell
    {
        public int x;
        public int y;
        public RainVisualKind kind;
        public float soot01;
        public float humidity01;
        public float severity01;

        public TileCoord Coord => new TileCoord(x, y);
    }

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
        RebindSourceEvents();
        BeginWaitingForSourcesReady();
    }

    private void Start()
    {
        if (initializeOnStart)
            BeginWaitingForSourcesReady();
    }

    private void OnDisable()
    {
        UnbindSourceEvents();
        StopRainVisualRefreshRoutine();

        if (_waitForSourcesReadyCoroutine != null)
        {
            StopCoroutine(_waitForSourcesReadyCoroutine);
            _waitForSourcesReadyCoroutine = null;
        }

        if (_rainStepCoroutine != null)
        {
            StopCoroutine(_rainStepCoroutine);
            _rainStepCoroutine = null;
        }

        _rainAdvanceQueued = false;
        _isAdvancingRain = false;
    }

    private void OnDestroy()
    {
        UnbindSourceEvents();

        if (Instance == this)
            Instance = null;
    }

    private void BeginWaitingForSourcesReady()
    {
        if (_waitForSourcesReadyCoroutine != null)
            return;

        _waitForSourcesReadyCoroutine = StartCoroutine(WaitForSourcesReadyRoutine());
    }

    private IEnumerator WaitForSourcesReadyRoutine()
    {
        while (true)
        {
            EnsureLinks();
            EnsurePool();
            RebindSourceEvents();

            if (TryInitializeGrid())
            {
                AdvanceRainOneStep();

                if (debugLogging) {}
                    //Debug.Log("[RainSimulationSystem] Sources ready. Rain system initialized.");

                _waitForSourcesReadyCoroutine = null;
                yield break;
            }

            yield return null;
        }
    }

    public bool TryInitializeGrid()
    {
        EnsureLinks();
        EnsurePool();

        if (weatherGridManager == null || !weatherGridManager.IsInitialized)
            return false;

        if (cloudSimulationSystem == null || !cloudSimulationSystem.IsInitialized)
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
            ClearAllRainVisuals();

        _cols = newCols;
        _rows = newRows;

        if (sizeChanged)
        {
            _rainChargeGrid = new float[_cols, _rows];
            _nextRainChargeGrid = new float[_cols, _rows];

            _rainIntensityGrid = new float[_cols, _rows];
            _nextRainIntensityGrid = new float[_cols, _rows];

            _rainActiveGrid = new bool[_cols, _rows];
            _nextRainActiveGrid = new bool[_cols, _rows];

            _rainVisuals = new GameObject[_cols, _rows];
            _rainVisualPrefabs = new GameObject[_cols, _rows];

            _activeRainCells.Clear();
        }

        _isInitialized = true;

        if (sizeChanged && prewarmPoolOnInitialize)
            PrewarmRainPool();

        if (sizeChanged)
            OnRainGridInitialized?.Invoke();

        if (debugLogging && sizeChanged) {}
            //Debug.Log($"[RainSimulationSystem] Initialized {_cols}x{_rows}");

        return true;
    }

    public void AdvanceRainOneStep()
    {
        if (!batchRainStateOverFrames)
        {
            AdvanceRainOneStepImmediate();
            return;
        }

        if (_rainStepCoroutine != null)
        {
            _rainAdvanceQueued = true;
            return;
        }

        _rainStepCoroutine = StartCoroutine(AdvanceRainOneStepBatchedRoutine());
    }

    private void BeginRainStepBuffers()
    {
        Array.Clear(_nextRainChargeGrid, 0, _nextRainChargeGrid.Length);
        Array.Clear(_nextRainIntensityGrid, 0, _nextRainIntensityGrid.Length);
        Array.Clear(_nextRainActiveGrid, 0, _nextRainActiveGrid.Length);

        _rainStartCandidates.Clear();
        _pendingDensityChanges.Clear();
    }

    private void ProcessRainCarryRows(int startY, int endY)
    {
        for (int y = startY; y < endY; y++)
        {
            for (int x = 0; x < _cols; x++)
            {
                float oldCharge = _rainChargeGrid[x, y];
                float oldIntensity = _rainIntensityGrid != null ? _rainIntensityGrid[x, y] : 0f;
                bool oldRaining = _rainActiveGrid[x, y];

                if (oldCharge <= 0.001f && oldIntensity <= 0.001f && !oldRaining)
                    continue;

                Vector2Int target = cloudSimulationSystem != null
                    ? cloudSimulationSystem.GetWindTargetForCell(x, y)
                    : new Vector2Int(x, y);

                if (!IsInBounds(target.x, target.y))
                    continue;

                _nextRainChargeGrid[target.x, target.y] =
                    Mathf.Max(_nextRainChargeGrid[target.x, target.y], oldCharge);

                _nextRainIntensityGrid[target.x, target.y] =
                    Mathf.Max(_nextRainIntensityGrid[target.x, target.y], oldIntensity);

                if (oldRaining)
                    _nextRainActiveGrid[target.x, target.y] = true;
            }
        }
    }

    private void ProcessRainRebuildRows(int startY, int endY)
    {
        for (int y = startY; y < endY; y++)
        {
            for (int x = 0; x < _cols; x++)
            {
                CloudDensity density = cloudSimulationSystem.GetCloudDensityAtCell(x, y);
                float humidity = GetHumidity01(x, y);

                float oldCharge = _nextRainChargeGrid[x, y];
                bool oldRaining = _nextRainActiveGrid[x, y];

                if (density == CloudDensity.None)
                {
                    _nextRainChargeGrid[x, y] = 0f;
                    _nextRainIntensityGrid[x, y] = 0f;
                    _nextRainActiveGrid[x, y] = false;
                    cloudSimulationSystem.SetExternalCloudDarkness01AtCell(x, y, 0f);
                    continue;
                }

                float newCharge = Mathf.Max(oldCharge, GetInitialRainCharge(humidity));
                newCharge += GetRainChargeGain(density, humidity);

                if (humidity >= thickenHumidityThreshold &&
                    density != CloudDensity.High &&
                    UnityEngine.Random.value <= thickenChancePerStep)
                {
                    _pendingDensityChanges.Add(new DensityChange
                    {
                        x = x,
                        y = y,
                        density = StepUp(density)
                    });
                }
                else if (humidity <= disperseHumidityThreshold &&
                         density != CloudDensity.Low &&
                         UnityEngine.Random.value <= disperseChancePerStep)
                {
                    _pendingDensityChanges.Add(new DensityChange
                    {
                        x = x,
                        y = y,
                        density = StepDown(density)
                    });
                }

                bool canRain = CanHumiditySupportRain(humidity) && CanDensityStartRain(density);

                bool newRaining = false;

                if (oldRaining)
                {
                    newCharge -= rainChargeLossPerStepWhenRaining;
                    newCharge = Mathf.Clamp01(newCharge);

                    if (canRain && newCharge > rainStopChargeThreshold)
                        newRaining = true;
                }
                else
                {
                    newCharge -= rainChargePassiveLossPerStep;
                    newCharge = Mathf.Clamp01(newCharge);

                    if (canRain && newCharge >= rainStartChargeThreshold)
                        AddRainStartCandidate(x, y, density, humidity, newCharge);
                }

                newCharge = Mathf.Clamp01(newCharge);

                _nextRainChargeGrid[x, y] = newCharge;
                _nextRainActiveGrid[x, y] = newRaining;

                _nextRainIntensityGrid[x, y] = newRaining
                    ? CalculateRainIntensity01(density, humidity, newCharge)
                    : 0f;
            }
        }
    }

    private bool ApplyRainStateRows(int startY, int endY)
    {
        bool anyChanged = false;

        for (int y = startY; y < endY; y++)
        {
            for (int x = 0; x < _cols; x++)
            {
                float oldCharge = _rainChargeGrid[x, y];
                float oldIntensity = _rainIntensityGrid != null ? _rainIntensityGrid[x, y] : 0f;
                bool oldRain = _rainActiveGrid[x, y];

                float newCharge = _nextRainChargeGrid[x, y];
                float newIntensity = _nextRainIntensityGrid != null ? _nextRainIntensityGrid[x, y] : 0f;
                bool newRain = _nextRainActiveGrid[x, y];

                _rainChargeGrid[x, y] = newCharge;

                if (_rainIntensityGrid != null)
                    _rainIntensityGrid[x, y] = newIntensity;

                _rainActiveGrid[x, y] = newRain;

                TileCoord rainCoord = new TileCoord(x, y);

                if (newRain)
                    _activeRainCells.Add(rainCoord);
                else
                    _activeRainCells.Remove(rainCoord);

                cloudSimulationSystem.SetExternalCloudDarkness01AtCell(
                    x,
                    y,
                    newCharge * cloudDarkenFromRainChargeStrength);

                bool changed =
                    Mathf.Abs(oldCharge - newCharge) > 0.01f ||
                    Mathf.Abs(oldIntensity - newIntensity) > 0.05f ||
                    oldRain != newRain;

                // Ash on dry cells never builds rain charge, so rain-state change never fires.
                // Separately detect when ash eligibility and the current visual diverge.
                if (!changed && enableVolcanicRainVisuals)
                {
                    float soot01 = GetSoot01AtCell(x, y);
                    bool ashEligible = soot01 >= ashFallSootThreshold
                        && GetHumidity01(x, y) <= ashFallMaxHumidity
                        && (ashCanShowWithoutRain || newRain);
                    bool ashShowing = _rainVisualPrefabs != null && _rainVisualPrefabs[x, y] == ashParticlePrefab;
                    if (ashEligible != ashShowing)
                        changed = true;
                }

                if (changed)
                {
                    QueueRainVisualRefresh(x, y);
                    anyChanged = true;
                }
            }
        }

        return anyChanged;
    }

    private bool ApplyPendingRainDensityChanges()
    {
        bool anyChanged = false;

        for (int i = 0; i < _pendingDensityChanges.Count; i++)
        {
            DensityChange change = _pendingDensityChanges[i];

            if (cloudSimulationSystem.TrySetCloudDensityAtCell(change.x, change.y, change.density))
            {
                QueueRainVisualRefresh(change.x, change.y);
                anyChanged = true;
            }
        }

        return anyChanged;
    }

    private void AdvanceRainOneStepImmediate()
    {
        if (_isAdvancingRain)
            return;

        if (!TryInitializeGrid())
            return;

        _isAdvancingRain = true;
        try
        {
            BeginRainStepBuffers();
            ProcessRainCarryRows(0, _rows);
            ProcessRainRebuildRows(0, _rows);
            ApplyRainStartCandidates();

            bool anyChanged = ApplyRainStateRows(0, _rows);
            if (ApplyPendingRainDensityChanges())
                anyChanged = true;

            if (anyChanged)
                OnRainStateChanged?.Invoke();

            if (debugLogging) {}
                //Debug.Log("[RainSimulationSystem] Advanced rain one step.");
        }
        finally
        {
            _isAdvancingRain = false;
        }
    }

    private IEnumerator AdvanceRainOneStepBatchedRoutine()
    {
        if (_isAdvancingRain || !TryInitializeGrid())
        {
            _rainStepCoroutine = null;
            yield break;
        }

        _isAdvancingRain = true;

        bool anyChanged = false;
        int rowsPerFrame = Mathf.Max(1, rainRowsPerFrame);

        try
        {
            BeginRainStepBuffers();

            for (int startY = 0; startY < _rows; startY += rowsPerFrame)
            {
                ProcessRainCarryRows(startY, Mathf.Min(startY + rowsPerFrame, _rows));

                if (startY + rowsPerFrame < _rows)
                    yield return null;
            }

            for (int startY = 0; startY < _rows; startY += rowsPerFrame)
            {
                ProcessRainRebuildRows(startY, Mathf.Min(startY + rowsPerFrame, _rows));

                if (startY + rowsPerFrame < _rows)
                    yield return null;
            }

            ApplyRainStartCandidates();

            for (int startY = 0; startY < _rows; startY += rowsPerFrame)
            {
                if (ApplyRainStateRows(startY, Mathf.Min(startY + rowsPerFrame, _rows)))
                    anyChanged = true;

                if (startY + rowsPerFrame < _rows)
                    yield return null;
            }

            if (ApplyPendingRainDensityChanges())
                anyChanged = true;

            if (anyChanged)
                OnRainStateChanged?.Invoke();

            if (debugLogging) {}
                //Debug.Log("[RainSimulationSystem] Advanced rain one batched step.");
        }
        finally
        {
            _isAdvancingRain = false;
            _rainStepCoroutine = null;

            if (_rainAdvanceQueued && isActiveAndEnabled)
            {
                _rainAdvanceQueued = false;
                _rainStepCoroutine = StartCoroutine(AdvanceRainOneStepBatchedRoutine());
            }
            else
            {
                _rainAdvanceQueued = false;
            }
        }
    }

    private int _lastRainAdvanceFrame = -1;

    private void RequestAdvanceRain()
    {
        if (!TryInitializeGrid())
            return;

        if (_lastRainAdvanceFrame == Time.frameCount)
            return;

        _lastRainAdvanceFrame = Time.frameCount;
        AdvanceRainOneStep();
    }

    public bool IsRainingAtCell(int x, int y)
    {
        if (!_isInitialized || !IsInBounds(x, y) || _rainActiveGrid == null)
            return false;

        return _rainActiveGrid[x, y];
    }

    public float GetRainCharge01AtCell(int x, int y)
    {
        if (!_isInitialized || !IsInBounds(x, y) || _rainChargeGrid == null)
            return 0f;

        return Mathf.Clamp01(_rainChargeGrid[x, y]);
    }

    public bool CopyActiveRainCells(List<TileCoord> results)
    {
        if (results == null)
            return false;

        results.Clear();

        if (!_isInitialized || _rainActiveGrid == null)
            return false;

        foreach (TileCoord coord in _activeRainCells)
            results.Add(coord);

        return results.Count > 0;
    }

    public float GetRainIntensity01AtCell(TileCoord coord)
    {
        return GetRainIntensity01AtCell(coord.x, coord.y);
    }

    public float GetRainIntensity01AtCell(int x, int y)
    {
        if (!_isInitialized || !IsInBounds(x, y) || _rainActiveGrid == null)
            return 0f;

        if (!_rainActiveGrid[x, y])
            return 0f;

        if (_rainIntensityGrid != null)
            return Mathf.Clamp01(_rainIntensityGrid[x, y]);

        float charge01 = _rainChargeGrid != null ? Mathf.Clamp01(_rainChargeGrid[x, y]) : 0f;
        return Mathf.Clamp01(Mathf.Lerp(minimumActiveRainIntensity, 1f, charge01));
    }

    public RainIntensityLevel GetRainIntensityLevelAtCell(TileCoord coord)
    {
        return GetRainIntensityLevelAtCell(coord.x, coord.y);
    }

    public RainIntensityLevel GetRainIntensityLevelAtCell(int x, int y)
    {
        float intensity01 = GetRainIntensity01AtCell(x, y);

        if (intensity01 <= 0.001f)
            return RainIntensityLevel.None;

        if (intensity01 < lightRainMaxIntensity)
            return RainIntensityLevel.Light;

        if (intensity01 >= heavyRainMinIntensity)
            return RainIntensityLevel.Heavy;

        return RainIntensityLevel.Normal;
    }

    private float CalculateRainIntensity01(CloudDensity density, float humidity01, float charge01)
    {
        if (!useRainIntensity)
            return Mathf.Clamp01(Mathf.Lerp(minimumActiveRainIntensity, 1f, charge01));

        float density01 = GetCloudDensityIntensity01(density);

        float raw =
            charge01 * rainChargeIntensityWeight +
            humidity01 * humidityIntensityWeight +
            density01 * cloudDensityIntensityWeight;

        float densityMultiplier = GetCloudDensityIntensityMultiplier(density);

        raw *= densityMultiplier;

        return Mathf.Clamp01(Mathf.Max(minimumActiveRainIntensity, raw));
    }

    private float GetCloudDensityIntensity01(CloudDensity density)
    {
        switch (density)
        {
            case CloudDensity.Low:
                return 0.35f;

            case CloudDensity.Mid:
                return 0.65f;

            case CloudDensity.High:
                return 1f;

            default:
                return 0f;
        }
    }

    private float GetCloudDensityIntensityMultiplier(CloudDensity density)
    {
        switch (density)
        {
            case CloudDensity.Low:
                return lowCloudIntensityMultiplier;

            case CloudDensity.Mid:
                return midCloudIntensityMultiplier;

            case CloudDensity.High:
                return highCloudIntensityMultiplier;

            default:
                return 0f;
        }
    }

    public void ClearAllRain()
    {
        if (!TryInitializeGrid())
            return;

        _activeRainCells.Clear();

        for (int x = 0; x < _cols; x++)
        {
            for (int y = 0; y < _rows; y++)
            {
                _rainChargeGrid[x, y] = 0f;

                if (_rainIntensityGrid != null)
                    _rainIntensityGrid[x, y] = 0f;

                _rainActiveGrid[x, y] = false;
                cloudSimulationSystem.SetExternalCloudDarkness01AtCell(x, y, 0f);
                QueueRainVisualRefresh(x, y);
            }
        }

        OnRainStateChanged?.Invoke();
    }

    private void HandleWeatherGridInitialized()
    {
        TryInitializeGrid();
    }

    private void HandleCloudGridInitialized()
    {
        TryInitializeGrid();
    }

    private void HandleWeatherStateRefreshed()
    {
        if (!advanceOnWeatherStateRefreshed)
            return;

        RequestAdvanceRain();
    }

    private void HandleCloudStateChanged()
    {
        if (!advanceOnCloudStateChanged)
            return;

        RequestAdvanceRain();
    }

    private void AddRainStartCandidate(int x, int y, CloudDensity density, float humidity, float chargeAfterStart)
    {
        float score = chargeAfterStart;

        score += density switch
        {
            CloudDensity.Low => 0.05f,
            CloudDensity.Mid => 0.20f,
            CloudDensity.High => 0.35f,
            _ => 0f
        };

        score += humidity * 0.25f;

        int limit = maxNewRainCellsPerStep <= 0 ? int.MaxValue : maxNewRainCellsPerStep;

        RainStartCandidate candidate = new RainStartCandidate
        {
            x = x,
            y = y,
            score = score,
            chargeAfterStart = chargeAfterStart
        };

        if (_rainStartCandidates.Count < limit)
        {
            _rainStartCandidates.Add(candidate);
            return;
        }

        int weakestIndex = 0;
        float weakestScore = _rainStartCandidates[0].score;

        for (int i = 1; i < _rainStartCandidates.Count; i++)
        {
            if (_rainStartCandidates[i].score < weakestScore)
            {
                weakestScore = _rainStartCandidates[i].score;
                weakestIndex = i;
            }
        }

        if (score > weakestScore)
            _rainStartCandidates[weakestIndex] = candidate;
    }

    private void ApplyRainStartCandidates()
    {
        if (_rainStartCandidates.Count == 0)
            return;

        for (int i = 0; i < _rainStartCandidates.Count; i++)
        {
            RainStartCandidate candidate = _rainStartCandidates[i];
            _nextRainChargeGrid[candidate.x, candidate.y] = candidate.chargeAfterStart;
            _nextRainActiveGrid[candidate.x, candidate.y] = true;
        }
    }

    private float GetHumidity01(int x, int y)
    {
        if (weatherGridManager == null)
            return 0f;

        return weatherGridManager.TryGetCellState(x, y, out WeatherCellState state)
            ? Mathf.Clamp01(state.humidity01)
            : 0f;
    }

    private bool CanHumiditySupportRain(float humidity01)
    {
        return humidity01 >= rainHumidityThreshold;
    }

    private bool CanDensityStartRain(CloudDensity density)
    {
        if (density == CloudDensity.None)
            return false;

        if (!allowLowCloudsToStartRain && density == CloudDensity.Low)
            return false;

        return true;
    }

    private float GetInitialRainCharge(float humidity01)
    {
        float t = Mathf.InverseLerp(rainHumidityThreshold, 1f, humidity01);
        return Mathf.Clamp01(t * initialRainChargeFromHumidity);
    }

    private float GetRainChargeGain(CloudDensity density, float humidity01)
    {
        if (density == CloudDensity.None || humidity01 < rainHumidityThreshold)
            return 0f;

        float humidityT = Mathf.InverseLerp(rainHumidityThreshold, 1f, humidity01);
        return rainChargeGainPerStep * humidityT * GetRainChargeDensityMultiplier(density);
    }

    private float GetRainChargeDensityMultiplier(CloudDensity density)
    {
        switch (density)
        {
            case CloudDensity.Low: return 0.60f;
            case CloudDensity.Mid: return 0.85f;
            case CloudDensity.High: return 1f;
            default: return 0f;
        }
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

    private void RefreshRainVisualAtCell(int x, int y)
    {
        EnsurePool();

        RainVisualKind visualKind = GetRainVisualKindAtCell(x, y);
        GameObject desiredPrefab = GetRainVisualPrefab(visualKind);

        if (_rainVisuals[x, y] != null && _rainVisualPrefabs[x, y] != desiredPrefab)
            ReturnRainVisualAtCell(x, y);

        if (desiredPrefab == null)
            return;

        if (rainVisualRoot == null)
        {
            GameObject root = new GameObject("Rain Visual Root");
            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            rainVisualRoot = root.transform;
        }

        if (!TryGetRainWorldPosition(x, y, out Vector3 rainPos))
            return;

        if (_rainVisuals[x, y] == null)
        {
            GameObject instance = rainPool.Get(
                desiredPrefab,
                rainVisualRoot,
                rainPos,
                Quaternion.identity,
                resetPooledEffects: true);

            if (instance == null)
                return;

            instance.name = $"{GetRainVisualName(visualKind)}_{x}_{y}";
            _rainVisuals[x, y] = instance;
            _rainVisualPrefabs[x, y] = desiredPrefab;
        }
        else
        {
            _rainVisuals[x, y].name = $"{GetRainVisualName(visualKind)}_{x}_{y}";
        }

        Transform tr = _rainVisuals[x, y].transform;
        tr.SetParent(rainVisualRoot, true);
        tr.position = rainPos;

        ConfigureRainParticleShape(_rainVisuals[x, y], x, y);
    }

    private bool ShouldShowRainVisualAtCell(int x, int y)
    {
        return GetRainVisualPrefab(GetRainVisualKindAtCell(x, y)) != null;
    }

    public RainVisualKind GetRainVisualKindAtCell(int x, int y)
    {
        if (!_isInitialized || !IsInBounds(x, y))
            return RainVisualKind.None;

        if (_rainActiveGrid == null)
            return RainVisualKind.None;

        float humidity = GetHumidity01(x, y);
        float soot01 = GetSoot01AtCell(x, y);

        bool hasSootForAcid = enableVolcanicRainVisuals && soot01 >= acidRainSootThreshold;
        bool hasSootForAsh = enableVolcanicRainVisuals && soot01 >= ashFallSootThreshold;

        bool raining = _rainActiveGrid[x, y];

        // Dry volcanic cloud = ash fall instead of rain.
        if (hasSootForAsh && humidity <= ashFallMaxHumidity)
        {
            if (ashCanShowWithoutRain || raining)
                return RainVisualKind.AshFall;
        }

        if (!raining)
            return RainVisualKind.None;

        if (onlyShowRainForMidOrHighClouds && cloudSimulationSystem != null)
        {
            CloudDensity density = cloudSimulationSystem.GetCloudDensityAtCell(x, y);
            if (density == CloudDensity.Low)
                return RainVisualKind.None;
        }

        // Wet soot cloud = acid rain.
        if (hasSootForAcid && humidity >= acidRainHumidityThreshold)
            return RainVisualKind.AcidRain;

        return RainVisualKind.NormalRain;
    }

    private GameObject GetRainVisualPrefab(RainVisualKind kind)
    {
        switch (kind)
        {
            case RainVisualKind.NormalRain:
                return rainParticlePrefab;

            case RainVisualKind.AcidRain:
                return acidRainParticlePrefab != null ? acidRainParticlePrefab : rainParticlePrefab;

            case RainVisualKind.AshFall:
                return ashParticlePrefab;

            default:
                return null;
        }
    }

    private string GetRainVisualName(RainVisualKind kind)
    {
        switch (kind)
        {
            case RainVisualKind.NormalRain:
                return "Rain";

            case RainVisualKind.AcidRain:
                return "AcidRain";

            case RainVisualKind.AshFall:
                return "AshFall";

            default:
                return "NoRain";
        }
    }

    private float GetSoot01AtCell(int x, int y)
    {
        if (cloudSimulationSystem == null)
            return 0f;

        return cloudSimulationSystem.GetVolcanicSoot01AtCell(x, y);
    }

    private bool TryGetRainWorldPosition(int x, int y, out Vector3 worldPosition)
    {
        worldPosition = Vector3.zero;

        if (cloudSimulationSystem != null &&
            cloudSimulationSystem.TryGetCloudWorldPosition(x, y, out worldPosition))
        {
            return true;
        }

        if (weatherGridManager != null)
            return weatherGridManager.TryGetWeatherCellCenterWorldPosition(x, y, out worldPosition);

        return false;
    }

    private void ConfigureRainParticleShape(GameObject rainInstance, int x, int y)
    {
        if (rainInstance == null)
            return;

        float intensity01 = GetRainIntensity01AtCell(x, y);
        RainIntensityLevel level = GetRainIntensityLevelAtCell(x, y);

        // Ash on a non-raining cell has zero rain intensity — derive from soot so particles emit.
        bool isAsh = GetRainVisualKindAtCell(x, y) == RainVisualKind.AshFall;
        if (isAsh && intensity01 <= 0f)
        {
            float soot01 = GetSoot01AtCell(x, y);
            intensity01 = Mathf.Clamp01(Mathf.Max(minimumActiveRainIntensity,
                Mathf.InverseLerp(ashFallSootThreshold, 1f, soot01)));
            level = intensity01 >= heavyRainMinIntensity ? RainIntensityLevel.Heavy
                  : intensity01 >= lightRainMaxIntensity ? RainIntensityLevel.Normal
                  : RainIntensityLevel.Light;
        }

        RainVisualShapeCache cache = rainInstance.GetComponent<RainVisualShapeCache>();
        if (cache == null)
            cache = rainInstance.AddComponent<RainVisualShapeCache>();

        float cellSize = gridManager != null ? gridManager.cellSize : 1f;

        float finalCoverage = GetRainTileCoverageForIntensity(level, intensity01);
        float finalShapeHeight = GetRainShapeHeightForIntensity(level, intensity01);

        bool unchanged =
            cache.lastIntensityLevel == level &&
            Mathf.Abs(cache.lastCellSize - cellSize) <= 0.0001f &&
            Mathf.Abs(cache.lastCoverage - finalCoverage) <= 0.0001f &&
            Mathf.Abs(cache.lastShapeHeight - finalShapeHeight) <= 0.0001f;

        if (unchanged)
            return;

        // Use cached component references from pool — avoids GetComponentsInChildren allocation each call.
        RainVisualPoolCache poolCache = rainInstance.GetComponent<RainVisualPoolCache>();
        ParticleSystem[] particleSystems = poolCache != null && poolCache.IsInitialized
            ? poolCache.ParticleSystems
            : rainInstance.GetComponentsInChildren<ParticleSystem>(true);

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem ps = particleSystems[i];

            if (ps == null)
                continue;

            ApplyRainParticleIntensity(ps, intensity01, level);

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.position = new Vector3(0f, -finalShapeHeight * 0.5f, 0f);
            shape.scale = new Vector3(
                cellSize * finalCoverage,
                finalShapeHeight,
                cellSize * finalCoverage);
        }

        cache.lastIntensityLevel = level;
        cache.lastCellSize = cellSize;
        cache.lastCoverage = finalCoverage;
        cache.lastShapeHeight = finalShapeHeight;
    }

    private void ApplyRainParticleIntensity(
    ParticleSystem ps,
    float intensity01,
    RainIntensityLevel level)
    {
        if (ps == null || !driveParticleSettingsFromIntensity)
            return;

        intensity01 = Mathf.Clamp01(intensity01);

        float emissionRate = GetRainEmissionRateForIntensity(level, intensity01);
        float startSpeed = GetRainStartSpeedForIntensity(level, intensity01);
        float startSize = GetRainStartSizeForIntensity(level, intensity01);

        var emission = ps.emission;
        emission.rateOverTime = emissionRate;

        var main = ps.main;
        main.startSpeed = startSpeed;
        main.startSize = startSize;
    }

    private float GetRainEmissionRateForIntensity(RainIntensityLevel level, float intensity01)
    {
        switch (level)
        {
            case RainIntensityLevel.Light:
                return Mathf.Lerp(lightRainEmissionRate, normalRainEmissionRate, Mathf.Clamp01(intensity01 / lightRainMaxIntensity));

            case RainIntensityLevel.Heavy:
                return Mathf.Lerp(normalRainEmissionRate, heavyRainEmissionRate, Mathf.InverseLerp(heavyRainMinIntensity, 1f, intensity01));

            case RainIntensityLevel.Normal:
                return Mathf.Lerp(normalRainEmissionRate, heavyRainEmissionRate, Mathf.InverseLerp(lightRainMaxIntensity, heavyRainMinIntensity, intensity01));

            default:
                return 0f;
        }
    }

    private float GetRainStartSpeedForIntensity(RainIntensityLevel level, float intensity01)
    {
        switch (level)
        {
            case RainIntensityLevel.Light:
                return Mathf.Lerp(lightRainStartSpeed, normalRainStartSpeed, Mathf.Clamp01(intensity01 / lightRainMaxIntensity));

            case RainIntensityLevel.Heavy:
                return Mathf.Lerp(normalRainStartSpeed, heavyRainStartSpeed, Mathf.InverseLerp(heavyRainMinIntensity, 1f, intensity01));

            case RainIntensityLevel.Normal:
                return Mathf.Lerp(normalRainStartSpeed, heavyRainStartSpeed, Mathf.InverseLerp(lightRainMaxIntensity, heavyRainMinIntensity, intensity01));

            default:
                return lightRainStartSpeed;
        }
    }

    private float GetRainStartSizeForIntensity(RainIntensityLevel level, float intensity01)
    {
        switch (level)
        {
            case RainIntensityLevel.Light:
                return Mathf.Lerp(lightRainStartSize, normalRainStartSize, Mathf.Clamp01(intensity01 / lightRainMaxIntensity));

            case RainIntensityLevel.Heavy:
                return Mathf.Lerp(normalRainStartSize, heavyRainStartSize, Mathf.InverseLerp(heavyRainMinIntensity, 1f, intensity01));

            case RainIntensityLevel.Normal:
                return Mathf.Lerp(normalRainStartSize, heavyRainStartSize, Mathf.InverseLerp(lightRainMaxIntensity, heavyRainMinIntensity, intensity01));

            default:
                return lightRainStartSize;
        }
    }

    private float GetRainShapeHeightForIntensity(RainIntensityLevel level, float intensity01)
    {
        switch (level)
        {
            case RainIntensityLevel.Light:
                return Mathf.Lerp(lightRainShapeHeight, normalRainShapeHeight, Mathf.Clamp01(intensity01 / lightRainMaxIntensity));

            case RainIntensityLevel.Heavy:
                return Mathf.Lerp(normalRainShapeHeight, heavyRainShapeHeight, Mathf.InverseLerp(heavyRainMinIntensity, 1f, intensity01));

            case RainIntensityLevel.Normal:
                return Mathf.Lerp(normalRainShapeHeight, heavyRainShapeHeight, Mathf.InverseLerp(lightRainMaxIntensity, heavyRainMinIntensity, intensity01));

            default:
                return rainShapeHeight;
        }
    }

    private float GetRainTileCoverageForIntensity(RainIntensityLevel level, float intensity01)
    {
        switch (level)
        {
            case RainIntensityLevel.Light:
                return Mathf.Lerp(lightRainTileCoverage, normalRainTileCoverage, Mathf.Clamp01(intensity01 / lightRainMaxIntensity));

            case RainIntensityLevel.Heavy:
                return Mathf.Lerp(normalRainTileCoverage, heavyRainTileCoverage, Mathf.InverseLerp(heavyRainMinIntensity, 1f, intensity01));

            case RainIntensityLevel.Normal:
                return Mathf.Lerp(normalRainTileCoverage, heavyRainTileCoverage, Mathf.InverseLerp(lightRainMaxIntensity, heavyRainMinIntensity, intensity01));

            default:
                return rainTileCoverage;
        }
    }

    private void ReturnRainVisualAtCell(int x, int y)
    {
        if (_rainVisuals == null || _rainVisualPrefabs == null)
            return;

        GameObject instance = _rainVisuals[x, y];
        GameObject prefab = _rainVisualPrefabs[x, y];

        if (instance != null && prefab != null && rainPool != null)
            rainPool.Return(prefab, instance, stopPooledEffects: true);

        _rainVisuals[x, y] = null;
        _rainVisualPrefabs[x, y] = null;
    }

    private void ClearAllRainVisuals()
    {
        if (_rainVisuals == null)
            return;

        for (int x = 0; x < _rainVisuals.GetLength(0); x++)
        {
            for (int y = 0; y < _rainVisuals.GetLength(1); y++)
                ReturnRainVisualAtCell(x, y);
        }
    }

    private void PrewarmRainPool()
    {
        EnsurePool();

        Transform parent = rainVisualRoot != null ? rainVisualRoot : transform;

        if (rainParticlePrefab != null)
            rainPool.Prewarm(rainParticlePrefab, prewarmRainCount, parent, maxCreatesPerPrewarmCall);

        if (acidRainParticlePrefab != null)
            rainPool.Prewarm(acidRainParticlePrefab, prewarmRainCount, parent, maxCreatesPerPrewarmCall);

        if (ashParticlePrefab != null)
            rainPool.Prewarm(ashParticlePrefab, prewarmRainCount, parent, maxCreatesPerPrewarmCall);
    }

    private void EnsurePool()
    {
        if (rainPool != null)
            return;

        GameObject go = new GameObject("Rain Visual Pool");
        go.transform.SetParent(transform, false);
        rainPool = go.AddComponent<RainVisualPool>();
    }

    private void EnsureLinks()
    {
        if (gridManager == null)
            gridManager = GridManager.Instance;

        if (weatherGridManager == null)
            weatherGridManager = WeatherGridManager.Instance;

        if (cloudSimulationSystem == null)
            cloudSimulationSystem = CloudSimulationSystem.Instance;
    }

    private void RebindSourceEvents()
    {
        RebindWeatherGridEvents();
        RebindCloudSimulationEvents();
    }

    private void RebindWeatherGridEvents()
    {
        if (_subscribedWeatherGridManager == weatherGridManager)
            return;

        if (_subscribedWeatherGridManager != null)
        {
            _subscribedWeatherGridManager.OnWeatherGridInitialized -= HandleWeatherGridInitialized;
            _subscribedWeatherGridManager.OnWeatherStateRefreshed -= HandleWeatherStateRefreshed;
        }

        _subscribedWeatherGridManager = weatherGridManager;

        if (_subscribedWeatherGridManager != null)
        {
            _subscribedWeatherGridManager.OnWeatherGridInitialized += HandleWeatherGridInitialized;
            _subscribedWeatherGridManager.OnWeatherStateRefreshed += HandleWeatherStateRefreshed;
        }
    }

    private void RebindCloudSimulationEvents()
    {
        if (_subscribedCloudSimulationSystem == cloudSimulationSystem)
            return;

        if (_subscribedCloudSimulationSystem != null)
        {
            _subscribedCloudSimulationSystem.OnCloudGridInitialized -= HandleCloudGridInitialized;
            _subscribedCloudSimulationSystem.OnCloudStateChanged -= HandleCloudStateChanged;
        }

        _subscribedCloudSimulationSystem = cloudSimulationSystem;

        if (_subscribedCloudSimulationSystem != null)
        {
            _subscribedCloudSimulationSystem.OnCloudGridInitialized += HandleCloudGridInitialized;
            _subscribedCloudSimulationSystem.OnCloudStateChanged += HandleCloudStateChanged;
        }
    }

    private void UnbindSourceEvents()
    {
        if (_subscribedWeatherGridManager != null)
        {
            _subscribedWeatherGridManager.OnWeatherGridInitialized -= HandleWeatherGridInitialized;
            _subscribedWeatherGridManager.OnWeatherStateRefreshed -= HandleWeatherStateRefreshed;
            _subscribedWeatherGridManager = null;
        }

        if (_subscribedCloudSimulationSystem != null)
        {
            _subscribedCloudSimulationSystem.OnCloudGridInitialized -= HandleCloudGridInitialized;
            _subscribedCloudSimulationSystem.OnCloudStateChanged -= HandleCloudStateChanged;
            _subscribedCloudSimulationSystem = null;
        }
    }

    private bool IsInBounds(int x, int y)
    {
        return x >= 0 && x < _cols && y >= 0 && y < _rows;
    }

    private int GetRainCellKey(int x, int y)
    {
        return x + (y * Mathf.Max(1, _cols));
    }

    private void QueueRainVisualRefresh(int x, int y)
    {
        if (!IsInBounds(x, y))
            return;

        bool shouldShow = ShouldShowRainVisualAtCell(x, y);
        bool hasVisual = _rainVisuals != null && _rainVisuals[x, y] != null;

        if (!shouldShow && !hasVisual)
            return;

        if (!enableQueuedRainVisualRefresh)
        {
            RefreshRainVisualAtCell(x, y);
            return;
        }

        int key = GetRainCellKey(x, y);
        if (!_pendingRainVisualRefreshKeys.Add(key))
            return;

        _pendingRainVisualRefreshes.Enqueue(new Vector2Int(x, y));
        EnsureRainVisualRefreshRoutine();
    }

    private void EnsureRainVisualRefreshRoutine()
    {
        if (_rainVisualRefreshCoroutine != null || !isActiveAndEnabled)
            return;

        _rainVisualRefreshCoroutine = StartCoroutine(RainVisualRefreshRoutine());
    }

    private IEnumerator RainVisualRefreshRoutine()
    {
        while (_pendingRainVisualRefreshes.Count > 0)
        {
            int refreshedThisBatch = 0;
            int maxRefreshes = Mathf.Max(1, rainVisualRefreshesPerFrame);

            while (_pendingRainVisualRefreshes.Count > 0 && refreshedThisBatch < maxRefreshes)
            {
                Vector2Int cell = _pendingRainVisualRefreshes.Dequeue();
                int x = cell.x;
                int y = cell.y;

                _pendingRainVisualRefreshKeys.Remove(GetRainCellKey(x, y));

                if (!_isInitialized || !IsInBounds(x, y))
                    continue;

                RefreshRainVisualAtCell(x, y);
                refreshedThisBatch++;
            }

            if (_pendingRainVisualRefreshes.Count > 0)
            {
                float interval = Mathf.Max(0f, rainVisualRefreshIntervalSeconds);

                if (interval > 0f)
                {
                    if (_cachedRainVisualRefreshWait == null ||
                        Mathf.Abs(_cachedRainVisualRefreshWaitSeconds - interval) > 0.0001f)
                    {
                        _cachedRainVisualRefreshWait = new WaitForSeconds(interval);
                        _cachedRainVisualRefreshWaitSeconds = interval;
                    }

                    yield return _cachedRainVisualRefreshWait;
                }
                else
                {
                    yield return null;
                }
            }
        }

        _rainVisualRefreshCoroutine = null;
        _cachedRainVisualRefreshWait = null;
        _cachedRainVisualRefreshWaitSeconds = -1f;
    }

    private void StopRainVisualRefreshRoutine()
    {
        _pendingRainVisualRefreshes.Clear();
        _pendingRainVisualRefreshKeys.Clear();

        if (_rainVisualRefreshCoroutine != null)
        {
            StopCoroutine(_rainVisualRefreshCoroutine);
            _rainVisualRefreshCoroutine = null;
        }

        _cachedRainVisualRefreshWait = null;
        _cachedRainVisualRefreshWaitSeconds = -1f;
    }

    public bool TryRaiseRainChargeFloorAtCell(int x, int y, float minCharge, bool activateIfAboveStart = true)
    {
        if (!_isInitialized || !IsInBounds(x, y) || _rainChargeGrid == null || _rainActiveGrid == null)
            return false;

        float clamped = Mathf.Clamp01(minCharge);
        bool changed = false;

        if (_rainChargeGrid[x, y] < clamped - 0.01f)
        {
            _rainChargeGrid[x, y] = clamped;
            changed = true;
        }

        if (activateIfAboveStart && !_rainActiveGrid[x, y] && _rainChargeGrid[x, y] >= rainStartChargeThreshold)
        {
            CloudDensity density = cloudSimulationSystem != null
                ? cloudSimulationSystem.GetCloudDensityAtCell(x, y)
                : CloudDensity.None;

            if (CanDensityStartRain(density))
            {
                _rainActiveGrid[x, y] = true;
                changed = true;
            }
        }

        if (changed)
            QueueRainVisualRefresh(x, y);

        return changed;
    }

    public bool IsAcidRainAtCell(int x, int y)
    {
        return GetRainVisualKindAtCell(x, y) == RainVisualKind.AcidRain;
    }

    public bool IsAshFallAtCell(int x, int y)
    {
        return GetRainVisualKindAtCell(x, y) == RainVisualKind.AshFall;
    }

    public bool IsVolcanicPrecipitationAtCell(int x, int y)
    {
        RainVisualKind kind = GetRainVisualKindAtCell(x, y);
        return kind == RainVisualKind.AcidRain || kind == RainVisualKind.AshFall;
    }

    public float GetVolcanicPrecipitationSeverity01AtCell(int x, int y)
    {
        RainVisualKind kind = GetRainVisualKindAtCell(x, y);
        return CalculateVolcanicPrecipitationSeverity01AtCell(x, y, kind);
    }

    public bool CopyActiveVolcanicPrecipitationCells(List<VolcanicPrecipitationCell> results)
    {
        if (results == null)
            return false;

        results.Clear();

        if (!_isInitialized || _rainActiveGrid == null)
            return false;

        for (int x = 0; x < _cols; x++)
        {
            for (int y = 0; y < _rows; y++)
            {
                RainVisualKind kind = GetRainVisualKindAtCell(x, y);

                if (kind != RainVisualKind.AcidRain &&
                    kind != RainVisualKind.AshFall)
                {
                    continue;
                }

                float soot01 = GetSoot01AtCell(x, y);
                float humidity01 = GetHumidity01(x, y);
                float severity01 = CalculateVolcanicPrecipitationSeverity01AtCell(x, y, kind);

                results.Add(new VolcanicPrecipitationCell
                {
                    x = x,
                    y = y,
                    kind = kind,
                    soot01 = soot01,
                    humidity01 = humidity01,
                    severity01 = severity01
                });
            }
        }

        return results.Count > 0;
    }

    private float CalculateVolcanicPrecipitationSeverity01AtCell(
        int x,
        int y,
        RainVisualKind kind)
    {
        if (!_isInitialized || !IsInBounds(x, y))
            return 0f;

        float soot01 = GetSoot01AtCell(x, y);
        float humidity01 = GetHumidity01(x, y);
        float rainCharge01 = GetRainCharge01AtCell(x, y);

        switch (kind)
        {
            case RainVisualKind.AcidRain:
                {
                    float sootT = Mathf.InverseLerp(acidRainSootThreshold, 1f, soot01);
                    float humidityT = Mathf.InverseLerp(acidRainHumidityThreshold, 1f, humidity01);
                    float chargeT = Mathf.Clamp01(rainCharge01);

                    return Mathf.Clamp01(
                        sootT * 0.55f +
                        humidityT * 0.25f +
                        chargeT * 0.20f);
                }

            case RainVisualKind.AshFall:
                {
                    float sootT = Mathf.InverseLerp(ashFallSootThreshold, 1f, soot01);
                    float drynessT = 1f - Mathf.InverseLerp(0f, ashFallMaxHumidity, humidity01);

                    return Mathf.Clamp01(
                        sootT * 0.70f +
                        drynessT * 0.30f);
                }

            default:
                return 0f;
        }
    }

    public RainSimulationSaveData SaveState()
    {
        RainSimulationSaveData data = new RainSimulationSaveData
        {
            activeRainCellCount = _activeRainCells != null ? _activeRainCells.Count : 0
        };

        if (!_isInitialized ||
            _rainChargeGrid == null ||
            _rainActiveGrid == null)
        {
            return data;
        }

        for (int x = 0; x < _cols; x++)
        {
            for (int y = 0; y < _rows; y++)
            {
                float charge = Mathf.Clamp01(_rainChargeGrid[x, y]);
                bool raining = _rainActiveGrid[x, y];

                float intensity = 0f;
                if (_rainIntensityGrid != null)
                    intensity = Mathf.Clamp01(_rainIntensityGrid[x, y]);
                else if (raining)
                    intensity = Mathf.Clamp01(Mathf.Lerp(minimumActiveRainIntensity, 1f, charge));

                bool hasSomethingToSave =
                    charge > 0.001f ||
                    intensity > 0.001f ||
                    raining;

                if (!hasSomethingToSave)
                    continue;

                data.rainCells.Add(new RainCellSaveData
                {
                    x = x,
                    y = y,

                    rain01 = raining ? intensity : 0f,
                    rainCharge01 = charge,

                    isRaining = raining,
                    rainIntensity01 = intensity,

                    visualKindValue = (int)GetRainVisualKindAtCell(x, y)
                });
            }
        }

        return data;
    }

    public void LoadState(RainSimulationSaveData data)
    {
        if (data == null)
            return;

        if (!TryInitializeGrid())
        {
            if (debugLogging) {}
                //Debug.LogWarning("[RainSimulationSystem] Could not load rain state because grid is not initialized yet.");

            return;
        }

        if (_waitForSourcesReadyCoroutine != null)
        {
            StopCoroutine(_waitForSourcesReadyCoroutine);
            _waitForSourcesReadyCoroutine = null;
        }

        if (_rainStepCoroutine != null)
        {
            StopCoroutine(_rainStepCoroutine);
            _rainStepCoroutine = null;
        }

        _rainAdvanceQueued = false;
        _isAdvancingRain = false;
        _lastRainAdvanceFrame = -1;

        StopRainVisualRefreshRoutine();
        ClearAllRainVisuals();

        Array.Clear(_rainChargeGrid, 0, _rainChargeGrid.Length);
        Array.Clear(_nextRainChargeGrid, 0, _nextRainChargeGrid.Length);

        if (_rainIntensityGrid != null)
            Array.Clear(_rainIntensityGrid, 0, _rainIntensityGrid.Length);

        if (_nextRainIntensityGrid != null)
            Array.Clear(_nextRainIntensityGrid, 0, _nextRainIntensityGrid.Length);

        Array.Clear(_rainActiveGrid, 0, _rainActiveGrid.Length);
        Array.Clear(_nextRainActiveGrid, 0, _nextRainActiveGrid.Length);

        _rainStartCandidates.Clear();
        _pendingDensityChanges.Clear();
        _activeRainCells.Clear();

        ClearRainCloudDarknessForLoad();

        int restoredCells = 0;
        int restoredActiveCells = 0;

        if (data.rainCells != null)
        {
            for (int i = 0; i < data.rainCells.Count; i++)
            {
                RainCellSaveData saved = data.rainCells[i];

                if (saved == null)
                    continue;

                if (!IsInBounds(saved.x, saved.y))
                    continue;

                float charge = Mathf.Clamp01(saved.rainCharge01);
                bool raining = RestoreRainActiveFromSave(data, saved, charge);

                float intensity = RestoreRainIntensityFromSave(data, saved, charge, raining);

                if (charge <= 0.001f && intensity <= 0.001f && !raining)
                    continue;

                _rainChargeGrid[saved.x, saved.y] = charge;

                if (_rainIntensityGrid != null)
                    _rainIntensityGrid[saved.x, saved.y] = raining ? intensity : 0f;

                _rainActiveGrid[saved.x, saved.y] = raining;

                TileCoord coord = new TileCoord(saved.x, saved.y);

                if (raining)
                {
                    _activeRainCells.Add(coord);
                    restoredActiveCells++;
                }

                if (cloudSimulationSystem != null)
                {
                    cloudSimulationSystem.SetExternalCloudDarkness01AtCell(
                        saved.x,
                        saved.y,
                        charge * cloudDarkenFromRainChargeStrength);
                }

                QueueRainVisualRefresh(saved.x, saved.y);
                restoredCells++;
            }
        }

        OnRainStateChanged?.Invoke();

        if (debugLogging)
        {
            //Debug.Log(
                //$"[RainSimulationSystem] Loaded rain state. " +
                //$"Cells={restoredCells}, Active={restoredActiveCells}");
        }
    }

    private bool RestoreRainActiveFromSave(
        RainSimulationSaveData data,
        RainCellSaveData saved,
        float charge)
    {
        if (saved == null)
            return false;

        // Version 2 saves active state directly.
        if (data != null && data.version >= 2)
            return saved.isRaining;

        // Old fallback for version 1 saves.
        return saved.rain01 > 0.001f || charge >= rainStartChargeThreshold;
    }

    private float RestoreRainIntensityFromSave(
        RainSimulationSaveData data,
        RainCellSaveData saved,
        float charge,
        bool raining)
    {
        if (saved == null || !raining)
            return 0f;

        if (data != null && data.version >= 2)
            return Mathf.Clamp01(saved.rainIntensity01);

        // Old fallback for version 1 saves.
        if (saved.rain01 > 0.001f)
            return Mathf.Clamp01(saved.rain01);

        return Mathf.Clamp01(Mathf.Lerp(minimumActiveRainIntensity, 1f, charge));
    }

    private void ClearRainCloudDarknessForLoad()
    {
        if (cloudSimulationSystem == null || !_isInitialized)
            return;

        for (int x = 0; x < _cols; x++)
        {
            for (int y = 0; y < _rows; y++)
                cloudSimulationSystem.SetExternalCloudDarkness01AtCell(x, y, 0f);
        }
    }

    public void ApplyPresetSettings(RainPresetSettings settings)
    {
        if (settings == null || !settings.overrideRain)
            return;

        rainHumidityThreshold = settings.rainHumidityThreshold;
        initialRainChargeFromHumidity = settings.initialRainChargeFromHumidity;
        rainChargeGainPerStep = settings.rainChargeGainPerStep;
        rainChargeLossPerStepWhenRaining = settings.rainChargeLossPerStepWhenRaining;
        rainChargePassiveLossPerStep = settings.rainChargePassiveLossPerStep;
        rainStartChargeThreshold = settings.rainStartChargeThreshold;
        rainStopChargeThreshold = settings.rainStopChargeThreshold;

        allowLowCloudsToStartRain = settings.allowLowCloudsToStartRain;
        maxNewRainCellsPerStep = settings.maxNewRainCellsPerStep;

        useRainIntensity = settings.useRainIntensity;
        minimumActiveRainIntensity = settings.minimumActiveRainIntensity;
        lightRainMaxIntensity = settings.lightRainMaxIntensity;
        heavyRainMinIntensity = settings.heavyRainMinIntensity;

        if (debugLogging) {}
            //Debug.Log("[RainSimulationSystem] Applied rain preset settings.");
    }
}
