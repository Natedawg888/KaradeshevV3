using System;
using UnityEngine;

public enum TrackEntityType
{
    Animal,
    Unit
}

[Serializable]
public class TrackingResultEntry
{
    public string entityName;
    public Sprite icon;
    public int count;

    public TrackEntityType entityType;

    // Where it was seen (for the "Track" button)
    public Vector2Int sourceGrid;

    [NonSerialized] public TileControl sourceTile; // runtime-only (fast path)
}
