using UnityEngine;
using UnityEngine.UI;

public class PopCivilisationHealthPanel : MonoBehaviour
{
    [Header("Panel Root & Open/Close")]
    public GameObject panelRoot;
    public Button closeButton;

    [Header("Embedded Health View")]
    public PopStatsHealthView healthView;

    private PlayersPopulationManager popMgr;

    public bool IsShowing => panelRoot != null && panelRoot.activeInHierarchy;

    void Awake()
    {
        popMgr = PlayersPopulationManager.Instance;

        if (closeButton)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }

        Hide();
    }

    private void OnEnable()
    {
        popMgr = PlayersPopulationManager.Instance;

        if (popMgr != null)
            popMgr.OnPopulationChanged += HandlePopulationChanged;
    }

    private void OnDisable()
    {
        if (popMgr != null)
            popMgr.OnPopulationChanged -= HandlePopulationChanged;
    }

    private void HandlePopulationChanged()
    {
        if (IsShowing)
            RefreshNow();
    }

    public void Show()
    {
        if (panelRoot) panelRoot.SetActive(true);
        RefreshNow();
    }

    public void Hide()
    {
        if (panelRoot) panelRoot.SetActive(false);
    }

    public void RefreshNow()
    {
        if (healthView)
            healthView.RefreshNow();
    }
}