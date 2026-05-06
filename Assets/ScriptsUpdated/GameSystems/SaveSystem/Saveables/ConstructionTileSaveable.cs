using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BuildingConstruction))]
public class ConstructionTileSaveable : Saveable
{
    public static readonly HashSet<ConstructionTileSaveable> Live = new HashSet<ConstructionTileSaveable>();
    public static readonly HashSet<ConstructionTileSaveable> Dirty = new HashSet<ConstructionTileSaveable>();

    private static Dictionary<string, GameObject> _prefabLookupByName;

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

    public ConstructionTileSaveData GetSaveData()
    {
        BuildingConstruction bc = GetComponent<BuildingConstruction>();

        return new ConstructionTileSaveData(
            SaveState(),
            CleanPrefabName(gameObject.name),
            bc != null ? bc.CaptureRuntimeSaveData() : null
        );
    }

    public void LoadFromSaveData(ConstructionTileSaveData data)
    {
        if (data == null || data.constructionTileData == null)
            return;

        LoadState(data.constructionTileData);

        BuildingConstruction bc = GetComponent<BuildingConstruction>();
        if (bc == null || data.runtimeData == null)
            return;

        Building resolvedDef = ResolveBuilding(data.runtimeData.buildingID);
        GameObject resolvedFinalOverride = ResolvePrefab(data.runtimeData.finalBuildingOverridePrefabName);

        if (resolvedDef == null && !string.IsNullOrWhiteSpace(data.runtimeData.buildingID))
        {
            Debug.LogWarning($"[ConstructionTileSaveable] Could not resolve building '{data.runtimeData.buildingID}' while loading '{name}'.");
        }

        bc.ApplyRuntimeSaveData(data.runtimeData, resolvedDef, resolvedFinalOverride);
        PlayerConstructionManager.Instance?.RegisterLoadedConstruction(bc);

        ClearDirty();
    }

    private static string CleanPrefabName(string rawName)
    {
        return string.IsNullOrWhiteSpace(rawName)
            ? string.Empty
            : rawName.Replace("(Clone)", "").Trim();
    }

    private static Building ResolveBuilding(string buildingID)
    {
        if (string.IsNullOrWhiteSpace(buildingID))
            return null;

        return BuildingManager.Instance != null
            ? BuildingManager.Instance.GetBuildingByID(buildingID.Trim())
            : null;
    }

    private static GameObject ResolvePrefab(string prefabName)
    {
        if (string.IsNullOrWhiteSpace(prefabName))
            return null;

        if (_prefabLookupByName == null)
        {
            _prefabLookupByName = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            GameObject[] allPrefabs = Resources.LoadAll<GameObject>(string.Empty);

            for (int i = 0; i < allPrefabs.Length; i++)
            {
                GameObject prefab = allPrefabs[i];
                if (prefab == null || string.IsNullOrWhiteSpace(prefab.name))
                    continue;

                if (!_prefabLookupByName.ContainsKey(prefab.name))
                    _prefabLookupByName.Add(prefab.name, prefab);
            }
        }

        _prefabLookupByName.TryGetValue(prefabName.Trim(), out GameObject result);
        return result;
    }
}