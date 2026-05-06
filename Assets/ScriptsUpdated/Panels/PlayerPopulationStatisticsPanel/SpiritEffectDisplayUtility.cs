using UnityEngine;

public static class SpiritEffectDisplayUtility
{
    public static string GetMoodDisplayName(SpiritMoodState mood)
    {
        switch (mood)
        {
            case SpiritMoodState.Angry: return "Angry";
            case SpiritMoodState.Sad: return "Sad";
            case SpiritMoodState.Pleased: return "Pleased";
            case SpiritMoodState.Exalted: return "Exalted";
            default: return "Neutral";
        }
    }

    public static string GetEffectDisplayName(SpiritEffectType type)
    {
        switch (type)
        {
            case SpiritEffectType.InventorySpoilageRateMultiplier: return "Spoilage Rate";
            case SpiritEffectType.TileBarrenChanceAdd: return "Barren Chance";
            case SpiritEffectType.BirthSuccessChanceAdd: return "Birth Success";
            case SpiritEffectType.TwinChanceAdd: return "Twin Chance";
            case SpiritEffectType.TripletChanceAdd: return "Triplet Chance";
            case SpiritEffectType.UnitAttackMultiplier: return "Unit Attack";
            case SpiritEffectType.UnitDefenseMultiplier: return "Unit Defense";
            case SpiritEffectType.UnitAccuracyMultiplier: return "Unit Accuracy";
            case SpiritEffectType.UnitMovementMultiplier: return "Unit Movement";
            case SpiritEffectType.ProductionOutputMultiplier: return "Production Output";
            case SpiritEffectType.CraftingOutputMultiplier: return "Crafting Output";
            case SpiritEffectType.PopulationRecoveryRateMultiplier: return "Recovery Rate";
            case SpiritEffectType.PopulationResistanceAdd: return "Population Resistance";
            default: return type.ToString();
        }
    }

    public static string GetEffectDescription(SpiritEffectType type, SpiritModifierMode mode)
    {
        switch (type)
        {
            case SpiritEffectType.InventorySpoilageRateMultiplier:
                return "Changes how quickly stored items spoil.";
            case SpiritEffectType.TileBarrenChanceAdd:
                return "Changes the chance for discovered resource nodes to become barren.";
            case SpiritEffectType.BirthSuccessChanceAdd:
                return "Changes the chance of successful births.";
            case SpiritEffectType.TwinChanceAdd:
                return "Changes the chance of twins.";
            case SpiritEffectType.TripletChanceAdd:
                return "Changes the chance of triplets.";
            case SpiritEffectType.UnitAttackMultiplier:
                return "Changes militia attack strength.";
            case SpiritEffectType.UnitDefenseMultiplier:
                return "Changes militia defense strength.";
            case SpiritEffectType.UnitAccuracyMultiplier:
                return "Changes militia hit chance.";
            case SpiritEffectType.UnitMovementMultiplier:
                return "Changes militia movement effectiveness.";
            case SpiritEffectType.ProductionOutputMultiplier:
                return "Changes production output.";
            case SpiritEffectType.CraftingOutputMultiplier:
                return "Changes crafting output.";
            case SpiritEffectType.PopulationRecoveryRateMultiplier:
                return "Changes how quickly population recovers health.";
            case SpiritEffectType.PopulationResistanceAdd:
                return "Adds flat resistance to population health-related systems.";
            default:
                return mode == SpiritModifierMode.Multiplier
                    ? "Multiplier effect."
                    : "Additive effect.";
        }
    }

    public static string GetEffectValueText(SpiritEffectEntry entry, SpiritMoodState mood)
    {
        if (entry == null)
            return string.Empty;

        float value = entry.GetValue(mood);

        if (entry.modifierMode == SpiritModifierMode.Multiplier)
        {
            float deltaPercent = (value - 1f) * 100f;
            return $"{deltaPercent:+0.#;-0.#;0}%";
        }

        if (UsesPercentAdditiveDisplay(entry.effectType))
        {
            float pct = value * 100f;
            return $"{pct:+0.#;-0.#;0}%";
        }

        if (Mathf.Approximately(value, Mathf.Round(value)))
            return $"{Mathf.RoundToInt(value):+0;-0;0}";

        return $"{value:+0.##;-0.##;0}";
    }

    public static string GetCombinedEffectText(SpiritEffectEntry entry, SpiritMoodState mood)
    {
        if (entry == null)
            return string.Empty;

        string effectName = GetEffectDisplayName(entry.effectType);
        string effectValue = GetEffectValueText(entry, mood);
        string effectDescription = GetEffectDescription(entry.effectType, entry.modifierMode);
        string moodName = GetMoodDisplayName(mood);

        return $"{effectName} ({effectValue})\n{effectDescription}\nMood: {moodName}";
    }

    private static bool UsesPercentAdditiveDisplay(SpiritEffectType type)
    {
        switch (type)
        {
            case SpiritEffectType.TileBarrenChanceAdd:
            case SpiritEffectType.BirthSuccessChanceAdd:
            case SpiritEffectType.TwinChanceAdd:
            case SpiritEffectType.TripletChanceAdd:
                return true;
            default:
                return false;
        }
    }
}