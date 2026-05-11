using System;
using System.Collections.Generic;
using UnityEngine;

public enum TaskFailureType
{
    Discovery,
    Gathering
}

[Serializable]
public struct TaskFailureData
{
    public TaskFailureType type;
    public EnvironmentType environmentType;
    public EnvironmentTileType tileType;
    public TileSize tileSize;
    public int populationLost;

    [TextArea] public string story;
}

public class EnvironmentControl : MonoBehaviour
{
    [Header("Environment Settings")]
    [SerializeField] private string environmentID;
    public string EnvironmentID => environmentID;
    public string environmentName;
    public EnvironmentType environmentType;
    public EnvironmentTileType environmentTileType;
    public TileSize tileSize;

    [Header("Discovery Process")]
    public int discoveryTurnsRequired = 3;
    public int requireDiscoveryPopulation = 5;

    [Range(0f, 100f)]
    public float DiscoveryFailureChance = 20f;

    public int DiscoveryPopPenaltyOnFailure = 2;

    [HideInInspector] public int discoveryTurnsLeft;
    public bool isBeingDiscovered = false;
    public bool IsDiscovered => envStatus != null && envStatus.IsDiscovered; // expose

    [Header("Gathering Process")]
    public int gatheringTurnsRequired;
    public int requireGatheringPopulation;
    [Range(0f, 100f)]
    public float GatheringFailureChance = 0f;
    public int GatheringPopPenaltyOnFailure = 0;

    [HideInInspector] public int gatheringTurnsLeft;
    [HideInInspector] public bool isGathering = false;

    [Header("Survey Process")]
    public int surveyTurnsRequired;
    public int requireSurveyPopulation;
    public int resurveyInterval;           // how many turns before needing to survey again

    [Header("Exploration Gate")]
    public bool canExplore = false;

    [Header("Clearing")]
    public bool canBeManuallyCleared = true;

    [HideInInspector] public int surveyTurnsLeft;
    [HideInInspector] public bool isSurveying = false;
    public bool IsSurveyed { get; set; } = false;

    [HideInInspector] public int resurveyTurnsLeft;
    [HideInInspector] public bool needsResurvey = false;

    [Header("Location")]
    public Vector2Int gridPosition;

    [Header("UI")]
    public TimerUI discoveryTimerUI; // optional UI to reflect remaining turns
    public TimerUI gatheringTimerUI;
    public TimerUI surveyTimerUI;    // optional UI to reflect remaining survey turns
    public GameObject canvas;        // discovery/gather/survey canvas / overlay

    [Header("Fire UI")]
    public GameObject fireIcon;      // shown while tile is burning
    public TimerUI fireTimerUI;      // radial fight-progress timer, shown while actively fighting

    [Header("Failure UI")]
    public GameObject discoveryFailedIcon;
    public GameObject gatheringFailedIcon;

    // stays true until the tile is selected
    private bool _pendingDiscoveryFailIcon = false;
    private bool _pendingGatheringFailIcon = false;

    // queued failures (so if multiple happen, player sees them one by one)
    [SerializeField] private List<TaskFailureData> pendingTaskFailures = new();

    public bool HasPendingTaskFailures => pendingTaskFailures != null && pendingTaskFailures.Count > 0;
    public bool HasPendingFailureIndicators => _pendingDiscoveryFailIcon || _pendingGatheringFailIcon;


    [Header("Collect UI")]
    public GameObject collectReadyIcon;

    [Header("Cached Base Task Values (do not edit at runtime)")]
    [SerializeField, HideInInspector] private int _baseDiscoveryTurnsRequired;
    [SerializeField, HideInInspector] private float _baseDiscoveryFailChance;

    [SerializeField, HideInInspector] private int _baseGatheringTurnsRequired;
    [SerializeField, HideInInspector] private float _baseGatheringFailChance;

    public int BaseDiscoveryTurnsRequired => _baseDiscoveryTurnsRequired;
    public float BaseDiscoveryFailChance => _baseDiscoveryFailChance;

    public int BaseGatheringTurnsRequired => _baseGatheringTurnsRequired;
    public float BaseGatheringFailChance => _baseGatheringFailChance;

    [SerializeField, HideInInspector] private int _baseDiscoveryRequiredPop;
    [SerializeField, HideInInspector] private int _baseGatheringRequiredPop;

    public int BaseDiscoveryRequiredPop => _baseDiscoveryRequiredPop;
    public int BaseGatheringRequiredPop => _baseGatheringRequiredPop;

    [Header("Fire")]
    public bool canCatchFire = true;
    public GameObject onFirePrefab;
    public Transform fireVisualRoot;

    [Header("Fire Simulation")]
    [Min(1)] public int baseBurnTurns = 4;
    [Range(0f, 1f)] public float baseDryness01 = 0.5f;
    [Range(0.05f, 3f)] public float fireIgnitionMultiplier = 1f;
    [Range(0.05f, 3f)] public float burnSpeedMultiplier = 1f;
    [Range(0f, 1f)] public float rainSuppressionStrength = 0.65f;
    [Range(0f, 1f)] public float rainExtinguishChanceAtFullRain = 0.35f;
    [Range(0f, 1f)] public float wettingPerRainTurn = 0.30f;
    [Range(0f, 1f)] public float dryOutPerDryTurn = 0.08f;

    [SerializeField] private bool isOnFire = false;
    [SerializeField] private GameObject activeFireInstance;
    [SerializeField, Range(0f, 1f)] private float currentDryness01 = 0.5f;
    [SerializeField] private int burnTurnsRemaining = 0;
    [SerializeField] private float burnProgress = 0f;

    [Range(0f, 1f)] public float maxDryness01 = 0.85f;

    [Tooltip("Burning tiles dry faster if they are not currently being rained on.")]
    [Range(0.1f, 3f)] public float dryOutMultiplierWhileBurning = 1.25f;

    [Tooltip("How strongly dryness resists rain extinguishing fire. Higher = dry fires survive rain more easily.")]
    [Range(0f, 1f)] public float rainExtinguishDrynessResistanceStrength = 0.80f;

    public bool IsOnFire => isOnFire;
    public int BurnTurnsRemaining => burnTurnsRemaining;
    public float CurrentDryness01 => currentDryness01;

    [SerializeField] private bool lavaFireSustained = false;

    // ----- Pending Loot (for post-gather collection) -----
    [Serializable]
    public class PendingLoot
    {
        public ResourceDefinition def;
        public int amount;
    }

    [SerializeField] private List<PendingLoot> pendingLoot = new();   // serialized for debugging
    public bool HasLootReady => pendingLoot != null && pendingLoot.Count > 0;

    /// <summary>Read-only peek for UI.</summary>
    public IReadOnlyList<PendingLoot> PeekPendingLoot() => pendingLoot;

