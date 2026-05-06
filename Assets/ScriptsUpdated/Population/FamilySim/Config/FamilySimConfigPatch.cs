// FamilySimConfigPatch.cs
using UnityEngine;

[System.Serializable]
public class FamilySimConfigPatch
{
    public float baseBirthChanceDelta = 0f;

    // Eligibility
    public float  minHealthForBirthDelta      = 0f;
    public int    minAdultAgeForBirthTurnsDelta = 0;
    public int    maxAdultAgeForBirthTurnsDelta = 0;

    // Diversity / limits
    public bool?  allowSameFamilyIfBothFoundersOverride = null;
    public int    maxIndividualsDelta = 0;

    // Cooldowns
    public int minParentCooldownTurnsDelta = 0;
    public int maxParentCooldownTurnsDelta = 0;
    public bool? onePregnancyPerMotherOverride = null;

    // Multiples
    public float twinChanceDelta    = 0f;
    public float tripletChanceDelta = 0f;
    public int   maxMultiplesDelta  = 0;

    // Health-scaled pregnancy outcomes
    public float fatherHealthWeightDelta   = 0f;
    public float failureAtMinHealthDelta   = 0f;
    public float failureAtMaxHealthDelta   = 0f;
    public float failureDeathAtMinHealthDelta = 0f;
    public float failureDeathAtMaxHealthDelta = 0f;

    // Cooldown scaling by needs
    public int  cooldownExtraMinAtNeed1Delta = 0;
    public int  cooldownExtraMaxAtNeed1Delta = 0;
    public float fatherNeedWeightDelta = 0f;

    public float newbornDeathChanceOnBirthDelta;
    public float  newbornDeathAtMinHealthDelta   = 0f;
    public float newbornDeathAtMaxHealthDelta = 0f;
    
    public float motherDeathOnSuccessfulBirthChanceDelta = 0f;
    public float successMotherDeathAtMinHealthDelta = 0f;
    public float successMotherDeathAtMaxHealthDelta = 0f;
}
