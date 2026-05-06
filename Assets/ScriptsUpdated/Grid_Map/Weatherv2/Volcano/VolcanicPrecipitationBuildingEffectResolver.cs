using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VolcanicPrecipitationBuildingEffectResolver : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RainSimulationSystem rainSimulationSystem;
    [SerializeField] private WeatherGridManager weatherGridManager;

    [Header("Timing")]
    [SerializeField] private bool applyEffectsOnEndOfTurn = true;

    [Header("Building Damage")]
    [Min(0)][SerializeField] private int acidRainBuildingDamagePerTurn = 6;
    [Min(0)][SerializeField] private int ashFallBuildingDamagePerTurn = 3;

    [Tooltip("If true, a multi-cell building is only affected once per pass.")]
    [SerializeField] private bool affectEachBuildingOnlyOncePerPass = true;

    [Header("Secondary Effects")]
    [SerializeField] private bool applyShelterEffects = true;
    [SerializeField] private bool applyCraftingEffects = true;
    [SerializeField] private bool applyProductionEffects = true;
    [SerializeField] private bool applyTrainingEffects = true;
    [SerializeField] private bool applyStorageEffects = true;

    [Header("Acid Rain Secondary Multipliers")]
    [Min(0f)][SerializeField] private float acidShelterCasualtyMultiplier = 0.25f;
    [Min(0f)][SerializeField] private float acidCraftingImpactMultiplier = 0.35f;
    [Min(0f)][SerializeField] private float acidProductionImpactMultiplier = 0.45f;
    [Min(0f)][SerializeField] private float acidTrainingImpactMultiplier = 0.35f;
    [Min(0f)][SerializeField] private float acidStorageLossMultiplier = 0.20f;

    [Header("Ash Fall Secondary Multipliers")]
    [Min(0f)][SerializeField] private float ashShelterCasualtyMultiplier = 0.10f;
    [Min(0f)][SerializeField] private float ashCraftingImpactMultiplier = 0.25f;
    [Min(0f)][SerializeField] private float ashProductionImpactMultiplier = 0.35f;
    [Min(0f)][SerializeField] private float ashTrainingImpactMultiplier = 0.25f;
    [Min(0f)][SerializeField] private float ashStorageLossMultiplier = 0.15f;

    [Header("Storage Effects")]
    [Tooltip("Maximum total resource units destroyed from this storage building per volcanic precipitation step. 0 = no cap.")]
    [Min(0)][SerializeField] private int maxStorageResourcesDestroyedPerStep = 6;

    [SerializeField] private bool canDestroySpoiledFood = true;

    [Header("Over-Frame Processing")]
    [SerializeField] private bool processOverFrames = true;

    [Min(1)]
    [SerializeField] private int cellsProcessedPerFrame = 8;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = false;

    private readonly List<RainSimulationSystem.VolcanicPrecipitationCell> activeCellsScratch =
        new List<RainSimulationSystem.VolcanicPrecipitationCell>(128);

    private readonly HashSet<int> processedBuildingsThisPass = new HashSet<int>();

    private Coroutine processRoutine;

    private void Awake()
    {
        EnsureLinks();
    }

    private void OnEnable()
    {
        EnsureLinks();

        if (applyEffectsOnEndOfTurn)
            TurnSystem.SubscribeToEndOfTurn(HandleEndOfTurn);
    }

    private void Start()
    {
        EnsureLinks();
    }

    private void OnDisable()
    {
        TurnSystem.UnsubscribeFromEndOfTurn(HandleEndOfTurn);

        if (processRoutine != null)
        {
            StopCoroutine(processRoutine);
            processRoutine = null;
        }

        activeCellsScratch.Clear();
        processedBuildingsThisPass.Clear();
    }

    private void EnsureLinks()
    {
        if (rainSimulationSystem == null)
            rainSimulationSystem = RainSimulationSystem.Instance;

        if (weatherGridManager == null)
            weatherGridManager = WeatherGridManager.Instance;
    }

    private void HandleEndOfTurn()
    {
        EnsureLinks();

        if (rainSimulationSystem == null || weatherGridManager == null)
            return;

        if (!rainSimulationSystem.CopyActiveVolcanicPrecipitationCells(activeCellsScratch))
            return;

        if (processOverFrames)
        {
            if (processRoutine == null)
                processRoutine = StartCoroutine(ProcessRoutine());
        }
        else
        {
            ProcessImmediate();
        }
    }

    private IEnumerator ProcessRoutine()
    {
        processedBuildingsThisPass.Clear();

        int processed = 0;
        int maxPerFrame = Mathf.Max(1, cellsProcessedPerFrame);

        for (int i = 0; i < activeCellsScratch.Count; i++)
        {
            ApplyEffectAtCell(activeCellsScratch[i]);

            processed++;

            if (processed >= maxPerFrame)
            {
                processed = 0;
                yield return null;
            }
        }

        processedBuildingsThisPass.Clear();
        processRoutine = null;
    }

    private void ProcessImmediate()
    {
        processedBuildingsThisPass.Clear();

        for (int i = 0; i < activeCellsScratch.Count; i++)
            ApplyEffectAtCell(activeCellsScratch[i]);

        processedBuildingsThisPass.Clear();
    }

    private void ApplyEffectAtCell(RainSimulationSystem.VolcanicPrecipitationCell cell)
    {
        if (weatherGridManager == null)
            return;

        if (!weatherGridManager.TryGetBuildingAtCell(
                cell.x,
                cell.y,
                out PlayerBuildingManager.Record record) ||
            record == null ||
            record.instance == null)
        {
            return;
        }

        GameObject buildingObject = record.instance;
        int buildingKey = buildingObject.GetInstanceID();

        if (affectEachBuildingOnlyOncePerPass && processedBuildingsThisPass.Contains(buildingKey))
            return;

        if (affectEachBuildingOnlyOncePerPass)
            processedBuildingsThisPass.Add(buildingKey);

        BuildingVolcanicPrecipitationResistance resistance =
            buildingObject.GetComponent<BuildingVolcanicPrecipitationResistance>();

        if (resistance == null)
            resistance = buildingObject.GetComponentInChildren<BuildingVolcanicPrecipitationResistance>(true);

        if (resistance == null)
            resistance = buildingObject.GetComponentInParent<BuildingVolcanicPrecipitationResistance>();

        if (resistance != null && resistance.IsImmuneTo(cell.kind))
        {
            if (debugLogging || resistance.debugLogging)
            {
                Debug.Log(
                    $"[VolcanicPrecipitationBuildingEffectResolver] {cell.kind} ignored immune building " +
                    $"'{buildingObject.name}' at ({cell.x},{cell.y}).");
            }

            return;
        }

        int baseDamage = GetBaseDamage(cell.kind);
        int severityScaledDamage = Mathf.RoundToInt(baseDamage * Mathf.Clamp01(cell.severity01));

        int finalDamage = resistance != null
            ? resistance.ModifyDamage(cell.kind, severityScaledDamage)
            : Mathf.Max(0, severityScaledDamage);

        float severity01 = baseDamage > 0
            ? Mathf.Clamp01(finalDamage / (float)baseDamage)
            : Mathf.Clamp01(cell.severity01);

        if (resistance != null)
            severity01 = resistance.ModifySecondarySeverity(cell.kind, severity01);

        ApplyCoreBuildingDamage(buildingObject, finalDamage);
        ApplySecondaryEffects(buildingObject, buildingKey, cell.kind, severity01);

        if (debugLogging || (resistance != null && resistance.debugLogging))
        {
            Debug.Log(
                $"[VolcanicPrecipitationBuildingEffectResolver] {cell.kind} affected '{buildingObject.name}' " +
                $"cell=({cell.x},{cell.y}) severity={cell.severity01:0.00} finalDamage={finalDamage}");
        }
    }

    private int GetBaseDamage(RainSimulationSystem.RainVisualKind kind)
    {
        switch (kind)
        {
            case RainSimulationSystem.RainVisualKind.AcidRain:
                return acidRainBuildingDamagePerTurn;

            case RainSimulationSystem.RainVisualKind.AshFall:
                return ashFallBuildingDamagePerTurn;

            default:
                return 0;
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

    private void ApplySecondaryEffects(
        GameObject buildingObject,
        int buildingKey,
        RainSimulationSystem.RainVisualKind kind,
        float severity01)
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

        float shelterMultiplier = kind == RainSimulationSystem.RainVisualKind.AcidRain
            ? acidShelterCasualtyMultiplier
            : ashShelterCasualtyMultiplier;

        float craftingMultiplier = kind == RainSimulationSystem.RainVisualKind.AcidRain
            ? acidCraftingImpactMultiplier
            : ashCraftingImpactMultiplier;

        float productionMultiplier = kind == RainSimulationSystem.RainVisualKind.AcidRain
            ? acidProductionImpactMultiplier
            : ashProductionImpactMultiplier;

        float trainingMultiplier = kind == RainSimulationSystem.RainVisualKind.AcidRain
            ? acidTrainingImpactMultiplier
            : ashTrainingImpactMultiplier;

        float storageMultiplier = kind == RainSimulationSystem.RainVisualKind.AcidRain
            ? acidStorageLossMultiplier
            : ashStorageLossMultiplier;

        ShelterControl shelter = building.GetComponent<ShelterControl>();
        if (shelter == null)
            shelter = building.GetComponentInChildren<ShelterControl>(true);

        if (applyShelterEffects && shelter != null)
            shelter.TryApplyFireCasualties(severity01 * shelterMultiplier, debugLogging);

        CraftingBuildingControl crafting = building.GetComponent<CraftingBuildingControl>();
        if (crafting == null)
            crafting = building.GetComponentInChildren<CraftingBuildingControl>(true);

        if (applyCraftingEffects && crafting != null)
            crafting.TryApplyFireCraftingImpact(severity01 * craftingMultiplier, debugLogging);

        ProductionBuildingControl production = building.GetComponent<ProductionBuildingControl>();
        if (production == null)
            production = building.GetComponentInChildren<ProductionBuildingControl>(true);

        if (applyProductionEffects && production != null)
        {
            production.RegisterFireImpact(
                buildingKey,
                severity01 * productionMultiplier,
                debugLogging);
        }

        KineticWarfareControl training = building.GetComponent<KineticWarfareControl>();
        if (training == null)
            training = building.GetComponentInChildren<KineticWarfareControl>(true);

        if (applyTrainingEffects && training != null)
        {
            training.RegisterFireImpact(
                buildingKey,
                severity01 * trainingMultiplier,
                debugLogging);
        }

        StorageBuildingControl storage = building.GetComponent<StorageBuildingControl>();
        if (storage == null)
            storage = building.GetComponentInChildren<StorageBuildingControl>(true);

        if (applyStorageEffects && storage != null)
        {
            storage.TryApplyFireStorageLoss(
                Mathf.Clamp01(severity01 * storageMultiplier),
                maxStorageResourcesDestroyedPerStep,
                canDestroySpoiledFood,
                debugLogging);
        }
    }
}