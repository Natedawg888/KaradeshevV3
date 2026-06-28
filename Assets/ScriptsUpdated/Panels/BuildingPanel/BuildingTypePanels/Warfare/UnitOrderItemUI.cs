using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UnitOrderItemUI : MonoBehaviour
{
    [Header("Main Info")]
    public TMP_Text itemNameText;
    public Image itemIconImage;

    [Header("Stats")]
    public TMP_Text healthText;
    public TMP_Text movementSpeedText;
    public TMP_Text powerText;
    public TMP_Text defenseText;
    public TMP_Text agilityText;
    public TMP_Text accuracyText;
    public TMP_Text stealthText;
    public TMP_Text rangeText;

    [Header("Category UI")]
    public Image categoryIconImage;

    [Header("Multiplier UI")]
    public TMP_Text multiplierText;
    public Button increaseMultiplierButton;
    public Button decreaseMultiplierButton;
    public Button confirmOrderButton;

    [Header("Costs Panel")]
    [Tooltip("Label which changes between 'Show Upkeep' / 'Show Costs'.")]
    public TMP_Text costLabel;
    [Tooltip("Button that opens/closes the cost panel.")]
    public Button costsButton;
    [Tooltip("Root object of the cost panel (contains list + toggle).")]
    public GameObject costPanelRoot;
    [Tooltip("Button inside the panel to close it.")]
    public Button closeCostsButton;
    [Tooltip("Button inside the panel to toggle between Cost and Upkeep view.")]
    public Button toggleCostViewButton;

    [Header("Costs Content")]
    [Tooltip("Parent for training cost entries.")]
    public GameObject resourceCostRoot;
    public Transform resourceCostContent;

    public GameObject upkeepCostRoot;
    [Tooltip("Parent for upkeep cost entries.")]
    public Transform upkeepCostContent;
    [Tooltip("Prefab with BuildingCostEntry component (used for BOTH cost & upkeep).")]
    public GameObject costItemPrefab;   // BuildingCostEntry

    [Header("Info UI")]
    [Tooltip("Button that opens the info panel (turns, population, output).")]
    public Button infoButton;
    [Tooltip("Root object of the info panel.")]
    public GameObject infoPanelRoot;
    public TMP_Text infoTurnsText;
    public TMP_Text infoPopulationText;
    public TMP_Text infoOutputText;
    public Button closeInfoButton;

    [Header("Info Colors")]
    public Color popEnoughColor    = new(0.20f, 0.70f, 0.20f);
    public Color popNotEnoughColor = new(0.80f, 0.20f, 0.20f);

    [Header("Config")]
    [Tooltip("Maximum multiplier allowed per order (UI limit).")]
    [Min(1)]
    public int maxMultiplier = 10;

    public static bool TutorialBypassCosts = false;
    public event Action OnOrderConfirmed;
    public event Action<int> OnMultiplierChanged;

    private MilitiaUnit _unit;
    private KineticWarfareControl _ownerControl;
    private int _multiplier = 1;

    // false = showing training costs, true = showing upkeep
    private bool _showingUpkeep = false;

    // -------------------------------------------------
    // Setup
    // -------------------------------------------------

    /// <summary>
    /// Called by KineticWarfarePanelControl when populating the scroll list.
    /// </summary>
    public void Setup(MilitiaUnit unit, KineticWarfareControl ownerControl)
    {
        _unit = unit;
        _ownerControl = ownerControl;

        if (_unit == null)
        {
            //Debug.LogWarning("[UnitOrderItemUI] Setup called with null unit.");
            return;
        }

        // ---- Basic info ----
        if (itemNameText)  itemNameText.text = _unit.unitName;
        if (itemIconImage) itemIconImage.sprite = _unit.unitIcon;

        // ---- Stats ----
        if (healthText)        healthText.text        = _unit.maxHealth.ToString();
        if (movementSpeedText) movementSpeedText.text = _unit.movementSpeed.ToString("0.0");
        if (powerText)         powerText.text         = _unit.power.ToString();
        if (defenseText)       defenseText.text       = _unit.defense.ToString();
        if (agilityText)       agilityText.text       = _unit.agility.ToString();
        if (accuracyText)      accuracyText.text      = _unit.accuracy.ToString();
        if (stealthText)       stealthText.text       = _unit.stealth.ToString();
        if (rangeText)         rangeText.text         = _unit.range.ToString();

        // ---- Category icon ----
        if (categoryIconImage != null && UnitCategoryIconManager.Instance != null)
        {
            var icon = UnitCategoryIconManager.Instance.GetIconForCategory(_unit.category);
            if (icon != null)
                categoryIconImage.sprite = icon;
        }

        // ---- Multiplier ----
        _multiplier = 1;
        if (multiplierText) multiplierText.text = _multiplier.ToString();

        // ---- Cost panel wiring ----
        WireCostPanelUI();

        // ---- Info panel wiring ----
        WireInfoPanelUI();

        // ---- Buttons ----
        if (increaseMultiplierButton != null)
        {
            increaseMultiplierButton.onClick.RemoveAllListeners();
            increaseMultiplierButton.onClick.AddListener(IncreaseMultiplier);
        }
        if (decreaseMultiplierButton != null)
        {
            decreaseMultiplierButton.onClick.RemoveAllListeners();
            decreaseMultiplierButton.onClick.AddListener(DecreaseMultiplier);
        }
        if (confirmOrderButton != null)
        {
            confirmOrderButton.onClick.RemoveAllListeners();
            confirmOrderButton.onClick.AddListener(ConfirmOrder);
        }

        // Subscribe to population changes (like CraftingRecipeItem)
        if (PlayersPopulationManager.Instance != null)
            PlayersPopulationManager.Instance.OnPopulationChanged += RefreshPopulationIndicators;

        UpdateConfirmButtonState();
        RefreshCostButtonColor();
        RefreshPopulationIndicators();   // initial info colours
    }

    private void OnDestroy()
    {
        if (PlayersPopulationManager.Instance != null)
            PlayersPopulationManager.Instance.OnPopulationChanged -= RefreshPopulationIndicators;
    }

    // -------------------------------------------------
    // Cost Panel (Cost / Upkeep toggle like Crafting)
    // -------------------------------------------------

    private void WireCostPanelUI()
    {
        if (costPanelRoot)
            costPanelRoot.SetActive(false);

        _showingUpkeep = false;
        UpdateCostLabel();

        if (costsButton)
        {
            costsButton.onClick.RemoveAllListeners();
            costsButton.onClick.AddListener(ToggleCostPanel);
        }

        if (closeCostsButton)
        {
            closeCostsButton.onClick.RemoveAllListeners();
            closeCostsButton.onClick.AddListener(HideCostPanel);
        }

        if (toggleCostViewButton)
        {
            toggleCostViewButton.onClick.RemoveAllListeners();
            toggleCostViewButton.onClick.AddListener(ToggleCostView);
        }
    }

    private void ToggleCostPanel()
    {
        if (!costPanelRoot) return;

        bool show = !costPanelRoot.activeSelf;
        costPanelRoot.SetActive(show);

        if (show)
        {
            RebuildCostView();
        }
        else
        {
            ClearContent(resourceCostContent);
            ClearContent(upkeepCostContent);
        }
    }

    private void HideCostPanel()
    {
        if (!costPanelRoot) return;

        costPanelRoot.SetActive(false);
        ClearContent(resourceCostContent);
        ClearContent(upkeepCostContent);
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
        costLabel.text = _showingUpkeep ? "Show Upkeep" : "Show Costs";
    }

    // ---------------- Cost / Upkeep building ----------------

    private void ClearContent(Transform content)
    {
        if (content == null) return;
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Destroy(content.GetChild(i).gameObject);
        }
    }

    private void RebuildCostView()
    {
        ClearContent(resourceCostContent);
        ClearContent(upkeepCostContent);

        if (_unit == null || costItemPrefab == null)
            return;

        var inv = PlayerInventoryManager.Instance;

        if (!_showingUpkeep)
        {
            // === Training Costs view ===
            if (resourceCostContent == null) return;

            resourceCostRoot.gameObject.SetActive(true);
            if (upkeepCostContent) upkeepCostRoot.gameObject.SetActive(false);

            foreach (var cost in _unit.trainingCosts)
            {
                if (cost == null || cost.resource == null) continue;

                var obj   = Instantiate(costItemPrefab, resourceCostContent);
                var entry = obj.GetComponent<BuildingCostEntry>();
                if (entry != null)
                {
                    int needed = cost.amount * _multiplier;
                    int owned  = inv != null ? inv.GetAmount(cost.resource) : 0;
                    entry.Bind(cost.resource, needed, owned);
                }
            }
        }
        else
        {
            // === Upkeep view ===
            if (upkeepCostContent == null) return;

            upkeepCostRoot.gameObject.SetActive(true);
            if (resourceCostContent) resourceCostRoot.gameObject.SetActive(false);

            foreach (var cost in _unit.upkeepPerTurn)
            {
                if (cost == null || cost.resource == null) continue;

                var obj   = Instantiate(costItemPrefab, upkeepCostContent);
                var entry = obj.GetComponent<BuildingCostEntry>();
                if (entry != null)
                {
                    int needed = cost.amount * _multiplier;
                    int owned  = inv != null ? inv.GetAmount(cost.resource) : 0;
                    entry.Bind(cost.resource, needed, owned);
                }
            }
        }
    }

    // -------------------------------------------------
    // Info Panel (turns, population, output)
    // -------------------------------------------------

    private void WireInfoPanelUI()
    {
        if (infoPanelRoot)
            infoPanelRoot.SetActive(false);

        if (infoButton)
        {
            infoButton.onClick.RemoveAllListeners();
            infoButton.onClick.AddListener(ToggleInfoPanel);
        }

        if (closeInfoButton)
        {
            closeInfoButton.onClick.RemoveAllListeners();
            closeInfoButton.onClick.AddListener(HideInfoPanel);
        }

        PopulateInfo();
    }

    private void ToggleInfoPanel()
    {
        if (!infoPanelRoot) return;
        bool show = !infoPanelRoot.activeSelf;
        infoPanelRoot.SetActive(show);
        if (show) PopulateInfo();
    }

    private void HideInfoPanel()
    {
        if (!infoPanelRoot) return;
        infoPanelRoot.SetActive(false);
    }

    private void PopulateInfo()
    {
        if (_unit == null) return;

        // Turns: training time per order (per batch; multiplier doesn't change this)
        if (infoTurnsText)
            infoTurnsText.text = $"Turns: {Mathf.Max(1, _unit.trainingTurns)}";

        // Population: required vs available, coloured
        if (infoPopulationText)
        {
            int need = Mathf.Max(0, _unit.populationToTrain * _multiplier);
            int available = PlayersPopulationManager.Instance
                ? PlayersPopulationManager.Instance.GetAvailableTaskPopulation()
                : 0;

            bool enough = available >= need;
            string hex = ColorUtility.ToHtmlStringRGB(enough ? popEnoughColor : popNotEnoughColor);
            infoPopulationText.richText = true;
            infoPopulationText.text = $"Population: <color=#{hex}>{need}</color>";
        }

        // Output: unit.outputUnits * multiplier
        if (infoOutputText)
        {
            int total = Mathf.Max(1, _unit.outputUnits * _multiplier);
            infoOutputText.text = $"Output Units: {total}";
        }
    }

    private void RefreshPopulationIndicators()
    {
        // Colour the info button based on whether we have enough pop
        if (infoButton)
        {
            var img = infoButton.GetComponent<Image>();
            if (img != null)
                img.color = HasEnoughPopulation() ? popEnoughColor : popNotEnoughColor;
        }

        if (infoPanelRoot && infoPanelRoot.activeSelf)
            PopulateInfo();

        // Also refresh confirm state since pop changes affect it
        UpdateConfirmButtonState();
    }

    // -------------------------------------------------
    // Multiplier
    // -------------------------------------------------

    private void IncreaseMultiplier()
    {
        if (_unit == null) return;

        int max = Mathf.Max(1, maxMultiplier);
        if (_multiplier >= max) return;

        _multiplier++;
        if (multiplierText) multiplierText.text = _multiplier.ToString();

        if (costPanelRoot != null && costPanelRoot.activeSelf)
            RebuildCostView();

        PopulateInfo();             // info depends on multiplier (population + output)
        UpdateConfirmButtonState();
        RefreshCostButtonColor();
        OnMultiplierChanged?.Invoke(_multiplier);
    }

    private void DecreaseMultiplier()
    {
        if (_multiplier <= 1) return;

        _multiplier--;
        if (multiplierText) multiplierText.text = _multiplier.ToString();

        if (costPanelRoot != null && costPanelRoot.activeSelf)
            RebuildCostView();

        PopulateInfo();
        UpdateConfirmButtonState();
        RefreshCostButtonColor();
    }

    // -------------------------------------------------
    // Affordability + Confirm button
    // -------------------------------------------------

    private bool CanAffordTrainingCosts()
    {
        if (TutorialBypassCosts) return true;
        if (_unit == null) return true;

        var inv = PlayerInventoryManager.Instance;
        if (!inv) return true; // fail-open if no manager

        foreach (var cost in _unit.trainingCosts)
        {
            if (cost == null || cost.resource == null) continue;

            int needed = cost.amount * _multiplier;
            int owned  = inv.GetAmount(cost.resource);
            if (owned < needed)
                return false;
        }

        return true;
    }

    private bool HasEnoughPopulation()
    {
        if (TutorialBypassCosts) return true;
        var popMgr = PlayersPopulationManager.Instance;
        if (!popMgr) return true; // fail-open

        int needed    = Mathf.Max(1, _unit.populationToTrain * _multiplier);
        int available = popMgr.GetAvailableTaskPopulation();
        return available >= needed;
    }

    public void RefreshTutorialBypassState()
    {
        UpdateConfirmButtonState();
        RefreshCostButtonColor();
    }

    public void SetTutorialMultiplier(int value)
    {
        _multiplier = Mathf.Clamp(value, 1, maxMultiplier);
        if (multiplierText) multiplierText.text = _multiplier.ToString();
        PopulateInfo();
        UpdateConfirmButtonState();
        RefreshCostButtonColor();
    }

    private void RefreshCostButtonColor()
    {
        if (costsButton == null) return;

        var img = costsButton.image;
        if (img == null) return;

        bool canAfford = CanAffordTrainingCosts();
        img.color = canAfford ? Color.white : Color.red;
    }

    private void UpdateConfirmButtonState()
    {
        if (confirmOrderButton == null) return;

        bool hasSlot   = _ownerControl != null && _ownerControl.HasFreeTrainingSlot();
        bool hasPop    = HasEnoughPopulation();
        bool hasRes    = CanAffordTrainingCosts();

        bool canOrder = hasSlot && hasPop && hasRes;
        confirmOrderButton.interactable = canOrder;

        // Optional visual feedback:
        var img = confirmOrderButton.image;
        if (img != null)
            img.color = canOrder ? Color.white : Color.grey;
    }

    // -------------------------------------------------
    // Confirm Order
    // -------------------------------------------------

    private void ConfirmOrder()
    {
        if (_ownerControl == null || _unit == null) return;

        // Safety check — should already be enforced by UpdateConfirmButtonState.
        if (!HasEnoughPopulation() || !CanAffordTrainingCosts() || !_ownerControl.HasFreeTrainingSlot())
        {
            //Debug.Log("[UnitOrderItemUI] ConfirmOrder blocked: requirements not met.");

            // If fail is due to pop, auto-open info panel like crafting does.
            if (!HasEnoughPopulation() && infoPanelRoot && !infoPanelRoot.activeSelf)
            {
                infoPanelRoot.SetActive(true);
                PopulateInfo();
            }

            // If fail is due to resources, auto-open cost panel.
            if (!CanAffordTrainingCosts() && costPanelRoot && !costPanelRoot.activeSelf)
            {
                costPanelRoot.SetActive(true);
                RebuildCostView();
            }

            UpdateConfirmButtonState();
            RefreshCostButtonColor();
            return;
        }

        if (!_ownerControl.TryStartTraining(_unit, _multiplier, out string failReason))
        {
            //Debug.LogWarning($"[UnitOrderItemUI] Failed to start training: {failReason}");
            UpdateConfirmButtonState();
            RefreshCostButtonColor();
            return;
        }

        OnOrderConfirmed?.Invoke();

        // After a successful order, state will change as turns pass / resources are spent.
        UpdateConfirmButtonState();
        RefreshCostButtonColor();
    }
}
