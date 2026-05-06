using System;

[Serializable]
public class PopulationBirthOrder
{
    public readonly string OrderId;
    public readonly string MotherId;
    public readonly string FatherId;
    public readonly string FamilyId;

    public int TurnsRemaining;

    // NEW: reservation that keeps the mother “busy” (1 pop reserved) during gestation
    public string ReservationId;

    public PopulationBirthOrder(string motherId, string fatherId, string familyId, int turns)
    {
        OrderId = Guid.NewGuid().ToString();
        MotherId = motherId;
        FatherId = fatherId;
        FamilyId = familyId;
        TurnsRemaining = Math.Max(0, turns);
    }
}