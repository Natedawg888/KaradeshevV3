using System;
using System.Collections.Generic;
using UnityEngine;

public class PregnancyService : IPregnancyService
{
    private readonly FamilySimConfig _config;
    private readonly IBirthService _birthService;
    private readonly PlayersPopulationManager _pop;
    private readonly RandomService _rng;
    private readonly IndividualRepository _indRepo;
    private readonly FamilyRepository _famRepo; // optional

    public event Action<string> OnPregnancyFailed;

    // state
    private readonly Dictionary<string, int> _parentCooldowns = new();                 // Individual.Id -> turns
    private readonly HashSet<string> _pregnantMothers = new();                         // Individual.Id
    private readonly Dictionary<string, string> _gestationReservationByMother = new(); // motherId -> reservationId
    private readonly Dictionary<string, string> _preferredBirthFamily = new();

    public bool IsMotherCurrentlyPregnant(string motherId) => _pregnantMothers.Contains(motherId);

    public PregnancyService(FamilySimConfig config,
                            IBirthService birthService,
                            PlayersPopulationManager pop,
                            RandomService rng,
                            IndividualRepository indRepo)
    {
        _config = config;
        _birthService = birthService;
        _pop = pop;
        _rng = rng;
        _indRepo = indRepo;
        _famRepo = null; // optional
    }

    public PregnancyService(FamilySimConfig config,
                            IBirthService birthService,
                            PlayersPopulationManager pop,
                            RandomService rng,
                            IndividualRepository indRepo,
                            FamilyRepository famRepo)
        : this(config, birthService, pop, rng, indRepo)
    {
        _famRepo = famRepo;
    }

    private sealed class PregnancyRecord
    {
        public string MotherId;
        public string FatherId;  // null allowed
        public int TotalTurns;
        public int RemainingTurns;
    }

    // motherId -> record
    private readonly Dictionary<string, PregnancyRecord> _pregnancies = new();

    private void TagPregnancyReservation(string motherId, string reservationId)
    {
        if (_pop == null || string.IsNullOrWhiteSpace(motherId) || string.IsNullOrWhiteSpace(reservationId))
            return;

        _pop.UpdateReservationMetadata(
            reservationId,
            PopulationReservationKind.Pregnancy,
            motherId,
            nameof(PregnancyService));
    }

    public void RetagAllPregnancyReservationsFromRuntime()
    {
        foreach (var kv in _gestationReservationByMother)
        {
            if (string.IsNullOrWhiteSpace(kv.Key) || string.IsNullOrWhiteSpace(kv.Value))
                continue;

            TagPregnancyReservation(kv.Key, kv.Value);
        }
    }

    public void TickOneTurn()
    {
        // 1) Cooldowns
        if (_parentCooldowns.Count > 0)
        {
            var keys = new List<string>(_parentCooldowns.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                var id = keys[i];
                if (_parentCooldowns.TryGetValue(id, out var cd))
                {
                    cd = Mathf.Max(0, cd - 1);
                    if (cd == 0) _parentCooldowns.Remove(id);
                    else _parentCooldowns[id] = cd;
                }
            }
        }

        // 2) Pregnancies
        if (_pregnancies.Count == 0) return;

        var mothers = new List<string>(_pregnancies.Keys); // defensive copy
        for (int i = 0; i < mothers.Count; i++)
        {
            var motherId = mothers[i];
            if (!_pregnancies.TryGetValue(motherId, out var rec)) continue;

            var mother = GetIndividualOrNull(motherId);
            var father = GetIndividualOrNull(rec.FatherId);

            if (_gestationReservationByMother.TryGetValue(motherId, out var gestationResId))
                TagPregnancyReservation(motherId, gestationResId);

            // If mother died externally, abort immediately
            if (mother == null || !mother.IsAlive)
            {
                AbortPregnancy(motherId);
                _pregnancies.Remove(motherId);

                OnPregnancyFailed?.Invoke(motherId);
                continue;
            }

            // Per-turn failure probability that DECREASES as gestation progresses
            float progress01 = 1f - (rec.RemainingTurns / (float)rec.TotalTurns); // 0 at start → 1 at birth
            float baseFail = GetFailureChanceFromHealth(mother, father);           // baseline (0..1)

            float exponent = (_config != null && _config.failureDecayExponent > 0f)
                ? _config.failureDecayExponent
                : 1f;

            float decay = Mathf.Pow(
                Mathf.Clamp01(1f - progress01),
                Mathf.Max(0.0001f, exponent)); // 1 → 0 across pregnancy

            float turnFail = Mathf.Clamp01(baseFail * decay);

            if (_rng.Value01() < turnFail)
            {
                float pDeathOnFail = GetFailureDeathChanceFromHealth(mother, father);
                bool motherDies = _rng.Value01() < pDeathOnFail;
                if (motherDies) mother.IsAlive = false;

                ApplyCooldowns(mother, father);
                ReleaseReservationIfAny(motherId);

                _pregnantMothers.Remove(motherId);
                _pregnancies.Remove(motherId);

                CivilizationHappinessSystem.Instance?.NotifyPregnancyFailure(motherDies);

                OnPregnancyFailed?.Invoke(motherId);
                continue;
            }

            // Progress gestation
            rec.RemainingTurns = Mathf.Max(0, rec.RemainingTurns - 1);

            // Completed this turn → perform birth (no extra failure roll)
            if (rec.RemainingTurns == 0)
            {
                _ = PerformBirthNoFailure(mother, father);

                ApplyCooldowns(mother, father);
                ReleaseReservationIfAny(motherId);

                _pregnantMothers.Remove(motherId);
                _pregnancies.Remove(motherId);
            }
        }
    }

