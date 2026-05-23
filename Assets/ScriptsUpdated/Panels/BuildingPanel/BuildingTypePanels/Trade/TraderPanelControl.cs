using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TraderPanelControl : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject panelRoot;

    [Header("Trader Info")]
    [SerializeField] private Image traderIcon;
    [SerializeField] private TMP_Text traderNameText;

    [Header("Offerings List")]
    [SerializeField] private Transform offeringsContent;
    [SerializeField] private GameObject offeringItemPrefab;
    [SerializeField] private GameObject offeringPopulationItemPrefab;

    [Header("Navigation")]
    [SerializeField] private Button backButton;

    [Header("Sub-Panels")]
    [SerializeField] private OfferingPanelControl offeringPanel;

    private TravelingTraderOffer _offer;
    private TradeBuildingControl _building;

    private void Awake()
    {
        if (backButton != null)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(Hide);
        }
    }

    public void Show(TravelingTraderOffer offer, Sprite portrait, TradeBuildingControl building)
    {
        _offer = offer;
        _building = building;

        if (traderIcon != null)
        {
            traderIcon.sprite = portrait;
            traderIcon.gameObject.SetActive(portrait != null);
        }

        if (traderNameText != null)
            traderNameText.text = offer.traderName;

        panelRoot?.SetActive(true);
        PopulateOfferings();
    }

    public void Hide()
    {
        ClearOfferings();
        panelRoot?.SetActive(false);
        _offer = null;
        _building = null;
    }

    private void PopulateOfferings()
    {
        ClearOfferings();
        if (offeringsContent == null) return;

        if (_offer?.offeredResources != null && offeringItemPrefab != null)
        {
            foreach (var resource in _offer.offeredResources)
            {
                if (resource?.resource == null) continue;
                if (PlayerResourceKnowledgeManager.Instance == null || !PlayerResourceKnowledgeManager.Instance.IsKnown(resource.resource)) continue;

                var go = Instantiate(offeringItemPrefab, offeringsContent);
                var item = go.GetComponent<OfferingItemUI>();
                if (item == null) continue;

                var captured = resource;
                item.Bind(resource, () => OpenOfferingPanel(captured));
            }
        }

        if (_offer?.offeredPopulation?.entries != null && offeringPopulationItemPrefab != null)
        {
            foreach (var entry in _offer.offeredPopulation.entries)
            {
                if (entry == null || entry.count <= 0) continue;

                var go = Instantiate(offeringPopulationItemPrefab, offeringsContent);
                var item = go.GetComponent<OfferingPopulationItemUI>();
                if (item == null) continue;

                item.Bind(entry, () => { });
            }
        }
    }

    private void OpenOfferingPanel(ResourceAmount resource)
    {
        if (offeringPanel == null) return;
        offeringPanel.Show(resource, _offer, _building);
    }

    private void ClearOfferings()
    {
        if (offeringsContent == null) return;
        for (int i = offeringsContent.childCount - 1; i >= 0; i--)
            Destroy(offeringsContent.GetChild(i).gameObject);
    }
}
