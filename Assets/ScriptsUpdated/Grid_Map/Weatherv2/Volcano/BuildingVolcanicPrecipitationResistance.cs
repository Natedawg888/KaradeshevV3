using UnityEngine;

[DisallowMultipleComponent]
public class BuildingVolcanicPrecipitationResistance : MonoBehaviour
{
    [Header("Acid Rain Resistance")]
    public bool acidRainImmune = false;

    [Tooltip("1 = normal damage, 0.5 = half, 0 = none, 2 = double.")]
    [Min(0f)] public float acidRainDamageMultiplier = 1f;

    [Tooltip("Negative reduces damage, positive adds damage before multiplier.")]
    public int acidRainFlatDamageOffset = 0;

    [Header("Ash Fall Resistance")]
    public bool ashFallImmune = false;

    [Tooltip("1 = normal damage, 0.5 = half, 0 = none, 2 = double.")]
    [Min(0f)] public float ashFallDamageMultiplier = 1f;

    [Tooltip("Negative reduces damage, positive adds damage before multiplier.")]
    public int ashFallFlatDamageOffset = 0;

    [Header("Secondary Effect Resistance")]
    [Tooltip("Scales shelter/crafting/production/training/storage side effects from acid rain.")]
    [Min(0f)] public float acidRainSecondaryEffectMultiplier = 1f;

    [Tooltip("Scales shelter/crafting/production/training/storage side effects from ash fall.")]
    [Min(0f)] public float ashFallSecondaryEffectMultiplier = 1f;

    [Header("Debug")]
    public bool debugLogging = false;

    public bool IsImmuneTo(RainSimulationSystem.RainVisualKind kind)
    {
        switch (kind)
        {
            case RainSimulationSystem.RainVisualKind.AcidRain:
                return acidRainImmune;

            case RainSimulationSystem.RainVisualKind.AshFall:
                return ashFallImmune;

            default:
                return false;
        }
    }

    public int ModifyDamage(RainSimulationSystem.RainVisualKind kind, int baseDamage)
    {
        baseDamage = Mathf.Max(0, baseDamage);

        switch (kind)
        {
            case RainSimulationSystem.RainVisualKind.AcidRain:
                {
                    if (acidRainImmune)
                        return 0;

                    int adjusted = Mathf.Max(0, baseDamage + acidRainFlatDamageOffset);
                    return Mathf.Max(0, Mathf.RoundToInt(adjusted * Mathf.Max(0f, acidRainDamageMultiplier)));
                }

            case RainSimulationSystem.RainVisualKind.AshFall:
                {
                    if (ashFallImmune)
                        return 0;

                    int adjusted = Mathf.Max(0, baseDamage + ashFallFlatDamageOffset);
                    return Mathf.Max(0, Mathf.RoundToInt(adjusted * Mathf.Max(0f, ashFallDamageMultiplier)));
                }

            default:
                return baseDamage;
        }
    }

    public float ModifySecondarySeverity(
        RainSimulationSystem.RainVisualKind kind,
        float baseSeverity01)
    {
        baseSeverity01 = Mathf.Clamp01(baseSeverity01);

        switch (kind)
        {
            case RainSimulationSystem.RainVisualKind.AcidRain:
                if (acidRainImmune)
                    return 0f;

                return Mathf.Clamp01(baseSeverity01 * Mathf.Max(0f, acidRainSecondaryEffectMultiplier));

            case RainSimulationSystem.RainVisualKind.AshFall:
                if (ashFallImmune)
                    return 0f;

                return Mathf.Clamp01(baseSeverity01 * Mathf.Max(0f, ashFallSecondaryEffectMultiplier));

            default:
                return baseSeverity01;
        }
    }
}