    public bool CanStartPregnancy(Individual mother, Individual father)
    {
        if (mother == null || !mother.IsAlive) return false;
        if (mother.Gender != Gender.Female) return false;
        if (mother.IsBusy) return false;

        if (_config != null)
        {
            if (mother.AgeInTurns < _config.minAdultAgeForBirthTurns ||
                mother.AgeInTurns > _config.maxAdultAgeForBirthTurns) return false;
            if (mother.Health01 < _config.minHealthForBirth) return false;
            if (_config.onePregnancyPerMother && _pregnantMothers.Contains(mother.Id)) return false;
        }

        if (_parentCooldowns.TryGetValue(mother.Id, out var momCd) && momCd > 0) return false;

        if (father != null)
        {
            if (!father.IsAlive) return false;
            if (father.Gender != Gender.Male) return false;
            if (father.IsBusy) return false;

            if (_config != null)
            {
                if (father.AgeInTurns < _config.minAdultAgeForBirthTurns ||
                    father.AgeInTurns > _config.maxAdultAgeForBirthTurns) return false;
                if (father.Health01 < _config.minHealthForBirth) return false;
            }

            if (_parentCooldowns.TryGetValue(father.Id, out var dadCd) && dadCd > 0) return false;
        }

        return true;
    }

    private float GetNewbornDeathChance(Individual mother, Individual father)
    {
        if (_config == null) return 0f;

        float baseP = Mathf.Clamp01(_config.newbornDeathChanceOnBirth);

        float hEff = GetEffectiveHealth01(mother, father);
        float t = RemapHealthTo01(hEff);

        float targetAtMin = Mathf.Clamp01(_config.newbornDeathAtMinHealth);
        float targetAtMax = Mathf.Clamp01(_config.newbornDeathAtMaxHealth);

        float result;

        if (baseP <= 0f)
        {
            result = Mathf.Lerp(targetAtMin, targetAtMax, t);
        }
        else
        {
            float scaleMin = targetAtMin / baseP;
            float scaleMax = targetAtMax / baseP;

            float scaled = baseP * Mathf.Lerp(scaleMin, scaleMax, t);
            result = Mathf.Clamp01(scaled);
        }

        // Religion blessing reduces newborn death risk.
        return ApplyBirthSuccessReduction(result);
    }

    public bool IsOnParentCooldown(string individualId)
    {
        if (string.IsNullOrEmpty(individualId)) return false;
        return _parentCooldowns.TryGetValue(individualId, out var cd) && cd > 0;
    }

    public void SetPreferredBirthFamily(string motherId, string familyId)
    {
        if (string.IsNullOrEmpty(motherId) || string.IsNullOrEmpty(familyId)) return;
        _preferredBirthFamily[motherId] = familyId;
    }

