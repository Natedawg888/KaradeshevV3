using System;
using UnityEngine;

[Serializable]
public class ConsumedResourceDiseaseRisk
{
    [Header("Disease Name")]
    public string diseaseName;

    [Header("Resource Match")]
    [Tooltip("Matches ResourceDefinition.resourceID. Example: contaminated_water, mixed_water, spd.")]
    public string resourceId;

    [Header("Consumption Mode")]
    [Tooltip("If true, this disease risk can trigger when this resource is consumed for nutrition/food.")]
    public bool applyWhenConsumedForNutrition = true;

    [Tooltip("If true, this disease risk can trigger when this resource is consumed for hydration/water.")]
    public bool applyWhenConsumedForHydration = true;

    [Header("Disease")]
    public DiseaseDefinitionSO disease;

    [Header("Risk")]
    [Tooltip("Base chance that one fully exposed person gets infected.")]
    [Range(0f, 1f)]
    public float infectionChancePerPerson = 0.1f;

    [Tooltip("Base exposure strength for a fully exposed person.")]
    [Range(0f, 1f)]
    public float exposureStrength01 = 1f;

    [Header("Point-Based Exposure Scaling")]
    [Tooltip("Use consumed nutrition/hydration points to estimate how many people were exposed.")]
    public bool scaleExposureByConsumedPoints = true;

    [Tooltip("Multiplier after converting consumed points into people. 1 = normal.")]
    [Min(0f)]
    public float exposedPeopleMultiplier = 1f;

    [Tooltip("If true, partial consumption still checks 1 person, but with reduced exposure strength.")]
    public bool targetOnePersonForPartialConsumption = true;

    [Tooltip("Safety cap so one big consumption event does not roll too many infections.")]
    [Min(1)]
    public int maxPeopleCheckedPerConsumption = 10;

    [Header("Targeting")]
    [Tooltip("If true, one consumption event tries to target different people instead of randomly hitting the same person repeatedly.")]
    public bool preferDifferentPeople = true;

    [Tooltip("If true, skip people who already have this disease when choosing exposed targets.")]
    public bool skipPeopleAlreadyInfectedWithThisDisease = true;

    [Header("Partial Consumption Balance")]
    [Tooltip("When a small amount is consumed, use at least this portion of exposure. Example: 0.25 means even tiny spoiled food gives 25% of normal exposure.")]
    [Range(0f, 1f)]
    public float minimumPartialExposurePortion01 = 0.25f;

    [Header("Source")]
    public DiseaseSourceType sourceType = DiseaseSourceType.UnsafeConsumedResource;

    [Tooltip("Optional label for debug logs. Example: Spoiled Food, Contaminated Water.")]
    public string debugLabel;

    public bool Matches(ResourceDefinition def)
    {
        if (def == null)
            return false;

        if (string.IsNullOrWhiteSpace(resourceId))
            return false;

        return string.Equals(def.resourceID, resourceId, StringComparison.OrdinalIgnoreCase);
    }

    public bool MatchesConsumptionMode(bool wasNutrition, bool wasHydration)
    {
        if (wasNutrition && !applyWhenConsumedForNutrition)
            return false;

        if (wasHydration && !applyWhenConsumedForHydration)
            return false;

        return true;
    }

    public float CalculateExposedPeopleFloat(float pointsConsumed, float pointsPerPersonScale)
    {
        if (pointsConsumed <= 0f)
            return 0f;

        if (!scaleExposureByConsumedPoints)
            return 1f;

        pointsPerPersonScale = Mathf.Max(0.0001f, pointsPerPersonScale);

        return Mathf.Max(0f, (pointsConsumed / pointsPerPersonScale) * exposedPeopleMultiplier);
    }

    public int CalculatePeopleToCheck(float exposedPeopleFloat)
    {
        if (exposedPeopleFloat <= 0f)
            return 0;

        int peopleToCheck = Mathf.CeilToInt(exposedPeopleFloat);

        if (targetOnePersonForPartialConsumption)
            peopleToCheck = Mathf.Max(1, peopleToCheck);

        return Mathf.Clamp(peopleToCheck, 1, Mathf.Max(1, maxPeopleCheckedPerConsumption));
    }

    public float CalculateTargetExposureStrength(float exposedPeopleFloat, int targetIndex)
    {
        if (!scaleExposureByConsumedPoints)
            return Mathf.Clamp01(exposureStrength01);

        float remainingForThisTarget = exposedPeopleFloat - targetIndex;
        float targetPortion01 = Mathf.Clamp01(remainingForThisTarget);

        // If this is a tiny partial consumption, still let it target 1 person
        // with a minimum partial exposure.
        if (targetOnePersonForPartialConsumption &&
            targetIndex == 0 &&
            exposedPeopleFloat > 0f &&
            exposedPeopleFloat < 1f)
        {
            targetPortion01 = Mathf.Max(targetPortion01, minimumPartialExposurePortion01);
        }

        return Mathf.Clamp01(exposureStrength01 * targetPortion01);
    }
}