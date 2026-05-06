using UnityEngine;

[CreateAssetMenu(
    fileName = "DiseaseDefinition",
    menuName = "Kardashev/Disease/Disease Definition")]
public class DiseaseDefinitionSO : ScriptableObject
{
    [Header("Identity")]
    public string diseaseId;
    public string displayName;

    [TextArea(3, 8)]
    public string description;

    public Sprite diseaseIcon;

    [Header("Cause / Pathogen")]
    public PathogenCauseType causeType;
    public PathogenCauseDefinitionSO causeDefinition;

    [Tooltip("Optional override. If empty, UI can use the icon from causeDefinition.")]
    public Sprite overrideCauseIcon;

    [Header("Spread")]
    public DiseaseSpreadType spreadType = DiseaseSpreadType.None;
    public DiseaseSeverity severity = DiseaseSeverity.Minor;

    [Header("Duration")]
    [Min(1)] public int minDurationTurns = 2;
    [Min(1)] public int maxDurationTurns = 5;

    [Header("Health Effects")]
    [Tooltip("Normalized health loss per turn. 0.02 = 2% Health01 lost per disease turn before severity/age modifiers.")]
    [Range(0f, 1f)] public float healthLossPerTurn = 0.02f;

    [Range(0f, 1f)] public float recoveryChancePerTurn = 0.15f;
    [Range(0f, 1f)] public float deathChancePerTurn = 0.01f;

    [Header("Age Group Severity Multipliers")]
    [Tooltip("Multiplies health loss and death chance for children.")]
    [Min(0f)] public float childSeverityMultiplier = 1.25f;

    [Tooltip("Multiplies health loss and death chance for teens.")]
    [Min(0f)] public float teenSeverityMultiplier = 1.0f;

    [Tooltip("Multiplies health loss and death chance for adults.")]
    [Min(0f)] public float adultSeverityMultiplier = 1.0f;

    [Tooltip("Multiplies health loss and death chance for elders.")]
    [Min(0f)] public float elderSeverityMultiplier = 1.5f;

    [Header("Age Group Recovery Multipliers")]
    [Tooltip("Multiplies recovery chance for children. Lower = harder to recover.")]
    [Min(0f)] public float childRecoveryMultiplier = 0.85f;

    [Tooltip("Multiplies recovery chance for teens.")]
    [Min(0f)] public float teenRecoveryMultiplier = 1.0f;

    [Tooltip("Multiplies recovery chance for adults.")]
    [Min(0f)] public float adultRecoveryMultiplier = 1.0f;

    [Tooltip("Multiplies recovery chance for elders. Lower = harder to recover.")]
    [Min(0f)] public float elderRecoveryMultiplier = 0.75f;

    [Header("Work / Task Effects")]
    [Range(0f, 1f)] public float workEfficiencyMultiplier = 0.8f;
    [Range(0f, 1f)] public float taskFailureChanceAdd = 0.05f;
    public bool preventsWork = false;

    [Header("Contagious Settings")]
    public bool contagious = false;

    [Tooltip("Chance per disease turn to try spreading from one infected target.")]
    [Range(0f, 1f)] public float spreadChancePerTurn = 0.05f;

    [Header("Virus Context Spread")]
    [Tooltip("If true, this virus spreads through shelter/task-group contact instead of random global spread.")]
    public bool useContextSpreadForVirus = true;

    [Tooltip("Extra multiplier when this disease spreads in a shelter group.")]
    [Min(0f)] public float shelterSpreadMultiplier = 1f;

    [Tooltip("Extra multiplier when this disease spreads in task groups such as gathering, discovery, crafting, or production.")]
    [Min(0f)] public float taskGroupSpreadMultiplier = 1f;

    [Tooltip("How many people one infected person can attempt to infect per context spread tick.")]
    [Min(1)] public int maxSpreadAttemptsPerSourcePerContext = 1;

    [Header("Virus Mutation")]
    public bool virusCanMutate = false;

    [Tooltip("Only allow mutation if Cause Type is Virus.")]
    public bool onlyMutateIfCauseTypeVirus = true;

