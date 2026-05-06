using UnityEngine;

public static class ClearingPopulationRequirementCalculator
{
    public static int CalculateRequiredPopulation(EnvironmentType env, EnvironmentTileType tile, TileSize size)
    {
        // Clearing is heavier than gathering, so base population is higher.
        int basePop = size switch
        {
            TileSize.Tiny => 2,
            TileSize.Small => 3,
            TileSize.Medium => 4,
            TileSize.Large => 6,
            TileSize.Giant => 8,
            TileSize.Massive => 10,
            _ => 3
        };

        float envMod = env switch
        {
            EnvironmentType.Desert => 1.7f,
            EnvironmentType.Grassland => 1.1f,
            EnvironmentType.Savanna => 1.3f,

            // Forests
            EnvironmentType.TemperateForest => 1.3f, // old Forest/Birch/Pine
            EnvironmentType.BorealForest => 1.6f, // old DeepForest/DeepPine

            // Dense / wet / hard clearing
            EnvironmentType.SubTropical => 1.7f, // old Swamp/Marsh-ish feel
            EnvironmentType.TropicalForest => 2.0f, // old Jungle

            // Cold / rugged
            EnvironmentType.Tundra => 1.5f,
            EnvironmentType.Mountain => 1.7f,

            // Water biomes (generally “hard” to clear / reclaim)
            EnvironmentType.Lake => 1.6f,
            EnvironmentType.Ocean => 2.1f,

            EnvironmentType.SaltLake => 1.5f,

            _ => 1f
        };

        float tileMod = tile switch
        {
            EnvironmentTileType.Land => 1.0f,

            // Rivers
            EnvironmentTileType.River => 1.1f,
            EnvironmentTileType.RiverCorner => 1.15f,
            EnvironmentTileType.RiverSplit => 1.2f,
            EnvironmentTileType.RiverMouth => 1.2f,
            EnvironmentTileType.LakeMouth => 1.2f,
            EnvironmentTileType.RiverCross => 1.3f,
            EnvironmentTileType.RiverEnd => 1.1f,

            // Water / lake edges
            EnvironmentTileType.Water => 1.2f,
            EnvironmentTileType.LakeEdge => 1.15f,
            EnvironmentTileType.LakeCorner => 1.2f,
            EnvironmentTileType.Lake => 1.35f,

            // Coast
            EnvironmentTileType.Coastline => 1.25f,
            EnvironmentTileType.CoastlineCorner => 1.25f,

            // Open ocean
            EnvironmentTileType.Ocean => 1.6f,

            // Specials / rough terrain
            EnvironmentTileType.Cave => 1.8f,
            EnvironmentTileType.Mountain => 1.9f, // replaces old cliff family

            EnvironmentTileType.SaltLake => 1.3f,

            _ => 1.0f
        };

        float raw = basePop * envMod * tileMod;

        // Round up and enforce at least 1 person.
        int result = Mathf.CeilToInt(raw);
        return Mathf.Max(1, result);
    }
}
