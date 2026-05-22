using System;
using System.Collections.Generic;
using UnityEngine;

public static class PlayerTradeBuildingSaveLoad
{
    private static Dictionary<string, ResourceDefinition> _resourceById;

    public static PlayerTradeBuildingsSaveData SaveState()
    {
        var data = new PlayerTradeBuildingsSaveData();

        TradeBuildingControl[] buildings = UnityEngine.Object.FindObjectsOfType<TradeBuildingControl>(true);
        for (int i = 0; i < buildings.Length; i++)
        {
            TradeBuildingControl building = buildings[i];
            if (building == null) continue;

            Saveable saveable = building.GetComponent<Saveable>();
            if (saveable == null) saveable = building.GetComponentInParent<Saveable>();
            if (saveable == null || string.IsNullOrWhiteSpace(saveable.uniqueID)) continue;

            data.buildings.Add(building.CaptureRuntimeSaveData(saveable.uniqueID));
        }

        return data;
    }

    public static void LoadState(PlayerTradeBuildingsSaveData data)
    {
        if (data?.buildings == null) return;

        TradeBuildingControl[] buildings = UnityEngine.Object.FindObjectsOfType<TradeBuildingControl>(true);
        var byId = new Dictionary<string, TradeBuildingControl>(StringComparer.Ordinal);

        for (int i = 0; i < buildings.Length; i++)
        {
            TradeBuildingControl b = buildings[i];
            if (b == null) continue;
            Saveable saveable = b.GetComponent<Saveable>();
            if (saveable == null) saveable = b.GetComponentInParent<Saveable>();
            if (saveable != null && !string.IsNullOrWhiteSpace(saveable.uniqueID) && !byId.ContainsKey(saveable.uniqueID))
                byId.Add(saveable.uniqueID, b);
        }

        for (int i = 0; i < data.buildings.Count; i++)
        {
            TradeBuildingSaveData saved = data.buildings[i];
            if (saved == null || string.IsNullOrWhiteSpace(saved.buildingInstanceId)) continue;
            if (!byId.TryGetValue(saved.buildingInstanceId, out TradeBuildingControl building) || building == null) continue;
            building.ApplyRuntimeSaveData(saved, ResolveResourceByID);
        }
    }

    private static ResourceDefinition ResolveResourceByID(string resourceID)
    {
        if (string.IsNullOrWhiteSpace(resourceID)) return null;

        if (_resourceById == null)
        {
            _resourceById = new Dictionary<string, ResourceDefinition>(StringComparer.OrdinalIgnoreCase);
            ResourceDefinition[] defs = Resources.LoadAll<ResourceDefinition>(string.Empty);
            for (int i = 0; i < defs.Length; i++)
            {
                ResourceDefinition def = defs[i];
                if (def == null || string.IsNullOrWhiteSpace(def.resourceID)) continue;
                if (!_resourceById.ContainsKey(def.resourceID))
                    _resourceById.Add(def.resourceID, def);
            }
        }

        _resourceById.TryGetValue(resourceID.Trim(), out ResourceDefinition result);
        return result;
    }
}