    /// <summary>Called by the manager to stash loot on this tile.</summary>
    public void StorePendingLoot(List<(ResourceDefinition def, int amount)> loot)
    {
        pendingLoot.Clear();
        if (loot != null)
        {
            foreach (var (def, amt) in loot)
            {
                if (def != null && amt > 0)
                    pendingLoot.Add(new PendingLoot { def = def, amount = amt });
            }
        }

        // reflect in UI
        if (collectReadyIcon) collectReadyIcon.SetActive(HasLootReady);
        UpdateCanvasVisibility();
    }

    /// <summary>Take and clear all stashed loot (used by your future "Collect Panel").</summary>
    public List<(ResourceDefinition def, int amount)> TakeAllPendingLoot()
    {
        var outList = new List<(ResourceDefinition, int)>(pendingLoot.Count);
        foreach (var p in pendingLoot) outList.Add((p.def, p.amount));
        pendingLoot.Clear();
        if (collectReadyIcon) collectReadyIcon.SetActive(false);
        UpdateCanvasVisibility();
        return outList;
    }

    /// <summary>Clear stashed loot without taking it.</summary>
    public void ClearPendingLoot()
    {
        pendingLoot.Clear();
        if (collectReadyIcon) collectReadyIcon.SetActive(false);
        UpdateCanvasVisibility();
    }

    // ----- Internals -----
    private EnvironmentStatus envStatus;
    private TileScript parentTile;

    private bool _showForProductionSelection = false;

    public event Action OnDiscoveryStarted;
    public event Action OnDiscoveryFailed;
    public event Action OnDiscoveryCompleted;

    public event Action OnGatheringStarted;
    public event Action OnGatheringFailed;
    public event Action OnGatheringCompleted;

    public event Action OnSurveyStarted;
    public event Action OnSurveyCompleted;

    public void InitializeForTile(TileScript tile)
    {
        if (tile == null)
            return;

        parentTile = tile;

        // Copy core identity from the tile
        environmentTileType = tile.GetChosenTileType();
        environmentType     = tile.GetChosenEnvironmentType();
        tileSize            = tile.tileSize;

        EnvironmentType taskEnvType =
            environmentType == EnvironmentType.Volcano
                ? EnvironmentType.Mountain
        : environmentType;

        ApplyFireDefaultsFromTile();
        ResetFireRuntimeState();

        // --- Task settings (SO if available, static calculators as fallback) ---
        var calc = EnvironmentCalculations.Instance;
        if (calc != null)
        {
            discoveryTurnsRequired       = calc.GetDiscoveryTurns(taskEnvType, environmentTileType, tileSize);
            DiscoveryFailureChance       = calc.GetDiscoveryFailureChance(taskEnvType, environmentTileType, tileSize);
            requireDiscoveryPopulation   = calc.GetDiscoveryRequiredPop(taskEnvType, environmentTileType, tileSize);
            DiscoveryPopPenaltyOnFailure = calc.GetDiscoveryPopPenalty(taskEnvType, environmentTileType, tileSize);

            gatheringTurnsRequired       = calc.GetGatheringTurns(taskEnvType, environmentTileType, tileSize);
            requireGatheringPopulation   = calc.GetGatheringRequiredPop(taskEnvType, environmentTileType, tileSize);
            GatheringFailureChance       = calc.GetGatheringFailureChance(taskEnvType, environmentTileType, tileSize);
            GatheringPopPenaltyOnFailure = calc.GetGatheringPopPenalty(taskEnvType, environmentTileType, tileSize);

            surveyTurnsRequired      = calc.GetSurveyTurns(taskEnvType, environmentTileType, tileSize);
            requireSurveyPopulation  = calc.GetSurveyRequiredPop(taskEnvType, environmentTileType, tileSize);
            resurveyInterval         = calc.GetResurveyInterval(taskEnvType, environmentTileType, tileSize);
        }
        else
        {
            discoveryTurnsRequired       = DiscoveryTurnCalculator.CalculateDiscoveryTurns(taskEnvType, environmentTileType, tileSize);
            DiscoveryFailureChance       = DiscoveryFailureCalculator.CalculateFailureChance(taskEnvType, environmentTileType, tileSize);
            requireDiscoveryPopulation   = DiscoveryPopulationRequirementCalculator.CalculateRequiredPopulation(taskEnvType, environmentTileType, tileSize);
            DiscoveryPopPenaltyOnFailure = DiscoveryPenaltyCalculator.CalculatePopulationPenalty(taskEnvType, environmentTileType, tileSize);

            gatheringTurnsRequired       = GatheringTurnCalculator.CalculateGatheringTurns(taskEnvType, environmentTileType, tileSize);
            requireGatheringPopulation   = GatheringPopulationRequirementCalculator.CalculateRequiredPopulation(taskEnvType, environmentTileType, tileSize);
            GatheringFailureChance       = GatheringFailureCalculator.CalculateFailureChance(taskEnvType, environmentTileType, tileSize);
            GatheringPopPenaltyOnFailure = GatheringPenaltyCalculator.CalculatePopulationPenalty(taskEnvType, environmentTileType, tileSize);

            surveyTurnsRequired     = SurveyTurnCalculator.CalculateSurveyTurns(taskEnvType, environmentTileType, tileSize);
            requireSurveyPopulation = SurveyPopulationRequirementCalculator.CalculateRequiredPopulation(taskEnvType, environmentTileType, tileSize);
            resurveyInterval        = ResurveyIntervalCalculator.CalculateResurveyInterval(taskEnvType, environmentTileType, tileSize);
        }

        _baseDiscoveryTurnsRequired = discoveryTurnsRequired;
        _baseDiscoveryFailChance = DiscoveryFailureChance;

        _baseGatheringTurnsRequired = gatheringTurnsRequired;
        _baseGatheringFailChance = GatheringFailureChance;

        _baseDiscoveryRequiredPop = requireDiscoveryPopulation;

        // after requireGatheringPopulation is calculated
        _baseGatheringRequiredPop = requireGatheringPopulation;

        // Reset runtime state when re-using from pool
        isBeingDiscovered = false;
        isGathering       = false;
        isSurveying       = false;
        IsSurveyed        = false;
        needsResurvey     = false;

        pendingLoot.Clear();
        if (collectReadyIcon != null) collectReadyIcon.SetActive(false);

        if (canvas != null) canvas.SetActive(false);

        // Ensure ID & name are valid *after* we know the type/tileType
        if (string.IsNullOrEmpty(environmentID))
            environmentID = Guid.NewGuid().ToString();

        if (string.IsNullOrEmpty(environmentName))
            environmentName = EnvironmentNameGenerator.GetRandomName(environmentType, environmentTileType);

        _pendingDiscoveryFailIcon = false;
        _pendingGatheringFailIcon = false;
        if (discoveryFailedIcon != null) discoveryFailedIcon.SetActive(false);
        if (gatheringFailedIcon != null) gatheringFailedIcon.SetActive(false);
    }

