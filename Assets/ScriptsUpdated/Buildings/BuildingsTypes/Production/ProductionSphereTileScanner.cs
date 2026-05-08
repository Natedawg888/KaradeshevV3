using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Sphere-based tile scanner for production buildings.
/// - Creates a child GameObject with a SphereCollider (trigger) so you can visualize range.
/// - Uses Physics.OverlapSphereNonAlloc to gather nearby colliders.
/// - Filters to tiles with TileControl -> only tiles that have EnvironmentControl.
/// - Filters OUT tiles that have a "status/discovery" component AND are undiscovered.
/// - Tracks EnvironmentResourceNodes found on those tiles.
/// </summary>
public class ProductionSphereTileScanner : MonoBehaviour
{
    [Header("Sphere Range")]
    [Tooltip("Base scan radius before applying plan multiplier.")]
    public float baseRadius = 3f;

    [Tooltip("Local Y offset for the sphere center (helps if your tile colliders are slightly above/below).")]
    public float centerYOffset = 0.5f;

    [Tooltip("Which layers contain tile colliders.")]
    public LayerMask tileMask = ~0;

    [Tooltip("Include trigger colliders in the scan. Set true if your tile colliders are triggers.")]
    public bool includeTriggerColliders = true;

    [Header("Scanner Object")]
    public string scannerObjectName = "ProductionRangeScanner";
    public bool parentToStarter = true;

    [Header("Buffers")]
    public int colliderBufferSize = 256;

    [Header("Discovery Filtering")]
    [Tooltip("If true: tiles with no EnvironmentStatus are treated as discovered (allowed).")]
    public bool allowTilesWithoutStatus = true;

    [Tooltip("Where to look for EnvironmentStatus.")]
    public bool searchStatusOnTileAlso = true;

    [Header("Optional Node Tracking")]
    public bool trackNodes = true;

    // --- tracked results ---
    [SerializeField] private List<TileControl> trackedTiles = new();
    [SerializeField] private List<EnvironmentControl> trackedEnvironmentTiles = new();
    [SerializeField] private List<EnvironmentResourceNode> trackedNodes = new();
    [SerializeField] private List<Vector3> trackedPositions = new();

    public IReadOnlyList<TileControl> TrackedTiles => trackedTiles;
    public IReadOnlyList<EnvironmentControl> TrackedEnvironmentTiles => trackedEnvironmentTiles;
    public IReadOnlyList<EnvironmentResourceNode> TrackedNodes => trackedNodes;
    public IReadOnlyList<Vector3> TrackedPositions => trackedPositions;

    // --- internals ---
    private GameObject _scannerGO;
    private SphereCollider _sphere;
    private Collider[] _buffer;

    private readonly HashSet<TileControl> _tileSet = new();
    private readonly HashSet<EnvironmentControl> _envSet = new();
    private readonly HashSet<EnvironmentResourceNode> _nodeSet = new();

#if UNITY_2020_2_OR_NEWER
    private readonly List<EnvironmentResourceNode> _tmpNodes = new(32);
#endif

    private void Reset()
    {
        EnsureScannerObject(gameObject);
        EnsureBuffer();
    }

    private void Awake()
    {
        EnsureScannerObject(gameObject);
        EnsureBuffer();
    }

    private void EnsureBuffer()
    {
        int size = Mathf.Max(8, colliderBufferSize);
        if (_buffer == null || _buffer.Length != size)
            _buffer = new Collider[size];
    }

    private void EnsureScannerObject(GameObject starter)
    {
        if (_scannerGO != null && _sphere != null)
            return;

        Transform existing = transform.Find(scannerObjectName);
        if (existing != null)
        {
            _scannerGO = existing.gameObject;
            _sphere = _scannerGO.GetComponent<SphereCollider>();
        }

        if (_scannerGO == null)
        {
            _scannerGO = new GameObject(scannerObjectName);
            _scannerGO.transform.SetParent(transform, false);
            _scannerGO.transform.localPosition = Vector3.zero;
            _scannerGO.transform.localRotation = Quaternion.identity;
            _scannerGO.transform.localScale = Vector3.one;
        }

        if (_sphere == null)
        {
            _sphere = _scannerGO.GetComponent<SphereCollider>();
            if (_sphere == null) _sphere = _scannerGO.AddComponent<SphereCollider>();
        }

        _sphere.isTrigger = true; // visual / intent
        _sphere.radius = Mathf.Max(0.01f, baseRadius);
        _sphere.center = new Vector3(0f, centerYOffset, 0f);

        if (starter != null && parentToStarter)
        {
            _scannerGO.transform.SetParent(starter.transform, false);
            _scannerGO.transform.localPosition = Vector3.zero;
        }
    }

