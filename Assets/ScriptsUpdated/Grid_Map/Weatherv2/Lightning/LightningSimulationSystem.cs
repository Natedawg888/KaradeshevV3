using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CloudDensity = CloudSimulationSystem.CloudDensity;

/// <summary>
/// Request data used when a lightning-ready cell is converted into a queued burst.
/// </summary>
[Serializable]
public struct LightningBurstRequest
{
    public int originCellX;
    public int originCellY;
    public int strikeCount;
    public float strikeIntervalSeconds;

    public float sourceStormIntensity01;
    public float sourceCloudSupport01;
    public float sourceRainSupport01;
    public float sourceCharge;

    public Vector2Int preferredDirection;
    public float directionalShiftChance;
}

/// <summary>
/// Summary payload fired when a burst is queued.
/// </summary>
[Serializable]
public struct LightningBurstInfo
{
    public int burstId;
    public int originCellX;
    public int originCellY;
    public int strikeCount;
    public float strikeIntervalSeconds;

    public float sourceStormIntensity01;
    public float sourceCloudSupport01;
    public float sourceRainSupport01;
    public float sourceCharge;

    public Vector2Int preferredDirection;
    public float directionalShiftChance;
}

/// <summary>
/// Final strike payload for future visuals/effects.
/// </summary>
[Serializable]
public struct LightningStrikePayload
{
    public int burstId;

    public int originCellX;
    public int originCellY;

    public int resolvedCellX;
    public int resolvedCellY;

    public int strikeIndexInBurst;
    public int totalStrikesInBurst;

    public float sourceStormIntensity01;
    public float sourceCloudSupport01;
    public float sourceRainSupport01;
    public float sourceCharge;

    /// <summary>
    /// Local offset inside the resolved cell in normalized cell space.
    /// Expected range is roughly [-0.5, 0.5] on each axis.
    /// </summary>
    public Vector2 localCellOffset01;

    public Vector2Int preferredDirection;
    public bool shiftedToNeighbourCell;
}

internal struct LightningCandidate
{
    public int x;
    public int y;

    public float score;
    public float charge;
    public float stormIntensity01;
    public float cloudSupport01;
    public float rainSupport01;

    public Vector2Int preferredDirection;
    public float directionalShiftChance;
}

internal sealed class QueuedLightningBurst
{
    public int BurstId;
    public int OriginCellX;
    public int OriginCellY;

    public int TotalStrikes;
    public int NextStrikeIndex;
    public float StrikeIntervalSeconds;
    public float NextStrikeTime;

    public float SourceStormIntensity01;
    public float SourceCloudSupport01;
    public float SourceRainSupport01;
    public float SourceCharge;

    public Vector2Int PreferredDirection;
    public float DirectionalShiftChance;
}

/// <summary>
/// Lightning-only simulation layered on top of WeatherGridManager + CloudSimulationSystem +
/// RainSimulationSystem + StormSimulationSystem.
/// Owns lightning charge state, burst scheduling, and queued strike resolution.
/// No visuals or gameplay consequence logic here.
/// Sparse processing version: active storm cells + charged cells + charge carry targets.
/// </summary>
public class LightningSimulationSystem : MonoBehaviour
{
    public static LightningSimulationSystem Instance { get; private set; }

    [Header("References")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private WeatherGridManager weatherGridManager;
    [SerializeField] private CloudSimulationSystem cloudSimulationSystem;
    [SerializeField] private RainSimulationSystem rainSimulationSystem;
    [SerializeField] private StormSimulationSystem stormSimulationSystem;

    [Header("Lifecycle")]
    [SerializeField] private bool initializeOnStart = true;
    [SerializeField] private bool advanceOnWeatherStateRefreshed = true;
    [SerializeField] private bool advanceOnStormStateChanged = true;
    [SerializeField] private bool advanceWithTurnSystem = false;
    [Min(1)][SerializeField] private int simulationStepsPerAdvance = 1;

    [Header("Charge Formation")]
    [Range(0f, 1f)][SerializeField] private float minStormIntensityForCharge = 0.35f;
    [Range(0f, 1f)][SerializeField] private float minHumidityForChargeSupport = 0.50f;
    [Min(0f)][SerializeField] private float maxLightningCharge = 1.50f;

    [Min(0f)][SerializeField] private float baseChargeGainPerStep = 0.05f;
    [Min(0f)][SerializeField] private float stormChargeGainPerStep = 0.30f;
    [Min(0f)][SerializeField] private float cloudChargeGainPerStep = 0.18f;
    [Min(0f)][SerializeField] private float rainChargeGainPerStep = 0.22f;

    [Range(0f, 1f)][SerializeField] private float chargeCarryForwardFactor = 0.65f;
    [Min(0f)][SerializeField] private float passiveChargeDecayPerStep = 0.10f;
    [Min(0f)][SerializeField] private float nonStormChargeLeakPerStep = 0.20f;

    [Header("Strike Readiness")]
    [Min(0f)][SerializeField] private float strikeReadyThreshold = 1.00f;
    [Range(0f, 1f)][SerializeField] private float readyStrikeChancePerStep = 0.35f;
    [Range(0f, 1f)][SerializeField] private float minStormIntensityForBurst = 0.45f;

    [Header("Burst Rules")]
    [Min(1)][SerializeField] private int maxQueuedBursts = 24;
    [Min(1)][SerializeField] private int maxBurstsQueuedPerSimulationStep = 4;
    [Min(1)][SerializeField] private int maxReadyCandidatesTrackedPerStep = 32;
    [Min(1)][SerializeField] private int maxTotalQueuedStrikeCount = 64;

    [Min(1)][SerializeField] private int minStrikesPerBurst = 1;
    [Min(1)][SerializeField] private int maxStrikesPerBurst = 3;

    [Min(0f)][SerializeField] private float minStrikeIntervalSeconds = 0.05f;
    [Min(0f)][SerializeField] private float maxStrikeIntervalSeconds = 0.15f;

    [Min(0f)][SerializeField] private float chargeConsumedPerBurst = 0.55f;
    [Min(0f)][SerializeField] private float extraChargeConsumedPerAdditionalStrike = 0.10f;
    [Min(0f)][SerializeField] private float minChargeAfterBurst = 0.05f;

    [Header("Strike Direction / Position Bias")]
    [SerializeField] private bool enableDirectionalBias = true;
    [Range(0f, 1f)][SerializeField] private float baseDirectionalShiftChance = 0.10f;
    [Range(0f, 1f)][SerializeField] private float maxDirectionalShiftChance = 0.45f;
    [Range(0f, 0.49f)][SerializeField] private float strikeJitterRadius01 = 0.30f;
    [Range(0f, 0.49f)][SerializeField] private float directionalOffsetPull01 = 0.18f;
    [Range(0f, 1f)][SerializeField] private float stormNeighbourBiasWeight = 1.00f;
    [Range(0f, 1f)][SerializeField] private float rainNeighbourBiasWeight = 0.15f;

    [Header("Queued Strike Processing")]
    [SerializeField] private bool processQueuedBurstsOverFrames = true;
    [Min(1)][SerializeField] private int maxStrikeSchedulesPerFrame = 4;

    [Header("Lightning Sparse Batching")]
    [SerializeField] private bool batchLightningStateOverFrames = true;
    [Min(1)][SerializeField] private int lightningCellsPerFrame = 64;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = false;

    public event Action OnLightningGridInitialized;
    public event Action OnLightningStateChanged;
    public event Action<LightningBurstInfo> OnLightningBurstQueued;
    public event Action<LightningStrikePayload> OnLightningStrikeScheduled;
    public event Action<LightningStrikePayload> OnLightningStrikeResolved;

    public int Columns => _cols;
    public int Rows => _rows;
    public bool IsInitialized => _isInitialized;
    public bool HasAnyLightningCharge => _chargedCells.Count > 0;

    private int _cols;
    private int _rows;
    private bool _isInitialized;

    private float[,] _chargeGrid;
    private float[,] _nextChargeGrid;
    private bool[,] _chargeReadyGrid;

    private bool _isAdvancingLightning;
    private bool _lightningAdvanceQueued;
    private int _lastLightningAdvanceFrame = -1;
    private int _nextBurstId = 1;

    private Coroutine _waitForSourcesReadyCoroutine;
    private Coroutine _lightningStepCoroutine;
    private Coroutine _burstQueueCoroutine;

    private WeatherGridManager _subscribedWeatherGridManager;
    private CloudSimulationSystem _subscribedCloudSimulationSystem;
    private StormSimulationSystem _subscribedStormSimulationSystem;

    private readonly List<LightningCandidate> _readyCandidates = new List<LightningCandidate>(32);
    private readonly List<QueuedLightningBurst> _queuedBursts = new List<QueuedLightningBurst>(24);

    private List<Vector2Int> _chargedCells = new List<Vector2Int>(128);
    private List<Vector2Int> _nextChargedCells = new List<Vector2Int>(128);

    private readonly List<Vector2Int> _cellsToProcess = new List<Vector2Int>(256);
    private readonly HashSet<int> _cellsToProcessKeys = new HashSet<int>();
    private readonly HashSet<int> _nextChargedCellKeys = new HashSet<int>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsureLinks();
    }

