using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Represents a single row in the notification panel.
/// The delete button (left stripe) removes this notification.
/// The Go To button navigates the camera to the tile (shown for tile-linked types).
/// </summary>
public class NotificationRowUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private TextMeshProUGUI turnText;
    [SerializeField] private Button deleteButton;
    [SerializeField] private Image typeIcon;
    [SerializeField] private NotificationIconSet iconSet;
    [SerializeField] private Button goToButton;

    private static readonly Color UnreadColor = new Color(0.2f, 0.8f, 1f, 1f);
    private static readonly Color ReadColor   = new Color(0.35f, 0.35f, 0.35f, 1f);

    private NotificationData _data;
    private NotificationPanelUI _panel;

    public void SetPanel(NotificationPanelUI panel) => _panel = panel;

    public void Populate(NotificationData data)
    {
        _data = data;

        if (titleText   != null) titleText.text  = data.title;
        if (messageText != null) messageText.text = data.message;
        if (turnText    != null) turnText.text    = $"{data.turnNumber}";

        if (deleteButton != null)
        {
            var img = deleteButton.targetGraphic as Image;
            if (img != null) img.color = data.isRead ? ReadColor : UnreadColor;

            deleteButton.onClick.RemoveAllListeners();
            deleteButton.onClick.AddListener(OnDeleteClicked);
        }

        if (typeIcon != null)
        {
            Sprite icon = iconSet != null ? iconSet.GetIcon(data.type) : null;
            typeIcon.sprite = icon;
            typeIcon.gameObject.SetActive(icon != null);
        }

        if (goToButton != null)
        {
            bool show = data.hasTileTarget;
            goToButton.gameObject.SetActive(show);
            if (show)
            {
                goToButton.onClick.RemoveAllListeners();
                goToButton.onClick.AddListener(OnGoToClicked);
            }
        }
    }

    private void OnDeleteClicked()
    {
        NotificationManager.Instance?.RemoveNotification(_data);
    }

    private void OnGoToClicked()
    {
        var cam = FindObjectOfType<CameraControl>();
        if (cam == null || _data == null) return;
        cam.FocusOnPoint(_data.worldPosition, Vector3.forward, 10f);
        _panel?.Close();
    }
}
