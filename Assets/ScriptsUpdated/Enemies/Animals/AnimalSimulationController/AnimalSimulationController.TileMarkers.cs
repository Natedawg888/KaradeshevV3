using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class AnimalSimulationController : MonoBehaviour
{
    private void BuildTileUiLookup()
    {
        _tileUIs.Clear();

        var tiles = FindAllFast<TileAnimalUI>(includeInactive: true);
        for (int i = 0; i < tiles.Length; i++)
        {
            var tileUI = tiles[i];
            if (tileUI == null) continue;

            var coord = tileUI.Coord;
            if (_tileUIs.ContainsKey(coord))
                continue;

            _tileUIs.Add(coord, tileUI);
        }
    }

    private bool TryGetTileUI(TileCoord coord, out TileAnimalUI tileUI)
        => _tileUIs.TryGetValue(coord, out tileUI);

    private void HandleGroupCreated(AnimalGroupState group)
    {
        if (markerPrefab == null)
        {
            Debug.LogWarning("[AnimalSimulationController] markerPrefab is null.");
            return;
        }

        if (!TryGetTileUI(group.tile, out var tileUI))
        {
            Debug.LogWarning($"[AnimalSimulationController] No TileAnimalUI for {group.tile} while creating marker for animal group {group.id}.");
            return;
        }

        tileUI.ResolveNow();

        if (tileUI.ContentRoot == null)
        {
            Debug.LogWarning($"[AnimalSimulationController] TileAnimalUI ContentRoot was null for {group.tile} while creating marker for animal group {group.id}.");
            return;
        }

        var marker = Instantiate(markerPrefab, tileUI.ContentRoot, false);
        marker.Init(group);
        marker.SetPlayerTargeted(_playerTargetedAnimalIds.Contains(group.id));

        _markerViews[group.id] = marker;

        PlayerReligionManager.Instance?.TryFillMissingSacredAnimalGroups();

        tileUI.RefreshNow();
    }

    private void HandleGroupUpdated(AnimalGroupState group)
    {
        if (group == null)
            return;

        if (!_tileUIs.TryGetValue(group.tile, out TileAnimalUI ui) || ui == null)
        {
            _tileUIs.Remove(group.tile);
            BuildTileUiLookup();

            if (!_tileUIs.TryGetValue(group.tile, out ui) || ui == null)
                return;
        }

        UpdateBuildingUnderAttackFromGroup(group);

        if (!_markerViews.TryGetValue(group.id, out var marker))
            return;

        if (marker == null)
        {
            _markerViews.Remove(group.id);
            return;
        }

        var oldTile = marker.CurrentTile;
        var newTile = group.tile;

        if (!TryGetTileUI(newTile, out var newTileUI))
            return;

        newTileUI.ResolveNow();

        if (newTileUI.ContentRoot == null)
            return;

        if (!oldTile.Equals(newTile))
        {
            if (TryGetTileUI(oldTile, out var oldTileUI))
                oldTileUI.RefreshNow();
        }

        marker.UpdateFromState(group, newTileUI.ContentRoot);
        newTileUI.RefreshNow();
    }

    private void HandleGroupRemoved(int groupId)
    {
        RemoveBuildingAttacker(groupId);
        UnregisterAnimalAttackIfAny(groupId);

        if (_markerViews.TryGetValue(groupId, out var marker))
        {
            if (marker != null)
            {
                var tileCoord = marker.CurrentTile;
                if (TryGetTileUI(tileCoord, out var tileUI))
                    tileUI.RefreshNow();

                Destroy(marker.gameObject);
            }

            PlayerReligionManager.Instance?.NotifyAnimalGroupRemoved(groupId);
            _markerViews.Remove(groupId);
        }
    }

    // ------------------------
    // Building attack icon API
    // ------------------------
}
