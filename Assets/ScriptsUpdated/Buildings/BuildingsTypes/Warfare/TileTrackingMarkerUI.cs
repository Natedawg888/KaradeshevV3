using UnityEngine;
using UnityEngine.UI;

public class TileTrackingMarkerUI : MonoBehaviour
{
    public GameObject root;
    public Image iconImage;

    [Header("Timer UI (optional)")]
    public TimerUI timerUI;

    [Header("Canvas Control")]
    [SerializeField] private Canvas worldCanvas;
    [SerializeField] private bool autoResolveCanvas = true;

    private void Awake()
    {
        ResolveCanvas();
        Hide();
    }

    private void ResolveCanvas()
    {
        if (worldCanvas == null && autoResolveCanvas)
            worldCanvas = GetComponentInParent<Canvas>(true);
    }

    public void Show(Sprite icon, int maxTurns, int turnsLeft)
    {
        ResolveCanvas();

        if (worldCanvas != null)
        {
            worldCanvas.enabled = true;
            worldCanvas.gameObject.SetActive(true);
        }

        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.gameObject.SetActive(icon != null);
        }

        if (timerUI != null)
        {
            timerUI.gameObject.SetActive(true);
            timerUI.SetState(maxTurns, turnsLeft);
        }

        if (root != null) root.SetActive(true);
        else gameObject.SetActive(true);
    }

    public void UpdateTurns(int maxTurns, int turnsLeft)
    {
        if (timerUI != null)
            timerUI.SetState(maxTurns, turnsLeft);
    }

    public void Show(Sprite icon)
    {
        Show(icon, 1, 1);
    }

    public void Hide()
    {
        if (timerUI != null)
            timerUI.gameObject.SetActive(false);

        if (root != null) root.SetActive(false);
        else gameObject.SetActive(false);

        RefreshCanvasVisibility();
    }

    private void RefreshCanvasVisibility()
    {
        ResolveCanvas();

        if (worldCanvas == null)
            return;

        // Only auto-hide dedicated action canvases like UnitTileActions.
        if (!IsActionCanvas(worldCanvas))
            return;

        bool anyVisibleActionUI = HasVisibleActionUI(worldCanvas);

        worldCanvas.enabled = anyVisibleActionUI;
        worldCanvas.gameObject.SetActive(anyVisibleActionUI);
    }

    private bool IsActionCanvas(Canvas canvas)
    {
        if (canvas == null) return false;

        string n = canvas.gameObject.name;
        return n == "UnitTileActions" || n.StartsWith("UnitTileActions");
    }

    private bool HasVisibleActionUI(Canvas canvas)
    {
        if (canvas == null)
            return false;

        var moveUIs = canvas.GetComponentsInChildren<TileMovementUI>(true);
        for (int i = 0; i < moveUIs.Length; i++)
        {
            var ui = moveUIs[i];
            if (ui == null) continue;

            if (ui.moveHereButton != null && ui.moveHereButton.gameObject.activeSelf)
                return true;

            if (ui.scoutButton != null && ui.scoutButton.gameObject.activeSelf)
                return true;
        }

        var trackingUIs = canvas.GetComponentsInChildren<TileTrackingMarkerUI>(true);
        for (int i = 0; i < trackingUIs.Length; i++)
        {
            var ui = trackingUIs[i];
            if (ui == null) continue;

            if (ui.root != null)
            {
                if (ui.root.activeSelf)
                    return true;
            }
            else if (ui.gameObject.activeSelf)
            {
                return true;
            }
        }

        return false;
    }
}