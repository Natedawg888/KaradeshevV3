using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EarthquakeUnitEffectResolver : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EarthquakeSimulationSystem simulationSystem;
    [SerializeField] private MapGenerator mapGenerator;
    [SerializeField] private GridManager gridManager;

    [Header("Magnitude")]
    [SerializeField] private float unitEffectsStartAtMagnitude = 4.0f;
    [SerializeField] private float severeUnitEffectsMagnitude = 8.0f;

    [Header("Fault Cell Scaling")]
    [Tooltip("Unit groups touching main fault cells receive the strongest damage.")]
    [Min(0f)]
    [SerializeField] private float faultCellDamageMultiplier = 1f;

    [Tooltip("Unit groups touching fault influence cells receive reduced damage.")]
    [Min(0f)]
    [SerializeField] private float influenceCellDamageMultiplier = 0.55f;

    [Tooltip("Unit groups inside quake radius but outside fault/influence cells receive this multiplier.")]
    [Min(0f)]
    [SerializeField] private float outsideFaultCellDamageMultiplier = 0.15f;

    [Tooltip("If true, units outside fault/influence cells are ignored even if inside the earthquake radius.")]
    [SerializeField] private bool noUnitEffectOutsideFaultInfluenceCells = false;

    [Header("Distance Falloff")]
    [SerializeField] private bool scaleByDistanceToEpicentre = true;

    [Range(0f, 1f)]
    [SerializeField] private float minimumDistanceMultiplierInsideRadius = 0.15f;

    [Header("Damage")]
    [Min(0)]
    [SerializeField] private int minUnitDamage = 2;

    [Min(0)]
    [SerializeField] private int maxUnitDamage = 28;

    [Tooltip("If true, every unit group can only be damaged once per earthquake event.")]
    [SerializeField] private bool affectEachUnitGroupOnlyOncePerEarthquake = true;

    [Header("Population")]
    [SerializeField] private bool applyPopulationLossFromUnitLoss = true;

    [Header("Over-Frame Processing")]
    [SerializeField] private bool processOverFrames = true;

    [Min(1)]
    [SerializeField] private int cellsProcessedPerFrame = 32;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = true;

    private readonly HashSet<Vector2Int> faultCells = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> influenceCells = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> affectedCells = new HashSet<Vector2Int>();

    private readonly List<PlayerUnitManager.GroupInfo> trackedGroupsScratch =
        new List<PlayerUnitManager.GroupInfo>(128);

    private readonly List<TileUnitGroupData> tmpUnitGroupSnapshot =
        new List<TileUnitGroupData>(16);

    private readonly List<TileUnitGroupControl> unitControlsAtTileScratch =
        new List<TileUnitGroupControl>(8);

    private readonly HashSet<TileUnitGroupControl> uniqueUnitControlsAtTileScratch =
        new HashSet<TileUnitGroupControl>();

    private readonly HashSet<string> processedGroupsThisPass =
        new HashSet<string>();

    private readonly Dictionary<long, List<TileUnitGroupControl>> unitControlsByCell =
        new Dictionary<long, List<TileUnitGroupControl>>();

    private readonly List<List<TileUnitGroupControl>> pooledControlLists =
        new List<List<TileUnitGroupControl>>();

    private EarthquakeSimulationSystem subscribedSimulationSystem;
    private Coroutine processRoutine;

    private void Awake()
    {
        EnsureLinks();
    }

    private void OnEnable()
    {
        EnsureLinks();
        RebindEarthquakeEvents();
    }

    private void Start()
    {
        EnsureLinks();
        RebindEarthquakeEvents();
    }

    private void OnDisable()
    {
        UnbindEarthquakeEvents();

        if (processRoutine != null)
        {
            StopCoroutine(processRoutine);
            processRoutine = null;
        }

        faultCells.Clear();
        influenceCells.Clear();
        affectedCells.Clear();

        trackedGroupsScratch.Clear();
        tmpUnitGroupSnapshot.Clear();
        unitControlsAtTileScratch.Clear();
        uniqueUnitControlsAtTileScratch.Clear();
        processedGroupsThisPass.Clear();

        ClearUnitControlLookup();
    }

    public void InstallRuntimeRefs(
        EarthquakeSimulationSystem newSimulationSystem,
        MapGenerator newMapGenerator,
        GridManager newGridManager)
    {
        if (newSimulationSystem != null)
            simulationSystem = newSimulationSystem;

        if (newMapGenerator != null)
            mapGenerator = newMapGenerator;

        if (newGridManager != null)
            gridManager = newGridManager;

        RebindEarthquakeEvents();

        if (debugLogging)
        {
            Debug.Log(
                $"EarthquakeUnitEffectResolver: Installed refs. " +
                $"Simulation={(simulationSystem != null ? simulationSystem.name : "NULL")}, " +
                $"MapGenerator={(mapGenerator != null ? mapGenerator.name : "NULL")}, " +
                $"GridManager={(gridManager != null ? gridManager.name : "NULL")}"
            );
        }
    }

    private void EnsureLinks()
    {
        if (simulationSystem == null)
            simulationSystem = FindObjectOfType<EarthquakeSimulationSystem>();

        if (mapGenerator == null)
            mapGenerator = FindObjectOfType<MapGenerator>();

        if (gridManager == null)
            gridManager = GridManager.Instance;
    }

    private void RebindEarthquakeEvents()
    {
        if (subscribedSimulationSystem == simulationSystem)
            return;

        UnbindEarthquakeEvents();

        subscribedSimulationSystem = simulationSystem;

        if (subscribedSimulationSystem != null)
            subscribedSimulationSystem.OnEarthquake += HandleEarthquake;
    }

    private void UnbindEarthquakeEvents()
    {
        if (subscribedSimulationSystem == null)
            return;

        subscribedSimulationSystem.OnEarthquake -= HandleEarthquake;
        subscribedSimulationSystem = null;
    }

    private void HandleEarthquake(EarthquakeEventData data)
    {
        EnsureLinks();

        if (data == null)
            return;

        if (mapGenerator == null || gridManager == null)
        {
            if (debugLogging)
                Debug.LogWarning("EarthquakeUnitEffectResolver: Missing MapGenerator or GridManager.");

            return;
        }

        if (processRoutine != null)
            StopCoroutine(processRoutine);

        BuildEarthquakeCellSets(data);
        BuildUnitControlLookup();

        if (unitControlsByCell.Count == 0)
        {
            ClearUnitControlLookup();
            return;
        }

        if (processOverFrames)
            processRoutine = StartCoroutine(ProcessEarthquakeUnitEffectsRoutine(data));
        else
            ProcessEarthquakeUnitEffectsImmediate(data);
    }

    private IEnumerator ProcessEarthquakeUnitEffectsRoutine(EarthquakeEventData data)
    {
        processedGroupsThisPass.Clear();

        int processedCells = 0;
        int maxPerFrame = Mathf.Max(1, cellsProcessedPerFrame);
        int affectedGroups = 0;

        float magnitude01 = GetMagnitude01(data.magnitude);

        if (magnitude01 <= 0f)
        {
            if (debugLogging)
                Debug.Log($"EarthquakeUnitEffectResolver: Magnitude {data.magnitude:0.0}, no unit effects.");

            processedGroupsThisPass.Clear();
            ClearUnitControlLookup();

            processRoutine = null;
            yield break;
        }

        foreach (Vector2Int cell in affectedCells)
        {
            affectedGroups += ApplyUnitEarthquakeEffectsAtCell(cell, data, magnitude01);

            processedCells++;

            if (processedCells >= maxPerFrame)
            {
                processedCells = 0;
                yield return null;
            }
        }

        if (debugLogging)
        {
            Debug.Log(
                $"EarthquakeUnitEffectResolver complete. " +
                $"AffectedGroups={affectedGroups}, " +
                $"FaultCells={faultCells.Count}, InfluenceCells={influenceCells.Count}, AffectedCells={affectedCells.Count}"
            );
        }

        processedGroupsThisPass.Clear();
        ClearUnitControlLookup();

        processRoutine = null;
    }

    private void ProcessEarthquakeUnitEffectsImmediate(EarthquakeEventData data)
    {
        processedGroupsThisPass.Clear();

        int affectedGroups = 0;
        float magnitude01 = GetMagnitude01(data.magnitude);

        if (magnitude01 <= 0f)
        {
            if (debugLogging)
                Debug.Log($"EarthquakeUnitEffectResolver: Magnitude {data.magnitude:0.0}, no unit effects.");

            processedGroupsThisPass.Clear();
            ClearUnitControlLookup();
            return;
        }

        foreach (Vector2Int cell in affectedCells)
            affectedGroups += ApplyUnitEarthquakeEffectsAtCell(cell, data, magnitude01);

        if (debugLogging)
        {
            Debug.Log(
                $"EarthquakeUnitEffectResolver complete. " +
                $"AffectedGroups={affectedGroups}, " +
                $"FaultCells={faultCells.Count}, InfluenceCells={influenceCells.Count}, AffectedCells={affectedCells.Count}"
            );
        }

        processedGroupsThisPass.Clear();
        ClearUnitControlLookup();
    }

    private int ApplyUnitEarthquakeEffectsAtCell(
        Vector2Int cell,
        EarthquakeEventData data,
        float magnitude01)
    {
        if (!IsCellInsideGrid(cell))
            return 0;

        long key = MakeGridKey(cell.x, cell.y);

        if (!unitControlsByCell.TryGetValue(key, out List<TileUnitGroupControl> controls) ||
            controls == null ||
            controls.Count == 0)
        {
            return 0;
        }

        float severity = GetDamageSeverityAtCell(cell, data, magnitude01, out string zoneName);

        if (severity <= 0f)
            return 0;

        int finalDamage = Mathf.RoundToInt(
            Mathf.Lerp(minUnitDamage, maxUnitDamage, severity)
        );

        if (finalDamage <= 0)
            return 0;

        TileCoord coord = new TileCoord(cell.x, cell.y);
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
                zoneName,
                severity
            );
        }

        return affectedGroups;
    }

    private int ApplyEffectsToUnitControl(
        TileUnitGroupControl unitControl,
        TileCoord coord,
        int finalDamage,
        string zoneName,
        float severity)
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

            if (affectEachUnitGroupOnlyOncePerEarthquake &&
                processedGroupsThisPass.Contains(group.groupId))
                continue;

            if (affectEachUnitGroupOnlyOncePerEarthquake)
                processedGroupsThisPass.Add(group.groupId);

            ApplyDamageToUnitGroup(
                unitControl,
                group,
                coord,
                finalDamage,
                zoneName,
                severity
            );

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
        string zoneName,
        float severity)
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
                Debug.Log(
                    $"[EarthquakeUnitEffectResolver] Earthquake destroyed unit group {group.groupId} " +
                    $"at ({coord.x},{coord.y}). Zone={zoneName}, Severity={severity:0.00}, Damage={finalDamage}"
                );
            }

            unitControl.RemoveGroupDueToFatalities(group);
            return;
        }

        if (unitsLost > 0)
            ApplyPopulationLossFromUnitLoss(group, oldUnitCount, unitsLost);

        unitControl.RefreshMarker(group);

        if (debugLogging)
        {
            Debug.Log(
                $"[EarthquakeUnitEffectResolver] Earthquake damaged unit group {group.groupId} " +
                $"at ({coord.x},{coord.y}). Zone={zoneName}, Severity={severity:0.00}, " +
                $"Damage={finalDamage}, UnitsLost={unitsLost}, RemainingUnits={group.unitCount}"
            );
        }
    }

    private float GetMagnitude01(float magnitude)
    {
        float min = Mathf.Min(unitEffectsStartAtMagnitude, severeUnitEffectsMagnitude);
        float max = Mathf.Max(unitEffectsStartAtMagnitude, severeUnitEffectsMagnitude);

        return Mathf.Clamp01(Mathf.InverseLerp(min, max, magnitude));
    }

    private float GetDamageSeverityAtCell(
        Vector2Int cell,
        EarthquakeEventData data,
        float magnitude01,
        out string zoneName)
    {
        float zoneMultiplier = GetZoneMultiplier(cell, out zoneName);

        if (zoneMultiplier <= 0f)
            return 0f;

        float distanceMultiplier = 1f;

        if (scaleByDistanceToEpicentre)
        {
            Vector2Int block = mapGenerator.GetBlockFromCell(cell);

            float distanceBlocks = Vector2Int.Distance(data.epicentreBlock, block);
            float radiusBlocks = Mathf.Max(0.001f, data.radiusBlocks);

            float distance01 = Mathf.Clamp01(distanceBlocks / radiusBlocks);
            distanceMultiplier = 1f - distance01;
            distanceMultiplier = Mathf.Max(minimumDistanceMultiplierInsideRadius, distanceMultiplier);
        }

        return Mathf.Clamp01(magnitude01 * zoneMultiplier * distanceMultiplier);
    }

    private float GetZoneMultiplier(Vector2Int cell, out string zoneName)
    {
        if (faultCells.Contains(cell))
        {
            zoneName = "Main Fault Cells";
            return faultCellDamageMultiplier;
        }

        if (influenceCells.Contains(cell))
        {
            zoneName = "Influence Cells";
            return influenceCellDamageMultiplier;
        }

        if (affectedCells.Contains(cell))
        {
            zoneName = "Affected Radius Cells";

            if (noUnitEffectOutsideFaultInfluenceCells)
                return 0f;

            return outsideFaultCellDamageMultiplier;
        }

        zoneName = "Outside Earthquake";
        return 0f;
    }

    private void BuildEarthquakeCellSets(EarthquakeEventData data)
    {
        faultCells.Clear();
        influenceCells.Clear();
        affectedCells.Clear();

        AddCellsFromBlocks(data.faultBlocks, faultCells);
        AddCellsFromBlocks(data.faultInfluenceBlocks, influenceCells);
        AddCellsFromBlocks(data.affectedBlocks, affectedCells);

        foreach (Vector2Int cell in faultCells)
            influenceCells.Remove(cell);
    }

    private void AddCellsFromBlocks(IEnumerable<Vector2Int> blocks, HashSet<Vector2Int> results)
    {
        if (blocks == null || results == null)
            return;

        int blockSize = Mathf.Max(1, mapGenerator.blockSize);

        foreach (Vector2Int block in blocks)
        {
            if (!mapGenerator.IsValidBlock(block))
                continue;

            Vector2Int min = mapGenerator.GetBlockMinCell(block);

            for (int x = 0; x < blockSize; x++)
            {
                for (int y = 0; y < blockSize; y++)
                {
                    Vector2Int cell = new Vector2Int(min.x + x, min.y + y);

                    if (!IsCellInsideGrid(cell))
                        continue;

                    results.Add(cell);
                }
            }
        }
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

        if (string.IsNullOrWhiteSpace(group.populationReservationId) || group.reservedPopulation <= 0)
            return;

        PlayersPopulationManager pop = PlayersPopulationManager.Instance;
        if (pop == null)
            return;

        int populationLoss = Mathf.Clamp(
            Mathf.RoundToInt(group.reservedPopulation * (unitsLost / (float)oldUnitCount)),
            0,
            group.reservedPopulation
        );

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