using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Calculates how many population points are required to survey a tile,
/// based on its environment type, tile subtype, and size, with some random variance.
/// </summary>
public static class SurveyPopulationRequirementCalculator
{
    // Base population requirement per EnvironmentType
    private static readonly Dictionary<EnvironmentType, int> BasePopulation = new()
    {
        { EnvironmentType.Desert,          8 },
        { EnvironmentType.TemperateForest, 6 },  // Birch/Forest/Pine cluster
        { EnvironmentType.BorealForest,    9 },  // DeepForest/DeepPine cluster
        { EnvironmentType.TropicalForest,  12 }, // Jungle
        { EnvironmentType.Grassland,       4 },
        { EnvironmentType.Lake,            8 },  // Marshland
        { EnvironmentType.Savanna,         5 },
        { EnvironmentType.SubTropical,     9 },  // Swamp
        { EnvironmentType.Ocean,           15 }, // ShallowOcean

        // New biomes (reasonable defaults)
        { EnvironmentType.Tundra,          9 },
        { EnvironmentType.Mountain,        10 },

        { EnvironmentType.SaltLake,        6 },
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

        { EnvironmentTileType.SaltLake,             1.25f },
    };

    // Modifier per TileSize
    private static readonly Dictionary<TileSize, float> TileSizeModifiers = new()
    {
        { TileSize.Tiny,    0.5f },
        { TileSize.Small,   0.75f },
        { TileSize.Medium,  1.0f },
        { TileSize.Large,   1.25f },
        { TileSize.Giant,   1.5f },
        { TileSize.Massive, 2.0f },
    };

    /// Calculates and returns the population needed to survey a tile.
    public static int CalculateRequiredPopulation(EnvironmentType envType, EnvironmentTileType tileType, TileSize size)
    {
        int basePop = BasePopulation.TryGetValue(envType, out var bp) ? bp : 5;
        float tileMod = TileTypeModifiers.TryGetValue(tileType, out var ttm) ? ttm : 1f;
        float sizeMod = TileSizeModifiers.TryGetValue(size, out var sm) ? sm : 1f;

        // Apply random variance of ±20%
        float raw = basePop * tileMod * sizeMod * Random.Range(0.8f, 1.2f);
        return Mathf.Max(1, Mathf.CeilToInt(raw));
    }
}