    private void Awake()
    {
        envStatus = GetComponent<EnvironmentStatus>();

        var tile = GetComponentInParent<TileScript>();
        if (tile != null)
            InitializeForTile(tile);

        var envFire = GetComponent<EnvironmentFireState>();
        if (envFire != null)
        {
            envFire.OnIgnited      += HandleFireIgnited;
            envFire.OnExtinguished += HandleFireExtinguished;
            envFire.OnFightProgress += HandleFireFightProgress;
        }

        SetFireIconActive(false);
        SetFireTimerActive(false);
    }

    private void OnDestroy()
    {
        var envFire = GetComponent<EnvironmentFireState>();
        if (envFire != null)
        {
            envFire.OnIgnited       -= HandleFireIgnited;
            envFire.OnExtinguished  -= HandleFireExtinguished;
            envFire.OnFightProgress -= HandleFireFightProgress;
        }
    }

    private void HandleFireIgnited(EnvironmentFireState state)
    {
        SetFireIconActive(true);
        SetFireTimerActive(false);
    }

    private void HandleFireExtinguished(EnvironmentFireState state)
    {
        SetFireIconActive(false);
        SetFireTimerActive(false);
    }

    private void HandleFireFightProgress(EnvironmentFireState state, int rollResult, int turnsRemaining)
    {
        SetFireIconActive(true);
        SetFireTimerActive(true);

        if (fireTimerUI != null)
            fireTimerUI.SetState(state.baseFightTurns, state.FightTurnsRemaining);
    }

    private void SetFireIconActive(bool on)
    {
        if (fireIcon != null) fireIcon.SetActive(on);
        UpdateCanvasVisibility();
    }

    private void SetFireTimerActive(bool on)
    {
        if (fireTimerUI != null) fireTimerUI.gameObject.SetActive(on);
    }

    private void OnValidate()
    {
        // auto-find the canvas child if not assigned
        if (canvas == null)
        {
            var found = Array.Find(GetComponentsInChildren<Transform>(true),
                                   t => t.name == "EnvironmentTileCanvas");
            if (found != null) canvas = found.gameObject;
        }

        // auto-find discovery timer
        if (discoveryTimerUI == null)
        {
            var timerTransform = Array.Find(GetComponentsInChildren<Transform>(true),
                                            t => t.name == "DiscoveryTaskIconTimer");
            if (timerTransform != null)
            {
                discoveryTimerUI = timerTransform.GetComponent<TimerUI>()
                                  ?? timerTransform.GetComponentInChildren<TimerUI>();
            }
        }

        // auto-find survey timer
        if (surveyTimerUI == null)
        {
            var surveyTransform = Array.Find(GetComponentsInChildren<Transform>(true),
                                             t => t.name == "SurveyTaskIconTimer");
            if (surveyTransform != null)
            {
                surveyTimerUI = surveyTransform.GetComponent<TimerUI>()
                               ?? surveyTransform.GetComponentInChildren<TimerUI>();
            }
        }

        // auto-find gathering timer
        if (gatheringTimerUI == null)
        {
            var gatherTransform = Array.Find(GetComponentsInChildren<Transform>(true),
                                             t => t.name == "GatheringTaskIconTimer");
            if (gatherTransform != null)
            {
                gatheringTimerUI = gatherTransform.GetComponent<TimerUI>()
                                ?? gatherTransform.GetComponentInChildren<TimerUI>();
            }
        }

        // auto-find fire timer
        if (fireTimerUI == null)
        {
            var fireTransform = Array.Find(GetComponentsInChildren<Transform>(true),
                                           t => t.name == "FireFightIconTimer");
            if (fireTransform != null)
                fireTimerUI = fireTransform.GetComponent<TimerUI>()
                           ?? fireTransform.GetComponentInChildren<TimerUI>();
        }

        if (fireIcon == null)
        {
            var t = Array.Find(GetComponentsInChildren<Transform>(true),
                               x => x.name == "FireIcon");
            if (t != null) fireIcon = t.gameObject;
        }

        // Try to auto-find a child named "CollectReadyIcon" if not assigned
        if (collectReadyIcon == null)
        {
            var t = Array.Find(GetComponentsInChildren<Transform>(true),
                               x => x.name == "CollectReadyIcon");
            if (t != null) collectReadyIcon = t.gameObject;
        }

        if (discoveryFailedIcon == null)
        {
            var t = Array.Find(GetComponentsInChildren<Transform>(true),
                x => x.name == "DiscoveryFailedIcon");
            if (t != null) discoveryFailedIcon = t.gameObject;
        }

        // Try to auto-find a child named "GatheringFailedIcon" if not assigned
        if (gatheringFailedIcon == null)
        {
            var t = Array.Find(GetComponentsInChildren<Transform>(true),
                x => x.name == "GatheringFailedIcon");
            if (t != null) gatheringFailedIcon = t.gameObject;
        }

        // Ensure clean editor state
        if (discoveryFailedIcon != null) discoveryFailedIcon.SetActive(false);
        if (gatheringFailedIcon != null) gatheringFailedIcon.SetActive(false);
        _pendingDiscoveryFailIcon = false;
        _pendingGatheringFailIcon = false;

        // Ensure clean editor state
        if (collectReadyIcon != null) collectReadyIcon.SetActive(false);
        pendingLoot.Clear();

        // Hide timers in editor
        if (discoveryTimerUI != null && discoveryTimerUI.gameObject.activeSelf)
            discoveryTimerUI.gameObject.SetActive(false);
        if (surveyTimerUI != null && surveyTimerUI.gameObject.activeSelf)
            surveyTimerUI.gameObject.SetActive(false);
        if (gatheringTimerUI != null && gatheringTimerUI.gameObject.activeSelf)
            gatheringTimerUI.gameObject.SetActive(false);
    }

    private void Start()
    {
        RebuildRuntimeUIState();
    }

    public void RebuildRuntimeUIState()
    {
        if (discoveryTimerUI != null)
        {
            discoveryTimerUI.gameObject.SetActive(isBeingDiscovered);
            if (isBeingDiscovered)
            {
                discoveryTimerUI.Initialize(Mathf.Max(1, discoveryTurnsRequired));
                discoveryTimerUI.UpdateTimer(discoveryTurnsLeft);
            }
        }

        if (gatheringTimerUI != null)
        {
            gatheringTimerUI.gameObject.SetActive(isGathering);
            if (isGathering)
            {
                gatheringTimerUI.Initialize(Mathf.Max(1, gatheringTurnsRequired));
                gatheringTimerUI.UpdateTimer(gatheringTurnsLeft);
            }
        }

        if (surveyTimerUI != null)
        {
            surveyTimerUI.gameObject.SetActive(isSurveying);
            if (isSurveying)
            {
                surveyTimerUI.Initialize(Mathf.Max(1, surveyTurnsRequired));
                surveyTimerUI.UpdateTimer(surveyTurnsLeft);
            }
        }

        if (collectReadyIcon != null)
            collectReadyIcon.SetActive(HasLootReady);

        if (discoveryFailedIcon != null)
            discoveryFailedIcon.SetActive(_pendingDiscoveryFailIcon);

        if (gatheringFailedIcon != null)
            gatheringFailedIcon.SetActive(_pendingGatheringFailIcon);

        UpdateCanvasVisibility();
        RefreshFireVisual();
    }

