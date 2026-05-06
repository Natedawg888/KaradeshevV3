public interface IDeathReconciliationService
{
    void Reconcile(IndividualRepository indRepo, FamilyRepository famRepo, PlayersPopulationManager popMgr, GeneralPopulationManager general);
}