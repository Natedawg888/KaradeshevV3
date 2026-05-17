using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(ProductionSphereTileScanner))]
public class ProductionBuildingControl : MonoBehaviour
{
    [Header("Environment Input")]
    public bool useSurroundingEnvironmentTiles = true;

    [Header("Tile Scanning (Sphere)")]
    public ProductionSphereTileScanner tileScanner;

    [Header("Production Plans")]
    public List<string> allowedPlanIDs = new();
    public bool autoBuildPlanCacheOnStart = true;

    [Header("Disease Production Output")]
    public bool productionDiseaseAffectsOutput = true;
    public bool debugDiseaseProductionOutput = false;

    [Header("Production Runtime UI")]
    [Tooltip("The building's normal world canvas (e.g. BuildingCanvas) that holds icons, including timers.")]
    public GameObject productionCanvas;

    [Tooltip("TimerUI that shows turns left in the active production cycle.")]
    public TimerUI productionTimerUI;

    [Tooltip("Separate TimerUI that shows cooldown turns remaining.")]
    public TimerUI cooldownTimerUI;

    [Tooltip("Icon shown when production is paused.")]
    public GameObject productionStoppedIcon;

    private TileControl _tile;

    // runtime cache of allowed plans
    private readonly Dictionary<string, ProductionPlan> _allowedById =
        new(StringComparer.Ordinal);

    // picked extraction tiles per plan ID
    private readonly Dictionary<string, List<EnvironmentControl>> _extractionTilesByPlanId =
        new(StringComparer.Ordinal);

    // --- runtime production state ---
    private ProductionPlan _activePlan;
    private int _turnsLeftInCycle;

    private enum ProductionPauseReason
    {
        None,
        MissingResources,
        OutputBlocked,
        Manual,
        ExtractionTileDestroyed,
        ExtractionTileBarren,
        TornadoImpact,
        FireImpact
    }

    private ProductionPauseReason _pauseReason = ProductionPauseReason.None;

    // If a cycle finished but output could not be deposited yet,
    // keep it pending until inventory has space.
    private bool _waitingToFinalizeCompletedCycle = false;

    private bool _isCoolingDown = false;
    private int _cooldownTurnsLeft = 0;
    private int _completedCyclesSinceCooldown = 0;

    [Header("Population Reservation (runtime)")]
    [SerializeField] private string _populationReservationId;
    [SerializeField] private int _populationReservedAmount;

    private BuildingControl _buildingControl;
    private int _pendingWorkerDeathsForNotification;

    public bool IsProducing =>
        _activePlan != null &&
        _turnsLeftInCycle > 0 &&
        _pauseReason == ProductionPauseReason.None &&
        !IsCoolingDown;

    public bool HasActivePlan => _activePlan != null;
    public ProductionPlan ActivePlan => _activePlan;
    public int TurnsLeftInCycle => _turnsLeftInCycle;
    public bool IsPaused => _pauseReason != ProductionPauseReason.None;
    public bool IsCoolingDown => _activePlan != null && _isCoolingDown && _cooldownTurnsLeft > 0;
    public int CooldownTurnsLeft => _cooldownTurnsLeft;

    [Header("Tornado Production Impact")]
    public bool tornadoCanPauseProduction = true;

    [Range(0f, 1f)] public float tornadoTeenWorkerDeathChance = 0.08f;
    [Range(0f, 1f)] public float tornadoAdultWorkerDeathChance = 0.06f;
    [Range(0f, 1f)] public float tornadoElderWorkerDeathChance = 0.12f;

    [Tooltip("Extra multiplier applied to tornado worker death chance.")]
    [Min(0f)] public float tornadoWorkerDeathChanceMultiplier = 1f;

    [Header("Fire Production Impact")]
    public bool fireCanPauseProduction = true;

    [Range(0f, 1f)] public float fireTeenWorkerDeathChance = 0.05f;
    [Range(0f, 1f)] public float fireAdultWorkerDeathChance = 0.03f;
    [Range(0f, 1f)] public float fireElderWorkerDeathChance = 0.07f;

    [Tooltip("Extra multiplier applied to fire worker death chance.")]
    [Min(0f)] public float fireWorkerDeathChanceMultiplier = 1f;

    [Header("Production Health Wear")]
    public bool productionCanLowerWorkerHealth = true;

    [Range(0f, 1f)] public float productionHealthLossMinPerTurn = 0.003f;
    [Range(0f, 1f)] public float productionHealthLossMaxPerTurn = 0.010f;

    [Tooltip("Extra multiplier applied to production health wear.")]
    [Min(0f)] public float productionHealthLossMultiplier = 1f;

    [Tooltip("Production wear lowers health, but does not kill.")]
    [Range(0f, 1f)] public float minimumHealthAfterProductionWear = 0.05f;

    [Header("Production Output Wear")]
    public bool productionHealthAffectsOutput = true;

    [Tooltip("No output penalty while average worker health is at or above this.")]
    [Range(0f, 1f)] public float productionOutputPenaltyStartsBelowHealth = 0.85f;

    [Tooltip("Lowest output multiplier allowed when workers are badly worn down.")]
    [Range(0f, 1f)] public float productionMinimumOutputMultiplier = 0.55f;

    [Tooltip("Extra multiplier on the size of the output penalty.")]
    [Min(0f)] public float productionOutputPenaltyStrength = 1f;

    [Header("Production Environmental Disease Exposure")]
    public bool productionWeatherDiseaseExposure = true;

    [Tooltip("Internal production building weather disease chance multiplier. Used only when active plan is NOT an external extractor.")]
    [Range(0f, 1f)]
    public float internalProductionWeatherDiseaseChanceMultiplier = 0.75f;

    [Tooltip("Internal production building weather disease exposure multiplier.")]
    [Range(0f, 1f)]
    public float internalProductionWeatherDiseaseExposureMultiplier = 0.85f;

    [Tooltip("0 means let each EnvironmentalDiseaseRisk decide.")]
    [Min(0)]
    public int maxInternalProductionWeatherDiseaseTargetsPerCycle = 0;

    [Tooltip("Extractor production tile disease chance multiplier. Used only when active plan is an external extractor.")]
    [Range(0f, 1f)]
    public float extractorProductionDiseaseChanceMultiplier = 1f;

    [Tooltip("Extractor production tile disease exposure multiplier.")]
    [Range(0f, 1f)]
    public float extractorProductionDiseaseExposureMultiplier = 1f;

    [Tooltip("0 means let each EnvironmentalDiseaseRisk decide.")]
    [Min(0)]
    public int maxExtractorProductionDiseaseTargetsPerTilePerCycle = 0;

    public bool debugProductionWeatherDiseaseExposure = false;

    private BuildingDiseaseExposureSource _buildingDiseaseExposure;
    private readonly List<string> _tmpBuildingDiseaseProductionWorkerIds = new();

    private readonly List<string> _tmpProductionWeatherDiseaseIds = new();

    private bool ShouldAutoResumePause =>
        _pauseReason == ProductionPauseReason.MissingResources ||
        _pauseReason == ProductionPauseReason.OutputBlocked;

    private readonly HashSet<int> _activeTornadoSourceIds = new();
    private readonly HashSet<int> _activeFireSourceIds = new();

    [SerializeField] private float _pendingCompletedCycleOutputMultiplier = 1f;

    private void Reset()
    {
        tileScanner = GetComponent<ProductionSphereTileScanner>();
        _tile = GetComponentInParent<TileControl>();
    }

    private void Awake()
    {
        if (tileScanner == null)
            tileScanner = GetComponent<ProductionSphereTileScanner>();

        if (_tile == null)
            _tile = GetComponentInParent<TileControl>();

        _buildingControl = GetComponent<BuildingControl>() ?? GetComponentInParent<BuildingControl>();
    }

    private void Start()
    {
        if (autoBuildPlanCacheOnStart)
            RebuildAllowedCache();

        _buildingDiseaseExposure = GetComponent<BuildingDiseaseExposureSource>();

        RefreshRuntimeUIAfterLoad();
        RefreshProductionReservationMetadata();
    }

    private void OnEnable()
    {
        PlayerProductionManager.Instance?.Register(this);

        EnvironmentResourceNode.OnNodeBecameBarren -= HandleNodeBecameBarren;
        EnvironmentResourceNode.OnNodeBecameBarren += HandleNodeBecameBarren;

        RefreshProductionReservationMetadata();
    }

