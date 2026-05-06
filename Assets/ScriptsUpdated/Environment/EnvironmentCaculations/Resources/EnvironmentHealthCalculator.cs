using System.Collections.Generic;
using UnityEngine;

public static class EnvironmentHealthCalculator
{
    // How resilient different environments are overall (capacity to absorb damage).
    private static readonly Dictionary<EnvironmentType, float> EnvHealthMods = new()
    {
        { EnvironmentType.Desert,          0.7f },
        { EnvironmentType.Grassland,       1.0f },
        { EnvironmentType.Savanna,         1.0f },

        // Forests
        { EnvironmentType.TemperateForest, 1.2f }, // old Forest-ish
        { EnvironmentType.BorealForest,    1.25f }, // old DeepForest/DeepPine-ish
        { EnvironmentType.TropicalForest,  1.4f }, // old Jungle

        // Wet / dense
        { EnvironmentType.Lake,            1.1f }, // old Marshland
        { EnvironmentType.SubTropical,     1.2f }, // old Swamp

        // Cold / rugged
        { EnvironmentType.Tundra,          1.0f },
        { EnvironmentType.Mountain,        1.1f },

        // Water biome
        { EnvironmentType.Ocean,           0.9f }, // old ShallowOcean

        { EnvironmentType.SaltLake,           0.8f },
    };

    // How quickly environments *recover* per tick relative to others.
    // Higher = faster recovery.
    private static readonly Dictionary<EnvironmentType, float> EnvRecoveryMods = new()
    {
        { EnvironmentType.Desert,          0.6f },
        { EnvironmentType.Grassland,       1.0f },
        { EnvironmentType.Savanna,         1.0f },

        // Forests
        { EnvironmentType.TemperateForest, 1.1f },
        { EnvironmentType.BorealForest,    1.15f },
        { EnvironmentType.TropicalForest,  1.3f },

        // Wet / dense (slower bounce-back if you "damage" it)
        { EnvironmentType.Lake,            0.9f },
        { EnvironmentType.SubTropical,     0.8f },

        // Cold / rugged
        { EnvironmentType.Tundra,          0.85f },
        { EnvironmentType.Mountain,        0.8f },

        // Water biome
        { EnvironmentType.Ocean,           0.7f },

        { EnvironmentType.SaltLake,           1.25f },
    };

    // Terrain difficulty table – tougher terrain tends to have a bit more "buffer" (health),
    // but often recovers more slowly.
    private static readonly Dictionary<EnvironmentTileType, float> TileDifficultyMods = new()
    {
        { EnvironmentTileType.Land,            1.0f },

        // Rivers / mouths
        { EnvironmentTileType.River,           1.0f },
        { EnvironmentTileType.RiverCorner,     1.05f },
        { EnvironmentTileType.RiverSplit,      1.1f },
        { EnvironmentTileType.RiverMouth,      1.1f },
        { EnvironmentTileType.LakeMouth,       1.1f },
        { EnvironmentTileType.RiverCross,      1.2f },
        { EnvironmentTileType.RiverEnd,        1.05f },

        // Water / lake edges
        { EnvironmentTileType.Water,           1.1f },
        { EnvironmentTileType.LakeEdge,        1.1f },
        { EnvironmentTileType.LakeCorner,      1.15f },
        { EnvironmentTileType.Lake,            1.25f },

        // Coast
        { EnvironmentTileType.Coastline,       1.2f },
        { EnvironmentTileType.CoastlineCorner, 1.2f },

        // Open ocean
        { EnvironmentTileType.Ocean,           1.5f },

        // Specials / rough terrain
        { EnvironmentTileType.Cave,            1.7f },
        { EnvironmentTileType.Mountain,        1.6f }, // replaces old cliff family
    };

    // Size → base health
    private static readonly Dictionary<TileSize, int> SizeBaseHealth = new()
    {
        { TileSize.Tiny,    40 },
        { TileSize.Small,   70 },
        { TileSize.Medium,  100 },
        { TileSize.Large,   140 },
        { TileSize.Giant,   190 },
        { TileSize.Massive, 240 },
    };

    // Size → base recovery per tick (small areas bounce back faster)
    private static readonly Dictionary<TileSize, float> SizeBaseRecovery = new()
    {
        { TileSize.Tiny,    4f },
        { TileSize.Small,   3f },
        { TileSize.Medium,  2.5f },
        { TileSize.Large,   2f },
        { TileSize.Giant,   1.5f },
        { TileSize.Massive, 1.2f },
    };

    // ------------ MAX HEALTH ------------

    public static int CalculateMaxHealth(EnvironmentType env, EnvironmentTileType tile, TileSize size)
    {
        int baseHealth = SizeBaseHealth.TryGetValue(size, out var bh) ? bh : 100;

        float envMod = EnvHealthMods.TryGetValue(env, out var em) ? em : 1f;
        float tileMod = TileDifficultyMods.TryGetValue(tile, out var tm) ? tm : 1f;

        // Harder terrain → a bit more health, but not insane.
        float raw = baseHealth * envMod * Mathf.Lerp(0.9f, 1.3f, Mathf.Clamp01((tileMod - 1f) * 0.8f + 0.5f));

        int result = Mathf.RoundToInt(raw);
        return Mathf.Clamp(result, 20, 400);
    }

    // ------------ RECOVERY PER TICK ------------

    public static int CalculateRecoveryPerTick(EnvironmentType env, EnvironmentTileType tile, TileSize size)
    {
        float baseRecovery = SizeBaseRecovery.TryGetValue(size, out var br) ? br : 2f;

        float envMod = EnvRecoveryMods.TryGetValue(env, out var erm) ? erm : 1f;

        // Difficult tiles recover slightly slower
        float tileDiff = TileDifficultyMods.TryGetValue(tile, out var td) ? td : 1f;
        float tileRecoveryMod = Mathf.Lerp(1.1f, 0.7f, Mathf.Clamp01((tileDiff - 1f) * 0.8f)); // higher difficulty => slower

        float raw = baseRecovery * envMod * tileRecoveryMod;

        int result = Mathf.CeilToInt(raw);

        // Always at least 1, but don't let it get silly-fast
        return Mathf.Clamp(result, 1, 10);
    }
}