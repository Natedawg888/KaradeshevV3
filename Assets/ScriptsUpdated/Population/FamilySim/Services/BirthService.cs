using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

public class BirthService : IBirthService
{
    private readonly IndividualRepository _indRepo;
    private readonly FamilyRepository _famRepo;
    private readonly PlayersPopulationManager _pop;

    public BirthService(IndividualRepository indRepo,
                        FamilyRepository famRepo,
                        PlayersPopulationManager pop)
    {
        _indRepo = indRepo;
        _famRepo = famRepo;
        _pop     = pop;
    }

    // Access config safely via manager
    private FamilySimConfig Config => PlayerFamilySimulationManager.Instance?.config;

    // Find existing (A,B) or (B,A) pair family (kept for household logic if you still use it)
    private Family FindExistingPairFamily(Individual mother, Individual father)
    {
        if (mother == null || father == null) return null;
        for (int i = 0; i < _famRepo.All.Count; i++)
        {
            var fam = _famRepo.All[i];
            if (fam == null) continue;
            bool match = (fam.PartnerAId == father.Id && fam.PartnerBId == mother.Id) // A=male, B=female
                      || (fam.PartnerAId == mother.Id && fam.PartnerBId == father.Id);
            if (match) return fam;
        }
        return null;
    }

    // Prefer mother’s lineage name
    private string ChooseAffiliatedFamilyName(Individual mother, Individual father)
    {
        string mName = (!string.IsNullOrEmpty(mother?.FamilyId))
            ? _famRepo.GetById(mother.FamilyId)?.FamilyName
            : null;

        string fName = (!string.IsNullOrEmpty(father?.FamilyId))
            ? _famRepo.GetById(father.FamilyId)?.FamilyName
            : null;

        var mode = Config != null
            ? Config.childAffiliation
            : PlayerFamilySimulationManager.ChildFamilyAffiliation.Mother;

        switch (mode)
        {
            case PlayerFamilySimulationManager.ChildFamilyAffiliation.Mother:
                if (!string.IsNullOrEmpty(mName)) return mName;
                if (!string.IsNullOrEmpty(fName)) return fName;
                return "Family";

            case PlayerFamilySimulationManager.ChildFamilyAffiliation.Father:
                if (!string.IsNullOrEmpty(fName)) return fName;
                if (!string.IsNullOrEmpty(mName)) return mName;
                return "Family";

            case PlayerFamilySimulationManager.ChildFamilyAffiliation.Random:
            default:
                bool pickMother = UnityEngine.Random.value < 0.5f;
                string first = pickMother ? mName : fName;
                string alt   = pickMother ? fName : mName;
                if (!string.IsNullOrEmpty(first)) return first;
                if (!string.IsNullOrEmpty(alt))   return alt;
                return "Family";
        }
    }

    // NEW: Always resolve/ensure the *mother's* family, return its ID
    private string EnsureMotherFamilyId(Individual mother, Individual father)
    {
        if (mother == null) return null;

        // If mother already has a family and it exists in the repo, use it.
        if (!string.IsNullOrEmpty(mother.FamilyId))
        {
            var fam = _famRepo.GetById(mother.FamilyId);
            if (fam != null) return fam.FamilyId;

            // Mother has an ID that doesn't exist (data drift) → repair it below.
        }

        // Create/repair a family for the mother:
        // - If there's a living male father, set A=father (male), B=mother (female)
        // - Otherwise create a single-parent family with B=mother
        string familyName = !string.IsNullOrWhiteSpace(mother.Surname)
            ? mother.Surname
            : ChooseAffiliatedFamilyName(mother, father);

        Family created;
        if (father != null && father.IsAlive && father.Gender == Gender.Male)
        {
            created = _famRepo.CreateFamily(father.Id, mother.Id, familyName); // A=male (father), B=female (mother)
        }
        else
        {
            created = _famRepo.CreateFamily(null, mother.Id, familyName);      // single-parent B=female (mother)
        }

        // Assign IDs/surnames to parents if needed
        mother.FamilyId = created.FamilyId;
        mother.Surname = created.FamilyName;

        if (father != null && father.IsAlive && father.Gender == Gender.Male)
        {
            // If father had no family, attach him as partner A for consistency
            father.FamilyId ??= created.FamilyId;
            if (string.IsNullOrWhiteSpace(father.Surname))
                father.Surname = created.FamilyName;
        }

        return created.FamilyId;
    }
    
    public bool TryCreateNewbornFromParents(
    Individual mother, Individual father,
    out Individual baby, out PopulationGroup groupOut)
    {
        return TryCreateNewbornFromParents(mother, father, null, out baby, out groupOut);
    }

