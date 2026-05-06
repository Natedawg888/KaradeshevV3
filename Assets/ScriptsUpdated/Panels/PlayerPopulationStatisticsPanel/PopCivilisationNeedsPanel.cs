using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class PopCivilisationNeedsPanel : MonoBehaviour
{
    [Header("Panel Root & Open/Close")]
    public GameObject panelRoot;
    public Button openButton;   // optional – hook from your HUD
    public Button closeButton;

    [Header("View")]
    public PopCivilisationNeedsView needsView;

    public bool IsShowing => panelRoot != null && panelRoot.activeInHierarchy;

    void Awake()
    {
        if (openButton)
        {
            openButton.onClick.RemoveAllListeners();
            openButton.onClick.AddListener(Show);
        }
        if (closeButton)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }
        Hide();
    }

    public void Show()
    {
        if (panelRoot) panelRoot.SetActive(true);
        if (needsView) needsView.RefreshNow();
    }

    public void Hide()
    {
        if (panelRoot) panelRoot.SetActive(false);
    }

    // If the panel remains open while data changes, call this from a tick/UI event.
    public void RefreshNow()
    {
        if (!panelRoot || !panelRoot.activeInHierarchy) return;
        if (needsView) needsView.RefreshNow();
    }
}