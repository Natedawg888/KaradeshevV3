using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FloodBuildingEffectResolver : MonoBehaviour
{
    [Header("References")]
    public FloodSimulationSystem floodSimulationSystem;
    public GridManager gridManager;
    public WorldBuildingManager worldBuildingManager;

    [Header("Damage")]
    public bool damageBuildings = true;

    [Tooltip("Flood depth must be at or above this value to damage buildings.")]
    [Range(0f, 1f)]
    public float damageStartsAtDepth01 = 0.18f;

    [Tooltip("Depth at or above this uses maximum damage.")]
    [Range(0f, 1f)]
    public float severeDamageDepth01 = 1f;

    public int minDamage = 1;
    public int maxDamage = 10;

    [Header("Over-Time Rules")]
    [Tooltip("If true, each building can only take flood damage once per turn.")]
    public bool damageEachBuildingOncePerTurn = true;

    [Tooltip("If true, only damage buildings when flood cells changed. Recommended.")]
    public bool processOnlyWhenFloodChanges = true;

    [Header("Footprint Coverage Scaling")]
    [Tooltip("If true, buildings hit on more cells take more damage.")]
    public bool scaleDamageByHitCellCount = true;

    [Tooltip("Extra damage multiplier per additional flooded building cell. Example: 0.10 = +10% per extra hit cell.")]
    [Min(0f)]
    public float extraDamageMultiplierPerAdditionalHitCell = 0.10f;

    [Tooltip("Maximum multiplier from hit cell count.")]
    [Min(1f)]
    public float maxHitCellDamageMultiplier = 1.75f;

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
    public string damageMessageName = "ApplyFloodDamage";
    public string hitMessageName = "OnFloodHit";

    public bool sendHitMessageEvenWhenNoDamage = false;

    [Header("Debug")]
    public bool debugLogging = true;

    private Coroutine resolverRoutine;
    private FloodSimulationSystem subscribedFloodSimulationSystem;

    private readonly HashSet<TileCoord> activeFloodCells =
        new HashSet<TileCoord>();

    private readonly List<TileCoord> buildingCellsScratch =
        new List<TileCoord>();

    private readonly List<TileCoord> hitCellsScratch =
        new List<TileCoord>();

    private readonly Dictionary<int, int> lastDamagedTurnByBuildingInstanceId =
        new Dictionary<int, int>();

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        RebindFloodSubscription();
    }

    private void OnDisable()
    {
        UnbindFloodSubscription();

        if (resolverRoutine != null)
        {
            StopCoroutine(resolverRoutine);
            resolverRoutine = null;
        }
    }

    public void InstallRuntimeRefs(
        FloodSimulationSystem newFloodSimulationSystem,
        GridManager newGridManager,
        WorldBuildingManager newWorldBuildingManager)
    {
        if (newFloodSimulationSystem != null)
            floodSimulationSystem = newFloodSimulationSystem;

        if (newGridManager != null)
            gridManager = newGridManager;

        if (newWorldBuildingManager != null)
            worldBuildingManager = newWorldBuildingManager;

        RebindFloodSubscription();

        if (debugLogging)
        {
            //Debug.Log(
                //$"[FloodBuildingEffectResolver] Installed refs. " +
                //$"Flood={(floodSimulationSystem != null ? floodSimulationSystem.name : "NULL")}, " +
                //$"GridManager={(gridManager != null ? gridManager.name : "NULL")}, " +
                //$"WorldBuildingManager={(worldBuildingManager != null ? worldBuildingManager.name : "NULL")}");
        }
    }

    public void SetWorldBuildingManager(WorldBuildingManager newWorldBuildingManager)
    {
        if (worldBuildingManager == newWorldBuildingManager)
            return;

        worldBuildingManager = newWorldBuildingManager;

        if (debugLogging)
        {
            //Debug.Log(
                //$"[FloodBuildingEffectResolver] SetWorldBuildingManager -> " +
                //$"{(worldBuildingManager != null ? worldBuildingManager.name : "NULL")}");
        }
    }

    private void RebindFloodSubscription()
    {
        if (subscribedFloodSimulationSystem == floodSimulationSystem)
            return;

        UnbindFloodSubscription();

        subscribedFloodSimulationSystem = floodSimulationSystem;

        if (subscribedFloodSimulationSystem != null)
            subscribedFloodSimulationSystem.OnFloodCellsChanged += HandleFloodCellsChanged;
    }

    private void UnbindFloodSubscription()
    {
        if (subscribedFloodSimulationSystem == null)
            return;

        subscribedFloodSimulationSystem.OnFloodCellsChanged -= HandleFloodCellsChanged;
        subscribedFloodSimulationSystem = null;
    }

    private void HandleFloodCellsChanged(IReadOnlyList<TileCoord> changedCells)
    {
        if (!damageBuildings)
            return;

        if (processOnlyWhenFloodChanges &&
            (changedCells == null || changedCells.Count == 0))
        {
            return;
        }

        if (resolverRoutine != null)
            StopCoroutine(resolverRoutine);

        resolverRoutine = StartCoroutine(ProcessBuildings());
    }

    [ContextMenu("Debug/Process Flood Building Damage Now")]
    public void ProcessFloodBuildingDamageNow()
    {
        if (resolverRoutine != null)
            StopCoroutine(resolverRoutine);

        resolverRoutine = StartCoroutine(ProcessBuildings());
    }

    private IEnumerator ProcessBuildings()
    {
        ResolveReferences();

        if (floodSimulationSystem == null || gridManager == null || worldBuildingManager == null)
        {
            if (debugLogging) {}
                //Debug.LogWarning("[FloodBuildingEffectResolver] Missing references.");

            resolverRoutine = null;
            yield break;
        }

        IReadOnlyList<WorldBuildingManager.Record> records = worldBuildingManager.GetAll();

        if (records == null || records.Count == 0)
        {
            resolverRoutine = null;
            yield break;
        }

        activeFloodCells.Clear();

        foreach (KeyValuePair<TileCoord, FloodCellState> pair in floodSimulationSystem.ActiveFloodCells)
        {
            if (pair.Value == null)
                continue;

            if (pair.Value.floodDepth01 < damageStartsAtDepth01)
                continue;

            activeFloodCells.Add(pair.Key);
        }

        if (activeFloodCells.Count == 0)
        {
            resolverRoutine = null;
            yield break;
        }

        int currentTurn = TurnSystem.GetCurrentTurn();

        int processed = 0;
        int damagedCount = 0;
        int hitNoDamageCount = 0;
        int skippedSameTurnCount = 0;

        for (int i = 0; i < records.Count; i++)
        {
            WorldBuildingManager.Record record = records[i];

            if (record == null || record.instance == null)
                continue;

            GameObject building = record.instance;
            int buildingId = building.GetInstanceID();

            if (damageEachBuildingOncePerTurn &&
                lastDamagedTurnByBuildingInstanceId.TryGetValue(buildingId, out int lastTurn) &&
                lastTurn == currentTurn)
            {
                skippedSameTurnCount++;
                continue;
            }

            GetBuildingCoveredCells(record, buildingCellsScratch);

            if (buildingCellsScratch.Count == 0)
                continue;

            GetHitCells(buildingCellsScratch, hitCellsScratch);

            if (hitCellsScratch.Count == 0)
                continue;

            float averageDepth01 = GetAverageFloodDepth(hitCellsScratch);
            float maxDepth01 = GetMaxFloodDepth(hitCellsScratch);

            float minDepth = Mathf.Min(damageStartsAtDepth01, severeDamageDepth01);
            float maxDepth = Mathf.Max(damageStartsAtDepth01, severeDamageDepth01);

            float depthDamage01 = Mathf.Clamp01(
                Mathf.InverseLerp(minDepth, maxDepth, maxDepth01));

            if (depthDamage01 <= 0f)
                continue;

            int baseDamage = Mathf.RoundToInt(
                Mathf.Lerp(minDamage, maxDamage, depthDamage01));

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

            BuildingFloodResistance resistance =
                building.GetComponent<BuildingFloodResistance>();

            if (resistance == null)
                resistance = building.GetComponentInChildren<BuildingFloodResistance>(true);

            if (resistance == null)
                resistance = building.GetComponentInParent<BuildingFloodResistance>();

            int finalDamage = resistance != null
                ? resistance.ModifyDamage(baseDamage, maxDepth01)
                : baseDamage;

            if (resistance != null && resistance.floodImmune)
                finalDamage = 0;

            FloodBuildingHitData hitData = BuildHitData(
                currentTurn,
                averageDepth01,
                maxDepth01,
                baseDamage,
                finalDamage,
                buildingCellsScratch.Count,
                hitCellsScratch);

            if (finalDamage > 0)
            {
                ApplyFloodDamageToBuilding(building, hitData);
                damagedCount++;

                if (damageEachBuildingOncePerTurn)
                    lastDamagedTurnByBuildingInstanceId[buildingId] = currentTurn;
            }
            else
            {
                hitNoDamageCount++;
            }

            bool sendHitMessage = resistance == null || resistance.receiveFloodMessages;

            if (sendHitMessage && (sendHitMessageEvenWhenNoDamage || finalDamage > 0))
                SendFloodHitToBuilding(building, hitData);

            if (debugLogging)
            {
                //Debug.Log(
                    //$"[FloodBuildingEffectResolver] Flood hit building '{building.name}'. " +
                    //$"Turn={currentTurn}, AvgDepth={averageDepth01:0.00}, MaxDepth={maxDepth01:0.00}, " +
                    //$"HitCells={hitCellsScratch.Count}/{buildingCellsScratch.Count}, " +
                    //$"BaseDamage={baseDamage}, FinalDamage={finalDamage}");
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
            //Debug.Log(
                //$"[FloodBuildingEffectResolver] Complete. " +
                //$"Turn={currentTurn}, Damaged={damagedCount}, HitNoDamage={hitNoDamageCount}, " +
                //$"SkippedSameTurn={skippedSameTurnCount}, ActiveFloodDamageCells={activeFloodCells.Count}");
        }

        resolverRoutine = null;
    }

    private FloodBuildingHitData BuildHitData(
        int turnIndex,
        float averageDepth01,
        float maxDepth01,
        int baseDamage,
        int finalDamage,
        int buildingCellCount,
        List<TileCoord> hitCells)
    {
        FloodBuildingHitData hitData = new FloodBuildingHitData
        {
            turnIndex = turnIndex,
            averageDepth01 = averageDepth01,
            maxDepth01 = maxDepth01,
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

    private void ApplyFloodDamageToBuilding(GameObject building, FloodBuildingHitData hitData)
    {
        if (building == null || hitData == null || hitData.finalDamage <= 0)
            return;

        BuildingFloodSecondaryEffects floodEffects =
            building.GetComponent<BuildingFloodSecondaryEffects>();

        if (floodEffects == null)
            floodEffects = building.GetComponentInChildren<BuildingFloodSecondaryEffects>(true);

        if (floodEffects == null)
            floodEffects = building.GetComponentInParent<BuildingFloodSecondaryEffects>();

        if (floodEffects != null)
        {
            floodEffects.ApplyFloodDamage(hitData);
            PostFloodNotification(building, hitData);
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
            PostFloodNotification(building, hitData);

            if (debugLogging)
            {
                //Debug.Log(
                    //$"[FloodBuildingEffectResolver] Applied direct BuildingControl damage " +
                    //$"{hitData.finalDamage} to '{buildingControl.name}' because no BuildingFloodSecondaryEffects was found.");
            }

            return;
        }

        building.SendMessage(
            damageMessageName,
            hitData,
            SendMessageOptions.DontRequireReceiver);

        if (debugLogging)
        {
            //Debug.LogWarning(
                //$"[FloodBuildingEffectResolver] Could not find BuildingFloodSecondaryEffects " +
                //$"or BuildingControl on '{building.name}'. SendMessage fallback used.");
        }
    }

    private void SendFloodHitToBuilding(GameObject building, FloodBuildingHitData hitData)
    {
        if (building == null || hitData == null)
            return;

        BuildingFloodSecondaryEffects floodEffects =
            building.GetComponent<BuildingFloodSecondaryEffects>();

        if (floodEffects == null)
            floodEffects = building.GetComponentInChildren<BuildingFloodSecondaryEffects>(true);

        if (floodEffects == null)
            floodEffects = building.GetComponentInParent<BuildingFloodSecondaryEffects>();

        if (floodEffects != null)
        {
            floodEffects.OnFloodHit(hitData);
            return;
        }

        building.SendMessage(
            hitMessageName,
            hitData,
            SendMessageOptions.DontRequireReceiver);
    }

    private void GetHitCells(
        List<TileCoord> buildingCells,
        List<TileCoord> results)
    {
        results.Clear();

        if (buildingCells == null)
            return;

        for (int i = 0; i < buildingCells.Count; i++)
        {
            TileCoord c = buildingCells[i];

            if (activeFloodCells.Contains(c))
                results.Add(c);
        }
    }

    private float GetAverageFloodDepth(List<TileCoord> cells)
    {
        if (cells == null || cells.Count == 0 || floodSimulationSystem == null)
            return 0f;

        float total = 0f;
        int count = 0;

        for (int i = 0; i < cells.Count; i++)
        {
            if (!floodSimulationSystem.TryGetFloodCell(cells[i], out FloodCellState state))
                continue;

            if (state == null)
                continue;

            total += state.floodDepth01;
            count++;
        }

        return count > 0 ? total / count : 0f;
    }

    private float GetMaxFloodDepth(List<TileCoord> cells)
    {
        if (cells == null || cells.Count == 0 || floodSimulationSystem == null)
            return 0f;

        float max = 0f;

        for (int i = 0; i < cells.Count; i++)
        {
            if (!floodSimulationSystem.TryGetFloodCell(cells[i], out FloodCellState state))
                continue;

            if (state == null)
                continue;

            max = Mathf.Max(max, state.floodDepth01);
        }

        return max;
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

    private void PostFloodNotification(GameObject building, FloodBuildingHitData hitData)
    {
        if (NotificationManager.Instance == null || building == null || hitData == null) return;

        var control = building.GetComponent<BuildingControl>();
        if (control == null) control = building.GetComponentInParent<BuildingControl>();
        string buildingName = control != null && !string.IsNullOrWhiteSpace(control.buildingName)
            ? control.buildingName
            : building.name;

        string depthLabel = hitData.maxDepth01 >= 0.66f ? "deep"
                          : hitData.maxDepth01 >= 0.33f ? "moderate"
                          : "shallow";

        string title, message;
        if (NotificationMessageCrafterManager.Instance != null)
            (title, message) = NotificationMessageCrafterManager.Instance.CraftFlood(buildingName, depthLabel);
        else
        {
            title   = "Building Flooded";
            message = $"{buildingName} is being flooded ({depthLabel} water).";
        }

        NotificationManager.Instance.AddNotification(
            NotificationType.BuildingFlooded, title, message, building.transform.position);
    }

    private void ResolveReferences()
    {
        if (floodSimulationSystem == null)
            floodSimulationSystem = FindFirstObjectByType<FloodSimulationSystem>();

        if (gridManager == null)
            gridManager = GridManager.Instance;

        if (worldBuildingManager == null)
            worldBuildingManager = WorldBuildingManager.Instance;
    }
}
