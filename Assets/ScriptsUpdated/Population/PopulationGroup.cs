using System;
using System.Collections.Generic;
using UnityEngine;

public enum AgeGroup
{
    Child,
    Teen,
    Adult,
    Elder
}

public enum Gender
{
    Male,
    Female
}

[Serializable]
public class PopulationGroup : IEquatable<PopulationGroup>, ISerializationCallbackReceiver
{
    [Tooltip("Unique ID for this population group. Do not modify unless you know what you're doing.")]
    [SerializeField]
    private string groupID = Guid.NewGuid().ToString();

    public Guid GroupID { get; private set; }
    public string GroupIDString => groupID;

    public AgeGroup ageGroup;
    public Gender gender;

    public int count; // total people in this group
    public int additionTurn;

    // Average age in turns
    public int averageAgeInTurns;

    [Range(0f, 1f)]
    public float averageHealth = 1f;

    [Range(0f, 1f)]
    public float hungerLevel = 0f;
    [Range(0f, 1f)]
    public float thirstLevel = 0f;

    public float healthVariance = 0f;

    public int maxHealthPerIndividual = 100;

    // RESERVED for tasks (teen/adult). Cannot exceed count.
    public int reservedCount;

    public PopulationGroup(AgeGroup ageGroup, Gender gender, int count, int additionTurn, int averageAgeInTurns = 0,
        float averageHealth = 1f, int maxHealthPerIndividual = 100)
    {
        if (string.IsNullOrEmpty(groupID))
            groupID = Guid.NewGuid().ToString();
        GroupID = Guid.TryParse(groupID, out var parsed) ? parsed : Guid.NewGuid();
        groupID = GroupID.ToString();

        this.ageGroup = ageGroup;
        this.gender = gender;
        this.count = count;
        this.additionTurn = additionTurn;
        this.averageAgeInTurns = averageAgeInTurns;
        this.averageHealth = Mathf.Clamp01(averageHealth);
        this.maxHealthPerIndividual = maxHealthPerIndividual;
        this.reservedCount = 0;
    }

    public void OnBeforeSerialize()
    {
        if (GroupID == Guid.Empty)
            GroupID = Guid.TryParse(groupID, out var p) ? p : Guid.NewGuid();
        groupID = GroupID.ToString();
    }

    public void OnAfterDeserialize()
    {
        if (!string.IsNullOrEmpty(groupID) && Guid.TryParse(groupID, out var parsed))
            GroupID = parsed;
        else
        {
            GroupID = Guid.NewGuid();
            groupID = GroupID.ToString();
        }
    }

    public bool Equals(PopulationGroup other)
    {
        if (other == null) return false;
        return GroupID == other.GroupID;
    }

    public override bool Equals(object obj) => Equals(obj as PopulationGroup);
    public override int GetHashCode() => GroupID.GetHashCode();
    public static bool operator ==(PopulationGroup a, PopulationGroup b) => a is null ? b is null : a.Equals(b);
    public static bool operator !=(PopulationGroup a, PopulationGroup b) => !(a == b);

    // Effective healthy count (rounded)
    public int EffectiveHealthyCount() => Mathf.RoundToInt(count * averageHealth);

    // Available for task (teen/adult) = not reserved
    public int AvailableForTask() => Mathf.Max(0, count - reservedCount);

    // Reduce population (e.g., deaths); clamp reserved accordingly
    public static bool debugAnyPopulationLoss = false;
    public static bool debugAnyPopulationLossStack = false;

    public void ApplyPopulationLoss(int lost)
    {
        if (lost <= 0)
            return;

        int before = count;

        count = Mathf.Max(0, count - lost);

        if (reservedCount > count)
            reservedCount = count;

        int actualLost = before - count;
        if (debugAnyPopulationLoss && actualLost > 0)
        {
            string msg =
                $"[POP LOSS - CATCH ALL] " +
                $"Lost={actualLost} | " +
                $"GroupID={GroupID} | " +
                $"AgeGroup={ageGroup} | Gender={gender} | " +
                $"Count {before}->{count} | " +
                $"AvgAgeTurns={averageAgeInTurns} | " +
                $"Health01={averageHealth:F3} | " +
                $"Hunger01={hungerLevel:F3} | " +
                $"Thirst01={thirstLevel:F3}";

            if (debugAnyPopulationLossStack)
                msg += $"\nStack:\n{Environment.StackTrace}";

            //Debug.LogWarning(msg);
        }
    }

    // Adjust health (bulk)
    public void AdjustHealth(float delta) => averageHealth = Mathf.Clamp01(averageHealth + delta);

    // Merge another group (simple merge, summing reserved)
    public void Merge(PopulationGroup other)
    {
        if (other == null) return;
        if (other.ageGroup != ageGroup || other.gender != gender) return;

        int totalCount = count + other.count;
        if (totalCount == 0)
        {
            averageHealth = 1f;
            averageAgeInTurns = 0;
            reservedCount = 0;
        }
        else
        {
            averageHealth = (averageHealth * count + other.averageHealth * other.count) / totalCount;
            averageAgeInTurns = Mathf.RoundToInt((averageAgeInTurns * count + other.averageAgeInTurns * other.count) / (float)totalCount);
            reservedCount = Mathf.Min(totalCount, reservedCount + other.reservedCount);
        }

        count = totalCount;
    }

