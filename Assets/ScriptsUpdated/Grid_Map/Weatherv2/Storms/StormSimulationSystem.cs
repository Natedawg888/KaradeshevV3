using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CloudDensity = CloudSimulationSystem.CloudDensity;

/// <summary>
/// Storm-only simulation layered on top of WeatherGridManager + CloudSimulationSystem + RainSimulationSystem.
/// Owns storm intensity / active state and feeds influence back into clouds and rain.
/// No lightning visuals or gameplay damage here yet.
/// </summary>
public class StormSimulationSystem : MonoBehaviour
{
    public static StormSimulationSystem Instance { get; private set; }

    [Header("References")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private WeatherGridManager weatherGridManager;
    [SerializeField] private CloudSimulationSystem cloudSimulationSystem;
    [SerializeField] private RainSimulationSystem rainSimulationSystem;

    [Header("Lifecycle")]
    [SerializeField] private bool initializeOnStart = true;
    [SerializeField] private bool advanceOnCloudStateChanged = true;
    [SerializeField] private bool advanceOnWeatherStateRefreshed = true;

    [Header("Storm Formation")]
    [Range(0f, 1f)][SerializeField] private float stormHumidityThreshold = 0.70f;
    [SerializeField] private float stormTemperatureDifferenceThreshold = 6f;
    [Range(0f, 1f)][SerializeField] private float initialStormIntensityFromWeather = 0.35f;
    [Range(0f, 1f)][SerializeField] private float stormStartIntensityThreshold = 0.65f;
    [Range(0f, 1f)][SerializeField] private float stormStopIntensityThreshold = 0.35f;
    [Range(0f, 1f)][SerializeField] private float stormIntensityGainPerStep = 0.18f;
    [Range(0f, 1f)][SerializeField] private float stormIntensityLossPerStep = 0.10f;

    [Header("Storm Influence On Clouds")]
    [Range(0f, 1f)][SerializeField] private float stormCloudDarknessStrength = 0.35f;

    [Header("Storm Band Influence")]
    [Min(1)][SerializeField] private int maxStormBandCentersPerStep = 24;
    [Range(0f, 1f)][SerializeField] private float minStormIntensityForBands = 0.55f;

    [SerializeField] private int centerBandMinDistance = 0;
    [SerializeField] private int centerBandMaxDistance = 0;
    [SerializeField] private CloudDensity centerBandDensity = CloudDensity.High;
    [Range(0f, 1f)][SerializeField] private float centerBandMinRainCharge = 0.75f;
    [Range(0f, 1f)][SerializeField] private float centerBandMaxRainCharge = 0.95f;

    [SerializeField] private int middleBandMinDistance = 1;
    [SerializeField] private int middleBandMaxDistance = 1;
    [SerializeField] private CloudDensity middleBandDensity = CloudDensity.Mid;
    [Range(0f, 1f)][SerializeField] private float middleBandMinRainCharge = 0.45f;
    [Range(0f, 1f)][SerializeField] private float middleBandMaxRainCharge = 0.70f;

    [SerializeField] private int outerBandMinDistance = 2;
    [SerializeField] private int outerBandMaxDistance = 2;
    [SerializeField] private CloudDensity outerBandDensity = CloudDensity.Low;
    [Range(0f, 1f)][SerializeField] private float outerBandMinRainCharge = 0.20f;
    [Range(0f, 1f)][SerializeField] private float outerBandMaxRainCharge = 0.45f;

    [Header("Storm Row Batching")]
    [SerializeField] private bool batchStormStateOverFrames = true;
    [Min(1)][SerializeField] private int stormRowsPerFrame = 8;

    private Coroutine _stormStepCoroutine;
    private bool _stormAdvanceQueued;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = false;

    public event Action OnStormGridInitialized;
    public event Action OnStormStateChanged;

    public int Columns => _cols;
    public int Rows => _rows;
    public bool IsInitialized => _isInitialized;

    public int ActiveStormCellCount => _activeStormCellCount;
    public bool HasAnyActiveStorms => _activeStormCellCount > 0;
    public IReadOnlyList<Vector2Int> ActiveStormCells => _activeStormCells;

    private int _cols;
    private int _rows;
    private bool _isInitialized;
    private bool _isAdvancingStorms;

    private float[,] _stormIntensityGrid;
    private float[,] _nextStormIntensityGrid;
    private bool[,] _stormActiveGrid;
    private bool[,] _nextStormActiveGrid;
    private bool[,] _stormCarriedState;

    private int _activeStormCellCount;
    private int _nextActiveStormCellCount;

    private List<Vector2Int> _activeStormCells = new List<Vector2Int>(128);
    private List<Vector2Int> _nextActiveStormCells = new List<Vector2Int>(128);

    private Coroutine _waitForSourcesReadyCoroutine;

    private WeatherGridManager _subscribedWeatherGridManager;
    private CloudSimulationSystem _subscribedCloudSimulationSystem;

    private readonly List<Vector2Int> _stormBandCenters = new List<Vector2Int>(24);
    private readonly List<float> _stormBandCenterIntensities = new List<float>(24);

    private int _lastStormAdvanceFrame = -1;

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

        if (_waitForSourcesReadyCoroutine != null)
        {
            StopCoroutine(_waitForSourcesReadyCoroutine);
            _waitForSourcesReadyCoroutine = null;
        }

        if (_stormStepCoroutine != null)
        {
            StopCoroutine(_stormStepCoroutine);
            _stormStepCoroutine = null;
        }

        _stormAdvanceQueued = false;
        _isAdvancingStorms = false;
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
            RebindSourceEvents();

            if (TryInitializeGrid())
            {
                RequestAdvanceStorm();

                if (debugLogging) {}
                    //Debug.Log("[StormSimulationSystem] Sources ready. Storm system initialized.");

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
            _stormIntensityGrid = new float[_cols, _rows];
            _nextStormIntensityGrid = new float[_cols, _rows];
            _stormActiveGrid = new bool[_cols, _rows];
            _nextStormActiveGrid = new bool[_cols, _rows];
            _stormCarriedState = new bool[_cols, _rows];

            _activeStormCellCount = 0;
            _nextActiveStormCellCount = 0;
            _activeStormCells.Clear();
            _nextActiveStormCells.Clear();
        }

        _isInitialized = true;

        if (sizeChanged)
            OnStormGridInitialized?.Invoke();

        if (debugLogging && sizeChanged) {}
            //Debug.Log($"[StormSimulationSystem] Initialized {_cols}x{_rows}");

        return true;
    }

