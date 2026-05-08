using System;
using System.Collections.Generic;
using UnityEngine;

public static class PlayerReligionBuildingSaveLoad
{
    private static Dictionary<string, SpiritDefinitionSO> _spiritById;
    private static Dictionary<string, ReligionRitualDefinitionSO> _ritualById;

    public static PlayerReligionBuildingsSaveData SaveState()
    {
        PlayerReligionBuildingsSaveData data = new PlayerReligionBuildingsSaveData();

        ReligiousBuildingControl[] buildings = UnityEngine.Object.FindObjectsOfType<ReligiousBuildingControl>(true);
        for (int i = 0; i < buildings.Length; i++)
        {
            ReligiousBuildingControl building = buildings[i];
            if (building == null)
                continue;

            Saveable saveable = building.GetComponent<Saveable>();
            if (saveable == null)
                saveable = building.GetComponentInParent<Saveable>();

            if (saveable == null || string.IsNullOrWhiteSpace(saveable.uniqueID))
                continue;

            data.buildings.Add(building.CaptureRuntimeSaveData(saveable.uniqueID));
        }

        return data;
    }

    public static void LoadState(PlayerReligionBuildingsSaveData data)
    {
        ReligiousBuildingControl[] buildings = UnityEngine.Object.FindObjectsOfType<ReligiousBuildingControl>(true);
        Dictionary<string, ReligiousBuildingControl> bySaveableId = new Dictionary<string, ReligiousBuildingControl>(StringComparer.Ordinal);

        for (int i = 0; i < buildings.Length; i++)
        {
            ReligiousBuildingControl building = buildings[i];
            if (building == null)
                continue;

            Saveable saveable = building.GetComponent<Saveable>();
            if (saveable == null)
                saveable = building.GetComponentInParent<Saveable>();

            if (saveable != null && !string.IsNullOrWhiteSpace(saveable.uniqueID) && !bySaveableId.ContainsKey(saveable.uniqueID))
                bySaveableId.Add(saveable.uniqueID, building);
        }

        if (data == null || data.buildings == null)
            return;

        for (int i = 0; i < data.buildings.Count; i++)
        {
            ReligiousBuildingRuntimeSaveData saved = data.buildings[i];
            if (saved == null || string.IsNullOrWhiteSpace(saved.buildingSaveableID))
                continue;

            if (!bySaveableId.TryGetValue(saved.buildingSaveableID, out ReligiousBuildingControl building) || building == null)
            {
                //Debug.LogWarning($"[Religion Save] Could not resolve religious building '{saved.buildingSaveableID}' while loading.");
                continue;
            }

            building.ApplyRuntimeSaveData(saved, ResolveSpiritByID, ResolveRitualByID);
        }
    }

    private static SpiritDefinitionSO ResolveSpiritByID(string spiritID)
    {
        if (string.IsNullOrWhiteSpace(spiritID))
            return null;

        if (_spiritById == null)
        {
            _spiritById = new Dictionary<string, SpiritDefinitionSO>(StringComparer.Ordinal);
            SpiritDefinitionSO[] defs = Resources.LoadAll<SpiritDefinitionSO>(string.Empty);

            for (int i = 0; i < defs.Length; i++)
            {
                SpiritDefinitionSO def = defs[i];
                if (def == null || string.IsNullOrWhiteSpace(def.spiritID))
                    continue;

                string id = def.spiritID.Trim();
                if (!_spiritById.ContainsKey(id))
                    _spiritById.Add(id, def);
            }
        }

        _spiritById.TryGetValue(spiritID.Trim(), out SpiritDefinitionSO result);
        return result;
    }

    private static ReligionRitualDefinitionSO ResolveRitualByID(string ritualID)
    {
        if (string.IsNullOrWhiteSpace(ritualID))
            return null;

        if (_ritualById == null)
        {
            _ritualById = new Dictionary<string, ReligionRitualDefinitionSO>(StringComparer.Ordinal);
            ReligionRitualDefinitionSO[] defs = Resources.LoadAll<ReligionRitualDefinitionSO>(string.Empty);

            for (int i = 0; i < defs.Length; i++)
            {
                ReligionRitualDefinitionSO def = defs[i];
                if (def == null || string.IsNullOrWhiteSpace(def.ritualID))
                    continue;

                string id = def.ritualID.Trim();
                if (!_ritualById.ContainsKey(id))
                    _ritualById.Add(id, def);
            }
        }

        _ritualById.TryGetValue(ritualID.Trim(), out ReligionRitualDefinitionSO result);
        return result;
    }
}
