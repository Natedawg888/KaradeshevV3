using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;

public partial class UnitGroupActionManager : MonoBehaviour
{
    public static UnitGroupActionManager Instance { get; private set; }

    [Header("Tile Detection")]
    [Tooltip("Physics layer mask for tile colliders (set this to your TileClickable layer).")]
    [SerializeField] private LayerMask tileLayerMask = 0;

    [Header("Shared Tracking Sphere")]
    [Tooltip("Single shared sphere scanner reused by all unit groups.")]
    [SerializeField] private UnitSphereTileScanner sharedTrackingScanner;

    [Header("Unit Control Cache")]
    [Tooltip("Build a tile -> TileUnitGroupControl cache on Awake.")]
    [SerializeField] private bool warmUnitControlCacheOnAwake = true;

    // Reused per turn (no allocations)
    private readonly List<PlayerUnitManager.GroupInfo> _groupInfoBuffer = new(128);

    // Scout UI we actually showed (so we can clear without FindObjectsOfType)
    private readonly List<TileMovementUI> _activeScoutButtonUIs = new(256);

    // Cached tile -> unit control lookup
    private readonly Dictionary<TileControl, TileUnitGroupControl> _unitControlByTile = new(256);

    // Reverse lookup
    private readonly Dictionary<TileUnitGroupControl, TileControl> _tileByUnitControl = new(256);

    public static event Action<TileUnitGroupData> TrackingResultsReady;
    public static void RaiseTrackingResultsReady(TileUnitGroupData group) => TrackingResultsReady?.Invoke(group);

    public static event Action<TileUnitGroupData> GroupActionStateChanged;
    public static void RaiseGroupActionStateChanged(TileUnitGroupData group) => GroupActionStateChanged?.Invoke(group);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (tileLayerMask == 0)
            tileLayerMask = LayerMask.GetMask("TileClickable");

        EnsureSharedScanner();

