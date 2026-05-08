using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Animals/AnimalDefinition")]
public class AnimalDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable internal ID used for save/load, lookups, or debugging.")]
    public string id;

    [Tooltip("Readable name shown in UI and logs.")]
    public string displayName;

    [Tooltip("Main animal diet used by hunting, food seeking, and spawn diversity logic.")]
    public AnimalDiet diet;

    [Tooltip("Broad body-size class used by combat, movement, and spawn balancing.")]
    public AnimalSizeCategory sizeCategory = AnimalSizeCategory.Medium;

    [Header("Visuals")]
    [Tooltip("Main portrait or icon used for this animal in the UI.")]
    public Sprite icon;

    [Header("Core Stats")]

    [Tooltip("Health contributed by each individual animal in the group.")]
    [Min(1)] public int healthPerAnimal = 10;

    [Tooltip("How aggressive this species is when choosing risky or hostile actions.")]
    [Range(0f, 1f)] public float aggression = 0f;

    [Tooltip("How likely this species is to flee instead of fight.")]
    [Range(0f, 1f)] public float flightiness = 0.5f;

    [Tooltip("How strongly this species prefers staying in cohesive groups.")]
    [Range(0f, 1f)] public float herding = 0.5f;

    [Tooltip("Raw offensive capability used in combat and hunting evaluation.")]
    [Range(0f, 1f)] public float strength = 0.3f;

    [Tooltip("Raw defensive capability used in combat and retaliation evaluation.")]
    [Range(0f, 1f)] public float defense = 0.3f;

    [Tooltip("Movement and initiative capability.")]
    [Range(0f, 1f)] public float speed = 0.3f;

    [Tooltip("Detection and awareness capability.")]
    [Range(0f, 1f)] public float sense = 0.3f;

    [Tooltip("Ability to stay hidden or gain ambush advantage.")]
    [Range(0f, 1f)] public float stealth = 0.3f;

    [Header("Core Stat Variation (+/-)")]
    [Tooltip("Random +/- variation applied to healthPerAnimal when a new group is spawned.")]
    [Min(0)] public int healthPerAnimalVariation = 0;

    [Tooltip("Random +/- variation applied to aggression when a new group is spawned.")]
    [Range(0f, 1f)] public float aggressionVariation = 0f;

    [Tooltip("Random +/- variation applied to flightiness when a new group is spawned.")]
    [Range(0f, 1f)] public float flightinessVariation = 0f;

    [Tooltip("Random +/- variation applied to herding when a new group is spawned.")]
    [Range(0f, 1f)] public float herdingVariation = 0f;

    [Tooltip("Random +/- variation applied to strength when a new group is spawned.")]
    [Range(0f, 1f)] public float strengthVariation = 0f;

    [Tooltip("Random +/- variation applied to defense when a new group is spawned.")]
    [Range(0f, 1f)] public float defenseVariation = 0f;

    [Tooltip("Random +/- variation applied to speed when a new group is spawned.")]
    [Range(0f, 1f)] public float speedVariation = 0f;

    [Tooltip("Random +/- variation applied to sense when a new group is spawned.")]
    [Range(0f, 1f)] public float senseVariation = 0f;

    [Tooltip("Random +/- variation applied to stealth when a new group is spawned.")]
    [Range(0f, 1f)] public float stealthVariation = 0f;

    [Header("Population Limits")]
    [Tooltip("Maximum number of live groups of this species allowed on the map at once. 0 = no cap.")]
    [Min(0)] public int maxLiveGroupsOnMap = 0;

    [Header("Group Size")]
    [Tooltip("Minimum number of animals in a newly created group.")]
    [Min(1)] public int minGroupSize = 5;

    [Tooltip("Maximum number of animals in a normal group before other systems push back.")]
    [Min(1)] public int maxGroupSize = 20;

    [Tooltip("Maximum age in turns before old-age mortality pressure reaches its peak.")]
    [Min(1)] public int maxAgeInTurns = 365;

    [Tooltip("How often this group updates in the simulation. 1 = every turn.")]
    [Min(1)] public int updateIntervalTurns = 1;

    [Header("Health Recovery")]
    [Tooltip("Flat amount of group health recovered each turn while the group is alive.")]
    [Min(0)] public int healthRecoveryPerTurn = 0;

    [Tooltip("If true, health recovery is blocked while hunger is at or above starvationThreshold or thirst is at or above dehydrationThreshold.")]
    public bool blockHealthRecoveryWhenCriticalNeeds = true;

    [Header("Needs")]
    [Tooltip("Maximum hunger value before the group is fully starving.")]
    [Min(0f)] public float maxHunger = 100f;

    [Tooltip("Maximum thirst value before the group is fully dehydrated.")]
    [Min(0f)] public float maxThirst = 100f;

    [Tooltip("How much hunger increases each turn.")]
    [Min(0f)] public float hungerPerTurn = 1f;

    [Tooltip("How much thirst increases each turn.")]
    [Min(0f)] public float thirstPerTurn = 1f;

    [Tooltip("Fraction of max hunger where starvation penalties and mortality can begin.")]
    [Range(0f, 1f)] public float starvationThreshold = 0.8f;

    [Tooltip("Fraction of max thirst where dehydration penalties and mortality can begin.")]
    [Range(0f, 1f)] public float dehydrationThreshold = 0.8f;

    [Header("Mortality")]
    [Tooltip("Fraction of the group lost per turn while starving.")]
    [Range(0f, 1f)] public float starvationDeathFractionPerTurn = 0.10f;

    [Tooltip("Fraction of the group lost per turn while severely dehydrated.")]
    [Range(0f, 1f)] public float dehydrationDeathFractionPerTurn = 0.25f;

    [Tooltip("Fraction of the group lost per turn once old age mortality applies.")]
    [Range(0f, 1f)] public float oldAgeDeathFractionPerTurn = 0.05f;

    [Tooltip("Age fraction where age-based weakness begins. 0.6 = starts at 60% of max age.")]
    [Range(0f, 1f)] public float ageWeaknessStartsFraction = 0.6f;

    [Tooltip("Maximum extra weakness added by old age when the group reaches max age.")]
    [Range(0f, 1f)] public float maxAgeWeaknessContribution = 0.35f;

    [Header("Diet & Resource Consumption")]
    [Tooltip("Tile resources this species can eat directly.")]
    public ResourceDefinition[] edibleResources;

    [Tooltip("Tile resources this species can drink or use for hydration.")]
    public ResourceDefinition[] hydrationResources;

    [Tooltip("How much hunger one resource unit satisfies.")]
    [Min(0f)] public float hungerPerResourceUnit = 5f;

    [Tooltip("How much thirst one resource unit satisfies.")]
    [Min(0f)] public float thirstPerResourceUnit = 5f;

    [Header("Water Search")]
    [Tooltip("When thirst reaches this fraction, water searching becomes more focused and less random.")]
    [Range(0f, 1f)] public float focusedWaterSearchThreshold = 0.50f;

    [Header("Habitat Preferences")]
    [Tooltip("Environment types this species prefers to live in.")]
    public EnvironmentType[] preferredEnvironments;

    [Tooltip("Tile shapes/types this species prefers to occupy.")]
    public EnvironmentTileType[] preferredTileTypes;

    [Tooltip("Environment types this species avoids when moving or spawning.")]
    public EnvironmentType[] avoidedEnvironments;

    [Tooltip("Tile types this species avoids when moving or spawning.")]
    public EnvironmentTileType[] avoidedTileTypes;

    [Header("Climate Preference")]
    [Tooltip("If enabled, this species tries to stay within its preferred temperature range.")]
    public bool useTemperaturePreference = false;

    [Tooltip("Minimum comfortable temperature in Celsius.")]
    public float minPreferredTemperatureC = 5f;

    [Tooltip("Maximum comfortable temperature in Celsius.")]
    public float maxPreferredTemperatureC = 25f;

    [Header("Social")]
    [Tooltip("Other animals this species is socially comfortable sharing space with.")]
    public AnimalDefinition[] likedAnimals;

    [Header("Reproduction")]
    [Tooltip("Minimum number of offspring produced in one litter.")]
    [Min(1)] public int minLitterSize = 1;

    [Tooltip("Maximum number of offspring produced in one litter.")]
    [Min(1)] public int maxLitterSize = 3;

    [Tooltip("Turns required before this group can reproduce again.")]
    [Min(0)] public int reproduceCooldownTurns = 30;

    [Header("Mating Season - Hard Steer")]
    [Tooltip("If true, this species prioritizes mate-seeking and merge behavior during mating season before normal fear/conflict/wander decisions.")]
    public bool urgentMateSeekingInMatingSeason = false;

    [Tooltip("Hard-steer mating is allowed only while hunger and thirst stay at or below this fraction. Urgent water can still interrupt first.")]
    [Range(0f, 1f)] public float urgentMateSeekMaxNeedFraction = 0.85f;

    [Tooltip("Breeding structure used to calculate effective breeding units. All systems start from the number of breedable females based on breedingFemaleFraction.")]
    public MatingSystem matingSystem = MatingSystem.MonogamousPair;

    [Tooltip("Season IDs in which this species is allowed to mate. Leave empty to allow mating in any season.")]
    public string[] matingSeasonIDs;

    [Tooltip("Minimum age in turns before the group can reproduce.")]
    [Min(0)] public int minReproductiveAgeTurns = 10;

    [Tooltip("Base fraction of the total group treated as breedable females when calculating offspring. Example: size 20 and fraction 0.20 gives 4 breedable females before per-group variation is applied.")]
    [Range(0f, 1f)] public float breedingFemaleFraction = 0.5f;

    [Tooltip("Random +/- variation applied to breedingFemaleFraction when a new group is spawned.")]
    [Range(0f, 1f)] public float breedingFemaleFractionVariation = 0f;

    [Tooltip("Maximum hunger/thirst fraction allowed before mating is blocked by survival pressure.")]
    [Range(0f, 1f)] public float maxNeedFractionForMating = 0.3f;

    [Header("Mating Season - Species Seeking")]
    [Tooltip("If true, low-herding groups can search for same-species groups during mating season.")]
    public bool seekOwnSpeciesDuringMatingSeason = true;

    [Tooltip("Only do mating-season species-seeking if herding is at or below this value.")]
    [Range(0f, 1f)] public float matingSeekMaxHerding = 0.35f;

    [Tooltip("How far low-herding groups will search for same-species groups during mating season.")]
    [Min(1)] public int matingSeekRangeTiles = 6;

    [Tooltip("Minimum number of same-species animals on another tile before steering toward them.")]
    [Min(1)] public int matingSeekMinTargetGroupSize = 1;

    [Header("Mating Season - Group Merge")]
    [Tooltip("If true, groups can merge or rebalance during mating season.")]
    public bool allowGroupMergeDuringMatingSeason = true;

    [Tooltip("At least one of the two groups must be below minGroupSize for a merge/rebalance to happen.")]
    public bool requireSmallGroupForMerge = true;

    [Tooltip("Maximum age difference allowed between two groups to merge.")]
    [Min(0)] public int maxGroupMergeAgeDifferenceTurns = 20;

    [Tooltip("Groups older than this fraction of maxAgeInTurns cannot merge. 0 = disabled.")]
    [Range(0f, 1f)] public float maxMergeAgeFraction = 0.80f;

    [Header("Off-Season Split")]
    [Tooltip("If true, oversized groups may split outside mating season.")]
    public bool splitOutsideMatingSeason = false;

    [Tooltip("Maximum allowed group size outside mating season before splitting pressure applies.")]
    [Min(1)] public int offSeasonMaxGroupSize = 8;

    [Tooltip("Minimum size of the split-off group created outside mating season.")]
    [Min(1)] public int offSeasonMinNewGroupSize = 2;

    [Header("Predation - Targeting")]
    [Tooltip("Preferred prey species this animal actively hunts.")]
    public AnimalDefinition[] preferredPrey;

    [Tooltip("Maximum tile range used to search for prey targets.")]
    [Min(1)] public int huntingRangeTiles = 6;

    [Tooltip("Minimum hunger fraction required before hunting begins.")]
    [Range(0f, 1f)] public float huntingHungerThreshold = 0.7f;

    [Tooltip("Maximum prey-to-predator power ratio considered acceptable for a normal hunt.")]
    [Range(0.5f, 2f)] public float maxPreyPowerAdvantageToHunt = 1.0f;

    [Tooltip("At this hunger fraction, the predator may consider riskier hunts.")]
    [Range(0f, 1f)] public float riskyHuntHungerThreshold = 0.90f;

    [Tooltip("How many successful prey escapes are allowed before the predator gives up.")]
    [Min(1)] public int maxTargetEscapesBeforeGiveUp = 3;

    [Header("Predation - Prey Retaliation")]
    [Tooltip("How close the prey's strength can be to the predator before retaliation is allowed.")]
    [Range(0f, 1f)] public float preyRetaliationStrengthTolerance = 0.15f;

    [Tooltip("If prey health falls below this fraction, desperate retaliation can become possible.")]
    [Range(0f, 1f)] public float preyLowHealthRetaliationThreshold = 0.35f;

    [Tooltip("How close the prey's defense can be to the predator before retaliation is allowed.")]
    [Range(0f, 1f)] public float preyRetaliationDefenseTolerance = 0.15f;

    [Header("Predation - Successful Hunt Recovery")]
    [Tooltip("Fraction of max hunger removed when this predator secures a successful kill.")]
    [Range(0f, 1f)] public float hungerSatisfiedOnSuccessfulHunt = 0.85f;

    [Tooltip("Fraction of max thirst removed when this predator secures a successful kill.")]
    [Range(0f, 1f)] public float thirstSatisfiedOnSuccessfulHunt = 0.15f;

    [Tooltip("If thirst reaches this fraction while hunting, abandon the hunt and seek water.")]
    [Range(0f, 1f)] public float abandonHuntForWaterNeedThreshold = 0.85f;

    [Header("Predator Conflict")]
    [Tooltip("Predator species this animal will treat as rivals or hostile conflict targets.")]
    public AnimalDefinition[] dislikedPredators;

    [Tooltip("Range used to search for predator conflict targets.")]
    [Min(1)] public int predatorConflictRangeTiles = 4;

    [Tooltip("If local predator groups in range reach this count, territorial/rivalry behavior can trigger.")]
    [Min(1)] public int predatorDensityConflictThreshold = 2;

    [Tooltip("How strongly this species reacts to predator crowding or territorial pressure.")]
    [Range(0f, 1f)] public float predatorTerritoriality = 0.5f;

    [Tooltip("Needs threshold above which conflict is usually avoided unless already engaged.")]
    [Range(0f, 1f)] public float conflictNeedThreshold = 0.4f;

    [Header("Own Species Territorial Conflict")]
    [Tooltip("Master toggle for same-species territorial conflict.")]
    public bool allowOwnSpeciesConflict = true;

    [Tooltip("If true, own-species conflict is allowed during mating season.")]
    public bool allowOwnSpeciesConflictInMatingSeason = true;

    [Tooltip("If true, own-species conflict is allowed outside mating season.")]
    public bool allowOwnSpeciesConflictOutOfMatingSeason = true;

    [Tooltip("Multiplier applied when evaluating same-species hostility outside mating season.")]
    [Range(0f, 2f)] public float ownSpeciesConflictBias = 1f;

    [Tooltip("Only allow own-species territorial conflict if herding is at or below this value.")]
    [Range(0f, 1f)] public float ownSpeciesConflictMaxHerding = 0.35f;

    [Tooltip("Minimum nearby same-species groups in range before territorial conflict can trigger.")]
    [Min(1)] public int ownSpeciesConflictMinNearbyGroups = 5;

    [Tooltip("Minimum nearby same-species animals in range before territorial conflict can trigger.")]
    [Min(1)] public int ownSpeciesConflictMinNearbyAnimals = 10;

    [Tooltip("How much stronger the target is allowed to be before we refuse to engage.")]
    [Range(0.5f, 2f)] public float maxTargetPowerAdvantageToEngage = 1.1f;

    [Tooltip("Aggression needed before taking risky fights against stronger predator groups.")]
    [Range(0f, 1f)] public float riskyConflictAggressionThreshold = 0.75f;

    [Tooltip("If target weakness reaches this value, stronger targets can still be engaged.")]
    [Range(0f, 1f)] public float weaknessThresholdToChallengeStrongerTarget = 0.45f;

    [Header("Detection & Escape")]
    [Tooltip("How far this group tries to flee from threats.")]
    [Min(1)] public int fleeDistanceTiles = 2;

    [Tooltip("If herding is at or above this value, detection can trigger nearby herd flee behavior.")]
    [Range(0f, 1f)] public float herdFleeTriggerThreshold = 0.65f;

    [Tooltip("How far the panic/flee signal can spread to nearby groups.")]
    [Min(1)] public int herdFleeSignalRangeTiles = 2;

    [Header("Escape Split")]
    [Tooltip("If true, escaping groups can leave slower stragglers behind.")]
    public bool canLeaveStragglersOnEscape = true;

    [Tooltip("Base chance to leave 1-2 slower animals behind when escaping.")]
    [Range(0f, 1f)] public float baseEscapeSplitChance = 0.05f;

    [Tooltip("Extra chance added from group weakness when escaping.")]
    [Range(0f, 1f)] public float maxExtraEscapeSplitChanceFromWeakness = 0.35f;

    [Tooltip("Minimum number of stragglers that can be left behind.")]
    [Min(1)] public int minEscapeStragglers = 1;

    [Tooltip("Maximum number of stragglers that can be left behind.")]
    [Min(1)] public int maxEscapeStragglers = 2;

    [Header("Loot")]
    [Tooltip("Resources granted per animal killed from this species.")]
    public List<ResourceLootEntry> lootPerKill = new();

    [Header("Human Interaction")]
    [Tooltip("If true, this species can actively hunt human units.")]
    public bool huntsHumans = false;

    [Tooltip("If true, this species avoids tiles and situations involving humans.")]
    public bool avoidsHumans = false;

    [Header("Food Storage Raiding")]
    [Tooltip("If true, this species will move to storage buildings with edible food and steal it when hungry.")]
    public bool raidsStorageForFood = false;

    [Tooltip("Hunger fraction above which this species will seek out food storage buildings.")]
    [Range(0f, 1f)] public float storageRaidHungerThreshold = 0.5f;

    [Tooltip("Maximum tile range to scan for food storage buildings.")]
    [Min(1)] public int storageRaidRangeTiles = 8;

    [Tooltip("Units of food stolen per raid action (multiplied by group size).")]
    [Min(1)] public int foodStolenPerRaidAction = 1;

    public bool IsMatingSeason(SeasonDefinition season)
    {
        if (matingSeasonIDs == null || matingSeasonIDs.Length == 0)
            return true;

        if (season == null || string.IsNullOrWhiteSpace(season.seasonID))
            return false;

        string currentSeasonID = NormalizeSeasonID(season.seasonID);

        for (int i = 0; i < matingSeasonIDs.Length; i++)
        {
            string allowedID = NormalizeSeasonID(matingSeasonIDs[i]);
            if (string.IsNullOrEmpty(allowedID))
                continue;

            if (allowedID == currentSeasonID)
                return true;
        }

        return false;
    }

    private static string NormalizeSeasonID(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        value = value.Trim().ToLowerInvariant();
        value = value.Replace(" ", "");
        value = value.Replace("_", "");
        value = value.Replace("-", "");
        return value;
    }
}