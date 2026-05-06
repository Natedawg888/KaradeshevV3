using System;
using System.Collections.Generic;
using UnityEngine;  

public enum BeliefSystemType
{
    Animism = 0,
    Polytheism = 1,
    Monotheism = 2
}

public enum SpiritMoodState
{
    Angry = 0,
    Sad = 1,
    Neutral = 2,
    Pleased = 3,
    Exalted = 4
}

public enum SpiritEffectType
{
    InventorySpoilageRateMultiplier = 0,
    TileBarrenChanceAdd = 1,
    BirthSuccessChanceAdd = 2,
    TwinChanceAdd = 3,
    TripletChanceAdd = 4,
    UnitAttackMultiplier = 5,
    UnitDefenseMultiplier = 6,
    UnitAccuracyMultiplier = 7,
    UnitMovementMultiplier = 8,
    ProductionOutputMultiplier = 9,
    CraftingOutputMultiplier = 10,
    PopulationRecoveryRateMultiplier = 11,
    PopulationResistanceAdd = 12,
    ResearchFailureChanceModifier = 13
}

public enum SpiritModifierMode
{
    Additive = 0,
    Multiplier = 1
}

public enum SpiritSacrificeSexFilter
{
    Any = 0,
    Male = 1,
    Female = 2
}

public enum SpiritSacrificeAgeFilter
{
    Any = 0,
    Child = 1,
    Teen = 2,
    Adult = 3,
    Elder = 4
}

[Serializable]
public class SpiritResourceOfferingOption
{
    public string offeringID;
    public string displayName;

    [Tooltip("Use your project's resource ScriptableObject here. If your type is named Resource, you can replace ScriptableObject with Resource.")]
    public ScriptableObject resourceDefinition;

    [Min(1)] public int amount = 1;
    public int favorChange = 10;
    public bool repeatable = true;

    public bool Matches(ScriptableObject resource, int offeredAmount)
    {
        if (resourceDefinition == null || resource == null)
            return false;

        if (resourceDefinition != resource)
            return false;

        return offeredAmount >= amount;
    }
}

[Serializable]
public class SpiritPopulationSacrificeOption
{
    public string offeringID;
    public string displayName;

    public SpiritSacrificeSexFilter sexFilter = SpiritSacrificeSexFilter.Any;
    public SpiritSacrificeAgeFilter ageFilter = SpiritSacrificeAgeFilter.Any;

    [Min(1)] public int count = 1;
    public int favorChange = 20;
    public bool repeatable = true;

    public bool Matches(SpiritSacrificeSexFilter sex, SpiritSacrificeAgeFilter age, int offeredCount)
    {
        if (offeredCount < count)
            return false;

        bool sexOk = sexFilter == SpiritSacrificeSexFilter.Any || sexFilter == sex;
        bool ageOk = ageFilter == SpiritSacrificeAgeFilter.Any || ageFilter == age;

        return sexOk && ageOk;
    }
}

[Serializable]
public class SacredAnimalRule
{
    public AnimalDefinition animalDefinition;

    [Header("Marker")]
    public bool overrideMarkerColor = true;
    public Color markerColor = Color.cyan;

    [Header("Favor Reactions")]
    public int favorPenaltyOnKill = 10;
    public int favorPenaltyOnAttack = 4;
}

[Serializable]
public class SpiritEffectEntry
{
    public SpiritEffectType effectType;
    public SpiritModifierMode modifierMode = SpiritModifierMode.Additive;

    [Header("Mood Values")]
    public float angryValue;
    public float sadValue;
    public float pleasedValue;
    public float exaltedValue;

    public float GetValue(SpiritMoodState mood)
    {
        switch (mood)
        {
            case SpiritMoodState.Angry:
                return angryValue;
            case SpiritMoodState.Sad:
                return sadValue;
            case SpiritMoodState.Pleased:
                return pleasedValue;
            case SpiritMoodState.Exalted:
                return exaltedValue;
            default:
                return modifierMode == SpiritModifierMode.Multiplier ? 1f : 0f;
        }
    }
}

[Serializable]
public class SpiritBarrenPreference
{
    public string preferenceID;
    public string displayName;

    [Header("Optional Filters")]
    public bool useEnvironmentType = false;
    public EnvironmentType environmentType = EnvironmentType.Grassland;

    public bool useTileType = false;
    public EnvironmentTileType tileType = EnvironmentTileType.Land;

    [Header("Weighting")]
    [Min(0f)] public float flatWeightBonus = 0f;
    [Min(0.01f)] public float weightMultiplier = 2f;

    public bool Matches(EnvironmentControl env)
    {
        if (env == null)
            return false;

        if (useEnvironmentType && env.environmentType != environmentType)
            return false;

        if (useTileType && env.environmentTileType != tileType)
            return false;

        return true;
    }
}

