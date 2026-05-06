using UnityEngine;

[CreateAssetMenu(fileName = "FamilySimConfig", menuName = "Game/Population/FamilySim Config")]
public class FamilySimConfig : ScriptableObject
{
    [Header("Lineage / Family Affiliation")]
    public PlayerFamilySimulationManager.ChildFamilyAffiliation childAffiliation
        = PlayerFamilySimulationManager.ChildFamilyAffiliation.Mother;

    [Header("Birth Eligibility")]
    [Range(0f, 1f)] public float minHealthForBirth = 0.6f;
    public int minAdultAgeForBirthTurns = 65;
    public int maxAdultAgeForBirthTurns = 130;

    [Header("Gestation")]
    [Min(1)] public int gestationTurns = 10;

    [Header("Pairing Commitment Rules")]
    public bool preferUncommittedMaleFirst = true;
    public bool allowMaleMultipleCommitments = true;
    public bool allowFallbackWhenCommittedPartnerUnavailable = true;
    public bool clearInvalidCommitments = true;

    [Header("Diversity (pair selection)")]
    public bool allowSameFamilyIfBothFounders = true;
    [Range(0f, 1f)] public float lowDiversityThreshold = 0.25f;

    [Header("Limits")]
    public int maxIndividuals = 5000;

    [Header("Parent Cooldowns")]
    public int minParentCooldownTurns = 6;
    public int maxParentCooldownTurns = 16;
    public bool onePregnancyPerMother = true;

    [Header("Multiples")]
    [Range(0f, 1f)] public float twinChance = 0.06f;
    [Range(0f, 1f)] public float tripletChance = 0.01f;
    public int maxMultiples = 3;

    // ─────────────────────────────────────────────────────────────
    // OLD BASELINES (kept for backward compatibility / fallback)
    [Header("Pregnancy Outcome (legacy baselines)")]
    [Range(0f, 1f)] public float pregnancyFailureChance = 0.15f;
    [Range(0f, 1f)] public float failureDeathChance = 0.10f;
    public float failureDecayExponent = 1f;

    [Header("Newborn Outcome")]
    [Range(0f, 1f)] public float newbornDeathChanceOnBirth = 0.02f;

    // ─────────────────────────────────────────────────────────────
    // NEW: health-scaled ranges (used if set; otherwise legacy used)
    [Header("Pregnancy Outcome (health-scaled)")]
    [Tooltip("Effective health used = lerp(mother, father, fatherHealthWeight).")]
    [Range(0f, 1f)] public float fatherHealthWeight = 0.25f;

    [Tooltip("Failure chance at the minimum allowed health (minHealthForBirth).")]
    [Range(0f, 1f)] public float failureAtMinHealth = 0.25f;

    [Tooltip("Failure chance at full health (1.0).")]
    [Range(0f, 1f)] public float failureAtMaxHealth = 0.05f;

    [Tooltip("Maternal death chance *on failure* at the minimum allowed health.")]
    [Range(0f, 1f)] public float failureDeathAtMinHealth = 0.20f;

    [Tooltip("Maternal death chance *on failure* at full health (1.0).")]
    [Range(0f, 1f)] public float failureDeathAtMaxHealth = 0.03f;

    [Header("Newborn Outcome (health-scaled)")]
    [Tooltip("Newborn death chance when parents are at the minimum allowed health (minHealthForBirth).")]
    [Range(0f, 1f)] public float newbornDeathAtMinHealth = 0.04f;

    [Tooltip("Newborn death chance when parents are at full health (1.0).")]
    [Range(0f, 1f)] public float newbornDeathAtMaxHealth = 0.01f;

    [Header("Maternal Outcome (on successful birth)")]
    [Tooltip("Legacy/base chance the mother dies even when the birth succeeds.")]
    [Range(0f, 1f)] public float motherDeathOnSuccessfulBirthChance = 0f;

    [Tooltip("Target death chance at the minimum allowed health (minHealthForBirth).")]
    [Range(0f, 1f)] public float successMotherDeathAtMinHealth = 0f;

    [Tooltip("Target death chance at full health (1.0).")]
    [Range(0f, 1f)] public float successMotherDeathAtMaxHealth = 0f;

    [Header("Cooldown scaling by needs")]
    [Tooltip("Extra cooldown added when need == 1.0 (linearly scaled by need).")]
    public int cooldownExtraMinAtNeed1 = 3;  // was cooldownExtraMinAtMaxNeed
    public int cooldownExtraMaxAtNeed1 = 8;  // was cooldownExtraMaxAtMaxNeed

    [Tooltip("Weight of the father's needs when blending with the mother's. 0=mother only, 1=father only.")]
    [Range(0f, 1f)] public float fatherNeedWeight = 0.25f;

    [Header("Birth Sex Bias")]
    [Range(0f, 1f)] public float sexBiasStrength = 0.75f; // 0=no bias, 1=full balancing
    [Range(0f, 1f)] public float sexBiasMinPMale = 0.35f; // lower clamp for P(male)
    [Range(0f, 1f)] public float sexBiasMaxPMale = 0.65f; // upper clamp for P(male)

    [Header("RNG")]
    public int randomSeed = 0;

