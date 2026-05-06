using System.Collections.Generic;

public struct TileEnvironmentData
{
    public bool hasWater;
    public float plantFood;
    public float dangerLevel;
    public float climateScore;

    public EnvironmentType environmentType;
    public EnvironmentTileType tileType;
}

// Result when animals eat/drink from resource nodes
public struct ResourceConsumptionResult
{
    public float hungerSatisfied;
    public float thirstSatisfied;
}

public interface IEnvironmentDataSource
{
    TileEnvironmentData GetTileData(TileCoord coord);

    /// <summary>
    /// NON-ALLOC: Fill 'results' with neighbour tiles within maxDistance.
    /// Caller owns and reuses 'results' to avoid allocations.
    /// Implementations should: results.Clear(); then Add coords.
    /// </summary>
    void GetNeighbourTilesNonAlloc(TileCoord center, int maxDistance, List<TileCoord> results, bool includeCenter = false);

    /// <summary>
    /// Convenience/legacy path. May allocate depending on implementation.
    /// Keep this so older code can still compile while you migrate to NonAlloc calls.
    /// </summary>
    IEnumerable<TileCoord> GetNeighbourTiles(TileCoord center, int maxDistance);

    ResourceConsumptionResult ConsumeResourcesForAnimalGroup(
        TileCoord coord,
        AnimalDefinition species,
        int groupSize,
        float maxHungerToSatisfy,
        float maxThirstToSatisfy
    );
}