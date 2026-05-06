using System;
using System.Collections.Generic;

[Serializable]
public class ActiveGatheringSaveData
{
    public string environmentID;
    public int turnsCompleted;
    public float effectiveFailureChance;
    public int effectiveTurnsRequired;
    public int originalTurnsRequired;
    public int requiredPopulation;
    public string reservationId;
    public int reservedPopulation;
}

[Serializable]
public class PlayerGatheringSaveData
{
    public List<ActiveGatheringSaveData> activeGatherings = new List<ActiveGatheringSaveData>();
}