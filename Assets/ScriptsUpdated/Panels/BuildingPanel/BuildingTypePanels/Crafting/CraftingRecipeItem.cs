using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CraftingRecipeItem : MonoBehaviour
{
    [Header("Main")]
    public Image icon;
    public TMP_Text title;

    [Header("Craft")]
    public Button craftButton;

    [Header("Multiplier (±)")]
    public Button minusButton;
    public TMP_Text multiplierText;
    public Button plusButton;

    [Header("Costs UI")]
    public TMP_Text costLabel;
    public Button costsButton;
    public GameObject costPanelRoot;
    public Transform costContentParent;
    public GameObject costEntryPrefab;
    public Button closeCostsButton;

    [Header("Output UI")]
    public Button outputButton;
    public GameObject rewardEntryPrefab;

    [Header("Info UI")]
    public Button infoButton;
    public GameObject infoPanelRoot;
    public TMP_Text infoTurnsText;
    public TMP_Text infoPopulationText;
    public Button closeInfoButton;

    [Header("Cost Sets UI (Optional)")]
    public Button prevCostSetButton;
    public Button nextCostSetButton;

    [Header("Colors")]
    public Color canAffordColor = new(0.20f, 0.70f, 0.20f);
    public Color cannotAffordColor = new(0.80f, 0.20f, 0.20f);
    public Color popEnoughColor = new(0.20f, 0.70f, 0.20f);
    public Color popNotEnoughColor = new(0.80f, 0.20f, 0.20f);

    private CraftingRecipe _def;
    private CraftingBuildingControl _crafting;
    private System.Action _onCraftStarted;
    private bool _showingOutput = false;

    private int _multiplier = 1;

    public void Bind(
        CraftingRecipe recipe,
        CraftingBuildingControl craftingBuilding,
        System.Action onCraftStarted = null)
    {
        _def = recipe;
        _crafting = craftingBuilding;
        _onCraftStarted = onCraftStarted;

        if (_def.HasAlternateCostSets && _def.activeCostSetIndex == -1)
            _def.activeCostSetIndex = 0;

        if (icon && _def.craftingIcon) icon.sprite = _def.craftingIcon;
        if (title) title.text = string.IsNullOrWhiteSpace(_def.craftingName) ? _def.craftingID : _def.craftingName;

        _multiplier = 1;
        WireMultiplierButtons();
        UpdateMultiplierVisuals();

        if (craftButton)
        {
            craftButton.onClick.RemoveAllListeners();
            craftButton.onClick.AddListener(OnCraftClicked);
        }

        if (outputButton)
        {
            outputButton.onClick.RemoveAllListeners();
            outputButton.onClick.AddListener(ToggleOutputView);
            UpdateOutputButtonLabel();
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
                _def.CyclePrevCostSet();
                if (costPanelRoot && costPanelRoot.activeSelf)
                {
                    if (_showingOutput) PopulateOutputs();
                    else PopulateCosts();
                }
                RefreshAffordabilityColor();
            });
            prevCostSetButton.gameObject.SetActive(_def.HasAlternateCostSets);
        }

        if (nextCostSetButton)
        {
            nextCostSetButton.onClick.RemoveAllListeners();
            nextCostSetButton.onClick.AddListener(() =>
            {
                _def.CycleNextCostSet();
                if (costPanelRoot && costPanelRoot.activeSelf)
                {
                    if (_showingOutput) PopulateOutputs();
                    else PopulateCosts();
                }
                RefreshAffordabilityColor();
            });
            nextCostSetButton.gameObject.SetActive(_def.HasAlternateCostSets);
        }

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

        PopulateInfo();
        RefreshPopulationIndicators();
        RefreshAffordabilityColor();
    }

    private void OnDestroy()
    {
        if (PlayersPopulationManager.Instance != null)
            PlayersPopulationManager.Instance.OnPopulationChanged -= RefreshPopulationIndicators;
    }

    private void Update()
    {
        RefreshAffordabilityColor();
    }

    private void WireMultiplierButtons()
    {
        if (minusButton)
        {
            minusButton.onClick.RemoveAllListeners();
            minusButton.onClick.AddListener(() => SetMultiplier(_multiplier - 1));
        }
        if (plusButton)
        {
            plusButton.onClick.RemoveAllListeners();
            plusButton.onClick.AddListener(() => SetMultiplier(_multiplier + 1));
        }
    }

    private void SetMultiplier(int v)
    {
        int clamped = _def.ClampMultiplier(v);
        if (clamped == _multiplier) { UpdateMultiplierVisuals(); return; }

        _multiplier = clamped;
        UpdateMultiplierVisuals();

        if (costPanelRoot && costPanelRoot.activeSelf)
        {
            if (_showingOutput) PopulateOutputs();
            else PopulateCosts();
        }

        PopulateInfo();
        RefreshAffordabilityColor();
    }

    private void UpdateMultiplierVisuals()
    {
        if (multiplierText)
            multiplierText.text = $"x{_multiplier}";

        if (minusButton) minusButton.interactable = _multiplier > 1;
        if (plusButton) plusButton.interactable = _multiplier < Mathf.Max(1, _def.maxMultiplier);
    }

    private void RefreshAffordabilityColor()
    {
        if (!costsButton) return;
        var img = costsButton.GetComponent<Image>();
        if (!img) return;

        bool canAfford = _def.CanAfford(_multiplier);
        img.color = canAfford ? canAffordColor : cannotAffordColor;

        if (craftButton)
            craftButton.interactable = canAfford && HasEnoughPopulation();
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
        else ClearCostContent();
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

        var costs = _def.GetCostsFor(_multiplier);
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

    private void ToggleInfoPanel()
    {
        if (!infoPanelRoot) return;
        bool show = !infoPanelRoot.activeSelf;
        infoPanelRoot.SetActive(show);
        if (show) PopulateInfo();
    }

    private void HideInfoPanel()
    {
        if (!infoPanelRoot) return;
        infoPanelRoot.SetActive(false);
    }

    private void PopulateInfo()
    {
        if (infoTurnsText)
            infoTurnsText.text = $"Turns: {Mathf.Max(1, _def.craftTurnsRequired)}";

        if (infoPopulationText)
        {
            int need = _def.GetPopulationRequiredFor(_multiplier);
            int available = PlayersPopulationManager.Instance
                ? PlayersPopulationManager.Instance.GetAvailableTaskPopulation()
                : 0;

            bool enough = available >= need;
            string hex = ColorUtility.ToHtmlStringRGB(enough ? popEnoughColor : popNotEnoughColor);
            infoPopulationText.richText = true;

            if (_def.scalePopulationWithMultiplier && _multiplier > 1 && _def.requiredPopulation > 0)
                infoPopulationText.text = $"Population: <color=#{hex}>{_def.requiredPopulation} x {_multiplier} = {need}</color>";
            else
                infoPopulationText.text = $"Population: <color=#{hex}>{need}</color>";
        }
    }

    private void RefreshPopulationIndicators()
    {
        if (infoButton)
        {
            var img = infoButton.GetComponent<Image>();
            if (img != null)
                img.color = HasEnoughPopulation() ? popEnoughColor : popNotEnoughColor;
        }

        if (infoPanelRoot && infoPanelRoot.activeSelf)
            PopulateInfo();

        if (craftButton)
            craftButton.interactable = _def.CanAfford(_multiplier) && HasEnoughPopulation();
    }

    private bool HasEnoughPopulation()
    {
        int need = _def.GetPopulationRequiredFor(_multiplier);
        int available = PlayersPopulationManager.Instance
            ? PlayersPopulationManager.Instance.GetAvailableTaskPopulation()
            : 0;

        return available >= need;
    }

    private void OnCraftClicked()
    {
        if (_crafting == null || _def == null) return;

        bool ok = _crafting.StartCrafting(_def, _multiplier);
        if (!ok)
        {
            if (!_def.CanAfford(_multiplier))
            {
                if (costPanelRoot && !costPanelRoot.activeSelf)
                {
                    costPanelRoot.SetActive(true);
                    PopulateCosts();
                }
            }
            else if (!HasEnoughPopulation())
            {
                if (infoPanelRoot && !infoPanelRoot.activeSelf)
                {
                    infoPanelRoot.SetActive(true);
                    PopulateInfo();
                }
            }
            return;
        }

        _onCraftStarted?.Invoke();
    }

    private void UpdateOutputButtonLabel()
    {
        var label = costLabel;
        if (label) label.text = _showingOutput ? "Show Output" : "Show Costs";
    }

    private void PopulateOutputs()
    {
        if (!costContentParent || !rewardEntryPrefab) return;
        ClearCostContent();

        var outputs = _def.GetOutputFor(_multiplier);
        if (outputs == null || outputs.Count == 0) return;

        for (int i = 0; i < outputs.Count; i++)
        {
            var o = outputs[i];
            if (o == null || o.resource == null || o.amount <= 0) continue;

            var go = Instantiate(rewardEntryPrefab, costContentParent);
            var ui = go.GetComponent<BuildingRewardEntry>();
            if (ui) ui.Bind(o.resource, o.amount);
        }
    }
}