    private bool IsFatherInMothersHousehold(Individual mother, Individual father)
    {
        if (mother == null || father == null) return false;
        if (father.Gender != Gender.Male) return false;

        if (string.IsNullOrEmpty(mother.FamilyId) || father.FamilyId != mother.FamilyId)
            return false;

        if (_famRepo != null)
        {
            var fam = _famRepo.GetById(mother.FamilyId);
            if (fam == null) return false;

            bool ok =
                (fam.PartnerAId == father.Id && fam.PartnerBId == mother.Id)
             || (fam.PartnerAId == mother.Id && fam.PartnerBId == father.Id); // legacy
            if (!ok) return false;
        }

        return true;
    }

    public bool TryStartPregnancyWithReservation(
        Individual mother,
        Individual father,
        int gestationTurns,
        out string reservationId)
    {
        reservationId = null;

        if (!CanStartPregnancy(mother, father)) return false;

        if (father == null) return false;
        if (!IsFatherInMothersHousehold(mother, father)) return false;

        if (mother.IsBusy) return false;
        if (father.IsBusy) return false;

        // If mother is already reserved somewhere else (eg paused production),
        // detach her first and backfill that reservation if possible.
        bool hadExistingReservation = _pop.IsIndividualReservedAnywhere(mother.Id);
        bool allReservationsReplaced = true;

        if (hadExistingReservation)
        {
            if (!_pop.TryDetachIndividualFromExistingReservations(mother.Id, out allReservationsReplaced))
                return false;
        }

        // Reserve the exact mother for gestation
        if (!_pop.TryReservePopulationForIndividuals(
                new[] { mother },
                PopulationReservationKind.Pregnancy,
                mother.Id,
                nameof(PregnancyService),
                out reservationId))
        {
            return false;
        }

        if (!allReservationsReplaced)
        {
            Debug.Log(
                $"[PregnancyService] Mother {mother.Id} was claimed for gestation. " +
                $"One or more previous reservations could not be backfilled and may fail validation."
            );
        }

        _gestationReservationByMother[mother.Id] = reservationId;
        TagPregnancyReservation(mother.Id, reservationId);

        var rec = new PregnancyRecord
        {
            MotherId = mother.Id,
            FatherId = father.Id,
            TotalTurns = Mathf.Max(1, gestationTurns),
            RemainingTurns = Mathf.Max(1, gestationTurns)
        };

        _pregnancies[mother.Id] = rec;
        _pregnantMothers.Add(mother.Id);

        PlayersPopulationManager.Instance?.ForceSyncUI();
        return true;
    }

    // Kept for compatibility if other systems call it directly.
    public void BeginPregnancy(Individual mother)
    {
        if (mother == null) return;
        _pregnantMothers.Add(mother.Id);
    }

    public void AbortPregnancy(string motherId)
    {
        if (string.IsNullOrEmpty(motherId)) return;

        _pregnantMothers.Remove(motherId);

        if (_gestationReservationByMother.TryGetValue(motherId, out var resId))
        {
            _pop.ReleaseReservation(resId);
            _gestationReservationByMother.Remove(motherId);
        }

        _pregnancies.Remove(motherId);
    }

    public int ResolveBirthAndReturnChildrenCount(Individual mother, Individual father)
    {
        if (mother == null || !mother.IsAlive) return 0;

        if (_pregnancies.TryGetValue(mother.Id, out var _))
        {
            var born = PerformBirthNoFailure(mother, father);
            ApplyCooldowns(mother, father);
            ReleaseReservationIfAny(mother.Id);
            AbortPregnancy(mother.Id);
            return born;
        }

        return 0;
    }

    private int PerformBirthNoFailure(Individual mother, Individual father)
    {
        if (mother == null || !mother.IsAlive) return 0;

        _preferredBirthFamily.TryGetValue(mother.Id, out var forcedFamilyId);

        int bornAlive = 0;
        int babiesDied = 0;

        int babies = RollMultiples();
        float pNewbornDie = GetNewbornDeathChance(mother, father);

        for (int i = 0; i < babies; i++)
        {
            if (_birthService.TryCreateNewbornFromParents(mother, father, forcedFamilyId, out var baby, out _))
            {
                if (_rng.Value01() < pNewbornDie)
                {
                    if (baby != null) _indRepo.Kill(baby);
                    babiesDied++;
                }
                else
                {
                    bornAlive++;
                }
            }
            else break;
        }

        if (!string.IsNullOrEmpty(forcedFamilyId))
            _preferredBirthFamily.Remove(mother.Id);

        bool motherDiedOnSuccess = false;
        float pMomDieOnSuccess = GetSuccessBirthMotherDeathChanceFromHealth(mother);
        if (_rng.Value01() < pMomDieOnSuccess)
        {
            mother.IsAlive = false;
            motherDiedOnSuccess = true;
        }

        CivilizationHappinessSystem.Instance?.NotifyBirthSuccess(bornAlive, babiesDied, motherDiedOnSuccess);

        return bornAlive;
    }

