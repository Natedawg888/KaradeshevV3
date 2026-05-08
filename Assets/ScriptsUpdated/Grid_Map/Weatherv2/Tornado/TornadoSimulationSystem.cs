using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CloudDensity = CloudSimulationSystem.CloudDensity;

[Serializable]
public struct TornadoCellState
{
    public bool isActive;
    public int lifetimeRemaining;
}

[Serializable]
public struct TornadoSpawnEventData
{
    public int tornadoId;
    public Vector2Int cell;
    public int lifetimeRemaining;
    public float stormIntensity01;
    public float humidity01;
    public float localTemperatureDifference;
    public CloudDensity cloudDensity;
}

[Serializable]
public struct TornadoExpireEventData
{
    public int tornadoId;
    public Vector2Int cell;
    public int lastLifetimeRemaining;
}

[Serializable]
public struct TornadoMoveEventData
{
    public int tornadoId;
    public Vector2Int fromCell;
    public Vector2Int toCell;
    public int lifetimeRemaining;
}

/// <summary>
/// Tornado-only simulation layered on top of WeatherGridManager + CloudSimulationSystem + StormSimulationSystem.
/// Owns tornado state only.
/// No tornado visuals or gameplay damage/effects here.
/// </summary>
public class TornadoSimulationSystem : MonoBehaviour
{
    public static TornadoSimulationSystem Instance { get; private set; }

    [Header("References")]
    [SerializeField] private WeatherGridManager weatherGridManager;
    [SerializeField] private CloudSimulationSystem cloudSimulationSystem;
    [SerializeField] private StormSimulationSystem stormSimulationSystem;
    [SerializeField] private MonoEnvironmentDataSource environmentDataSource;

    [Header("Lifecycle")]
    [SerializeField] private bool initializeOnStart = true;
    [SerializeField] private bool advanceOnWeatherStateRefreshed = true;
    [SerializeField] private bool advanceOnCloudStateChanged = true;
    [SerializeField] private bool advanceOnStormStateChanged = true;

    [Header("Spawn Rules")]
    [Range(0f, 1f)][SerializeField] private float tornadoBaseSpawnChancePerCandidateStep = 0.01f;
    [Range(0f, 1f)][SerializeField] private float tornadoStormIntensityThreshold = 0.70f;
    [Range(0f, 1f)][SerializeField] private float tornadoHumidityThreshold = 0.72f;
    [SerializeField] private float tornadoTemperatureDifferenceThreshold = 8f;
    [SerializeField] private CloudDensity minimumCloudDensityToSpawn = CloudDensity.Mid;
    [Range(0f, 1f)][SerializeField] private float highCloudSpawnChanceBonus = 0.05f;

    [Header("Lifetime / Count")]
    [Min(1)][SerializeField] private int tornadoMinLifetimeTurns = 2;
    [Min(1)][SerializeField] private int tornadoMaxLifetimeTurns = 5;
    [Min(1)][SerializeField] private int maxActiveTornadoes = 1;
    [Min(0)][SerializeField] private int minTornadoSpacingCells = 4;
    [Min(1)][SerializeField] private int maxNewTornadoesPerStep = 1;
    [Min(1)][SerializeField] private int maxSpawnCandidatesPerStep = 24;

    [Header("Movement")]
    [SerializeField] private bool keepTornadoInPlaceIfBlocked = true;

    [Header("Row Batching")]
    [SerializeField] private bool batchTornadoStateOverFrames = true;
    [Min(1)][SerializeField] private int tornadoRowsPerFrame = 8;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = false;
    [SerializeField] private bool debugLogBuildingCellActivations = true;

    public event Action OnTornadoGridInitialized;
    public event Action OnTornadoStateChanged;

    // Hook events for future systems.
    public event Action OnTornadoCellsChanged;
    public event Action<TornadoSpawnEventData> OnTornadoSpawned;
    public event Action<TornadoExpireEventData> OnTornadoExpired;
    public event Action<TornadoMoveEventData> OnTornadoMoved;

    public int Columns => _cols;
    public int Rows => _rows;
    public bool IsInitialized => _isInitialized;

    private int _cols;
    private int _rows;
    private bool _isInitialized;
    private bool _isAdvancingTornadoes;

    // Public-facing tornado state: active + lifetime.
    private int[,] _tornadoLifetimeGrid;
    private bool[,] _tornadoActiveGrid;

    // Internal identity grid so moves can be tracked cleanly.
    private int[,] _tornadoIdGrid;

    // Step buffers.
    private int[,] _nextTornadoLifetimeGrid;
    private bool[,] _nextTornadoActiveGrid;
    private int[,] _nextTornadoIdGrid;

    private int _nextActiveCount;
    private int _nextTornadoId = 1;

    private readonly List<Vector2Int> _activeTornadoCells = new List<Vector2Int>(16);
    private readonly List<TornadoSpawnCandidate> _spawnCandidates = new List<TornadoSpawnCandidate>(32);

    private readonly Dictionary<int, Vector2Int> _oldCellById = new Dictionary<int, Vector2Int>(16);
    private readonly Dictionary<int, int> _oldLifetimeById = new Dictionary<int, int>(16);
    private readonly Dictionary<int, Vector2Int> _newCellById = new Dictionary<int, Vector2Int>(16);
    private readonly Dictionary<int, int> _newLifetimeById = new Dictionary<int, int>(16);