    [Tooltip("Minimum infected turns before this virus can mutate.")]
    [Min(0)] public int minTurnsBeforeVirusMutation = 2;

    [Tooltip("Chance per disease turn for this virus strain to mutate.")]
    [Range(0f, 1f)] public float virusMutationChancePerTurn = 0.025f;

    [Tooltip("Maximum mutation generation. I = first mutation, II = second mutation, etc.")]
    [Min(1)] public int maxVirusMutationGeneration = 12;

    [Header("Mutation Severity Change")]
    [Range(0f, 1f)] public float mutationChanceToIncreaseSeverity = 0.5f;
    [Range(0f, 1f)] public float mutationSeverityStepMin = 0.04f;
    [Range(0f, 1f)] public float mutationSeverityStepMax = 0.14f;

    [Header("Mutation Contagion Change")]
    [Range(0f, 1f)] public float mutationChanceToIncreaseContagion = 0.5f;
    [Range(0f, 1f)] public float mutationContagionStepMin = 0.08f;
    [Range(0f, 1f)] public float mutationContagionStepMax = 0.22f;

    [Tooltip("Lowest strain contagion multiplier after mutations.")]
    [Min(0f)] public float minVirusStrainContagionMultiplier = 0.35f;

    [Tooltip("Highest strain contagion multiplier after mutations.")]
    [Min(0f)] public float maxVirusStrainContagionMultiplier = 2.25f;



    [Header("Immunity")]
    public bool grantsTemporaryImmunity = false;
    [Min(0)] public int immunityTurns = 0;

    [Header("Debug")]
    public bool debugThisDisease = false;

    public Sprite CauseIcon
    {
        get
        {
            if (overrideCauseIcon != null)
                return overrideCauseIcon;

            return causeDefinition != null ? causeDefinition.causeIcon : null;
        }
    }

    public string CauseDisplayName
    {
        get
        {
            if (causeDefinition != null && !string.IsNullOrWhiteSpace(causeDefinition.displayName))
                return causeDefinition.displayName;

            return causeType.ToString();
        }
    }

    public int RollDuration()
    {
        int min = Mathf.Max(1, minDurationTurns);
        int max = Mathf.Max(min, maxDurationTurns);
        return Random.Range(min, max + 1);
    }

    public float GetAgeSeverityMultiplier(AgeGroup ageGroup)
    {
        return ageGroup switch
        {
            AgeGroup.Child => Mathf.Max(0f, childSeverityMultiplier),
            AgeGroup.Teen => Mathf.Max(0f, teenSeverityMultiplier),
            AgeGroup.Adult => Mathf.Max(0f, adultSeverityMultiplier),
            AgeGroup.Elder => Mathf.Max(0f, elderSeverityMultiplier),
            _ => 1f
        };
    }

    public float GetAgeRecoveryMultiplier(AgeGroup ageGroup)
    {
        return ageGroup switch
        {
            AgeGroup.Child => Mathf.Max(0f, childRecoveryMultiplier),
            AgeGroup.Teen => Mathf.Max(0f, teenRecoveryMultiplier),
            AgeGroup.Adult => Mathf.Max(0f, adultRecoveryMultiplier),
            AgeGroup.Elder => Mathf.Max(0f, elderRecoveryMultiplier),
            _ => 1f
        };
    }

    public float GetSeverityScale(float severity01)
    {
        return Mathf.Lerp(0.5f, 1.5f, Mathf.Clamp01(severity01));
    }

    public float GetEffectiveHealthLossPerTurn(AgeGroup ageGroup, float severity01)
    {
        float value =
            healthLossPerTurn *
            GetSeverityScale(severity01) *
            GetAgeSeverityMultiplier(ageGroup);

        return Mathf.Clamp01(value);
    }

    public float GetEffectiveDeathChancePerTurn(AgeGroup ageGroup, float severity01)
    {
        float value =
            deathChancePerTurn *
            GetSeverityScale(severity01) *
            GetAgeSeverityMultiplier(ageGroup);

        return Mathf.Clamp01(value);
    }

