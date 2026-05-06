using UnityEngine;

[DisallowMultipleComponent]
public class BuildingFloodSecondaryEffects : MonoBehaviour
{
    [Header("Core Building Flood Damage")]
    [SerializeField] private bool applyCoreBuildingDamage = true;

    [Tooltip("1 = use final flood damage as-is. 0.5 = half damage. 2 = double damage.")]
    [Min(0f)]
    [SerializeField] private float coreBuildingDamageMultiplier = 1f;

    [Header("Secondary Effects")]
    [SerializeField] private bool applyShelterEffects = true;
    [SerializeField] private bool applyCraftingEffects = true;
    [SerializeField] private bool applyProductionEffects = true;
    [SerializeField] private bool applyTrainingEffects = true;
    [SerializeField] private bool applyStorageEffects = true;

    [Header("Effect Scaling")]
    [Tooltip("Used to turn incoming flood damage into a 0-1-ish severity ratio.")]
    [Min(1)]
    [SerializeField] private int baseDamageReference = 12;

    [Min(0f)][SerializeField] private float shelterCasualtyMultiplier = 0.12f;
    [Min(0f)][SerializeField] private float craftingImpactMultiplier = 0.20f;
    [Min(0f)][SerializeField] private float productionImpactMultiplier = 0.20f;
    [Min(0f)][SerializeField] private float trainingImpactMultiplier = 0.20f;

    [Header("Storage Flood Effects")]
    [Tooltip("Scales how much stored resource is destroyed/spoiled from flood severity.")]
    [Min(0f)]
    [SerializeField] private float storageResourceLossMultiplier = 0.18f;

    [Tooltip("Maximum total resource units destroyed from this storage building per flood tick. 0 = no cap.")]
    [Min(0)]
    [SerializeField] private int maxStorageResourcesDestroyedPerHit = 4;

    [Tooltip("If false, spoiled food stacks are ignored by flood resource destruction.")]
    [SerializeField] private bool floodCanDestroySpoiledFood = true;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = true;

    private BuildingControl building;

    private void Awake()
    {
        EnsureRefs();
    }

    private void OnEnable()
    {
        EnsureRefs();
    }

    private void EnsureRefs()
    {
        if (building == null)
            building = GetComponent<BuildingControl>();

        if (building == null)
            building = GetComponentInChildren<BuildingControl>(true);

        if (building == null)
            building = GetComponentInParent<BuildingControl>();
    }

    public void ApplyFloodDamage(FloodBuildingHitData hitData)
    {
        EnsureRefs();

        if (hitData == null)
            return;

        int finalDamage = Mathf.Max(0, hitData.finalDamage);

        if (finalDamage <= 0)
            return;

        float damageRatio = baseDamageReference > 0
            ? finalDamage / (float)baseDamageReference
            : 1f;

        damageRatio = Mathf.Clamp01(damageRatio);

        int actualBuildingDamage = 0;

        if (applyCoreBuildingDamage && building != null)
        {
            actualBuildingDamage = Mathf.RoundToInt(finalDamage * coreBuildingDamageMultiplier);
            actualBuildingDamage = Mathf.Max(0, actualBuildingDamage);

            if (actualBuildingDamage > 0)
                building.ApplyDamage(actualBuildingDamage);
        }

        int killedInShelter = 0;
        int cancelledCraftOrders = 0;
        int killedCrafters = 0;
        int killedProductionWorkers = 0;
        bool productionPaused = false;
        int killedTrainees = 0;
        bool trainingPaused = false;
        int storageResourcesDestroyed = 0;

        if (building != null)
        {
            ShelterControl shelter = building.GetComponent<ShelterControl>();
            if (shelter == null)
                shelter = building.GetComponentInChildren<ShelterControl>(true);

            if (applyShelterEffects && shelter != null)
            {
                killedInShelter = shelter.TryApplyFireCasualties(
                    damageRatio * shelterCasualtyMultiplier,
                    debugLogging);
            }

            CraftingBuildingControl crafting = building.GetComponent<CraftingBuildingControl>();
            if (crafting == null)
                crafting = building.GetComponentInChildren<CraftingBuildingControl>(true);

            if (applyCraftingEffects && crafting != null)
            {
                var craftingImpact = crafting.TryApplyFireCraftingImpact(
                    damageRatio * craftingImpactMultiplier,
                    debugLogging);

                cancelledCraftOrders = craftingImpact.cancelledOrders;
                killedCrafters = craftingImpact.workersKilled;
            }

            ProductionBuildingControl production = building.GetComponent<ProductionBuildingControl>();
            if (production == null)
                production = building.GetComponentInChildren<ProductionBuildingControl>(true);

            if (applyProductionEffects && production != null)
            {
                var productionImpact = production.RegisterFireImpact(
                    building.GetInstanceID(),
                    damageRatio * productionImpactMultiplier,
                    debugLogging);

                killedProductionWorkers = productionImpact.workersKilled;
                productionPaused = productionImpact.paused;
            }

            KineticWarfareControl training = building.GetComponent<KineticWarfareControl>();
            if (training == null)
                training = building.GetComponentInChildren<KineticWarfareControl>(true);

            if (applyTrainingEffects && training != null)
            {
                var trainingImpact = training.RegisterFireImpact(
                    building.GetInstanceID(),
                    damageRatio * trainingImpactMultiplier,
                    debugLogging);

                killedTrainees = trainingImpact.traineesKilled;
                trainingPaused = trainingImpact.paused;
            }

            StorageBuildingControl storage = building.GetComponent<StorageBuildingControl>();
            if (storage == null)
                storage = building.GetComponentInChildren<StorageBuildingControl>(true);

            if (applyStorageEffects && storage != null)
            {
                float storageSeverity01 = Mathf.Clamp01(
                    damageRatio * storageResourceLossMultiplier);

                storageResourcesDestroyed = storage.TryApplyFireStorageLoss(
                    storageSeverity01,
                    maxStorageResourcesDestroyedPerHit,
                    floodCanDestroySpoiledFood,
                    debugLogging);
            }
        }

        if (debugLogging)
        {
            string buildingName = building != null ? building.name : name;

            Debug.Log(
                $"[BuildingFloodSecondaryEffects] Flood damaged '{buildingName}'. " +
                $"Turn={hitData.turnIndex} | " +
                $"AvgDepth={hitData.averageDepth01:0.00} | " +
                $"MaxDepth={hitData.maxDepth01:0.00} | " +
                $"HitCells={hitData.hitCellCount}/{hitData.buildingCellCount} | " +
                $"IncomingFloodDamage={finalDamage} | " +
                $"AppliedBuildingDamage={actualBuildingDamage} | " +
                $"ShelterDeaths={killedInShelter} | " +
                $"CancelledCraftOrders={cancelledCraftOrders} | " +
                $"CrafterDeaths={killedCrafters} | " +
                $"ProductionPaused={productionPaused} | " +
                $"ProductionWorkerDeaths={killedProductionWorkers} | " +
                $"TrainingPaused={trainingPaused} | " +
                $"TrainingDeaths={killedTrainees} | " +
                $"StorageDestroyed={storageResourcesDestroyed}");
        }
    }

    public void OnFloodHit(FloodBuildingHitData hitData)
    {
        if (!debugLogging || hitData == null)
            return;

        string buildingName = building != null ? building.name : name;

        Debug.Log(
            $"[BuildingFloodSecondaryEffects] '{buildingName}' received flood hit. " +
            $"Turn={hitData.turnIndex}, AvgDepth={hitData.averageDepth01:0.00}, MaxDepth={hitData.maxDepth01:0.00}");
    }
}