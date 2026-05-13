using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Detail panel for a production plan in the tech panel.
/// Toggles between running costs (with cost-set cycling) and outputs (with
/// output-set cycling). A separate section lists buildings that support the plan.
/// </summary>
public class TechProductionDetailPanel : MonoBehaviour
{
    [Header("Root")]
    public GameObject root;
    public Button closeButton;

    [Header("Header")]
    public Image icon;
    public TMP_Text nameText;

    [Header("Stats")]
    public TMP_Text turnsText;
    public TMP_Text populationText;
    [Tooltip("Number of cycles that run before the cooldown triggers (0 = no cooldown).")]
    public TMP_Text cyclesBeforeCooldownText;
    [Tooltip("Duration of the cooldown in turns (GetCooldownTurns).")]
    public TMP_Text cooldownTurnsText;

    [Header("Content Area")]
    public Transform contentRoot;
    public BuildingCostEntry costEntryPrefab;
    [Tooltip("Label showing 'Costs' or 'Outputs'.")]
    public TMP_Text contentModeText;

    [Header("Cost Set Cycling")]
    [Tooltip("Parent shown only while viewing costs. Contains prev/next buttons and label.")]
    public GameObject costSetGroup;
    public Button prevCostSetButton;
    public Button nextCostSetButton;
    public TMP_Text costSetLabelText;

    [Header("View Toggle")]
    [Tooltip("Switches the content area between costs and outputs.")]
    public Button toggleButton;

    [Header("Output Set Cycling")]
    [Tooltip("Parent shown only while viewing outputs. Contains prev/next buttons and label.")]
    public GameObject outputSetGroup;
    public Button prevOutputSetButton;
    public Button nextOutputSetButton;
    public TMP_Text outputSetLabelText;

    [Header("Compatible Buildings")]
    public Transform buildingsContentRoot;
    public TechBuildingEntryUI buildingEntryPrefab;

    private ProductionPlan _plan;
    private bool _showOutputs;
    private readonly List<GameObject> _entries         = new();
    private readonly List<GameObject> _buildingEntries = new();