    private void UpdateCanvasVisibility()
    {
        if (canvas == null) return;

        bool anyTaskActive = isBeingDiscovered || isSurveying || isGathering;

        bool shouldShow =
            anyTaskActive ||
            HasLootReady ||
            _showForProductionSelection ||
            _pendingDiscoveryFailIcon ||
            _pendingGatheringFailIcon ||
            (fireIcon != null && fireIcon.activeSelf);

        canvas.SetActive(shouldShow);
    }

    public void GetEffectiveDiscovery(out int effTurns, out float effFail)
    {
        GetEffectiveDiscovery(out effTurns, out effFail, out _);
    }

    public void GetEffectiveDiscovery(out int effTurns, out float effFail, out int effRequiredPop)
    {
        // existing logic (your current method contents)...
        int baseTurns = Mathf.Max(1, _baseDiscoveryTurnsRequired);
        float baseFail = Mathf.Clamp(_baseDiscoveryFailChance, 0f, 100f);

        var tile = GetComponentInParent<TileControl>();
        baseFail = Mathf.Clamp(baseFail + PredatorFailureBonus.GetBonusPercent(tile), 0f, 100f);

        SeasonalTaskDifficulty.Apply(TaskFailureType.Discovery, ref baseTurns, ref baseFail);

        var buffs = PlayerTechBuffs.Instance;
        effTurns = baseTurns;
        effFail = baseFail;

        if (buffs != null)
            (effFail, effTurns) = buffs.GetDiscoveryEffective(this, baseFail, baseTurns);

        effTurns = Mathf.Max(1, effTurns);
        effFail = Mathf.Clamp(effFail, 0f, 100f);

        // NEW: required population
        int basePop = Mathf.Max(1, _baseDiscoveryRequiredPop);
        effRequiredPop = basePop;
        if (buffs != null)
            effRequiredPop = buffs.GetDiscoveryRequiredPopEffective(this, basePop);

        effRequiredPop = Mathf.Max(1, effRequiredPop);
    }

    public void GetEffectiveGathering(out int effTurns, out float effFail)
    {
        GetEffectiveGathering(out effTurns, out effFail, out _);
    }

    public void GetEffectiveGathering(out int effTurns, out float effFail, out int effRequiredPop)
    {
        int baseTurns = Mathf.Max(1, _baseGatheringTurnsRequired);
        float baseFail = Mathf.Clamp(_baseGatheringFailChance, 0f, 100f);

        var tile = GetComponentInParent<TileControl>();
        baseFail = Mathf.Clamp(baseFail + PredatorFailureBonus.GetBonusPercent(tile), 0f, 100f);

        SeasonalTaskDifficulty.Apply(TaskFailureType.Gathering, ref baseTurns, ref baseFail);

        var buffs = PlayerTechBuffs.Instance;
        effTurns = baseTurns;
        effFail = baseFail;

        if (buffs != null)
            (effFail, effTurns) = buffs.GetGatheringEffective(this, baseFail, baseTurns);

        effTurns = Mathf.Max(1, effTurns);
        effFail = Mathf.Clamp(effFail, 0f, 100f);

        // NEW: required population
        int basePop = Mathf.Max(1, _baseGatheringRequiredPop);
        effRequiredPop = basePop;
        if (buffs != null)
            effRequiredPop = buffs.GetGatheringRequiredPopEffective(this, basePop);

        effRequiredPop = Mathf.Max(1, effRequiredPop);
    }

    // ---------------- Discovery Visuals ----------------
    public void BeginDiscoveryVisuals()
    {
        isBeingDiscovered = true;

        _pendingDiscoveryFailIcon = false;
        if (discoveryFailedIcon != null) discoveryFailedIcon.SetActive(false);

        if (canvas != null) canvas.SetActive(true);

        if (discoveryTimerUI != null)
        {
            discoveryTurnsLeft = discoveryTurnsRequired;
            discoveryTimerUI.gameObject.SetActive(true);
            discoveryTimerUI.Initialize(discoveryTurnsRequired);
            UpdateDiscoveryTimerUI();
        }

        OnDiscoveryStarted?.Invoke();
    }

    public void UpdateDiscoveryTimerUI()
    {
        if (discoveryTimerUI != null)
            discoveryTimerUI.UpdateTimer(discoveryTurnsLeft);
    }

    public void CompleteDiscoveryVisuals()
    {
        EnvironmentTaskRewardManager.Instance?.TryGrantPopulationReward(EnvironmentTaskKind.Discovery, this);

        isBeingDiscovered = false;
        if (discoveryTimerUI != null) discoveryTimerUI.gameObject.SetActive(false);
        UpdateCanvasVisibility();
        OnDiscoveryCompleted?.Invoke();
    }

    public void FailDiscoveryVisuals(int populationLostOverride = -1)
    {
        isBeingDiscovered = false;
        if (discoveryTimerUI != null) discoveryTimerUI.gameObject.SetActive(false);

        RegisterTaskFailure(TaskFailureType.Discovery, populationLostOverride);

        // show icon until tile is selected (unless already selected)
        if (!IsCurrentlySelectedTile())
        {
            _pendingDiscoveryFailIcon = true;
            if (discoveryFailedIcon != null) discoveryFailedIcon.SetActive(true);
        }

        UpdateCanvasVisibility();
        OnDiscoveryFailed?.Invoke();
    }

    public void AdvanceDiscoveryTurn()
    {
        if (!isBeingDiscovered) return;
        discoveryTurnsLeft = Mathf.Max(0, discoveryTurnsLeft - 1);
        UpdateDiscoveryTimerUI();
        if (discoveryTurnsLeft == 0) CompleteDiscoveryVisuals();
    }

    // ---------------- Gathering Visuals ----------------
    public void BeginGatheringVisuals()
    {
        isGathering = true;

        _pendingGatheringFailIcon = false;
        if (gatheringFailedIcon != null) gatheringFailedIcon.SetActive(false);

        // reset any previous stash and hide collect icon
        pendingLoot.Clear();
        if (collectReadyIcon != null) collectReadyIcon.SetActive(false);

        if (canvas != null) canvas.SetActive(true);

        if (gatheringTimerUI != null)
        {
            gatheringTurnsLeft = gatheringTurnsRequired;
            gatheringTimerUI.gameObject.SetActive(true);
            gatheringTimerUI.Initialize(gatheringTurnsRequired);
            UpdateGatheringTimerUI();
        }

        OnGatheringStarted?.Invoke();
    }

