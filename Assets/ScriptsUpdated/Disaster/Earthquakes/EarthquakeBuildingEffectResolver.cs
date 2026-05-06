using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EarthquakeBuildingEffectResolver : MonoBehaviour
{
    [Header("References")]
    public EarthquakeSimulationSystem simulationSystem;
    public MapGenerator mapGenerator;
    public GridManager gridManager;
    public WorldBuildingManager worldBuildingManager;

    [Header("Damage")]
    public bool damageBuildings = true;

    public float damageStartsAtMagnitude = 4.5f;
    public float severeDamageMagnitude = 8.0f;

    public int minDamage = 1;
    public int maxDamage = 35;

    [Header("Fault Cell Scaling")]
    [Tooltip("Buildings touching cells covered by main fault blocks receive this damage multiplier.")]
    [Min(0f)] public float faultCellDamageMultiplier = 1f;

    [Tooltip("Buildings touching cells covered by fault influence blocks receive this damage multiplier.")]
    [Min(0f)] public float influenceCellDamageMultiplier = 0.55f;

    [Tooltip("Buildings inside quake radius but outside fault/influence cells receive this multiplier.")]
    [Min(0f)] public float outsideFaultCellDamageMultiplier = 0.15f;

    [Tooltip("If true, buildings outside fault/influence cells take no damage even if inside the quake radius.")]
    public bool noDamageOutsideFaultInfluenceCells = false;

    [Header("Distance Falloff")]
    public bool scaleByDistanceToEpicentre = true;

    [Range(0f, 1f)]
    public float minimumDistanceMultiplierInsideRadius = 0.15f;

    [Header("Earthquake Ignition")]
    public bool earthquakeCanIgniteBuildings = true;

    [Tooltip("If true, only buildings with BuildingEarthquakeResistance.canIgniteFromEarthquake can ignite.")]
    public bool requireEarthquakeIgnitionOptIn = true;

    [Tooltip("If true, the building must take earthquake damage before ignition is attempted.")]
    public bool requireEarthquakeDamageBeforeIgnition = true;

    [Tooltip("Earthquakes below this magnitude cannot ignite buildings.")]
    public float ignitionStartsAtMagnitude = 5.5f;

    [Tooltip("Earthquakes at or above this magnitude use max ignition strength.")]
    public float severeIgnitionMagnitude = 8.0f;

    [Tooltip("Lowest ignition chance after the earthquake passes the magnitude threshold.")]
    [Range(0f, 1f)] public float minEarthquakeIgnitionChance = 0.01f;

    [Tooltip("Highest ignition chance for severe quakes on strong fault cells.")]
    [Range(0f, 1f)] public float maxEarthquakeIgnitionChance = 0.22f;

    [Tooltip("Minimum burn duration if a building ignites.")]
    [Min(1)] public int minEarthquakeBurnTurns = 2;

    [Tooltip("Maximum burn duration if a building ignites.")]
    [Min(1)] public int maxEarthquakeBurnTurns = 5;

    public bool debugIgnitionLogging = true;

    [Header("Building Footprint")]
    [Tooltip("If true, use building bounds/colliders to find every grid cell the building covers.")]
    public bool useBuildingFootprintCells = true;

    [Tooltip("Small shrink amount for bounds checks so touching edges don't over-count cells.")]
    [Range(0f, 0.45f)] public float boundsCellInset = 0.08f;

    [Header("Processing")]
    [Min(1)] public int buildingsProcessedPerFrame = 16;

    [Header("Messages")]
    public string damageMessageName = "ApplyEarthquakeDamage";
    public string hitMessageName = "OnEarthquakeHit";

    public bool sendHitMessageEvenWhenNoDamage = false;

    [Header("Debug")]
    public bool debugLogging = true;

    private Coroutine resolverRoutine;

    private readonly HashSet<Vector2Int> faultCells = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> influenceCells = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> affectedCells = new HashSet<Vector2Int>();

    private readonly List<Vector2Int> buildingCellsScratch = new List<Vector2Int>();

    private EarthquakeSimulationSystem subscribedSimulationSystem;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        RebindSimulationSubscription();
    }

    private void OnDisable()
    {
        UnbindSimulationSubscription();
    }

    public void InstallRuntimeRefs(
    EarthquakeSimulationSystem newSimulationSystem,
    MapGenerator newMapGenerator,
    GridManager newGridManager,
    WorldBuildingManager newWorldBuildingManager)
    {
        if (newSimulationSystem != null)
            simulationSystem = newSimulationSystem;

        if (newMapGenerator != null)
            mapGenerator = newMapGenerator;

        if (newGridManager != null)
            gridManager = newGridManager;

        if (newWorldBuildingManager != null)
            worldBuildingManager = newWorldBuildingManager;

        RebindSimulationSubscription();

        if (debugLogging)
        {
            Debug.Log(
                $"EarthquakeBuildingEffectResolver: Installed refs. " +
                $"Simulation={(simulationSystem != null ? simulationSystem.name : "NULL")}, " +
                $"MapGenerator={(mapGenerator != null ? mapGenerator.name : "NULL")}, " +
                $"GridManager={(gridManager != null ? gridManager.name : "NULL")}, " +
                $"WorldBuildingManager={(worldBuildingManager != null ? worldBuildingManager.name : "NULL")}"
            );
        }
    }

    public void SetWorldBuildingManager(WorldBuildingManager newWorldBuildingManager)
    {
        if (worldBuildingManager == newWorldBuildingManager)
            return;

        worldBuildingManager = newWorldBuildingManager;

        if (debugLogging)
        {
            Debug.Log(
                $"EarthquakeBuildingEffectResolver: SetWorldBuildingManager -> " +
                $"{(worldBuildingManager != null ? worldBuildingManager.name : "NULL")}"
            );
        }
    }

    private void RebindSimulationSubscription()
    {
        if (subscribedSimulationSystem == simulationSystem)
            return;

        UnbindSimulationSubscription();

        subscribedSimulationSystem = simulationSystem;

        if (subscribedSimulationSystem != null)
            subscribedSimulationSystem.OnEarthquake += HandleEarthquake;
    }

    private void UnbindSimulationSubscription()
    {
        if (subscribedSimulationSystem == null)
            return;

        subscribedSimulationSystem.OnEarthquake -= HandleEarthquake;
        subscribedSimulationSystem = null;
    }

    private void HandleEarthquake(EarthquakeEventData data)
    {
        if (!damageBuildings)
            return;

        if (resolverRoutine != null)
            StopCoroutine(resolverRoutine);

        resolverRoutine = StartCoroutine(ProcessBuildings(data));
    }

    private IEnumerator ProcessBuildings(EarthquakeEventData data)
    {
        ResolveReferences();

        if (mapGenerator == null || gridManager == null || worldBuildingManager == null)
        {
            if (debugLogging)
                Debug.LogWarning("EarthquakeBuildingEffectResolver: Missing references.");

            resolverRoutine = null;
            yield break;
        }

        IReadOnlyList<WorldBuildingManager.Record> records = worldBuildingManager.GetAll();

        if (records == null || records.Count == 0)
        {
            resolverRoutine = null;
            yield break;
        }

        BuildEarthquakeCellSets(data);

        float magnitude01 = Mathf.InverseLerp(
            damageStartsAtMagnitude,
            severeDamageMagnitude,
            data.magnitude
        );

        magnitude01 = Mathf.Clamp01(magnitude01);

        if (magnitude01 <= 0f)
        {
            if (debugLogging)
                Debug.Log($"EarthquakeBuildingEffectResolver: Magnitude {data.magnitude:0.0}, no building damage.");

            resolverRoutine = null;
            yield break;
        }

        int processed = 0;
        int damagedCount = 0;
        int skippedOutsideAffectedCells = 0;
        int skippedOutsideFaultInfluence = 0;

        for (int i = 0; i < records.Count; i++)
        {
            WorldBuildingManager.Record record = records[i];

            if (record == null || record.instance == null)
                continue;

            GameObject building = record.instance;

            GetBuildingCoveredCells(record, buildingCellsScratch);

            if (buildingCellsScratch.Count == 0)
                continue;

            if (!AnyCellInSet(buildingCellsScratch, affectedCells))
            {
                skippedOutsideAffectedCells++;
                continue;
            }

            float zoneMultiplier = GetCellZoneMultiplier(buildingCellsScratch, out string zoneName);

            if (zoneMultiplier <= 0f)
            {
                skippedOutsideFaultInfluence++;
                continue;
            }

            float distanceMultiplier = 1f;

            if (scaleByDistanceToEpicentre)
            {
                Vector2Int closestBlock = GetClosestBuildingBlockToEpicentre(buildingCellsScratch, data.epicentreBlock);

                float distanceBlocks = Vector2Int.Distance(data.epicentreBlock, closestBlock);
                float radiusBlocks = Mathf.Max(0.001f, data.radiusBlocks);

                float distance01 = Mathf.Clamp01(distanceBlocks / radiusBlocks);
                distanceMultiplier = 1f - distance01;
                distanceMultiplier = Mathf.Max(minimumDistanceMultiplierInsideRadius, distanceMultiplier);
            }

            float finalStrength = Mathf.Clamp01(magnitude01 * zoneMultiplier * distanceMultiplier);

            int baseDamage = Mathf.RoundToInt(
                Mathf.Lerp(minDamage, maxDamage, finalStrength)
            );

            BuildingEarthquakeResistance resistance =
                building.GetComponent<BuildingEarthquakeResistance>();

            int finalDamage = resistance != null
                ? resistance.ModifyDamage(baseDamage)
                : baseDamage;

            if (resistance != null && resistance.earthquakeImmune)
                finalDamage = 0;

            if (finalDamage > 0)
            {
                ApplyEarthquakeDamageToBuilding(building, finalDamage);
                damagedCount++;
            }

            bool sendHitMessage = resistance == null || resistance.receiveEarthquakeMessages;

            if (sendHitMessage && (sendHitMessageEvenWhenNoDamage || finalDamage > 0))
            {
                SendEarthquakeHitToBuilding(building, data);
            }

            TryIgniteBuildingFromEarthquake(
                building,
                resistance,
                finalStrength,
                finalDamage,
                data.magnitude,
                zoneName
            );

            if (debugLogging)
            {
                Debug.Log(
                    $"Earthquake building '{building.name}' zone={zoneName}, " +
                    $"cells={buildingCellsScratch.Count}, zoneMult={zoneMultiplier:0.00}, " +
                    $"distMult={distanceMultiplier:0.00}, damage={finalDamage}, " +
                    $"magnitude={data.magnitude:0.0}"
                );
            }

            processed++;

            if (processed >= buildingsProcessedPerFrame)
            {
                processed = 0;
                yield return null;
            }
        }

        if (debugLogging)
        {
            Debug.Log(
                $"EarthquakeBuildingEffectResolver complete. " +
                $"Damaged={damagedCount}, " +
                $"SkippedOutsideAffectedCells={skippedOutsideAffectedCells}, " +
                $"SkippedOutsideFaultInfluence={skippedOutsideFaultInfluence}, " +
                $"FaultCells={faultCells.Count}, InfluenceCells={influenceCells.Count}, AffectedCells={affectedCells.Count}"
            );
        }

        resolverRoutine = null;
    }

    private void TryIgniteBuildingFromEarthquake(
    GameObject building,
    BuildingEarthquakeResistance resistance,
    float earthquakeStrength01,
    int finalDamage,
    float magnitude,
    string zoneName)
    {
        if (!earthquakeCanIgniteBuildings)
            return;

        if (building == null)
            return;

        if (requireEarthquakeDamageBeforeIgnition && finalDamage <= 0)
            return;

        if (resistance != null && resistance.earthquakeImmune)
            return;

        if (requireEarthquakeIgnitionOptIn)
        {
            if (resistance == null)
                return;

            if (!resistance.canIgniteFromEarthquake)
                return;
        }

        BuildingFireState fireState = GetBuildingFireState(building);

        if (fireState == null)
            return;

        if (!fireState.CanCatchFire || fireState.IsOnFire)
            return;

        float minMag = Mathf.Min(ignitionStartsAtMagnitude, severeIgnitionMagnitude);
        float maxMag = Mathf.Max(ignitionStartsAtMagnitude, severeIgnitionMagnitude);

        float ignitionMagnitude01 = Mathf.Clamp01(Mathf.InverseLerp(minMag, maxMag, magnitude));

        if (ignitionMagnitude01 <= 0f)
            return;

        float severity01 = Mathf.Clamp01(earthquakeStrength01 * ignitionMagnitude01);

        float baseChance = Mathf.Lerp(
            minEarthquakeIgnitionChance,
            maxEarthquakeIgnitionChance,
            severity01
        );

        float finalChance = resistance != null
            ? resistance.ModifyEarthquakeIgnitionChance(baseChance)
            : baseChance;

        int baseBurnTurns = Mathf.RoundToInt(
            Mathf.Lerp(minEarthquakeBurnTurns, maxEarthquakeBurnTurns, severity01)
        );

        int finalBurnTurns = resistance != null
            ? resistance.ModifyEarthquakeBurnTurns(baseBurnTurns)
            : baseBurnTurns;

        finalChance = Mathf.Clamp01(finalChance);
        finalBurnTurns = Mathf.Max(0, finalBurnTurns);

        if (finalChance <= 0f || finalBurnTurns <= 0)
            return;

        bool ignited = fireState.TryIgnite(finalChance, finalBurnTurns);

        if (debugIgnitionLogging)
        {
            Debug.Log(
                $"Earthquake ignition check '{building.name}'. " +
                $"Zone={zoneName}, Magnitude={magnitude:0.0}, " +
                $"Strength={earthquakeStrength01:0.00}, IgnitionMagnitude={ignitionMagnitude01:0.00}, " +
                $"Chance={finalChance:0.00}, BurnTurns={finalBurnTurns}, Ignited={ignited}"
            );
        }
    }

    private BuildingFireState GetBuildingFireState(GameObject building)
    {
        if (building == null)
            return null;

        BuildingFireState fireState = building.GetComponent<BuildingFireState>();

        if (fireState == null)
            fireState = building.GetComponentInChildren<BuildingFireState>(true);

        if (fireState == null)
            fireState = building.GetComponentInParent<BuildingFireState>();

        return fireState;
    }

    private void ApplyEarthquakeDamageToBuilding(GameObject building, int finalDamage)
    {
        if (building == null || finalDamage <= 0)
            return;

        BuildingEarthquakeSecondaryEffects earthquakeEffects =
            building.GetComponent<BuildingEarthquakeSecondaryEffects>();

        if (earthquakeEffects == null)
            earthquakeEffects = building.GetComponentInChildren<BuildingEarthquakeSecondaryEffects>(true);

        if (earthquakeEffects == null)
            earthquakeEffects = building.GetComponentInParent<BuildingEarthquakeSecondaryEffects>();

        if (earthquakeEffects != null)
        {
            earthquakeEffects.ApplyEarthquakeDamage(finalDamage);
            return;
        }

        // Fallback: apply core damage directly even if no earthquake secondary script exists.
        BuildingControl buildingControl = building.GetComponent<BuildingControl>();

        if (buildingControl == null)
            buildingControl = building.GetComponentInChildren<BuildingControl>(true);

        if (buildingControl == null)
            buildingControl = building.GetComponentInParent<BuildingControl>();

        if (buildingControl != null)
        {
            buildingControl.ApplyDamage(finalDamage);

            if (debugLogging)
            {
                Debug.Log(
                    $"EarthquakeBuildingEffectResolver: Applied direct BuildingControl damage " +
                    $"{finalDamage} to '{buildingControl.name}' because no BuildingEarthquakeSecondaryEffects was found."
                );
            }

            return;
        }

        // Last fallback for any future custom receivers.
        building.SendMessage(
            damageMessageName,
            finalDamage,
            SendMessageOptions.DontRequireReceiver
        );

        if (debugLogging)
        {
            Debug.LogWarning(
                $"EarthquakeBuildingEffectResolver: Could not find BuildingEarthquakeSecondaryEffects " +
                $"or BuildingControl on '{building.name}'. SendMessage fallback used."
            );
        }
    }

    private void SendEarthquakeHitToBuilding(GameObject building, EarthquakeEventData data)
    {
        if (building == null || data == null)
            return;

        BuildingEarthquakeSecondaryEffects earthquakeEffects =
            building.GetComponent<BuildingEarthquakeSecondaryEffects>();

        if (earthquakeEffects == null)
            earthquakeEffects = building.GetComponentInChildren<BuildingEarthquakeSecondaryEffects>(true);

        if (earthquakeEffects == null)
            earthquakeEffects = building.GetComponentInParent<BuildingEarthquakeSecondaryEffects>();

        if (earthquakeEffects != null)
        {
            earthquakeEffects.OnEarthquakeHit(data);
            return;
        }

        building.SendMessage(
            hitMessageName,
            data,
            SendMessageOptions.DontRequireReceiver
        );
    }

    private void BuildEarthquakeCellSets(EarthquakeEventData data)
    {
        faultCells.Clear();
        influenceCells.Clear();
        affectedCells.Clear();

        AddCellsFromBlocks(data.faultBlocks, faultCells);
        AddCellsFromBlocks(data.faultInfluenceBlocks, influenceCells);
        AddCellsFromBlocks(data.affectedBlocks, affectedCells);

        // Main fault should win over influence if they overlap.
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

    private float GetCellZoneMultiplier(List<Vector2Int> buildingCells, out string zoneName)
    {
        if (AnyCellInSet(buildingCells, faultCells))
        {
            zoneName = "Main Fault Cells";
            return faultCellDamageMultiplier;
        }

        if (AnyCellInSet(buildingCells, influenceCells))
        {
            zoneName = "Influence Cells";
            return influenceCellDamageMultiplier;
        }

        zoneName = "Outside Fault/Influence Cells";

        if (noDamageOutsideFaultInfluenceCells)
            return 0f;

        return outsideFaultCellDamageMultiplier;
    }

    private bool AnyCellInSet(List<Vector2Int> cells, HashSet<Vector2Int> set)
    {
        if (cells == null || set == null || set.Count == 0)
            return false;

        for (int i = 0; i < cells.Count; i++)
        {
            if (set.Contains(cells[i]))
                return true;
        }

        return false;
    }

    private Vector2Int GetClosestBuildingBlockToEpicentre(List<Vector2Int> buildingCells, Vector2Int epicentreBlock)
    {
        Vector2Int bestBlock = epicentreBlock;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < buildingCells.Count; i++)
        {
            Vector2Int block = mapGenerator.GetBlockFromCell(buildingCells[i]);

            if (!mapGenerator.IsValidBlock(block))
                continue;

            float d = Vector2Int.Distance(epicentreBlock, block);

            if (d < bestDistance)
            {
                bestDistance = d;
                bestBlock = block;
            }
        }

        return bestBlock;
    }

    private void GetBuildingCoveredCells(
        WorldBuildingManager.Record record,
        List<Vector2Int> results)
    {
        results.Clear();

        if (record == null || record.instance == null)
            return;

        if (!useBuildingFootprintCells)
        {
            Vector2Int single = gridManager.GetGridPosition(record.instance.transform.position);

            if (IsCellInsideGrid(single))
                results.Add(single);

            return;
        }

        if (TryGetWorldBounds(record.instance, out Bounds bounds))
        {
            AddCellsCoveredByBounds(bounds, results);
            return;
        }

        Vector2Int fallbackCell = gridManager.GetGridPosition(record.worldPos);

        if (IsCellInsideGrid(fallbackCell))
            results.Add(fallbackCell);
    }

    private void AddCellsCoveredByBounds(Bounds bounds, List<Vector2Int> results)
    {
        if (gridManager == null)
            return;

        float inset = Mathf.Clamp01(boundsCellInset) * gridManager.cellSize;

        Vector3 minWorld = new Vector3(
            bounds.min.x + inset,
            0f,
            bounds.min.z + inset
        );

        Vector3 maxWorld = new Vector3(
            bounds.max.x - inset,
            0f,
            bounds.max.z - inset
        );

        Vector2Int min = gridManager.GetGridPosition(minWorld);
        Vector2Int max = gridManager.GetGridPosition(maxWorld);

        min.x = Mathf.Clamp(min.x, 0, gridManager.columns - 1);
        min.y = Mathf.Clamp(min.y, 0, gridManager.rows - 1);
        max.x = Mathf.Clamp(max.x, 0, gridManager.columns - 1);
        max.y = Mathf.Clamp(max.y, 0, gridManager.rows - 1);

        for (int x = min.x; x <= max.x; x++)
        {
            for (int y = min.y; y <= max.y; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);

                if (!results.Contains(cell))
                    results.Add(cell);
            }
        }
    }

    private bool TryGetWorldBounds(GameObject go, out Bounds bounds)
    {
        bounds = default;

        if (go == null)
            return false;

        Collider ownCollider = go.GetComponent<Collider>();

        if (ownCollider != null)
        {
            bounds = ownCollider.bounds;
            return true;
        }

        Collider[] colliders = go.GetComponentsInChildren<Collider>(true);

        if (colliders != null && colliders.Length > 0)
        {
            bounds = colliders[0].bounds;

            for (int i = 1; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                    bounds.Encapsulate(colliders[i].bounds);
            }

            return true;
        }

        Renderer ownRenderer = go.GetComponent<Renderer>();

        if (ownRenderer != null)
        {
            bounds = ownRenderer.bounds;
            return true;
        }

        Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);

        if (renderers != null && renderers.Length > 0)
        {
            bounds = renderers[0].bounds;

            for (int i = 1; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    bounds.Encapsulate(renderers[i].bounds);
            }

            return true;
        }

        return false;
    }

    private bool IsCellInsideGrid(Vector2Int cell)
    {
        return gridManager != null &&
               cell.x >= 0 &&
               cell.y >= 0 &&
               cell.x < gridManager.columns &&
               cell.y < gridManager.rows;
    }

    private void ResolveReferences()
    {
        if (simulationSystem == null)
            simulationSystem = FindObjectOfType<EarthquakeSimulationSystem>();

        if (gridManager == null)
            gridManager = GridManager.Instance;

        if (mapGenerator == null)
            mapGenerator = FindObjectOfType<MapGenerator>();

        if (worldBuildingManager == null)
            worldBuildingManager = WorldBuildingManager.Instance;
    }
}