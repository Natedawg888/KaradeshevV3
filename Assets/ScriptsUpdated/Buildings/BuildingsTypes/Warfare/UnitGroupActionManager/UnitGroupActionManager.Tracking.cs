using UnityEngine;

public partial class UnitGroupActionManager
{
    // SELF action: begins tracking scan around the group's current tile.
    public void BeginTrackingForGroup(TileUnitGroupData group, TileUnitGroupControl owner, TrackAreaActionSO actionDef)
    {
        if (group == null || owner == null || actionDef == null)
        {
            //Debug.LogWarning("[UnitGroupActionManager] BeginTrackingForGroup: missing group/owner/actionDef.");
            return;
        }

        var unit = group.unitType;
        if (unit == null || !actionDef.CanUnitUseAction(unit))
        {
            //Debug.LogWarning("[UnitGroupActionManager] BeginTrackingForGroup: unit cannot use this action.");
            return;
        }

        var originTile = owner.GetComponentInParent<TileControl>();
        if (originTile == null)
        {
            //Debug.LogWarning("[UnitGroupActionManager] BeginTrackingForGroup: owner has no TileControl parent.");
            return;
        }

        int turns = actionDef.GetTurnCost(group, originTile, originTile);
        if (turns <= 0) turns = 1;

        group.activeAction = actionDef;
        group.activeActionTargetGrid = originTile.GetGridPosition();
        group.activeActionTargetTile = originTile;
        group.remainingActionTurns = turns;

        //Debug.Log($"[UnitGroupActionManager] Group {group.groupId} started TRACK for {turns} turns.");

        owner.RefreshMarker(group);
    }
}
