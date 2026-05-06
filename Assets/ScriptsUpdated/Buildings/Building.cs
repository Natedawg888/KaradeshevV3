using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ResourceCost
{
    public ResourceDefinition resource;
    public int amount = 1;
}

[Serializable]
public class ResourceCostSet
{
    public string displayName; // optional label like "Wood", "Stone", "Trade"
    public List<ResourceCost> costs = new();
}


[Serializable]
public class ResourceAmount
{
    public ResourceDefinition resource;
    public int amount = 1;
}

[System.Serializable]
public class Building
{
    public string buildingID;
    public string buildingName;
    public string buildingDescription;
    public string buildingSize;
    public Sprite buildingIcon;
    public Sprite lockBuildingIcon;

    public TileSize requiredTileSize;
    
    public List<EnvironmentType>      requiredEnvironmentTypes      = new();
    public List<EnvironmentTileType>  requiredEnvironmentTileTypes  = new();

    [Header("Placement")]
    public bool canRotate = true;
    public bool isStarterCandidate = false;

    public BuildingType buildingType;
    public GameObject buildingPrefab;
    public GameObject finalBuildingPrefab;

    [Header("Health Defaults")]
    [Min(1)] public int   defaultMaxHealth                 = 100;
    [Min(0)] public int   defaultDegenerationAmount        = 5;
    [Min(1)] public int   defaultDegenerationIntervalTurns = 3;
    [Range(0f,1f)] public float defaultDamagedThreshold    = 0.33f;

    // cost to build
    public List<ResourceCost> buildCosts = new();

    [Tooltip("If present, these override the legacy buildCosts when a set is selected.")]
    public List<ResourceCostSet> buildCostSets = new();
    [HideInInspector] public int activeBuildCostSetIndex = -1;

    public List<int> availableLevels = new();

    [Header("Build Requirements")]
    [Min(1)] public int buildTurnsRequired = 1;

    [Tooltip("How many population must be reserved for the duration of construction.")]
    [Min(1)] public int requireBuildPopulation = 1;

    // convenience: true if availableLevels empty or contains this level
    public bool IsAvailableAtLevel(int level)
        => availableLevels == null || availableLevels.Count == 0 || availableLevels.Contains(level);

    [Header("Destroyed Auto-Clear")]
    [Min(0)] public int destroyedAutoClearAfterTurns = 3;

    [Header("Destroyed Manual Clear")]
    [Min(0)] public int manualClearTurns = 0;

    [Tooltip("Population reserved while the manual clear is in progress (0 = none).")]
    [Min(0)] public int manualClearPopulation = 0;

    [Tooltip("Resource cost to initiate/perform a manual clear.")]
    public List<ResourceCost> manualClearCosts = new();

    [Tooltip("Resources refunded/recovered when the manual clear completes.")]
    public List<ResourceAmount> manualClearRewards = new();

    [Tooltip("Building IDs this building can upgrade to.")]
    public List<string> upgradeToIDs = new();

    public bool HasAlternateCostSets => buildCostSets != null && buildCostSets.Count > 0;

    [Header("Build Limits")]
    [Tooltip("Buildings with the same familyId count toward the same cap. Leave empty to fall back to buildingID.")]
    public string familyId;

    [Tooltip("0 or less = no limit. If greater than 0, this is the max number allowed across the whole family.")]
    [Min(0)] public int maxCountPerFamily = 0;

    // Active costs that UI / checks should use
    public IReadOnlyList<ResourceCost> GetActiveBuildCosts()
    {
        if (HasAlternateCostSets && activeBuildCostSetIndex >= 0 && activeBuildCostSetIndex < buildCostSets.Count)
            return buildCostSets[activeBuildCostSetIndex].costs;
        return buildCosts;
    }

