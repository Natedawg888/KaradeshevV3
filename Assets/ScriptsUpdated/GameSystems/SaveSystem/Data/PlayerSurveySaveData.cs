using System;
using System.Collections.Generic;

[Serializable]
public class ActiveSurveySaveData
{
    public string environmentID;
    public int turnsCompleted;
    public string reservationId;
}

[Serializable]
public class PlayerSurveySaveData
{
    public List<ActiveSurveySaveData> activeSurveys = new List<ActiveSurveySaveData>();
    public List<string> surveyedEnvironmentIDs = new List<string>();
}