    private void OnDisable()
    {
        PlayerProductionManager.Instance?.Unregister(this);
        EnvironmentResourceNode.OnNodeBecameBarren -= HandleNodeBecameBarren;
        ReleasePopulationReservation();
        DestroyRuntimePlanInstance();
    }

    private ProductionPlan CreateRuntimePlanInstance(ProductionPlan source)
    {
        if (source == null)
            return null;

        var runtime = Instantiate(source);
        runtime.name = $"{source.name}_Runtime_{GetInstanceID()}";
        return runtime;
    }

    private void DestroyRuntimePlanInstance()
    {
        if (_activePlan != null)
        {
            Destroy(_activePlan);
            _activePlan = null;
        }
    }

    private string GetReservationOwnerId()
    {
        Saveable saveable = GetComponent<Saveable>();
        if (saveable == null)
            saveable = GetComponentInParent<Saveable>();

        if (saveable != null && !string.IsNullOrWhiteSpace(saveable.uniqueID))
            return saveable.uniqueID;

        return gameObject.GetInstanceID().ToString();
    }

    private void RefreshProductionReservationMetadata()
    {
        if (string.IsNullOrWhiteSpace(_populationReservationId))
            return;

        PlayersPopulationManager.Instance?.UpdateReservationMetadata(
            _populationReservationId,
            PopulationReservationKind.Production,
            GetReservationOwnerId(),
            nameof(ProductionBuildingControl));
    }

    public void RefreshReservationMetadataFromRuntime()
    {
        RefreshProductionReservationMetadata();
    }

    public bool CyclePrevRunningCostSet()
    {
        if (_activePlan == null || !_activePlan.HasAlternateRunningCostSets)
            return false;

        _activePlan.CyclePrevRunningCostSet();
        return true;
    }

    public bool CycleNextRunningCostSet()
    {
        if (_activePlan == null || !_activePlan.HasAlternateRunningCostSets)
            return false;

        _activePlan.CycleNextRunningCostSet();
        return true;
    }

    public bool CyclePrevOutputSet()
    {
        if (_activePlan == null || !_activePlan.HasAlternateOutputSets)
            return false;

        _activePlan.CyclePrevOutputSet();
        return true;
    }

    public bool CycleNextOutputSet()
    {
        if (_activePlan == null || !_activePlan.HasAlternateOutputSets)
            return false;

        _activePlan.CycleNextOutputSet();
        return true;
    }

    // ----------------- ENVIRONMENT INPUT -----------------

    public void RefreshEnvironmentTiles(ProductionPlan plan = null)
    {
        if (tileScanner == null)
            return;

        if (!useSurroundingEnvironmentTiles)
        {
            tileScanner.RefreshFromStarter(null, 1f);
            return;
        }

        GameObject starter = null;

        if (_tile != null)
            starter = _tile.gameObject;
        else
        {
            var env = GetComponent<EnvironmentControl>();
            if (env != null)
                starter = env.gameObject;
        }

        float mult = plan != null ? plan.bfsTilePreferenceMultiplier : 1f;
        tileScanner.RefreshFromStarter(starter, mult);
    }

    public IReadOnlyList<EnvironmentControl> GetDiscoveredEnvironmentTilesInRange()
    {
        if (!useSurroundingEnvironmentTiles || tileScanner == null)
            return Array.Empty<EnvironmentControl>();

        return tileScanner.TrackedEnvironmentTiles;
    }

    public IReadOnlyList<EnvironmentResourceNode> GetResourceNodesInRange()
    {
        if (!useSurroundingEnvironmentTiles || tileScanner == null)
            return Array.Empty<EnvironmentResourceNode>();

        return tileScanner.TrackedNodes;
    }

    public IEnumerable<ResourceSpawnEntry> EnumerateAllResourcesInRange()
    {
        if (!useSurroundingEnvironmentTiles || tileScanner == null)
            yield break;

        foreach (var entry in tileScanner.EnumerateAllResources())
            yield return entry;
    }

    // ----------------- ALLOWED PLAN CACHE -----------------

    public void RebuildAllowedCache()
    {
        _allowedById.Clear();

        var mgr = ProductionPlanManager.Instance;
        if (!mgr) return;

        for (int i = 0; i < allowedPlanIDs.Count; i++)
        {
            var id = allowedPlanIDs[i];
            if (string.IsNullOrWhiteSpace(id)) continue;

            var plan = mgr.GetByID(id);
            if (plan != null)
                _allowedById[id] = plan;
        }
    }

    public IReadOnlyList<ProductionPlan> GetAllowedPlans()
        => _allowedById.Values.ToList();

    public bool IsPlanAllowed(string productionID)
        => !string.IsNullOrWhiteSpace(productionID) && _allowedById.ContainsKey(productionID);

    // ----------------- EXTRACTION TILE STORAGE -----------------

    public void StoreExtractionTilesForPlan(ProductionPlan plan, IEnumerable<EnvironmentControl> tiles)
    {
        if (plan == null) return;

        var key = plan.productionID;
        if (string.IsNullOrWhiteSpace(key)) return;

        var list = tiles != null
            ? tiles.Where(t => t != null).Distinct().ToList()
            : new List<EnvironmentControl>();

        _extractionTilesByPlanId[key] = list;

        //Debug.Log($"[ProductionBuildingControl] Stored {list.Count} extraction tiles for plan {key} on {name}.");
    }

    public IReadOnlyList<EnvironmentControl> GetExtractionTilesForPlan(ProductionPlan plan)
    {
        if (plan == null) return Array.Empty<EnvironmentControl>();
        return GetExtractionTilesForPlan(plan.productionID);
    }

    public IReadOnlyList<EnvironmentControl> GetExtractionTilesForPlan(string productionID)
    {
        if (string.IsNullOrWhiteSpace(productionID))
            return Array.Empty<EnvironmentControl>();

        if (_extractionTilesByPlanId.TryGetValue(productionID, out var list) && list != null)
            return list;

        return Array.Empty<EnvironmentControl>();
    }

    public bool HasExtractionTilesForPlan(ProductionPlan plan)
    {
        if (plan == null) return false;
        return HasExtractionTilesForPlan(plan.productionID);
    }

    public bool HasExtractionTilesForPlan(string productionID)
    {
        return !string.IsNullOrWhiteSpace(productionID) &&
               _extractionTilesByPlanId.TryGetValue(productionID, out var list) &&
               list != null &&
               list.Count > 0;
    }

    // ----------------- PRODUCTION RUNTIME -----------------

    public bool StartProduction(string productionID)
    {
        if (!IsPlanAllowed(productionID)) return false;
        return StartProduction(_allowedById[productionID]);
    }

    public bool StartProduction(ProductionPlan plan)
    {
        if (plan == null)
            return false;

        if (!string.IsNullOrWhiteSpace(plan.productionID) && !IsPlanAllowed(plan.productionID))
            return false;

        var runtimePlan = CreateRuntimePlanInstance(plan);
        if (runtimePlan == null)
            return false;

        if (runtimePlan.isExternalExtractor && !HasExtractionTilesForPlan(runtimePlan))
        {
            //Debug.LogWarning($"[ProductionBuildingControl] Cannot start {runtimePlan.productionID} on {name}: no extraction tiles stored.");
            Destroy(runtimePlan);
            return false;
        }

        if (_activePlan != null)
            StopProduction();

        _activePlan = runtimePlan;
        _turnsLeftInCycle = 0;
        _pauseReason = ProductionPauseReason.None;
        _waitingToFinalizeCompletedCycle = false;
        _isCoolingDown = false;
        _cooldownTurnsLeft = 0;
        _completedCyclesSinceCooldown = 0;
        _pendingCompletedCycleOutputMultiplier = 1f;
        _pendingWorkerDeathsForNotification = 0;

        if (!EnsurePopulationReservation(_activePlan))
        {
            //Debug.LogWarning($"[ProductionBuildingControl] Failed to reserve population for {_activePlan.productionID} on {name}.");
            StopProduction();
            return false;
        }

        if (PlayerProductionManager.Instance == null)
        {
            //Debug.LogWarning("[ProductionBuildingControl] No PlayerProductionManager found.");
            StopProduction();
            return false;
        }

        if (!PlayerProductionManager.Instance.TryConsumeRunningCostsForNextCycle(_activePlan))
        {
            PauseForLackOfResources();
            return false;
        }

        _turnsLeftInCycle = Mathf.Max(1, _activePlan.requiredTurnsPerCycle);
        ShowProductionTimer(_turnsLeftInCycle);

        //Debug.Log($"[ProductionBuildingControl] Started production {_activePlan.productionID} on {name}.");
        return true;
    }

