using System.Collections.Generic;
using UnityEngine;

public static class DiscoveryTurnCalculator
{
    // Base discovery turns per EnvironmentType (NEW enums)
    private static readonly Dictionary<EnvironmentType, int> BaseDiscoveryTurns = new()
    {
        { EnvironmentType.Desert,          12 },
        { EnvironmentType.Grassland,       11 },
        { EnvironmentType.Savanna,         11 },

        // Forest buckets
        { EnvironmentType.TemperateForest, 13 }, // old Birch/Forest/Pine cluster
        { EnvironmentType.BorealForest,    13 }, // old DeepForest/DeepPine cluster
        { EnvironmentType.TropicalForest,  15 }, // old Jungle

        // Wet
        { EnvironmentType.Lake,            16 }, // old Marshland-ish
        { EnvironmentType.SubTropical,     15 }, // old Swamp

        // Cold / rugged
        { EnvironmentType.Tundra,          22 },
        { EnvironmentType.Mountain,        30 },

        // Sea
        { EnvironmentType.Ocean,           17 },

        { EnvironmentType.SaltLake,           10 },
    };

    // Multiplier per EnvironmentTileType (NEW enums)
    private static readonly Dictionary<EnvironmentTileType, float> TileTypeModifiers = new()
    {
        { EnvironmentTileType.Land,            0.8f },

        // Rivers
        { EnvironmentTileType.River,           0.9f },
        { EnvironmentTileType.RiverCorner,     0.95f },
        { EnvironmentTileType.RiverSplit,      1.0f },
        { EnvironmentTileType.RiverMouth,      1.0f },
        { EnvironmentTileType.LakeMouth,       1.0f },
        { EnvironmentTileType.RiverCross,      1.1f },
        { EnvironmentTileType.RiverEnd,        0.95f },

        // Water / lake edges
        { EnvironmentTileType.Water,           0.75f },
        { EnvironmentTileType.LakeEdge,        1.0f },
        { EnvironmentTileType.LakeCorner,      1.05f },
        { EnvironmentTileType.Lake,            1.15f },

        // Coast
        { EnvironmentTileType.Coastline,       1.1f },
        { EnvironmentTileType.CoastlineCorner, 1.1f },

        // Ocean / specials
        { EnvironmentTileType.Ocean,           1.4f },
        { EnvironmentTileType.Cave,            0.8f },

        // Rough terrain (replaces cliff family)
        { EnvironmentTileType.Mountain,        1.45f },

        { EnvironmentTileType.SaltLake,        1.15f },
    };

    // Multiplier per TileSize
    private static readonly Dictionary<TileSize, float> TileSizeModifiers = new()
    {
        { TileSize.Tiny,    0.75f },
        { TileSize.Small,   1.5f },
        { TileSize.Medium,  3f },
        { TileSize.Large,   6f },
        { TileSize.Giant,   12f },
        { TileSize.Massive, 24f },
    };

    public static int CalculateDiscoveryTurns(EnvironmentType envType, EnvironmentTileType tileType, TileSize size)
    {
        int baseTurns = BaseDiscoveryTurns.TryGetValue(envType, out var bt) ? bt : 3;
        float tileMod = TileTypeModifiers.TryGetValue(tileType, out var tm) ? tm : 1f;
        float sizeMod = TileSizeModifiers.TryGetValue(size, out var sm) ? sm : 1f;

        float raw = baseTurns * tileMod * sizeMod * Random.Range(0.9f, 1.1f);
        return Mathf.Max(1, Mathf.CeilToInt(raw));
    }
}