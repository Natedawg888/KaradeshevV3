using System;
using System.Collections.Generic;
using UnityEngine;

public class TileUnitGroupControl : MonoBehaviour
{
    [Header("UI")]
    public TileUnitUI tileUI;
    public UnitGroupMarker groupMarkerPrefab;

    private readonly List<TileUnitGroupData> _groups = new();
    private readonly Dictionary<string, UnitGroupMarker> _markers = new();
    private readonly Dictionary<string, TileTrackingMarkerUI> _trackingMarkerUIs = new();

    public IReadOnlyList<TileUnitGroupData> Groups => _groups;

    public List<ScoutResultEntry> lastScoutResults = new();
    public bool hasPendingScoutResults;

    [Header("UnitsOnly: Hide unit canvases when tile has no units")]
    [SerializeField] private bool hideUnitCanvasesWhenEmpty = true;

    [SerializeField]
    private string[] unitCanvasRootNames =
    {
        "UnitCanvas",
        "UnitTileActions",
        "UnitWorldCanvas"
    };

    [SerializeField] private bool allowStartsWithMatch = true;

    private Canvas[] _unitWorldCanvases;

    private struct CanvasState
    {
        public GameObject go;
        public bool activeSelf;
        public bool canvasEnabled;
    }

    private readonly Dictionary<int, CanvasState> _emptySuppressedCache = new();
    private bool _suppressedByEmpty = false;

    public static readonly HashSet<TileUnitGroupControl> NonEmptyControls = new();

    public bool HasAnyGroups => _groups.Count > 0;

    public static event Action OnAnyUnitGroupChanged;

    private void Awake()
    {
        if (tileUI == null)
            tileUI = GetComponentInChildren<TileUnitUI>(true);

        CacheUnitWorldCanvases();
    }

    private void OnEnable()
    {
        WorldCanvasMode.OnChanged += OnWorldCanvasModeChanged;
        OnWorldCanvasModeChanged(WorldCanvasMode.UnitsOnly);

        UnitGroupActionManager.Instance?.RegisterUnitControl(this);
        PlayerTrackingManager.TrackingChanged += OnTrackingChanged;

        RefreshSaveRegistry();
    }

    private void OnDisable()
    {
        WorldCanvasMode.OnChanged -= OnWorldCanvasModeChanged;

        UnitGroupActionManager.Instance?.UnregisterUnitControl(this);
        PlayerTrackingManager.TrackingChanged -= OnTrackingChanged;

        NonEmptyControls.Remove(this);
    }

    private void RefreshSaveRegistry()
    {
        if (HasAnyGroups)
            NonEmptyControls.Add(this);
        else
            NonEmptyControls.Remove(this);
    }

    private void OnDestroy()
    {
        UnitGroupActionManager.Instance?.UnregisterUnitControl(this);
        PlayerTrackingManager.TrackingChanged -= OnTrackingChanged;

        if (PlayerUnitManager.Instance != null)
        {
            for (int i = 0; i < _groups.Count; i++)
                PlayerUnitManager.Instance.UnregisterGroup(_groups[i]);
        }

        NonEmptyControls.Remove(this);
    }

    private void OnTransformParentChanged()
    {
        UnitGroupActionManager.Instance?.RefreshRegisteredUnitControl(this);
    }

    public TileUnitGroupData AddGroup(
        MilitiaUnit unitType,
        int unitCount,
        string populationReservationId = null,
        int reservedPopulation = 0,
        int expiryTurn = -1)
    {
        if (unitType == null || unitCount <= 0)
            return null;

        string groupId = Guid.NewGuid().ToString("N");

        var group = new TileUnitGroupData(
            groupId,
            unitType,
            unitCount,
            populationReservationId,
            reservedPopulation,
            expiryTurn);

        if (TurnSystem.Instance != null)
        {
            int currentTurn = TurnSystem.GetCurrentTurn();

            int interval = 4;
            if (PlayerUnitManager.Instance != null)
                interval = Mathf.Max(1, PlayerUnitManager.Instance.upkeepIntervalTurns);

            group.upkeepStartTurn = currentTurn + interval;
        }

        _groups.Add(group);
        OnAnyUnitGroupChanged?.Invoke();

        PlayerUnitManager.Instance?.RegisterGroup(group, this);
        SpawnMarker(group);
        RefreshUnitsOnlyEmptyCanvasState();
        UnitGroupActionManager.Instance?.RefreshRegisteredUnitControl(this);

        RefreshSaveRegistry();

        return group;
    }

