using UnityEngine;
using System.Collections.Generic;

public static class EnvironmentVarietyConfig
{
    // Base richness / variety weight per environment. Higher means more variety propensity.
    private static readonly Dictionary<EnvironmentType, float> baseVarietyByEnvironment = new()
    {
        { EnvironmentType.Desert,          1.8f },

        // Forests
        { EnvironmentType.TemperateForest, 2.4f }, // Birch/Forest/Pine averaged
        { EnvironmentType.BorealForest,    2.5f }, // DeepForest/DeepPine averaged
        { EnvironmentType.TropicalForest,  2.8f }, // Jungle

        { EnvironmentType.Grassland,       2.3f },
        { EnvironmentType.Savanna,         2.0f },

        // Wet / dense
        { EnvironmentType.Lake,            2.0f }, // Marshland
        { EnvironmentType.SubTropical,     2.1f }, // Swamp

        // Water / harsh
        { EnvironmentType.Ocean,           1.6f }, // ShallowOcean

        // New biomes (sane defaults)
        { EnvironmentType.Mountain,        1.9f },
        { EnvironmentType.Tundra,          1.7f },

        { EnvironmentType.SaltLake,           1.5f },
    };

    // Size factor: bigger tiles tend to support more variety/spawn rate
    public static float SizeFactor(TileSize size)
    {
        return size switch
        {
            TileSize.Tiny => 1.5f,
            TileSize.Small => 1.8f,
            TileSize.Medium => 2f,
            TileSize.Large => 2.3f,
            TileSize.Giant => 2.7f,
            TileSize.Massive => 3.2f,
            _ => 1f,
        };
    }

    private static float GetBaseVariety(EnvironmentType envType)
    {
        return baseVarietyByEnvironment.TryGetValue(envType, out var v) ? v : 1f;
    }

    /// <summary>
    /// Randomized multiplier for variety spawn rate based on environment, size, and candidate count.
    /// </summary>
    public static float GetRandomSpawnRateMultiplier(EnvironmentType envType, TileSize size, int candidateCount)
    {
        float envBase = GetBaseVariety(envType);
        float sizeFactor = SizeFactor(size);

        // Candidate influence: more candidate definitions increases potential max
        float candidateInfluence = Mathf.Clamp(candidateCount / 4f, 0.5f, 2f);

        // Compute bounds
        float min = envBase * sizeFactor * 0.5f; // ensure some floor
        float max = envBase * sizeFactor * candidateInfluence;

        // Avoid zero span
        if (max < min) max = min;

        return Random.Range(min, max);
    }

    /// <summary>
    /// Randomized variety cap (number of distinct types) based on environment, size, candidate count, and inspector ceiling.
    /// </summary>
    public static int GetRandomVarietyCap(EnvironmentType envType, TileSize size, int candidateCount, int inspectorCap)
    {
        float envBase = GetBaseVariety(envType);
        float sizeFactor = SizeFactor(size);

        // Expected nominal cap before randomization
        int nominal = Mathf.CeilToInt(envBase * sizeFactor * 2f); // e.g., scales with richness and size

        // Upper bound is min of candidate count, inspector-specified cap, and nominal
        int upper = Mathf.Min(candidateCount, inspectorCap, nominal);
        upper = Mathf.Max(1, upper); // at least 1

        // Random cap between 1 and upper inclusive
        return Random.Range(1, upper + 1);
    }
}