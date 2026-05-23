using System;
using System.Collections.Generic;

[Serializable]
public class TradeItemSaveEntry
{
    public string resourceID;
    public int amount;
}

[Serializable]
public class TradePopulationSaveEntry
{
    public string ageGroup;
    public string gender;
    public int count;
}

[Serializable]
public class TradeBuildingSaveData
{
    public string buildingInstanceId;
    public bool hasActiveTrader;
    public int traderTurnsRemaining;
    public int nextVisitTurn;
    public string traderName;
    public float traderGreedMultiplier;
    public string traderFlavorDescription;
    public List<TradeItemSaveEntry> traderOfferedResources = new List<TradeItemSaveEntry>();
    public List<TradePopulationSaveEntry> traderOfferedPopulation = new List<TradePopulationSaveEntry>();
}

[Serializable]
public class PlayerTradeBuildingsSaveData
{
    public List<TradeBuildingSaveData> buildings = new List<TradeBuildingSaveData>();
}
