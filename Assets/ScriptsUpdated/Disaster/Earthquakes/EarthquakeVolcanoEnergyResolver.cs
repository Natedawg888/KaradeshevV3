using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EarthquakeVolcanoEnergyResolver : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EarthquakeSimulationSystem simulationSystem;
    [SerializeField] private VolcanoManager volcanoManager;
    [SerializeField] private MapGenerator mapGenerator;
    [SerializeField] private GridManager gridManager;

    [Header("Magnitude")]
    [Tooltip("Earthquakes below this magnitude do not add volcano energy.")]
    [SerializeField] private float volcanoEnergyStartsAtMagnitude = 4.5f;

    [Tooltip("Earthquakes at or above this magnitude use maximum volcano energy boost.")]
    [SerializeField] private float severeVolcanoEnergyMagnitude = 8.0f;

    [Header("Energy Boost")]
    [Tooltip("Minimum energy boost from a weak qualifying earthquake.")]
    [Range(0f, 1f)]
    [SerializeField] private float minEnergyBoost = 0.03f;

    [Tooltip("Maximum energy boost from a severe earthquake.")]
    [Range(0f, 1f)]
    [SerializeField] private float maxEnergyBoost = 0.25f;

    [Header("Fault Zone Scaling")]
    [Tooltip("Volcanoes directly on main fault blocks receive full energy boost.")]
    [Min(0f)]
    [SerializeField] private float mainFaultMultiplier = 1f;

    [Tooltip("Volcanoes on fault influence blocks receive reduced energy boost.")]
    [Min(0f)]
    [SerializeField] private float influenceFaultMultiplier = 0.45f;

    [Tooltip("Volcanoes inside quake radius but outside fault/influence receive this multiplier.")]
    [Min(0f)]
    [SerializeField] private float affectedRadiusOnlyMultiplier = 0f;

    [Tooltip("If true, only volcanoes on main fault or influence blocks can gain energy.")]
    [SerializeField] private bool requireFaultOrInfluence = true;

    [Header("Distance Falloff")]
    [SerializeField] private bool scaleByDistanceToEpicentre = true;

    [Range(0f, 1f)]
    [SerializeField] private float minimumDistanceMultiplierInsideRadius = 0.25f;

    [Header("Processing")]
    [SerializeField] private bool processOverFrames = true;

    [Min(1)]
    [SerializeField] private int volcanoesProcessedPerFrame = 8;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = true;

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
    }

    public void InstallRuntimeRefs(
        EarthquakeSimulationSystem newSimulationSystem,
        VolcanoManager newVolcanoManager,
        MapGenerator newMapGenerator,
        GridManager newGridManager)
    {
        if (newSimulationSystem != null)
            simulationSystem = newSimulationSystem;

        if (newVolcanoManager != null)
            volcanoManager = newVolcanoManager;

        if (newMapGenerator != null)
            mapGenerator = newMapGenerator;

        if (newGridManager != null)
            gridManager = newGridManager;

        RebindEarthquakeEvents();

        if (debugLogging)
        {
            //Debug.Log(
                //$"EarthquakeVolcanoEnergyResolver: Installed refs. " +
                //$"Simulation={(simulationSystem != null ? simulationSystem.name : "NULL")}, " +
                //$"VolcanoManager={(volcanoManager != null ? volcanoManager.name : "NULL")}, " +
                //$"MapGenerator={(mapGenerator != null ? mapGenerator.name : "NULL")}, " +
                //$"GridManager={(gridManager != null ? gridManager.name : "NULL")}"
            //);
        }
    }

    private void EnsureLinks()
    {
        if (simulationSystem == null)
            simulationSystem = FindObjectOfType<EarthquakeSimulationSystem>();

        if (volcanoManager == null)
            volcanoManager = VolcanoManager.Instance;

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

        if (volcanoManager == null || mapGenerator == null || gridManager == null)
        {
            if (debugLogging) {}
                //Debug.LogWarning("EarthquakeVolcanoEnergyResolver: Missing references.");

            return;
        }

        float magnitude01 = GetMagnitude01(data.magnitude);

        if (magnitude01 <= 0f)
        {
            if (debugLogging)
            {
                //Debug.Log(
                    //$"EarthquakeVolcanoEnergyResolver: Magnitude {data.magnitude:0.0} too low to affect volcanoes."
                //);
            }

            return;
        }

        if (processRoutine != null)
            StopCoroutine(processRoutine);

        if (processOverFrames)
            processRoutine = StartCoroutine(ProcessVolcanoesRoutine(data, magnitude01));
        else
            ProcessVolcanoesImmediate(data, magnitude01);
    }

    private IEnumerator ProcessVolcanoesRoutine(EarthquakeEventData data, float magnitude01)
    {
        int affected = 0;
        int processedThisFrame = 0;
        int maxPerFrame = Mathf.Max(1, volcanoesProcessedPerFrame);

        foreach (VolcanoTileState volcano in volcanoManager.RegisteredVolcanoes)
        {
            if (volcano == null)
                continue;

            if (ApplyEarthquakeEnergyToVolcano(volcano, data, magnitude01))
                affected++;

            processedThisFrame++;

            if (processedThisFrame >= maxPerFrame)
            {
                processedThisFrame = 0;
                yield return null;
            }
        }

        if (debugLogging)
        {
            //Debug.Log(
                //$"EarthquakeVolcanoEnergyResolver complete. VolcanoesAffected={affected}, " +
                //$"Magnitude={data.magnitude:0.0}"
            //);
        }

        processRoutine = null;
    }

    private void ProcessVolcanoesImmediate(EarthquakeEventData data, float magnitude01)
    {
        int affected = 0;

        foreach (VolcanoTileState volcano in volcanoManager.RegisteredVolcanoes)
        {
            if (volcano == null)
                continue;

            if (ApplyEarthquakeEnergyToVolcano(volcano, data, magnitude01))
                affected++;
        }

        if (debugLogging)
        {
            //Debug.Log(
                //$"EarthquakeVolcanoEnergyResolver complete. VolcanoesAffected={affected}, " +
                //$"Magnitude={data.magnitude:0.0}"
            //);
        }
    }

    private bool ApplyEarthquakeEnergyToVolcano(
        VolcanoTileState volcano,
        EarthquakeEventData data,
        float magnitude01)
    {
        if (volcano == null)
            return false;

        if (!volcano.TryGetPrimaryCell(out Vector2Int volcanoCell))
            return false;

        if (!IsCellInsideGrid(volcanoCell))
            return false;

        Vector2Int volcanoBlock = mapGenerator.GetBlockFromCell(volcanoCell);

        if (!mapGenerator.IsValidBlock(volcanoBlock))
            return false;

        float zoneMultiplier = GetZoneMultiplier(data, volcanoBlock, out string zoneName);

        if (zoneMultiplier <= 0f)
            return false;

        float distanceMultiplier = 1f;

        if (scaleByDistanceToEpicentre)
        {
            float distanceBlocks = Vector2Int.Distance(data.epicentreBlock, volcanoBlock);
            float radiusBlocks = Mathf.Max(0.001f, data.radiusBlocks);

            float distance01 = Mathf.Clamp01(distanceBlocks / radiusBlocks);
            distanceMultiplier = 1f - distance01;
            distanceMultiplier = Mathf.Max(minimumDistanceMultiplierInsideRadius, distanceMultiplier);
        }

        float severity = Mathf.Clamp01(magnitude01 * zoneMultiplier * distanceMultiplier);

        if (severity <= 0f)
            return false;

        float boost = Mathf.Lerp(minEnergyBoost, maxEnergyBoost, severity);
        boost = Mathf.Clamp01(boost);

        float oldEnergy = volcano.Energy01;
        float newEnergy = Mathf.Clamp01(oldEnergy + boost);

        if (Mathf.Approximately(oldEnergy, newEnergy))
            return false;

        volcano.SetEnergy01(newEnergy);

        if (debugLogging)
        {
            //Debug.Log(
                //$"Earthquake added volcano energy to '{volcano.name}'. " +
                //$"Zone={zoneName}, Block={volcanoBlock}, Magnitude={data.magnitude:0.0}, " +
                //$"Severity={severity:0.00}, Boost={boost:0.000}, " +
                //$"Energy {oldEnergy:0.000} -> {newEnergy:0.000}"
            //);
        }

        return true;
    }

    private float GetMagnitude01(float magnitude)
    {
        float min = Mathf.Min(volcanoEnergyStartsAtMagnitude, severeVolcanoEnergyMagnitude);
        float max = Mathf.Max(volcanoEnergyStartsAtMagnitude, severeVolcanoEnergyMagnitude);

        return Mathf.Clamp01(Mathf.InverseLerp(min, max, magnitude));
    }

    private float GetZoneMultiplier(
        EarthquakeEventData data,
        Vector2Int volcanoBlock,
        out string zoneName)
    {
        if (ContainsBlock(data.faultBlocks, volcanoBlock))
        {
            zoneName = "Main Fault";
            return mainFaultMultiplier;
        }

        if (ContainsBlock(data.faultInfluenceBlocks, volcanoBlock))
        {
            zoneName = "Fault Influence";
            return influenceFaultMultiplier;
        }

        if (data.ContainsBlock(volcanoBlock))
        {
            zoneName = "Affected Radius Only";

            if (requireFaultOrInfluence)
                return 0f;

            return affectedRadiusOnlyMultiplier;
        }

        zoneName = "Outside Earthquake";
        return 0f;
    }

    private bool ContainsBlock(
        IReadOnlyCollection<Vector2Int> blocks,
        Vector2Int target)
    {
        if (blocks == null || blocks.Count == 0)
            return false;

        foreach (Vector2Int block in blocks)
        {
            if (block == target)
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
}
