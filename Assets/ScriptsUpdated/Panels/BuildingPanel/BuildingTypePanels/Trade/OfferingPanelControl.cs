using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OfferingPanelControl : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject panelRoot;

    [Header("Trader Offering Display")]
    [SerializeField] private GameObject resourceIconParent;
    [SerializeField] private Image traderOfferingIcon;
    [SerializeField] private GameObject populationIconParent;
    [SerializeField] private Image traderPopulationAgeIcon;
    [SerializeField] private Image traderPopulationGenderIcon;
    [SerializeField] private TMP_Text traderOfferingNameText;
    [SerializeField] private TMP_Text traderOfferingAmountText;
    [SerializeField] private Button increaseDesiredButton;
    [SerializeField] private Button decreaseDesiredButton;

    [Header("Population Age Sprites")]
    [SerializeField] private Sprite childSprite;
    [SerializeField] private Sprite teenSprite;
    [SerializeField] private Sprite adultSprite;
    [SerializeField] private Sprite elderSprite;

    [Header("Population Gender Sprites")]
    [SerializeField] private Sprite maleSprite;
    [SerializeField] private Sprite femaleSprite;

    [Header("Player Offer List")]
    [SerializeField] private Transform playerOfferContent;
    [SerializeField] private GameObject playerOfferingItemPrefab;
    [SerializeField] private GameObject playerOfferingPopulationItemPrefab;

    [Header("Available Resources")]
    [SerializeField] private Transform availableContent;
    [SerializeField] private GameObject availableResourceItemPrefab;
    [SerializeField] private GameObject availablePopulationItemPrefab;

    [Header("Actions")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button backButton;

    [Header("Feedback")]
    [SerializeField] private TMP_Text feedbackText;

    private ResourceAmount _traderResource;
    private TradePopulationEntry _traderPopulation;
    private TravelingTraderOffer _traderOffer;
    private TradeBuildingControl _building;

    private int _desiredAmount;
    private readonly List<ResourceAmount> _playerGiving = new List<ResourceAmount>();
    private readonly List<TradePopulationEntry> _playerGivingPopulation = new List<TradePopulationEntry>();

    private void Awake()
    {
        if (increaseDesiredButton != null)
            increaseDesiredButton.onClick.AddListener(IncreaseDesired);

        if (decreaseDesiredButton != null)
            decreaseDesiredButton.onClick.AddListener(DecreaseDesired);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(ConfirmTrade);

        if (backButton != null)
            backButton.onClick.AddListener(Hide);
    }

    public void Show(ResourceAmount traderResource, TravelingTraderOffer traderOffer, TradeBuildingControl building)
    {
        _traderResource = traderResource;
        _traderPopulation = null;
        _traderOffer = traderOffer;
        _building = building;
        _desiredAmount = traderResource.amount;
        _playerGiving.Clear();
        _playerGivingPopulation.Clear();

        SetFeedback(string.Empty);
        panelRoot?.SetActive(true);

        RefreshTraderOfferingDisplay();
        RefreshPlayerOfferList();
        RefreshAvailableList();
    }

    public void Show(TradePopulationEntry populationEntry, TravelingTraderOffer traderOffer, TradeBuildingControl building)
    {
        _traderPopulation = populationEntry;
        _traderResource = null;
        _traderOffer = traderOffer;
        _building = building;
        _desiredAmount = populationEntry.count;
        _playerGiving.Clear();
        _playerGivingPopulation.Clear();

        SetFeedback(string.Empty);
        panelRoot?.SetActive(true);

        RefreshTraderOfferingDisplay();
        RefreshPlayerOfferList();
        RefreshAvailablePopulationList();
    }

    public void Hide()
    {
        _playerGiving.Clear();
        _playerGivingPopulation.Clear();
        _traderResource = null;
        _traderPopulation = null;
        _traderOffer = null;
        _building = null;
        panelRoot?.SetActive(false);
    }

    // ── Desired amount ────────────────────────────────────────────

    private void IncreaseDesired()
    {
        int max = _traderResource != null ? _traderResource.amount
                : _traderPopulation != null ? _traderPopulation.count
                : 1;
        _desiredAmount = Mathf.Min(_desiredAmount + 1, max);
        RefreshTraderOfferingDisplay();
    }

    private void DecreaseDesired()
    {
        _desiredAmount = Mathf.Max(1, _desiredAmount - 1);
        RefreshTraderOfferingDisplay();
    }

    private void RefreshTraderOfferingDisplay()
    {
        if (_traderResource != null)
        {
            resourceIconParent?.SetActive(true);
            populationIconParent?.SetActive(false);

            if (traderOfferingIcon != null)
                traderOfferingIcon.sprite = _traderResource.resource?.resourceIcon;

            if (traderOfferingNameText != null)
                traderOfferingNameText.text = _traderResource.resource?.resourceName;

            if (traderOfferingAmountText != null)
                traderOfferingAmountText.text = _desiredAmount.ToString();
        }
        else if (_traderPopulation != null)
        {
            resourceIconParent?.SetActive(false);
            populationIconParent?.SetActive(true);

            if (traderPopulationAgeIcon != null)
                traderPopulationAgeIcon.sprite = SpriteForAge(_traderPopulation.ageGroup);

            if (traderPopulationGenderIcon != null)
                traderPopulationGenderIcon.sprite = SpriteForGender(_traderPopulation.gender);

            if (traderOfferingNameText != null)
                traderOfferingNameText.text = LabelForAge(_traderPopulation.ageGroup);

            if (traderOfferingAmountText != null)
                traderOfferingAmountText.text = _desiredAmount.ToString();
        }
    }

    // ── Player offer list ─────────────────────────────────────────

    private void RefreshPlayerOfferList()
    {
        ClearContent(playerOfferContent);

        if (_traderPopulation != null)
        {
            _playerGivingPopulation.RemoveAll(e => e.count <= 0);
            if (playerOfferingPopulationItemPrefab == null || playerOfferContent == null) return;

            var pop = PlayersPopulationManager.Instance;
            foreach (var entry in _playerGivingPopulation)
            {
                int maxAvailable = 0;
                if (pop != null)
                    foreach (var g in pop.AllPopulations)
                        if (g.ageGroup == entry.ageGroup && g.gender == entry.gender)
                            { maxAvailable = g.AvailableForTask(); break; }

                var go = Instantiate(playerOfferingPopulationItemPrefab, playerOfferContent);
                var item = go.GetComponent<PlayerOfferingPopulationItemUI>();
                if (item == null) continue;

                item.Bind(entry, maxAvailable, () =>
                {
                    RefreshPlayerOfferList();
                    SetFeedback(string.Empty);
                });
            }
        }
        else
        {
            _playerGiving.RemoveAll(e => e.amount <= 0);
            if (playerOfferingItemPrefab == null || playerOfferContent == null) return;

            foreach (var entry in _playerGiving)
            {
                var go = Instantiate(playerOfferingItemPrefab, playerOfferContent);
                var item = go.GetComponent<PlayerOfferingItemUI>();
                if (item == null) continue;

                item.Bind(entry, () =>
                {
                    RefreshPlayerOfferList();
                    SetFeedback(string.Empty);
                });
            }
        }
    }

    // ── Available resources ───────────────────────────────────────

    private void RefreshAvailableList()
    {
        ClearContent(availableContent);
        if (availableResourceItemPrefab == null || availableContent == null) return;

        var inv = PlayerInventoryManager.Instance;
        if (inv == null) return;

        var allStacks = new List<InventoryStack>();
        allStacks.AddRange(inv.GetStacks(ResourceType.Material));
        allStacks.AddRange(inv.GetStacks(ResourceType.Food));
        allStacks.AddRange(inv.GetStacks(ResourceType.Water));

        foreach (var stack in allStacks)
        {
            if (stack?.definition == null || stack.amount <= 0) continue;

            var go = Instantiate(availableResourceItemPrefab, availableContent);
            var item = go.GetComponent<AvailableResourceItemUI>();
            if (item == null) continue;

            item.Bind(stack, AddResourceToOffer);
        }
    }

    private void RefreshAvailablePopulationList()
    {
        ClearContent(availableContent);
        if (availablePopulationItemPrefab == null || availableContent == null) return;

        var pop = PlayersPopulationManager.Instance;
        if (pop == null) return;

        foreach (var group in pop.AllPopulations)
        {
            int available = group.AvailableForTask();
            if (available <= 0) continue;

            var go = Instantiate(availablePopulationItemPrefab, availableContent);
            var item = go.GetComponent<AvailablePopulationItemUI>();
            if (item == null) continue;

            var entry = new TradePopulationEntry
            {
                ageGroup = group.ageGroup,
                gender   = group.gender,
                count    = available
            };
            item.Bind(entry, AddPopulationToOffer);
        }
    }

    private void AddPopulationToOffer(TradePopulationEntry source)
    {
        if (source == null) return;

        foreach (var entry in _playerGivingPopulation)
        {
            if (entry.ageGroup != source.ageGroup || entry.gender != source.gender) continue;

            var pop = PlayersPopulationManager.Instance;
            int available = 0;
            if (pop != null)
                foreach (var g in pop.AllPopulations)
                    if (g.ageGroup == source.ageGroup && g.gender == source.gender)
                        { available = g.AvailableForTask(); break; }

            if (entry.count < available)
            {
                entry.count++;
                RefreshPlayerOfferList();
            }
            return;
        }

        _playerGivingPopulation.Add(new TradePopulationEntry
        {
            ageGroup = source.ageGroup,
            gender   = source.gender,
            count    = 1
        });
        RefreshPlayerOfferList();
    }

    private void AddResourceToOffer(ResourceDefinition def)
    {
        if (def == null) return;

        foreach (var entry in _playerGiving)
        {
            if (entry.resource != def) continue;
            int available = PlayerInventoryManager.Instance?.GetAmount(def) ?? 0;
            if (entry.amount < available)
            {
                entry.amount++;
                RefreshPlayerOfferList();
            }
            return;
        }

        int avail = PlayerInventoryManager.Instance?.GetAmount(def) ?? 0;
        if (avail > 0)
        {
            _playerGiving.Add(new ResourceAmount { resource = def, amount = 1 });
            RefreshPlayerOfferList();
        }
    }

    // ── Confirm ───────────────────────────────────────────────────

    private void ConfirmTrade()
    {
        if (_building == null || _traderResource?.resource == null) return;

        var offer = new TradeOffer();
        offer.playerGivesResources.AddRange(_playerGiving);
        offer.traderGivesResources.Add(new ResourceAmount
        {
            resource = _traderResource.resource,
            amount   = _desiredAmount
        });

        var result = _building.SubmitPlayerOffer(offer);
        SetFeedback(result.message);

        if (result.resultType == TradeResultType.Accepted)
            Hide();
    }

    // ── Helpers ───────────────────────────────────────────────────

    private string LabelForAge(AgeGroup age)
    {
        switch (age)
        {
            case AgeGroup.Child: return "Child";
            case AgeGroup.Teen:  return "Teen";
            case AgeGroup.Adult: return "Adult";
            case AgeGroup.Elder: return "Elder";
            default:             return "Adult";
        }
    }

    private Sprite SpriteForAge(AgeGroup age)
    {
        switch (age)
        {
            case AgeGroup.Child: return childSprite;
            case AgeGroup.Teen:  return teenSprite;
            case AgeGroup.Adult: return adultSprite;
            case AgeGroup.Elder: return elderSprite;
            default:             return adultSprite;
        }
    }

    private Sprite SpriteForGender(Gender gender)
    {
        switch (gender)
        {
            case Gender.Male:   return maleSprite;
            case Gender.Female: return femaleSprite;
            default:            return maleSprite;
        }
    }

    private void SetFeedback(string message)
    {
        if (feedbackText != null)
            feedbackText.text = message;
    }

    private static void ClearContent(Transform content)
    {
        if (content == null) return;
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);
    }
}
