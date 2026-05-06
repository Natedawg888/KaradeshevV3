using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class UnitGroupPanelControl : MonoBehaviour
{
    [Header("Training UI")]
    public GameObject trainingPanelRoot;
    public TMP_Text   trainingTitleText;
    public TMP_Text   trainingSkillText;
    public TMP_Text   trainingAvailablePointsText;
    public TMP_Text   trainingTurnsText;

    [Header("Training Stat Rows")]
    public TMP_Text trainHealthText;
    public TMP_Text trainMovementText;
    public TMP_Text trainPowerText;
    public TMP_Text trainDefenseText;
    public TMP_Text trainAgilityText;
    public TMP_Text trainAccuracyText;
    public TMP_Text trainRangeText;
    public TMP_Text trainStealthText;

    [Header("Training Stat Buttons")]
    public Button trainHealthMinusButton;
    public Button trainHealthPlusButton;
    public Button trainMovementMinusButton;
    public Button trainMovementPlusButton;
    public Button trainPowerMinusButton;
    public Button trainPowerPlusButton;
    public Button trainDefenseMinusButton;
    public Button trainDefensePlusButton;
    public Button trainAgilityMinusButton;
    public Button trainAgilityPlusButton;
    public Button trainAccuracyMinusButton;
    public Button trainAccuracyPlusButton;
    public Button trainRangeMinusButton;
    public Button trainRangePlusButton;
    public Button trainStealthMinusButton;
    public Button trainStealthPlusButton;

    [Header("Training Stat Blockers (overlays)")]
    public GameObject blockHealthOverlay;
    public GameObject blockMovementOverlay;
    public GameObject blockPowerOverlay;
    public GameObject blockDefenseOverlay;
    public GameObject blockAgilityOverlay;
    public GameObject blockAccuracyOverlay;
    public GameObject blockRangeOverlay;
    public GameObject blockStealthOverlay;

    [Header("Training Cost UI")]
    public Button     trainingCostButton;           // like costsButton on crafting
    public GameObject trainingCostPanelRoot;        // panel root
    public Transform  trainingCostContentParent;    // parent for BuildingCostEntry
    public GameObject trainingCostEntryPrefab;      // BuildingCostEntry prefab
    public Button     trainingCostCloseButton;

    [Header("Training Cost Colors")]
    public Color trainingCanAffordColor    = new(0.20f, 0.70f, 0.20f);
    public Color trainingCannotAffordColor = new(0.80f, 0.20f, 0.20f);

    [Header("Training Actions")]
    public Button trainingConfirmButton;
    public Button trainingCancelButton;

    // --- runtime training state ---

    private int _trainingBaseSkill;
    private int _trainingTargetSkill;
    private int _trainingTotalPoints;
    private int _trainingTurnsForUpgrade;

    private int _trainAllocHealth;
    private int _trainAllocMovement;
    private int _trainAllocPower;
    private int _trainAllocDefense;
    private int _trainAllocAgility;
    private int _trainAllocAccuracy;
    private int _trainAllocRange;
    private int _trainAllocStealth;

    private bool _canTrainHealth;
    private bool _canTrainMovement;
    private bool _canTrainPower;
    private bool _canTrainDefense;
    private bool _canTrainAgility;
    private bool _canTrainAccuracy;
    private bool _canTrainRange;
    private bool _canTrainStealth;

    // Preview widget instance for this group's training session
    private CraftOrderWidget _activeTrainingWidget;

    // How much each movement point gives (tweak if you like)
    private const float MovementPerPoint = 0.5f;

    private int TrainingPointsSpent =>
        _trainAllocHealth +
        _trainAllocMovement +
        _trainAllocPower +
        _trainAllocDefense +
        _trainAllocAgility +
        _trainAllocAccuracy +
        _trainAllocRange +
        _trainAllocStealth;

    private int TrainingPointsRemaining =>
        Mathf.Max(0, _trainingTotalPoints - TrainingPointsSpent);

    // -------------------------------------------------
    // Initial wiring (called from Core.Awake())
    // -------------------------------------------------
    private void SetupTrainingUI()
    {
        if (trainingPanelRoot != null)
            trainingPanelRoot.SetActive(false);

        // Confirm / cancel
        if (trainingConfirmButton != null)
        {
            trainingConfirmButton.onClick.RemoveAllListeners();
            trainingConfirmButton.onClick.AddListener(ConfirmTraining);
        }

        if (trainingCancelButton != null)
        {
            trainingCancelButton.onClick.RemoveAllListeners();
            trainingCancelButton.onClick.AddListener(CancelTraining);
        }

        // ----- NEW: training cost button / panel -----
        if (trainingCostButton != null)
        {
            trainingCostButton.onClick.RemoveAllListeners();
            trainingCostButton.onClick.AddListener(ToggleTrainingCostPanel);
        }

        if (trainingCostCloseButton != null)
        {
            trainingCostCloseButton.onClick.RemoveAllListeners();
            trainingCostCloseButton.onClick.AddListener(HideTrainingCostPanel);
        }

        if (trainingCostPanelRoot != null)
            trainingCostPanelRoot.SetActive(false);

        // ----- Stat +/- wiring (unchanged) -----
        // Health
        if (trainHealthPlusButton != null)
        {
            trainHealthPlusButton.onClick.RemoveAllListeners();
            trainHealthPlusButton.onClick.AddListener(
                () => AdjustTrainingAllocation(ref _trainAllocHealth, +1));
        }
        if (trainHealthMinusButton != null)
        {
            trainHealthMinusButton.onClick.RemoveAllListeners();
            trainHealthMinusButton.onClick.AddListener(
                () => AdjustTrainingAllocation(ref _trainAllocHealth, -1));
        }

        // Movement
        if (trainMovementPlusButton != null)
        {
            trainMovementPlusButton.onClick.RemoveAllListeners();
            trainMovementPlusButton.onClick.AddListener(
                () => AdjustTrainingAllocation(ref _trainAllocMovement, +1));
        }
        if (trainMovementMinusButton != null)
        {
            trainMovementMinusButton.onClick.RemoveAllListeners();
            trainMovementMinusButton.onClick.AddListener(
                () => AdjustTrainingAllocation(ref _trainAllocMovement, -1));
        }

        // Power
        if (trainPowerPlusButton != null)
        {
            trainPowerPlusButton.onClick.RemoveAllListeners();
            trainPowerPlusButton.onClick.AddListener(
                () => AdjustTrainingAllocation(ref _trainAllocPower, +1));
        }
        if (trainPowerMinusButton != null)
        {
            trainPowerMinusButton.onClick.RemoveAllListeners();
            trainPowerMinusButton.onClick.AddListener(
                () => AdjustTrainingAllocation(ref _trainAllocPower, -1));
        }

        // Defense
        if (trainDefensePlusButton != null)
        {
            trainDefensePlusButton.onClick.RemoveAllListeners();
            trainDefensePlusButton.onClick.AddListener(
                () => AdjustTrainingAllocation(ref _trainAllocDefense, +1));
        }
        if (trainDefenseMinusButton != null)
        {
            trainDefenseMinusButton.onClick.RemoveAllListeners();
            trainDefenseMinusButton.onClick.AddListener(
                () => AdjustTrainingAllocation(ref _trainAllocDefense, -1));
        }

        // Agility
        if (trainAgilityPlusButton != null)
        {
            trainAgilityPlusButton.onClick.RemoveAllListeners();
            trainAgilityPlusButton.onClick.AddListener(
                () => AdjustTrainingAllocation(ref _trainAllocAgility, +1));
        }
        if (trainAgilityMinusButton != null)
        {
            trainAgilityMinusButton.onClick.RemoveAllListeners();
            trainAgilityMinusButton.onClick.AddListener(
                () => AdjustTrainingAllocation(ref _trainAllocAgility, -1));
        }

        // Accuracy
        if (trainAccuracyPlusButton != null)
        {
            trainAccuracyPlusButton.onClick.RemoveAllListeners();
            trainAccuracyPlusButton.onClick.AddListener(
                () => AdjustTrainingAllocation(ref _trainAllocAccuracy, +1));
        }
        if (trainAccuracyMinusButton != null)
        {
            trainAccuracyMinusButton.onClick.RemoveAllListeners();
            trainAccuracyMinusButton.onClick.AddListener(
                () => AdjustTrainingAllocation(ref _trainAllocAccuracy, -1));
        }

        // Range
        if (trainRangePlusButton != null)
        {
            trainRangePlusButton.onClick.RemoveAllListeners();
            trainRangePlusButton.onClick.AddListener(
                () => AdjustTrainingAllocation(ref _trainAllocRange, +1));
        }
        if (trainRangeMinusButton != null)
        {
            trainRangeMinusButton.onClick.RemoveAllListeners();
            trainRangeMinusButton.onClick.AddListener(
                () => AdjustTrainingAllocation(ref _trainAllocRange, -1));
        }

        // Stealth
        if (trainStealthPlusButton != null)
        {
            trainStealthPlusButton.onClick.RemoveAllListeners();
            trainStealthPlusButton.onClick.AddListener(
                () => AdjustTrainingAllocation(ref _trainAllocStealth, +1));
        }
        if (trainStealthMinusButton != null)
        {
            trainStealthMinusButton.onClick.RemoveAllListeners();
            trainStealthMinusButton.onClick.AddListener(
                () => AdjustTrainingAllocation(ref _trainAllocStealth, -1));
        }
    }

    private float GetSkillTrainingCostMultiplier()
    {
        // _trainingTargetSkill is the level we're going TO
        int targetSkill = Mathf.Max(1, _trainingTargetSkill);
        return 1f + 0.1f * targetSkill;
    }

    // -------------------------------------------------
    // Begin training flow (called by Core.OnTrainClicked)
    // -------------------------------------------------
    private void BeginTraining()
    {
        if (_group == null || _group.unitType == null || _trainerContext == null)
            return;

        var unit = _group.unitType;

        // Extra safety: ensure this building can train this unit
        var trainable = _trainerContext.GetAvailableTrainableUnits();
        bool typeOk = false;
        if (trainable != null)
        {
            for (int i = 0; i < trainable.Count; i++)
            {
                if (trainable[i] == unit)
                {
                    typeOk = true;
                    break;
                }
            }
        }
        if (!typeOk)
        {
            Debug.LogWarning("[UnitGroupPanel] BeginTraining called but building cannot train this unit type.");
            return;
        }

        int maxSkill = Mathf.Max(1, unit.maxSkillLevel);
        if (_group.skillLevel >= maxSkill)
        {
            Debug.Log("[UnitGroupPanel] Group already at max skill; cannot train.");
            return;
        }

        _trainingBaseSkill   = Mathf.Max(0, _group.skillLevel);
        _trainingTargetSkill = Mathf.Min(_trainingBaseSkill + 1, maxSkill);

        int basePoints = unit.trainingStatPointsPerLevel;
        if (basePoints <= 0) basePoints = 5;

        _trainingTotalPoints = basePoints;

        int baseTurns = Mathf.Max(1, unit.trainingTurns);
        _trainingTurnsForUpgrade = baseTurns * (int)Mathf.Pow(2, _trainingTargetSkill);

        // Reset allocations
        _trainAllocHealth   = 0;
        _trainAllocMovement = 0;
        _trainAllocPower    = 0;
        _trainAllocDefense  = 0;
        _trainAllocAgility  = 0;
        _trainAllocAccuracy = 0;
        _trainAllocRange    = 0;
        _trainAllocStealth  = 0;

        // Copy trainable stat flags from the unit (you've added these bools on MilitiaUnit)
        _canTrainHealth   = unit.canTrainHealth;
        _canTrainMovement = unit.canTrainMovement;
        _canTrainPower    = unit.canTrainPower;
        _canTrainDefense  = unit.canTrainDefense;
        _canTrainAgility  = unit.canTrainAgility;
        _canTrainAccuracy = unit.canTrainAccuracy;
        _canTrainRange    = unit.canTrainRange;
        _canTrainStealth  = unit.canTrainStealth;

        SetupTrainingOrderWidgetPreview();

        if (trainingPanelRoot != null)
            trainingPanelRoot.SetActive(true);

        RefreshTrainingUI();
    }

    private void SetupTrainingOrderWidgetPreview()
    {
        if (_activeTrainingWidget != null)
        {
            Destroy(_activeTrainingWidget.gameObject);
            _activeTrainingWidget = null;
        }

        if (_group == null || _group.unitType == null || _trainerContext == null)
            return;

        Transform root = _trainerContext.skillTrainingPreviewRoot != null
            ? _trainerContext.skillTrainingPreviewRoot
            : _trainerContext.ordersUIRoot;

        var prefab = _trainerContext.orderWidgetPrefab;

        if (root == null || prefab == null)
        {
            Debug.LogWarning("[UnitGroupPanel] Cannot spawn training order preview; missing prefab or root on trainerContext.");
            return;
        }

        _activeTrainingWidget = Object.Instantiate(prefab, root);

        var icon     = _group.unitType.unitIcon;
        int maxTurns = Mathf.Max(1, _trainingTurnsForUpgrade);

        string orderId = $"group-skill-{_group.groupId}-{_trainingTargetSkill}";

        _activeTrainingWidget.Bind(orderId, maxTurns, icon);
        _activeTrainingWidget.UpdateTurns(maxTurns);
    }

    // -------------------------------------------------
    // Adjusting stat allocations
    // -------------------------------------------------
    private void AdjustTrainingAllocation(ref int statAlloc, int delta)
    {
        if (_group == null || _group.unitType == null)
            return;

        if (delta > 0)
        {
            if (TrainingPointsRemaining <= 0)
                return;

            statAlloc += 1;
        }
        else if (delta < 0)
        {
            if (statAlloc <= 0)
                return;

            statAlloc -= 1;
        }

        RefreshTrainingUI();
    }

    // -------------------------------------------------
    // Refresh UI
    // -------------------------------------------------
    private void RefreshTrainingUI()
    {
        if (_group == null || _group.unitType == null)
            return;

        var unit     = _group.unitType;
        int maxSkill = Mathf.Max(1, unit.maxSkillLevel);

        if (trainingTitleText != null)
        {
            string name = !string.IsNullOrEmpty(_group.groupName)
                ? _group.groupName
                : (unit != null ? unit.unitName : "Unit Group");

            trainingTitleText.text = $"Train {name}";
        }

        if (trainingSkillText != null)
            trainingSkillText.text = $"{_trainingTargetSkill} / {maxSkill}";

        if (trainingAvailablePointsText != null)
            trainingAvailablePointsText.text = $"{TrainingPointsRemaining}/{_trainingTotalPoints}";

        if (trainingTurnsText != null)
            trainingTurnsText.text = $"{_trainingTurnsForUpgrade} turns";

        int baseHealth = unit.maxHealth + _group.bonusHealth;
        int newHealth  = baseHealth + _trainAllocHealth;
        if (trainHealthText != null)
            trainHealthText.text = $"{newHealth}";

        float baseMove = unit.movementSpeed + _group.bonusMovementSpeed;
        float newMove  = baseMove + _trainAllocMovement * MovementPerPoint;
        if (trainMovementText != null)
            trainMovementText.text = $"{newMove}";

        int basePower = unit.power + _group.bonusPower;
        int newPower  = basePower + _trainAllocPower;
        if (trainPowerText != null)
            trainPowerText.text = $"{newPower}";

        int baseDefense = unit.defense + _group.bonusDefense;
        int newDefense  = baseDefense + _trainAllocDefense;
        if (trainDefenseText != null)
            trainDefenseText.text = $"{newDefense}";

        int baseAgility = unit.agility + _group.bonusAgility;
        int newAgility  = baseAgility + _trainAllocAgility;
        if (trainAgilityText != null)
            trainAgilityText.text = $"{newAgility}";

        int baseAccuracy = unit.accuracy + _group.bonusAccuracy;
        int newAccuracy  = baseAccuracy + _trainAllocAccuracy;
        if (trainAccuracyText != null)
            trainAccuracyText.text = $"{newAccuracy}";

        int baseRange = unit.range + _group.bonusRange;
        int newRange  = baseRange + _trainAllocRange;
        if (trainRangeText != null)
            trainRangeText.text = $"{newRange}";

        int baseStealth = unit.stealth + _group.bonusStealth;
        int newStealth  = baseStealth + _trainAllocStealth;
        if (trainStealthText != null)
            trainStealthText.text = $"{newStealth}";

        bool canSpend = TrainingPointsRemaining > 0;

        // Buttons + overlays
        if (trainHealthPlusButton != null)
            trainHealthPlusButton.interactable = _canTrainHealth && canSpend;
        if (trainHealthMinusButton != null)
            trainHealthMinusButton.interactable = _canTrainHealth && _trainAllocHealth > 0;
        if (blockHealthOverlay != null)
            blockHealthOverlay.SetActive(!_canTrainHealth);

        if (trainMovementPlusButton != null)
            trainMovementPlusButton.interactable = _canTrainMovement && canSpend;
        if (trainMovementMinusButton != null)
            trainMovementMinusButton.interactable = _canTrainMovement && _trainAllocMovement > 0;
        if (blockMovementOverlay != null)
            blockMovementOverlay.SetActive(!_canTrainMovement);

        if (trainPowerPlusButton != null)
            trainPowerPlusButton.interactable = _canTrainPower && canSpend;
        if (trainPowerMinusButton != null)
            trainPowerMinusButton.interactable = _canTrainPower && _trainAllocPower > 0;
        if (blockPowerOverlay != null)
            blockPowerOverlay.SetActive(!_canTrainPower);

        if (trainDefensePlusButton != null)
            trainDefensePlusButton.interactable = _canTrainDefense && canSpend;
        if (trainDefenseMinusButton != null)
            trainDefenseMinusButton.interactable = _canTrainDefense && _trainAllocDefense > 0;
        if (blockDefenseOverlay != null)
            blockDefenseOverlay.SetActive(!_canTrainDefense);

        if (trainAgilityPlusButton != null)
            trainAgilityPlusButton.interactable = _canTrainAgility && canSpend;
        if (trainAgilityMinusButton != null)
            trainAgilityMinusButton.interactable = _canTrainAgility && _trainAllocAgility > 0;
        if (blockAgilityOverlay != null)
            blockAgilityOverlay.SetActive(!_canTrainAgility);

        if (trainAccuracyPlusButton != null)
            trainAccuracyPlusButton.interactable = _canTrainAccuracy && canSpend;
        if (trainAccuracyMinusButton != null)
            trainAccuracyMinusButton.interactable = _canTrainAccuracy && _trainAllocAccuracy > 0;
        if (blockAccuracyOverlay != null)
            blockAccuracyOverlay.SetActive(!_canTrainAccuracy);

        if (trainRangePlusButton != null)
            trainRangePlusButton.interactable = _canTrainRange && canSpend;
        if (trainRangeMinusButton != null)
            trainRangeMinusButton.interactable = _canTrainRange && _trainAllocRange > 0;
        if (blockRangeOverlay != null)
            blockRangeOverlay.SetActive(!_canTrainRange);

        if (trainStealthPlusButton != null)
            trainStealthPlusButton.interactable = _canTrainStealth && canSpend;
        if (trainStealthMinusButton != null)
            trainStealthMinusButton.interactable = _canTrainStealth && _trainAllocStealth > 0;
        if (blockStealthOverlay != null)
            blockStealthOverlay.SetActive(!_canTrainStealth);

        bool hasAllocatedAllPoints = (TrainingPointsRemaining == 0);

        bool canAffordTraining = CanAffordSkillTraining();
        UpdateTrainingCostButtonVisual(canAffordTraining);

        if (trainingConfirmButton != null)
        {
            // Must be below max skill, all stat points spent, and can afford
            trainingConfirmButton.interactable =
                (_group.skillLevel < unit.maxSkillLevel) &&
                hasAllocatedAllPoints &&
                canAffordTraining;
        }
    }

    // -------------------------------------------------
    // Cancel / Confirm
    // -------------------------------------------------
    private void CancelTraining()
    {
        if (trainingPanelRoot != null)
            trainingPanelRoot.SetActive(false);

        if (_activeTrainingWidget != null)
        {
            Destroy(_activeTrainingWidget.gameObject);
            _activeTrainingWidget = null;
        }
    }

    private void ConfirmTraining()
    {
        if (_group == null || _group.unitType == null || _trainerContext == null || _owner == null)
        {
            CancelTraining();
            return;
        }

        var unit = _group.unitType;
        int maxSkill = Mathf.Max(1, unit.maxSkillLevel);
        if (_group.skillLevel >= maxSkill)
        {
            CancelTraining();
            return;
        }

        // Must have used all points
        if (TrainingPointsRemaining > 0)
        {
            Debug.Log("[UnitGroupPanel] Cannot start training; not all stat points have been allocated.");
            return;
        }

        // NEW: must have enough resources
        if (!CanAffordSkillTraining())
        {
            Debug.Log("[UnitGroupPanel] Cannot start training; insufficient resources for skill training.");
            if (trainingCostPanelRoot != null && !trainingCostPanelRoot.activeSelf)
            {
                trainingCostPanelRoot.SetActive(true);
                PopulateTrainingCostPanel();
            }
            return;
        }

        int newSkill      = Mathf.Clamp(_trainingTargetSkill, 0, maxSkill);
        int trainingTurns = _trainingTurnsForUpgrade;

        // Only apply deltas for stats this unit is allowed to train
        int   deltaHealth   = _canTrainHealth   ? _trainAllocHealth : 0;
        float deltaMove     = _canTrainMovement ? _trainAllocMovement * MovementPerPoint : 0f;
        int   deltaPower    = _canTrainPower    ? _trainAllocPower   : 0;
        int   deltaDefense  = _canTrainDefense  ? _trainAllocDefense : 0;
        int   deltaAgility  = _canTrainAgility  ? _trainAllocAgility : 0;
        int   deltaAccuracy = _canTrainAccuracy ? _trainAllocAccuracy: 0;
        int   deltaRange    = _canTrainRange    ? _trainAllocRange   : 0;
        int   deltaStealth  = _canTrainStealth  ? _trainAllocStealth : 0;

        string failReason;
        bool started = _trainerContext.TryStartGroupSkillTraining(
            _owner,
            _group,
            trainingTurns,
            deltaHealth,
            deltaMove,
            deltaPower,
            deltaDefense,
            deltaAgility,
            deltaAccuracy,
            deltaRange,
            deltaStealth,
            newSkill,
            out failReason);

        if (!started)
        {
            Debug.LogWarning($"[UnitGroupPanel] Could not start skill training: {failReason}");
            return;
        }

        Debug.Log(
            $"[UnitGroupPanel] Started training group {_group.groupId} to skill {newSkill} " +
            $"over {trainingTurns} turns via {_trainerContext.name}."
        );

        // Group is now "inside" the building until completion.
        // Close all panels (group panel, building panel, etc.).
        OnGroupTrainingOrAdvancementStarted();
    }

    private bool CanAffordSkillTraining()
    {
        if (_group == null || _group.unitType == null)
            return false;

        var unit = _group.unitType;
        if (unit.trainingCosts == null || unit.trainingCosts.Count == 0)
            return true; // no cost defined => always affordable

        int unitsInGroup = Mathf.Max(1, _group.unitCount);
        float factor     = GetSkillTrainingCostMultiplier();

        foreach (var baseCost in unit.trainingCosts)
        {
            if (baseCost == null || baseCost.resource == null)
                continue;

            // main order cost * group size * (1 + n * 10%)
            int baseAmount   = baseCost.amount * unitsInGroup;
            int finalNeeded  = Mathf.CeilToInt(baseAmount * factor);
            int owned        = InventoryQuery.GetOwned(baseCost.resource);

            if (owned < finalNeeded)
                return false;
        }

        return true;
    }

    private void ToggleTrainingCostPanel()
    {
        if (trainingCostPanelRoot == null) return;

        bool show = !trainingCostPanelRoot.activeSelf;
        trainingCostPanelRoot.SetActive(show);

        if (show)
            PopulateTrainingCostPanel();
        else
            ClearTrainingCostPanel();
    }

    private void HideTrainingCostPanel()
    {
        if (trainingCostPanelRoot == null) return;
        trainingCostPanelRoot.SetActive(false);
        ClearTrainingCostPanel();
    }

    private void ClearTrainingCostPanel()
    {
        if (trainingCostContentParent == null) return;

        for (int i = trainingCostContentParent.childCount - 1; i >= 0; i--)
        {
            var child = trainingCostContentParent.GetChild(i);
            if (child != null)
                Destroy(child.gameObject);
        }
    }

    private void PopulateTrainingCostPanel()
    {
        if (_group == null || _group.unitType == null)
            return;
        if (trainingCostContentParent == null || trainingCostEntryPrefab == null)
            return;

        ClearTrainingCostPanel();

        var unit = _group.unitType;
        var costs = unit.trainingCosts;
        if (costs == null || costs.Count == 0)
            return;

        int unitsInGroup = Mathf.Max(1, _group.unitCount);
        float factor     = GetSkillTrainingCostMultiplier();

        foreach (var baseCost in costs)
        {
            if (baseCost == null || baseCost.resource == null)
                continue;

            int baseAmount  = baseCost.amount * unitsInGroup;
            int finalNeeded = Mathf.CeilToInt(baseAmount * factor);
            int owned       = InventoryQuery.GetOwned(baseCost.resource);

            var go = Instantiate(trainingCostEntryPrefab, trainingCostContentParent);
            var ui = go.GetComponent<BuildingCostEntry>();
            if (ui != null)
                ui.Bind(baseCost.resource, finalNeeded, owned);
        }
    }

    private void UpdateTrainingCostButtonVisual(bool canAfford)
    {
        if (trainingCostButton == null) return;
        var img = trainingCostButton.GetComponent<Image>();
        if (img == null) return;

        img.color = canAfford ? trainingCanAffordColor : trainingCannotAffordColor;
    }
}