    public string FamilyKey
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(familyId))
                return familyId.Trim();

            // fallback so old buildings still work safely
            return string.IsNullOrWhiteSpace(buildingID) ? string.Empty : buildingID.Trim();
        }
    }

    public bool HasFamilyLimit => maxCountPerFamily > 0;

    // Active set label for UI
    public string GetActiveCostSetLabel()
    {
        if (!HasAlternateCostSets) return "Default";
        if (activeBuildCostSetIndex < 0 || activeBuildCostSetIndex >= buildCostSets.Count)
            return "Default";
        var set = buildCostSets[activeBuildCostSetIndex];
        return string.IsNullOrWhiteSpace(set.displayName)
            ? $"Set {activeBuildCostSetIndex + 1}"
            : set.displayName;
    }

    // Switch to a specific set (-1 = legacy)
    public void SetActiveCostSet(int index)
    {
        if (!HasAlternateCostSets)
        {
            activeBuildCostSetIndex = -1;
            return;
        }
        if (index < -1) index = -1;
        if (index >= buildCostSets.Count) index = buildCostSets.Count - 1;
        activeBuildCostSetIndex = index;
    }

    // Cycle to next/prev set (wraps). If no sets, stays at -1.
    public void CycleNextCostSet()
    {
        if (!HasAlternateCostSets) { activeBuildCostSetIndex = -1; return; }
        if (activeBuildCostSetIndex < 0) activeBuildCostSetIndex = 0;
        else activeBuildCostSetIndex = (activeBuildCostSetIndex + 1) % buildCostSets.Count;
    }
    public void CyclePrevCostSet()
    {
        if (!HasAlternateCostSets) { activeBuildCostSetIndex = -1; return; }
        if (activeBuildCostSetIndex < 0) activeBuildCostSetIndex = buildCostSets.Count - 1;
        else activeBuildCostSetIndex = (activeBuildCostSetIndex - 1 + buildCostSets.Count) % buildCostSets.Count;
    }

    // Utility: find first affordable set; returns index (-1 = legacy) or null if none
    public int? GetFirstAffordableCostSetIndex()
    {
        // Legacy first
        if (!HasAlternateCostSets)
            return InventoryQuery.CanAfford(buildCosts) ? -1 : (int?)null;

        // Check sets
        for (int i = 0; i < buildCostSets.Count; i++)
        {
            var set = buildCostSets[i];
            if (set != null && InventoryQuery.CanAfford(set.costs))
                return i;
        }
        // If none affordable, maybe legacy qualifies
        if (buildCosts != null && buildCosts.Count > 0 && InventoryQuery.CanAfford(buildCosts))
            return -1;

        return null;
    }

    // Convenience: true if any option (legacy or sets) is affordable
    public bool CanAffordAnyCostOption()
    {
        if (InventoryQuery.CanAfford(buildCosts)) return true;
        if (HasAlternateCostSets)
        {
            foreach (var set in buildCostSets)
                if (set != null && InventoryQuery.CanAfford(set.costs))
                    return true;
        }
        return false;
    }

    public Building(
        string buildingID,
        string buildingName,
        string buildingDescription,
        string buildingSize,
        Sprite buildingIcon,
        Sprite lockBuildingIcon,
        TileSize requiredTileSize,
        List<EnvironmentType> requiredEnvironmentTypes,
        List<EnvironmentTileType> requiredEnvironmentTileTypes,
        BuildingType buildingType,
        GameObject buildingPrefab,
        GameObject finalBuildingPrefab = null,
        List<int> availableLevels = null,
        List<ResourceCost> buildCosts = null,
        int buildTurnsRequired = 1,
        int requireBuildPopulation = 1,
        int destroyedAutoClearAfterTurns = 3,
        int manualClearTurns = 0,
        int manualClearPopulation = 0,
        List<ResourceCost> manualClearCosts = null,
        List<ResourceAmount> manualClearRewards = null,
        List<string> upgradeToIDs = null,

        // NEW
        List<ResourceCostSet> buildCostSets = null,
        int activeBuildCostSetIndex = -1,
        bool canRotate = true,
        bool isStarterCandidate = false
    )
    {
        this.buildingID = buildingID;
        this.buildingName = buildingName;
        this.buildingDescription = buildingDescription;
        this.buildingSize = buildingSize;
        this.buildingIcon = buildingIcon;
        this.lockBuildingIcon = lockBuildingIcon;
        this.requiredTileSize = requiredTileSize;
        this.requiredEnvironmentTypes = requiredEnvironmentTypes ?? new();
        this.requiredEnvironmentTileTypes = requiredEnvironmentTileTypes ?? new();
        this.buildingType = buildingType;
        this.buildingPrefab = buildingPrefab;
        this.finalBuildingPrefab = finalBuildingPrefab;
        this.availableLevels = availableLevels ?? new();
        this.buildCosts = buildCosts ?? new();
        this.buildTurnsRequired = Mathf.Max(1, buildTurnsRequired);
        this.requireBuildPopulation = Mathf.Max(1, requireBuildPopulation);

        this.destroyedAutoClearAfterTurns = Mathf.Max(0, destroyedAutoClearAfterTurns);

        this.manualClearTurns       = Mathf.Max(0, manualClearTurns);
        this.manualClearPopulation  = Mathf.Max(0, manualClearPopulation);
        this.manualClearCosts       = manualClearCosts   ?? new();
        this.manualClearRewards     = manualClearRewards ?? new();
        this.upgradeToIDs           = upgradeToIDs       ?? new();

        // NEW
        this.buildCostSets = buildCostSets ?? new();
        this.activeBuildCostSetIndex = activeBuildCostSetIndex;
        if (!HasAlternateCostSets) this.activeBuildCostSetIndex = -1; // normalize

        this.canRotate = canRotate;
        this.isStarterCandidate = isStarterCandidate;
    }

    // Keep the parameterless constructor
    public Building() { }
}

public enum BuildingType
{
    Shelter,
    Production,
    Storage,
    Trade,
    Crafting,
    Culture,
    KineticWarfare,
    CapturedPopulation,
    Health,
    Religious,
    Waste
}