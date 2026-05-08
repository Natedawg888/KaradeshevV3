using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EarthquakeAnimalEffectResolver : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EarthquakeSimulationSystem simulationSystem;
    [SerializeField] private MapGenerator mapGenerator;
    [SerializeField] private GridManager gridManager;
    [SerializeField] private AnimalSimulation animalSimulation;

    [Header("Magnitude")]
    [SerializeField] private float animalEffectsStartAtMagnitude = 3.5f;
    [SerializeField] private float severeAnimalEffectsMagnitude = 8.0f;

    [Header("Fault Cell Scaling")]
    [Tooltip("Animal groups touching main fault cells receive the strongest threat.")]
    [Min(0f)]
    [SerializeField] private float faultCellThreatMultiplier = 1f;

    [Tooltip("Animal groups touching fault influence cells receive reduced threat.")]
    [Min(0f)]
    [SerializeField] private float influenceCellThreatMultiplier = 0.55f;

    [Tooltip("Animal groups inside quake radius but outside fault/influence cells receive this threat.")]
    [Min(0f)]
    [SerializeField] private float outsideFaultCellThreatMultiplier = 0.15f;

    [Tooltip("If true, animals outside fault/influence cells are ignored even if inside the quake radius.")]
    [SerializeField] private bool noAnimalEffectOutsideFaultInfluenceCells = false;

    [Header("Distance Falloff")]
    [SerializeField] private bool scaleByDistanceToEpicentre = true;

    [Range(0f, 1f)]
    [SerializeField] private float minimumDistanceMultiplierInsideRadius = 0.15f;

    [Header("Animal Flee")]
    [Range(0f, 1f)]
    [SerializeField] private float minFleeChance = 0.15f;

    [Range(0f, 1f)]
    [SerializeField] private float maxFleeChance = 0.85f;

    [Min(1)]
    [SerializeField] private int fleeSearchDistance = 2;

    [Header("If Flee Fails")]
    [SerializeField] private bool instantKillIfFleeFails = false;

    [Min(0)]
    [SerializeField] private int minDamageIfFleeFails = 2;

    [Min(0)]
    [SerializeField] private int maxDamageIfFleeFails = 20;

    [Header("Filtering")]
    [Range(0f, 1f)]
    [SerializeField] private float minThreatSeverityToAffect = 0.05f;

    [Tooltip("If true, flee tiles cannot be on main fault or influence cells.")]
    [SerializeField] private bool avoidFaultAndInfluenceCellsWhenFleeing = true;

    [Tooltip("If true, flee tiles cannot be inside any affected quake cell. This is stricter and can make fleeing harder.")]
    [SerializeField] private bool avoidAffectedCellsWhenFleeing = false;

    [Header("Over-Frame Processing")]
    [SerializeField] private bool processOverFrames = true;

    [Min(1)]
    [SerializeField] private int cellsProcessedPerFrame = 32;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = true;

    private readonly HashSet<Vector2Int> faultCells = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> influenceCells = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> affectedCells = new HashSet<Vector2Int>();

    private readonly List<int> groupIdsScratch = new List<int>(16);
    private readonly HashSet<int> processedGroupsThisPass = new HashSet<int>();

    private EarthquakeSimulationSystem subscribedSimulationSystem;
    private Coroutine processRoutine;

    private TileCoord currentQuakeCell;
    private bool hasCurrentQuakeCell;

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

        groupIdsScratch.Clear();
        processedGroupsThisPass.Clear();

        faultCells.Clear();
        influenceCells.Clear();
        affectedCells.Clear();

        hasCurrentQuakeCell = false;
    }

    public void InstallRuntimeRefs(
        EarthquakeSimulationSystem newSimulationSystem,
        MapGenerator newMapGenerator,
        GridManager newGridManager,
        AnimalSimulation newAnimalSimulation)
    {
        if (newSimulationSystem != null)
            simulationSystem = newSimulationSystem;

        if (newMapGenerator != null)
            mapGenerator = newMapGenerator;

        if (newGridManager != null)
            gridManager = newGridManager;

        if (newAnimalSimulation != null)
            animalSimulation = newAnimalSimulation;

        RebindEarthquakeEvents();

        if (debugLogging)
        {
            //Debug.Log(
                //$"EarthquakeAnimalEffectResolver: Installed refs. " +
                //$"Simulation={(simulationSystem != null ? simulationSystem.name : "NULL")}, " +
                //$"MapGenerator={(mapGenerator != null ? mapGenerator.name : "NULL")}, " +
                //$"GridManager={(gridManager != null ? gridManager.name : "NULL")}, "
            //);
        }
    }

    public void SetAnimalSimulation(AnimalSimulation newAnimalSimulation)
    {
        animalSimulation = newAnimalSimulation;
    }

    private void EnsureLinks()
    {
        if (simulationSystem == null)
            simulationSystem = FindObjectOfType<EarthquakeSimulationSystem>();

        if (mapGenerator == null)
            mapGenerator = FindObjectOfType<MapGenerator>();

        if (gridManager == null)
            gridManager = GridManager.Instance;

        if (animalSimulation == null)
            animalSimulation = AnimalSimulationAccess.Current;
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

        if (animalSimulation == null || mapGenerator == null || gridManager == null)
        {
            if (debugLogging) {}
                //Debug.LogWarning("EarthquakeAnimalEffectResolver: Missing references.");

            return;
        }

        if (processRoutine != null)
            StopCoroutine(processRoutine);

        BuildEarthquakeCellSets(data);

        if (processOverFrames)
            processRoutine = StartCoroutine(ProcessEarthquakeAnimalEffectsRoutine(data));
        else
            ProcessEarthquakeAnimalEffectsImmediate(data);
    }

    private IEnumerator ProcessEarthquakeAnimalEffectsRoutine(EarthquakeEventData data)
    {
        processedGroupsThisPass.Clear();

        int processedCells = 0;
        int maxPerFrame = Mathf.Max(1, cellsProcessedPerFrame);
        int affectedGroups = 0;

        float magnitude01 = GetMagnitude01(data.magnitude);

        if (magnitude01 <= 0f)
        {
            if (debugLogging) {}
                //Debug.Log($"EarthquakeAnimalEffectResolver: Magnitude {data.magnitude:0.0}, no animal effects.");

            processRoutine = null;
            yield break;
        }

        foreach (Vector2Int cell in affectedCells)
        {
            affectedGroups += ApplyAnimalEarthquakeThreatAtCell(cell, data, magnitude01);

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
                //$"EarthquakeAnimalEffectResolver complete. " +
                //$"AffectedGroups={affectedGroups}, " +
                //$"FaultCells={faultCells.Count}, InfluenceCells={influenceCells.Count}, AffectedCells={affectedCells.Count}"
            //);
        }

        processedGroupsThisPass.Clear();
        processRoutine = null;
    }

    private void ProcessEarthquakeAnimalEffectsImmediate(EarthquakeEventData data)
    {
        processedGroupsThisPass.Clear();

        int affectedGroups = 0;
        float magnitude01 = GetMagnitude01(data.magnitude);

        if (magnitude01 <= 0f)
        {
            if (debugLogging) {}
                //Debug.Log($"EarthquakeAnimalEffectResolver: Magnitude {data.magnitude:0.0}, no animal effects.");

            return;
        }

        foreach (Vector2Int cell in affectedCells)
            affectedGroups += ApplyAnimalEarthquakeThreatAtCell(cell, data, magnitude01);

        if (debugLogging)
        {
            //Debug.Log(
                //$"EarthquakeAnimalEffectResolver complete. " +
                //$"AffectedGroups={affectedGroups}, " +
                //$"FaultCells={faultCells.Count}, InfluenceCells={influenceCells.Count}, AffectedCells={affectedCells.Count}"
            //);
        }

        processedGroupsThisPass.Clear();
    }

    private int ApplyAnimalEarthquakeThreatAtCell(
        Vector2Int cell,
        EarthquakeEventData data,
        float magnitude01)
    {
        if (!IsCellInsideGrid(cell))
            return 0;

        TileCoord coord = new TileCoord(cell.x, cell.y);

        if (!animalSimulation.HasGroupsAtTile(coord))
            return 0;

        float severity = GetThreatSeverityAtCell(cell, data, magnitude01, out string zoneName);

        if (severity < minThreatSeverityToAffect)
            return 0;

        groupIdsScratch.Clear();

        int count = animalSimulation.GetGroupIdsAtTileNonAlloc(coord, groupIdsScratch);

        if (count <= 0)
            return 0;

        hasCurrentQuakeCell = true;
        currentQuakeCell = coord;

        int affected = 0;

        float fleeChance = Mathf.Lerp(minFleeChance, maxFleeChance, severity);
        int damageIfFleeFails = Mathf.RoundToInt(
            Mathf.Lerp(minDamageIfFleeFails, maxDamageIfFleeFails, severity)
        );

        for (int i = 0; i < groupIdsScratch.Count; i++)
        {
            int groupId = groupIdsScratch[i];

            if (processedGroupsThisPass.Contains(groupId))
                continue;

            processedGroupsThisPass.Add(groupId);

            bool changed = animalSimulation.TryApplyEarthquakeThreatToGroup(
                groupId,
                fleeChance,
                instantKillIfFleeFails,
                damageIfFleeFails,
                fleeSearchDistance,
                IsDangerousEarthquakeTile,
                IsValidFleeTile,
                debugLogging
            );

            if (changed)
                affected++;

            if (debugLogging)
            {
                //Debug.Log(
                    //$"Earthquake animal group {groupId} at {coord}. " +
                    //$"Zone={zoneName}, Severity={severity:0.00}, " +
                    //$"FleeChance={fleeChance:0.00}, DamageIfFail={damageIfFleeFails}"
                //);
            }
        }

        hasCurrentQuakeCell = false;

        return affected;
    }

    private float GetMagnitude01(float magnitude)
    {
        float min = Mathf.Min(animalEffectsStartAtMagnitude, severeAnimalEffectsMagnitude);
        float max = Mathf.Max(animalEffectsStartAtMagnitude, severeAnimalEffectsMagnitude);

        return Mathf.Clamp01(Mathf.InverseLerp(min, max, magnitude));
    }

    private float GetThreatSeverityAtCell(
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
            return faultCellThreatMultiplier;
        }

        if (influenceCells.Contains(cell))
        {
            zoneName = "Influence Cells";
            return influenceCellThreatMultiplier;
        }

        if (affectedCells.Contains(cell))
        {
            zoneName = "Affected Radius Cells";

            if (noAnimalEffectOutsideFaultInfluenceCells)
                return 0f;

            return outsideFaultCellThreatMultiplier;
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

    private bool IsDangerousEarthquakeTile(TileCoord coord)
    {
        Vector2Int cell = new Vector2Int(coord.x, coord.y);

        if (hasCurrentQuakeCell && coord.Equals(currentQuakeCell))
            return true;

        if (avoidFaultAndInfluenceCellsWhenFleeing)
        {
            if (faultCells.Contains(cell))
                return true;

            if (influenceCells.Contains(cell))
                return true;
        }

        if (avoidAffectedCellsWhenFleeing && affectedCells.Contains(cell))
            return true;

        return false;
    }

    private bool IsValidFleeTile(TileCoord coord)
    {
        if (IsOutsideGrid(coord))
            return false;

        if (IsDangerousEarthquakeTile(coord))
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

    private bool IsCellInsideGrid(Vector2Int cell)
    {
        return gridManager != null &&
               cell.x >= 0 &&
               cell.y >= 0 &&
               cell.x < gridManager.columns &&
               cell.y < gridManager.rows;
    }
}