[Serializable]
public class SpiritRuntimeState
{
    public SpiritDefinitionSO definition;
    public bool accepted = true;
    public int favor;
    public int totalOfferingsGiven;
    public int lastOfferingTurn = -1;

    [Header("Sacred Animal Runtime")]
    public List<int> currentSacredAnimalGroupIds = new List<int>();

    public SpiritRuntimeState(SpiritDefinitionSO spirit)
    {
        definition = spirit;
        accepted = true;
        favor = spirit != null ? spirit.startingFavor : 0;
        totalOfferingsGiven = 0;
        lastOfferingTurn = -1;
        currentSacredAnimalGroupIds = new List<int>();
    }

    public bool HasSacredAnimalGroups =>
        currentSacredAnimalGroupIds != null && currentSacredAnimalGroupIds.Count > 0;

    public void EnsureSacredGroupList()
    {
        if (currentSacredAnimalGroupIds == null)
            currentSacredAnimalGroupIds = new List<int>();
    }

    public bool ContainsSacredAnimalGroup(int groupId)
    {
        EnsureSacredGroupList();
        return groupId > 0 && currentSacredAnimalGroupIds.Contains(groupId);
    }

    public bool RemoveSacredAnimalGroup(int groupId)
    {
        EnsureSacredGroupList();
        return groupId > 0 && currentSacredAnimalGroupIds.Remove(groupId);
    }

    public void ClearSacredAnimalGroups()
    {
        EnsureSacredGroupList();
        currentSacredAnimalGroupIds.Clear();
    }
}

[Serializable]
public class SpiritSpoilageTabooRule
{
    public string tabooID;
    public string displayName;

    [Tooltip("Assign either a single resource or a group resource definition.")]
    public ScriptableObject resourceDefinition;

    [Min(1)] public int minSpoiledAmount = 1;

    [Tooltip("How much favor is lost when this taboo is broken.")]
    [Min(1)] public int favorPenalty = 5;

    [Tooltip("If true, scales penalty by spoiled amount.")]
    public bool scaleByAmount = false;

    [Min(1)] public int amountPerPenaltyStep = 1;

    [Tooltip("Caps the total penalty from this taboo in a single turn.")]
    [Min(1)] public int maxPenaltyPerTurn = 999;

    public bool Matches(ResourceDefinition spoiledDef, int spoiledAmount)
    {
        if (spoiledDef == null || spoiledAmount < minSpoiledAmount)
            return false;

        ResourceDefinition tabooDef = resourceDefinition as ResourceDefinition;
        if (tabooDef == null)
            return false;

        if (tabooDef.isGroup)
            return spoiledDef.resourceType == tabooDef.groupType;

        return tabooDef == spoiledDef;
    }

    public int GetPenalty(int spoiledAmount)
    {
        if (spoiledAmount < minSpoiledAmount)
            return 0;

        int penalty = Mathf.Abs(favorPenalty);

        if (scaleByAmount)
        {
            int step = Mathf.Max(1, amountPerPenaltyStep);
            int stepCount = Mathf.Max(1, spoiledAmount / step);
            penalty *= stepCount;
        }

        penalty = Mathf.Min(penalty, Mathf.Max(1, maxPenaltyPerTurn));
        return Mathf.Max(0, penalty);
    }
}

[Serializable]
public class SpiritLeftBehindGatheredLootTabooRule
{
    public string tabooID;
    public string displayName;

    [Tooltip("Assign either a single resource or a group resource definition.")]
    public ScriptableObject resourceDefinition;

    [Min(1)] public int minLeftAmount = 1;

    [Tooltip("How much favor is lost when this taboo is broken.")]
    [Min(1)] public int favorPenalty = 5;

    [Tooltip("If true, scales penalty by amount left behind.")]
    public bool scaleByAmount = false;

    [Min(1)] public int amountPerPenaltyStep = 1;

    [Tooltip("Caps the total penalty from this taboo in a single abandon action.")]
    [Min(1)] public int maxPenaltyPerClose = 999;

    public bool Matches(ResourceDefinition leftDef, int leftAmount)
    {
        if (leftDef == null || leftAmount < minLeftAmount)
            return false;

        ResourceDefinition tabooDef = resourceDefinition as ResourceDefinition;
        if (tabooDef == null)
            return false;

        if (tabooDef.isGroup)
            return leftDef.resourceType == tabooDef.groupType;

        return tabooDef == leftDef;
    }

