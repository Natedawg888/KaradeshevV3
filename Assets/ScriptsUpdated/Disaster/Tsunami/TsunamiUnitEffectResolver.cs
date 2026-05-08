using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TsunamiUnitEffectResolver : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TsunamiSimulationSystem simulationSystem;
    [SerializeField] private GridManager gridManager;

    [Header("Energy")]
    [Tooltip("Tsunami energy01 must be at or above this before units are affected.")]
    [Range(0f, 1f)]
    [SerializeField] private float unitEffectsStartAtEnergy01 = 0.10f;

    [Tooltip("Energy01 at or above this uses maximum unit damage.")]
    [Range(0f, 1f)]
    [SerializeField] private float severeUnitEffectsEnergy01 = 1f;

    [Header("Damage")]
    [Min(0)]
    [SerializeField] private int minUnitDamage = 2;

    [Min(0)]
    [SerializeField] private int maxUnitDamage = 32;

    [Tooltip("If true, every unit group can only be damaged once per tsunami advance step.")]
    [SerializeField] private bool affectEachUnitGroupOnlyOncePerStep = true;

    [Header("Population")]
    [SerializeField] private bool applyPopulationLossFromUnitLoss = true;

    [Header("Over-Frame Processing")]
    [SerializeField] private bool processOverFrames = true;

    [Min(1)]
    [SerializeField] private int cellsProcessedPerFrame = 32;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = true;

    private TsunamiSimulationSystem subscribedSimulationSystem;
    private Coroutine processRoutine;

    private readonly HashSet<TileCoord> activeTsunamiCells =
        new HashSet<TileCoord>();

    private readonly List<PlayerUnitManager.GroupInfo> trackedGroupsScratch =
        new List<PlayerUnitManager.GroupInfo>(128);

    private readonly List<TileUnitGroupData> tmpUnitGroupSnapshot =
        new List<TileUnitGroupData>(16);

    private readonly HashSet<string> processedGroupsThisPass =
        new HashSet<string>();

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
        RebindTsunamiEvents();
    }

    private void Start()
    {
        EnsureLinks();
        RebindTsunamiEvents();
    }

    private void OnDisable()
    {
        UnbindTsunamiEvents();

        if (processRoutine != null)
        {
            StopCoroutine(processRoutine);
            processRoutine = null;
        }

        activeTsunamiCells.Clear();
        trackedGroupsScratch.Clear();
        tmpUnitGroupSnapshot.Clear();
        processedGroupsThisPass.Clear();

        ClearUnitControlLookup();
    }

    public void InstallRuntimeRefs(
        TsunamiSimulationSystem newSimulationSystem,
        GridManager newGridManager)
    {
        if (newSimulationSystem != null)
            simulationSystem = newSimulationSystem;

        if (newGridManager != null)
            gridManager = newGridManager;

        RebindTsunamiEvents();

        if (debugLogging)
        {
            //Debug.Log(
                //$"[TsunamiUnitEffectResolver] Installed refs. " +
                //$"Simulation={(simulationSystem != null ? simulationSystem.name : "NULL")}, " +
                //$"GridManager={(gridManager != null ? gridManager.name : "NULL")}"
            //);
        }
    }

    private void EnsureLinks()
    {
        if (simulationSystem == null)
            simulationSystem = TsunamiSimulationSystem.Instance;

        if (simulationSystem == null)
            simulationSystem = FindObjectOfType<TsunamiSimulationSystem>();

        if (gridManager == null)
            gridManager = GridManager.Instance;
    }

    private void RebindTsunamiEvents()
    {
        if (subscribedSimulationSystem == simulationSystem)
            return;

        UnbindTsunamiEvents();

        subscribedSimulationSystem = simulationSystem;

        if (subscribedSimulationSystem != null)
            subscribedSimulationSystem.OnTsunamiAdvanced += HandleTsunamiAdvanced;
    }

    private void UnbindTsunamiEvents()
    {
        if (subscribedSimulationSystem == null)
            return;

        subscribedSimulationSystem.OnTsunamiAdvanced -= HandleTsunamiAdvanced;
        subscribedSimulationSystem = null;
    }

    private void HandleTsunamiAdvanced(TsunamiAdvancedEventData data)
    {
        EnsureLinks();

        if (data == null || data.activeCells == null || data.activeCells.Count == 0)
            return;

        if (gridManager == null)
        {
            if (debugLogging) {}
                //Debug.LogWarning("[TsunamiUnitEffectResolver] Missing GridManager.");

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

        activeTsunamiCells.Clear();

        for (int i = 0; i < data.activeCells.Count; i++)
            activeTsunamiCells.Add(data.activeCells[i]);

        if (processOverFrames)
            processRoutine = StartCoroutine(ProcessTsunamiUnitEffectsRoutine(data));
        else
            ProcessTsunamiUnitEffectsImmediate(data);
    }

    private IEnumerator ProcessTsunamiUnitEffectsRoutine(TsunamiAdvancedEventData data)
    {
        processedGroupsThisPass.Clear();

        int processedCells = 0;
        int maxPerFrame = Mathf.Max(1, cellsProcessedPerFrame);
        int affectedGroups = 0;

        float severity01 = GetEnergySeverity01(data.energy01);

        if (severity01 <= 0f)
        {
            if (debugLogging)
            {
                //Debug.Log(
                    //$"[TsunamiUnitEffectResolver] Energy too low for unit effects. " +
                    //$"TsunamiId={data.tsunamiId}, Energy01={data.energy01:0.00}");
            }

            processedGroupsThisPass.Clear();
            ClearUnitControlLookup();

            processRoutine = null;
            yield break;
        }

        for (int i = 0; i < data.activeCells.Count; i++)
        {
            TileCoord coord = data.activeCells[i];

            affectedGroups += ApplyUnitTsunamiEffectsAtCell(
                coord,
                data,
                severity01);

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
                //$"[TsunamiUnitEffectResolver] Complete. " +
                //$"TsunamiId={data.tsunamiId}, Step={data.stepCount}, " +
                //$"AffectedGroups={affectedGroups}, ActiveCells={activeTsunamiCells.Count}");
        }

        processedGroupsThisPass.Clear();
        ClearUnitControlLookup();

        processRoutine = null;
    }

    private void ProcessTsunamiUnitEffectsImmediate(TsunamiAdvancedEventData data)
    {
        processedGroupsThisPass.Clear();

        int affectedGroups = 0;
        float severity01 = GetEnergySeverity01(data.energy01);

        if (severity01 <= 0f)
        {
            if (debugLogging)
            {
                //Debug.Log(
                    //$"[TsunamiUnitEffectResolver] Energy too low for unit effects. " +
                    //$"TsunamiId={data.tsunamiId}, Energy01={data.energy01:0.00}");
            }

            processedGroupsThisPass.Clear();
            ClearUnitControlLookup();
            return;
        }

        for (int i = 0; i < data.activeCells.Count; i++)
        {
            affectedGroups += ApplyUnitTsunamiEffectsAtCell(
                data.activeCells[i],
                data,
                severity01);
        }

        if (debugLogging)
        {
            //Debug.Log(
                //$"[TsunamiUnitEffectResolver] Complete. " +
                //$"TsunamiId={data.tsunamiId}, Step={data.stepCount}, " +
                //$"AffectedGroups={affectedGroups}, ActiveCells={activeTsunamiCells.Count}");
        }

        processedGroupsThisPass.Clear();
        ClearUnitControlLookup();
    }

    private int ApplyUnitTsunamiEffectsAtCell(
        TileCoord coord,
        TsunamiAdvancedEventData data,
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
                finalDamage,
                data,
                severity01);
        }

        return affectedGroups;
    }

    private int ApplyEffectsToUnitControl(
        TileUnitGroupControl unitControl,
        TileCoord coord,
        int finalDamage,
        TsunamiAdvancedEventData data,
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

        for (int i = 0; i < tmpUnitGroupSnapshot.Count; i++)
        {
            TileUnitGroupData group = tmpUnitGroupSnapshot[i];

            if (group == null || string.IsNullOrWhiteSpace(group.groupId))
                continue;

            if (affectEachUnitGroupOnlyOncePerStep &&
                processedGroupsThisPass.Contains(group.groupId))
            {
                continue;
            }

            if (affectEachUnitGroupOnlyOncePerStep)
                processedGroupsThisPass.Add(group.groupId);

            ApplyDamageToUnitGroup(
                unitControl,
                group,
                coord,
                finalDamage,
                data,
                severity01);

            affectedGroups++;
        }

        tmpUnitGroupSnapshot.Clear();

        return affectedGroups;
    }

    private void ApplyDamageToUnitGroup(
        TileUnitGroupControl unitControl,
        TileUnitGroupData group,
        TileCoord coord,
        int finalDamage,
        TsunamiAdvancedEventData data,
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
                    //$"[TsunamiUnitEffectResolver] Tsunami destroyed unit group {group.groupId} " +
                    //$"at ({coord.x},{coord.y}). " +
                    //$"TsunamiId={data.tsunamiId}, Step={data.stepCount}, " +
                    //$"Energy01={data.energy01:0.00}, Severity={severity01:0.00}, Damage={finalDamage}"
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
                //$"[TsunamiUnitEffectResolver] Tsunami damaged unit group {group.groupId} " +
                //$"at ({coord.x},{coord.y}). " +
                //$"TsunamiId={data.tsunamiId}, Step={data.stepCount}, " +
                //$"Energy01={data.energy01:0.00}, Severity={severity01:0.00}, " +
                //$"Damage={finalDamage}, UnitsLost={unitsLost}, RemainingUnits={group.unitCount}"
            //);
        }
    }

    private float GetEnergySeverity01(float energy01)
    {
        float min = Mathf.Min(unitEffectsStartAtEnergy01, severeUnitEffectsEnergy01);
        float max = Mathf.Max(unitEffectsStartAtEnergy01, severeUnitEffectsEnergy01);

        return Mathf.Clamp01(Mathf.InverseLerp(min, max, energy01));
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
