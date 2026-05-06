using UnityEngine;
using System.Collections.Generic;

public static class EnvironmentResourceCapacityCalculator
{
    // Base capacity per environment type
    private static readonly Dictionary<EnvironmentType, float> baseCapacityByEnvironment = new()
    {
        { EnvironmentType.Desert,          1000f },
        { EnvironmentType.Grassland,       3000f },
        { EnvironmentType.Savanna,         2000f },

        // Forests
        { EnvironmentType.TemperateForest, 4000f }, // Birch/Forest/Pine-ish
        { EnvironmentType.BorealForest,    4500f }, // DeepForest/DeepPine-ish average
        { EnvironmentType.TropicalForest,  5000f }, // Jungle

        // Wet / water-ish
        { EnvironmentType.Lake,            4000f }, // Marshland
        { EnvironmentType.SubTropical,     4000f }, // Swamp

        // Cold / rugged
        { EnvironmentType.Tundra,          2500f },
        { EnvironmentType.Mountain,        2000f },

        // Sea
        { EnvironmentType.Ocean,           2000f }, // ShallowOcean

        { EnvironmentType.SaltLake,           2000f },
    };

    // Modifiers by tile type (positive increases capacity)
    private static readonly Dictionary<EnvironmentTileType, float> tileTypeModifiers = new()
    {
        { EnvironmentTileType.Land,             0.1f },
        { EnvironmentTileType.Coastline,        0.5f },
        { EnvironmentTileType.CoastlineCorner,  0.5f },

        { EnvironmentTileType.LakeEdge,         1f },
        { EnvironmentTileType.LakeCorner,       1f },

        // Rough terrain (replaces cliff family)
        { EnvironmentTileType.Mountain,        -0.3f },

        // Rivers
        { EnvironmentTileType.River,            1f },
        { EnvironmentTileType.RiverCorner,      0.8f },
        { EnvironmentTileType.RiverSplit,       1.2f },
        { EnvironmentTileType.RiverMouth,       1f },
        { EnvironmentTileType.LakeMouth,        1f },
        { EnvironmentTileType.RiverCross,       1.3f },
        { EnvironmentTileType.RiverEnd,         0.6f },

        // Water / lake
        { EnvironmentTileType.Water,            1f },
        { EnvironmentTileType.Lake,             0.9f },

        // Specials
        { EnvironmentTileType.Cave,             2f },

        // Ocean tile
        { EnvironmentTileType.Ocean,            0.5f },

        { EnvironmentTileType.SaltLake,            1.25f },
    };

    // Size multiplier (larger tiles support more)
    public static float SizeMultiplier(TileSize size)
    {
        return size switch
        {
            TileSize.Tiny => 1.25f,
            TileSize.Small => 1.5f,
            TileSize.Medium => 2f,
            TileSize.Large => 2.5f,
            TileSize.Giant => 4f,
            TileSize.Massive => 8f,
            _ => 1f,
        };
    }

    /// <summary>
    /// Calculates the total capacity (sum of resource amounts) for a tile, returning an integer >=1.
    /// Randomized within a jitter range to avoid uniformity.
    /// </summary>
    public static int CalculateTotalCapacity(EnvironmentType envType, EnvironmentTileType tileType, TileSize size, float randomness = 0.2f)
    {
        float baseCap = baseCapacityByEnvironment.TryGetValue(envType, out var b) ? b : 1f;
        float tileMod = tileTypeModifiers.TryGetValue(tileType, out var t) ? t : 0f;
        float sizeMul = SizeMultiplier(size);

        float raw = (baseCap + tileMod) * sizeMul;
        raw = Mathf.Max(0.1f, raw); // avoid zero

        // apply randomness +/- percentage
        float delta = raw * randomness;
        float min = Mathf.Max(1f, raw - delta);
        float max = raw + delta;

        int capacity = Mathf.Max(1, Mathf.RoundToInt(Random.Range(min, max)));
        return capacity;
    }
}