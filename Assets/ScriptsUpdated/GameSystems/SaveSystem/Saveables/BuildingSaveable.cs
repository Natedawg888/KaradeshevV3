using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BuildingControl))]
public class BuildingSaveable : Saveable
{
    public static readonly HashSet<BuildingSaveable> Live = new HashSet<BuildingSaveable>();
    public static readonly HashSet<BuildingSaveable> Dirty = new HashSet<BuildingSaveable>();

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

    public override SaveData SaveState()
    {
        SaveData data = base.SaveState();

        BuildingRuntimeSaveData runtimeData = new BuildingRuntimeSaveData();

        BuildingControl control = GetComponent<BuildingControl>();
        if (control != null)
            runtimeData.controlData = control.CaptureRuntimeSaveData();

        BuildingHealth health = GetComponent<BuildingHealth>();
        if (health != null)
            runtimeData.healthData = health.CaptureRuntimeSaveData();

        BuildingStatus status = GetComponent<BuildingStatus>();
        if (status != null)
            runtimeData.statusData = status.CaptureRuntimeSaveData();

        data.jsonData = JsonUtility.ToJson(runtimeData);
        return data;
    }

    public override void LoadState(SaveData data)
    {
        base.LoadState(data);

        if (data == null || string.IsNullOrWhiteSpace(data.jsonData))
            return;

        BuildingRuntimeSaveData runtimeData = JsonUtility.FromJson<BuildingRuntimeSaveData>(data.jsonData);
        if (runtimeData == null)
            return;

        BuildingControl control = GetComponent<BuildingControl>();
        if (control != null && runtimeData.controlData != null)
            control.ApplyRuntimeSaveData(runtimeData.controlData);

        BuildingHealth health = GetComponent<BuildingHealth>();
        if (health != null && runtimeData.healthData != null)
            health.ApplyRuntimeSaveData(runtimeData.healthData);

        BuildingStatus status = GetComponent<BuildingStatus>();
        if (status != null && runtimeData.statusData != null)
            status.ApplyRuntimeSaveData(runtimeData.statusData);

        ClearDirty();
    }
}