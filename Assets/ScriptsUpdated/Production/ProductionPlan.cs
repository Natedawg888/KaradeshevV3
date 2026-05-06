using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "NewProductionPlan",
    menuName = "Kardashev/Production/Production Plan")]
public class ProductionPlan : ScriptableObject
{
    [Header("Identity")]
    public string productionID;
    public string planName;

    [Header("Visuals")]
    public Sprite productionIcon;

    [Header("Cycle Requirements")]
    public int requiredTurnsPerCycle = 1;
    public int requiredPopulation = 1;

    [Header("Worker Risk")]
    [Range(0f, 1f)]
    [Tooltip("Per reserved worker, chance to die when a production cycle successfully completes.")]
    public float fatalityRatePerWorkerPerCompletedCycle = 0f;

    [Header("Progression")]
    [Min(0)]
    [Tooltip("XP granted each time a production cycle is successfully completed and finalized.")]
    public int xpPerCompletedCycle = 0;

    [Header("Running Resource Cost")]
    [Tooltip("Legacy/default running costs per cycle (used when no running cost set is selected).")]
    public List<ProductionResourceAmount> runningCosts = new();

    [Header("Alternate Running Cost Sets (Optional)")]
    [Tooltip("Optional alternate running cost sets. If selected, overrides runningCosts.")]
    public List<ProductionCostSet> runningCostSets = new();

    [HideInInspector]
    public int activeRunningCostSetIndex = -1;

    [Header("Output Resource Reward")]
    [Tooltip("Legacy/default outputs per cycle (used when no output set is selected).")]
    public List<ProductionResourceAmount> outputs = new();

    [Header("Alternate Output Sets (Optional)")]
    [Tooltip("Optional alternate output sets. If selected, overrides outputs.")]
    public List<ProductionOutputSet> outputSets = new();

    [HideInInspector]
    public int activeOutputSetIndex = -1;

    [Header("Seasonal Output Modifiers")]
    [Tooltip("If true, this production plan's outputs are modified by the current season.")]
    public bool isAffectedBySeason = false;

    [Tooltip("Output multiplier per season ID. 1 = unchanged, 0.5 = half, 2 = double.")]
    public List<ProductionSeasonOutputModifier> seasonalOutputModifiers = new();

    [Header("Production Type")]
    public bool isExternalExtractor = true;

    [Header("Extraction Conditions")]
    public EnvironmentType[] allowedEnvironmentTypes;
    public EnvironmentTileType[] allowedTileTypes;
    public int environmentDamagePerCycle = 1;

    [Header("Extraction Capacity")]
    [Min(1)]
    public int maxExtractionTiles = 1;

    [Header("BFSTileScanner Weighting")]
    public float bfsTilePreferenceMultiplier = 1.0f;

    [Header("Cycle Cooldown")]
    [Tooltip("If enabled, this plan pauses after a number of completed cycles.")]
    public bool useCycleCooldown = false;

    [Min(1)]
    [Tooltip("How many completed cycles run before cooldown starts.")]
    public int cyclesBeforeCooldown = 3;

    [Min(1)]
    [Tooltip("How many cycle-lengths the cooldown lasts.")]
    public int cooldownCycles = 1;

    public bool HasAlternateRunningCostSets =>
        runningCostSets != null && runningCostSets.Count > 0;

    public bool HasAlternateOutputSets =>
        outputSets != null && outputSets.Count > 0;

    public bool UsesCycleCooldown =>
        useCycleCooldown &&
        cyclesBeforeCooldown > 0 &&
        cooldownCycles > 0;

    public int GetCyclesBeforeCooldown()
    {
        return Mathf.Max(1, cyclesBeforeCooldown);
    }

    public int GetCooldownCycles()
    {
        return Mathf.Max(1, cooldownCycles);
    }

    public int GetCooldownTurns()
    {
        return Mathf.Max(1, requiredTurnsPerCycle) * GetCooldownCycles();
    }

    public IReadOnlyList<ProductionResourceAmount> GetActiveRunningCosts()
    {
        if (HasAlternateRunningCostSets &&
            activeRunningCostSetIndex >= 0 &&
            activeRunningCostSetIndex < runningCostSets.Count)
        {
            var set = runningCostSets[activeRunningCostSetIndex];
            if (set != null && set.costs != null)
                return set.costs;
        }

        return runningCosts;
    }

    public IReadOnlyList<ProductionResourceAmount> GetActiveOutputs()
    {
        if (HasAlternateOutputSets &&
            activeOutputSetIndex >= 0 &&
            activeOutputSetIndex < outputSets.Count)
        {
            var set = outputSets[activeOutputSetIndex];
            if (set != null && set.outputs != null)
                return set.outputs;
        }

        return outputs;
    }

