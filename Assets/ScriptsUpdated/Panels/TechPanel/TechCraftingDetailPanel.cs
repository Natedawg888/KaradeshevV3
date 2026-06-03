using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Detail panel for a crafting recipe in the tech panel.
/// Shows name, icon, turns, population requirements, max multiplier, and
/// toggles between input costs (with cost-set cycling) and output resources.
/// </summary>
public class TechCraftingDetailPanel : MonoBehaviour
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
    [Tooltip("Shows 'Scales' if population multiplies with batch count, 'Fixed' otherwise.")]
    public TMP_Text popScalesText;
    public TMP_Text maxMultiplierText;

    [Header("Content Area")]
    public Transform contentRoot;
    public BuildingCostEntry costEntryPrefab;
    [Tooltip("Label showing the current view mode — 'Costs' or 'Outputs'.")]
    public TMP_Text contentModeText;

    [Header("Cost Set Cycling")]
    [Tooltip("Parent GameObject containing prev/next buttons and label — hidden when viewing outputs.")]
    public GameObject costSetGroup;
    public Button prevCostSetButton;
    public Button nextCostSetButton;
    public TMP_Text costSetLabelText;

    [Header("Toggle Button")]
    [Tooltip("Switches content area between costs and outputs.")]
    public Button toggleButton;

    [Header("Compatible Buildings")]
    [Tooltip("Scroll content root for the buildings-that-can-craft-this list.")]
    public Transform buildingsContentRoot;
    public TechBuildingEntryUI buildingEntryPrefab;

    private CraftingRecipe _recipe;
    private bool _showOutputs;
    private readonly List<GameObject> _entries         = new();
    private readonly List<GameObject> _buildingEntries = new();

    private void Awake()
    {
        if (closeButton)       closeButton.onClick.AddListener(Hide);
        if (prevCostSetButton) prevCostSetButton.onClick.AddListener(OnPrevCostSet);
        if (nextCostSetButton) nextCostSetButton.onClick.AddListener(OnNextCostSet);
        if (toggleButton)      toggleButton.onClick.AddListener(OnToggle);
        if (root) root.SetActive(false);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void ShowFor(CraftingRecipe recipe)
    {
        if (recipe == null) { Hide(); return; }
        _recipe      = recipe;
        _showOutputs = false;

        ClearEntries();
        ClearBuildingEntries();

        if (root) root.SetActive(true);
        gameObject.SetActive(true);

        if (nameText) nameText.text = recipe.craftingName ?? recipe.craftingID;
        if (icon)
        {
            icon.sprite  = recipe.craftingIcon;
            icon.enabled = recipe.craftingIcon != null;
        }

        if (turnsText)         turnsText.text         = recipe.craftTurnsRequired.ToString();
        if (populationText)    populationText.text     = recipe.requiredPopulation.ToString();
        if (popScalesText)     popScalesText.text      = recipe.scalePopulationWithMultiplier ? "Scales" : "Fixed";
        if (maxMultiplierText) maxMultiplierText.text  = recipe.maxMultiplier.ToString();

        bool hasSets = recipe.HasAlternateCostSets;
        if (prevCostSetButton) prevCostSetButton.gameObject.SetActive(hasSets);
        if (nextCostSetButton) nextCostSetButton.gameObject.SetActive(hasSets);

        RefreshContent();
        PopulateCompatibleBuildings();
    }

    public void Hide()
    {
        ClearEntries();
        ClearBuildingEntries();
        _recipe = null;
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
        _recipe?.CyclePrevCostSet();
        RefreshContent();
    }

    private void OnNextCostSet()
    {
        _recipe?.CycleNextCostSet();
        RefreshContent();
    }

    // ── Content ───────────────────────────────────────────────────────────────

    private void RefreshContent()
    {
        ClearEntries();
        if (_recipe == null) return;

        bool showCostSets = !_showOutputs && _recipe.HasAlternateCostSets;
        if (costSetGroup) costSetGroup.SetActive(showCostSets);

        if (_showOutputs)
            PopulateOutputs();
        else
            PopulateCosts();
    }

    private void PopulateCosts()
    {
        if (costEntryPrefab == null || contentRoot == null) return;

        if (costSetLabelText)
            costSetLabelText.text = _recipe.HasAlternateCostSets
                ? _recipe.GetActiveCostSetLabel()
                : string.Empty;

        var costs = _recipe.GetActiveCosts();
        foreach (var cost in costs)
        {
            if (cost?.resource == null) continue;
            int owned = InventoryQuery.GetOwned(cost.resource);
            var entry = Instantiate(costEntryPrefab, contentRoot);
            entry.Bind(cost.resource, cost.amount, owned);
            _entries.Add(entry.gameObject);
        }
    }

    private void PopulateOutputs()
    {
        if (costEntryPrefab == null || contentRoot == null) return;

        var outputs = _recipe.outputResources;
        if (outputs == null) return;

        foreach (var output in outputs)
        {
            if (output?.resource == null) continue;
            var entry = Instantiate(costEntryPrefab, contentRoot);
            entry.Bind(output.resource, output.amount, output.amount);
            // "Have" column is meaningless for outputs — hide it
            if (entry.haveText) entry.haveText.gameObject.SetActive(false);
            _entries.Add(entry.gameObject);
        }
    }

    // ── Compatible buildings ──────────────────────────────────────────────────

    private void PopulateCompatibleBuildings()
    {
        if (buildingEntryPrefab == null || buildingsContentRoot == null || _recipe == null) return;

        var knownMgr = PlayerKnownBuildingsManager.Instance;
        if (knownMgr == null) return;

        foreach (var building in knownMgr.GetKnownBuildings())
        {
            if (building?.finalBuildingPrefab == null) continue;

            var ctrl = building.finalBuildingPrefab.GetComponent<CraftingBuildingControl>();
            if (ctrl == null || !ctrl.allowedRecipeIDs.Contains(_recipe.craftingID)) continue;

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
