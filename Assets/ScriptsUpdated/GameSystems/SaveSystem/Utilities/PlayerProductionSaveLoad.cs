using System;
using System.Collections.Generic;
using UnityEngine;

public static class PlayerProductionSaveLoad
{
    private static Dictionary<string, EnvironmentControl> _environmentById;

    public static PlayerProductionSaveData SaveState()
    {
        PlayerProductionSaveData data = new PlayerProductionSaveData();

        ProductionBuildingControl[] controls = UnityEngine.Object.FindObjectsOfType<ProductionBuildingControl>(true);
        for (int i = 0; i < controls.Length; i++)
        {
            ProductionBuildingControl prod = controls[i];
            if (prod == null)
                continue;

            Saveable saveable = prod.GetComponent<Saveable>();
            if (saveable == null)
                saveable = prod.GetComponentInParent<Saveable>();

            if (saveable == null || string.IsNullOrWhiteSpace(saveable.uniqueID))
                continue;

            data.buildings.Add(prod.CaptureRuntimeSaveData(saveable.uniqueID));
        }

        return data;
    }

    public static void LoadState(PlayerProductionSaveData data)
    {
        ProductionBuildingControl[] controls = UnityEngine.Object.FindObjectsOfType<ProductionBuildingControl>(true);
        Dictionary<string, ProductionBuildingControl> bySaveableId = new Dictionary<string, ProductionBuildingControl>(StringComparer.Ordinal);

        for (int i = 0; i < controls.Length; i++)
        {
            ProductionBuildingControl prod = controls[i];
            if (prod == null)
                continue;

            Saveable saveable = prod.GetComponent<Saveable>();
            if (saveable == null)
                saveable = prod.GetComponentInParent<Saveable>();

            if (saveable != null && !string.IsNullOrWhiteSpace(saveable.uniqueID) && !bySaveableId.ContainsKey(saveable.uniqueID))
                bySaveableId.Add(saveable.uniqueID, prod);
        }

        BuildEnvironmentLookup();

        if (data == null || data.buildings == null)
            return;

        for (int i = 0; i < data.buildings.Count; i++)
        {
            ProductionBuildingRuntimeSaveData saved = data.buildings[i];
            if (saved == null || string.IsNullOrWhiteSpace(saved.buildingSaveableID))
                continue;

            if (!bySaveableId.TryGetValue(saved.buildingSaveableID, out ProductionBuildingControl prod) || prod == null)
            {
                Debug.LogWarning($"[Production Save] Could not resolve production building '{saved.buildingSaveableID}' while loading.");
                continue;
            }

            prod.ApplyRuntimeSaveData(saved, ResolvePlanByID, ResolveEnvironmentByID);
        }
    }

    private static void BuildEnvironmentLookup()
    {
        _environmentById = new Dictionary<string, EnvironmentControl>(StringComparer.Ordinal);

        EnvironmentControl[] envs = UnityEngine.Object.FindObjectsOfType<EnvironmentControl>(true);
        for (int i = 0; i < envs.Length; i++)
        {
            EnvironmentControl env = envs[i];
            if (env == null || string.IsNullOrWhiteSpace(env.EnvironmentID))
                continue;

            if (!_environmentById.ContainsKey(env.EnvironmentID))
                _environmentById.Add(env.EnvironmentID, env);
        }
    }

    private static ProductionPlan ResolvePlanByID(string productionID)
    {
        if (string.IsNullOrWhiteSpace(productionID))
            return null;

        return ProductionPlanManager.Instance != null
            ? ProductionPlanManager.Instance.GetByID(productionID.Trim())
            : null;
    }

    private static EnvironmentControl ResolveEnvironmentByID(string environmentID)
    {
        if (string.IsNullOrWhiteSpace(environmentID) || _environmentById == null)
            return null;

        _environmentById.TryGetValue(environmentID.Trim(), out EnvironmentControl result);
        return result;
    }
}