    public bool TryCreateNewbornFromParents(
    Individual mother, Individual father, string targetFamilyId,
    out Individual baby, out PopulationGroup groupOut)
    {
        baby = null; groupOut = null;
        if (mother == null || !mother.IsAlive) return false;

        string familyId = null;

        // 1) Forced household (captured at pairing)
        if (!string.IsNullOrEmpty(targetFamilyId))
        {
            var forcedFam = _famRepo.GetById(targetFamilyId);
            if (forcedFam != null)
            {
                familyId = forcedFam.FamilyId;
                // Normalize parents into this household for consistency
                if (mother.FamilyId != familyId) { mother.FamilyId = familyId; mother.Surname = forcedFam.FamilyName; }
                if (father != null && father.IsAlive && father.FamilyId != familyId)
                {
                    father.FamilyId = familyId;
                    if (string.IsNullOrWhiteSpace(father.Surname)) father.Surname = forcedFam.FamilyName;
                }
            }
            else
            {
                // Forced family was merged/deleted → if parents already share a family, reuse that
                if (father != null && father.IsAlive &&
                    !string.IsNullOrEmpty(mother.FamilyId) &&
                    mother.FamilyId == father.FamilyId &&
                    _famRepo.GetById(mother.FamilyId) != null)
                {
                    familyId = mother.FamilyId; // already a shared, valid household
                }
            }
        }

        // 2) Existing pair household (if no valid forced id)
        if (string.IsNullOrEmpty(familyId))
        {
            var pair = FindExistingPairFamily(mother, father);
            if (pair != null)
            {
                familyId = pair.FamilyId;
                if (mother.FamilyId != familyId) { mother.FamilyId = familyId; mother.Surname = pair.FamilyName; }
                if (father != null && father.IsAlive && father.FamilyId != familyId)
                {
                    father.FamilyId = familyId;
                    if (string.IsNullOrWhiteSpace(father.Surname)) father.Surname = pair.FamilyName;
                }
            }
        }

        // 3) Final fallback: resolve via mother (repair/create if needed)
        if (string.IsNullOrEmpty(familyId))
            familyId = EnsureMotherFamilyId(mother, father);
        if (string.IsNullOrEmpty(familyId)) return false;

        // Aggregate & create baby (unchanged)
        float pMale = GetBiasedMaleProbability();
        var sex = (UnityEngine.Random.value < pMale) ? Gender.Male : Gender.Female;
        var g = _pop.AddBirthAndReturnGroup(sex);
        if (g == null) return false;

        int fatherGen = father != null ? father.Generation : mother.Generation;
        int childGen  = Mathf.Max(mother.Generation, fatherGen) + 1;
        string surname = GetFamilyName(familyId);

        var nb = new Individual(sex, 0, 1f, AgeGroup.Child, g.GroupID, childGen, surname)
        {
            FamilyId = familyId
        };
        if (!_indRepo.TryAdd(nb)) return false;

        var fam = _famRepo.GetById(familyId);
        if (fam != null)
        {
            if (fam.ChildrenIds == null) fam.ChildrenIds = new List<string>();
            fam.ChildrenIds.Add(nb.Id);
        }

        var rng = new System.Random();
        string mGene = !string.IsNullOrEmpty(mother.LineageId) ? mother.LineageId : LineageUtils.NewGene(32, rng);
        string fGene = (father != null && !string.IsNullOrEmpty(father.LineageId)) ? father.LineageId : LineageUtils.NewGene(32, rng);
        nb.LineageId = LineageUtils.MergeForChild(mGene, fGene, sex, rng);

        _pop.MarkUIDirty();
        baby = nb; groupOut = g;
        return true;
    }

    private float GetBiasedMaleProbability()
    {
        float p = 0.5f;

        var pop = _pop != null ? _pop : PlayersPopulationManager.Instance;
        if (pop == null || pop.AllPopulations == null || pop.AllPopulations.Count == 0)
            return p;

        int male   = 0;
        int female = 0;
        for (int i = 0; i < pop.AllPopulations.Count; i++)
        {
            var g = pop.AllPopulations[i];
            if (g == null || g.count <= 0) continue;
            if (g.gender == Gender.Male)   male   += g.count;
            if (g.gender == Gender.Female) female += g.count;
        }

        int total = male + female;
        if (total <= 0) return 0.5f;

        float maleShare   = (float)male / total; // 0..1
        float targetPMale = 1f - maleShare;      // bias toward balancing

        var cfg = PlayerFamilySimulationManager.Instance?.config;
        float strength = Mathf.Clamp01(cfg ? cfg.sexBiasStrength : 0.35f);
        float minPMale = cfg ? cfg.sexBiasMinPMale : 0.25f;
        float maxPMale = cfg ? cfg.sexBiasMaxPMale : 0.35f;

        p = Mathf.Lerp(0.5f, targetPMale, strength);
        return Mathf.Clamp(p, minPMale, maxPMale);
    }

    private string GetFamilyName(string familyId)
    {
        var f = _famRepo.GetById(familyId);
        return f?.FamilyName ?? "Family";
    }
}