    private void Awake()
    {
        if (closeButton)        closeButton.onClick.AddListener(Hide);
        if (prevCostSetButton)  prevCostSetButton.onClick.AddListener(OnPrevCostSet);
        if (nextCostSetButton)  nextCostSetButton.onClick.AddListener(OnNextCostSet);
        if (toggleButton)       toggleButton.onClick.AddListener(OnToggle);
        if (prevOutputSetButton) prevOutputSetButton.onClick.AddListener(OnPrevOutputSet);
        if (nextOutputSetButton) nextOutputSetButton.onClick.AddListener(OnNextOutputSet);
        if (root) root.SetActive(false);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void ShowFor(ProductionPlan plan)
    {
        if (plan == null) { Hide(); return; }
        _plan        = plan;
        _showOutputs = false;

        ClearEntries();
        ClearBuildingEntries();

        if (root) root.SetActive(true);
        gameObject.SetActive(true);

        if (nameText) nameText.text = plan.planName ?? plan.productionID;
        if (icon)
        {
            icon.sprite  = plan.productionIcon;
            icon.enabled = plan.productionIcon != null;
        }

        if (turnsText)      turnsText.text      = plan.requiredTurnsPerCycle.ToString();
        if (populationText) populationText.text  = plan.requiredPopulation.ToString();

        if (cyclesBeforeCooldownText)
            cyclesBeforeCooldownText.text = plan.UsesCycleCooldown
                ? plan.GetCyclesBeforeCooldown().ToString()
                : "—";

        if (cooldownTurnsText)
            cooldownTurnsText.text = plan.UsesCycleCooldown
                ? plan.GetCooldownTurns().ToString()
                : "—";

        RefreshContent();
        PopulateCompatibleBuildings();
    }

    public void Hide()
    {
        ClearEntries();
        ClearBuildingEntries();
        _plan = null;
        if (root) root.SetActive(false);
    }

    // ── Toggle ────────────────────────────────────────────────────────────────

    private void OnToggle()
    {
        _showOutputs = !_showOutputs;
        RefreshContent();
    }

    // ── Cost set cycling ──────────────────────────────────────────────────────

    private void OnPrevCostSet()
    {
        _plan?.CyclePrevRunningCostSet();
        RefreshContent();
    }

    private void OnNextCostSet()
    {
        _plan?.CycleNextRunningCostSet();
        RefreshContent();
    }

    // ── Output set cycling ────────────────────────────────────────────────────

    private void OnPrevOutputSet()
    {
        _plan?.CyclePrevOutputSet();
        RefreshContent();
    }

    private void OnNextOutputSet()
    {
        _plan?.CycleNextOutputSet();
        RefreshContent();
    }

    // ── Content ───────────────────────────────────────────────────────────────

    private void RefreshContent()
    {
        ClearEntries();
        if (_plan == null) return;

        if (contentModeText) contentModeText.text = _showOutputs ? "Outputs" : "Costs";

        bool hasCostSets   = _plan.HasAlternateRunningCostSets;
        bool hasOutputSets = _plan.HasAlternateOutputSets;

        if (costSetGroup)   costSetGroup.SetActive(!_showOutputs && hasCostSets);
        if (outputSetGroup) outputSetGroup.SetActive(_showOutputs && hasOutputSets);

        if (_showOutputs)
            PopulateOutputs();
        else
            PopulateCosts();
    }

    private void PopulateCosts()
    {
        if (costEntryPrefab == null || contentRoot == null) return;

        if (costSetLabelText)
            costSetLabelText.text = _plan.HasAlternateRunningCostSets
                ? _plan.GetActiveRunningCostSetLabel()
                : string.Empty;

        foreach (var cost in _plan.GetActiveRunningCosts())
        {
            if (cost?.resource == null) continue;
            int owned = InventoryQuery.GetOwned(cost.resource);
            var entry = Instantiate(costEntryPrefab, contentRoot);
            entry.Bind(cost.resource, cost.amountPerCycle, owned);
            _entries.Add(entry.gameObject);
        }
    }

    private void PopulateOutputs()
    {
        if (costEntryPrefab == null || contentRoot == null) return;

        if (outputSetLabelText)
            outputSetLabelText.text = _plan.HasAlternateOutputSets
                ? _plan.GetActiveOutputSetLabel()
                : string.Empty;

        foreach (var output in _plan.GetActiveOutputs())
        {
            if (output?.resource == null) continue;
            var entry = Instantiate(costEntryPrefab, contentRoot);
            entry.Bind(output.resource, output.amountPerCycle, output.amountPerCycle);
            if (entry.haveText) entry.haveText.gameObject.SetActive(false);
            _entries.Add(entry.gameObject);
        }
    }

    // ── Compatible buildings ──────────────────────────────────────────────────

    private void PopulateCompatibleBuildings()
    {
        if (buildingEntryPrefab == null || buildingsContentRoot == null || _plan == null) return;

        var knownMgr = PlayerKnownBuildingsManager.Instance;
        if (knownMgr == null) return;

        foreach (var building in knownMgr.GetKnownBuildings())
        {
            if (building?.finalBuildingPrefab == null) continue;

            var ctrl = building.finalBuildingPrefab.GetComponent<ProductionBuildingControl>();
            if (ctrl == null || !ctrl.allowedPlanIDs.Contains(_plan.productionID)) continue;

            var entry = Instantiate(buildingEntryPrefab, buildingsContentRoot);
            entry.Bind(building, null);
            _buildingEntries.Add(entry.gameObject);
        }
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────

    private void ClearEntries()
    {
        for (int i = _entries.Count - 1; i >= 0; i--)
            if (_entries[i] != null) Destroy(_entries[i]);
        _entries.Clear();
    }

    private void ClearBuildingEntries()
    {
        for (int i = _buildingEntries.Count - 1; i >= 0; i--)
            if (_buildingEntries[i] != null) Destroy(_buildingEntries[i]);
        _buildingEntries.Clear();
    }
}