    private Coroutine _waitForSourcesReadyCoroutine;
    private Coroutine _tornadoStepCoroutine;
    private bool _tornadoAdvanceQueued;
    private int _lastTornadoAdvanceFrame = -1;

    private WeatherGridManager _subscribedWeatherGridManager;
    private CloudSimulationSystem _subscribedCloudSimulationSystem;
    private StormSimulationSystem _subscribedStormSimulationSystem;

    private struct TornadoSpawnCandidate
    {
        public int x;
        public int y;
        public float score;
        public float stormIntensity01;
        public float humidity01;
        public float localTemperatureDifference;
        public CloudDensity cloudDensity;
    }

    [Header("Environment Blocking")]
    [SerializeField] private bool blockTornadoesByEnvironment = true;

    [Tooltip("If true, cells with no registered environment tile cannot have tornadoes.")]
    [SerializeField] private bool blockCellsWithoutEnvironment = false;

    [SerializeField]
    private EnvironmentTileType[] blockedTornadoTileTypes =
    {
    EnvironmentTileType.Mountain,
    EnvironmentTileType.SaltLake
};

    [SerializeField]
    private EnvironmentType[] blockedTornadoEnvironmentTypes =
    {
    EnvironmentType.Mountain,
    EnvironmentType.SaltLake,
    EnvironmentType.Volcano
};

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

        if (_tornadoStepCoroutine != null)
        {
            StopCoroutine(_tornadoStepCoroutine);
            _tornadoStepCoroutine = null;
        }

