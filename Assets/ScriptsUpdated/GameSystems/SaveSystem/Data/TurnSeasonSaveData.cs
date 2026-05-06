using System;

[Serializable]
public class TurnSystemSaveData
{
    public int currentTurn;
    public DayPhase currentPhase;
    public float phaseTimer;
}

[Serializable]
public class SeasonManagerSaveData
{
    public int activeSeasonSetID = -1;
    public int currentSeasonIndex = 0;
    public int turnsIntoCurrentSeason = 0;
}