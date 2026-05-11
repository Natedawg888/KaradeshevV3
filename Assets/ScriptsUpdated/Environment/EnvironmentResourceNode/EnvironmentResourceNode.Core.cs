using System;
using System.Collections.Generic;
using UnityEngine;

public partial class EnvironmentResourceNode : MonoBehaviour
{
    [Header("References")]
    public EnvironmentControl environmentControl;
    public GridManager gridManager;

    [Header("Spawn configuration")]
    public int totalCapacity = 0;

    [Header("Resulting spawned resources (read-only)")]
    [SerializeField]
    private List<ResourceSpawnEntry> spawnedResources = new();

    public IReadOnlyList<ResourceSpawnEntry> SpawnedResources => spawnedResources;
    public List<ResourceSpawnEntry> MutableSpawnedResources => spawnedResources;

    // Extra-spawn tracking
    private int turnsSinceLastExtraSpawn = 0;
    private const int ExtraSpawnInterval = 4;

    [Header("Environment Health")]
    public int maxEnvironmentHealth = 100;
    [SerializeField] private int currentEnvironmentHealth;
    public int environmentRecoveryPerTick = 1;

    public int CurrentEnvironmentHealth => currentEnvironmentHealth;
    public int MaxEnvironmentHealth => maxEnvironmentHealth;

    [Header("Spawner-Based Resources")]
    [Tooltip("Assign ResourceSpawnerDefinition SOs here.")]
    public List<ResourceSpawnerDefinition> baseSpawners = new();

    [SerializeField, HideInInspector]
    private List<ResourceSpawnerRuntime> activeSpawners = new();

    /// <summary>
    /// Returns every spawner definition this node knows about — both base-environment
    /// spawners and any currently-active dynamic spawners (burnt, carcass, flood, etc.).
    /// Definitions are deduplicated by spawnerID.
    /// </summary>
    public void CollectAllSpawnerDefinitions(HashSet<string> seenIds, List<ResourceSpawnerDefinition> results)
    {
        if (baseSpawners != null)
            foreach (var def in baseSpawners)
                if (def != null && seenIds.Add(def.spawnerID)) results.Add(def);

        if (activeSpawners != null)
            foreach (var runtime in activeSpawners)
                if (runtime?.definition != null && seenIds.Add(runtime.definition.spawnerID))
                    results.Add(runtime.definition);
    }

    [HideInInspector]
    public TileStateFlags currentTileState = TileStateFlags.None;

    [Header("Barren Settings")]
    public int barrenRecoveryTurns = 8;

    [Header("Barren UI (World Canvas)")]
    public GameObject barrenIcon;
    public TimerUI barrenTimerUI;

    [Header("Barren Degradation / Auto-Clear")]
    public int barrenRecoveryIncreasePerUse = 2;
    public int barrenRecoveryClearThreshold = 40;
    public bool allowImmediateClearOnOveruse = true;
    public EnvironmentClearingTask clearingTaskPrefab;

    private bool isBarren = false;

    [SerializeField, Tooltip("Read-only: barren turns left.")]
    private int barrenTurnsLeft = 0;

    public bool IsBarren => isBarren;
    public int BarrenTurnsLeft => barrenTurnsLeft;

    private void OnEnable()
    {
        if (ResourceNodeManager.Instance != null)
            ResourceNodeManager.Instance.RegisterNode(this);

        EnvironmentResourceUpdater.RegisterNode(this);
    }

    private void OnDisable()
    {
        if (ResourceNodeManager.Instance != null)
            ResourceNodeManager.Instance.UnregisterNode(this);

        EnvironmentResourceUpdater.UnregisterNode(this);
    }

