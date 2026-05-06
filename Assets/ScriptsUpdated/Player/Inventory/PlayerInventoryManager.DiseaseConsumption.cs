using UnityEngine;

public partial class PlayerInventoryManager
{
    [Header("Disease From Consumed Resources")]
    [Tooltip("If true, consumed food/water resources can trigger disease risk by resourceID.")]
    public bool enableConsumedResourceDiseaseRisk = true;

    [Tooltip("Debug logs for resourceID-based disease risk after population consumes food/water.")]
    public bool debugConsumedResourceDiseaseRisk = true;

    private void TryApplyDiseaseRiskFromConsumedResource(
        ResourceDefinition def,
        int unitsConsumed,
        float pointsConsumed,
        bool useNutrition)
    {
        if (!enableConsumedResourceDiseaseRisk)
            return;

        if (def == null || unitsConsumed <= 0 || pointsConsumed <= 0f)
            return;

        DiseaseManager diseaseManager = DiseaseManager.Instance;
        if (diseaseManager == null)
            return;

        bool wasNutrition = useNutrition;
        bool wasHydration = !useNutrition;

        int infections = diseaseManager.TryApplyConsumedResourceDiseaseRisk(
            def,
            unitsConsumed,
            pointsConsumed,
            wasNutrition,
            wasHydration);

        if (debugConsumedResourceDiseaseRisk && infections > 0)
        {
            Debug.Log(
                $"[INV][Disease] Consumed resource caused disease. " +
                $"Resource={def.resourceName} ({def.resourceID}), " +
                $"Units={unitsConsumed}, " +
                $"Points={pointsConsumed:F2}, " +
                $"Mode={(useNutrition ? "Nutrition" : "Hydration")}, " +
                $"Infections={infections}");
        }
    }
}