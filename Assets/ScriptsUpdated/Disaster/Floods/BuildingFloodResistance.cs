using UnityEngine;

[DisallowMultipleComponent]
public class BuildingFloodResistance : MonoBehaviour
{
    [Header("Flood Resistance")]
    public bool floodImmune = false;

    [Tooltip("1 = normal damage, 0.5 = half damage, 2 = double damage.")]
    [Min(0f)] public float damageMultiplier = 1f;

    [Tooltip("Flat damage adjustment before multiplier. Negative reduces damage.")]
    public int flatDamageModifier = 0;

    [Tooltip("If true, this object receives flood hit messages when hit by flooding.")]
    public bool receiveFloodMessages = true;

    [Header("Depth Scaling")]
    [Tooltip("Extra damage scaling based on flood depth. 1 = normal.")]
    [Min(0f)] public float depthDamageMultiplier = 1f;

    [Tooltip("If true, shallow floods do less damage before the final resistance multiplier.")]
    public bool scaleDamageByDepth = true;

    [Tooltip("Minimum damage scale when flood depth is very shallow.")]
    [Range(0f, 1f)] public float minimumLowDepthDamageScale = 0.15f;

    public int ModifyDamage(int baseDamage, float depth01)
    {
        if (floodImmune)
            return 0;

        float scaledDamage = baseDamage;

        if (scaleDamageByDepth)
        {
            float depthScale = Mathf.Lerp(
                minimumLowDepthDamageScale,
                1f,
                Mathf.Clamp01(depth01));

            scaledDamage *= depthScale;
        }

        scaledDamage *= Mathf.Max(0f, depthDamageMultiplier);

        int modified = Mathf.RoundToInt((scaledDamage + flatDamageModifier) * damageMultiplier);
        return Mathf.Max(0, modified);
    }
}