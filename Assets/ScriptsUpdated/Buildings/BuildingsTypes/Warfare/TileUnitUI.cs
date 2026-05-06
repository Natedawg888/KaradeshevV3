using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Canvas))]
public class TileUnitUI : MonoBehaviour
{
    [Header("UI")]
    public Canvas worldCanvas;          // world-space canvas on the tile
    public ScrollRect scrollRect;       // scroll view
    public RectTransform contentRoot;   // scrollRect.content (markers go here)

    private int _groupCount;

    public RectTransform ContentRoot => contentRoot;

    private TileWorldCanvasVisibility _visibility;

    private void Awake()
    {
        if (worldCanvas == null)
            worldCanvas = GetComponent<Canvas>();

        _visibility = GetComponent<TileWorldCanvasVisibility>();

        if (scrollRect == null)
            scrollRect = GetComponentInChildren<ScrollRect>(true);

        if (scrollRect != null && contentRoot == null)
            contentRoot = scrollRect.content;

        _visibility?.Refresh();
    }

    public void RegisterGroup()
    {
        _groupCount++;
        _visibility?.Refresh();
    }

    public void UnregisterGroup()
    {
        _groupCount = Mathf.Max(0, _groupCount - 1);
        _visibility?.Refresh();
    }
}
