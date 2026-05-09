using System;
using System.Collections.Generic;
using UnityEngine;

// Singleton registry for all ResourceSpawnerDefinition SOs.
// Place one instance in your persistent/FinalSetup scene.
// Right-click the component → "Auto-Populate from Project" in the Editor to fill allSpawners,
// then save the scene so the list is baked into the prefab/scene reference.
public class ResourceSpawnerRegistry : MonoBehaviour
{
    public static ResourceSpawnerRegistry Instance { get; private set; }

    [Tooltip("All ResourceSpawnerDefinition SOs in the project. Use Context Menu to auto-populate.")]
    public List<ResourceSpawnerDefinition> allSpawners = new();

    private Dictionary<string, ResourceSpawnerDefinition> _byID;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            BuildLookup();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void BuildLookup()
    {
        _byID = new Dictionary<string, ResourceSpawnerDefinition>(StringComparer.Ordinal);
        for (int i = 0; i < allSpawners.Count; i++)
        {
            var def = allSpawners[i];
            if (def == null || string.IsNullOrWhiteSpace(def.spawnerID)) continue;
            if (!_byID.ContainsKey(def.spawnerID))
                _byID[def.spawnerID] = def;
        }
    }

    public ResourceSpawnerDefinition GetByID(string id)
    {
        if (string.IsNullOrEmpty(id) || _byID == null) return null;
        _byID.TryGetValue(id, out var def);
        return def;
    }

#if UNITY_EDITOR
    [ContextMenu("Auto-Populate from Project")]
    private void AutoPopulate()
    {
        allSpawners.Clear();
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:ResourceSpawnerDefinition");
        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            var def = UnityEditor.AssetDatabase.LoadAssetAtPath<ResourceSpawnerDefinition>(path);
            if (def != null && !string.IsNullOrWhiteSpace(def.spawnerID))
                allSpawners.Add(def);
        }
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[SpawnerRegistry] Auto-populated {allSpawners.Count} spawner definitions.");
    }
#endif
}