    /// <summary>
    /// Used by skill-training: remove the group from this tile WITHOUT
    /// releasing its population reservation. The people stay "locked" while training.
    /// </summary>
    public void RemoveGroupForTraining(TileUnitGroupData group)
    {
        if (group == null) return;

        int index = _groups.IndexOf(group);
        if (index < 0) return;

        RemoveGroupAtIndex(index, releasePopulationReservation: false);
    }

    /// <summary>
    /// Re-add an existing TileUnitGroupData that was taken away for
    /// training, now with upgraded stats.
    /// </summary>
    public void AddExistingGroup(TileUnitGroupData group)
    {
        if (group == null) return;

        if (!_groups.Contains(group))
            _groups.Add(group);

        OnAnyUnitGroupChanged?.Invoke();

        PlayerUnitManager.Instance?.RegisterGroup(group, this);
        SpawnMarker(group);
        RefreshUnitsOnlyEmptyCanvasState();
        UnitGroupActionManager.Instance?.RefreshRegisteredUnitControl(this);

        RefreshSaveRegistry();
    }

    private void SpawnMarker(TileUnitGroupData group)
    {
        if (tileUI == null || tileUI.ContentRoot == null || groupMarkerPrefab == null)
        {
            //Debug.LogWarning("[TileUnitGroupControl] Missing TileUnitUI or groupMarkerPrefab; cannot display unit group marker.");
            return;
        }

        var marker = Instantiate(groupMarkerPrefab, tileUI.ContentRoot);

        var rt = marker.transform as RectTransform;
        if (rt != null)
            rt.SetAsFirstSibling();

        marker.Bind(group);

        _markers[group.groupId] = marker;

        var trackingUI = marker.GetComponentInChildren<TileTrackingMarkerUI>(true);
        if (trackingUI != null)
        {
            _trackingMarkerUIs[group.groupId] = trackingUI;
            RefreshTrackingUI(group);
        }

        tileUI.RegisterGroup();
    }

    private void ReleasePopulationForGroup(TileUnitGroupData group)
    {
        if (group == null) return;
        if (string.IsNullOrEmpty(group.populationReservationId)) return;

        var popMgr = PlayersPopulationManager.Instance;
        if (popMgr != null)
        {
            popMgr.ReleaseReservation(group.populationReservationId);
            //Debug.Log($"[TileUnitGroupControl] Released pop reservation {group.populationReservationId} for group {group.groupId}.");
        }

        group.populationReservationId = null;
        group.reservedPopulation = 0;
    }

    private void RemoveMarkerAndTrackingForGroup(string groupId)
    {
        if (_markers.TryGetValue(groupId, out var marker) && marker != null)
        {
            Destroy(marker.gameObject);
            tileUI?.UnregisterGroup();
        }

        _markers.Remove(groupId);
        _trackingMarkerUIs.Remove(groupId);
    }

    private void RemoveGroupAtIndex(int index, bool releasePopulationReservation)
    {
        if (index < 0 || index >= _groups.Count)
            return;

        var group = _groups[index];
        if (group == null)
            return;

        if (releasePopulationReservation)
            ReleasePopulationForGroup(group);

        PlayerUnitManager.Instance?.UnregisterGroup(group);
        _groups.RemoveAt(index);
        OnAnyUnitGroupChanged?.Invoke();

        RemoveMarkerAndTrackingForGroup(group.groupId);

        RefreshUnitsOnlyEmptyCanvasState();
        UnitGroupActionManager.Instance?.RefreshRegisteredUnitControl(this);
        RefreshSaveRegistry();
    }

    public void RemoveGroup(string groupId, bool releasePopulationReservation = true)
    {
        int index = _groups.FindIndex(g => g.groupId == groupId);
        if (index < 0) return;

        RemoveGroupAtIndex(index, releasePopulationReservation);
    }

    public void RemoveGroupDueToFatalities(TileUnitGroupData group)
    {
        if (group == null) return;

        PostUnitGroupDestroyedNotification(group);

        if (!string.IsNullOrEmpty(group.populationReservationId))
        {
            var pop = PlayersPopulationManager.Instance;
            if (pop != null)
            {
                if (group.reservedPopulation > 0)
                    pop.ApplyPenaltyFromReservation(group.populationReservationId, group.reservedPopulation);

                pop.ReleaseReservation(group.populationReservationId);
            }

            group.populationReservationId = null;
            group.reservedPopulation = 0;
        }

        int index = _groups.IndexOf(group);
        if (index < 0) return;

        PlayerUnitManager.Instance?.UnregisterGroup(group);
        _groups.RemoveAt(index);

        RemoveMarkerAndTrackingForGroup(group.groupId);

        RefreshUnitsOnlyEmptyCanvasState();
        UnitGroupActionManager.Instance?.RefreshRegisteredUnitControl(this);
        RefreshSaveRegistry();
    }

