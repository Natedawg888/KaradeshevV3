using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Kardashev/Religion/Spirit", fileName = "SpiritDefinition")]
public class SpiritDefinitionSO : ScriptableObject
{
    [Header("Identity")]
    public string spiritID;
    public string displayName;
    public Sprite icon;
    [TextArea(2, 5)] public string description;

    [Header("Belief System")]
    public BeliefSystemType beliefSystem = BeliefSystemType.Animism;

    [Header("Favor")]
    public int startingFavor = 0;
    public int minFavor = -100;
    public int maxFavor = 100;
    [Min(0)] public int favorDecayPerTurn = 1;
    public int favorOnAcceptance = 5;

    [Header("Mood Thresholds")]
    public int angryThreshold = -60;
    public int sadThreshold = -20;
    public int pleasedThreshold = 20;
    public int exaltedThreshold = 60;

    [Header("Offerings")]
    public List<SpiritResourceOfferingOption> resourceOfferings = new List<SpiritResourceOfferingOption>();
    public List<SpiritPopulationSacrificeOption> populationSacrifices = new List<SpiritPopulationSacrificeOption>();

    [Header("Relationships")]
    public List<SpiritDefinitionSO> likedSpirits = new List<SpiritDefinitionSO>();
    public List<SpiritDefinitionSO> dislikedSpirits = new List<SpiritDefinitionSO>();

    [Header("Sacred Animal Groups")]
    [Min(0)] public int activeSacredGroupCount = 1;

    [Header("Sacred Animals")]
    public List<SacredAnimalRule> sacredAnimals = new List<SacredAnimalRule>();

    [Header("Taboos - Spoilage")]
    public List<SpiritSpoilageTabooRule> spoilageTaboos = new List<SpiritSpoilageTabooRule>();

    [Header("Taboos - Left Behind Gathered Loot")]
    public List<SpiritLeftBehindGatheredLootTabooRule> leftBehindGatheredLootTaboos = new List<SpiritLeftBehindGatheredLootTabooRule>();

    [Header("Taboos - Combat Retreat")]
    public List<SpiritCombatRetreatTabooRule> combatRetreatTaboos = new List<SpiritCombatRetreatTabooRule>();

    [Header("Taboos - Left Behind Unit Loot")]
    public List<SpiritLeftBehindUnitLootTabooRule> leftBehindUnitLootTaboos = new List<SpiritLeftBehindUnitLootTabooRule>();

    [Header("Taboos - Religious Building Health")]
    public List<SpiritReligiousBuildingHealthTabooRule> religiousBuildingHealthTaboos =
    new List<SpiritReligiousBuildingHealthTabooRule>();

    [Header("Effects")]
    public List<SpiritEffectEntry> effects = new List<SpiritEffectEntry>();

    [Header("Barren Preferences")]
    public List<SpiritBarrenPreference> barrenPreferences = new List<SpiritBarrenPreference>();

    [Header("Banishment")]
    public SpiritBanishmentOption banishment;

    public int ClampFavor(int value)
    {
        return Mathf.Clamp(value, minFavor, maxFavor);
    }

    public SpiritMoodState GetMoodForFavor(int favor)
    {
        if (favor <= angryThreshold)
            return SpiritMoodState.Angry;

        if (favor <= sadThreshold)
            return SpiritMoodState.Sad;

        if (favor >= exaltedThreshold)
            return SpiritMoodState.Exalted;

        if (favor >= pleasedThreshold)
            return SpiritMoodState.Pleased;

        return SpiritMoodState.Neutral;
    }

    public bool ConflictsWith(SpiritDefinitionSO other)
    {
        if (other == null)
            return false;

        return dislikedSpirits.Contains(other) || other.dislikedSpirits.Contains(this);
    }

    public bool Likes(SpiritDefinitionSO other)
    {
        if (other == null)
            return false;

        return likedSpirits.Contains(other) || other.likedSpirits.Contains(this);
    }

    public bool TryGetSacredAnimalRule(AnimalDefinition animal, out SacredAnimalRule rule)
    {
        rule = null;

        if (animal == null)
            return false;

        for (int i = 0; i < sacredAnimals.Count; i++)
        {
            var entry = sacredAnimals[i];
            if (entry == null || entry.animalDefinition == null)
                continue;

            if (entry.animalDefinition == animal)
            {
                rule = entry;
                return true;
            }
        }

        return false;
    }

    public bool TryGetMatchingResourceOffering(ScriptableObject resource, int amount, out SpiritResourceOfferingOption option)
    {
        option = null;

        for (int i = 0; i < resourceOfferings.Count; i++)
        {
            var entry = resourceOfferings[i];
            if (entry == null)
                continue;

            if (entry.Matches(resource, amount))
            {
                option = entry;
                return true;
            }
        }

        return false;
    }