    public void AdvanceStormsOneStep()
    {
        if (!batchStormStateOverFrames)
        {
            AdvanceStormsOneStepImmediate();
            return;
        }

        if (_stormStepCoroutine != null)
        {
            _stormAdvanceQueued = true;
            return;
        }

        _stormStepCoroutine = StartCoroutine(AdvanceStormsOneStepBatchedRoutine());
    }

    private void BeginStormStepBuffers()
    {
        Array.Clear(_nextStormIntensityGrid, 0, _nextStormIntensityGrid.Length);
        Array.Clear(_nextStormActiveGrid, 0, _nextStormActiveGrid.Length);
        Array.Clear(_stormCarriedState, 0, _stormCarriedState.Length);

        _nextActiveStormCellCount = 0;
        _nextActiveStormCells.Clear();
    }

    private void ProcessStormCarryRows(int startY, int endY)
    {
        for (int y = startY; y < endY; y++)
        {
            for (int x = 0; x < _cols; x++)
            {
                float currentIntensity = _stormIntensityGrid[x, y];
                bool wasActive = _stormActiveGrid[x, y];

                if (currentIntensity <= 0.001f && !wasActive)
                    continue;

                Vector2Int target = GetStormWindTarget(x, y);
                if (!IsInBounds(target.x, target.y))
                    continue;

                _nextStormIntensityGrid[target.x, target.y] =
                    Mathf.Max(_nextStormIntensityGrid[target.x, target.y], currentIntensity);

                if (wasActive)
                    _stormCarriedState[target.x, target.y] = true;
            }
        }
    }

