using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CivilizationStatePanel : MonoBehaviour
{
    [Header("Panel Root & Open/Close")]
    public GameObject panelRoot;
    public Button closeButton;

    [Header("Bars View (graph-style)")]
    public CivilizationStateBarsView barsView;

    public bool IsShowing => panelRoot != null && panelRoot.activeInHierarchy;

    void Awake()
    {
        if (closeButton)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }

        Hide(); // start closed
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
        if (barsView) barsView.RefreshNow();
    }
}