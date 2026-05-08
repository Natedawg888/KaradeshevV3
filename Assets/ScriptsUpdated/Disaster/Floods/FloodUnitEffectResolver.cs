using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FloodUnitEffectResolver : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FloodSimulationSystem floodSimulationSystem;
    [SerializeField] private GridManager gridManager;

    [Header("Depth")]
    [Tooltip("Flood depth01 must be at or above this before units are affected.")]
    [Range(0f, 1f)]
    [SerializeField] private float unitEffectsStartAtDepth01 = 0.18f;

    [Tooltip("Flood depth01 at or above this uses maximum unit damage.")]
    [Range(0f, 1f)]
    [SerializeField] private float severeUnitEffectsDepth01 = 1f;

    [Header("Damage")]
    [Min(0)]
    [SerializeField] private int minUnitDamage = 1;

    [Min(0)]
    [SerializeField] private int maxUnitDamage = 18;

    [Tooltip("If true, every unit group can only be damaged once per turn by flooding.")]
    [SerializeField] private bool affectEachUnitGroupOnlyOncePerTurn = true;

    [Header("Population")]
    [SerializeField] private bool applyPopulationLossFromUnitLoss = true;

    [Header("Processing")]
    [Tooltip("If true, only processes unit effects when flood cells change.")]
    [SerializeField] private bool processOnlyWhenFloodChanges = true;

    [SerializeField] private bool processOverFrames = true;

    [Min(1)]
    [SerializeField] private int cellsProcessedPerFrame = 32;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = true;

    private FloodSimulationSystem subscribedFloodSimulationSystem;
    private Coroutine processRoutine;

    private readonly HashSet<TileCoord> activeFloodDamageCells =
        new HashSet<TileCoord>();

    private readonly List<TileCoord> activeFloodCellScratch =
        new List<TileCoord>();

    private readonly List<PlayerUnitManager.GroupInfo> trackedGroupsScratch =
        new List<PlayerUnitManager.GroupInfo>(128);

    private readonly List<TileUnitGroupData> tmpUnitGroupSnapshot =
        new List<TileUnitGroupData>(16);

    private readonly HashSet<string> processedGroupsThisPass =
        new HashSet<string>();

    private readonly Dictionary<string, int> lastAffectedTurnByGroupId =
        new Dictionary<string, int>();

    private readonly Dictionary<long, List<TileUnitGroupControl>> unitControlsByCell =
        new Dictionary<long, List<TileUnitGroupControl>>();

    private readonly List<List<TileUnitGroupControl>> pooledControlLists =
        new List<List<TileUnitGroupControl>>();

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

        activeFloodDamageCells.Clear();
        activeFloodCellScratch.Clear();
        trackedGroupsScratch.Clear();
        tmpUnitGroupSnapshot.Clear();
        processedGroupsThisPass.Clear();

        ClearUnitControlLookup();
    }

    public void InstallRuntimeRefs(
        FloodSimulationSystem newFloodSimulationSystem,
        GridManager newGridManager)
    {
        if (newFloodSimulationSystem != null)
            floodSimulationSystem = newFloodSimulationSystem;

        if (newGridManager != null)
            gridManager = newGridManager;

        RebindFloodEvents();

        if (debugLogging)
        {
            //Debug.Log(
                //$"[FloodUnitEffectResolver] Installed refs. " +
                //$"Flood={(floodSimulationSystem != null ? floodSimulationSystem.name : "NULL")}, " +
                //$"GridManager={(gridManager != null ? gridManager.name : "NULL")}"
            //);
        }
    }

    private void EnsureLinks()
    {
        if (floodSimulationSystem == null)
            floodSimulationSystem = FindFirstObjectByType<FloodSimulationSystem>();

        if (gridManager == null)
            gridManager = GridManager.Instance;
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

        ProcessFloodUnitEffects();
    }

    [ContextMenu("Debug/Process Flood Unit Effects Now")]
    public void ProcessFloodUnitEffects()
    {
        EnsureLinks();

        if (floodSimulationSystem == null || gridManager == null)
        {
            if (debugLogging)
                //Debug.LogWarning("[FloodUnitEffectResolver] Missing references.");

            return;
        }

        if (processRoutine != null)
            StopCoroutine(processRoutine);

        BuildUnitControlLookup();

        if (unitControlsByCell.Count == 0)
        {
            ClearUnitControlLookup();
            return;
        }

        BuildActiveFloodDamageCells();

        if (activeFloodDamageCells.Count == 0)
        {
            if (debugLogging)
                //Debug.Log("[FloodUnitEffectResolver] No active flood damage cells.");

            ClearUnitControlLookup();
            return;
        }

        if (processOverFrames)
            processRoutine = StartCoroutine(ProcessFloodUnitEffectsRoutine());
        else
            ProcessFloodUnitEffectsImmediate();
    }

    private void BuildActiveFloodDamageCells()
    {
        activeFloodDamageCells.Clear();
        activeFloodCellScratch.Clear();

        if (floodSimulationSystem == null)
            return;

        foreach (KeyValuePair<TileCoord, FloodCellState> pair in floodSimulationSystem.ActiveFloodCells)
        {
            FloodCellState state = pair.Value;

            if (state == null)
                continue;

            if (state.floodDepth01 < unitEffectsStartAtDepth01)
                continue;

            activeFloodDamageCells.Add(pair.Key);
            activeFloodCellScratch.Add(pair.Key);
        }
    }

    private IEnumerator ProcessFloodUnitEffectsRoutine()
    {
        processedGroupsThisPass.Clear();

        int processedCells = 0;
        int maxPerFrame = Mathf.Max(1, cellsProcessedPerFrame);
        int affectedGroups = 0;

        for (int i = 0; i < activeFloodCellScratch.Count; i++)
        {
            TileCoord coord = activeFloodCellScratch[i];

            if (!floodSimulationSystem.TryGetFloodCell(coord, out FloodCellState state) || state == null)
                continue;

            float severity01 = GetDepthSeverity01(state.floodDepth01);

            if (severity01 > 0f)
            {
                affectedGroups += ApplyUnitFloodEffectsAtCell(
                    coord,
                    state,
                    severity01);
            }

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
                //$"[FloodUnitEffectResolver] Complete. " +
                //$"AffectedGroups={affectedGroups}, ActiveFloodDamageCells={activeFloodDamageCells.Count}");
        }

        processedGroupsThisPass.Clear();
        ClearUnitControlLookup();

        processRoutine = null;
    }

    private void ProcessFloodUnitEffectsImmediate()
    {
        processedGroupsThisPass.Clear();

        int affectedGroups = 0;

        for (int i = 0; i < activeFloodCellScratch.Count; i++)
        {
            TileCoord coord = activeFloodCellScratch[i];

            if (!floodSimulationSystem.TryGetFloodCell(coord, out FloodCellState state) || state == null)
                continue;

            float severity01 = GetDepthSeverity01(state.floodDepth01);

            if (severity01 > 0f)
            {
                affectedGroups += ApplyUnitFloodEffectsAtCell(
                    coord,
                    state,
                    severity01);
            }
        }

        if (debugLogging)
        {
            //Debug.Log(
                //$"[FloodUnitEffectResolver] Complete. " +
                //$"AffectedGroups={affectedGroups}, ActiveFloodDamageCells={activeFloodDamageCells.Count}");
        }

        processedGroupsThisPass.Clear();
        ClearUnitControlLookup();
    }

    private int ApplyUnitFloodEffectsAtCell(
        TileCoord coord,
        FloodCellState floodState,
        float severity01)
    {
        if (IsOutsideGrid(coord))
            return 0;

        long key = MakeGridKey(coord.x, coord.y);

        if (!unitControlsByCell.TryGetValue(key, out List<TileUnitGroupControl> controls) ||
            controls == null ||
            controls.Count == 0)
        {
            return 0;
        }

        int finalDamage = Mathf.RoundToInt(
            Mathf.Lerp(minUnitDamage, maxUnitDamage, severity01));

        if (finalDamage <= 0)
            return 0;

        int affectedGroups = 0;

        for (int i = 0; i < controls.Count; i++)
        {
            TileUnitGroupControl control = controls[i];

            if (control == null || !control.HasAnyGroups)
                continue;

            affectedGroups += ApplyEffectsToUnitControl(
                control,
                coord,
                floodState,
                finalDamage,
                severity01);
        }

        return affectedGroups;
    }

    private int ApplyEffectsToUnitControl(
        TileUnitGroupControl unitControl,
        TileCoord coord,
        FloodCellState floodState,
        int finalDamage,
        float severity01)
    {
        if (unitControl == null || !unitControl.HasAnyGroups)
            return 0;

        tmpUnitGroupSnapshot.Clear();

        IReadOnlyList<TileUnitGroupData> groups = unitControl.Groups;

        if (groups == null || groups.Count == 0)
            return 0;

        for (int i = 0; i < groups.Count; i++)
        {
            TileUnitGroupData group = groups[i];

            if (group != null)
                tmpUnitGroupSnapshot.Add(group);
        }

        int affectedGroups = 0;
        int currentTurn = TurnSystem.GetCurrentTurn();

        for (int i = 0; i < tmpUnitGroupSnapshot.Count; i++)
        {
            TileUnitGroupData group = tmpUnitGroupSnapshot[i];

            if (group == null || string.IsNullOrWhiteSpace(group.groupId))
                continue;

            if (processedGroupsThisPass.Contains(group.groupId))
                continue;

            if (affectEachUnitGroupOnlyOncePerTurn &&
                lastAffectedTurnByGroupId.TryGetValue(group.groupId, out int lastTurn) &&
                lastTurn == currentTurn)
            {
                continue;
            }

            processedGroupsThisPass.Add(group.groupId);

            ApplyDamageToUnitGroup(
                unitControl,
                group,
                coord,
                floodState,
                finalDamage,
                severity01);

            if (affectEachUnitGroupOnlyOncePerTurn)
                lastAffectedTurnByGroupId[group.groupId] = currentTurn;

            affectedGroups++;
        }

        tmpUnitGroupSnapshot.Clear();

        return affectedGroups;
    }

    private void ApplyDamageToUnitGroup(
        TileUnitGroupControl unitControl,
        TileUnitGroupData group,
        TileCoord coord,
        FloodCellState floodState,
        int finalDamage,
        float severity01)
    {
        if (unitControl == null || group == null || finalDamage <= 0)
            return;

        int oldUnitCount = Mathf.Max(0, group.unitCount);

        int unitsLost = group.ApplyDamageAndReturnUnitsLost(finalDamage);

        if (group.unitCount <= 0 || group.currentHealth <= 0)
        {
            ApplyPopulationLossFromUnitLoss(group, oldUnitCount, oldUnitCount);

            if (debugLogging)
            {
                //Debug.Log(
                    //$"[FloodUnitEffectResolver] Flood destroyed unit group {group.groupId} " +
                    //$"at ({coord.x},{coord.y}). " +
                    //$"Depth01={floodState.floodDepth01:0.00}, Severity={severity01:0.00}, Damage={finalDamage}"
                //);
            }

            unitControl.RemoveGroupDueToFatalities(group);
            return;
        }

        if (unitsLost > 0)
            ApplyPopulationLossFromUnitLoss(group, oldUnitCount, unitsLost);

        unitControl.RefreshMarker(group);

        if (debugLogging)
        {
            //Debug.Log(
                //$"[FloodUnitEffectResolver] Flood damaged unit group {group.groupId} " +
                //$"at ({coord.x},{coord.y}). " +
                //$"Depth01={floodState.floodDepth01:0.00}, Severity={severity01:0.00}, " +
                //$"Damage={finalDamage}, UnitsLost={unitsLost}, RemainingUnits={group.unitCount}"
            //);
        }
    }

    private float GetDepthSeverity01(float depth01)
    {
        float min = Mathf.Min(unitEffectsStartAtDepth01, severeUnitEffectsDepth01);
        float max = Mathf.Max(unitEffectsStartAtDepth01, severeUnitEffectsDepth01);

        return Mathf.Clamp01(Mathf.InverseLerp(min, max, depth01));
    }

    private void BuildUnitControlLookup()
    {
        ClearUnitControlLookup();

        PlayerUnitManager unitManager = PlayerUnitManager.Instance;

        if (unitManager == null)
            return;

        trackedGroupsScratch.Clear();
        unitManager.GetAllGroups(trackedGroupsScratch);

        if (trackedGroupsScratch.Count == 0)
            return;

        for (int i = 0; i < trackedGroupsScratch.Count; i++)
        {
            PlayerUnitManager.GroupInfo info = trackedGroupsScratch[i];

            TileUnitGroupControl owner = info.owner;
            TileUnitGroupData data = info.data;

            if (owner == null || data == null)
                continue;

            if (!owner.HasAnyGroups)
                continue;

            if (!owner.TryGetOwningGridPosition(out Vector2Int ownerGrid))
                continue;

            if (!IsCellInsideGrid(ownerGrid))
                continue;

            long key = MakeGridKey(ownerGrid.x, ownerGrid.y);
            List<TileUnitGroupControl> list = GetOrCreateControlListForCell(key);

            if (!list.Contains(owner))
                list.Add(owner);
        }
    }

    private List<TileUnitGroupControl> GetOrCreateControlListForCell(long key)
    {
        if (unitControlsByCell.TryGetValue(key, out List<TileUnitGroupControl> existing))
            return existing;

        List<TileUnitGroupControl> list;

        if (pooledControlLists.Count > 0)
        {
            int last = pooledControlLists.Count - 1;
            list = pooledControlLists[last];
            pooledControlLists.RemoveAt(last);
            list.Clear();
        }
        else
        {
            list = new List<TileUnitGroupControl>(4);
        }

        unitControlsByCell.Add(key, list);
        return list;
    }

    private void ClearUnitControlLookup()
    {
        foreach (KeyValuePair<long, List<TileUnitGroupControl>> pair in unitControlsByCell)
        {
            if (pair.Value == null)
                continue;

            pair.Value.Clear();
            pooledControlLists.Add(pair.Value);
        }

        unitControlsByCell.Clear();
    }

    private void ApplyPopulationLossFromUnitLoss(
        TileUnitGroupData group,
        int oldUnitCount,
        int unitsLost)
    {
        if (!applyPopulationLossFromUnitLoss)
            return;

        if (group == null)
            return;

        if (unitsLost <= 0 || oldUnitCount <= 0)
            return;

        if (string.IsNullOrWhiteSpace(group.populationReservationId) ||
            group.reservedPopulation <= 0)
        {
            return;
        }

        PlayersPopulationManager pop = PlayersPopulationManager.Instance;

        if (pop == null)
            return;

        int populationLoss = Mathf.Clamp(
            Mathf.RoundToInt(group.reservedPopulation * (unitsLost / (float)oldUnitCount)),
            0,
            group.reservedPopulation);

        if (populationLoss <= 0)
            return;

        pop.ApplyPenaltyFromReservation(group.populationReservationId, populationLoss);

        group.reservedPopulation = Mathf.Max(0, group.reservedPopulation - populationLoss);

        if (group.reservedPopulation <= 0)
        {
            pop.ReleaseReservation(group.populationReservationId);
            group.populationReservationId = null;
            group.reservedPopulation = 0;
        }
    }

    private bool IsOutsideGrid(TileCoord coord)
    {
        return gridManager == null ||
               coord.x < 0 ||
               coord.y < 0 ||
               coord.x >= gridManager.columns ||
               coord.y >= gridManager.rows;
    }

    private bool IsCellInsideGrid(Vector2Int cell)
    {
        return gridManager != null &&
               cell.x >= 0 &&
               cell.y >= 0 &&
               cell.x < gridManager.columns &&
               cell.y < gridManager.rows;
    }

    private static long MakeGridKey(int x, int y)
    {
        return ((long)x << 32) ^ (uint)y;
    }
}
