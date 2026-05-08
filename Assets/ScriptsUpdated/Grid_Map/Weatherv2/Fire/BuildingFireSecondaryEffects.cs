using UnityEngine;

[DisallowMultipleComponent]
public class BuildingFireSecondaryEffects : MonoBehaviour
{
    [Header("Core Building Fire Damage")]
    [SerializeField] private bool applyCoreBuildingDamage = true;

    [Tooltip("1 = use final fire damage as-is. 0.5 = half damage. 2 = double damage.")]
    [Min(0f)]
    [SerializeField] private float coreBuildingDamageMultiplier = 1f;

    [Header("Secondary Effects")]
    [SerializeField] private bool applyShelterEffects = true;
    [SerializeField] private bool applyCraftingEffects = true;
    [SerializeField] private bool applyProductionEffects = true;
    [SerializeField] private bool applyTrainingEffects = true;
    [SerializeField] private bool applyStorageEffects = true;

    [Min(0f)][SerializeField] private float shelterCasualtyMultiplier = 0.50f;
    [Min(0f)][SerializeField] private float craftingImpactMultiplier = 0.40f;
    [Min(0f)][SerializeField] private float productionImpactMultiplier = 0.35f;
    [Min(0f)][SerializeField] private float trainingImpactMultiplier = 0.35f;

    [Header("Storage Fire Effects")]
    [Tooltip("Scales how much stored resource is destroyed from fire damage severity.")]
    [Min(0f)][SerializeField] private float storageResourceLossMultiplier = 0.30f;

    [Tooltip("Maximum total resource units destroyed from this storage building per fire damage step. 0 = no cap.")]
    [Min(0)][SerializeField] private int maxStorageResourcesDestroyedPerStep = 8;

    [Tooltip("If false, spoiled food stacks are ignored by fire resource destruction.")]
    [SerializeField] private bool fireCanDestroySpoiledFood = true;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = false;

    private BuildingFireState fireState;
    private BuildingControl building;

    private int baseDamagePerStep = 8;

    private void OnEnable()
    {
        EnsureRefs();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    public void SetBaseDamagePerStep(int value)
    {
        baseDamagePerStep = Mathf.Max(1, value);
    }

    private void EnsureRefs()
    {
        if (fireState == null)
            fireState = GetComponent<BuildingFireState>();

        if (fireState == null)
            fireState = GetComponentInChildren<BuildingFireState>(true);

        if (building == null)
            building = GetComponent<BuildingControl>();

        if (building == null)
            building = GetComponentInChildren<BuildingControl>(true);

        if (building == null)
            building = GetComponentInParent<BuildingControl>();
    }

    private void Subscribe()
    {
        if (fireState == null)
            return;

        fireState.OnFireDamageStep -= HandleFireDamageStep;
        fireState.OnFireDamageStep += HandleFireDamageStep;

        fireState.OnExtinguished -= HandleFireExtinguished;
        fireState.OnExtinguished += HandleFireExtinguished;
    }

    private void Unsubscribe()
    {
        if (fireState == null)
            return;

        fireState.OnFireDamageStep -= HandleFireDamageStep;
        fireState.OnExtinguished -= HandleFireExtinguished;
    }

    private void HandleFireDamageStep(BuildingFireState state, int finalDamage)
    {
        EnsureRefs();

        if (finalDamage <= 0)
            return;

        float damageRatio = baseDamagePerStep > 0
            ? finalDamage / (float)baseDamagePerStep
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
                float storageSeverity01 = Mathf.Clamp01(damageRatio * storageResourceLossMultiplier);

                storageResourcesDestroyed = storage.TryApplyFireStorageLoss(
                    storageSeverity01,
                    maxStorageResourcesDestroyedPerStep,
                    fireCanDestroySpoiledFood,
                    debugLogging);
            }
        }

        if (debugLogging)
        {
            string buildingName = building != null ? building.name : name;

            //Debug.Log(
                //$"[BuildingFireSecondaryEffects] Fire damaged '{buildingName}'. " +
                //$"IncomingFireDamage={finalDamage} | " +
                //$"AppliedBuildingDamage={actualBuildingDamage} | " +
                //$"ShelterDeaths={killedInShelter} | " +
                //$"CancelledCraftOrders={cancelledCraftOrders} | " +
                //$"CrafterDeaths={killedCrafters} | " +
                //$"ProductionPaused={productionPaused} | " +
                //$"ProductionWorkerDeaths={killedProductionWorkers} | " +
                //$"TrainingPaused={trainingPaused} | " +
                //$"TrainingDeaths={killedTrainees} | " +
                //$"StorageDestroyed={storageResourcesDestroyed}");
        }
    }

    private void HandleFireExtinguished(BuildingFireState state)
    {
        EnsureRefs();

        if (building == null)
            return;

        ProductionBuildingControl production = building.GetComponent<ProductionBuildingControl>();
        if (production == null)
            production = building.GetComponentInChildren<ProductionBuildingControl>(true);

        if (production != null)
            production.NotifyFireCleared(building.GetInstanceID(), debugLogging);

        KineticWarfareControl training = building.GetComponent<KineticWarfareControl>();
        if (training == null)
            training = building.GetComponentInChildren<KineticWarfareControl>(true);

        if (training != null)
            training.NotifyFireCleared(building.GetInstanceID(), debugLogging);
    }
}
