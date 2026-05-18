using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the notification panel. Assign to the panel root GameObject.
/// Requires a scroll view content transform and a row prefab (NotificationRowUI).
/// </summary>
public class NotificationPanelUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panelRoot;

    [Header("Row Prefab & Container")]
    [Tooltip("Prefab with a NotificationRowUI component.")]
    [SerializeField] private NotificationRowUI rowPrefab;
    [SerializeField] private Transform rowContainer;

    [Header("Empty State")]
    [SerializeField] private GameObject emptyLabel;

    [Header("Buttons")]
    [SerializeField] private Button clearAllButton;
    [SerializeField] private Button closeButton;

    [Header("Camera")]
    [SerializeField] private CameraControl cameraControl;

    private readonly List<NotificationRowUI> _rows = new();

    private void Awake()
    {
        if (clearAllButton != null)
            clearAllButton.onClick.AddListener(OnClearAll);

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        if (panelRoot != null)
            panelRoot.SetActive(false);

        if (cameraControl == null)
            cameraControl = FindObjectOfType<CameraControl>();
    }

    private void OnEnable()
    {
        if (NotificationManager.Instance != null)
            NotificationManager.Instance.OnNotificationsChanged += Refresh;
    }

    private void OnDisable()
    {
        if (NotificationManager.Instance != null)
            NotificationManager.Instance.OnNotificationsChanged -= Refresh;
    }

    // ------------------------------------------------------------------
    // Public API
    // ------------------------------------------------------------------

    public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

    public void Open()
    {
        if (panelRoot != null)
            panelRoot.SetActive(true);

        TileInteraction.SetSelectionEnabled(false);
        cameraControl?.PushInputLock();

        Refresh();
        NotificationManager.Instance?.MarkAllAsRead();
    }

    public void Close()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);

        TileInteraction.SetSelectionEnabled(false);
        TileInteraction.GetInstance()?.EnableSelectionAfter(0.01f);
        cameraControl?.PopInputLock();
    }

    public void Toggle()
    {
        if (IsOpen) Close();
        else         Open();
    }

    // ------------------------------------------------------------------
    // Internal
    // ------------------------------------------------------------------

    private void Refresh()
    {
        foreach (var row in _rows)
            if (row != null) Destroy(row.gameObject);
        _rows.Clear();

        if (NotificationManager.Instance == null) return;

        var all = NotificationManager.Instance.GetAllNotifications();

        bool hasAny = all.Count > 0;
        if (emptyLabel != null) emptyLabel.SetActive(!hasAny);
        if (clearAllButton != null) clearAllButton.gameObject.SetActive(hasAny);

        for (int i = all.Count - 1; i >= 0; i--)
        {
            if (rowPrefab == null) break;

            var row = Instantiate(rowPrefab, rowContainer);
            row.SetPanel(this);
            row.Populate(all[i]);
            _rows.Add(row);
        }
    }

    private void OnClearAll()
    {
        NotificationManager.Instance?.ClearAll();
    }
}
