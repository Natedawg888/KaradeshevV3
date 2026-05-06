using UnityEngine;

[RequireComponent(typeof(Canvas))]
public class TileWorldCanvasVisibility : MonoBehaviour
{
    [SerializeField] private Canvas worldCanvas;
    [SerializeField] private RectTransform[] contentRoots;

    private void Awake()
    {
        if (worldCanvas == null)
            worldCanvas = GetComponent<Canvas>();

        Refresh();
    }

    private void OnEnable()
    {
        WorldCanvasMode.OnChanged += HandleModeChanged;
        Refresh();
    }

    private void OnDisable()
    {
        WorldCanvasMode.OnChanged -= HandleModeChanged;
    }

    private void HandleModeChanged(bool unitsOnly)
    {
        Refresh();
    }

    public void Refresh()
    {
        if (worldCanvas == null)
            return;

        // ✅ If not in UnitsOnly mode, unit canvases must stay hidden
        if (!WorldCanvasMode.UnitsOnly)
        {
            worldCanvas.enabled = false;
            return;
        }

        bool hasAny = false;

        if (contentRoots != null)
        {
            for (int i = 0; i < contentRoots.Length; i++)
            {
                var root = contentRoots[i];
                if (root != null && root.childCount > 0)
                {
                    hasAny = true;
                    break;
                }
            }
        }

        worldCanvas.enabled = hasAny;
    }
}