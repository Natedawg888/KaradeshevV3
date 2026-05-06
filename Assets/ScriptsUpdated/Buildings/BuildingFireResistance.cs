using UnityEngine;

[DisallowMultipleComponent]
public class BuildingFireResistance : MonoBehaviour
{
    [Header("Fire Protection")]
    public bool fireImmune = false;

    [Tooltip("1 = normal ignition chance, 0.5 = half chance, 0 = cannot ignite, 2 = double chance.")]
    [Min(0f)] public float fireIgnitionChanceMultiplier = 1f;

    [Tooltip("1 = normal damage, 0.5 = half damage, 0 = no damage, 1.5 = 50% extra damage, 2 = double damage.")]
    [Min(0f)] public float fireDamageMultiplier = 1f;

    [Tooltip("Negative reduces damage, positive adds damage before multiplier.")]
    public int fireFlatDamageOffset = 0;

    [Tooltip("1 = normal burn duration, 0.5 = shorter burn, 2 = burns twice as long.")]
    [Min(0f)] public float fireBurnDurationMultiplier = 1f;

    public float ModifyFireIgnitionChance(float incomingChance01)
    {
        if (fireImmune || incomingChance01 <= 0f)
            return 0f;

        float finalChance = Mathf.Clamp01(incomingChance01 * fireIgnitionChanceMultiplier);
        return finalChance;
    }

    public int ModifyFireDamage(int incomingDamage)
    {
        if (fireImmune || incomingDamage <= 0)
            return 0;

        int modifiedBaseDamage = Mathf.Max(0, incomingDamage + fireFlatDamageOffset);
        int finalDamage = Mathf.RoundToInt(modifiedBaseDamage * fireDamageMultiplier);

        return Mathf.Max(0, finalDamage);
    }

    public int ModifyFireBurnTurns(int incomingTurns)
    {
        if (fireImmune || incomingTurns <= 0)
            return 0;

        int finalTurns = Mathf.RoundToInt(incomingTurns * fireBurnDurationMultiplier);
        return Mathf.Max(1, finalTurns);
    }
}