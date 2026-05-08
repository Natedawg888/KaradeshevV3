using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BuildingLavaEffectResolver : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("Apply lava impact immediately when a new lava cell activates on this building.")]
    [SerializeField] private bool applyWhenLavaCellActivates = true;

    [Tooltip("Apply ongoing lava impact each end of turn while this building is under lava.")]
    [SerializeField] private bool applyOngoingEffectsEachTurn = true;

    [Header("Damage")]
    [Tooltip("Damage applied when lava first reaches this building.")]
    [Min(0)][SerializeField] private int activationLavaDamage = 20;

    [Tooltip("Damage applied each turn while this building remains under lava.")]
    [Min(0)][SerializeField] private int ongoingLavaDamagePerTurn = 10;

    [Header("Fire Ignition")]
    [SerializeField] private bool igniteBuildingFire = true;

    [Range(0f, 1f)]
    [SerializeField] private float lavaFireIgnitionChance = 1f;

    [Min(1)]
    [SerializeField] private int lavaFireBurnTurns = 8;

    [Header("Secondary Effects")]
    [SerializeField] private bool applyShelterEffects = true;
    [SerializeField] private bool applyCraftingEffects = true;
    [SerializeField] private bool applyProductionEffects = true;
    [SerializeField] private bool applyTrainingEffects = true;
    [SerializeField] private bool applyStorageEffects = true;

    [Min(0f)][SerializeField] private float shelterCasualtyMultiplier = 0.70f;
    [Min(0f)][SerializeField] private float craftingImpactMultiplier = 0.55f;
    [Min(0f)][SerializeField] private float productionImpactMultiplier = 0.55f;
    [Min(0f)][SerializeField] private float trainingImpactMultiplier = 0.55f;

    [Header("Storage Lava Effects")]
    [Min(0f)][SerializeField] private float storageResourceLossMultiplier = 0.50f;

    [Tooltip("Maximum total resource units destroyed from this building per lava step. 0 = no cap.")]
    [Min(0)][SerializeField] private int maxStorageResourcesDestroyedPerStep = 12;

    [SerializeField] private bool lavaCanDestroySpoiledFood = true;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = false;

    private BuildingControl building;
    private BuildingLavaResistance resistance;
    private LavaOverlayManager lavaOverlayManager;
    private WeatherGridManager weatherGridManager;

    private readonly List<TileCoord> activeLavaCellsScratch = new List<TileCoord>(32);
    private LavaOverlayManager subscribedManager;

    private void OnEnable()
    {
        EnsureRefs();
        BindLavaEvent();

        if (applyOngoingEffectsEachTurn)
            TurnSystem.SubscribeToEndOfTurn(HandleEndOfTurn);
    }

    private void OnDisable()
    {
        UnbindLavaEvent();
        TurnSystem.UnsubscribeFromEndOfTurn(HandleEndOfTurn);
        activeLavaCellsScratch.Clear();
    }

    private void EnsureRefs()
    {
        if (building == null)
            building = GetComponent<BuildingControl>();
        if (building == null)
            building = GetComponentInChildren<BuildingControl>(true);
        if (building == null)
            building = GetComponentInParent<BuildingControl>();

        if (resistance == null)
            resistance = GetComponent<BuildingLavaResistance>();
        if (resistance == null)
            resistance = GetComponentInChildren<BuildingLavaResistance>(true);
        if (resistance == null)
            resistance = GetComponentInParent<BuildingLavaResistance>();

        if (lavaOverlayManager == null)
            lavaOverlayManager = LavaOverlayManager.Instance;

        if (weatherGridManager == null)
            weatherGridManager = WeatherGridManager.Instance;
    }

    private void BindLavaEvent()
    {
        if (lavaOverlayManager == null || subscribedManager == lavaOverlayManager)
            return;

        UnbindLavaEvent();
        subscribedManager = lavaOverlayManager;
        subscribedManager.OnLavaCellActivated += HandleLavaCellActivated;
    }

    private void UnbindLavaEvent()
    {
        if (subscribedManager == null)
            return;

        subscribedManager.OnLavaCellActivated -= HandleLavaCellActivated;
        subscribedManager = null;
    }

    private void HandleLavaCellActivated(TileCoord coord)
    {
        if (!applyWhenLavaCellActivates)
            return;

        EnsureRefs();

        if (!IsThisBuildingAtCoord(coord))
            return;

        ApplyLavaEffectsToSelf(activationLavaDamage, isActivation: true);
    }

    private void HandleEndOfTurn()
    {
        if (!applyOngoingEffectsEachTurn)
            return;

        EnsureRefs();

        if (lavaOverlayManager == null)
            return;

        if (!lavaOverlayManager.CopyActiveLavaCells(activeLavaCellsScratch))
            return;

        for (int i = 0; i < activeLavaCellsScratch.Count; i++)
        {
            if (IsThisBuildingAtCoord(activeLavaCellsScratch[i]))
            {
                // Apply once even if multiple lava cells touch this building.
                ApplyLavaEffectsToSelf(ongoingLavaDamagePerTurn, isActivation: false);
                return;
            }
        }
    }

    private bool IsThisBuildingAtCoord(TileCoord coord)
    {
        if (weatherGridManager == null)
            return false;

        if (!weatherGridManager.TryGetBuildingAtCell(coord.x, coord.y, out WorldBuildingManager.Record record) ||
            record == null ||
            record.instance == null)
        {
            return false;
        }

        return record.instance == gameObject ||
               (building != null && record.instance == building.gameObject);
    }

    private void ApplyLavaEffectsToSelf(int baseDamage, bool isActivation)
    {
        EnsureRefs();

        if (resistance != null && resistance.lavaImmune)
        {
            if (debugLogging || resistance.debugLogging) {}
                //Debug.Log($"[BuildingLavaEffectResolver] Lava ignored immune building '{name}'.");
            return;
        }

        int finalDamage = resistance != null
            ? resistance.ModifyLavaDamage(baseDamage)
            : Mathf.Max(0, baseDamage);

        float severity01 = baseDamage > 0
            ? Mathf.Clamp01(finalDamage / (float)baseDamage)
            : 0f;

        if (resistance != null)
            severity01 = resistance.ModifySecondarySeverity(severity01);

        ApplyCoreBuildingDamage(finalDamage);
        TryIgniteBuildingFire();
        ApplySecondaryEffects(severity01);

        if (debugLogging || (resistance != null && resistance.debugLogging))
        {
            //Debug.Log(
                //$"[BuildingLavaEffectResolver] Lava affected '{name}' " +
                //$"activation={isActivation} baseDamage={baseDamage} " +
                //$"finalDamage={finalDamage} severity={severity01:0.00}");
        }
    }

    private void ApplyCoreBuildingDamage(int finalDamage)
    {
        if (finalDamage <= 0 || building == null)
            return;

        building.ApplyDamage(finalDamage);
    }

    private void TryIgniteBuildingFire()
    {
        if (!igniteBuildingFire)
            return;

        float finalChance = resistance != null
            ? resistance.ModifyIgnitionChance(lavaFireIgnitionChance)
            : Mathf.Clamp01(lavaFireIgnitionChance);

        int finalBurnTurns = resistance != null
            ? resistance.ModifyBurnTurns(lavaFireBurnTurns)
            : Mathf.Max(1, lavaFireBurnTurns);

        if (finalChance <= 0f || finalBurnTurns <= 0)
            return;

        BuildingFireState fireState = GetComponent<BuildingFireState>();
        if (fireState == null)
            fireState = GetComponentInChildren<BuildingFireState>(true);
        if (fireState == null)
            fireState = GetComponentInParent<BuildingFireState>();

        if (fireState != null)
            fireState.TryIgnite(finalChance, finalBurnTurns);
    }

    private void ApplySecondaryEffects(float severity01)
    {
        if (building == null || severity01 <= 0f)
            return;

        int buildingKey = building.GetInstanceID();

        ShelterControl shelter = building.GetComponent<ShelterControl>();
        if (shelter == null)
            shelter = building.GetComponentInChildren<ShelterControl>(true);
        if (applyShelterEffects && shelter != null)
            shelter.TryApplyFireCasualties(severity01 * shelterCasualtyMultiplier, debugLogging);

        CraftingBuildingControl crafting = building.GetComponent<CraftingBuildingControl>();
        if (crafting == null)
            crafting = building.GetComponentInChildren<CraftingBuildingControl>(true);
        if (applyCraftingEffects && crafting != null)
            crafting.TryApplyFireCraftingImpact(severity01 * craftingImpactMultiplier, debugLogging);

        ProductionBuildingControl production = building.GetComponent<ProductionBuildingControl>();
        if (production == null)
            production = building.GetComponentInChildren<ProductionBuildingControl>(true);
        if (applyProductionEffects && production != null)
            production.RegisterFireImpact(buildingKey, severity01 * productionImpactMultiplier, debugLogging);

        KineticWarfareControl training = building.GetComponent<KineticWarfareControl>();
        if (training == null)
            training = building.GetComponentInChildren<KineticWarfareControl>(true);
        if (applyTrainingEffects && training != null)
            training.RegisterFireImpact(buildingKey, severity01 * trainingImpactMultiplier, debugLogging);

        StorageBuildingControl storage = building.GetComponent<StorageBuildingControl>();
        if (storage == null)
            storage = building.GetComponentInChildren<StorageBuildingControl>(true);
        if (applyStorageEffects && storage != null)
        {
            storage.TryApplyFireStorageLoss(
                Mathf.Clamp01(severity01 * storageResourceLossMultiplier),
                maxStorageResourcesDestroyedPerStep,
                lavaCanDestroySpoiledFood,
                debugLogging);
        }
    }
}