    public string GetActiveRunningCostSetLabel()
    {
        if (!HasAlternateRunningCostSets) return "Default";
        if (activeRunningCostSetIndex < 0 || activeRunningCostSetIndex >= runningCostSets.Count)
            return "Default";

        var set = runningCostSets[activeRunningCostSetIndex];
        if (set == null) return "Default";

        return string.IsNullOrWhiteSpace(set.displayName)
            ? $"Set {activeRunningCostSetIndex + 1}"
            : set.displayName;
    }

    public string GetActiveOutputSetLabel()
    {
        if (!HasAlternateOutputSets) return "Default";
        if (activeOutputSetIndex < 0 || activeOutputSetIndex >= outputSets.Count)
            return "Default";

        var set = outputSets[activeOutputSetIndex];
        if (set == null) return "Default";

        return string.IsNullOrWhiteSpace(set.displayName)
            ? $"Set {activeOutputSetIndex + 1}"
            : set.displayName;
    }

    public void SetActiveRunningCostSet(int index)
    {
        if (!HasAlternateRunningCostSets)
        {
            activeRunningCostSetIndex = -1;
            return;
        }

        if (index < -1) index = -1;
        if (index >= runningCostSets.Count) index = runningCostSets.Count - 1;
        activeRunningCostSetIndex = index;
    }

    public void SetActiveOutputSet(int index)
    {
        if (!HasAlternateOutputSets)
        {
            activeOutputSetIndex = -1;
            return;
        }

        if (index < -1) index = -1;
        if (index >= outputSets.Count) index = outputSets.Count - 1;
        activeOutputSetIndex = index;
    }

    public void CycleNextRunningCostSet()
    {
        if (!HasAlternateRunningCostSets)
        {
            activeRunningCostSetIndex = -1;
            return;
        }

        if (activeRunningCostSetIndex < 0)
            activeRunningCostSetIndex = 0;
        else
            activeRunningCostSetIndex = (activeRunningCostSetIndex + 1) % runningCostSets.Count;
    }

    public void CyclePrevRunningCostSet()
    {
        if (!HasAlternateRunningCostSets)
        {
            activeRunningCostSetIndex = -1;
            return;
        }

        if (activeRunningCostSetIndex < 0)
            activeRunningCostSetIndex = runningCostSets.Count - 1;
        else
            activeRunningCostSetIndex = (activeRunningCostSetIndex - 1 + runningCostSets.Count) % runningCostSets.Count;
    }

    public void CycleNextOutputSet()
    {
        if (!HasAlternateOutputSets)
        {
            activeOutputSetIndex = -1;
            return;
        }

        if (activeOutputSetIndex < 0)
            activeOutputSetIndex = 0;
        else
            activeOutputSetIndex = (activeOutputSetIndex + 1) % outputSets.Count;
    }

    public void CyclePrevOutputSet()
    {
        if (!HasAlternateOutputSets)
        {
            activeOutputSetIndex = -1;
            return;
        }

        if (activeOutputSetIndex < 0)
            activeOutputSetIndex = outputSets.Count - 1;
        else
            activeOutputSetIndex = (activeOutputSetIndex - 1 + outputSets.Count) % outputSets.Count;
    }

    public bool CanExtractFrom(EnvironmentType env, EnvironmentTileType tile)
    {
        if (!isExternalExtractor)
            return false;

        bool envMatches = true;
        if (allowedEnvironmentTypes != null && allowedEnvironmentTypes.Length > 0)
        {
            envMatches = false;
            for (int i = 0; i < allowedEnvironmentTypes.Length; i++)
            {
                if (allowedEnvironmentTypes[i] == env)
                {
                    envMatches = true;
                    break;
                }
            }
        }

        bool tileMatches = true;
        if (allowedTileTypes != null && allowedTileTypes.Length > 0)
        {
            tileMatches = false;
            for (int i = 0; i < allowedTileTypes.Length; i++)
            {
                if (allowedTileTypes[i] == tile)
                {
                    tileMatches = true;
                    break;
                }
            }
        }

        return envMatches && tileMatches;
    }

    public int GetMaxExtractionTiles()
    {
        return Mathf.Max(1, maxExtractionTiles);
    }

    public bool HasSeasonalOutputModifiers =>
        isAffectedBySeason &&
        seasonalOutputModifiers != null &&
        seasonalOutputModifiers.Count > 0;

