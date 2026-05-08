using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GeneralPopulationManager : MonoBehaviour
{
    public static GeneralPopulationManager Instance { get; private set; }

    [Header("Health Settings")]
    public int baseChildHealth = 50;
    public int baseTeenHealth = 100;
    public int baseAdultHealth = 100;
    public int baseElderHealth = 75;

    [Header("Lifespan Settings (in turns)")]
    public int childToTeenAge = 25;   // turns for child → teen
    public int teenToAdultAge = 60;   // turns for teen → adult
    public int adultToElderAge = 135; // turns for adult → elder
    public int lifespan = 180;        // total lifespan

    [Header("Recovery Rate Settings (per cycle)")]
    public float childRecoveryRate = 1f;
    public float teenRecoveryRate = 2f;
    public float adultRecoveryRate = 3f;
    public float elderRecoveryRate = 1.5f;

    [Header("Disease Resistance (0..1)")]
    [Range(0f,1f)] public float childResistance = 0f;
    [Range(0f,1f)] public float teenResistance  = 0f;
    [Range(0f,1f)] public float adultResistance = 0f;
    [Range(0f, 1f)] public float elderResistance = 0f;
    
    [Header("Daily Need Points (per person per cycle)")]
    [Tooltip("How many nutrition points each person needs per cycle.")]
    public float nutritionPointsPerPersonPerCycle = 20f;

    [Tooltip("How many hydration points each person needs per cycle.")]
    public float hydrationPointsPerPersonPerCycle = 20f;

    [Header("Need Points Scale")]
    [Tooltip("1.0 hunger/thirst level equals this many points per person.")]
    public float pointsPerPersonScale = 100f;

    [Header("Needs → Health")]
    [Range(0f, 1f)] public float hungerDamageThreshold = 0.6f;
    [Range(0f, 1f)] public float thirstDamageThreshold = 0.6f;
    [Range(0f, 0.2f)] public float healthLossPerTurnAtMaxHunger = 0.02f;
    [Range(0f, 0.2f)] public float healthLossPerTurnAtMaxThirst = 0.03f;

    [Header("Mortality: Low Health")]
    [Range(0f, 1f)] public float lowHealthMortalityThreshold = 0.35f;
    [Range(0f, 1f)] public float mortalityChanceAtZeroHealth = 0.25f;

    [Header("Mortality: Elder Age")]
    [Range(0f, 1f)] public float elderMortalityAtElderStart = 0.005f;
    [Range(0f, 1f)] public float elderMortalityAtLifespan = 0.20f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            //Debug.LogWarning("Multiple GeneralPopulationManager instances detected; destroying duplicate.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // --------- Helpers ---------

    public int GetBaseHealth(AgeGroup age)
    {
        return age switch
        {
            AgeGroup.Child => baseChildHealth,
            AgeGroup.Teen => baseTeenHealth,
            AgeGroup.Adult => baseAdultHealth,
            AgeGroup.Elder => baseElderHealth,
            _ => baseAdultHealth
        };
    }

    public float GetRecoveryRate(AgeGroup age)
    {
        return age switch
        {
            AgeGroup.Child => childRecoveryRate,
            AgeGroup.Teen => teenRecoveryRate,
            AgeGroup.Adult => adultRecoveryRate,
            AgeGroup.Elder => elderRecoveryRate,
            _ => adultRecoveryRate
        };
    }

    /// Returns the recovery amount (rounded up) for the given age group and multiplier.
    public int GetRecoveryAmount(AgeGroup age, float multiplier = 1f)
    {
        float recovery = GetRecoveryRate(age) * multiplier;
        return Mathf.CeilToInt(recovery);
    }

    /// Given total turns lived, returns which AgeGroup that corresponds to.
    public AgeGroup GetAgeGroupForTotalAge(int totalTurnsLived)
    {
        if (totalTurnsLived < childToTeenAge)
            return AgeGroup.Child;
        if (totalTurnsLived < teenToAdultAge)
            return AgeGroup.Teen;
        if (totalTurnsLived < adultToElderAge)
            return AgeGroup.Adult;
        return AgeGroup.Elder;
    }

    /// Returns the next age threshold (in turns) for aging up from the given group.
    public int GetNextAgeThreshold(AgeGroup group)
    {
        return group switch
        {
            AgeGroup.Child => childToTeenAge,
            AgeGroup.Teen => teenToAdultAge,
            AgeGroup.Adult => adultToElderAge,
            AgeGroup.Elder => lifespan,
            _ => lifespan
        };
    }

    public float GetResistance(AgeGroup age)
    {
        return age switch
        {
            AgeGroup.Child => childResistance,
            AgeGroup.Teen  => teenResistance,
            AgeGroup.Adult => adultResistance,
            AgeGroup.Elder => elderResistance,
            _ => adultResistance
        };
    }

    public float GetHungerIncrease()  => GetHungerIncreaseNormalized();
    public float GetThirstIncrease()  => GetThirstIncreaseNormalized();

    public float GetLowHealthMortalityProb(float health01)
    {
        if (health01 >= lowHealthMortalityThreshold) return 0f;
        // health == threshold -> 0, health == 0 -> mortalityChanceAtZeroHealth
        float t = 1f - Mathf.InverseLerp(0f, lowHealthMortalityThreshold, health01);
        return Mathf.Lerp(0f, mortalityChanceAtZeroHealth, Mathf.Clamp01(t));
    }

    public float GetElderMortalityProb(int totalTurnsLived)
    {
        // ramp from Elder start to lifespan
        int start = adultToElderAge;
        int end = Mathf.Max(start + 1, lifespan);
        if (totalTurnsLived <= start) return elderMortalityAtElderStart;
        if (totalTurnsLived >= end) return elderMortalityAtLifespan;

        float t = Mathf.InverseLerp(start, end, totalTurnsLived);
        return Mathf.Lerp(elderMortalityAtElderStart, elderMortalityAtLifespan, t);
    }
    
    public float GetHungerIncreaseNormalized()
    {
        return Mathf.Max(0f, nutritionPointsPerPersonPerCycle) / Mathf.Max(1f, pointsPerPersonScale);
    }

    public float GetThirstIncreaseNormalized()
    {
        return Mathf.Max(0f, hydrationPointsPerPersonPerCycle) / Mathf.Max(1f, pointsPerPersonScale);
    }
}