    private void PostUnitGroupDestroyedNotification(TileUnitGroupData group)
    {
        if (NotificationManager.Instance == null) return;
        string groupName = !string.IsNullOrWhiteSpace(group.groupName) ? group.groupName : "Unit Group";
        string unitName  = group.unitType != null && !string.IsNullOrWhiteSpace(group.unitType.unitName)
            ? group.unitType.unitName : "Unit";
        string title, message;
        if (NotificationMessageCrafterManager.Instance != null)
            (title, message) = NotificationMessageCrafterManager.Instance.CraftUnitGroupDestroyed(groupName, unitName);
        else
            (title, message) = ("Unit Lost", $"{groupName} has been destroyed.");
        NotificationManager.Instance.AddNotification(NotificationType.UnitGroupDestroyed, title, message, transform.position);
    }

    public void RefreshMarker(TileUnitGroupData group)
    {
        if (group == null) return;

        if (_markers.TryGetValue(group.groupId, out var marker) && marker != null)
            marker.Refresh();

        RefreshTrackingUI(group);
    }

    public void RefreshAllMarkers()
    {
        foreach (var kv in _markers)
        {
            if (kv.Value != null)
                kv.Value.Refresh();
        }

        for (int i = 0; i < _groups.Count; i++)
            RefreshTrackingUI(_groups[i]);
    }

    /// <summary>
    /// Legacy temp-disband helper.
    /// IMPORTANT:
    /// A temporary disband should NOT release the population reservation.
    /// It should only detach the group from the tile so the reservation can be
    /// re-used later when regrouping by the original trained individual IDs.
    /// </summary>
    public bool TryTemporarilyDisbandGroup(TileUnitGroupData group, out int requiredPopulation)
    {
        requiredPopulation = 0;

        if (group == null)
            return false;

        int index = _groups.IndexOf(group);
        if (index < 0)
            return false;

        requiredPopulation = Mathf.Max(0, group.reservedPopulation);

        RemoveGroupAtIndex(index, releasePopulationReservation: false);
        return true;
    }

    public void MoveGroupTo(TileUnitGroupData group, TileUnitGroupControl target)
    {
        if (group == null || target == null || target == this)
            return;

        int index = _groups.IndexOf(group);
        if (index < 0)
        {
            //Debug.LogWarning($"[TileUnitGroupControl] MoveGroupTo: group {group.groupId} not found on {name}.");
            return;
        }

        _groups.RemoveAt(index);

        RemoveMarkerAndTrackingForGroup(group.groupId);

        RefreshUnitsOnlyEmptyCanvasState();
        UnitGroupActionManager.Instance?.RefreshRegisteredUnitControl(this);

        target.AddExistingGroup(group);
        UnitGroupActionManager.Instance?.RefreshRegisteredUnitControl(target);

        RefreshSaveRegistry();
        target.RefreshSaveRegistry();
    }

    private void OnTrackingChanged(TileUnitGroupData changedGroup)
    {
        if (changedGroup == null)
            return;

        for (int i = 0; i < _groups.Count; i++)
        {
            var g = _groups[i];
            if (g == null) continue;

            if (g == changedGroup || g.groupId == changedGroup.groupId)
            {
                RefreshTrackingUI(g);
                return;
            }
        }
    }

    private void RefreshTrackingUI(TileUnitGroupData group)
    {
        if (group == null)
            return;

        if (!_trackingMarkerUIs.TryGetValue(group.groupId, out var ui) || ui == null)
            return;

        var trackingMgr = PlayerTrackingManager.Instance;
        if (trackingMgr == null || !trackingMgr.HasActiveTracking(group))
        {
            ui.Hide();
            return;
        }

        Sprite icon = trackingMgr.GetIcon(group);
        int maxTurns = trackingMgr.GetMaxTurns(group);
        int remainingTurns = trackingMgr.GetRemainingTurns(group);

        ui.Show(icon, maxTurns, remainingTurns);
    }

    private void CacheUnitWorldCanvases()
    {
        var all = GetComponentsInChildren<Canvas>(true);
        var list = new List<Canvas>();

        for (int i = 0; i < all.Length; i++)
        {
            var c = all[i];
            if (c == null) continue;
            if (c.renderMode != RenderMode.WorldSpace) continue;

            if (HasAncestorNamed(c.transform, unitCanvasRootNames))
                list.Add(c);
        }

        _unitWorldCanvases = list.ToArray();
    }

