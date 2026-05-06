using UnityEngine;

public class PlayerHealthRulebook : MonoBehaviour
{
    public static PlayerHealthRulebook Instance { get; private set; }

    // Existing player overrides
    public int baseChildHealth, baseTeenHealth, baseAdultHealth, baseElderHealth;
    public int childToTeenAge, teenToAdultAge, adultToElderAge, lifespan;
    public float childRecoveryRate, teenRecoveryRate, adultRecoveryRate, elderRecoveryRate;

    // Resistance
    public float childResistance, teenResistance, adultResistance, elderResistance;

    // NEW: Mortality knobs (player-only, copied from General at boot)
    public float lowHealthMortalityThreshold;
    public float mortalityChanceAtZeroHealth;
    public float elderMortalityAtElderStart;
    public float elderMortalityAtLifespan;

    private void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BootstrapFromGeneral();
    }

    public void BootstrapFromGeneral()
    {
        var g = GeneralPopulationManager.Instance;

        // existing copies...
        baseChildHealth = g ? g.baseChildHealth : 50;
        baseTeenHealth = g ? g.baseTeenHealth : 100;
        baseAdultHealth = g ? g.baseAdultHealth : 100;
        baseElderHealth = g ? g.baseElderHealth : 75;

        childToTeenAge = g ? g.childToTeenAge : 100;
        teenToAdultAge = g ? g.teenToAdultAge : 250;
        adultToElderAge = g ? g.adultToElderAge : 550;
        lifespan = g ? g.lifespan : 650;

        childRecoveryRate = g ? g.childRecoveryRate : 1f;
        teenRecoveryRate = g ? g.teenRecoveryRate : 2f;
        adultRecoveryRate = g ? g.adultRecoveryRate : 3f;
        elderRecoveryRate = g ? g.elderRecoveryRate : 1.5f;

        childResistance = g ? Mathf.Clamp01(g.childResistance) : 0f;
        teenResistance = g ? Mathf.Clamp01(g.teenResistance) : 0f;
        adultResistance = g ? Mathf.Clamp01(g.adultResistance) : 0f;
        elderResistance = g ? Mathf.Clamp01(g.elderResistance) : 0f;

        // mortality from General
        lowHealthMortalityThreshold = g ? g.lowHealthMortalityThreshold : 0.35f;
        mortalityChanceAtZeroHealth = g ? g.mortalityChanceAtZeroHealth : 0.25f;
        elderMortalityAtElderStart = g ? g.elderMortalityAtElderStart : 0.005f;
        elderMortalityAtLifespan = g ? g.elderMortalityAtLifespan : 0.20f;
    }

    public void ApplyDeltas(
        int dChildH, int dTeenH, int dAdultH, int dElderH,
        int dC2T, int dT2A, int dA2E, int dLife,
        float dChildRec, float dTeenRec, float dAdultRec, float dElderRec,
        float dChildRes = 0f, float dTeenRes = 0f, float dAdultRes = 0f, float dElderRes = 0f,
        float dLowHealthThresh = 0f, float dMortAtZero = 0f,
        float dElderStart = 0f, float dElderLife = 0f)
    {
        baseChildHealth = Mathf.Max(1, baseChildHealth + dChildH);
        baseTeenHealth = Mathf.Max(1, baseTeenHealth + dTeenH);
        baseAdultHealth = Mathf.Max(1, baseAdultHealth + dAdultH);
        baseElderHealth = Mathf.Max(1, baseElderHealth + dElderH);

        childToTeenAge = Mathf.Max(1, childToTeenAge + dC2T);
        teenToAdultAge = Mathf.Max(childToTeenAge + 1, teenToAdultAge + dT2A);
        adultToElderAge = Mathf.Max(teenToAdultAge + 1, adultToElderAge + dA2E);
        lifespan = Mathf.Max(adultToElderAge + 1, lifespan + dLife);

        childRecoveryRate = Mathf.Max(0f, childRecoveryRate + dChildRec);
        teenRecoveryRate = Mathf.Max(0f, teenRecoveryRate + dTeenRec);
        adultRecoveryRate = Mathf.Max(0f, adultRecoveryRate + dAdultRec);
        elderRecoveryRate = Mathf.Max(0f, elderRecoveryRate + dElderRec);

        childResistance = Mathf.Clamp01(childResistance + dChildRes);
        teenResistance = Mathf.Clamp01(teenResistance + dTeenRes);
        adultResistance = Mathf.Clamp01(adultResistance + dAdultRes);
        elderResistance = Mathf.Clamp01(elderResistance + dElderRes);

        lowHealthMortalityThreshold = Mathf.Clamp01(lowHealthMortalityThreshold + dLowHealthThresh);
        mortalityChanceAtZeroHealth = Mathf.Clamp01(mortalityChanceAtZeroHealth + dMortAtZero);
        elderMortalityAtElderStart = Mathf.Clamp01(elderMortalityAtElderStart + dElderStart);
        elderMortalityAtLifespan = Mathf.Clamp01(elderMortalityAtLifespan + dElderLife);
    }

    private float GetReligionRecoveryMultiplier()
    {
        var religion = PlayerReligionManager.Instance;
        if (religion == null)
            return 1f;

        return Mathf.Max(0f,
            religion.GetMultiplierProduct(SpiritEffectType.PopulationRecoveryRateMultiplier));
    }

    private float GetReligionResistanceAdd()
    {
        var religion = PlayerReligionManager.Instance;
        if (religion == null)
            return 0f;

        return religion.GetAdditiveSum(SpiritEffectType.PopulationResistanceAdd);
    }

    // Mortality helpers for the player sim
    public float GetLowHealthMortalityProb(float health01)
    {
        if (health01 >= lowHealthMortalityThreshold) return 0f;
        float t = 1f - Mathf.InverseLerp(0f, lowHealthMortalityThreshold, health01);
        return Mathf.Lerp(0f, mortalityChanceAtZeroHealth, Mathf.Clamp01(t));
    }

    public float GetElderMortalityProb(int totalTurnsLived)
    {
        int start = adultToElderAge;
        int end = Mathf.Max(start + 1, lifespan);
        if (totalTurnsLived <= start) return elderMortalityAtElderStart;
        if (totalTurnsLived >= end) return elderMortalityAtLifespan;

        float t = Mathf.InverseLerp(start, end, totalTurnsLived);
        return Mathf.Lerp(elderMortalityAtElderStart, elderMortalityAtLifespan, t);
    }

    public int GetBaseHealth(AgeGroup age) => age switch
    {
        AgeGroup.Child => baseChildHealth,
        AgeGroup.Teen => baseTeenHealth,
        AgeGroup.Adult => baseAdultHealth,
        AgeGroup.Elder => baseElderHealth,
        _ => baseAdultHealth
    };

    public float GetRecoveryRate(AgeGroup age)
    {
        float baseRate = age switch
        {
            AgeGroup.Child => childRecoveryRate,
            AgeGroup.Teen => teenRecoveryRate,
            AgeGroup.Adult => adultRecoveryRate,
            AgeGroup.Elder => elderRecoveryRate,
            _ => adultRecoveryRate
        };

        return Mathf.Max(0f, baseRate * GetReligionRecoveryMultiplier());
    }

    public AgeGroup GetAgeGroupForTotalAge(int totalTurns)
    {
        if (totalTurns < childToTeenAge) return AgeGroup.Child;
        if (totalTurns < teenToAdultAge) return AgeGroup.Teen;
        if (totalTurns < adultToElderAge) return AgeGroup.Adult;
        return AgeGroup.Elder;
    }

    public int GetNextAgeThreshold(AgeGroup group) => group switch
    {
        AgeGroup.Child => childToTeenAge,
        AgeGroup.Teen => teenToAdultAge,
        AgeGroup.Adult => adultToElderAge,
        AgeGroup.Elder => lifespan,
        _ => lifespan
    };

    public float GetResistance(AgeGroup age)
    {
        float baseResistance = age switch
        {
            AgeGroup.Child => childResistance,
            AgeGroup.Teen => teenResistance,
            AgeGroup.Adult => adultResistance,
            AgeGroup.Elder => elderResistance,
            _ => adultResistance
        };

        return Mathf.Clamp01(baseResistance + GetReligionResistanceAdd());
    }
}