    public void UpdateGatheringTimerUI()
    {
        gatheringTimerUI?.UpdateTimer(gatheringTurnsLeft);
    }

    public void CompleteGatheringVisuals()
    {
        EnvironmentTaskRewardManager.Instance?.TryGrantPopulationReward(EnvironmentTaskKind.Gathering, this);

        isGathering = false;

        // Hide timer
        if (gatheringTimerUI != null) gatheringTimerUI.gameObject.SetActive(false);

        // Show the collect icon if loot is ready (StorePendingLoot may have already set this)
        if (collectReadyIcon != null) collectReadyIcon.SetActive(HasLootReady);

        UpdateCanvasVisibility();
        OnGatheringCompleted?.Invoke();
    }

    public void FailGatheringVisuals(int populationLostOverride = -1)
    {
        isGathering = false;
        if (gatheringTimerUI != null) gatheringTimerUI.gameObject.SetActive(false);

        // Clear loot and hide icon
        pendingLoot.Clear();
        if (collectReadyIcon != null) collectReadyIcon.SetActive(false);

        RegisterTaskFailure(TaskFailureType.Gathering, populationLostOverride);

        // show icon until tile is selected (unless already selected)
        if (!IsCurrentlySelectedTile())
        {
            _pendingGatheringFailIcon = true;
            if (gatheringFailedIcon != null) gatheringFailedIcon.SetActive(true);
        }

        UpdateCanvasVisibility();
        OnGatheringFailed?.Invoke();
    }

    /// <summary>Use this when a gathering task is cancelled externally.</summary>
    public void ClearCollectReady()
    {
        pendingLoot.Clear();
        if (collectReadyIcon != null) collectReadyIcon.SetActive(false);
        UpdateCanvasVisibility();
    }

    public void AdvanceGatheringTurn()
    {
        if (!isGathering) return;
        gatheringTurnsLeft = Mathf.Max(0, gatheringTurnsLeft - 1);
        UpdateGatheringTimerUI();
        if (gatheringTurnsLeft == 0) CompleteGatheringVisuals();
    }

    // ---------------- Survey Visuals ----------------
    public void BeginSurveyVisuals()
    {
        isSurveying = true;
        if (canvas != null) canvas.SetActive(true);

        if (surveyTimerUI != null)
        {
            surveyTurnsLeft = surveyTurnsRequired;
            surveyTimerUI.gameObject.SetActive(true);
            surveyTimerUI.Initialize(surveyTurnsRequired);
            UpdateSurveyTimerUI();
        }

        OnSurveyStarted?.Invoke();
    }

    public void UpdateSurveyTimerUI()
    {
        surveyTimerUI?.UpdateTimer(surveyTurnsLeft);
    }

    public void CompleteSurveyVisuals()
    {
        isSurveying = false;
        IsSurveyed = true;
        if (surveyTimerUI != null) surveyTimerUI.gameObject.SetActive(false);
        UpdateCanvasVisibility();
        OnSurveyCompleted?.Invoke();
        ResetResurveyInterval();
    }

    public void ResetResurveyInterval()
    {
        needsResurvey = false;
        resurveyTurnsLeft = resurveyInterval;
    }

    public void AdvanceSurveyTurn()
    {
        if (!isSurveying) return;
        surveyTurnsLeft = Mathf.Max(0, surveyTurnsLeft - 1);
        UpdateSurveyTimerUI();
        if (surveyTurnsLeft == 0) CompleteSurveyVisuals();
    }

    public void AdvanceResurveyTurn()
    {
        // Only count down after an initial successful survey
        if (!IsSurveyed) return;
        if (needsResurvey) return;

        resurveyTurnsLeft = Mathf.Max(0, resurveyTurnsLeft - 1);
        if (resurveyTurnsLeft == 0)
            MarkSurveyExpired();
    }

    public void MarkSurveyExpired()
    {
        IsSurveyed = false;
        needsResurvey = true;
        resurveyTurnsLeft = 0;
        UpdateCanvasVisibility();
    }

    // Returns how many units of 'def' remain in pending loot.
    public int GetPendingAmount(ResourceDefinition def)
    {
        if (def == null || pendingLoot == null) return 0;
        for (int i = 0; i < pendingLoot.Count; i++)
            if (pendingLoot[i].def == def)
                return Mathf.Max(0, pendingLoot[i].amount);
        return 0;
    }

    // Tries to move up to 'desiredAmount' of 'def' from pending loot into the player's inventory.
    // Respects inventory capacity; updates the collect icon and pending list. Returns actually taken.
    public int TryTakePending(ResourceDefinition def, int desiredAmount)
    {
        if (def == null || desiredAmount <= 0 || pendingLoot == null) return 0;

        // Find the entry
        int idx = -1;
        for (int i = 0; i < pendingLoot.Count; i++)
        {
            if (pendingLoot[i].def == def)
            {
                idx = i; break;
            }
        }
        if (idx < 0) return 0;

        int available = Mathf.Max(0, pendingLoot[idx].amount);
        if (available <= 0) return 0;

        int toTry = Mathf.Min(desiredAmount, available);

        // Respect inventory capacity per your manager (weight/size handled by manager)
        int actuallyAdded = 0;
        if (PlayerInventoryManager.Instance != null)
        {
            // Try full chunk; if it fails (capacity) try to add smaller amounts progressively.
            if (PlayerInventoryManager.Instance.TryAdd(def, toTry))
            {
                actuallyAdded = toTry;
            }
            else
            {
                // binary-ish backoff
                int tryAmt = toTry;
                while (tryAmt > 0 && actuallyAdded == 0)
                {
                    int half = Mathf.Max(0, tryAmt / 2);
                    if (half == tryAmt) half = tryAmt - 1;
                    tryAmt = half;
                    if (tryAmt > 0 && PlayerInventoryManager.Instance.TryAdd(def, tryAmt))
                        actuallyAdded = tryAmt;
                }
            }
        }

        if (actuallyAdded > 0)
        {
            pendingLoot[idx].amount -= actuallyAdded;
            if (pendingLoot[idx].amount <= 0)
                pendingLoot.RemoveAt(idx);
        }

        // Update icon + canvas
        if (collectReadyIcon) collectReadyIcon.SetActive(HasLootReady);
        UpdateCanvasVisibility();

        return actuallyAdded;
    }

    public void SetProductionSelectionCanvas(bool active)
    {
        _showForProductionSelection = active;
        UpdateCanvasVisibility();
    }

    private bool IsCurrentlySelectedTile()
    {
        // If this tile is already selected when it fails, we treat it as already �acknowledged�.
        var sel = TileInteraction.SelectedTile;
        return sel != null && sel.EnvironmentControl == this;
    }

