using System;

public interface IHouseholdService
{
    void CreateInitialHouseholds(IndividualRepository indRepo, FamilyRepository famRepo, System.Func<string> familyNameFactory);
    void MaintainHouseholds(IndividualRepository indRepo, FamilyRepository famRepo, System.Func<string> familyNameFactory);

    void EmancipateTeensToOwnFamilies(IndividualRepository indRepo, FamilyRepository famRepo, Func<string> familyNameFactory);
}