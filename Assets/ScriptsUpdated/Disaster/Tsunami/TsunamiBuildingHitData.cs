using System.Collections.Generic;
using UnityEngine;

public class TsunamiBuildingHitData
{
    public int tsunamiId;
    public int stepCount;

    public TsunamiDirection directionKind;
    public Vector2Int direction;

    public float startEnergy;
    public float energyRemaining;
    public float energy01;

    public int baseDamage;
    public int finalDamage;

    public int buildingCellCount;
    public int hitCellCount;

    public List<TileCoord> hitCells = new List<TileCoord>();
}