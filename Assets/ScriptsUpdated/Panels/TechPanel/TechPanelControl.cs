using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Main tech/knowledge encyclopedia panel.
/// Filter tabs: Resources | Buildings | Crafting | Production | Units
/// Currently populates Resources only; other tabs are stubbed.
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
    public TechResourceEntryUI resourceEntryPrefab;

    [Header("Detail Panel")]
    public TechResourceDetailPanel detailPanel;

    private enum FilterMode { Resources, Buildings, Crafting, Production, Units, Tech }
    private FilterMode _currentFilter = FilterMode.Resources;

    private readonly List<TechResourceEntryUI> _spawnedEntries = new();

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

        if (PlayerKnownTechnologyManager.Instance != null)
            PlayerKnownTechnologyManager.Instance.OnKnownTechnologyChanged += HandleKnownChanged;

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

        if (PlayerKnownTechnologyManager.Instance != null)
            PlayerKnownTechnologyManager.Instance.OnKnownTechnologyChanged -= HandleKnownChanged;

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

        RefreshHeader();
        RefreshXP();
        RefreshList();
    }

    public void Close()
    {
        if (detailPanel != null) detailPanel.Hide();
        if (root) root.SetActive(false);
    }

    // ── Header ────────────────────────────────────────────────────────────────

    private void RefreshHeader()
    {
        // Title: "[Civ Name] Tech Panel"
        if (titleText != null)
        {
            string civName = ProfilePanelControl.Instance != null
                ? ProfilePanelControl.Instance.CivilizationName
                : string.Empty;

            titleText.text = string.IsNullOrWhiteSpace(civName)
                ? "Tech Panel"
                : $"{civName} Tech Panel";
        }

        // Stage name + icons
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

        // Player name
        if (playerNameText != null)
        {
            string pName = ProfilePanelControl.Instance != null
                ? ProfilePanelControl.Instance.PlayerName
                : string.Empty;
            playerNameText.text = pName;
        }

        // Current / next level
        if (currentLevelText != null)
            currentLevelText.text = $"Level {currentLevel}";

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
        if (detailPanel != null) detailPanel.Hide();
        RefreshList();
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
        RefreshActiveFilterText();
        ClearList();

        switch (_currentFilter)
        {
            case FilterMode.Resources:
                PopulateResources();
                break;
            case FilterMode.Tech:
                PopulateTech();
                break;
            // Buildings, Crafting, Production, Units — to be implemented
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
            entry.Bind(resource, OnResourceEntryClicked);
            _spawnedEntries.Add(entry);
        }
    }

    private void PopulateTech()
    {
        // To be implemented: show known technologies using TechTechnologyEntryUI prefab
    }

    private void OnResourceEntryClicked(ResourceDefinition resource)
    {
        if (detailPanel != null)
            detailPanel.ShowFor(resource);
    }

    private void ClearList()
    {
        for (int i = _spawnedEntries.Count - 1; i >= 0; i--)
        {
            if (_spawnedEntries[i] != null)
                Destroy(_spawnedEntries[i].gameObject);
        }
        _spawnedEntries.Clear();
    }
}
