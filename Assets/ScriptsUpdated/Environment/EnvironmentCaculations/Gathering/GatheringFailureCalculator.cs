using System.Collections.Generic;
using UnityEngine;

public static class GatheringFailureCalculator
{
    private static readonly Dictionary<EnvironmentType, int> BaseByEnvironment = new()
    {
        { EnvironmentType.Desert,          40 },
        { EnvironmentType.TemperateForest, 20 },   // Birch/Forest/Pine
        { EnvironmentType.BorealForest,    30 },  // DeepForest/DeepPine
        { EnvironmentType.TropicalForest,  45 },  // Jungle
        { EnvironmentType.Grassland,       10 },
        { EnvironmentType.Lake,            40 },  // Marshland
        { EnvironmentType.Savanna,         15 },
        { EnvironmentType.SubTropical,     30 },  // Swamp
        { EnvironmentType.Ocean,           40 },  // ShallowOcean

        // Reasonable defaults for new biomes (tweak if you want)
        { EnvironmentType.Tundra,          50 },
        { EnvironmentType.Mountain,        60 },

        { EnvironmentType.SaltLake,           20 },
    };

    private static readonly Dictionary<EnvironmentTileType, int> TileMods = new()
    {
        { EnvironmentTileType.Land,             0 },

        // Rivers
        { EnvironmentTileType.River,           -1 },
        { EnvironmentTileType.RiverCorner,     -1 },
        { EnvironmentTileType.RiverSplit,       0 },
        { EnvironmentTileType.RiverMouth,       0 },
        { EnvironmentTileType.LakeMouth,        0 },
        { EnvironmentTileType.RiverCross,       1 },
        { EnvironmentTileType.RiverEnd,        -1 },

        // Water / lake edges
        { EnvironmentTileType.Water,           -2 },
        { EnvironmentTileType.LakeEdge,        -2 },
        { EnvironmentTileType.LakeCorner,      -1 },
        { EnvironmentTileType.Lake,             0 },

        // Coast
        { EnvironmentTileType.Coastline,        1 },
        { EnvironmentTileType.CoastlineCorner,  1 },

        // Ocean
        { EnvironmentTileType.Ocean,            4 },

        // Specials / rough terrain
        { EnvironmentTileType.Cave,             3 },
        { EnvironmentTileType.Mountain,         3 }, // replaces all old cliff variants

        { EnvironmentTileType.SaltLake,         2 },
    };

    private static int SizeMod(TileSize size) => size switch
    {
        TileSize.Tiny => 1,
        TileSize.Small => 2,
        TileSize.Medium => 3,
        TileSize.Large => 4,
        TileSize.Giant => 5,
        TileSize.Massive => 6,
        _ => 0,
    };

    public static int CalculateFailureChance(EnvironmentType env, EnvironmentTileType tile, TileSize size)
    {
        int baseChance = BaseByEnvironment.TryGetValue(env, out var b) ? b : 10;
        int tileMod = TileMods.TryGetValue(tile, out var t) ? t : 0;
        int sizeMod = SizeMod(size);

        int raw = baseChance + tileMod + sizeMod;
        return Mathf.Clamp(raw, 1, 90);
    }
}