    public void StopProduction()
    {
        ReleasePopulationReservation();

        _activeTornadoSourceIds.Clear();
        _activeFireSourceIds.Clear();

        _turnsLeftInCycle = 0;
        _pauseReason = ProductionPauseReason.None;
        _waitingToFinalizeCompletedCycle = false;
        _isCoolingDown = false;
        _cooldownTurnsLeft = 0;
        _completedCyclesSinceCooldown = 0;
        _pendingCompletedCycleOutputMultiplier = 1f;
        _pendingWorkerDeathsForNotification = 0;

        HideRuntimeUI();
        DestroyRuntimePlanInstance();
    }

    public void CancelCurrentPlan()
    {
        StopProduction();
    }

    public void PauseProductionManual()
    {
        PauseInternal(ProductionPauseReason.Manual, "manual pause", keepCurrentTurns: true);
    }

    public bool ResumeProductionManual()
    {
        return TryResumeProductionInternal(allowManualResumeAnyPause: true);
    }

    public void PauseForLackOfResources()
    {
        PauseInternal(ProductionPauseReason.MissingResources, "missing running costs");
    }

    public void PauseForOutputBlocked()
    {
        PauseInternal(ProductionPauseReason.OutputBlocked, "output blocked / inventory full");
    }

    private void PauseInternal(
        ProductionPauseReason reason,
        string logReason,
        bool keepCurrentTurns = false)
    {
        if (_activePlan == null)
            return;

        bool wasFresh = _pauseReason == ProductionPauseReason.None;
        _pauseReason = reason;

        if (!keepCurrentTurns)
            _turnsLeftInCycle = 0;

        UnbusyButKeepReservation();

        HideAllTimers();

        if (productionStoppedIcon != null)
            productionStoppedIcon.SetActive(true);

        //Debug.Log($"[ProductionBuildingControl] Production paused on {name}: {logReason}");

        if (wasFresh && reason == ProductionPauseReason.MissingResources)
            PostProductionPausedNotification(NotificationType.ProductionPausedLackOfResources, false);
    }

    public void TickProductionTurn()
    {
        RefreshProductionReservationMetadata();

        if (_pauseReason == ProductionPauseReason.TornadoImpact ||
                _pauseReason == ProductionPauseReason.FireImpact)
            return;

        if (_activePlan != null && _activePlan.isExternalExtractor)
        {
            if (!CheckExtractionTilesStillExist())
                return;
        }

        if (!ReconcileReservedProductionPopulation())
            return;

        if (IsCoolingDown)
        {
            TickCooldownTurn();
            return;
        }

        if (IsPaused)
        {
            TryAutoResumeIfPossible();
            return;
        }

        if (!IsProducing || _activePlan == null)
            return;

        TryApplyProductionHealthWear(debugLogging: false);

        _turnsLeftInCycle = Mathf.Max(0, _turnsLeftInCycle - 1);

        if (productionTimerUI != null)
            productionTimerUI.UpdateTimer(_turnsLeftInCycle);

        if (_turnsLeftInCycle == 0)
            HandleCycleComplete();
    }

    private void HandleCycleComplete()
    {
        if (_activePlan == null)
            return;

        var plan = _activePlan;
        var ppm = PlayerProductionManager.Instance;

        if (ppm == null)
        {
            StopProduction();
            return;
        }

        _pendingCompletedCycleOutputMultiplier = GetProductionOutputMultiplierForCurrentReservation();

        TryApplyProductionEnvironmentalDiseaseForCompletedCycle(plan, debugLogging: false);
        TryApplyBuildingDiseaseToProductionWorkers();

        if (!ppm.CanFinalizeCompletedCycle(this, plan))
        {
            _waitingToFinalizeCompletedCycle = true;
            PauseForOutputBlocked();
            return;
        }

        bool completionHandled = ppm.ApplyCompletedCycleResults(this, plan);
        if (!completionHandled)
        {
            StopProduction();
            return;
        }

        float completedMultiplier = _pendingCompletedCycleOutputMultiplier;
        _pendingCompletedCycleOutputMultiplier = 1f;
        PostProductionCompletedNotification(completedMultiplier);

        if (_activePlan == null || IsPaused)
            return;

        _completedCyclesSinceCooldown++;

        if (plan.UsesCycleCooldown &&
            _completedCyclesSinceCooldown >= plan.GetCyclesBeforeCooldown())
        {
            BeginCooldown(plan);
            return;
        }

        if (!TryStartImmediateNextCycle(plan))
        {
            if (!IsPaused)
                StopProduction();
        }
    }

    private void BeginCooldown(ProductionPlan plan)
    {
        if (plan == null || !plan.UsesCycleCooldown)
            return;

        _isCoolingDown = true;
        _cooldownTurnsLeft = Mathf.Max(1, plan.GetCooldownTurns());
        _turnsLeftInCycle = 0;
        _completedCyclesSinceCooldown = 0;
        _pauseReason = ProductionPauseReason.None;
        _waitingToFinalizeCompletedCycle = false;

        UnbusyButKeepReservation();

        ShowCooldownTimer(_cooldownTurnsLeft);

        //Debug.Log($"[ProductionBuildingControl] Cooldown started for {plan.productionID} on {name}. Turns: {_cooldownTurnsLeft}");
    }

    private void TickCooldownTurn()
    {
        if (!IsCoolingDown || _activePlan == null)
            return;

        if (!ReconcileReservedProductionPopulation())
            return;

        _cooldownTurnsLeft = Mathf.Max(0, _cooldownTurnsLeft - 1);

        if (cooldownTimerUI != null)
            cooldownTimerUI.UpdateTimer(_cooldownTurnsLeft);

        if (_cooldownTurnsLeft > 0)
            return;

        _isCoolingDown = false;
        _cooldownTurnsLeft = 0;

        if (!TryStartNextCycleAfterCooldown(_activePlan))
        {
            if (!IsPaused)
                StopProduction();
        }
    }

    private bool TryStartNextCycleAfterCooldown(ProductionPlan plan)
    {
        if (plan == null)
            return false;

        if (!EnsurePopulationReservation(plan))
        {
            //Debug.LogWarning($"[ProductionBuildingControl] Could not re-reserve population after cooldown for {plan.productionID} on {name}.");
            return false;
        }

        bool canContinue =
            PlayerProductionManager.Instance != null &&
            PlayerProductionManager.Instance.TryConsumeRunningCostsForNextCycle(plan);

        if (!canContinue)
        {
            PauseForLackOfResources();
            return false;
        }

        _pauseReason = ProductionPauseReason.None;
        _turnsLeftInCycle = Mathf.Max(1, plan.requiredTurnsPerCycle);
        ShowProductionTimer(_turnsLeftInCycle);

        //Debug.Log($"[ProductionBuildingControl] Cooldown ended. New cycle started for {plan.productionID} on {name}.");
        return true;
    }

    private bool TryStartImmediateNextCycle(ProductionPlan plan)
    {
        if (plan == null)
            return false;

        if (!EnsurePopulationReservation(plan))
        {
            //Debug.LogWarning($"[ProductionBuildingControl] Could not reserve population for next cycle of {plan.productionID} on {name}.");
            return false;
        }

        bool canContinue =
            PlayerProductionManager.Instance != null &&
            PlayerProductionManager.Instance.TryConsumeRunningCostsForNextCycle(plan);

        if (!canContinue)
        {
            PauseForLackOfResources();
            return false;
        }

        _pauseReason = ProductionPauseReason.None;
        _turnsLeftInCycle = Mathf.Max(1, plan.requiredTurnsPerCycle);
        ShowProductionTimer(_turnsLeftInCycle);

        //Debug.Log($"[ProductionBuildingControl] New cycle started for {plan.productionID} on {name}.");
        return true;
    }

    private bool TryAutoResumeIfPossible()
    {
        return TryResumeProductionInternal(allowManualResumeAnyPause: false);
    }

