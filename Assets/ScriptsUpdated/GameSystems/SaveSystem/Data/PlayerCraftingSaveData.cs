using System;
using System.Collections.Generic;

[Serializable]
public class CraftingPayoutSaveData
{
    public string resourceID;
    public int amount;
}

[Serializable]
public class ActiveCraftOrderSaveData
{
    public string buildingSaveableID;

    public string orderId;
    public string craftingID;
    public int multiplier;
    public int totalTurns;
    public int turnsLeft;
    public string reservationId;

    public List<CraftingPayoutSaveData> payout = new List<CraftingPayoutSaveData>();
}

[Serializable]
public class PendingCraftCompletionSaveData
{
    public string sourceBuildingSaveableID;
    public string orderId;
    public string reservationId;
    public int xpAward;

    public List<CraftingPayoutSaveData> payout = new List<CraftingPayoutSaveData>();
}

[Serializable]
public class PlayerCraftingSaveData
{
    public List<ActiveCraftOrderSaveData> activeOrders = new List<ActiveCraftOrderSaveData>();
    public List<PendingCraftCompletionSaveData> pendingCompletions = new List<PendingCraftCompletionSaveData>();
}