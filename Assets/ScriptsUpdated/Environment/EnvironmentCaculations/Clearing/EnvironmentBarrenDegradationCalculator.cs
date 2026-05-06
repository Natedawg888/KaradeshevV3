using System.Collections.Generic;
using UnityEngine;

public static class EnvironmentBarrenDegradationCalculator
{
    // How harsh each environment is for degradation / overuse.
    private static readonly Dictionary<EnvironmentType, float> EnvIncreaseMods = new()
    {
        { EnvironmentType.Desert,          1.3f },
        { EnvironmentType.Grassland,       0.8f },
        { EnvironmentType.Savanna,         1.0f },

        // Forests
        { EnvironmentType.TemperateForest, 1.0f }, // old Forest/Pine/Birch-ish
        { EnvironmentType.BorealForest,    1.1f }, // old DeepForest/DeepPine-ish
        { EnvironmentType.TropicalForest,  1.2f }, // old Jungle

        // Wet / dense
        { EnvironmentType.SubTropical,     1.2f }, // old Swamp-ish
        { EnvironmentType.Lake,            1.1f }, // old Marshland-ish

        // Cold / rugged
        { EnvironmentType.Tundra,          1.1f },
        { EnvironmentType.Mountain,        1.2f },

        // Water biomes
        { EnvironmentType.Ocean,           1.3f },

        { EnvironmentType.SaltLake,           0.75f },
    };

    // For thresholds we keep a very similar mapping (you can tweak independently if needed).
    private static readonly Dictionary<EnvironmentType, float> EnvThresholdMods = new()
    {
        { EnvironmentType.Desert,          1.2f },
        { EnvironmentType.Grassland,       0.9f },
        { EnvironmentType.Savanna,         1.0f },

        // Forests
        { EnvironmentType.TemperateForest, 1.0f },
        { EnvironmentType.BorealForest,    1.1f },
        { EnvironmentType.TropicalForest,  1.1f },

        // Wet / dense
        { EnvironmentType.SubTropical,     1.1f },
        { EnvironmentType.Lake,            1.1f },

        // Cold / rugged
        { EnvironmentType.Tundra,          1.1f },
        { EnvironmentType.Mountain,        1.1f },

        // Water biomes
        { EnvironmentType.Ocean,           1.2f },

        { EnvironmentType.SaltLake,        1.25f },
    };

    // Tile difficulty modifiers (updated to new tile types)
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

        { EnvironmentTileType.SaltLake,        1.25f },
    };

    public static int CalculateRecoveryIncreasePerUse(
        EnvironmentType env,
        EnvironmentTileType tile,
        TileSize size)
    {
        // Base by size – small tiles degrade slower, huge tiles faster.
        float baseIncrease = size switch
        {
            TileSize.Tiny => 1f,
            TileSize.Small => 1f,
            TileSize.Medium => 2f,
            TileSize.Large => 3f,
            TileSize.Giant => 4f,
            TileSize.Massive => 5f,
            _ => 2f
        };

        float envMod = EnvIncreaseMods.TryGetValue(env, out var em) ? em : 1f;
        float tileMod = TileDifficultyMods.TryGetValue(tile, out var tm) ? tm : 1f;

        float raw = baseIncrease * envMod * tileMod;
        int result = Mathf.CeilToInt(raw);

        // Keep it in a sane range so it doesn't explode too fast
        return Mathf.Clamp(result, 1, 8);
    }

    public static int CalculateRecoveryClearThreshold(
        EnvironmentType env,
        EnvironmentTileType tile,
        TileSize size)
    {
        // Base threshold by size (how many turns of recovery is "too much")
        float baseThreshold = size switch
        {
            TileSize.Tiny => 10f,
            TileSize.Small => 15f,
            TileSize.Medium => 25f,
            TileSize.Large => 35f,
            TileSize.Giant => 45f,
            TileSize.Massive => 55f,
            _ => 30f
        };

        float envMod = EnvThresholdMods.TryGetValue(env, out var em) ? em : 1f;
        float tileMod = TileDifficultyMods.TryGetValue(tile, out var tm) ? tm : 1f;

        float raw = baseThreshold * envMod * tileMod;
        int result = Mathf.RoundToInt(raw);

        // Reasonable bounds so it's not silly low or crazy high
        return Mathf.Clamp(result, 8, 80);
    }
}