    private bool TryResumeProductionInternal(bool allowManualResumeAnyPause)
    {
        if (_activePlan == null || IsCoolingDown)
            return false;

        if (!ReconcileReservedProductionPopulation())
            return false;

        if (!allowManualResumeAnyPause && !ShouldAutoResumePause)
            return false;

        var ppm = PlayerProductionManager.Instance;
        if (ppm == null)
            return false;

        if (_waitingToFinalizeCompletedCycle)
        {
            if (!ppm.CanFinalizeCompletedCycle(this, _activePlan))
                return false;

            _pauseReason = ProductionPauseReason.None;
            _waitingToFinalizeCompletedCycle = false;

            bool completionHandled = ppm.ApplyCompletedCycleResults(this, _activePlan);
            if (!completionHandled)
            {
                StopProduction();
                return false;
            }

            PostProductionCompletedNotification(1f);
            _completedCyclesSinceCooldown++;

            if (_activePlan == null || IsPaused)
                return false;

            if (_activePlan.UsesCycleCooldown &&
                _completedCyclesSinceCooldown >= _activePlan.GetCyclesBeforeCooldown())
            {
                BeginCooldown(_activePlan);
                return true;
            }

            return TryStartImmediateNextCycle(_activePlan);
        }

        if (_turnsLeftInCycle > 0)
        {
            if (!EnsurePopulationReservation(_activePlan))
                return false;

            _pauseReason = ProductionPauseReason.None;
            ShowProductionTimer(_turnsLeftInCycle);
            return true;
        }

        return TryStartImmediateNextCycle(_activePlan);
    }

    // ----------------- POPULATION -----------------

    private bool EnsurePopulationReservation(ProductionPlan plan)
    {
        if (plan == null)
            return false;

        int required = Mathf.Max(0, plan.requiredPopulation);
        if (required <= 0)
            return true;

        var familySim = PlayerFamilySimulationManager.Instance;
        if (familySim == null)
            return true;

        if (!string.IsNullOrEmpty(_populationReservationId) &&
            _populationReservedAmount == required &&
            familySim.IsProductionReservationStillValid(_populationReservationId, required))
        {
            RefreshProductionReservationMetadata();
            TryRebusyReservation(familySim, _populationReservationId);
            return true;
        }

        if (!string.IsNullOrEmpty(_populationReservationId))
        {
            familySim.ReleaseBusyIndividuals(_populationReservationId, null);
            _populationReservationId = null;
            _populationReservedAmount = 0;
        }

        if (!familySim.TryReservePopulationForProduction(
                required,
                out var picked,
                out _populationReservationId))
        {
            _populationReservationId = null;
            _populationReservedAmount = 0;
            return false;
        }

        _populationReservedAmount = required;
        RefreshProductionReservationMetadata();
        RefreshPopulationUI();
        return true;
    }

    private void UnbusyButKeepReservation()
    {
        if (string.IsNullOrEmpty(_populationReservationId))
            return;

        var familySim = PlayerFamilySimulationManager.Instance;
        if (familySim == null)
            return;

        familySim.UnbusyReservationOnly(_populationReservationId);
        RefreshProductionReservationMetadata();
        RefreshPopulationUI();
    }

    private void TryRebusyReservation(PlayerFamilySimulationManager familySim, string reservationId)
    {
        if (familySim == null || string.IsNullOrEmpty(reservationId))
            return;

        familySim.RebusyReservation(reservationId);
        RefreshProductionReservationMetadata();
        RefreshPopulationUI();
    }

    private void ReleasePopulationReservation()
    {
        if (string.IsNullOrEmpty(_populationReservationId))
            return;

        var familySim = PlayerFamilySimulationManager.Instance;
        if (familySim != null)
            familySim.ReleaseBusyIndividuals(_populationReservationId, null);

        _populationReservationId = null;
        _populationReservedAmount = 0;
        RefreshPopulationUI();
    }

    private bool ReconcileReservedProductionPopulation()
    {
        if (_activePlan == null)
            return true;

        int required = Mathf.Max(0, _activePlan.requiredPopulation);
        if (required <= 0)
            return true;

        var familySim = PlayerFamilySimulationManager.Instance;
        var pop = PlayersPopulationManager.Instance;

        if (familySim == null || pop == null)
            return false;

        if (familySim.IsProductionReservationStillValid(_populationReservationId, required))
        {
            RefreshProductionReservationMetadata();
            return true;
        }

        bool replacedInvalid = TryBackfillInvalidReservedWorkers(pop, familySim);
        bool toppedUp = pop.TryTopUpReservationToRequiredCount(_populationReservationId, required);

        if ((replacedInvalid || toppedUp) &&
            familySim.IsProductionReservationStillValid(_populationReservationId, required))
        {
            RefreshProductionReservationMetadata();
            return true;
        }

        //Debug.Log($"[ProductionBuildingControl] Cancelling {_activePlan.productionID} on {name} because reserved workers could not be backfilled.");
        PostProductionPausedNotification(NotificationType.ProductionPausedLackOfWorkers, _pendingWorkerDeathsForNotification > 0);
        StopProduction();
        RefreshPopulationUI();
        return false;
    }

    private bool TryBackfillInvalidReservedWorkers(
        PlayersPopulationManager pop,
        PlayerFamilySimulationManager familySim)
    {
        if (string.IsNullOrEmpty(_populationReservationId))
            return false;

        if (!pop.TryGetReservedIndividualIds(_populationReservationId, out var reservedIds) ||
            reservedIds == null || reservedIds.Count == 0)
        {
            return false;
        }

        bool allReplaced = true;
        var snapshot = reservedIds.ToList();
        var people = familySim.GetIndividuals();

        for (int i = 0; i < snapshot.Count; i++)
        {
            string id = snapshot[i];
            Individual person = null;

            for (int j = 0; j < people.Count; j++)
            {
                var candidate = people[j];
                if (candidate != null && candidate.Id == id)
                {
                    person = candidate;
                    break;
                }
            }

            bool shouldReplace =
                person == null ||
                !person.IsAlive ||
                (person.AggregatedAgeGroup != AgeGroup.Teen &&
                 person.AggregatedAgeGroup != AgeGroup.Adult);

            if (!shouldReplace)
                continue;

            if (person == null || !person.IsAlive)
            {
                allReplaced = false;
                continue;
            }

            bool replaced;
            if (!pop.TryDetachIndividualFromExistingReservations(person.Id, out replaced) || !replaced)
                allReplaced = false;
        }

        return allReplaced;
    }

    // ----------------- EXTRACTION VALIDATION -----------------

    private bool CheckExtractionTilesStillExist()
    {
        if (_activePlan == null || !_activePlan.isExternalExtractor)
            return true;

        string key = _activePlan.productionID;
        if (string.IsNullOrWhiteSpace(key))
            return true;

        if (!_extractionTilesByPlanId.TryGetValue(key, out var list) || list == null || list.Count == 0)
        {
            PauseInternal(ProductionPauseReason.ExtractionTileDestroyed, "no extraction tiles selected");
            return false;
        }

        var valid = list.Where(t => t != null).Distinct().ToList();

        if (valid.Count != list.Count)
            _extractionTilesByPlanId[key] = valid;

        if (valid.Count == 0)
        {
            PauseInternal(ProductionPauseReason.ExtractionTileDestroyed, "selected extraction tiles were destroyed");
            return false;
        }

        return true;
    }

    private void HandleNodeBecameBarren(EnvironmentResourceNode node)
    {
        if (node == null || _activePlan == null || !_activePlan.isExternalExtractor)
            return;

        var env = node.GetComponent<EnvironmentControl>();
        if (env == null)
            return;

        var key = _activePlan.productionID;
        if (!_extractionTilesByPlanId.TryGetValue(key, out var list) || list == null)
            return;

        if (list.Contains(env))
            PauseInternal(ProductionPauseReason.ExtractionTileBarren, "selected extraction tile became barren");
    }

    // ----------------- UI -----------------

    private void ShowProductionTimer(int turns)
    {
        if (productionCanvas != null && !productionCanvas.activeSelf)
            productionCanvas.SetActive(true);

        if (productionStoppedIcon != null)
            productionStoppedIcon.SetActive(false);

        if (cooldownTimerUI != null)
            cooldownTimerUI.gameObject.SetActive(false);

        if (productionTimerUI != null)
        {
            productionTimerUI.gameObject.SetActive(true);
            productionTimerUI.Initialize(Mathf.Max(1, turns));
        }
    }

