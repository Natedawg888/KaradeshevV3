using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight pooled GameObject provider for tornado visuals.
/// Designed to match the API shape already used by TornadoVisualSystem.
/// </summary>
public class TornadoVisualPool : MonoBehaviour
{
    [Header("Pool Settings")]
    [SerializeField, Min(0), Tooltip("Max inactive instances kept per prefab. 0 = unlimited.")]
    private int maxPooledPerPrefab = 0;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = false;

    private sealed class PoolBucket
    {
        public readonly Stack<GameObject> available = new Stack<GameObject>(8);
        public readonly HashSet<GameObject> all = new HashSet<GameObject>();
    }

    private readonly Dictionary<GameObject, PoolBucket> _buckets = new Dictionary<GameObject, PoolBucket>(8);
    private readonly Dictionary<GameObject, GameObject> _instanceToPrefab = new Dictionary<GameObject, GameObject>(32);

    private Transform _inactiveRoot;

    private void Awake()
    {
        EnsureInactiveRoot();
    }

    private void OnDestroy()
    {
        _buckets.Clear();
        _instanceToPrefab.Clear();
    }

    public void Prewarm(GameObject prefab, int targetCount, Transform parent, int maxCreatesPerPrewarmCall = 24)
    {
        if (prefab == null || targetCount <= 0)
            return;

        PoolBucket bucket = GetOrCreateBucket(prefab);
        int clampedCreates = Mathf.Max(1, maxCreatesPerPrewarmCall);
        int missing = Mathf.Max(0, targetCount - bucket.all.Count);
        int createNow = Mathf.Min(missing, clampedCreates);

        for (int i = 0; i < createNow; i++)
        {
            GameObject instance = CreateInstance(prefab, parent);
            Return(prefab, instance, stopPooledEffects: true);
        }

        if (debugLogging && createNow > 0) {}
            //Debug.Log($"[TornadoVisualPool] Prewarmed {createNow} for prefab '{prefab.name}'.");
    }

    public GameObject Get(
        GameObject prefab,
        Transform parent,
        Vector3 position,
        Quaternion rotation,
        bool resetPooledEffects = true)
    {
        if (prefab == null)
            return null;

        EnsureInactiveRoot();

        PoolBucket bucket = GetOrCreateBucket(prefab);
        GameObject instance = null;

        while (bucket.available.Count > 0 && instance == null)
            instance = bucket.available.Pop();

        if (instance == null)
            instance = CreateInstance(prefab, parent);

        Transform tr = instance.transform;
        tr.SetParent(parent, false);
        tr.SetPositionAndRotation(position, rotation);
        tr.localScale = Vector3.one;

        instance.SetActive(true);

        if (resetPooledEffects)
            ResetPooledEffects(instance);

        return instance;
    }

    public void Return(GameObject prefab, GameObject instance, bool stopPooledEffects = true)
    {
        if (instance == null)
            return;

        EnsureInactiveRoot();

        GameObject resolvedPrefab = ResolvePrefab(prefab, instance);
        if (resolvedPrefab == null)
        {
            if (debugLogging) {}
                //Debug.LogWarning($"[TornadoVisualPool] Could not resolve prefab for returned instance '{instance.name}'. Destroying.");
            Destroy(instance);
            return;
        }

        PoolBucket bucket = GetOrCreateBucket(resolvedPrefab);

        if (maxPooledPerPrefab > 0 && bucket.available.Count >= maxPooledPerPrefab)
        {
            _instanceToPrefab.Remove(instance);
            bucket.all.Remove(instance);
            Destroy(instance);
            return;
        }

        if (stopPooledEffects)
            StopPooledEffects(instance);

        instance.SetActive(false);
        instance.transform.SetParent(_inactiveRoot, false);

        if (!bucket.available.Contains(instance))
            bucket.available.Push(instance);
    }

    public void ClearPool(GameObject prefab, bool destroyActiveInstancesToo = false)
    {
        if (prefab == null)
            return;

        if (!_buckets.TryGetValue(prefab, out PoolBucket bucket))
            return;

        while (bucket.available.Count > 0)
        {
            GameObject instance = bucket.available.Pop();
            if (instance != null)
            {
                _instanceToPrefab.Remove(instance);
                Destroy(instance);
            }
        }

        if (destroyActiveInstancesToo)
        {
            List<GameObject> toDestroy = new List<GameObject>(bucket.all.Count);

            foreach (GameObject instance in bucket.all)
            {
                if (instance != null)
                    toDestroy.Add(instance);
            }

            for (int i = 0; i < toDestroy.Count; i++)
            {
                GameObject instance = toDestroy[i];
                _instanceToPrefab.Remove(instance);
                Destroy(instance);
            }
        }

        bucket.all.Clear();
        _buckets.Remove(prefab);
    }

    public void ClearAllPools(bool destroyActiveInstancesToo = false)
    {
        List<GameObject> prefabs = new List<GameObject>(_buckets.Keys);
        for (int i = 0; i < prefabs.Count; i++)
            ClearPool(prefabs[i], destroyActiveInstancesToo);
    }

    private PoolBucket GetOrCreateBucket(GameObject prefab)
    {
        if (!_buckets.TryGetValue(prefab, out PoolBucket bucket))
        {
            bucket = new PoolBucket();
            _buckets[prefab] = bucket;
        }

        return bucket;
    }

    private GameObject CreateInstance(GameObject prefab, Transform parent)
    {
        EnsureInactiveRoot();

        Transform spawnParent = parent != null ? parent : _inactiveRoot;
        GameObject instance = Instantiate(prefab, spawnParent);
        instance.name = $"{prefab.name}_Pooled";

        PoolBucket bucket = GetOrCreateBucket(prefab);
        bucket.all.Add(instance);
        _instanceToPrefab[instance] = prefab;

        return instance;
    }

    private GameObject ResolvePrefab(GameObject prefab, GameObject instance)
    {
        if (prefab != null)
            return prefab;

        if (instance != null && _instanceToPrefab.TryGetValue(instance, out GameObject resolvedPrefab))
            return resolvedPrefab;

        return null;
    }

    private void EnsureInactiveRoot()
    {
        if (_inactiveRoot != null)
            return;

        GameObject go = new GameObject("Inactive");
        go.transform.SetParent(transform, false);
        go.SetActive(false);
        _inactiveRoot = go.transform;
    }

    private static void ResetPooledEffects(GameObject instance)
    {
        if (instance == null)
            return;

        ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem ps = particleSystems[i];
            if (ps == null)
                continue;

            ps.Clear(true);
            ps.Play(true);
        }

        TrailRenderer[] trailRenderers = instance.GetComponentsInChildren<TrailRenderer>(true);
        for (int i = 0; i < trailRenderers.Length; i++)
        {
            if (trailRenderers[i] != null)
                trailRenderers[i].Clear();
        }

        Animator[] animators = instance.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (animator == null)
                continue;

            animator.Rebind();
            animator.Update(0f);
        }
    }

    private static void StopPooledEffects(GameObject instance)
    {
        if (instance == null)
            return;

        ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem ps = particleSystems[i];
            if (ps == null)
                continue;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Clear(true);
        }

        TrailRenderer[] trailRenderers = instance.GetComponentsInChildren<TrailRenderer>(true);
        for (int i = 0; i < trailRenderers.Length; i++)
        {
            if (trailRenderers[i] != null)
                trailRenderers[i].Clear();
        }
    }
}
