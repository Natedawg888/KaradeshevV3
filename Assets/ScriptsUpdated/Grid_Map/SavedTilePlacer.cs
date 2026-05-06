using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class SavedTilePlacer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private MapTilePlacer tilePrefabSource;
    [SerializeField] private Transform tileParent;

    [Header("Optional Extra Tile Prefabs")]
    [SerializeField] private GameObject[] extraTilePrefabs;

    [Header("Options")]
    [SerializeField] private bool includeResourcesFallback = true;
    [SerializeField] private bool logWarnings = true;

    [SerializeField, Min(1)] private int tilesPerFrameDuringLoad = 1;

    private readonly Dictionary<string, GameObject> _prefabLookup =
        new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);

    private GameSceneManager _cachedGameSceneManager;

    private GameSceneManager GetGameSceneManager()
    {
        if (_cachedGameSceneManager == null)
            _cachedGameSceneManager = FindObjectOfType<GameSceneManager>(true);

        return _cachedGameSceneManager;
    }

    private void BeginSharedLoadProgress()
    {
        GameSceneManager gsm = GetGameSceneManager();
        if (gsm == null)
            return;

        gsm.BeginExternalLoopingLoad();
    }

    private void EndSharedLoadProgress(bool showEmpty = false)
    {
        GameSceneManager gsm = GetGameSceneManager();
        if (gsm == null)
            return;

        gsm.EndExternalLoopingLoad(showEmpty, false);
    }

    public IEnumerator PlaceSavedTilesCoroutine(List<TileSaveData> savedTiles)
    {
        Debug.Log($"[SavedTilePlacer] START PlaceSavedTilesCoroutine. savedTiles={(savedTiles != null ? savedTiles.Count : 0)}");

        if (gridManager == null)
        {
            Debug.LogError("[SavedTilePlacer] GridManager reference is missing.");
            yield break;
        }

        if (tilePrefabSource == null)
        {
            Debug.LogError("[SavedTilePlacer] MapTilePlacer reference is missing.");
            yield break;
        }

        if (tileParent == null)
            tileParent = transform;

        MapTilePlacer.SetWorldReady(false);

        BeginSharedLoadProgress();
        yield return null;

        Debug.Log("[SavedTilePlacer] Clearing existing tiles...");
        yield return StartCoroutine(ClearExistingTilesCoroutine());

        Debug.Log("[SavedTilePlacer] Resetting grid occupancy...");
        TryResetGridOccupancy();

        Debug.Log("[SavedTilePlacer] Rebuilding prefab lookup...");
        RebuildPrefabLookup();
        Debug.Log($"[SavedTilePlacer] Prefab lookup count = {_prefabLookup.Count}");

        if (savedTiles == null || savedTiles.Count == 0)
        {
            Debug.LogWarning("[SavedTilePlacer] No saved tiles were provided.");
            EndSharedLoadProgress(false);
            MapTilePlacer.SetWorldReady(true);
            yield break;
        }

        int restoredCount = 0;
        int failedPrefabCount = 0;
        int failedRestoreCount = 0;
        int batch = 0;

        for (int i = 0; i < savedTiles.Count; i++)
        {
            TileSaveData savedTile = savedTiles[i];

            if (savedTile == null)
                continue;

            SaveData baseData = GetSaveData(savedTile);
            string tilePrefabName = GetTilePrefabName(savedTile);

            if (string.IsNullOrWhiteSpace(tilePrefabName))
            {
                Debug.LogWarning($"[SavedTilePlacer] Saved tile at index {i} has no tile prefab name.");
                failedPrefabCount++;
                continue;
            }

            GameObject tilePrefab = ResolveTilePrefab(tilePrefabName);
            if (tilePrefab == null)
            {
                Debug.LogWarning($"[SavedTilePlacer] Tile prefab not found for '{tilePrefabName}'.");
                failedPrefabCount++;
                continue;
            }

            Vector3 position = GetSavedPosition(savedTile, baseData);
            Quaternion rotation = GetSavedRotation(savedTile, baseData);
            Vector3 scale = GetSavedScale(savedTile, baseData);

            GameObject tileGO = Instantiate(tilePrefab, position, rotation, tileParent);
            tileGO.name = CleanName(tilePrefab.name);
            tileGO.transform.localScale = scale;

            if (baseData != null)
            {
                Saveable saveable = tileGO.GetComponent<Saveable>();
                if (saveable != null)
                    saveable.LoadState(baseData);
            }

            MarkOccupiedCells(tileGO);

            TileScript tileScript = tileGO.GetComponent<TileScript>();
            if (tileScript != null)
            {
                bool restored = RestoreTileSpawn(tileScript, savedTile);

                if (restored)
                {
                    restoredCount++;
                }
                else
                {
                    failedRestoreCount++;
                    Debug.LogWarning($"[SavedTilePlacer] Could not restore spawned environment for tile '{tileGO.name}'.");
                }
            }
            else
            {
                failedRestoreCount++;
                Debug.LogWarning($"[SavedTilePlacer] Loaded tile prefab '{tilePrefab.name}' has no TileScript.");
            }

            batch++;
            if (batch >= Mathf.Max(1, tilesPerFrameDuringLoad))
            {
                batch = 0;
                yield return null;
            }
        }

        Debug.Log(
            $"[SavedTilePlacer] DONE. restoredCount={restoredCount}, " +
            $"failedPrefabCount={failedPrefabCount}, failedRestoreCount={failedRestoreCount}");

        EndSharedLoadProgress(false);
        MapTilePlacer.SetWorldReady(true);
    }

    private IEnumerator ClearExistingTilesCoroutine()
    {
        if (tileParent == null)
            yield break;

        int batch = 0;

        for (int i = tileParent.childCount - 1; i >= 0; i--)
        {
            Transform child = tileParent.GetChild(i);
            if (child == null)
                continue;

            Destroy(child.gameObject);

            batch++;
            if (batch >= 32)
            {
                batch = 0;
                yield return null;
            }
        }

        yield return null;
    }

    private void TryResetGridOccupancy()
    {
        if (gridManager == null)
            return;

        InvokeIfExists(gridManager,
            "ClearOccupiedCells",
            "ClearAllOccupiedCells",
            "ResetOccupiedCells",
            "ResetGridOccupancy",
            "ClearGridOccupancy");
    }

    private void RebuildPrefabLookup()
    {
        _prefabLookup.Clear();

        if (tilePrefabSource != null)
        {
            RegisterPrefab(tilePrefabSource.oceanTilePrefab);
            RegisterPrefab(tilePrefabSource.beachTilePrefab);
            RegisterPrefab(tilePrefabSource.lakeTilePrefab);
            RegisterPrefab(tilePrefabSource.lakeEdgePrefab);
            RegisterPrefab(tilePrefabSource.beachCornerPrefab);
            RegisterPrefab(tilePrefabSource.lakeEdgeCornerPrefab);
            RegisterPrefab(tilePrefabSource.riverTilePrefab);

            if (tilePrefabSource.tilePrefabs != null)
            {
                for (int i = 0; i < tilePrefabSource.tilePrefabs.Length; i++)
                {
                    TilePrefab entry = tilePrefabSource.tilePrefabs[i];
                    if (entry != null)
                        RegisterPrefab(entry.prefab);
                }
            }
        }

        if (extraTilePrefabs != null)
        {
            for (int i = 0; i < extraTilePrefabs.Length; i++)
                RegisterPrefab(extraTilePrefabs[i]);
        }

        if (includeResourcesFallback)
        {
            GameObject[] all = Resources.LoadAll<GameObject>(string.Empty);
            for (int i = 0; i < all.Length; i++)
                RegisterPrefab(all[i]);
        }
    }

    private void RegisterPrefab(GameObject prefab)
    {
        if (prefab == null)
            return;

        string clean = CleanName(prefab.name);
        if (string.IsNullOrWhiteSpace(clean))
            return;

        if (!_prefabLookup.ContainsKey(clean))
            _prefabLookup.Add(clean, prefab);
    }

    private GameObject ResolveTilePrefab(string prefabName)
    {
        string clean = CleanName(prefabName);

        if (_prefabLookup.TryGetValue(clean, out GameObject prefab))
            return prefab;

        return null;
    }

    private void MarkOccupiedCells(GameObject tileGO)
    {
        if (gridManager == null || tileGO == null)
            return;

        BoxCollider bc = tileGO.GetComponent<BoxCollider>();
        if (bc == null)
            return;

        Bounds b = bc.bounds;

        int sx = Mathf.Max(1, Mathf.CeilToInt(b.size.x / gridManager.cellSize));
        int sy = Mathf.Max(1, Mathf.CeilToInt(b.size.z / gridManager.cellSize));

        Vector3 originWorld = new Vector3(
            b.min.x + 0.001f,
            tileGO.transform.position.y,
            b.min.z + 0.001f);

        Vector2Int origin = gridManager.GetGridPosition(originWorld);

        for (int dx = 0; dx < sx; dx++)
        {
            for (int dy = 0; dy < sy; dy++)
            {
                gridManager.MarkCellOccupied(origin.x + dx, origin.y + dy);
            }
        }
    }

    private bool RestoreTileSpawn(TileScript tileScript, TileSaveData savedTile)
    {
        if (tileScript == null || savedTile == null)
            return false;

        EnvironmentRuntimeSaveData envData =
            GetValue<EnvironmentRuntimeSaveData>(savedTile, "environmentData");

        if (envData == null)
            return false;

        bool spawned = false;

        if (!string.IsNullOrWhiteSpace(envData.spawnedPrefabName))
        {
            spawned = tileScript.TryForceSpawnSavedPrefab(
                envData.spawnedPrefabName,
                envData.environmentType,
                envData.environmentTileType,
                envData.isDiscovered,
                envData.localYRotation
            );
        }

        if (!spawned)
        {
            spawned = tileScript.ForceSpawnSpecific(
                envData.environmentType,
                envData.environmentTileType,
                envData.isDiscovered
            );

            if (spawned && tileScript.GetSpawnedInstance() != null)
            {
                tileScript.GetSpawnedInstance().transform.localRotation =
                    Quaternion.Euler(0f, envData.localYRotation, 0f);
            }
        }

        if (!spawned)
            return false;

        EnvironmentControl envControl = tileScript.GetComponentInChildren<EnvironmentControl>(true);
        if (envControl == null)
        {
            if (logWarnings)
                Debug.LogWarning($"[SavedTilePlacer] Spawned environment on tile '{tileScript.name}' but no EnvironmentControl was found.");
            return false;
        }

        envControl.ApplyRuntimeSaveData(envData, ResolveResourceDefinitionByKey);
        return true;
    }

    private static Dictionary<string, ResourceDefinition> _resourceLookup;

    private static ResourceDefinition ResolveResourceDefinitionByKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        if (_resourceLookup == null)
        {
            _resourceLookup = new Dictionary<string, ResourceDefinition>(StringComparer.Ordinal);

            ResourceDefinition[] allDefs = Resources.LoadAll<ResourceDefinition>(string.Empty);
            for (int i = 0; i < allDefs.Length; i++)
            {
                ResourceDefinition def = allDefs[i];
                if (def == null)
                    continue;

                string cleanName = def.name.Trim();
                if (!_resourceLookup.ContainsKey(cleanName))
                    _resourceLookup.Add(cleanName, def);
            }
        }

        _resourceLookup.TryGetValue(key.Trim(), out ResourceDefinition result);
        return result;
    }

    private SaveData GetSaveData(object source)
    {
        return GetValue<SaveData>(source, "tileData", "saveData", "baseData", "data");
    }

    private string GetTilePrefabName(object source)
    {
        return GetString(source, "tilePrefabName", "prefabName", "savedTilePrefabName", "prefab");
    }

    private Vector3 GetSavedPosition(object source, SaveData baseData)
    {
        if (baseData != null && baseData.transformData != null)
            return baseData.transformData.position;

        return GetValue<Vector3>(source, "position", "_position");
    }

    private Quaternion GetSavedRotation(object source, SaveData baseData)
    {
        if (baseData != null && baseData.transformData != null)
            return baseData.transformData.rotation;

        return GetValue<Quaternion>(source, "rotation", "_rotation");
    }

    private Vector3 GetSavedScale(object source, SaveData baseData)
    {
        if (baseData != null && baseData.transformData != null)
            return baseData.transformData.scale;

        Vector3 scale = GetValue<Vector3>(source, "scale", "localScale", "_scale");
        return scale == default ? Vector3.one : scale;
    }

    private static string CleanName(string rawName)
    {
        return string.IsNullOrWhiteSpace(rawName)
            ? string.Empty
            : rawName.Replace("(Clone)", string.Empty).Trim();
    }

    private static void InvokeIfExists(object target, params string[] methodNames)
    {
        if (target == null)
            return;

        Type type = target.GetType();

        for (int i = 0; i < methodNames.Length; i++)
        {
            MethodInfo mi = type.GetMethod(
                methodNames[i],
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);

            if (mi != null)
            {
                mi.Invoke(target, null);
                return;
            }
        }
    }

    private static T GetValue<T>(object source, params string[] names)
    {
        object boxed = GetBoxed(source, names);
        if (boxed == null)
            return default;

        if (boxed is T exact)
            return exact;

        try
        {
            return (T)Convert.ChangeType(boxed, typeof(T));
        }
        catch
        {
            return default;
        }
    }

    private static object GetObject(object source, params string[] names)
    {
        return GetBoxed(source, names);
    }

    private static string GetString(object source, params string[] names)
    {
        object boxed = GetBoxed(source, names);
        return boxed?.ToString();
    }

    private static bool? GetBool(object source, bool defaultValue, params string[] names)
    {
        object boxed = GetBoxed(source, names);
        if (boxed == null)
            return defaultValue;

        if (boxed is bool b)
            return b;

        if (bool.TryParse(boxed.ToString(), out bool parsed))
            return parsed;

        return defaultValue;
    }

    private static float? GetFloat(object source, float defaultValue, params string[] names)
    {
        object boxed = GetBoxed(source, names);
        if (boxed == null)
            return defaultValue;

        if (boxed is float f)
            return f;

        if (boxed is int i)
            return i;

        if (float.TryParse(boxed.ToString(), out float parsed))
            return parsed;

        return defaultValue;
    }

    private static bool TryGetEnum<TEnum>(object source, out TEnum value, params string[] names) where TEnum : struct
    {
        value = default;

        object boxed = GetBoxed(source, names);
        if (boxed == null)
            return false;

        if (boxed is TEnum typed)
        {
            value = typed;
            return true;
        }

        Type enumType = typeof(TEnum);

        try
        {
            if (boxed is string s && Enum.TryParse(s, true, out TEnum parsed))
            {
                value = parsed;
                return true;
            }

            value = (TEnum)Enum.ToObject(enumType, boxed);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static object GetBoxed(object source, params string[] names)
    {
        if (source == null)
            return null;

        Type type = source.GetType();

        for (int i = 0; i < names.Length; i++)
        {
            FieldInfo field = type.GetField(names[i],
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (field != null)
                return field.GetValue(source);

            PropertyInfo prop = type.GetProperty(names[i],
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (prop != null && prop.CanRead)
                return prop.GetValue(source);
        }

        return null;
    }
}