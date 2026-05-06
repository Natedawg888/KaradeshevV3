using System;

[Serializable]
public class BuildingRuntimeSaveData
{
    public BuildingControlRuntimeSaveData controlData;
    public BuildingHealthRuntimeSaveData healthData;
    public BuildingStatusRuntimeSaveData statusData;
}

[Serializable]
public class BuildingControlRuntimeSaveData
{
    public string buildingID;
    public string buildingName;
    public BuildingType activeType;
    public string uniqueInstanceID;
}

[Serializable]
public class BuildingHealthRuntimeSaveData
{
    public int maxHealth;
    public int currentHealth;
    public int degenerationAmount;
    public int degenerationIntervalTurns;
    public float damagedThreshold;
    public bool useManagerDefaults;
    public string buildingIDOverride;
    public int turnsSinceDegenerate;
    public int degenerationPauseCounter;
}

[Serializable]
public class BuildingStatusRuntimeSaveData
{
    public BuildingState currentState;
    public int autoClearAfterTurns;
    public int destroyedTurnsElapsed;
}