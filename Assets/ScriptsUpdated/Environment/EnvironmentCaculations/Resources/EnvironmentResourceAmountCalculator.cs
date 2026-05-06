using UnityEngine;
using System.Collections.Generic;

public static class EnvironmentResourceAmountCalculator
{
    // Example abundance modifiers per environment type.
    private static readonly Dictionary<EnvironmentType, float> environmentModifiers = new()
    {
        { EnvironmentType.Desert,          0.5f },

        // Forests
        { EnvironmentType.TemperateForest, 1.1f }, // old Forest-ish avg
        { EnvironmentType.BorealForest,    1.2f }, // old DeepForest-ish
        { EnvironmentType.TropicalForest,  1.3f }, // old Jungle

        { EnvironmentType.Grassland,       1.2f },
        { EnvironmentType.Savanna,         0.9f },

        // Wet / water-ish
        { EnvironmentType.Lake,            1.0f }, // old Marshland
        { EnvironmentType.SubTropical,     1.0f }, // old Swamp

        // Cold / rugged
        { EnvironmentType.Tundra,          0.8f },
        { EnvironmentType.Mountain,        0.8f },

        // Sea
        { EnvironmentType.Ocean,           0.7f }, // old ShallowOcean

        { EnvironmentType.SaltLake,           0.75f },
    };

    // Example modifiers per tile type.
    private static readonly Dictionary<EnvironmentTileType, float> tileModifiers = new()
    {
        { EnvironmentTileType.Land,             1f },
        { EnvironmentTileType.Coastline,        1f },
        { EnvironmentTileType.CoastlineCorner,  1f },

        { EnvironmentTileType.LakeEdge,         1.1f },
        { EnvironmentTileType.LakeCorner,       1.1f },

        // Rough terrain (replaces cliff family)
        { EnvironmentTileType.Mountain,         0.85f },

        // Rivers
        { EnvironmentTileType.River,            1.2f },
        { EnvironmentTileType.RiverCorner,      1.1f },
        { EnvironmentTileType.RiverSplit,       1.0f },
        { EnvironmentTileType.RiverMouth,       1.2f },
        { EnvironmentTileType.LakeMouth,        1.2f },
        { EnvironmentTileType.RiverCross,       1.3f },
        { EnvironmentTileType.RiverEnd,         1.0f },

        // Water / lake
        { EnvironmentTileType.Water,            1.1f },
        { EnvironmentTileType.Lake,             1.05f },

        // Special
        { EnvironmentTileType.Cave,             1.5f },

        // Ocean tile
        { EnvironmentTileType.Ocean,            0.6f },

        { EnvironmentTileType.SaltLake,            1.25f },
    };

    public static float GetEnvironmentModifier(EnvironmentType envType)
    {
        return environmentModifiers.TryGetValue(envType, out var m) ? m : 1f;
    }

    public static float GetTileModifier(EnvironmentTileType tileType)
    {
        return tileModifiers.TryGetValue(tileType, out var m) ? m : 1f;
    }
}