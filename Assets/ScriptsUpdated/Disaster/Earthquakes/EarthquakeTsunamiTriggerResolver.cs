using System.Collections.Generic;
using UnityEngine;

public class EarthquakeTsunamiTriggerResolver : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EarthquakeSimulationSystem earthquakeSimulationSystem;
    [SerializeField] private TsunamiSimulationSystem tsunamiSimulationSystem;
    [SerializeField] private MapGenerator mapGenerator;
    [SerializeField] private GridManager gridManager;

    [Header("Trigger Rules")]
    [SerializeField] private bool enableEarthquakeTriggeredTsunamis = true;

    [Tooltip("Earthquakes below this magnitude cannot trigger tsunamis.")]
    [SerializeField] private float tsunamiStartsAtMagnitude = 6.0f;

    [Tooltip("Earthquakes at or above this magnitude use max tsunami chance and max tsunami energy.")]
    [SerializeField] private float severeTsunamiMagnitude = 8.5f;

    [Tooltip("Lowest tsunami chance once the earthquake passes the magnitude threshold.")]
    [Range(0f, 1f)]
    [SerializeField] private float minTsunamiChance = 0.05f;

    [Tooltip("Highest tsunami chance for severe earthquakes.")]
    [Range(0f, 1f)]
    [SerializeField] private float maxTsunamiChance = 0.85f;

    [Tooltip("Chance curve. X = magnitude severity 0-1, Y = chance blend.")]
    [SerializeField]
    private AnimationCurve magnitudeToChanceCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Ocean Requirement")]
    [Tooltip("If true, the earthquake must affect at least one Sea block before it can create a tsunami.")]
    [SerializeField] private bool requireSeaInAffectedBlocks = true;

    [Tooltip("If true, if the preferred ocean edge fails, the tsunami system may use any valid ocean edge.")]
    [SerializeField] private bool fallbackToAnyOceanEdge = true;

    [Header("Energy")]
    [Tooltip("Tsunami energy for the weakest tsunami-triggering earthquake.")]
    [SerializeField] private float minTsunamiEnergy = 8f;

    [Tooltip("Tsunami energy for a severe tsunami-triggering earthquake.")]
    [SerializeField] private float maxTsunamiEnergy = 28f;

    [Tooltip("Energy curve. X = magnitude severity 0-1, Y = energy blend.")]
    [SerializeField]
    private AnimationCurve magnitudeToEnergyCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Random energy variation. 0.10 = +/-10%.")]
    [Range(0f, 1f)]
    [SerializeField] private float energyRandomVariation = 0.10f;

    [Tooltip("If epicentre is on a fault, multiply tsunami energy by this.")]
    [Min(0f)]
    [SerializeField] private float epicentreOnFaultEnergyMultiplier = 1.10f;

    [Header("Forced Earthquakes")]
    [Tooltip("If true, forced/test earthquakes can trigger tsunamis too.")]
    [SerializeField] private bool allowForcedEarthquakesToTriggerTsunamis = true;

    [Tooltip("If true, forced earthquakes skip the random tsunami chance roll once magnitude is high enough.")]
    [SerializeField] private bool forcedEarthquakesBypassChanceRoll = true;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = true;

    private EarthquakeSimulationSystem subscribedEarthquakeSystem;

    private readonly List<TsunamiGridEdge> validOceanEdgesScratch =
        new List<TsunamiGridEdge>(4);

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        RebindEarthquakeEvents();
    }

    private void Start()
    {
        ResolveReferences();
        RebindEarthquakeEvents();
    }

    private void OnDisable()
    {
        UnbindEarthquakeEvents();
    }

    public void InstallRuntimeRefs(
        EarthquakeSimulationSystem newEarthquakeSimulationSystem,
        TsunamiSimulationSystem newTsunamiSimulationSystem,
        MapGenerator newMapGenerator,
        GridManager newGridManager)
    {
        if (newEarthquakeSimulationSystem != null)
            earthquakeSimulationSystem = newEarthquakeSimulationSystem;

        if (newTsunamiSimulationSystem != null)
            tsunamiSimulationSystem = newTsunamiSimulationSystem;

        if (newMapGenerator != null)
            mapGenerator = newMapGenerator;

        if (newGridManager != null)
            gridManager = newGridManager;

        RebindEarthquakeEvents();

        if (debugLogging)
        {
            Debug.Log(
                $"[EarthquakeTsunamiTriggerResolver] Installed refs. " +
                $"Earthquake={(earthquakeSimulationSystem != null ? earthquakeSimulationSystem.name : "NULL")}, " +
                $"Tsunami={(tsunamiSimulationSystem != null ? tsunamiSimulationSystem.name : "NULL")}, " +
                $"Map={(mapGenerator != null ? mapGenerator.name : "NULL")}, " +
                $"Grid={(gridManager != null ? gridManager.name : "NULL")}");
        }
    }

    private void RebindEarthquakeEvents()
    {
        if (subscribedEarthquakeSystem == earthquakeSimulationSystem)
            return;

        UnbindEarthquakeEvents();

        subscribedEarthquakeSystem = earthquakeSimulationSystem;

        if (subscribedEarthquakeSystem != null)
            subscribedEarthquakeSystem.OnEarthquake += HandleEarthquake;
    }

    private void UnbindEarthquakeEvents()
    {
        if (subscribedEarthquakeSystem == null)
            return;

        subscribedEarthquakeSystem.OnEarthquake -= HandleEarthquake;
        subscribedEarthquakeSystem = null;
    }

    private void HandleEarthquake(EarthquakeEventData data)
    {
        if (!enableEarthquakeTriggeredTsunamis)
            return;

        ResolveReferences();

        if (data == null)
            return;

        if (data.forced && !allowForcedEarthquakesToTriggerTsunamis)
        {
            if (debugLogging)
                Debug.Log("[EarthquakeTsunamiTriggerResolver] Forced earthquake ignored for tsunami trigger.");

            return;
        }

        if (tsunamiSimulationSystem == null || mapGenerator == null || gridManager == null)
        {
            if (debugLogging)
                Debug.LogWarning("[EarthquakeTsunamiTriggerResolver] Missing references.");

            return;
        }

        float magnitude01 = GetMagnitudeSeverity01(data.magnitude);

        if (magnitude01 <= 0f)
        {
            if (debugLogging)
            {
                Debug.Log(
                    $"[EarthquakeTsunamiTriggerResolver] Earthquake too weak for tsunami. " +
                    $"Magnitude={data.magnitude:0.0}");
            }

            return;
        }

        if (requireSeaInAffectedBlocks && !EarthquakeAffectedSea(data))
        {
            if (debugLogging)
            {
                Debug.Log(
                    $"[EarthquakeTsunamiTriggerResolver] Earthquake did not affect sea blocks. " +
                    $"Magnitude={data.magnitude:0.0}, Epicentre={data.epicentreBlock}");
            }

            return;
        }

        float chance = CalculateTsunamiChance(magnitude01);

        bool bypassRoll = data.forced && forcedEarthquakesBypassChanceRoll;
        float roll = UnityEngine.Random.value;

        if (!bypassRoll && roll > chance)
        {
            if (debugLogging)
            {
                Debug.Log(
                    $"[EarthquakeTsunamiTriggerResolver] Earthquake tsunami roll failed. " +
                    $"Magnitude={data.magnitude:0.0}, Chance={chance:0.00}, Roll={roll:0.00}");
            }

            return;
        }

        if (!TryPickPreferredOceanEdge(data, out TsunamiGridEdge edge))
        {
            if (debugLogging)
                Debug.LogWarning("[EarthquakeTsunamiTriggerResolver] No valid ocean edge found for tsunami.");

            return;
        }

        float energy = CalculateTsunamiEnergy(data, magnitude01);

        bool triggered = tsunamiSimulationSystem.TryTriggerTsunamiFromEarthquake(
            edge,
            energy,
            fallbackToAnyOceanEdge);

        if (debugLogging)
        {
            Debug.Log(
                $"[EarthquakeTsunamiTriggerResolver] Earthquake tsunami decision. " +
                $"Triggered={triggered}, Magnitude={data.magnitude:0.0}, Magnitude01={magnitude01:0.00}, " +
                $"Chance={chance:0.00}, Roll={(bypassRoll ? -1f : roll):0.00}, " +
                $"Energy={energy:0.00}, Edge={edge}, ForcedEarthquake={data.forced}");
        }
    }

    private float GetMagnitudeSeverity01(float magnitude)
    {
        float min = Mathf.Min(tsunamiStartsAtMagnitude, severeTsunamiMagnitude);
        float max = Mathf.Max(tsunamiStartsAtMagnitude, severeTsunamiMagnitude);

        return Mathf.Clamp01(Mathf.InverseLerp(min, max, magnitude));
    }

    private float CalculateTsunamiChance(float magnitude01)
    {
        float t = Mathf.Clamp01(magnitude01);

        if (magnitudeToChanceCurve != null)
            t = Mathf.Clamp01(magnitudeToChanceCurve.Evaluate(t));

        return Mathf.Lerp(minTsunamiChance, maxTsunamiChance, t);
    }

    private float CalculateTsunamiEnergy(EarthquakeEventData data, float magnitude01)
    {
        float t = Mathf.Clamp01(magnitude01);

        if (magnitudeToEnergyCurve != null)
            t = Mathf.Clamp01(magnitudeToEnergyCurve.Evaluate(t));

        float minEnergy = Mathf.Min(minTsunamiEnergy, maxTsunamiEnergy);
        float maxEnergy = Mathf.Max(minTsunamiEnergy, maxTsunamiEnergy);

        float energy = Mathf.Lerp(minEnergy, maxEnergy, t);

        if (data != null && data.epicentreWasOnFault)
            energy *= Mathf.Max(0f, epicentreOnFaultEnergyMultiplier);

        if (energyRandomVariation > 0f)
        {
            float variation = Random.Range(
                1f - energyRandomVariation,
                1f + energyRandomVariation);

            energy *= Mathf.Max(0f, variation);
        }

        return Mathf.Max(0.01f, energy);
    }

    private bool EarthquakeAffectedSea(EarthquakeEventData data)
    {
        if (data == null || data.affectedBlocks == null || mapGenerator == null)
            return false;

        for (int i = 0; i < data.affectedBlocks.Count; i++)
        {
            Vector2Int block = data.affectedBlocks[i];

            if (!mapGenerator.IsValidBlock(block))
                continue;

            if (!mapGenerator.TryGetBlockTerrain(block, out TerrainBlockKind kind))
                continue;

            if (kind == TerrainBlockKind.Sea)
                return true;
        }

        return false;
    }

    private bool TryPickPreferredOceanEdge(EarthquakeEventData data, out TsunamiGridEdge edge)
    {
        edge = TsunamiGridEdge.West;

        BuildValidOceanEdges(validOceanEdgesScratch);

        if (validOceanEdgesScratch.Count == 0)
            return false;

        if (data == null || mapGenerator == null || gridManager == null)
        {
            edge = validOceanEdgesScratch[Random.Range(0, validOceanEdgesScratch.Count)];
            return true;
        }

        Vector2Int epicentreCell = GetApproxEpicentreCell(data.epicentreBlock);

        float bestDistance = float.MaxValue;
        TsunamiGridEdge bestEdge = validOceanEdgesScratch[0];

        for (int i = 0; i < validOceanEdgesScratch.Count; i++)
        {
            TsunamiGridEdge candidate = validOceanEdgesScratch[i];

            float distance = GetDistanceFromCellToGridEdge(epicentreCell, candidate);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestEdge = candidate;
            }
        }

        edge = bestEdge;
        return true;
    }

    private Vector2Int GetApproxEpicentreCell(Vector2Int epicentreBlock)
    {
        if (mapGenerator == null)
            return Vector2Int.zero;

        Vector2Int minCell = mapGenerator.GetBlockMinCell(epicentreBlock);
        int blockSize = Mathf.Max(1, mapGenerator.blockSize);

        return new Vector2Int(
            minCell.x + blockSize / 2,
            minCell.y + blockSize / 2);
    }

    private float GetDistanceFromCellToGridEdge(Vector2Int cell, TsunamiGridEdge edge)
    {
        if (gridManager == null)
            return float.MaxValue;

        switch (edge)
        {
            case TsunamiGridEdge.West:
                return Mathf.Abs(cell.x);

            case TsunamiGridEdge.East:
                return Mathf.Abs((gridManager.columns - 1) - cell.x);

            case TsunamiGridEdge.South:
                return Mathf.Abs(cell.y);

            case TsunamiGridEdge.North:
                return Mathf.Abs((gridManager.rows - 1) - cell.y);

            default:
                return float.MaxValue;
        }
    }

    private void BuildValidOceanEdges(List<TsunamiGridEdge> results)
    {
        results.Clear();

        if (gridManager == null)
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

    private bool IsSeaCell(TileCoord coord)
    {
        if (gridManager == null || mapGenerator == null)
            return false;

        if (coord.x < 0 || coord.y < 0 || coord.x >= gridManager.columns || coord.y >= gridManager.rows)
            return false;

        Vector2Int block = mapGenerator.GetBlockFromCell(new Vector2Int(coord.x, coord.y));

        if (!mapGenerator.IsValidBlock(block))
            return false;

        if (!mapGenerator.TryGetBlockTerrain(block, out TerrainBlockKind kind))
            return false;

        return kind == TerrainBlockKind.Sea;
    }

    private void ResolveReferences()
    {
        if (earthquakeSimulationSystem == null)
            earthquakeSimulationSystem = FindObjectOfType<EarthquakeSimulationSystem>();

        if (tsunamiSimulationSystem == null)
            tsunamiSimulationSystem = TsunamiSimulationSystem.Instance;

        if (tsunamiSimulationSystem == null)
            tsunamiSimulationSystem = FindObjectOfType<TsunamiSimulationSystem>();

        if (mapGenerator == null)
            mapGenerator = FindObjectOfType<MapGenerator>();

        if (gridManager == null)
            gridManager = GridManager.Instance;
    }
}