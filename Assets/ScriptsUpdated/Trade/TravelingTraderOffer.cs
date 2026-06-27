using System;
using System.Collections.Generic;

[Serializable]
public class TravelingTraderOffer
{
    public string traderName;
    public List<ResourceAmount> offeredResources = new List<ResourceAmount>();
    public TradePopulationAmount offeredPopulation = new TradePopulationAmount();
    public List<TradeResourcePreference> preferences = new List<TradeResourcePreference>();
    public List<ResourceDefinition> rejectedResources = new List<ResourceDefinition>();
    public List<AgeGroup> rejectedAgeGroups = new List<AgeGroup>();
    public List<Gender> rejectedGenders = new List<Gender>();
    public string feedbackRefused;
    public float greedMultiplier = 1.15f;
    public float counterOfferTolerance = 0.75f;
    public bool acceptsPopulationFromPlayer = true;
    public float childValue = 1f;
    public float teenValue = 1f;
    public float adultValue = 1f;
    public float elderValue = 1f;
    public float maleValue = 1f;
    public float femaleValue = 1f;
    public int turnsRemaining;
    public string flavorDescription;
}
