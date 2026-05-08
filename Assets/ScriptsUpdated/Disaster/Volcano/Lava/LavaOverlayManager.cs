using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LavaOverlayManager : MonoBehaviour
{
    public static LavaOverlayManager Instance { get; private set; }

    private struct LavaVisualRecord
    {
        public GameObject visual;
        public GameObject prefab;
        public LavaOverlayVisualKind kind;
        public float rotationY;
        public Renderer[] renderers;
    }

    private struct LavaVisualSelection
    {
        public bool shouldRender;
        public LavaOverlayVisualKind kind;
        public float rotationY;

        public LavaVisualSelection(bool shouldRender, LavaOverlayVisualKind kind, float rotationY)
        {
            this.shouldRender = shouldRender;
            this.kind = kind;
            this.rotationY = rotationY;
        }
    }

    private struct LavaCellState
    {
        public TileCoord coord;
        public TileCoord source;
        public int distanceFromSource;

        public float heat01;
        public int coolingDelayTurnsRemaining;
        public int coolingTurnsRemaining;
        public int coolingTurnsTotal;
    }

    private static readonly LavaVisualSelection NoVisual =
        new LavaVisualSelection(false, LavaOverlayVisualKind.None, 0f);

    private static readonly Vector2Int[] CardinalDirs =
    {
        new Vector2Int(0, 1),
        new Vector2Int(1, 0),
        new Vector2Int(0, -1),
        new Vector2Int(-1, 0),
    };

    private static readonly Vector2Int[] DiagonalDirs =
    {
        new Vector2Int(1, 1),
        new Vector2Int(1, -1),
        new Vector2Int(-1, -1),
        new Vector2Int(-1, 1),
    };

    private static readonly Vector2Int[] DirtyNeighborhood =
    {
        new Vector2Int(-1, -1),
        new Vector2Int( 0, -1),
        new Vector2Int( 1, -1),
        new Vector2Int(-1,  0),
        new Vector2Int( 0,  0),
        new Vector2Int( 1,  0),
        new Vector2Int(-1,  1),
        new Vector2Int( 0,  1),
        new Vector2Int( 1,  1),
    };

    [Header("References")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private WeatherGridManager weatherGridManager;
    [SerializeField] private WeatherFireSystem weatherFireSystem;
    [SerializeField] private MonoEnvironmentDataSource environmentDataSource;
    [SerializeField] private FloodSimulationSystem floodSimulationSystem;

    [Header("Overlay Root")]
    [SerializeField] private Transform lavaOverlayRoot;

    [Tooltip("Y height for lava visuals. Set this above environment/building base tiles.")]
    [SerializeField] private float lavaOverlayHeight = 0.18f;

    [Tooltip("Optional extra local scale multiplier for lava prefabs.")]
    [SerializeField] private Vector3 lavaPrefabScale = Vector3.one;

    [Header("Prefabs")]
    [SerializeField] private GameObject fillPrefab;
    [SerializeField] private GameObject straightPrefab;
    [SerializeField] private GameObject innerCornerPrefab;
    [SerializeField] private GameObject outerCornerPrefab;

    [Header("Expansion")]
    [Tooltip("If true, LavaOverlayManager subscribes to TurnSystem end turn and expands lava.")]
    public bool expandWithTurnSystem = true;

    [Tooltip("How many frontier cells to expand per lava source per turn.")]
    [Min(0)] public int frontierCellsPerSourcePerTurn = 1;

    [Tooltip("Maximum new lava cells added globally per turn. 0 = unlimited.")]
    [Min(0)] public int maxNewLavaCellsPerTurn = 24;

    [Tooltip("Maximum lava distance from source. 0 = unlimited.")]
    [Min(0)] public int maxDistanceFromSource = 0;

    [Tooltip("If true, lava can expand diagonally.")]
    public bool allowDiagonalExpansion = false;

    [Header("Volcano Driven Flow")]
    [Tooltip("Legacy mode. If true, lava keeps expanding every end turn from its global frontier. Usually keep this OFF now.")]
    [SerializeField] private bool legacyGlobalExpansionEachTurn = false;

    [Header("Lava Cooling")]
    [SerializeField] private bool processCoolingOnEndTurn = true;

    [Tooltip("Default delay before cooling if a cell was not refreshed by a volcano this turn.")]
    [SerializeField, Min(0)] private int defaultCoolingDelayTurns = 1;

    [Tooltip("Default turns to cool from hot to black before removal.")]
    [SerializeField, Min(1)] private int defaultCoolingTurns = 4;

    [SerializeField] private bool removeLavaAfterCooling = true;

    [SerializeField] private bool tintLavaAsItCools = true;
    [SerializeField] private Color hotLavaTint = new Color(1f, 0.35f, 0.05f, 1f);
    [SerializeField] private Color cooledLavaTint = Color.black;

    private MaterialPropertyBlock lavaMaterialBlock;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [Tooltip("Use WeatherFireSystem so lava fires can extinguish, damage, and spread normally.")]
    [SerializeField] private bool useWeatherFireSystemForLavaIgnition = true;

    [Tooltip("If true, active lava cells can keep attempting ignition each turn while cooling.")]
    [SerializeField] private bool lavaCanIgniteWhileCooling = true;

    [Tooltip("If true, cooler lava has lower ignition chance, but can still ignite.")]
    [SerializeField] private bool scaleIgnitionChanceByLavaHeat = true;

    [Tooltip("Ignition multiplier when lava is cold/black but still active. 0.15 = 15% of normal chance.")]
    [Range(0f, 1f)]
    [SerializeField] private float coldLavaIgnitionChanceMultiplier = 0.15f;

    [Tooltip("How many active lava cells can queue ignition attempts per turn. 0 = unlimited.")]
    [SerializeField, Min(0)] private int maxCoolingLavaIgnitionAttemptsPerTurn = 24;

    [Header("Flood Blocking")]
    [SerializeField] private bool floodBlocksLavaAdvance = true;

    [Tooltip("If true, source/volcano cells also cannot become lava while flooded.")]
    [SerializeField] private bool floodBlocksLavaSourceCells = true;

    [Tooltip("Minimum flood depth needed to block lava. Keep low so any visible flood blocks lava.")]
    [Range(0f, 1f)]
    [SerializeField] private float minFloodDepthToBlockLava = 0.01f;

    [Header("Over-Frame Visual Refresh")]
    public bool processVisualRefreshOverFrames = true;
    [Min(1)] public int visualRefreshesPerFrame = 16;

    [Header("Pool")]
    public bool prewarmOnStart = true;
    [Min(0)] public int prewarmEachPrefabCount = 16;

    [Header("Debug")]
    public bool debugLogging = false;

    private LavaOverlayPool pool;

    private readonly Dictionary<TileCoord, LavaCellState> lavaCells =
        new Dictionary<TileCoord, LavaCellState>();

    // Important:
    // Visuals now include BOTH lava cells and nearby non-lava border cells.
    private readonly Dictionary<TileCoord, LavaVisualRecord> activeVisuals =
        new Dictionary<TileCoord, LavaVisualRecord>();

    private readonly List<TileCoord> activeSources = new List<TileCoord>();
    private readonly Queue<TileCoord> frontierQueue = new Queue<TileCoord>();
    private readonly HashSet<TileCoord> queuedFrontierCells = new HashSet<TileCoord>();

    private readonly Queue<TileCoord> pendingVisualRefreshes = new Queue<TileCoord>();
    private readonly HashSet<TileCoord> pendingVisualRefreshSet = new HashSet<TileCoord>();

    private readonly List<TileCoord> neighbourScratch = new List<TileCoord>(8);
    private readonly List<TileCoord> lavaRefreshSnapshot = new List<TileCoord>(256);

    private readonly Queue<TileCoord> pendingLavaFireIgnitions = new Queue<TileCoord>();
    private readonly HashSet<TileCoord> pendingLavaFireIgnitionSet = new HashSet<TileCoord>();
    private Coroutine lavaFireIgnitionRoutine;

    [Header("Environment Blocking")]
    [SerializeField] private bool blockLavaByEnvironment = true;

    [Tooltip("If true, cells with no registered environment tile cannot become lava.")]
    [SerializeField] private bool blockCellsWithoutEnvironment = false;

    [Tooltip("Source volcano cells can ignore the block list so the erupting volcano can still start lava.")]
    [SerializeField] private bool allowSourceCellsToIgnoreEnvironmentBlocks = true;

    [Header("Lava Fire Ignition")]
    [SerializeField] private bool lavaIgnitesFireOnActivation = true;

    [SerializeField] private bool lavaIgnitesEnvironmentTiles = true;
    [SerializeField] private bool lavaIgnitesBuildings = true;

    [Range(0f, 1f)]
    [SerializeField] private float lavaFireIgnitionChance = 1f;

    [Min(1)]
    [SerializeField] private int environmentLavaBurnTurns = 6;

    [Min(1)]
    [SerializeField] private int buildingLavaBurnTurns = 6;

    [Tooltip("If true, lava fire ignition is processed over frames instead of all at once.")]
    [SerializeField] private bool processLavaFireIgnitionsOverFrames = true;

    [Min(1)]
    [SerializeField] private int lavaFireIgnitionsPerFrame = 4;

    [SerializeField] private bool tintOnlyLavaMaterialSlot = true;

    [Tooltip("Usually 0 = rock, 1 = lava. Set this to the material slot used by the lava material.")]
    [SerializeField, Min(0)] private int lavaMaterialSlotIndex = 1;

    [Tooltip("If true, searches renderer shared materials for a material name containing this text.")]
    [SerializeField] private bool findLavaMaterialSlotByName = true;

    [SerializeField] private string lavaMaterialNameContains = "lava";

    private readonly List<TileCoord> lavaIgnitionSnapshot = new List<TileCoord>(256);

    public event Action<TileCoord> OnBeforeLavaCellActivated;
    public event Action<TileCoord> OnLavaCellActivated;
    public event Action OnLavaCellsChanged;

    [SerializeField]
    private EnvironmentTileType[] blockedEnvironmentTileTypes =
    {
            EnvironmentTileType.Mountain,
            EnvironmentTileType.SaltLake
        };

            [SerializeField]
            private EnvironmentType[] blockedEnvironmentTypes =
            {
            EnvironmentType.Mountain,
            EnvironmentType.SaltLake,
            EnvironmentType.Volcano
        };

    private Coroutine visualRefreshRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        EnsureLinks();
        EnsureRootAndPool();
    }

    private void OnEnable()
    {
        if (expandWithTurnSystem)
            TurnSystem.SubscribeToEndOfTurn(HandleEndOfTurn);
    }

    private void Start()
    {
        EnsureLinks();
        EnsureRootAndPool();

        if (prewarmOnStart)
            Prewarm();
    }

    private void OnDisable()
    {
        TurnSystem.UnsubscribeFromEndOfTurn(HandleEndOfTurn);

        if (visualRefreshRoutine != null)
        {
            StopCoroutine(visualRefreshRoutine);
            visualRefreshRoutine = null;
        }

        pendingVisualRefreshes.Clear();
        pendingVisualRefreshSet.Clear();

        if (lavaFireIgnitionRoutine != null)
        {
            StopCoroutine(lavaFireIgnitionRoutine);
            lavaFireIgnitionRoutine = null;
        }

        pendingLavaFireIgnitions.Clear();
        pendingLavaFireIgnitionSet.Clear();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void EnsureLinks()
    {
        if (gridManager == null)
            gridManager = GridManager.Instance;

        if (weatherGridManager == null)
            weatherGridManager = WeatherGridManager.Instance;

        if (weatherFireSystem == null)
            weatherFireSystem = WeatherFireSystem.Instance;

        if (environmentDataSource == null)
            environmentDataSource = MonoEnvironmentDataSource.Instance;

        if (floodSimulationSystem == null)
            floodSimulationSystem = FindFirstObjectByType<FloodSimulationSystem>();
    }

    private void EnsureRootAndPool()
    {
        if (lavaOverlayRoot == null)
        {
            GameObject root = new GameObject("Lava Overlay Root");
            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            lavaOverlayRoot = root.transform;
        }

        if (pool == null)
            pool = new LavaOverlayPool(lavaOverlayRoot);
    }

    private void Prewarm()
    {
        EnsureRootAndPool();

        pool.Prewarm(fillPrefab, prewarmEachPrefabCount);
        pool.Prewarm(straightPrefab, prewarmEachPrefabCount);
        pool.Prewarm(innerCornerPrefab, prewarmEachPrefabCount);
        pool.Prewarm(outerCornerPrefab, prewarmEachPrefabCount);
    }

    private void HandleEndOfTurn()
    {
        if (legacyGlobalExpansionEachTurn)
            ExpandLavaOneTurn();

        if (processCoolingOnEndTurn)
            ProcessLavaCoolingOneTurn();

        if (lavaCanIgniteWhileCooling)
            QueueLavaFireIgnitionForActiveLavaCells();
    }

    public bool HasLavaAt(TileCoord coord)
    {
        return lavaCells.ContainsKey(coord);
    }

    public bool HasLavaAt(int x, int y)
    {
        return lavaCells.ContainsKey(new TileCoord(x, y));
    }

    public void SeedLavaCells(IReadOnlyList<TileCoord> cells)
    {
        if (cells == null || cells.Count == 0)
            return;

        for (int i = 0; i < cells.Count; i++)
        {
            AddLavaCell(
                cells[i],
                cells[i],
                0,
                ignoreEnvironmentBlock: allowSourceCellsToIgnoreEnvironmentBlocks);
        }

        RefreshAllTouchedCells(cells);

        if (debugLogging)
            //Debug.Log($"[LavaOverlayManager] Seeded lava cells={cells.Count}");
    }

    public void SeedLavaCell(TileCoord coord)
    {
        AddLavaCell(
            coord,
            coord,
            0,
            ignoreEnvironmentBlock: allowSourceCellsToIgnoreEnvironmentBlocks);

        MarkDirtyNeighborhood(coord);
    }

    public void ClearAllLava()
    {
        foreach (KeyValuePair<TileCoord, LavaVisualRecord> pair in activeVisuals)
        {
            LavaVisualRecord record = pair.Value;

            if (record.visual != null && record.prefab != null && pool != null)
                pool.Return(record.prefab, record.visual);
        }

        activeVisuals.Clear();

        lavaCells.Clear();
        activeSources.Clear();
        frontierQueue.Clear();
        queuedFrontierCells.Clear();

        pendingVisualRefreshes.Clear();
        pendingVisualRefreshSet.Clear();

        pendingLavaFireIgnitions.Clear();
        pendingLavaFireIgnitionSet.Clear();

        MarkLavaSaveDirty();

        if (debugLogging)
            //Debug.Log("[LavaOverlayManager] Cleared all lava.");
    }

    public void ExpandLavaOneTurn()
    {
        if (frontierCellsPerSourcePerTurn <= 0)
            return;

        EnsureLinks();

        if (gridManager == null)
            return;

        int newCellsThisTurn = 0;
        int maxNew = maxNewLavaCellsPerTurn <= 0 ? int.MaxValue : maxNewLavaCellsPerTurn;

        int sourceCount = Mathf.Max(1, activeSources.Count);
        int frontierToProcess = Mathf.Min(frontierQueue.Count, sourceCount * frontierCellsPerSourcePerTurn);

        for (int i = 0; i < frontierToProcess; i++)
        {
            if (frontierQueue.Count == 0)
                break;

            TileCoord current = frontierQueue.Dequeue();
            queuedFrontierCells.Remove(current);

            if (!lavaCells.TryGetValue(current, out LavaCellState currentState))
                continue;

            GetExpansionNeighbours(current, neighbourScratch);

            for (int n = 0; n < neighbourScratch.Count; n++)
            {
                if (newCellsThisTurn >= maxNew)
                    break;

                TileCoord next = neighbourScratch[n];

                if (lavaCells.ContainsKey(next))
                    continue;

                if (IsOutsideGrid(next.x, next.y))
                    continue;

                int nextDistance = currentState.distanceFromSource + 1;

                if (maxDistanceFromSource > 0 && nextDistance > maxDistanceFromSource)
                    continue;

                if (AddLavaCell(next, currentState.source, nextDistance))
                    newCellsThisTurn++;
            }

            if (newCellsThisTurn >= maxNew)
                break;
        }

        if (newCellsThisTurn > 0)
        {
            QueueFullVisualRefreshAroundLava();

            if (debugLogging)
                //Debug.Log($"[LavaOverlayManager] Expanded lava. NewCells={newCellsThisTurn}");
        }
    }

    private bool AddLavaCell(
    TileCoord coord,
    TileCoord source,
    int distance,
    bool ignoreEnvironmentBlock = false,
    float heat01 = 1f,
    int coolingDelayTurns = -1,
    int coolingTurns = -1)
    {
        if (IsOutsideGrid(coord.x, coord.y))
            return false;

        if (IsBlockedByFlood(coord, ignoreEnvironmentBlock))
            return false;

        if (!ignoreEnvironmentBlock && !CanLavaEnterEnvironmentCell(coord))
            return false;

        heat01 = Mathf.Clamp01(heat01);

        if (coolingDelayTurns < 0)
            coolingDelayTurns = defaultCoolingDelayTurns;

        if (coolingTurns <= 0)
            coolingTurns = defaultCoolingTurns;

        coolingDelayTurns = Mathf.Max(0, coolingDelayTurns);
        coolingTurns = Mathf.Max(1, coolingTurns);

        if (lavaCells.TryGetValue(coord, out LavaCellState existing))
        {
            existing.heat01 = Mathf.Max(existing.heat01, heat01);
            existing.coolingDelayTurnsRemaining = coolingDelayTurns;
            existing.coolingTurnsRemaining = coolingTurns;
            existing.coolingTurnsTotal = coolingTurns;

            lavaCells[coord] = existing;

            MarkDirtyNeighborhood(coord);
            MarkLavaSaveDirty();
            return false;
        }

        OnBeforeLavaCellActivated?.Invoke(coord);

        LavaCellState state = new LavaCellState
        {
            coord = coord,
            source = source,
            distanceFromSource = distance,
            heat01 = heat01,
            coolingDelayTurnsRemaining = coolingDelayTurns,
            coolingTurnsRemaining = coolingTurns,
            coolingTurnsTotal = coolingTurns
        };

        lavaCells.Add(coord, state);

        if (distance == 0)
            activeSources.Add(coord);

        EnqueueFrontier(coord);
        MarkDirtyNeighborhood(coord);
        QueueLavaFireIgnition(coord);

        OnLavaCellActivated?.Invoke(coord);
        OnLavaCellsChanged?.Invoke();

        return true;
    }

    private bool IsBlockedByFlood(TileCoord coord, bool isSourceOrIgnoredCell)
    {
        if (!floodBlocksLavaAdvance)
            return false;

        if (isSourceOrIgnoredCell && !floodBlocksLavaSourceCells)
            return false;

        EnsureLinks();

        if (floodSimulationSystem == null)
            return false;

        if (!floodSimulationSystem.TryGetFloodCell(coord, out FloodCellState floodState))
            return false;

        if (floodState == null)
            return false;

        bool blocked = floodState.floodDepth01 >= minFloodDepthToBlockLava;

        if (blocked && debugLogging)
        {
            //Debug.Log(
                //$"[LavaOverlayManager] Blocked lava at ({coord.x},{coord.y}) " +
                //$"because the cell is flooded. Depth={floodState.floodDepth01:0.00}");
        }

        return blocked;
    }

    public int EmitLavaFromSourceCells(
    IReadOnlyList<TileCoord> sourceCells,
    int maxNewCells,
    int maxDistanceFromSource,
    float heat01,
    int coolingDelayTurns,
    int coolingTurns,
    bool ignoreEnvironmentBlockForSourceCells)
    {
        if (sourceCells == null || sourceCells.Count == 0)
            return 0;

        EnsureLinks();

        heat01 = Mathf.Clamp01(heat01);
        maxNewCells = Mathf.Max(0, maxNewCells);
        maxDistanceFromSource = Mathf.Max(0, maxDistanceFromSource);

        if (coolingDelayTurns < 0)
            coolingDelayTurns = defaultCoolingDelayTurns;

        if (coolingTurns <= 0)
            coolingTurns = defaultCoolingTurns;

        coolingDelayTurns = Mathf.Max(0, coolingDelayTurns);
        coolingTurns = Mathf.Max(1, coolingTurns);

        // 1. Always seed/refresh the erupting volcano footprint.
        for (int i = 0; i < sourceCells.Count; i++)
        {
            AddLavaCell(
                sourceCells[i],
                sourceCells[i],
                0,
                ignoreEnvironmentBlock: ignoreEnvironmentBlockForSourceCells,
                heat01: heat01,
                coolingDelayTurns: coolingDelayTurns,
                coolingTurns: coolingTurns);
        }

        // 2. Reheat every existing lava cell that came from this volcano.
        // This is the key fix: older lava stays hot while the volcano keeps erupting.
        int reheated = ReheatExistingLavaForSources(
            sourceCells,
            heat01,
            coolingDelayTurns,
            coolingTurns);

        if (maxNewCells <= 0)
        {
            if (reheated > 0)
                OnLavaCellsChanged?.Invoke();

            return 0;
        }

        // 3. Build expansion frontier from all currently active lava belonging to this volcano.
        lavaRefreshSnapshot.Clear();

        foreach (KeyValuePair<TileCoord, LavaCellState> pair in lavaCells)
        {
            if (IsSourceCellInList(pair.Value.source, sourceCells))
                lavaRefreshSnapshot.Add(pair.Key);
        }

        lavaRefreshSnapshot.Sort((a, b) =>
        {
            LavaCellState stateA = lavaCells[a];
            LavaCellState stateB = lavaCells[b];
            return stateA.distanceFromSource.CompareTo(stateB.distanceFromSource);
        });

        int added = 0;

        // 4. Add only X new lava cells this eruption turn.
        for (int i = 0; i < lavaRefreshSnapshot.Count && added < maxNewCells; i++)
        {
            TileCoord current = lavaRefreshSnapshot[i];

            if (!lavaCells.TryGetValue(current, out LavaCellState currentState))
                continue;

            GetExpansionNeighbours(current, neighbourScratch);

            for (int n = 0; n < neighbourScratch.Count && added < maxNewCells; n++)
            {
                TileCoord next = neighbourScratch[n];

                if (lavaCells.ContainsKey(next))
                    continue;

                int nextDistance = currentState.distanceFromSource + 1;

                if (maxDistanceFromSource > 0 && nextDistance > maxDistanceFromSource)
                    continue;

                if (AddLavaCell(
                        next,
                        currentState.source,
                        nextDistance,
                        ignoreEnvironmentBlock: false,
                        heat01: heat01,
                        coolingDelayTurns: coolingDelayTurns,
                        coolingTurns: coolingTurns))
                {
                    added++;
                }
            }
        }

        lavaRefreshSnapshot.Clear();

        if (added > 0)
            QueueFullVisualRefreshAroundLava();

        if (added > 0 || reheated > 0)
        {
            OnLavaCellsChanged?.Invoke();
            MarkLavaSaveDirty();
        }

        if (debugLogging)
        {
            //Debug.Log(
                //$"[LavaOverlayManager] Volcano lava emission. " +
                //$"Added={added}/{maxNewCells} Reheated={reheated}");
        }

        return added;
    }

    private int ReheatExistingLavaForSources(
    IReadOnlyList<TileCoord> sourceCells,
    float heat01,
    int coolingDelayTurns,
    int coolingTurns)
    {
        if (sourceCells == null || sourceCells.Count == 0)
            return 0;

        lavaRefreshSnapshot.Clear();

        foreach (KeyValuePair<TileCoord, LavaCellState> pair in lavaCells)
        {
            if (IsSourceCellInList(pair.Value.source, sourceCells))
                lavaRefreshSnapshot.Add(pair.Key);
        }

        int reheated = 0;

        for (int i = 0; i < lavaRefreshSnapshot.Count; i++)
        {
            TileCoord coord = lavaRefreshSnapshot[i];

            if (!lavaCells.TryGetValue(coord, out LavaCellState state))
                continue;

            state.heat01 = Mathf.Max(state.heat01, heat01);
            state.coolingDelayTurnsRemaining = coolingDelayTurns;
            state.coolingTurnsRemaining = coolingTurns;
            state.coolingTurnsTotal = coolingTurns;

            lavaCells[coord] = state;

            MarkDirtyNeighborhood(coord);
            reheated++;
        }

        lavaRefreshSnapshot.Clear();

        return reheated;
    }

    private bool IsSourceCellInList(TileCoord source, IReadOnlyList<TileCoord> sourceCells)
    {
        if (sourceCells == null)
            return false;

        for (int i = 0; i < sourceCells.Count; i++)
        {
            TileCoord c = sourceCells[i];

            if (c.x == source.x && c.y == source.y)
                return true;
        }

        return false;
    }

    private void ProcessLavaCoolingOneTurn()
    {
        if (lavaCells.Count == 0)
            return;

        lavaRefreshSnapshot.Clear();

        foreach (KeyValuePair<TileCoord, LavaCellState> pair in lavaCells)
            lavaRefreshSnapshot.Add(pair.Key);

        bool changedAny = false;

        for (int i = 0; i < lavaRefreshSnapshot.Count; i++)
        {
            TileCoord coord = lavaRefreshSnapshot[i];

            if (!lavaCells.TryGetValue(coord, out LavaCellState state))
                continue;

            if (state.coolingDelayTurnsRemaining > 0)
            {
                state.coolingDelayTurnsRemaining--;
                lavaCells[coord] = state;
                continue;
            }

            state.coolingTurnsRemaining = Mathf.Max(0, state.coolingTurnsRemaining - 1);

            if (state.coolingTurnsTotal <= 0)
                state.coolingTurnsTotal = Mathf.Max(1, defaultCoolingTurns);

            state.heat01 = Mathf.Clamp01(
                state.coolingTurnsRemaining / (float)state.coolingTurnsTotal);

            if (removeLavaAfterCooling && state.coolingTurnsRemaining <= 0)
            {
                RemoveLavaCell(coord);
                changedAny = true;
                continue;
            }

            lavaCells[coord] = state;
            MarkDirtyNeighborhood(coord);
            changedAny = true;
        }

        lavaRefreshSnapshot.Clear();

        if (changedAny)
        {
            OnLavaCellsChanged?.Invoke();
            MarkLavaSaveDirty();
        }
    }

    private void RemoveLavaCell(TileCoord coord)
    {
        if (!lavaCells.ContainsKey(coord))
            return;

        lavaCells.Remove(coord);
        activeSources.Remove(coord);
        queuedFrontierCells.Remove(coord);

        MarkDirtyNeighborhood(coord);
    }

    public bool CopyActiveLavaCells(List<TileCoord> results)
    {
        if (results == null)
            return false;

        results.Clear();

        if (lavaCells.Count == 0)
            return false;

        foreach (KeyValuePair<TileCoord, LavaCellState> pair in lavaCells)
            results.Add(pair.Key);

        return results.Count > 0;
    }

    private void EnqueueFrontier(TileCoord coord)
    {
        if (queuedFrontierCells.Add(coord))
            frontierQueue.Enqueue(coord);
    }

    private void RefreshAllTouchedCells(IReadOnlyList<TileCoord> cells)
    {
        for (int i = 0; i < cells.Count; i++)
            MarkDirtyNeighborhood(cells[i]);
    }

    private void QueueFullVisualRefreshAroundLava()
    {
        lavaRefreshSnapshot.Clear();

        foreach (KeyValuePair<TileCoord, LavaCellState> pair in lavaCells)
            lavaRefreshSnapshot.Add(pair.Key);

        int count = lavaRefreshSnapshot.Count;

        for (int i = 0; i < count; i++)
            MarkDirtyNeighborhood(lavaRefreshSnapshot[i]);

        lavaRefreshSnapshot.Clear();
    }

    private void MarkDirtyNeighborhood(TileCoord center)
    {
        for (int i = 0; i < DirtyNeighborhood.Length; i++)
        {
            int x = center.x + DirtyNeighborhood[i].x;
            int y = center.y + DirtyNeighborhood[i].y;

            if (IsOutsideGrid(x, y))
                continue;

            QueueVisualRefresh(new TileCoord(x, y));
        }
    }

    private void QueueVisualRefresh(TileCoord coord)
    {
        if (IsOutsideGrid(coord.x, coord.y))
            return;

        if (!pendingVisualRefreshSet.Add(coord))
            return;

        pendingVisualRefreshes.Enqueue(coord);

        if (!processVisualRefreshOverFrames)
        {
            ProcessVisualRefreshImmediate();
            return;
        }

        if (visualRefreshRoutine == null && isActiveAndEnabled)
            visualRefreshRoutine = StartCoroutine(VisualRefreshRoutine());
    }

    private IEnumerator VisualRefreshRoutine()
    {
        while (pendingVisualRefreshes.Count > 0)
        {
            int processed = 0;
            int max = Mathf.Max(1, visualRefreshesPerFrame);

            while (pendingVisualRefreshes.Count > 0 && processed < max)
            {
                TileCoord coord = pendingVisualRefreshes.Dequeue();
                pendingVisualRefreshSet.Remove(coord);

                RefreshVisualAtCell(coord);
                processed++;
            }

            if (pendingVisualRefreshes.Count > 0)
                yield return null;
        }

        visualRefreshRoutine = null;
    }

    private void ProcessVisualRefreshImmediate()
    {
        while (pendingVisualRefreshes.Count > 0)
        {
            TileCoord coord = pendingVisualRefreshes.Dequeue();
            pendingVisualRefreshSet.Remove(coord);

            RefreshVisualAtCell(coord);
        }
    }

    private void ApplyCoolingTintToVisual(TileCoord coord, ref LavaVisualRecord record)
    {
        if (!tintLavaAsItCools)
            return;

        if (record.visual == null)
            return;

        if (record.renderers == null || record.renderers.Length == 0)
            record.renderers = record.visual.GetComponentsInChildren<Renderer>(true);

        float heat01 = GetVisualHeat01(coord);
        Color tint = Color.Lerp(cooledLavaTint, hotLavaTint, heat01);

        if (lavaMaterialBlock == null)
            lavaMaterialBlock = new MaterialPropertyBlock();

        for (int i = 0; i < record.renderers.Length; i++)
        {
            Renderer renderer = record.renderers[i];
            if (renderer == null)
                continue;

            ApplyCoolingTintToRenderer(renderer, tint, heat01);
        }
    }

    private void ApplyCoolingTintToRenderer(Renderer renderer, Color tint, float heat01)
    {
        if (renderer == null)
            return;

        if (!tintOnlyLavaMaterialSlot)
        {
            // Old behavior: tint whole renderer.
            renderer.GetPropertyBlock(lavaMaterialBlock);

            lavaMaterialBlock.SetColor(BaseColorId, tint);
            lavaMaterialBlock.SetColor(ColorId, tint);
            lavaMaterialBlock.SetColor(EmissionColorId, tint * heat01);

            renderer.SetPropertyBlock(lavaMaterialBlock);
            return;
        }

        int slot = ResolveLavaMaterialSlot(renderer);

        if (slot < 0)
            return;

        // Important:
        // Clear any old whole-renderer property block from the previous version,
        // otherwise the rock material may stay tinted.
        renderer.SetPropertyBlock(null);

        renderer.GetPropertyBlock(lavaMaterialBlock, slot);

        lavaMaterialBlock.SetColor(BaseColorId, tint);
        lavaMaterialBlock.SetColor(ColorId, tint);
        lavaMaterialBlock.SetColor(EmissionColorId, tint * heat01);

        // This overload applies the tint to only one material slot.
        renderer.SetPropertyBlock(lavaMaterialBlock, slot);
    }

    private int ResolveLavaMaterialSlot(Renderer renderer)
    {
        if (renderer == null)
            return -1;

        Material[] materials = renderer.sharedMaterials;

        if (materials == null || materials.Length == 0)
            return -1;

        if (findLavaMaterialSlotByName && !string.IsNullOrWhiteSpace(lavaMaterialNameContains))
        {
            string search = lavaMaterialNameContains.ToLowerInvariant();

            for (int i = 0; i < materials.Length; i++)
            {
                Material mat = materials[i];

                if (mat == null)
                    continue;

                string matName = mat.name.ToLowerInvariant();

                if (matName.Contains(search))
                    return i;
            }
        }

        if (lavaMaterialSlotIndex >= 0 && lavaMaterialSlotIndex < materials.Length)
            return lavaMaterialSlotIndex;

        return -1;
    }

    private float GetVisualHeat01(TileCoord coord)
    {
        if (lavaCells.TryGetValue(coord, out LavaCellState state))
            return Mathf.Clamp01(state.heat01);

        float best = 0f;

        for (int i = 0; i < DirtyNeighborhood.Length; i++)
        {
            TileCoord nearby = new TileCoord(
                coord.x + DirtyNeighborhood[i].x,
                coord.y + DirtyNeighborhood[i].y);

            if (lavaCells.TryGetValue(nearby, out LavaCellState nearbyState))
                best = Mathf.Max(best, nearbyState.heat01);
        }

        return Mathf.Clamp01(best);
    }

    private void RefreshVisualAtCell(TileCoord coord)
    {
        LavaVisualSelection selection = DetermineVisualForCell(coord);

        if (!selection.shouldRender)
        {
            ReleaseVisual(coord);
            return;
        }

        GameObject prefab = GetPrefab(selection.kind);

        if (prefab == null)
        {
            ReleaseVisual(coord);
            return;
        }

        Vector3 pos = GetWorldPositionForCell(coord);
        Quaternion rot = Quaternion.Euler(0f, selection.rotationY, 0f);

        bool hasRecord = activeVisuals.TryGetValue(coord, out LavaVisualRecord record);

        bool needsReplacement =
            !hasRecord ||
            record.visual == null ||
            record.prefab != prefab ||
            record.kind != selection.kind;

        if (needsReplacement)
        {
            if (hasRecord && record.visual != null && record.prefab != null && pool != null)
                pool.Return(record.prefab, record.visual);

            GameObject instance = pool.Get(prefab, pos, rot);

            if (instance == null)
                return;

            instance.name = $"Lava_{selection.kind}_{coord.x}_{coord.y}";
            instance.transform.localScale = lavaPrefabScale;

            record = new LavaVisualRecord
            {
                visual = instance,
                prefab = prefab,
                kind = selection.kind,
                rotationY = selection.rotationY,
                renderers = instance.GetComponentsInChildren<Renderer>(true)
            };
        }
        else
        {
            record.visual.transform.SetPositionAndRotation(pos, rot);
            record.visual.transform.localScale = lavaPrefabScale;
            record.visual.name = $"Lava_{selection.kind}_{coord.x}_{coord.y}";
            record.rotationY = selection.rotationY;
        }

        ApplyCoolingTintToVisual(coord, ref record);

        activeVisuals[coord] = record;
    }

    private void ReleaseVisual(TileCoord coord)
    {
        if (!activeVisuals.TryGetValue(coord, out LavaVisualRecord record))
            return;

        if (record.visual != null && record.prefab != null && pool != null)
            pool.Return(record.prefab, record.visual);

        activeVisuals.Remove(coord);
    }

    private LavaVisualSelection DetermineVisualForCell(TileCoord coord)
    {
        int x = coord.x;
        int y = coord.y;

        bool center = HasLavaAt(x, y);

        bool n = HasLavaAt(x, y + 1);
        bool e = HasLavaAt(x + 1, y);
        bool s = HasLavaAt(x, y - 1);
        bool w = HasLavaAt(x - 1, y);

        bool ne = HasLavaAt(x + 1, y + 1);
        bool se = HasLavaAt(x + 1, y - 1);
        bool sw = HasLavaAt(x - 1, y - 1);
        bool nw = HasLavaAt(x - 1, y + 1);

        // Actual lava cells are always fill.
        if (center)
            return DetermineVisualForActualLavaCell(coord);

        int cardinalCount = 0;
        if (n) cardinalCount++;
        if (e) cardinalCount++;
        if (s) cardinalCount++;
        if (w) cardinalCount++;

        int diagonalCount = 0;
        if (ne) diagonalCount++;
        if (se) diagonalCount++;
        if (sw) diagonalCount++;
        if (nw) diagonalCount++;

        if (cardinalCount == 0 && diagonalCount == 0)
            return NoVisual;

        // Important:
        // If this cell is blocked from becoming lava, but it would receive
        // a border/edge/corner visual, render it as Fill instead.
        // This is visual only. It does NOT add the cell to lavaCells.
        if (IsBlockedEnvironmentCellForLavaVisual(coord))
            return NoVisual;

        // INNER CORNERS
        if (s && w && !n && !e)
            return new LavaVisualSelection(true, LavaOverlayVisualKind.InnerCorner, 90f);

        if (n && w && !s && !e)
            return new LavaVisualSelection(true, LavaOverlayVisualKind.InnerCorner, 180f);

        if (n && e && !s && !w)
            return new LavaVisualSelection(true, LavaOverlayVisualKind.InnerCorner, -90f);

        if (s && e && !n && !w)
            return new LavaVisualSelection(true, LavaOverlayVisualKind.InnerCorner, 0f);

        // OUTER CORNERS
        if (cardinalCount == 0)
        {
            if (sw && !se && !nw && !ne)
                return new LavaVisualSelection(true, LavaOverlayVisualKind.OuterCorner, 180f);

            if (nw && !sw && !ne && !se)
                return new LavaVisualSelection(true, LavaOverlayVisualKind.OuterCorner, -90f);

            if (ne && !nw && !se && !sw)
                return new LavaVisualSelection(true, LavaOverlayVisualKind.OuterCorner, 0f);

            if (se && !ne && !sw && !nw)
                return new LavaVisualSelection(true, LavaOverlayVisualKind.OuterCorner, 90f);
        }

        // STRAIGHTS
        if (s && !n && !e && !w)
            return new LavaVisualSelection(true, LavaOverlayVisualKind.Straight, 90f);

        if (w && !n && !e && !s)
            return new LavaVisualSelection(true, LavaOverlayVisualKind.Straight, 180f);

        if (n && !e && !s && !w)
            return new LavaVisualSelection(true, LavaOverlayVisualKind.Straight, -90f);

        if (e && !n && !s && !w)
            return new LavaVisualSelection(true, LavaOverlayVisualKind.Straight, 0f);

        if (s && !n)
            return new LavaVisualSelection(true, LavaOverlayVisualKind.Straight, 0f);

        if (w && !e)
            return new LavaVisualSelection(true, LavaOverlayVisualKind.Straight, 90f);

        if (n && !s)
            return new LavaVisualSelection(true, LavaOverlayVisualKind.Straight, 180f);

        if (e && !w)
            return new LavaVisualSelection(true, LavaOverlayVisualKind.Straight, 270f);

        if (s) return new LavaVisualSelection(true, LavaOverlayVisualKind.Straight, 0f);
        if (w) return new LavaVisualSelection(true, LavaOverlayVisualKind.Straight, 90f);
        if (n) return new LavaVisualSelection(true, LavaOverlayVisualKind.Straight, 180f);
        if (e) return new LavaVisualSelection(true, LavaOverlayVisualKind.Straight, 270f);

        return NoVisual;
    }

    private GameObject GetPrefab(LavaOverlayVisualKind kind)
    {
        switch (kind)
        {
            case LavaOverlayVisualKind.Fill:
                return fillPrefab;

            case LavaOverlayVisualKind.Straight:
                return straightPrefab != null ? straightPrefab : fillPrefab;

            case LavaOverlayVisualKind.InnerCorner:
                return innerCornerPrefab != null ? innerCornerPrefab : fillPrefab;

            case LavaOverlayVisualKind.OuterCorner:
                return outerCornerPrefab != null ? outerCornerPrefab : fillPrefab;

            default:
                return null;
        }
    }

    private LavaVisualSelection DetermineVisualForActualLavaCell(TileCoord coord)
    {
        int x = coord.x;
        int y = coord.y;

        bool blockedN = IsBlockedEnvironmentCellForLavaVisual(new TileCoord(x, y + 1));
        bool blockedE = IsBlockedEnvironmentCellForLavaVisual(new TileCoord(x + 1, y));
        bool blockedS = IsBlockedEnvironmentCellForLavaVisual(new TileCoord(x, y - 1));
        bool blockedW = IsBlockedEnvironmentCellForLavaVisual(new TileCoord(x - 1, y));

        int blockedCount = 0;
        if (blockedN) blockedCount++;
        if (blockedE) blockedCount++;
        if (blockedS) blockedCount++;
        if (blockedW) blockedCount++;

        // No blocked neighbour: normal lava interior.
        if (blockedCount == 0)
            return new LavaVisualSelection(true, LavaOverlayVisualKind.Fill, 0f);

        // Two adjacent blocked sides means the lava cell is touching a blocked corner.
        if (blockedN && blockedE)
            return new LavaVisualSelection(true, LavaOverlayVisualKind.OuterCorner, 180f);

        if (blockedE && blockedS)
            return new LavaVisualSelection(true, LavaOverlayVisualKind.OuterCorner, -90f);

        if (blockedS && blockedW)
            return new LavaVisualSelection(true, LavaOverlayVisualKind.OuterCorner, 0f);

        if (blockedW && blockedN)
            return new LavaVisualSelection(true, LavaOverlayVisualKind.OuterCorner, 90f);

        // Opposite blocked sides or one blocked side use straight.
        if (blockedN)
            return new LavaVisualSelection(true, LavaOverlayVisualKind.Straight, 90f);

        if (blockedE)
            return new LavaVisualSelection(true, LavaOverlayVisualKind.Straight, 180f);

        if (blockedS)
            return new LavaVisualSelection(true, LavaOverlayVisualKind.Straight, -90f);

        if (blockedW)
            return new LavaVisualSelection(true, LavaOverlayVisualKind.Straight, 0f);

        return new LavaVisualSelection(true, LavaOverlayVisualKind.Fill, 0f);
    }

    private Vector3 GetWorldPositionForCell(TileCoord coord)
    {
        EnsureLinks();

        if (gridManager == null)
            return new Vector3(coord.x, lavaOverlayHeight, coord.y);

        Vector3 corner = gridManager.GetWorldPosition(coord.x, coord.y);
        float size = gridManager.cellSize;

        return new Vector3(
            corner.x + size * 0.5f,
            lavaOverlayHeight,
            corner.z + size * 0.5f);
    }

    private void GetExpansionNeighbours(TileCoord coord, List<TileCoord> results)
    {
        results.Clear();

        for (int i = 0; i < CardinalDirs.Length; i++)
        {
            int x = coord.x + CardinalDirs[i].x;
            int y = coord.y + CardinalDirs[i].y;

            if (!IsOutsideGrid(x, y))
                results.Add(new TileCoord(x, y));
        }

        if (!allowDiagonalExpansion)
            return;

        for (int i = 0; i < DiagonalDirs.Length; i++)
        {
            int x = coord.x + DiagonalDirs[i].x;
            int y = coord.y + DiagonalDirs[i].y;

            if (!IsOutsideGrid(x, y))
                results.Add(new TileCoord(x, y));
        }
    }

    private bool IsOutsideGrid(int x, int y)
    {
        EnsureLinks();

        if (gridManager == null)
            return true;

        return x < 0 ||
               y < 0 ||
               x >= gridManager.columns ||
               y >= gridManager.rows;
    }

    private bool CanLavaEnterEnvironmentCell(TileCoord coord)
    {
        if (!blockLavaByEnvironment)
            return true;

        EnsureLinks();

        if (environmentDataSource == null)
            return true;

        bool hasEnvironment = environmentDataSource.HasLiveEnvironmentTile(coord);

        if (!hasEnvironment)
            return !blockCellsWithoutEnvironment;

        TileEnvironmentData data = environmentDataSource.GetTileData(coord);

        if (IsBlockedEnvironmentType(data.environmentType))
        {
            if (debugLogging)
            {
                //Debug.Log(
                    //$"[LavaOverlayManager] Blocked lava at ({coord.x},{coord.y}) " +
                    //$"because environmentType={data.environmentType}");
            }

            return false;
        }

        if (IsBlockedEnvironmentTileType(data.tileType))
        {
            if (debugLogging)
            {
                //Debug.Log(
                    //$"[LavaOverlayManager] Blocked lava at ({coord.x},{coord.y}) " +
                    //$"because tileType={data.tileType}");
            }

            return false;
        }

        return true;
    }

    private bool IsBlockedEnvironmentType(EnvironmentType type)
    {
        if (blockedEnvironmentTypes == null)
            return false;

        for (int i = 0; i < blockedEnvironmentTypes.Length; i++)
        {
            if (blockedEnvironmentTypes[i] == type)
                return true;
        }

        return false;
    }

    private bool IsBlockedEnvironmentTileType(EnvironmentTileType type)
    {
        if (blockedEnvironmentTileTypes == null)
            return false;

        for (int i = 0; i < blockedEnvironmentTileTypes.Length; i++)
        {
            if (blockedEnvironmentTileTypes[i] == type)
                return true;
        }

        return false;
    }

    private void QueueLavaFireIgnition(TileCoord coord)
    {
        if (!lavaIgnitesFireOnActivation)
            return;

        if (IsOutsideGrid(coord.x, coord.y))
            return;

        if (!pendingLavaFireIgnitionSet.Add(coord))
            return;

        pendingLavaFireIgnitions.Enqueue(coord);

        if (!processLavaFireIgnitionsOverFrames)
        {
            ProcessLavaFireIgnitionsImmediate();
            return;
        }

        if (lavaFireIgnitionRoutine == null && isActiveAndEnabled)
            lavaFireIgnitionRoutine = StartCoroutine(LavaFireIgnitionRoutine());
    }

    private IEnumerator LavaFireIgnitionRoutine()
    {
        while (pendingLavaFireIgnitions.Count > 0)
        {
            int processed = 0;
            int max = Mathf.Max(1, lavaFireIgnitionsPerFrame);

            while (pendingLavaFireIgnitions.Count > 0 && processed < max)
            {
                TileCoord coord = pendingLavaFireIgnitions.Dequeue();
                pendingLavaFireIgnitionSet.Remove(coord);

                IgniteFireAtLavaCell(coord);
                processed++;
            }

            if (pendingLavaFireIgnitions.Count > 0)
                yield return null;
        }

        lavaFireIgnitionRoutine = null;
    }

    private void ProcessLavaFireIgnitionsImmediate()
    {
        while (pendingLavaFireIgnitions.Count > 0)
        {
            TileCoord coord = pendingLavaFireIgnitions.Dequeue();
            pendingLavaFireIgnitionSet.Remove(coord);

            IgniteFireAtLavaCell(coord);
        }
    }

    private void IgniteFireAtLavaCell(TileCoord coord)
    {
        if (!lavaIgnitesFireOnActivation)
            return;

        EnsureLinks();

        if (IsOutsideGrid(coord.x, coord.y))
            return;

        float lavaHeat01 = GetLavaHeat01AtCell(coord);

        float finalChance = lavaFireIgnitionChance;

        if (scaleIgnitionChanceByLavaHeat)
        {
            float heatMultiplier = Mathf.Lerp(
                coldLavaIgnitionChanceMultiplier,
                1f,
                Mathf.Clamp01(lavaHeat01));

            finalChance *= heatMultiplier;
        }

        finalChance = Mathf.Clamp01(finalChance);

        if (finalChance <= 0f)
            return;

        bool ignitedAny = false;

        if (useWeatherFireSystemForLavaIgnition && weatherFireSystem != null)
        {
            // Important:
            // This path registers the fire with WeatherFireSystem.
            // That means it can extinguish, damage, and spread normally.
            ignitedAny = weatherFireSystem.TryIgniteFireAtCell(
                coord.x,
                coord.y,
                finalChance,
                sourceDryness01: 1f,
                sourceHeat01: lavaHeat01,
                ignitionEvent: false);
        }
        else
        {
            // Fallback only. This can visually ignite, but it will NOT be tracked by WeatherFireSystem.
            if (lavaIgnitesEnvironmentTiles)
                ignitedAny |= TryIgniteEnvironmentFireAtCell(coord);

            if (lavaIgnitesBuildings)
                ignitedAny |= TryIgniteBuildingFireAtCell(coord);
        }

        if (debugLogging && ignitedAny)
        {
            //Debug.Log(
                //$"[LavaOverlayManager] Lava ignited fire at cell ({coord.x},{coord.y}) " +
                //$"heat={lavaHeat01:0.00} chance={finalChance:0.00} registeredByWeatherFireSystem={useWeatherFireSystemForLavaIgnition}");
        }
    }

    private void QueueLavaFireIgnitionForActiveLavaCells()
    {
        if (!lavaIgnitesFireOnActivation)
            return;

        if (!lavaCanIgniteWhileCooling)
            return;

        if (lavaCells.Count == 0)
            return;

        lavaIgnitionSnapshot.Clear();

        foreach (KeyValuePair<TileCoord, LavaCellState> pair in lavaCells)
            lavaIgnitionSnapshot.Add(pair.Key);

        int maxAttempts = maxCoolingLavaIgnitionAttemptsPerTurn <= 0
            ? int.MaxValue
            : maxCoolingLavaIgnitionAttemptsPerTurn;

        int queued = 0;

        for (int i = 0; i < lavaIgnitionSnapshot.Count && queued < maxAttempts; i++)
        {
            TileCoord coord = lavaIgnitionSnapshot[i];

            if (!lavaCells.ContainsKey(coord))
                continue;

            QueueLavaFireIgnition(coord);
            queued++;
        }

        lavaIgnitionSnapshot.Clear();
    }

    public float GetLavaHeat01AtCell(TileCoord coord)
    {
        if (lavaCells.TryGetValue(coord, out LavaCellState state))
            return Mathf.Clamp01(state.heat01);

        return 0f;
    }

    private bool TryIgniteEnvironmentFireAtCell(TileCoord coord)
    {
        EnvironmentControl environment = null;

        if (weatherGridManager != null)
            weatherGridManager.TryGetEnvironmentAtCell(coord.x, coord.y, out environment);

        if (environment == null)
            return false;

        EnvironmentFireState fireState = environment.GetComponent<EnvironmentFireState>();

        if (fireState == null)
            fireState = environment.GetComponentInChildren<EnvironmentFireState>(true);

        if (fireState == null)
            fireState = environment.GetComponentInParent<EnvironmentFireState>(true);

        if (fireState == null)
            return false;

        return fireState.TryIgnite(
            lavaFireIgnitionChance,
            environmentLavaBurnTurns);
    }

    private bool TryIgniteBuildingFireAtCell(TileCoord coord)
    {
        if (weatherGridManager == null)
            return false;

        if (!weatherGridManager.TryGetBuildingAtCell(
                coord.x,
                coord.y,
                out WorldBuildingManager.Record record) ||
            record == null ||
            record.instance == null)
        {
            return false;
        }

        BuildingFireState fireState = record.instance.GetComponent<BuildingFireState>();

        if (fireState == null)
            fireState = record.instance.GetComponentInChildren<BuildingFireState>(true);

        if (fireState == null)
            fireState = record.instance.GetComponentInParent<BuildingFireState>(true);

        if (fireState == null)
            return false;

        return fireState.TryIgnite(
            lavaFireIgnitionChance,
            buildingLavaBurnTurns);
    }

    private bool IsBlockedEnvironmentCellForLavaVisual(TileCoord coord)
    {
        if (HasLavaAt(coord))
            return false;

        if (!blockLavaByEnvironment)
            return false;

        EnsureLinks();

        if (environmentDataSource == null)
            return false;

        bool hasEnvironment = environmentDataSource.HasLiveEnvironmentTile(coord);

        if (!hasEnvironment)
            return blockCellsWithoutEnvironment;

        TileEnvironmentData data = environmentDataSource.GetTileData(coord);

        if (IsBlockedEnvironmentType(data.environmentType))
            return true;

        if (IsBlockedEnvironmentTileType(data.tileType))
            return true;

        return false;
    }

    public LavaOverlaySaveData SaveState()
    {
        LavaOverlaySaveData data = new LavaOverlaySaveData();

        foreach (KeyValuePair<TileCoord, LavaCellState> pair in lavaCells)
        {
            LavaCellState state = pair.Value;

            data.lavaCells.Add(new LavaCellSaveData
            {
                x = state.coord.x,
                y = state.coord.y,

                sourceX = state.source.x,
                sourceY = state.source.y,

                distanceFromSource = state.distanceFromSource,

                heat01 = Mathf.Clamp01(state.heat01),

                coolingDelayTurnsRemaining = Mathf.Max(0, state.coolingDelayTurnsRemaining),
                coolingTurnsRemaining = Mathf.Max(0, state.coolingTurnsRemaining),
                coolingTurnsTotal = Mathf.Max(1, state.coolingTurnsTotal)
            });
        }

        return data;
    }

    public void LoadState(LavaOverlaySaveData data)
    {
        EnsureLinks();
        EnsureRootAndPool();

        StopLavaLoadSensitiveRoutines();

        ClearAllLava();

        if (data == null || data.lavaCells == null || data.lavaCells.Count == 0)
        {
            OnLavaCellsChanged?.Invoke();
            return;
        }

        int restored = 0;

        for (int i = 0; i < data.lavaCells.Count; i++)
        {
            LavaCellSaveData saved = data.lavaCells[i];

            TileCoord coord = new TileCoord(saved.x, saved.y);

            if (IsOutsideGrid(coord.x, coord.y))
                continue;

            TileCoord source = new TileCoord(saved.sourceX, saved.sourceY);

            LavaCellState state = new LavaCellState
            {
                coord = coord,
                source = source,
                distanceFromSource = Mathf.Max(0, saved.distanceFromSource),

                heat01 = Mathf.Clamp01(saved.heat01),

                coolingDelayTurnsRemaining = Mathf.Max(0, saved.coolingDelayTurnsRemaining),
                coolingTurnsRemaining = Mathf.Max(0, saved.coolingTurnsRemaining),
                coolingTurnsTotal = Mathf.Max(1, saved.coolingTurnsTotal)
            };

            lavaCells[coord] = state;

            if (state.distanceFromSource == 0 || SameCoord(coord, source))
                AddActiveSourceIfMissing(coord);

            EnqueueFrontier(coord);
            restored++;
        }

        QueueFullVisualRefreshAroundLava();

        OnLavaCellsChanged?.Invoke();

        if (debugLogging)
            //Debug.Log($"[LavaOverlayManager] Loaded lava overlay. Cells={restored}");
    }

    private void StopLavaLoadSensitiveRoutines()
    {
        if (visualRefreshRoutine != null)
        {
            StopCoroutine(visualRefreshRoutine);
            visualRefreshRoutine = null;
        }

        pendingVisualRefreshes.Clear();
        pendingVisualRefreshSet.Clear();

        if (lavaFireIgnitionRoutine != null)
        {
            StopCoroutine(lavaFireIgnitionRoutine);
            lavaFireIgnitionRoutine = null;
        }

        pendingLavaFireIgnitions.Clear();
        pendingLavaFireIgnitionSet.Clear();
    }

    private bool SameCoord(TileCoord a, TileCoord b)
    {
        return a.x == b.x && a.y == b.y;
    }

    private void AddActiveSourceIfMissing(TileCoord coord)
    {
        for (int i = 0; i < activeSources.Count; i++)
        {
            if (SameCoord(activeSources[i], coord))
                return;
        }

        activeSources.Add(coord);
    }

    private void MarkLavaSaveDirty()
    {
        SaveSystem.MarkSectionDirty(SaveSectionKeys.WorldSim);
    }
}
