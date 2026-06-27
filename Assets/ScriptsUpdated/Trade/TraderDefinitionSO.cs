using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class TraderResourceEntry
{
    public ResourceDefinition resource;
    public Vector2Int amountRange = new Vector2Int(1, 5);
    [Tooltip("How much this resource is worth in this trader's economy.")]
    public float tradeValue = 1f;
}

[CreateAssetMenu(menuName = "Trade/Trader Definition", fileName = "NewTraderDefinition")]
public class TraderDefinitionSO : ScriptableObject
{
    [Header("Identity")]
    public string traderName;
    public Sprite portrait;
    [TextArea] public string flavorDescription;

    [Header("Offer — Resources")]
    public List<TraderResourceEntry> possibleResources = new List<TraderResourceEntry>();
    public int minResourceTypes = 1;
    public int maxResourceTypes = 3;

    [Header("Offer — Population")]
    public bool canOfferPopulation = false;
    public int minPopulationOffered = 0;
    public int maxPopulationOffered = 2;
    [UnityEngine.Tooltip("Which age+sex combinations this trader can bring. Each slot has equal probability of being picked.")]
    public List<TradePopulationSlot> offerablePopulation = new List<TradePopulationSlot>();

    [Header("Preferences — What They Value From Player")]
    public List<TradeResourcePreference> resourcePreferences = new List<TradeResourcePreference>();
    [Tooltip("Resources the trader will never accept from the player. They count as zero value regardless of other settings.")]
    public List<ResourceDefinition> rejectedResources = new List<ResourceDefinition>();
    public bool acceptsPopulationFromPlayer = true;
    [Tooltip("Age groups this trader refuses from the player. Population of these ages counts as zero value.")]
    public List<AgeGroup> rejectedAgeGroups = new List<AgeGroup>();
    [Tooltip("Genders this trader refuses from the player. Population of these genders counts as zero value.")]
    public List<Gender> rejectedGenders = new List<Gender>();
    [Tooltip("Multiplier on the PopulationValueManager base value. 1 = neutral, 2 = this trader values children twice as much.")]
    public float childValue = 1f;
    [Tooltip("Multiplier on the PopulationValueManager base value.")]
    public float teenValue = 1f;
    [Tooltip("Multiplier on the PopulationValueManager base value.")]
    public float adultValue = 1f;
    [Tooltip("Multiplier on the PopulationValueManager base value.")]
    public float elderValue = 1f;
    [Tooltip("Multiplier on the PopulationValueManager base value.")]
    public float maleValue = 1f;
    [Tooltip("Multiplier on the PopulationValueManager base value.")]
    public float femaleValue = 1f;

    [Header("Negotiation")]
    [Range(1f, 3f)] public float greedMultiplier = 1.15f;
    [Range(0f, 1f)] public float counterOfferTolerance = 0.75f;

    [Header("Arrival Timing")]
    [Tooltip("Minimum turns between visits from this trader type. 0 = no minimum.")]
    public int minTurnsBetweenVisits = 0;
    [Tooltip("Maximum turns between visits. 0 = driven by season change only.")]
    public int maxTurnsBetweenVisits = 0;
    [Range(0f, 1f), Tooltip("Chance this trader actually arrives when a visit is rolled.")]
    public float arrivalChance = 1f;
    [Tooltip("How many turns this trader stays before moving on.")]
    public int turnsAvailable = 3;

    [Header("Offer Feedback Messages")]
    [Tooltip("Leave blank to use defaults.")]
    public string feedbackNeedMore          = "";
    public string feedbackAlittleMore       = "";
    public string feedbackAcceptable        = "";
    public string feedbackGenerous          = "";
    public string feedbackMassivelyGenerous = "";
    [Tooltip("Shown when the player offers something this trader refuses. Leave blank for default: \"Won't accept: [items].\"")]
    public string feedbackRefused           = "";

    [Header("Season Restrictions")]
    [Tooltip("If empty, this trader can appear in any season. Otherwise only in the listed season IDs.")]
    public List<string> allowedSeasonIDs = new List<string>();

    public bool IsAvailableInSeason(SeasonDefinition season)
    {
        if (allowedSeasonIDs == null || allowedSeasonIDs.Count == 0) return true;
        if (season == null) return true;
        for (int i = 0; i < allowedSeasonIDs.Count; i++)
            if (allowedSeasonIDs[i] == season.seasonID) return true;
        return false;
    }
}
