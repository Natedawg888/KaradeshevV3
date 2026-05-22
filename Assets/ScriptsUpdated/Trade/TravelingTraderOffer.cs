using System;
using System.Collections.Generic;

[Serializable]
public class TravelingTraderOffer
{
    public string traderName;
    public List<ResourceAmount> offeredResources = new List<ResourceAmount>();
    public TradePopulationAmount offeredPopulation = new TradePopulationAmount();
    public List<TradeResourcePreference> preferences = new List<TradeResourcePreference>();
    public float greedMultiplier = 1.15f;
    public int turnsRemaining;
    public string flavorDescription;
}
