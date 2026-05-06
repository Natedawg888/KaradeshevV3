using UnityEngine;
using System.Collections.Generic;

public enum ResourceType
{
    Water,
    Food,
    Material,
}

public enum FoodGrade
{
    Poor = 0,
    Low = 1,
    Common = 2,
    Good = 3,
    Great = 4,
    Premium = 5
}

[CreateAssetMenu(menuName = "Resources/ResourceDefinition", fileName = "NewResourceDefinition")]
public class ResourceDefinition : ScriptableObject
{
    [Header("Identity")]
    public string resourceID;
    public string resourceName;
    public Sprite resourceIcon;

    [TextArea]
    public string description;

    [Header("Type")]
    public ResourceType resourceType;

    [Header("Grouping (Optional)")]
    public bool isGroup = false;

    [Tooltip("If this is a group, sum across ALL stacks of this type (e.g., Food, Water).")]
    public ResourceType groupType;

    [Header("Perishability")]
    public bool nonPerishable = false;

    [Header("Spoilage")]
    [Range(0f, 1f)]
    public float spoilageRate = 0f;
    public int spoilageInterval = 1;

    [Header("Environment Availability")]
    public bool spawnOnEnvironmentTiles = true;
    public List<EnvironmentType> allowedEnvironmentTypes = new();
    public List<EnvironmentTileType> allowedTileTypes = new();

    [Header("Regeneration")]
    public bool regenerates = false;

    [Range(0f, 1f)]
    public float recoveryRate = 0f;
    public int recoveryInterval = 1;

    public bool ShouldRegenerate => regenerates && recoveryRate > 0f && recoveryInterval > 0;

    [Header("Spawn Rate")]
    public float spawnRate = 1f;

    [Header("Weight")]
    public float weightPerUnit = 1f;

    [Header("Size")]
    public float sizePerUnit = 1f;

    [Header("Nutrition")]
    [Tooltip("Per-unit nutrition score (0 = none, 1 = excellent). Materials always evaluate to 0.")]
    [Range(0f, 10f)] public float nutritionPerUnit = 0f;

    public bool HasNutrition => resourceType != ResourceType.Material;
    public float GetNutritionPerUnit() => HasNutrition ? nutritionPerUnit : 0f;
    public float GetTotalNutrition(int amount) => Mathf.Max(0, amount) * GetNutritionPerUnit();

    [Header("Food Quality")]
    public FoodGrade foodGrade = FoodGrade.Common;

    [Header("Hydration")]
    [Tooltip("Per-unit hydration score (0 = none, 1 = excellent). Materials always 0.")]
    [Range(0f, 10f)] public float hydrationPerUnit = 0f;

    [Header("Toxins (applies when consumed)")]
    [Tooltip("If true, consuming this item applies poison damage to the population.")]
    public bool poisonous = false;

    [Tooltip("Health damage per *unit* consumed, normalized 0..1 (same scale as other health deltas).")]
    [Range(0f, 10f)] public float poisonDamagePerUnit01 = 0f;

    public bool HasPoison => poisonous &&
                             poisonDamagePerUnit01 > 0f &&
                             (resourceType == ResourceType.Food || resourceType == ResourceType.Water);

    [Header("Health / Recovery Effects")]
    [Tooltip("If true, this resource can restore health when consumed or used.")]
    public bool restoresHealth = false;

    [Tooltip("Health restored per unit consumed/used. 0.01 = 1% health on your 0..1 health scale.")]
    [Range(0f, 1f)]
    public float healthRestorePerUnit01 = 0f;

    [Tooltip("If true, healing targets the lowest-health people first.")]
    public bool prioritizeLowestHealth = true;

    [Tooltip("How many people each consumed unit can help. 1 = focused. 0 = no target limit.")]
    [Min(0)]
    public int maxHealthTargetsPerUnit = 1;

    [Tooltip("If true, this resource can help disease recovery when consumed or used.")]
    public bool boostsDiseaseRecovery = false;

    [Tooltip("Recovery boost per unit. DiseaseManager decides how this affects active diseases.")]
    [Range(0f, 1f)]
    public float diseaseRecoveryBoostPerUnit01 = 0f;

    [Tooltip("How many diseased people each consumed unit can help. 1 = focused. 0 = no target limit.")]
    [Min(0)]
    public int maxDiseaseRecoveryTargetsPerUnit = 1;

    public bool HasHealthRestore =>
        restoresHealth && healthRestorePerUnit01 > 0f;

    public bool HasDiseaseRecoveryBoost =>
        boostsDiseaseRecovery && diseaseRecoveryBoostPerUnit01 > 0f;

    public bool HasHydration => resourceType != ResourceType.Material;
    public float GetHydrationPerUnit() => HasHydration ? hydrationPerUnit : 0f;
    public float GetTotalHydration(int amount) => Mathf.Max(0, amount) * GetHydrationPerUnit();

    [Header("Seasonal Availability")]
    [Tooltip("If empty, this resource is allowed in every season. Otherwise only seasons with matching seasonID are allowed.")]
    public List<string> allowedSeasonIDs = new();

    public bool IsAvailableIn(EnvironmentType envType, EnvironmentTileType tileType, SeasonDefinition season)
    {
        if (!spawnOnEnvironmentTiles)
            return false;

        if (allowedEnvironmentTypes != null &&
            allowedEnvironmentTypes.Count > 0 &&
            !allowedEnvironmentTypes.Contains(envType))
            return false;

        if (allowedTileTypes != null &&
            allowedTileTypes.Count > 0 &&
            !allowedTileTypes.Contains(tileType))
            return false;

        return IsAllowedInSeason(season);
    }

    public bool IsAllowedInSeason(SeasonDefinition season)
    {
        if (season == null)
            return true;

        if (allowedSeasonIDs == null || allowedSeasonIDs.Count == 0)
            return true;

        string currentSeasonID = NormalizeSeasonID(season.seasonID);
        if (string.IsNullOrEmpty(currentSeasonID))
            return false;

        for (int i = 0; i < allowedSeasonIDs.Count; i++)
        {
            string allowedID = NormalizeSeasonID(allowedSeasonIDs[i]);
            if (string.IsNullOrEmpty(allowedID))
                continue;

            if (allowedID == currentSeasonID)
                return true;
        }

        return false;
    }

    public float GetTotalSpawnRate(int amount)
    {
        return Mathf.Max(0, amount) * spawnRate;
    }

    public float GetTotalWeight(int amount)
    {
        return Mathf.Max(0, amount) * weightPerUnit;
    }

    public float GetTotalSize(int amount)
    {
        return Mathf.Max(0, amount) * sizePerUnit;
    }

    public bool IsAvailableOnTile(EnvironmentType envType, EnvironmentTileType tileType)
    {
        if (!spawnOnEnvironmentTiles)
            return false;

        if (allowedEnvironmentTypes != null &&
            allowedEnvironmentTypes.Count > 0 &&
            !allowedEnvironmentTypes.Contains(envType))
            return false;

        if (allowedTileTypes != null &&
            allowedTileTypes.Count > 0 &&
            !allowedTileTypes.Contains(tileType))
            return false;

        return true;
    }

    public int GetFoodGradeValue()
    {
        return resourceType == ResourceType.Food ? (int)foodGrade : 0;
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