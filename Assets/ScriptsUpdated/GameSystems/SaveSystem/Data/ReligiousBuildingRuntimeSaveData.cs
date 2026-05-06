using System;
using System.Collections.Generic;

[Serializable]
public class ReligiousBuildingRitualCooldownSaveData
{
    public string ritualID;
    public int readyOnTurn;
}

[Serializable]
public class ReligiousBuildingActiveRitualSaveData
{
    public string ritualID;
    public string targetSpiritID;

    public int totalTurns;      // <-- add this
    public int turnsRemaining;
    public int startedOnTurn;
    public string workerReservationId;
}

[Serializable]
public class ReligiousBuildingRuntimeSaveData
{
    public string buildingSaveableID;
    public List<string> affiliatedSpiritIDs = new List<string>();
    public List<string> completedNonRepeatableRitualIDs = new List<string>();
    public List<ReligiousBuildingRitualCooldownSaveData> ritualCooldowns = new List<ReligiousBuildingRitualCooldownSaveData>();
    public ReligiousBuildingActiveRitualSaveData activeRitual;
}
[Serializable]
public class PlayerReligionBuildingsSaveData
{
    public List<ReligiousBuildingRuntimeSaveData> buildings = new List<ReligiousBuildingRuntimeSaveData>();
}