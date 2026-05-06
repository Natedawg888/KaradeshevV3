using System;
using System.Collections.Generic;

[Serializable]
public class ProductionExtractionTileSetSaveData
{
    public string productionID;
    public List<string> environmentIDs = new List<string>();
}

[Serializable]
public class ProductionBuildingRuntimeSaveData
{
    public string buildingSaveableID;

    public string activePlanID;
    public int activeRunningCostSetIndex;
    public int activeOutputSetIndex;

    public int turnsLeftInCycle;

    public string pauseReason;
    public bool waitingToFinalizeCompletedCycle;

    public bool isCoolingDown;
    public int cooldownTurnsLeft;
    public int completedCyclesSinceCooldown;

    public string populationReservationId;
    public int populationReservedAmount;

    public List<ProductionExtractionTileSetSaveData> extractionTileSets = new List<ProductionExtractionTileSetSaveData>();
}

[Serializable]
public class PlayerProductionSaveData
{
    public List<ProductionBuildingRuntimeSaveData> buildings = new List<ProductionBuildingRuntimeSaveData>();
}