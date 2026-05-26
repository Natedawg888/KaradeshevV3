using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public class PairingService : IPairingService
{
    private readonly IndividualRepository _indRepo;
    private readonly FamilyRepository _famRepo;
    private readonly FamilySimConfig _config;
    private readonly IPregnancyService _pregnancy;
    private readonly RandomService _rng;

    // Pre-allocated buffers — reused every call to avoid per-turn GC
    private readonly List<Individual> _eligible = new();
    private readonly List<Individual> _females  = new();
    private readonly List<Individual> _males    = new();
    private readonly List<string>     _keyBuffer      = new();
    private readonly List<string>     _toRemoveBuffer = new();
    private readonly HashSet<string>  _familyIdSet    = new();

    // Cached config booleans — reflection is called once at construction, not per property access
    private readonly bool _preferUncommittedMaleFirst;
    private readonly bool _allowMaleMultipleCommitments;
    private readonly bool _allowFallbackWhenCommittedUnavailable;
    private readonly bool _clearInvalidCommitments;

    public PairingService(
        IndividualRepository indRepo,
        FamilyRepository famRepo,
        FamilySimConfig config,
        IPregnancyService pregnancy,
        RandomService rng)
    {
        _indRepo   = indRepo;
        _famRepo   = famRepo;
        _config    = config;
        _pregnancy = pregnancy;
        _rng       = rng;

        // Cache once — avoids reflection on every property access during pairing
        _preferUncommittedMaleFirst           = GetOptionalBoolConfig("preferUncommittedMaleFirst",                    true);
        _allowMaleMultipleCommitments         = GetOptionalBoolConfig("allowMaleMultipleCommitments",                  true);
        _allowFallbackWhenCommittedUnavailable = GetOptionalBoolConfig("allowFallbackWhenCommittedPartnerUnavailable", true);
        _clearInvalidCommitments              = GetOptionalBoolConfig("clearInvalidCommitments",                       true);
    }

    // motherId -> fatherId (persisted “current partner”)
    private readonly Dictionary<string, string> _partnerOf = new();
    public IReadOnlyDictionary<string, string> CurrentPairs => _partnerOf;

    // ---------------------------------------------------------------------
    // CONFIG HELPERS
    // Uses reflection so this script still compiles even if those fields
    // are not yet added to FamilySimConfig.
    // ---------------------------------------------------------------------

    private bool GetOptionalBoolConfig(string memberName, bool fallback)
    {
        if (_config == null) return fallback;

        var t = _config.GetType();

        var prop = t.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (prop != null && prop.PropertyType == typeof(bool))
            return (bool)prop.GetValue(_config);

        var field = t.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null && field.FieldType == typeof(bool))
            return (bool)field.GetValue(_config);

        return fallback;
    }

    private bool PreferUncommittedMaleFirst               => _preferUncommittedMaleFirst;
    private bool AllowMaleMultipleCommitments             => _allowMaleMultipleCommitments;
    private bool AllowFallbackWhenCommittedPartnerUnavailable => _allowFallbackWhenCommittedUnavailable;
    private bool ClearInvalidCommitments                  => _clearInvalidCommitments;

    private int MinBirthAgeTurns => _config != null ? _config.minAdultAgeForBirthTurns : 180;
    private int MaxBirthAgeTurns => _config != null ? _config.maxAdultAgeForBirthTurns : 525;

    // ---------------------------------------------------------------------
    // COMMITMENT HELPERS
    // ---------------------------------------------------------------------

    private bool IsCommitmentStillValid(Individual mother, Individual father)
    {
        if (mother == null || father == null) return false;
        if (!mother.IsAlive || !father.IsAlive) return false;
        if (mother.Gender != Gender.Female) return false;
        if (father.Gender != Gender.Male) return false;

        // hard invalidation: father dead / wrong gender / too young / too old
        if (father.AgeInTurns < MinBirthAgeTurns) return false;
        if (father.AgeInTurns > MaxBirthAgeTurns) return false;

        return true;
    }

    private Individual GetCommittedPartnerForMother(Individual mother, bool pruneInvalid = true)
    {
        if (mother == null) return null;

        if (_partnerOf.TryGetValue(mother.Id, out var fatherId) && !string.IsNullOrEmpty(fatherId))
        {
            var father = FindById(fatherId);
            if (IsCommitmentStillValid(mother, father))
                return father;

            if (pruneInvalid && ClearInvalidCommitments)
                _partnerOf.Remove(mother.Id);

            return null;
        }

        // fallback from family repo
        string repoFatherId = GetCommittedPartnerIdForMotherFromFamilyRepo(mother.Id);
        if (string.IsNullOrEmpty(repoFatherId))
            return null;

        var repoFather = FindById(repoFatherId);
        if (IsCommitmentStillValid(mother, repoFather))
        {
            _partnerOf[mother.Id] = repoFather.Id;
            return repoFather;
        }

        if (pruneInvalid && ClearInvalidCommitments)
            _partnerOf.Remove(mother.Id);

        return null;
    }

    private string GetCommittedPartnerIdForMotherFromFamilyRepo(string motherId)
    {
        if (string.IsNullOrEmpty(motherId)) return null;

        for (int i = 0; i < _famRepo.All.Count; i++)
        {
            var fam = _famRepo.All[i];
            if (fam == null) continue;

            if (fam.PartnerBId == motherId && !string.IsNullOrEmpty(fam.PartnerAId))
                return fam.PartnerAId;

            // legacy order fallback
            if (fam.PartnerAId == motherId)
            {
                var a = FindById(fam.PartnerAId);
                var b = FindById(fam.PartnerBId);

                var mother = (a?.Gender == Gender.Female) ? a : (b?.Gender == Gender.Female) ? b : null;
                var father = (a?.Gender == Gender.Male) ? a : (b?.Gender == Gender.Male) ? b : null;

                if (mother != null && father != null && mother.Id == motherId)
                    return father.Id;
            }
        }

        return null;
    }

    private bool IsMaleCommittedToAnotherMother(string maleId, string ignoreMotherId = null)
    {
        if (string.IsNullOrEmpty(maleId)) return false;

        foreach (var kv in _partnerOf)
        {
            if (!string.IsNullOrEmpty(ignoreMotherId) && kv.Key == ignoreMotherId)
                continue;

            if (kv.Value == maleId)
                return true;
        }

        return false;
    }

    private bool PassesFamilyConstraint(
        Individual mother,
        Individual father,
        bool requireDifferentFamily,
        bool requireSameFamily)
    {
        if (mother == null || father == null) return false;

        if (requireDifferentFamily && father.FamilyId == mother.FamilyId)
            return false;

        if (requireSameFamily && father.FamilyId != mother.FamilyId)
            return false;

        return true;
    }

    private bool IsGeneticallyTooSimilar(Individual mother, Individual candidate)
    {
        float threshold = _config != null ? _config.inbreedingBlockThreshold : 0.5f;
        return LineageUtils.IsTooSimilarForPairing(mother.LineageId, candidate.LineageId, threshold);
    }

    private Individual FindBestMaleCandidateForMother(
    Individual mother,
    List<Individual> malePool,
    bool requireDifferentFamily,
    bool requireSameFamily)
    {
        if (mother == null || malePool == null || malePool.Count == 0)
            return null;

        // 1) If she already has a valid committed partner, she keeps that commitment.
        //    Genetic similarity is a permanent block — fall through to seek an unrelated male.
        //    Other unavailability (cooldown, busy) is temporary — keep commitment and skip this turn.
        var committed = GetCommittedPartnerForMother(mother, pruneInvalid: true);
        bool committedGeneticallyBlocked = false;
        if (committed != null)
        {
            bool passesFamily  = PassesFamilyConstraint(mother, committed, requireDifferentFamily, requireSameFamily);
            bool notTooSimilar = !IsGeneticallyTooSimilar(mother, committed);
            bool canBreed      = _pregnancy.CanStartPregnancy(mother, committed);

            if (passesFamily && notTooSimilar && canBreed)
                return committed;

            // Genetic block is permanent — fall through to seek an unrelated male this turn.
            // Any other block (cooldown, busy) is temporary — honour the commitment and wait.
            if (passesFamily && !notTooSimilar)
                committedGeneticallyBlocked = true;
            else
                return null;
        }

        // 2) No valid commitment (or committed partner is genetically blocked): seek a new male.
        // Prefer an uncommitted male first.
        if (PreferUncommittedMaleFirst)
        {
            for (int i = 0; i < malePool.Count; i++)
            {
                var cand = malePool[i];
                if (cand == null) continue;
                if (!PassesFamilyConstraint(mother, cand, requireDifferentFamily, requireSameFamily))
                    continue;
                if (IsMaleCommittedToAnotherMother(cand.Id, mother.Id))
                    continue;
                if (IsGeneticallyTooSimilar(mother, cand))
                    continue;
                if (!_pregnancy.CanStartPregnancy(mother, cand))
                    continue;

                return cand;
            }
        }

        // 3) If allowed, fall back to already-committed males.
        if (AllowMaleMultipleCommitments)
        {
            for (int i = 0; i < malePool.Count; i++)
            {
                var cand = malePool[i];
                if (cand == null) continue;
                if (!PassesFamilyConstraint(mother, cand, requireDifferentFamily, requireSameFamily))
                    continue;
                if (IsGeneticallyTooSimilar(mother, cand))
                    continue;
                if (!_pregnancy.CanStartPregnancy(mother, cand))
                    continue;

                return cand;
            }
        }
        else if (!PreferUncommittedMaleFirst)
        {
            for (int i = 0; i < malePool.Count; i++)
            {
                var cand = malePool[i];
                if (cand == null) continue;
                if (!PassesFamilyConstraint(mother, cand, requireDifferentFamily, requireSameFamily))
                    continue;
                if (IsMaleCommittedToAnotherMother(cand.Id, mother.Id))
                    continue;
                if (IsGeneticallyTooSimilar(mother, cand))
                    continue;
                if (!_pregnancy.CanStartPregnancy(mother, cand))
                    continue;

                return cand;
            }
        }

        // 4) Last resort: no genetically-acceptable partner exists.
        //    Allow inbreeding — pick the least-similar available male so the population survives.
        //    Prefer the committed partner if he was only blocked by genetics.
        if (committedGeneticallyBlocked && _pregnancy.CanStartPregnancy(mother, committed))
            return committed;

        Individual bestFallback = null;
        double lowestSimilarity = double.MaxValue;
        for (int i = 0; i < malePool.Count; i++)
        {
            var cand = malePool[i];
            if (cand == null) continue;
            if (!PassesFamilyConstraint(mother, cand, requireDifferentFamily, requireSameFamily))
                continue;
            if (!_pregnancy.CanStartPregnancy(mother, cand))
                continue;

            double sim = LineageUtils.HammingSimilarity(mother.LineageId, cand.LineageId);
            if (sim < lowestSimilarity)
            {
                lowestSimilarity = sim;
                bestFallback = cand;
            }
        }

        return bestFallback;
    }

    // ---------------------------------------------------------------------
    // FAMILY / HOUSEHOLD HELPERS
    // ---------------------------------------------------------------------

    private string ChooseAffiliatedFamilyName(Individual mother, Individual father)
    {
        string mName = (!string.IsNullOrEmpty(mother?.FamilyId))
            ? _famRepo.GetById(mother.FamilyId)?.FamilyName
            : null;

        string fName = (!string.IsNullOrEmpty(father?.FamilyId))
            ? _famRepo.GetById(father.FamilyId)?.FamilyName
            : null;

        var mode = _config != null
            ? _config.childAffiliation
            : PlayerFamilySimulationManager.ChildFamilyAffiliation.Mother;

        switch (mode)
        {
            case PlayerFamilySimulationManager.ChildFamilyAffiliation.Mother:
                return !string.IsNullOrEmpty(mName) ? mName : (!string.IsNullOrEmpty(fName) ? fName : "Family");

            case PlayerFamilySimulationManager.ChildFamilyAffiliation.Father:
                return !string.IsNullOrEmpty(fName) ? fName : (!string.IsNullOrEmpty(mName) ? mName : "Family");

            case PlayerFamilySimulationManager.ChildFamilyAffiliation.Random:
            default:
                bool pickMother = UnityEngine.Random.value < 0.5f;
                string first = pickMother ? mName : fName;
                string alt = pickMother ? fName : mName;
                return !string.IsNullOrEmpty(first) ? first : (!string.IsNullOrEmpty(alt) ? alt : "Family");
        }
    }

    private Family EnsurePairFamily(Individual mother, Individual father)
    {
        if (mother == null || father == null) return null;

        // Reuse if already exists
        for (int i = 0; i < _famRepo.All.Count; i++)
        {
            var fam = _famRepo.All[i];
            if (fam == null) continue;

            bool match =
                (fam.PartnerAId == father.Id && fam.PartnerBId == mother.Id) ||
                (fam.PartnerAId == mother.Id && fam.PartnerBId == father.Id); // legacy

            if (match)
            {
                fam.PartnerAId = father.Id;
                fam.PartnerBId = mother.Id;
                return fam;
            }
        }

        // If they belong to different families, merge first
        if (!string.IsNullOrEmpty(mother.FamilyId) &&
            !string.IsNullOrEmpty(father.FamilyId) &&
            mother.FamilyId != father.FamilyId)
        {
            var merged = MergeFamiliesForPair(mother, father);
            if (merged != null) return merged;
        }

        string name = ChooseAffiliatedFamilyName(mother, father);

        Family lineageSource = null;
        var mode = _config != null
            ? _config.childAffiliation
            : PlayerFamilySimulationManager.ChildFamilyAffiliation.Mother;

        string srcId =
            (mode == PlayerFamilySimulationManager.ChildFamilyAffiliation.Father) ? father.FamilyId :
            (mode == PlayerFamilySimulationManager.ChildFamilyAffiliation.Mother) ? mother.FamilyId :
            (UnityEngine.Random.value < 0.5f ? mother.FamilyId : father.FamilyId);

        if (!string.IsNullOrEmpty(srcId))
            lineageSource = _famRepo.GetById(srcId);

        var famNew = (lineageSource != null)
            ? _famRepo.CreateChildFamilyFrom(lineageSource, father.Id, mother.Id, name)
            : _famRepo.CreateFamily(father.Id, mother.Id, name);

        mother.FamilyId = famNew.FamilyId;
        mother.Surname = famNew.FamilyName;
        father.FamilyId = famNew.FamilyId;
        father.Surname = famNew.FamilyName;

        return famNew;
    }

    private Family MergeFamiliesForPair(Individual mother, Individual father)
    {
        var mFam = string.IsNullOrEmpty(mother.FamilyId) ? null : _famRepo.GetById(mother.FamilyId);
        var fFam = string.IsNullOrEmpty(father.FamilyId) ? null : _famRepo.GetById(father.FamilyId);

        if (mFam == null && fFam == null) return null;

        if (mFam != null && fFam != null && mFam.FamilyId == fFam.FamilyId)
        {
            mFam.PartnerAId = father.Id;
            mFam.PartnerBId = mother.Id;
            mFam.FamilyName = ChooseAffiliatedFamilyName(mother, father);
            mother.Surname = mFam.FamilyName;
            father.Surname = mFam.FamilyName;
            return mFam;
        }

        var mode = _config != null
            ? _config.childAffiliation
            : PlayerFamilySimulationManager.ChildFamilyAffiliation.Mother;

        Family keep = null, absorb = null;

        if (mode == PlayerFamilySimulationManager.ChildFamilyAffiliation.Father)
        {
            keep = fFam ?? mFam;
            absorb = (keep == fFam) ? mFam : fFam;
        }
        else if (mode == PlayerFamilySimulationManager.ChildFamilyAffiliation.Mother)
        {
            keep = mFam ?? fFam;
            absorb = (keep == mFam) ? fFam : mFam;
        }
        else
        {
            bool pickMother = UnityEngine.Random.value < 0.5f;
            keep = pickMother ? (mFam ?? fFam) : (fFam ?? mFam);
            absorb = (keep == mFam) ? fFam : mFam;
        }

        if (absorb == null)
        {
            if (keep != null)
            {
                keep.PartnerAId = father.Id;
                keep.PartnerBId = mother.Id;
                keep.FamilyName = ChooseAffiliatedFamilyName(mother, father);

                mother.FamilyId = keep.FamilyId;
                mother.Surname = keep.FamilyName;
                father.FamilyId = keep.FamilyId;
                father.Surname = keep.FamilyName;
            }
            return keep;
        }

        string unifiedName = ChooseAffiliatedFamilyName(mother, father);
        keep.FamilyName = unifiedName;

        keep.PartnerAId = father.Id;
        keep.PartnerBId = mother.Id;

        if (absorb.ChildrenIds != null && absorb.ChildrenIds.Count > 0)
        {
            if (keep.ChildrenIds == null)
                keep.ChildrenIds = new List<string>();

            var set = new HashSet<string>(keep.ChildrenIds);
            for (int i = 0; i < absorb.ChildrenIds.Count; i++)
            {
                if (set.Add(absorb.ChildrenIds[i]))
                    keep.ChildrenIds.Add(absorb.ChildrenIds[i]);
            }
        }

        for (int i = 0; i < _indRepo.All.Count; i++)
        {
            var p = _indRepo.All[i];
            if (p == null || !p.IsAlive) continue;
            if (p.FamilyId != absorb.FamilyId) continue;

            p.FamilyId = keep.FamilyId;
            p.Surname = unifiedName;
        }

        string keepRoot = string.IsNullOrEmpty(keep.LineageRootId) ? keep.FamilyId : keep.LineageRootId;
        keep.LineageRootId = keepRoot;

        _famRepo.Remove(absorb);

        mother.FamilyId = keep.FamilyId;
        mother.Surname = unifiedName;
        father.FamilyId = keep.FamilyId;
        father.Surname = unifiedName;

        return keep;
    }

    // ---------------------------------------------------------------------
    // PUBLIC API
    // ---------------------------------------------------------------------

    public bool TryGetOrCreatePairForFamily(
        string familyId,
        float minHealth,
        int minAgeTurns,
        int maxAgeTurns,
        out Individual mother,
        out Individual father)
    {
        mother = null;
        father = null;

        if (string.IsNullOrEmpty(familyId))
            return false;

        CleanupInvalidPairs();

        // 1) Try to reuse an existing remembered pair inside this family
        foreach (var kv in _partnerOf)
        {
            var m = FindById(kv.Key);
            var f = FindById(kv.Value);
            if (m == null || f == null) continue;
            if (!m.IsAlive || !f.IsAlive) continue;
            if (m.FamilyId != familyId || f.FamilyId != familyId) continue;

            if (IsEligibleByAgeWindow(m, minHealth, minAgeTurns, maxAgeTurns) &&
                IsEligibleByAgeWindow(f, minHealth, minAgeTurns, maxAgeTurns) &&
                _pregnancy.CanStartPregnancy(m, f))
            {
                mother = m;
                father = f;
                EnsurePairFamily(mother, father);
                return true;
            }
        }

        // 2) Create/pick a new one inside this family
        var famList = new List<string> { familyId };
        if (!TryPickParentsInternal(famList, minHealth, minAgeTurns, maxAgeTurns, out mother, out father))
            return false;

        if (mother != null && father != null)
        {
            _partnerOf[mother.Id] = father.Id;
            EnsurePairFamily(mother, father);
        }

        return true;
    }

    public int CollectPairsForFamilies(
        IList<string> familyIds,
        float minHealth,
        int minAgeTurns,
        int maxAgeTurns,
        List<(Individual mother, Individual father)> outPairs,
        int maxPairs)
    {
        if (outPairs == null || familyIds == null || familyIds.Count == 0 || maxPairs <= 0)
            return 0;

        CleanupInvalidPairs();

        int added = 0;
        outPairs.Clear();

        bool IsElig(Individual p) =>
            p != null &&
            p.IsAlive &&
            !string.IsNullOrEmpty(p.FamilyId) &&
            familyIds.Contains(p.FamilyId) &&
            IsEligibleByAgeWindow(p, minHealth, minAgeTurns, maxAgeTurns) &&
            (p.Gender != Gender.Female || _pregnancy.CanStartPregnancy(p, null));

        var usedMothers = new HashSet<string>();

        // PASS 0: existing couples already in FamilyRepository
        for (int i = 0; i < _famRepo.All.Count && added < maxPairs; i++)
        {
            var fam = _famRepo.All[i];
            if (fam == null || string.IsNullOrEmpty(fam.FamilyId)) continue;
            if (!familyIds.Contains(fam.FamilyId)) continue;

            var a = FindById(fam.PartnerAId);
            var b = FindById(fam.PartnerBId);
            if (a == null || b == null) continue;

            var mother = (a.Gender == Gender.Female) ? a : (b.Gender == Gender.Female) ? b : null;
            var father = (a.Gender == Gender.Male) ? a : (b.Gender == Gender.Male) ? b : null;

            if (mother == null || father == null) continue;
            if (!IsElig(mother) || !IsElig(father)) continue;
            if (usedMothers.Contains(mother.Id)) continue;

            if (_pregnancy.CanStartPregnancy(mother, father))
            {
                outPairs.Add((mother, father));
                usedMothers.Add(mother.Id);
                _partnerOf[mother.Id] = father.Id;
                EnsurePairFamily(mother, father);
                added++;
            }
        }

        if (added >= maxPairs) return added;

        // PASS 1: remembered commitments
        _keyBuffer.Clear();
        _keyBuffer.AddRange(_partnerOf.Keys);
        foreach (var motherId in _keyBuffer)
        {
            if (added >= maxPairs) break;

            var mother = FindById(motherId);
            if (!IsElig(mother) || usedMothers.Contains(mother.Id))
                continue;

            var father = GetCommittedPartnerForMother(mother, pruneInvalid: true);
            if (!IsElig(father))
                continue;

            if (_pregnancy.CanStartPregnancy(mother, father))
            {
                outPairs.Add((mother, father));
                usedMothers.Add(mother.Id);
                EnsurePairFamily(mother, father);
                added++;
            }
        }

        if (added >= maxPairs) return added;

        // Gather remaining eligible people — use pre-allocated buffers
        _eligible.Clear();
        _females.Clear();
        _males.Clear();
        foreach (var p in _indRepo.All)
        {
            if (!IsElig(p)) continue;
            _eligible.Add(p);
            if (p.Gender == Gender.Female && !usedMothers.Contains(p.Id)) _females.Add(p);
            else if (p.Gender == Gender.Male) _males.Add(p);
        }
        if (_eligible.Count < 2) return added;
        var females = _females;
        var males   = _males;

        _rng.Shuffle(females);
        _rng.Shuffle(males);

        // PASS 2: cross-family preferred
        for (int i = 0; i < females.Count && added < maxPairs; i++)
        {
            var mom = females[i];
            if (mom == null || usedMothers.Contains(mom.Id)) continue;

            var dad = FindBestMaleCandidateForMother(
                mom,
                males,
                requireDifferentFamily: true,
                requireSameFamily: false);

            if (dad == null)
                continue;

            outPairs.Add((mom, dad));
            usedMothers.Add(mom.Id);
            _partnerOf[mom.Id] = dad.Id;
            EnsurePairFamily(mom, dad);
            added++;
        }

        if (added >= maxPairs) return added;

        // PASS 3: same-family fallback on low diversity
        _familyIdSet.Clear();
        foreach (var p in _eligible)
            if (p.FamilyId != null) _familyIdSet.Add(p.FamilyId);
        float diversityRatio = _familyIdSet.Count / (float)Math.Max(1, _eligible.Count);
        if (diversityRatio < _config.lowDiversityThreshold)
        {
            for (int i = 0; i < females.Count && added < maxPairs; i++)
            {
                var mom = females[i];
                if (mom == null || usedMothers.Contains(mom.Id)) continue;

                var dad = FindBestMaleCandidateForMother(
                    mom,
                    males,
                    requireDifferentFamily: false,
                    requireSameFamily: true);

                if (dad == null)
                    continue;

                outPairs.Add((mom, dad));
                usedMothers.Add(mom.Id);
                _partnerOf[mom.Id] = dad.Id;
                EnsurePairFamily(mom, dad);
                added++;
            }
        }

        return added;
    }

    public void CleanupInvalidPairs()
    {
        _toRemoveBuffer.Clear();

        foreach (var kv in _partnerOf)
        {
            var mother = FindById(kv.Key);
            var father = FindById(kv.Value);

            if (!IsCommitmentStillValid(mother, father))
                _toRemoveBuffer.Add(kv.Key);
        }

        for (int i = 0; i < _toRemoveBuffer.Count; i++)
            _partnerOf.Remove(_toRemoveBuffer[i]);
    }

    public void SeedPairsFromExistingFamilies()
    {
        for (int i = 0; i < _famRepo.All.Count; i++)
        {
            var fam = _famRepo.All[i];
            if (fam == null) continue;

            var a = FindById(fam.PartnerAId);
            var b = FindById(fam.PartnerBId);
            if (a == null || b == null) continue;
            if (!a.IsAlive || !b.IsAlive) continue;

            var mother = (a.Gender == Gender.Female) ? a : (b.Gender == Gender.Female) ? b : null;
            var father = (a.Gender == Gender.Male) ? a : (b.Gender == Gender.Male) ? b : null;

            if (mother != null && father != null)
                _partnerOf[mother.Id] = father.Id;
        }
    }

    public bool TryPickParentsForFamilies(
        IList<string> allowedFamilyIds,
        float minHealth,
        int minAgeTurns,
        int maxAgeTurns,
        out Individual mother,
        out Individual father)
    {
        return TryPickParentsInternal(allowedFamilyIds, minHealth, minAgeTurns, maxAgeTurns, out mother, out father);
    }

    private bool TryPickParentsInternal(
        IList<string> allowedFamilyIds,
        float minHealth,
        int minAgeTurns,
        int maxAgeTurns,
        out Individual mother,
        out Individual father)
    {
        mother = null;
        father = null;

        if (allowedFamilyIds == null || allowedFamilyIds.Count == 0)
            return false;

        CleanupInvalidPairs();

        // Populate adults/females/males directly — no intermediate ToList allocation
        _eligible.Clear();
        _females.Clear();
        _males.Clear();
        foreach (var a in _indRepo.All)
        {
            if (a == null || string.IsNullOrEmpty(a.FamilyId)) continue;
            if (!allowedFamilyIds.Contains(a.FamilyId)) continue;
            if (!IsEligibleByAgeWindow(a, minHealth, minAgeTurns, maxAgeTurns)) continue;
            _eligible.Add(a);
            if (a.Gender == Gender.Female) _females.Add(a);
            else if (a.Gender == Gender.Male) _males.Add(a);
        }
        var adults = _eligible; // alias so the rest of the method is unchanged

        if (adults.Count < 2)
            return false;

        if (_females.Count == 0 || _males.Count == 0)
            return false;

        _rng.Shuffle(_females);
        _rng.Shuffle(_males);

        _familyIdSet.Clear();
        foreach (var a in adults)
            if (a.FamilyId != null) _familyIdSet.Add(a.FamilyId);
        int uniqueFamilies = _familyIdSet.Count;
        float diversityRatio = (float)uniqueFamilies / Math.Max(1, adults.Count);

        // 1) cross-family preferred
        for (int i = 0; i < _females.Count; i++)
        {
            var f = _females[i];
            var m = FindBestMaleCandidateForMother(
                f,
                _males,
                requireDifferentFamily: true,
                requireSameFamily: false);

            if (m != null)
            {
                mother = f;
                father = m;
                return true;
            }
        }

        // 2) founders same-family
        if (_config.allowSameFamilyIfBothFounders)
        {
            var famIds = adults.Select(a => a.FamilyId).Distinct();
            foreach (var famId in famIds)
            {
                var fems = _females.Where(a => a.FamilyId == famId && a.Generation == 0).ToList();
                var mals = _males.Where(a => a.FamilyId == famId && a.Generation == 0).ToList();
                if (fems.Count == 0 || mals.Count == 0) continue;

                foreach (var f in fems)
                {
                    var m = FindBestMaleCandidateForMother(
                        f,
                        mals,
                        requireDifferentFamily: false,
                        requireSameFamily: true);

                    if (m != null)
                    {
                        mother = f;
                        father = m;
                        return true;
                    }
                }
            }
        }

        // 3) low-diversity same-family fallback
        if (diversityRatio < _config.lowDiversityThreshold)
        {
            var famIds = adults.Select(a => a.FamilyId).Distinct();
            foreach (var famId in famIds)
            {
                var fems = _females.Where(a => a.FamilyId == famId).ToList();
                var mals = _males.Where(a => a.FamilyId == famId).ToList();
                if (fems.Count == 0 || mals.Count == 0) continue;

                foreach (var f in fems)
                {
                    var m = FindBestMaleCandidateForMother(
                        f,
                        mals,
                        requireDifferentFamily: false,
                        requireSameFamily: true);

                    if (m != null)
                    {
                        mother = f;
                        father = m;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    public string DebugWhyNoPairs(
        IList<string> familyIds,
        float minHealth,
        int minAgeTurns,
        int maxAgeTurns)
    {
        var lines = new List<string>();
        lines.Add($"[PairingService.Debug] families={familyIds.Count} minH={minHealth} age=({minAgeTurns}-{maxAgeTurns})");

        bool IsElig(Individual p) => IsEligibleByAgeWindow(p, minHealth, minAgeTurns, maxAgeTurns);

        foreach (var fid in familyIds)
        {
            if (string.IsNullOrEmpty(fid))
            {
                lines.Add($"- family:<null>");
                continue;
            }

            var fam = _famRepo.GetById(fid);
            if (fam == null)
            {
                lines.Add($"- family:{fid} -> MISSING");
                continue;
            }

            var a = FindById(fam.PartnerAId);
            var b = FindById(fam.PartnerBId);

            var members = _indRepo.All.Where(p => p != null && p.FamilyId == fid && p.IsAlive).ToList();
            int eligFem = members.Count(p => p.Gender == Gender.Female && IsElig(p));
            int eligMal = members.Count(p => p.Gender == Gender.Male && IsElig(p));

            string partnerInfo =
                $"partners: A={fam.PartnerAId}({a?.Gender}/{a?.AgeInTurns}/{a?.Health01:F2}/{a?.AggregatedAgeGroup}) " +
                $"B={fam.PartnerBId}({b?.Gender}/{b?.AgeInTurns}/{b?.Health01:F2}/{b?.AggregatedAgeGroup})";

            lines.Add($"- family:{fid} eligible(F/M)={eligFem}/{eligMal}; {partnerInfo}");

            if (a != null && b != null)
            {
                var mother = (a.Gender == Gender.Female) ? a : (b.Gender == Gender.Female) ? b : null;
                var father = (a.Gender == Gender.Male) ? a : (b.Gender == Gender.Male) ? b : null;

                if (mother == null || father == null)
                {
                    lines.Add("  • seeded couple is not (F,M) → skip");
                }
                else
                {
                    bool mAdult = IsElig(mother);
                    bool fAdult = IsElig(father);

                    if (!mAdult || !fAdult)
                        lines.Add($"  • not eligible by thresholds: motherOK={mAdult} fatherOK={fAdult}");

                    bool can = _pregnancy.CanStartPregnancy(mother, father);
                    if (!can)
                        lines.Add("  • CanStartPregnancy returned FALSE (pregnant/cooldown/needs/reservation?)");
                }
            }
            else
            {
                lines.Add("  • family has no complete (PartnerA, PartnerB) pair");
            }
        }

        var elig = _indRepo.All.Where(p =>
            p != null &&
            p.IsAlive &&
            !string.IsNullOrEmpty(p.FamilyId) &&
            familyIds.Contains(p.FamilyId) &&
            IsElig(p)).ToList();

        int totalEligFem = elig.Count(p => p.Gender == Gender.Female);
        int totalEligMal = elig.Count(p => p.Gender == Gender.Male);

        lines.Add($"[PairingService.Debug] eligible adults across housed families: F={totalEligFem} M={totalEligMal} total={elig.Count}");
        return string.Join("\n", lines);
    }

    // ---------------------------------------------------------------------
    // BASIC HELPERS
    // ---------------------------------------------------------------------

    private bool IsEligibleByAgeWindow(Individual p, float minHealth, int minAge, int maxAge)
    {
        if (p == null || !p.IsAlive) return false;
        if (p.AgeInTurns < minAge || p.AgeInTurns > maxAge) return false;
        if (p.Health01 < minHealth) return false;
        return true;
    }

    private Individual FindById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        var list = _indRepo.All;
        for (int i = 0; i < list.Count; i++)
        {
            var p = list[i];
            if (p != null && p.Id == id)
                return p;
        }

        return null;
    }
}