    public bool TryGetMatchingPopulationSacrifice(
        SpiritSacrificeSexFilter sex,
        SpiritSacrificeAgeFilter age,
        int count,
        out SpiritPopulationSacrificeOption option)
    {
        option = null;

        for (int i = 0; i < populationSacrifices.Count; i++)
        {
            var entry = populationSacrifices[i];
            if (entry == null)
                continue;

            if (entry.Matches(sex, age, count))
            {
                option = entry;
                return true;
            }
        }

        return false;
    }

    public void GetMatchingSpoilageTaboos(
    ResourceDefinition spoiledResource,
    int spoiledAmount,
    List<SpiritSpoilageTabooRule> results)
    {
        if (results == null)
            return;

        results.Clear();

        if (spoiledResource == null || spoiledAmount <= 0 || spoilageTaboos == null || spoilageTaboos.Count == 0)
            return;

        for (int i = 0; i < spoilageTaboos.Count; i++)
        {
            SpiritSpoilageTabooRule taboo = spoilageTaboos[i];
            if (taboo == null)
                continue;

            if (taboo.Matches(spoiledResource, spoiledAmount))
                results.Add(taboo);
        }
    }

    public void GetMatchingLeftBehindGatheredLootTaboos(
    ResourceDefinition leftResource,
    int leftAmount,
    List<SpiritLeftBehindGatheredLootTabooRule> results)
    {
        if (results == null)
            return;

        results.Clear();

        if (leftResource == null || leftAmount <= 0 || leftBehindGatheredLootTaboos == null || leftBehindGatheredLootTaboos.Count == 0)
            return;

        for (int i = 0; i < leftBehindGatheredLootTaboos.Count; i++)
        {
            SpiritLeftBehindGatheredLootTabooRule taboo = leftBehindGatheredLootTaboos[i];
            if (taboo == null)
                continue;

            if (taboo.Matches(leftResource, leftAmount))
                results.Add(taboo);
        }
    }

    public void GetMatchingCombatRetreatTaboos(
    bool againstUnit,
    bool againstAnimal,
    bool afterRetaliation,
    bool wasSurround,
    List<SpiritCombatRetreatTabooRule> results)
    {
        if (results == null)
            return;

        results.Clear();

        if (combatRetreatTaboos == null || combatRetreatTaboos.Count == 0)
            return;

        for (int i = 0; i < combatRetreatTaboos.Count; i++)
        {
            var taboo = combatRetreatTaboos[i];
            if (taboo == null)
                continue;

            if (taboo.Matches(againstUnit, againstAnimal, afterRetaliation, wasSurround))
                results.Add(taboo);
        }
    }

    public void GetMatchingLeftBehindUnitLootTaboos(
    ResourceDefinition leftResource,
    int leftAmount,
    List<SpiritLeftBehindUnitLootTabooRule> results)
    {
        if (results == null)
            return;

        results.Clear();

        if (leftResource == null || leftAmount <= 0 || leftBehindUnitLootTaboos == null || leftBehindUnitLootTaboos.Count == 0)
            return;

        for (int i = 0; i < leftBehindUnitLootTaboos.Count; i++)
        {
            SpiritLeftBehindUnitLootTabooRule taboo = leftBehindUnitLootTaboos[i];
            if (taboo == null)
                continue;

            if (taboo.Matches(leftResource, leftAmount))
                results.Add(taboo);
        }
    }

    public void GetMatchingReligiousBuildingHealthTaboos(
    float previousFraction,
    float currentFraction,
    bool isReligiousType,
    bool isAffiliatedAtBuilding,
    List<SpiritReligiousBuildingHealthTabooRule> results)
    {
        if (results == null)
            return;

        results.Clear();

        if (religiousBuildingHealthTaboos == null || religiousBuildingHealthTaboos.Count == 0)
            return;

        for (int i = 0; i < religiousBuildingHealthTaboos.Count; i++)
        {
            SpiritReligiousBuildingHealthTabooRule taboo = religiousBuildingHealthTaboos[i];
            if (taboo == null)
                continue;

            if (taboo.Matches(
                    previousFraction,
                    currentFraction,
                    isReligiousType,
                    isAffiliatedAtBuilding))
            {
                results.Add(taboo);
            }
        }
    }

    public bool TryGetBanishmentOption(out SpiritBanishmentOption option)
    {
        option = banishment;
        return option != null && option.enabled;
    }
}