using System.Collections.Generic;
using UnityEngine;

public static class ResurveyIntervalCalculator
{
    // Base re-survey interval per EnvironmentType
    private static readonly Dictionary<EnvironmentType, int> BaseResurveyTurns = new()
    {
        { EnvironmentType.Desert,          12 },
        { EnvironmentType.TemperateForest, 9 },  // Birch/Forest/Pine cluster (8/9/9 -> ~9)
        { EnvironmentType.BorealForest,    14 }, // DeepForest/DeepPine cluster
        { EnvironmentType.TropicalForest,  16 }, // Jungle
        { EnvironmentType.Grassland,       7 },
        { EnvironmentType.Lake,            12 }, // Marshland
        { EnvironmentType.Savanna,         8 },
        { EnvironmentType.SubTropical,     13 }, // Swamp
        { EnvironmentType.Ocean,           18 }, // ShallowOcean

        // New biomes (reasonable defaults)
        { EnvironmentType.Tundra,          14 },
        { EnvironmentType.Mountain,        15 },

        { EnvironmentType.SaltLake,        10 },
    };

    // Modifier per EnvironmentTileType
    private static readonly Dictionary<EnvironmentTileType, float> TileTypeModifiers = new()
    {
        { EnvironmentTileType.Land,             1.0f },

        { EnvironmentTileType.Coastline,        1.2f },
        { EnvironmentTileType.CoastlineCorner,  1.2f },

        { EnvironmentTileType.LakeEdge,         1.3f },
        { EnvironmentTileType.LakeCorner,       1.3f },

        // Rough terrain (replaces cliff family)
        { EnvironmentTileType.Mountain,         1.4f },

        // Rivers / mouths
        { EnvironmentTileType.River,            1.1f },
        { EnvironmentTileType.RiverCorner,      1.1f },
        { EnvironmentTileType.RiverSplit,       1.2f },
        { EnvironmentTileType.RiverMouth,       1.2f },
        { EnvironmentTileType.LakeMouth,        1.2f },
        { EnvironmentTileType.RiverCross,       1.2f },
        { EnvironmentTileType.RiverEnd,         1.1f },

        // Water / lake / ocean
        { EnvironmentTileType.Water,            1.1f },
        { EnvironmentTileType.Lake,             1.3f },
        { EnvironmentTileType.Ocean,            1.6f },

        // Specials
        { EnvironmentTileType.Cave,             1.5f },

        { EnvironmentTileType.SaltLake,             1.2f },
    };

    // Modifier per TileSize
    private static readonly Dictionary<TileSize, float> TileSizeModifiers = new()
    {
        { TileSize.Tiny,    2.5f },
        { TileSize.Small,   2.0f },
        { TileSize.Medium,  1.50f },
        { TileSize.Large,   1.25f },
        { TileSize.Giant,   0.75f },
        { TileSize.Massive, 0.5f },
    };

    /// Returns the number of turns after completion of a survey before the tile must be surveyed again.
    public static int CalculateResurveyInterval(EnvironmentType envType, EnvironmentTileType tileType, TileSize size)
    {
        int baseTurns = BaseResurveyTurns.TryGetValue(envType, out var bt) ? bt : 10;
        float tileMod = TileTypeModifiers.TryGetValue(tileType, out var ttm) ? ttm : 1f;
        float sizeMod = TileSizeModifiers.TryGetValue(size, out var sm) ? sm : 1f;

        // ±10% variance
        float raw = baseTurns * tileMod * sizeMod * Random.Range(0.9f, 1.1f);
        return Mathf.Max(1, Mathf.CeilToInt(raw));
    }
}