    private Individual GetIndividualOrNull(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        var list = _indRepo.All;
        for (int i = 0; i < list.Count; i++)
        {
            var p = list[i];
            if (p != null && p.Id == id) return p;
        }
        return null;
    }

    private void ApplyCooldowns(Individual mother, Individual father)
    {
        if (mother == null) return;

        (int cdMin, int cdMax) = GetCooldownRangeAdjustedByNeeds(mother, father);
        int cd = UnityEngine.Random.Range(cdMin, cdMax + 1);
        if (cd > 0)
        {
            if (mother.IsAlive) _parentCooldowns[mother.Id] = cd;
            if (father != null && father.IsAlive) _parentCooldowns[father.Id] = cd;
        }
    }

    private void ReleaseReservationIfAny(string motherId)
    {
        if (_gestationReservationByMother.TryGetValue(motherId, out var resId))
        {
            _pop.ReleaseReservation(resId);
            _gestationReservationByMother.Remove(motherId);
        }
        PlayersPopulationManager.Instance?.ForceSyncUI();
    }

    private (int min, int max) GetCooldownRangeAdjustedByNeeds(Individual mother, Individual father)
    {
        int baseMin = Mathf.Max(0, _config != null ? _config.minParentCooldownTurns : 0);
        int baseMax = Mathf.Max(baseMin, _config != null ? _config.maxParentCooldownTurns : baseMin);

        float need = GetEffectiveNeed01(mother, father);

        float extraMinF = need * (_config != null ? _config.cooldownExtraMinAtNeed1 : 0f);
        float extraMaxF = need * (_config != null ? _config.cooldownExtraMaxAtNeed1 : 0f);

        int outMin = baseMin + Mathf.RoundToInt(extraMinF);
        int outMax = Mathf.Max(outMin, baseMax + Mathf.RoundToInt(extraMaxF));
        return (outMin, outMax);
    }

    private float GetEffectiveNeed01(Individual mother, Individual father)
    {
        float nm = GetNeed01ForIndividual(mother);
        float nf = (father != null) ? GetNeed01ForIndividual(father) : nm;
        float w = Mathf.Clamp01(_config != null ? _config.fatherNeedWeight : 0f);
        return Mathf.Clamp01(Mathf.Lerp(nm, nf, w));
    }

    private float GetNeed01ForIndividual(Individual ind)
    {
        if (ind == null) return 0f;

        var groups = PlayersPopulationManager.Instance?.AllPopulations;
        if (groups == null) return 0f;

        for (int i = 0; i < groups.Count; i++)
        {
            var g = groups[i];
            if (g != null && g.GroupID == ind.AggregatedGroupGuid)
            {
                return Mathf.Clamp01((g.hungerLevel + g.thirstLevel) * 0.5f);
            }
        }
        return 0f;
    }

    private float GetEffectiveHealth01(Individual mother, Individual father)
    {
        float hMom = Mathf.Clamp01(mother?.Health01 ?? 0f);
        float hDad = Mathf.Clamp01(father?.Health01 ?? hMom);
        float w = Mathf.Clamp01(_config != null ? _config.fatherHealthWeight : 0f);
        float hEff = Mathf.Lerp(hMom, hDad, w);
        return Mathf.Clamp01(hEff);
    }

    private float RemapHealthTo01(float hEff)
    {
        float min = Mathf.Clamp01(_config != null ? _config.minHealthForBirth : 0f);
        return Mathf.InverseLerp(min, 1f, hEff);
    }

    private float GetFailureChanceFromHealth(Individual mother, Individual father)
    {
        if (_config == null) return 0f;

        float baseFail = Mathf.Clamp01(_config.pregnancyFailureChance);

        float hEff = GetEffectiveHealth01(mother, father);
        float t = RemapHealthTo01(hEff);

        float targetAtMin = Mathf.Clamp01(_config.failureAtMinHealth);
        float targetAtMax = Mathf.Clamp01(_config.failureAtMaxHealth);

        float result;

        if (baseFail <= 0f)
        {
            result = Mathf.Lerp(targetAtMin, targetAtMax, t);
        }
        else
        {
            float scaleMin = targetAtMin / baseFail;
            float scaleMax = targetAtMax / baseFail;

            float scaled = baseFail * Mathf.Lerp(scaleMin, scaleMax, t);
            result = Mathf.Clamp01(scaled);
        }

        // Religion blessing reduces pregnancy failure risk.
        return ApplyBirthSuccessReduction(result);
    }

