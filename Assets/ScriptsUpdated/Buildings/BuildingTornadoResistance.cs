using UnityEngine;

[DisallowMultipleComponent]
public class BuildingTornadoResistance : MonoBehaviour
{
    [Header("Tornado Protection")]
    public bool tornadoImmune = false;

    [Tooltip("1 = normal damage, 0.5 = half damage, 0 = no damage, 1.5 = 50% extra damage, 2 = double damage.")]
    [Min(0f)] public float tornadoDamageMultiplier = 1f;

    [Tooltip("Negative reduces damage, positive adds damage before multiplier.")]
    public int tornadoFlatDamageOffset = 0;

    public int ModifyTornadoDamage(int incomingDamage)
    {
        if (tornadoImmune || incomingDamage <= 0)
            return 0;

        int modifiedBaseDamage = Mathf.Max(0, incomingDamage + tornadoFlatDamageOffset);
        int finalDamage = Mathf.RoundToInt(modifiedBaseDamage * tornadoDamageMultiplier);

        return Mathf.Max(0, finalDamage);
    }
}