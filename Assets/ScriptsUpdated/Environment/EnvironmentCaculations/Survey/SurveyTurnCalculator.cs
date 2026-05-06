using System.Collections.Generic;
using UnityEngine;

/// Calculates how many turns are required to “survey” (scan) a tile,
/// based on its environment type, tile subtype, and size, with some random variance.
public static class SurveyTurnCalculator
{
    // Base survey turns per EnvironmentType
    private static readonly Dictionary<EnvironmentType, int> BaseSurveyTurns = new()
    {
        { EnvironmentType.Desert,          4 },
        { EnvironmentType.TemperateForest, 3 }, // Birch/Forest/Pine cluster
        { EnvironmentType.BorealForest,    5 }, // DeepForest/DeepPine cluster
        { EnvironmentType.TropicalForest,  6 }, // Jungle
        { EnvironmentType.Grassland,       2 },
        { EnvironmentType.Lake,            4 }, // Marshland
        { EnvironmentType.Savanna,         3 },
        { EnvironmentType.SubTropical,     5 }, // Swamp
        { EnvironmentType.Ocean,           7 }, // ShallowOcean

        // New biomes (reasonable defaults)
        { EnvironmentType.Tundra,          5 },
        { EnvironmentType.Mountain,        5 },

        { EnvironmentType.SaltLake,        8 },
    };

    // Multiplier per EnvironmentTileType
    private static readonly Dictionary<EnvironmentTileType, float> TileTypeModifiers = new()
    {
        { EnvironmentTileType.Land,             1.0f },

        { EnvironmentTileType.Coastline,        1.1f },
        { EnvironmentTileType.CoastlineCorner,  1.2f },

        { EnvironmentTileType.LakeEdge,         1.3f },
        { EnvironmentTileType.LakeCorner,       1.3f },

        // Rough terrain (replaces cliff family)
        { EnvironmentTileType.Mountain,         1.5f },

        // Rivers / mouths
        { EnvironmentTileType.River,            1.2f },
        { EnvironmentTileType.RiverCorner,      1.2f },
        { EnvironmentTileType.RiverSplit,       1.3f },
        { EnvironmentTileType.RiverMouth,       1.3f },
        { EnvironmentTileType.LakeMouth,        1.3f },
        { EnvironmentTileType.RiverCross,       1.3f },
        { EnvironmentTileType.RiverEnd,         1.1f },

        // Water / lake / ocean
        { EnvironmentTileType.Water,            1.1f },
        { EnvironmentTileType.Lake,             1.3f },
        { EnvironmentTileType.Ocean,            2.5f },

        // Specials
        { EnvironmentTileType.Cave,             2.0f },

        { EnvironmentTileType.SaltLake,             1.75f },
    };

    // Multiplier per TileSize
    private static readonly Dictionary<TileSize, float> TileSizeModifiers = new()
    {
        { TileSize.Tiny,    0.5f },
        { TileSize.Small,   0.75f },
        { TileSize.Medium,  1.0f },
        { TileSize.Large,   1.25f },
        { TileSize.Giant,   1.5f },
        { TileSize.Massive, 2.0f },
    };

    /// Calculates and returns the number of turns needed to survey a tile.
    public static int CalculateSurveyTurns(EnvironmentType envType, EnvironmentTileType tileType, TileSize size)
    {
        // Look up base turns, tile modifier, and size modifier
        int baseTurns = BaseSurveyTurns.TryGetValue(envType, out var bt) ? bt : 3;
        float tileMod = TileTypeModifiers.TryGetValue(tileType, out var ttm) ? ttm : 1f;
        float sizeMod = TileSizeModifiers.TryGetValue(size, out var sm) ? sm : 1f;

        // Raw value with a ±10% random variance
        float raw = baseTurns * tileMod * sizeMod * Random.Range(0.9f, 1.1f);

        // Round up and ensure at least 1
        return Mathf.Max(1, Mathf.CeilToInt(raw));
    }
}