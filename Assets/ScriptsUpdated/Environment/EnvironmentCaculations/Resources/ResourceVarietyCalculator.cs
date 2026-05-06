using UnityEngine;
using System.Collections.Generic;

public static class ResourceVarietyCalculator
{
    // Example size modifier: larger tiles can support more variety
    private static float SizeFactor(TileSize size)
    {
        return size switch
        {
            TileSize.Tiny => 0.5f,
            TileSize.Small => 0.75f,
            TileSize.Medium => 1f,
            TileSize.Large => 1.25f,
            TileSize.Giant => 1.5f,
            TileSize.Massive => 1.75f,
            _ => 1f,
        };
    }

    // Example environment richness modifier (you can tune these per your game's design)
    private static float EnvironmentRichness(EnvironmentType env)
    {
        return env switch
        {
            EnvironmentType.Grassland => 1.2f,
            EnvironmentType.Savanna => 1.1f,

            // Forests
            EnvironmentType.TemperateForest => 1.3f, // old Forest-ish average
            EnvironmentType.BorealForest => 1.4f, // old DeepForest-ish / DeepPine-ish
            EnvironmentType.TropicalForest => 1.5f, // old Jungle

            // Wetlands / water-ish
            EnvironmentType.Lake => 1.0f, // old Marshland
            EnvironmentType.SubTropical => 0.9f, // old Swamp

            // Harsh biomes
            EnvironmentType.Desert => 0.6f,
            EnvironmentType.Tundra => 0.8f,
            EnvironmentType.Mountain => 0.8f,

            // Open ocean (low "resource variety" unless you treat marine as rich)
            EnvironmentType.Ocean => 0.5f,

            EnvironmentType.SaltLake => 0.5f,

            _ => 1f,
        };
    }

    public static int CalculateVarietyLimit(EnvironmentType envType, TileSize size, int availableCandidateCount, int hardCap)
    {
        // Example: start with sqrt of available candidates scaled by environment and size
        float raw = Mathf.Sqrt(Mathf.Max(1, availableCandidateCount)) * EnvironmentRichness(envType) * SizeFactor(size);

        int variety = Mathf.CeilToInt(raw);
        variety = Mathf.Clamp(variety, 1, hardCap);
        variety = Mathf.Min(variety, availableCandidateCount); // can't exceed what's available
        return variety;
    }
}