using System.Collections.Generic;
using UnityEngine;

public class TsunamiOverlayPool
{
    private readonly Transform parent;

    private readonly Dictionary<GameObject, Stack<GameObject>> pools =
        new Dictionary<GameObject, Stack<GameObject>>();

    public TsunamiOverlayPool(Transform parent)
    {
        this.parent = parent;
    }

    public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
            return null;

        if (!pools.TryGetValue(prefab, out Stack<GameObject> pool))
        {
            pool = new Stack<GameObject>();
            pools.Add(prefab, pool);
        }

        GameObject instance = pool.Count > 0
            ? pool.Pop()
            : Object.Instantiate(prefab);

        if (parent != null)
            instance.transform.SetParent(parent, true);

        instance.transform.SetPositionAndRotation(position, rotation);
        instance.SetActive(true);

        return instance;
    }

    public void Return(GameObject prefab, GameObject instance)
    {
        if (prefab == null || instance == null)
            return;

        if (!pools.TryGetValue(prefab, out Stack<GameObject> pool))
        {
            pool = new Stack<GameObject>();
            pools.Add(prefab, pool);
        }

        instance.SetActive(false);

        if (parent != null)
            instance.transform.SetParent(parent, false);

        pool.Push(instance);
    }

    public void Prewarm(GameObject prefab, int count)
    {
        if (prefab == null || count <= 0)
            return;

        if (!pools.TryGetValue(prefab, out Stack<GameObject> pool))
        {
            pool = new Stack<GameObject>();
            pools.Add(prefab, pool);
        }

        for (int i = 0; i < count; i++)
        {
            GameObject instance = Object.Instantiate(prefab);

            if (parent != null)
                instance.transform.SetParent(parent, false);

            instance.SetActive(false);
            pool.Push(instance);
        }
    }

    public void Clear()
    {
        foreach (KeyValuePair<GameObject, Stack<GameObject>> pair in pools)
        {
            Stack<GameObject> pool = pair.Value;

            while (pool.Count > 0)
            {
                GameObject instance = pool.Pop();

                if (instance != null)
                    Object.Destroy(instance);
            }
        }

        pools.Clear();
    }
}