    private bool ProcessStormRebuildRows(int startY, int endY)
    {
        bool anyChanged = false;

        for (int y = startY; y < endY; y++)
        {
            for (int x = 0; x < _cols; x++)
            {
                CloudDensity density = cloudSimulationSystem.GetCloudDensityAtCell(x, y);
                float densityFactor = GetStormDensityFactor(density);

                float carriedIntensity = _nextStormIntensityGrid[x, y];
                bool carriedActive = _stormCarriedState[x, y];

                float humidity = GetHumidity01(x, y);
                float rainCharge = rainSimulationSystem.GetRainCharge01AtCell(x, y);
                bool raining = rainSimulationSystem.IsRainingAtCell(x, y);

                if (densityFactor <= 0f && carriedIntensity <= 0.001f && !carriedActive && rainCharge <= 0.01f && !raining)
                {
                    _nextStormIntensityGrid[x, y] = 0f;
                    _nextStormActiveGrid[x, y] = false;
                    continue;
                }

                float intensity = carriedIntensity;

                bool favorableWeather = false;
                float weatherStrength = 0f;

                if (densityFactor > 0f && humidity >= stormHumidityThreshold)
                {
                    float localTempDifference = GetLocalTemperatureDifference(x, y);

                    if (localTempDifference >= stormTemperatureDifferenceThreshold)
                    {
                        float humidityT = Mathf.InverseLerp(stormHumidityThreshold, 1f, humidity);
                        float tempT = Mathf.Clamp01(
                            localTempDifference / Mathf.Max(0.01f, stormTemperatureDifferenceThreshold * 2f));

                        float rainSupport = raining ? 1f : rainCharge;
                        float moistureSupport = Mathf.Clamp01(densityFactor + rainSupport * 0.35f);

                        weatherStrength = Mathf.Clamp01(
                            (humidityT * 0.55f + tempT * 0.30f + rainSupport * 0.15f) * moistureSupport);

                        favorableWeather = weatherStrength > 0f;
                    }
                }

                if (favorableWeather)
                {
                    float seededIntensity = initialStormIntensityFromWeather * weatherStrength;
                    intensity = Mathf.Max(intensity, seededIntensity);
                    intensity = Mathf.Clamp01(intensity + stormIntensityGainPerStep * weatherStrength);
                }
                else
                {
                    intensity = Mathf.Max(0f, intensity - stormIntensityLossPerStep);
                }

                bool shouldRemainActive = carriedActive
                    ? intensity > stormStopIntensityThreshold
                    : intensity >= stormStartIntensityThreshold;

                bool active = shouldRemainActive && intensity > 0.01f;

                if (active)
                {
                    _nextActiveStormCellCount++;
                    _nextActiveStormCells.Add(new Vector2Int(x, y));
                }

                float oldIntensity = _stormIntensityGrid[x, y];
                bool oldActive = _stormActiveGrid[x, y];

                _stormIntensityGrid[x, y] = intensity;
                _stormActiveGrid[x, y] = active;

                cloudSimulationSystem.SetStormCloudDarkness01AtCell(
                    x,
                    y,
                    active ? intensity * stormCloudDarknessStrength : 0f);

                bool changed =
                    Mathf.Abs(oldIntensity - intensity) > 0.01f ||
                    oldActive != active;

                if (changed)
                    anyChanged = true;
            }
        }

        return anyChanged;
    }

    private void CommitActiveStormSnapshot()
    {
        _activeStormCellCount = _nextActiveStormCellCount;

        List<Vector2Int> temp = _activeStormCells;
        _activeStormCells = _nextActiveStormCells;
        _nextActiveStormCells = temp;
        _nextActiveStormCells.Clear();
    }

    private void AdvanceStormsOneStepImmediate()
    {
        if (_isAdvancingStorms)
            return;

        if (!TryInitializeGrid())
            return;

        _isAdvancingStorms = true;
        try
        {
            BeginStormStepBuffers();
            ProcessStormCarryRows(0, _rows);

            bool anyChanged = ProcessStormRebuildRows(0, _rows);

            if (ApplyStormInfluenceBands())
                anyChanged = true;

            CommitActiveStormSnapshot();

            if (anyChanged)
                OnStormStateChanged?.Invoke();

            if (debugLogging) {}
                //Debug.Log("[StormSimulationSystem] Advanced storms one step.");
        }
        finally
        {
            _isAdvancingStorms = false;
        }
    }

