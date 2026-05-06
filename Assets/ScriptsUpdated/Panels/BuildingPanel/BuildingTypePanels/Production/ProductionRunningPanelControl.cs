using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ProductionRunningPanelControl : MonoBehaviour
{
    [Header("Roots")]
    public GameObject root;
    public Button closeButton;

    [Header("Header")]
    public TMP_Text buildingNameText;
    public TMP_Text planNameText;
    public Image planIconImage;
    public TMP_Text cycleInfoText;      // e.g. "Turns left: X"
    public TMP_Text cooldownInfoText;   // NEW

    [Header("Pause State UI")]
    [Tooltip("Shown when production is paused (manual or lack of resources).")]
    public GameObject pausedIconRoot;

    [Header("Details Panel (Costs / Outputs)")]
    public GameObject detailsRoot;
    public Button openDetailsButton;
    public Button closeDetailsButton;
    public Transform detailsContentRoot;
    public BuildingCostEntry costEntryPrefab;
    public BuildingRewardEntry rewardEntryPrefab;

    [Header("Toggle & Cost Sets (live on detailsRoot)")]
    public Button toggleCostsOutputsButton;
    public TMP_Text toggleCostsOutputsLabel;

    public Button prevCostSetButton;
    public Button nextCostSetButton;

    [Header("Controls")]
    public Button restartButton;  // restart/resume production
    public Button pauseButton;    // manual pause
    public Button cancelButton;   // cancel plan & clear tiles

    public Button repickTilesButton;

    [Header("Optional Fallbacks")]
    [SerializeField] private BuildingPanelControl defaultParentPanel;

    // --- runtime ---
    private BuildingPanelControl _parentPanel;
    private BuildingControl _building;
    private ProductionBuildingControl _production;
    private TileControl _tile;

    // true  = currently showing running costs in details panel
    // false = currently showing outputs in details panel
    private bool _showingCosts = true;

    [SerializeField] private ProductionRunningTutorial productionRunningTutorial;

    private void Awake()
    {
        if (root != null)
            root.SetActive(false);

        // Details panel is hidden until explicitly opened via openDetailsButton.
        if (detailsRoot != null)
            detailsRoot.SetActive(false);

        // Pause icon hidden by default
        if (pausedIconRoot != null)
            pausedIconRoot.SetActive(false);

        if (closeButton)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }

        if (openDetailsButton)
        {
            openDetailsButton.onClick.RemoveAllListeners();
            openDetailsButton.onClick.AddListener(OnOpenDetailsClicked);
        }

        if (closeDetailsButton)
        {
            closeDetailsButton.onClick.RemoveAllListeners();
            closeDetailsButton.onClick.AddListener(OnCloseDetailsClicked);
        }

        if (toggleCostsOutputsButton)
        {
            toggleCostsOutputsButton.onClick.RemoveAllListeners();
            toggleCostsOutputsButton.onClick.AddListener(OnToggleCostsOutputsClicked);
        }

        if (prevCostSetButton)
        {
            prevCostSetButton.onClick.RemoveAllListeners();
            prevCostSetButton.onClick.AddListener(OnPrevCostSetClicked);
        }

        if (nextCostSetButton)
        {
            nextCostSetButton.onClick.RemoveAllListeners();
            nextCostSetButton.onClick.AddListener(OnNextCostSetClicked);
        }

        if (restartButton)
        {
            restartButton.onClick.RemoveAllListeners();
            restartButton.onClick.AddListener(OnRestartClicked);
        }

        if (pauseButton)
        {
            pauseButton.onClick.RemoveAllListeners();
            pauseButton.onClick.AddListener(OnPauseClicked);
        }

        if (cancelButton)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(OnCancelClicked);
        }

        if (repickTilesButton)
        {
            repickTilesButton.onClick.RemoveAllListeners();
            repickTilesButton.onClick.AddListener(OnRepickTilesClicked);
        }
    }

    public void OpenFor(BuildingControl building, BuildingPanelControl parent, TileControl tile)
    {
        _parentPanel = parent != null ? parent : defaultParentPanel;
        _building = building;
        _production = building ? building.GetComponent<ProductionBuildingControl>() : null;
        _tile = tile != null ? tile : building.GetComponentInParent<TileControl>();

        if (_production == null || !_production.HasActivePlan)
        {
            Debug.LogWarning("[ProductionRunningPanel] OpenFor called but building has no active production plan.");
            return;
        }

        _showingCosts = true; // default to running costs first

        RefreshHeader();
        RefreshToggleLabel();

        if (detailsRoot != null)
            detailsRoot.SetActive(false);

        RefreshCostButtonColors();
        RefreshControlButtons();

        if (repickTilesButton != null)
        {
            var plan = _production.ActivePlan;
            bool canRepick = (plan != null && plan.isExternalExtractor);
            repickTilesButton.gameObject.SetActive(canRepick);
            repickTilesButton.interactable = canRepick;
        }

        if (root) root.SetActive(true);

        if (productionRunningTutorial != null && productionRunningTutorial.ShouldRunTutorial())
            productionRunningTutorial.BeginTutorial();
    }

    public void Hide()
    {
        if (root) root.SetActive(false);

        if (detailsRoot != null)
            detailsRoot.SetActive(false);

        _parentPanel?.SoftShowFromChild();
    }

    // ----------------- UI REFRESH -----------------

    private void RefreshHeader()
    {
        if (_building == null || _production == null)
            return;

        var plan = _production.ActivePlan;
        if (plan == null)
            return;

        // Building name
        if (buildingNameText != null)
        {
            string name = !string.IsNullOrWhiteSpace(_building.buildingName)
                ? _building.buildingName
                : (_building.buildingID ?? "Building");

            if (_tile != null)
            {
                var gp = _tile.GetGridPosition();
                buildingNameText.text = $"{name}";
            }
            else
            {
                buildingNameText.text = name;
            }
        }

        // Plan name
        if (planNameText != null)
        {
            string pName = !string.IsNullOrWhiteSpace(plan.planName)
                ? plan.planName
                : plan.productionID;
            planNameText.text = pName;
        }

        // Icon
        if (planIconImage != null)
        {
            planIconImage.sprite = plan.productionIcon;
            planIconImage.enabled = (plan.productionIcon != null);
        }

        // Turn / status info
        if (cycleInfoText != null)
        {
            int turnsLeft = _production.TurnsLeftInCycle;
            int total = Mathf.Max(1, plan.requiredTurnsPerCycle);

            if (_production.IsCoolingDown)
            {
                cycleInfoText.text = $"Cooling Down: {_production.CooldownTurnsLeft}";
            }
            else if (_production.IsPaused)
            {
                cycleInfoText.text = "Status: Paused";
            }
            else if (_production.IsProducing)
            {
                cycleInfoText.text = $"{turnsLeft}/{total}";
            }
            else
            {
                cycleInfoText.text = "Status: Idle";
            }
        }

        // NEW: cooldown rules text
        if (cooldownInfoText != null)
        {
            if (plan.UsesCycleCooldown)
            {
                int activeCycles = plan.GetCyclesBeforeCooldown();
                int cooldownCycles = plan.GetCooldownCycles();
                int cooldownTurns = plan.GetCooldownTurns();

                cooldownInfoText.gameObject.SetActive(true);
                cooldownInfoText.text = $"Cooldown: {activeCycles} on / {cooldownCycles} off ({cooldownTurns} turns)";
            }
            else
            {
                cooldownInfoText.gameObject.SetActive(true);
                cooldownInfoText.text = "Cooldown: None";
            }
        }

        // paused icon visibility
        if (pausedIconRoot != null)
            pausedIconRoot.SetActive(_production.IsPaused);

        RefreshToggleLabel();
        RefreshCostButtonColors();
        RefreshControlButtons();
    }

    private void RefreshToggleLabel()
    {
        if (toggleCostsOutputsLabel == null) return;

        toggleCostsOutputsLabel.text = _showingCosts
            ? "Show Running Costs"
            : "Show Outputs";
    }

    private void RefreshDetails()
    {
        if (detailsContentRoot == null || _production == null || _production.ActivePlan == null)
            return;

        var plan = _production.ActivePlan;

        for (int i = detailsContentRoot.childCount - 1; i >= 0; i--)
            Destroy(detailsContentRoot.GetChild(i).gameObject);

        if (_showingCosts)
        {
            if (costEntryPrefab == null)
            {
                Debug.LogWarning("[ProductionRunningPanel] No costEntryPrefab assigned.");
                return;
            }

            var costs = plan.GetActiveRunningCosts();
            if (costs == null) return;

            foreach (var c in costs)
            {
                if (c == null || c.resource == null || c.amountPerCycle <= 0) continue;

                int owned = InventoryQuery.GetOwned(c.resource);

                var entry = Instantiate(costEntryPrefab, detailsContentRoot);
                entry.Bind(c.resource, c.amountPerCycle, owned);
            }
        }
        else
        {
            if (rewardEntryPrefab == null)
            {
                Debug.LogWarning("[ProductionRunningPanel] No rewardEntryPrefab assigned.");
                return;
            }

            var outs = plan.GetSeasonAdjustedOutputs();
            if (outs == null) return;

            foreach (var o in outs)
            {
                if (o == null || o.resource == null || o.amountPerCycle <= 0) continue;

                var entry = Instantiate(rewardEntryPrefab, detailsContentRoot);
                entry.Bind(o.resource, o.amountPerCycle);
            }
        }
    }

    // -------- affordability + button colours --------

    private bool CanAffordCurrentRunningCosts()
    {
        if (_production == null || !_production.HasActivePlan)
            return true;

        var plan = _production.ActivePlan;
        var costs = plan.GetActiveRunningCosts();
        if (costs == null) return true;

        foreach (var c in costs)
        {
            if (c == null || c.resource == null || c.amountPerCycle <= 0)
                continue;

            int owned = InventoryQuery.GetOwned(c.resource);
            if (owned < c.amountPerCycle)
                return false;
        }

        return true;
    }

    private void RefreshCostButtonColors()
    {
        if (!_showingCosts)
        {
            if (openDetailsButton && openDetailsButton.image)
                openDetailsButton.image.color = Color.white;

            if (toggleCostsOutputsButton && toggleCostsOutputsButton.image)
                toggleCostsOutputsButton.image.color = Color.white;

            return;
        }

        bool canAfford = CanAffordCurrentRunningCosts();

        if (openDetailsButton && openDetailsButton.image)
            openDetailsButton.image.color = canAfford ? Color.green : Color.red;

        if (toggleCostsOutputsButton && toggleCostsOutputsButton.image)
            toggleCostsOutputsButton.image.color = canAfford ? Color.green : Color.red;
    }

    private void RefreshControlButtons()
    {
        if (restartButton == null && pauseButton == null)
            return;

        if (_production == null || !_production.HasActivePlan)
        {
            if (restartButton) restartButton.interactable = false;
            if (pauseButton) pauseButton.interactable = false;
            return;
        }

        bool isProducing = _production.IsProducing;
        bool isPaused = _production.IsPaused;
        bool isCooling = _production.IsCoolingDown;

        if (restartButton)
            restartButton.interactable = !isProducing && !isCooling;

        if (pauseButton)
            pauseButton.interactable = isProducing && !isPaused && !isCooling;
    }

    // ----------------- BUTTON HANDLERS -----------------

    private void OnOpenDetailsClicked()
    {
        if (_production == null || _production.ActivePlan == null)
            return;

        if (detailsRoot != null)
            detailsRoot.SetActive(true);

        RefreshDetails();
        RefreshToggleLabel();
        RefreshCostButtonColors();
    }

    private void OnCloseDetailsClicked()
    {
        if (detailsRoot != null)
            detailsRoot.SetActive(false);
    }

    private void OnToggleCostsOutputsClicked()
    {
        _showingCosts = !_showingCosts;
        RefreshDetails();
        RefreshToggleLabel();
        RefreshCostButtonColors();
    }

    private void OnPrevCostSetClicked()
    {
        if (_production == null || !_production.HasActivePlan) return;

        bool changed = _showingCosts
            ? _production.CyclePrevRunningCostSet()
            : _production.CyclePrevOutputSet();

        if (!changed) return;

        if (detailsRoot != null && detailsRoot.activeSelf)
            RefreshDetails();

        RefreshHeader();
        RefreshCostButtonColors();
    }

    private void OnNextCostSetClicked()
    {
        if (_production == null || !_production.HasActivePlan) return;

        bool changed = _showingCosts
            ? _production.CycleNextRunningCostSet()
            : _production.CycleNextOutputSet();

        if (!changed) return;

        if (detailsRoot != null && detailsRoot.activeSelf)
            RefreshDetails();

        RefreshHeader();
        RefreshCostButtonColors();
    }

    private void OnRestartClicked()
    {
        if (_production == null || _production.ActivePlan == null) return;

        var plan = _production.ActivePlan;
        _production.StartProduction(plan);

        RefreshHeader();
        RefreshCostButtonColors();
    }

    private void OnPauseClicked()
    {
        if (_production == null || _production.ActivePlan == null) return;

        _production.PauseProductionManual();
        RefreshHeader();
        RefreshCostButtonColors();
    }

    private void OnCancelClicked()
    {
        if (_production == null || _production.ActivePlan == null) return;

        _production.CancelCurrentPlan();
        Hide();
    }

    private void OnRepickTilesClicked()
    {
        if (_production == null || _production.ActivePlan == null)
            return;

        var plan = _production.ActivePlan;
        if (!plan.isExternalExtractor)
        {
            Debug.LogWarning("[ProductionRunningPanel] Re-pick tiles clicked, but active plan is not an external extractor.");
            return;
        }

        _production.PauseProductionManual();
        Hide();
        ProductionSelectionController.BeginSelection(_production, plan);
    }

    public void InstallRuntimeRefs(ProductionRunningTutorial newProductionRunningTutorial = null)
    {
        if (newProductionRunningTutorial != null)
            productionRunningTutorial = newProductionRunningTutorial;
    }
}