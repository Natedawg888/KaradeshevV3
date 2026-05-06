using System;

public static class TileLifecycleEvents
{
    // First time the tile gets an environment instance
    public static event Action<TileScript> OnTileEnvironmentSpawned;

    // Any later swap (climate, forced changes, etc.)
    public static event Action<TileScript> OnTileEnvironmentChanged;

    public static void RaiseSpawned(TileScript tile)  => OnTileEnvironmentSpawned?.Invoke(tile);
    public static void RaiseChanged(TileScript tile)  => OnTileEnvironmentChanged?.Invoke(tile);
}
