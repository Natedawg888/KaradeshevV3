using UnityEngine;

[DisallowMultipleComponent]
public class BuildingEarthquakeResistance : MonoBehaviour
{
    [Header("Earthquake Resistance")]
    public bool earthquakeImmune = false;

    [Tooltip("1 = normal damage, 0.5 = half damage, 2 = double damage.")]
    [Min(0f)] public float damageMultiplier = 1f;

    [Tooltip("Flat damage adjustment before multiplier. Negative reduces damage.")]
    public int flatDamageModifier = 0;

    [Tooltip("If true, this object receives SendMessage calls when hit by an earthquake.")]
    public bool receiveEarthquakeMessages = true;

    [Header("Earthquake Ignition")]
    [Tooltip("If false, this building will not ignite from earthquakes even if it has BuildingFireState.")]
    public bool canIgniteFromEarthquake = false;

    [Tooltip("1 = normal ignition chance, 0.5 = half chance, 2 = double chance.")]
    [Min(0f)] public float earthquakeIgnitionChanceMultiplier = 1f;

    [Tooltip("Flat chance added after base ignition chance. Example: 0.05 = +5%.")]
    [Range(-1f, 1f)] public float earthquakeIgnitionFlatChanceBonus = 0f;

    [Tooltip("1 = normal burn turns, 0.5 = shorter fire, 2 = longer fire.")]
    [Min(0f)] public float earthquakeBurnTurnMultiplier = 1f;

    [Tooltip("Flat burn turns added after multiplier. Negative reduces burn time.")]
    public int earthquakeBurnTurnFlatModifier = 0;

    public int ModifyDamage(int baseDamage)
    {
        if (earthquakeImmune)
            return 0;

        int modified = Mathf.RoundToInt((baseDamage + flatDamageModifier) * damageMultiplier);
        return Mathf.Max(0, modified);
    }

    public float ModifyEarthquakeIgnitionChance(float baseChance)
    {
        if (earthquakeImmune)
            return 0f;

        if (!canIgniteFromEarthquake)
            return 0f;

        float modified = (baseChance + earthquakeIgnitionFlatChanceBonus) * earthquakeIgnitionChanceMultiplier;
        return Mathf.Clamp01(modified);
    }

    public int ModifyEarthquakeBurnTurns(int baseBurnTurns)
    {
        if (earthquakeImmune)
            return 0;

        if (!canIgniteFromEarthquake)
            return 0;

        int modified = Mathf.RoundToInt(baseBurnTurns * earthquakeBurnTurnMultiplier);
        modified += earthquakeBurnTurnFlatModifier;

        return Mathf.Max(0, modified);
    }
}