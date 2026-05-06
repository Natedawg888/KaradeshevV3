using UnityEngine;

public static class GatheringPopulationRequirementCalculator
{
    public static int CalculateRequiredPopulation(EnvironmentType env, EnvironmentTileType tile, TileSize size)
    {
        // Slightly cheaper than discovery by size
        int basePop = size switch
        {
            TileSize.Tiny => 1,
            TileSize.Small => 2,
            TileSize.Medium => 4,
            TileSize.Large => 8,
            TileSize.Giant => 10,
            TileSize.Massive => 20,
            _ => 2
        };

        float envMod = env switch
        {
            EnvironmentType.Desert => 1.6f,
            EnvironmentType.Grassland => 1.1f,
            EnvironmentType.Savanna => 1.2f,

            // Forests
            EnvironmentType.TemperateForest => 1.3f, // Birch/Forest/Pine cluster
            EnvironmentType.BorealForest => 1.5f, // DeepForest-ish
            EnvironmentType.TropicalForest => 1.8f, // Jungle

            // Wet / harsh
            EnvironmentType.Lake => 1.4f, // Marshland
            EnvironmentType.SubTropical => 1.6f, // Swamp

            // Cold / rugged
            EnvironmentType.Tundra => 1.4f,
            EnvironmentType.Mountain => 1.5f,

            // Sea travel
            EnvironmentType.Ocean => 1.9f,

            EnvironmentType.SaltLake => 1.25f,

            _ => 1f
        };

        float tileMod = tile switch
        {
            EnvironmentTileType.Land => 1.0f,

            // Rivers / mouths
            EnvironmentTileType.River => 1.1f,
            EnvironmentTileType.RiverCorner => 1.1f,
            EnvironmentTileType.RiverSplit => 1.2f,
            EnvironmentTileType.RiverMouth => 1.2f,
            EnvironmentTileType.LakeMouth => 1.2f,
            EnvironmentTileType.RiverCross => 1.3f,
            EnvironmentTileType.RiverEnd => 1.1f,

            // Water / lake edges
            EnvironmentTileType.Water => 1.2f,
            EnvironmentTileType.LakeEdge => 1.1f,
            EnvironmentTileType.LakeCorner => 1.1f,
            EnvironmentTileType.Lake => 1.25f,

            // Coast
            EnvironmentTileType.Coastline => 1.2f,
            EnvironmentTileType.CoastlineCorner => 1.2f,

            // Ocean / specials / rough
            EnvironmentTileType.Ocean => 1.5f,
            EnvironmentTileType.Cave => 1.6f,
            EnvironmentTileType.Mountain => 1.5f, // replaces all old cliff variants

            EnvironmentTileType.SaltLake => 1.25f,

            _ => 1.0f
        };

        int result = Mathf.CeilToInt(basePop * envMod * tileMod);
        return Mathf.Max(1, result);
    }
}