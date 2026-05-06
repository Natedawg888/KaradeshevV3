using UnityEngine;
using System.Collections.Generic;

public static class DiscoveryFailureCalculator
{
    // Base failure chance per environment type (whole numbers)
    private static readonly Dictionary<EnvironmentType, int> baseByEnvironment = new()
    {
        { EnvironmentType.Desert,          60 },
        { EnvironmentType.Grassland,       20 },
        { EnvironmentType.Savanna,         25 },

        // Forests
        { EnvironmentType.TemperateForest, 30 }, // old Forest/Pine-ish
        { EnvironmentType.BorealForest,    40 }, // old DeepPine/DeepForest-ish
        { EnvironmentType.TropicalForest,  60 }, // old Jungle

        // Wet / dense / tricky
        { EnvironmentType.SubTropical,     50 }, // old Swamp hardest

        // Cold / rugged
        { EnvironmentType.Tundra,          60 },
        { EnvironmentType.Mountain,        80 },

        // Water biomes
        { EnvironmentType.Lake,            60 }, // old Marshland-ish difficulty bucket
        { EnvironmentType.Ocean,           60 }, // old ShallowOcean

        { EnvironmentType.SaltLake,           20 },
    };

    // Modifiers by environment tile type (positive = harder, negative = easier)
    private static readonly Dictionary<EnvironmentTileType, int> tileTypeModifiers = new()
    {
        { EnvironmentTileType.Land,             0 },

        // Coast / lake edges slightly easier
        { EnvironmentTileType.Coastline,       -1 },
        { EnvironmentTileType.CoastlineCorner,  0 },
        { EnvironmentTileType.LakeEdge,        -1 },
        { EnvironmentTileType.LakeCorner,      -1 },

        // Mountains replace old cliff “harder to discover”
        { EnvironmentTileType.Mountain,         3 },

        // Rivers generally easier to traverse/observe
        { EnvironmentTileType.River,           -2 },
        { EnvironmentTileType.RiverCorner,     -1 },
        { EnvironmentTileType.RiverSplit,       1 },
        { EnvironmentTileType.RiverCross,       1 },
        { EnvironmentTileType.RiverEnd,        -1 },

        // Caves easier to "discover" (you notice them)
        { EnvironmentTileType.Cave,            -5 },

        // Water / mouths
        { EnvironmentTileType.Water,           -2 },
        { EnvironmentTileType.RiverMouth,       0 },
        { EnvironmentTileType.LakeMouth,        0 },

        // Open water harder
        { EnvironmentTileType.Lake,             2 },
        { EnvironmentTileType.Ocean,            5 },

        { EnvironmentTileType.SaltLake,        2 },
    };

    // Size affects failure: larger = harder to discover (whole number)
    private static int SizeModifier(TileSize size)
    {
        return size switch
        {
            TileSize.Tiny => 2,
            TileSize.Small => 3,
            TileSize.Medium => 4,
            TileSize.Large => 5,
            TileSize.Giant => 6,
            TileSize.Massive => 7,
            _ => 0,
        };
    }

    public static int CalculateFailureChance(EnvironmentType envType, EnvironmentTileType tileType, TileSize size)
    {
        int baseChance = baseByEnvironment.TryGetValue(envType, out var b) ? b : 10;
        int tileMod = tileTypeModifiers.TryGetValue(tileType, out var t) ? t : 0;
        int sizeMod = SizeModifier(size);

        int raw = baseChance + tileMod + sizeMod;

        // Clamp between 1 and 95 (inclusive)
        return Mathf.Clamp(raw, 1, 95);
    }
}