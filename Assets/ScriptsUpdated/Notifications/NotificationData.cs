using System;

[Serializable]
public class NotificationData
{
    public NotificationType type;
    public string title;
    public string message;
    public bool isRead;
    public int turnNumber;

    public NotificationData(NotificationType type, string title, string message, int turnNumber = 0)
    {
        this.type       = type;
        this.title      = title;
        this.message    = message;
        this.isRead     = false;
        this.turnNumber = turnNumber;
    }
}