    public IReadOnlyList<ProductionResourceAmount> GetSeasonAdjustedOutputs()
    {
        var baseOutputs = GetActiveOutputs();

        if (baseOutputs == null || baseOutputs.Count == 0)
            return baseOutputs;

        if (!HasSeasonalOutputModifiers)
            return baseOutputs;

        if (SeasonManager.Instance == null)
            return baseOutputs;

        var currentSeason = SeasonManager.Instance.CurrentSeason;
        var modifier = GetSeasonalOutputModifier(currentSeason);
        if (modifier == null)
            return baseOutputs;

        float multiplier = Mathf.Max(0f, modifier.outputMultiplier);

        if (Mathf.Approximately(multiplier, 1f))
            return baseOutputs;

        var adjusted = new List<ProductionResourceAmount>(baseOutputs.Count);

        foreach (var o in baseOutputs)
        {
            if (o == null || o.resource == null)
                continue;

            adjusted.Add(new ProductionResourceAmount
            {
                resource = o.resource,
                amountPerCycle = Mathf.Max(0, Mathf.RoundToInt(o.amountPerCycle * multiplier))
            });
        }

        return adjusted;
    }

    public float GetCurrentSeasonOutputMultiplier()
    {
        if (!HasSeasonalOutputModifiers || SeasonManager.Instance == null)
            return 1f;

        var modifier = GetSeasonalOutputModifier(SeasonManager.Instance.CurrentSeason);
        if (modifier == null)
            return 1f;

        return Mathf.Max(0f, modifier.outputMultiplier);
    }

    public string GetCurrentSeasonOutputModifierLabel()
    {
        if (!HasSeasonalOutputModifiers || SeasonManager.Instance == null)
            return string.Empty;

        var currentSeason = SeasonManager.Instance.CurrentSeason;
        if (currentSeason == null)
            return string.Empty;

        var modifier = GetSeasonalOutputModifier(currentSeason);
        if (modifier == null)
            return string.Empty;

        float pct = (modifier.outputMultiplier - 1f) * 100f;
        return $"{currentSeason.displayName}: {pct:+0;-0;0}%";
    }

    public int RollFatalitiesForCompletedCycle(int workerCount)
    {
        workerCount = Mathf.Max(0, workerCount);
        float chance = Mathf.Clamp01(fatalityRatePerWorkerPerCompletedCycle);

        if (workerCount <= 0 || chance <= 0f)
            return 0;

        int dead = 0;
        for (int i = 0; i < workerCount; i++)
        {
            if (UnityEngine.Random.value < chance)
                dead++;
        }

        return dead;
    }

    private ProductionSeasonOutputModifier GetSeasonalOutputModifier(SeasonDefinition season)
    {
        if (season == null || seasonalOutputModifiers == null)
            return null;

        string currentSeasonID = NormalizeSeasonID(season.seasonID);
        if (string.IsNullOrEmpty(currentSeasonID))
            return null;

        for (int i = 0; i < seasonalOutputModifiers.Count; i++)
        {
            var entry = seasonalOutputModifiers[i];
            if (entry == null)
                continue;

            string entrySeasonID = NormalizeSeasonID(entry.seasonID);
            if (string.IsNullOrEmpty(entrySeasonID))
                continue;

            if (entrySeasonID == currentSeasonID)
                return entry;
        }

        return null;
    }

    private static string NormalizeSeasonID(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        value = value.Trim().ToLowerInvariant();
        value = value.Replace(" ", "");
        value = value.Replace("_", "");
        value = value.Replace("-", "");
        return value;
    }
}

[Serializable]
public class ProductionResourceAmount
{
    [Tooltip("Which resource is consumed/produced.")]
    public ResourceDefinition resource;

    [Tooltip("Amount per cycle (integer).")]
    public int amountPerCycle = 1;
}

[Serializable]
public class ProductionCostSet
{
    [Tooltip("Optional label shown in UI. If empty, a default label is used.")]
    public string displayName;

    [Tooltip("Running costs per cycle for this set.")]
    public List<ProductionResourceAmount> costs = new();
}

[Serializable]
public class ProductionOutputSet
{
    [Tooltip("Optional label shown in UI. If empty, a default label is used.")]
    public string displayName;

    [Tooltip("Outputs per cycle for this set.")]
    public List<ProductionResourceAmount> outputs = new();
}

[Serializable]
public class ProductionSeasonOutputModifier
{
    [Tooltip("Matches SeasonDefinition.seasonID")]
    public string seasonID;

    [Min(0f)]
    [Tooltip("1 = unchanged, 0.5 = half output, 2 = double output.")]
    public float outputMultiplier = 1f;
}