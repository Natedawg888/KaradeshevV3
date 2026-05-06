using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BuildingCatalogItem : MonoBehaviour
{
    [Header("Main")]
    public Image icon;
    public TMP_Text title;

    [Header("Meta")]
    public TMP_Text sizeText;

    public Button buildButton;

    [Header("Costs UI")]
    public Button costsButton;
    public GameObject costPanelRoot;
    public Transform costContentParent;
    public GameObject costEntryPrefab;
    public Button closeCostsButton;

    [Header("Info UI")]
    public Button infoButton;
    public GameObject infoPanelRoot;
    public TMP_Text infoTurnsText;
    public TMP_Text infoPopulationText;
    public Button closeInfoButton;

    [Header("Colors")]
    public Color canAffordColor = new(0.20f, 0.70f, 0.20f);
    public Color cannotAffordColor = new(0.80f, 0.20f, 0.20f);
    public Color popEnoughColor = new(0.20f, 0.70f, 0.20f);
    public Color popNotEnoughColor = new(0.80f, 0.20f, 0.20f);

    [Header("Cost Sets UI (Optional)")]
    public Button prevCostSetButton;
    public Button nextCostSetButton;

    [Header("Limits UI")]
    public TMP_Text limitText;
    public TMP_Text buildButtonLabel;

    private Button _buildButton;
    private CanvasGroup _buildButtonCanvasGroup;

    private Building def;
    private EnvironmentControl env;

    private BuildingCatalogPanelControl catalogPanelRef;
    private DiscoveredTilePanelControl discoveredPanelRef;

    public Func<BuildingCatalogItem, bool> TutorialBuildOverride;

    public Building Definition => def;
    public EnvironmentControl TargetEnvironment => env;
    public bool IsCostsPanelShowing => costPanelRoot != null && costPanelRoot.activeSelf;
    public bool IsInfoPanelShowing => infoPanelRoot != null && infoPanelRoot.activeSelf;

    private void Awake()
    {
        _buildButton = buildButton ? buildButton : GetComponentInChildren<Button>(true);

        if (_buildButton != null)
        {
            _buildButtonCanvasGroup = _buildButton.GetComponent<CanvasGroup>()
                ?? _buildButton.gameObject.GetComponentInChildren<CanvasGroup>(true);
        }
    }

    public void Bind(
        Building building,
        EnvironmentControl environment,
        BuildingCatalogPanelControl catalogPanel,
        DiscoveredTilePanelControl discoveredPanel)
    {
        def = building;
        env = environment;
        catalogPanelRef = catalogPanel;
        discoveredPanelRef = discoveredPanel;

        if (def.HasAlternateCostSets && def.activeBuildCostSetIndex == -1)
            def.activeBuildCostSetIndex = 0;

        if (title) title.text = def.buildingName;
        if (icon && def.buildingIcon) icon.sprite = def.buildingIcon;
        if (sizeText) sizeText.text = GetSizeLabel(def);

        if (buildButton)
        {
            buildButton.onClick.RemoveAllListeners();
            buildButton.onClick.AddListener(OnBuildClicked);
        }

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

        if (costPanelRoot) costPanelRoot.SetActive(false);

        if (prevCostSetButton)
        {
            prevCostSetButton.onClick.RemoveAllListeners();
            prevCostSetButton.onClick.AddListener(() =>
            {
                def.CyclePrevCostSet();
                RefreshCostSetHeader();
                if (costPanelRoot && costPanelRoot.activeSelf)
                    PopulateCosts();
                RefreshAffordabilityColor();
            });

            prevCostSetButton.gameObject.SetActive(def.HasAlternateCostSets);
        }

        if (nextCostSetButton)
        {
            nextCostSetButton.onClick.RemoveAllListeners();
            nextCostSetButton.onClick.AddListener(() =>
            {
                def.CycleNextCostSet();
                RefreshCostSetHeader();
                if (costPanelRoot && costPanelRoot.activeSelf)
                    PopulateCosts();
                RefreshAffordabilityColor();
            });

            nextCostSetButton.gameObject.SetActive(def.HasAlternateCostSets);
        }

        RefreshCostSetHeader();

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

        if (infoPanelRoot) infoPanelRoot.SetActive(false);

        if (PlayersPopulationManager.Instance != null)
            PlayersPopulationManager.Instance.OnPopulationChanged += RefreshPopulationIndicators;

        RefreshLimitUI();

        if (PlayerBuildingManager.Instance != null)
        {
            PlayerBuildingManager.Instance.OnBuildingPlaced += HandleBuildingPlaced;
            PlayerBuildingManager.Instance.OnBuildingRemoved += HandleBuildingRemoved;
        }

        RefreshAffordabilityColor();
        PopulateInfo();
        RefreshPopulationIndicators();
    }

    public void ShowCostsPanelForTutorial()
    {
        if (!costPanelRoot) return;
        costPanelRoot.SetActive(true);
        PopulateCosts();
    }

    public void HideCostsPanelForTutorial()
    {
        HideCostsPanel();
    }

    public void ShowInfoPanelForTutorial()
    {
        if (!infoPanelRoot) return;
        infoPanelRoot.SetActive(true);
        PopulateInfo();
    }

    public void HideInfoPanelForTutorial()
    {
        HideInfoPanel();
    }

    private void RefreshLimitUI()
    {
        if (def == null) return;

        bool hasLimit = def.HasFamilyLimit;
        int current = PlayerBuildingManager.Instance != null
            ? PlayerBuildingManager.Instance.GetCountForBuildingFamily(def)
            : 0;

        int max = def.maxCountPerFamily;
        bool isMaxed = hasLimit && current >= max;

        if (limitText)
        {
            if (!hasLimit)
            {
                limitText.gameObject.SetActive(false);
            }
            else
            {
                limitText.gameObject.SetActive(true);
                limitText.text = isMaxed ? $"MAX ({current}/{max})" : $"{current} / {max}";
            }
        }

        if (_buildButton)
            _buildButton.interactable = !isMaxed;

        if (_buildButtonCanvasGroup)
        {
            _buildButtonCanvasGroup.alpha = isMaxed ? 0.45f : 1f;
            _buildButtonCanvasGroup.interactable = !isMaxed;
            _buildButtonCanvasGroup.blocksRaycasts = !isMaxed;
        }

        if (buildButtonLabel && hasLimit)
            buildButtonLabel.text = isMaxed ? "MAX" : "Build";
    }

    private void HandleBuildingPlaced(PlayerBuildingManager.Record r)
    {
        if (def == null || r == null) return;
        if (r.familyId == def.FamilyKey) RefreshLimitUI();
    }

    private void HandleBuildingRemoved(PlayerBuildingManager.Record r)
    {
        if (def == null || r == null) return;
        if (r.familyId == def.FamilyKey) RefreshLimitUI();
    }

    private void RefreshCostSetHeader()
    {
        if (prevCostSetButton) prevCostSetButton.gameObject.SetActive(def.HasAlternateCostSets);
        if (nextCostSetButton) nextCostSetButton.gameObject.SetActive(def.HasAlternateCostSets);
    }

    private void OnDestroy()
    {
        if (PlayersPopulationManager.Instance != null)
            PlayersPopulationManager.Instance.OnPopulationChanged -= RefreshPopulationIndicators;

        if (PlayerBuildingManager.Instance != null)
        {
            PlayerBuildingManager.Instance.OnBuildingPlaced -= HandleBuildingPlaced;
            PlayerBuildingManager.Instance.OnBuildingRemoved -= HandleBuildingRemoved;
        }
    }

    private string GetSizeLabel(Building b)
    {
        if (!string.IsNullOrWhiteSpace(b.buildingSize))
            return b.buildingSize;

        return PrettySize(b.requiredTileSize);
    }

    private static string PrettySize(TileSize size)
    {
        int n = (int)size;
        return n > 0 ? $"{n}×{n}" : size.ToString();
    }

    private void Update()
    {
        RefreshAffordabilityColor();
    }

    private void RefreshAffordabilityColor()
    {
        if (!costsButton || def == null) return;

        Image img = costsButton.GetComponent<Image>();
        if (!img) return;

        bool canAffordCurrent = InventoryQuery.CanAfford(def.GetActiveBuildCosts());
        bool anyOptionAffordable = def.CanAffordAnyCostOption();

        img.color = canAffordCurrent
            ? canAffordColor
            : (anyOptionAffordable ? cannotAffordColor : cannotAffordColor);
    }

    private void ToggleCostsPanel()
    {
        if (!costPanelRoot) return;

        bool show = !costPanelRoot.activeSelf;
        costPanelRoot.SetActive(show);

        if (show)
            PopulateCosts();
        else
            ClearCostContent();
    }

    private void HideCostsPanel()
    {
        if (!costPanelRoot) return;
        costPanelRoot.SetActive(false);
        ClearCostContent();
    }

    private void PopulateCosts()
    {
        if (!costContentParent || !costEntryPrefab || def == null) return;

        ClearCostContent();

        var costs = def.GetActiveBuildCosts();
        if (costs == null || costs.Count == 0) return;

        foreach (var c in costs)
        {
            if (c == null || c.resource == null) continue;

            int owned = InventoryQuery.GetOwned(c.resource);
            GameObject go = Instantiate(costEntryPrefab, costContentParent);
            BuildingCostEntry ui = go.GetComponent<BuildingCostEntry>();
            if (ui) ui.Bind(c.resource, c.amount, owned);
        }
    }

    private void ClearCostContent()
    {
        if (!costContentParent) return;

        for (int i = costContentParent.childCount - 1; i >= 0; i--)
            Destroy(costContentParent.GetChild(i).gameObject);
    }

    private void ToggleInfoPanel()
    {
        if (!infoPanelRoot) return;

        bool show = !infoPanelRoot.activeSelf;
        infoPanelRoot.SetActive(show);

        if (show)
            PopulateInfo();
    }

    private void HideInfoPanel()
    {
        if (!infoPanelRoot) return;
        infoPanelRoot.SetActive(false);
    }

    private void PopulateInfo()
    {
        if (def == null) return;

        if (infoTurnsText)
            infoTurnsText.text = $"Turns: {Mathf.Max(1, def.buildTurnsRequired)}";

        if (infoPopulationText)
        {
            int need = Mathf.Max(1, def.requireBuildPopulation);
            int available = PlayersPopulationManager.Instance != null
                ? PlayersPopulationManager.Instance.GetAvailableTaskPopulation()
                : 0;

            bool enough = available >= need;
            string hex = ColorUtility.ToHtmlStringRGB(enough ? popEnoughColor : popNotEnoughColor);

            infoPopulationText.richText = true;
            infoPopulationText.text = $"Population: <color=#{hex}>{need}</color>";
        }
    }

    private void RefreshPopulationIndicators()
    {
        if (def == null) return;

        if (infoButton)
        {
            Image img = infoButton.GetComponent<Image>();
            if (img != null)
            {
                int need = Mathf.Max(1, def.requireBuildPopulation);
                int available = PlayersPopulationManager.Instance != null
                    ? PlayersPopulationManager.Instance.GetAvailableTaskPopulation()
                    : 0;

                bool enough = available >= need;
                img.color = enough ? popEnoughColor : popNotEnoughColor;
            }
        }

        if (infoPanelRoot && infoPanelRoot.activeSelf)
            PopulateInfo();
    }

    private void OnBuildClicked()
    {
        if (TutorialBuildOverride != null && TutorialBuildOverride.Invoke(this))
            return;

        TryStartRealBuild();
    }

    public bool TryStartRealBuild()
    {
        if (def == null || env == null)
            return false;

        if (PlayerBuildingManager.Instance != null && PlayerBuildingManager.Instance.HasReachedFamilyLimit(def))
        {
            Debug.Log(
                $"Cannot build {def.buildingName}: family limit reached " +
                $"({PlayerBuildingManager.Instance.GetCountForBuildingFamily(def)}/{def.maxCountPerFamily}) for family '{def.FamilyKey}'.");
            return false;
        }

        var activeCosts = def.GetActiveBuildCosts();

        if (!InventoryQuery.CanAfford(activeCosts))
        {
            Debug.Log($"Cannot build {def.buildingName}: not enough resources for {def.GetActiveCostSetLabel()}.");
            if (costPanelRoot && !costPanelRoot.activeSelf)
            {
                costPanelRoot.SetActive(true);
                PopulateCosts();
            }
            return false;
        }

        int needPop = Mathf.Max(1, def.requireBuildPopulation);
        int availPop = PlayersPopulationManager.Instance != null
            ? PlayersPopulationManager.Instance.GetAvailableTaskPopulation()
            : 0;

        if (availPop < needPop)
        {
            Debug.Log($"Cannot build {def.buildingName}: not enough population (need {needPop}, have {availPop}).");
            if (infoPanelRoot && !infoPanelRoot.activeSelf)
            {
                infoPanelRoot.SetActive(true);
                PopulateInfo();
            }
            return false;
        }

        BuildingPlacementManager.Instance?.BeginPlacement(def, env);

        if (catalogPanelRef) catalogPanelRef.Hide();
        if (discoveredPanelRef) discoveredPanelRef.Hide();

        return true;
    }
}