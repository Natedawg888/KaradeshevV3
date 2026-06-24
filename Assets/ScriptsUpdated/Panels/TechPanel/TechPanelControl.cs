using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Main tech/knowledge encyclopedia panel.
/// Filter tabs: Resources | Buildings | Crafting | Production | Units | Technologies
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

    [Header("Filter Buttons")]
    public Button   filterResources;
    public Button   filterBuildings;
    public Button   filterCrafting;
    public Button   filterProduction;
    public Button   filterUnits;
    public Button   filterTechnologies;
    public TMP_Text filterLabelText;

    [Header("List")]
    public Transform contentRoot;
    public TechResourceEntryUI   resourceEntryPrefab;
    public TechBuildingEntryUI   buildingEntryPrefab;
    public TechCraftingEntryUI   craftingEntryPrefab;
    public TechProductionEntryUI productionEntryPrefab;
    public TechUnitEntryUI       unitEntryPrefab;
    public TechTechnologyEntryUI techEntryPrefab;

    [Header("Detail Panels")]
    public TechResourceDetailPanel      detailPanel;
    public TechBuildingDetailPanel      buildingDetailPanel;
    public TechCraftingDetailPanel      craftingDetailPanel;
    public TechProductionDetailPanel    productionDetailPanel;
    public TechUnitDetailPanel          unitDetailPanel;
    public TechnologyDetailPanelControl techDetailPanel;

    public bool IsShowing => root != null ? root.activeInHierarchy : gameObject.activeInHierarchy;

    public event System.Action OnOpen;
    public event System.Action OnClose;

    private enum FilterMode { Resources, Buildings, Crafting, Production, Units, Technologies }
    private FilterMode _currentFilter = FilterMode.Resources;

    private readonly List<GameObject> _spawnedEntries = new();

    public CameraControl cameraControl;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (closeButton) closeButton.onClick.AddListener(Close);

        BindFilter(filterResources,    FilterMode.Resources);
        BindFilter(filterBuildings,    FilterMode.Buildings);
        BindFilter(filterCrafting,     FilterMode.Crafting);
        BindFilter(filterProduction,   FilterMode.Production);
        BindFilter(filterUnits,        FilterMode.Units);
        BindFilter(filterTechnologies, FilterMode.Technologies);

        if (cameraControl == null)
            cameraControl = FindObjectOfType<CameraControl>();

        if (root) root.SetActive(false);
    }

    private void OnEnable()
    {
        if (PlayerKnownResourcesManager.Instance != null)
            PlayerKnownResourcesManager.Instance.OnKnownChanged += HandleKnownChanged;

        if (PlayerLevel.Instance != null)
        {
            PlayerLevel.Instance.OnLevelUp  += HandleLevelUp;
            PlayerLevel.Instance.OnXPChanged += HandleXPChanged;
        }
    }

    private void OnDisable()
    {
        if (PlayerKnownResourcesManager.Instance != null)
            PlayerKnownResourcesManager.Instance.OnKnownChanged -= HandleKnownChanged;

        if (PlayerLevel.Instance != null)
        {
            PlayerLevel.Instance.OnLevelUp  -= HandleLevelUp;
            PlayerLevel.Instance.OnXPChanged -= HandleXPChanged;
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Open()
    {
        if (root) root.SetActive(true);
        gameObject.SetActive(true);

        ResourceSourceCache.BuildCache();

        if (cameraControl != null)
            cameraControl.PushInputLock();

        TileInteraction.SetSelectionEnabled(false);

        RefreshHeader();
        RefreshXP();
        RefreshList();
        OnOpen?.Invoke();
    }

    public void Close()
    {
        TileInteraction.SetSelectionEnabled(false);
        TileInteraction.GetInstance()?.EnableSelectionAfter(0.01f);

        if (cameraControl != null)
            cameraControl.PopInputLock();

        HideAllDetailPanels();
        if (root) root.SetActive(false);
        OnClose?.Invoke();
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

        var levelMgr = FindObjectOfType<LevelManager>();
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

        if (levelIconLeft != null)
        {
            levelIconLeft.sprite  = stageIcon;
            levelIconLeft.enabled = stageIcon != null;
        }

        if (levelIconRight != null)
        {
            levelIconRight.sprite  = stageIcon;
            levelIconRight.enabled = stageIcon != null;
        }

        if (playerNameText != null)
        {
            string pName = ProfilePanelControl.Instance != null
                ? ProfilePanelControl.Instance.PlayerName
                : string.Empty;
            playerNameText.text = pName;
        }

        if (currentLevelText != null)
            currentLevelText.text = $"{currentLevel}";

        if (nextLevelText != null)
        {
            int next = currentLevel + 1;
            bool maxed = pl != null && pl.XPToNextLevel <= 0;
            nextLevelText.text = maxed ? "Max" : $"Level {next}";
        }
    }

    // ── Internal ──────────────────────────────────────────────────────────────

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
        if (detailPanel           != null) detailPanel.Hide();
        if (buildingDetailPanel   != null) buildingDetailPanel.Hide();
        if (craftingDetailPanel   != null) craftingDetailPanel.Hide();
        if (productionDetailPanel != null) productionDetailPanel.Hide();
        if (unitDetailPanel       != null) unitDetailPanel.Hide();
        if (techDetailPanel       != null) techDetailPanel.Hide();
    }

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

    private void RefreshList()
    {
        ClearList();

        if (filterLabelText != null)
            filterLabelText.text = _currentFilter switch
            {
                FilterMode.Resources    => "Resources",
                FilterMode.Buildings    => "Buildings",
                FilterMode.Crafting     => "Crafting",
                FilterMode.Production   => "Production",
                FilterMode.Units        => "Units",
                FilterMode.Technologies => "Technologies",
                _                       => string.Empty
            };

        switch (_currentFilter)
        {
            case FilterMode.Resources:    PopulateResources();    break;
            case FilterMode.Buildings:    PopulateBuildings();    break;
            case FilterMode.Crafting:     PopulateCrafting();     break;
            case FilterMode.Production:   PopulateProduction();   break;
            case FilterMode.Units:        PopulateUnits();        break;
            case FilterMode.Technologies: PopulateTechnologies(); break;
        }
    }

    // ── Populate ──────────────────────────────────────────────────────────────

    private void PopulateResources()
    {
        var knownMgr = PlayerKnownResourcesManager.Instance;
        if (knownMgr == null || resourceEntryPrefab == null || contentRoot == null) return;

        foreach (var resource in knownMgr.GetAllKnown())
        {
            if (resource == null) continue;
            var entry = Instantiate(resourceEntryPrefab, contentRoot);
            entry.Bind(resource, OnResourceEntryClicked);
            _spawnedEntries.Add(entry.gameObject);
        }
    }

    private void PopulateBuildings()
    {
        var mgr = PlayerKnownBuildingsManager.Instance;
        if (mgr == null || buildingEntryPrefab == null || contentRoot == null) return;

        foreach (var building in mgr.GetKnownBuildings())
        {
            if (building == null) continue;
            var entry = Instantiate(buildingEntryPrefab, contentRoot);
            entry.Bind(building, OnBuildingEntryClicked);
            _spawnedEntries.Add(entry.gameObject);
        }
    }

    private void PopulateCrafting()
    {
        var mgr = PlayerKnownCraftingManager.Instance;
        if (mgr == null || craftingEntryPrefab == null || contentRoot == null) return;

        foreach (var recipe in mgr.GetKnownRecipes())
        {
            if (recipe == null) continue;
            var entry = Instantiate(craftingEntryPrefab, contentRoot);
            entry.Bind(recipe, OnCraftingEntryClicked);
            _spawnedEntries.Add(entry.gameObject);
        }
    }

    private void PopulateProduction()
    {
        var mgr = PlayerKnownProductionManager.Instance;
        if (mgr == null || productionEntryPrefab == null || contentRoot == null) return;

        foreach (var plan in mgr.GetKnownPlans())
        {
            if (plan == null) continue;
            var entry = Instantiate(productionEntryPrefab, contentRoot);
            entry.Bind(plan, OnProductionEntryClicked);
            _spawnedEntries.Add(entry.gameObject);
        }
    }

    private void PopulateUnits()
    {
        var mgr = PlayerKnownUnitsManager.Instance;
        if (mgr == null || unitEntryPrefab == null || contentRoot == null) return;

        foreach (var unit in mgr.GetAllKnown())
        {
            if (unit == null) continue;
            var entry = Instantiate(unitEntryPrefab, contentRoot);
            entry.Bind(unit, OnUnitEntryClicked);
            _spawnedEntries.Add(entry.gameObject);
        }
    }

    private void PopulateTechnologies()
    {
        var mgr = PlayerKnownTechnologyManager.Instance;
        if (mgr == null || techEntryPrefab == null || contentRoot == null) return;

        int playerLevel = PlayerLevel.Instance != null ? PlayerLevel.Instance.GetCurrentLevel() : 1;

        foreach (var id in mgr.GetKnownIDs())
        {
            var tech = TechnologyManager.Instance?.GetByID(id);
            if (tech == null || tech.requiredPlayerLevel > playerLevel) continue;
            var entry = Instantiate(techEntryPrefab, contentRoot);
            entry.Bind(tech, OnTechEntryClicked);
            _spawnedEntries.Add(entry.gameObject);
        }
    }

    // ── Click handlers ────────────────────────────────────────────────────────

    private void OnResourceEntryClicked(ResourceDefinition resource)
    {
        if (detailPanel != null) detailPanel.ShowFor(resource);
    }

    private void OnBuildingEntryClicked(Building building)
    {
        if (buildingDetailPanel != null) buildingDetailPanel.ShowFor(building);
    }

    private void OnCraftingEntryClicked(CraftingRecipe recipe)
    {
        if (craftingDetailPanel != null) craftingDetailPanel.ShowFor(recipe);
    }

    private void OnProductionEntryClicked(ProductionPlan plan)
    {
        if (productionDetailPanel != null) productionDetailPanel.ShowFor(plan);
    }

    private void OnUnitEntryClicked(MilitiaUnit unit)
    {
        if (unitDetailPanel != null) unitDetailPanel.ShowFor(unit);
    }

    private void OnTechEntryClicked(Technology tech)
    {
        if (techDetailPanel != null) techDetailPanel.Show(tech);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void ClearList()
    {
        for (int i = _spawnedEntries.Count - 1; i >= 0; i--)
            if (_spawnedEntries[i] != null) Destroy(_spawnedEntries[i]);
        _spawnedEntries.Clear();
    }
}
