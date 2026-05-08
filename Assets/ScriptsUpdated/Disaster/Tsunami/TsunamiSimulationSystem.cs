using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TsunamiSimulationSystem : MonoBehaviour
{
    public static TsunamiSimulationSystem Instance { get; private set; }

    [Header("References")]
    public GridManager gridManager;
    public MapGenerator mapGenerator;
    public MonoEnvironmentDataSource environmentDataSource;

    [Header("Turn System")]
    public bool rollTsunamiOnEndOfTurn = true;
    public bool advanceTsunamisOnEndOfTurn = true;
    public bool preventMultipleRollsPerTurn = true;

    [Header("Tsunami Chance")]
    public bool canHaveTsunamis = true;

    [Range(0f, 1f)]
    public float tsunamiChancePerTurn = 0.005f;

    [Header("Energy")]
    public float minStartEnergy = 8f;
    public float maxStartEnergy = 18f;

    [Min(0f)]
    public float energyLossPerStep = 1f;

    [Tooltip("Land/coast energy loss multiplier. 2 = land costs twice as much energy as sea.")]
    [Min(0f)]
    public float landEnergyLossMultiplier = 2f;

    [Tooltip("Optional extra loss based on active wave size. Useful for stopping massive waves sooner.")]
    [Min(0f)]
    public float extraEnergyLossPerCellCount = 0.01f;

    [Header("Movement")]
    [Min(1)]
    public int cellsAdvancedPerStep = 1;

    [Min(1)]
    public int maxStepsPerTsunami = 30;

    [Min(1)]
    public int waveWidthCells = 5;

    [Range(0f, 1f)]
    public float sideSpreadChance = 0.35f;

    public bool allowDiagonalDirections = false;

    [Tooltip("If false, lake cells block tsunami movement.")]
    public bool allowTsunamiIntoLakes = false;

    [Header("Environment Wave Blocking")]
    [Tooltip("If true, mountains, volcanoes, and any listed blocked environment types stop tsunami wave movement.")]
    public bool blockTsunamiByEnvironment = true;

    [Tooltip("If true, cells with no registered environment tile block tsunami movement. Usually keep false.")]
    public bool blockCellsWithoutEnvironment = false;

    [SerializeField]
    private EnvironmentTileType[] blockedTsunamiTileTypes =
    {
    EnvironmentTileType.Mountain
    };

        [SerializeField]
        private EnvironmentType[] blockedTsunamiEnvironmentTypes =
        {
        EnvironmentType.Mountain,
        EnvironmentType.Volcano
    };

    [Header("Processing")]
    public bool processStepsOverFrames = true;

    [Min(1)]
    public int waveCellsProcessedPerFrame = 64;

    [Header("Testing")]
    public bool forceWithKey = true;
    public KeyCode forceKey = KeyCode.T;
    public TsunamiDirection forcedDirection = TsunamiDirection.East;

    [Tooltip("If true, natural tsunamis pick a random direction. Forced tsunamis use Forced Direction.")]
    public bool naturalTsunamisUseRandomDirection = true;

    [Header("Grid Edge Source")]
    [Tooltip("If true, tsunamis always start from an outer grid edge and move inward away from that edge.")]
    public bool alwaysMoveAwayFromGridEdge = true;

    [Tooltip("Used for forced tsunamis. The wave will spawn from this edge and move inward.")]
    public TsunamiGridEdge forcedSpawnEdge = TsunamiGridEdge.West;

    [Tooltip("If true, natural tsunamis pick a random outer grid edge.")]
    public bool naturalTsunamisUseRandomGridEdge = true;

    [Header("Flood Bridge")]
    public TsunamiFloodBridge tsunamiFloodBridge;

    [Header("Debug")]
    public bool debugLogging = true;
    public bool drawGizmos = true;
    public Color sourceGizmoColor = new Color(0f, 0.75f, 1f, 0.75f);
    public Color activeGizmoColor = new Color(0f, 0.25f, 1f, 0.55f);
    public Color visitedGizmoColor = new Color(0f, 0.1f, 0.7f, 0.18f);

    public event Action<TsunamiStartedEventData> OnTsunamiStarted;
    public event Action<TsunamiCellsChangedEventData> OnTsunamiCellsChanged;
    public event Action<TsunamiAdvancedEventData> OnTsunamiAdvanced;
    public event Action<TsunamiEndedEventData> OnTsunamiEnded;

    private readonly Dictionary<int, TsunamiWaveState> activeTsunamis =
        new Dictionary<int, TsunamiWaveState>();

    private readonly List<TsunamiGridEdge> validOceanEdgesScratch = new List<TsunamiGridEdge>();

    private readonly List<int> activeIdScratch = new List<int>();
    private readonly List<TileCoord> sourceScratch = new List<TileCoord>();
    private readonly List<TileCoord> seaEdgeScratch = new List<TileCoord>();
    private readonly List<TileCoord> oldCellsScratch = new List<TileCoord>();
    private readonly List<TileCoord> newCellsScratch = new List<TileCoord>();
    private readonly HashSet<TileCoord> newCellSetScratch = new HashSet<TileCoord>();
    private readonly List<TileCoord> addedScratch = new List<TileCoord>();
    private readonly List<TileCoord> removedScratch = new List<TileCoord>();

    private Coroutine advanceRoutine;
    private int nextTsunamiId = 1;
    private int lastRollTurn = int.MinValue;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolveReferences();
    }

    private void OnEnable()
    {
        if (rollTsunamiOnEndOfTurn || advanceTsunamisOnEndOfTurn)
            TurnSystem.SubscribeToEndOfTurn(HandleEndOfTurn);
    }

    private void OnDisable()
    {
        TurnSystem.UnsubscribeFromEndOfTurn(HandleEndOfTurn);

        if (advanceRoutine != null)
        {
            StopCoroutine(advanceRoutine);
            advanceRoutine = null;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        ResolveReferences();
    }

    private void Update()
    {
        if (forceWithKey && Input.GetKeyDown(forceKey))
            ForceTsunami();
    }

    private void HandleEndOfTurn()
    {
        if (advanceTsunamisOnEndOfTurn)
            AdvanceAllTsunamisOneTurn();

        if (rollTsunamiOnEndOfTurn)
            RollForTsunami();
    }

    public void RollForTsunami()
    {
        if (!canHaveTsunamis)
            return;

        ResolveReferences();

        if (!IsMapReady())
        {
            if (debugLogging) {}
                //Debug.LogWarning("[TsunamiSimulationSystem] Map is not ready. Roll skipped.");

            return;
        }

        int currentTurn = TurnSystem.GetCurrentTurn();

        if (preventMultipleRollsPerTurn && lastRollTurn == currentTurn)
            return;

        lastRollTurn = currentTurn;

        MarkTsunamiSaveDirty();

        float roll = UnityEngine.Random.value;

        if (debugLogging)
        {
            //Debug.Log(
                //$"[TsunamiSimulationSystem] Turn roll. " +
                //$"Turn={currentTurn} Chance={tsunamiChancePerTurn:0.000} Roll={roll:0.000}");
        }

        if (roll > tsunamiChancePerTurn)
            return;

        TriggerTsunami(false);
    }

    [ContextMenu("Force Tsunami")]
    public void ForceTsunami()
    {
        ResolveReferences();
        TriggerTsunami(true);
    }

    public bool ForceTsunamiAtCell(Vector2Int sourceCell, TsunamiDirection directionKind, float energy)
    {
        ResolveReferences();

        sourceScratch.Clear();

        TileCoord coord = new TileCoord(sourceCell.x, sourceCell.y);

        if (!IsCellInsideGrid(coord))
            return false;

        if (!IsSeaCell(coord))
        {
            if (debugLogging) {}
                //Debug.LogWarning($"[TsunamiSimulationSystem] ForceTsunamiAtCell failed. Source is not sea: {coord}");

            return false;
        }

        BuildWaveBandAroundSource(coord, DirectionToVector(directionKind), waveWidthCells, sourceScratch);

        if (sourceScratch.Count == 0)
            sourceScratch.Add(coord);

        StartTsunamiFromCells(sourceScratch, directionKind, Mathf.Max(0.01f, energy), true);
        return true;
    }

    public bool StartTsunamiFromCells(
        IReadOnlyList<TileCoord> sourceCells,
        TsunamiDirection directionKind,
        float energy,
        bool forced)
    {
        ResolveReferences();

        if (!IsMapReady())
        {
            if (debugLogging) {}
                //Debug.LogWarning("[TsunamiSimulationSystem] Cannot start tsunami. Map is not ready.");

            return false;
        }

        if (sourceCells == null || sourceCells.Count == 0)
            return false;

        Vector2Int direction = DirectionToVector(directionKind);

        if (direction == Vector2Int.zero)
            return false;

        sourceScratch.Clear();

        for (int i = 0; i < sourceCells.Count; i++)
        {
            TileCoord c = sourceCells[i];

            if (!IsCellInsideGrid(c))
                continue;

            if (!IsSeaCell(c))
                continue;

            if (!sourceScratch.Contains(c))
                sourceScratch.Add(c);
        }

        if (sourceScratch.Count == 0)
            return false;

        StartTsunamiFromCells(sourceScratch, directionKind, Mathf.Max(0.01f, energy), forced);
        return true;
    }

    public void AdvanceAllTsunamisOneTurn()
    {
        if (activeTsunamis.Count == 0)
            return;

        if (processStepsOverFrames)
        {
            if (advanceRoutine == null && isActiveAndEnabled)
                advanceRoutine = StartCoroutine(AdvanceAllTsunamisRoutine());

            return;
        }

        AdvanceAllTsunamisImmediate();
    }

    public bool HasActiveTsunamiAt(TileCoord coord)
    {
        foreach (KeyValuePair<int, TsunamiWaveState> pair in activeTsunamis)
        {
            if (pair.Value.currentCells.Contains(coord))
                return true;
        }

        return false;
    }

    public bool CopyActiveTsunamiCells(List<TileCoord> results)
    {
        if (results == null)
            return false;

        results.Clear();

        foreach (KeyValuePair<int, TsunamiWaveState> pair in activeTsunamis)
        {
            foreach (TileCoord coord in pair.Value.currentCells)
            {
                if (!results.Contains(coord))
                    results.Add(coord);
            }
        }

        return results.Count > 0;
    }

    [ContextMenu("Clear All Tsunamis")]
    public void ClearAllTsunamis()
    {
        activeIdScratch.Clear();

        foreach (KeyValuePair<int, TsunamiWaveState> pair in activeTsunamis)
            activeIdScratch.Add(pair.Key);

        for (int i = 0; i < activeIdScratch.Count; i++)
        {
            if (activeTsunamis.TryGetValue(activeIdScratch[i], out TsunamiWaveState wave))
                EndTsunami(wave, TsunamiEndReason.ManuallyCleared);
        }

        activeIdScratch.Clear();
    }

    private void TriggerTsunami(bool forced)
    {
        if (!canHaveTsunamis && !forced)
            return;

        if (!IsMapReady())
        {
            if (debugLogging) {}
                //Debug.LogWarning("[TsunamiSimulationSystem] Cannot trigger tsunami. Map is not ready.");

            return;
        }

        TsunamiDirection directionKind;
        TsunamiGridEdge spawnEdge;

        if (alwaysMoveAwayFromGridEdge)
        {
            if (!TryPickOceanGridEdge(forced, out spawnEdge))
            {
                if (debugLogging) {}
                    //Debug.LogWarning("[TsunamiSimulationSystem] No valid ocean edge found. Tsunami skipped.");

                return;
            }

            directionKind = GetInwardDirectionFromEdge(spawnEdge);
        }
        else
        {
            directionKind = forced
                ? forcedDirection
                : PickNaturalDirection();

            spawnEdge = GetSpawnEdgeForDirection(directionKind);
        }

        float minEnergy = Mathf.Min(minStartEnergy, maxStartEnergy);
        float maxEnergy = Mathf.Max(minStartEnergy, maxStartEnergy);
        float energy = UnityEngine.Random.Range(minEnergy, maxEnergy);

        sourceScratch.Clear();

        bool foundSource = alwaysMoveAwayFromGridEdge
            ? TryGetGridEdgeSeaSourceCells(spawnEdge, waveWidthCells, sourceScratch)
            : TryGetSeaEdgeSourceCells(DirectionToVector(directionKind), waveWidthCells, sourceScratch);

        if (!foundSource || sourceScratch.Count == 0)
        {
            if (debugLogging)
            {
                //Debug.LogWarning(
                    //$"[TsunamiSimulationSystem] Picked ocean edge={spawnEdge}, " +
                    //$"but no valid sea source cells were built. Tsunami skipped.");
            }

            return;
        }

        StartTsunamiFromCells(sourceScratch, directionKind, energy, forced);

        if (debugLogging)
        {
            //Debug.Log(
                //$"[TsunamiSimulationSystem] Triggered tsunami from ocean edge={spawnEdge}, " +
                //$"direction={directionKind}, sourceCells={sourceScratch.Count}");
        }
    }

    private bool TryPickOceanGridEdge(bool forced, out TsunamiGridEdge edge)
    {
        edge = forcedSpawnEdge;

        BuildValidOceanEdges(validOceanEdgesScratch);

        if (validOceanEdgesScratch.Count == 0)
            return false;

        // Forced tsunami tries the chosen edge first.
        // If that edge has no ocean, it falls back to any valid ocean edge.
        if (forced)
        {
            if (validOceanEdgesScratch.Contains(forcedSpawnEdge))
            {
                edge = forcedSpawnEdge;
                return true;
            }

            edge = validOceanEdgesScratch[UnityEngine.Random.Range(0, validOceanEdgesScratch.Count)];

            if (debugLogging)
            {
                //Debug.Log(
                    //$"[TsunamiSimulationSystem] Forced edge={forcedSpawnEdge} had no ocean cells. " +
                    //$"Falling back to ocean edge={edge}.");
            }

            return true;
        }

        // Natural tsunami only picks from edges that actually have ocean cells.
        if (!naturalTsunamisUseRandomGridEdge && validOceanEdgesScratch.Contains(forcedSpawnEdge))
        {
            edge = forcedSpawnEdge;
            return true;
        }

        edge = validOceanEdgesScratch[UnityEngine.Random.Range(0, validOceanEdgesScratch.Count)];
        return true;
    }

    private void BuildValidOceanEdges(List<TsunamiGridEdge> results)
    {
        results.Clear();

        if (gridManager == null)
            return;

        int columns = gridManager.columns;
        int rows = gridManager.rows;

        if (columns <= 0 || rows <= 0)
            return;

        if (EdgeHasSeaCells(TsunamiGridEdge.West))
            results.Add(TsunamiGridEdge.West);

        if (EdgeHasSeaCells(TsunamiGridEdge.East))
            results.Add(TsunamiGridEdge.East);

        if (EdgeHasSeaCells(TsunamiGridEdge.South))
            results.Add(TsunamiGridEdge.South);

        if (EdgeHasSeaCells(TsunamiGridEdge.North))
            results.Add(TsunamiGridEdge.North);
    }

    private bool EdgeHasSeaCells(TsunamiGridEdge edge)
    {
        if (gridManager == null)
            return false;

        int columns = gridManager.columns;
        int rows = gridManager.rows;

        if (columns <= 0 || rows <= 0)
            return false;

        switch (edge)
        {
            case TsunamiGridEdge.West:
                {
                    int x = 0;

                    for (int y = 0; y < rows; y++)
                    {
                        if (IsSeaCell(new TileCoord(x, y)))
                            return true;
                    }

                    return false;
                }

            case TsunamiGridEdge.East:
                {
                    int x = columns - 1;

                    for (int y = 0; y < rows; y++)
                    {
                        if (IsSeaCell(new TileCoord(x, y)))
                            return true;
                    }

                    return false;
                }

            case TsunamiGridEdge.South:
                {
                    int y = 0;

                    for (int x = 0; x < columns; x++)
                    {
                        if (IsSeaCell(new TileCoord(x, y)))
                            return true;
                    }

                    return false;
                }

            case TsunamiGridEdge.North:
                {
                    int y = rows - 1;

                    for (int x = 0; x < columns; x++)
                    {
                        if (IsSeaCell(new TileCoord(x, y)))
                            return true;
                    }

                    return false;
                }

            default:
                return false;
        }
    }

    private TsunamiGridEdge PickNaturalSpawnEdge()
    {
        if (!naturalTsunamisUseRandomGridEdge)
            return forcedSpawnEdge;

        return (TsunamiGridEdge)UnityEngine.Random.Range(0, 4);
    }

    private TsunamiDirection GetInwardDirectionFromEdge(TsunamiGridEdge edge)
    {
        switch (edge)
        {
            case TsunamiGridEdge.West:
                return TsunamiDirection.East;

            case TsunamiGridEdge.East:
                return TsunamiDirection.West;

            case TsunamiGridEdge.South:
                return TsunamiDirection.North;

            case TsunamiGridEdge.North:
                return TsunamiDirection.South;

            default:
                return TsunamiDirection.East;
        }
    }

    private TsunamiGridEdge GetSpawnEdgeForDirection(TsunamiDirection direction)
    {
        switch (direction)
        {
            case TsunamiDirection.East:
            case TsunamiDirection.NorthEast:
            case TsunamiDirection.SouthEast:
                return TsunamiGridEdge.West;

            case TsunamiDirection.West:
            case TsunamiDirection.NorthWest:
            case TsunamiDirection.SouthWest:
                return TsunamiGridEdge.East;

            case TsunamiDirection.North:
                return TsunamiGridEdge.South;

            case TsunamiDirection.South:
                return TsunamiGridEdge.North;

            default:
                return TsunamiGridEdge.West;
        }
    }

    private bool TryGetGridEdgeSeaSourceCells(
        TsunamiGridEdge edge,
        int width,
        List<TileCoord> results)
    {
        results.Clear();
        seaEdgeScratch.Clear();

        if (gridManager == null)
            return false;

        int columns = gridManager.columns;
        int rows = gridManager.rows;

        if (columns <= 0 || rows <= 0)
            return false;

        switch (edge)
        {
            case TsunamiGridEdge.West:
                {
                    int x = 0;

                    for (int y = 0; y < rows; y++)
                    {
                        TileCoord c = new TileCoord(x, y);

                        if (IsSeaCell(c))
                            seaEdgeScratch.Add(c);
                    }

                    break;
                }

            case TsunamiGridEdge.East:
                {
                    int x = columns - 1;

                    for (int y = 0; y < rows; y++)
                    {
                        TileCoord c = new TileCoord(x, y);

                        if (IsSeaCell(c))
                            seaEdgeScratch.Add(c);
                    }

                    break;
                }

            case TsunamiGridEdge.South:
                {
                    int y = 0;

                    for (int x = 0; x < columns; x++)
                    {
                        TileCoord c = new TileCoord(x, y);

                        if (IsSeaCell(c))
                            seaEdgeScratch.Add(c);
                    }

                    break;
                }

            case TsunamiGridEdge.North:
                {
                    int y = rows - 1;

                    for (int x = 0; x < columns; x++)
                    {
                        TileCoord c = new TileCoord(x, y);

                        if (IsSeaCell(c))
                            seaEdgeScratch.Add(c);
                    }

                    break;
                }
        }

        if (seaEdgeScratch.Count == 0)
            return false;

        TileCoord center = seaEdgeScratch[UnityEngine.Random.Range(0, seaEdgeScratch.Count)];

        TsunamiDirection inwardDirectionKind = GetInwardDirectionFromEdge(edge);
        Vector2Int inwardDirection = DirectionToVector(inwardDirectionKind);

        BuildWaveBandAroundSource(center, inwardDirection, width, results);

        if (results.Count == 0)
            results.Add(center);

        return results.Count > 0;
    }

    private void StartTsunamiFromCells(
        List<TileCoord> sourceCells,
        TsunamiDirection directionKind,
        float energy,
        bool forced)
    {
        int id = nextTsunamiId++;
        Vector2Int direction = DirectionToVector(directionKind);

        TsunamiWaveState wave = new TsunamiWaveState(
            id,
            directionKind,
            direction,
            energy,
            Mathf.Max(1, maxStepsPerTsunami));

        for (int i = 0; i < sourceCells.Count; i++)
        {
            TileCoord c = sourceCells[i];

            wave.sourceCells.Add(c);
            wave.currentCells.Add(c);
            wave.visitedCells.Add(c);
        }

        activeTsunamis.Add(id, wave);

        List<TileCoord> sourceSnapshot = new List<TileCoord>(wave.sourceCells);
        List<TileCoord> activeSnapshot = new List<TileCoord>(wave.currentCells);

        OnTsunamiStarted?.Invoke(new TsunamiStartedEventData
        {
            tsunamiId = id,
            directionKind = directionKind,
            direction = direction,
            startEnergy = energy,
            sourceCells = sourceSnapshot,
            forced = forced
        });

        OnTsunamiCellsChanged?.Invoke(new TsunamiCellsChangedEventData
        {
            tsunamiId = id,

            startEnergy = wave.startEnergy,
            energyRemaining = wave.energy,
            energy01 = wave.Energy01,

            addedCells = new List<TileCoord>(activeSnapshot),
            removedCells = new List<TileCoord>(),
            activeCells = activeSnapshot
        });

        if (debugLogging)
        {
            //Debug.Log(
                //$"[TsunamiSimulationSystem] Started tsunami id={id} " +
                //$"direction={directionKind} energy={energy:0.00} sourceCells={sourceCells.Count} forced={forced}");
        }

        MarkTsunamiSaveDirty();
    }

    private IEnumerator AdvanceAllTsunamisRoutine()
    {
        activeIdScratch.Clear();

        foreach (KeyValuePair<int, TsunamiWaveState> pair in activeTsunamis)
            activeIdScratch.Add(pair.Key);

        for (int i = 0; i < activeIdScratch.Count; i++)
        {
            int id = activeIdScratch[i];

            if (!activeTsunamis.TryGetValue(id, out TsunamiWaveState wave))
                continue;

            int steps = Mathf.Max(1, cellsAdvancedPerStep);

            for (int s = 0; s < steps; s++)
            {
                if (!activeTsunamis.ContainsKey(id))
                    break;

                yield return StartCoroutine(AdvanceWaveOneStepRoutine(wave));
            }
        }

        activeIdScratch.Clear();
        advanceRoutine = null;
    }

    private void AdvanceAllTsunamisImmediate()
    {
        activeIdScratch.Clear();

        foreach (KeyValuePair<int, TsunamiWaveState> pair in activeTsunamis)
            activeIdScratch.Add(pair.Key);

        for (int i = 0; i < activeIdScratch.Count; i++)
        {
            int id = activeIdScratch[i];

            if (!activeTsunamis.TryGetValue(id, out TsunamiWaveState wave))
                continue;

            int steps = Mathf.Max(1, cellsAdvancedPerStep);

            for (int s = 0; s < steps; s++)
            {
                if (!activeTsunamis.ContainsKey(id))
                    break;

                AdvanceWaveOneStep(wave);
            }
        }

        activeIdScratch.Clear();
    }

    private IEnumerator AdvanceWaveOneStepRoutine(TsunamiWaveState wave)
    {
        oldCellsScratch.Clear();

        foreach (TileCoord c in wave.currentCells)
            oldCellsScratch.Add(c);

        newCellSetScratch.Clear();
        newCellsScratch.Clear();

        int processed = 0;
        int maxPerFrame = Mathf.Max(1, waveCellsProcessedPerFrame);

        Vector2Int sideA = GetSideVectorA(wave.direction);
        Vector2Int sideB = -sideA;

        for (int i = 0; i < oldCellsScratch.Count; i++)
        {
            TileCoord current = oldCellsScratch[i];
            TileCoord forward = Offset(current, wave.direction);

            TryAddNextWaveCell(wave, forward, newCellSetScratch, newCellsScratch);

            if (UnityEngine.Random.value <= sideSpreadChance)
            {
                TryAddNextWaveCell(wave, Offset(forward, sideA), newCellSetScratch, newCellsScratch);
                TryAddNextWaveCell(wave, Offset(forward, sideB), newCellSetScratch, newCellsScratch);
            }

            processed++;

            if (processed >= maxPerFrame)
            {
                processed = 0;
                yield return null;
            }
        }

        FinishAdvanceStep(wave);
    }

    private void AdvanceWaveOneStep(TsunamiWaveState wave)
    {
        oldCellsScratch.Clear();

        foreach (TileCoord c in wave.currentCells)
            oldCellsScratch.Add(c);

        newCellSetScratch.Clear();
        newCellsScratch.Clear();

        Vector2Int sideA = GetSideVectorA(wave.direction);
        Vector2Int sideB = -sideA;

        for (int i = 0; i < oldCellsScratch.Count; i++)
        {
            TileCoord current = oldCellsScratch[i];
            TileCoord forward = Offset(current, wave.direction);

            TryAddNextWaveCell(wave, forward, newCellSetScratch, newCellsScratch);

            if (UnityEngine.Random.value <= sideSpreadChance)
            {
                TryAddNextWaveCell(wave, Offset(forward, sideA), newCellSetScratch, newCellsScratch);
                TryAddNextWaveCell(wave, Offset(forward, sideB), newCellSetScratch, newCellsScratch);
            }
        }

        FinishAdvanceStep(wave);
    }

    private void FinishAdvanceStep(TsunamiWaveState wave)
    {
        if (newCellsScratch.Count == 0)
        {
            EndTsunami(wave, TsunamiEndReason.NoValidCells);
            return;
        }

        addedScratch.Clear();
        removedScratch.Clear();

        for (int i = 0; i < newCellsScratch.Count; i++)
        {
            TileCoord c = newCellsScratch[i];

            if (!wave.currentCells.Contains(c))
                addedScratch.Add(c);
        }

        for (int i = 0; i < oldCellsScratch.Count; i++)
        {
            TileCoord c = oldCellsScratch[i];

            if (!newCellSetScratch.Contains(c))
                removedScratch.Add(c);
        }

        wave.currentCells.Clear();

        int landCells = 0;

        for (int i = 0; i < newCellsScratch.Count; i++)
        {
            TileCoord c = newCellsScratch[i];

            wave.currentCells.Add(c);
            wave.visitedCells.Add(c);

            if (IsLandCell(c))
                landCells++;
        }

        wave.stepCount++;

        float loss = energyLossPerStep;
        float landRatio = newCellsScratch.Count > 0
            ? landCells / (float)newCellsScratch.Count
            : 0f;

        float landMultiplier = Mathf.Lerp(1f, Mathf.Max(0f, landEnergyLossMultiplier), landRatio);
        loss *= landMultiplier;
        loss += extraEnergyLossPerCellCount * newCellsScratch.Count;

        wave.energy -= loss;

        if (tsunamiFloodBridge != null)
        {
            float energy01 = wave.Energy01;

            for (int i = 0; i < newCellsScratch.Count; i++)
            {
                TileCoord cellCoord = newCellsScratch[i];

                if (IsSeaCell(cellCoord))
                    continue;

                tsunamiFloodBridge.AddFloodFromTsunamiCell(cellCoord, energy01);
            }
        }

        List<TileCoord> activeSnapshot = new List<TileCoord>(wave.currentCells);

        OnTsunamiAdvanced?.Invoke(new TsunamiAdvancedEventData
        {
            tsunamiId = wave.tsunamiId,
            directionKind = wave.directionKind,
            direction = wave.direction,
            stepCount = wave.stepCount,

            startEnergy = wave.startEnergy,
            energyRemaining = wave.energy,
            energy01 = wave.Energy01,

            activeCells = activeSnapshot
        });

        OnTsunamiCellsChanged?.Invoke(new TsunamiCellsChangedEventData
        {
            tsunamiId = wave.tsunamiId,

            startEnergy = wave.startEnergy,
            energyRemaining = wave.energy,
            energy01 = wave.Energy01,

            addedCells = new List<TileCoord>(addedScratch),
            removedCells = new List<TileCoord>(removedScratch),
            activeCells = activeSnapshot
        });

        MarkTsunamiSaveDirty();

        if (debugLogging)
        {
            //Debug.Log(
                //$"[TsunamiSimulationSystem] Advanced id={wave.tsunamiId} " +
                //$"step={wave.stepCount} energy={wave.energy:0.00} activeCells={wave.currentCells.Count} landRatio={landRatio:0.00}");
        }

        if (wave.energy <= 0f)
        {
            EndTsunami(wave, TsunamiEndReason.EnergyDepleted);
            return;
        }

        if (wave.maxSteps > 0 && wave.stepCount >= wave.maxSteps)
        {
            EndTsunami(wave, TsunamiEndReason.MaxStepsReached);
            return;
        }
    }

    private void EndTsunami(TsunamiWaveState wave, TsunamiEndReason reason)
    {
        if (wave == null)
            return;

        if (!activeTsunamis.ContainsKey(wave.tsunamiId))
            return;

        List<TileCoord> finalCells = new List<TileCoord>(wave.currentCells);

        activeTsunamis.Remove(wave.tsunamiId);

        if (finalCells.Count > 0)
        {
            OnTsunamiCellsChanged?.Invoke(new TsunamiCellsChangedEventData
            {
                tsunamiId = wave.tsunamiId,

                startEnergy = wave.startEnergy,
                energyRemaining = wave.energy,
                energy01 = wave.Energy01,

                addedCells = new List<TileCoord>(),
                removedCells = new List<TileCoord>(finalCells),
                activeCells = new List<TileCoord>()
            });
        }

        OnTsunamiEnded?.Invoke(new TsunamiEndedEventData
        {
            tsunamiId = wave.tsunamiId,
            reason = reason,
            finalStepCount = wave.stepCount,
            finalEnergy = wave.energy,
            finalCells = finalCells
        });

        MarkTsunamiSaveDirty();

        if (debugLogging)
        {
            //Debug.Log(
                //$"[TsunamiSimulationSystem] Ended id={wave.tsunamiId} " +
                //$"reason={reason} finalStep={wave.stepCount} finalEnergy={wave.energy:0.00}");
        }
    }

    private bool TryAddNextWaveCell(
        TsunamiWaveState wave,
        TileCoord coord,
        HashSet<TileCoord> newSet,
        List<TileCoord> newList)
    {
        if (!IsCellInsideGrid(coord))
            return false;

        if (!CanWaveEnterCell(coord))
            return false;

        if (wave.visitedCells.Contains(coord))
            return false;

        if (!newSet.Add(coord))
            return false;

        newList.Add(coord);
        return true;
    }

    private bool TryGetSeaEdgeSourceCells(Vector2Int direction, int width, List<TileCoord> results)
    {
        results.Clear();
        seaEdgeScratch.Clear();

        if (gridManager == null)
            return false;

        for (int x = 0; x < gridManager.columns; x++)
        {
            for (int y = 0; y < gridManager.rows; y++)
            {
                TileCoord c = new TileCoord(x, y);

                if (!IsSeaCell(c))
                    continue;

                TileCoord next = Offset(c, direction);

                if (!IsCellInsideGrid(next))
                    continue;

                if (IsSeaCell(next))
                    continue;

                if (IsLakeCell(next))
                    continue;

                seaEdgeScratch.Add(c);
            }
        }

        if (seaEdgeScratch.Count == 0)
        {
            if (!TryGetRandomSeaCell(out TileCoord fallback))
                return false;

            BuildWaveBandAroundSource(fallback, direction, width, results);
            return results.Count > 0;
        }

        TileCoord center = seaEdgeScratch[UnityEngine.Random.Range(0, seaEdgeScratch.Count)];
        BuildWaveBandAroundSource(center, direction, width, results);

        if (results.Count == 0)
            results.Add(center);

        return results.Count > 0;
    }

    private bool TryGetRandomSeaCell(out TileCoord coord)
    {
        coord = default;

        seaEdgeScratch.Clear();

        if (gridManager == null)
            return false;

        for (int x = 0; x < gridManager.columns; x++)
        {
            for (int y = 0; y < gridManager.rows; y++)
            {
                TileCoord c = new TileCoord(x, y);

                if (IsSeaCell(c))
                    seaEdgeScratch.Add(c);
            }
        }

        if (seaEdgeScratch.Count == 0)
            return false;

        coord = seaEdgeScratch[UnityEngine.Random.Range(0, seaEdgeScratch.Count)];
        return true;
    }

    private void BuildWaveBandAroundSource(
        TileCoord center,
        Vector2Int direction,
        int width,
        List<TileCoord> results)
    {
        results.Clear();

        int safeWidth = Mathf.Max(1, width);
        int startOffset = -safeWidth / 2;
        Vector2Int side = GetSideVectorA(direction);

        for (int i = 0; i < safeWidth; i++)
        {
            int offset = startOffset + i;
            TileCoord c = Offset(center, side * offset);

            if (!IsCellInsideGrid(c))
                continue;

            if (!IsSeaCell(c))
                continue;

            if (!results.Contains(c))
                results.Add(c);
        }
    }

    private TsunamiDirection PickNaturalDirection()
    {
        if (!naturalTsunamisUseRandomDirection)
            return forcedDirection;

        int maxExclusive = allowDiagonalDirections ? 8 : 4;
        return (TsunamiDirection)UnityEngine.Random.Range(0, maxExclusive);
    }

    private bool IsMapReady()
    {
        ResolveReferences();

        if (gridManager == null || mapGenerator == null)
            return false;

        if (gridManager.columns <= 0 || gridManager.rows <= 0)
            return false;

        if (mapGenerator.IsGenerating)
            return false;

        if (!mapGenerator.HasBlockTerrainData)
            return false;

        if (mapGenerator.BlockColumns <= 0 || mapGenerator.BlockRows <= 0)
            return false;

        return true;
    }

    private bool IsCellInsideGrid(TileCoord coord)
    {
        if (gridManager == null)
            return false;

        return coord.x >= 0 &&
               coord.y >= 0 &&
               coord.x < gridManager.columns &&
               coord.y < gridManager.rows;
    }

    private bool CanWaveEnterCell(TileCoord coord)
    {
        if (!IsCellInsideGrid(coord))
            return false;

        if (IsLakeCell(coord) && !allowTsunamiIntoLakes)
            return false;

        if (!CanWaveEnterEnvironmentCell(coord))
            return false;

        return true;
    }

    private bool CanWaveEnterEnvironmentCell(TileCoord coord)
    {
        if (!blockTsunamiByEnvironment)
            return true;

        ResolveReferences();

        if (environmentDataSource == null)
            return !blockCellsWithoutEnvironment;

        if (!environmentDataSource.HasLiveEnvironmentTile(coord))
            return !blockCellsWithoutEnvironment;

        TileEnvironmentData data = environmentDataSource.GetTileData(coord);

        if (IsBlockedTsunamiEnvironmentType(data.environmentType))
        {
            if (debugLogging)
            {
                //Debug.Log(
                    //$"[TsunamiSimulationSystem] Wave blocked by environment type. " +
                    //$"coord={coord} envType={data.environmentType}");
            }

            return false;
        }

        if (IsBlockedTsunamiTileType(data.tileType))
        {
            if (debugLogging)
            {
                //Debug.Log(
                    //$"[TsunamiSimulationSystem] Wave blocked by tile type. " +
                    //$"coord={coord} tileType={data.tileType}");
            }

            return false;
        }

        return true;
    }

    private bool IsBlockedTsunamiEnvironmentType(EnvironmentType type)
    {
        if (blockedTsunamiEnvironmentTypes == null)
            return false;

        for (int i = 0; i < blockedTsunamiEnvironmentTypes.Length; i++)
        {
            if (blockedTsunamiEnvironmentTypes[i] == type)
                return true;
        }

        return false;
    }

    private bool IsBlockedTsunamiTileType(EnvironmentTileType type)
    {
        if (blockedTsunamiTileTypes == null)
            return false;

        for (int i = 0; i < blockedTsunamiTileTypes.Length; i++)
        {
            if (blockedTsunamiTileTypes[i] == type)
                return true;
        }

        return false;
    }

    private bool IsSeaCell(TileCoord coord)
    {
        if (!TryGetTerrainKindForCell(coord, out TerrainBlockKind kind))
            return false;

        return kind == TerrainBlockKind.Sea;
    }

    private bool IsLakeCell(TileCoord coord)
    {
        if (!TryGetTerrainKindForCell(coord, out TerrainBlockKind kind))
            return false;

        return kind == TerrainBlockKind.Lake;
    }

    private bool IsLandCell(TileCoord coord)
    {
        if (!TryGetTerrainKindForCell(coord, out TerrainBlockKind kind))
            return false;

        return kind == TerrainBlockKind.Land;
    }

    private bool TryGetTerrainKindForCell(TileCoord coord, out TerrainBlockKind kind)
    {
        kind = TerrainBlockKind.Sea;

        if (mapGenerator == null)
            return false;

        Vector2Int cell = new Vector2Int(coord.x, coord.y);
        Vector2Int block = mapGenerator.GetBlockFromCell(cell);

        if (!mapGenerator.IsValidBlock(block))
            return false;

        return mapGenerator.TryGetBlockTerrain(block, out kind);
    }

    private static Vector2Int DirectionToVector(TsunamiDirection direction)
    {
        switch (direction)
        {
            case TsunamiDirection.North:
                return new Vector2Int(0, 1);

            case TsunamiDirection.East:
                return new Vector2Int(1, 0);

            case TsunamiDirection.South:
                return new Vector2Int(0, -1);

            case TsunamiDirection.West:
                return new Vector2Int(-1, 0);

            case TsunamiDirection.NorthEast:
                return new Vector2Int(1, 1);

            case TsunamiDirection.SouthEast:
                return new Vector2Int(1, -1);

            case TsunamiDirection.SouthWest:
                return new Vector2Int(-1, -1);

            case TsunamiDirection.NorthWest:
                return new Vector2Int(-1, 1);

            default:
                return Vector2Int.zero;
        }
    }

    private static Vector2Int GetSideVectorA(Vector2Int direction)
    {
        if (direction == Vector2Int.zero)
            return Vector2Int.right;

        return new Vector2Int(-direction.y, direction.x);
    }

    private static TileCoord Offset(TileCoord coord, Vector2Int offset)
    {
        return new TileCoord(coord.x + offset.x, coord.y + offset.y);
    }

    public bool TryTriggerTsunamiFromEarthquake(
    TsunamiGridEdge preferredEdge,
    float startEnergy,
    bool fallbackToAnyOceanEdge = true)
    {
        if (!canHaveTsunamis)
            return false;

        ResolveReferences();

        if (!IsMapReady())
        {
            if (debugLogging) {}
                //Debug.LogWarning("[TsunamiSimulationSystem] Cannot trigger earthquake tsunami. Map is not ready.");

            return false;
        }

        TsunamiGridEdge spawnEdge = preferredEdge;

        if (!EdgeHasSeaCells(spawnEdge))
        {
            if (!fallbackToAnyOceanEdge)
            {
                if (debugLogging)
                {
                    //Debug.LogWarning(
                        //$"[TsunamiSimulationSystem] Earthquake tsunami preferred edge={preferredEdge} " +
                        //$"has no sea cells and fallback is disabled.");
                }

                return false;
            }

            if (!TryPickOceanGridEdge(false, out spawnEdge))
            {
                if (debugLogging) {}
                    //Debug.LogWarning("[TsunamiSimulationSystem] Earthquake tsunami failed. No valid ocean edge found.");

                return false;
            }

            if (debugLogging)
            {
                //Debug.Log(
                    //$"[TsunamiSimulationSystem] Earthquake tsunami preferred edge={preferredEdge} had no sea cells. " +
                    //$"Using fallback edge={spawnEdge}.");
            }
        }

        TsunamiDirection directionKind = GetInwardDirectionFromEdge(spawnEdge);

        sourceScratch.Clear();

        bool foundSource = TryGetGridEdgeSeaSourceCells(
            spawnEdge,
            waveWidthCells,
            sourceScratch);

        if (!foundSource || sourceScratch.Count == 0)
        {
            if (debugLogging)
            {
                //Debug.LogWarning(
                    //$"[TsunamiSimulationSystem] Earthquake tsunami failed. " +
                    //$"No valid sea source cells built for edge={spawnEdge}.");
            }

            return false;
        }

        float finalEnergy = Mathf.Max(0.01f, startEnergy);

        // forced=false because this is a natural disaster chain, not the debug key.
        StartTsunamiFromCells(
            sourceScratch,
            directionKind,
            finalEnergy,
            false);

        if (debugLogging)
        {
            //Debug.Log(
                //$"[TsunamiSimulationSystem] Earthquake triggered tsunami. " +
                //$"Edge={spawnEdge}, Direction={directionKind}, Energy={finalEnergy:0.00}, SourceCells={sourceScratch.Count}");
        }

        return true;
    }

    private void ResolveReferences()
    {
        if (gridManager == null)
            gridManager = GridManager.Instance;

        if (mapGenerator == null)
            mapGenerator = FindObjectOfType<MapGenerator>();

        if (environmentDataSource == null)
            environmentDataSource = MonoEnvironmentDataSource.Instance;

        if (environmentDataSource == null)
            environmentDataSource = FindObjectOfType<MonoEnvironmentDataSource>();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
            return;

        if (gridManager == null)
            gridManager = GridManager.Instance;

        if (gridManager == null)
            return;

        float size = gridManager.cellSize * 0.85f;

        foreach (KeyValuePair<int, TsunamiWaveState> pair in activeTsunamis)
        {
            TsunamiWaveState wave = pair.Value;

            Gizmos.color = visitedGizmoColor;
            foreach (TileCoord c in wave.visitedCells)
                DrawCellGizmo(c, size);

            Gizmos.color = sourceGizmoColor;
            foreach (TileCoord c in wave.sourceCells)
                DrawCellGizmo(c, size * 0.9f);

            Gizmos.color = activeGizmoColor;
            foreach (TileCoord c in wave.currentCells)
                DrawCellGizmo(c, size);
        }
    }

    private void DrawCellGizmo(TileCoord coord, float size)
    {
        Vector3 pos = GetCellCenterWorld(coord);
        Gizmos.DrawCube(pos + Vector3.up * 0.35f, new Vector3(size, 0.08f, size));
    }

    private Vector3 GetCellCenterWorld(TileCoord coord)
    {
        if (gridManager == null)
            return new Vector3(coord.x, 0f, coord.y);

        Vector3 corner = gridManager.GetWorldPosition(coord.x, coord.y);

        return corner + new Vector3(
            gridManager.cellSize * 0.5f,
            0f,
            gridManager.cellSize * 0.5f);
    }

    public TsunamiSimulationSaveData SaveState()
    {
        TsunamiSimulationSaveData data = new TsunamiSimulationSaveData
        {
            nextTsunamiId = Mathf.Max(1, nextTsunamiId),
            lastRollTurn = lastRollTurn
        };

        foreach (KeyValuePair<int, TsunamiWaveState> pair in activeTsunamis)
        {
            TsunamiWaveState wave = pair.Value;

            if (wave == null)
                continue;

            TsunamiWaveSaveData saved = new TsunamiWaveSaveData
            {
                tsunamiId = wave.tsunamiId,

                directionKindValue = (int)wave.directionKind,
                directionX = wave.direction.x,
                directionY = wave.direction.y,

                startEnergy = Mathf.Max(0.01f, wave.startEnergy),
                energyRemaining = Mathf.Max(0f, wave.energy),

                maxSteps = Mathf.Max(1, wave.maxSteps),
                stepCount = Mathf.Max(0, wave.stepCount)
            };

            CopyCellsToSaveList(wave.sourceCells, saved.sourceCells);
            CopyCellsToSaveList(wave.currentCells, saved.currentCells);
            CopyCellsToSaveList(wave.visitedCells, saved.visitedCells);

            if (saved.currentCells.Count <= 0)
                continue;

            data.activeWaves.Add(saved);
        }

        return data;
    }

    public void LoadState(TsunamiSimulationSaveData data)
    {
        ResolveReferences();

        if (advanceRoutine != null)
        {
            StopCoroutine(advanceRoutine);
            advanceRoutine = null;
        }

        activeTsunamis.Clear();

        activeIdScratch.Clear();
        sourceScratch.Clear();
        seaEdgeScratch.Clear();
        oldCellsScratch.Clear();
        newCellsScratch.Clear();
        newCellSetScratch.Clear();
        addedScratch.Clear();
        removedScratch.Clear();

        if (data == null)
        {
            nextTsunamiId = 1;
            lastRollTurn = int.MinValue;

            if (TsunamiOverlayManager.Instance != null)
                TsunamiOverlayManager.Instance.ClearAllOverlays();

            return;
        }

        nextTsunamiId = Mathf.Max(1, data.nextTsunamiId);
        lastRollTurn = data.lastRollTurn;

        int highestLoadedId = 0;
        int restored = 0;

        if (data.activeWaves != null)
        {
            for (int i = 0; i < data.activeWaves.Count; i++)
            {
                TsunamiWaveSaveData saved = data.activeWaves[i];

                if (saved == null)
                    continue;

                TsunamiWaveState wave = RestoreWaveFromSave(saved);

                if (wave == null)
                    continue;

                activeTsunamis[wave.tsunamiId] = wave;

                if (wave.tsunamiId > highestLoadedId)
                    highestLoadedId = wave.tsunamiId;

                restored++;
            }
        }

        nextTsunamiId = Mathf.Max(nextTsunamiId, highestLoadedId + 1);

        // Rebuild visuals only. Do not fire tsunami gameplay events on load.
        if (TsunamiOverlayManager.Instance != null)
            TsunamiOverlayManager.Instance.RebuildAllOverlaysFromSimulation();

        if (debugLogging)
        {
            //Debug.Log(
                //$"[TsunamiSimulationSystem] Loaded tsunami state. " +
                //$"ActiveWaves={restored}, NextId={nextTsunamiId}, LastRollTurn={lastRollTurn}");
        }
    }

    private TsunamiWaveState RestoreWaveFromSave(TsunamiWaveSaveData saved)
    {
        if (saved == null)
            return null;

        TsunamiDirection directionKind = RestoreTsunamiDirection(saved.directionKindValue);

        Vector2Int direction = DirectionToVector(directionKind);

        if (direction == Vector2Int.zero)
        {
            direction = new Vector2Int(
                Mathf.Clamp(saved.directionX, -1, 1),
                Mathf.Clamp(saved.directionY, -1, 1));
        }

        if (direction == Vector2Int.zero)
            return null;

        int id = saved.tsunamiId > 0 ? saved.tsunamiId : nextTsunamiId++;

        float startEnergy = Mathf.Max(0.01f, saved.startEnergy);
        float energyRemaining = Mathf.Max(0f, saved.energyRemaining);

        if (energyRemaining <= 0f)
            return null;

        int maxSteps = Mathf.Max(1, saved.maxSteps);

        TsunamiWaveState wave = new TsunamiWaveState(
            id,
            directionKind,
            direction,
            startEnergy,
            maxSteps);

        wave.energy = energyRemaining;
        wave.stepCount = Mathf.Max(0, saved.stepCount);

        RestoreCellsIntoList(saved.sourceCells, wave.sourceCells, requireInsideGrid: true);
        RestoreCellsIntoList(saved.currentCells, wave.currentCells, requireInsideGrid: true);
        RestoreCellsIntoList(saved.visitedCells, wave.visitedCells, requireInsideGrid: true);

        if (wave.currentCells.Count <= 0)
            return null;

        if (wave.sourceCells.Count <= 0)
        {
            foreach (TileCoord c in wave.currentCells)
            {
                if (!wave.sourceCells.Contains(c))
                    wave.sourceCells.Add(c);
            }
        }

        if (wave.visitedCells.Count <= 0)
        {
            foreach (TileCoord c in wave.sourceCells)
            {
                if (!wave.visitedCells.Contains(c))
                    wave.visitedCells.Add(c);
            }

            foreach (TileCoord c in wave.currentCells)
            {
                if (!wave.visitedCells.Contains(c))
                    wave.visitedCells.Add(c);
            }
        }

        return wave;
    }

    private void CopyCellsToSaveList(
        IEnumerable<TileCoord> source,
        List<TsunamiCellSaveData> results)
    {
        if (source == null || results == null)
            return;

        results.Clear();

        foreach (TileCoord coord in source)
            results.Add(new TsunamiCellSaveData(coord.x, coord.y));
    }

    private void RestoreCellsIntoList(
    List<TsunamiCellSaveData> savedCells,
    ICollection<TileCoord> results,
    bool requireInsideGrid)
    {
        if (results == null)
            return;

        results.Clear();

        if (savedCells == null)
            return;

        for (int i = 0; i < savedCells.Count; i++)
        {
            TsunamiCellSaveData saved = savedCells[i];

            if (saved == null)
                continue;

            TileCoord coord = new TileCoord(saved.x, saved.y);

            if (requireInsideGrid && !IsCellInsideGrid(coord))
                continue;

            if (!results.Contains(coord))
                results.Add(coord);
        }
    }

    private TsunamiDirection RestoreTsunamiDirection(int value)
    {
        if (value < (int)TsunamiDirection.North ||
            value > (int)TsunamiDirection.NorthWest)
        {
            return TsunamiDirection.East;
        }

        return (TsunamiDirection)value;
    }

    public bool CopyActiveTsunamiVisualSnapshots(List<TsunamiVisualSnapshot> results)
    {
        if (results == null)
            return false;

        results.Clear();

        foreach (KeyValuePair<int, TsunamiWaveState> pair in activeTsunamis)
        {
            TsunamiWaveState wave = pair.Value;

            if (wave == null)
                continue;

            TsunamiVisualSnapshot snapshot = new TsunamiVisualSnapshot
            {
                tsunamiId = wave.tsunamiId,
                direction = wave.direction,
                energy01 = wave.Energy01
            };

            foreach (TileCoord coord in wave.currentCells)
            {
                if (!snapshot.activeCells.Contains(coord))
                    snapshot.activeCells.Add(coord);
            }

            if (snapshot.activeCells.Count > 0)
                results.Add(snapshot);
        }

        return results.Count > 0;
    }

    private void MarkTsunamiSaveDirty()
    {
        SaveSystem.MarkSectionDirty(SaveSectionKeys.WorldSim);
    }

    public void ApplyPresetSettings(TsunamiPresetSettings settings)
    {
        if (settings == null || !settings.overrideTsunamis)
            return;

        canHaveTsunamis = settings.canHaveTsunamis;
        tsunamiChancePerTurn = settings.tsunamiChancePerTurn;

        minStartEnergy = settings.minStartEnergy;
        maxStartEnergy = settings.maxStartEnergy;
        energyLossPerStep = settings.energyLossPerStep;
        landEnergyLossMultiplier = settings.landEnergyLossMultiplier;
        extraEnergyLossPerCellCount = settings.extraEnergyLossPerCellCount;

        maxStepsPerTsunami = settings.maxStepsPerTsunami;
        waveWidthCells = settings.waveWidthCells;
        sideSpreadChance = settings.sideSpreadChance;

        allowDiagonalDirections = settings.allowDiagonalDirections;
        allowTsunamiIntoLakes = settings.allowTsunamiIntoLakes;

        if (debugLogging) {}
            //Debug.Log("[TsunamiSimulationSystem] Applied tsunami preset settings.");
    }
}