    private IEnumerator AdvanceStormsOneStepBatchedRoutine()
    {
        if (_isAdvancingStorms || !TryInitializeGrid())
        {
            _stormStepCoroutine = null;
            yield break;
        }

        _isAdvancingStorms = true;

        bool anyChanged = false;
        int rowsPerFrame = Mathf.Max(1, stormRowsPerFrame);

        try
        {
            BeginStormStepBuffers();

            for (int startY = 0; startY < _rows; startY += rowsPerFrame)
            {
                ProcessStormCarryRows(startY, Mathf.Min(startY + rowsPerFrame, _rows));

                if (startY + rowsPerFrame < _rows)
                    yield return null;
            }

            for (int startY = 0; startY < _rows; startY += rowsPerFrame)
            {
                if (ProcessStormRebuildRows(startY, Mathf.Min(startY + rowsPerFrame, _rows)))
                    anyChanged = true;

                if (startY + rowsPerFrame < _rows)
                    yield return null;
            }

            if (ApplyStormInfluenceBands())
                anyChanged = true;

            CommitActiveStormSnapshot();

            if (anyChanged)
                OnStormStateChanged?.Invoke();

            if (debugLogging) {}
                //Debug.Log("[StormSimulationSystem] Advanced storms one batched step.");
        }
        finally
        {
            _isAdvancingStorms = false;
            _stormStepCoroutine = null;

            if (_stormAdvanceQueued && isActiveAndEnabled)
            {
                _stormAdvanceQueued = false;
                _stormStepCoroutine = StartCoroutine(AdvanceStormsOneStepBatchedRoutine());
            }
            else
            {
                _stormAdvanceQueued = false;
            }
        }
    }

    public bool IsStormActiveAtCell(int x, int y)
    {
        if (!_isInitialized || !IsInBounds(x, y) || _stormActiveGrid == null)
            return false;

        return _stormActiveGrid[x, y];
    }

    public float GetStormIntensity01AtCell(int x, int y)
    {
        if (!_isInitialized || !IsInBounds(x, y) || _stormIntensityGrid == null)
            return 0f;

        return Mathf.Clamp01(_stormIntensityGrid[x, y]);
    }

    public void ClearAllStorms()
    {
        if (!TryInitializeGrid())
            return;

        for (int x = 0; x < _cols; x++)
        {
            for (int y = 0; y < _rows; y++)
            {
                _stormIntensityGrid[x, y] = 0f;
                _stormActiveGrid[x, y] = false;
                cloudSimulationSystem.SetStormCloudDarkness01AtCell(x, y, 0f);
            }
        }

        _activeStormCellCount = 0;
        _nextActiveStormCellCount = 0;
        _activeStormCells.Clear();
        _nextActiveStormCells.Clear();

        OnStormStateChanged?.Invoke();
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

        RequestAdvanceStorm();
    }

    private void HandleCloudStateChanged()
    {
        if (!advanceOnCloudStateChanged)
            return;

        RequestAdvanceStorm();
    }

    private bool ApplyStormInfluenceBands()
    {
        _stormBandCenters.Clear();
        _stormBandCenterIntensities.Clear();

        for (int x = 0; x < _cols; x++)
        {
            for (int y = 0; y < _rows; y++)
            {
                if (!_stormActiveGrid[x, y])
                    continue;

                float intensity = _stormIntensityGrid[x, y];
                if (intensity < minStormIntensityForBands)
                    continue;

                if (_stormBandCenters.Count < maxStormBandCentersPerStep)
                {
                    _stormBandCenters.Add(new Vector2Int(x, y));
                    _stormBandCenterIntensities.Add(intensity);
                    continue;
                }

                int weakestIndex = 0;
                float weakestIntensity = _stormBandCenterIntensities[0];

                for (int i = 1; i < _stormBandCenterIntensities.Count; i++)
                {
                    if (_stormBandCenterIntensities[i] < weakestIntensity)
                    {
                        weakestIntensity = _stormBandCenterIntensities[i];
                        weakestIndex = i;
                    }
                }

                if (intensity > weakestIntensity)
                {
                    _stormBandCenters[weakestIndex] = new Vector2Int(x, y);
                    _stormBandCenterIntensities[weakestIndex] = intensity;
                }
            }
        }

        bool anyChanged = false;

        for (int i = 0; i < _stormBandCenters.Count; i++)
        {
            Vector2Int center = _stormBandCenters[i];
            float intensity = _stormBandCenterIntensities[i];

            anyChanged |= StampStormInfluenceBand(
                center.x,
                center.y,
                centerBandMinDistance,
                centerBandMaxDistance,
                centerBandDensity,
                Mathf.Lerp(centerBandMinRainCharge, centerBandMaxRainCharge, intensity));

            anyChanged |= StampStormInfluenceBand(
                center.x,
                center.y,
                middleBandMinDistance,
                middleBandMaxDistance,
                middleBandDensity,
                Mathf.Lerp(middleBandMinRainCharge, middleBandMaxRainCharge, intensity));

            anyChanged |= StampStormInfluenceBand(
                center.x,
                center.y,
                outerBandMinDistance,
                outerBandMaxDistance,
                outerBandDensity,
                Mathf.Lerp(outerBandMinRainCharge, outerBandMaxRainCharge, intensity));
        }

        return anyChanged;
    }

