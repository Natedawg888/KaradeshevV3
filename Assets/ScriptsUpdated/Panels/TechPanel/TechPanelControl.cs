using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Main tech/knowledge encyclopedia panel.
/// Filter tabs: Resources | Buildings | Crafting | Production | Units | Tech
/// </summary>
public class TechPanelControl : MonoBehaviour
{
    public static TechPanelControl Instance { get; private set; }

    [Header("Root")]
    public GameObject root;
    public Button closeButton;

    [Header("Header")]
    public TMP_Text titleText;
    public Image levelIconLeft;
    public Image levelIconRight;
    public TMP_Text stageNameText;
    public TMP_Text playerNameText;
    public TMP_Text currentLevelText;
    public TMP_Text nextLevelText;

    [Header("XP")]
    public TMP_Text xpText;
    public Slider xpSlider;

    [Header("Filter")]
    public TMP_Text activeFilterText;
    public Button filterResources;
    public Button filterBuildings;
    public Button filterCrafting;
    public Button filterProduction;
    public Button filterUnits;
    public Button filterTech;

    [Header("List")]
    public Transform contentRoot;

    [Header("Resources")]
    public TechResourceEntryUI resourceEntryPrefab;
    public TechResourceDetailPanel resourceDetailPanel;

    [Header("Buildings")]
    public TechBuildingEntryUI buildingEntryPrefab;
    public TechBuildingDetailPanel buildingDetailPanel;

    [Header("Crafting")]
    public TechCraftingEntryUI craftingEntryPrefab;
    public TechCraftingDetailPanel craftingDetailPanel;

    [Header("Production")]
    public TechProductionEntryUI productionEntryPrefab;
    public TechProductionDetailPanel productionDetailPanel;

    [Header("Units")]
    public TechUnitEntryUI unitEntryPrefab;
    public TechUnitDetailPanel unitDetailPanel;

    [Header("Technology")]
    public TechTechnologyEntryUI      techEntryPrefab;
    public TechnologyDetailPanelControl techDetailPanel;

    private enum FilterMode { Resources, Buildings, Crafting, Production, Units, Tech }
    private FilterMode _currentFilter = FilterMode.Resources;

    private readonly List<GameObject> _spawnedEntries = new();
    private LevelManager _levelManager;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (closeButton) closeButton.onClick.AddListener(Close);

        BindFilter(filterResources,  FilterMode.Resources);
        BindFilter(filterBuildings,  FilterMode.Buildings);
        BindFilter(filterCrafting,   FilterMode.Crafting);
        BindFilter(filterProduction, FilterMode.Production);
        BindFilter(filterUnits,      FilterMode.Units);
        BindFilter(filterTech,       FilterMode.Tech);

