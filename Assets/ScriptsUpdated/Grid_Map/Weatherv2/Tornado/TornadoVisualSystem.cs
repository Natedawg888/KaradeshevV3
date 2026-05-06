using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Visual-only tornado layer on top of TornadoSimulationSystem.
/// Owns pooled tornado visuals, visual root, queued refresh, and spin animation.
/// No tornado gameplay/effect logic here.
/// </summary>
public class TornadoVisualSystem : MonoBehaviour
{
    public static TornadoVisualSystem Instance { get; private set; }

    [Header("References")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private WeatherGridManager weatherGridManager;
    [SerializeField] private CloudSimulationSystem cloudSimulationSystem;
    [SerializeField] private TornadoSimulationSystem tornadoSimulationSystem;
    [SerializeField] private Transform tornadoVisualRoot;
    [SerializeField] private TornadoVisualPool tornadoPool;

    [Header("Lifecycle")]
    [SerializeField] private bool initializeOnStart = true;
    [SerializeField] private bool rebuildVisualsOnGridInitialized = true;
    [SerializeField] private bool rebuildVisualsOnEnable = true;

    [Header("Visuals")]
    [SerializeField] private GameObject[] tornadoPrefabs;
    [SerializeField] private float tornadoVisualHeight = 0f;
    [SerializeField] private bool randomizeSpawnYRotation = true;

    [Header("Visual Spin")]
    [SerializeField] private bool tornadoSpinEnabled = true;
    [SerializeField] private float tornadoSpinSpeed = 240f;

    [Header("Pool Warmup")]
    [SerializeField] private bool prewarmPoolOnInitialize = true;
    [Min(0)][SerializeField] private int tornadoPrewarmInstancesPerPrefab = 1;
    [Min(1)][SerializeField] private int maxCreatesPerPrewarmCall = 1;

    [Header("Queued Refresh Performance")]
    [SerializeField] private bool enableQueuedVisualRefresh = true;
    [Min(1)][SerializeField] private int visualRefreshesPerFrame = 8;
    [Min(0f)][SerializeField] private float visualRefreshIntervalSeconds = 0f;

    [Header("Tornado Cloud Height Response")]
    [SerializeField] private bool followCloudHeight = true;

    [Tooltip("Extra Y offset added on top of tornadoVisualHeight from the cloud's actual height.")]
    [SerializeField] private float tornadoCloudHeightOffsetMultiplier = 1f;

    [Tooltip("Base local scale before any cloud-based stretch.")]
    [SerializeField] private Vector3 tornadoBaseScale = Vector3.one;

    [Tooltip("How much the cloud height difference stretches the tornado upward.")]
    [SerializeField] private float tornadoCloudStretchMultiplier = 1f;

    [Tooltip("Clamp the extra Y scale added from cloud height.")]
    [SerializeField] private float maxExtraTornadoYScale = 2f;

    [Tooltip("Absolute cap for final tornado Y scale.")]
    [SerializeField] private float maxTornadoYScale = 3f;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = false;

    public bool IsInitialized => _isInitialized;
    public int Columns => _cols;
    public int Rows => _rows;

    private int _cols;
    private int _rows;
    private bool _isInitialized;

    private GameObject[,] _tornadoVisuals;
    private GameObject[,] _tornadoVisualPrefabs;

    private readonly List<Transform> _activeSpinTargets = new List<Transform>(16);

    private readonly Queue<Vector2Int> _pendingVisualRefreshes = new Queue<Vector2Int>();
    private readonly HashSet<int> _pendingVisualRefreshKeys = new HashSet<int>();

    private Coroutine _visualRefreshCoroutine;
    private Coroutine _waitForSourcesReadyCoroutine;

    private WaitForSeconds _cachedRefreshWait;
    private float _cachedRefreshWaitSeconds = -1f;

    private WeatherGridManager _subscribedWeatherGridManager;
    private TornadoSimulationSystem _subscribedTornadoSimulationSystem;

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

        if (rebuildVisualsOnEnable)
            RebuildAllVisualsFromState();
    }

    private void Start()
    {
        if (initializeOnStart)
            BeginWaitingForSourcesReady();
    }

    private void OnDisable()
    {
        UnbindSourceEvents();
        StopVisualRefreshRoutine();

        if (_waitForSourcesReadyCoroutine != null)
        {
            StopCoroutine(_waitForSourcesReadyCoroutine);
            _waitForSourcesReadyCoroutine = null;
        }
    }

