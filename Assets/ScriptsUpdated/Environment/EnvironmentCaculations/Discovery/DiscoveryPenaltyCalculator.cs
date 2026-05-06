using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class DiscoveryPenaltyCalculator
{
    public static int CalculatePopulationPenalty(EnvironmentType envType, EnvironmentTileType tileType, TileSize size)
    {
        // Base penalty by size (bigger = harsher penalty)
        float basePenalty = size switch
        {
            TileSize.Tiny => 1f,
            TileSize.Small => 1f,
            TileSize.Medium => 2f,
            TileSize.Large => 3f,
            TileSize.Giant => 4f,
            TileSize.Massive => 5f,
            _ => 2f
        };

        // Environment type modifier
        float envModifier = envType switch
        {
            EnvironmentType.Desert => 1.3f,
            EnvironmentType.Grassland => 1.0f,
            EnvironmentType.Savanna => 1.0f,

            // Forests (generally easier than open harsh biomes)
            EnvironmentType.TemperateForest => 0.9f, // old Forest
            EnvironmentType.BorealForest => 0.85f, // old DeepForest-ish
            EnvironmentType.TropicalForest => 0.8f, // old Jungle

            // Wet / harsh on population when scouting
            EnvironmentType.SubTropical => 1.4f, // old Swamp
            EnvironmentType.Lake => 1.2f, // old Marshland-ish

            // Harsh / draining
            EnvironmentType.Tundra => 1.2f,
            EnvironmentType.Mountain => 1.2f,

            // Sea travel / exposure risk
            EnvironmentType.Ocean => 1.5f,

            EnvironmentType.SaltLake => 0.75f,

            _ => 1.0f
        };

        // Tile type modifier
        float tileModifier = tileType switch
        {
            EnvironmentTileType.Land => 1.0f,
            EnvironmentTileType.Coastline => 1.0f,
            EnvironmentTileType.CoastlineCorner => 1.0f,

            // Lakes / edges
            EnvironmentTileType.LakeEdge => 1.1f,
            EnvironmentTileType.LakeCorner => 1.1f,
            EnvironmentTileType.Lake => 1.25f,

            // Mountains replace old cliff family
            EnvironmentTileType.Mountain => 1.3f,

            // Rivers
            EnvironmentTileType.River => 1.1f,
            EnvironmentTileType.RiverCorner => 1.1f,
            EnvironmentTileType.RiverSplit => 1.2f,
            EnvironmentTileType.RiverMouth => 1.2f,
            EnvironmentTileType.LakeMouth => 1.2f,

            // Specials
            EnvironmentTileType.Cave => 2.0f,

            // Water / ocean
            EnvironmentTileType.Water => 1.2f,
            EnvironmentTileType.Ocean => 1.5f,

            // Other river cases
            EnvironmentTileType.RiverCross => 1.4f,
            EnvironmentTileType.RiverEnd => 1.2f,

            EnvironmentTileType.SaltLake => 1.25f,

            _ => 1.0f
        };

        float raw = basePenalty * envModifier * tileModifier;
        int penalty = Mathf.CeilToInt(raw);

        // Clamp to reasonable bounds (at least 1, but not absurd)
        return Mathf.Clamp(penalty, 1, 10);
    }
}