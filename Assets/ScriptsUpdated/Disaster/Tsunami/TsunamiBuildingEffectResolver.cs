using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TsunamiBuildingEffectResolver : MonoBehaviour
{
    [Header("References")]
    public TsunamiSimulationSystem simulationSystem;
    public GridManager gridManager;
    public WorldBuildingManager worldBuildingManager;

    [Header("Damage")]
    public bool damageBuildings = true;

    [Tooltip("Tsunami energy01 must be at or above this value to damage buildings.")]
    [Range(0f, 1f)]
    public float damageStartsAtEnergy01 = 0.10f;

    [Tooltip("Energy01 at or above this uses maximum damage.")]
    [Range(0f, 1f)]
    public float severeDamageEnergy01 = 1f;

    public int minDamage = 1;
    public int maxDamage = 28;

    [Header("Footprint Coverage Scaling")]
    [Tooltip("If true, buildings hit on more cells take more damage.")]
    public bool scaleDamageByHitCellCount = true;

    [Tooltip("Extra damage multiplier per additional hit cell. Example: 0.15 = +15% per extra hit cell.")]
    [Min(0f)]
    public float extraDamageMultiplierPerAdditionalHitCell = 0.15f;

    [Tooltip("Maximum multiplier from hit cell count.")]
    [Min(1f)]
    public float maxHitCellDamageMultiplier = 2f;

    [Header("Building Footprint")]
    [Tooltip("If true, use building bounds/colliders/renderers to find every grid cell the building covers.")]
    public bool useBuildingFootprintCells = true;

    [Tooltip("Small shrink amount for bounds checks so touching edges do not over-count cells.")]
    [Range(0f, 0.45f)]
    public float boundsCellInset = 0.08f;

    [Header("Processing")]
    [Min(1)]
    public int buildingsProcessedPerFrame = 16;

    [Header("Messages")]
    public string damageMessageName = "ApplyTsunamiDamage";
    public string hitMessageName = "OnTsunamiHit";

    public bool sendHitMessageEvenWhenNoDamage = false;

    [Header("Debug")]
    public bool debugLogging = true;

    private Coroutine resolverRoutine;
    private TsunamiSimulationSystem subscribedSimulationSystem;

    private readonly HashSet<TileCoord> activeTsunamiCells =
        new HashSet<TileCoord>();

    private readonly List<TileCoord> buildingCellsScratch =
        new List<TileCoord>();

    private readonly List<TileCoord> hitCellsScratch =
        new List<TileCoord>();

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

        if (resolverRoutine != null)
        {
            StopCoroutine(resolverRoutine);
            resolverRoutine = null;
        }
    }

    public void InstallRuntimeRefs(
        TsunamiSimulationSystem newSimulationSystem,
        GridManager newGridManager,
        WorldBuildingManager newWorldBuildingManager)
    {
        if (newSimulationSystem != null)
            simulationSystem = newSimulationSystem;

        if (newGridManager != null)
            gridManager = newGridManager;

        if (newPlayerBuildingManager != null)
            worldBuildingManager = newPlayerBuildingManager;

        RebindSimulationSubscription();

        if (debugLogging)
        {
            Debug.Log(
                $"[TsunamiBuildingEffectResolver] Installed refs. " +
                $"Simulation={(simulationSystem != null ? simulationSystem.name : "NULL")}, " +
                $"GridManager={(gridManager != null ? gridManager.name : "NULL")}, " +
                $"PlayerBuildingManager={(worldBuildingManager != null ? worldBuildingManager.name : "NULL")}");
        }
    }

    public void SetPlayerBuildingManager(PlayerBuildingManager newPlayerBuildingManager)
    {
        if (worldBuildingManager == newPlayerBuildingManager)
            return;

        worldBuildingManager = newPlayerBuildingManager;

        if (debugLogging)
        {
            Debug.Log(
                $"[TsunamiBuildingEffectResolver] SetPlayerBuildingManager -> " +
                $"{(worldBuildingManager != null ? worldBuildingManager.name : "NULL")}");
        }
    }

    private void RebindSimulationSubscription()
    {
        if (subscribedSimulationSystem == simulationSystem)
            return;

        UnbindSimulationSubscription();

        subscribedSimulationSystem = simulationSystem;

        if (subscribedSimulationSystem != null)
            subscribedSimulationSystem.OnTsunamiAdvanced += HandleTsunamiAdvanced;
    }

    private void UnbindSimulationSubscription()
    {
        if (subscribedSimulationSystem == null)
            return;

        subscribedSimulationSystem.OnTsunamiAdvanced -= HandleTsunamiAdvanced;
        subscribedSimulationSystem = null;
    }

    private void HandleTsunamiAdvanced(TsunamiAdvancedEventData data)
    {
        if (!damageBuildings)
            return;

        if (data == null || data.activeCells == null || data.activeCells.Count == 0)
            return;

        if (resolverRoutine != null)
            StopCoroutine(resolverRoutine);

        resolverRoutine = StartCoroutine(ProcessBuildings(data));
    }

    private IEnumerator ProcessBuildings(TsunamiAdvancedEventData data)
    {
        ResolveReferences();

        if (gridManager == null || worldBuildingManager == null)
        {
            if (debugLogging)
                Debug.LogWarning("[TsunamiBuildingEffectResolver] Missing references.");

            resolverRoutine = null;
            yield break;
        }

        IReadOnlyList<WorldBuildingManager.Record> records = worldBuildingManager.GetAll();

        if (records == null || records.Count == 0)
        {
            resolverRoutine = null;
            yield break;
        }

        activeTsunamiCells.Clear();

        for (int i = 0; i < data.activeCells.Count; i++)
            activeTsunamiCells.Add(data.activeCells[i]);

        float minEnergy = Mathf.Min(damageStartsAtEnergy01, severeDamageEnergy01);
        float maxEnergy = Mathf.Max(damageStartsAtEnergy01, severeDamageEnergy01);

        float energyDamage01 = Mathf.Clamp01(
            Mathf.InverseLerp(minEnergy, maxEnergy, data.energy01));

        if (energyDamage01 <= 0f)
        {
            if (debugLogging)
            {
                Debug.Log(
                    $"[TsunamiBuildingEffectResolver] Energy too low for building damage. " +
                    $"TsunamiId={data.tsunamiId}, Energy01={data.energy01:0.00}");
            }

            resolverRoutine = null;
            yield break;
        }

        int processed = 0;
        int damagedCount = 0;
        int hitNoDamageCount = 0;

        for (int i = 0; i < records.Count; i++)
        {
            WorldBuildingManager.Record record = records[i];

            if (record == null || record.instance == null)
                continue;

            GameObject building = record.instance;

            GetBuildingCoveredCells(record, buildingCellsScratch);

            if (buildingCellsScratch.Count == 0)
                continue;

            GetHitCells(buildingCellsScratch, activeTsunamiCells, hitCellsScratch);

            if (hitCellsScratch.Count == 0)
                continue;

            int baseDamage = Mathf.RoundToInt(
                Mathf.Lerp(minDamage, maxDamage, energyDamage01));

            if (scaleDamageByHitCellCount)
            {
                int extraHitCells = Mathf.Max(0, hitCellsScratch.Count - 1);

                float hitCellMultiplier = 1f +
                    extraHitCells * Mathf.Max(0f, extraDamageMultiplierPerAdditionalHitCell);

                hitCellMultiplier = Mathf.Min(
                    Mathf.Max(1f, maxHitCellDamageMultiplier),
                    hitCellMultiplier);

                baseDamage = Mathf.RoundToInt(baseDamage * hitCellMultiplier);
            }

            BuildingTsunamiResistance resistance =
                building.GetComponent<BuildingTsunamiResistance>();

            if (resistance == null)
                resistance = building.GetComponentInChildren<BuildingTsunamiResistance>(true);

            if (resistance == null)
                resistance = building.GetComponentInParent<BuildingTsunamiResistance>();

            int finalDamage = resistance != null
                ? resistance.ModifyDamage(baseDamage, data.energy01)
                : baseDamage;

            if (resistance != null && resistance.tsunamiImmune)
                finalDamage = 0;

            TsunamiBuildingHitData hitData = BuildHitData(
                data,
                baseDamage,
                finalDamage,
                buildingCellsScratch.Count,
                hitCellsScratch);

            if (finalDamage > 0)
            {
                ApplyTsunamiDamageToBuilding(building, hitData);
                damagedCount++;
            }
            else
            {
                hitNoDamageCount++;
            }

            bool sendHitMessage = resistance == null || resistance.receiveTsunamiMessages;

            if (sendHitMessage && (sendHitMessageEvenWhenNoDamage || finalDamage > 0))
                SendTsunamiHitToBuilding(building, hitData);

            if (debugLogging)
            {
                Debug.Log(
                    $"[TsunamiBuildingEffectResolver] Tsunami hit building '{building.name}'. " +
                    $"TsunamiId={data.tsunamiId}, Step={data.stepCount}, " +
                    $"Energy01={data.energy01:0.00}, " +
                    $"HitCells={hitCellsScratch.Count}/{buildingCellsScratch.Count}, " +
                    $"BaseDamage={baseDamage}, FinalDamage={finalDamage}");
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
                $"[TsunamiBuildingEffectResolver] Complete. " +
                $"TsunamiId={data.tsunamiId}, Step={data.stepCount}, " +
                $"Damaged={damagedCount}, HitNoDamage={hitNoDamageCount}, " +
                $"ActiveWaveCells={activeTsunamiCells.Count}");
        }

        resolverRoutine = null;
    }

    private TsunamiBuildingHitData BuildHitData(
        TsunamiAdvancedEventData data,
        int baseDamage,
        int finalDamage,
        int buildingCellCount,
        List<TileCoord> hitCells)
    {
        TsunamiBuildingHitData hitData = new TsunamiBuildingHitData
        {
            tsunamiId = data.tsunamiId,
            stepCount = data.stepCount,

            directionKind = data.directionKind,
            direction = data.direction,

            startEnergy = data.startEnergy,
            energyRemaining = data.energyRemaining,
            energy01 = data.energy01,

            baseDamage = baseDamage,
            finalDamage = finalDamage,

            buildingCellCount = buildingCellCount,
            hitCellCount = hitCells != null ? hitCells.Count : 0
        };

        if (hitCells != null)
        {
            for (int i = 0; i < hitCells.Count; i++)
                hitData.hitCells.Add(hitCells[i]);
        }

        return hitData;
    }

    private void ApplyTsunamiDamageToBuilding(GameObject building, TsunamiBuildingHitData hitData)
    {
        if (building == null || hitData == null || hitData.finalDamage <= 0)
            return;

        BuildingTsunamiSecondaryEffects tsunamiEffects =
            building.GetComponent<BuildingTsunamiSecondaryEffects>();

        if (tsunamiEffects == null)
            tsunamiEffects = building.GetComponentInChildren<BuildingTsunamiSecondaryEffects>(true);

        if (tsunamiEffects == null)
            tsunamiEffects = building.GetComponentInParent<BuildingTsunamiSecondaryEffects>();

        if (tsunamiEffects != null)
        {
            tsunamiEffects.ApplyTsunamiDamage(hitData);
            return;
        }

        BuildingControl buildingControl = building.GetComponent<BuildingControl>();

        if (buildingControl == null)
            buildingControl = building.GetComponentInChildren<BuildingControl>(true);

        if (buildingControl == null)
            buildingControl = building.GetComponentInParent<BuildingControl>();

        if (buildingControl != null)
        {
            buildingControl.ApplyDamage(hitData.finalDamage);

            if (debugLogging)
            {
                Debug.Log(
                    $"[TsunamiBuildingEffectResolver] Applied direct BuildingControl damage " +
                    $"{hitData.finalDamage} to '{buildingControl.name}' because no BuildingTsunamiSecondaryEffects was found.");
            }

            return;
        }

        building.SendMessage(
            damageMessageName,
            hitData,
            SendMessageOptions.DontRequireReceiver);

        if (debugLogging)
        {
            Debug.LogWarning(
                $"[TsunamiBuildingEffectResolver] Could not find BuildingTsunamiSecondaryEffects " +
                $"or BuildingControl on '{building.name}'. SendMessage fallback used.");
        }
    }

    private void SendTsunamiHitToBuilding(GameObject building, TsunamiBuildingHitData hitData)
    {
        if (building == null || hitData == null)
            return;

        BuildingTsunamiSecondaryEffects tsunamiEffects =
            building.GetComponent<BuildingTsunamiSecondaryEffects>();

        if (tsunamiEffects == null)
            tsunamiEffects = building.GetComponentInChildren<BuildingTsunamiSecondaryEffects>(true);

        if (tsunamiEffects == null)
            tsunamiEffects = building.GetComponentInParent<BuildingTsunamiSecondaryEffects>();

        if (tsunamiEffects != null)
        {
            tsunamiEffects.OnTsunamiHit(hitData);
            return;
        }

        building.SendMessage(
            hitMessageName,
            hitData,
            SendMessageOptions.DontRequireReceiver);
    }

    private void GetHitCells(
        List<TileCoord> buildingCells,
        HashSet<TileCoord> tsunamiCells,
        List<TileCoord> results)
    {
        results.Clear();

        if (buildingCells == null || tsunamiCells == null)
            return;

        for (int i = 0; i < buildingCells.Count; i++)
        {
            TileCoord c = buildingCells[i];

            if (tsunamiCells.Contains(c))
                results.Add(c);
        }
    }

    private void GetBuildingCoveredCells(
        WorldBuildingManager.Record record,
        List<TileCoord> results)
    {
        results.Clear();

        if (record == null || record.instance == null)
            return;

        GameObject building = record.instance;

        if (!useBuildingFootprintCells)
        {
            AddFallbackBuildingCell(building, results);
            return;
        }

        if (!TryGetWorldBounds(building.transform, out Bounds bounds))
        {
            AddFallbackBuildingCell(building, results);
            return;
        }

        float inset = gridManager != null
            ? gridManager.cellSize * boundsCellInset
            : 0.05f;

        Vector3 minWorld = new Vector3(
            bounds.min.x + inset,
            0f,
            bounds.min.z + inset);

        Vector3 maxWorld = new Vector3(
            bounds.max.x - inset,
            0f,
            bounds.max.z - inset);

        Vector2Int min = gridManager.GetGridPosition(minWorld);
        Vector2Int max = gridManager.GetGridPosition(maxWorld);

        int minX = Mathf.Min(min.x, max.x);
        int maxX = Mathf.Max(min.x, max.x);
        int minY = Mathf.Min(min.y, max.y);
        int maxY = Mathf.Max(min.y, max.y);

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                TileCoord coord = new TileCoord(x, y);

                if (!IsCellInsideGrid(coord))
                    continue;

                if (!results.Contains(coord))
                    results.Add(coord);
            }
        }

        if (results.Count == 0)
            AddFallbackBuildingCell(building, results);
    }

    private bool TryGetWorldBounds(Transform root, out Bounds bounds)
    {
        if (root == null)
        {
            bounds = default;
            return false;
        }

        Collider ownCollider = root.GetComponent<Collider>();
        if (ownCollider != null)
        {
            bounds = ownCollider.bounds;
            return true;
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
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

        Renderer ownRenderer = root.GetComponent<Renderer>();
        if (ownRenderer != null)
        {
            bounds = ownRenderer.bounds;
            return true;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
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

        bounds = default;
        return false;
    }

    private void AddFallbackBuildingCell(GameObject building, List<TileCoord> results)
    {
        if (building == null || gridManager == null)
            return;

        Vector2Int cell = gridManager.GetGridPosition(building.transform.position);
        TileCoord coord = new TileCoord(cell.x, cell.y);

        if (IsCellInsideGrid(coord) && !results.Contains(coord))
            results.Add(coord);
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

    private void ResolveReferences()
    {
        if (simulationSystem == null)
            simulationSystem = TsunamiSimulationSystem.Instance;

        if (simulationSystem == null)
            simulationSystem = FindObjectOfType<TsunamiSimulationSystem>();

        if (gridManager == null)
            gridManager = GridManager.Instance;

        if (worldBuildingManager == null)
            worldBuildingManager = PlayerBuildingManager.Instance;
    }
}