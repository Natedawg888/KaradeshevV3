using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach to the notification button GameObject on your main UI Canvas.
/// Swaps the button image between normal and unread sprites automatically.
/// Call Toggle() from the button's OnClick event to open/close the panel.
/// </summary>
public class NotificationButtonUI : MonoBehaviour
{
    [Header("Button Reference")]
    [SerializeField] private Image buttonImage;

    [Header("Sprites")]
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite unreadSprite;

    [Header("Panel")]
    [Tooltip("The NotificationPanelUI that this button controls.")]
    [SerializeField] private NotificationPanelUI notificationPanel;

    private void OnEnable()
    {
        if (NotificationManager.Instance != null)
            NotificationManager.Instance.OnNotificationsChanged += RefreshSprite;

        RefreshSprite();
    }

    private void OnDisable()
    {
        if (NotificationManager.Instance != null)
            NotificationManager.Instance.OnNotificationsChanged -= RefreshSprite;
    }

    private void Start()
    {
        if (NotificationManager.Instance != null)
        {
            NotificationManager.Instance.OnNotificationsChanged -= RefreshSprite;
            NotificationManager.Instance.OnNotificationsChanged += RefreshSprite;
        }

        RefreshSprite();
    }

    /// <summary>
    /// Wire this to the button's OnClick event.
    /// Opens the panel (or closes it if already open) and marks all as read.
    /// </summary>
    public void Toggle()
    {
        notificationPanel?.Toggle();
    }

    /// <summary>
    /// Kept for backward compatibility — marks all notifications read without toggling.
    /// </summary>
    public void MarkRead()
    {
        NotificationManager.Instance?.MarkAllAsRead();
    }

    private void RefreshSprite()
    {
        if (buttonImage == null) return;

        bool hasUnread = NotificationManager.Instance != null &&
                         NotificationManager.Instance.HasUnreadNotifications();

        buttonImage.sprite = hasUnread ? unreadSprite : normalSprite;
    }
}
