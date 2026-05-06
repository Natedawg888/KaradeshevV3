using System;
using UnityEngine;

public enum RepairOption
{
    TenPercent,     // 10% health, 10% of build cost
    FiftyPercent,   // 50% health, 50% of build cost
    Full            // to 100% health, 90% of build cost
}

[Serializable]
public struct CalculatedCost
{
    public ResourceDefinition resource;
    public int amount; // after tier multiplier & rounding
}
