#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Run AFTER Tools → Kardeshev → Create Base Resource Spawner Definitions.
//
// Scene install:  Tools → Kardeshev → Install Base Spawners on Scene Nodes
// Prefab install: select prefab(s) in Project, then
//                 Tools → Kardeshev → Install Base Spawners on Selected Prefabs
// Dry run:        Tools → Kardeshev → Preview Base Spawner Assignments
//
// Assignment logic:
//   Primary folder  — Base/{EnvironmentType}/  (name matches enum exactly)
//   Bonus folder    — Base/Cave/   when tileType is Cave
//                   — Base/River/  when tileType is River*, RiverMouth, or LakeMouth
public static class EnvironmentSpawnerInstaller
{
    private const string Base = "Assets/ScriptableObjects/ResourceSpawners/Base";

    // ── Scene installer ───────────────────────────────────────────────────────

    [MenuItem("Tools/Kardeshev/Install Base Spawners on Scene Nodes")]
    public static void InstallOnScene()
    {
        var nodes = UnityEngine.Object.FindObjectsOfType<EnvironmentResourceNode>(true);
        if (nodes.Length == 0)
        {
            EditorUtility.DisplayDialog("No Nodes", "No EnvironmentResourceNode found in the active scene.", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("Install Base Spawners",
            $"Replace baseSpawners on {nodes.Length} node(s) in the active scene.\n\nUndo is supported.",
            "Install", "Cancel")) return;

        int assigned = 0, skipped = 0, totalSOs = 0;
        var sb = new StringBuilder("=== Base Spawner Install (Scene) ===\n");

        foreach (var node in nodes)
        {
            var ec = node.GetComponent<EnvironmentControl>();
            if (ec == null) { skipped++; continue; }

            var spawners = LoadSpawnersFor(ec.environmentType, ec.environmentTileType);
            Undo.RecordObject(node, "Install Base Spawners");
            node.baseSpawners = spawners;
            EditorUtility.SetDirty(node);
            assigned++;
            totalSOs += spawners.Count;
            sb.AppendLine($"  {node.name,-35} [{ec.environmentType}/{ec.environmentTileType}] → {spawners.Count}");
        }

        sb.AppendLine($"\nAssigned: {assigned}   Skipped (no EC): {skipped}   Total SOs: {totalSOs}");
        Debug.Log(sb.ToString());
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    // ── Selected-prefab installer ─────────────────────────────────────────────

    [MenuItem("Tools/Kardeshev/Install Base Spawners on Selected Prefabs")]
    public static void InstallOnSelectedPrefabs()
    {
        var prefabs = Selection.GetFiltered<GameObject>(SelectionMode.Assets);
        if (prefabs.Length == 0)
        {
            EditorUtility.DisplayDialog("No Selection", "Select one or more prefabs in the Project window first.", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("Install on Prefabs",
            $"Replace baseSpawners on {prefabs.Length} selected prefab(s).\nThis saves the prefab asset.",
            "Install", "Cancel")) return;

        int assigned = 0, skipped = 0;
        var sb = new StringBuilder("=== Base Spawner Install (Prefabs) ===\n");

        foreach (var prefab in prefabs)
        {
            string path = AssetDatabase.GetAssetPath(prefab);
            using var scope = new PrefabUtility.EditPrefabContentsScope(path);
            var root = scope.prefabContentsRoot;
            foreach (var node in root.GetComponentsInChildren<EnvironmentResourceNode>(true))
            {
                var ec = node.GetComponent<EnvironmentControl>();
                if (ec == null) { skipped++; continue; }

                node.baseSpawners = LoadSpawnersFor(ec.environmentType, ec.environmentTileType);
                assigned++;
                sb.AppendLine($"  {prefab.name}/{node.name} [{ec.environmentType}] → {node.baseSpawners.Count}");
            }
        }

        sb.AppendLine($"\nAssigned: {assigned}   Skipped: {skipped}");
        Debug.Log(sb.ToString());
    }

    // ── Dry-run preview ───────────────────────────────────────────────────────

    [MenuItem("Tools/Kardeshev/Preview Base Spawner Assignments")]
    public static void Preview()
    {
        var sb = new StringBuilder("=== Base Spawner Preset Table (no changes made) ===\n");
        sb.AppendLine($"  {"Folder",-25} {"SOs":>4}   Path");
        sb.AppendLine(new string('-', 65));

        foreach (EnvironmentType et in Enum.GetValues(typeof(EnvironmentType)))
        {
            string folder = Base + "/" + et;
            if (!AssetDatabase.IsValidFolder(folder)) continue;
            int count = AssetDatabase.FindAssets("t:ResourceSpawnerDefinition", new[]{ folder }).Length;
            sb.AppendLine($"  {et.ToString(),-25} {count,4}   Base/{et}/");
        }

        // Tile-type-driven bonus folders
        var tileTypeFolders = new (string folder, string label)[]
        {
            ("Cave",       "Cave tile type"),
            ("River",      "River/RiverMouth/LakeMouth tile types"),
            ("Lake",       "Lake/Water tile types"),
            ("LakeEdge",   "LakeEdge/LakeCorner/LakeEdgeEnd tile types"),
            ("Ocean",      "Ocean / Coastline tile types"),
            ("Beach",      "Beach/BeachEnd / Coastline tile types"),
            ("SaltLake",   "SaltLake tile type"),
            ("Mountain",   "Mountain tile type"),
        };
        foreach (var (folder, label) in tileTypeFolders)
        {
            string path = Base + "/" + folder;
            if (!AssetDatabase.IsValidFolder(path)) continue;
            int count = AssetDatabase.FindAssets("t:ResourceSpawnerDefinition", new[]{ path }).Length;
            sb.AppendLine($"  [{label}]{new string(' ', Mathf.Max(1, 38 - label.Length))}{count,4}   Base/{folder}/");
        }

        Debug.Log(sb.ToString());
    }

    // ── Core lookup ───────────────────────────────────────────────────────────

    private static List<ResourceSpawnerDefinition> LoadSpawnersFor(
        EnvironmentType et, EnvironmentTileType tt)
    {
        var seen = new HashSet<string>();
        var list = new List<ResourceSpawnerDefinition>();

        void AddFolder(string sub)
        {
            string path = Base + "/" + sub;
            if (!AssetDatabase.IsValidFolder(path)) return;
            foreach (var guid in AssetDatabase.FindAssets("t:ResourceSpawnerDefinition", new[]{ path }))
            {
                if (!seen.Add(guid)) continue;
                var so = AssetDatabase.LoadAssetAtPath<ResourceSpawnerDefinition>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (so != null) list.Add(so);
            }
        }

        // Primary: environment-type folder (covers most biome tiles)
        AddFolder(et.ToString());

        // Bonus: tile-type-driven folders.
        // Deduplication via 'seen' means loading an already-covered folder is a no-op.
        switch (tt)
        {
            case EnvironmentTileType.Cave:
                AddFolder("Cave");
                break;
            case EnvironmentTileType.River:
            case EnvironmentTileType.RiverCorner:
            case EnvironmentTileType.RiverSplit:
            case EnvironmentTileType.RiverCross:
            case EnvironmentTileType.RiverEnd:
            case EnvironmentTileType.RiverMouth:
            case EnvironmentTileType.LakeMouth:
                AddFolder("River");
                break;
            case EnvironmentTileType.Lake:
            case EnvironmentTileType.Water:
                AddFolder("Lake");
                break;
            case EnvironmentTileType.LakeEdge:
            case EnvironmentTileType.LakeCorner:
            case EnvironmentTileType.LakeEdgeEnd:
                AddFolder("LakeEdge");
                break;
            case EnvironmentTileType.Ocean:
                AddFolder("Ocean");
                break;
            case EnvironmentTileType.Coastline:
            case EnvironmentTileType.CoastlineCorner:
                AddFolder("Ocean");
                AddFolder("Beach");
                break;
            case EnvironmentTileType.Beach:
            case EnvironmentTileType.BeachEnd:
                AddFolder("Beach");
                break;
            case EnvironmentTileType.SaltLake:
                AddFolder("SaltLake");
                break;
            case EnvironmentTileType.Mountain:
                AddFolder("Mountain");
                break;
        }

        return list;
    }
}
#endif
