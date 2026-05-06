using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple prefab-keyed visual pool for cloud objects.
/// Instances are reused per prefab type.
/// </summary>
public class CloudVisualPool : MonoBehaviour
{
    private readonly Dictionary<GameObject, Stack<GameObject>> _poolByPrefab =
        new Dictionary<GameObject, Stack<GameObject>>();

    private readonly Dictionary<GameObject, GameObject> _prefabByInstance =
        new Dictionary<GameObject, GameObject>();

    public void Prewarm(GameObject prefab, int targetCount, Transform parent, int maxCreatesThisCall = int.MaxValue)
    {
        if (prefab == null || targetCount <= 0)
            return;

        if (!_poolByPrefab.TryGetValue(prefab, out Stack<GameObject> stack))
        {
            stack = new Stack<GameObject>(targetCount);
            _poolByPrefab[prefab] = stack;
        }

        int missing = Mathf.Max(0, targetCount - stack.Count);
        int createCount = Mathf.Min(missing, Mathf.Max(0, maxCreatesThisCall));

        for (int i = 0; i < createCount; i++)
        {
            GameObject instance = Instantiate(prefab, transform);
            instance.SetActive(false);

            if (parent != null)
                instance.transform.SetParent(transform, false);

            _prefabByInstance[instance] = prefab;
            stack.Push(instance);
        }
    }

    public GameObject Get(GameObject prefab, Transform activeParent, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
            return null;

        if (!_poolByPrefab.TryGetValue(prefab, out Stack<GameObject> stack))
        {
            stack = new Stack<GameObject>();
            _poolByPrefab[prefab] = stack;
        }

        GameObject instance = null;

        while (stack.Count > 0 && instance == null)
            instance = stack.Pop();

        if (instance == null)
        {
            instance = Instantiate(prefab);
            _prefabByInstance[instance] = prefab;
        }

        Transform tr = instance.transform;
        tr.SetParent(activeParent, true);
        tr.position = position;
        tr.rotation = rotation;
        instance.SetActive(true);

        return instance;
    }

    public void Return(GameObject prefab, GameObject instance)
    {
        if (instance == null)
            return;

        if (prefab == null && !_prefabByInstance.TryGetValue(instance, out prefab))
        {
            Destroy(instance);
            return;
        }

        if (!_poolByPrefab.TryGetValue(prefab, out Stack<GameObject> stack))
        {
            stack = new Stack<GameObject>();
            _poolByPrefab[prefab] = stack;
        }

        instance.SetActive(false);
        instance.transform.SetParent(transform, false);
        stack.Push(instance);
    }

    public void Return(GameObject instance)
    {
        if (instance == null)
            return;

        if (_prefabByInstance.TryGetValue(instance, out GameObject prefab))
            Return(prefab, instance);
        else
            Destroy(instance);
    }

    public void ClearDestroyedReferences()
    {
        List<GameObject> prefabKeys = new List<GameObject>(_poolByPrefab.Keys);

        for (int i = 0; i < prefabKeys.Count; i++)
        {
            GameObject prefab = prefabKeys[i];
            Stack<GameObject> oldStack = _poolByPrefab[prefab];
            Stack<GameObject> newStack = new Stack<GameObject>(oldStack.Count);

            while (oldStack.Count > 0)
            {
                GameObject instance = oldStack.Pop();
                if (instance != null)
                    newStack.Push(instance);
            }

            _poolByPrefab[prefab] = newStack;
        }
    }
}