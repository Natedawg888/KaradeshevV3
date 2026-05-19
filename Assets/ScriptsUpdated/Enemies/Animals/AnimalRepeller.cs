using UnityEngine;

/// <summary>
/// Attach to a building to repel animals from the building tile and surrounding radius.
/// Caches its tile coordinate via MonoEnvironmentDataSource on enable.
/// </summary>
public class AnimalRepeller : MonoBehaviour
{
    [Tooltip("How many tiles in each direction from this building to repel animal movement.")]
    [Min(1)] public int repelRadiusTiles = 2;

    public TileCoord CachedTileCoord { get; private set; }
    public bool HasCachedTile { get; private set; }

    private void OnEnable()
    {
        CacheTileCoord();
        AnimalRepellerRegistry.Register(this);
    }

    private void OnDisable()
    {
        HasCachedTile = false;
        AnimalRepellerRegistry.Unregister(this);
    }

    private void CacheTileCoord()
    {
        var monoEnv = MonoEnvironmentDataSource.Instance;
        if (monoEnv != null && monoEnv.TryGetTileCoordForPosition(transform.position, out TileCoord coord))
        {
            CachedTileCoord = coord;
            HasCachedTile = true;
        }
    }
}
