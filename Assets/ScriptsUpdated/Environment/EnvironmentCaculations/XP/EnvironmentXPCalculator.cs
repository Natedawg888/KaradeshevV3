using System.Collections.Generic;
using UnityEngine;

public static class EnvironmentXPCalculator
{
    // Shared base XP from biome/environment type
    private static readonly Dictionary<EnvironmentType, int> EnvironmentBaseXP = new()
    {
        { EnvironmentType.Desert,          16 },
        { EnvironmentType.TemperateForest, 13 },
        { EnvironmentType.BorealForest,    14 },
        { EnvironmentType.TropicalForest,  17 },
        { EnvironmentType.Grassland,       12 },
        { EnvironmentType.Lake,            15 },
        { EnvironmentType.Savanna,         13 },
        { EnvironmentType.SubTropical,     15 },
        { EnvironmentType.Ocean,           17 },
        { EnvironmentType.Tundra,          18 },
        { EnvironmentType.Mountain,        19 },
        { EnvironmentType.SaltLake,        14 },
    };

    // Shared base XP from tile form/type
    private static readonly Dictionary<EnvironmentTileType, int> TileBaseXP = new()
    {
        { EnvironmentTileType.Land,              10 },

        // Rivers
        { EnvironmentTileType.River,             11 },
        { EnvironmentTileType.RiverCorner,       11 },
        { EnvironmentTileType.RiverSplit,        11 },
        { EnvironmentTileType.RiverMouth,        12 },
        { EnvironmentTileType.LakeMouth,         12 },
        { EnvironmentTileType.RiverCross,        12 },
        { EnvironmentTileType.RiverEnd,          11 },

        // Water / lake edges
        { EnvironmentTileType.Water,             12 },
        { EnvironmentTileType.LakeEdge,          12 },
        { EnvironmentTileType.LakeCorner,        11 },
        { EnvironmentTileType.Lake,              12 },

        // Coast
        { EnvironmentTileType.Coastline,         13 },
        { EnvironmentTileType.CoastlineCorner,   13 },

        // Ocean
        { EnvironmentTileType.Ocean,             14 },

        // Specials / rough terrain
        { EnvironmentTileType.Cave,              13 },
        { EnvironmentTileType.Mountain,          14 },
        { EnvironmentTileType.SaltLake,          12 },
    };

    public static int CalculateDiscoveryXP(
        EnvironmentType env,
        EnvironmentTileType tile,
        int requiredPopulation,
        float discoveryDifficulty)
    {
        return CalculateXPInternal(env, tile, requiredPopulation, discoveryDifficulty);
    }

    public static int CalculateGatheringXP(
        EnvironmentType env,
        EnvironmentTileType tile,
        int requiredPopulation,
        float gatheringDifficulty)
    {
        return CalculateXPInternal(env, tile, requiredPopulation, gatheringDifficulty);
    }

    // Handy wrapper if you already use fail chance as the difficulty source.
    public static int CalculateDiscoveryXPFromFailChance(
        EnvironmentType env,
        EnvironmentTileType tile,
        int requiredPopulation,
        float failChancePercent)
    {
        float difficulty = Mathf.Clamp01(failChancePercent / 100f);
        return CalculateDiscoveryXP(env, tile, requiredPopulation, difficulty);
    }

    public static int CalculateGatheringXPFromFailChance(
        EnvironmentType env,
        EnvironmentTileType tile,
        int requiredPopulation,
        float failChancePercent)
    {
        float difficulty = Mathf.Clamp01(failChancePercent / 100f);
        return CalculateGatheringXP(env, tile, requiredPopulation, difficulty);
    }

    private static int CalculateXPInternal(
        EnvironmentType env,
        EnvironmentTileType tile,
        int requiredPopulation,
        float difficulty)
    {
        int envBase = EnvironmentBaseXP.TryGetValue(env, out var e) ? e : 2;
        int tileBase = TileBaseXP.TryGetValue(tile, out var t) ? t : 0;

        int xpBase = envBase + tileBase;

        float clampedDifficulty = Mathf.Max(0f, difficulty);
        int xpGranted = Mathf.RoundToInt(
            xpBase + (xpBase * clampedDifficulty) + Mathf.Max(0, requiredPopulation));

        return Mathf.Max(1, xpGranted);
    }
}