    public void AcknowledgeFailureIndicators()
    {
        _pendingDiscoveryFailIcon = false;
        _pendingGatheringFailIcon = false;

        if (discoveryFailedIcon != null) discoveryFailedIcon.SetActive(false);
        if (gatheringFailedIcon != null) gatheringFailedIcon.SetActive(false);

        UpdateCanvasVisibility();
    }

    public bool TryDequeuePendingTaskFailure(out TaskFailureData data)
    {
        if (pendingTaskFailures != null && pendingTaskFailures.Count > 0)
        {
            data = pendingTaskFailures[0];
            pendingTaskFailures.RemoveAt(0);
            return true;
        }

        data = default;
        return false;
    }

    private void RegisterTaskFailure(TaskFailureType type, int populationLostOverride = -1)
    {
        int lost = populationLostOverride >= 0 ? populationLostOverride : 0;

        lost = Mathf.Max(0, lost);

        string story = TaskFailureStoryManager.Instance != null
            ? TaskFailureStoryManager.Instance.BuildStory(this, type, lost)
            : "";

        var data = new TaskFailureData
        {
            type = type,
            environmentType = environmentType,
            tileType = environmentTileType,
            tileSize = tileSize,
            populationLost = lost,
            story = story
        };

        if (pendingTaskFailures == null) pendingTaskFailures = new List<TaskFailureData>();
        pendingTaskFailures.Add(data);

        // If this tile is currently selected, show the panel immediately (if possible)
        TileInteraction.NotifyTaskFailed(this);
    }

    public EnvironmentRuntimeSaveData CaptureRuntimeSaveData(string spawnedPrefabName, float localYRotation)
    {
        EnvironmentRuntimeSaveData data = new EnvironmentRuntimeSaveData
        {
            environmentID = environmentID,
            environmentName = environmentName,

            environmentType = environmentType,
            environmentTileType = environmentTileType,
            tileSize = tileSize,

            spawnedPrefabName = spawnedPrefabName,
            localYRotation = localYRotation,

            isDiscovered = IsDiscovered,
            isSurveyed = IsSurveyed,
            needsResurvey = needsResurvey,

            canExplore = canExplore,
            canBeManuallyCleared = canBeManuallyCleared,

            discoveryTurnsRequired = discoveryTurnsRequired,
            requireDiscoveryPopulation = requireDiscoveryPopulation,
            discoveryFailureChance = DiscoveryFailureChance,
            discoveryPopPenaltyOnFailure = DiscoveryPopPenaltyOnFailure,
            discoveryTurnsLeft = discoveryTurnsLeft,
            isBeingDiscovered = isBeingDiscovered,

            gatheringTurnsRequired = gatheringTurnsRequired,
            requireGatheringPopulation = requireGatheringPopulation,
            gatheringFailureChance = GatheringFailureChance,
            gatheringPopPenaltyOnFailure = GatheringPopPenaltyOnFailure,
            gatheringTurnsLeft = gatheringTurnsLeft,
            isGathering = isGathering,

            surveyTurnsRequired = surveyTurnsRequired,
            requireSurveyPopulation = requireSurveyPopulation,
            surveyTurnsLeft = surveyTurnsLeft,
            isSurveying = isSurveying,

            resurveyInterval = resurveyInterval,
            resurveyTurnsLeft = resurveyTurnsLeft,

            pendingDiscoveryFailIcon = _pendingDiscoveryFailIcon,
            pendingGatheringFailIcon = _pendingGatheringFailIcon
        };

        if (pendingTaskFailures != null)
            data.pendingTaskFailures = new List<TaskFailureData>(pendingTaskFailures);

        if (pendingLoot != null)
        {
            foreach (var loot in pendingLoot)
            {
                if (loot == null || loot.def == null || loot.amount <= 0)
                    continue;

                data.pendingLoot.Add(new PendingLootSaveData
                {
                    // Replace with a stronger ID if your ResourceDefinition has one.
                    resourceKey = loot.def.name,
                    amount = loot.amount
                });
            }
        }

        EnvironmentResourceNode resourceNode = GetComponent<EnvironmentResourceNode>();
        if (resourceNode != null)
        {
            data.resourceNodeData = resourceNode.CaptureRuntimeSaveData();
        }

        return data;
    }

    public void ApplyRuntimeSaveData(
        EnvironmentRuntimeSaveData data,
        Func<string, ResourceDefinition> resourceResolver)
    {
        if (data == null)
            return;

        environmentID = data.environmentID;
        environmentName = data.environmentName;

        environmentType = data.environmentType;
        environmentTileType = data.environmentTileType;
        tileSize = data.tileSize;

        canExplore = data.canExplore;
        canBeManuallyCleared = data.canBeManuallyCleared;

        discoveryTurnsRequired = Mathf.Max(1, data.discoveryTurnsRequired);
        requireDiscoveryPopulation = Mathf.Max(1, data.requireDiscoveryPopulation);
        DiscoveryFailureChance = Mathf.Clamp(data.discoveryFailureChance, 0f, 100f);
        DiscoveryPopPenaltyOnFailure = Mathf.Max(0, data.discoveryPopPenaltyOnFailure);
        discoveryTurnsLeft = Mathf.Clamp(data.discoveryTurnsLeft, 0, discoveryTurnsRequired);
        isBeingDiscovered = data.isBeingDiscovered && discoveryTurnsLeft > 0;

        gatheringTurnsRequired = Mathf.Max(1, data.gatheringTurnsRequired);
        requireGatheringPopulation = Mathf.Max(1, data.requireGatheringPopulation);
        GatheringFailureChance = Mathf.Clamp(data.gatheringFailureChance, 0f, 100f);
        GatheringPopPenaltyOnFailure = Mathf.Max(0, data.gatheringPopPenaltyOnFailure);
        gatheringTurnsLeft = Mathf.Clamp(data.gatheringTurnsLeft, 0, gatheringTurnsRequired);
        isGathering = data.isGathering && gatheringTurnsLeft > 0;

        surveyTurnsRequired = Mathf.Max(1, data.surveyTurnsRequired);
        requireSurveyPopulation = Mathf.Max(1, data.requireSurveyPopulation);
        surveyTurnsLeft = Mathf.Clamp(data.surveyTurnsLeft, 0, surveyTurnsRequired);
        isSurveying = data.isSurveying && surveyTurnsLeft > 0;

        resurveyInterval = Mathf.Max(0, data.resurveyInterval);
        resurveyTurnsLeft = Mathf.Max(0, data.resurveyTurnsLeft);
        needsResurvey = data.needsResurvey;

        IsSurveyed = data.isSurveyed;

        if (envStatus == null)
            envStatus = GetComponent<EnvironmentStatus>();

        if (envStatus != null)
            envStatus.SetDiscovered(data.isDiscovered);

        pendingTaskFailures = data.pendingTaskFailures != null
            ? new List<TaskFailureData>(data.pendingTaskFailures)
            : new List<TaskFailureData>();

        pendingLoot.Clear();
        if (data.pendingLoot != null)
        {
            foreach (var loot in data.pendingLoot)
            {
                if (loot == null || string.IsNullOrWhiteSpace(loot.resourceKey) || loot.amount <= 0)
                    continue;

                ResourceDefinition def = resourceResolver != null
                    ? resourceResolver(loot.resourceKey)
                    : null;

                if (def != null)
                {
                    pendingLoot.Add(new PendingLoot
                    {
                        def = def,
                        amount = loot.amount
                    });
                }
            }
        }

        EnvironmentResourceNode resourceNode = GetComponent<EnvironmentResourceNode>();
        if (resourceNode != null && data.resourceNodeData != null)
        {
            resourceNode.ApplyRuntimeSaveData(data.resourceNodeData, resourceResolver);
        }

        _pendingDiscoveryFailIcon = data.pendingDiscoveryFailIcon;
        _pendingGatheringFailIcon = data.pendingGatheringFailIcon;

        // Do not restore temporary selection-only UI state.
        _showForProductionSelection = false;

        RebuildRuntimeUIState();
    }

