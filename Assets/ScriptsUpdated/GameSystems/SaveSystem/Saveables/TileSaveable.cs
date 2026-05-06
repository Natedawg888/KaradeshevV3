using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(TileScript))]
public class TileSaveable : Saveable
{
    public static readonly HashSet<TileSaveable> Live = new HashSet<TileSaveable>();
    public static readonly HashSet<TileSaveable> Dirty = new HashSet<TileSaveable>();

    public bool IsDirty { get; private set; } = true;

    protected override void Awake()
    {
        base.Awake();
        Live.Add(this);
        MarkDirty();
    }

    protected virtual void OnEnable()
    {
        MarkDirty();
    }

    protected virtual void OnDestroy()
    {
        Live.Remove(this);
        Dirty.Remove(this);
    }

    public void MarkDirty()
    {
        IsDirty = true;
        Dirty.Add(this);
        SaveSystem.MarkSectionDirty(SaveSectionKeys.WorldObjects);
    }

    public void ClearDirty()
    {
        IsDirty = false;
        Dirty.Remove(this);
    }
}