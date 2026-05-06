using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-10000)]
public class SceneStartupLifecycleScanner : MonoBehaviour
{
    [Header("Scan Settings")]
    [SerializeField] private bool includeInactiveObjects = true;
    [SerializeField] private bool onlyLogActiveAndEnabled = false;
    [SerializeField] private bool logAutomatically = true;
    [SerializeField] private bool includeAwakeMethods = true;
    [SerializeField] private bool includeOnEnableMethods = true;
    [SerializeField] private bool includeStartMethods = true;
    [SerializeField] private bool sortByHierarchyPath = true;
    [SerializeField] private int framesToWaitAfterSceneLoad = 1;

    private bool _hasDumped;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private IEnumerator Start()
    {
        if (!logAutomatically)
            yield break;

        // If this object already exists in the loaded scene, still do a delayed scan.
        yield return StartCoroutine(DelayedDump("Start"));
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!logAutomatically)
            return;

        if (_hasDumped)
            return;

        if (scene != gameObject.scene)
            return;

        StartCoroutine(DelayedDump("sceneLoaded"));
    }

    private IEnumerator DelayedDump(string phase)
    {
        if (_hasDumped)
            yield break;

        for (int i = 0; i < framesToWaitAfterSceneLoad; i++)
            yield return null;

        Scene scene = gameObject.scene;
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogWarning($"[LifecycleScan] Scene still not ready during phase '{phase}'.", this);
            yield break;
        }

        _hasDumped = true;
        DumpLifecycleInventory(phase);
    }

    [ContextMenu("Dump Lifecycle Inventory")]
    public void DumpLifecycleInventoryFromMenu()
    {
        DumpLifecycleInventory("Manual");
    }

    private void DumpLifecycleInventory(string phase)
    {
        Scene scene = gameObject.scene;
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogWarning("[LifecycleScan] Scene is not valid or not loaded yet.", this);
            return;
        }

        List<Entry> entries = new List<Entry>();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(includeInactiveObjects);

            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour == null)
                    continue;

                if (onlyLogActiveAndEnabled && !behaviour.isActiveAndEnabled)
                    continue;

                Type type = behaviour.GetType();

                bool hasAwake = includeAwakeMethods && DeclaresParameterlessMethod(type, "Awake");
                bool hasOnEnable = includeOnEnableMethods && DeclaresParameterlessMethod(type, "OnEnable");
                bool hasStart = includeStartMethods && DeclaresParameterlessMethod(type, "Start");

                if (!hasAwake && !hasOnEnable && !hasStart)
                    continue;

                entries.Add(new Entry
                {
                    hierarchyPath = GetHierarchyPath(behaviour.transform),
                    typeName = type.FullName,
                    activeInHierarchy = behaviour.gameObject.activeInHierarchy,
                    enabled = behaviour.enabled,
                    hasAwake = hasAwake,
                    hasOnEnable = hasOnEnable,
                    hasStart = hasStart
                });
            }
        }

        if (sortByHierarchyPath)
        {
            entries.Sort((a, b) =>
            {
                int pathCompare = string.Compare(a.hierarchyPath, b.hierarchyPath, StringComparison.Ordinal);
                if (pathCompare != 0) return pathCompare;
                return string.Compare(a.typeName, b.typeName, StringComparison.Ordinal);
            });
        }
        else
        {
            entries.Sort((a, b) =>
            {
                int typeCompare = string.Compare(a.typeName, b.typeName, StringComparison.Ordinal);
                if (typeCompare != 0) return typeCompare;
                return string.Compare(a.hierarchyPath, b.hierarchyPath, StringComparison.Ordinal);
            });
        }

        int awakeCount = 0;
        int onEnableCount = 0;
        int startCount = 0;

        foreach (Entry entry in entries)
        {
            if (entry.hasAwake) awakeCount++;
            if (entry.hasOnEnable) onEnableCount++;
            if (entry.hasStart) startCount++;
        }

        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine($"[LifecycleScan] Scene='{scene.name}' Phase='{phase}'");
        sb.AppendLine($"[LifecycleScan] Candidates={entries.Count}  Awake={awakeCount}  OnEnable={onEnableCount}  Start={startCount}");
        sb.AppendLine("--------------------------------------------------------------------------------");

        foreach (Entry entry in entries)
        {
            sb.AppendLine(
                $"{entry.hierarchyPath} | {entry.typeName} | " +
                $"activeInHierarchy={entry.activeInHierarchy} enabled={entry.enabled} | " +
                $"Awake={entry.hasAwake} OnEnable={entry.hasOnEnable} Start={entry.hasStart}");
        }

        Debug.Log(sb.ToString(), this);
    }

    private static bool DeclaresParameterlessMethod(Type type, string methodName)
    {
        return type.GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
            null,
            Type.EmptyTypes,
            null) != null;
    }

    private static string GetHierarchyPath(Transform t)
    {
        if (t == null)
            return "<null>";

        Stack<string> names = new Stack<string>();
        Transform current = t;

        while (current != null)
        {
            names.Push(current.name);
            current = current.parent;
        }

        return string.Join("/", names.ToArray());
    }

    private struct Entry
    {
        public string hierarchyPath;
        public string typeName;
        public bool activeInHierarchy;
        public bool enabled;
        public bool hasAwake;
        public bool hasOnEnable;
        public bool hasStart;
    }
}