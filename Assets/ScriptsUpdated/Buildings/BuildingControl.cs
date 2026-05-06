using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BuildingStatus))]
[RequireComponent(typeof(BuildingHealth))]
public class BuildingControl : MonoBehaviour
{
    [Header("Identity")]
    public string buildingID;           // DB key
    public string buildingName;         // display
    public BuildingType buildingType;   // base/initial category (also used if switchableTypes empty)
    [SerializeField] private string uniqueInstanceID;
    public string UniqueInstanceID => uniqueInstanceID;

    [Header("Type Switching")]
    public List<BuildingType> switchableTypes = new();
    [SerializeField] private int currentTypeIndex = 0;

    public BuildingType ActiveType
    {
        get
        {
            if (switchableTypes == null || switchableTypes.Count == 0) return buildingType;
            currentTypeIndex = Mathf.Clamp(currentTypeIndex, 0, switchableTypes.Count - 1);
            return switchableTypes[currentTypeIndex];
        }
    }

    private BuildingStatus _status;
    private BuildingHealth _health;

    private BuildingInstance _tag;

    public event System.Action<BuildingType> OnTypeApplied;

    private void Awake()
    {
        uniqueInstanceID = Guid.NewGuid().ToString();
        _status = GetComponent<BuildingStatus>();
        _health = GetComponent<BuildingHealth>();

        // ensure a tag exists
        _tag = GetComponent<BuildingInstance>();
        if (_tag == null) _tag = gameObject.AddComponent<BuildingInstance>();
        if (string.IsNullOrEmpty(_tag.instanceId))
            _tag.instanceId = Guid.NewGuid().ToString();
    }

    private void Start()
    {
        // Resolve DB definition
        var def = BuildingManager.Instance?.GetBuildingByID(buildingID);
        if (def != null)
        {
            if (string.IsNullOrWhiteSpace(buildingName))
                buildingName = def.buildingName;

            if (switchableTypes == null || switchableTypes.Count == 0)
                buildingType = def.buildingType;

            // Make sure the tag knows its definition
            _tag.definition = def;
        }

        // Register this live instance with both managers
        BuildingManager.Instance?.RegisterBuildingControl(this);
        if (_tag != null)
            PlayerBuildingManager.Instance?.Register(_tag);

        // Refresh cached building footprint coverage now that the record exists
        if (_tag != null && WorldBuildingManager.Instance != null && WeatherGridManager.Instance != null)
        {
            if (WorldBuildingManager.Instance.TryGetById(_tag.instanceId, out WorldBuildingManager.Record record))
                WeatherGridManager.Instance.RefreshBuildingCoverage(record);
        }

        // Wire state change broadcasts
        var status = GetComponent<BuildingStatus>();
        if (status != null)
            status.OnStateChanged += BroadcastStateChangeToHandlers;

        // Ensure correct type active
        ForceType(ActiveType);
    }

    private void OnDestroy()
    {
        // Unregister in reverse
        if (_tag != null)
            PlayerBuildingManager.Instance?.Unregister(_tag); // << use Unregister here

        BuildingManager.Instance?.UnregisterBuildingControl(this);

        var status = GetComponent<BuildingStatus>();
        if (status != null)
            status.OnStateChanged -= BroadcastStateChangeToHandlers;
    }

    // —— Type switching API (unchanged) ——
    public bool CanSwitchTo(BuildingType target)
    {
        if (_status != null && _status.CurrentState == BuildingState.Destroyed) return false;
        if (switchableTypes == null || switchableTypes.Count == 0) return target == buildingType;
        return switchableTypes.Contains(target);
    }

    public bool TrySwitchType(BuildingType target)
    {
        if (!CanSwitchTo(target)) return false;
        if (ActiveType == target) return true;
        if (switchableTypes != null && switchableTypes.Count > 0)
            currentTypeIndex = switchableTypes.IndexOf(target);
        ApplyType(target);
        return true;
    }

    public BuildingType CycleNextType()
    {
        if (switchableTypes == null || switchableTypes.Count == 0)
        {
            ApplyType(buildingType);
            return buildingType;
        }
        currentTypeIndex = (currentTypeIndex + 1) % switchableTypes.Count;
        var t = switchableTypes[currentTypeIndex];
        ApplyType(t);
        return t;
    }

    public BuildingType CyclePrevType()
    {
        if (switchableTypes == null || switchableTypes.Count == 0)
        {
            ApplyType(buildingType);
            return buildingType;
        }
        currentTypeIndex = (currentTypeIndex - 1 + switchableTypes.Count) % switchableTypes.Count;
        var t = switchableTypes[currentTypeIndex];
        ApplyType(t);
        return t;
    }

    public void ForceType(BuildingType target)
    {
        if (switchableTypes != null && switchableTypes.Count > 0)
            currentTypeIndex = Mathf.Clamp(
                switchableTypes.IndexOf(target) >= 0 ? switchableTypes.IndexOf(target) : 0,
                0, switchableTypes.Count - 1);
        ApplyType(target);
    }

    private void ApplyType(BuildingType target)
    {
        var behaviours = GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IBuildingTypeHandler h)
            {
                bool enable = (h.HandledType == target);
                bool wasEnabled = behaviours[i].enabled;
                behaviours[i].enabled = enable;
                if (!wasEnabled && enable) h.OnTypeEnabled();
                if (wasEnabled && !enable) h.OnTypeDisabled();
            }
        }
        buildingType = target;

        OnTypeApplied?.Invoke(target);
    }

    private void BroadcastStateChangeToHandlers(BuildingState state)
    {
        var handlers = GetComponents<IBuildingTypeHandler>();
        for (int i = 0; i < handlers.Length; i++)
            handlers[i]?.OnBuildingStateChanged(state);
    }

    // Health helpers
    public void ApplyDamage(int amount)  => _health?.ApplyDamage(amount);
    public void RepairPercent(float p)   => _health?.RepairPercent(p);
    public void RepairAbsolute(int amt)  => _health?.RepairAbsolute(amt);

    public void ShowCanvas(bool on) { /* optional UI hook */ }

    public BuildingControlRuntimeSaveData CaptureRuntimeSaveData()
    {
        return new BuildingControlRuntimeSaveData
        {
            buildingID = buildingID,
            buildingName = buildingName,
            activeType = ActiveType,
            uniqueInstanceID = uniqueInstanceID
        };
    }

    public void ApplyRuntimeSaveData(BuildingControlRuntimeSaveData data)
    {
        if (data == null)
            return;

        buildingID = data.buildingID;
        buildingName = data.buildingName;

        if (!string.IsNullOrWhiteSpace(data.uniqueInstanceID))
            uniqueInstanceID = data.uniqueInstanceID;

        ForceType(data.activeType);

        if (_tag != null && !string.IsNullOrWhiteSpace(buildingID))
        {
            var def = BuildingManager.Instance?.GetBuildingByID(buildingID);
            if (def != null)
                _tag.definition = def;
        }
    }
}