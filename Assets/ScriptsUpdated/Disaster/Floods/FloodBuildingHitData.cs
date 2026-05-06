using System.Collections.Generic;
using UnityEngine;

public class FloodBuildingHitData
{
    public int turnIndex;

    public float averageDepth01;
    public float maxDepth01;

    public int baseDamage;
    public int finalDamage;

    public int buildingCellCount;
    public int hitCellCount;

    public readonly List<TileCoord> hitCells = new List<TileCoord>();

    public bool IsValidHit => hitCellCount > 0 && finalDamage > 0;
}