    private void ResetFireRuntimeState()
    {
        isOnFire = false;
        lavaFireSustained = false;
        burnTurnsRemaining = 0;
        burnProgress = 0f;
        currentDryness01 = Mathf.Clamp01(baseDryness01);

        if (activeFireInstance != null)
            Destroy(activeFireInstance);

        activeFireInstance = null;
    }

    private void ApplyFireDefaultsFromTile()
    {
        // Delegate to SO if available — all fire values become inspector-tunable.
        var calc = EnvironmentCalculations.Instance;
        if (calc != null)
        {
            calc.ApplyFireDefaults(this, environmentType, environmentTileType);
            return;
        }

        // Hardcoded fallback when no SO is assigned.
        canCatchFire = true;
        baseBurnTurns = 4;
        baseDryness01 = 0.50f;
        maxDryness01 = 0.85f;
        fireIgnitionMultiplier = 1f;
        burnSpeedMultiplier = 1f;

        switch (environmentTileType)
        {
            case EnvironmentTileType.Ocean:
            case EnvironmentTileType.Lake:
            case EnvironmentTileType.Water:
            case EnvironmentTileType.River:
            case EnvironmentTileType.RiverCorner:
            case EnvironmentTileType.RiverSplit:
            case EnvironmentTileType.RiverCross:
            case EnvironmentTileType.RiverEnd:
            case EnvironmentTileType.RiverMouth:
            case EnvironmentTileType.LakeEdge:
            case EnvironmentTileType.LakeCorner:
            case EnvironmentTileType.LakeEdgeEnd:
            case EnvironmentTileType.LakeMouth:
                canCatchFire = false;
                baseBurnTurns = 0;
                baseDryness01 = 0f;
                maxDryness01 = 0f;
                fireIgnitionMultiplier = 0f;
                burnSpeedMultiplier = 0f;
                return;

            case EnvironmentTileType.Beach:
            case EnvironmentTileType.BeachEnd:
            case EnvironmentTileType.Mountain:
            case EnvironmentTileType.SaltLake:
                baseBurnTurns = 1;
                baseDryness01 = 0.70f;
                maxDryness01 = 0.95f;
                fireIgnitionMultiplier = 0.20f;
                burnSpeedMultiplier = 0.50f;
                break;
        }

        switch (environmentType)
        {
            case EnvironmentType.TropicalForest:
                baseBurnTurns = 6;
                baseDryness01 = 0.55f;
                maxDryness01 = 0.75f;
                fireIgnitionMultiplier = 1.20f;
                burnSpeedMultiplier = 1.10f;
                break;

            case EnvironmentType.SubTropical:
            case EnvironmentType.TemperateForest:
            case EnvironmentType.BorealForest:
                baseBurnTurns = 5;
                baseDryness01 = 0.50f;
                maxDryness01 = 0.80f;
                fireIgnitionMultiplier = 1.00f;
                burnSpeedMultiplier = 1.00f;
                break;

            case EnvironmentType.Grassland:
            case EnvironmentType.Savanna:
                baseBurnTurns = 3;
                baseDryness01 = 0.80f;
                maxDryness01 = 1.00f;
                fireIgnitionMultiplier = 1.35f;
                burnSpeedMultiplier = 1.45f;
                break;

            case EnvironmentType.Desert:
                baseBurnTurns = 1;
                baseDryness01 = 0.95f;
                maxDryness01 = 1.00f;
                fireIgnitionMultiplier = 0.15f;
                burnSpeedMultiplier = 0.40f;
                break;

            case EnvironmentType.Tundra:
                baseBurnTurns = 2;
                baseDryness01 = 0.30f;
                maxDryness01 = 0.55f;
                fireIgnitionMultiplier = 0.40f;
                burnSpeedMultiplier = 0.60f;
                break;
        }

        currentDryness01 = Mathf.Clamp01(baseDryness01);
    }

    public void RefreshDrynessFromWeather(float rainIntensity01)
    {
        rainIntensity01 = Mathf.Clamp01(rainIntensity01);

        if (rainIntensity01 > 0.001f)
        {
            currentDryness01 = Mathf.MoveTowards(
                currentDryness01,
                0f,
                wettingPerRainTurn * rainIntensity01
            );
        }
        else
        {
            float dryRate = dryOutPerDryTurn;

            if (isOnFire)
                dryRate *= dryOutMultiplierWhileBurning;

            float dryTarget = Mathf.Max(baseDryness01, maxDryness01);

            currentDryness01 = Mathf.MoveTowards(
                currentDryness01,
                dryTarget,
                dryRate
            );
        }

        currentDryness01 = Mathf.Clamp01(currentDryness01);
    }

    public bool TryIgniteFire(float chance01 = 1f)
    {
        if (!canCatchFire || isOnFire || onFirePrefab == null)
            return false;

        chance01 = Mathf.Clamp01(chance01);

        float drynessIgnitionBonus = Mathf.Lerp(0.35f, 1.55f, currentDryness01);
        float finalChance = Mathf.Clamp01(chance01 * fireIgnitionMultiplier * drynessIgnitionBonus);

        if (finalChance <= 0f)
            return false;

        if (UnityEngine.Random.value > finalChance)
            return false;

        IgniteFire();
        return true;
    }