    public float GetEffectiveRecoveryChancePerTurn(AgeGroup ageGroup, float severity01)
    {
        // Higher severity slightly reduces recovery chance.
        float severityScale = GetSeverityScale(severity01);
        float value =
            recoveryChancePerTurn *
            GetAgeRecoveryMultiplier(ageGroup) /
            Mathf.Max(0.25f, severityScale);

        return Mathf.Clamp01(value);
    }

    private void OnValidate()
    {
        minDurationTurns = Mathf.Max(1, minDurationTurns);
        maxDurationTurns = Mathf.Max(minDurationTurns, maxDurationTurns);

        childSeverityMultiplier = Mathf.Max(0f, childSeverityMultiplier);
        teenSeverityMultiplier = Mathf.Max(0f, teenSeverityMultiplier);
        adultSeverityMultiplier = Mathf.Max(0f, adultSeverityMultiplier);
        elderSeverityMultiplier = Mathf.Max(0f, elderSeverityMultiplier);

        childRecoveryMultiplier = Mathf.Max(0f, childRecoveryMultiplier);
        teenRecoveryMultiplier = Mathf.Max(0f, teenRecoveryMultiplier);
        adultRecoveryMultiplier = Mathf.Max(0f, adultRecoveryMultiplier);
        elderRecoveryMultiplier = Mathf.Max(0f, elderRecoveryMultiplier);

        maxVirusMutationGeneration = Mathf.Max(1, maxVirusMutationGeneration);

        mutationSeverityStepMax = Mathf.Max(mutationSeverityStepMin, mutationSeverityStepMax);
        mutationContagionStepMax = Mathf.Max(mutationContagionStepMin, mutationContagionStepMax);

        maxVirusStrainContagionMultiplier = Mathf.Max(
            minVirusStrainContagionMultiplier,
            maxVirusStrainContagionMultiplier);

        if (string.IsNullOrWhiteSpace(displayName))
            displayName = name;

        if (string.IsNullOrWhiteSpace(diseaseId))
            diseaseId = MakeSafeId(displayName);
    }

    private string MakeSafeId(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            input = name;

        return input
            .Trim()
            .ToLowerInvariant()
            .Replace(" ", "_")
            .Replace("-", "_");
    }

    public float GetEffectiveTaskFailureChanceAdd(AgeGroup ageGroup, float severity01)
    {
        float value =
            taskFailureChanceAdd *
            GetSeverityScale(severity01) *
            GetAgeSeverityMultiplier(ageGroup);

        return Mathf.Clamp01(value);
    }

    public float GetEffectiveWorkEfficiencyMultiplier(AgeGroup ageGroup, float severity01)
    {
        if (preventsWork)
            return 0f;

        float baseMultiplier = Mathf.Clamp01(workEfficiencyMultiplier);

        float penalty01 = 1f - baseMultiplier;

        float ageSeverity = GetAgeSeverityMultiplier(ageGroup);
        float scaledPenalty = penalty01 * Mathf.Clamp01(severity01) * ageSeverity;

        return Mathf.Clamp01(1f - scaledPenalty);
    }

    public bool IsVirusLike()
    {
        return causeType == PathogenCauseType.Virus;
    }

    public bool CanMutateAsVirus()
    {
        if (!virusCanMutate)
            return false;

        if (onlyMutateIfCauseTypeVirus && causeType != PathogenCauseType.Virus)
            return false;

        return true;
    }

    public static string ToRomanNumeral(int number)
    {
        number = Mathf.Clamp(number, 1, 3999);

        string[] thousands = { "", "M", "MM", "MMM" };
        string[] hundreds = { "", "C", "CC", "CCC", "CD", "D", "DC", "DCC", "DCCC", "CM" };
        string[] tens = { "", "X", "XX", "XXX", "XL", "L", "LX", "LXX", "LXXX", "XC" };
        string[] ones = { "", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX" };

        return thousands[number / 1000] +
               hundreds[(number % 1000) / 100] +
               tens[(number % 100) / 10] +
               ones[number % 10];
    }

    public static string RollFourDigitMutationCode()
    {
        return UnityEngine.Random.Range(1000, 10000).ToString();
    }
}