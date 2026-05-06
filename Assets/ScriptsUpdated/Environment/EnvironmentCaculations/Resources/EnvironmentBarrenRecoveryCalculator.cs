using System.Collections.Generic;
using UnityEngine;

public static class EnvironmentBarrenRecoveryCalculator
{
    // How quickly environments recover from being barren.
    // < 1 = faster recovery, > 1 = slower recovery.
    private static readonly Dictionary<EnvironmentType, float> environmentRecoveryFactor = new()
    {
        { EnvironmentType.Desert,          1.6f },

        // Forests
        { EnvironmentType.TemperateForest, 0.9f }, // old Forest-ish
        { EnvironmentType.BorealForest,    0.85f }, // old DeepForest-ish
        { EnvironmentType.TropicalForest,  0.7f }, // old Jungle

        { EnvironmentType.Grassland,       0.8f },
        { EnvironmentType.Savanna,         1.2f },

        // Wet / slow to "un-barren" depending on your design
        { EnvironmentType.Lake,            1.1f }, // old Marshland
        { EnvironmentType.SubTropical,     1.3f }, // old Swamp

        // Cold / rugged
        { EnvironmentType.Tundra,          1.4f },
        { EnvironmentType.Mountain,        1.5f },

        // Sea
        { EnvironmentType.Ocean,           1.5f }, // old ShallowOcean

        { EnvironmentType.SaltLake,        1.25f },
    };

    // Tile-based factors (eg. river tiles recover a bit faster, mountains slower, etc.)
    private static readonly Dictionary<EnvironmentTileType, float> tileRecoveryFactor = new()
    {
        { EnvironmentTileType.Land,             1.0f },

        { EnvironmentTileType.Coastline,        1.1f },
        { EnvironmentTileType.CoastlineCorner,  1.1f },

        { EnvironmentTileType.LakeEdge,         0.9f },
        { EnvironmentTileType.LakeCorner,       0.9f },

        // Rough terrain (replaces cliff family)
        { EnvironmentTileType.Mountain,         1.25f },

        // Rivers
        { EnvironmentTileType.River,            0.8f },
        { EnvironmentTileType.RiverCorner,      0.9f },
        { EnvironmentTileType.RiverSplit,       0.9f },
        { EnvironmentTileType.RiverCross,       0.8f },
        { EnvironmentTileType.RiverEnd,         1.0f },

        // Mouths
        { EnvironmentTileType.RiverMouth,       0.9f },
        { EnvironmentTileType.LakeMouth,        0.9f },

        // Water / lake / ocean
        { EnvironmentTileType.Water,            0.9f },
        { EnvironmentTileType.Lake,             1.0f },
        { EnvironmentTileType.Ocean,            1.4f },

        // Specials
        { EnvironmentTileType.Cave,             1.4f },

        { EnvironmentTileType.SaltLake,             1.15f },
    };

    // Optional: size factor if you want bigger tiles to take longer to recover.
    private static readonly Dictionary<TileSize, float> sizeRecoveryFactor = new()
    {
        { TileSize.Tiny,    0.4f },
        { TileSize.Small,   0.9f },
        { TileSize.Medium,  1.0f },
        { TileSize.Large,   1.2f },
        { TileSize.Giant,   1.5f },
        { TileSize.Massive, 1.9f },
    };

    public static int CalculateBarrenRecoveryTurns(
        EnvironmentType envType,
        EnvironmentTileType tileType,
        TileSize size)
    {
        float baseTurns = 8f;

        float envFactor = environmentRecoveryFactor.TryGetValue(envType, out var e) ? e : 1f;
        float tileFactor = tileRecoveryFactor.TryGetValue(tileType, out var t) ? t : 1f;
        float sizeFactor = sizeRecoveryFactor.TryGetValue(size, out var s) ? s : 1f;

        float result = baseTurns * envFactor * tileFactor * sizeFactor;

        int turns = Mathf.RoundToInt(result);
        return Mathf.Max(1, turns);
    }
}