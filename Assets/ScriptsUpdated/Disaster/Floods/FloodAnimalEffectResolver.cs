using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FloodAnimalEffectResolver : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FloodSimulationSystem floodSimulationSystem;
    [SerializeField] private GridManager gridManager;
    [SerializeField] private AnimalSimulation animalSimulation;

    [Header("Depth")]
    [Tooltip("Flood depth01 must be at or above this before animals are affected.")]
    [Range(0f, 1f)]
    [SerializeField] private float animalEffectsStartAtDepth01 = 0.18f;

    [Tooltip("Depth01 at or above this uses strongest animal effects.")]
    [Range(0f, 1f)]
    [SerializeField] private float severeAnimalEffectsDepth01 = 1f;

    [Header("Animal Flee")]
    [Range(0f, 1f)]
    [SerializeField] private float minFleeChance = 0.15f;

    [Range(0f, 1f)]
    [SerializeField] private float maxFleeChance = 0.75f;

    [Min(1)]
    [SerializeField] private int fleeSearchDistance = 3;

    [Header("If Flee Fails")]
    [SerializeField] private bool instantKillIfFleeFails = false;

    [Min(0)]
    [SerializeField] private int minDamageIfFleeFails = 1;

    [Min(0)]
    [SerializeField] private int maxDamageIfFleeFails = 18;

    [Header("Filtering")]
    [Range(0f, 1f)]
    [SerializeField] private float minThreatSeverityToAffect = 0.05f;

    [Tooltip("If true, flee tiles cannot be active flood damage cells.")]
    [SerializeField] private bool avoidActiveFloodCellsWhenFleeing = true;

    [Tooltip("If true, flee tiles cannot be the current flood cell being processed.")]
    [SerializeField] private bool avoidCurrentDangerCellWhenFleeing = true;

    [Tooltip("If true, flee tiles cannot be any currently flooded cell, even below animal damage threshold.")]
    [SerializeField] private bool avoidAnyFloodedCellWhenFleeing = true;

    [Header("Over-Time Rules")]
    [Tooltip("If true, each animal group can only be affected by flooding once per turn.")]
    [SerializeField] private bool affectEachGroupOncePerTurn = true;

    [Header("Processing")]
    [SerializeField] private bool processOnlyWhenFloodChanges = true;
    [SerializeField] private bool processOverFrames = true;

    [Min(1)]
    [SerializeField] private int cellsProcessedPerFrame = 32;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = true;

    private FloodSimulationSystem subscribedFloodSimulationSystem;
    private Coroutine processRoutine;

    private readonly HashSet<TileCoord> activeFloodDangerCells = new HashSet<TileCoord>();
    private readonly List<TileCoord> activeFloodCellScratch = new List<TileCoord>();
    private readonly List<int> groupIdsScratch = new List<int>(16);

    private readonly HashSet<int> processedGroupsThisPass = new HashSet<int>();
    private readonly Dictionary<int, int> lastAffectedTurnByGroupId = new Dictionary<int, int>();

    private TileCoord currentFloodCell;
    private bool hasCurrentFloodCell;

    private void Awake()
    {
        EnsureLinks();
    }

    private void OnEnable()
    {
        EnsureLinks();
        RebindFloodEvents();
    }

    private void Start()
    {
        EnsureLinks();
        RebindFloodEvents();
    }

    private void OnDisable()
    {
        UnbindFloodEvents();

        if (processRoutine != null)
        {
            StopCoroutine(processRoutine);
            processRoutine = null;
        }

        activeFloodDangerCells.Clear();
        activeFloodCellScratch.Clear();
        groupIdsScratch.Clear();
        processedGroupsThisPass.Clear();

        hasCurrentFloodCell = false;
    }

    public void InstallRuntimeRefs(
        FloodSimulationSystem newFloodSimulationSystem,
        GridManager newGridManager,
        AnimalSimulation newAnimalSimulation)
    {
        if (newFloodSimulationSystem != null)
            floodSimulationSystem = newFloodSimulationSystem;

        if (newGridManager != null)
            gridManager = newGridManager;

        if (newAnimalSimulation != null)
            animalSimulation = newAnimalSimulation;

        RebindFloodEvents();

        if (debugLogging)
        {
            //Debug.Log(
                //$"[FloodAnimalEffectResolver] Installed refs. " +
                //$"Flood={(floodSimulationSystem != null ? floodSimulationSystem.name : "NULL")}, " +
                //$"GridManager={(gridManager != null ? gridManager.name : "NULL")}, " +
                //$"AnimalSimulation={(animalSimulation != null ? "OK" : "NULL")}"
            //);
        }
    }

    public void SetAnimalSimulation(AnimalSimulation newAnimalSimulation)
    {
        animalSimulation = newAnimalSimulation;
    }

    private void EnsureLinks()
    {
        if (floodSimulationSystem == null)
            floodSimulationSystem = FindFirstObjectByType<FloodSimulationSystem>();

        if (gridManager == null)
            gridManager = GridManager.Instance;

        if (animalSimulation == null)
            animalSimulation = AnimalSimulationAccess.Current;
    }

    private void RebindFloodEvents()
    {
        if (subscribedFloodSimulationSystem == floodSimulationSystem)
            return;

        UnbindFloodEvents();

        subscribedFloodSimulationSystem = floodSimulationSystem;

        if (subscribedFloodSimulationSystem != null)
            subscribedFloodSimulationSystem.OnFloodCellsChanged += HandleFloodCellsChanged;
    }

    private void UnbindFloodEvents()
    {
        if (subscribedFloodSimulationSystem == null)
            return;

        subscribedFloodSimulationSystem.OnFloodCellsChanged -= HandleFloodCellsChanged;
        subscribedFloodSimulationSystem = null;
    }

    private void HandleFloodCellsChanged(IReadOnlyList<TileCoord> changedCells)
    {
        if (processOnlyWhenFloodChanges &&
            (changedCells == null || changedCells.Count == 0))
        {
            return;
        }

        ProcessFloodAnimalEffects();
    }

    [ContextMenu("Debug/Process Flood Animal Effects Now")]
    public void ProcessFloodAnimalEffects()
    {
        EnsureLinks();

        if (animalSimulation == null || gridManager == null || floodSimulationSystem == null)
        {
            if (debugLogging) {}
                //Debug.LogWarning("[FloodAnimalEffectResolver] Missing references.");

            return;
        }

        if (processRoutine != null)
            StopCoroutine(processRoutine);

        BuildActiveFloodDangerCells();

        if (activeFloodDangerCells.Count == 0)
        {
            if (debugLogging) {}
                //Debug.Log("[FloodAnimalEffectResolver] No active flood danger cells.");

            return;
        }

        if (processOverFrames)
            processRoutine = StartCoroutine(ProcessFloodAnimalEffectsRoutine());
        else
            ProcessFloodAnimalEffectsImmediate();
    }

    private void BuildActiveFloodDangerCells()
    {
        activeFloodDangerCells.Clear();
        activeFloodCellScratch.Clear();

        if (floodSimulationSystem == null)
            return;

        foreach (KeyValuePair<TileCoord, FloodCellState> pair in floodSimulationSystem.ActiveFloodCells)
        {
            FloodCellState state = pair.Value;

            if (state == null)
                continue;

            if (state.floodDepth01 < animalEffectsStartAtDepth01)
                continue;

            activeFloodDangerCells.Add(pair.Key);
            activeFloodCellScratch.Add(pair.Key);
        }
    }

    private IEnumerator ProcessFloodAnimalEffectsRoutine()
    {
        processedGroupsThisPass.Clear();

        int processedCells = 0;
        int maxPerFrame = Mathf.Max(1, cellsProcessedPerFrame);
        int affectedGroups = 0;

        for (int i = 0; i < activeFloodCellScratch.Count; i++)
        {
            TileCoord cell = activeFloodCellScratch[i];

            if (!floodSimulationSystem.TryGetFloodCell(cell, out FloodCellState state) || state == null)
                continue;

            float severity01 = GetDepthSeverity01(state.floodDepth01);

            if (severity01 >= minThreatSeverityToAffect)
                affectedGroups += ApplyAnimalFloodThreatAtCell(cell, state, severity01);

            processedCells++;

            if (processedCells >= maxPerFrame)
            {
                processedCells = 0;
                yield return null;
            }
        }

        if (debugLogging)
        {
            //Debug.Log(
                //$"[FloodAnimalEffectResolver] Complete. " +
                //$"AffectedGroups={affectedGroups}, ActiveFloodDangerCells={activeFloodDangerCells.Count}");
        }

        processedGroupsThisPass.Clear();
        processRoutine = null;
    }

    private void ProcessFloodAnimalEffectsImmediate()
    {
        processedGroupsThisPass.Clear();

        int affectedGroups = 0;

        for (int i = 0; i < activeFloodCellScratch.Count; i++)
        {
            TileCoord cell = activeFloodCellScratch[i];

            if (!floodSimulationSystem.TryGetFloodCell(cell, out FloodCellState state) || state == null)
                continue;

            float severity01 = GetDepthSeverity01(state.floodDepth01);

            if (severity01 >= minThreatSeverityToAffect)
                affectedGroups += ApplyAnimalFloodThreatAtCell(cell, state, severity01);
        }

        if (debugLogging)
        {
            //Debug.Log(
                //$"[FloodAnimalEffectResolver] Complete. " +
                //$"AffectedGroups={affectedGroups}, ActiveFloodDangerCells={activeFloodDangerCells.Count}");
        }

        processedGroupsThisPass.Clear();
    }

    private int ApplyAnimalFloodThreatAtCell(
        TileCoord coord,
        FloodCellState state,
        float severity01)
    {
        if (IsOutsideGrid(coord))
            return 0;

        if (!animalSimulation.HasGroupsAtTile(coord))
            return 0;

        groupIdsScratch.Clear();

        int count = animalSimulation.GetGroupIdsAtTileNonAlloc(coord, groupIdsScratch);

        if (count <= 0)
            return 0;

        hasCurrentFloodCell = true;
        currentFloodCell = coord;

        int affected = 0;
        int currentTurn = TurnSystem.GetCurrentTurn();

        float fleeChance = Mathf.Lerp(minFleeChance, maxFleeChance, severity01);
        int damageIfFleeFails = Mathf.RoundToInt(
            Mathf.Lerp(minDamageIfFleeFails, maxDamageIfFleeFails, severity01)
        );

        for (int i = 0; i < groupIdsScratch.Count; i++)
        {
            int groupId = groupIdsScratch[i];

            if (processedGroupsThisPass.Contains(groupId))
                continue;

            if (affectEachGroupOncePerTurn &&
                lastAffectedTurnByGroupId.TryGetValue(groupId, out int lastTurn) &&
                lastTurn == currentTurn)
            {
                continue;
            }

            processedGroupsThisPass.Add(groupId);

            bool changed = animalSimulation.TryApplyFloodThreatToGroup(
                groupId,
                fleeChance,
                instantKillIfFleeFails,
                damageIfFleeFails,
                fleeSearchDistance,
                IsDangerousFloodTile,
                IsValidFleeTile,
                debugLogging
            );

            if (changed)
            {
                affected++;

                if (affectEachGroupOncePerTurn)
                    lastAffectedTurnByGroupId[groupId] = currentTurn;
            }

            if (debugLogging)
            {
                //Debug.Log(
                    //$"[FloodAnimalEffectResolver] Flood animal group {groupId} at {coord}. " +
                    //$"Depth01={state.floodDepth01:0.00}, Severity={severity01:0.00}, " +
                    //$"FleeChance={fleeChance:0.00}, DamageIfFail={damageIfFleeFails}"
                //);
            }
        }

        hasCurrentFloodCell = false;

        return affected;
    }

    private float GetDepthSeverity01(float depth01)
    {
        float min = Mathf.Min(animalEffectsStartAtDepth01, severeAnimalEffectsDepth01);
        float max = Mathf.Max(animalEffectsStartAtDepth01, severeAnimalEffectsDepth01);

        return Mathf.Clamp01(Mathf.InverseLerp(min, max, depth01));
    }

    private bool IsDangerousFloodTile(TileCoord coord)
    {
        if (hasCurrentFloodCell && avoidCurrentDangerCellWhenFleeing && coord.Equals(currentFloodCell))
            return true;

        if (avoidActiveFloodCellsWhenFleeing && activeFloodDangerCells.Contains(coord))
            return true;

        if (avoidAnyFloodedCellWhenFleeing &&
            floodSimulationSystem != null &&
            floodSimulationSystem.IsFlooded(coord))
        {
            return true;
        }

        return false;
    }

    private bool IsValidFleeTile(TileCoord coord)
    {
        if (IsOutsideGrid(coord))
            return false;

        if (IsDangerousFloodTile(coord))
            return false;

        return true;
    }

    private bool IsOutsideGrid(TileCoord coord)
    {
        return gridManager == null ||
               coord.x < 0 ||
               coord.y < 0 ||
               coord.x >= gridManager.columns ||
               coord.y >= gridManager.rows;
    }
}