    private void Start()
    {
        if (environmentControl == null)
            environmentControl = GetComponent<EnvironmentControl>();

        if (environmentControl == null)
            return;

        var envType  = environmentControl.environmentType;
        var tileType = environmentControl.environmentTileType;
        var size     = environmentControl.tileSize;

        totalCapacity = EnvironmentResourceCapacityCalculator.CalculateTotalCapacity(
            envType, tileType, size);

        if (barrenRecoveryTurns <= 0)
            barrenRecoveryTurns = EnvironmentBarrenRecoveryCalculator.CalculateBarrenRecoveryTurns(
                envType, tileType, size);

        if (barrenRecoveryIncreasePerUse <= 0)
            barrenRecoveryIncreasePerUse = EnvironmentBarrenDegradationCalculator.CalculateRecoveryIncreasePerUse(
                envType, tileType, size);

        if (barrenRecoveryClearThreshold <= 0)
            barrenRecoveryClearThreshold = EnvironmentBarrenDegradationCalculator.CalculateRecoveryClearThreshold(
                envType, tileType, size);

        if (maxEnvironmentHealth <= 0)
            maxEnvironmentHealth = EnvironmentHealthCalculator.CalculateMaxHealth(
                envType, tileType, size);

        if (environmentRecoveryPerTick <= 0)
            environmentRecoveryPerTick = EnvironmentHealthCalculator.CalculateRecoveryPerTick(
                envType, tileType, size);

        if (currentEnvironmentHealth <= 0 || currentEnvironmentHealth > maxEnvironmentHealth)
            currentEnvironmentHealth = maxEnvironmentHealth;

        ApplyBarrenVisuals();
        InitializeSpawners();
        GenerateResources();
    }

    private void OnValidate()
    {
        if (environmentControl == null)
            environmentControl = GetComponent<EnvironmentControl>();

        if (environmentControl != null && environmentControl.canvas != null)
        {
            if (barrenIcon == null)
            {
                var t = environmentControl.canvas.transform.Find("BarrenIcon");
                if (t != null)
                    barrenIcon = t.gameObject;
            }

            if (barrenTimerUI == null)
            {
                var t = environmentControl.canvas.transform.Find("BarrenTimer");
                if (t != null)
                    barrenTimerUI = t.GetComponent<TimerUI>()
                                    ?? t.GetComponentInChildren<TimerUI>();
            }
        }

        ApplyBarrenVisuals();
    }

    public EnvironmentResourceNodeRuntimeSaveData CaptureRuntimeSaveData()
    {
        var data = new EnvironmentResourceNodeRuntimeSaveData
        {
            totalCapacity              = totalCapacity,
            maxEnvironmentHealth       = maxEnvironmentHealth,
            currentEnvironmentHealth   = currentEnvironmentHealth,
            environmentRecoveryPerTick = environmentRecoveryPerTick,

            barrenRecoveryTurns          = barrenRecoveryTurns,
            barrenRecoveryIncreasePerUse = barrenRecoveryIncreasePerUse,
            barrenRecoveryClearThreshold = barrenRecoveryClearThreshold,
            allowImmediateClearOnOveruse = allowImmediateClearOnOveruse,

            isBarren     = isBarren,
            barrenTurnsLeft = barrenTurnsLeft,

            turnsSinceLastExtraSpawn = turnsSinceLastExtraSpawn
        };

        if (spawnedResources != null)
        {
            foreach (var entry in spawnedResources)
            {
                var def    = GetEntryDefinition(entry);
                int amount = GetEntryAmount(entry);
                if (def == null || amount <= 0) continue;

                data.spawnedResources.Add(new EnvironmentNodeResourceEntrySaveData
                {
                    resourceKey = def.name,
                    amount      = amount
                });
            }
        }

        // Save tile state flags
        data.tileStateFlags = (int)currentTileState;

        // Save only non-base (temporary) active spawners.
        // Base spawners are deterministic and re-created by InitializeSpawners() on load.
        if (activeSpawners != null)
        {
            for (int i = 0; i < activeSpawners.Count; i++)
            {
                var rt = activeSpawners[i];
                if (rt?.definition == null) continue;
                if (rt.sourceReason == SpawnerSourceReason.BaseEnvironment) continue;

                data.activeSpawners.Add(new SpawnerRuntimeSaveData
                {
                    spawnerID              = rt.definition.spawnerID,
                    sourceReason           = (int)rt.sourceReason,
                    turnsSinceLastSpawn    = rt.turnsSinceLastSpawn,
                    remainingUses          = rt.remainingUses,
                    remainingLifetimeTurns = rt.remainingLifetimeTurns
                });
            }
        }

        return data;
    }

