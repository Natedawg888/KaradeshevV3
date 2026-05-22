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
}
