using System;
using System.Collections.Generic;

public enum TradeResultType
{
    Accepted,
    Declined,
    CounterOffer
}

[Serializable]
public class TradeEvaluationResult
{
    public TradeResultType resultType;
    public string title;
    public string message;
    public float playerOfferValue;
    public float traderOfferValue;
    public float requiredValue;
    public List<string> preferredResourceHints = new List<string>();
    public TradeOffer suggestedCounterOffer;
}