    public void AgeOneTurn(GeneralPopulationManager general)
    {
        if (general == null) return;

        averageAgeInTurns++;

        var newAgeGroup = general.GetAgeGroupForTotalAge(averageAgeInTurns);
        if (newAgeGroup != ageGroup)
        {
            ageGroup = newAgeGroup;
            maxHealthPerIndividual = general.GetBaseHealth(newAgeGroup);
            // Optionally: adjust other derived state when aging up
        }
    }

    public void IncreaseNeedsCycle(GeneralPopulationManager general)
    {
        if (general == null) return;

        float dh = general.GetHungerIncreaseNormalized(); // 0..1 per cycle
        float dt = general.GetThirstIncreaseNormalized(); // 0..1 per cycle

        hungerLevel = Mathf.Clamp01(hungerLevel + dh);
        thirstLevel = Mathf.Clamp01(thirstLevel + dt);
    }


    public void SatisfyNeedsFromInventory(PlayerInventoryManager inv)
    {
        if (inv == null || count <= 0) return;

        // Use a single baseline points-per-person. If you later want different
        // baselines for nutrition/hydration, split these two lines.
        var general = GeneralPopulationManager.Instance;
        float ppp = (general != null && general.pointsPerPersonScale > 0f)
            ? general.pointsPerPersonScale
            : 100f; // sane default/fallback

        // ---------- HUNGER ----------
        if (hungerLevel > 1e-4f)
        {
            // points needed for the whole group at current normalized need
            float needPts = hungerLevel * ppp * count;

            // consume points from FOOD stacks
            float providedPts = inv.ConsumeNutrition(needPts);

            if (providedPts > 0f)
            {
                // convert points back to normalized need delta
                float needDelta = providedPts / (ppp * Mathf.Max(1, count));
                hungerLevel = Mathf.Clamp01(hungerLevel - needDelta);
            }
        }

        // ---------- THIRST ----------
        if (thirstLevel > 1e-4f)
        {
            float needPts = thirstLevel * ppp * count;

            // consume points: WATER first, then hydrating FOODS (your method already does that)
            float providedPts = inv.ConsumeHydration(needPts);

            if (providedPts > 0f)
            {
                float needDelta = providedPts / (ppp * Mathf.Max(1, count));
                thirstLevel = Mathf.Clamp01(thirstLevel - needDelta);
            }
        }
    }

    public void TickHealthFromNeeds(GeneralPopulationManager general)
    {
        if (general == null) return;

        float loss = 0f;

        if (hungerLevel > general.hungerDamageThreshold)
        {
            float t = Mathf.InverseLerp(general.hungerDamageThreshold, 1f, hungerLevel);
            loss += t * general.healthLossPerTurnAtMaxHunger;
        }
        if (thirstLevel > general.thirstDamageThreshold)
        {
            float t = Mathf.InverseLerp(general.thirstDamageThreshold, 1f, thirstLevel);
            loss += t * general.healthLossPerTurnAtMaxThirst;
        }

        float delta = (loss > 0f)
            ? -loss
            : general.GetRecoveryRate(ageGroup) *
            Mathf.Clamp01(Mathf.Min(
                1f - Mathf.InverseLerp(0f, general.hungerDamageThreshold, hungerLevel),
                1f - Mathf.InverseLerp(0f, general.thirstDamageThreshold, thirstLevel)));

        AdjustHealth(delta); // clamps 0..1

        // Hard rule: if health is zero, the whole group dies this turn.
        if (averageHealth <= 0f)
        {
            ApplyPopulationLoss(count);   // sets count to 0 and clamps reservedCount
        }
    }

    public int ApplyMortalityThisTurn(GeneralPopulationManager general)
    {
        // (unchanged) used by world/general sims
        if (general == null || count <= 0) return 0;
        float pHealth = general.GetLowHealthMortalityProb(averageHealth);
        float pAge = (ageGroup == AgeGroup.Elder) ? general.GetElderMortalityProb(averageAgeInTurns) : 0f;
        float pTotal = 1f - (1f - pHealth) * (1f - pAge);
        if (pTotal <= 0f) return 0;
        int deaths = 0;
        for (int i = 0; i < count; i++) if (UnityEngine.Random.value < pTotal) deaths++;
        if (deaths > 0) ApplyPopulationLoss(deaths);
        return deaths;
    }

    // NEW: player-only path using PlayerHealthRulebook (tech-adjusted)
    public int ApplyMortalityThisTurn(PlayerHealthRulebook rules)
    {
        if (rules == null || count <= 0) return 0;

        float pHealth = rules.GetLowHealthMortalityProb(averageHealth);

        float pAge = 0f;
        if (ageGroup == AgeGroup.Elder)
            pAge = rules.GetElderMortalityProb(averageAgeInTurns);

        // Combine independent probabilities
        float pTotal = 1f - (1f - pHealth) * (1f - pAge);
        pTotal = Mathf.Clamp01(pTotal);

        if (pTotal <= 0f) return 0;

        int deaths = 0;
        for (int i = 0; i < count; i++)
            if (UnityEngine.Random.value < pTotal) deaths++;

        if (deaths > 0) ApplyPopulationLoss(deaths);
        return deaths;
    }
}