    private float GetFailureDeathChanceFromHealth(Individual mother, Individual father)
    {
        if (_config == null) return 0f;

        float baseDeath = Mathf.Clamp01(_config.failureDeathChance);

        float hEff = GetEffectiveHealth01(mother, father);
        float t = RemapHealthTo01(hEff);

        float targetAtMin = Mathf.Clamp01(_config.failureDeathAtMinHealth);
        float targetAtMax = Mathf.Clamp01(_config.failureDeathAtMaxHealth);

        if (baseDeath <= 0f)
            return Mathf.Lerp(targetAtMin, targetAtMax, t);

        float scaleMin = targetAtMin / baseDeath;
        float scaleMax = targetAtMax / baseDeath;

        float scaled = baseDeath * Mathf.Lerp(scaleMin, scaleMax, t);
        return Mathf.Clamp01(scaled);
    }

    private float GetSuccessBirthMotherDeathChanceFromHealth(Individual mother)
    {
        if (_config == null || mother == null) return 0f;

        float baseChance = Mathf.Clamp01(_config.motherDeathOnSuccessfulBirthChance);
        float targetAtMin = Mathf.Clamp01(_config.successMotherDeathAtMinHealth);
        float targetAtMax = Mathf.Clamp01(_config.successMotherDeathAtMaxHealth);

        float h = Mathf.Clamp01(mother.Health01);
        float t = RemapHealthTo01(h);

        if (baseChance <= 0f)
            return Mathf.Lerp(targetAtMin, targetAtMax, t);

        float scaleMin = targetAtMin / baseChance;
        float scaleMax = targetAtMax / baseChance;

        float scaled = baseChance * Mathf.Lerp(scaleMin, scaleMax, t);
        return Mathf.Clamp01(scaled);
    }

    private int RollMultiples()
    {
        float baseTwinChance = _config != null ? _config.twinChance : 0f;
        float baseTripletChance = _config != null ? _config.tripletChance : 0f;

        float twinChance = Mathf.Clamp01(baseTwinChance + GetReligionTwinChanceBonus());
        float tripletChance = Mathf.Clamp01(baseTripletChance + GetReligionTripletChanceBonus());

        // Roll once so triplets are not accidentally suppressed by the twin check.
        float roll = _rng.Value01();

        if (roll < tripletChance)
            return Mathf.Clamp(3, 1, Mathf.Max(1, _config != null ? _config.maxMultiples : 1));

        if (roll < tripletChance + twinChance)
            return Mathf.Clamp(2, 1, Mathf.Max(1, _config != null ? _config.maxMultiples : 1));

        return 1;
    }

    public int GetParentCooldownTurnsLeft(string individualId)
    {
        if (string.IsNullOrEmpty(individualId)) return 0;
        return _parentCooldowns.TryGetValue(individualId, out var cd) ? Mathf.Max(0, cd) : 0;
    }

    public PregnancyServiceSaveData SaveState()
    {
        PregnancyServiceSaveData data = new PregnancyServiceSaveData();

        foreach (var kv in _parentCooldowns)
        {
            if (string.IsNullOrWhiteSpace(kv.Key))
                continue;

            data.parentCooldowns.Add(new StringIntPairSaveData
            {
                key = kv.Key,
                value = kv.Value
            });
        }

        foreach (string motherId in _pregnantMothers)
        {
            if (!string.IsNullOrWhiteSpace(motherId))
                data.pregnantMothers.Add(motherId);
        }

        foreach (var kv in _gestationReservationByMother)
        {
            if (string.IsNullOrWhiteSpace(kv.Key) || string.IsNullOrWhiteSpace(kv.Value))
                continue;

            data.gestationReservationByMother.Add(new StringStringPairSaveData
            {
                key = kv.Key,
                value = kv.Value
            });
        }

        foreach (var kv in _preferredBirthFamily)
        {
            if (string.IsNullOrWhiteSpace(kv.Key) || string.IsNullOrWhiteSpace(kv.Value))
                continue;

            data.preferredBirthFamily.Add(new StringStringPairSaveData
            {
                key = kv.Key,
                value = kv.Value
            });
        }

        foreach (var kv in _pregnancies)
        {
            PregnancyRecord rec = kv.Value;
            if (rec == null || string.IsNullOrWhiteSpace(kv.Key))
                continue;

            data.pregnancies.Add(new PregnancyRecordSaveData
            {
                motherId = rec.MotherId,
                fatherId = rec.FatherId,
                totalTurns = rec.TotalTurns,
                remainingTurns = rec.RemainingTurns
            });
        }

        return data;
    }

