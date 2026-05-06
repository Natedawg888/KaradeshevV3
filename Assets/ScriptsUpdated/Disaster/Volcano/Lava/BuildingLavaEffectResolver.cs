using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildingLavaEffectResolver : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LavaOverlayManager lavaOverlayManager;
    [SerializeField] private WeatherGridManager weatherGridManager;

    [Header("Timing")]
    [Tooltip("Apply lava impact immediately when a new lava cell activates.")]
    [SerializeField] private bool applyWhenLavaCellActivates = true;

    [Tooltip("Apply ongoing lava impact each end of turn to buildings still under lava.")]
    [SerializeField] private bool applyOngoingEffectsEachTurn = true;

    [Header("Damage")]
    [Tooltip("Damage applied when lava first reaches a building cell.")]
    [Min(0)][SerializeField] private int activationLavaDamage = 20;

    [Tooltip("Damage applied each turn while the building remains under lava.")]
    [Min(0)][SerializeField] private int ongoingLavaDamagePerTurn = 10;

    [Tooltip("If true, a multi-cell building is only damaged once per lava pass, even if multiple lava cells touch it.")]
    [SerializeField] private bool damageEachBuildingOnlyOncePerPass = true;

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

    [Tooltip("Maximum total resource units destroyed from this storage building per lava step. 0 = no cap.")]
    [Min(0)][SerializeField] private int maxStorageResourcesDestroyedPerStep = 12;

    [SerializeField] private bool lavaCanDestroySpoiledFood = true;

    [Header("Over-Frame Processing")]
    [SerializeField] private bool processOngoingEffectsOverFrames = true;

    [Min(1)]
    [SerializeField] private int lavaCellsProcessedPerFrame = 8;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = false;

    private readonly List<TileCoord> activeLavaCellsScratch = new List<TileCoord>(128);
    private readonly HashSet<int> processedBuildingsThisPass = new HashSet<int>();

    private Coroutine ongoingRoutine;
    private LavaOverlayManager subscribedLavaOverlayManager;

    private void Awake()
    {
        EnsureLinks();
    }

    private void OnEnable()
    {
        EnsureLinks();
        RebindLavaEvents();

        if (applyOngoingEffectsEachTurn)
            TurnSystem.SubscribeToEndOfTurn(HandleEndOfTurn);
    }

    private void Start()
    {
        EnsureLinks();
        RebindLavaEvents();
    }

    private void OnDisable()
    {
        UnbindLavaEvents();
        TurnSystem.UnsubscribeFromEndOfTurn(HandleEndOfTurn);

        if (ongoingRoutine != null)
        {
            StopCoroutine(ongoingRoutine);
            ongoingRoutine = null;
        }

        processedBuildingsThisPass.Clear();
        activeLavaCellsScratch.Clear();
    }

    private void EnsureLinks()
    {
        if (lavaOverlayManager == null)
            lavaOverlayManager = LavaOverlayManager.Instance;

        if (weatherGridManager == null)
            weatherGridManager = WeatherGridManager.Instance;
    }

    private void RebindLavaEvents()
    {
        if (subscribedLavaOverlayManager == lavaOverlayManager)
            return;

        UnbindLavaEvents();

        subscribedLavaOverlayManager = lavaOverlayManager;

        if (subscribedLavaOverlayManager == null)
            return;

        subscribedLavaOverlayManager.OnLavaCellActivated += HandleLavaCellActivated;
    }

    private void UnbindLavaEvents()
    {
        if (subscribedLavaOverlayManager == null)
            return;

        subscribedLavaOverlayManager.OnLavaCellActivated -= HandleLavaCellActivated;
        subscribedLavaOverlayManager = null;
    }

    private void HandleLavaCellActivated(TileCoord coord)
    {
        if (!applyWhenLavaCellActivates)
            return;

        EnsureLinks();

        processedBuildingsThisPass.Clear();
        ApplyLavaEffectsAtCell(coord, activationLavaDamage, isActivation: true);
        processedBuildingsThisPass.Clear();
    }

    private void HandleEndOfTurn()
    {
        if (!applyOngoingEffectsEachTurn)
            return;

        EnsureLinks();

        if (lavaOverlayManager == null)
            return;

        if (!lavaOverlayManager.CopyActiveLavaCells(activeLavaCellsScratch))
            return;

        if (processOngoingEffectsOverFrames)
        {
            if (ongoingRoutine == null)
                ongoingRoutine = StartCoroutine(ProcessOngoingLavaEffectsRoutine());
        }
        else
        {
            ProcessOngoingLavaEffectsImmediate();
        }
    }

    private IEnumerator ProcessOngoingLavaEffectsRoutine()
    {
        processedBuildingsThisPass.Clear();

        int processed = 0;
        int maxPerFrame = Mathf.Max(1, lavaCellsProcessedPerFrame);

        for (int i = 0; i < activeLavaCellsScratch.Count; i++)
        {
            ApplyLavaEffectsAtCell(
                activeLavaCellsScratch[i],
                ongoingLavaDamagePerTurn,
                isActivation: false);

            processed++;

            if (processed >= maxPerFrame)
            {
                processed = 0;
                yield return null;
            }
        }

        processedBuildingsThisPass.Clear();
        ongoingRoutine = null;
    }

    private void ProcessOngoingLavaEffectsImmediate()
    {
        processedBuildingsThisPass.Clear();

        for (int i = 0; i < activeLavaCellsScratch.Count; i++)
        {
            ApplyLavaEffectsAtCell(
                activeLavaCellsScratch[i],
                ongoingLavaDamagePerTurn,
                isActivation: false);
        }

        processedBuildingsThisPass.Clear();
    }

    private void ApplyLavaEffectsAtCell(TileCoord coord, int baseDamage, bool isActivation)
    {
        if (weatherGridManager == null)
            return;

        if (!weatherGridManager.TryGetBuildingAtCell(
                coord.x,
                coord.y,
                out PlayerBuildingManager.Record record) ||
            record == null ||
            record.instance == null)
        {
            return;
        }

        int buildingKey = record.instance.GetInstanceID();

        if (damageEachBuildingOnlyOncePerPass && processedBuildingsThisPass.Contains(buildingKey))
            return;

        if (damageEachBuildingOnlyOncePerPass)
            processedBuildingsThisPass.Add(buildingKey);

        GameObject buildingObject = record.instance;

        BuildingLavaResistance resistance = buildingObject.GetComponent<BuildingLavaResistance>();
        if (resistance == null)
            resistance = buildingObject.GetComponentInChildren<BuildingLavaResistance>(true);
        if (resistance == null)
            resistance = buildingObject.GetComponentInParent<BuildingLavaResistance>();

        if (resistance != null && resistance.lavaImmune)
        {
            if (debugLogging || resistance.debugLogging)
            {
                Debug.Log(
                    $"[BuildingLavaEffectResolver] Lava ignored immune building '{buildingObject.name}' at ({coord.x},{coord.y}).");
            }

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

        ApplyCoreBuildingDamage(buildingObject, finalDamage);
        TryIgniteBuildingFire(buildingObject, resistance);
        ApplySecondaryEffects(buildingObject, buildingKey, severity01);

        if (debugLogging || (resistance != null && resistance.debugLogging))
        {
            Debug.Log(
                $"[BuildingLavaEffectResolver] Lava affected building '{buildingObject.name}' " +
                $"cell=({coord.x},{coord.y}) activation={isActivation} " +
                $"baseDamage={baseDamage} finalDamage={finalDamage} severity={severity01:0.00}");
        }
    }

    private void ApplyCoreBuildingDamage(GameObject buildingObject, int finalDamage)
    {
        if (finalDamage <= 0 || buildingObject == null)
            return;

        BuildingControl building = buildingObject.GetComponent<BuildingControl>();
        if (building == null)
            building = buildingObject.GetComponentInChildren<BuildingControl>(true);
        if (building == null)
            building = buildingObject.GetComponentInParent<BuildingControl>();

        if (building != null)
            building.ApplyDamage(finalDamage);
    }

    private void TryIgniteBuildingFire(GameObject buildingObject, BuildingLavaResistance resistance)
    {
        if (!igniteBuildingFire || buildingObject == null)
            return;

        float finalChance = resistance != null
            ? resistance.ModifyIgnitionChance(lavaFireIgnitionChance)
            : Mathf.Clamp01(lavaFireIgnitionChance);

        int finalBurnTurns = resistance != null
            ? resistance.ModifyBurnTurns(lavaFireBurnTurns)
            : Mathf.Max(1, lavaFireBurnTurns);

        if (finalChance <= 0f || finalBurnTurns <= 0)
            return;

        BuildingFireState fireState = buildingObject.GetComponent<BuildingFireState>();
        if (fireState == null)
            fireState = buildingObject.GetComponentInChildren<BuildingFireState>(true);
        if (fireState == null)
            fireState = buildingObject.GetComponentInParent<BuildingFireState>();

        if (fireState != null)
            fireState.TryIgnite(finalChance, finalBurnTurns);
    }

    private void ApplySecondaryEffects(GameObject buildingObject, int buildingKey, float severity01)
    {
        if (buildingObject == null || severity01 <= 0f)
            return;

        BuildingControl building = buildingObject.GetComponent<BuildingControl>();
        if (building == null)
            building = buildingObject.GetComponentInChildren<BuildingControl>(true);
        if (building == null)
            building = buildingObject.GetComponentInParent<BuildingControl>();

        if (building == null)
            return;

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
        {
            production.RegisterFireImpact(
                buildingKey,
                severity01 * productionImpactMultiplier,
                debugLogging);
        }

        KineticWarfareControl training = building.GetComponent<KineticWarfareControl>();
        if (training == null)
            training = building.GetComponentInChildren<KineticWarfareControl>(true);

        if (applyTrainingEffects && training != null)
        {
            training.RegisterFireImpact(
                buildingKey,
                severity01 * trainingImpactMultiplier,
                debugLogging);
        }

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