using UnityEngine;

public static class GatheringPenaltyCalculator
{
    public static int CalculatePopulationPenalty(EnvironmentType env, EnvironmentTileType tile, TileSize size)
    {
        // Penalties are lighter than discovery, generally
        float basePenalty = size switch
        {
            TileSize.Tiny => 0.5f,
            TileSize.Small => 1f,
            TileSize.Medium => 1.5f,
            TileSize.Large => 2f,
            TileSize.Giant => 3f,
            TileSize.Massive => 4f,
            _ => 1.5f
        };

        float envMod = env switch
        {
            EnvironmentType.Desert => 1.2f,
            EnvironmentType.Grassland => 0.8f,
            EnvironmentType.Savanna => 1.0f,

            // Forests
            EnvironmentType.TemperateForest => 0.9f,
            EnvironmentType.BorealForest => 1.0f,
            EnvironmentType.TropicalForest => 1.5f,

            // Wet / harsh
            EnvironmentType.Lake => 1.2f,
            EnvironmentType.SubTropical => 1.4f,

            // Cold / rugged
            EnvironmentType.Tundra => 1.1f,
            EnvironmentType.Mountain => 1.2f,

            // Sea travel
            EnvironmentType.Ocean => 1.6f,

            EnvironmentType.SaltLake => 1.25f,

            _ => 1f
        };

        float tileMod = tile switch
        {
            EnvironmentTileType.Land => 1.0f,
            EnvironmentTileType.River => 1.0f,
            EnvironmentTileType.LakeEdge => 1.0f,
            EnvironmentTileType.Coastline => 1.1f,
            EnvironmentTileType.Water => 1.1f,

            // New tiles
            EnvironmentTileType.Lake => 1.2f,
            EnvironmentTileType.Ocean => 1.5f,
            EnvironmentTileType.Cave => 1.8f,
            EnvironmentTileType.Mountain => 1.6f,

            // River variants
            EnvironmentTileType.RiverSplit => 1.1f,
            EnvironmentTileType.RiverCross => 1.2f,

            EnvironmentTileType.SaltLake => 1.5f,

            _ => 1f
        };

        int penalty = Mathf.CeilToInt(basePenalty * envMod * tileMod);
        return Mathf.Clamp(penalty, 0, 8);
    }
}