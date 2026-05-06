using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class EnvironmentResourceNode : MonoBehaviour
{
    [Header("References")]
    public EnvironmentControl environmentControl;
    public GridManager gridManager;

    [Header("Spawn configuration")]
    public int maxVarietyCap = 0;
    public int totalCapacity = 0;

    [Header("Allowed Resources (runtime filtered by season)")]
    public List<ResourceDefinition> resourceDefinitions = new();

    // base list (environment/tile filtered, ignoring season)
    private List<ResourceDefinition> baseResourceDefinitions = new();

    [Header("Resulting spawned resources (read-only)")]
    [SerializeField]
    private List<ResourceSpawnEntry> spawnedResources = new();

    public IReadOnlyList<ResourceSpawnEntry> SpawnedResources => spawnedResources;
    public List<ResourceSpawnEntry> MutableSpawnedResources => spawnedResources;

    // Extra-spawn tracking
    private int turnsSinceLastExtraSpawn = 0;
    private const int ExtraSpawnInterval = 4;

    [Range(0f, 3f)]
    public float outOfSeasonFavorMultiplier = 1.5f;

    [System.Serializable]
    public class GuaranteedSpawn
    {
        [Tooltip("Matches ResourceDefinition.resourceID (case-insensitive).")]
        public string resourceId;

        [Tooltip("If true, only spawn when the resource is allowed in the current season.")]
        public bool respectSeason = true;
    }

    [Header("Guaranteed Spawns")]
    public List<GuaranteedSpawn> guaranteedSpawns = new();

    [Header("Environment Health")]
    public int maxEnvironmentHealth = 100;
    [SerializeField] private int currentEnvironmentHealth;
    public int environmentRecoveryPerTick = 1;

    public int CurrentEnvironmentHealth => currentEnvironmentHealth;
    public int MaxEnvironmentHealth => maxEnvironmentHealth;

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

    // key = (EnvironmentType, EnvironmentTileType)
    private static readonly Dictionary<(EnvironmentType env, EnvironmentTileType tile), List<ResourceDefinition>>
        s_baseDefsByEnvTile = new();

    private void OnEnable()
    {
        if (SeasonManager.Instance != null)
            SeasonManager.Instance.OnSeasonChanged += HandleSeasonChanged;

        if (ResourceNodeManager.Instance != null)
            ResourceNodeManager.Instance.RegisterNode(this);
    }

    private void OnDisable()
    {
        if (SeasonManager.Instance != null)
            SeasonManager.Instance.OnSeasonChanged -= HandleSeasonChanged;

        if (ResourceNodeManager.Instance != null)
            ResourceNodeManager.Instance.UnregisterNode(this);
    }

    private void Start()
    {
        if (environmentControl == null)
            environmentControl = GetComponent<EnvironmentControl>();

        if (environmentControl == null)
        {
            Debug.LogWarning($"{nameof(EnvironmentResourceNode)} on '{name}' has no EnvironmentControl; aborting resource generation.");
            return;
        }

        var envType = environmentControl.environmentType;
        var tileType = environmentControl.environmentTileType;
        var size = environmentControl.tileSize;

        SeasonDefinition currentSeason = SeasonManager.Instance != null
            ? SeasonManager.Instance.CurrentSeason
            : null;

        // base definitions ignore season
        if (resourceDefinitions == null || resourceDefinitions.Count == 0)
        {
            baseResourceDefinitions = GetBaseDefinitionsFor(envType, tileType);
        }
        else
        {
            baseResourceDefinitions = resourceDefinitions
                .Where(def => def != null && def.IsAvailableOnTile(envType, tileType))
                .ToList();
        }

        // runtime list is season-filtered
        UpdateAllowedDefinitions(currentSeason);

        totalCapacity = EnvironmentResourceCapacityCalculator.CalculateTotalCapacity(
            envType, tileType, size);
        maxVarietyCap = Mathf.Max(1, baseResourceDefinitions.Count);

        if (barrenRecoveryTurns <= 0)
        {
            barrenRecoveryTurns = EnvironmentBarrenRecoveryCalculator.CalculateBarrenRecoveryTurns(
                envType,
                tileType,
                size
            );
        }

        if (barrenRecoveryIncreasePerUse <= 0)
        {
            barrenRecoveryIncreasePerUse = EnvironmentBarrenDegradationCalculator.CalculateRecoveryIncreasePerUse(
                envType,
                tileType,
                size
            );
        }

        if (barrenRecoveryClearThreshold <= 0)
        {
            barrenRecoveryClearThreshold = EnvironmentBarrenDegradationCalculator.CalculateRecoveryClearThreshold(
                envType,
                tileType,
                size
            );
        }

        if (maxEnvironmentHealth <= 0)
        {
            maxEnvironmentHealth = EnvironmentHealthCalculator.CalculateMaxHealth(
                envType,
                tileType,
                size
            );
        }

        if (environmentRecoveryPerTick <= 0)
        {
            environmentRecoveryPerTick = EnvironmentHealthCalculator.CalculateRecoveryPerTick(
                envType,
                tileType,
                size
            );
        }

        if (currentEnvironmentHealth <= 0 || currentEnvironmentHealth > maxEnvironmentHealth)
            currentEnvironmentHealth = maxEnvironmentHealth;

        ApplyBarrenVisuals();
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

    private void HandleSeasonChanged(SeasonDefinition newSeason)
    {
        if (environmentControl == null)
            return;

        UpdateAllowedDefinitions(newSeason);
    }

    private void UpdateAllowedDefinitions(SeasonDefinition season)
    {
        if (baseResourceDefinitions == null)
        {
            resourceDefinitions = new List<ResourceDefinition>();
            return;
        }

        resourceDefinitions = baseResourceDefinitions
            .Where(def => def != null && def.IsAllowedInSeason(season))
            .ToList();
    }

    private static List<ResourceDefinition> GetBaseDefinitionsFor(EnvironmentType envType,
                                                                  EnvironmentTileType tileType)
    {
        var key = (envType, tileType);

        if (s_baseDefsByEnvTile.TryGetValue(key, out var cached))
            return cached;

        var dict = ResourceDictionary.Instance;
        if (dict == null || dict.allResources == null)
        {
            Debug.LogWarning($"[EnvironmentResourceNode] No ResourceDictionary.Instance; env={envType}, tile={tileType}");
            cached = new List<ResourceDefinition>();
        }
        else
        {
            cached = new List<ResourceDefinition>();
            foreach (var def in dict.allResources)
            {
                if (def == null) continue;
                if (def.IsAvailableOnTile(envType, tileType))
                    cached.Add(def);
            }
        }

        s_baseDefsByEnvTile[key] = cached;
        return cached;
    }

    public EnvironmentResourceNodeRuntimeSaveData CaptureRuntimeSaveData()
    {
        var data = new EnvironmentResourceNodeRuntimeSaveData
        {
            maxVarietyCap = maxVarietyCap,
            totalCapacity = totalCapacity,

            maxEnvironmentHealth = maxEnvironmentHealth,
            currentEnvironmentHealth = currentEnvironmentHealth,
            environmentRecoveryPerTick = environmentRecoveryPerTick,

            barrenRecoveryTurns = barrenRecoveryTurns,
            barrenRecoveryIncreasePerUse = barrenRecoveryIncreasePerUse,
            barrenRecoveryClearThreshold = barrenRecoveryClearThreshold,
            allowImmediateClearOnOveruse = allowImmediateClearOnOveruse,

            isBarren = isBarren,
            barrenTurnsLeft = barrenTurnsLeft,

            turnsSinceLastExtraSpawn = turnsSinceLastExtraSpawn,
            outOfSeasonFavorMultiplier = outOfSeasonFavorMultiplier
        };

        if (spawnedResources != null)
        {
            foreach (var entry in spawnedResources)
            {
                ResourceDefinition def = GetEntryDefinition(entry);
                int amount = GetEntryAmount(entry);

                if (def == null || amount <= 0)
                    continue;

                data.spawnedResources.Add(new EnvironmentNodeResourceEntrySaveData
                {
                    resourceKey = def.name,
                    amount = amount
                });
            }
        }

        return data;
    }

    public void ApplyRuntimeSaveData(
        EnvironmentResourceNodeRuntimeSaveData data,
        Func<string, ResourceDefinition> resourceResolver)
    {
        if (data == null)
            return;

        maxVarietyCap = Mathf.Max(0, data.maxVarietyCap);
        totalCapacity = Mathf.Max(0, data.totalCapacity);

        maxEnvironmentHealth = Mathf.Max(1, data.maxEnvironmentHealth);
        currentEnvironmentHealth = Mathf.Clamp(data.currentEnvironmentHealth, 0, maxEnvironmentHealth);
        environmentRecoveryPerTick = Mathf.Max(0, data.environmentRecoveryPerTick);

        barrenRecoveryTurns = Mathf.Max(0, data.barrenRecoveryTurns);
        barrenRecoveryIncreasePerUse = Mathf.Max(0, data.barrenRecoveryIncreasePerUse);
        barrenRecoveryClearThreshold = Mathf.Max(0, data.barrenRecoveryClearThreshold);
        allowImmediateClearOnOveruse = data.allowImmediateClearOnOveruse;

        isBarren = data.isBarren;
        barrenTurnsLeft = Mathf.Max(0, data.barrenTurnsLeft);

        turnsSinceLastExtraSpawn = Mathf.Max(0, data.turnsSinceLastExtraSpawn);
        outOfSeasonFavorMultiplier = Mathf.Max(0f, data.outOfSeasonFavorMultiplier);

        spawnedResources.Clear();

        if (data.spawnedResources != null)
        {
            foreach (var saved in data.spawnedResources)
            {
                if (saved == null || string.IsNullOrWhiteSpace(saved.resourceKey) || saved.amount <= 0)
                    continue;

                ResourceDefinition def = resourceResolver != null
                    ? resourceResolver(saved.resourceKey)
                    : null;

                if (def == null)
                    continue;

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
    }

    private static ResourceDefinition GetEntryDefinition(ResourceSpawnEntry entry)
    {
        return entry.definition;
    }

    private static int GetEntryAmount(ResourceSpawnEntry entry)
    {
        return entry.amount;
    }

    private static ResourceSpawnEntry CreateEntry(ResourceDefinition def, int amount)
    {
        var entry = new ResourceSpawnEntry { definition = def };
        entry.Initialize(amount);
        return entry;
    }
}