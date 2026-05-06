using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerTrackingManager : MonoBehaviour
{
    public static PlayerTrackingManager Instance { get; private set; }

    public static event Action<TileUnitGroupData> TrackingChanged;
    private static void RaiseTrackingChanged(TileUnitGroupData group) => TrackingChanged?.Invoke(group);

    private class ActiveTracking
    {
        public TileUnitGroupData group;
        public Sprite icon;
        public int maxTurns;
        public int remainingTurns;
        public readonly HashSet<TileControl> targets = new();
    }

    private readonly Dictionary<string, ActiveTracking> _activeByGroupId = new();
    private readonly List<string> _removeIds = new();
    private readonly List<TileControl> _tileBuffer = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnEnable()
    {
        TurnSystem.SubscribeToEndOfTurn(OnEndTurn);
    }

    private void OnDisable()
    {
        TurnSystem.UnsubscribeFromEndOfTurn(OnEndTurn);
    }

    public bool HasActiveTracking(TileUnitGroupData group)
    {
        if (group == null || string.IsNullOrWhiteSpace(group.groupId))
            return false;

        return _activeByGroupId.TryGetValue(group.groupId, out var state) &&
               state != null &&
               state.remainingTurns > 0;
    }

    public int GetRemainingTurns(TileUnitGroupData group)
    {
        if (!TryGetState(group, out var state))
            return 0;

        return state.remainingTurns;
    }

    public int GetMaxTurns(TileUnitGroupData group)
    {
        if (!TryGetState(group, out var state))
            return 0;

        return state.maxTurns;
    }

    public Sprite GetIcon(TileUnitGroupData group)
    {
        if (!TryGetState(group, out var state))
            return null;

        return state.icon;
    }

    public void RegisterTrackingTarget(TileUnitGroupData group, TileControl tile, Sprite icon, int durationTurns)
    {
        if (group == null || tile == null || string.IsNullOrWhiteSpace(group.groupId))
            return;

        durationTurns = Mathf.Max(1, durationTurns);

        if (!_activeByGroupId.TryGetValue(group.groupId, out var state) || state == null)
        {
            state = new ActiveTracking
            {
                group = group
            };
            _activeByGroupId[group.groupId] = state;
        }

        state.group = group;
        state.icon = icon;
        state.maxTurns = durationTurns;
        state.remainingTurns = durationTurns;
        state.targets.Add(tile);

        group.AddTrackingTarget(tile, icon);

        RaiseTrackingChanged(group);
    }

    public void UnregisterTrackingTarget(TileUnitGroupData group, TileControl tile, Sprite icon)
    {
        if (group == null || tile == null || string.IsNullOrWhiteSpace(group.groupId))
            return;

        if (!_activeByGroupId.TryGetValue(group.groupId, out var state) || state == null)
            return;

        state.targets.Remove(tile);
        group.RemoveTrackingTarget(tile, icon);

        if (state.targets.Count == 0)
            _activeByGroupId.Remove(group.groupId);

        RaiseTrackingChanged(group);
    }

    public void ClearTracking(TileUnitGroupData group)
    {
        if (group == null || string.IsNullOrWhiteSpace(group.groupId))
            return;

        if (!_activeByGroupId.TryGetValue(group.groupId, out var state) || state == null)
            return;

        _tileBuffer.Clear();
        foreach (var tile in state.targets)
        {
            if (tile != null)
                _tileBuffer.Add(tile);
        }

        for (int i = 0; i < _tileBuffer.Count; i++)
            TrackingMarkerManager.Instance?.HideMarker(_tileBuffer[i]);

        _tileBuffer.Clear();
        _activeByGroupId.Remove(group.groupId);

        RaiseTrackingChanged(group);
    }

    private bool TryGetState(TileUnitGroupData group, out ActiveTracking state)
    {
        state = null;

        if (group == null || string.IsNullOrWhiteSpace(group.groupId))
            return false;

        return _activeByGroupId.TryGetValue(group.groupId, out state) && state != null;
    }

    private void OnEndTurn()
    {
        if (_activeByGroupId.Count == 0)
            return;

        _removeIds.Clear();

        foreach (var kv in _activeByGroupId)
        {
            var state = kv.Value;
            if (state == null || state.group == null)
            {
                _removeIds.Add(kv.Key);
                continue;
            }

            state.remainingTurns = Mathf.Max(0, state.remainingTurns - 1);

            if (state.remainingTurns <= 0)
            {
                _removeIds.Add(kv.Key);
            }
            else
            {
                TrackingMarkerManager.Instance?.UpdateGroupTurns(state.group, state.maxTurns, state.remainingTurns);
                RaiseTrackingChanged(state.group);
            }
        }

        for (int i = 0; i < _removeIds.Count; i++)
        {
            string id = _removeIds[i];

            if (!_activeByGroupId.TryGetValue(id, out var state) || state == null)
                continue;

            var group = state.group;

            _tileBuffer.Clear();
            foreach (var tile in state.targets)
            {
                if (tile != null)
                    _tileBuffer.Add(tile);
            }

            for (int t = 0; t < _tileBuffer.Count; t++)
                TrackingMarkerManager.Instance?.HideMarker(_tileBuffer[t]);

            _tileBuffer.Clear();
            _activeByGroupId.Remove(id);

            if (group != null)
                RaiseTrackingChanged(group);
        }

        _removeIds.Clear();
    }
}