    private void ShowCooldownTimer(int turns)
    {
        if (productionCanvas != null && !productionCanvas.activeSelf)
            productionCanvas.SetActive(true);

        if (productionStoppedIcon != null)
            productionStoppedIcon.SetActive(false);

        if (productionTimerUI != null)
            productionTimerUI.gameObject.SetActive(false);

        if (cooldownTimerUI != null)
        {
            cooldownTimerUI.gameObject.SetActive(true);
            cooldownTimerUI.Initialize(Mathf.Max(1, turns));
        }
    }

    private void HideAllTimers()
    {
        if (productionTimerUI != null)
            productionTimerUI.gameObject.SetActive(false);

        if (cooldownTimerUI != null)
            cooldownTimerUI.gameObject.SetActive(false);
    }

    private void HideRuntimeUI()
    {
        HideAllTimers();

        if (productionStoppedIcon != null)
            productionStoppedIcon.SetActive(false);
    }

    private bool HasValidReservedProductionPopulation()
    {
        if (_activePlan == null)
            return true;

        int required = Mathf.Max(0, _activePlan.requiredPopulation);
        if (required <= 0)
            return true;

        var familySim = PlayerFamilySimulationManager.Instance;
        if (familySim == null)
            return false;

        return familySim.IsProductionReservationStillValid(_populationReservationId, required);
    }

    private bool CancelIfReservedPopulationBecameIneligible()
    {
        if (_activePlan == null)
            return false;

        if (HasValidReservedProductionPopulation())
            return false;

        //Debug.Log($"[ProductionBuildingControl] Cancelling {_activePlan.productionID} on {name} because reserved workers became ineligible.");
        StopProduction();
        return true;
    }

    public int ApplyFatalitiesForCompletedCycle()
    {
        if (_activePlan == null)
            return 0;

        if (string.IsNullOrEmpty(_populationReservationId))
            return 0;

        int usedWorkers = Mathf.Max(0, _populationReservedAmount);
        if (usedWorkers <= 0)
            return 0;

        int deathsToApply = _activePlan.RollFatalitiesForCompletedCycle(usedWorkers);
        if (deathsToApply <= 0)
            return 0;

        var pop = PlayersPopulationManager.Instance;
        if (pop == null)
            return 0;

        int actualDeaths = pop.ApplyPenaltyFromReservation(_populationReservationId, deathsToApply);

        if (actualDeaths > 0)
        {
            _pendingWorkerDeathsForNotification += actualDeaths;

            //Debug.Log(
                //$"[ProductionBuildingControl] {name} lost {actualDeaths} worker(s) " +
                //$"from production plan {_activePlan.productionID}."
            //);

            ReconcileReservedProductionPopulation();
        }

        return actualDeaths;
    }

    public ProductionBuildingRuntimeSaveData CaptureRuntimeSaveData(string buildingSaveableID)
    {
        ProductionBuildingRuntimeSaveData data = new ProductionBuildingRuntimeSaveData
        {
            buildingSaveableID = buildingSaveableID,
            activePlanID = _activePlan != null ? _activePlan.productionID : null,
            activeRunningCostSetIndex = _activePlan != null ? _activePlan.activeRunningCostSetIndex : -1,
            activeOutputSetIndex = _activePlan != null ? _activePlan.activeOutputSetIndex : -1,
            turnsLeftInCycle = _turnsLeftInCycle,

            pauseReason = _pauseReason.ToString(),
            waitingToFinalizeCompletedCycle = _waitingToFinalizeCompletedCycle,

            isCoolingDown = _isCoolingDown,
            cooldownTurnsLeft = _cooldownTurnsLeft,
            completedCyclesSinceCooldown = _completedCyclesSinceCooldown,

            populationReservationId = _populationReservationId,
            populationReservedAmount = _populationReservedAmount
        };

        foreach (var kv in _extractionTilesByPlanId)
        {
            if (string.IsNullOrWhiteSpace(kv.Key))
                continue;

            ProductionExtractionTileSetSaveData set = new ProductionExtractionTileSetSaveData
            {
                productionID = kv.Key
            };

            List<EnvironmentControl> envs = kv.Value;
            if (envs != null)
            {
                for (int i = 0; i < envs.Count; i++)
                {
                    EnvironmentControl env = envs[i];
                    if (env == null || string.IsNullOrWhiteSpace(env.EnvironmentID))
                        continue;

                    set.environmentIDs.Add(env.EnvironmentID);
                }
            }

            data.extractionTileSets.Add(set);
        }

        return data;
    }

    public void ApplyRuntimeSaveData(
        ProductionBuildingRuntimeSaveData data,
        Func<string, ProductionPlan> planResolver,
        Func<string, EnvironmentControl> environmentResolver)
    {
        if (data == null)
            return;

        StopProduction();
        _extractionTilesByPlanId.Clear();

        if (data.extractionTileSets != null)
        {
            for (int i = 0; i < data.extractionTileSets.Count; i++)
            {
                ProductionExtractionTileSetSaveData set = data.extractionTileSets[i];
                if (set == null || string.IsNullOrWhiteSpace(set.productionID))
                    continue;

                List<EnvironmentControl> envs = new List<EnvironmentControl>();

                if (set.environmentIDs != null)
                {
                    for (int j = 0; j < set.environmentIDs.Count; j++)
                    {
                        string envId = set.environmentIDs[j];
                        if (string.IsNullOrWhiteSpace(envId))
                            continue;

                        EnvironmentControl env = environmentResolver != null ? environmentResolver(envId) : null;
                        if (env != null && !envs.Contains(env))
                            envs.Add(env);
                    }
                }

                _extractionTilesByPlanId[set.productionID] = envs;
            }
        }

        if (string.IsNullOrWhiteSpace(data.activePlanID))
        {
            HideRuntimeUI();
            return;
        }

        ProductionPlan sourcePlan = planResolver != null ? planResolver(data.activePlanID) : null;
        if (sourcePlan == null)
        {
            //Debug.LogWarning($"[ProductionBuildingControl] Could not resolve production plan '{data.activePlanID}' while loading on {name}.");
            HideRuntimeUI();
            return;
        }

        _activePlan = CreateRuntimePlanInstance(sourcePlan);
        if (_activePlan == null)
        {
            HideRuntimeUI();
            return;
        }

        _activePlan.SetActiveRunningCostSet(data.activeRunningCostSetIndex);
        _activePlan.SetActiveOutputSet(data.activeOutputSetIndex);

        _turnsLeftInCycle = Mathf.Max(0, data.turnsLeftInCycle);

        if (!Enum.TryParse(data.pauseReason, out _pauseReason))
            _pauseReason = ProductionPauseReason.None;

        _waitingToFinalizeCompletedCycle = data.waitingToFinalizeCompletedCycle;

        _isCoolingDown = data.isCoolingDown;
        _cooldownTurnsLeft = Mathf.Max(0, data.cooldownTurnsLeft);
        _completedCyclesSinceCooldown = Mathf.Max(0, data.completedCyclesSinceCooldown);

        _populationReservationId = data.populationReservationId;
        _populationReservedAmount = Mathf.Max(0, data.populationReservedAmount);

        RefreshProductionReservationMetadata();
        RefreshRuntimeUIAfterLoad();

        var familySim = PlayerFamilySimulationManager.Instance;
        if (familySim != null && !string.IsNullOrEmpty(_populationReservationId))
        {
            if (_isCoolingDown || _pauseReason != ProductionPauseReason.None)
                UnbusyButKeepReservation();
            else
                TryRebusyReservation(familySim, _populationReservationId);
        }
    }

    private void RefreshRuntimeUIAfterLoad()
    {
        if (_activePlan == null)
        {
            HideRuntimeUI();
            return;
        }

        if (productionCanvas != null && !productionCanvas.activeSelf)
            productionCanvas.SetActive(true);

        if (_isCoolingDown && _cooldownTurnsLeft > 0)
        {
            ShowCooldownTimer(_cooldownTurnsLeft);
            if (cooldownTimerUI != null)
                cooldownTimerUI.UpdateTimer(_cooldownTurnsLeft);
            return;
        }

        if (_pauseReason != ProductionPauseReason.None)
        {
            HideAllTimers();

            if (productionStoppedIcon != null)
                productionStoppedIcon.SetActive(true);

            return;
        }

        if (_turnsLeftInCycle > 0)
        {
            ShowProductionTimer(_turnsLeftInCycle);
            if (productionTimerUI != null)
                productionTimerUI.UpdateTimer(_turnsLeftInCycle);
            return;
        }

        HideRuntimeUI();
    }

