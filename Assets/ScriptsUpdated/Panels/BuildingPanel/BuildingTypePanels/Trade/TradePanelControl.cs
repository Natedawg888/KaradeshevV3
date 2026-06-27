using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TradePanelControl : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject panelRoot;

    [Header("Trader List")]
    [SerializeField] private Transform tradersContent;
    [SerializeField] private GameObject traderEntryPrefab;

    [Header("Empty State")]
    [SerializeField] private TMP_Text emptyText;

    [Header("Navigation")]
    [SerializeField] private Button closeButton;

    [Header("Sub-Panels")]
    [SerializeField] private TraderPanelControl traderPanel;

    private TradeBuildingControl _building;
    private BuildingPanelControl _buildingPanel;

    public bool IsShowing => panelRoot != null && panelRoot.activeSelf;
    public event System.Action OnOpen;
    public event System.Action OnClose;

    private void Awake()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }
    }

    public void Show(TradeBuildingControl building, BuildingPanelControl buildingPanel)
    {
        _building = building;
        _buildingPanel = buildingPanel;
        panelRoot?.SetActive(true);
        Refresh();
        OnOpen?.Invoke();
    }

    public void Hide()
    {
        ClearList();
        panelRoot?.SetActive(false);
        _buildingPanel?.SoftShowFromChild();
        _building = null;
        _buildingPanel = null;
        OnClose?.Invoke();
    }

    private void Refresh()
    {
        ClearList();

        bool hasTrader = _building != null && _building.HasActiveTrader();

        if (emptyText != null)
            emptyText.gameObject.SetActive(!hasTrader);

        if (!hasTrader || tradersContent == null || traderEntryPrefab == null) return;

        var offer = _building.GetCurrentTraderOffer();
        var portrait = _building.GetCurrentTraderPortrait();
        SpawnTraderEntry(offer, portrait);
    }

    private void SpawnTraderEntry(TravelingTraderOffer offer, Sprite portrait)
    {
        var go = Instantiate(traderEntryPrefab, tradersContent);
        var entry = go.GetComponent<TraderEntryUI>();
        if (entry == null) return;

        entry.Bind(offer, portrait, () => OpenTraderPanel(offer, portrait));
    }

    private void OpenTraderPanel(TravelingTraderOffer offer, Sprite portrait)
    {
        if (traderPanel == null) return;
        traderPanel.Show(offer, portrait, _building);
    }

    private void ClearList()
    {
        if (tradersContent == null) return;
        for (int i = tradersContent.childCount - 1; i >= 0; i--)
            Destroy(tradersContent.GetChild(i).gameObject);
    }
}
