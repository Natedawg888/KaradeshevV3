using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Trade/Trader Definition", fileName = "NewTraderDefinition")]
public class TraderDefinitionSO : ScriptableObject
{
    [Header("Identity")]
    public string traderName;
    public Sprite portrait;
    [TextArea] public string flavorDescription;

    [Header("Offer — Resources")]
    public List<ResourceAmount> possibleResources = new List<ResourceAmount>();
    public Vector2Int resourceAmountRange = new Vector2Int(1, 5);
    public int minResourceTypes = 1;
    public int maxResourceTypes = 3;

    [Header("Offer — Population")]
    public bool canOfferPopulation = false;
    public int minPopulationOffered = 0;
    public int maxPopulationOffered = 2;
    public bool canOfferChildren = false;
    public bool canOfferTeens = true;
    public bool canOfferAdults = true;
    public bool canOfferElders = false;

    [Header("Preferences — What They Value From Player")]
    public List<TradeResourcePreference> resourcePreferences = new List<TradeResourcePreference>();
    public bool acceptsPopulationFromPlayer = true;
    public float childValue = 1f;
    public float teenValue = 2f;
    public float adultValue = 4f;
    public float elderValue = 2f;

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

    [Header("Season Restrictions")]
    [Tooltip("If empty, this trader can appear in any season. Otherwise only in the listed seasons.")]
    public List<SeasonDefinition> allowedSeasons = new List<SeasonDefinition>();

    public bool IsAvailableInSeason(SeasonDefinition season)
    {
        if (allowedSeasons == null || allowedSeasons.Count == 0) return true;
        if (season == null) return true;
        for (int i = 0; i < allowedSeasons.Count; i++)
            if (allowedSeasons[i] == season) return true;
        return false;
    }
}
