using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach to the notification button GameObject on your main UI Canvas.
/// Swaps the button image between normal and unread sprites automatically.
/// Call MarkRead() (e.g. from an OnClick event) to clear unread state.
/// </summary>
public class NotificationButtonUI : MonoBehaviour
{
    [Header("Button Reference")]
    [Tooltip("The Image component on the notification button.")]
    [SerializeField] private Image buttonImage;

    [Header("Sprites")]
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite unreadSprite;

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
        // Late-bind in case NotificationManager was not yet alive in OnEnable.
        if (NotificationManager.Instance != null)
        {
            NotificationManager.Instance.OnNotificationsChanged -= RefreshSprite;
            NotificationManager.Instance.OnNotificationsChanged += RefreshSprite;
        }

        RefreshSprite();
    }

    /// <summary>
    /// Call this from the button's OnClick event or when the notification panel opens.
    /// </summary>
    public void MarkRead()
    {
        NotificationManager.Instance?.MarkAllAsRead();
    }

    private void RefreshSprite()
    {
        if (buttonImage == null)
            return;

        bool hasUnread = NotificationManager.Instance != null &&
                         NotificationManager.Instance.HasUnreadNotifications();

        buttonImage.sprite = hasUnread ? unreadSprite : normalSprite;

        Debug.Log($"[NotificationButtonUI] Sprite updated — hasUnread={hasUnread}");
    }
}
