using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ReligiousRitualItemUI : MonoBehaviour
{
    [Header("Main")]
    public Image icon;
    public TMP_Text nameText;
    public TMP_Text descriptionText;
    public TMP_Text statusText;

    [Header("Requirements")]
    public TMP_Text turnsRequiredText;
    public TMP_Text workerCountText;

    [Header("Actions")]
    public Button startButton;

    [Header("Costs UI")]
    public Button costsButton;
    public GameObject costPanelRoot;
    public Transform costContentParent;
    public GameObject costEntryPrefab;
    public GameObject populationCostEntryPrefab;
    public Button closeCostsButton;

    [Header("Cost Button Sprites")]
    public Sprite resourceCostButtonSprite;
    public Sprite populationCostButtonSprite;
    public Sprite noCostButtonSprite;

    [Header("Population Sacrifice Age Icons")]
    public Sprite childPopulationSprite;
    public Sprite teenPopulationSprite;
    public Sprite adultPopulationSprite;
    public Sprite elderPopulationSprite;
    public Sprite anyAgePopulationSprite;

    [Header("Population Sacrifice Gender Icons")]
    public Sprite malePopulationSprite;
    public Sprite femalePopulationSprite;
    public Sprite anyGenderPopulationSprite;

    [Header("Colors")]
    public Color canAffordColor = new Color(0.20f, 0.70f, 0.20f);
    public Color cannotAffordColor = new Color(0.80f, 0.20f, 0.20f);
    public Color noCostColor = Color.white;
    public Color popEnoughColor = new Color(0.20f, 0.70f, 0.20f);
    public Color popNotEnoughColor = new Color(0.80f, 0.20f, 0.20f);

    private ReligionRitualDefinitionSO _ritual;
    private ReligiousBuildingControl _control;
    private ReligiousBuildingPanelControl _ownerPanel;
    private ReligiousRitualPanelControl _parentPanel;

    private readonly List<Individual> _tmpSacrificeCandidates = new List<Individual>(64);

    private void OnEnable()
    {
        if (PlayersPopulationManager.Instance != null)
            PlayersPopulationManager.Instance.OnPopulationChanged += HandlePopulationChanged;

        if (PlayerReligionManager.Instance != null)
            PlayerReligionManager.Instance.ReligionChanged += HandleReligionChanged;
    }

    private void OnDisable()
    {
        if (PlayersPopulationManager.Instance != null)
            PlayersPopulationManager.Instance.OnPopulationChanged -= HandlePopulationChanged;

        if (PlayerReligionManager.Instance != null)
            PlayerReligionManager.Instance.ReligionChanged -= HandleReligionChanged;
    }

    private void Update()
    {
        RefreshCostsButtonVisual();
    }

    private void HandlePopulationChanged()
    {
        RefreshState();
    }

    private void HandleReligionChanged()
    {
        RefreshState();
    }

    public void Setup(
        ReligionRitualDefinitionSO ritual,
        ReligiousBuildingControl control,
        ReligiousBuildingPanelControl ownerPanel,
        ReligiousRitualPanelControl parentPanel)
    {
        _ritual = ritual;
        _control = control;
        _ownerPanel = ownerPanel;
        _parentPanel = parentPanel;

        if (icon != null)
            icon.sprite = ritual != null ? ritual.icon : null;

        if (nameText != null)
            nameText.text = ritual != null ? ritual.displayName : "Unknown Ritual";

        if (descriptionText != null)
            descriptionText.text = ritual != null ? ritual.description : string.Empty;

        if (turnsRequiredText != null)
        {
            int turns = ritual != null ? Mathf.Max(1, ritual.turnsRequired) : 0;
            turnsRequiredText.text = $"Turns: {turns}";
        }

        if (workerCountText != null)
        {
            int workers = ritual != null ? Mathf.Max(0, ritual.workerCount) : 0;
            workerCountText.text = $"Pop: {workers}";
        }

        if (startButton != null)
        {
            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(OnClickStart);
        }

        if (costsButton != null)
        {
            costsButton.onClick.RemoveAllListeners();
            costsButton.onClick.AddListener(ToggleCostsPanel);
        }

        if (closeCostsButton != null)
        {
            closeCostsButton.onClick.RemoveAllListeners();
            closeCostsButton.onClick.AddListener(HideCostsPanel);
        }

        if (costPanelRoot != null)
            costPanelRoot.SetActive(false);

        RefreshState();
    }

    public void RefreshState()
    {
        if (_ritual == null || _control == null)
            return;

        SpiritDefinitionSO selectedSpirit = _ownerPanel != null ? _ownerPanel.SelectedSpirit : null;

        string status = string.Empty;
        bool canStart = true;

        if (_control.HasActiveRitual)
        {
            canStart = false;
            status = "Another ritual is already in progress.";
        }
        else if (_control.IsRitualOnCooldown(_ritual, out int cooldownLeft))
        {
            canStart = false;
            status = $"Cooldown: {cooldownLeft} turns";
        }
        else if (_ritual.IsSummoningRitual)
        {
            if (!HasEnoughPopulation())
            {
                canStart = false;
                status = $"Need {_ritual.workerCount} population.";
            }
            else
            {
                canStart = true;
                status = "Complete this ritual to receive spirit choices.";
            }
        }
        else if (selectedSpirit == null)
        {
            canStart = false;
            status = "Select a spirit first.";
        }
        else if (!HasEnoughPopulation())
        {
            canStart = false;
            status = $"Need {_ritual.workerCount} population.";
        }
        else if (HasResourceCost() && !CanAffordResourceCost())
        {
            canStart = false;
            status = "Not enough resources.";
        }
        else if (HasPopulationSacrificeCost() && !CanAffordPopulationSacrificeCost())
        {
            canStart = false;
            status = GetPopulationSacrificeMissingText();
        }

        if (statusText != null)
            statusText.text = status;

        if (startButton != null)
            startButton.interactable = canStart;

        if (workerCountText != null)
        {
            int workers = _ritual != null ? Mathf.Max(0, _ritual.workerCount) : 0;
            string hex = ColorUtility.ToHtmlStringRGB(HasEnoughPopulation() ? popEnoughColor : popNotEnoughColor);
            workerCountText.richText = true;
            workerCountText.text = $"Pop: <color=#{hex}>{workers}</color>";
        }

        if (costPanelRoot != null && costPanelRoot.activeSelf)
            PopulateCosts();
    }

    private void RefreshCostsButtonVisual()
    {
        if (costsButton == null)
            return;

        bool hasCost = HasDisplayableCost();
        costsButton.interactable = hasCost;

        Image buttonImage = costsButton.GetComponent<Image>();
        if (buttonImage == null)
            return;

        if (!hasCost)
        {
            buttonImage.color = noCostColor;
            if (noCostButtonSprite != null)
                buttonImage.sprite = noCostButtonSprite;
            return;
        }

        if (HasPopulationSacrificeCost())
        {
            if (populationCostButtonSprite != null)
                buttonImage.sprite = populationCostButtonSprite;
        }
        else if (HasResourceCost())
        {
            if (resourceCostButtonSprite != null)
                buttonImage.sprite = resourceCostButtonSprite;
        }

        buttonImage.color = CanAffordDisplayedCost() ? canAffordColor : cannotAffordColor;
    }

    private bool HasDisplayableCost()
    {
        return HasResourceCost() || HasPopulationSacrificeCost();
    }

    private bool CanAffordDisplayedCost()
    {
        if (HasResourceCost())
            return CanAffordResourceCost();

        if (HasPopulationSacrificeCost())
            return CanAffordPopulationSacrificeCost();

        return true;
    }

    private bool HasResourceCost()
    {
        return _ritual != null &&
               _ritual.IsResourceOffering &&
               _ritual.resourceDefinition != null &&
               _ritual.resourceAmount > 0;
    }

    private bool HasPopulationSacrificeCost()
    {
        return _ritual != null &&
               _ritual.IsPopulationSacrifice &&
               _ritual.sacrificeCount > 0;
    }

    private bool CanAffordResourceCost()
    {
        if (!HasResourceCost())
            return true;

        List<ResourceCost> costs = new List<ResourceCost>(1)
        {
            new ResourceCost
            {
                resource = _ritual.resourceDefinition,
                amount = _ritual.resourceAmount
            }
        };

        return InventoryQuery.CanAfford(costs);
    }

    private bool CanAffordPopulationSacrificeCost()
    {
        if (!HasPopulationSacrificeCost())
            return true;

        return GetEligiblePopulationSacrificeCount() >= Mathf.Max(1, _ritual.sacrificeCount);
    }

    private bool HasEnoughPopulation()
    {
        if (_ritual == null)
            return false;

        int need = Mathf.Max(0, _ritual.workerCount);
        int available = PlayersPopulationManager.Instance != null
            ? PlayersPopulationManager.Instance.GetAvailableTaskPopulation()
            : 0;

        return available >= need;
    }

    private void ToggleCostsPanel()
    {
        if (costPanelRoot == null || !HasDisplayableCost())
            return;

        bool show = !costPanelRoot.activeSelf;
        costPanelRoot.SetActive(show);

        if (show)
            PopulateCosts();
        else
            ClearCostContent();
    }

    private void HideCostsPanel()
    {
        if (costPanelRoot == null)
            return;

        costPanelRoot.SetActive(false);
        ClearCostContent();
    }

    private void PopulateCosts()
    {
        if (costContentParent == null)
            return;

        ClearCostContent();

        if (HasResourceCost())
        {
            PopulateResourceCost();
            return;
        }

        if (HasPopulationSacrificeCost())
            PopulatePopulationSacrificeCost();
    }

    private void PopulateResourceCost()
    {
        if (costEntryPrefab == null)
            return;

        int owned = InventoryQuery.GetOwned(_ritual.resourceDefinition);

        GameObject go = Instantiate(costEntryPrefab, costContentParent);
        BuildingCostEntry ui = go.GetComponent<BuildingCostEntry>();
        if (ui != null)
            ui.Bind(_ritual.resourceDefinition, _ritual.resourceAmount, owned);
    }

    private void PopulatePopulationSacrificeCost()
    {
        GameObject prefabToUse = populationCostEntryPrefab != null ? populationCostEntryPrefab : costEntryPrefab;
        if (prefabToUse == null)
            return;

        int available = GetEligiblePopulationSacrificeCount();
        string displayName = GetPopulationSacrificeDisplayName();
        Sprite ageSprite = GetSacrificeAgeGroupSprite(_ritual.sacrificeAgeFilter);
        Sprite genderSprite = GetSacrificeGenderSprite(_ritual.sacrificeSexFilter);

        GameObject go = Instantiate(prefabToUse, costContentParent);

        RitualPopulationCostEntryUI popUi = go.GetComponent<RitualPopulationCostEntryUI>();
        if (popUi != null)
        {
            popUi.Bind(
                ageSprite,
                genderSprite,
                displayName,
                Mathf.Max(1, _ritual.sacrificeCount),
                available);
            return;
        }

        Image[] images = go.GetComponentsInChildren<Image>(true);
        TMP_Text[] texts = go.GetComponentsInChildren<TMP_Text>(true);

        if (images != null && images.Length > 0)
            images[0].sprite = ageSprite;

        if (images != null && images.Length > 1)
            images[1].sprite = genderSprite;

        if (texts != null && texts.Length > 0)
            texts[0].text = displayName;

        if (texts != null && texts.Length > 1)
            texts[1].text = $"x {_ritual.sacrificeCount}";

        if (texts != null && texts.Length > 2)
            texts[2].text = $"Available: {available}";
    }

    private void ClearCostContent()
    {
        if (costContentParent == null)
            return;

        for (int i = costContentParent.childCount - 1; i >= 0; i--)
            Destroy(costContentParent.GetChild(i).gameObject);
    }

    private int GetEligiblePopulationSacrificeCount()
    {
        if (_ritual == null || !_ritual.IsPopulationSacrifice)
            return 0;

        PlayerFamilySimulationManager sim = PlayerFamilySimulationManager.Instance;
        PlayersPopulationManager pop = PlayersPopulationManager.Instance;

        if (sim == null || pop == null)
            return 0;

        _tmpSacrificeCandidates.Clear();

        IReadOnlyList<Individual> people = sim.GetIndividuals();
        if (people == null)
            return 0;

        for (int i = 0; i < people.Count; i++)
        {
            Individual person = people[i];
            if (!IsEligibleSacrificeCandidate(person, pop))
                continue;

            _tmpSacrificeCandidates.Add(person);
        }

        return _tmpSacrificeCandidates.Count;
    }

    private bool IsEligibleSacrificeCandidate(Individual person, PlayersPopulationManager pop)
    {
        if (person == null || !person.IsAlive)
            return false;

        if (person.IsBusy)
            return false;

        if (pop != null && pop.IsIndividualReservedAnywhere(person.Id))
            return false;

        if (!MatchesSacrificeAge(person.AggregatedAgeGroup, _ritual.sacrificeAgeFilter))
            return false;

        if (!MatchesSacrificeSex(person.Gender, _ritual.sacrificeSexFilter))
            return false;

        return true;
    }

    private bool MatchesSacrificeAge(AgeGroup ageGroup, SpiritSacrificeAgeFilter filter)
    {
        switch (filter)
        {
            case SpiritSacrificeAgeFilter.Child:
                return ageGroup == AgeGroup.Child;
            case SpiritSacrificeAgeFilter.Teen:
                return ageGroup == AgeGroup.Teen;
            case SpiritSacrificeAgeFilter.Adult:
                return ageGroup == AgeGroup.Adult;
            case SpiritSacrificeAgeFilter.Elder:
                return ageGroup == AgeGroup.Elder;
            default:
                return true;
        }
    }

    private bool MatchesSacrificeSex(Gender gender, SpiritSacrificeSexFilter filter)
    {
        switch (filter)
        {
            case SpiritSacrificeSexFilter.Male:
                return gender == Gender.Male;
            case SpiritSacrificeSexFilter.Female:
                return gender == Gender.Female;
            default:
                return true;
        }
    }

    private string GetPopulationSacrificeMissingText()
    {
        if (_ritual == null)
            return "Not enough eligible population.";

        int need = Mathf.Max(1, _ritual.sacrificeCount);
        int available = GetEligiblePopulationSacrificeCount();
        string displayName = GetPopulationSacrificeDisplayName();

        return $"Need {need} {displayName.ToLower()}, found {available}.";
    }

    private string GetPopulationSacrificeDisplayName()
    {
        string ageName = GetSacrificeAgeGroupDisplayName(_ritual.sacrificeAgeFilter);
        string sexName = GetSacrificeSexDisplayName(_ritual.sacrificeSexFilter);

        if (string.IsNullOrWhiteSpace(ageName))
            return sexName;

        if (string.IsNullOrWhiteSpace(sexName) || sexName == "Any")
            return ageName;

        if (ageName == "Any Age")
            return sexName;

        return $"{sexName} {ageName}";
    }

    private string GetSacrificeAgeGroupDisplayName(SpiritSacrificeAgeFilter filter)
    {
        switch (filter)
        {
            case SpiritSacrificeAgeFilter.Child:
                return "Child";
            case SpiritSacrificeAgeFilter.Teen:
                return "Teen";
            case SpiritSacrificeAgeFilter.Adult:
                return "Adult";
            case SpiritSacrificeAgeFilter.Elder:
                return "Elder";
            default:
                return "Any Age";
        }
    }

    private string GetSacrificeSexDisplayName(SpiritSacrificeSexFilter filter)
    {
        switch (filter)
        {
            case SpiritSacrificeSexFilter.Male:
                return "Male";
            case SpiritSacrificeSexFilter.Female:
                return "Female";
            default:
                return "Any";
        }
    }

    private Sprite GetSacrificeAgeGroupSprite(SpiritSacrificeAgeFilter filter)
    {
        switch (filter)
        {
            case SpiritSacrificeAgeFilter.Child:
                return childPopulationSprite != null ? childPopulationSprite : anyAgePopulationSprite;
            case SpiritSacrificeAgeFilter.Teen:
                return teenPopulationSprite != null ? teenPopulationSprite : anyAgePopulationSprite;
            case SpiritSacrificeAgeFilter.Adult:
                return adultPopulationSprite != null ? adultPopulationSprite : anyAgePopulationSprite;
            case SpiritSacrificeAgeFilter.Elder:
                return elderPopulationSprite != null ? elderPopulationSprite : anyAgePopulationSprite;
            default:
                return anyAgePopulationSprite;
        }
    }

    private Sprite GetSacrificeGenderSprite(SpiritSacrificeSexFilter filter)
    {
        switch (filter)
        {
            case SpiritSacrificeSexFilter.Male:
                return malePopulationSprite != null ? malePopulationSprite : anyGenderPopulationSprite;
            case SpiritSacrificeSexFilter.Female:
                return femalePopulationSprite != null ? femalePopulationSprite : anyGenderPopulationSprite;
            default:
                return anyGenderPopulationSprite;
        }
    }

    private void OnClickStart()
    {
        if (_ritual == null || _control == null)
            return;

        SpiritDefinitionSO selectedSpirit = _ownerPanel != null ? _ownerPanel.SelectedSpirit : null;

        if (_ritual.IsSummoningRitual)
            selectedSpirit = null;

        if (!_control.TryStartRitual(_ritual, selectedSpirit, out string reason))
        {
            Debug.LogWarning($"[ReligiousRitualItemUI] Failed to start ritual '{_ritual.displayName}': {reason}");

            if (HasDisplayableCost() && !CanAffordDisplayedCost() && costPanelRoot != null && !costPanelRoot.activeSelf)
            {
                costPanelRoot.SetActive(true);
                PopulateCosts();
            }

            RefreshState();
            return;
        }

        PlayersPopulationManager.Instance?.ForceSyncUI();

        HideCostsPanel();
        _parentPanel?.Refresh();
        RefreshState();
    }
}