    private void RefreshUnitsOnlyEmptyCanvasState()
    {
        if (!hideUnitCanvasesWhenEmpty) return;
        if (!WorldCanvasMode.UnitsOnly) return;

        bool hasUnits = _groups.Count > 0;

        if (!hasUnits)
            SuppressUnitCanvasesBecauseEmpty();
        else
            RestoreUnitCanvasesSuppressedBecauseEmpty();
    }

    private void SuppressUnitCanvasesBecauseEmpty()
    {
        if (_suppressedByEmpty) return;
        if (_unitWorldCanvases == null || _unitWorldCanvases.Length == 0) return;

        for (int i = 0; i < _unitWorldCanvases.Length; i++)
        {
            var c = _unitWorldCanvases[i];
            if (c == null) continue;

            int id = c.GetInstanceID();
            if (!_emptySuppressedCache.ContainsKey(id))
            {
                _emptySuppressedCache[id] = new CanvasState
                {
                    go = c.gameObject,
                    activeSelf = c.gameObject.activeSelf,
                    canvasEnabled = c.enabled
                };
            }

            c.enabled = false;
            c.gameObject.SetActive(false);
        }

        _suppressedByEmpty = true;
    }

    private void RestoreUnitCanvasesSuppressedBecauseEmpty()
    {
        if (!_suppressedByEmpty) return;
        if (_unitWorldCanvases == null) return;

        for (int i = 0; i < _unitWorldCanvases.Length; i++)
        {
            var c = _unitWorldCanvases[i];
            if (c == null) continue;

            int id = c.GetInstanceID();
            if (_emptySuppressedCache.TryGetValue(id, out var saved) && saved.go != null)
            {
                saved.go.SetActive(saved.activeSelf);
                c.enabled = saved.canvasEnabled;
            }
        }

        _emptySuppressedCache.Clear();
        _suppressedByEmpty = false;

        var vis = GetComponentsInChildren<TileWorldCanvasVisibility>(true);
        for (int i = 0; i < vis.Length; i++)
            vis[i]?.Refresh();
    }

    private void OnWorldCanvasModeChanged(bool unitsOnly)
    {
        if (!hideUnitCanvasesWhenEmpty) return;

        if (unitsOnly)
            RefreshUnitsOnlyEmptyCanvasState();
        else
            RestoreUnitCanvasesSuppressedBecauseEmpty();
    }

    private bool HasAncestorNamed(Transform t, string[] targetNames)
    {
        if (targetNames == null || targetNames.Length == 0) return false;

        Transform cur = t;
        while (cur != null)
        {
            if (MatchesAny(cur.gameObject.name, targetNames))
                return true;

            cur = cur.parent;
        }
        return false;
    }

    private bool MatchesAny(string actual, string[] targets)
    {
        for (int i = 0; i < targets.Length; i++)
        {
            string target = targets[i];
            if (string.IsNullOrEmpty(target)) continue;

            if (actual == target) return true;
            if (allowStartsWithMatch && actual.StartsWith(target)) return true;
        }
        return false;
    }

    public void ClearGroupsForLoad()
    {
        for (int i = _groups.Count - 1; i >= 0; i--)
        {
            TileUnitGroupData group = _groups[i];
            if (group == null)
                continue;

            PlayerUnitManager.Instance?.UnregisterGroup(group);

            if (_markers.TryGetValue(group.groupId, out var marker) && marker != null)
            {
                Destroy(marker.gameObject);
                tileUI?.UnregisterGroup();
            }

            _markers.Remove(group.groupId);
            _trackingMarkerUIs.Remove(group.groupId);
        }

        _groups.Clear();
        RefreshUnitsOnlyEmptyCanvasState();
        UnitGroupActionManager.Instance?.RefreshRegisteredUnitControl(this);
        RefreshSaveRegistry();
    }

    public void AddLoadedGroup(TileUnitGroupData group)
    {
        if (group == null)
            return;

        if (!_groups.Contains(group))
            _groups.Add(group);

        PlayerUnitManager.Instance?.RegisterGroup(group, this);
        SpawnMarker(group);
        RefreshUnitsOnlyEmptyCanvasState();
        UnitGroupActionManager.Instance?.RefreshRegisteredUnitControl(this);
        RefreshSaveRegistry();
    }

    public bool TryGetOwningGridPosition(out Vector2Int grid)
    {
        grid = default;

        TileControl tile = GetComponent<TileControl>();
        if (tile == null)
            tile = GetComponentInParent<TileControl>(true);

        if (tile == null)
            return false;

        grid = tile.GetGridPosition();
        return true;
    }
}
