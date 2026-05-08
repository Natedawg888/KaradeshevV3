using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ProductionPlanItem : MonoBehaviour
{
    [Header("Main")]
    public Image icon;
    public TMP_Text titleText;

    [Header("Info")]
    public TMP_Text populationText;
    public TMP_Text turnsText;
    public TMP_Text cooldownText;   // NEW

    [Header("Costs / Output UI")]
    public TMP_Text costLabel;
    public Button costsButton;
    public GameObject costPanelRoot;
    public Transform costContentParent;
    public BuildingCostEntry costEntryPrefab;
    public Button closeCostsButton;
    public Button outputButton;
    public BuildingRewardEntry rewardEntryPrefab;

    [Header("Environment/Tile Icons")]
    public EnvironmentIconLibrary iconLibrary;

    [Header("Environment Type UI")]
    public Transform envTypeContentRoot;
    public EnvironmentTypeEntry envTypeEntryPrefab;

    [Header("Tile Type UI")]
    public Transform tileTypeContentRoot;
    public TileTypeEntry tileTypeEntryPrefab;

    [Header("Cost Sets UI (Optional)")]
    public Button prevCostSetButton;
    public Button nextCostSetButton;

    [Header("Non-Extractor UI")]
    public GameObject importInfoRoot;  // bloke + text container
    public TMP_Text importInfoText;    // "Import goods"

    [Header("Start Production")]
    public Button startProductionButton;   // assign in Inspector

    // --- runtime ---
    private ProductionPlan _plan;
    private bool _showingOutput = false;

    // which building this plan item belongs to:
    private ProductionBuildingControl _productionBuilding;

    // OLD signature kept for safety if something else still calls it
    public void Bind(ProductionPlan plan)
    {
        Bind(plan, null);
    }

    // NEW main bind: plan + building
    public void Bind(ProductionPlan plan, ProductionBuildingControl productionBuilding)
    {
        _plan = plan;
        _productionBuilding = productionBuilding;

        if (_plan == null)
            return;

        // Default indexes for sets
        if (_plan.HasAlternateRunningCostSets && _plan.activeRunningCostSetIndex == -1)
            _plan.activeRunningCostSetIndex = 0;

        if (_plan.HasAlternateOutputSets && _plan.activeOutputSetIndex == -1)
            _plan.activeOutputSetIndex = 0;

        // Basic visuals
        if (icon)
        {
            icon.sprite = _plan.productionIcon;
            icon.enabled = _plan.productionIcon != null;
        }

        if (titleText)
            titleText.text = string.IsNullOrWhiteSpace(_plan.planName)
                ? _plan.productionID
                : _plan.planName;

        if (populationText)
            populationText.text = $"Pop: {_plan.requiredPopulation}";

        if (turnsText)
            turnsText.text = $"Turns: {_plan.requiredTurnsPerCycle}";

        RefreshCooldownText(); // NEW

        // Wire buttons
        WireCostsAndOutputUI();
        WireCostSetButtons();
        WireStartProductionButton();

        // Extraction / Import UI
        SetupExtractionUI();

        PopulateEnvTypes();
        PopulateTileTypes();

        RefreshCostsButtonColor();
        RefreshOutputButtonColor();
        RefreshStartButtonState();
    }

    // ----------------- Cooldown UI -----------------
    private void RefreshCooldownText()
    {
        if (cooldownText == null || _plan == null)
            return;

        if (_plan.UsesCycleCooldown)
        {
            int activeCycles = _plan.GetCyclesBeforeCooldown();
            int cooldownCycles = _plan.GetCooldownCycles();
            int cooldownTurns = _plan.GetCooldownTurns();

            cooldownText.gameObject.SetActive(true);
            cooldownText.text = $"Cooldown: {activeCycles} on / {cooldownCycles} off ({cooldownTurns} turns)";
        }
        else
        {
            cooldownText.gameObject.SetActive(true);
            cooldownText.text = "Cooldown: None";
        }
    }

    // ----------------- Start Production -----------------
    private void WireStartProductionButton()
    {
        if (!startProductionButton) return;

        startProductionButton.onClick.RemoveAllListeners();
        startProductionButton.onClick.AddListener(() =>
        {
            if (_plan == null || _productionBuilding == null)
                return;

            if (!HasEnoughPopulation() || !CanAffordPlanResources())
            {
                //Debug.Log("[ProductionPlanItem] Start blocked: not enough pop or resources.");
                return;
            }

            if (_plan.isExternalExtractor)
            {
                ProductionSelectionController.BeginSelection(_productionBuilding, _plan);
            }
            else
            {
                _productionBuilding.StartProduction(_plan);
            }
        });

        startProductionButton.gameObject.SetActive(_plan != null);
        RefreshStartButtonState();
    }

    // ----------------- helpers for gating start button -----------------
    private bool CanAffordPlanResources()
    {
        if (_plan == null) return true;

        var costs = _plan.GetActiveRunningCosts();
        if (costs == null || costs.Count == 0)
            return true;

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

    private bool HasEnoughPopulation()
    {
        if (_plan == null) return true;

        var popMgr = PlayersPopulationManager.Instance;
        if (!popMgr) return true;

        int needed = Mathf.Max(1, _plan.requiredPopulation);
        int available = popMgr.GetAvailableTaskPopulation();
        return available >= needed;
    }

    private void RefreshStartButtonState()
    {
        if (startProductionButton == null || _plan == null)
            return;

        bool canAfford = CanAffordPlanResources();
        bool hasPop = HasEnoughPopulation();
        bool canStart = canAfford && hasPop;

        startProductionButton.interactable = canStart;

        var img = startProductionButton.image;
        if (img != null)
            img.color = canStart ? Color.white : Color.grey;
    }

    // ----------------- Costs / Output panel -----------------
    private void WireCostsAndOutputUI()
    {
        if (costPanelRoot)
            costPanelRoot.SetActive(false);

        if (costsButton)
        {
            costsButton.onClick.RemoveAllListeners();
            costsButton.onClick.AddListener(ToggleCostsPanel);
        }

        if (closeCostsButton)
        {
            closeCostsButton.onClick.RemoveAllListeners();
            closeCostsButton.onClick.AddListener(HideCostsPanel);
        }

        if (outputButton)
        {
            outputButton.onClick.RemoveAllListeners();
            outputButton.onClick.AddListener(ToggleOutputView);
        }

        _showingOutput = false;
        UpdateOutputButtonLabel();
    }

    private void ToggleCostsPanel()
    {
        if (!costPanelRoot) return;
        bool show = !costPanelRoot.activeSelf;
        costPanelRoot.SetActive(show);

        if (show)
        {
            if (_showingOutput) PopulateOutputs();
            else PopulateCosts();
        }
        else
        {
            ClearCosts();
        }
    }

    private void HideCostsPanel()
    {
        if (!costPanelRoot) return;
        costPanelRoot.SetActive(false);
        ClearCosts();
    }

    private void ToggleOutputView()
    {
        if (!costPanelRoot) return;

        _showingOutput = !_showingOutput;
        UpdateOutputButtonLabel();

        if (costPanelRoot.activeSelf)
        {
            if (_showingOutput) PopulateOutputs();
            else PopulateCosts();
        }

        RefreshOutputButtonColor();
    }

    private void UpdateOutputButtonLabel()
    {
        if (!costLabel) return;
        costLabel.text = _showingOutput ? "Show Output" : "Show Costs";
    }

    // ----------------- Cost sets -----------------
    private void WireCostSetButtons()
    {
        bool hasAnySets = _plan != null && (_plan.HasAlternateRunningCostSets || _plan.HasAlternateOutputSets);

        if (prevCostSetButton)
        {
            prevCostSetButton.onClick.RemoveAllListeners();
            prevCostSetButton.onClick.AddListener(() =>
            {
                if (_plan == null) return;

                if (_showingOutput)
                {
                    if (_plan.HasAlternateOutputSets)
                        _plan.CyclePrevOutputSet();
                }
                else
                {
                    if (_plan.HasAlternateRunningCostSets)
                        _plan.CyclePrevRunningCostSet();
                }

                if (costPanelRoot && costPanelRoot.activeSelf)
                {
                    if (_showingOutput) PopulateOutputs();
                    else PopulateCosts();
                }

                RefreshCostsButtonColor();
                RefreshOutputButtonColor();
                RefreshStartButtonState();
            });

            prevCostSetButton.gameObject.SetActive(hasAnySets);
        }

        if (nextCostSetButton)
        {
            nextCostSetButton.onClick.RemoveAllListeners();
            nextCostSetButton.onClick.AddListener(() =>
            {
                if (_plan == null) return;

                if (_showingOutput)
                {
                    if (_plan.HasAlternateOutputSets)
                        _plan.CycleNextOutputSet();
                }
                else
                {
                    if (_plan.HasAlternateRunningCostSets)
                        _plan.CycleNextRunningCostSet();
                }

                if (costPanelRoot && costPanelRoot.activeSelf)
                {
                    if (_showingOutput) PopulateOutputs();
                    else PopulateCosts();
                }

                RefreshCostsButtonColor();
                RefreshOutputButtonColor();
                RefreshStartButtonState();
            });

            nextCostSetButton.gameObject.SetActive(hasAnySets);
        }
    }

    private void RefreshCostsButtonColor()
    {
        if (costsButton == null || _plan == null)
            return;

        bool canAfford = CanAffordPlanResources();

        var img = costsButton.image;
        if (img != null)
            img.color = canAfford ? Color.green : Color.red;
    }

    private void RefreshOutputButtonColor()
    {
        if (outputButton == null)
            return;

        var img = outputButton.image;
        if (img == null)
            return;

        if (_plan == null || _showingOutput)
        {
            img.color = Color.white;
            return;
        }

        bool canAfford = CanAffordPlanResources();
        img.color = canAfford ? Color.green : Color.red;
    }

    // ----------------- Extraction vs Import UI -----------------
    private void SetupExtractionUI()
    {
        bool usesTiles = _plan != null && _plan.isExternalExtractor;

        if (envTypeContentRoot)
            envTypeContentRoot.gameObject.SetActive(usesTiles);
        if (tileTypeContentRoot)
            tileTypeContentRoot.gameObject.SetActive(usesTiles);

        if (importInfoRoot)
            importInfoRoot.SetActive(!usesTiles);

        if (!usesTiles && importInfoText)
            importInfoText.text = "Imported goods needed";
    }

    // ----------------- Costs -----------------
    private void PopulateCosts()
    {
        if (!costContentParent || !costEntryPrefab || _plan == null) return;

        ClearCosts();

        var inv = PlayerInventoryManager.Instance;
        var costs = _plan.GetActiveRunningCosts();

        if (costs == null) return;

        foreach (var c in costs)
        {
            if (c == null || c.resource == null) continue;

            int owned = inv != null ? inv.GetAmount(c.resource) : 0;

            var entry = Instantiate(costEntryPrefab, costContentParent);
            entry.Bind(c.resource, c.amountPerCycle, owned);
        }
    }

    private void ClearCosts()
    {
        if (!costContentParent) return;
        for (int i = costContentParent.childCount - 1; i >= 0; i--)
            Destroy(costContentParent.GetChild(i).gameObject);
    }

    // ----------------- Outputs -----------------
    private void PopulateOutputs()
    {
        if (!costContentParent || !rewardEntryPrefab || _plan == null) return;

        ClearCosts();

        var outs = _plan.GetSeasonAdjustedOutputs();
        if (outs == null) return;

        foreach (var o in outs)
        {
            if (o == null || o.resource == null || o.amountPerCycle <= 0) continue;

            var entry = Instantiate(rewardEntryPrefab, costContentParent);
            entry.Bind(o.resource, o.amountPerCycle);
        }
    }

    // ----------------- Environment / Tile types -----------------
    private void PopulateEnvTypes()
    {
        if (!envTypeContentRoot || !envTypeEntryPrefab || _plan == null) return;
        if (!_plan.isExternalExtractor) return;

        for (int i = envTypeContentRoot.childCount - 1; i >= 0; i--)
            Destroy(envTypeContentRoot.GetChild(i).gameObject);

        if (_plan.allowedEnvironmentTypes == null || _plan.allowedEnvironmentTypes.Length == 0)
            return;

        foreach (var envType in _plan.allowedEnvironmentTypes)
        {
            var entry = Instantiate(envTypeEntryPrefab, envTypeContentRoot);
            Sprite icon = iconLibrary ? iconLibrary.GetEnvIcon(envType) : null;
            entry.Bind(envType, icon);
        }
    }

    private void PopulateTileTypes()
    {
        if (!tileTypeContentRoot || !tileTypeEntryPrefab || _plan == null) return;
        if (!_plan.isExternalExtractor) return;

        for (int i = tileTypeContentRoot.childCount - 1; i >= 0; i--)
            Destroy(tileTypeContentRoot.GetChild(i).gameObject);

        if (_plan.allowedTileTypes == null || _plan.allowedTileTypes.Length == 0)
            return;

        foreach (var tileType in _plan.allowedTileTypes)
        {
            var entry = Instantiate(tileTypeEntryPrefab, tileTypeContentRoot);
            Sprite icon = iconLibrary ? iconLibrary.GetTileIcon(tileType) : null;
            entry.Bind(tileType, icon);
        }
    }
}
