using System;
using System.Collections.Generic;
using UnityEngine;

public static class PlayerStorageSaveLoad
{
    private static Dictionary<string, ResourceDefinition> _resourceById;

    public static PlayerStorageSaveData SaveState()
    {
        PlayerStorageSaveData data = new PlayerStorageSaveData();

        StorageBuildingControl[] storages = UnityEngine.Object.FindObjectsOfType<StorageBuildingControl>(true);
        for (int i = 0; i < storages.Length; i++)
        {
            StorageBuildingControl storage = storages[i];
            if (storage == null)
                continue;

            Saveable saveable = storage.GetComponent<Saveable>();
            if (saveable == null)
                saveable = storage.GetComponentInParent<Saveable>();

            if (saveable == null || string.IsNullOrWhiteSpace(saveable.uniqueID))
                continue;

            data.buildings.Add(storage.CaptureRuntimeSaveData(saveable.uniqueID));
        }

        return data;
    }

    public static void LoadState(PlayerStorageSaveData data)
    {
        StorageBuildingControl[] storages = UnityEngine.Object.FindObjectsOfType<StorageBuildingControl>(true);
        Dictionary<string, StorageBuildingControl> bySaveableId = new Dictionary<string, StorageBuildingControl>(StringComparer.Ordinal);

        for (int i = 0; i < storages.Length; i++)
        {
            StorageBuildingControl storage = storages[i];
            if (storage == null)
                continue;

            storage.ClearStorageForLoad();

            Saveable saveable = storage.GetComponent<Saveable>();
            if (saveable == null)
                saveable = storage.GetComponentInParent<Saveable>();

            if (saveable != null && !string.IsNullOrWhiteSpace(saveable.uniqueID) && !bySaveableId.ContainsKey(saveable.uniqueID))
                bySaveableId.Add(saveable.uniqueID, storage);
        }

        if (data == null || data.buildings == null)
            return;

        for (int i = 0; i < data.buildings.Count; i++)
        {
            StorageBuildingSaveData saved = data.buildings[i];
            if (saved == null || string.IsNullOrWhiteSpace(saved.buildingSaveableID))
                continue;

            if (!bySaveableId.TryGetValue(saved.buildingSaveableID, out StorageBuildingControl storage) || storage == null)
            {
                //Debug.LogWarning($"[Storage Save] Could not resolve storage building '{saved.buildingSaveableID}' while loading.");
                continue;
            }

            storage.ApplyRuntimeSaveData(saved, ResolveResourceByID);
        }
    }

    private static ResourceDefinition ResolveResourceByID(string resourceID)
    {
        if (string.IsNullOrWhiteSpace(resourceID))
            return null;

        if (_resourceById == null)
        {
            _resourceById = new Dictionary<string, ResourceDefinition>(StringComparer.Ordinal);
            ResourceDefinition[] defs = Resources.LoadAll<ResourceDefinition>(string.Empty);

            for (int i = 0; i < defs.Length; i++)
            {
                ResourceDefinition def = defs[i];
                if (def == null || string.IsNullOrWhiteSpace(def.resourceID))
                    continue;

                string id = def.resourceID.Trim();
                if (!_resourceById.ContainsKey(id))
                    _resourceById.Add(id, def);
            }
        }

        _resourceById.TryGetValue(resourceID.Trim(), out ResourceDefinition result);
        return result;
    }
}
