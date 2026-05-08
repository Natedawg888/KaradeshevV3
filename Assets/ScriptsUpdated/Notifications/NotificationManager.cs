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
        AddNotificationInternal(new NotificationData(type, title, message, TurnSystem.Instance != null ? TurnSystem.CurrentTurn : 0));
    }

    public void AddNotification(NotificationType type, string title, string message, bool showDeathIcon)
    {
        var data = new NotificationData(type, title, message, TurnSystem.Instance != null ? TurnSystem.CurrentTurn : 0);
        data.showDeathIcon = showDeathIcon;
        AddNotificationInternal(data);
    }

    public void AddProductionCompletedNotification(string title, string message, List<ProductionOutputEntry> outputs, Vector3 worldPosition = default)
    {
        var data = worldPosition != Vector3.zero
            ? new NotificationData(NotificationType.ProductionCompleted, title, message, worldPosition, TurnSystem.Instance != null ? TurnSystem.CurrentTurn : 0)
            : new NotificationData(NotificationType.ProductionCompleted, title, message, TurnSystem.Instance != null ? TurnSystem.CurrentTurn : 0);
        data.producedOutputs = outputs;
        AddNotificationInternal(data);
    }

    public void AddProductionPausedNotification(NotificationType type, string title, string message, Vector3 worldPosition = default)
    {
        var data = worldPosition != Vector3.zero
            ? new NotificationData(type, title, message, worldPosition, TurnSystem.Instance != null ? TurnSystem.CurrentTurn : 0)
            : new NotificationData(type, title, message, TurnSystem.Instance != null ? TurnSystem.CurrentTurn : 0);
        AddNotificationInternal(data);
    }

    public void AddCraftingCompletedNotification(string title, string message, List<ProductionOutputEntry> outputs, Vector3 worldPosition = default)
    {
        var data = worldPosition != Vector3.zero
            ? new NotificationData(NotificationType.CraftingCompleted, title, message, worldPosition, TurnSystem.Instance != null ? TurnSystem.CurrentTurn : 0)
            : new NotificationData(NotificationType.CraftingCompleted, title, message, TurnSystem.Instance != null ? TurnSystem.CurrentTurn : 0);
        data.producedOutputs = outputs;
        AddNotificationInternal(data);
    }

    public void AddCraftingFailedNotification(string title, string message, Vector3 worldPosition = default)
    {
        var data = worldPosition != Vector3.zero
            ? new NotificationData(NotificationType.CraftingFailedWeather, title, message, worldPosition, TurnSystem.Instance != null ? TurnSystem.CurrentTurn : 0)
            : new NotificationData(NotificationType.CraftingFailedWeather, title, message, TurnSystem.Instance != null ? TurnSystem.CurrentTurn : 0);
        AddNotificationInternal(data);
    }

    public void AddNotification(NotificationType type, string title, string message, Vector3 worldPosition)
    {
        AddNotificationInternal(new NotificationData(type, title, message, worldPosition, TurnSystem.Instance != null ? TurnSystem.CurrentTurn : 0));
    }

    private void AddNotificationInternal(NotificationData notification)
    {
        _notifications.Add(notification);

        //Debug.Log($"[NotificationManager] Added: [{notification.type}] {notification.title} — {notification.message}");

        SaveSystem.MarkSectionDirty(SaveSectionKeys.Notifications);
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
            //Debug.Log("[NotificationManager] All notifications marked as read.");
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

    public void RemoveNotification(NotificationData notification)
    {
        if (_notifications.Remove(notification))
            OnNotificationsChanged?.Invoke();
    }

    public void ClearAll()
    {
        _notifications.Clear();
        OnNotificationsChanged?.Invoke();
    }

    public NotificationsSaveData SaveState()
    {
        var data = new NotificationsSaveData();
        for (int i = 0; i < _notifications.Count; i++)
        {
            var n = _notifications[i];
            if (n.isRead) continue;
            data.notifications.Add(new NotificationSaveEntry
            {
                type           = (int)n.type,
                title          = n.title,
                message        = n.message,
                isRead         = n.isRead,
                turnNumber     = n.turnNumber,
                hasTileTarget  = n.hasTileTarget,
                worldPositionX = n.worldPosition.x,
                worldPositionY = n.worldPosition.y,
                worldPositionZ = n.worldPosition.z,
                showDeathIcon  = n.showDeathIcon,
            });
        }
        return data;
    }

    public void LoadState(NotificationsSaveData data)
    {
        _notifications.Clear();
        if (data?.notifications == null)
        {
            OnNotificationsChanged?.Invoke();
            return;
        }
        for (int i = 0; i < data.notifications.Count; i++)
        {
            var e = data.notifications[i];
            var n = new NotificationData((NotificationType)e.type, e.title, e.message, e.turnNumber)
            {
                isRead        = e.isRead,
                hasTileTarget = e.hasTileTarget,
                worldPosition = new UnityEngine.Vector3(e.worldPositionX, e.worldPositionY, e.worldPositionZ),
                showDeathIcon = e.showDeathIcon,
            };
            _notifications.Add(n);
        }
        OnNotificationsChanged?.Invoke();
    }
}
