using System.Collections.Generic;
using UnityEngine;

public interface IPooledEnvironmentCallbacks
{
    void OnTakenFromPool(Transform newParent);
    void OnReturnedToPool();
}


public class EnvironmentPoolManager : MonoBehaviour
{
    public static EnvironmentPoolManager Instance { get; private set; }

    [System.Serializable]
    public class PrewarmEntry
    {
        public GameObject prefab;
        public int initialSize = 0;
    }

    [Header("Optional prewarm")]
    public PrewarmEntry[] prewarmEntries;

    private readonly Dictionary<GameObject, Queue<GameObject>> _poolByPrefab =
        new Dictionary<GameObject, Queue<GameObject>>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (prewarmEntries != null)
        {
            foreach (var e in prewarmEntries)
            {
                if (e.prefab == null || e.initialSize <= 0) continue;

                var q = GetOrCreateQueue(e.prefab);

                for (int i = 0; i < e.initialSize; i++)
                {
                    var go = Instantiate(e.prefab, transform);
                    go.SetActive(false);

                    var meta = go.GetComponent<PooledEnvironmentInstance>();
                    if (meta == null) meta = go.AddComponent<PooledEnvironmentInstance>();
                    meta.prefab = e.prefab;

                    q.Enqueue(go);
                }
            }
        }
    }

    private Queue<GameObject> GetOrCreateQueue(GameObject prefab)
    {
        if (!_poolByPrefab.TryGetValue(prefab, out var q))
        {
            q = new Queue<GameObject>();
            _poolByPrefab[prefab] = q;
        }
        return q;
    }

    private void EnsureMeta(GameObject instance, GameObject prefab)
    {
        var meta = instance.GetComponent<PooledEnvironmentInstance>();
        if (meta == null) meta = instance.AddComponent<PooledEnvironmentInstance>();
        meta.prefab = prefab;
    }

    private void NotifyTakenFromPool(GameObject instance, Transform newParent)
    {
        var behaviours = instance.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IPooledEnvironmentCallbacks cb)
                cb.OnTakenFromPool(newParent);
        }
    }

    private void NotifyReturnedToPool(GameObject instance)
    {
        var behaviours = instance.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IPooledEnvironmentCallbacks cb)
                cb.OnReturnedToPool();
        }
    }

    public GameObject Get(GameObject prefab, Transform parent, Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return null;

        var queue = GetOrCreateQueue(prefab);
        GameObject go;

        if (queue.Count > 0)
        {
            go = queue.Dequeue();
        }
        else
        {
            go = Instantiate(prefab, parent, false);
            EnsureMeta(go, prefab);
        }

        EnsureMeta(go, prefab);

        go.transform.SetParent(parent, worldPositionStays: false);
        go.transform.position = position;
        go.transform.rotation = rotation;

        // Rebind/reset before enabling
        NotifyTakenFromPool(go, parent);

        go.SetActive(true);
        return go;
    }

    public void Release(GameObject instance)
    {
        if (instance == null) return;

        var meta = instance.GetComponent<PooledEnvironmentInstance>();
        if (meta == null || meta.prefab == null)
        {
            Destroy(instance);
            return;
        }

        var queue = GetOrCreateQueue(meta.prefab);

        // Clear runtime state before storage
        NotifyReturnedToPool(instance);

        instance.SetActive(false);
        instance.transform.SetParent(transform, worldPositionStays: false);
        queue.Enqueue(instance);
    }
}

public class PooledEnvironmentInstance : MonoBehaviour
{
    [HideInInspector] public GameObject prefab;
}