public interface IPerTurnUnitAction
{
    /// Return true if the action should end immediately (target died/fled/etc).
    bool Tick(TileUnitGroupData group, TileUnitGroupControl owner, TileControl targetTile);
}
