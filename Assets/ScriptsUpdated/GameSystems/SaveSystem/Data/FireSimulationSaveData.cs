using System;
using System.Collections.Generic;

[Serializable]
public class FireSimulationSaveData
{
    public int version = 1;

    public List<EnvironmentFireSaveData> burningEnvironments = new List<EnvironmentFireSaveData>();
    public List<BuildingFireSaveData> burningBuildings = new List<BuildingFireSaveData>();
}

[Serializable]
public class EnvironmentFireSaveData
{
    public int x;
    public int y;

    public int burnTurnsRemaining;
    public int baseBurnTurns;

    public float currentDryness01;
}

[Serializable]
public class BuildingFireSaveData
{
    public int x;
    public int y;

    public int burnTurnsRemaining;
    public int baseBurnTurns;
}