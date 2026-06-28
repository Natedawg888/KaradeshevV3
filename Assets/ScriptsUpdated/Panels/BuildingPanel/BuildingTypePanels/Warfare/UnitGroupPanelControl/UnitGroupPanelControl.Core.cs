using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class UnitGroupPanelControl : MonoBehaviour
{
    [Header("UI Roots")]
    public GameObject root;
    public Button closeButton;
    public Button closeAllPanelsButton;

    [Header("Header")]
    public TMP_Text titleText;
    public TMP_Text coordinatesText;

    [Header("Basic Info")]
    public Image unitIconImage;
    public TMP_Text unitCountText;

    [Header("Category UI")]
    public Image categoryIconImage;

    [Header("Rename UI")]
    public Button renameButton;
    public GameObject renameContainer;
    public TMP_InputField renameInputField;
    public Button saveRenameButton;
    public Button cancelRenameButton;

    [Header("Stats")]
    public TMP_Text healthText;
    public TMP_Text movementSpeedText;
    public TMP_Text powerText;
    public TMP_Text defenseText;
    public TMP_Text agilityText;
    public TMP_Text accuracyText;
    public TMP_Text stealthText;
    public TMP_Text rangeText;
    public TMP_Text skillText;
    public Slider healthSlider;

    [Header("Service / Upkeep")]
    public Slider expirySlider;
    public Slider upkeepMissSlider;

    [Header("Split Group")]
    public Button splitButton;
    public GameObject splitPanelRoot;
    public TMP_Text splitCountText;
    public Button splitMinusButton;
    public Button splitPlusButton;
    public Button splitConfirmButton;
    public Button splitCancelButton;

    [Header("Merge Group")]
    public Button mergeButton;
    public GameObject mergePanelRoot;

    public GameObject mergeAmountControlsRoot;
    public TMP_Text mergeAmountText;
    public Button mergeMinusButton;
    public Button mergePlusButton;
    public Button mergeAmountConfirmButton;
    public Button mergeCancelButton;

    public GameObject mergeChoicesRoot;
    public Transform mergeChoicesContentRoot;
    public MergeGroupItemUI mergeChoicePrefab;

    [Header("Actions")]
    public Button trainButton;

    [Header("Unit Actions UI")]
    public Button actionOpenButton;
    public GameObject actionPanelRoot;
    public Transform actionListContentRoot;
    public UnitActionItemUI actionItemPrefab;

    [Header("Movement")]
    public Button moveOpenButton;        // main "Move..." button
    public GameObject movePanelRoot;     // small panel with Move / Patrol
    public Button moveModeButton;        // "Movement" button inside the small panel
    public Button patrolModeButton;      // "Patrol" button inside the small panel
    public Button moveCancelButton;      // closes the small movement panel

    [Header("Disband")]
    public Button disbandOpenButton;
    public GameObject disbandPanelRoot;
    public Button temporaryDisbandButton;
    public Button fullDisbandButton;
    public Button disbandCancelButton;

    private TileUnitGroupData     _group;
    private TileUnitGroupControl  _owner;
    private KineticWarfareControl _trainerContext;
    private TileControl           _tile;

    private KineticWarfarePanelControl _kineticPanel;
    private BuildingPanelControl       _buildingPanel;

    public event Action OnOpen;
    public event Action OnClose;

    private int    _initialRemainingServiceTurns = -1;
    private string _currentGroupId = null;

    // Used by split + merge partials
    private int _pendingSplitCount  = 1;
    private int _pendingMergeAmount = 1;

    [Header("Melee Targets")]
    public GameObject meleeTargetsPanelRoot;
    public Transform meleeTargetsContentRoot;
    public MeleeTargetItemUI meleeTargetItemPrefab;
    public Button meleeTargetsCloseButton;

    private static readonly List<MeleeTargetEntry> _meleeUiBuffer = new(32);
    private static readonly List<AnimalGroupState> _animalBuf = new(32);

    [Header("In Combat Panel")]
    public GameObject inCombatPanelRoot;

    public Transform inCombatAttackerRoot;   // left side root
    public Transform inCombatTargetRoot;     // right side root
    public InCombatTargetDisplayUI inCombatTargetDisplayPrefab;

    public Button inCombatCloseButton;
    public Button stopAttackButton;
    public Button retreatButton;

    public CameraControl cameraControl;

    [Header("Loot UI")]
    [SerializeField] private GameObject lootPanelRoot;
    [SerializeField] private Transform lootListContentRoot;
    [SerializeField] private CollectedItemEntry collectedItemPrefab;
    [SerializeField] private Button lootCloseButton;

    private InCombatTargetDisplayUI _attackerDisplay;
    private InCombatTargetDisplayUI _targetDisplay;

    private void Awake()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }

        if (closeAllPanelsButton != null)
        {
            closeAllPanelsButton.onClick.RemoveAllListeners();
            closeAllPanelsButton.onClick.AddListener(CloseAllPanelsStayHere);
        }

        if (renameButton != null)
        {
            renameButton.onClick.RemoveAllListeners();
            renameButton.onClick.AddListener(BeginRename);
        }

        if (saveRenameButton != null)
        {
            saveRenameButton.onClick.RemoveAllListeners();
            saveRenameButton.onClick.AddListener(SubmitRename);
        }

        if (cancelRenameButton != null)
        {
            cancelRenameButton.onClick.RemoveAllListeners();
            cancelRenameButton.onClick.AddListener(CancelRename);
        }

        if (trainButton != null)
        {
            trainButton.onClick.RemoveAllListeners();
            trainButton.onClick.AddListener(OnTrainClicked);
        }

        SetupMovementUI();
        SetupActionsUI();
        SetupMeleeTargetsUI();
        SetupInCombatUI();
        SetupLootUI();

        // ⬇ NEW: main "Disband..." button
        if (disbandOpenButton != null)
        {
            disbandOpenButton.onClick.RemoveAllListeners();
            disbandOpenButton.onClick.AddListener(OnDisbandOpenClicked);
        }

        // Let partials wire their own UI
        SetupSplitUI();
        SetupMergeUI();
        SetupTrainingUI();
        SetupDisbandUI();
        SetupScoutResultsUI();
        SetupTrackingResultsUI();

        if (renameContainer != null)
            renameContainer.SetActive(false);

        if (splitPanelRoot != null)
            splitPanelRoot.SetActive(false);

        if (mergePanelRoot != null)
            mergePanelRoot.SetActive(false);

        if (actionPanelRoot != null)
            actionPanelRoot.SetActive(false);

        if (cameraControl == null)
            cameraControl = FindObjectOfType<CameraControl>();

        if (root != null)
            root.SetActive(false);
    }

    public void ShowFor(
        TileUnitGroupData group,
        TileUnitGroupControl owner,
        KineticWarfareControl trainerContext,
        KineticWarfarePanelControl kineticPanel = null,
        BuildingPanelControl buildingPanel      = null)
    {
        _group          = group;
        _owner          = owner;
        _trainerContext = trainerContext;
        _tile           = owner != null ? owner.GetComponentInParent<TileControl>() : null;
        _kineticPanel   = kineticPanel;
        _buildingPanel  = buildingPanel;

        // ✅ Close tracking results UI whenever the unit panel opens
        if (trackingResultsPanelRoot != null)
            trackingResultsPanelRoot.SetActive(false);

        if (inCombatPanelRoot != null)
            inCombatPanelRoot.SetActive(false);

        // (optional but nice: also close action list if it was open)
        if (actionPanelRoot != null)
            actionPanelRoot.SetActive(false);

        cameraControl.PushInputLock();

        if (root != null)
            root.SetActive(true);

        Refresh();
        OnOpen?.Invoke();
    }

    public void Hide()
    {
        cameraControl.PopInputLock();

        if (root != null)
            root.SetActive(false);

        if (lootPanelRoot != null && lootPanelRoot.activeSelf)
            CloseLootPanel(discardLeftovers: true);

        // Go back to kinetic panel if we had one
        if (_kineticPanel != null)
        {
            _kineticPanel.RefreshForSameBuilding();
        }

        _group          = null;
        _owner          = null;
        _trainerContext = null;
        _tile           = null;

        var cam = GameObject.FindObjectOfType<CameraControl>();
        if (cam != null)
            cam.RestoreCameraPose();

        OnClose?.Invoke();
    }

    private void CloseAllPanelsStayHere()
    {
        if (root != null)
            root.SetActive(false);

        cameraControl.PopInputLock();

        _group          = null;
        _owner          = null;
        _trainerContext = null;
        _tile           = null;

        if (_kineticPanel != null)
        {
            _kineticPanel.Hide();
        }

        if (_buildingPanel != null)
        {
            _buildingPanel.Hide();
        }

        _kineticPanel  = null;
        _buildingPanel = null;

        OnClose?.Invoke();
    }

    public void OnGroupTrainingOrAdvancementStarted()
    {
        // Close training panel + destroy preview widget if any
        CancelTraining();

        // Close advancement panel and clear its items
        CloseAdvancementPanel();

        // Close split panel
        if (splitPanelRoot != null)
            splitPanelRoot.SetActive(false);

        // Close merge panel
        if (mergePanelRoot != null)
            mergePanelRoot.SetActive(false);

        // Close rename UI
        if (renameContainer != null)
            renameContainer.SetActive(false);

        // Finally close this group panel + its linked building/kinetic panels
        CloseAllPanelsStayHere();
    }

    public void Refresh()
    {
        if (_group == null) return;

        var unit = _group.unitType;

        string displayName = !string.IsNullOrEmpty(_group.groupName)
            ? _group.groupName
            : (unit != null ? unit.unitName : "Unit Group");

        if (titleText != null)
            titleText.text = displayName;

        if (unitIconImage != null)
            unitIconImage.sprite = unit != null ? unit.unitIcon : null;

        if (unitCountText != null)
            unitCountText.text = _group.unitCount.ToString();

        if (coordinatesText != null)
        {
            var gp = _tile != null ? _tile.GetGridPosition() : new Vector2Int(0, 0);
            coordinatesText.text = $"({gp.x},{gp.y})";
        }

        // Category icon
        if (categoryIconImage != null)
        {
            if (unit != null && UnitCategoryIconManager.Instance != null)
            {
                var icon = UnitCategoryIconManager.Instance.GetIconForCategory(unit.category);
                categoryIconImage.sprite = icon;
                categoryIconImage.gameObject.SetActive(icon != null);
            }
            else
            {
                categoryIconImage.gameObject.SetActive(false);
            }
        }

        // Stats
        if (unit != null)
        {
            int   displayMaxHealth = unit.maxHealth; // health currently not modified by training
            float displayMove      = unit.movementSpeed;
            int   displayPower     = unit.power;
            int   displayDefense   = unit.defense;
            int   displayAgility   = unit.agility;
            int   displayAccuracy  = unit.accuracy;
            int   displayRange     = unit.range;
            int   displayStealth   = unit.stealth;

            if (_group != null)
            {
                displayMaxHealth += _group.bonusHealth;
                displayMove     += _group.bonusMovementSpeed;
                displayPower    += _group.bonusPower;
                displayDefense  += _group.bonusDefense;
                displayAgility  += _group.bonusAgility;
                displayAccuracy += _group.bonusAccuracy;
                displayRange    += _group.bonusRange;
                displayStealth  += _group.bonusStealth;
            }

            if (healthText)        healthText.text        = displayMaxHealth.ToString();
            if (movementSpeedText) movementSpeedText.text = displayMove.ToString("0.0");
            if (powerText)         powerText.text         = displayPower.ToString();
            if (defenseText)       defenseText.text       = displayDefense.ToString();
            if (agilityText)       agilityText.text       = displayAgility.ToString();
            if (accuracyText)      accuracyText.text      = displayAccuracy.ToString();
            if (stealthText)       stealthText.text       = displayStealth.ToString();
            if (rangeText)         rangeText.text         = displayRange.ToString();
        }

        // Skill
        if (skillText != null)
        {
            int currentSkill = Mathf.Max(0, _group.skillLevel);
            int maxSkill     = unit != null ? Mathf.Max(1, unit.maxSkillLevel) : currentSkill;
            skillText.text   = $"{currentSkill}/{maxSkill}";
        }

        // Group health bar
        if (healthSlider != null)
        {
            healthSlider.minValue = 0f;
            healthSlider.maxValue = Mathf.Max(1, _group.maxHealth);
            healthSlider.value    = Mathf.Clamp(_group.currentHealth, 0, _group.maxHealth);
        }

        UpdateExpirySlider();
        UpdateUpkeepMissSlider();

        UpdateTrainButtonState();
        UpdateSplitButtonState();
        UpdateMergeButtonState();
        SetupAdvancementUI();
        UpdateAdvanceButtonState();
        UpdateDisbandButtonsState();
        UpdateMoveButtonState();
        UpdateActionButtonState();
    }

    private void UpdateTrainButtonState()
    {
        if (trainButton == null)
            return;

        // If the group currently has a movement route, training is not allowed.
        if (GroupHasActiveRoute(_group))
        {
            trainButton.interactable = false;
            return;
        }

        // Basic null / context checks
        if (_group == null || _group.unitType == null || _owner == null || _trainerContext == null)
        {
            trainButton.interactable = false;
            return;
        }

        var unit = _group.unitType;

        // Skill cap: cannot train if already at max skill
        int maxSkill = Mathf.Max(1, unit.maxSkillLevel);
        if (_group.skillLevel >= maxSkill)
        {
            trainButton.interactable = false;
            return;
        }

        bool canTrainHere = false;

        // Building must be able to train this unit type
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

        if (typeOk)
        {
            var buildingTile = _trainerContext.GetComponentInParent<TileControl>();
            var groupTile    = _owner.GetComponentInParent<TileControl>();

            if (buildingTile != null && groupTile != null && buildingTile == groupTile)
                canTrainHere = true;
        }

        trainButton.interactable = canTrainHere;
    }

    // ---------- Rename ----------

    private void BeginRename()
    {
        if (_group == null || renameContainer == null || renameInputField == null || renameButton == null)
            return;

        renameContainer.SetActive(true);
        renameButton.gameObject.SetActive(false);

        string currentName = !string.IsNullOrEmpty(_group.groupName)
            ? _group.groupName
            : (_group.unitType != null ? _group.unitType.unitName : "Unit Group");

        renameInputField.text = currentName;
        renameInputField.ActivateInputField();
    }

    private void SubmitRename()
    {
        if (_group == null || renameContainer == null || renameInputField == null || renameButton == null)
            return;

        string newName = renameInputField.text.Trim();
        if (!string.IsNullOrEmpty(newName))
        {
            _group.groupName = newName;
        }

        renameContainer.SetActive(false);
        renameButton.gameObject.SetActive(true);

        Refresh();
    }

    private void CancelRename()
    {
        if (renameContainer == null || renameButton == null) return;

        renameContainer.SetActive(false);
        renameButton.gameObject.SetActive(true);
    }

    private void OnTrainClicked()
    {
        // validation is done inside BeginTraining
        BeginTraining();
    }

    // ---------- Expiry ----------

    private void SetupExpirySliderInitially()
    {
        if (expirySlider == null || _group == null)
            return;

        if (TurnSystem.Instance == null)
        {
            expirySlider.gameObject.SetActive(false);
            return;
        }

        // Only for human units that actually have an expiry.
        if (!_group.HasExpiry || _group.unitType == null || !_group.unitType.isHuman)
        {
            expirySlider.gameObject.SetActive(false);
            return;
        }

        int currentTurn = TurnSystem.GetCurrentTurn();
        int remaining   = Mathf.Max(0, _group.expiryTurn - currentTurn);

        if (remaining <= 0)
        {
            // Already expired or due this turn.
            expirySlider.minValue = 0f;
            expirySlider.maxValue = 1f;
            expirySlider.value    = 0f;
            expirySlider.gameObject.SetActive(true);

            if (_initialRemainingServiceTurns <= 0)
                _initialRemainingServiceTurns = 1;

            return;
        }

        if (_initialRemainingServiceTurns <= 0)
        {
            _initialRemainingServiceTurns = remaining;
        }

        expirySlider.minValue = 0f;
        expirySlider.maxValue = _initialRemainingServiceTurns;
        expirySlider.value    = Mathf.Clamp(remaining, 0, expirySlider.maxValue);
        expirySlider.gameObject.SetActive(true);
    }

    private void UpdateExpirySlider()
    {
        if (expirySlider == null || _group == null || TurnSystem.Instance == null)
            return;

        if (!_group.HasExpiry || _group.unitType == null || !_group.unitType.isHuman)
        {
            expirySlider.gameObject.SetActive(false);
            return;
        }

        int currentTurn = TurnSystem.GetCurrentTurn();
        int remaining   = Mathf.Max(0, _group.expiryTurn - currentTurn);

        if (_initialRemainingServiceTurns <= 0)
        {
            _initialRemainingServiceTurns = Mathf.Max(1, remaining);
            expirySlider.minValue = 0f;
            expirySlider.maxValue = _initialRemainingServiceTurns;
        }

        expirySlider.gameObject.SetActive(true);
        expirySlider.value = Mathf.Clamp(remaining, 0, expirySlider.maxValue);
    }

    // ---------- Upkeep ----------

    private void UpdateUpkeepMissSlider()
    {
        if (upkeepMissSlider == null || _group == null)
        {
            return;
        }

        var unit = _group.unitType;
        if (unit == null)
        {
            return;
        }

        int maxMisses = Mathf.Max(0, unit.maxMissedUpkeepTurns);
        int missed    = Mathf.Max(0, _group.missedUpkeepTurns);

        int remaining = Mathf.Max(0, maxMisses - missed);

        upkeepMissSlider.minValue = 0f;
        upkeepMissSlider.maxValue = maxMisses;
        upkeepMissSlider.value    = remaining;
    }

    private void SetupMeleeTargetsUI()
    {
        if (meleeTargetsCloseButton != null)
        {
            meleeTargetsCloseButton.onClick.RemoveAllListeners();
            meleeTargetsCloseButton.onClick.AddListener(() =>
            {
                if (meleeTargetsPanelRoot) meleeTargetsPanelRoot.SetActive(false);
            });
        }

        if (meleeTargetsPanelRoot) meleeTargetsPanelRoot.SetActive(false);
    }

    private bool IsPlayerOwnedUnitGroup(TileUnitGroupData g)
    {
        if (g == null) return false;

        // If you have a dedicated manager that tracks ONLY the player's unit groups,
        // this is the cleanest way to avoid friendly-fire in the UI.
        var playerMgr = PlayerUnitManager.Instance;
        if (playerMgr == null) return false;

        return playerMgr.IsPlayerUnitGroupId(g.groupId);
    }

    private void OnEnable()
    {
        UnitGroupActionManager.GroupActionStateChanged += OnExternalGroupActionStateChanged;
    }

    private void OnDisable()
    {
        UnitGroupActionManager.GroupActionStateChanged -= OnExternalGroupActionStateChanged;
    }
}
