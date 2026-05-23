using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OfferingPanelControl : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject panelRoot;

    [Header("Trader Offering Display")]
    [SerializeField] private Image traderOfferingIcon;
    [SerializeField] private TMP_Text traderOfferingNameText;
    [SerializeField] private TMP_Text traderOfferingAmountText;
    [SerializeField] private Button increaseDesiredButton;
    [SerializeField] private Button decreaseDesiredButton;

    [Header("Player Offer List")]
    [SerializeField] private Transform playerOfferContent;
    [SerializeField] private GameObject playerOfferingItemPrefab;

    [Header("Available Resources")]
    [SerializeField] private Transform availableContent;
    [SerializeField] private GameObject availableResourceItemPrefab;

    [Header("Actions")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button backButton;

    [Header("Feedback")]
    [SerializeField] private TMP_Text feedbackText;

    private ResourceAmount _traderResource;
    private TravelingTraderOffer _traderOffer;
    private TradeBuildingControl _building;

    private int _desiredAmount;
    private readonly List<ResourceAmount> _playerGiving = new List<ResourceAmount>();

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
        _traderOffer = traderOffer;
        _building = building;
        _desiredAmount = traderResource.amount;
        _playerGiving.Clear();

        SetFeedback(string.Empty);
        panelRoot?.SetActive(true);

        RefreshTraderOfferingDisplay();
        RefreshPlayerOfferList();
        RefreshAvailableList();
    }

    public void Hide()
    {
        _playerGiving.Clear();
        _traderResource = null;
        _traderOffer = null;
        _building = null;
        panelRoot?.SetActive(false);
    }

    // ── Desired amount ────────────────────────────────────────────

    private void IncreaseDesired()
    {
        if (_traderResource == null) return;
        _desiredAmount = Mathf.Min(_desiredAmount + 1, _traderResource.amount);
        RefreshTraderOfferingDisplay();
    }

    private void DecreaseDesired()
    {
        _desiredAmount = Mathf.Max(1, _desiredAmount - 1);
        RefreshTraderOfferingDisplay();
    }

    private void RefreshTraderOfferingDisplay()
    {
        if (_traderResource?.resource == null) return;

        if (traderOfferingIcon != null)
            traderOfferingIcon.sprite = _traderResource.resource.resourceIcon;

        if (traderOfferingNameText != null)
            traderOfferingNameText.text = _traderResource.resource.resourceName;

        if (traderOfferingAmountText != null)
            traderOfferingAmountText.text = _desiredAmount.ToString();
    }

    // ── Player offer list ─────────────────────────────────────────

    private void RefreshPlayerOfferList()
    {
        _playerGiving.RemoveAll(e => e.amount <= 0);

        ClearContent(playerOfferContent);
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
