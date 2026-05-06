using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central notification manager. Place on a GameObject in your manager setup scene.
/// Call AddNotification() from any system that completes a player task.
/// </summary>
public class NotificationManager : MonoBehaviour
{
    public static NotificationManager Instance { get; private set; }

    private readonly List<NotificationData> _notifications = new();

    public event Action<NotificationData> OnNotificationAdded;
    public event Action OnNotificationsChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // ------------------------------------------------------------------
    // Public API
    // ------------------------------------------------------------------

    public void AddNotification(NotificationType type, string title, string message)
    {
        int turn = TurnSystem.Instance != null ? TurnSystem.Instance.CurrentTurn : 0;

        var notification = new NotificationData(type, title, message, turn);
        _notifications.Add(notification);

        Debug.Log($"[NotificationManager] Added: [{type}] {title} — {message}");

        OnNotificationAdded?.Invoke(notification);
        OnNotificationsChanged?.Invoke();
    }

    public void MarkAllAsRead()
    {
        bool anyChanged = false;

        for (int i = 0; i < _notifications.Count; i++)
        {
            if (!_notifications[i].isRead)
            {
                _notifications[i].isRead = true;
                anyChanged = true;
            }
        }

        if (anyChanged)
        {
            Debug.Log("[NotificationManager] All notifications marked as read.");
            OnNotificationsChanged?.Invoke();
        }
    }

    public bool HasUnreadNotifications()
    {
        for (int i = 0; i < _notifications.Count; i++)
        {
            if (!_notifications[i].isRead)
                return true;
        }

        return false;
    }

    public IReadOnlyList<NotificationData> GetAllNotifications() => _notifications;

    public List<NotificationData> GetUnreadNotifications()
    {
        var unread = new List<NotificationData>();

        for (int i = 0; i < _notifications.Count; i++)
        {
            if (!_notifications[i].isRead)
                unread.Add(_notifications[i]);
        }

        return unread;
    }

    public void ClearAll()
    {
        _notifications.Clear();
        OnNotificationsChanged?.Invoke();
    }
}
