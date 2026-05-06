using UnityEngine;
using UnityEngine.UI;

public class PlayerPopulationStatisticsPanelRoot : MonoBehaviour
{
    [Header("Panel Root & Open/Close")]
    public GameObject panelRoot;
    public Button openPanelButton;
    public Button closePanelButton;

    [Header("Tabs (Containers)")]
    public GameObject pieTabContainer;
    public GameObject barsTabContainer;
    public GameObject lineTabContainer;

    [Header("Tab Buttons")]
    public Button pieTabButton;
    public Button barsTabButton;
    public Button lineTabButton;

    [Header("Subviews")]
    public PopStatsTextsView textsView;
    public PopStatsPieView pieView;
    public PopStatsBarsView barsView;
    public PopStatsLineGraphView lineView;
    public PopStatsHealthView healthView;

    [Header("Civilisation Needs")]
    public Button civilisationNeedsButton;
    public PopCivilisationNeedsPanel civilisationNeedsPanel;

    [Header("Civilisation Health")]
    public Button civilisationHealthButton;
    public PopCivilisationHealthPanel civilisationHealthPanel;

    [Header("Civilization State")]
    public Button civilizationStateButton;
    public CivilizationStatePanel civilizationStatePanel;

    [Header("Spirits")]
    public Button spiritAffiliatedButton;
    public SpiritAffiliatedPanelControl spiritAffiliatedPanel;

    public CameraControl cameraControl;

    private enum GraphTab { Pie, Bars, Line }
    [SerializeField] private GraphTab _currentTab = GraphTab.Pie;

    private GameObject RootGO => panelRoot ? panelRoot : gameObject;
    public bool IsShowing => RootGO != null && RootGO.activeInHierarchy;

    void Awake()
    {
        if (openPanelButton)
        {
            openPanelButton.onClick.RemoveAllListeners();
            openPanelButton.onClick.AddListener(Show);
        }

        if (closePanelButton)
        {
            closePanelButton.onClick.RemoveAllListeners();
            closePanelButton.onClick.AddListener(Hide);
        }

        if (pieTabButton)
            pieTabButton.onClick.AddListener(() => ShowTab(GraphTab.Pie));

        if (barsTabButton)
            barsTabButton.onClick.AddListener(() => ShowTab(GraphTab.Bars));

        if (lineTabButton)
            lineTabButton.onClick.AddListener(() => ShowTab(GraphTab.Line));

        if (civilisationNeedsButton)
        {
            civilisationNeedsButton.onClick.RemoveAllListeners();
            civilisationNeedsButton.onClick.AddListener(() =>
            {
                if (civilisationNeedsPanel)
                    civilisationNeedsPanel.Show();
            });
        }

        if (civilisationHealthButton)
        {
            civilisationHealthButton.onClick.RemoveAllListeners();
            civilisationHealthButton.onClick.AddListener(() =>
            {
                if (civilisationHealthPanel)
                    civilisationHealthPanel.Show();
            });
        }

        if (civilizationStateButton)
        {
            civilizationStateButton.onClick.RemoveAllListeners();
            civilizationStateButton.onClick.AddListener(() =>
            {
                if (civilizationStatePanel)
                    civilizationStatePanel.Show();
            });
        }

        if (spiritAffiliatedButton)
        {
            spiritAffiliatedButton.onClick.RemoveAllListeners();
            spiritAffiliatedButton.onClick.AddListener(() =>
            {
                if (spiritAffiliatedPanel)
                    spiritAffiliatedPanel.Show();
            });
        }

        if (cameraControl == null)
            cameraControl = FindObjectOfType<CameraControl>();

        Hide();
    }

    public void Show()
    {
        RootGO.SetActive(true);
        ShowTab(_currentTab);
        TileInteraction.SetSelectionEnabled(false);

        cameraControl?.PushInputLock();

        if (textsView) textsView.RefreshNow();
        if (healthView) healthView.RefreshNow();
    }

    public void Hide()
    {
        if (lineView && lineView.isActiveAndEnabled)
            lineView.PrepareForHide();

        if (spiritAffiliatedPanel)
            spiritAffiliatedPanel.Hide();

        TileInteraction.SetSelectionEnabled(false);
        TileInteraction.GetInstance()?.EnableSelectionAfter(0.01f);

        cameraControl?.PopInputLock();

        RootGO.SetActive(false);
    }

    private void ShowTab(GraphTab tab)
    {
        _currentTab = tab;

        if (pieTabContainer) pieTabContainer.SetActive(tab == GraphTab.Pie);
        if (barsTabContainer) barsTabContainer.SetActive(tab == GraphTab.Bars);
        if (lineTabContainer) lineTabContainer.SetActive(tab == GraphTab.Line);

        if (pieTabButton) pieTabButton.interactable = tab != GraphTab.Pie;
        if (barsTabButton) barsTabButton.interactable = tab != GraphTab.Bars;
        if (lineTabButton) lineTabButton.interactable = tab != GraphTab.Line;

        switch (tab)
        {
            case GraphTab.Pie:
                if (pieView) pieView.RefreshNow();
                break;
            case GraphTab.Bars:
                if (barsView) barsView.RefreshNow();
                break;
            case GraphTab.Line:
                if (lineView) lineView.RefreshNow();
                break;
        }

        if (textsView) textsView.RefreshNow();
        if (healthView) healthView.RefreshNow();
    }

    public void InstallPopulationRefs(PlayerPopulationStatistic stats, PlayersPopulationManager populationManager, bool refreshNow = true)
    {
        PopStatsSubviewBase[] subviews = GetComponentsInChildren<PopStatsSubviewBase>(true);
        if (subviews == null || subviews.Length == 0)
            return;

        for (int i = 0; i < subviews.Length; i++)
        {
            if (subviews[i] == null)
                continue;

            subviews[i].SetDataSources(stats, populationManager, refreshNow);
        }
    }
}