        _tornadoAdvanceQueued = false;
        _isAdvancingTornadoes = false;
    }

    private void OnDestroy()
    {
        UnbindSourceEvents();

        if (Instance == this)
            Instance = null;
    }

    public void InstallRuntimeRefs(
        WeatherGridManager newWeatherGridManager,
        CloudSimulationSystem newCloudSimulationSystem,
        StormSimulationSystem newStormSimulationSystem,
        bool initializeNow = true)
    {
        if (newWeatherGridManager != null)
            weatherGridManager = newWeatherGridManager;

        if (newCloudSimulationSystem != null)
            cloudSimulationSystem = newCloudSimulationSystem;

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
                RequestAdvanceTornadoes();

                if (debugLogging)
                    //Debug.Log("[TornadoSimulationSystem] Sources ready. Tornado system initialized.");

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

        if (stormSimulationSystem == null || !stormSimulationSystem.IsInitialized)
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
            _tornadoLifetimeGrid = new int[_cols, _rows];
            _tornadoActiveGrid = new bool[_cols, _rows];
            _tornadoIdGrid = new int[_cols, _rows];

            _nextTornadoLifetimeGrid = new int[_cols, _rows];
            _nextTornadoActiveGrid = new bool[_cols, _rows];
            _nextTornadoIdGrid = new int[_cols, _rows];

            _activeTornadoCells.Clear();
            _spawnCandidates.Clear();
            _oldCellById.Clear();
            _oldLifetimeById.Clear();
            _newCellById.Clear();
            _newLifetimeById.Clear();
            _nextTornadoId = 1;
        }

        _isInitialized = true;

        if (sizeChanged)
            OnTornadoGridInitialized?.Invoke();

        if (debugLogging && sizeChanged)
            //Debug.Log($"[TornadoSimulationSystem] Initialized {_cols}x{_rows}");

        return true;
    }

    public void AdvanceTornadoesOneStep()
    {
        if (!batchTornadoStateOverFrames)
        {
            AdvanceTornadoesOneStepImmediate();
            return;
        }

        if (_tornadoStepCoroutine != null)
        {
            _tornadoAdvanceQueued = true;
            return;
        }

        _tornadoStepCoroutine = StartCoroutine(AdvanceTornadoesOneStepBatchedRoutine());
    }

    private void AdvanceTornadoesOneStepImmediate()
    {
        if (_isAdvancingTornadoes)
            return;

        if (!TryInitializeGrid())
            return;

        _isAdvancingTornadoes = true;
        try
        {
            BeginTornadoStepBuffers();
            ProcessTornadoCarryRows(0, _rows);
            ProcessSpawnCandidateRows(0, _rows);
            ApplySpawnCandidates();

            bool anyChanged = ApplyTornadoStateRows(0, _rows);
            bool anyCellPositionsChanged = ResolveTornadoCellEvents();

            if (anyChanged)
                OnTornadoStateChanged?.Invoke();

            if (anyCellPositionsChanged)
                OnTornadoCellsChanged?.Invoke();

            if (debugLogging)
                //Debug.Log("[TornadoSimulationSystem] Advanced tornadoes one step.");
        }
        finally
        {
            _isAdvancingTornadoes = false;
        }
    }

    private IEnumerator AdvanceTornadoesOneStepBatchedRoutine()
    {
        if (_isAdvancingTornadoes || !TryInitializeGrid())
        {
            _tornadoStepCoroutine = null;
            yield break;
        }

        _isAdvancingTornadoes = true;
        bool anyChanged = false;
        bool anyCellPositionsChanged = false;
        int rowsPerFrame = Mathf.Max(1, tornadoRowsPerFrame);

        try
        {
            BeginTornadoStepBuffers();

            for (int startY = 0; startY < _rows; startY += rowsPerFrame)
            {
                ProcessTornadoCarryRows(startY, Mathf.Min(startY + rowsPerFrame, _rows));

                if (startY + rowsPerFrame < _rows)
                    yield return null;
            }

            for (int startY = 0; startY < _rows; startY += rowsPerFrame)
            {
                ProcessSpawnCandidateRows(startY, Mathf.Min(startY + rowsPerFrame, _rows));

                if (startY + rowsPerFrame < _rows)
                    yield return null;
            }

            ApplySpawnCandidates();

            _activeTornadoCells.Clear();

            for (int startY = 0; startY < _rows; startY += rowsPerFrame)
            {
                if (ApplyTornadoStateRows(startY, Mathf.Min(startY + rowsPerFrame, _rows)))
                    anyChanged = true;

                if (startY + rowsPerFrame < _rows)
                    yield return null;
            }

            anyCellPositionsChanged = ResolveTornadoCellEvents();

            if (anyChanged)
                OnTornadoStateChanged?.Invoke();

            if (anyCellPositionsChanged)
                OnTornadoCellsChanged?.Invoke();

            if (debugLogging)
                //Debug.Log("[TornadoSimulationSystem] Advanced tornadoes one batched step.");
        }
        finally
        {
            _isAdvancingTornadoes = false;
            _tornadoStepCoroutine = null;

            if (_tornadoAdvanceQueued && isActiveAndEnabled)
            {
                _tornadoAdvanceQueued = false;
                _tornadoStepCoroutine = StartCoroutine(AdvanceTornadoesOneStepBatchedRoutine());
            }
            else
            {
                _tornadoAdvanceQueued = false;
            }
        }
    }

    private void BeginTornadoStepBuffers()
    {
        Array.Clear(_nextTornadoLifetimeGrid, 0, _nextTornadoLifetimeGrid.Length);
        Array.Clear(_nextTornadoActiveGrid, 0, _nextTornadoActiveGrid.Length);
        Array.Clear(_nextTornadoIdGrid, 0, _nextTornadoIdGrid.Length);

        _nextActiveCount = 0;
        _spawnCandidates.Clear();

        _activeTornadoCells.Clear();
        _oldCellById.Clear();
        _oldLifetimeById.Clear();
        _newCellById.Clear();
        _newLifetimeById.Clear();
    }

    private void ProcessTornadoCarryRows(int startY, int endY)
    {
        for (int y = startY; y < endY; y++)
        {
            for (int x = 0; x < _cols; x++)
            {
                int tornadoId = _tornadoIdGrid[x, y];
                int currentLifetime = _tornadoLifetimeGrid[x, y];

                if (tornadoId <= 0 || currentLifetime <= 0)
                    continue;

                int remainingLifetime = currentLifetime - 1;
                if (remainingLifetime <= 0)
                    continue;

                Vector2Int windTarget = GetTornadoWindTarget(x, y);
                bool placed = false;

                if (IsInBounds(windTarget.x, windTarget.y))
                    placed = TryPlaceTornadoInNextGrid(tornadoId, windTarget.x, windTarget.y, remainingLifetime);

                if (!placed && keepTornadoInPlaceIfBlocked)
                    placed = TryPlaceTornadoInNextGrid(tornadoId, x, y, remainingLifetime);

                if (!placed && debugLogging)
                {
                    //Debug.Log(
                        //$"[TornadoSimulationSystem] Tornado {tornadoId} could not be placed after carry " +
                        //$"from ({x},{y}).");
                }
            }
        }
    }

    private void ProcessSpawnCandidateRows(int startY, int endY)
    {
        if (_nextActiveCount >= maxActiveTornadoes)
            return;

        for (int y = startY; y < endY; y++)
        {
            for (int x = 0; x < _cols; x++)
            {
                if (_nextActiveCount >= maxActiveTornadoes && _spawnCandidates.Count == 0)
                    return;

                if (_nextTornadoIdGrid[x, y] != 0)
                    continue;

                if (!CanTornadoOccupyEnvironmentCell(x, y))
                    continue;

                if (minTornadoSpacingCells > 0 &&
                    IsTooCloseToOtherTornado(_nextTornadoIdGrid, x, y, minTornadoSpacingCells))
                {
                    continue;
                }

                if (!stormSimulationSystem.IsStormActiveAtCell(x, y))
                    continue;

                float stormIntensity = stormSimulationSystem.GetStormIntensity01AtCell(x, y);
                if (stormIntensity < tornadoStormIntensityThreshold)
                    continue;

                if (!weatherGridManager.TryGetCellState(x, y, out WeatherCellState weatherState) || !weatherState.isValid)
                    continue;

                if (weatherState.humidity01 < tornadoHumidityThreshold)
                    continue;

                float localTemperatureDifference = GetLocalTemperatureDifference(x, y);
                if (localTemperatureDifference < tornadoTemperatureDifferenceThreshold)
                    continue;

                CloudDensity cloudDensity = cloudSimulationSystem.GetCloudDensityAtCell(x, y);
                if ((int)cloudDensity < (int)minimumCloudDensityToSpawn)
                    continue;

                float spawnChance = GetSpawnChance(
                    stormIntensity,
                    weatherState.humidity01,
                    localTemperatureDifference,
                    cloudDensity);

                if (UnityEngine.Random.value > spawnChance)
                    continue;

                float score = GetSpawnScore(
                    stormIntensity,
                    weatherState.humidity01,
                    localTemperatureDifference,
                    cloudDensity);

                AddSpawnCandidate(new TornadoSpawnCandidate
                {
                    x = x,
                    y = y,
                    score = score,
                    stormIntensity01 = stormIntensity,
                    humidity01 = weatherState.humidity01,
                    localTemperatureDifference = localTemperatureDifference,
                    cloudDensity = cloudDensity
                });
            }
        }
    }

    private void ApplySpawnCandidates()
    {
        int remainingCapacity = Mathf.Max(0, maxActiveTornadoes - _nextActiveCount);
        if (remainingCapacity <= 0 || _spawnCandidates.Count == 0)
            return;

        _spawnCandidates.Sort((a, b) => b.score.CompareTo(a.score));

        int allowedNewThisStep = Mathf.Min(remainingCapacity, Mathf.Max(1, maxNewTornadoesPerStep));
        int spawned = 0;

        for (int i = 0; i < _spawnCandidates.Count && spawned < allowedNewThisStep; i++)
        {
            TornadoSpawnCandidate candidate = _spawnCandidates[i];

            if (_nextTornadoIdGrid[candidate.x, candidate.y] != 0)
                continue;

            if (minTornadoSpacingCells > 0 &&
                IsTooCloseToOtherTornado(_nextTornadoIdGrid, candidate.x, candidate.y, minTornadoSpacingCells))
            {
                continue;
            }

            int lifetime = UnityEngine.Random.Range(
                Mathf.Max(1, tornadoMinLifetimeTurns),
                Mathf.Max(tornadoMinLifetimeTurns, tornadoMaxLifetimeTurns) + 1);

            int tornadoId = _nextTornadoId++;
            if (!TryPlaceTornadoInNextGrid(tornadoId, candidate.x, candidate.y, lifetime))
                continue;

            spawned++;

            if (debugLogging)
            {
                //Debug.Log(
                    //$"[TornadoSimulationSystem] Tornado spawned candidate accepted at ({candidate.x},{candidate.y}) " +
                    //$"| Lifetime={lifetime} | Storm={candidate.stormIntensity01:F2} " +
                    //$"| Humidity={candidate.humidity01:F2} " +
                    //$"| TempDiff={candidate.localTemperatureDifference:F2} " +
                    //$"| Cloud={candidate.cloudDensity}");
            }
        }
    }

    private void DebugLogTornadoBuildingCellIfAny(int tornadoId, Vector2Int cell, int lifetimeRemaining, string reason)
    {
        if (!debugLogging || !debugLogBuildingCellActivations)
            return;

        if (weatherGridManager == null)
            return;

        if (!weatherGridManager.TryGetBuildingAtCell(cell.x, cell.y, out WorldBuildingManager.Record record) ||
            record == null)
        {
            return;
        }

        string buildingName = record.instance != null ? record.instance.name : "null-instance";

        //Debug.Log(
            //$"[TornadoSimulationSystem] Tornado {reason} on building-owned cell {cell} | " +
            //$"TornadoId={tornadoId} | Lifetime={lifetimeRemaining} | " +
            //$"BuildingInstanceId={record.instanceId} | BuildingObject={buildingName}");
    }

    private bool ApplyTornadoStateRows(int startY, int endY)
    {
        bool anyChanged = false;

        for (int y = startY; y < endY; y++)
        {
            for (int x = 0; x < _cols; x++)
            {
                int oldId = _tornadoIdGrid[x, y];
                int oldLifetime = _tornadoLifetimeGrid[x, y];
                bool oldActive = _tornadoActiveGrid[x, y];

                int newId = _nextTornadoIdGrid[x, y];
                int newLifetime = _nextTornadoLifetimeGrid[x, y];
                bool newActive = _nextTornadoActiveGrid[x, y];

                if (oldId > 0)
                {
                    _oldCellById[oldId] = new Vector2Int(x, y);
                    _oldLifetimeById[oldId] = oldLifetime;
                }

                if (newId > 0)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    _newCellById[newId] = cell;
                    _newLifetimeById[newId] = newLifetime;
                    _activeTornadoCells.Add(cell);
                }

                _tornadoIdGrid[x, y] = newId;
                _tornadoLifetimeGrid[x, y] = newLifetime;
                _tornadoActiveGrid[x, y] = newActive;

                if (!oldActive && newActive && debugLogging && debugLogBuildingCellActivations)
                {
                    if (weatherGridManager != null &&
                        weatherGridManager.TryGetBuildingAtCell(x, y, out WorldBuildingManager.Record record) &&
                        record != null)
                    {
                        string buildingName =
                            record.instance != null ? record.instance.name : "null-instance";

                        //Debug.Log(
                            //$"[TornadoSimulationSystem] Tornado cell ACTIVATED on building-owned cell ({x},{y}) | " +
                            //$"TornadoId={newId} | Lifetime={newLifetime} | " +
                            //$"BuildingInstanceId={record.instanceId} | BuildingObject={buildingName}");
                    }
                }

                if (oldId != newId || oldLifetime != newLifetime || oldActive != newActive)
                    anyChanged = true;
            }
        }

        return anyChanged;
    }

    private void DebugCompareTornadoCellToBuildingCache(Vector2Int cell)
    {
        if (!debugLogging || weatherGridManager == null)
            return;

        bool hit = weatherGridManager.TryGetBuildingAtCell(cell.x, cell.y, out WorldBuildingManager.Record record);

        //Debug.Log(
            //$"[TornadoSimulationSystem] Query building cache at tornado cell {cell} | " +
            //$"Hit={hit} | Record={(record != null ? record.instanceId : "null")}");

        if (record != null)
        {
            List<TileCoord> covered = new List<TileCoord>();
            if (weatherGridManager.TryGetBuildingCoveredCells(record.instanceId, covered))
            {
                string coords = "";
                for (int i = 0; i < covered.Count; i++)
                {
                    if (i > 0) coords += ", ";
                    coords += covered[i].ToString();
                }

                //Debug.Log(
                    //$"[TornadoSimulationSystem] Building cache cells for {record.instanceId}: [{coords}]");
            }
        }
    }

    private bool ResolveTornadoCellEvents()
    {
        bool anyCellPositionsChanged = false;

        foreach (KeyValuePair<int, Vector2Int> oldEntry in _oldCellById)
        {
            int tornadoId = oldEntry.Key;
            Vector2Int oldCell = oldEntry.Value;

            if (!_newCellById.TryGetValue(tornadoId, out Vector2Int newCell))
            {
                TornadoExpireEventData expireData = new TornadoExpireEventData
                {
                    tornadoId = tornadoId,
                    cell = oldCell,
                    lastLifetimeRemaining = _oldLifetimeById.TryGetValue(tornadoId, out int oldLifetime)
                        ? oldLifetime
                        : 0
                };

                OnTornadoExpired?.Invoke(expireData);
                anyCellPositionsChanged = true;
                continue;
            }

            if (oldCell != newCell)
            {
                TornadoMoveEventData moveData = new TornadoMoveEventData
                {
                    tornadoId = tornadoId,
                    fromCell = oldCell,
                    toCell = newCell,
                    lifetimeRemaining = _newLifetimeById.TryGetValue(tornadoId, out int newLifetime)
                        ? newLifetime
                        : 0
                };

                OnTornadoMoved?.Invoke(moveData);

                DebugCompareTornadoCellToBuildingCache(newCell);

                anyCellPositionsChanged = true;
            }
            else
            {
                int lifetime = _newLifetimeById.TryGetValue(tornadoId, out int sameCellLifetime)
                    ? sameCellLifetime
                    : 0;

                DebugLogTornadoBuildingCellIfAny(
                    tornadoId,
                    newCell,
                    lifetime,
                    "REMAINED on");
            }
        }

        foreach (KeyValuePair<int, Vector2Int> newEntry in _newCellById)
        {
            int tornadoId = newEntry.Key;
            Vector2Int cell = newEntry.Value;

            if (_oldCellById.ContainsKey(tornadoId))
                continue;

            TornadoSpawnEventData spawnData = BuildSpawnEventData(tornadoId, cell);
            OnTornadoSpawned?.Invoke(spawnData);

            DebugLogTornadoBuildingCellIfAny(
                tornadoId,
                cell,
                spawnData.lifetimeRemaining,
                "SPAWNED onto");

            anyCellPositionsChanged = true;
        }

        return anyCellPositionsChanged;
    }

    private TornadoSpawnEventData BuildSpawnEventData(int tornadoId, Vector2Int cell)
    {
        float humidity = 0f;

        if (weatherGridManager != null &&
            weatherGridManager.TryGetCellState(cell.x, cell.y, out WeatherCellState weatherState) &&
            weatherState.isValid)
        {
            humidity = weatherState.humidity01;
        }

        return new TornadoSpawnEventData
        {
            tornadoId = tornadoId,
            cell = cell,
            lifetimeRemaining = _newLifetimeById.TryGetValue(tornadoId, out int lifetime) ? lifetime : 0,
            stormIntensity01 = stormSimulationSystem != null
                ? stormSimulationSystem.GetStormIntensity01AtCell(cell.x, cell.y)
                : 0f,
            humidity01 = humidity,
            localTemperatureDifference = GetLocalTemperatureDifference(cell.x, cell.y),
            cloudDensity = cloudSimulationSystem != null
                ? cloudSimulationSystem.GetCloudDensityAtCell(cell.x, cell.y)
                : CloudDensity.None
        };
    }

    private bool TryPlaceTornadoInNextGrid(int tornadoId, int x, int y, int lifetimeRemaining)
    {
        if (tornadoId <= 0 || lifetimeRemaining <= 0 || !IsInBounds(x, y))
            return false;

        if (!CanTornadoOccupyEnvironmentCell(x, y))
            return false;

        if (_nextTornadoIdGrid[x, y] != 0)
            return false;

        if (minTornadoSpacingCells > 0 &&
            IsTooCloseToOtherTornado(_nextTornadoIdGrid, x, y, minTornadoSpacingCells))
        {
            return false;
        }

        _nextTornadoIdGrid[x, y] = tornadoId;
        _nextTornadoLifetimeGrid[x, y] = lifetimeRemaining;
        _nextTornadoActiveGrid[x, y] = true;
        _nextActiveCount++;
        return true;
    }

    private bool CanTornadoOccupyEnvironmentCell(int x, int y)
    {
        if (!blockTornadoesByEnvironment)
            return true;

        if (!IsInBounds(x, y))
            return false;

        EnsureLinks();

        if (environmentDataSource == null)
            return true;

        TileCoord coord = new TileCoord(x, y);

        bool hasEnvironment = environmentDataSource.HasLiveEnvironmentTile(coord);

        if (!hasEnvironment)
            return !blockCellsWithoutEnvironment;

        TileEnvironmentData data = environmentDataSource.GetTileData(coord);

        if (IsBlockedTornadoEnvironmentType(data.environmentType))
        {
            if (debugLogging)
            {
                //Debug.Log(
                    //$"[TornadoSimulationSystem] Blocked tornado cell ({x},{y}) " +
                    //$"because environmentType={data.environmentType}");
            }

            return false;
        }

        if (IsBlockedTornadoTileType(data.tileType))
        {
            if (debugLogging)
            {
                //Debug.Log(
                    //$"[TornadoSimulationSystem] Blocked tornado cell ({x},{y}) " +
                    //$"because tileType={data.tileType}");
            }

            return false;
        }

        return true;
    }

    private bool IsBlockedTornadoEnvironmentType(EnvironmentType type)
    {
        if (blockedTornadoEnvironmentTypes == null)
            return false;

        for (int i = 0; i < blockedTornadoEnvironmentTypes.Length; i++)
        {
            if (blockedTornadoEnvironmentTypes[i] == type)
                return true;
        }

        return false;
    }

    private bool IsBlockedTornadoTileType(EnvironmentTileType type)
    {
        if (blockedTornadoTileTypes == null)
            return false;

        for (int i = 0; i < blockedTornadoTileTypes.Length; i++)
        {
            if (blockedTornadoTileTypes[i] == type)
                return true;
        }

        return false;
    }

    private void AddSpawnCandidate(TornadoSpawnCandidate candidate)
    {
        int limit = Mathf.Max(1, maxSpawnCandidatesPerStep);

        if (_spawnCandidates.Count < limit)
        {
            _spawnCandidates.Add(candidate);
            return;
        }

        int weakestIndex = 0;
        float weakestScore = _spawnCandidates[0].score;

        for (int i = 1; i < _spawnCandidates.Count; i++)
        {
            if (_spawnCandidates[i].score < weakestScore)
            {
                weakestScore = _spawnCandidates[i].score;
                weakestIndex = i;
            }
        }

        if (candidate.score > weakestScore)
            _spawnCandidates[weakestIndex] = candidate;
    }

    private float GetSpawnChance(
        float stormIntensity01,
        float humidity01,
        float localTemperatureDifference,
        CloudDensity cloudDensity)
    {
        float intensityT = Mathf.InverseLerp(tornadoStormIntensityThreshold, 1f, stormIntensity01);
        float humidityT = Mathf.InverseLerp(tornadoHumidityThreshold, 1f, humidity01);
        float tempT = Mathf.Clamp01(
            localTemperatureDifference /
            Mathf.Max(0.01f, tornadoTemperatureDifferenceThreshold * 2f));

        float cloudSupport = GetCloudSupport01(cloudDensity);

        float chance = tornadoBaseSpawnChancePerCandidateStep;
        chance += intensityT * 0.08f;
        chance += humidityT * 0.06f;
        chance += tempT * 0.06f;
        chance += cloudSupport * 0.08f;

        if (cloudDensity == CloudDensity.High)
            chance += highCloudSpawnChanceBonus;

        return Mathf.Clamp01(chance);
    }

    private float GetSpawnScore(
        float stormIntensity01,
        float humidity01,
        float localTemperatureDifference,
        CloudDensity cloudDensity)
    {
        float intensityT = Mathf.InverseLerp(tornadoStormIntensityThreshold, 1f, stormIntensity01);
        float humidityT = Mathf.InverseLerp(tornadoHumidityThreshold, 1f, humidity01);
        float tempT = Mathf.Clamp01(
            localTemperatureDifference /
            Mathf.Max(0.01f, tornadoTemperatureDifferenceThreshold * 2f));

        float cloudSupport = GetCloudSupport01(cloudDensity);

        return
            intensityT * 0.35f +
            humidityT * 0.25f +
            tempT * 0.20f +
            cloudSupport * 0.20f +
            UnityEngine.Random.value * 0.01f;
    }

    private float GetCloudSupport01(CloudDensity density)
    {
        switch (density)
        {
            case CloudDensity.Low: return 0.25f;
            case CloudDensity.Mid: return 0.65f;
            case CloudDensity.High: return 1f;
            default: return 0f;
        }
    }

    private Vector2Int GetTornadoWindTarget(int x, int y)
    {
        if (cloudSimulationSystem != null)
            return cloudSimulationSystem.GetWindTargetForCell(x, y);

        return new Vector2Int(x, y);
    }

    private float GetLocalTemperatureDifference(int x, int y)
    {
        if (weatherGridManager == null)
            return 0f;

        if (!weatherGridManager.TryGetCellState(x, y, out WeatherCellState center) || !center.isValid)
            return 0f;

        float maxDifference = 0f;

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

                if (!weatherGridManager.TryGetCellState(nx, ny, out WeatherCellState neighbour) || !neighbour.isValid)
                    continue;

                float difference = Mathf.Abs(neighbour.temperatureC - center.temperatureC);
                if (difference > maxDifference)
                    maxDifference = difference;
            }
        }

        return maxDifference;
    }

    private bool IsTooCloseToOtherTornado(int[,] tornadoIdGrid, int x, int y, int spacing)
    {
        if (tornadoIdGrid == null || spacing <= 0)
            return false;

        int minX = Mathf.Max(0, x - spacing);
        int maxX = Mathf.Min(_cols - 1, x + spacing);
        int minY = Mathf.Max(0, y - spacing);
        int maxY = Mathf.Min(_rows - 1, y + spacing);

        for (int tx = minX; tx <= maxX; tx++)
        {
            for (int ty = minY; ty <= maxY; ty++)
            {
                if (tx == x && ty == y)
                    continue;

                if (tornadoIdGrid[tx, ty] <= 0)
                    continue;

                int distance = Mathf.Max(Mathf.Abs(tx - x), Mathf.Abs(ty - y));
                if (distance <= spacing)
                    return true;
            }
        }

        return false;
    }

    private void RequestAdvanceTornadoes()
    {
        if (!TryInitializeGrid())
            return;

        if (_lastTornadoAdvanceFrame == Time.frameCount)
            return;

        _lastTornadoAdvanceFrame = Time.frameCount;
        AdvanceTornadoesOneStep();
    }

    public bool TryGetTornadoCellState(int x, int y, out TornadoCellState state)
    {
        state = default;

        if (!_isInitialized || !IsInBounds(x, y) || _tornadoLifetimeGrid == null || _tornadoActiveGrid == null)
            return false;

        state = new TornadoCellState
        {
            isActive = _tornadoActiveGrid[x, y],
            lifetimeRemaining = _tornadoLifetimeGrid[x, y]
        };

        return state.isActive;
    }

    public TornadoCellState GetTornadoCellStateOrDefault(int x, int y)
    {
        return TryGetTornadoCellState(x, y, out TornadoCellState state)
            ? state
            : default;
    }

    public bool IsTornadoActiveAtCell(int x, int y)
    {
        if (!_isInitialized || !IsInBounds(x, y) || _tornadoActiveGrid == null)
            return false;

        return _tornadoActiveGrid[x, y];
    }

    public int GetTornadoLifetimeAtCell(int x, int y)
    {
        if (!_isInitialized || !IsInBounds(x, y) || _tornadoLifetimeGrid == null)
            return 0;

        return Mathf.Max(0, _tornadoLifetimeGrid[x, y]);
    }

    public int GetActiveTornadoCount()
    {
        return _activeTornadoCells.Count;
    }

    public IReadOnlyList<Vector2Int> GetActiveTornadoCells()
    {
        return _activeTornadoCells;
    }

    public bool CopyActiveTornadoCells(List<Vector2Int> results)
    {
        if (results == null)
            return false;

        results.Clear();

        if (!_isInitialized || _activeTornadoCells.Count == 0)
            return false;

        for (int i = 0; i < _activeTornadoCells.Count; i++)
            results.Add(_activeTornadoCells[i]);

        return results.Count > 0;
    }

    public void ClearAllTornadoes()
    {
        if (!TryInitializeGrid())
            return;

        _oldCellById.Clear();
        _oldLifetimeById.Clear();

        for (int x = 0; x < _cols; x++)
        {
            for (int y = 0; y < _rows; y++)
            {
                int tornadoId = _tornadoIdGrid[x, y];
                int lifetime = _tornadoLifetimeGrid[x, y];

                if (tornadoId > 0)
                {
                    _oldCellById[tornadoId] = new Vector2Int(x, y);
                    _oldLifetimeById[tornadoId] = lifetime;
                }
            }
        }

        Array.Clear(_tornadoLifetimeGrid, 0, _tornadoLifetimeGrid.Length);
        Array.Clear(_tornadoActiveGrid, 0, _tornadoActiveGrid.Length);
        Array.Clear(_tornadoIdGrid, 0, _tornadoIdGrid.Length);

        Array.Clear(_nextTornadoLifetimeGrid, 0, _nextTornadoLifetimeGrid.Length);
        Array.Clear(_nextTornadoActiveGrid, 0, _nextTornadoActiveGrid.Length);
        Array.Clear(_nextTornadoIdGrid, 0, _nextTornadoIdGrid.Length);

        _activeTornadoCells.Clear();

        bool hadAny = _oldCellById.Count > 0;
        if (!hadAny)
            return;

        foreach (KeyValuePair<int, Vector2Int> oldEntry in _oldCellById)
        {
            TornadoExpireEventData expireData = new TornadoExpireEventData
            {
                tornadoId = oldEntry.Key,
                cell = oldEntry.Value,
                lastLifetimeRemaining = _oldLifetimeById.TryGetValue(oldEntry.Key, out int lifetime)
                    ? lifetime
                    : 0
            };

            OnTornadoExpired?.Invoke(expireData);
        }

        OnTornadoStateChanged?.Invoke();
        OnTornadoCellsChanged?.Invoke();
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

        RequestAdvanceTornadoes();
    }

    private void HandleCloudStateChanged()
    {
        if (!advanceOnCloudStateChanged)
            return;

        RequestAdvanceTornadoes();
    }

    private void HandleStormStateChanged()
    {
        if (!advanceOnStormStateChanged)
            return;

        RequestAdvanceTornadoes();
    }

    private void EnsureLinks()
    {
        if (weatherGridManager == null)
            weatherGridManager = WeatherGridManager.Instance;

        if (cloudSimulationSystem == null)
            cloudSimulationSystem = CloudSimulationSystem.Instance;

        if (stormSimulationSystem == null)
            stormSimulationSystem = StormSimulationSystem.Instance;

        if (environmentDataSource == null)
            environmentDataSource = MonoEnvironmentDataSource.Instance;
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
            _subscribedCloudSimulationSystem.OnCloudStateChanged -= HandleCloudStateChanged;
        }

        _subscribedCloudSimulationSystem = cloudSimulationSystem;

        if (_subscribedCloudSimulationSystem != null)
        {
            _subscribedCloudSimulationSystem.OnCloudGridInitialized += HandleCloudGridInitialized;
            _subscribedCloudSimulationSystem.OnCloudStateChanged += HandleCloudStateChanged;
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
            _subscribedCloudSimulationSystem.OnCloudStateChanged -= HandleCloudStateChanged;
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

    public TornadoSimulationSaveData SaveState()
    {
        TornadoSimulationSaveData data = new TornadoSimulationSaveData
        {
            nextTornadoId = Mathf.Max(1, _nextTornadoId)
        };

        if (!_isInitialized ||
            _tornadoActiveGrid == null ||
            _tornadoLifetimeGrid == null ||
            _tornadoIdGrid == null)
        {
            return data;
        }

        for (int i = 0; i < _activeTornadoCells.Count; i++)
        {
            Vector2Int cell = _activeTornadoCells[i];

            int x = cell.x;
            int y = cell.y;

            if (!IsInBounds(x, y))
                continue;

            if (!_tornadoActiveGrid[x, y])
                continue;

            int lifetime = _tornadoLifetimeGrid[x, y];
            if (lifetime <= 0)
                continue;

            int tornadoId = _tornadoIdGrid[x, y];
            if (tornadoId <= 0)
                continue;

            data.tornadoes.Add(new TornadoCellSaveData
            {
                tornadoId = tornadoId,
                x = x,
                y = y,
                lifetimeRemaining = lifetime,
                strength01 = 1f
            });
        }

        return data;
    }

    public void LoadState(TornadoSimulationSaveData data)
    {
        if (data == null)
            return;

        if (!TryInitializeGrid())
        {
            if (debugLogging)
                //Debug.LogWarning("[TornadoSimulationSystem] Could not load tornado state because grid is not initialized yet.");

            return;
        }

        if (_tornadoStepCoroutine != null)
        {
            StopCoroutine(_tornadoStepCoroutine);
            _tornadoStepCoroutine = null;
        }

        _tornadoAdvanceQueued = false;
        _isAdvancingTornadoes = false;

        Array.Clear(_tornadoLifetimeGrid, 0, _tornadoLifetimeGrid.Length);
        Array.Clear(_tornadoActiveGrid, 0, _tornadoActiveGrid.Length);
        Array.Clear(_tornadoIdGrid, 0, _tornadoIdGrid.Length);

        Array.Clear(_nextTornadoLifetimeGrid, 0, _nextTornadoLifetimeGrid.Length);
        Array.Clear(_nextTornadoActiveGrid, 0, _nextTornadoActiveGrid.Length);
        Array.Clear(_nextTornadoIdGrid, 0, _nextTornadoIdGrid.Length);

        _activeTornadoCells.Clear();
        _spawnCandidates.Clear();
        _oldCellById.Clear();
        _oldLifetimeById.Clear();
        _newCellById.Clear();
        _newLifetimeById.Clear();

        int highestLoadedId = 0;

        if (data.tornadoes != null)
        {
            for (int i = 0; i < data.tornadoes.Count; i++)
            {
                TornadoCellSaveData saved = data.tornadoes[i];

                if (!IsInBounds(saved.x, saved.y))
                    continue;

                if (saved.lifetimeRemaining <= 0)
                    continue;

                if (!CanTornadoOccupyEnvironmentCell(saved.x, saved.y))
                    continue;

                int tornadoId = saved.tornadoId > 0 ? saved.tornadoId : highestLoadedId + 1;

                _tornadoIdGrid[saved.x, saved.y] = tornadoId;
                _tornadoLifetimeGrid[saved.x, saved.y] = saved.lifetimeRemaining;
                _tornadoActiveGrid[saved.x, saved.y] = true;

                _activeTornadoCells.Add(new Vector2Int(saved.x, saved.y));

                if (tornadoId > highestLoadedId)
                    highestLoadedId = tornadoId;
            }
        }

        _nextTornadoId = Mathf.Max(1, data.nextTornadoId, highestLoadedId + 1);

        OnTornadoStateChanged?.Invoke();
        OnTornadoCellsChanged?.Invoke();

        if (debugLogging)
            //Debug.Log($"[TornadoSimulationSystem] Loaded {_activeTornadoCells.Count} tornado cells.");
    }

    public void ApplyPresetSettings(TornadoPresetSettings settings)
    {
        if (settings == null || !settings.overrideTornados)
            return;

        tornadoBaseSpawnChancePerCandidateStep = settings.tornadoBaseSpawnChancePerCandidateStep;
        tornadoStormIntensityThreshold = settings.tornadoStormIntensityThreshold;
        tornadoHumidityThreshold = settings.tornadoHumidityThreshold;
        tornadoTemperatureDifferenceThreshold = settings.tornadoTemperatureDifferenceThreshold;
        minimumCloudDensityToSpawn = settings.minimumCloudDensityToSpawn;
        highCloudSpawnChanceBonus = settings.highCloudSpawnChanceBonus;

        tornadoMinLifetimeTurns = settings.tornadoMinLifetimeTurns;
        tornadoMaxLifetimeTurns = settings.tornadoMaxLifetimeTurns;
        maxActiveTornadoes = settings.maxActiveTornadoes;
        minTornadoSpacingCells = settings.minTornadoSpacingCells;
        maxNewTornadoesPerStep = settings.maxNewTornadoesPerStep;
        maxSpawnCandidatesPerStep = settings.maxSpawnCandidatesPerStep;

        if (debugLogging)
            //Debug.Log("[TornadoSimulationSystem] Applied tornado preset settings.");
    }
}
