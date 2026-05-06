using System;
using System.Collections.Generic;

[Serializable]
public class BlockCoordSaveData
{
    public int x;
    public int y;

    public BlockCoordSaveData() { }

    public BlockCoordSaveData(int x, int y)
    {
        this.x = x;
        this.y = y;
    }
}

[Serializable]
public class EarthquakeFaultLineSaveData
{
    public int version = 1;

    public bool generatedForCurrentMap;
    public bool hasFaults;

    public int blockColumns;
    public int blockRows;

    public int directionValue;

    public List<BlockCoordSaveData> faultBlocks = new List<BlockCoordSaveData>();
    public List<BlockCoordSaveData> faultInfluenceBlocks = new List<BlockCoordSaveData>();
}

[Serializable]
public class EarthquakeSimulationSaveData
{
    public int version = 1;

    public float tectonicEnergy01;
    public float lastCalculatedChance01;

    public int lastProcessedTurn;
}