using System;
using System.Collections.Generic;

[Serializable]
public class FloodSimulationSaveData
{
    public int version = 1;

    public int currentTurn;

    public List<FloodCellSaveData> floodCells = new List<FloodCellSaveData>();
    public List<RainfallAccumulatorSaveData> rainfallAccumulators = new List<RainfallAccumulatorSaveData>();
}

[Serializable]
public class FloodCellSaveData
{
    public int x;
    public int y;

    public float waterAmount;
    public float floodDepth01;

    public int sourceTypeValue;
    public bool sourceFed;

    public int ageTurns;
    public int lastUpdatedTurn;
}

[Serializable]
public class RainfallAccumulatorSaveData
{
    public int x;
    public int y;
    public float amount;
}