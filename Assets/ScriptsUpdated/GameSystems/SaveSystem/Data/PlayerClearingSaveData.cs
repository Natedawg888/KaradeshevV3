using System;
using System.Collections.Generic;

[Serializable]
public class ManualClearRewardSaveData
{
    public string resourceID;
    public int amount;
}

[Serializable]
public class ActiveManualClearSaveData
{
    public string buildingSaveableID;
    public int totalTurns;
    public int turnsLeft;
    public string reservationId;
    public List<ManualClearRewardSaveData> rewards = new List<ManualClearRewardSaveData>();
}

[Serializable]
public class PlayerClearingSaveData
{
    public List<ActiveManualClearSaveData> activeClears = new List<ActiveManualClearSaveData>();
}