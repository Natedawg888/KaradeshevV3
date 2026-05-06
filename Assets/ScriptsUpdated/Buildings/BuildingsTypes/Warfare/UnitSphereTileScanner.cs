using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared sphere-based tile scanner for units.
/// One scanner can be reused for every unit/group by moving it to the acting unit's world position.
/// </summary>
public class UnitSphereTileScanner : MonoBehaviour
{
    [Header("Sphere Settings")]
    [Tooltip("Local Y offset for the sphere center.")]
    public float centerYOffset = 0.5f;

    [Tooltip("Which layers contain tile colliders.")]
    public LayerMask tileMask = ~0;

    [Tooltip("Include trigger colliders in the scan. Set true if your tile colliders are triggers.")]
    public bool includeTriggerColliders = true;

    [Header("Range Conversion")]
    [Tooltip("Extra padding added after converting tile range to world radius.")]
    [Range(0f, 1f)]
    public float rangePaddingMultiplier = 0.10f;

    [Tooltip("Optional extra world-space radius added after tile-range conversion.")]
    public float additionalRadius = 0f;

    [Tooltip("Minimum radius used when scanning only the origin tile.")]
    public float minimumRadius = 0.25f;

    [Header("Buffers")]
    public int colliderBufferSize = 256;

    [Header("Tracked Results")]
    [SerializeField] private List<TileControl> trackedTiles = new();

    public IReadOnlyList<TileControl> TrackedTiles => trackedTiles;

    private Collider[] _buffer;
    private readonly HashSet<TileControl> _tileSet = new();

    // Cache collider -> tile so we don't keep calling GetComponentInParent on every scan.
    private readonly Dictionary<int, TileControl> _colliderTileCache = new();

    // Cache tile -> world step so we don't keep re-reading collider/render bounds every scan.
    private readonly Dictionary<int, float> _tileStepCache = new();

    // Gizmo/debug only
    private Vector3 _lastCenterWorld;
    private float _lastRadius;

    private void Reset()
    {
        EnsureBuffer();
    }

    private void Awake()
    {
        EnsureBuffer();
    }

    private void EnsureBuffer()
    {
        int size = Mathf.Max(8, colliderBufferSize);
        if (_buffer == null || _buffer.Length != size)
            _buffer = new Collider[size];
    }

    public void RefreshFromTile(TileControl originTile, int tileRange, bool includeOrigin)
    {
        if (originTile == null)
        {
            ClearTracked();
            return;
        }

        RefreshAtWorldPosition(originTile, originTile.transform.position, tileRange, includeOrigin);
    }

    public void RefreshAtWorldPosition(TileControl originTile, Vector3 worldPosition, int tileRange, bool includeOrigin)
    {
        EnsureBuffer();
        ClearTracked();

        if (originTile == null)
            return;

        float radius = ComputeRadiusFromTileRange(originTile, tileRange);
        Vector3 centerWorld = worldPosition + new Vector3(0f, centerYOffset, 0f);

        _lastCenterWorld = centerWorld;
        _lastRadius = radius;

        QueryTriggerInteraction qti = includeTriggerColliders
            ? QueryTriggerInteraction.Collide
            : QueryTriggerInteraction.Ignore;

        int hitCount = Physics.OverlapSphereNonAlloc(centerWorld, radius, _buffer, tileMask, qti);

        if (hitCount >= _buffer.Length)
        {
            Debug.LogWarning($"[UnitSphereTileScanner] Collider buffer FULL ({_buffer.Length}). Increase colliderBufferSize.");
        }

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = _buffer[i];
            if (col == null)
                continue;

            TileControl tile = ResolveTileFromCollider(col);
            if (tile == null)
                continue;

            if (!includeOrigin && tile == originTile)
                continue;

            if (!_tileSet.Add(tile))
                continue;

            trackedTiles.Add(tile);
        }

        if (includeOrigin && !_tileSet.Contains(originTile))
        {
            _tileSet.Add(originTile);
            trackedTiles.Add(originTile);
        }

        // Clear stale references from buffer slots so old colliders don't hang around.
        for (int i = hitCount; i < _buffer.Length; i++)
            _buffer[i] = null;
    }

    private TileControl ResolveTileFromCollider(Collider col)
    {
        if (col == null)
            return null;

        int id = col.GetInstanceID();

        if (_colliderTileCache.TryGetValue(id, out var cached))
        {
            if (cached != null)
                return cached;

            _colliderTileCache.Remove(id);
        }

        var tile = col.GetComponentInParent<TileControl>();
        if (tile != null)
            _colliderTileCache[id] = tile;

        return tile;
    }

    private void ClearTracked()
    {
        trackedTiles.Clear();
        _tileSet.Clear();
    }

    private float ComputeRadiusFromTileRange(TileControl originTile, int tileRange)
    {
        tileRange = Mathf.Max(0, tileRange);

        float tileStep = GetTileWorldStepCached(originTile);

        if (tileRange <= 0)
            return Mathf.Max(minimumRadius, (tileStep * 0.25f) + additionalRadius);

        float radius =
            (tileStep * tileRange) +
            (tileStep * rangePaddingMultiplier) +
            additionalRadius;

        return Mathf.Max(minimumRadius, radius);
    }

    private float GetTileWorldStepCached(TileControl tile)
    {
        if (tile == null)
            return 1f;

        int id = tile.GetInstanceID();

        if (_tileStepCache.TryGetValue(id, out float cached) && cached > 0f)
            return cached;

        float step = 1f;

        BoxCollider box = tile.GetComponentInChildren<BoxCollider>();
        if (box != null)
        {
            Vector3 size = box.bounds.size;
            step = Mathf.Max(size.x, size.z);
        }
        else
        {
            Renderer rend = tile.GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                Vector3 size = rend.bounds.size;
                step = Mathf.Max(size.x, size.z);
            }
        }

        step = Mathf.Max(0.01f, step);
        _tileStepCache[id] = step;
        return step;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (_lastRadius <= 0f) return;
        Gizmos.DrawWireSphere(_lastCenterWorld, _lastRadius);
    }
#endif
}