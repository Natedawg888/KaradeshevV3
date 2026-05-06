using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class LightningVisualPool : MonoBehaviour
{
    private readonly Dictionary<GameObject, Stack<GameObject>> _poolByPrefab =
        new Dictionary<GameObject, Stack<GameObject>>();

    public GameObject Get(GameObject prefab, Transform parent, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
            return null;

        Stack<GameObject> stack;
        if (!_poolByPrefab.TryGetValue(prefab, out stack))
        {
            stack = new Stack<GameObject>();
            _poolByPrefab[prefab] = stack;
        }

        GameObject instance = null;

        while (stack.Count > 0 && instance == null)
            instance = stack.Pop();

        if (instance == null)
            instance = CreateInstance(prefab);

        Transform tr = instance.transform;
        tr.SetParent(parent, false);
        tr.SetPositionAndRotation(position, rotation);
        tr.localScale = prefab.transform.localScale;

        instance.SetActive(true);
        return instance;
    }

    public void Return(GameObject prefab, GameObject instance)
    {
        if (prefab == null || instance == null)
            return;

        Stack<GameObject> stack;
        if (!_poolByPrefab.TryGetValue(prefab, out stack))
        {
            stack = new Stack<GameObject>();
            _poolByPrefab[prefab] = stack;
        }

        instance.SetActive(false);
        instance.transform.SetParent(transform, false);
        stack.Push(instance);
    }

    public void Prewarm(GameObject prefab, int count, Transform parent = null, int maxCreatesPerCall = 32)
    {
        if (prefab == null || count <= 0)
            return;

        Stack<GameObject> stack;
        if (!_poolByPrefab.TryGetValue(prefab, out stack))
        {
            stack = new Stack<GameObject>();
            _poolByPrefab[prefab] = stack;
        }

        int creates = Mathf.Min(count, Mathf.Max(1, maxCreatesPerCall));

        for (int i = 0; i < creates; i++)
        {
            GameObject instance = CreateInstance(prefab);
            instance.SetActive(false);
            instance.transform.SetParent(parent != null ? parent : transform, false);
            stack.Push(instance);
        }
    }

    public void ClearDestroyedReferences()
    {
        foreach (KeyValuePair<GameObject, Stack<GameObject>> kvp in _poolByPrefab)
        {
            if (kvp.Value == null || kvp.Value.Count == 0)
                continue;

            Stack<GameObject> rebuilt = new Stack<GameObject>(kvp.Value.Count);

            while (kvp.Value.Count > 0)
            {
                GameObject go = kvp.Value.Pop();
                if (go != null)
                    rebuilt.Push(go);
            }

            kvp.Value.Clear();

            while (rebuilt.Count > 0)
                kvp.Value.Push(rebuilt.Pop());
        }
    }

    private GameObject CreateInstance(GameObject prefab)
    {
        GameObject instance = Instantiate(prefab, transform);
        instance.name = prefab.name + "_Pooled";
        return instance;
    }
}