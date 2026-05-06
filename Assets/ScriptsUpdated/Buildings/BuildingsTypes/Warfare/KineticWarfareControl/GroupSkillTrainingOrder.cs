using System;

[Serializable]
public class GroupSkillTrainingOrder
{
    public string orderId;

    public string groupId;
    public MilitiaUnit unit;
    public int unitCount;
    public string groupName;

    public int totalTurns;
    public int remainingTurns;

    // Stat deltas to apply on completion
    public int   bonusHealthDelta;
    public float bonusMovementDelta;
    public int   bonusPowerDelta;
    public int   bonusDefenseDelta;
    public int   bonusAgilityDelta;
    public int   bonusAccuracyDelta;
    public int   bonusRangeDelta;
    public int   bonusStealthDelta;

    public int newSkillLevel;

    public string populationReservationId;
    public int    reservedPopulation;
    public int    expiryTurn;

    // --- Advancement metadata ---
    public bool        isAdvancementOrder;
    public MilitiaUnit advancementTargetUnit;

    [NonSerialized] public TileUnitGroupControl owner;
    [NonSerialized] public TileUnitGroupData    groupData;
}