    public void IgniteFire()
    {
        if (isOnFire)
            return;

        isOnFire = true;
        burnProgress = 0f;

        float drynessDurationBonus = Mathf.Lerp(0.75f, 1.50f, currentDryness01);
        burnTurnsRemaining = Mathf.Max(1, Mathf.RoundToInt(baseBurnTurns * drynessDurationBonus));

        if (activeFireInstance == null && onFirePrefab != null)
        {
            Transform parent = fireVisualRoot != null ? fireVisualRoot : transform;

            activeFireInstance = Instantiate(onFirePrefab, parent);
            activeFireInstance.transform.localPosition = Vector3.zero;
            activeFireInstance.transform.localRotation = Quaternion.identity;
            activeFireInstance.transform.localScale = Vector3.one;
        }
    }

    public void AdvanceFireTurn(float rainIntensity01)
    {
        if (!isOnFire)
            return;

        if (lavaFireSustained)
        {
            RefreshFireVisual();
            return;
        }

        RefreshDrynessFromWeather(rainIntensity01);

        float wetness01 = 1f - currentDryness01;

        float wetnessBoost = Mathf.Lerp(0.25f, 1.85f, wetness01);

        float drynessResistance = Mathf.Lerp(
            1f,
            1f - rainExtinguishDrynessResistanceStrength,
            currentDryness01
        );

        float extinguishChance =
            rainExtinguishChanceAtFullRain *
            rainIntensity01 *
            wetnessBoost *
            drynessResistance;

        extinguishChance = Mathf.Clamp01(extinguishChance);

        if (UnityEngine.Random.value < extinguishChance)
        {
            ExtinguishFire();
            return;
        }

        float burnStep = burnSpeedMultiplier * Mathf.Lerp(0.70f, 1.55f, currentDryness01);
        burnStep *= Mathf.Lerp(1f, 1f - rainSuppressionStrength, rainIntensity01);
        burnStep = Mathf.Max(0.05f, burnStep);

        burnProgress += burnStep;

        while (burnProgress >= 1f && isOnFire)
        {
            burnProgress -= 1f;
            burnTurnsRemaining--;

            if (burnTurnsRemaining <= 0)
            {
                ExtinguishFire();
                break;
            }
        }
    }

    public void ExtinguishFire()
    {
        isOnFire = false;
        lavaFireSustained = false;
        burnTurnsRemaining = 0;
        burnProgress = 0f;

        if (activeFireInstance != null)
            Destroy(activeFireInstance);

        activeFireInstance = null;
    }

    public void RefreshFireVisual()
    {
        if (isOnFire || lavaFireSustained)
        {
            if (activeFireInstance == null && onFirePrefab != null)
            {
                Transform parent = fireVisualRoot != null ? fireVisualRoot : transform;

                activeFireInstance = Instantiate(onFirePrefab, parent);
                activeFireInstance.transform.localPosition = Vector3.zero;
                activeFireInstance.transform.localRotation = Quaternion.identity;
                activeFireInstance.transform.localScale = Vector3.one;
            }
        }
        else
        {
            if (activeFireInstance != null)
                Destroy(activeFireInstance);

            activeFireInstance = null;
        }
    }

    public void BeginTutorialDiscoverySimulation(int turnsOverride = -1)
    {
        if (envStatus == null)
            envStatus = GetComponent<EnvironmentStatus>();

        int turns = turnsOverride > 0 ? turnsOverride : Mathf.Max(1, discoveryTurnsRequired);

        isBeingDiscovered = true;
        discoveryTurnsRequired = turns;
        discoveryTurnsLeft = turns;

        _pendingDiscoveryFailIcon = false;
        if (discoveryFailedIcon != null)
            discoveryFailedIcon.SetActive(false);

        envStatus?.ResetReveal();
        envStatus?.StartPartialReveal(turns);

        if (canvas != null)
            canvas.SetActive(true);

        if (discoveryTimerUI != null)
        {
            discoveryTimerUI.gameObject.SetActive(true);
            discoveryTimerUI.Initialize(turns);
            discoveryTimerUI.UpdateTimer(discoveryTurnsLeft);
        }

        UpdateCanvasVisibility();
    }

    public void ApplyTutorialDiscoveryGhostTick()
    {
        if (!isBeingDiscovered)
            return;

        if (envStatus == null)
            envStatus = GetComponent<EnvironmentStatus>();

        envStatus?.AdvancePartialReveal();

        discoveryTurnsLeft = Mathf.Max(0, discoveryTurnsLeft - 1);
        UpdateDiscoveryTimerUI();

        if (discoveryTurnsLeft <= 0)
        {
            isBeingDiscovered = false;

            if (discoveryTimerUI != null)
                discoveryTimerUI.gameObject.SetActive(false);

            UpdateCanvasVisibility();
        }
    }

    public void ShowTutorialDiscoveryFailureIcon(int populationLost = 0)
    {
        _pendingDiscoveryFailIcon = true;

        if (discoveryFailedIcon != null)
            discoveryFailedIcon.SetActive(true);

        UpdateCanvasVisibility();
    }

    public void CompleteTutorialDiscoveryNow()
    {
        if (envStatus == null)
            envStatus = GetComponent<EnvironmentStatus>();

        envStatus?.CompleteDiscovery();

        isBeingDiscovered = false;
        discoveryTurnsLeft = 0;

        if (discoveryTimerUI != null)
            discoveryTimerUI.gameObject.SetActive(false);

        AcknowledgeFailureIndicators();
        UpdateCanvasVisibility();
    }

    public void ResetTutorialEnvironmentState()
    {
        if (envStatus == null)
            envStatus = GetComponent<EnvironmentStatus>();

        // This resets the reveal state and reapplies the undiscovered material.
        envStatus?.SetDiscovered(false);

        isBeingDiscovered = false;
        discoveryTurnsLeft = 0;
        if (discoveryTimerUI != null)
            discoveryTimerUI.gameObject.SetActive(false);

        isGathering = false;
        gatheringTurnsLeft = 0;
        if (gatheringTimerUI != null)
            gatheringTimerUI.gameObject.SetActive(false);

        isSurveying = false;
        surveyTurnsLeft = 0;
        if (surveyTimerUI != null)
            surveyTimerUI.gameObject.SetActive(false);

        IsSurveyed = false;
        needsResurvey = false;
        resurveyTurnsLeft = resurveyInterval;

        AcknowledgeFailureIndicators();
        ClearPendingLoot();

        ExtinguishFire();

        UpdateCanvasVisibility();
    }

    public void ForceStartOrSustainLavaFire()
    {
        if (!canCatchFire)
            return;

        lavaFireSustained = true;

        if (!isOnFire)
        {
            isOnFire = true;
            burnProgress = 0f;

            // Give it a real burn buffer in case sustain misses for a step.
            burnTurnsRemaining = Mathf.Max(1, baseBurnTurns);

            RefreshFireVisual();
            return;
        }

        // Keep at least a normal burn buffer while lava is present.
        burnTurnsRemaining = Mathf.Max(burnTurnsRemaining, Mathf.Max(1, baseBurnTurns));
        RefreshFireVisual();
    }

    public void ClearLavaFireSustain()
    {
        lavaFireSustained = false;
    }
}