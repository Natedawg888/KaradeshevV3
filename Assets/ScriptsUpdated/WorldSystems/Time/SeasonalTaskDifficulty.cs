using UnityEngine;

public static class SeasonalTaskDifficulty
{
    private enum LegacySeasonBucket
    {
        Spring,
        Summer,
        Autumn,
        Winter
    }

    public static void Apply(TaskFailureType type, ref int turns, ref float failChance)
    {
        var presetMgr = EnvironmentPresetManager.Instance;
        var preset = presetMgr != null ? presetMgr.GetCurrentPreset() : null;
        var section = preset != null ? preset.planetarySection : null;

        if (section == null || !section.useSeasonalTaskDifficulty)
            return;

        SeasonDefinition season = SeasonManager.Instance != null
            ? SeasonManager.Instance.CurrentSeason
            : null;

        LegacySeasonBucket bucket = ResolveSeasonBucket(season);

        float failAdd = bucket switch
        {
            LegacySeasonBucket.Spring => section.springFailAdd,
            LegacySeasonBucket.Summer => section.summerFailAdd,
            LegacySeasonBucket.Autumn => section.autumnFailAdd,
            LegacySeasonBucket.Winter => section.winterFailAdd,
            _ => 0f
        };

        float turnsMult = bucket switch
        {
            LegacySeasonBucket.Spring => section.springTurnsMult,
            LegacySeasonBucket.Summer => section.summerTurnsMult,
            LegacySeasonBucket.Autumn => section.autumnTurnsMult,
            LegacySeasonBucket.Winter => section.winterTurnsMult,
            _ => 1f
        };

        turns = Mathf.Max(1, Mathf.CeilToInt(turns * Mathf.Max(0.01f, turnsMult)));
        failChance = Mathf.Clamp(failChance + failAdd, 0f, 100f);
    }

    private static LegacySeasonBucket ResolveSeasonBucket(SeasonDefinition season)
    {
        if (season == null)
            return LegacySeasonBucket.Spring;

        switch (season.visualType)
        {
            case SeasonVisualType.Spring:
                return LegacySeasonBucket.Spring;

            case SeasonVisualType.Summer:
            case SeasonVisualType.Dry:
            case SeasonVisualType.Heatwave:
                return LegacySeasonBucket.Summer;

            case SeasonVisualType.Autumn:
            case SeasonVisualType.Wet:
            case SeasonVisualType.Rainy:
            case SeasonVisualType.Monsoon:
            case SeasonVisualType.Storm:
                return LegacySeasonBucket.Autumn;

            case SeasonVisualType.Winter:
            case SeasonVisualType.Frozen:
            case SeasonVisualType.ColdSnap:
                return LegacySeasonBucket.Winter;
        }

        string token = NormalizeSeasonToken(!string.IsNullOrWhiteSpace(season.seasonID)
            ? season.seasonID
            : season.displayName);

        switch (token)
        {
            case "spring":
                return LegacySeasonBucket.Spring;

            case "summer":
            case "dry":
            case "heatwave":
                return LegacySeasonBucket.Summer;

            case "autumn":
            case "fall":
            case "wet":
            case "rainy":
            case "rainseason":
            case "monsoon":
            case "storm":
                return LegacySeasonBucket.Autumn;

            case "winter":
            case "frozen":
            case "deepsinter":
            case "coldsnap":
                return LegacySeasonBucket.Winter;

            default:
                return LegacySeasonBucket.Spring;
        }
    }

    private static string NormalizeSeasonToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        value = value.Trim().ToLowerInvariant();
        value = value.Replace(" ", "");
        value = value.Replace("_", "");
        value = value.Replace("-", "");
        return value;
    }
}