using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class LightningVisualSystem : MonoBehaviour
{
    public static LightningVisualSystem Instance { get; private set; }

    [Serializable]
    private struct ActiveLightningVisual
    {
        public GameObject prefab;
        public GameObject instance;
        public float returnTime;
    }

    [Header("References")]
    [SerializeField] private LightningSimulationSystem lightningSimulationSystem;
    [SerializeField] private CloudSimulationSystem cloudSimulationSystem;
    [SerializeField] private Transform lightningVisualRoot;
    [SerializeField] private LightningVisualPool lightningPool;

    [Header("Lightning Prefabs")]
    [SerializeField] private GameObject[] lightningStrikePrefabs;

    [Header("Lifecycle")]
    [SerializeField] private bool initializeOnStart = true;
    [SerializeField] private bool subscribeOnEnable = true;

    [Header("Visual Settings")]
    [Tooltip("Extra vertical offset added after locking to the cloud position.")]
    [SerializeField] private float lightningVisualHeightOffset = 0f;
    [Min(0.01f)][SerializeField] private float lightningVisualLifetimeSeconds = 0.40f;
    [Range(0f, 360f)][SerializeField] private float lightningRandomYRotationMax = 360f;
    [SerializeField] private bool useResolvedStrikeCellForCloudAnchor = true;
    [SerializeField] private bool fallbackToStrikeWorldPositionIfNoCloud = true;
    [SerializeField] private bool restartParticlesOnSpawn = true;
    [SerializeField] private bool restartAnimatorsOnSpawn = true;
    [SerializeField] private bool clearTrailsOnSpawn = true;

    [Header("Pool Warmup")]
    [SerializeField] private bool prewarmPoolsOnInitialize = true;
    [Min(0)][SerializeField] private int prewarmPerPrefabCount = 6;
    [Min(1)][SerializeField] private int maxCreatesPerPrewarmCall = 16;

    [Header("Performance")]
    [Min(1)][SerializeField] private int maxConcurrentLightningVisuals = 12;
    [Min(1)][SerializeField] private int maxVisualSpawnsPerFrame = 4;
    [SerializeField] private bool queueStrikesWhenAtCapacity = true;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = false;

    private LightningSimulationSystem _subscribedLightningSimulationSystem;

    private readonly Queue<LightningStrikePayload> _pendingStrikeVisuals = new Queue<LightningStrikePayload>();
    private readonly List<ActiveLightningVisual> _activeLightningVisuals = new List<ActiveLightningVisual>(16);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsureLinks();
        EnsurePool();
    }

    private void OnEnable()
    {
        EnsureLinks();
        EnsurePool();

        if (subscribeOnEnable)
            RebindLightningEvents();
    }

    private void Start()
    {
        if (!initializeOnStart)
            return;

        EnsureLinks();
        EnsurePool();
        EnsureVisualRoot();

        if (prewarmPoolsOnInitialize)
            PrewarmVisualPools();
    }

    private void Update()
    {
        UpdateActiveLightningVisuals();
        PumpPendingStrikeVisuals();
    }

    private void OnDisable()
    {
        UnbindLightningEvents();
        ClearPendingStrikeVisuals();
        ReturnAllActiveVisuals();
    }

    private void OnDestroy()
    {
        UnbindLightningEvents();

        if (Instance == this)
            Instance = null;
    }

    public void InstallRuntimeRefs(
        LightningSimulationSystem newLightningSimulationSystem,
        CloudSimulationSystem newCloudSimulationSystem = null,
        Transform newLightningVisualRoot = null,
        LightningVisualPool newLightningPool = null,
        bool initializeNow = true)
    {
        if (newLightningSimulationSystem != null)
            lightningSimulationSystem = newLightningSimulationSystem;

        if (newCloudSimulationSystem != null)
            cloudSimulationSystem = newCloudSimulationSystem;

        if (newLightningVisualRoot != null)
            lightningVisualRoot = newLightningVisualRoot;

        if (newLightningPool != null)
            lightningPool = newLightningPool;

        EnsurePool();

        if (subscribeOnEnable && isActiveAndEnabled)
            RebindLightningEvents();

        if (initializeNow)
        {
            EnsureVisualRoot();

            if (prewarmPoolsOnInitialize)
                PrewarmVisualPools();
        }
    }

    private void HandleLightningStrikeScheduled(LightningStrikePayload payload)
    {
        if (!HasAnyUsablePrefabs(lightningStrikePrefabs))
            return;

        if (_activeLightningVisuals.Count < maxConcurrentLightningVisuals)
        {
            if (TrySpawnLightningVisual(payload))
                return;
        }

        if (queueStrikesWhenAtCapacity)
            _pendingStrikeVisuals.Enqueue(payload);
    }

    private void PumpPendingStrikeVisuals()
    {
        if (_pendingStrikeVisuals.Count == 0)
            return;

        if (!HasAnyUsablePrefabs(lightningStrikePrefabs))
        {
            _pendingStrikeVisuals.Clear();
            return;
        }

        int spawnedThisFrame = 0;
        int spawnCap = Mathf.Max(1, maxVisualSpawnsPerFrame);

        while (_pendingStrikeVisuals.Count > 0 &&
               _activeLightningVisuals.Count < maxConcurrentLightningVisuals &&
               spawnedThisFrame < spawnCap)
        {
            LightningStrikePayload payload = _pendingStrikeVisuals.Dequeue();

            if (TrySpawnLightningVisual(payload))
                spawnedThisFrame++;
        }
    }

    private bool TrySpawnLightningVisual(LightningStrikePayload payload)
    {
        EnsureLinks();
        EnsurePool();
        EnsureVisualRoot();

        if (lightningSimulationSystem == null)
            return false;

        GameObject prefab = GetRandomUsablePrefab(lightningStrikePrefabs);
        if (prefab == null)
            return false;

        Vector3 worldPosition;
        if (!TryGetLightningSpawnWorldPosition(payload, out worldPosition))
            return false;

        Quaternion rotation = Quaternion.Euler(
            0f,
            UnityEngine.Random.Range(0f, lightningRandomYRotationMax),
            0f);

        GameObject instance = lightningPool.Get(prefab, lightningVisualRoot, worldPosition, rotation);
        if (instance == null)
            return false;

        instance.name = string.Format(
            "Lightning_{0}_{1}_{2}",
            payload.resolvedCellX,
            payload.resolvedCellY,
            payload.strikeIndexInBurst + 1);

        PrepareVisualInstance(instance);

        _activeLightningVisuals.Add(new ActiveLightningVisual
        {
            prefab = prefab,
            instance = instance,
            returnTime = Time.time + lightningVisualLifetimeSeconds
        });

        if (debugLogging)
        {
            Debug.Log(
                string.Format(
                    "[LightningVisualSystem] Spawned lightning visual locked to cloud at {0},{1} for strike {2}/{3}",
                    payload.resolvedCellX,
                    payload.resolvedCellY,
                    payload.strikeIndexInBurst + 1,
                    payload.totalStrikesInBurst));
        }

        return true;
    }

    private bool TryGetLightningSpawnWorldPosition(LightningStrikePayload payload, out Vector3 worldPosition)
    {
        worldPosition = Vector3.zero;

        int anchorX = useResolvedStrikeCellForCloudAnchor ? payload.resolvedCellX : payload.originCellX;
        int anchorY = useResolvedStrikeCellForCloudAnchor ? payload.resolvedCellY : payload.originCellY;

        if (cloudSimulationSystem != null &&
            cloudSimulationSystem.TryGetCloudWorldPosition(anchorX, anchorY, out worldPosition))
        {
            worldPosition.y += lightningVisualHeightOffset;
            return true;
        }

        if (fallbackToStrikeWorldPositionIfNoCloud &&
            lightningSimulationSystem != null &&
            lightningSimulationSystem.TryGetLightningStrikeWorldPosition(payload, out worldPosition, 0f))
        {
            worldPosition.y += lightningVisualHeightOffset;
            return true;
        }

        return false;
    }

    private void UpdateActiveLightningVisuals()
    {
        if (_activeLightningVisuals.Count == 0)
            return;

        float now = Time.time;

        for (int i = _activeLightningVisuals.Count - 1; i >= 0; i--)
        {
            ActiveLightningVisual active = _activeLightningVisuals[i];

            if (active.instance == null)
            {
                _activeLightningVisuals.RemoveAt(i);
                continue;
            }

            if (now < active.returnTime)
                continue;

            StopVisualInstance(active.instance);
            lightningPool.Return(active.prefab, active.instance);
            _activeLightningVisuals.RemoveAt(i);
        }
    }

    private void ReturnAllActiveVisuals()
    {
        for (int i = _activeLightningVisuals.Count - 1; i >= 0; i--)
        {
            ActiveLightningVisual active = _activeLightningVisuals[i];

            if (active.instance == null)
                continue;

            StopVisualInstance(active.instance);
            lightningPool.Return(active.prefab, active.instance);
        }

        _activeLightningVisuals.Clear();
    }

    private void ClearPendingStrikeVisuals()
    {
        _pendingStrikeVisuals.Clear();
    }

    private void PrepareVisualInstance(GameObject instance)
    {
        if (instance == null)
            return;

        if (clearTrailsOnSpawn)
        {
            TrailRenderer[] trails = instance.GetComponentsInChildren<TrailRenderer>(true);
            for (int i = 0; i < trails.Length; i++)
                trails[i].Clear();
        }

        if (restartAnimatorsOnSpawn)
        {
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

        if (restartParticlesOnSpawn)
        {
            ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem ps = particleSystems[i];
                if (ps == null)
                    continue;

                ps.Clear(true);
                ps.Play(true);
            }
        }
    }

    private void StopVisualInstance(GameObject instance)
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
        }

        TrailRenderer[] trails = instance.GetComponentsInChildren<TrailRenderer>(true);
        for (int i = 0; i < trails.Length; i++)
            trails[i].Clear();
    }

    private void PrewarmVisualPools()
    {
        if (!HasAnyUsablePrefabs(lightningStrikePrefabs))
            return;

        EnsurePool();
        EnsureVisualRoot();

        for (int i = 0; i < lightningStrikePrefabs.Length; i++)
        {
            GameObject prefab = lightningStrikePrefabs[i];
            if (prefab == null)
                continue;

            lightningPool.Prewarm(
                prefab,
                prewarmPerPrefabCount,
                lightningVisualRoot,
                maxCreatesPerPrewarmCall);
        }
    }

    private void EnsureLinks()
    {
        if (lightningSimulationSystem == null)
            lightningSimulationSystem = LightningSimulationSystem.Instance;

        if (cloudSimulationSystem == null)
            cloudSimulationSystem = CloudSimulationSystem.Instance;
    }

    private void EnsurePool()
    {
        if (lightningPool != null)
            return;

        GameObject go = new GameObject("Lightning Visual Pool");
        go.transform.SetParent(transform, false);
        lightningPool = go.AddComponent<LightningVisualPool>();
    }

    private void EnsureVisualRoot()
    {
        if (lightningVisualRoot != null)
            return;

        GameObject root = new GameObject("Lightning Visual Root");
        root.transform.SetParent(transform, false);
        lightningVisualRoot = root.transform;
    }

    private void RebindLightningEvents()
    {
        if (_subscribedLightningSimulationSystem == lightningSimulationSystem)
            return;

        UnbindLightningEvents();

        _subscribedLightningSimulationSystem = lightningSimulationSystem;

        if (_subscribedLightningSimulationSystem != null)
            _subscribedLightningSimulationSystem.OnLightningStrikeScheduled += HandleLightningStrikeScheduled;
    }

    private void UnbindLightningEvents()
    {
        if (_subscribedLightningSimulationSystem == null)
            return;

        _subscribedLightningSimulationSystem.OnLightningStrikeScheduled -= HandleLightningStrikeScheduled;
        _subscribedLightningSimulationSystem = null;
    }

    private bool HasAnyUsablePrefabs(GameObject[] prefabs)
    {
        if (prefabs == null || prefabs.Length == 0)
            return false;

        for (int i = 0; i < prefabs.Length; i++)
        {
            if (prefabs[i] != null)
                return true;
        }

        return false;
    }

    private GameObject GetRandomUsablePrefab(GameObject[] prefabs)
    {
        if (!HasAnyUsablePrefabs(prefabs))
            return null;

        int start = UnityEngine.Random.Range(0, prefabs.Length);

        for (int i = 0; i < prefabs.Length; i++)
        {
            GameObject prefab = prefabs[(start + i) % prefabs.Length];
            if (prefab != null)
                return prefab;
        }

        return null;
    }
}