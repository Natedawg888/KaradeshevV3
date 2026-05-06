public interface IPregnancyService
{
    bool CanStartPregnancy(Individual mother, Individual father);
    bool TryStartPregnancyWithReservation(Individual mother, Individual father, int gestationTurns, out string reservationId);
    void BeginPregnancy(Individual mother);
    void AbortPregnancy(string motherId);
    int ResolveBirthAndReturnChildrenCount(Individual mother, Individual father);

    void TickOneTurn();
}