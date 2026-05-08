using System;
using System.Collections.Generic;
using UnityEngine;

public static class PlayerShelterSaveLoad
{
    public static PlayerShelterSaveData SaveState()
    {
        PlayerShelterSaveData data = new PlayerShelterSaveData();

        ShelterControl[] shelters = UnityEngine.Object.FindObjectsOfType<ShelterControl>(true);
        for (int i = 0; i < shelters.Length; i++)
        {
            ShelterControl shelter = shelters[i];
            if (shelter == null)
                continue;

            if (!TryGetOwningBuildingSaveableId(shelter, out string buildingSaveableId))
                continue;

            data.shelters.Add(shelter.CaptureRuntimeSaveData(buildingSaveableId));
        }

        return data;
    }

    public static void LoadState(PlayerShelterSaveData data)
    {
        Dictionary<string, ShelterControl> bySaveableId = BuildShelterLookup();

        if (data == null || data.shelters == null)
            return;

        for (int i = 0; i < data.shelters.Count; i++)
        {
            ShelterRuntimeSaveData saved = data.shelters[i];
            if (saved == null || string.IsNullOrWhiteSpace(saved.buildingSaveableID))
                continue;

            if (!bySaveableId.TryGetValue(saved.buildingSaveableID, out ShelterControl shelter) || shelter == null)
            {
                //Debug.LogWarning($"[Shelter Save] Could not resolve shelter '{saved.buildingSaveableID}' while loading.");
                continue;
            }

            shelter.ApplyRuntimeSaveData(saved);
        }
    }

    private static Dictionary<string, ShelterControl> BuildShelterLookup()
    {
        Dictionary<string, ShelterControl> map = new Dictionary<string, ShelterControl>(StringComparer.Ordinal);

        ShelterControl[] shelters = UnityEngine.Object.FindObjectsOfType<ShelterControl>(true);
        for (int i = 0; i < shelters.Length; i++)
        {
            ShelterControl shelter = shelters[i];
            if (shelter == null)
                continue;

            if (!TryGetOwningBuildingSaveableId(shelter, out string buildingSaveableId))
                continue;

            if (!map.ContainsKey(buildingSaveableId))
                map.Add(buildingSaveableId, shelter);
        }

        return map;
    }

    private static bool TryGetOwningBuildingSaveableId(ShelterControl shelter, out string buildingSaveableId)
    {
        buildingSaveableId = null;

        if (shelter == null)
            return false;

        BuildingSaveable buildingSaveable = shelter.GetComponentInParent<BuildingSaveable>(true);
        if (buildingSaveable != null && !string.IsNullOrWhiteSpace(buildingSaveable.uniqueID))
        {
            buildingSaveableId = buildingSaveable.uniqueID;
            return true;
        }

        Saveable saveable = shelter.GetComponent<Saveable>();
        if (saveable == null)
            saveable = shelter.GetComponentInParent<Saveable>(true);

        if (saveable != null && !string.IsNullOrWhiteSpace(saveable.uniqueID))
        {
            buildingSaveableId = saveable.uniqueID;
            return true;
        }

        return false;
    }
}