    public void LoadState(PregnancyServiceSaveData data)
    {
        _parentCooldowns.Clear();
        _pregnantMothers.Clear();
        _gestationReservationByMother.Clear();
        _preferredBirthFamily.Clear();
        _pregnancies.Clear();

        if (data == null)
            return;

        if (data.parentCooldowns != null)
        {
            for (int i = 0; i < data.parentCooldowns.Count; i++)
            {
                StringIntPairSaveData pair = data.parentCooldowns[i];
                if (pair == null || string.IsNullOrWhiteSpace(pair.key))
                    continue;

                _parentCooldowns[pair.key] = Mathf.Max(0, pair.value);
            }
        }

        if (data.pregnantMothers != null)
        {
            for (int i = 0; i < data.pregnantMothers.Count; i++)
            {
                string motherId = data.pregnantMothers[i];
                if (!string.IsNullOrWhiteSpace(motherId))
                    _pregnantMothers.Add(motherId);
            }
        }

        if (data.gestationReservationByMother != null)
        {
            for (int i = 0; i < data.gestationReservationByMother.Count; i++)
            {
                StringStringPairSaveData pair = data.gestationReservationByMother[i];
                if (pair == null || string.IsNullOrWhiteSpace(pair.key) || string.IsNullOrWhiteSpace(pair.value))
                    continue;

                _gestationReservationByMother[pair.key] = pair.value;
                TagPregnancyReservation(pair.key, pair.value);
            }
        }

        if (data.preferredBirthFamily != null)
        {
            for (int i = 0; i < data.preferredBirthFamily.Count; i++)
            {
                StringStringPairSaveData pair = data.preferredBirthFamily[i];
                if (pair == null || string.IsNullOrWhiteSpace(pair.key) || string.IsNullOrWhiteSpace(pair.value))
                    continue;

                _preferredBirthFamily[pair.key] = pair.value;
            }
        }

        if (data.pregnancies != null)
        {
            for (int i = 0; i < data.pregnancies.Count; i++)
            {
                PregnancyRecordSaveData saved = data.pregnancies[i];
                if (saved == null || string.IsNullOrWhiteSpace(saved.motherId))
                    continue;

                PregnancyRecord rec = new PregnancyRecord
                {
                    MotherId = saved.motherId,
                    FatherId = saved.fatherId,
                    TotalTurns = Mathf.Max(1, saved.totalTurns),
                    RemainingTurns = Mathf.Clamp(saved.remainingTurns, 0, Mathf.Max(1, saved.totalTurns))
                };

                _pregnancies[saved.motherId] = rec;
                _pregnantMothers.Add(saved.motherId);
            }
        }
    }

    private float GetReligionBirthSuccessBonus()
    {
        PlayerReligionManager religion = PlayerReligionManager.Instance;
        if (religion == null)
            return 0f;

        return religion.GetAdditiveSum(SpiritEffectType.BirthSuccessChanceAdd);
    }

    private float GetReligionTwinChanceBonus()
    {
        PlayerReligionManager religion = PlayerReligionManager.Instance;
        if (religion == null)
            return 0f;

        return religion.GetAdditiveSum(SpiritEffectType.TwinChanceAdd);
    }

    private float GetReligionTripletChanceBonus()
    {
        PlayerReligionManager religion = PlayerReligionManager.Instance;
        if (religion == null)
            return 0f;

        return religion.GetAdditiveSum(SpiritEffectType.TripletChanceAdd);
    }

    private float ApplyBirthSuccessReduction(float chance01)
    {
        float bonus = GetReligionBirthSuccessBonus();
        return Mathf.Clamp01(chance01 - bonus);
    }
}