    public void CopyFrom(FamilySimConfig other)
    {
        if (!other) return;

        childAffiliation = other.childAffiliation;

        minHealthForBirth        = other.minHealthForBirth;
        minAdultAgeForBirthTurns = other.minAdultAgeForBirthTurns;
        maxAdultAgeForBirthTurns = other.maxAdultAgeForBirthTurns;

        allowSameFamilyIfBothFounders = other.allowSameFamilyIfBothFounders;
        lowDiversityThreshold         = other.lowDiversityThreshold;

        maxIndividuals = other.maxIndividuals;

        minParentCooldownTurns = other.minParentCooldownTurns;
        maxParentCooldownTurns = other.maxParentCooldownTurns;
        onePregnancyPerMother = other.onePregnancyPerMother;
        
        gestationTurns = other.gestationTurns;

        twinChance    = other.twinChance;
        tripletChance = other.tripletChance;
        maxMultiples  = other.maxMultiples;

        pregnancyFailureChance = other.pregnancyFailureChance;
        failureDeathChance     = other.failureDeathChance;
        failureDecayExponent   = other.failureDecayExponent;

        fatherHealthWeight      = other.fatherHealthWeight;
        failureAtMinHealth      = other.failureAtMinHealth;
        failureAtMaxHealth      = other.failureAtMaxHealth;
        failureDeathAtMinHealth = other.failureDeathAtMinHealth;
        failureDeathAtMaxHealth = other.failureDeathAtMaxHealth;

        cooldownExtraMinAtNeed1 = other.cooldownExtraMinAtNeed1;
        cooldownExtraMaxAtNeed1 = other.cooldownExtraMaxAtNeed1;
        fatherNeedWeight = other.fatherNeedWeight;

        newbornDeathChanceOnBirth = other.newbornDeathChanceOnBirth;
        newbornDeathAtMinHealth   = other.newbornDeathAtMinHealth;
        newbornDeathAtMaxHealth   = other.newbornDeathAtMaxHealth;

        newbornDeathChanceOnBirth = other.newbornDeathChanceOnBirth;
        
        motherDeathOnSuccessfulBirthChance = other.motherDeathOnSuccessfulBirthChance;
        successMotherDeathAtMinHealth      = other.successMotherDeathAtMinHealth;
        successMotherDeathAtMaxHealth      = other.successMotherDeathAtMaxHealth;

        randomSeed = other.randomSeed;
    }

    public void ApplyPatch(FamilySimConfigPatch p)
    {
        if (p == null) return;

        // Example usage pattern: add deltas; apply nullable overrides.
        minHealthForBirth        = Mathf.Clamp01(minHealthForBirth + p.minHealthForBirthDelta);
        minAdultAgeForBirthTurns += p.minAdultAgeForBirthTurnsDelta;
        maxAdultAgeForBirthTurns += p.maxAdultAgeForBirthTurnsDelta;

        if (p.allowSameFamilyIfBothFoundersOverride.HasValue)
            allowSameFamilyIfBothFounders = p.allowSameFamilyIfBothFoundersOverride.Value;

        maxIndividuals += p.maxIndividualsDelta;

        minParentCooldownTurns += p.minParentCooldownTurnsDelta;
        maxParentCooldownTurns += p.maxParentCooldownTurnsDelta;

        if (p.onePregnancyPerMotherOverride.HasValue)
            onePregnancyPerMother = p.onePregnancyPerMotherOverride.Value;

        twinChance    = Mathf.Clamp01(twinChance    + p.twinChanceDelta);
        tripletChance = Mathf.Clamp01(tripletChance + p.tripletChanceDelta);
        maxMultiples  = Mathf.Max(1, maxMultiples + p.maxMultiplesDelta);

        fatherHealthWeight      = Mathf.Clamp01(fatherHealthWeight + p.fatherHealthWeightDelta);
        failureAtMinHealth      = Mathf.Clamp01(failureAtMinHealth + p.failureAtMinHealthDelta);
        failureAtMaxHealth      = Mathf.Clamp01(failureAtMaxHealth + p.failureAtMaxHealthDelta);
        failureDeathAtMinHealth = Mathf.Clamp01(failureDeathAtMinHealth + p.failureDeathAtMinHealthDelta);
        failureDeathAtMaxHealth = Mathf.Clamp01(failureDeathAtMaxHealth + p.failureDeathAtMaxHealthDelta);

        newbornDeathChanceOnBirth = Mathf.Clamp01(newbornDeathChanceOnBirth + p.newbornDeathChanceOnBirthDelta);
        newbornDeathAtMinHealth   = Mathf.Clamp01(newbornDeathAtMinHealth   + p.newbornDeathAtMinHealthDelta);
        newbornDeathAtMaxHealth   = Mathf.Clamp01(newbornDeathAtMaxHealth   + p.newbornDeathAtMaxHealthDelta);

        cooldownExtraMinAtNeed1 += p.cooldownExtraMinAtNeed1Delta;
        cooldownExtraMaxAtNeed1 += p.cooldownExtraMaxAtNeed1Delta;
        fatherNeedWeight = Mathf.Clamp01(fatherNeedWeight + p.fatherNeedWeightDelta);

        newbornDeathChanceOnBirth = Mathf.Clamp01(newbornDeathChanceOnBirth + p.newbornDeathChanceOnBirthDelta);
        
        motherDeathOnSuccessfulBirthChance = Mathf.Clamp01(
            motherDeathOnSuccessfulBirthChance + p.motherDeathOnSuccessfulBirthChanceDelta
        );
        successMotherDeathAtMinHealth = Mathf.Clamp01(
            successMotherDeathAtMinHealth + p.successMotherDeathAtMinHealthDelta
        );
        successMotherDeathAtMaxHealth = Mathf.Clamp01(
            successMotherDeathAtMaxHealth + p.successMotherDeathAtMaxHealthDelta
        );
    }
}