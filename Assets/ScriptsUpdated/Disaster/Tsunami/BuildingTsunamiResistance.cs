using UnityEngine;

[DisallowMultipleComponent]
public class BuildingTsunamiResistance : MonoBehaviour
{
    [Header("Tsunami Resistance")]
    public bool tsunamiImmune = false;

    [Tooltip("1 = normal damage, 0.5 = half damage, 2 = double damage.")]
    [Min(0f)] public float damageMultiplier = 1f;

    [Tooltip("Flat damage adjustment before multiplier. Negative reduces damage.")]
    public int flatDamageModifier = 0;

    [Tooltip("If true, this object receives tsunami hit messages when hit by a wave.")]
    public bool receiveTsunamiMessages = true;

    [Header("Energy Scaling")]
    [Tooltip("Extra damage scaling based on wave energy. 1 = normal.")]
    [Min(0f)] public float energyDamageMultiplier = 1f;

    [Tooltip("If true, low-energy waves do less damage before the final resistance multiplier.")]
    public bool scaleDamageByEnergy = true;

    [Tooltip("Minimum damage scale when wave energy is almost gone.")]
    [Range(0f, 1f)] public float minimumLowEnergyDamageScale = 0.25f;

    public int ModifyDamage(int baseDamage, float energy01)
    {
        if (tsunamiImmune)
            return 0;

        float scaledDamage = baseDamage;

        if (scaleDamageByEnergy)
        {
            float energyScale = Mathf.Lerp(
                minimumLowEnergyDamageScale,
                1f,
                Mathf.Clamp01(energy01));

            scaledDamage *= energyScale;
        }

        scaledDamage *= Mathf.Max(0f, energyDamageMultiplier);

        int modified = Mathf.RoundToInt((scaledDamage + flatDamageModifier) * damageMultiplier);
        return Mathf.Max(0, modified);
    }
}