using System;
using System.Collections.Generic;

[Serializable]
public class LavaOverlaySaveData
{
    public int version = 1;

    public List<LavaCellSaveData> lavaCells = new List<LavaCellSaveData>();
}

[Serializable]
public class LavaCellSaveData
{
    public int x;
    public int y;

    public int sourceX;
    public int sourceY;

    public int distanceFromSource;

    public float heat01;

    public int coolingDelayTurnsRemaining;
    public int coolingTurnsRemaining;
    public int coolingTurnsTotal;
}