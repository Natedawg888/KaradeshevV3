using UnityEngine;

[CreateAssetMenu(menuName = "Kardashev/Tech Effects/Health", fileName = "HealthTechEffect")]
public class HealthTechEffectSO : TechnologyEffectSO
{
    [Header("Base Health (additive deltas)")]
    public int baseChildHealthDelta = 0;
    public int baseTeenHealthDelta  = 0;
    public int baseAdultHealthDelta = 0;
    public int baseElderHealthDelta = 0;

    [Header("Age Thresholds & Lifespan (additive deltas, in turns)")]
    public int childToTeenAgeDelta  = 0;
    public int teenToAdultAgeDelta  = 0;
    public int adultToElderAgeDelta = 0;
    public int lifespanDelta        = 0;

    [Header("Recovery Rates (additive deltas, per turn multiplier)")]
    public float childRecoveryDelta = 0f;
    public float teenRecoveryDelta  = 0f;
    public float adultRecoveryDelta = 0f;
    public float elderRecoveryDelta = 0f;

    [Header("Disease Resistance (additive deltas, 0..1)")]
    public float childResistanceDelta = 0f;
    public float teenResistanceDelta  = 0f;
    public float adultResistanceDelta = 0f;
    public float elderResistanceDelta = 0f;

    [Header("Mortality (additive deltas)")]
    [Range(-1f, 1f)] public float lowHealthMortalityThresholdDelta = 0f;
    [Range(-1f, 1f)] public float mortalityChanceAtZeroHealthDelta = 0f;
    [Range(-1f, 1f)] public float elderMortalityAtElderStartDelta  = 0f;
    [Range(-1f, 1f)] public float elderMortalityAtLifespanDelta    = 0f;

    [Header("Family Sim Config")]
    [Tooltip("If set, replaces PlayerFamilySimulationManager config when this tech completes.")]
    public FamilySimConfig replaceFamilyConfig;

    [Tooltip("Optional patch to apply on top of the current family config (fields added).")]
    public FamilySimConfigPatch patchFamilyConfig;
}
