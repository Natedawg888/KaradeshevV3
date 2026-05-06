public interface IBirthService
{
    // existing
    bool TryCreateNewbornFromParents(
        Individual mother, Individual father,
        out Individual baby, out PopulationGroup groupOut);

    bool TryCreateNewbornFromParents(
        Individual mother, Individual father, string targetFamilyId,
        out Individual baby, out PopulationGroup groupOut);
}