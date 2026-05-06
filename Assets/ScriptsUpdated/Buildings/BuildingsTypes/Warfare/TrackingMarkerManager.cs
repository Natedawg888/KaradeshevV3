using System.Collections.Generic;
using UnityEngine;

public class TrackingMarkerManager : MonoBehaviour
{
    public static TrackingMarkerManager Instance { get; private set; }

    private class ActiveMarker
    {
        public TileTrackingMarkerUI ui;
        public int maxTurns;
        public int remainingTurns;
        public Sprite icon;
        public TileUnitGroupData ownerGroup;
    }

    private readonly Dictionary<TileControl, ActiveMarker> _active = new();
    private readonly Dictionary<TileControl, TileTrackingMarkerUI> _markerCache = new();
    private readonly List<TileControl> _removeBuffer = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public bool IsMarkerActive(TileControl tile, Sprite icon = null)
    {
        if (tile == null)
            return false;

        if (!_active.TryGetValue(tile, out var active) || active == null)
            return false;

        if (active.remainingTurns <= 0 || active.ui == null)
            return false;

        if (icon == null)
            return true;

        return active.icon == icon;
    }

    public void ShowMarker(TileControl tile, Sprite icon, int durationTurns)
    {
        ShowMarker(tile, icon, durationTurns, null);
    }

    public void ShowMarker(TileControl tile, Sprite icon, int durationTurns, TileUnitGroupData ownerGroup)
    {
        if (tile == null)
            return;

        durationTurns = Mathf.Max(1, durationTurns);

        var ui = GetMarkerUI(tile);
        if (ui == null)
        {
            Debug.LogWarning($"[TrackingMarkerManager] Tile '{tile.name}' has no TileTrackingMarkerUI.");
            return;
        }

        if (!_active.TryGetValue(tile, out var active) || active == null)
        {
            active = new ActiveMarker { ui = ui };
            _active[tile] = active;
        }

        if (active.ownerGroup != null && active.icon != null)
            PlayerTrackingManager.Instance?.UnregisterTrackingTarget(active.ownerGroup, tile, active.icon);

        active.ui = ui;
        active.maxTurns = durationTurns;
        active.remainingTurns = durationTurns;
        active.icon = icon;
        active.ownerGroup = ownerGroup;

        PlayerTrackingManager.Instance?.RegisterTrackingTarget(ownerGroup, tile, icon, durationTurns);

        active.ui.Show(icon, active.maxTurns, active.remainingTurns);
    }

    public void HideMarker(TileControl tile)
    {
        if (tile == null)
            return;

        if (_active.TryGetValue(tile, out var active) && active != null)
        {
            if (active.ownerGroup != null && active.icon != null)
                PlayerTrackingManager.Instance?.UnregisterTrackingTarget(active.ownerGroup, tile, active.icon);

            if (active.ui != null)
                active.ui.Hide();
        }

        _active.Remove(tile);
    }

    public void UpdateGroupTurns(TileUnitGroupData ownerGroup, int maxTurns, int turnsLeft)
    {
        if (ownerGroup == null)
            return;

        foreach (var kv in _active)
        {
            var active = kv.Value;
            if (active == null || active.ownerGroup != ownerGroup)
                continue;

            active.maxTurns = maxTurns;
            active.remainingTurns = turnsLeft;

            if (active.ui != null)
                active.ui.UpdateTurns(maxTurns, turnsLeft);
        }
    }

    public void ClearGroup(TileUnitGroupData ownerGroup)
    {
        if (ownerGroup == null)
            return;

        _removeBuffer.Clear();

        foreach (var kv in _active)
        {
            if (kv.Value != null && kv.Value.ownerGroup == ownerGroup)
                _removeBuffer.Add(kv.Key);
        }

        for (int i = 0; i < _removeBuffer.Count; i++)
            HideMarker(_removeBuffer[i]);

        _removeBuffer.Clear();
    }

    private TileTrackingMarkerUI GetMarkerUI(TileControl tile)
    {
        if (tile == null)
            return null;

        if (_markerCache.TryGetValue(tile, out var cached) && cached != null)
            return cached;

        var ui = tile.GetComponentInChildren<TileTrackingMarkerUI>(true);
        if (ui != null)
        {
            _markerCache[tile] = ui;
            return ui;
        }

        return null;
    }
}