using System;
using System.Collections.Generic;

[Serializable]
public class ActiveTrainingOrderSaveData
{
    public string buildingSaveableID;

    public string orderID;
    public string unitID;
    public int multiplier;
    public int totalTurns;
    public int remainingTurns;

    public string populationReservationId;
    public int reservedPopulation;
    public int expiryTurn;
}

[Serializable]
public class PendingTrainingCompletionSaveData
{
    public string sourceBuildingSaveableID;
    public string orderId;
    public string unitID;
    public int totalUnits;

    public string populationReservationId;
    public int reservedPopulation;
    public int expiryTurn;

    public float startingHealthFraction = 1f;

    public int fatigueBonusPower;
    public int fatigueBonusDefense;
    public int fatigueBonusAgility;
    public int fatigueBonusAccuracy;
    public int fatigueBonusRange;
    public int fatigueBonusStealth;
    public float fatigueBonusMovementSpeed;
}

[Serializable]
public class PlayerTrainingSaveData
{
    public List<ActiveTrainingOrderSaveData> activeOrders = new List<ActiveTrainingOrderSaveData>();
    public List<PendingTrainingCompletionSaveData> pendingCompletions = new List<PendingTrainingCompletionSaveData>();
}