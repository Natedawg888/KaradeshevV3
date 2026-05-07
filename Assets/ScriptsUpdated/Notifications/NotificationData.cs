using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ProductionOutputEntry
{
    public ResourceDefinition resource;
    public int amount;
}

[Serializable]
public class NotificationData
{
    public NotificationType type;
    public string title;
    public string message;
    public bool isRead;
    public int turnNumber;
    public bool hasTileTarget;
    public Vector3 worldPosition;
    public bool showDeathIcon;
    public List<ProductionOutputEntry> producedOutputs;

    public NotificationData(NotificationType type, string title, string message, int turnNumber = 0)
    {
        this.type        = type;
        this.title       = title;
        this.message     = message;
        this.isRead      = false;
        this.turnNumber  = turnNumber;
        this.hasTileTarget = false;
        this.worldPosition = Vector3.zero;
    }

    public NotificationData(NotificationType type, string title, string message, Vector3 worldPosition, int turnNumber = 0)
    {
        this.type          = type;
        this.title         = title;
        this.message       = message;
        this.isRead        = false;
        this.turnNumber    = turnNumber;
        this.hasTileTarget = true;
        this.worldPosition = worldPosition;
    }
}