    private void OnEnable()
    {
        EnsureLinks();
        RebindSourceEvents();

        TurnSystem.SubscribeToStartOfTurn(HandleStartOfTurn);
        BeginWaitingForSourcesReady();
    }

    private void Start()
    {
        if (initializeOnStart)
            BeginWaitingForSourcesReady();
    }

    private void OnDisable()
    {
        TurnSystem.UnsubscribeFromStartOfTurn(HandleStartOfTurn);
        UnbindSourceEvents();

        if (_waitForSourcesReadyCoroutine != null)
        {
            StopCoroutine(_waitForSourcesReadyCoroutine);
            _waitForSourcesReadyCoroutine = null;
        }

        if (_lightningStepCoroutine != null)
        {
            StopCoroutine(_lightningStepCoroutine);
            _lightningStepCoroutine = null;
        }

        if (_burstQueueCoroutine != null)
        {
            StopCoroutine(_burstQueueCoroutine);
            _burstQueueCoroutine = null;
        }

        _lightningAdvanceQueued = false;
        _isAdvancingLightning = false;
    }

    private void OnDestroy()
    {
        UnbindSourceEvents();

        if (Instance == this)
            Instance = null;
    }

    public void InstallRuntimeRefs(
        GridManager newGridManager,
        WeatherGridManager newWeatherGridManager,
        CloudSimulationSystem newCloudSimulationSystem,
        RainSimulationSystem newRainSimulationSystem,
        StormSimulationSystem newStormSimulationSystem,
        bool initializeNow = true)
    {
        if (newGridManager != null)
            gridManager = newGridManager;

        if (newWeatherGridManager != null)
            weatherGridManager = newWeatherGridManager;

        if (newCloudSimulationSystem != null)
            cloudSimulationSystem = newCloudSimulationSystem;

        if (newRainSimulationSystem != null)
            rainSimulationSystem = newRainSimulationSystem;

        if (newStormSimulationSystem != null)
            stormSimulationSystem = newStormSimulationSystem;

        RebindSourceEvents();

        if (initializeNow)
            TryInitializeGrid();
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
            RebindSourceEvents();

            if (TryInitializeGrid())
            {
                if (debugLogging)
                    //Debug.Log("[LightningSimulationSystem] Sources ready. Lightning system initialized.");

                _waitForSourcesReadyCoroutine = null;
                yield break;
            }

            yield return null;
        }
    }

    public bool TryInitializeGrid()
    {
        EnsureLinks();

        if (weatherGridManager == null || !weatherGridManager.IsInitialized)
            return false;

        if (cloudSimulationSystem == null || !cloudSimulationSystem.IsInitialized)
            return false;

        if (rainSimulationSystem == null || !rainSimulationSystem.IsInitialized)
            return false;

        if (stormSimulationSystem == null || !stormSimulationSystem.IsInitialized)
            return false;

        if (gridManager == null)
            return false;

        int newCols = weatherGridManager.Columns;
        int newRows = weatherGridManager.Rows;

        if (newCols <= 0 || newRows <= 0)
            return false;

        bool sizeChanged = !_isInitialized || newCols != _cols || newRows != _rows;

        _cols = newCols;
        _rows = newRows;

        if (sizeChanged)
        {
            _chargeGrid = new float[_cols, _rows];
            _nextChargeGrid = new float[_cols, _rows];
            _chargeReadyGrid = new bool[_cols, _rows];

            _readyCandidates.Clear();
            _queuedBursts.Clear();
            _chargedCells.Clear();
            _nextChargedCells.Clear();
            _cellsToProcess.Clear();
            _cellsToProcessKeys.Clear();
            _nextChargedCellKeys.Clear();
        }

        _isInitialized = true;

        if (sizeChanged)
            OnLightningGridInitialized?.Invoke();

        if (debugLogging && sizeChanged)
            //Debug.Log($"[LightningSimulationSystem] Initialized {_cols}x{_rows}");

        return true;
    }

    public void AdvanceLightningOneStep()
    {
        if (!batchLightningStateOverFrames)
        {
            AdvanceLightningOneStepImmediate();
            return;
        }

        if (_lightningStepCoroutine != null)
        {
            _lightningAdvanceQueued = true;
            return;
        }

        _lightningStepCoroutine = StartCoroutine(AdvanceLightningOneStepBatchedRoutine());
    }

    public void RequestAdvanceLightning()
    {
        if (!TryInitializeGrid())
            return;

        bool hasStormWork = stormSimulationSystem != null && stormSimulationSystem.HasAnyActiveStorms;
        bool hasChargeWork = _chargedCells.Count > 0;

        if (!hasStormWork && !hasChargeWork)
            return;

        if (_lastLightningAdvanceFrame == Time.frameCount)
            return;

        _lastLightningAdvanceFrame = Time.frameCount;
        AdvanceLightningOneStep();
    }

    private void AdvanceLightningOneStepImmediate()
    {
        if (_isAdvancingLightning)
            return;

        if (!TryInitializeGrid())
            return;

        bool hasStormWork = stormSimulationSystem != null && stormSimulationSystem.HasAnyActiveStorms;
        bool hasChargeWork = _chargedCells.Count > 0;

        if (!hasStormWork && !hasChargeWork)
            return;

        _isAdvancingLightning = true;
        try
        {
            bool anyChanged = false;
            int steps = Mathf.Max(1, simulationStepsPerAdvance);

            for (int i = 0; i < steps; i++)
            {
                BeginLightningStepBuffers();
                BuildTrackedLightningCellSet();
                ProcessChargeCarryTrackedCells();

                if (ProcessChargeRebuildTrackedCells(0, _cellsToProcess.Count))
                    anyChanged = true;

                if (QueueBurstsFromReadyCandidates())
                    anyChanged = true;

                CommitChargedCellSnapshot();
            }

            if (anyChanged)
                OnLightningStateChanged?.Invoke();

            if (debugLogging)
                //Debug.Log("[LightningSimulationSystem] Advanced lightning one sparse step.");
        }
        finally
        {
            _isAdvancingLightning = false;
        }
    }

    private IEnumerator AdvanceLightningOneStepBatchedRoutine()
    {
        if (_isAdvancingLightning || !TryInitializeGrid())
        {
            _lightningStepCoroutine = null;
            yield break;
        }

        bool hasStormWork = stormSimulationSystem != null && stormSimulationSystem.HasAnyActiveStorms;
        bool hasChargeWork = _chargedCells.Count > 0;

        if (!hasStormWork && !hasChargeWork)
        {
            _lightningStepCoroutine = null;
            yield break;
        }

        _isAdvancingLightning = true;
        bool anyChanged = false;
        int cellsPerFrame = Mathf.Max(1, lightningCellsPerFrame);

        try
        {
            int steps = Mathf.Max(1, simulationStepsPerAdvance);

            for (int step = 0; step < steps; step++)
            {
                BeginLightningStepBuffers();
                BuildTrackedLightningCellSet();
                ProcessChargeCarryTrackedCells();

                for (int start = 0; start < _cellsToProcess.Count; start += cellsPerFrame)
                {
                    int end = Mathf.Min(start + cellsPerFrame, _cellsToProcess.Count);

                    if (ProcessChargeRebuildTrackedCells(start, end))
                        anyChanged = true;

                    if (end < _cellsToProcess.Count)
                        yield return null;
                }

                if (QueueBurstsFromReadyCandidates())
                    anyChanged = true;

                CommitChargedCellSnapshot();
            }

            if (anyChanged)
                OnLightningStateChanged?.Invoke();

            if (debugLogging)
                //Debug.Log("[LightningSimulationSystem] Advanced lightning one sparse batched step.");
        }
        finally
        {
            _isAdvancingLightning = false;
            _lightningStepCoroutine = null;

            if (_lightningAdvanceQueued && isActiveAndEnabled)
            {
                _lightningAdvanceQueued = false;
                _lightningStepCoroutine = StartCoroutine(AdvanceLightningOneStepBatchedRoutine());
            }
            else
            {
                _lightningAdvanceQueued = false;
            }
        }
    }

    private void BeginLightningStepBuffers()
    {
        Array.Clear(_nextChargeGrid, 0, _nextChargeGrid.Length);

        _readyCandidates.Clear();

        _cellsToProcess.Clear();
        _cellsToProcessKeys.Clear();

        _nextChargedCells.Clear();
        _nextChargedCellKeys.Clear();
    }

    private int GetCellKey(int x, int y)
    {
        return x + (y * Mathf.Max(1, _cols));
    }

    private void AddCellToProcess(int x, int y)
    {
        if (!IsInBounds(x, y))
            return;

        int key = GetCellKey(x, y);
        if (_cellsToProcessKeys.Add(key))
            _cellsToProcess.Add(new Vector2Int(x, y));
    }

    private void AddNextChargedCell(int x, int y)
    {
        if (!IsInBounds(x, y))
            return;

        int key = GetCellKey(x, y);
        if (_nextChargedCellKeys.Add(key))
            _nextChargedCells.Add(new Vector2Int(x, y));
    }

    private void RemoveNextChargedCell(int x, int y)
    {
        int key = GetCellKey(x, y);
        if (!_nextChargedCellKeys.Remove(key))
            return;

        for (int i = 0; i < _nextChargedCells.Count; i++)
        {
            Vector2Int cell = _nextChargedCells[i];
            if (cell.x == x && cell.y == y)
            {
                _nextChargedCells.RemoveAt(i);
                return;
            }
        }
    }

    private void BuildTrackedLightningCellSet()
    {
        if (stormSimulationSystem != null && stormSimulationSystem.HasAnyActiveStorms)
        {
            IReadOnlyList<Vector2Int> stormCells = stormSimulationSystem.ActiveStormCells;

            for (int i = 0; i < stormCells.Count; i++)
            {
                Vector2Int cell = stormCells[i];
                AddCellToProcess(cell.x, cell.y);
            }
        }

        for (int i = 0; i < _chargedCells.Count; i++)
        {
            Vector2Int cell = _chargedCells[i];
            AddCellToProcess(cell.x, cell.y);

            Vector2Int target = GetChargeCarryTarget(cell.x, cell.y);
            AddCellToProcess(target.x, target.y);
        }
    }

    private void CommitChargedCellSnapshot()
    {
        List<Vector2Int> temp = _chargedCells;
        _chargedCells = _nextChargedCells;
        _nextChargedCells = temp;

        _nextChargedCells.Clear();
        _nextChargedCellKeys.Clear();
    }

    private void ProcessChargeCarryTrackedCells()
    {
        float carryFactor = Mathf.Clamp01(chargeCarryForwardFactor);

        for (int i = 0; i < _chargedCells.Count; i++)
        {
            Vector2Int cell = _chargedCells[i];
            int x = cell.x;
            int y = cell.y;

            if (!IsInBounds(x, y))
                continue;

            float currentCharge = _chargeGrid[x, y];
            if (currentCharge <= 0.0001f)
                continue;

            Vector2Int target = GetChargeCarryTarget(x, y);
            if (!IsInBounds(target.x, target.y))
                target = cell;

            float carriedCharge = currentCharge * carryFactor;
            if (carriedCharge <= 0.0001f)
                continue;

            if (carriedCharge > _nextChargeGrid[target.x, target.y])
                _nextChargeGrid[target.x, target.y] = carriedCharge;
        }
    }

    private bool ProcessChargeRebuildTrackedCells(int startIndex, int endIndex)
    {
        bool anyChanged = false;

        for (int i = startIndex; i < endIndex; i++)
        {
            Vector2Int cell = _cellsToProcess[i];
            int x = cell.x;
            int y = cell.y;

            float carriedCharge = _nextChargeGrid[x, y];
            float oldCharge = _chargeGrid[x, y];
            bool oldReady = _chargeReadyGrid[x, y];

            float stormIntensity01;
            float cloudSupport01;
            float rainSupport01;
            float humiditySupport01;

            float support01 = CalculateLightningSupport01(
                x,
                y,
                out stormIntensity01,
                out cloudSupport01,
                out rainSupport01,
                out humiditySupport01);

            float charge = carriedCharge;

            if (support01 > 0f)
            {
                float gain =
                    baseChargeGainPerStep +
                    stormChargeGainPerStep * stormIntensity01 +
                    cloudChargeGainPerStep * cloudSupport01 +
                    rainChargeGainPerStep * rainSupport01;

                gain *= support01;
                charge = Mathf.Clamp(charge + gain, 0f, Mathf.Max(strikeReadyThreshold, maxLightningCharge));
            }
            else
            {
                float decay = passiveChargeDecayPerStep;

                if (stormIntensity01 < minStormIntensityForCharge)
                    decay += nonStormChargeLeakPerStep;

                charge = Mathf.Max(0f, charge - decay);
            }

            bool ready =
                charge >= strikeReadyThreshold &&
                stormIntensity01 >= minStormIntensityForBurst &&
                support01 > 0f;

            _chargeGrid[x, y] = charge;
            _chargeReadyGrid[x, y] = ready;

            if (charge > 0.001f)
                AddNextChargedCell(x, y);

            if (Mathf.Abs(oldCharge - charge) > 0.01f || oldReady != ready)
                anyChanged = true;

            if (!ready)
                continue;

            float normalizedCharge = Mathf.InverseLerp(
                strikeReadyThreshold,
                Mathf.Max(strikeReadyThreshold + 0.001f, maxLightningCharge),
                charge);

            float readinessScore = Mathf.Clamp01(
                stormIntensity01 * 0.50f +
                normalizedCharge * 0.30f +
                cloudSupport01 * 0.10f +
                rainSupport01 * 0.10f);

            float strikeChance = readyStrikeChancePerStep * readinessScore;
            if (UnityEngine.Random.value > strikeChance)
                continue;

            Vector2Int preferredDirection = GetPreferredStrikeDirectionForCell(x, y);
            float directionalShiftChance = GetDirectionalShiftChance(stormIntensity01, rainSupport01);

            LightningCandidate candidate = new LightningCandidate
            {
                x = x,
                y = y,
                score = readinessScore,
                charge = charge,
                stormIntensity01 = stormIntensity01,
                cloudSupport01 = cloudSupport01,
                rainSupport01 = rainSupport01,
                preferredDirection = preferredDirection,
                directionalShiftChance = directionalShiftChance
            };

            TryTrackCandidate(candidate);
        }

        return anyChanged;
    }

    private bool QueueBurstsFromReadyCandidates()
    {
        if (_readyCandidates.Count == 0)
            return false;

        int availableBurstSlots = Mathf.Max(0, maxQueuedBursts - _queuedBursts.Count);
        if (availableBurstSlots <= 0)
            return false;

        int burstBudget = Mathf.Min(
            Mathf.Max(1, maxBurstsQueuedPerSimulationStep),
            availableBurstSlots);

        _readyCandidates.Sort((a, b) => b.score.CompareTo(a.score));

        bool anyQueued = false;
        int queuedThisStep = 0;
        int totalQueuedStrikeCount = GetQueuedStrikeCount();

        for (int i = 0; i < _readyCandidates.Count; i++)
        {
            if (queuedThisStep >= burstBudget)
                break;

            LightningCandidate candidate = _readyCandidates[i];

            if (!IsInBounds(candidate.x, candidate.y))
                continue;

            float currentCharge = _chargeGrid[candidate.x, candidate.y];
            if (currentCharge < strikeReadyThreshold)
                continue;

            int strikeCount = DetermineStrikeCountForCandidate(candidate);
            if (strikeCount <= 0)
                continue;

            if (totalQueuedStrikeCount + strikeCount > maxTotalQueuedStrikeCount)
                break;

            float interval = DetermineStrikeIntervalForCandidate(candidate);

            LightningBurstRequest request = new LightningBurstRequest
            {
                originCellX = candidate.x,
                originCellY = candidate.y,
                strikeCount = strikeCount,
                strikeIntervalSeconds = interval,
                sourceStormIntensity01 = candidate.stormIntensity01,
                sourceCloudSupport01 = candidate.cloudSupport01,
                sourceRainSupport01 = candidate.rainSupport01,
                sourceCharge = currentCharge,
                preferredDirection = candidate.preferredDirection,
                directionalShiftChance = candidate.directionalShiftChance
            };

            QueueBurst(request);
            ConsumeChargeForBurst(candidate.x, candidate.y, strikeCount);

            totalQueuedStrikeCount += strikeCount;
            queuedThisStep++;
            anyQueued = true;
        }

        if (anyQueued)
            EnsureBurstQueueRoutine();

        return anyQueued;
    }

    private void QueueBurst(LightningBurstRequest request)
    {
        int strikeCount = Mathf.Max(1, request.strikeCount);
        float intervalSeconds = Mathf.Max(0f, request.strikeIntervalSeconds);

        QueuedLightningBurst queued = new QueuedLightningBurst
        {
            BurstId = _nextBurstId++,
            OriginCellX = request.originCellX,
            OriginCellY = request.originCellY,
            TotalStrikes = strikeCount,
            NextStrikeIndex = 0,
            StrikeIntervalSeconds = intervalSeconds,
            NextStrikeTime = Time.time,
            SourceStormIntensity01 = Mathf.Clamp01(request.sourceStormIntensity01),
            SourceCloudSupport01 = Mathf.Clamp01(request.sourceCloudSupport01),
            SourceRainSupport01 = Mathf.Clamp01(request.sourceRainSupport01),
            SourceCharge = Mathf.Max(0f, request.sourceCharge),
            PreferredDirection = request.preferredDirection,
            DirectionalShiftChance = Mathf.Clamp01(request.directionalShiftChance)
        };

        _queuedBursts.Add(queued);

        LightningBurstInfo info = new LightningBurstInfo
        {
            burstId = queued.BurstId,
            originCellX = queued.OriginCellX,
            originCellY = queued.OriginCellY,
            strikeCount = queued.TotalStrikes,
            strikeIntervalSeconds = queued.StrikeIntervalSeconds,
            sourceStormIntensity01 = queued.SourceStormIntensity01,
            sourceCloudSupport01 = queued.SourceCloudSupport01,
            sourceRainSupport01 = queued.SourceRainSupport01,
            sourceCharge = queued.SourceCharge,
            preferredDirection = queued.PreferredDirection,
            directionalShiftChance = queued.DirectionalShiftChance
        };

        OnLightningBurstQueued?.Invoke(info);

        if (debugLogging)
        {
            //Debug.Log(
                //$"[LightningSimulationSystem] Queued burst {info.burstId} at ({info.originCellX},{info.originCellY}) " +
                //$"strikes={info.strikeCount} interval={info.strikeIntervalSeconds:0.00}");
        }
    }

    private void EnsureBurstQueueRoutine()
    {
        if (!processQueuedBurstsOverFrames)
        {
            ResolveDueStrikesImmediate();
            return;
        }

        if (_burstQueueCoroutine != null || !isActiveAndEnabled)
            return;

        _burstQueueCoroutine = StartCoroutine(BurstQueueRoutine());
    }

    private IEnumerator BurstQueueRoutine()
    {
        while (_queuedBursts.Count > 0)
        {
            ResolveDueStrikesImmediate();
            yield return null;
        }

        _burstQueueCoroutine = null;
    }

    private void ResolveDueStrikesImmediate()
    {
        if (_queuedBursts.Count == 0)
            return;

        float now = Time.time;
        int scheduledThisFrame = 0;
        int perFrameCap = Mathf.Max(1, maxStrikeSchedulesPerFrame);

        for (int i = 0; i < _queuedBursts.Count && scheduledThisFrame < perFrameCap; i++)
        {
            QueuedLightningBurst burst = _queuedBursts[i];
            if (burst == null)
                continue;

            if (now + 0.0001f < burst.NextStrikeTime)
                continue;

            LightningStrikePayload payload = BuildStrikePayload(burst);

            OnLightningStrikeScheduled?.Invoke(payload);
            OnLightningStrikeResolved?.Invoke(payload);

            scheduledThisFrame++;

            burst.NextStrikeIndex++;

            if (burst.NextStrikeIndex >= burst.TotalStrikes)
            {
                _queuedBursts.RemoveAt(i);
                i--;
            }
            else
            {
                burst.NextStrikeTime = now + burst.StrikeIntervalSeconds;
            }
        }
    }

    private LightningStrikePayload BuildStrikePayload(QueuedLightningBurst burst)
    {
        int resolvedX = burst.OriginCellX;
        int resolvedY = burst.OriginCellY;
        bool shiftedToNeighbour = false;

        TryResolveStrikeTargetCell(
            burst.OriginCellX,
            burst.OriginCellY,
            burst.PreferredDirection,
            burst.DirectionalShiftChance,
            out resolvedX,
            out resolvedY,
            out shiftedToNeighbour);

        Vector2 localOffset01 = GetBiasedStrikeOffsetInsideCell(
            burst.PreferredDirection,
            burst.DirectionalShiftChance,
            shiftedToNeighbour);

        LightningStrikePayload payload = new LightningStrikePayload
        {
            burstId = burst.BurstId,
            originCellX = burst.OriginCellX,
            originCellY = burst.OriginCellY,
            resolvedCellX = resolvedX,
            resolvedCellY = resolvedY,
            strikeIndexInBurst = burst.NextStrikeIndex,
            totalStrikesInBurst = burst.TotalStrikes,
            sourceStormIntensity01 = burst.SourceStormIntensity01,
            sourceCloudSupport01 = burst.SourceCloudSupport01,
            sourceRainSupport01 = burst.SourceRainSupport01,
            sourceCharge = burst.SourceCharge,
            localCellOffset01 = localOffset01,
            preferredDirection = burst.PreferredDirection,
            shiftedToNeighbourCell = shiftedToNeighbour
        };

        return payload;
    }

    private void TryResolveStrikeTargetCell(
        int originX,
        int originY,
        Vector2Int preferredDirection,
        float directionalShiftChance,
        out int resolvedX,
        out int resolvedY,
        out bool shiftedToNeighbour)
    {
        resolvedX = originX;
        resolvedY = originY;
        shiftedToNeighbour = false;

        if (!enableDirectionalBias)
            return;

        if (preferredDirection == Vector2Int.zero)
            return;

        if (UnityEngine.Random.value > directionalShiftChance)
            return;

        int targetX = originX + preferredDirection.x;
        int targetY = originY + preferredDirection.y;

        if (!IsInBounds(targetX, targetY))
            return;

        float originStormIntensity = stormSimulationSystem != null
            ? stormSimulationSystem.GetStormIntensity01AtCell(originX, originY)
            : 0f;

        float targetStormIntensity = stormSimulationSystem != null
            ? stormSimulationSystem.GetStormIntensity01AtCell(targetX, targetY)
            : 0f;

        if (targetStormIntensity < minStormIntensityForCharge)
            return;

        if (targetStormIntensity + 0.01f < originStormIntensity)
            return;

        resolvedX = targetX;
        resolvedY = targetY;
        shiftedToNeighbour = true;
    }

    public float GetLightningChargeAtCell(int x, int y)
    {
        if (!_isInitialized || !IsInBounds(x, y) || _chargeGrid == null)
            return 0f;

        return Mathf.Max(0f, _chargeGrid[x, y]);
    }

    public bool IsLightningChargeReadyAtCell(int x, int y)
    {
        if (!_isInitialized || !IsInBounds(x, y) || _chargeReadyGrid == null)
            return false;

        return _chargeReadyGrid[x, y];
    }

    public int GetQueuedLightningBurstCount()
    {
        return _queuedBursts.Count;
    }

    public bool IsGoodLightningCandidateThisStep(int x, int y)
    {
        if (!_isInitialized || !IsInBounds(x, y))
            return false;

        float stormIntensity01;
        float cloudSupport01;
        float rainSupport01;
        float humiditySupport01;

        float support01 = CalculateLightningSupport01(
            x,
            y,
            out stormIntensity01,
            out cloudSupport01,
            out rainSupport01,
            out humiditySupport01);

        return
            support01 > 0f &&
            stormIntensity01 >= minStormIntensityForBurst &&
            _chargeGrid[x, y] >= strikeReadyThreshold;
    }

    public Vector2Int GetPreferredStrikeDirectionForCell(int x, int y)
    {
        if (!_isInitialized || !enableDirectionalBias || !IsInBounds(x, y))
            return Vector2Int.zero;

        float originStormIntensity = stormSimulationSystem != null
            ? stormSimulationSystem.GetStormIntensity01AtCell(x, y)
            : 0f;

        Vector2Int bestDirection = Vector2Int.zero;
        float bestScore = originStormIntensity;

        for (int oy = -1; oy <= 1; oy++)
        {
            for (int ox = -1; ox <= 1; ox++)
            {
                if (ox == 0 && oy == 0)
                    continue;

                int nx = x + ox;
                int ny = y + oy;

                if (!IsInBounds(nx, ny))
                    continue;

                float stormScore = stormSimulationSystem != null
                    ? stormSimulationSystem.GetStormIntensity01AtCell(nx, ny)
                    : 0f;

                float rainScore = rainSimulationSystem != null
                    ? GetRainSupport01(nx, ny) * rainNeighbourBiasWeight
                    : 0f;

                float totalScore = stormScore * stormNeighbourBiasWeight + rainScore;

                if (totalScore > bestScore + 0.0001f)
                {
                    bestScore = totalScore;
                    bestDirection = new Vector2Int(ox, oy);
                }
            }
        }

        return bestDirection;
    }

    public Vector2 GetBiasedStrikeOffsetInsideCell(
        Vector2Int preferredDirection,
        float directionalShiftChance,
        bool shiftedToNeighbourCell = false)
    {
        float radius = Mathf.Clamp(strikeJitterRadius01, 0f, 0.49f);

        Vector2 offset = new Vector2(
            UnityEngine.Random.Range(-radius, radius),
            UnityEngine.Random.Range(-radius, radius));

        if (preferredDirection != Vector2Int.zero)
        {
            Vector2 dir = new Vector2(preferredDirection.x, preferredDirection.y).normalized;
            float pull = Mathf.Lerp(0f, directionalOffsetPull01, Mathf.Clamp01(directionalShiftChance));

            if (shiftedToNeighbourCell)
                pull *= 1.20f;

            offset += dir * pull;
        }

        offset.x = Mathf.Clamp(offset.x, -0.49f, 0.49f);
        offset.y = Mathf.Clamp(offset.y, -0.49f, 0.49f);

        return offset;
    }

    public bool TryGetLightningStrikeWorldPosition(
        LightningStrikePayload payload,
        out Vector3 worldPosition,
        float extraHeight = 0f)
    {
        worldPosition = Vector3.zero;

        if (!_isInitialized || gridManager == null)
            return false;

        if (!IsInBounds(payload.resolvedCellX, payload.resolvedCellY))
            return false;

        Vector3 cellCorner = gridManager.GetWorldPosition(payload.resolvedCellX, payload.resolvedCellY);
        float halfCell = gridManager.cellSize * 0.5f;

        worldPosition = new Vector3(
            cellCorner.x + halfCell + payload.localCellOffset01.x * gridManager.cellSize,
            cellCorner.y + extraHeight,
            cellCorner.z + halfCell + payload.localCellOffset01.y * gridManager.cellSize);

        return true;
    }

    public void ClearAllLightningCharge()
    {
        if (!TryInitializeGrid())
            return;

        for (int x = 0; x < _cols; x++)
        {
            for (int y = 0; y < _rows; y++)
            {
                _chargeGrid[x, y] = 0f;
                _chargeReadyGrid[x, y] = false;
            }
        }

        _queuedBursts.Clear();
        _chargedCells.Clear();
        _nextChargedCells.Clear();
        _cellsToProcess.Clear();
        _cellsToProcessKeys.Clear();
        _nextChargedCellKeys.Clear();

        OnLightningStateChanged?.Invoke();
    }

    private float CalculateLightningSupport01(
        int x,
        int y,
        out float stormIntensity01,
        out float cloudSupport01,
        out float rainSupport01,
        out float humiditySupport01)
    {
        stormIntensity01 = 0f;
        cloudSupport01 = 0f;
        rainSupport01 = 0f;
        humiditySupport01 = 0f;

        if (!_isInitialized || !IsInBounds(x, y))
            return 0f;

        if (stormSimulationSystem != null)
            stormIntensity01 = stormSimulationSystem.GetStormIntensity01AtCell(x, y);

        if (stormIntensity01 < minStormIntensityForCharge)
            return 0f;

        cloudSupport01 = GetCloudSupport01(x, y);
        rainSupport01 = GetRainSupport01(x, y);
        humiditySupport01 = GetHumiditySupport01(x, y);

        float support01 = Mathf.Clamp01(
            stormIntensity01 * 0.55f +
            cloudSupport01 * 0.20f +
            rainSupport01 * 0.15f +
            humiditySupport01 * 0.10f);

        return support01;
    }

    private float GetCloudSupport01(int x, int y)
    {
        if (cloudSimulationSystem == null)
            return 0f;

        switch (cloudSimulationSystem.GetCloudDensityAtCell(x, y))
        {
            case CloudDensity.Low: return 0.20f;
            case CloudDensity.Mid: return 0.60f;
            case CloudDensity.High: return 1f;
            default: return 0f;
        }
    }

    private float GetRainSupport01(int x, int y)
    {
        if (rainSimulationSystem == null)
            return 0f;

        float rainCharge01 = rainSimulationSystem.GetRainCharge01AtCell(x, y);
        bool raining = rainSimulationSystem.IsRainingAtCell(x, y);

        if (raining)
            return Mathf.Max(rainCharge01, 1f);

        return Mathf.Clamp01(rainCharge01);
    }

    private float GetHumiditySupport01(int x, int y)
    {
        if (weatherGridManager == null)
            return 0f;

        WeatherCellState state;
        if (!weatherGridManager.TryGetCellState(x, y, out state) || !state.isValid)
            return 0f;

        if (state.humidity01 < minHumidityForChargeSupport)
            return 0f;

        return Mathf.InverseLerp(minHumidityForChargeSupport, 1f, state.humidity01);
    }

    private Vector2Int GetChargeCarryTarget(int x, int y)
    {
        bool stormActiveHere = stormSimulationSystem != null && stormSimulationSystem.IsStormActiveAtCell(x, y);
        if (!stormActiveHere)
            return new Vector2Int(x, y);

        if (cloudSimulationSystem != null)
            return cloudSimulationSystem.GetWindTargetForCell(x, y);

        return new Vector2Int(x, y);
    }

    private float GetDirectionalShiftChance(float stormIntensity01, float rainSupport01)
    {
        float biasDriver = Mathf.Clamp01(stormIntensity01 * 0.75f + rainSupport01 * 0.25f);
        return Mathf.Lerp(baseDirectionalShiftChance, maxDirectionalShiftChance, biasDriver);
    }

    private int DetermineStrikeCountForCandidate(LightningCandidate candidate)
    {
        int minCount = Mathf.Max(1, minStrikesPerBurst);
        int maxCount = Mathf.Max(minCount, maxStrikesPerBurst);

        float chargeT = Mathf.InverseLerp(
            strikeReadyThreshold,
            Mathf.Max(strikeReadyThreshold + 0.001f, maxLightningCharge),
            candidate.charge);

        float score = Mathf.Clamp01(
            candidate.stormIntensity01 * 0.55f +
            chargeT * 0.30f +
            candidate.rainSupport01 * 0.15f);

        return Mathf.Clamp(
            Mathf.RoundToInt(Mathf.Lerp(minCount, maxCount, score)),
            minCount,
            maxCount);
    }

    private float DetermineStrikeIntervalForCandidate(LightningCandidate candidate)
    {
        float minInterval = Mathf.Min(minStrikeIntervalSeconds, maxStrikeIntervalSeconds);
        float maxInterval = Mathf.Max(minStrikeIntervalSeconds, maxStrikeIntervalSeconds);

        float score = Mathf.Clamp01(
            candidate.stormIntensity01 * 0.6f +
            candidate.charge / Mathf.Max(0.001f, maxLightningCharge) * 0.4f);

        return Mathf.Lerp(maxInterval, minInterval, score);
    }

    private void ConsumeChargeForBurst(int x, int y, int strikeCount)
    {
        if (!_isInitialized || !IsInBounds(x, y))
            return;

        float consumeAmount =
            chargeConsumedPerBurst +
            Mathf.Max(0, strikeCount - 1) * extraChargeConsumedPerAdditionalStrike;

        float newCharge = Mathf.Max(minChargeAfterBurst, _chargeGrid[x, y] - consumeAmount);
        _chargeGrid[x, y] = newCharge;
        _chargeReadyGrid[x, y] = newCharge >= strikeReadyThreshold;

        if (newCharge > 0.001f)
            AddNextChargedCell(x, y);
        else
            RemoveNextChargedCell(x, y);
    }

    private void TryTrackCandidate(LightningCandidate candidate)
    {
        int cap = Mathf.Max(1, maxReadyCandidatesTrackedPerStep);

        if (_readyCandidates.Count < cap)
        {
            _readyCandidates.Add(candidate);
            return;
        }

        int weakestIndex = 0;
        float weakestScore = _readyCandidates[0].score;

        for (int i = 1; i < _readyCandidates.Count; i++)
        {
            if (_readyCandidates[i].score < weakestScore)
            {
                weakestScore = _readyCandidates[i].score;
                weakestIndex = i;
            }
        }

        if (candidate.score > weakestScore)
            _readyCandidates[weakestIndex] = candidate;
    }

    private int GetQueuedStrikeCount()
    {
        int total = 0;

        for (int i = 0; i < _queuedBursts.Count; i++)
        {
            QueuedLightningBurst burst = _queuedBursts[i];
            if (burst == null)
                continue;

            total += Mathf.Max(0, burst.TotalStrikes - burst.NextStrikeIndex);
        }

        return total;
    }

    private void HandleStartOfTurn()
    {
        if (!advanceWithTurnSystem)
            return;

        RequestAdvanceLightning();
    }

    private void HandleWeatherGridInitialized()
    {
        TryInitializeGrid();
    }

    private void HandleCloudGridInitialized()
    {
        TryInitializeGrid();
    }

    private void HandleStormGridInitialized()
    {
        TryInitializeGrid();
    }

    private void HandleWeatherStateRefreshed()
    {
        if (!advanceOnWeatherStateRefreshed)
            return;

        RequestAdvanceLightning();
    }

    private void HandleStormStateChanged()
    {
        if (!advanceOnStormStateChanged)
            return;

        RequestAdvanceLightning();
    }

    private void EnsureLinks()
    {
        if (gridManager == null)
            gridManager = GridManager.Instance;

        if (weatherGridManager == null)
            weatherGridManager = WeatherGridManager.Instance;

        if (cloudSimulationSystem == null)
            cloudSimulationSystem = CloudSimulationSystem.Instance;

        if (rainSimulationSystem == null)
            rainSimulationSystem = RainSimulationSystem.Instance;

        if (stormSimulationSystem == null)
            stormSimulationSystem = StormSimulationSystem.Instance;
    }

    private void RebindSourceEvents()
    {
        RebindWeatherGridEvents();
        RebindCloudSimulationEvents();
        RebindStormSimulationEvents();
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
        }

        _subscribedCloudSimulationSystem = cloudSimulationSystem;

        if (_subscribedCloudSimulationSystem != null)
        {
            _subscribedCloudSimulationSystem.OnCloudGridInitialized += HandleCloudGridInitialized;
        }
    }

    private void RebindStormSimulationEvents()
    {
        if (_subscribedStormSimulationSystem == stormSimulationSystem)
            return;

        if (_subscribedStormSimulationSystem != null)
        {
            _subscribedStormSimulationSystem.OnStormGridInitialized -= HandleStormGridInitialized;
            _subscribedStormSimulationSystem.OnStormStateChanged -= HandleStormStateChanged;
        }

        _subscribedStormSimulationSystem = stormSimulationSystem;

        if (_subscribedStormSimulationSystem != null)
        {
            _subscribedStormSimulationSystem.OnStormGridInitialized += HandleStormGridInitialized;
            _subscribedStormSimulationSystem.OnStormStateChanged += HandleStormStateChanged;
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
            _subscribedCloudSimulationSystem = null;
        }

        if (_subscribedStormSimulationSystem != null)
        {
            _subscribedStormSimulationSystem.OnStormGridInitialized -= HandleStormGridInitialized;
            _subscribedStormSimulationSystem.OnStormStateChanged -= HandleStormStateChanged;
            _subscribedStormSimulationSystem = null;
        }
    }

    private bool IsInBounds(int x, int y)
    {
        return x >= 0 && x < _cols && y >= 0 && y < _rows;
    }

    public LightningSimulationSaveData SaveState()
    {
        LightningSimulationSaveData data = new LightningSimulationSaveData
        {
            nextBurstId = Mathf.Max(1, _nextBurstId)
        };

        if (!_isInitialized || _chargeGrid == null || _chargeReadyGrid == null)
            return data;

        for (int i = 0; i < _chargedCells.Count; i++)
        {
            Vector2Int cell = _chargedCells[i];

            int x = cell.x;
            int y = cell.y;

            if (!IsInBounds(x, y))
                continue;

            float charge = _chargeGrid[x, y];
            if (charge <= 0.001f)
                continue;

            data.chargeCells.Add(new LightningChargeCellSaveData
            {
                x = x,
                y = y,
                charge = charge,
                ready = _chargeReadyGrid[x, y]
            });
        }

        float now = Time.time;

        for (int i = 0; i < _queuedBursts.Count; i++)
        {
            QueuedLightningBurst burst = _queuedBursts[i];
            if (burst == null)
                continue;

            int remainingStrikes = Mathf.Max(0, burst.TotalStrikes - burst.NextStrikeIndex);
            if (remainingStrikes <= 0)
                continue;

            data.queuedBursts.Add(new QueuedLightningBurstSaveData
            {
                burstId = burst.BurstId,

                originCellX = burst.OriginCellX,
                originCellY = burst.OriginCellY,

                totalStrikes = burst.TotalStrikes,
                nextStrikeIndex = burst.NextStrikeIndex,

                strikeIntervalSeconds = burst.StrikeIntervalSeconds,
                remainingDelaySeconds = Mathf.Max(0f, burst.NextStrikeTime - now),

                sourceStormIntensity01 = burst.SourceStormIntensity01,
                sourceCloudSupport01 = burst.SourceCloudSupport01,
                sourceRainSupport01 = burst.SourceRainSupport01,
                sourceCharge = burst.SourceCharge,

                preferredDirectionX = burst.PreferredDirection.x,
                preferredDirectionY = burst.PreferredDirection.y,

                directionalShiftChance = burst.DirectionalShiftChance
            });
        }

        return data;
    }

    public void LoadState(LightningSimulationSaveData data)
    {
        if (data == null)
            return;

        if (!TryInitializeGrid())
        {
            if (debugLogging)
                //Debug.LogWarning("[LightningSimulationSystem] Could not load lightning state because grid is not initialized yet.");

            return;
        }

        if (_lightningStepCoroutine != null)
        {
            StopCoroutine(_lightningStepCoroutine);
            _lightningStepCoroutine = null;
        }

        if (_burstQueueCoroutine != null)
        {
            StopCoroutine(_burstQueueCoroutine);
            _burstQueueCoroutine = null;
        }

        _isAdvancingLightning = false;
        _lightningAdvanceQueued = false;

        Array.Clear(_chargeGrid, 0, _chargeGrid.Length);
        Array.Clear(_nextChargeGrid, 0, _nextChargeGrid.Length);
        Array.Clear(_chargeReadyGrid, 0, _chargeReadyGrid.Length);

        _readyCandidates.Clear();
        _queuedBursts.Clear();

        _chargedCells.Clear();
        _nextChargedCells.Clear();

        _cellsToProcess.Clear();
        _cellsToProcessKeys.Clear();
        _nextChargedCellKeys.Clear();

        int highestLoadedBurstId = 0;

        if (data.chargeCells != null)
        {
            for (int i = 0; i < data.chargeCells.Count; i++)
            {
                LightningChargeCellSaveData saved = data.chargeCells[i];

                if (!IsInBounds(saved.x, saved.y))
                    continue;

                float charge = Mathf.Clamp(saved.charge, 0f, Mathf.Max(strikeReadyThreshold, maxLightningCharge));

                if (charge <= 0.001f)
                    continue;

                _chargeGrid[saved.x, saved.y] = charge;
                _chargeReadyGrid[saved.x, saved.y] = saved.ready && charge >= strikeReadyThreshold;

                _chargedCells.Add(new Vector2Int(saved.x, saved.y));
            }
        }

        if (data.queuedBursts != null)
        {
            float now = Time.time;

            for (int i = 0; i < data.queuedBursts.Count; i++)
            {
                QueuedLightningBurstSaveData saved = data.queuedBursts[i];

                if (!IsInBounds(saved.originCellX, saved.originCellY))
                    continue;

                int totalStrikes = Mathf.Max(1, saved.totalStrikes);
                int nextStrikeIndex = Mathf.Clamp(saved.nextStrikeIndex, 0, totalStrikes);

                if (nextStrikeIndex >= totalStrikes)
                    continue;

                int burstId = saved.burstId > 0 ? saved.burstId : highestLoadedBurstId + 1;

                QueuedLightningBurst burst = new QueuedLightningBurst
                {
                    BurstId = burstId,

                    OriginCellX = saved.originCellX,
                    OriginCellY = saved.originCellY,

                    TotalStrikes = totalStrikes,
                    NextStrikeIndex = nextStrikeIndex,

                    StrikeIntervalSeconds = Mathf.Max(0f, saved.strikeIntervalSeconds),
                    NextStrikeTime = now + Mathf.Max(0f, saved.remainingDelaySeconds),

                    SourceStormIntensity01 = Mathf.Clamp01(saved.sourceStormIntensity01),
                    SourceCloudSupport01 = Mathf.Clamp01(saved.sourceCloudSupport01),
                    SourceRainSupport01 = Mathf.Clamp01(saved.sourceRainSupport01),
                    SourceCharge = Mathf.Max(0f, saved.sourceCharge),

                    PreferredDirection = new Vector2Int(
                        Mathf.Clamp(saved.preferredDirectionX, -1, 1),
                        Mathf.Clamp(saved.preferredDirectionY, -1, 1)),

                    DirectionalShiftChance = Mathf.Clamp01(saved.directionalShiftChance)
                };

                _queuedBursts.Add(burst);

                if (burstId > highestLoadedBurstId)
                    highestLoadedBurstId = burstId;
            }
        }

        _nextBurstId = Mathf.Max(1, data.nextBurstId, highestLoadedBurstId + 1);

        if (_queuedBursts.Count > 0)
            EnsureBurstQueueRoutine();

        OnLightningStateChanged?.Invoke();

        if (debugLogging)
        {
            //Debug.Log(
                //$"[LightningSimulationSystem] Loaded lightning state. " +
                //$"ChargedCells={_chargedCells.Count}, QueuedBursts={_queuedBursts.Count}");
        }
    }
}
