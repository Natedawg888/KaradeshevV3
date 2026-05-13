using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Detail panel for a building in the tech panel.
/// Shows name, icon, allowed environment/tile types, tile size, build costs
/// (with cost-set cycling), population, turns, health, and degeneration info.
/// </summary>
public class TechBuildingDetailPanel : MonoBehaviour
{
    [Header("Root")]
    public GameObject root;
    public Button closeButton;

    [Header("Header")]
    public Image icon;
    public TMP_Text nameText;

    [Header("Placement")]
    public Transform envIconsRoot;
    public Transform tileIconsRoot;
    public EnvironmentTypeEntry envEntryPrefab;
    public TileTypeEntry tileEntryPrefab;
    public EnvironmentIconLibrary iconLibrary;
    public TMP_Text tileSizeText;

    [Header("Build Costs")]
    public Transform costContentRoot;
    public BuildingCostEntry costEntryPrefab;
    public Button prevCostSetButton;
    public Button nextCostSetButton;
    public TMP_Text costSetLabelText;

    [Header("Requirements")]
    public TMP_Text populationText;
    public TMP_Text turnsText;

    [Header("Health / Degeneration")]
    public TMP_Text healthText;
    public TMP_Text degenerationText;
    public TMP_Text degenerationIntervalText;

    private Building _building;
    private readonly List<GameObject> _placementEntries = new();
    private readonly List<GameObject> _costEntries      = new();

    private void Awake()
    {
        if (closeButton)      closeButton.onClick.AddListener(Hide);
        if (prevCostSetButton) prevCostSetButton.onClick.AddListener(OnPrevCostSet);
        if (nextCostSetButton) nextCostSetButton.onClick.AddListener(OnNextCostSet);
        if (root) root.SetActive(false);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void ShowFor(Building building)
    {
        if (building == null) { Hide(); return; }
        _building = building;

        ClearAll();

        if (root) root.SetActive(true);
        gameObject.SetActive(true);

        // Header
        if (nameText) nameText.text = building.buildingName ?? building.buildingID;
        if (icon)
        {
            icon.sprite  = building.buildingIcon;
            icon.enabled = building.buildingIcon != null;
        }

        // Tile size
        if (tileSizeText) tileSizeText.text = building.requiredTileSize.ToString();

        // Environment type icons
        if (envIconsRoot != null && envEntryPrefab != null && building.requiredEnvironmentTypes != null)
        {
            foreach (var envType in building.requiredEnvironmentTypes)
            {
                var entry = Instantiate(envEntryPrefab, envIconsRoot);
                entry.Bind(envType, iconLibrary != null ? iconLibrary.GetEnvIcon(envType) : null);
                _placementEntries.Add(entry.gameObject);
            }
        }

        // Tile type icons
        if (tileIconsRoot != null && tileEntryPrefab != null && building.requiredEnvironmentTileTypes != null)
        {
            foreach (var tileType in building.requiredEnvironmentTileTypes)
            {
                var entry = Instantiate(tileEntryPrefab, tileIconsRoot);
                entry.Bind(tileType, iconLibrary != null ? iconLibrary.GetTileIcon(tileType) : null);
                _placementEntries.Add(entry.gameObject);
            }
        }

        // Requirements
        if (populationText) populationText.text = building.requireBuildPopulation.ToString();
        if (turnsText)      turnsText.text      = building.buildTurnsRequired.ToString();

        // Health / degeneration
        if (healthText)             healthText.text             = building.defaultMaxHealth.ToString();
        if (degenerationText)       degenerationText.text       = building.defaultDegenerationAmount.ToString();
        if (degenerationIntervalText) degenerationIntervalText.text = building.defaultDegenerationIntervalTurns.ToString();

        // Cost set buttons
        bool hasSets = building.HasAlternateCostSets;
        if (prevCostSetButton) prevCostSetButton.gameObject.SetActive(hasSets);
        if (nextCostSetButton) nextCostSetButton.gameObject.SetActive(hasSets);

        RefreshCosts();
    }

    public void Hide()
    {
        ClearAll();
        _building = null;
        if (root) root.SetActive(false);
    }

    // ── Cost set cycling ──────────────────────────────────────────────────────

    private void OnPrevCostSet()
    {
        _building?.CyclePrevCostSet();
        RefreshCosts();
    }

    private void OnNextCostSet()
    {
        _building?.CycleNextCostSet();
        RefreshCosts();
    }

    private void RefreshCosts()
    {
        ClearCostEntries();

        if (_building == null) return;

        if (costSetLabelText)
            costSetLabelText.text = _building.HasAlternateCostSets
                ? _building.GetActiveCostSetLabel()
                : string.Empty;

        var costs = _building.GetActiveBuildCosts();
        if (costContentRoot == null || costEntryPrefab == null || costs == null) return;

        foreach (var cost in costs)
        {
            if (cost?.resource == null) continue;
            int owned = InventoryQuery.GetOwned(cost.resource);
            var entry = Instantiate(costEntryPrefab, costContentRoot);
            entry.Bind(cost.resource, cost.amount, owned);
            _costEntries.Add(entry.gameObject);
        }
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────

    private void ClearAll()
    {
        ClearList(_placementEntries);
        ClearCostEntries();
    }

    private void ClearCostEntries() => ClearList(_costEntries);

    private static void ClearList(List<GameObject> list)
    {
        for (int i = list.Count - 1; i >= 0; i--)
            if (list[i] != null) Destroy(list[i]);
        list.Clear();
    }
}