        if (warmUnitControlCacheOnAwake)
            RebuildUnitControlCache();
    }

    private void EnsureSharedScanner()
    {
        if (sharedTrackingScanner == null)
            sharedTrackingScanner = GetComponent<UnitSphereTileScanner>();

        if (sharedTrackingScanner == null)
            sharedTrackingScanner = gameObject.AddComponent<UnitSphereTileScanner>();

        sharedTrackingScanner.tileMask = tileLayerMask;
    }

    // ---------------------------------------------------------------------
    //  TILE / UNIT CONTROL RESOLUTION
    // ---------------------------------------------------------------------

    public TileControl ResolveTileForUnitControl(TileUnitGroupControl unitCtrl)
    {
        if (unitCtrl == null)
            return null;

        // 1) Same object
        var tile = unitCtrl.GetComponent<TileControl>();
        if (tile != null)
            return tile;

        // 2) Parent chain
        tile = unitCtrl.GetComponentInParent<TileControl>(true);
        if (tile != null)
            return tile;

        // 3) Children
        tile = unitCtrl.GetComponentInChildren<TileControl>(true);
        if (tile != null)
            return tile;

        // 4) Sibling/nearby under the same parent root
        if (unitCtrl.transform.parent != null)
        {
            tile = unitCtrl.transform.parent.GetComponentInChildren<TileControl>(true);
            if (tile != null)
                return tile;
        }

        return null;
    }

    // ---------------------------------------------------------------------
    //  UNIT CONTROL CACHE
    // ---------------------------------------------------------------------

    [ContextMenu("Rebuild Unit Control Cache")]
    public void RebuildUnitControlCache()
    {
        _unitControlByTile.Clear();
        _tileByUnitControl.Clear();

#if UNITY_2020_1_OR_NEWER
        var controls = UnityEngine.Object.FindObjectsOfType<TileUnitGroupControl>(true);
#else
        var controls = UnityEngine.Object.FindObjectsOfType<TileUnitGroupControl>();
#endif

        for (int i = 0; i < controls.Length; i++)
        {
            var ctrl = controls[i];
            if (ctrl == null)
                continue;

            RegisterUnitControl(ctrl);
        }
    }

    public void RegisterUnitControl(TileUnitGroupControl unitCtrl, TileControl tile = null)
    {
        if (unitCtrl == null)
            return;

        if (tile == null)
            tile = ResolveTileForUnitControl(unitCtrl);

        if (tile == null)
            return;

        UnregisterUnitControl(unitCtrl);

        _unitControlByTile[tile] = unitCtrl;
        _tileByUnitControl[unitCtrl] = tile;
    }

    public void UnregisterUnitControl(TileUnitGroupControl unitCtrl)
    {
        if (unitCtrl == null)
            return;

        if (_tileByUnitControl.TryGetValue(unitCtrl, out var tile))
        {
            if (tile != null &&
                _unitControlByTile.TryGetValue(tile, out var mapped) &&
                mapped == unitCtrl)
            {
                _unitControlByTile.Remove(tile);
            }

            _tileByUnitControl.Remove(unitCtrl);
        }
    }

    public void RefreshRegisteredUnitControl(TileUnitGroupControl unitCtrl)
    {
        if (unitCtrl == null)
            return;

        RegisterUnitControl(unitCtrl);
    }

    public bool TryGetUnitControlForTile(TileControl tile, out TileUnitGroupControl unitCtrl)
    {
        unitCtrl = null;

        if (tile == null)
            return false;

        if (_unitControlByTile.TryGetValue(tile, out unitCtrl))
        {
            if (unitCtrl != null && IsUnitControlStillOnTile(unitCtrl, tile))
                return true;

            _unitControlByTile.Remove(tile);
            if (unitCtrl != null)
                _tileByUnitControl.Remove(unitCtrl);

            unitCtrl = null;
        }

        // Fallback only if cache is missing or stale.
        unitCtrl = tile.GetComponentInChildren<TileUnitGroupControl>(true);
        if (unitCtrl == null && tile.transform.parent != null)
            unitCtrl = tile.transform.parent.GetComponentInChildren<TileUnitGroupControl>(true);

        if (unitCtrl == null)
            return false;

        RegisterUnitControl(unitCtrl, tile);
        return true;
    }

    private bool IsUnitControlStillOnTile(TileUnitGroupControl unitCtrl, TileControl tile)
    {
        if (unitCtrl == null || tile == null)
            return false;

        var resolvedTile = ResolveTileForUnitControl(unitCtrl);
        return resolvedTile == tile;
    }

    // ---------------------------------------------------------------------
    //  PER-TURN PROCESSING
    // ---------------------------------------------------------------------

    public void ProcessActionsForAllGroupsBatched()
    {
        var pum = PlayerUnitManager.Instance;
        if (pum == null) return;

        pum.GetAllGroups(_groupInfoBuffer);
        if (_groupInfoBuffer.Count == 0) return;

        for (int i = 0; i < _groupInfoBuffer.Count; i++)
        {
            var info = _groupInfoBuffer[i];
            var g = info.data;
            var owner = info.owner;

            if (g == null || owner == null)
                continue;

            if (g.activeAction != null && g.remainingActionTurns > 0)
                ProcessActionForTurn(g, owner);
        }
    }

    private void ProcessActionForTurn(TileUnitGroupData group, TileUnitGroupControl owner)
    {
        if (group == null || owner == null || group.activeAction == null)
            return;

        TileControl targetTile = group.activeActionTargetTile;
        if (targetTile == null)
        {
            targetTile = FindTileByGridPosition_SLOW(group.activeActionTargetGrid);
            group.activeActionTargetTile = targetTile;
        }

        if (group.activeAction is IPerTurnUnitAction perTurn)
        {
            if (targetTile == null)
            {
                ClearActiveAction(group);
                owner.RefreshMarker(group);
                RaiseGroupActionStateChanged(group);
                return;
            }

            bool endNow = perTurn.Tick(group, owner, targetTile);

            group.remainingActionTurns = Mathf.Max(0, group.remainingActionTurns - 1);

            if (endNow || group.remainingActionTurns <= 0)
                ClearActiveAction(group);

            owner.RefreshMarker(group);
            RaiseGroupActionStateChanged(group);
            return;
        }

        group.remainingActionTurns = Mathf.Max(0, group.remainingActionTurns - 1);

        if (group.remainingActionTurns > 0)
        {
            owner.RefreshMarker(group);
            RaiseGroupActionStateChanged(group);
            return;
        }

        if (targetTile != null)
            group.activeAction.Resolve(group, owner, targetTile);

        ClearActiveAction(group);
        owner.RefreshMarker(group);
        RaiseGroupActionStateChanged(group);
    }

    private void ClearActiveAction(TileUnitGroupData group)
    {
        if (group == null)
            return;

        ClearTrackedMeleeTargetMarker(group);
        ClearTrackedSurroundTargetMarker(group);

        group.activeAction = null;
        group.activeActionTargetGrid = Vector2Int.zero;
        group.activeActionTargetTile = null;
        group.remainingActionTurns = 0;

        group.ClearCombatActionState();
    }

    // ---------------------------------------------------------------------
    //  SHARED PHYSICS SPHERE TILE COLLECTION
    // ---------------------------------------------------------------------

    public void CollectTilesInRangeBFS(TileControl origin, int maxRange, List<TileControl> outTiles, bool includeOrigin)
    {
        Vector3 scanPosition = origin != null ? origin.transform.position : Vector3.zero;
        CollectTilesInRangeSphere(origin, scanPosition, maxRange, outTiles, includeOrigin);
    }

    public void CollectTilesInRangeSphere(TileControl origin, Vector3 worldPosition, int maxRange, List<TileControl> outTiles, bool includeOrigin)
    {
        if (outTiles == null) return;
        outTiles.Clear();

        if (origin == null) return;

        EnsureSharedScanner();
        sharedTrackingScanner.tileMask = tileLayerMask;
        sharedTrackingScanner.RefreshAtWorldPosition(origin, worldPosition, maxRange, outTiles != null && includeOrigin);

        var tracked = sharedTrackingScanner.TrackedTiles;
        if (tracked == null || tracked.Count == 0)
            return;

        for (int i = 0; i < tracked.Count; i++)
        {
            var t = tracked[i];
            if (t != null)
                outTiles.Add(t);
        }
    }

    public TileControl FindTileByGridPosition_SLOW(Vector2Int gridPos)
    {
        var allTiles = GameObject.FindObjectsOfType<TileControl>();
        for (int i = 0; i < allTiles.Length; i++)
        {
            var t = allTiles[i];
            if (t == null) continue;
            if (t.GetGridPosition() == gridPos)
                return t;
        }
        return null;
    }
}