        if (root) root.SetActive(false);
    }

    private void OnEnable()
    {
        if (PlayerKnownResourcesManager.Instance != null)
            PlayerKnownResourcesManager.Instance.OnKnownChanged += HandleKnownChanged;

        if (PlayerKnownBuildingsManager.Instance != null)
            PlayerKnownBuildingsManager.Instance.OnKnownBuildingsChanged += HandleKnownChanged;

        if (PlayerKnownCraftingManager.Instance != null)
            PlayerKnownCraftingManager.Instance.OnKnownCraftingChanged += HandleKnownChanged;

        if (PlayerKnownProductionManager.Instance != null)
            PlayerKnownProductionManager.Instance.OnKnownProductionChanged += HandleKnownChanged;

        if (PlayerKnownUnitsManager.Instance != null)
            PlayerKnownUnitsManager.Instance.OnKnownChanged += HandleKnownChanged;

        if (PlayerKnownTechnologyManager.Instance != null)
            PlayerKnownTechnologyManager.Instance.OnKnownTechnologyChanged += HandleKnownChanged;

        if (PlayerLevel.Instance != null)
        {
            PlayerLevel.Instance.OnLevelUp   += HandleLevelUp;
            PlayerLevel.Instance.OnXPChanged += HandleXPChanged;
        }
    }

    private void OnDisable()
    {
        if (PlayerKnownResourcesManager.Instance != null)
            PlayerKnownResourcesManager.Instance.OnKnownChanged -= HandleKnownChanged;

        if (PlayerKnownBuildingsManager.Instance != null)
            PlayerKnownBuildingsManager.Instance.OnKnownBuildingsChanged -= HandleKnownChanged;

        if (PlayerKnownCraftingManager.Instance != null)
            PlayerKnownCraftingManager.Instance.OnKnownCraftingChanged -= HandleKnownChanged;

        if (PlayerKnownProductionManager.Instance != null)
            PlayerKnownProductionManager.Instance.OnKnownProductionChanged -= HandleKnownChanged;

        if (PlayerKnownUnitsManager.Instance != null)
            PlayerKnownUnitsManager.Instance.OnKnownChanged -= HandleKnownChanged;

        if (PlayerKnownTechnologyManager.Instance != null)
            PlayerKnownTechnologyManager.Instance.OnKnownTechnologyChanged -= HandleKnownChanged;

        if (PlayerLevel.Instance != null)
        {
            PlayerLevel.Instance.OnLevelUp   -= HandleLevelUp;
            PlayerLevel.Instance.OnXPChanged -= HandleXPChanged;
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Open()
    {
        if (root) root.SetActive(true);
        gameObject.SetActive(true);

        ResourceSourceCache.BuildCache();

        RefreshHeader();
        RefreshXP();
        RefreshList();
    }

    public void Close()
    {
        HideAllDetailPanels();
        if (root) root.SetActive(false);
    }

    // ── Header ────────────────────────────────────────────────────────────────

    private void RefreshHeader()
    {
        if (titleText != null)
        {
            string civName = ProfilePanelControl.Instance != null
                ? ProfilePanelControl.Instance.CivilizationName
                : string.Empty;

            titleText.text = string.IsNullOrWhiteSpace(civName)
                ? "Tech Panel"
                : $"{civName} Tech Panel";
        }

        if (_levelManager == null) _levelManager = FindObjectOfType<LevelManager>();
        var levelMgr = _levelManager;
        var pl = PlayerLevel.Instance;
        int currentLevel = pl ? pl.GetCurrentLevel() : 1;
        StageData stageData = null;

        if (levelMgr != null)
        {
            Stage stage = levelMgr.GetStageForLevel(currentLevel);
            stageData = levelMgr.GetStageData(stage);
        }

        if (stageNameText != null)
            stageNameText.text = stageData != null ? stageData.displayName : string.Empty;

        Sprite stageIcon = stageData?.icon;

        if (levelIconLeft  != null) { levelIconLeft.sprite  = stageIcon; levelIconLeft.enabled  = stageIcon != null; }
        if (levelIconRight != null) { levelIconRight.sprite = stageIcon; levelIconRight.enabled = stageIcon != null; }

        if (playerNameText != null)
            playerNameText.text = ProfilePanelControl.Instance != null
                ? ProfilePanelControl.Instance.PlayerName
                : string.Empty;

        if (currentLevelText != null)
            currentLevelText.text = $"Level {currentLevel}";

        if (nextLevelText != null)
        {
            bool maxed = pl != null && pl.XPToNextLevel <= 0;
            nextLevelText.text = maxed ? "Max" : $"Level {currentLevel + 1}";
        }
    }

    // ── Filter ────────────────────────────────────────────────────────────────

    private void BindFilter(Button btn, FilterMode mode)
    {
        if (btn == null) return;
        btn.onClick.AddListener(() => SetFilter(mode));
    }

    private void SetFilter(FilterMode mode)
    {
        _currentFilter = mode;
        HideAllDetailPanels();
        RefreshList();
    }

    private void HideAllDetailPanels()
    {
        if (resourceDetailPanel   != null) resourceDetailPanel.Hide();
        if (buildingDetailPanel   != null) buildingDetailPanel.Hide();
        if (craftingDetailPanel   != null) craftingDetailPanel.Hide();
        if (productionDetailPanel != null) productionDetailPanel.Hide();
        if (unitDetailPanel       != null) unitDetailPanel.Hide();
        if (techDetailPanel       != null) techDetailPanel.Hide();
    }

    private void RefreshActiveFilterText()
    {
        if (activeFilterText == null) return;
        activeFilterText.text = _currentFilter switch
        {
            FilterMode.Resources  => "Resources",
            FilterMode.Buildings  => "Buildings",
            FilterMode.Crafting   => "Crafting",
            FilterMode.Production => "Production",
            FilterMode.Units      => "Units",
            FilterMode.Tech       => "Tech",
            _                     => string.Empty
        };
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void HandleKnownChanged()
    {
        if (root != null && root.activeInHierarchy)
            RefreshList();
    }

    private void HandleLevelUp(int newLevel)
    {
        if (root != null && root.activeInHierarchy)
        {
            RefreshHeader();
            RefreshXP();
        }
    }

    private void HandleXPChanged(int currentXP, int xpToNext)
    {
        if (root != null && root.activeInHierarchy)
            RefreshXP();
    }

    // ── XP ────────────────────────────────────────────────────────────────────

    private void RefreshXP()
    {
        var pl = PlayerLevel.Instance;
        if (pl == null) return;

        int needed = pl.XPToNextLevel;
        bool maxed = needed <= 0;

        if (xpText != null)
            xpText.text = maxed ? "Max Level" : $"{pl.currentXP} / {needed} XP";

        if (xpSlider != null)
        {
            xpSlider.minValue = 0f;
            xpSlider.maxValue = 1f;
            xpSlider.value    = pl.Progress01;
        }
    }

    // ── List ──────────────────────────────────────────────────────────────────

    private void RefreshList()
    {
        RefreshActiveFilterText();
        ClearList();

        switch (_currentFilter)
        {
            case FilterMode.Resources:  PopulateResources();  break;
            case FilterMode.Buildings:  PopulateBuildings();  break;
            case FilterMode.Crafting:    PopulateCrafting();    break;
            case FilterMode.Production:  PopulateProduction();  break;
            case FilterMode.Units:       PopulateUnits();       break;
            case FilterMode.Tech:        PopulateTech();        break;
        }
    }

    private void PopulateResources()
    {
        var knownMgr = PlayerKnownResourcesManager.Instance;
        if (knownMgr == null || resourceEntryPrefab == null || contentRoot == null) return;

        foreach (var resource in knownMgr.GetAllKnown())
        {
            if (resource == null) continue;
            var entry = Instantiate(resourceEntryPrefab, contentRoot);
            entry.Bind(resource, r => resourceDetailPanel?.ShowFor(r));
            _spawnedEntries.Add(entry.gameObject);
        }
    }

    private void PopulateBuildings()
    {
        var knownMgr = PlayerKnownBuildingsManager.Instance;
        if (knownMgr == null || buildingEntryPrefab == null || contentRoot == null) return;

        var buildings = knownMgr.GetKnownBuildings();
        if (buildings == null) return;

        foreach (var building in buildings)
        {
            if (building == null) continue;
            var entry = Instantiate(buildingEntryPrefab, contentRoot);
            entry.Bind(building, b => buildingDetailPanel?.ShowFor(b));
            _spawnedEntries.Add(entry.gameObject);
        }
    }

    private void PopulateCrafting()
    {
        var knownMgr = PlayerKnownCraftingManager.Instance;
        if (knownMgr == null || craftingEntryPrefab == null || contentRoot == null) return;

        foreach (var recipe in knownMgr.GetKnownRecipes())
        {
            if (recipe == null) continue;
            var entry = Instantiate(craftingEntryPrefab, contentRoot);
            entry.Bind(recipe, r => craftingDetailPanel?.ShowFor(r));
            _spawnedEntries.Add(entry.gameObject);
        }
    }

    private void PopulateProduction()
    {
        var knownMgr = PlayerKnownProductionManager.Instance;
        if (knownMgr == null || productionEntryPrefab == null || contentRoot == null) return;

        foreach (var plan in knownMgr.GetKnownPlans())
        {
            if (plan == null) continue;
            var entry = Instantiate(productionEntryPrefab, contentRoot);
            entry.Bind(plan, p => productionDetailPanel?.ShowFor(p));
            _spawnedEntries.Add(entry.gameObject);
        }
    }

    private void PopulateUnits()
    {
        var knownMgr = PlayerKnownUnitsManager.Instance;
        if (knownMgr == null || unitEntryPrefab == null || contentRoot == null) return;

        foreach (var unit in knownMgr.GetAllKnown())
        {
            if (unit == null) continue;
            var entry = Instantiate(unitEntryPrefab, contentRoot);
            entry.Bind(unit, u => unitDetailPanel?.ShowFor(u));
            _spawnedEntries.Add(entry.gameObject);
        }
    }

    private void PopulateTech()
    {
        var techMgr  = TechnologyManager.Instance;
        var knownMgr = PlayerKnownTechnologyManager.Instance;
        if (techMgr == null || techEntryPrefab == null || contentRoot == null) return;

        int playerLevel = PlayerLevel.Instance != null ? PlayerLevel.Instance.GetCurrentLevel() : 0;

        foreach (var tech in techMgr.GetAll())
        {
            if (tech == null) continue;
            if (knownMgr != null && !knownMgr.IsKnown(tech.techID)) continue;
            if (!tech.IsEligibleForLevel(playerLevel)) continue;

            var entry = Instantiate(techEntryPrefab, contentRoot);
            entry.Bind(tech, t => techDetailPanel?.Show(t));
            _spawnedEntries.Add(entry.gameObject);
        }
    }

    private void ClearList()
    {
        for (int i = _spawnedEntries.Count - 1; i >= 0; i--)
            if (_spawnedEntries[i] != null) Destroy(_spawnedEntries[i]);
        _spawnedEntries.Clear();
    }
}
