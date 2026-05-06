using System;
using UnityEngine;

public enum FloodSourceType
{
    None = 0,
    Rain = 1,
    River = 2,
    Lake = 3,
    Ocean = 4,
    Tsunami = 5,
    Mixed = 6
}

public enum FloodCellChangeReason
{
    Unknown = 0,
    Started = 1,
    Expanded = 2,
    DepthChanged = 3,
    Drained = 4,
    Cleared = 5,
    RainInput = 6,
    TsunamiInput = 7,
    DebugInput = 8
}

public enum FloodOverlayVisualKind
{
    None = 0,
    Fill = 1,
    Straight = 2,
    InnerCorner = 3,
    OuterCorner = 4
}

public enum FloodWaterMaterialKind
{
    FreshWater = 0,
    OceanWater = 1,
    Mixed = 2
}

[Serializable]
public class FloodCellState
{
    public TileCoord coord;

    [Range(0f, 1f)]
    public float floodDepth01;

    [Tooltip("Raw water amount. In this first version, 0-1 maps directly to floodDepth01.")]
    public float waterAmount;

    public FloodSourceType sourceType;

    public int ageTurns;
    public bool sourceFed;
    public int lastUpdatedTurn;

    public FloodCellState(TileCoord coord, float waterAmount, FloodSourceType sourceType, bool sourceFed, int turn)
    {
        this.coord = coord;
        this.waterAmount = Mathf.Max(0f, waterAmount);
        this.floodDepth01 = Mathf.Clamp01(this.waterAmount);
        this.sourceType = sourceType;
        this.sourceFed = sourceFed;
        this.ageTurns = 0;
        this.lastUpdatedTurn = turn;
    }

    public void AddWater(float amount, FloodSourceType inputSource, int turn)
    {
        if (amount <= 0f)
            return;

        waterAmount = Mathf.Clamp01(waterAmount + amount);
        floodDepth01 = Mathf.Clamp01(waterAmount);

        if (sourceType == FloodSourceType.None)
        {
            sourceType = inputSource;
        }
        else if (inputSource != FloodSourceType.None && inputSource != sourceType)
        {
            sourceType = FloodSourceType.Mixed;
        }

        lastUpdatedTurn = turn;
    }

    public void RemoveWater(float amount, int turn)
    {
        if (amount <= 0f)
            return;

        waterAmount = Mathf.Max(0f, waterAmount - amount);
        floodDepth01 = Mathf.Clamp01(waterAmount);
        lastUpdatedTurn = turn;
    }
}

public readonly struct FloodCellChangedEvent
{
    public readonly TileCoord coord;
    public readonly FloodCellState state;
    public readonly FloodCellChangeReason reason;

    public FloodCellChangedEvent(TileCoord coord, FloodCellState state, FloodCellChangeReason reason)
    {
        this.coord = coord;
        this.state = state;
        this.reason = reason;
    }
}