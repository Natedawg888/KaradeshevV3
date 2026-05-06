using System;
using System.Collections.Generic;

[Serializable]
public class ActiveDiscoverySaveData
{
    public string environmentID;
    public int turnsCompleted;
    public float effectiveFailureChance;
    public int effectiveTurnsRequired;
    public int requiredPopulation;
    public string reservationId;
}

[Serializable]
public class PlayerDiscoverySaveData
{
    public List<ActiveDiscoverySaveData> activeDiscoveries = new List<ActiveDiscoverySaveData>();
}