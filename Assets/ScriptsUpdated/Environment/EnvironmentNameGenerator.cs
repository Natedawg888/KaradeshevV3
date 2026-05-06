using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class EnvironmentNameGenerator
{
    // Preset combination names (you can expand this as needed)
    private static readonly Dictionary<(EnvironmentType, EnvironmentTileType), List<string>> combinationPresets
        = new()
    {
        {
            // Old: (Savanna, Cliff) -> New: (Savanna, Mountain)
            (EnvironmentType.Savanna, EnvironmentTileType.Mountain),
            new List<string>
            {
                "Golden Bluff",
                "Sunbaked Crag",
                "Whispering Ridge",
                "Scorched Overlook",
                "Tallgrass Escarpment",
                "Dusty Precipice",
                "Windblown Edge",
                "Burnt Spire",
                "Fertile Outcrop",
                "Dryview Heights"
            }
        },
        // Example: add more combos here
        // { (EnvironmentType.TemperateForest, EnvironmentTileType.Land), new List<string>{ "Mossy Vale", "Green Hollow", ... } }
    };

    // Descriptors by environment type (NEW enums)
    private static readonly Dictionary<EnvironmentType, string[]> envDescriptors = new()
    {
        { EnvironmentType.Desert, new[] { "Barren", "Shimmering", "Searing", "Parched", "Bleached", "Drifting", "Heatwave", "Cracked", "Golden", "Windblown" } },

        // TemperateForest = Birch/Forest/Pine vibe combined
        { EnvironmentType.TemperateForest, new[] { "Silver", "Pale", "Whispering", "Leafy", "Dappled", "Breezy", "Tranquil", "Glimmering", "Mossy", "Emerald" } },

        // BorealForest = DeepForest/DeepPine vibe combined
        { EnvironmentType.BorealForest, new[] { "Towering", "Oldgrowth", "Shadowed", "Darkpine", "Coldneedle", "Frozen", "Silentwood", "Primeval", "Twilight", "Ancient" } },

        { EnvironmentType.TropicalForest, new[] { "Lush", "Vivid", "Steamy", "Tangled", "Wild", "Verdurous", "Dripping", "Choked", "Emergent", "Overgrown" } },

        { EnvironmentType.Grassland, new[] { "Rolling", "Sunlit", "Breezy", "Open", "Amber", "Softwind", "Sweeping", "Prairie", "Wide", "Golden" } },

        // Lake = Marshland-ish (reedy, misty, wetland)
        { EnvironmentType.Lake, new[] { "Misty", "Reedy", "Stillwater", "Foggy", "Marshy", "Soggy", "Boggy", "Fen", "Moist", "Murk" } },

        { EnvironmentType.Savanna, new[] { "Dry", "Golden", "Tallgrass", "Sunbaked", "Windblown", "Dusty", "Wide", "Scorched", "Warm", "Sparse" } },

        // SubTropical = Swamp-ish (heavier, darker wetland vibe)
        { EnvironmentType.SubTropical, new[] { "Stagnant", "Muddy", "Smothered", "Sullen", "Murky", "Fogbound", "Reeking", "Drifting", "Rotten", "Gloom" } },

        // Ocean biome (old ShallowOcean bucket)
        { EnvironmentType.Ocean, new[] { "Vast", "Azure", "Salted", "Rolling", "Stormy", "Endless", "Briny", "Foaming", "Blue", "Tidebound" } },

        { EnvironmentType.Mountain, new[] { "Jagged", "High", "Windswept", "Rugged", "Stony", "Sheer", "Granite", "Cloudcut", "Skyborne", "Craggy" } },

        { EnvironmentType.Tundra, new[] { "Frosted", "Bleak", "Whitewind", "Icy", "Snowbound", "Cold", "Pale", "Silent", "Glazed", "Winter" } },
    };

    // Feature nouns by tile type (NEW enums)
    private static readonly Dictionary<EnvironmentTileType, string[]> tileFeatures = new()
    {
        { EnvironmentTileType.Land, new[] { "Plain", "Plateau", "Haven", "Hollow", "Rise", "Terrace", "Domain", "Field", "Basin", "Commons" } },

        { EnvironmentTileType.Coastline, new[] { "Shore", "Point", "Cove", "Headland", "Beach", "Bight", "Bay", "Spit", "Sound", "Bluff" } },
        { EnvironmentTileType.CoastlineCorner, new[] { "Cape", "Nook", "Bend", "Turn", "Jut", "Edge", "Fold", "Crook", "Inlet", "Cornerhead" } },

        { EnvironmentTileType.LakeEdge, new[] { "Bank", "Shore", "Fringe", "Rim", "Waterfront", "Edge", "Ledge", "Refuge", "Glen", "Harbor" } },
        { EnvironmentTileType.LakeCorner, new[] { "Curve", "Baylet", "Crescent", "Nook", "Point", "Shelf", "Quiet Cove", "Serene Bend", "Turn", "Basin Corner" } },
        { EnvironmentTileType.Lake, new[] { "Lake", "Waters", "Depths", "Mirror", "Still", "Blueglass", "Basin", "Pool", "Reach", "Expanse" } },

        { EnvironmentTileType.River, new[] { "Stream", "Flow", "Current", "Run", "Ribbon", "Waterway", "Channel", "Course", "Riverbed", "Swale" } },
        { EnvironmentTileType.RiverCorner, new[] { "Bend", "Turn", "Elbow", "Curve", "Twist", "Crook", "Corner Run", "Fork Bend", "Twirl", "S-Flow" } },
        { EnvironmentTileType.RiverSplit, new[] { "Fork", "Branch", "Y-Junction", "Split", "Twinflow", "Divergence", "Double Run", "Sprawl", "Dividing", "Deltalet" } },
        { EnvironmentTileType.RiverCross, new[] { "Crossing", "Intersect", "Junction", "Nexus", "Cross Run", "Merge Point", "Woven Stream", "Braided Flow", "X-Flow", "Confluence" } },
        { EnvironmentTileType.RiverEnd, new[] { "Terminus", "Still End", "Deadwater", "Final Flow", "Last Bend", "Quiet Exit", "Endwater", "Finish Run", "Cease", "Silent Mouth" } },

        { EnvironmentTileType.RiverMouth, new[] { "Mouth", "Outlet", "Delta", "Estuary", "Spill", "Exit", "Flowend", "Merge", "Opening", "Confluence" } },
        { EnvironmentTileType.LakeMouth, new[] { "Outlet", "Spillway", "Outflow", "Drain", "Breakwater", "Channel Mouth", "Watergate", "Runoff", "Passage", "Opening" } },

        { EnvironmentTileType.Water, new[] { "Pool", "Spring", "Well", "Basin", "Pond", "Stillwater", "Glass", "Ripple", "Watering Hole", "Oasis" } },

        { EnvironmentTileType.Ocean, new[] { "Expanse", "Tide", "Brine", "Bluefall", "Stretch", "Sea", "Abyss", "Waves", "Deep", "Trench" } },

        { EnvironmentTileType.Cave, new[] { "Hollow", "Grotto", "Sanctum", "Refuge", "Den", "Shelter", "Cavern", "Vault", "Burrow", "Haven" } },

        // Mountain tile replaces the entire old cliff family
        { EnvironmentTileType.Mountain, new[] { "Ridge", "Crag", "Bluff", "Escarpment", "Precipice", "Overlook", "Spire", "Peak", "Crest", "Pinnacle" } },
    };

    // Return up to `count` unique names for the combination.
    public static List<string> GetNames(EnvironmentType env, EnvironmentTileType tileType, int count)
    {
        var pool = BuildNamePool(env, tileType);

        // Ensure uniqueness and enough entries
        var result = new List<string>();
        if (pool.Count >= count)
        {
            result = pool.OrderBy(_ => UnityEngine.Random.value).Take(count).ToList();
        }
        else
        {
            result.AddRange(pool);
            int attempts = 0;
            while (result.Count < count && attempts < count * 5)
            {
                string extra = GenerateFallbackName(env, tileType);
                if (!result.Contains(extra))
                    result.Add(extra);
                else
                    result.Add($"{extra} {UnityEngine.Random.Range(1, 100)}");
                attempts++;
            }
        }

        // Final safety padding
        while (result.Count < count)
            result.Add($"{env} {tileType} {result.Count + 1}");

        return result;
    }

    // Single random name
    public static string GetRandomName(EnvironmentType env, EnvironmentTileType tileType)
    {
        var names = GetNames(env, tileType, 1);
        return names.FirstOrDefault() ?? $"{env} {tileType}";
    }

    private static List<string> BuildNamePool(EnvironmentType env, EnvironmentTileType tileType)
    {
        // 1) If there's a preset list for this exact combo, use it as the seed pool.
        var pool = new HashSet<string>();
        if (combinationPresets.TryGetValue((env, tileType), out var presets) && presets != null)
        {
            for (int i = 0; i < presets.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(presets[i]))
                    pool.Add(presets[i].Trim());
            }
        }

        // 2) Fallback generation from descriptors/features
        string descriptor = GetRandomFrom(envDescriptors, env) ?? env.ToString();
        string feature = GetRandomFrom(tileFeatures, tileType) ?? tileType.ToString();

        // Basic templates
        pool.Add($"{descriptor} {feature}");
        pool.Add($"{feature} of the {descriptor}");
        pool.Add($"{descriptor} {tileType}");
        pool.Add($"{tileType} of {descriptor}");
        pool.Add($"The {descriptor} {feature}");
        pool.Add($"{feature} Ridge");
        pool.Add($"{descriptor} Haven");
        pool.Add($"{feature} Hollow");
        pool.Add($"{descriptor} Expanse");
        pool.Add($"{feature} Point");

        // Mix in variants with environment name itself
        pool.Add($"{env} {feature}");
        pool.Add($"{feature} of {env}");
        pool.Add($"{descriptor} {feature} Heights");
        pool.Add($"{feature} Bluff");
        pool.Add($"{descriptor} {feature} Basin");

        return pool.Take(30).ToList(); // trim some to keep variety
    }

    private static string GenerateFallbackName(EnvironmentType env, EnvironmentTileType tileType)
    {
        string descriptor = GetRandomFrom(envDescriptors, env) ?? env.ToString();
        string feature = GetRandomFrom(tileFeatures, tileType) ?? tileType.ToString();

        int format = UnityEngine.Random.Range(0, 4);
        return format switch
        {
            0 => $"{descriptor} {feature}",
            1 => $"{feature} of the {descriptor}",
            2 => $"{env} {feature}",
            _ => $"{feature} Basin"
        };
    }

    private static string GetRandomFrom<T>(Dictionary<T, string[]> dict, T key)
    {
        if (dict.TryGetValue(key, out var arr) && arr != null && arr.Length > 0)
            return arr[UnityEngine.Random.Range(0, arr.Length)];
        return null;
    }

    // Example utility: get all combos with N names each
    public static Dictionary<(EnvironmentType, EnvironmentTileType), List<string>> GetAllCombos(int namesPerCombo = 10)
    {
        var result = new Dictionary<(EnvironmentType, EnvironmentTileType), List<string>>();
        foreach (EnvironmentType env in Enum.GetValues(typeof(EnvironmentType)))
        {
            foreach (EnvironmentTileType tile in Enum.GetValues(typeof(EnvironmentTileType)))
            {
                var names = GetNames(env, tile, namesPerCombo);
                result[(env, tile)] = names;
            }
        }
        return result;
    }
}