    private bool StampStormInfluenceBand(int centerX, int centerY, int minDistance, int maxDistance, CloudDensity density, float minRainCharge)
    {
        bool anyChanged = false;

        for (int x = centerX - maxDistance; x <= centerX + maxDistance; x++)
        {
            for (int y = centerY - maxDistance; y <= centerY + maxDistance; y++)
            {
                if (!IsInBounds(x, y))
                    continue;

                int dx = Mathf.Abs(x - centerX);
                int dy = Mathf.Abs(y - centerY);
                int ringDistance = Mathf.Max(dx, dy);

                if (ringDistance < minDistance || ringDistance > maxDistance)
                    continue;

                if (cloudSimulationSystem.TrySetCloudDensityAtCell(x, y, density))
                    anyChanged = true;

                if (rainSimulationSystem.TryRaiseRainChargeFloorAtCell(x, y, minRainCharge, activateIfAboveStart: true))
                    anyChanged = true;
            }
        }

        return anyChanged;
    }

    private float GetStormDensityFactor(CloudDensity density)
    {
        switch (density)
        {
            case CloudDensity.Low: return 0.15f;
            case CloudDensity.Mid: return 0.50f;
            case CloudDensity.High: return 1f;
            default: return 0f;
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

    private float GetLocalTemperatureDifference(int x, int y)
    {
        if (weatherGridManager == null)
            return 0f;

        if (!weatherGridManager.TryGetCellState(x, y, out WeatherCellState center) || !center.isValid)
            return 0f;

        float maxDifference = 0f;

        TryAccumulateTemperatureDifference(x - 1, y, center.temperatureC, ref maxDifference);
        TryAccumulateTemperatureDifference(x + 1, y, center.temperatureC, ref maxDifference);
        TryAccumulateTemperatureDifference(x, y - 1, center.temperatureC, ref maxDifference);
        TryAccumulateTemperatureDifference(x, y + 1, center.temperatureC, ref maxDifference);

        return maxDifference;
    }

    private void TryAccumulateTemperatureDifference(int x, int y, float centerTemp, ref float maxDifference)
    {
        if (!IsInBounds(x, y))
            return;

        if (!weatherGridManager.TryGetCellState(x, y, out WeatherCellState neighbour) || !neighbour.isValid)
            return;

        float difference = Mathf.Abs(neighbour.temperatureC - centerTemp);
        if (difference > maxDifference)
            maxDifference = difference;
    }

    private Vector2Int GetStormWindTarget(int x, int y)
    {
        if (cloudSimulationSystem != null)
            return cloudSimulationSystem.GetWindTargetForCell(x, y);

        return new Vector2Int(x, y);
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

    private void RequestAdvanceStorm()
    {
        if (!TryInitializeGrid())
            return;

        if (_lastStormAdvanceFrame == Time.frameCount)
            return;

        _lastStormAdvanceFrame = Time.frameCount;
        AdvanceStormsOneStep();
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

    public StormSimulationSaveData SaveState()
    {
        StormSimulationSaveData data = new StormSimulationSaveData
        {
            activeStormCellCount = _activeStormCellCount
        };

        if (!_isInitialized || _stormIntensityGrid == null || _stormActiveGrid == null)
            return data;

        for (int x = 0; x < _cols; x++)
        {
            for (int y = 0; y < _rows; y++)
            {
                float intensity = Mathf.Clamp01(_stormIntensityGrid[x, y]);
                bool active = _stormActiveGrid[x, y];

                // Save active storms and also non-active cells that still have fading intensity.
                if (!active && intensity <= 0.001f)
                    continue;

                data.stormCells.Add(new StormCellSaveData
                {
                    x = x,
                    y = y,
                    intensity01 = intensity,
                    isActive = active,
                    remainingTurns = 0
                });
            }
        }

        return data;
    }

    public void LoadState(StormSimulationSaveData data)
    {
        if (data == null)
            return;

        if (!TryInitializeGrid())
        {
            if (debugLogging) {}
                //Debug.LogWarning("[StormSimulationSystem] Could not load storm state because grid is not initialized yet.");

            return;
        }

        if (_waitForSourcesReadyCoroutine != null)
        {
            StopCoroutine(_waitForSourcesReadyCoroutine);
            _waitForSourcesReadyCoroutine = null;
        }

        if (_stormStepCoroutine != null)
        {
            StopCoroutine(_stormStepCoroutine);
            _stormStepCoroutine = null;
        }

        _stormAdvanceQueued = false;
        _isAdvancingStorms = false;
        _lastStormAdvanceFrame = -1;

        Array.Clear(_stormIntensityGrid, 0, _stormIntensityGrid.Length);
        Array.Clear(_nextStormIntensityGrid, 0, _nextStormIntensityGrid.Length);
        Array.Clear(_stormActiveGrid, 0, _stormActiveGrid.Length);
        Array.Clear(_nextStormActiveGrid, 0, _nextStormActiveGrid.Length);
        Array.Clear(_stormCarriedState, 0, _stormCarriedState.Length);

        _activeStormCellCount = 0;
        _nextActiveStormCellCount = 0;

        _activeStormCells.Clear();
        _nextActiveStormCells.Clear();

        _stormBandCenters.Clear();
        _stormBandCenterIntensities.Clear();

        ClearStormCloudDarknessForLoad();

        int restoredCells = 0;
        int restoredActiveCells = 0;

        if (data.stormCells != null)
        {
            for (int i = 0; i < data.stormCells.Count; i++)
            {
                StormCellSaveData saved = data.stormCells[i];

                if (saved == null)
                    continue;

                if (!IsInBounds(saved.x, saved.y))
                    continue;

                float intensity = Mathf.Clamp01(saved.intensity01);

                if (intensity <= 0.001f && !saved.isActive)
                    continue;

                bool active = RestoreStormActiveFromSave(data, saved, intensity);

                _stormIntensityGrid[saved.x, saved.y] = intensity;
                _stormActiveGrid[saved.x, saved.y] = active;

                if (active)
                {
                    _activeStormCells.Add(new Vector2Int(saved.x, saved.y));
                    restoredActiveCells++;

                    if (cloudSimulationSystem != null)
                    {
                        cloudSimulationSystem.SetStormCloudDarkness01AtCell(
                            saved.x,
                            saved.y,
                            intensity * stormCloudDarknessStrength);
                    }
                }

                restoredCells++;
            }
        }

        _activeStormCellCount = restoredActiveCells;

        OnStormStateChanged?.Invoke();

        if (debugLogging)
        {
            //Debug.Log(
                //$"[StormSimulationSystem] Loaded storm state. " +
                //$"Cells={restoredCells}, Active={_activeStormCellCount}");
        }
    }

    private bool RestoreStormActiveFromSave(
        StormSimulationSaveData data,
        StormCellSaveData saved,
        float intensity)
    {
        if (saved == null)
            return false;

        // Version 2 saves active state directly.
        if (data != null && data.version >= 2)
            return saved.isActive && intensity > 0.001f;

        // Old fallback if loading a version 1 save.
        return intensity >= stormStartIntensityThreshold;
    }

    private void ClearStormCloudDarknessForLoad()
    {
        if (cloudSimulationSystem == null || !_isInitialized)
            return;

        for (int x = 0; x < _cols; x++)
        {
            for (int y = 0; y < _rows; y++)
                cloudSimulationSystem.SetStormCloudDarkness01AtCell(x, y, 0f);
        }
    }

    public void ApplyPresetSettings(StormPresetSettings settings)
    {
        if (settings == null || !settings.overrideStorms)
            return;

        stormHumidityThreshold = settings.stormHumidityThreshold;
        stormTemperatureDifferenceThreshold = settings.stormTemperatureDifferenceThreshold;
        initialStormIntensityFromWeather = settings.initialStormIntensityFromWeather;
        stormStartIntensityThreshold = settings.stormStartIntensityThreshold;
        stormStopIntensityThreshold = settings.stormStopIntensityThreshold;
        stormIntensityGainPerStep = settings.stormIntensityGainPerStep;
        stormIntensityLossPerStep = settings.stormIntensityLossPerStep;

        stormCloudDarknessStrength = settings.stormCloudDarknessStrength;
        maxStormBandCentersPerStep = settings.maxStormBandCentersPerStep;
        minStormIntensityForBands = settings.minStormIntensityForBands;

        if (debugLogging) {}
            //Debug.Log("[StormSimulationSystem] Applied storm preset settings.");
    }
}