    private void OnDestroy()
    {
        UnbindSourceEvents();
        ClearAllVisuals();

        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        SpinTornadoVisuals();
    }

    public void InstallRuntimeRefs(
    GridManager newGridManager,
    WeatherGridManager newWeatherGridManager,
    TornadoSimulationSystem newTornadoSimulationSystem,
    CloudSimulationSystem newCloudSimulationSystem = null,
    Transform newVisualRoot = null,
    TornadoVisualPool newTornadoPool = null,
    bool initializeNow = true)
    {
        if (newGridManager != null)
            gridManager = newGridManager;

        if (newWeatherGridManager != null)
            weatherGridManager = newWeatherGridManager;

        if (newTornadoSimulationSystem != null)
            tornadoSimulationSystem = newTornadoSimulationSystem;

        if (newCloudSimulationSystem != null)
            cloudSimulationSystem = newCloudSimulationSystem;

        if (newVisualRoot != null)
            tornadoVisualRoot = newVisualRoot;

        if (newTornadoPool != null)
            tornadoPool = newTornadoPool;

        EnsurePool();
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
            EnsurePool();
            RebindSourceEvents();

            if (TryInitializeGrid())
            {
                if (rebuildVisualsOnGridInitialized)
                    RebuildAllVisualsFromState();

                if (debugLogging)
                    Debug.Log("[TornadoVisualSystem] Sources ready. Visual system initialized.");

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

        if (tornadoSimulationSystem == null || !tornadoSimulationSystem.IsInitialized)
            return false;

        if (gridManager == null)
            return false;

        int newCols = weatherGridManager.Columns;
        int newRows = weatherGridManager.Rows;

        if (newCols <= 0 || newRows <= 0)
            return false;

        bool sizeChanged = !_isInitialized || newCols != _cols || newRows != _rows;

        if (sizeChanged)
            ClearAllVisuals();

        _cols = newCols;
        _rows = newRows;

        if (sizeChanged)
        {
            _tornadoVisuals = new GameObject[_cols, _rows];
            _tornadoVisualPrefabs = new GameObject[_cols, _rows];
        }

        _isInitialized = true;

        if (sizeChanged && prewarmPoolOnInitialize)
            PrewarmTornadoPool();

        if (debugLogging && sizeChanged)
            Debug.Log($"[TornadoVisualSystem] Initialized {_cols}x{_rows}");

        return true;
    }

    public void RebuildAllVisualsFromState()
    {
        if (!TryInitializeGrid())
            return;

        ClearAllVisuals();

        IReadOnlyList<Vector2Int> cells = tornadoSimulationSystem.GetActiveTornadoCells();
        if (cells == null || cells.Count == 0)
            return;

        for (int i = 0; i < cells.Count; i++)
        {
            Vector2Int cell = cells[i];
            if (!IsInBounds(cell.x, cell.y))
                continue;

            RefreshTornadoVisualAtCell(cell.x, cell.y);
        }
    }

    private void HandleWeatherGridInitialized()
    {
        if (!TryInitializeGrid())
            return;

        if (rebuildVisualsOnGridInitialized)
            RebuildAllVisualsFromState();
    }

    private void HandleTornadoGridInitialized()
    {
        if (!TryInitializeGrid())
            return;

        if (rebuildVisualsOnGridInitialized)
            RebuildAllVisualsFromState();
    }

    private void HandleTornadoStateChanged()
    {
        // Safe fallback resync point if another listener missed granular events.
        QueueRefreshForAllActiveTornadoCells();
    }

    private void HandleTornadoCellsChanged()
    {
        // State already changed before this event fires, so a queued refresh can reconcile.
        QueueRefreshForAllExistingVisualsAndActiveTornadoCells();
    }

    private void HandleTornadoSpawned(TornadoSpawnEventData data)
    {
        QueueTornadoVisualRefresh(data.cell.x, data.cell.y);
    }

    private void HandleTornadoExpired(TornadoExpireEventData data)
    {
        QueueTornadoVisualRefresh(data.cell.x, data.cell.y);
    }

    private void HandleTornadoMoved(TornadoMoveEventData data)
    {
        if (!TryInitializeGrid())
            return;

        if (!enableQueuedVisualRefresh)
        {
            MoveOrRefreshTornadoVisual(data.fromCell.x, data.fromCell.y, data.toCell.x, data.toCell.y);
            return;
        }

        // Queue both endpoints. The refresh routine will reconcile with current state.
        QueueTornadoVisualRefresh(data.fromCell.x, data.fromCell.y);
        QueueTornadoVisualRefresh(data.toCell.x, data.toCell.y);
    }

    private void MoveOrRefreshTornadoVisual(int fromX, int fromY, int toX, int toY)
    {
        if (_tornadoVisuals == null || _tornadoVisualPrefabs == null)
            return;

        if (!IsInBounds(fromX, fromY) || !IsInBounds(toX, toY))
            return;

        GameObject instance = _tornadoVisuals[fromX, fromY];
        GameObject prefab = _tornadoVisualPrefabs[fromX, fromY];

        if (instance == null || prefab == null)
        {
            RefreshTornadoVisualAtCell(fromX, fromY);
            RefreshTornadoVisualAtCell(toX, toY);
            return;
        }

        _tornadoVisuals[fromX, fromY] = null;
        _tornadoVisualPrefabs[fromX, fromY] = null;

        if (!ShouldShowTornadoVisualAtCell(toX, toY))
        {
            tornadoPool.Return(prefab, instance, stopPooledEffects: false);
            RemoveSpinTarget(instance.transform);
            return;
        }

        if (_tornadoVisuals[toX, toY] != null)
            ReturnTornadoVisualAtCell(toX, toY);

        _tornadoVisuals[toX, toY] = instance;
        _tornadoVisualPrefabs[toX, toY] = prefab;

        Transform tr = instance.transform;
        tr.SetParent(GetOrCreateVisualRoot(), true);
        tr.SetPositionAndRotation(GetTornadoWorldPosition(toX, toY), tr.rotation);
        tr.localScale = GetTornadoScaleForCell(toX, toY);
        instance.name = $"Tornado_{toX}_{toY}";

        AddSpinTarget(tr);
    }

    private void QueueRefreshForAllActiveTornadoCells()
    {
        if (!_isInitialized || tornadoSimulationSystem == null)
            return;

        IReadOnlyList<Vector2Int> cells = tornadoSimulationSystem.GetActiveTornadoCells();
        if (cells == null)
            return;

        for (int i = 0; i < cells.Count; i++)
        {
            Vector2Int cell = cells[i];
            QueueTornadoVisualRefresh(cell.x, cell.y);
        }
    }

    private void QueueRefreshForAllExistingVisualsAndActiveTornadoCells()
    {
        if (!_isInitialized)
            return;

        if (_tornadoVisuals != null)
        {
            for (int x = 0; x < _cols; x++)
            {
                for (int y = 0; y < _rows; y++)
                {
                    if (_tornadoVisuals[x, y] != null)
                        QueueTornadoVisualRefresh(x, y);
                }
            }
        }

        QueueRefreshForAllActiveTornadoCells();
    }

    private bool ShouldShowTornadoVisualAtCell(int x, int y)
    {
        if (!_isInitialized || !IsInBounds(x, y))
            return false;

        if (tornadoSimulationSystem == null)
            return false;

        if (!HasAnyUsablePrefabs(tornadoPrefabs))
            return false;

        return tornadoSimulationSystem.IsTornadoActiveAtCell(x, y);
    }

    private void QueueTornadoVisualRefresh(int x, int y)
    {
        if (!_isInitialized || !IsInBounds(x, y))
            return;

        bool shouldShow = ShouldShowTornadoVisualAtCell(x, y);
        bool hasVisual = _tornadoVisuals != null && _tornadoVisuals[x, y] != null;

        if (!shouldShow && !hasVisual)
            return;

        if (!enableQueuedVisualRefresh)
        {
            RefreshTornadoVisualAtCell(x, y);
            return;
        }

        int key = GetCellKey(x, y);
        if (!_pendingVisualRefreshKeys.Add(key))
            return;

        _pendingVisualRefreshes.Enqueue(new Vector2Int(x, y));
        EnsureVisualRefreshRoutine();
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
                _pendingVisualRefreshKeys.Remove(GetCellKey(cell.x, cell.y));

                if (!_isInitialized || !IsInBounds(cell.x, cell.y))
                    continue;

                RefreshTornadoVisualAtCell(cell.x, cell.y);
                refreshedThisBatch++;
            }

            if (_pendingVisualRefreshes.Count > 0)
            {
                float interval = Mathf.Max(0f, visualRefreshIntervalSeconds);

                if (interval > 0f)
                {
                    if (_cachedRefreshWait == null ||
                        Mathf.Abs(_cachedRefreshWaitSeconds - interval) > 0.0001f)
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

    private void StopVisualRefreshRoutine()
    {
        _pendingVisualRefreshes.Clear();
        _pendingVisualRefreshKeys.Clear();

        if (_visualRefreshCoroutine != null)
        {
            StopCoroutine(_visualRefreshCoroutine);
            _visualRefreshCoroutine = null;
        }

        _cachedRefreshWait = null;
        _cachedRefreshWaitSeconds = -1f;
    }

    private void RefreshTornadoVisualAtCell(int x, int y)
    {
        EnsurePool();

        bool shouldShow = ShouldShowTornadoVisualAtCell(x, y);

        if (!shouldShow)
        {
            ReturnTornadoVisualAtCell(x, y);
            return;
        }

        GameObject desiredPrefab = GetPreferredPrefabForCell(x, y);
        if (desiredPrefab == null)
        {
            ReturnTornadoVisualAtCell(x, y);
            return;
        }

        Transform root = GetOrCreateVisualRoot();
        Vector3 worldPos = GetTornadoWorldPosition(x, y);
        Vector3 targetScale = GetTornadoScaleForCell(x, y);

        if (_tornadoVisuals[x, y] != null && _tornadoVisualPrefabs[x, y] != desiredPrefab)
            ReturnTornadoVisualAtCell(x, y);

        if (_tornadoVisuals[x, y] == null)
        {
            Quaternion rotation = Quaternion.identity;

            if (randomizeSpawnYRotation)
                rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);

            GameObject instance = tornadoPool.Get(
                desiredPrefab,
                root,
                worldPos,
                rotation,
                resetPooledEffects: false);

            instance.name = $"Tornado_{x}_{y}";

            _tornadoVisuals[x, y] = instance;
            _tornadoVisualPrefabs[x, y] = desiredPrefab;

            AddSpinTarget(instance.transform);
        }

        Transform tr = _tornadoVisuals[x, y].transform;
        tr.SetParent(root, true);
        tr.position = worldPos;
        tr.localScale = targetScale;
        _tornadoVisuals[x, y].name = $"Tornado_{x}_{y}";
    }

    private void ReturnTornadoVisualAtCell(int x, int y)
    {
        if (_tornadoVisuals == null || _tornadoVisualPrefabs == null || !IsInBounds(x, y))
            return;

        GameObject instance = _tornadoVisuals[x, y];
        GameObject prefab = _tornadoVisualPrefabs[x, y];

        if (instance != null)
            RemoveSpinTarget(instance.transform);

        if (instance != null && prefab != null && tornadoPool != null)
            tornadoPool.Return(prefab, instance, stopPooledEffects: false);

        _tornadoVisuals[x, y] = null;
        _tornadoVisualPrefabs[x, y] = null;
    }

    private void ClearAllVisuals()
    {
        StopVisualRefreshRoutine();
        _activeSpinTargets.Clear();

        if (_tornadoVisuals == null)
            return;

        for (int x = 0; x < _tornadoVisuals.GetLength(0); x++)
        {
            for (int y = 0; y < _tornadoVisuals.GetLength(1); y++)
                ReturnTornadoVisualAtCell(x, y);
        }
    }

    private void PrewarmTornadoPool()
    {
        EnsurePool();

        if (!HasAnyUsablePrefabs(tornadoPrefabs))
            return;

        Transform parent = GetOrCreateVisualRoot();
        int targetPerPrefab = Mathf.Max(0, tornadoPrewarmInstancesPerPrefab);

        if (targetPerPrefab <= 0)
            return;

        for (int i = 0; i < tornadoPrefabs.Length; i++)
        {
            GameObject prefab = tornadoPrefabs[i];
            if (prefab == null)
                continue;

            tornadoPool.Prewarm(prefab, targetPerPrefab, parent, maxCreatesPerPrewarmCall);
        }
    }

    private void SpinTornadoVisuals()
    {
        if (!tornadoSpinEnabled)
            return;

        if (Mathf.Approximately(tornadoSpinSpeed, 0f) || _activeSpinTargets.Count == 0)
            return;

        float delta = tornadoSpinSpeed * Time.deltaTime;

        for (int i = _activeSpinTargets.Count - 1; i >= 0; i--)
        {
            Transform t = _activeSpinTargets[i];
            if (t == null)
            {
                _activeSpinTargets.RemoveAt(i);
                continue;
            }

            t.Rotate(0f, delta, 0f, Space.Self);
        }
    }

    private void AddSpinTarget(Transform tr)
    {
        if (tr == null)
            return;

        _activeSpinTargets.Remove(tr);
        _activeSpinTargets.Add(tr);
    }

    private void RemoveSpinTarget(Transform tr)
    {
        if (tr == null)
            return;

        _activeSpinTargets.Remove(tr);
    }

    private Transform GetOrCreateVisualRoot()
    {
        if (tornadoVisualRoot != null)
            return tornadoVisualRoot;

        GameObject root = new GameObject("Tornado Visual Root");
        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;
        tornadoVisualRoot = root.transform;
        return tornadoVisualRoot;
    }

    private Vector3 GetTornadoWorldPosition(int x, int y)
    {
        if (gridManager == null)
            return Vector3.zero;

        Vector3 cellCorner = gridManager.GetWorldPosition(x, y);

        float weatherBaseHeight = weatherGridManager != null
            ? weatherGridManager.WeatherGridBaseHeight
            : 0f;

        float finalY = weatherBaseHeight + tornadoVisualHeight;

        if (followCloudHeight && cloudSimulationSystem != null && cloudSimulationSystem.IsInitialized)
        {
            if (cloudSimulationSystem.TryGetCloudWorldPosition(x, y, out Vector3 cloudWorldPos))
            {
                float cloudOffsetAboveWeatherBase = cloudWorldPos.y - weatherBaseHeight;
                finalY += cloudOffsetAboveWeatherBase * Mathf.Max(0f, tornadoCloudHeightOffsetMultiplier);
            }
        }

        return new Vector3(
            cellCorner.x + (gridManager.cellSize * 0.5f),
            finalY,
            cellCorner.z + (gridManager.cellSize * 0.5f));
    }

    private Vector3 GetTornadoScaleForCell(int x, int y)
    {
        Vector3 scale = tornadoBaseScale;

        if (!followCloudHeight || cloudSimulationSystem == null || !cloudSimulationSystem.IsInitialized || weatherGridManager == null)
            return scale;

        if (!cloudSimulationSystem.TryGetCloudWorldPosition(x, y, out Vector3 cloudWorldPos))
            return scale;

        float weatherBaseHeight = weatherGridManager.WeatherGridBaseHeight;
        float cloudOffsetAboveWeatherBase = Mathf.Max(0f, cloudWorldPos.y - weatherBaseHeight);

        float extraY = cloudOffsetAboveWeatherBase * Mathf.Max(0f, tornadoCloudStretchMultiplier);
        extraY = Mathf.Min(extraY, Mathf.Max(0f, maxExtraTornadoYScale));

        scale.y += extraY;
        scale.y = Mathf.Min(scale.y, Mathf.Max(tornadoBaseScale.y, maxTornadoYScale));

        return scale;
    }

    private GameObject GetPreferredPrefabForCell(int x, int y)
    {
        // Keep prefab stable if the cell already has one.
        if (_tornadoVisualPrefabs != null && IsInBounds(x, y) && _tornadoVisualPrefabs[x, y] != null)
            return _tornadoVisualPrefabs[x, y];

        return GetRandomUsablePrefab(tornadoPrefabs);
    }

    private static GameObject GetRandomUsablePrefab(GameObject[] prefabs)
    {
        if (prefabs == null || prefabs.Length == 0)
            return null;

        int validCount = 0;
        for (int i = 0; i < prefabs.Length; i++)
        {
            if (prefabs[i] != null)
                validCount++;
        }

        if (validCount == 0)
            return null;

        int chosen = UnityEngine.Random.Range(0, validCount);

        for (int i = 0; i < prefabs.Length; i++)
        {
            if (prefabs[i] == null)
                continue;

            if (chosen == 0)
                return prefabs[i];

            chosen--;
        }

        return null;
    }

    private static bool HasAnyUsablePrefabs(GameObject[] prefabs)
    {
        if (prefabs == null || prefabs.Length == 0)
            return false;

        for (int i = 0; i < prefabs.Length; i++)
        {
            if (prefabs[i] != null)
                return true;
        }

        return false;
    }

    private int GetCellKey(int x, int y)
    {
        return x + (y * Mathf.Max(1, _cols));
    }

    private bool IsInBounds(int x, int y)
    {
        return x >= 0 && x < _cols && y >= 0 && y < _rows;
    }

    private void EnsurePool()
    {
        if (tornadoPool != null)
            return;

        GameObject go = new GameObject("Tornado Visual Pool");
        go.transform.SetParent(transform, false);
        tornadoPool = go.AddComponent<TornadoVisualPool>();
    }

    private void EnsureLinks()
    {
        if (gridManager == null)
            gridManager = GridManager.Instance;

        if (weatherGridManager == null)
            weatherGridManager = WeatherGridManager.Instance;

        if (tornadoSimulationSystem == null)
            tornadoSimulationSystem = TornadoSimulationSystem.Instance;

        if (cloudSimulationSystem == null)
            cloudSimulationSystem = CloudSimulationSystem.Instance;
    }

    private void RebindSourceEvents()
    {
        RebindWeatherGridEvents();
        RebindTornadoSimulationEvents();
    }

    private void RebindWeatherGridEvents()
    {
        if (_subscribedWeatherGridManager == weatherGridManager)
            return;

        if (_subscribedWeatherGridManager != null)
            _subscribedWeatherGridManager.OnWeatherGridInitialized -= HandleWeatherGridInitialized;

        _subscribedWeatherGridManager = weatherGridManager;

        if (_subscribedWeatherGridManager != null)
            _subscribedWeatherGridManager.OnWeatherGridInitialized += HandleWeatherGridInitialized;
    }

    private void RebindTornadoSimulationEvents()
    {
        if (_subscribedTornadoSimulationSystem == tornadoSimulationSystem)
            return;

        if (_subscribedTornadoSimulationSystem != null)
        {
            _subscribedTornadoSimulationSystem.OnTornadoGridInitialized -= HandleTornadoGridInitialized;
            _subscribedTornadoSimulationSystem.OnTornadoStateChanged -= HandleTornadoStateChanged;
            _subscribedTornadoSimulationSystem.OnTornadoCellsChanged -= HandleTornadoCellsChanged;
            _subscribedTornadoSimulationSystem.OnTornadoSpawned -= HandleTornadoSpawned;
            _subscribedTornadoSimulationSystem.OnTornadoExpired -= HandleTornadoExpired;
            _subscribedTornadoSimulationSystem.OnTornadoMoved -= HandleTornadoMoved;
        }

        _subscribedTornadoSimulationSystem = tornadoSimulationSystem;

        if (_subscribedTornadoSimulationSystem != null)
        {
            _subscribedTornadoSimulationSystem.OnTornadoGridInitialized += HandleTornadoGridInitialized;
            _subscribedTornadoSimulationSystem.OnTornadoStateChanged += HandleTornadoStateChanged;
            _subscribedTornadoSimulationSystem.OnTornadoCellsChanged += HandleTornadoCellsChanged;
            _subscribedTornadoSimulationSystem.OnTornadoSpawned += HandleTornadoSpawned;
            _subscribedTornadoSimulationSystem.OnTornadoExpired += HandleTornadoExpired;
            _subscribedTornadoSimulationSystem.OnTornadoMoved += HandleTornadoMoved;
        }
    }

    private void UnbindSourceEvents()
    {
        if (_subscribedWeatherGridManager != null)
        {
            _subscribedWeatherGridManager.OnWeatherGridInitialized -= HandleWeatherGridInitialized;
            _subscribedWeatherGridManager = null;
        }

        if (_subscribedTornadoSimulationSystem != null)
        {
            _subscribedTornadoSimulationSystem.OnTornadoGridInitialized -= HandleTornadoGridInitialized;
            _subscribedTornadoSimulationSystem.OnTornadoStateChanged -= HandleTornadoStateChanged;
            _subscribedTornadoSimulationSystem.OnTornadoCellsChanged -= HandleTornadoCellsChanged;
            _subscribedTornadoSimulationSystem.OnTornadoSpawned -= HandleTornadoSpawned;
            _subscribedTornadoSimulationSystem.OnTornadoExpired -= HandleTornadoExpired;
            _subscribedTornadoSimulationSystem.OnTornadoMoved -= HandleTornadoMoved;
            _subscribedTornadoSimulationSystem = null;
        }
    }
}