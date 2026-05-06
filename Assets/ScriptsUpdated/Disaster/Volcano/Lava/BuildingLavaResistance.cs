using UnityEngine;

[DisallowMultipleComponent]
public class BuildingLavaResistance : MonoBehaviour
{
    [Header("Lava Protection")]
    [Tooltip("If true, this building ignores direct lava damage and lava secondary effects.")]
    public bool lavaImmune = false;

    [Tooltip("1 = normal lava damage, 0.5 = half damage, 0 = no damage, 2 = double damage.")]
    [Min(0f)] public float lavaDamageMultiplier = 1f;

    [Tooltip("Negative reduces damage, positive adds damage before multiplier.")]
    public int lavaFlatDamageOffset = 0;

    [Header("Lava Fire Ignition")]
    [Tooltip("If true, lava can ignite this building's BuildingFireState.")]
    public bool lavaCanIgniteFire = true;

    [Tooltip("1 = normal ignition chance, 0.5 = half chance, 0 = never ignite, 2 = double chance.")]
    [Min(0f)] public float lavaFireIgnitionChanceMultiplier = 1f;

    [Tooltip("1 = normal burn duration, 0.5 = shorter burn, 2 = longer burn.")]
    [Min(0f)] public float lavaFireBurnTurnsMultiplier = 1f;

    [Header("Secondary Effect Resistance")]
    [Tooltip("Scales shelter/crafting/production/training/storage lava side effects.")]
    [Min(0f)] public float lavaSecondaryEffectMultiplier = 1f;

    [Header("Debug")]
    public bool debugLogging = false;

    public int ModifyLavaDamage(int baseDamage)
    {
        if (lavaImmune)
            return 0;

        int adjusted = Mathf.Max(0, baseDamage + lavaFlatDamageOffset);
        adjusted = Mathf.RoundToInt(adjusted * Mathf.Max(0f, lavaDamageMultiplier));

        return Mathf.Max(0, adjusted);
    }

    public float ModifyIgnitionChance(float baseChance01)
    {
        if (lavaImmune || !lavaCanIgniteFire)
            return 0f;

        return Mathf.Clamp01(baseChance01 * Mathf.Max(0f, lavaFireIgnitionChanceMultiplier));
    }

    public int ModifyBurnTurns(int baseBurnTurns)
    {
        if (lavaImmune || !lavaCanIgniteFire)
            return 0;

        return Mathf.Max(0, Mathf.RoundToInt(baseBurnTurns * Mathf.Max(0f, lavaFireBurnTurnsMultiplier)));
    }

    public float ModifySecondarySeverity(float baseSeverity01)
    {
        if (lavaImmune)
            return 0f;

        return Mathf.Clamp01(baseSeverity01 * Mathf.Max(0f, lavaSecondaryEffectMultiplier));
    }
}