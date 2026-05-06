using UnityEngine;

[DisallowMultipleComponent]
public class BuildingEarthquakeSecondaryEffects : MonoBehaviour
{
    [Header("Core Building Earthquake Damage")]
    [SerializeField] private bool applyCoreBuildingDamage = true;

    [Tooltip("1 = use final earthquake damage as-is. 0.5 = half damage. 2 = double damage.")]
    [Min(0f)]
    [SerializeField] private float coreBuildingDamageMultiplier = 1f;

    [Header("Secondary Effects")]
    [SerializeField] private bool applyShelterEffects = true;
    [SerializeField] private bool applyCraftingEffects = true;
    [SerializeField] private bool applyProductionEffects = true;
    [SerializeField] private bool applyTrainingEffects = true;
    [SerializeField] private bool applyStorageEffects = true;

    [Header("Effect Scaling")]
    [Tooltip("Used to turn incoming earthquake damage into a 0-1-ish severity ratio.")]
    [Min(1)]
    [SerializeField] private int baseDamageReference = 20;

    [Min(0f)][SerializeField] private float shelterCasualtyMultiplier = 0.20f;
    [Min(0f)][SerializeField] private float craftingImpactMultiplier = 0.30f;
    [Min(0f)][SerializeField] private float productionImpactMultiplier = 0.30f;
    [Min(0f)][SerializeField] private float trainingImpactMultiplier = 0.30f;

    [Header("Storage Earthquake Effects")]
    [Tooltip("Scales how much stored resource is destroyed from earthquake damage severity.")]
    [Min(0f)]
    [SerializeField] private float storageResourceLossMultiplier = 0.15f;

    [Tooltip("Maximum total resource units destroyed from this storage building per earthquake hit. 0 = no cap.")]
    [Min(0)]
    [SerializeField] private int maxStorageResourcesDestroyedPerHit = 6;

    [Tooltip("If false, spoiled food stacks are ignored by earthquake resource destruction.")]
    [SerializeField] private bool earthquakeCanDestroySpoiledFood = true;

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

    // Called by EarthquakeBuildingEffectResolver using SendMessage or direct call.
    public void ApplyEarthquakeDamage(int finalDamage)
    {
        EnsureRefs();

        if (finalDamage <= 0)
            return;

        float damageRatio = baseDamageReference > 0
            ? finalDamage / (float)baseDamageReference
            : 1f;

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
                // Reuses your existing casualty hook for now.
                killedInShelter = shelter.TryApplyFireCasualties(
                    damageRatio * shelterCasualtyMultiplier,
                    debugLogging
                );
            }

            CraftingBuildingControl crafting = building.GetComponent<CraftingBuildingControl>();
            if (crafting == null)
                crafting = building.GetComponentInChildren<CraftingBuildingControl>(true);

            if (applyCraftingEffects && crafting != null)
            {
                // Reuses your existing crafting impact hook for now.
                var craftingImpact = crafting.TryApplyFireCraftingImpact(
                    damageRatio * craftingImpactMultiplier,
                    debugLogging
                );

                cancelledCraftOrders = craftingImpact.cancelledOrders;
                killedCrafters = craftingImpact.workersKilled;
            }

            ProductionBuildingControl production = building.GetComponent<ProductionBuildingControl>();
            if (production == null)
                production = building.GetComponentInChildren<ProductionBuildingControl>(true);

            if (applyProductionEffects && production != null)
            {
                // Reuses your existing pause/worker impact hook for now.
                var productionImpact = production.RegisterFireImpact(
                    building.GetInstanceID(),
                    damageRatio * productionImpactMultiplier,
                    debugLogging
                );

                killedProductionWorkers = productionImpact.workersKilled;
                productionPaused = productionImpact.paused;
            }

            KineticWarfareControl training = building.GetComponent<KineticWarfareControl>();
            if (training == null)
                training = building.GetComponentInChildren<KineticWarfareControl>(true);

            if (applyTrainingEffects && training != null)
            {
                // Reuses your existing training impact hook for now.
                var trainingImpact = training.RegisterFireImpact(
                    building.GetInstanceID(),
                    damageRatio * trainingImpactMultiplier,
                    debugLogging
                );

                killedTrainees = trainingImpact.traineesKilled;
                trainingPaused = trainingImpact.paused;
            }

            StorageBuildingControl storage = building.GetComponent<StorageBuildingControl>();
            if (storage == null)
                storage = building.GetComponentInChildren<StorageBuildingControl>(true);

            if (applyStorageEffects && storage != null)
            {
                float storageSeverity01 = Mathf.Clamp01(
                    damageRatio * storageResourceLossMultiplier
                );

                storageResourcesDestroyed = storage.TryApplyFireStorageLoss(
                    storageSeverity01,
                    maxStorageResourcesDestroyedPerHit,
                    earthquakeCanDestroySpoiledFood,
                    debugLogging
                );
            }
        }

        if (debugLogging)
        {
            string buildingName = building != null ? building.name : name;

            Debug.Log(
                $"[BuildingEarthquakeSecondaryEffects] Earthquake damaged '{buildingName}'. " +
                $"IncomingEarthquakeDamage={finalDamage} | " +
                $"AppliedBuildingDamage={actualBuildingDamage} | " +
                $"ShelterDeaths={killedInShelter} | " +
                $"CancelledCraftOrders={cancelledCraftOrders} | " +
                $"CrafterDeaths={killedCrafters} | " +
                $"ProductionPaused={productionPaused} | " +
                $"ProductionWorkerDeaths={killedProductionWorkers} | " +
                $"TrainingPaused={trainingPaused} | " +
                $"TrainingDeaths={killedTrainees} | " +
                $"StorageDestroyed={storageResourcesDestroyed}"
            );
        }
    }

    // Called by EarthquakeBuildingEffectResolver if you want non-damage reactions later.
    public void OnEarthquakeHit(EarthquakeEventData data)
    {
        if (!debugLogging || data == null)
            return;

        string buildingName = building != null ? building.name : name;

        Debug.Log(
            $"[BuildingEarthquakeSecondaryEffects] '{buildingName}' received earthquake hit. " +
            $"Magnitude={data.magnitude:0.0}, Epicentre={data.epicentreBlock}"
        );
    }
}