using System;
using System.Collections.Generic;
using UnityEngine;

public enum TsunamiDirection
{
    North = 0,
    East = 1,
    South = 2,
    West = 3,

    NorthEast = 4,
    SouthEast = 5,
    SouthWest = 6,
    NorthWest = 7
}

public enum TsunamiEndReason
{
    EnergyDepleted = 0,
    MaxStepsReached = 1,
    NoValidCells = 2,
    ManuallyCleared = 3,
    MapNotReady = 4
}

[Serializable]
public class TsunamiWaveState
{
    public int tsunamiId;
    public TsunamiDirection directionKind;
    public Vector2Int direction;

    public float startEnergy;
    public float energy;
    public int stepCount;
    public int maxSteps;

    public HashSet<TileCoord> currentCells = new HashSet<TileCoord>();
    public HashSet<TileCoord> visitedCells = new HashSet<TileCoord>();
    public HashSet<TileCoord> sourceCells = new HashSet<TileCoord>();

    public float Energy01
    {
        get
        {
            if (startEnergy <= 0.0001f)
                return 0f;

            return Mathf.Clamp01(energy / startEnergy);
        }
    }

    public TsunamiWaveState(
        int tsunamiId,
        TsunamiDirection directionKind,
        Vector2Int direction,
        float energy,
        int maxSteps)
    {
        this.tsunamiId = tsunamiId;
        this.directionKind = directionKind;
        this.direction = direction;

        this.startEnergy = Mathf.Max(0.01f, energy);
        this.energy = energy;

        this.maxSteps = maxSteps;
    }
}

public class TsunamiStartedEventData
{
    public int tsunamiId;
    public TsunamiDirection directionKind;
    public Vector2Int direction;
    public float startEnergy;
    public List<TileCoord> sourceCells;
    public bool forced;
}

public class TsunamiCellsChangedEventData
{
    public int tsunamiId;

    public float startEnergy;
    public float energyRemaining;
    public float energy01;

    public List<TileCoord> addedCells;
    public List<TileCoord> removedCells;
    public List<TileCoord> activeCells;
}

public class TsunamiAdvancedEventData
{
    public int tsunamiId;
    public TsunamiDirection directionKind;
    public Vector2Int direction;
    public int stepCount;

    public float startEnergy;
    public float energyRemaining;
    public float energy01;

    public List<TileCoord> activeCells;
}

public class TsunamiEndedEventData
{
    public int tsunamiId;
    public TsunamiEndReason reason;
    public int finalStepCount;
    public float finalEnergy;
    public List<TileCoord> finalCells;
}

public enum TsunamiGridEdge
{
    West = 0,
    East = 1,
    South = 2,
    North = 3
}