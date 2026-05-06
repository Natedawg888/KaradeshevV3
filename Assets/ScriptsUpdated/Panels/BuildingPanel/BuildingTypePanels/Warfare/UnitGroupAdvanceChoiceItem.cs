using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UnitGroupAdvanceChoiceItem : MonoBehaviour
{
    [Header("Main")]
    public TMP_Text unitNameText;
    public Image unitIconImage;

    [Header("Category UI")]
    public Image categoryIconImage;   // category icon

    [Header("Stats")]
    public TMP_Text healthText;
    public TMP_Text movementSpeedText;
    public TMP_Text powerText;
    public TMP_Text defenseText;
    public TMP_Text agilityText;
    public TMP_Text accuracyText;
    public TMP_Text stealthText;
    public TMP_Text rangeText;

    [Header("Info")]
    public TMP_Text turnsText;

    [Header("Cost UI")]
    public Button costButton;
    public GameObject costPanelRoot;
    public Transform costContentParent;
    public GameObject costEntryPrefab; // BuildingCostEntry
    public Button closeCostButton;

    [Header("Cost / Upkeep Toggle")]
    [Tooltip("Label that toggles between 'Show Upkeep' / 'Show Costs'.")]
    public TMP_Text costLabel;
    [Tooltip("Button inside the panel to toggle between Cost and Upkeep view.")]
    public Button toggleCostViewButton;

    [Header("Cost Colors")]
    public Color canAffordColor    = new(0.20f, 0.70f, 0.20f);
    public Color cannotAffordColor = new(0.80f, 0.20f, 0.20f);

    [Header("Actions")]
    public Button confirmButton;

    private MilitiaUnit _targetUnit;
    private TileUnitGroupData _group;
    private TileUnitGroupControl _owner;
    private KineticWarfareControl _trainer;
    private UnitGroupPanelControl _panel;

    // false = showing training costs, true = showing upkeep
    private bool _showingUpkeep = false;

    public void Setup(
        MilitiaUnit target,
        TileUnitGroupData group,
        TileUnitGroupControl owner,
        KineticWarfareControl trainer,
        UnitGroupPanelControl panel)
    {
        _targetUnit = target;
        _group      = group;
        _owner      = owner;
        _trainer    = trainer;
        _panel      = panel;

        if (_targetUnit == null) return;

        if (unitNameText)  unitNameText.text = _targetUnit.unitName;
        if (unitIconImage) unitIconImage.sprite = _targetUnit.unitIcon;

        // Category icon
        if (categoryIconImage != null)
        {
            if (UnitCategoryIconManager.Instance != null)
            {
                var icon = UnitCategoryIconManager.Instance.GetIconForCategory(_targetUnit.category);
                categoryIconImage.sprite = icon;
                categoryIconImage.gameObject.SetActive(icon != null);
            }
            else
            {
                categoryIconImage.gameObject.SetActive(false);
            }
        }

        // Stats for the target specialization
        if (healthText)        healthText.text        = _targetUnit.maxHealth.ToString();
        if (movementSpeedText) movementSpeedText.text = _targetUnit.movementSpeed.ToString("0.0");
        if (powerText)         powerText.text         = _targetUnit.power.ToString();
        if (defenseText)       defenseText.text       = _targetUnit.defense.ToString();
        if (agilityText)       agilityText.text       = _targetUnit.agility.ToString();
        if (accuracyText)      accuracyText.text      = _targetUnit.accuracy.ToString();
        if (stealthText)       stealthText.text       = _targetUnit.stealth.ToString();
        if (rangeText)         rangeText.text         = _targetUnit.range.ToString();

        if (turnsText)
        {
            int turns = Mathf.Max(1, _targetUnit.trainingTurns);
            turnsText.text = $"{turns} turns";
        }

        // Cost panel setup
        if (costPanelRoot) costPanelRoot.SetActive(false);
        _showingUpkeep = false;
        UpdateCostLabel();

        if (costButton)
        {
            costButton.onClick.RemoveAllListeners();
            costButton.onClick.AddListener(ToggleCostPanel);
        }

        if (closeCostButton)
        {
            closeCostButton.onClick.RemoveAllListeners();
            closeCostButton.onClick.AddListener(HideCostPanel);
        }

        if (toggleCostViewButton)
        {
            toggleCostViewButton.onClick.RemoveAllListeners();
            toggleCostViewButton.onClick.AddListener(ToggleCostView);
        }

        if (confirmButton)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(OnConfirmClicked);
        }

        RefreshCostButtonColor();
    }

    private void Update()
    {
        // keep cost colour and confirm interactable fresh
        RefreshCostButtonColor();
    }

    // -------- Costs / Upkeep --------

    private void ToggleCostPanel()
    {
        if (!costPanelRoot) return;

        bool show = !costPanelRoot.activeSelf;
        costPanelRoot.SetActive(show);

        if (show) 
            RebuildCostView();
        else      
            ClearCosts();
    }

    private void HideCostPanel()
    {
        if (!costPanelRoot) return;
        costPanelRoot.SetActive(false);
        ClearCosts();
    }

    private void ToggleCostView()
    {
        _showingUpkeep = !_showingUpkeep;
        UpdateCostLabel();

        if (costPanelRoot != null && costPanelRoot.activeSelf)
            RebuildCostView();
    }

    private void UpdateCostLabel()
    {
        if (!costLabel) return;
        // When we're showing costs, label offers "Show Upkeep".
        // When we're showing upkeep, label offers "Show Costs".
        costLabel.text = _showingUpkeep ? "Show Costs" : "Show Upkeep";
    }

    private void ClearCosts()
    {
        if (!costContentParent) return;

        for (int i = costContentParent.childCount - 1; i >= 0; i--)
            Destroy(costContentParent.GetChild(i).gameObject);
    }

    private void RebuildCostView()
    {
        if (_targetUnit == null || costContentParent == null || costEntryPrefab == null)
            return;

        ClearCosts();

        int unitsInGroup = Mathf.Max(1, _group != null ? _group.unitCount : 1);

        if (!_showingUpkeep)
        {
            // === Training costs for advancement (current behaviour) ===
            foreach (var c in _targetUnit.trainingCosts)
            {
                if (c == null || c.resource == null) continue;

                int needed = c.amount * unitsInGroup;
                int owned  = InventoryQuery.GetOwned(c.resource);

                var go = Instantiate(costEntryPrefab, costContentParent);
                var ui = go.GetComponent<BuildingCostEntry>();
                if (ui != null)
                    ui.Bind(c.resource, needed, owned);
            }
        }
        else
        {
            // === Upkeep per turn (scaled by group size) ===
            foreach (var c in _targetUnit.upkeepPerTurn)
            {
                if (c == null || c.resource == null) continue;

                int needed = c.amount * unitsInGroup; // upkeep for the whole group per turn
                int owned  = InventoryQuery.GetOwned(c.resource);

                var go = Instantiate(costEntryPrefab, costContentParent);
                var ui = go.GetComponent<BuildingCostEntry>();
                if (ui != null)
                    ui.Bind(c.resource, needed, owned);
            }
        }
    }

    private bool CanAffordAdvancement()
    {
        if (_targetUnit == null || _group == null) return false;

        int unitsInGroup = Mathf.Max(1, _group.unitCount);

        // Affordability is based on TRAINING costs only
        foreach (var c in _targetUnit.trainingCosts)
        {
            if (c == null || c.resource == null) continue;

            int needed = c.amount * unitsInGroup;
            int owned  = InventoryQuery.GetOwned(c.resource);

            if (owned < needed)
                return false;
        }

        return true;
    }

    private void RefreshCostButtonColor()
    {
        if (costButton == null) return;
        var img = costButton.GetComponent<Image>();
        if (img == null) return;

        bool canAfford = CanAffordAdvancement();
        img.color = canAfford ? canAffordColor : cannotAffordColor;

        if (confirmButton != null)
            confirmButton.interactable = canAfford && _trainer != null && _trainer.HasFreeTrainingSlot();
    }

    // -------- Confirm --------

    private void OnConfirmClicked()
    {
        if (_trainer == null || _group == null || _owner == null || _targetUnit == null)
            return;

        if (!CanAffordAdvancement())
        {
            // Force show *costs* when we can’t afford, so the player sees what’s missing
            if (costPanelRoot)
            {
                _showingUpkeep = false;
                UpdateCostLabel();

                if (!costPanelRoot.activeSelf)
                    costPanelRoot.SetActive(true);

                RebuildCostView();
            }
            return;
        }

        if (!_trainer.TryStartGroupAdvancement(_owner, _group, _targetUnit, out string failReason))
        {
            Debug.LogWarning($"[AdvanceChoice] Failed to start advancement: {failReason}");

            // If fail due to resources, pop cost panel open with costs view
            if (!string.IsNullOrEmpty(failReason) && failReason.Contains("Not enough"))
            {
                if (costPanelRoot)
                {
                    _showingUpkeep = false;
                    UpdateCostLabel();

                    if (!costPanelRoot.activeSelf)
                        costPanelRoot.SetActive(true);

                    RebuildCostView();
                }
            }

            return;
        }

        // Success – close panel and refresh group UI
        _panel?.OnGroupTrainingOrAdvancementStarted();
    }
}