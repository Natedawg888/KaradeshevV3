using UnityEngine;

[DisallowMultipleComponent]
public class BuildingVolcanicResistance : MonoBehaviour
{
    [Header("Volcanic Immunity")]
    public bool immuneToAsh = false;
    public bool immuneToAcidRain = false;

    [Header("Exposure Chance Multipliers")]
    [Tooltip("1 = normal chance, 0.5 = half chance, 0 = cannot be affected by ash exposure.")]
    [Min(0f)] public float ashExposureChanceMultiplier = 1f;

    [Tooltip("1 = normal chance, 0.5 = half chance, 0 = cannot be affected by acid-rain exposure.")]
    [Min(0f)] public float acidRainExposureChanceMultiplier = 1f;

    [Header("Health Loss Multipliers")]
    [Tooltip("1 = normal health loss, 0.5 = half health loss, 0 = no health loss from ash.")]
    [Min(0f)] public float ashHealthLossMultiplier = 1f;

    [Tooltip("1 = normal health loss, 0.5 = half health loss, 0 = no health loss from acid rain.")]
    [Min(0f)] public float acidRainHealthLossMultiplier = 1f;

    public bool IsImmune(bool acidRain)
    {
        return acidRain ? immuneToAcidRain : immuneToAsh;
    }

    public float GetExposureChanceMultiplier(bool acidRain)
    {
        return acidRain ? acidRainExposureChanceMultiplier : ashExposureChanceMultiplier;
    }

    public float GetHealthLossMultiplier(bool acidRain)
    {
        return acidRain ? acidRainHealthLossMultiplier : ashHealthLossMultiplier;
    }
}