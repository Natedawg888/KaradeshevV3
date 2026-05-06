using System.Collections.Generic;
using UnityEngine;

public partial class UnitGroupActionManager
{
    private class ScoutContext
    {
        public TileUnitGroupData group;
        public TileUnitGroupControl owner;
        public TileControl originTile;
        public ScoutTileActionSO actionDef;
    }

    private readonly List<TileControl> _scoutTileBuffer = new List<TileControl>(128);
    private ScoutContext _activeScoutContext;

    public void BeginScoutForGroup(TileUnitGroupData group, TileUnitGroupControl owner, ScoutTileActionSO actionDef)
    {
        if (group == null || owner == null || actionDef == null)
        {
            Debug.LogWarning("[UnitGroupActionManager] BeginScoutForGroup: missing group/owner/actionDef.");
            return;
        }

        var unit = group.unitType;
        if (unit == null || !actionDef.CanUnitUseAction(unit))
        {
            Debug.LogWarning("[UnitGroupActionManager] BeginScoutForGroup: unit cannot use this action.");
            return;
        }

        var originTile = owner.GetComponentInParent<TileControl>();
        if (originTile == null)
        {
            Debug.LogWarning("[UnitGroupActionManager] BeginScoutForGroup: owner has no TileControl parent.");
            return;
        }

        _activeScoutContext = new ScoutContext
        {
            group = group,
            owner = owner,
            originTile = originTile,
            actionDef = actionDef
        };

        ClearAllScoutButtons();
        ShowScoutOptionsSphere();
    }

    private void ShowScoutOptionsSphere()
    {
        if (_activeScoutContext == null ||
            _activeScoutContext.group == null ||
            _activeScoutContext.owner == null ||
            _activeScoutContext.originTile == null ||
            _activeScoutContext.actionDef == null)
        {
            ClearAllScoutButtons();
            return;
        }

        var group = _activeScoutContext.group;
        var owner = _activeScoutContext.owner;
        var origin = _activeScoutContext.originTile;
        var action = _activeScoutContext.actionDef;
        int maxRange = action.GetMaxRangeInTiles();

        _scoutTileBuffer.Clear();

        // Use the shared physics sphere, centered on the actual unit/group world position.
        CollectTilesInRangeSphere(
            origin,
            owner.transform.position,
            maxRange,
            _scoutTileBuffer,
            includeOrigin: false
        );

        for (int i = 0; i < _scoutTileBuffer.Count; i++)
        {
            var tile = _scoutTileBuffer[i];
            if (tile == null) continue;

            // Keep your gameplay validation exactly the same.
            if (!action.IsValidTarget(group, origin, tile))
                continue;

            var ui = tile.GetComponentInChildren<TileMovementUI>(true);
            if (ui == null)
                continue;

            int turnCost = action.GetTurnCost(group, origin, tile);

            // hazard UI (optional)
            bool showHazard = false;
            float dmg01 = 0f;
            float fatal01 = 0f;

            if (TryComputeScoutHazardChances(
                tile,
                group,
                out _,
                out _,
                out float dmgOutcome,
                out float fatalOutcome))
            {
                showHazard = true;
                dmg01 = dmgOutcome;
                fatal01 = fatalOutcome;
            }

            TileControl capturedTile = tile;

            ui.ShowScoutButton(
                onClick: () => OnScoutTileClicked(capturedTile),
                turnCost: turnCost,
                damageChance01: dmg01,
                fatalChance01: fatal01,
                showHazard: showHazard
            );

            _activeScoutButtonUIs.Add(ui);
        }
    }

    private void OnScoutTileClicked(TileControl targetTile)
    {
        if (_activeScoutContext == null ||
            _activeScoutContext.group == null ||
            _activeScoutContext.owner == null ||
            _activeScoutContext.actionDef == null)
        {
            Debug.LogWarning("[UnitGroupActionManager] OnScoutTileClicked with no active context.");
            ClearAllScoutButtons();
            return;
        }

        var group = _activeScoutContext.group;
        var owner = _activeScoutContext.owner;
        var origin = _activeScoutContext.originTile;
        var action = _activeScoutContext.actionDef;

        if (targetTile == null)
        {
            Debug.LogWarning("[UnitGroupActionManager] OnScoutTileClicked: targetTile is null.");
            ClearAllScoutButtons();
            _activeScoutContext = null;
            return;
        }

        int turns = action.GetTurnCost(group, origin, targetTile);
        if (turns <= 0)
        {
            Debug.LogWarning("[UnitGroupActionManager] OnScoutTileClicked: computed turn cost <= 0.");
            ClearAllScoutButtons();
            _activeScoutContext = null;
            return;
        }

        group.activeAction = action;
        group.activeActionTargetGrid = targetTile.GetGridPosition();
        group.activeActionTargetTile = targetTile;
        group.remainingActionTurns = turns;

        Debug.Log($"[UnitGroupActionManager] Group {group.groupId} started SCOUT on {targetTile.name} for {turns} turns.");

        ClearAllScoutButtons();
        _activeScoutContext = null;

        owner.RefreshMarker(group);
    }

    private void ClearAllScoutButtons()
    {
        for (int i = 0; i < _activeScoutButtonUIs.Count; i++)
        {
            var ui = _activeScoutButtonUIs[i];
            if (ui != null) ui.HideScout();
        }

        _activeScoutButtonUIs.Clear();
    }
}