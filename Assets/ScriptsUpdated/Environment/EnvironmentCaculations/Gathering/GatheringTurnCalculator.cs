using System.Collections.Generic;
using UnityEngine;

public static class GatheringTurnCalculator
{
    private static readonly Dictionary<EnvironmentType, int> EnvBaseTurns = new()
    {
        { EnvironmentType.Desert,          8 },
        { EnvironmentType.TemperateForest, 9 }, // Birch/Forest/Pine
        { EnvironmentType.BorealForest,    7 }, // DeepForest/DeepPine
        { EnvironmentType.TropicalForest,  10 }, // Jungle
        { EnvironmentType.Grassland,       7 },
        { EnvironmentType.Lake,            10 }, // Marshland
        { EnvironmentType.Savanna,         7 },
        { EnvironmentType.SubTropical,     10 }, // Swamp
        { EnvironmentType.Ocean,           30 }, // ShallowOcean

        // Reasonable defaults for new biomes
        { EnvironmentType.Tundra,          15 },
        { EnvironmentType.Mountain,        20 },

        { EnvironmentType.SaltLake,        12 },
    };

    private static readonly Dictionary<EnvironmentTileType, float> TileMods = new()
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

    private static readonly Dictionary<TileSize, float> SizeMods = new()
    {
        { TileSize.Tiny,    0.75f },
        { TileSize.Small,   1.5f },
        { TileSize.Medium,  3f },
        { TileSize.Large,   6f },
        { TileSize.Giant,   12f },
        { TileSize.Massive, 24f },
    };

    public static int CalculateGatheringTurns(EnvironmentType env, EnvironmentTileType tile, TileSize size)
    {
        int baseTurns = EnvBaseTurns.TryGetValue(env, out var bt) ? bt : 3;
        float tileMod = TileMods.TryGetValue(tile, out var tm) ? tm : 1f;
        float sizeMod = SizeMods.TryGetValue(size, out var sm) ? sm : 1f;

        float raw = baseTurns * tileMod * sizeMod;
        raw *= Random.Range(0.9f, 1.1f); // small variability

        return Mathf.Max(1, Mathf.CeilToInt(raw));
    }
}