    private void RefreshPopulationUI()
    {
        PlayersPopulationManager.Instance?.ForceSyncUI();
    }

    public void HandlePopulationAvailabilityChanged()
    {
        RefreshPopulationUI();

        if (_activePlan == null)
            return;

        if (_pauseReason == ProductionPauseReason.TornadoImpact ||
                _pauseReason == ProductionPauseReason.FireImpact)
            return;

        ReconcileReservedProductionPopulation();
        RefreshPopulationUI();
    }

    public struct TornadoProductionImpact
    {
        public bool paused;
        public int workersRolled;
        public int workersKilled;
    }

    public struct FireProductionImpact
    {
        public bool paused;
        public int workersRolled;
        public int workersKilled;
    }

    public bool IsPausedForTornadoImpact => _pauseReason == ProductionPauseReason.TornadoImpact;
    public bool IsPausedForFireImpact => _pauseReason == ProductionPauseReason.FireImpact;

    private float GetFireWorkerDeathChance(AgeGroup ageGroup)
    {
        return ageGroup switch
        {
            AgeGroup.Teen => fireTeenWorkerDeathChance,
            AgeGroup.Adult => fireAdultWorkerDeathChance,
            AgeGroup.Elder => fireElderWorkerDeathChance,
            _ => 0f
        };
    }

    private float GetTornadoWorkerDeathChance(AgeGroup ageGroup)
    {
        return ageGroup switch
        {
            AgeGroup.Teen => tornadoTeenWorkerDeathChance,
            AgeGroup.Adult => tornadoAdultWorkerDeathChance,
            AgeGroup.Elder => tornadoElderWorkerDeathChance,
            _ => 0f
        };
    }

    private Individual FindIndividualById(string individualId)
    {
        if (string.IsNullOrWhiteSpace(individualId))
            return null;

        var familySim = PlayerFamilySimulationManager.Instance;
        if (familySim == null)
            return null;

        var people = familySim.GetIndividuals();
        if (people == null)
            return null;

        for (int i = 0; i < people.Count; i++)
        {
            var person = people[i];
            if (person != null && person.Id == individualId)
                return person;
        }

        return null;
    }

    public TornadoProductionImpact RegisterTornadoImpact(
    int tornadoSourceId,
    float externalChanceMultiplier = 1f,
    bool debugLogging = false)
    {
        TornadoProductionImpact result = default;

        if (!tornadoCanPauseProduction || _activePlan == null)
            return result;

        // Same tornado still overlapping this building: do not reroll worker deaths again.
        bool firstContactFromThisTornado = _activeTornadoSourceIds.Add(tornadoSourceId);
        if (!firstContactFromThisTornado)
            return result;

        PauseInternal(ProductionPauseReason.TornadoImpact, "tornado impact", keepCurrentTurns: true);
        result.paused = true;

        if (string.IsNullOrWhiteSpace(_populationReservationId) || _populationReservedAmount <= 0)
            return result;

        var pop = PlayersPopulationManager.Instance;
        var familySim = PlayerFamilySimulationManager.Instance;

        if (pop == null || familySim == null)
            return result;

        if (!pop.TryGetReservedIndividualIds(_populationReservationId, out var reservedIds) ||
            reservedIds == null || reservedIds.Count == 0)
        {
            return result;
        }

        HashSet<string> killIds = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < reservedIds.Count; i++)
        {
            string individualId = reservedIds[i];
            Individual person = FindIndividualById(individualId);

            if (person == null || !person.IsAlive)
                continue;

            result.workersRolled++;

            float chance = GetTornadoWorkerDeathChance(person.AggregatedAgeGroup);
            if (chance <= 0f)
                continue;

            chance *= tornadoWorkerDeathChanceMultiplier;
            chance *= Mathf.Max(0f, externalChanceMultiplier);
            chance = Mathf.Clamp01(chance);

            if (UnityEngine.Random.value <= chance)
                killIds.Add(person.Id);
        }

        if (killIds.Count > 0)
            familySim.TryKillIndividualsById(killIds, out result.workersKilled);

        if (debugLogging)
        {
            //Debug.Log(
                //$"[ProductionBuildingControl] Tornado impacted production on '{name}' | " +
                //$"Plan={(_activePlan != null ? _activePlan.productionID : "None")} | " +
                //$"WorkersRolled={result.workersRolled} | " +
                //$"WorkersKilled={result.workersKilled} | " +
                //$"Paused={result.paused}"
            //);
        }

