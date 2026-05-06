using System.Collections.Generic;
using System.Linq;

public partial class AnimalSimulation
{
    /// <summary>
    /// External API to force-update a group state (eg. unit combat system),
    /// keeping tile index consistent and notifying listeners.
    /// </summary>
    public void SetGroup(AnimalGroupState group)
    {
        // Safety: ignore invalid ids
        if (group.id <= 0)
            return;

        // If we already had this group, update tile index if its tile changed
        if (_groups.TryGetValue(group.id, out var old))
        {
            if (!group.tile.Equals(old.tile))
            {
                MoveGroupInTileIndex(group.id, old.tile, group.tile);
            }
        }
        else
        {
            // New group: ensure it's indexed on its tile
            AddToTileIndex(group.id, group.tile);
        }

        group.EnsureHealthValid();

        _groups[group.id] = group;
        OnGroupUpdated?.Invoke(group);
    }

    /// <summary>
    /// Collect all alive groups on a tile into a reusable buffer (no allocations).
    /// </summary>
    public void CollectGroupsOnTile(TileCoord tile, List<AnimalGroupState> outGroups)
    {
        if (outGroups == null)
            return;

        outGroups.Clear();

        if (!_tileIndex.TryGetValue(tile, out var ids) || ids == null || ids.Count == 0)
            return;

        for (int i = 0; i < ids.Count; i++)
        {
            int id = ids[i];
            if (_groups.TryGetValue(id, out var group) && group != null)
                outGroups.Add(group);
        }
    }

    /// <summary>
    /// Compute a flee tile away from a threat. If StepAwayFrom can't improve distance,
    /// force a move to ANY neighbour (prefer not stepping onto the threat tile).
    /// </summary>
    public TileCoord ComputeFleeTile(TileCoord from, TileCoord threatTile)
    {
        TileCoord next = StepAwayFrom(from, threatTile);

        // If StepAwayFrom couldn't find a "better" tile, force ANY neighbour move.
        if (next.Equals(from))
        {
            if (_env == null) return next;

            var neighEnum = _env.GetNeighbourTiles(from, 1);
            if (neighEnum == null) return next;

            // Materialize once so we can Count/index reliably.
            var neigh = neighEnum as IList<TileCoord> ?? neighEnum.ToList();
            if (neigh.Count == 0) return next;

            // Prefer something that's NOT the threat tile.
            for (int i = 0; i < neigh.Count; i++)
            {
                if (!neigh[i].Equals(threatTile))
                    return neigh[i];
            }

            // Otherwise just take the first.
            return neigh[0];
        }

        return next;
    }

    /// <summary>
    /// Force a group to step away from a specific threat tile (used by unit combat resolution).
    /// Keeps tile index consistent and notifies listeners.
    /// </summary>
    public void ForceStepAwayFromThreat(int groupId, TileCoord threatTile)
    {
        if (!_groups.TryGetValue(groupId, out var g) || !g.isAlive)
            return;

        var oldTile = g.tile;
        var newTile = ComputeFleeTile(oldTile, threatTile);

        if (newTile.Equals(oldTile))
            return;

        // Update tile index
        MoveGroupInTileIndex(groupId, oldTile, newTile);

        // Apply move
        g.tile = newTile;
        g.lastAction = AnimalActionType.Flee;

        _groups[groupId] = g;
        OnGroupUpdated?.Invoke(g);
    }
}