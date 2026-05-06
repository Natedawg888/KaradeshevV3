using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Particle-aware pool for rain visuals.
/// Unlike the cloud pool, this resets and replays particle systems when reused,
/// and stops/clears them when returned.
/// </summary>
public class RainVisualPool : MonoBehaviour
{
    [System.Serializable]
    private sealed class PoolBucket
    {
        public GameObject prefab;
        public Transform root;
        public readonly Queue<GameObject> inactive = new Queue<GameObject>();
    }

    private readonly Dictionary<GameObject, PoolBucket> _buckets = new();

    public GameObject Get(
        GameObject prefab,
        Transform parent,
        Vector3 position,
        Quaternion rotation,
        bool resetPooledEffects = true)
    {
        if (prefab == null)
            return null;

        PoolBucket bucket = GetOrCreateBucket(prefab, parent);

        GameObject instance = null;
        while (bucket.inactive.Count > 0 && instance == null)
            instance = bucket.inactive.Dequeue();

        if (instance == null)
        {
            instance = Instantiate(prefab, position, rotation, bucket.root);
            instance.name = prefab.name;

            if (resetPooledEffects)
                EnsureCache(instance);
        }
        else
        {
            Transform t = instance.transform;

            if (t.parent != bucket.root)
                t.SetParent(bucket.root, false);

            t.SetPositionAndRotation(position, rotation);
            instance.SetActive(true);
        }

        if (resetPooledEffects)
            ResetPooledInstance(instance);

        return instance;
    }

    public void Return(
        GameObject prefab,
        GameObject instance,
        bool stopPooledEffects = true)
    {
        if (prefab == null || instance == null)
            return;

        PoolBucket bucket = GetOrCreateBucket(prefab, transform);

        if (stopPooledEffects)
            StopPooledInstance(instance);

        Transform t = instance.transform;
        if (t.parent != bucket.root)
            t.SetParent(bucket.root, false);

        instance.SetActive(false);
        bucket.inactive.Enqueue(instance);
    }

    public void Prewarm(GameObject prefab, int count, Transform parent, int maxCreateThisCall = int.MaxValue)
    {
        if (prefab == null || count <= 0 || maxCreateThisCall <= 0)
            return;

        PoolBucket bucket = GetOrCreateBucket(prefab, parent);

        int needed = count - bucket.inactive.Count;
        if (needed <= 0)
            return;

        needed = Mathf.Min(needed, maxCreateThisCall);

        for (int i = 0; i < needed; i++)
        {
            GameObject instance = Instantiate(prefab, bucket.root);
            instance.name = prefab.name;
            instance.SetActive(false);
            bucket.inactive.Enqueue(instance);
        }
    }

    private PoolBucket GetOrCreateBucket(GameObject prefab, Transform parent)
    {
        if (!_buckets.TryGetValue(prefab, out PoolBucket bucket))
        {
            bucket = new PoolBucket
            {
                prefab = prefab,
                root = CreateBucketRoot(prefab.name, transform)
            };

            _buckets[prefab] = bucket;
        }
        else if (bucket.root == null)
        {
            bucket.root = CreateBucketRoot(prefab.name, transform);
        }

        return bucket;
    }

    private Transform CreateBucketRoot(string prefabName, Transform parent)
    {
        GameObject go = new GameObject($"{prefabName}_Pool");
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private RainVisualPoolCache EnsureCache(GameObject instance)
    {
        if (instance == null)
            return null;

        RainVisualPoolCache cache = instance.GetComponent<RainVisualPoolCache>();
        if (cache == null)
            cache = instance.AddComponent<RainVisualPoolCache>();

        if (!cache.IsInitialized)
            cache.Rebuild();

        return cache;
    }

    private void ResetPooledInstance(GameObject instance)
    {
        if (instance == null)
            return;

        RainVisualPoolCache cache = EnsureCache(instance);
        if (cache == null)
            return;

        ParticleSystem[] particleSystems = cache.ParticleSystems;
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem ps = particleSystems[i];
            if (ps == null)
                continue;

            ps.Clear(true);
            ps.Play(true);
        }

        TrailRenderer[] trails = cache.TrailRenderers;
        for (int i = 0; i < trails.Length; i++)
        {
            TrailRenderer tr = trails[i];
            if (tr != null)
                tr.Clear();
        }
    }

    private void StopPooledInstance(GameObject instance)
    {
        if (instance == null)
            return;

        RainVisualPoolCache cache = EnsureCache(instance);
        if (cache == null)
            return;

        ParticleSystem[] particleSystems = cache.ParticleSystems;
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem ps = particleSystems[i];
            if (ps == null)
                continue;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        TrailRenderer[] trails = cache.TrailRenderers;
        for (int i = 0; i < trails.Length; i++)
        {
            TrailRenderer tr = trails[i];
            if (tr != null)
                tr.Clear();
        }
    }
}

[DisallowMultipleComponent]
public sealed class RainVisualPoolCache : MonoBehaviour
{
    [SerializeField] private ParticleSystem[] particleSystems;
    [SerializeField] private TrailRenderer[] trailRenderers;
    [SerializeField] private bool initialized;

    public ParticleSystem[] ParticleSystems => particleSystems ?? System.Array.Empty<ParticleSystem>();
    public TrailRenderer[] TrailRenderers => trailRenderers ?? System.Array.Empty<TrailRenderer>();
    public bool IsInitialized => initialized;

    public void Rebuild()
    {
        particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        trailRenderers = GetComponentsInChildren<TrailRenderer>(true);
        initialized = true;
    }
}