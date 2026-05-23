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
    public float counterOfferTolerance = 0.75f;
    public bool acceptsPopulationFromPlayer = true;
    public float childValue = 1f;
    public float teenValue = 2f;
    public float adultValue = 4f;
    public float elderValue = 2f;
    public int turnsRemaining;
    public string flavorDescription;

    [Header("Offer Feedback Messages")]
    [Tooltip("Leave blank to use defaults.")]
    public string feedbackNeedMore          = "";
    public string feedbackAlittleMore       = "";
    public string feedbackAcceptable        = "";
    public string feedbackGenerous          = "";
    public string feedbackMassivelyGenerous = "";
}
