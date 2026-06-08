using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public readonly struct ConsumedEntry
{
    public readonly ResourceDefinition Definition;
    public readonly int AmountConsumed;
    public ConsumedEntry(ResourceDefinition def, int amount) { Definition = def; AmountConsumed = amount; }
}

public struct ConsumptionCycleResult
{
    public float FoodNeededPts;
    public float FoodProvidedPts;
    public float WaterNeededPts;
    public float WaterProvidedPts;
    public bool HungerSatisfied;
    public bool ThirstSatisfied;
    public List<ConsumedEntry> Consumed;
}

public class PlayerAggregatedPopulationSimulationManager : MonoBehaviour
{
    public static PlayerAggregatedPopulationSimulationManager Instance { get; private set; }

    public static event Action<ConsumptionCycleResult> OnConsumptionCycle;

    private GeneralPopulationManager general;
    private PlayersPopulationManager playerPop;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        general = GeneralPopulationManager.Instance;
        if (general == null) {}
            //Debug.LogError("GeneralPopulationManager missing in scene.");

        playerPop = PlayersPopulationManager.Instance;
        if (playerPop == null) {}
            //Debug.LogError("PlayersPopulationManager missing in scene.");
    }

    private void OnEnable()
    {
        TurnSystem.SubscribeToStartOfTurn(AdvanceTurn);
    }

    private void OnDisable()
    {
        TurnSystem.UnsubscribeFromStartOfTurn(AdvanceTurn);
    }

    public void AdvanceTurn()
    {
        if (general == null || playerPop == null) return;

        int turn = TurnSystem.GetCurrentTurn();
        bool isCycleTick = (turn > 0) && (turn % 4 == 0);

        var inv = PlayerInventoryManager.Instance;

        Dictionary<string, int> preSnap = null;
        Dictionary<string, ResourceDefinition> defMap = null;
        float totalFoodNeeded = 0f, totalFoodProvided = 0f;
        float totalWaterNeeded = 0f, totalWaterProvided = 0f;
        float ppp = general.pointsPerPersonScale > 0f ? general.pointsPerPersonScale : 100f;

        if (isCycleTick && inv != null)
        {
            defMap  = new Dictionary<string, ResourceDefinition>(StringComparer.OrdinalIgnoreCase);
            preSnap = SnapshotInventory(inv, defMap);
        }

        var deathsByGroup  = new Dictionary<Guid, int>();
        var ageUpCounts    = new Dictionary<AgeGroup, int>();
        int lifespanDeaths = 0;

        var all = playerPop.AllPopulations;
        for (int gi = 0; gi < all.Count; gi++)
        {
            var group = all[gi];
            if (group.count <= 0) continue;

            int beforeAgeTurns = group.averageAgeInTurns;
            AgeGroup beforeAgeGroup = group.ageGroup;

            AgeOneTurn_Player(group);
            LogAgeTransition(turn, group, beforeAgeGroup);

            if (group.ageGroup != beforeAgeGroup && group.count > 0)
            {
                if (!ageUpCounts.TryAdd(group.ageGroup, group.count))
                    ageUpCounts[group.ageGroup] += group.count;
            }

            if (isCycleTick)
            {
                IncreaseNeeds_Player(group);
                float preH = group.hungerLevel, preT = group.thirstLevel;
                totalFoodNeeded  += preH * ppp * group.count;
                totalWaterNeeded += preT * ppp * group.count;

                group.SatisfyNeedsFromInventory(inv);

                totalFoodProvided  += (preH - group.hungerLevel) * ppp * group.count;
                totalWaterProvided += (preT - group.thirstLevel) * ppp * group.count;
            }

            int beforeHealthCount = group.count;
            float beforeHealth = group.averageHealth;

            TickHealth_Player(group);

            int healthZeroDeaths = beforeHealthCount - group.count;
            if (healthZeroDeaths > 0)
            {
                if (!deathsByGroup.TryAdd(group.GroupID, healthZeroDeaths))
                    deathsByGroup[group.GroupID] += healthZeroDeaths;
            }

            LogTurnLoss(
                "HealthZeroFromNeeds",
                group,
                turn,
                beforeHealthCount,
                group.count,
                beforeHealth,
                group.averageHealth,
                group.hungerLevel,
                group.thirstLevel,
                group.averageAgeInTurns
            );

            int beforeMortalityCount = group.count;
            float beforeMortalityHealth = group.averageHealth;

            int deaths = ApplyMortality_Player(group);
            if (deaths > 0)
            {
                LogTurnLoss(
                    "MortalityRoll",
                    group,
                    turn,
                    beforeMortalityCount,
                    group.count,
                    beforeMortalityHealth,
                    group.averageHealth,
                    group.hungerLevel,
                    group.thirstLevel,
                    group.averageAgeInTurns
                );

                if (!deathsByGroup.TryAdd(group.GroupID, deaths))
                    deathsByGroup[group.GroupID] += deaths;
            }

            if (group.averageAgeInTurns >= general.lifespan)
            {
                int beforeLifespanCount = group.count;
                float beforeLifespanHealth = group.averageHealth;

                int extraDeaths = group.count;
                if (extraDeaths > 0)
                {
                    lifespanDeaths += extraDeaths;
                    if (!deathsByGroup.TryAdd(group.GroupID, extraDeaths))
                        deathsByGroup[group.GroupID] += extraDeaths;
                }

                group.ApplyPopulationLoss(group.count);

                LogTurnLoss(
                    "LifespanCutoff",
                    group,
                    turn,
                    beforeLifespanCount,
                    group.count,
                    beforeLifespanHealth,
                    group.averageHealth,
                    group.hungerLevel,
                    group.thirstLevel,
                    group.averageAgeInTurns
                );
            }
        }

        if (isCycleTick && preSnap != null && inv != null)
        {
            var postSnap = SnapshotInventory(inv, null);
            var consumed = new List<ConsumedEntry>();
            foreach (var kv in preSnap)
            {
                postSnap.TryGetValue(kv.Key, out int after);
                int delta = kv.Value - after;
                if (delta > 0 && defMap.TryGetValue(kv.Key, out var def))
                    consumed.Add(new ConsumedEntry(def, delta));
            }
            OnConsumptionCycle?.Invoke(new ConsumptionCycleResult
            {
                FoodNeededPts    = totalFoodNeeded,
                FoodProvidedPts  = totalFoodProvided,
                WaterNeededPts   = totalWaterNeeded,
                WaterProvidedPts = totalWaterProvided,
                HungerSatisfied  = totalFoodNeeded  < 1f || totalFoodProvided  >= totalFoodNeeded  - 1f,
                ThirstSatisfied  = totalWaterNeeded < 1f || totalWaterProvided >= totalWaterNeeded - 1f,
                Consumed         = consumed,
            });
        }

        foreach (var kv in ageUpCounts)
            PostAgingNotification(kv.Key, kv.Value);

        if (lifespanDeaths > 0)
            PostElderDeathNotification(lifespanDeaths, general.lifespan);

        // Remove empty / end-of-life groups
        playerPop.PruneDeadOrEmptyGroups();

        // Tell families exactly how many died in each group (so they pick *who*)
        var fam = PlayerFamilySimulationManager.Instance;
        if (fam != null)
        {
            foreach (var kv in deathsByGroup)
                fam.ApplyDeathsToIndividuals(kv.Key, kv.Value);

            // Now run family pass (handles births + household upkeep)
            fam.AdvanceFamilies(isCycleTick);
        }

        if (deathsByGroup.Count > 0 || ageUpCounts.Count > 0)
            playerPop.MarkUIDirty();
    }

    private PlayerHealthRulebook Rules => PlayerHealthRulebook.Instance;

    private void AgeOneTurn_Player(PopulationGroup g)
    {
        g.averageAgeInTurns++;
        var newGroup = Rules != null ? Rules.GetAgeGroupForTotalAge(g.averageAgeInTurns)
                                    : GeneralPopulationManager.Instance.GetAgeGroupForTotalAge(g.averageAgeInTurns);
        if (newGroup != g.ageGroup)
        {
            g.ageGroup = newGroup;
            g.maxHealthPerIndividual = Rules != null
                ? Rules.GetBaseHealth(newGroup)
                : GeneralPopulationManager.Instance.GetBaseHealth(newGroup);
        }
    }

    private void IncreaseNeeds_Player(PopulationGroup g)
    {
        var gen = GeneralPopulationManager.Instance;

        // Normalized (0..1 per cycle) from the new point-based system
        float dh = gen ? gen.GetHungerIncreaseNormalized() : 0.2f;  // fallback ≈ 20/100
        float dt = gen ? gen.GetThirstIncreaseNormalized() : 0.2f;

        g.hungerLevel = Mathf.Clamp01(g.hungerLevel + dh);
        g.thirstLevel = Mathf.Clamp01(g.thirstLevel + dt);
    }

    private void TickHealth_Player(PopulationGroup g)
    {
        var gen = GeneralPopulationManager.Instance;
        if (gen == null) return;

        float loss = 0f;
        if (g.hungerLevel > gen.hungerDamageThreshold)
        {
            float t = Mathf.InverseLerp(gen.hungerDamageThreshold, 1f, g.hungerLevel);
            loss += t * gen.healthLossPerTurnAtMaxHunger;
        }
        if (g.thirstLevel > gen.thirstDamageThreshold)
        {
            float t = Mathf.InverseLerp(gen.thirstDamageThreshold, 1f, g.thirstLevel);
            loss += t * gen.healthLossPerTurnAtMaxThirst;
        }

        float delta;
        if (loss > 0f) delta = -loss;
        else
        {
            bool hasDisease = DiseaseManager.Instance != null &&
                              DiseaseManager.Instance.HasActiveDiseaseInGroup(g.GroupID);

            if (hasDisease)
            {
                float safeH = 1f - Mathf.InverseLerp(0f, gen.hungerDamageThreshold, g.hungerLevel);
                float safeT = 1f - Mathf.InverseLerp(0f, gen.thirstDamageThreshold, g.thirstLevel);
                float mult = Mathf.Clamp01(Mathf.Min(safeH, safeT));

                float ageRecPoints = Rules != null ? Rules.GetRecoveryRate(g.ageGroup)
                                                   : gen.GetRecoveryRate(g.ageGroup);

                float normalizedRecovery = ageRecPoints / Mathf.Max(1f, g.maxHealthPerIndividual);

                delta = normalizedRecovery * mult * 0.5f; // diseases cut recovery in half, then apply their own damage
            }
            else
            {
                float safeH = 1f - Mathf.InverseLerp(0f, gen.hungerDamageThreshold, g.hungerLevel);
                float safeT = 1f - Mathf.InverseLerp(0f, gen.thirstDamageThreshold, g.thirstLevel);
                float mult = Mathf.Clamp01(Mathf.Min(safeH, safeT));

                float ageRecPoints = Rules != null ? Rules.GetRecoveryRate(g.ageGroup)
                                                   : gen.GetRecoveryRate(g.ageGroup);

                float normalizedRecovery = ageRecPoints / Mathf.Max(1f, g.maxHealthPerIndividual);

                delta = normalizedRecovery * mult;
            }
        }

        g.AdjustHealth(delta);
        if (g.averageHealth <= 0f) g.ApplyPopulationLoss(g.count);
    }

    private int ApplyMortality_Player(PopulationGroup g)
    {
        var rules = PlayerHealthRulebook.Instance;
        return (rules != null)
            ? g.ApplyMortalityThisTurn(rules)                         // player tech-adjusted
            : g.ApplyMortalityThisTurn(GeneralPopulationManager.Instance); // fallback to general
    }

    private static Dictionary<string, int> SnapshotInventory(
        PlayerInventoryManager inv,
        Dictionary<string, ResourceDefinition> defMap)
    {
        var snap = new Dictionary<string, int>(StringComparer.Ordinal);
        AddToSnapshot(inv.GetStacks(ResourceType.Food),  snap, defMap);
        AddToSnapshot(inv.GetStacks(ResourceType.Water), snap, defMap);
        return snap;
    }

    private static void AddToSnapshot(
        IReadOnlyList<InventoryStack> stacks,
        Dictionary<string, int> snap,
        Dictionary<string, ResourceDefinition> defMap)
    {
        for (int i = 0; i < stacks.Count; i++)
        {
            var s = stacks[i];
            if (s?.definition == null || string.IsNullOrEmpty(s.definition.resourceID)) continue;
            string id = s.definition.resourceID;
            if (!snap.TryAdd(id, s.amount)) snap[id] += s.amount;
            defMap?.TryAdd(id, s.definition);
        }
    }

    private void PostAgingNotification(AgeGroup newGroup, int count)
    {
        if (NotificationManager.Instance == null) return;
        string title, message;
        if (NotificationMessageCrafterManager.Instance != null)
            (title, message) = NotificationMessageCrafterManager.Instance.CraftAging(newGroup, count);
        else
        {
            string gn = newGroup switch
            {
                AgeGroup.Teen  => "teenagers",
                AgeGroup.Adult => "adults",
                AgeGroup.Elder => "elders",
                _              => newGroup.ToString().ToLower() + "s",
            };
            (title, message) = ("People are Growing Up", $"{count} of your people have become {gn}.");
        }
        NotificationManager.Instance.AddNotification(NotificationType.PopulationAgedUp, title, message);
    }

    private void PostElderDeathNotification(int count, int lifespanTurns)
    {
        if (NotificationManager.Instance == null) return;
        string title, message;
        if (NotificationMessageCrafterManager.Instance != null)
            (title, message) = NotificationMessageCrafterManager.Instance.CraftElderDeath(count, lifespanTurns);
        else
            (title, message) = ("Elders Passed", $"{count} elder(s) have died of old age after {lifespanTurns} turns.");
        NotificationManager.Instance.AddNotification(NotificationType.ElderDiedOfOldAge, title, message, true);
    }

    [SerializeField] private bool debugPopulationTurnLosses = true;

    private void LogTurnLoss(
        string reason,
        PopulationGroup group,
        int turn,
        int beforeCount,
        int afterCount,
        float beforeHealth,
        float afterHealth,
        float hunger,
        float thirst,
        int ageTurns)
    {
        if (!debugPopulationTurnLosses)
            return;

        if (afterCount >= beforeCount)
            return;

        //Debug.LogWarning(
            //$"[POP LOSS - TURN] " +
            //$"Reason={reason} | " +
            //$"Turn={turn} | " +
            //$"Lost={beforeCount - afterCount} | " +
            //$"GroupID={group.GroupID} | " +
            //$"AgeGroup={group.ageGroup} | Gender={group.gender} | " +
            //$"Count {beforeCount}->{afterCount} | " +
            //$"Health01 {beforeHealth:F3}->{afterHealth:F3} | " +
            //$"Hunger01={hunger:F3} | " +
            //$"Thirst01={thirst:F3} | " +
            //$"AvgAgeTurns={ageTurns}");
    }

    private void LogAgeTransition(int turn, PopulationGroup group, AgeGroup beforeAgeGroup)
    {
        if (!debugPopulationTurnLosses)
            return;

        if (beforeAgeGroup == group.ageGroup)
            return;

        //Debug.Log(
            //$"[POP AGE CHANGE] " +
            //$"Turn={turn} | " +
            //$"GroupID={group.GroupID} | " +
            //$"Gender={group.gender} | " +
            //$"AgeGroup {beforeAgeGroup}->{group.ageGroup} | " +
            //$"AvgAgeTurns={group.averageAgeInTurns}");
    }
}
