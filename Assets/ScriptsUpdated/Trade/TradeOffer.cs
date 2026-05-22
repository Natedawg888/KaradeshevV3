using System;
using System.Collections.Generic;

[Serializable]
public class TradeOffer
{
    public List<ResourceAmount> playerGivesResources = new List<ResourceAmount>();
    public TradePopulationAmount playerGivesPopulation = new TradePopulationAmount();
    public List<ResourceAmount> traderGivesResources = new List<ResourceAmount>();
    public TradePopulationAmount traderGivesPopulation = new TradePopulationAmount();
}