    public int GetPenalty(int leftAmount)
    {
        if (leftAmount < minLeftAmount)
            return 0;

        int penalty = Mathf.Abs(favorPenalty);

        if (scaleByAmount)
        {
            int step = Mathf.Max(1, amountPerPenaltyStep);
            int stepCount = Mathf.Max(1, leftAmount / step);
            penalty *= stepCount;
        }

        penalty = Mathf.Min(penalty, Mathf.Max(1, maxPenaltyPerClose));
        return Mathf.Max(0, penalty);
    }
}

[Serializable]
public class SpiritCombatRetreatTabooRule
{
    public string tabooID;
    public string displayName;

    [Header("Filters")]
    public bool requireAgainstUnits = false;
    public bool requireAgainstAnimals = false;
    public bool requireAfterRetaliation = false;
    public bool requireSurroundCombat = false;

    [Header("Penalty")]
    [Min(1)] public int favorPenalty = 5;

    public bool Matches(
        bool againstUnit,
        bool againstAnimal,
        bool afterRetaliation,
        bool wasSurround)
    {
        if (requireAgainstUnits && !againstUnit)
            return false;

        if (requireAgainstAnimals && !againstAnimal)
            return false;

        if (requireAfterRetaliation && !afterRetaliation)
            return false;

        if (requireSurroundCombat && !wasSurround)
            return false;

        return true;
    }

    public int GetPenalty()
    {
        return Mathf.Max(1, Mathf.Abs(favorPenalty));
    }
}

[Serializable]
public class SpiritLeftBehindUnitLootTabooRule
{
    public string tabooID;
    public string displayName;

    [Tooltip("Assign either a single resource or a group resource definition.")]
    public ScriptableObject resourceDefinition;

    [Min(1)] public int minLeftAmount = 1;

    [Tooltip("How much favor is lost when this taboo is broken.")]
    [Min(1)] public int favorPenalty = 5;

    [Tooltip("If true, scales penalty by amount left behind.")]
    public bool scaleByAmount = false;

    [Min(1)] public int amountPerPenaltyStep = 1;

    [Tooltip("Caps the total penalty from this taboo in a single abandon action.")]
    [Min(1)] public int maxPenaltyPerClose = 999;

    public bool Matches(ResourceDefinition leftDef, int leftAmount)
    {
        if (leftDef == null || leftAmount < minLeftAmount)
            return false;

        ResourceDefinition tabooDef = resourceDefinition as ResourceDefinition;
        if (tabooDef == null)
            return false;

        if (tabooDef.isGroup)
            return leftDef.resourceType == tabooDef.groupType;

        return tabooDef == leftDef;
    }

    public int GetPenalty(int leftAmount)
    {
        if (leftAmount < minLeftAmount)
            return 0;

        int penalty = Mathf.Abs(favorPenalty);

        if (scaleByAmount)
        {
            int step = Mathf.Max(1, amountPerPenaltyStep);
            int stepCount = Mathf.Max(1, leftAmount / step);
            penalty *= stepCount;
        }

        penalty = Mathf.Min(penalty, Mathf.Max(1, maxPenaltyPerClose));
        return Mathf.Max(0, penalty);
    }
}

[Serializable]
public class SpiritReligiousBuildingHealthTabooRule
{
    public string tabooID;
    public string displayName;

    [Header("Threshold")]
    [Range(0f, 1f)] public float triggerAtOrBelowHealthFraction = 0.5f;

    [Header("Filters")]
    [Tooltip("If true, only triggers for buildings currently in Religious mode.")]
    public bool requireReligiousType = true;

    [Tooltip("If true, this taboo only applies when the spirit is affiliated with the damaged building.")]
    public bool requireAffiliationAtBuilding = true;

    [Header("Penalty")]
    [Min(1)] public int favorPenalty = 5;

    public bool Matches(
        float previousFraction,
        float currentFraction,
        bool isReligiousType,
        bool isAffiliatedAtBuilding)
    {
        float threshold = Mathf.Clamp01(triggerAtOrBelowHealthFraction);

        if (requireReligiousType && !isReligiousType)
            return false;

        if (requireAffiliationAtBuilding && !isAffiliatedAtBuilding)
            return false;

        // Only trigger when crossing downward through the threshold.
        bool crossedDownward = previousFraction > threshold && currentFraction <= threshold;
        return crossedDownward;
    }

    public int GetPenalty()
    {
        return Mathf.Max(1, Mathf.Abs(favorPenalty));
    }
}

[Serializable]
public class SpiritBanishmentOption
{
    public bool enabled = true;
    public string displayName = "Banishment";

    [Header("Cost")]
    public ResourceDefinition resourceDefinition;
    [Min(1)] public int resourceAmount = 1;

    [Header("Timing")]
    [Min(1)] public int turnsRequired = 3;

    [Header("Outcome")]
    [Range(0f, 1f)] public float failureChance = 0.25f;
}