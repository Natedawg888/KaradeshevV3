using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TechnologyItem : MonoBehaviour
{
    [Header("Main")]
    public TMP_Text titleText;
    public Button researchButton;
    public Image iconImage;

    public TMP_Text failChanceText;

    [Header("Costs UI")]
    public Button costsButton;
    public GameObject costPanelRoot;
    public Transform costContentParent;
    public GameObject costEntryPrefab;   // must have BuildingCostEntry
    public Button closeCostsButton;

    [Header("Needs UI")]
    public Button needsButton;
    public GameObject needsPanelRoot;
    public TMP_Text needTurnsText;         // "Turns: X"
    public TMP_Text needKnowledgeText;     // "Knowledge: X%"
    public TMP_Text needPopulationText;
    public Button closeNeedsButton;

    [Header("Colors")]
    public Color canAffordColor    = new(0.20f, 0.70f, 0.20f);
    public Color cannotAffordColor = new(0.80f, 0.20f, 0.20f);
    public Color needOkColor       = new(0.20f, 0.70f, 0.20f);
    public Color needBadColor      = new(0.80f, 0.20f, 0.20f);

    private Technology tech;
    private BuildingControl station;
    private ResearchPanelControl ownerPanel;

    public void Bind(Technology t, BuildingControl stationBuilding, ResearchPanelControl owner)
    {
        tech = t;
        station = stationBuilding;
        ownerPanel = owner;

        if (titleText) titleText.text = tech.techName ?? tech.techID;

        if (researchButton)
        {
            researchButton.onClick.RemoveAllListeners();
            researchButton.onClick.AddListener(OnClickResearch);
        }

        if (iconImage)
        {
            iconImage.sprite  = tech.icon;
            iconImage.enabled = (tech.icon != null);
        }

        // Costs panel
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

        // Needs panel
        if (needsButton)
        {
            needsButton.onClick.RemoveAllListeners();
            needsButton.onClick.AddListener(ToggleNeedsPanel);
        }
        if (closeNeedsButton)
        {
            closeNeedsButton.onClick.RemoveAllListeners();
            closeNeedsButton.onClick.AddListener(HideNeedsPanel);
        }
        if (needsPanelRoot) needsPanelRoot.SetActive(false);

        if (failChanceText)
        {
            float p = -1f;
            if (PlayerResearchManager.Instance != null)
                p = PlayerResearchManager.Instance.PreviewFailureChance(tech);

            if (p < 0f)
            {
                failChanceText.text = "Fail chance: N/A";
            }
            else
            {
                int pct = Mathf.RoundToInt(p * 100f);
                // color it red-ish when high, green-ish when low using existing colors
                bool low = (pct <= 20);
                string hex = ColorUtility.ToHtmlStringRGB(low ? needOkColor : needBadColor);
                failChanceText.richText = true;
                failChanceText.text = $"{pct}%";
            }
        }

        RefreshAffordabilityColor();
        PopulateNeeds();
        RefreshResearchButtonState();
    }

    private void Update()
    {
        RefreshAffordabilityColor();
        RefreshResearchButtonState();
        if (needsPanelRoot && needsPanelRoot.activeSelf) PopulateNeeds();
    }

    // ---- Research click ----
    private void OnClickResearch()
    {
        if (!PlayerResearchManager.Instance || tech == null) return;

        bool ok = PlayerResearchManager.Instance.StartResearch(tech, station);
        if (!ok)
        {
            if (!InventoryQuery.CanAfford(tech.researchCosts) && costPanelRoot && !costPanelRoot.activeSelf)
            {
                costPanelRoot.SetActive(true);
                PopulateCosts();
            }
            else if (needsPanelRoot && !needsPanelRoot.activeSelf)
            {
                needsPanelRoot.SetActive(true);
                PopulateNeeds();
            }
            return;
        }
        ownerPanel?.RefreshNow();

        Destroy(gameObject);
    }

    // ---- Costs ----
    private void RefreshAffordabilityColor()
    {
        if (!costsButton) return;
        var img = costsButton.GetComponent<Image>();
        if (!img) return;

        bool canAfford = InventoryQuery.CanAfford(tech.researchCosts);
        img.color = canAfford ? canAffordColor : cannotAffordColor;
    }

    private void ToggleCostsPanel()
    {
        if (!costPanelRoot) return;
        bool show = !costPanelRoot.activeSelf;
        costPanelRoot.SetActive(show);
        if (show) PopulateCosts();
        else ClearCostContent();
    }

    private void HideCostsPanel()
    {
        if (!costPanelRoot) return;
        costPanelRoot.SetActive(false);
        ClearCostContent();
    }

    private void PopulateCosts()
    {
        if (!costContentParent || !costEntryPrefab) return;
        ClearCostContent();

        var costs = tech.researchCosts;
        if (costs == null || costs.Count == 0) return;

        foreach (var c in costs)
        {
            if (c == null || c.resource == null) continue;

            int owned = InventoryQuery.GetOwned(c.resource);
            var go = Instantiate(costEntryPrefab, costContentParent);
            var ui = go.GetComponent<BuildingCostEntry>();
            if (ui) ui.Bind(c.resource, c.amount, owned);
        }
    }

    private void ClearCostContent()
    {
        if (!costContentParent) return;
        for (int i = costContentParent.childCount - 1; i >= 0; i--)
            Destroy(costContentParent.GetChild(i).gameObject);
    }

    // ---- Needs (Turns / Knowledge / Population) ----
    private void ToggleNeedsPanel()
    {
        if (!needsPanelRoot) return;
        bool show = !needsPanelRoot.activeSelf;
        needsPanelRoot.SetActive(show);
        if (show) PopulateNeeds();
    }

    private void HideNeedsPanel()
    {
        if (!needsPanelRoot) return;
        needsPanelRoot.SetActive(false);
    }

    private void PopulateNeeds()
    {
        int turns          = Mathf.Max(0, tech.turnsRequired);
        int needKnowledge  = Mathf.Max(0, tech.requiredKnowledge);      // 0..100
        int needPopulation = Mathf.Max(0, tech.requiredPopulation);     // NEW

        int currentKnowledge = 0;
        if (CivilizationStateManager.Instance != null)
            currentKnowledge = Mathf.Max(0, Mathf.RoundToInt(Mathf.Clamp01(CivilizationStateManager.Instance.knowledge01) * 100f));

        int availablePop = PlayersPopulationManager.Instance
            ? PlayersPopulationManager.Instance.GetAvailableTaskPopulation()
            : 0;

        if (needTurnsText) needTurnsText.text = $"Turns: {turns}";

        if (needKnowledgeText)
        {
            bool ok = currentKnowledge >= needKnowledge;
            string hex = ColorUtility.ToHtmlStringRGB(ok ? needOkColor : needBadColor);
            needKnowledgeText.richText = true;
            needKnowledgeText.text = $"Knowledge: <color=#{hex}>{needKnowledge}%</color>";
        }

        if (needPopulationText)
        {
            bool ok = availablePop >= needPopulation;
            string hex = ColorUtility.ToHtmlStringRGB(ok ? needOkColor : needBadColor);
            needPopulationText.richText = true;
            needPopulationText.text = $"Population: <color=#{hex}>{needPopulation}</color>";
        }
    }

    private void RefreshResearchButtonState()
    {
        if (!researchButton) return;

        int playerLevel = PlayerLevel.Instance ? PlayerLevel.Instance.GetCurrentLevel() : int.MaxValue;
        int currentKnowledge = 0;
        if (CivilizationStateManager.Instance != null)
            currentKnowledge = Mathf.Max(0, Mathf.RoundToInt(Mathf.Clamp01(CivilizationStateManager.Instance.knowledge01) * 100f));

        int needPop = Mathf.Max(0, tech.requiredPopulation);
        int availPop = PlayersPopulationManager.Instance
            ? PlayersPopulationManager.Instance.GetAvailableTaskPopulation()
            : 0;

        bool levelOK = tech.IsEligibleForLevel(playerLevel);
        bool knowOK  = tech.IsEligibleForKnowledge(currentKnowledge);
        bool byOK    = tech.IsResearchableBy(station ? station.buildingID : null);
        bool costOK  = InventoryQuery.CanAfford(tech.researchCosts);
        bool popOK   = availPop >= needPop;

        researchButton.interactable = levelOK && knowOK && byOK && costOK && popOK;
    }
}