        return result;
    }

    public bool NotifyTornadoCleared(int tornadoSourceId, bool debugLogging = false)
    {
        if (!_activeTornadoSourceIds.Remove(tornadoSourceId))
            return false;

        if (_activeFireSourceIds.Count > 0)
            return false;

        if (_pauseReason != ProductionPauseReason.TornadoImpact)
            return false;

        return TryResumeAfterTornadoCleared(debugLogging);
    }

    private bool TryResumeAfterTornadoCleared(bool debugLogging = false)
    {
        if (_activePlan == null)
        {
            _pauseReason = ProductionPauseReason.None;
            HideRuntimeUI();
            return false;
        }

        // Rebuild/refresh the worker reservation if workers died during the tornado.
        if (!EnsurePopulationReservation(_activePlan))
        {
            if (debugLogging)
            {
                //Debug.Log(
                    //$"[ProductionBuildingControl] Tornado cleared at '{name}', " +
                    //$"but production could not resume because workers were unavailable."
                //);
            }

            // Stay paused until population becomes available later.
            return false;
        }

        _pauseReason = ProductionPauseReason.None;

        if (_isCoolingDown && _cooldownTurnsLeft > 0)
        {
            ShowCooldownTimer(_cooldownTurnsLeft);
            return true;
        }

        if (_turnsLeftInCycle > 0)
        {
            ShowProductionTimer(_turnsLeftInCycle);

            if (debugLogging) {}
                //Debug.Log($"[ProductionBuildingControl] Resumed paused production cycle on '{name}' after tornado cleared.");

            return true;
        }

        bool resumed = TryResumeProductionInternal(allowManualResumeAnyPause: true);

        if (debugLogging && resumed) {}
            //Debug.Log($"[ProductionBuildingControl] Resumed production on '{name}' after tornado cleared.");

        return resumed;
    }

    public FireProductionImpact RegisterFireImpact(
    int fireSourceId,
    float externalChanceMultiplier = 1f,
    bool debugLogging = false)
    {
        FireProductionImpact result = default;

        if (!fireCanPauseProduction || _activePlan == null)
            return result;

        // Same fire still affecting this building: do not reroll worker deaths again.
        bool firstContactFromThisFire = _activeFireSourceIds.Add(fireSourceId);
        if (!firstContactFromThisFire)
            return result;

        PauseInternal(ProductionPauseReason.FireImpact, "fire impact", keepCurrentTurns: true);
        result.paused = true;

        if (string.IsNullOrWhiteSpace(_populationReservationId) || _populationReservedAmount <= 0)
            return result;

        var pop = PlayersPopulationManager.Instance;
        var familySim = PlayerFamilySimulationManager.Instance;

        if (pop == null || familySim == null)
            return result;

        if (!pop.TryGetReservedIndividualIds(_populationReservationId, out var reservedIds) ||
            reservedIds == null || reservedIds.Count == 0)
        {
            return result;
        }

        HashSet<string> killIds = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < reservedIds.Count; i++)
        {
            string individualId = reservedIds[i];
            Individual person = FindIndividualById(individualId);

            if (person == null || !person.IsAlive)
                continue;

            result.workersRolled++;

            float chance = GetFireWorkerDeathChance(person.AggregatedAgeGroup);
            if (chance <= 0f)
                continue;

            chance *= fireWorkerDeathChanceMultiplier;
            chance *= Mathf.Max(0f, externalChanceMultiplier);
            chance = Mathf.Clamp01(chance);

            if (UnityEngine.Random.value <= chance)
                killIds.Add(person.Id);
        }

        if (killIds.Count > 0)
            familySim.TryKillIndividualsById(killIds, out result.workersKilled);

        if (debugLogging)
        {
            //Debug.Log(
                //$"[ProductionBuildingControl] Fire impacted production on '{name}' | " +
                //$"Plan={(_activePlan != null ? _activePlan.productionID : "None")} | " +
                //$"WorkersRolled={result.workersRolled} | " +
                //$"WorkersKilled={result.workersKilled} | " +
                //$"Paused={result.paused}"
            //);
        }

        return result;
    }

    public bool NotifyFireCleared(int fireSourceId, bool debugLogging = false)
    {
        if (!_activeFireSourceIds.Remove(fireSourceId))
            return false;

        // Another fire source is still affecting this building.
        if (_activeFireSourceIds.Count > 0)
            return false;

        // Tornado still active on this building, so do not resume yet.
        if (_activeTornadoSourceIds.Count > 0)
            return false;

        if (_pauseReason != ProductionPauseReason.FireImpact)
            return false;

        return TryResumeAfterFireCleared(debugLogging);
    }

    private bool TryResumeAfterFireCleared(bool debugLogging = false)
    {
        if (_activePlan == null)
        {
            _pauseReason = ProductionPauseReason.None;
            HideRuntimeUI();
            return false;
        }

        // Rebuild/refresh the worker reservation if workers died during the fire.
        if (!EnsurePopulationReservation(_activePlan))
        {
            if (debugLogging)
            {
                //Debug.Log(
                    //$"[ProductionBuildingControl] Fire cleared at '{name}', " +
                    //$"but production could not resume because workers were unavailable."
                //);
            }

            // Stay paused until population becomes available later.
            return false;
        }

        _pauseReason = ProductionPauseReason.None;

        if (_isCoolingDown && _cooldownTurnsLeft > 0)
        {
            ShowCooldownTimer(_cooldownTurnsLeft);
            return true;
        }

        if (_turnsLeftInCycle > 0)
        {
            ShowProductionTimer(_turnsLeftInCycle);

            if (debugLogging) {}
                //Debug.Log($"[ProductionBuildingControl] Resumed paused production cycle on '{name}' after fire cleared.");

            return true;
        }

        bool resumed = TryResumeProductionInternal(allowManualResumeAnyPause: true);

        if (debugLogging && resumed) {}
            //Debug.Log($"[ProductionBuildingControl] Resumed production on '{name}' after fire cleared.");

        return resumed;
    }

    private float GetProductionWorkerAgeResistance01(Individual person)
    {
        if (person == null)
            return 0f;

        if (PlayerHealthRulebook.Instance != null)
            return Mathf.Clamp01(PlayerHealthRulebook.Instance.GetResistance(person.AggregatedAgeGroup));

        if (GeneralPopulationManager.Instance != null)
            return Mathf.Clamp01(GeneralPopulationManager.Instance.GetResistance(person.AggregatedAgeGroup));

        return 0f;
    }

    public int TryApplyProductionHealthWear(float externalMultiplier = 1f, bool debugLogging = false)
    {
        if (!productionCanLowerWorkerHealth)
            return 0;

        if (_activePlan == null || string.IsNullOrWhiteSpace(_populationReservationId) || _populationReservedAmount <= 0)
            return 0;

        var pop = PlayersPopulationManager.Instance;
        var familySim = PlayerFamilySimulationManager.Instance;

        if (pop == null || familySim == null)
            return 0;

        if (!pop.TryGetReservedIndividualIds(_populationReservationId, out var reservedIds) ||
            reservedIds == null || reservedIds.Count == 0)
        {
            return 0;
        }

        int affectedWorkers = 0;
        float totalHealthLoss = 0f;

        for (int i = 0; i < reservedIds.Count; i++)
        {
            string individualId = reservedIds[i];
            Individual person = FindIndividualById(individualId);

            if (person == null || !person.IsAlive)
                continue;

            float loss = UnityEngine.Random.Range(productionHealthLossMinPerTurn, productionHealthLossMaxPerTurn);
            loss *= Mathf.Max(0f, productionHealthLossMultiplier);
            loss *= Mathf.Max(0f, externalMultiplier);

            float ageResistance01 = GetProductionWorkerAgeResistance01(person);
            loss *= (1f - ageResistance01);

            loss = Mathf.Max(0f, loss);
            if (loss <= 0f)
                continue;

            float oldHealth = person.Health01;
            float newHealth = Mathf.Clamp(person.Health01 - loss, minimumHealthAfterProductionWear, 1f);

            if (newHealth >= oldHealth - 0.0001f)
                continue;

            person.Health01 = newHealth;
            affectedWorkers++;
            totalHealthLoss += (oldHealth - newHealth);

            if (debugLogging)
            {
                //Debug.Log(
                    //$"[ProductionBuildingControl] Worker wear | Building='{name}' | " +
                    //$"Person={person.Id} | Age={person.AggregatedAgeGroup} | " +
                    //$"Resistance01={ageResistance01:F2} | " +
                    //$"Health {oldHealth:F3}->{newHealth:F3}");
            }
        }

        if (affectedWorkers > 0)
        {
            pop.MarkUIDirty();
            SaveSystem.MarkSectionDirty(SaveSectionKeys.Population);
        }

        if (debugLogging && affectedWorkers > 0)
        {
            //Debug.Log(
                //$"[ProductionBuildingControl] Production wear at '{name}' | " +
                //$"Plan={(_activePlan != null ? _activePlan.productionID : "None")} | " +
                //$"AffectedWorkers={affectedWorkers} | " +
                //$"TotalHealthLoss={totalHealthLoss:F3}");
        }

        return affectedWorkers;
    }

    private float GetProductionOutputMultiplierForCurrentReservation()
    {
        if (string.IsNullOrWhiteSpace(_populationReservationId))
            return 1f;

        var pop = PlayersPopulationManager.Instance;
        if (pop == null)
            return 1f;

        if (!pop.TryGetReservedIndividualIds(_populationReservationId, out var reservedIds) ||
            reservedIds == null || reservedIds.Count == 0)
        {
            return 1f;
        }

        float healthMultiplier = 1f;

        if (productionHealthAffectsOutput)
            healthMultiplier = GetHealthOutputMultiplierForReservedIds(reservedIds);

        float diseaseMultiplier = 1f;

        if (productionDiseaseAffectsOutput && DiseaseManager.Instance != null)
        {
            diseaseMultiplier = DiseaseManager.Instance.GetWorkEfficiencyMultiplierForIndividuals(
                reservedIds,
                "Production",
                name);
        }

        float finalMultiplier = Mathf.Clamp01(healthMultiplier * diseaseMultiplier);

        if (debugDiseaseProductionOutput && diseaseMultiplier < 0.999f)
        {
            //Debug.Log(
                //$"[ProductionBuildingControl] Disease lowered production output. " +
                //$"Building={name}, " +
                //$"HealthMultiplier={healthMultiplier:F3}, " +
                //$"DiseaseMultiplier={diseaseMultiplier:F3}, " +
                //$"FinalMultiplier={finalMultiplier:F3}");
        }

        return Mathf.Clamp(finalMultiplier, productionMinimumOutputMultiplier, 1f);
    }

    private float GetHealthOutputMultiplierForReservedIds(IReadOnlyList<string> reservedIds)
    {
        if (reservedIds == null || reservedIds.Count == 0)
            return 1f;

        float totalHealth = 0f;
        int liveCount = 0;

        for (int i = 0; i < reservedIds.Count; i++)
        {
            string individualId = reservedIds[i];
            Individual person = FindIndividualById(individualId);

            if (person == null || !person.IsAlive)
                continue;

            totalHealth += Mathf.Clamp01(person.Health01);
            liveCount++;
        }

        if (liveCount <= 0)
            return 1f;

        float averageHealth = totalHealth / liveCount;

        if (averageHealth >= productionOutputPenaltyStartsBelowHealth)
            return 1f;

        float healthT = Mathf.InverseLerp(
            minimumHealthAfterProductionWear,
            productionOutputPenaltyStartsBelowHealth,
            averageHealth);

        float multiplier = Mathf.Lerp(
            productionMinimumOutputMultiplier,
            1f,
            healthT);

        multiplier = Mathf.Lerp(
            1f,
            multiplier,
            Mathf.Clamp01(productionOutputPenaltyStrength));

        return Mathf.Clamp(multiplier, productionMinimumOutputMultiplier, 1f);
    }

    private int TryApplyProductionEnvironmentalDiseaseForCompletedCycle(
    ProductionPlan plan,
    bool debugLogging = false)
    {
        if (!productionWeatherDiseaseExposure)
            return 0;

        if (plan == null)
            return 0;

        if (DiseaseManager.Instance == null)
            return 0;

        if (string.IsNullOrWhiteSpace(_populationReservationId))
            return 0;

        if (!TryCollectReservedProductionWorkerIds(_tmpProductionWeatherDiseaseIds))
            return 0;

        DiseaseManager.Instance?.TrySpreadContagiousVirusesWithinGroup(
            _tmpProductionWeatherDiseaseIds,
            "Production",
            name,
            1f);

        int totalInfections = 0;

        if (plan.isExternalExtractor)
        {
            IReadOnlyList<EnvironmentControl> extractionTiles = GetExtractionTilesForPlan(plan);

            if (extractionTiles == null || extractionTiles.Count == 0)
                return 0;

            for (int i = 0; i < extractionTiles.Count; i++)
            {
                EnvironmentControl env = extractionTiles[i];

                if (env == null)
                    continue;

                totalInfections += DiseaseManager.Instance.TryApplyEnvironmentalDiseaseRiskForTaskResult(
                    env,
                    _populationReservationId,
                    DiseaseTaskResultType.ProductionExtractorTileExposure,
                    extractorProductionDiseaseChanceMultiplier,
                    extractorProductionDiseaseExposureMultiplier,
                    maxExtractorProductionDiseaseTargetsPerTilePerCycle);
            }

            if (debugLogging || debugProductionWeatherDiseaseExposure)
            {
                if (totalInfections > 0)
                {
                    //Debug.Log(
                        //$"[ProductionBuildingControl] Extractor tile disease exposure. " +
                        //$"Building={name}, " +
                        //$"Plan={plan.productionID}, " +
                        //$"Tiles={extractionTiles.Count}, " +
                        //$"Workers={_tmpProductionWeatherDiseaseIds.Count}, " +
                        //$"Infections={totalInfections}");
                }
            }

            return totalInfections;
        }

        totalInfections += DiseaseManager.Instance.TryApplyEnvironmentalDiseaseRiskForBuildingComponent(
            this,
            _tmpProductionWeatherDiseaseIds,
            DiseaseTaskResultType.ProductionInternalWeatherExposure,
            internalProductionWeatherDiseaseChanceMultiplier,
            internalProductionWeatherDiseaseExposureMultiplier,
            maxInternalProductionWeatherDiseaseTargetsPerCycle);

        if (debugLogging || debugProductionWeatherDiseaseExposure)
        {
            if (totalInfections > 0)
            {
                //Debug.Log(
                    //$"[ProductionBuildingControl] Internal production weather disease exposure. " +
                    //$"Building={name}, " +
                    //$"Plan={plan.productionID}, " +
                    //$"Workers={_tmpProductionWeatherDiseaseIds.Count}, " +
                    //$"Infections={totalInfections}");
            }
        }

        return totalInfections;
    }

    private bool TryCollectReservedProductionWorkerIds(List<string> results)
    {
        if (results == null)
            return false;

        results.Clear();

        if (string.IsNullOrWhiteSpace(_populationReservationId))
            return false;

        PlayersPopulationManager pop = PlayersPopulationManager.Instance;
        if (pop == null)
            return false;

        if (!pop.TryGetReservedIndividualIds(_populationReservationId, out var reservedIds) ||
            reservedIds == null ||
            reservedIds.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < reservedIds.Count; i++)
        {
            string id = reservedIds[i];

            if (string.IsNullOrWhiteSpace(id))
                continue;

            if (results.Contains(id))
                continue;

            results.Add(id);
        }

        return results.Count > 0;
    }

    private int TryApplyBuildingDiseaseToProductionWorkers()
    {
        if (_buildingDiseaseExposure == null)
            return 0;

        if (string.IsNullOrWhiteSpace(_populationReservationId))
            return 0;

        PlayersPopulationManager pop = PlayersPopulationManager.Instance;
        if (pop == null)
            return 0;

        _tmpBuildingDiseaseProductionWorkerIds.Clear();

        if (!pop.TryGetReservedIndividualIds(_populationReservationId, out var reservedIds) ||
            reservedIds == null ||
            reservedIds.Count == 0)
        {
            return 0;
        }

        for (int i = 0; i < reservedIds.Count; i++)
        {
            string id = reservedIds[i];

            if (string.IsNullOrWhiteSpace(id))
                continue;

            if (_tmpBuildingDiseaseProductionWorkerIds.Contains(id))
                continue;

            _tmpBuildingDiseaseProductionWorkerIds.Add(id);
        }

        if (_tmpBuildingDiseaseProductionWorkerIds.Count == 0)
            return 0;

        return _buildingDiseaseExposure.TryApplyToActiveProductionWorkers(
            _tmpBuildingDiseaseProductionWorkerIds,
            BuildingDiseaseTriggerTiming.OnCompletedCycle,
            name);
    }

    public float GetCompletedCycleOutputMultiplier()
    {
        return Mathf.Clamp(_pendingCompletedCycleOutputMultiplier, productionMinimumOutputMultiplier, 1f);
    }

    // ----------------- NOTIFICATIONS -----------------

    private void PostProductionCompletedNotification(float outputMultiplier)
    {
        if (NotificationManager.Instance == null || _activePlan == null) return;

        string buildingDisplayName = GetBuildingDisplayName();
        string planName = !string.IsNullOrWhiteSpace(_activePlan.productionID)
            ? _activePlan.productionID
            : "production";

        string title, message;
        if (NotificationMessageCrafterManager.Instance != null)
            (title, message) = NotificationMessageCrafterManager.Instance.CraftProduction(
                NotificationType.ProductionCompleted, buildingDisplayName, planName);
        else
            (title, message) = ("Production Complete", $"{buildingDisplayName} finished a cycle of {planName}.");

        NotificationManager.Instance.AddProductionCompletedNotification(
            title, message, BuildOutputList(Mathf.Max(0f, outputMultiplier)));
    }

    private List<ProductionOutputEntry> BuildOutputList(float multiplier)
    {
        if (_activePlan == null) return null;

        var active = _activePlan.GetActiveOutputs();
        if (active == null || active.Count == 0) return null;

        var list = new List<ProductionOutputEntry>(active.Count);
        for (int i = 0; i < active.Count; i++)
        {
            var item = active[i];
            if (item?.resource == null || item.amountPerCycle <= 0) continue;
            int produced = multiplier >= 0.999f
                ? item.amountPerCycle
                : Mathf.Max(1, Mathf.RoundToInt(item.amountPerCycle * multiplier));
            list.Add(new ProductionOutputEntry { resource = item.resource, amount = produced });
        }
        return list.Count > 0 ? list : null;
    }

    private string GetBuildingDisplayName()
    {
        if (_buildingControl != null && !string.IsNullOrWhiteSpace(_buildingControl.buildingName))
            return _buildingControl.buildingName;
        return name;
    }

    private void PostProductionPausedNotification(NotificationType type, bool showDeathIcon)
    {
        if (NotificationManager.Instance == null || _activePlan == null) return;

        string buildingDisplayName = GetBuildingDisplayName();
        string planName = !string.IsNullOrWhiteSpace(_activePlan.productionID)
            ? _activePlan.productionID
            : "production";

        string title, message;
        if (NotificationMessageCrafterManager.Instance != null)
            (title, message) = NotificationMessageCrafterManager.Instance.CraftProduction(type, buildingDisplayName, planName);
        else
        {
            (title, message) = type switch
            {
                NotificationType.ProductionPausedLackOfResources =>
                    ("Production Paused",  $"{buildingDisplayName} paused — not enough resources for {planName}."),
                NotificationType.ProductionPausedLackOfWorkers =>
                    ("Production Stopped", $"{buildingDisplayName} stopped — not enough workers for {planName}."),
                _ => ("Production Issue", buildingDisplayName),
            };
        }

        NotificationManager.Instance.AddNotification(type, title, message, showDeathIcon);
    }
}