    public void ApplyRuntimeSaveData(
        EnvironmentResourceNodeRuntimeSaveData data,
        Func<string, ResourceDefinition> resourceResolver)
    {
        if (data == null) return;

        totalCapacity              = Mathf.Max(0, data.totalCapacity);
        maxEnvironmentHealth       = Mathf.Max(1, data.maxEnvironmentHealth);
        currentEnvironmentHealth   = Mathf.Clamp(data.currentEnvironmentHealth, 0, maxEnvironmentHealth);
        environmentRecoveryPerTick = Mathf.Max(0, data.environmentRecoveryPerTick);

        barrenRecoveryTurns          = Mathf.Max(0, data.barrenRecoveryTurns);
        barrenRecoveryIncreasePerUse = Mathf.Max(0, data.barrenRecoveryIncreasePerUse);
        barrenRecoveryClearThreshold = Mathf.Max(0, data.barrenRecoveryClearThreshold);
        allowImmediateClearOnOveruse = data.allowImmediateClearOnOveruse;

        isBarren                 = data.isBarren;
        barrenTurnsLeft          = Mathf.Max(0, data.barrenTurnsLeft);
        turnsSinceLastExtraSpawn = Mathf.Max(0, data.turnsSinceLastExtraSpawn);

        spawnedResources.Clear();

        if (data.spawnedResources != null)
        {
            foreach (var saved in data.spawnedResources)
            {
                if (saved == null || string.IsNullOrWhiteSpace(saved.resourceKey) || saved.amount <= 0)
                    continue;

                var def = resourceResolver != null ? resourceResolver(saved.resourceKey) : null;
                if (def == null) continue;

                spawnedResources.Add(CreateEntry(def, saved.amount));
            }
        }

        ApplyBarrenVisuals();

        if (barrenTimerUI != null)
        {
            barrenTimerUI.gameObject.SetActive(isBarren && barrenTurnsLeft > 0);
            if (isBarren && barrenTurnsLeft > 0)
            {
                barrenTimerUI.Initialize(Mathf.Max(1, barrenRecoveryTurns));
                barrenTimerUI.UpdateTimer(barrenTurnsLeft);
            }
        }

        // Restore tile state flags
        currentTileState = (TileStateFlags)data.tileStateFlags;

        // Restore temporary spawners. Base spawners are already present via InitializeSpawners()
        // which runs in Start() before ApplyRuntimeSaveData is called.
        if (data.activeSpawners != null && data.activeSpawners.Count > 0)
        {
            var registry = ResourceSpawnerRegistry.Instance;
            if (registry != null)
            {
                for (int i = 0; i < data.activeSpawners.Count; i++)
                {
                    var saved = data.activeSpawners[i];
                    if (string.IsNullOrWhiteSpace(saved.spawnerID)) continue;

                    var def = registry.GetByID(saved.spawnerID);
                    if (def == null) continue;

                    activeSpawners.Add(new ResourceSpawnerRuntime
                    {
                        definition             = def,
                        isActive               = true,
                        sourceReason           = (SpawnerSourceReason)saved.sourceReason,
                        turnsSinceLastSpawn    = saved.turnsSinceLastSpawn,
                        remainingUses          = saved.remainingUses,
                        remainingLifetimeTurns = saved.remainingLifetimeTurns
                    });
                }
            }
        }
    }

    private static ResourceDefinition GetEntryDefinition(ResourceSpawnEntry entry) => entry.definition;
    private static int GetEntryAmount(ResourceSpawnEntry entry) => entry.amount;

    private static ResourceSpawnEntry CreateEntry(ResourceDefinition def, int amount)
    {
        var entry = new ResourceSpawnEntry { definition = def };
        entry.Initialize(amount);
        return entry;
    }
}
