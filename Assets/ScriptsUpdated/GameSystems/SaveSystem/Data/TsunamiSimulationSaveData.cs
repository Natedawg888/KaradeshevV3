using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class TsunamiSimulationSaveData
{
    public int version = 1;

    public int nextTsunamiId = 1;
    public int lastRollTurn = int.MinValue;

    public List<TsunamiWaveSaveData> activeWaves = new List<TsunamiWaveSaveData>();
}

[Serializable]
public class TsunamiWaveSaveData
{
    public int tsunamiId;

    // Enum value for TsunamiDirection.
    public int directionKindValue;

    // Stored too, just in case enum changes later.
    public int directionX;
    public int directionY;

    public float startEnergy;
    public float energyRemaining;

    public int maxSteps;
    public int stepCount;

    public List<TsunamiCellSaveData> sourceCells = new List<TsunamiCellSaveData>();
    public List<TsunamiCellSaveData> currentCells = new List<TsunamiCellSaveData>();
    public List<TsunamiCellSaveData> visitedCells = new List<TsunamiCellSaveData>();
}

[Serializable]
public class TsunamiCellSaveData
{
    public int x;
    public int y;

    public TsunamiCellSaveData() { }

    public TsunamiCellSaveData(int x, int y)
    {
        this.x = x;
        this.y = y;
    }
}

// Runtime visual rebuild helper.
// This is not written directly as a save file section unless you choose to.
// It lets the overlay rebuild from restored simulation state without firing gameplay events.
public class TsunamiVisualSnapshot
{
    public int tsunamiId;
    public Vector2Int direction;
    public float energy01;
    public List<TileCoord> activeCells = new List<TileCoord>();
}