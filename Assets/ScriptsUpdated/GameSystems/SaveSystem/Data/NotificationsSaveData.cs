using System;
using System.Collections.Generic;

[Serializable]
public class NotificationsSaveData
{
    public List<NotificationSaveEntry> notifications = new List<NotificationSaveEntry>();
}

[Serializable]
public class NotificationSaveEntry
{
    public int    type;
    public string title;
    public string message;
    public bool   isRead;
    public int    turnNumber;
    public bool   hasTileTarget;
    public float  worldPositionX;
    public float  worldPositionY;
    public float  worldPositionZ;
    public bool   showDeathIcon;
    // producedOutputs omitted — ScriptableObject references cannot be JSON-serialised
}
