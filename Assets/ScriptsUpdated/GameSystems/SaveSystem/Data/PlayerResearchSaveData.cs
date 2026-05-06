using System;
using System.Collections.Generic;

[Serializable]
public class ActiveResearchSaveData
{
    public string techID;
    public int totalTurns;
    public int turnsLeft;
    public string stationSaveableID;
    public string reservationId;
    public float baseFail;
}

[Serializable]
public class PlayerResearchSaveData
{
    public List<string> researchedTechIDs = new List<string>();
    public List<ActiveResearchSaveData> activeResearches = new List<ActiveResearchSaveData>();
}