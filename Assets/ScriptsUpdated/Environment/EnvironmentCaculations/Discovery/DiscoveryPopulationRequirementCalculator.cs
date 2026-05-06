using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public static class DiscoveryPopulationRequirementCalculator
{
    public static int CalculateRequiredPopulation(EnvironmentType envType, EnvironmentTileType tileType, TileSize size)
    {
        // Base from size
        int basePop = size switch
        {
            TileSize.Tiny => 2,
            TileSize.Small => 4,
            TileSize.Medium => 8,
            TileSize.Large => 16,
            TileSize.Giant => 32,
            TileSize.Massive => 64,
            _ => 3
        };

        // Environment type modifier
        float envModifier = envType switch
        {
            EnvironmentType.Desert => 2.3f,
            EnvironmentType.Grassland => 2.0f,
            EnvironmentType.Savanna => 2.0f,

            // Forests
            EnvironmentType.TemperateForest => 2.9f, // old Forest (high)
            EnvironmentType.BorealForest => 2.9f, // old DeepPine / dense forests
            EnvironmentType.TropicalForest => 2.8f, // old Jungle

            // Wet / harsh
            EnvironmentType.SubTropical => 2.3f, // old Swamp
            EnvironmentType.Lake => 2.2f, // old Marshland-ish

            // Cold / rugged
            EnvironmentType.Tundra => 2.3f,
            EnvironmentType.Mountain => 2.4f,

            // Sea travel
            EnvironmentType.Ocean => 3.0f,

            EnvironmentType.SaltLake => 2.25f,

            _ => 1.0f
        };

        // Tile type modifier (covers all new enum values)
        float tileModifier = tileType switch
        {
            EnvironmentTileType.Land => 1.0f,

            // Coast / lake edges
            EnvironmentTileType.Coastline => 1.1f,
            EnvironmentTileType.CoastlineCorner => 1.1f,
            EnvironmentTileType.LakeEdge => 1.1f,
            EnvironmentTileType.LakeCorner => 1.1f,

            // Mountains replace old cliff family
            EnvironmentTileType.Mountain => 2f,

            // Rivers / mouths
            EnvironmentTileType.River => 1.2f,
            EnvironmentTileType.RiverCorner => 1.2f,
            EnvironmentTileType.RiverSplit => 1.3f,
            EnvironmentTileType.RiverMouth => 1.3f,
            EnvironmentTileType.LakeMouth => 1.3f,

            // River extra cases
            EnvironmentTileType.RiverCross => 1.5f,
            EnvironmentTileType.RiverEnd => 1.3f,

            // Water / ocean / lake
            EnvironmentTileType.Water => 1.5f,
            EnvironmentTileType.Lake => 1.7f,
            EnvironmentTileType.Ocean => 2.0f,

            // Specials
            EnvironmentTileType.Cave => 1.1f,

            EnvironmentTileType.SaltLake => 1.5f,

            _ => 1.0f
        };

        float raw = basePop * envModifier * tileModifier;
        int result = Mathf.CeilToInt(raw);
        return Mathf.Max(1, result);
    }
}