    public IEnumerable<ResourceSpawnEntry> EnumerateAllResources()
    {
        // Enumerate all spawned resource entries across all tracked nodes.
        for (int i = 0; i < trackedNodes.Count; i++)
        {
            var node = trackedNodes[i];
            if (node == null) continue;

            var list = node.SpawnedResources;
            if (list == null) continue;

            for (int j = 0; j < list.Count; j++)
            {
                var entry = list[j];
                if (entry != null)
                    yield return entry;
            }
        }
    }

    public void RefreshFromStarter(GameObject starter, float rangeMultiplier = 1f)
    {
        EnsureScannerObject(starter != null ? starter : gameObject);
        EnsureBuffer();

        if (starter != null)
        {
            if (parentToStarter)
                _scannerGO.transform.SetParent(starter.transform, false);

            _scannerGO.transform.position = starter.transform.position;
        }

        float radius = Mathf.Max(0.01f, baseRadius * Mathf.Max(0.01f, rangeMultiplier));
        _sphere.radius = radius;
        _sphere.center = new Vector3(0f, centerYOffset, 0f);

        Vector3 centerWorld = _scannerGO.transform.TransformPoint(_sphere.center);

        // clear
        trackedTiles.Clear();
        trackedEnvironmentTiles.Clear();
        trackedNodes.Clear();
        trackedPositions.Clear();
        _tileSet.Clear();
        _envSet.Clear();
        _nodeSet.Clear();

        var qti = includeTriggerColliders ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;
        int hitCount = Physics.OverlapSphereNonAlloc(centerWorld, radius, _buffer, tileMask, qti);

        if (hitCount >= _buffer.Length)
        {
            //Debug.LogWarning(
                //$"[ProductionSphereTileScanner] Collider buffer FULL ({_buffer.Length}). Increase colliderBufferSize.");
        }

        for (int i = 0; i < hitCount; i++)
        {
            var col = _buffer[i];
            if (col == null) continue;

            var tile = col.GetComponentInParent<TileControl>();
            if (tile == null) continue;

            // Ensure its cached content refs are up to date (safe/cheap)
            tile.RefreshContentType();

            if (!_tileSet.Add(tile))
                continue;

            // Filter OUT building tiles (only environment)
            if (tile.tileContentType != TileContentType.Environment)
                continue;

            // Get env from TileControl (preferred)
            var env = tile.EnvironmentControl != null
                ? tile.EnvironmentControl
                : tile.GetComponentInChildren<EnvironmentControl>(true);

            if (env == null)
                continue;

            // Discovery filter: if there is an EnvironmentStatus AND not discovered -> skip
            if (IsUndiscovered(env, tile))
                continue;

            if (!_envSet.Add(env))
                continue;

            trackedTiles.Add(tile);
            trackedEnvironmentTiles.Add(env);
            trackedPositions.Add(tile.transform.position);

            if (trackNodes)
            {
#if UNITY_2020_2_OR_NEWER
                _tmpNodes.Clear();
                env.GetComponentsInChildren(true, _tmpNodes);
                for (int n = 0; n < _tmpNodes.Count; n++)
                {
                    var node = _tmpNodes[n];
                    if (node != null && _nodeSet.Add(node))
                        trackedNodes.Add(node);
                }
#else
                var nodes = env.GetComponentsInChildren<EnvironmentResourceNode>(true);
                for (int n = 0; n < nodes.Length; n++)
                {
                    var node = nodes[n];
                    if (node != null && _nodeSet.Add(node))
                        trackedNodes.Add(node);
                }
#endif
            }
        }
    }

    private bool IsUndiscovered(EnvironmentControl env, TileControl tile)
    {
        EnvironmentStatus status = null;

        // Prefer status on environment
        if (env != null)
            status = env.GetComponentInChildren<EnvironmentStatus>(true);

        // Optionally also check tile root/children
        if (status == null && searchStatusOnTileAlso && tile != null)
            status = tile.GetComponentInChildren<EnvironmentStatus>(true);

        if (status == null)
            return !allowTilesWithoutStatus; // if no status, usually allow

        return !status.IsDiscovered;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (_scannerGO == null || _sphere == null) return;
        Gizmos.DrawWireSphere(_scannerGO.transform.TransformPoint(_sphere.center), _sphere.radius);
    }
#endif
}
