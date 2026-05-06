using System;
using System.Collections.Generic;
using UnityEngine;

public partial class PlayerInventoryManager : MonoBehaviour
{
    public event Action OnCapacityChanged;

    private float _baseMaterialsSpace;
    private float _baseFoodSpace;
    private float _baseWaterSpace;

    private struct CapacityBonus
    {
        public float materials;
        public float food;
        public float water;
    }

    // Key = source instance id (StorageBuildingControl.GetInstanceID())
    private readonly Dictionary<int, CapacityBonus> _capacityBonuses = new();

    private void CacheBaseCapacities()
    {
        _baseMaterialsSpace = maxMaterialsSpace;
        _baseFoodSpace = maxFoodSpace;
        _baseWaterSpace = maxWaterSpace;
    }

    private void RebuildCapacities()
    {
        float m = _baseMaterialsSpace;
        float f = _baseFoodSpace;
        float w = _baseWaterSpace;

        foreach (var kv in _capacityBonuses)
        {
            m += kv.Value.materials;
            f += kv.Value.food;
            w += kv.Value.water;
        }

        maxMaterialsSpace = Mathf.Max(0f, m);
        maxFoodSpace = Mathf.Max(0f, f);
        maxWaterSpace = Mathf.Max(0f, w);

        OnCapacityChanged?.Invoke();
        inventoryPanel?.Refresh();
    }

    /// <summary>
    /// Sets (overwrites) the bonus for a given source. Safe to call multiple times.
    /// </summary>
    public void SetCapacityBonus(int sourceId, float materialsDelta, float foodDelta, float waterDelta)
    {
        if (sourceId == 0) return;

        _capacityBonuses[sourceId] = new CapacityBonus
        {
            materials = Mathf.Max(0f, materialsDelta),
            food = Mathf.Max(0f, foodDelta),
            water = Mathf.Max(0f, waterDelta),
        };

        RebuildCapacities();
    }

    public void RemoveCapacityBonus(int sourceId)
    {
        if (sourceId == 0) return;
        if (_capacityBonuses.Remove(sourceId))
            RebuildCapacities();
    }

    /// Call this if something external changed stacks/capacity and you want the panel updated.
    public void ForceRefreshUI() => inventoryPanel?.Refresh();
}