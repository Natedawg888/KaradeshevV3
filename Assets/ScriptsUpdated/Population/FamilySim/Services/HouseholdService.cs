using System;                 // <-- needed for Func<>
using System.Collections.Generic;

public class HouseholdService : IHouseholdService
{
    private readonly RandomService _rng;
    private readonly List<Individual> _males = new();
    private readonly List<Individual> _females = new();

    public HouseholdService(RandomService rng) { _rng = rng; }

    public void CreateInitialHouseholds(
        IndividualRepository indRepo,
        FamilyRepository famRepo,
        Func<string> familyNameFactory)   // CHANGED
    {
        _males.Clear(); _females.Clear();

        foreach (var p in indRepo.All)
        {
            if (!p.IsAlive) continue;
            if (p.AggregatedAgeGroup != AgeGroup.Adult) continue;
            if (!string.IsNullOrEmpty(p.FamilyId)) continue;

            if (p.Gender == Gender.Male) _males.Add(p);
            else if (p.Gender == Gender.Female) _females.Add(p);
        }

        _rng.Shuffle(_males);
        _rng.Shuffle(_females);

        int pairs = System.Math.Min(_males.Count, _females.Count);
        for (int i = 0; i < pairs; i++)
        {
            var m = _males[i];
            var f = _females[i];

            // A = male, B = female
            var fam = famRepo.CreateFamily(m.Id, f.Id, familyNameFactory()); // NEW: unique each time
            m.FamilyId = fam.FamilyId; m.Surname = fam.FamilyName;
            f.FamilyId = fam.FamilyId; f.Surname = fam.FamilyName;
        }

        // Assign leftovers into single-parent families (A=male, B=female)
        for (int i = pairs; i < _males.Count; i++)
        {
            var m = _males[i];
            var fam = famRepo.CreateFamily(m.Id, null, familyNameFactory()); // A=male
            m.FamilyId = fam.FamilyId; m.Surname = fam.FamilyName;
        }
        for (int i = pairs; i < _females.Count; i++)
        {
            var f = _females[i];
            var fam = famRepo.CreateFamily(null, f.Id, familyNameFactory()); // B=female
            f.FamilyId = fam.FamilyId; f.Surname = fam.FamilyName;
        }
    }

    public void MaintainHouseholds(
        IndividualRepository indRepo,
        FamilyRepository famRepo,
        Func<string> familyNameFactory)   // CHANGED
    {
        _males.Clear(); _females.Clear();

        foreach (var p in indRepo.All)
        {
            if (!p.IsAlive) continue;
            if (p.AggregatedAgeGroup != AgeGroup.Adult) continue;
            if (!string.IsNullOrEmpty(p.FamilyId)) continue;

            if (p.Gender == Gender.Male) _males.Add(p);
            else if (p.Gender == Gender.Female) _females.Add(p);
        }

        _rng.Shuffle(_males);
        _rng.Shuffle(_females);

        int pairs = System.Math.Min(_males.Count, _females.Count);
        for (int i = 0; i < pairs; i++)
        {
            var m = _males[i];
            var f = _females[i];

            // A = male, B = female
            var fam = famRepo.CreateFamily(m.Id, f.Id, familyNameFactory()); // NEW
            m.FamilyId = fam.FamilyId; m.Surname = fam.FamilyName;
            f.FamilyId = fam.FamilyId; f.Surname = fam.FamilyName;

            ScoreManager.NotifyFamilyFormed();
        }

        // prune empty families
        var all = new List<Family>(famRepo.All);
        for (int i = all.Count - 1; i >= 0; i--)
        {
            var fam = all[i];
            bool noPartners = string.IsNullOrEmpty(fam.PartnerAId) && string.IsNullOrEmpty(fam.PartnerBId);
            if (noPartners)
                famRepo.Remove(fam);
        }
    }

    public void EmancipateTeensToOwnFamilies(
    IndividualRepository indRepo,
    FamilyRepository famRepo,
    Func<string> familyNameFactory)
    {
        // Work over a snapshot because we'll mutate families
        var families = new List<Family>(famRepo.All);

        for (int i = 0; i < families.Count; i++)
        {
            var fam = families[i];
            if (fam == null) continue;

            // Walk children backwards to be consistent with previous pattern (safe if we ever remove)
            for (int c = fam.ChildrenIds.Count - 1; c >= 0; c--)
            {
                var childId = fam.ChildrenIds[c];
                var child = indRepo.FindById(childId);
                if (child == null || !child.IsAlive)
                {
                    // Still clean up dead/missing children
                    fam.ChildrenIds.RemoveAt(c);
                    continue;
                }

                // Teen → create a solo household for them (only once)
                if (child.AggregatedAgeGroup == AgeGroup.Teen)
                {
                    // If they already have their own (different) household, KEEP them on the parents' list and skip.
                    if (!string.IsNullOrEmpty(child.FamilyId) && child.FamilyId != fam.FamilyId)
                        continue;

                    // IMPORTANT: Keep the parent's family NAME (lineage surname)
                    string inheritedName = fam.FamilyName;

                    // Create teen's SOLO household with inherited family name
                    // A = male teen, B = female teen
                    Family solo = (child.Gender == Gender.Male)
                        ? famRepo.CreateFamily(child.Id, null, inheritedName)   // A=male
                        : famRepo.CreateFamily(null, child.Id, inheritedName);  // B=female

                    solo.ParentFamilyId = fam.FamilyId;
                    if (string.IsNullOrEmpty(solo.LineageRootId))
                        solo.LineageRootId = string.IsNullOrEmpty(fam.LineageRootId) ? fam.FamilyId : fam.LineageRootId;

                    // NEW: override with the teen's personal lineage if present
                    if (!string.IsNullOrEmpty(child.LineageId))
                        solo.LineageRootId = child.LineageId;

                    // Move the teen into their new family and keep surname
                    child.FamilyId = solo.FamilyId;
                    child.Surname  = solo.FamilyName;

                    // NOTE: Do NOT remove from parents' ChildrenIds — we keep lineage link in parents.
                }
            }
        }

        // Prune now-empty families (only those with no partners AND no children)
        var all = new List<Family>(famRepo.All);
        for (int i = all.Count - 1; i >= 0; i--)
        {
            var f = all[i];
            bool noPartners = string.IsNullOrEmpty(f.PartnerAId) && string.IsNullOrEmpty(f.PartnerBId);
            bool noChildren = (f.ChildrenIds == null || f.ChildrenIds.Count == 0);
            if (noPartners && noChildren) famRepo.Remove(f);
        }
    }
}