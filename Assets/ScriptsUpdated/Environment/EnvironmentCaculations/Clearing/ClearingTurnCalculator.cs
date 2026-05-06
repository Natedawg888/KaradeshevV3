using System.Collections.Generic;
using UnityEngine;

public static class ClearingTurnCalculator
{
    // Base turns by environment type (generally harder/denser = more turns than gathering).
    private static readonly Dictionary<EnvironmentType, int> EnvBaseTurns = new()
    {
        { EnvironmentType.Desert,          5 },
        { EnvironmentType.Grassland,       3 },
        { EnvironmentType.Savanna,         4 },

        // Forests
        { EnvironmentType.TemperateForest, 4 }, // old Birch/Forest/Pine vibe
        { EnvironmentType.BorealForest,    5 }, // old DeepForest/DeepPine vibe (denser/harsher)
        { EnvironmentType.SubTropical,     6 }, // old swampy / heavy vegetation vibe
        { EnvironmentType.TropicalForest,  7 }, // old Jungle

        // Cold / harsh
        { EnvironmentType.Tundra,          5 },

        // Water / rugged
        { EnvironmentType.Lake,            5 },
        { EnvironmentType.Ocean,           7 }, // old ShallowOcean
        { EnvironmentType.Mountain,        6 },

        { EnvironmentType.SaltLake,        5 },
    };

    // Tile difficulty modifiers (slightly harsher).
    private static readonly Dictionary<EnvironmentTileType, float> TileMods = new()
    {
        { EnvironmentTileType.Land,            1.0f },

        // Rivers
        { EnvironmentTileType.River,           1.0f },
        { EnvironmentTileType.RiverCorner,     1.05f },
        { EnvironmentTileType.RiverSplit,      1.1f },
        { EnvironmentTileType.RiverMouth,      1.1f },
        { EnvironmentTileType.LakeMouth,       1.1f },
        { EnvironmentTileType.RiverCross,      1.2f },
        { EnvironmentTileType.RiverEnd,        1.05f },

        // Lakes / water edges
        { EnvironmentTileType.Water,           1.1f },
        { EnvironmentTileType.LakeEdge,        1.1f },
        { EnvironmentTileType.LakeCorner,      1.15f },
        { EnvironmentTileType.Lake,            1.25f }, // between Water and Ocean in difficulty

        // Coast
        { EnvironmentTileType.Coastline,       1.2f },
        { EnvironmentTileType.CoastlineCorner, 1.2f },

        // Open ocean
        { EnvironmentTileType.Ocean,           1.5f },

        // Specials / rough terrain
        { EnvironmentTileType.Cave,            1.7f },
        { EnvironmentTileType.Mountain,        1.6f }, // replaces old cliff harshness

        { EnvironmentTileType.SaltLake,        1.25f },
    };

    private static readonly Dictionary<TileSize, float> SizeMods = new()
    {
        { TileSize.Tiny,    1.0f },
        { TileSize.Small,   1.6f },
        { TileSize.Medium,  2.8f },
        { TileSize.Large,   4.5f },
        { TileSize.Giant,   7.0f },
        { TileSize.Massive, 10.0f },
    };

    public static int CalculateClearingTurns(EnvironmentType env, EnvironmentTileType tile, TileSize size)
    {
        int baseTurns = EnvBaseTurns.TryGetValue(env, out var bt) ? bt : 4;
        float tileMod = TileMods.TryGetValue(tile, out var tm) ? tm : 1f;
        float sizeMod = SizeMods.TryGetValue(size, out var sm) ? sm : 1f;

        float raw = baseTurns * tileMod * sizeMod;

        // small random variation so it's not always identical
        raw *= Random.Range(0.9f, 1.1f);

        return Mathf.Max(1, Mathf.CeilToInt(raw));
    }
}
