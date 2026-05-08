using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerFamilySimulationManager : MonoBehaviour
{
    public static PlayerFamilySimulationManager Instance { get; private set; }

    public enum ChildFamilyAffiliation { Mother, Father, Random }

    [Header("Config")]
    public FamilySimConfig config;

    [Header("Debug (read-only)")]
    [SerializeField] private List<Individual> debugIndividualsView = new();
    [SerializeField] private List<Family> debugFamiliesView = new();

    // Data
    private IndividualRepository _indRepo;
    private FamilyRepository _famRepo;

    [Header("External Managers")]
    [SerializeField] private PlayersPopulationManager playerPop;
    [SerializeField] private GeneralPopulationManager general;

    private PlayersPopulationManager PlayerPop => playerPop != null ? playerPop : PlayersPopulationManager.Instance;
    private GeneralPopulationManager General => general != null ? general : GeneralPopulationManager.Instance;

    // -------- Batched family advance --------
    private readonly List<string> _advanceFamilyOrder = new();
    private readonly Dictionary<string, List<Individual>> _advanceMembersByFamily = new();
    private readonly List<Individual> _advanceUnassignedMembers = new();

    private int _advanceFamilyCursor = 0;
    private bool _advanceTurnInProgress = false;
    private bool _advanceProcessedUnassigned = false;
    private bool _advanceStagesChanged = false;
    private bool _advanceAnyTaskAvailabilityChanged = false;

    public bool IsAdvanceFamiliesInProgress => _advanceTurnInProgress;

    // RNG (for repo bootstrapping etc.)
    private System.Random _rng;

    // === Services ===
    private RandomService _randSvc;
    private IBirthService _birthSvc;
    private IPregnancyService _pregnancySvc;
    private IPairingService _pairingSvc;
    private IDeathReconciliationService _deathReconSvc;
    private IHouseholdService _householdSvc;

    // temp buffers
    private readonly List<Individual> _tmpChildrenNoFamily = new();
    private readonly HashSet<string> _usedFamilyNames = new();

    private readonly List<Individual> _tmpDeathCandidates = new(256);
    private readonly List<Individual> _tmpTaskCandidates = new(256);
    private readonly List<Individual> _tmpLivingPeople = new(256);
    private readonly List<Individual> _tmpAdults = new(256);
    private readonly List<Individual> _tmpMales = new(128);
    private readonly List<Individual> _tmpFemales = new(128);
    private readonly List<string> _tmpKillIds = new();
    private readonly Dictionary<Guid, int> _tmpAvailByGroup = new();
    private readonly HashSet<string> _tmpAddedFamilyIds = new(StringComparer.Ordinal);

    private ProductionBuildingControl[] _cachedProductionBuildings;

    [Header("Batch Settings")]
    [SerializeField] private int familiesToProcessPerFrame = 2;

    [SerializeField] private bool debugFamilyPassPopulationDelta = true;

    private void Awake()
    {
        if (Instance && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (PlayerPop == null || General == null)
        {
            //Debug.LogError("[FamilySim] Missing required managers in scene (PlayersPopulationManager / GeneralPopulationManager).");
            enabled = false;
            return;
        }

        EnsureRepositoriesExist();
        EnsureServicesExist();
    }

    private void Start()
    {
        BootstrapFromAggregates();
        RebuildUsedFamilyNames();

        BuildInitialFamiliesFromAdults();
        AssignChildrenToFamiliesOnStart();
        EnsureAdultsHaveFamilies();

        if (_pairingSvc is PairingService ps)
        {
            ps.CleanupInvalidPairs();
            ps.SeedPairsFromExistingFamilies();
        }

        _indRepo.RebuildIndexes();
        CacheProductionBuildings();
        RefreshDebugViews();
    }

    private void Update()
    {
        if (!_advanceTurnInProgress)
            return;

        TickAdvanceFamiliesBatch(familiesToProcessPerFrame);
    }

    private void EnsureRepositoriesExist()
    {
        if (_rng == null)
            _rng = (config != null && config.randomSeed != 0)
                ? new System.Random(config.randomSeed)
                : new System.Random();

        if (_indRepo == null)
        {
            int maxInd = config != null ? config.maxIndividuals : 5000;
            _indRepo = new IndividualRepository(maxInd, _rng);
        }

        if (_famRepo == null)
            _famRepo = new FamilyRepository(_rng);
    }

    private void EnsureServicesExist()
    {
        if (_randSvc == null)
            _randSvc = new RandomService(config != null ? config.randomSeed : 0);

        if (_birthSvc == null)
            _birthSvc = new BirthService(_indRepo, _famRepo, PlayerPop);

        if (_pregnancySvc == null)
            _pregnancySvc = new PregnancyService(config, _birthSvc, PlayerPop, _randSvc, _indRepo, _famRepo);

        if (_pairingSvc == null)
            _pairingSvc = new PairingService(_indRepo, _famRepo, config, _pregnancySvc, _randSvc);

        if (_deathReconSvc == null)
            _deathReconSvc = new DeathReconciliationService();

        if (_householdSvc == null)
            _householdSvc = new HouseholdService(_randSvc);
    }

    private void CacheProductionBuildings()
    {
        _cachedProductionBuildings = FindObjectsOfType<ProductionBuildingControl>(true);
    }

    private void MarkPopulationDirty()
    {
        SaveSystem.MarkSectionDirty(SaveSectionKeys.Population);
    }

    private void BootstrapFromAggregates()
    {
        _indRepo.Clear();
        _famRepo.Clear();

        var all = PlayerPop.AllPopulations;
        int max = _indRepo.MaxIndividuals;

        for (int gIdx = 0; gIdx < all.Count; gIdx++)
        {
            var g = all[gIdx];
            if (g == null)
                continue;

            if (_indRepo.Count >= max)
                break;

            int remain = Math.Max(0, max - _indRepo.Count);
            int toCreate = Mathf.Min(g.count, remain);

            for (int i = 0; i < toCreate; i++)
            {
                var ind = new Individual(g.gender, g.averageAgeInTurns, g.averageHealth, g.ageGroup, g.GroupID, 0);
                ind.LineageId = LineageUtils.NewGene(32, _rng);
                _indRepo.TryAdd(ind);
            }
        }
    }

    private void BuildInitialFamiliesFromAdults()
    {
        _famRepo.Clear();
        RebuildUsedFamilyNames();

        _tmpLivingPeople.Clear();
        _tmpAdults.Clear();
        _tmpMales.Clear();
        _tmpFemales.Clear();

        var all = _indRepo.All;
        for (int i = 0; i < all.Count; i++)
        {
            var p = all[i];
            if (p == null || !p.IsAlive)
                continue;

            _tmpLivingPeople.Add(p);

            if (!string.IsNullOrEmpty(p.FamilyId))
            {
                _indRepo.SetFamily(p, null);
                p.Surname = null;
            }
            else
            {
                p.Surname = null;
            }

            if (p.AggregatedAgeGroup == AgeGroup.Adult)
            {
                _tmpAdults.Add(p);

                if (p.Gender == Gender.Male)
                    _tmpMales.Add(p);
                else if (p.Gender == Gender.Female)
                    _tmpFemales.Add(p);
            }
        }

        if (_tmpLivingPeople.Count == 0 || _tmpAdults.Count == 0)
            return;

        _indRepo.Shuffle(_tmpMales);
        _indRepo.Shuffle(_tmpFemales);

        int desiredFamilies = GetDesiredInitialFamilyCount(_tmpAdults.Count);
        int maleIndex = 0;
        int femaleIndex = 0;

        for (int i = 0; i < desiredFamilies; i++)
        {
            Individual male = maleIndex < _tmpMales.Count ? _tmpMales[maleIndex++] : null;
            Individual female = femaleIndex < _tmpFemales.Count ? _tmpFemales[femaleIndex++] : null;

            if (male == null && female == null)
                break;

            var fam = _famRepo.CreateFamily(
                male != null ? male.Id : null,
                female != null ? female.Id : null,
                GenerateUniqueFamilyName());

            if (male != null)
                _indRepo.SetFamily(male, fam.FamilyId, fam.FamilyName);

            if (female != null)
                _indRepo.SetFamily(female, fam.FamilyId, fam.FamilyName);
        }
    }

    private int GetDesiredInitialFamilyCount(int livingAdults)
    {
        if (livingAdults <= 0)
            return 0;

        int desired = PlayerPop != null ? PlayerPop.startingFamilyCount : 0;

        if (desired <= 0)
        {
            int startPop = PlayerPop != null ? PlayerPop.startingPopulation : livingAdults;
            desired = Mathf.Max(1, startPop / 3);
        }

        return Mathf.Clamp(desired, 1, livingAdults);
    }

    private void RebuildUsedFamilyNames()
    {
        _usedFamilyNames.Clear();

        var families = _famRepo.All;
        for (int i = 0; i < families.Count; i++)
        {
            var f = families[i];
            if (f != null && !string.IsNullOrWhiteSpace(f.FamilyName))
                _usedFamilyNames.Add(f.FamilyName);
        }
    }

    private string GenerateUniqueFamilyName()
    {
        for (int attempt = 0; attempt < 64; attempt++)
        {
            string candidate = NameGenerator.Instance != null
                ? NameGenerator.Instance.NextFamilyName()
                : "Family";

            if (_usedFamilyNames.Add(candidate))
                return candidate;
        }

        int n = 2;
        string baseName = NameGenerator.Instance != null
            ? NameGenerator.Instance.NextFamilyName()
            : "Family";

        string withSuffix = baseName;
        while (!_usedFamilyNames.Add(withSuffix))
            withSuffix = $"{baseName}-{n++}";

        return withSuffix;
    }

    private void AssignChildrenToFamiliesOnStart()
    {
        _tmpChildrenNoFamily.Clear();

        var households = new List<Family>(Mathf.Max(1, _famRepo.Count));
        var families = _famRepo.All;

        for (int i = 0; i < families.Count; i++)
        {
            var f = families[i];
            if (f == null)
                continue;

            if (!string.IsNullOrEmpty(f.PartnerAId) || !string.IsNullOrEmpty(f.PartnerBId))
                households.Add(f);
        }

        if (households.Count == 0)
            households.Add(_famRepo.CreateFamily(null, null, GenerateUniqueFamilyName()));

        var all = _indRepo.All;
        for (int i = 0; i < all.Count; i++)
        {
            var p = all[i];
            if (p == null || !p.IsAlive)
                continue;

            if (p.AggregatedAgeGroup != AgeGroup.Child)
                continue;

            if (!string.IsNullOrEmpty(p.FamilyId))
                continue;

            _tmpChildrenNoFamily.Add(p);
        }

        _indRepo.Shuffle(_tmpChildrenNoFamily);

        for (int i = 0; i < _tmpChildrenNoFamily.Count; i++)
        {
            var child = _tmpChildrenNoFamily[i];
            var fam = households[i % households.Count];

            _indRepo.SetFamily(child, fam.FamilyId, fam.FamilyName);

            if (fam.ChildrenIds != null && !fam.ChildrenIds.Contains(child.Id))
                fam.ChildrenIds.Add(child.Id);
        }
    }

    private void EnsureAdultsHaveFamilies()
    {
        var all = _indRepo.All;
        for (int i = 0; i < all.Count; i++)
        {
            var p = all[i];
            if (p == null || !p.IsAlive)
                continue;

            if (!string.IsNullOrEmpty(p.FamilyId))
                continue;

            if (p.AggregatedAgeGroup != AgeGroup.Adult)
                continue;

            var fam = _famRepo.CreateFamily(
                p.Gender == Gender.Female ? p.Id : null,
                p.Gender == Gender.Male ? p.Id : null,
                GenerateUniqueFamilyName());

            _indRepo.SetFamily(p, fam.FamilyId, fam.FamilyName);
        }
    }

    // -------- Per-turn --------
    public void AdvanceFamilies(bool _isCycleTick)
    {
        BeginAdvanceFamiliesTurn(_isCycleTick);

        while (!TickAdvanceFamiliesBatch(int.MaxValue))
        {
        }
    }

    public void BeginAdvanceFamiliesTurn(bool _isCycleTick)
    {
        if (_advanceTurnInProgress)
            return;

        int totalBefore = PlayerPop != null ? PlayerPop.GetTotalPopulation() : -1;

        _deathReconSvc.Reconcile(_indRepo, _famRepo, PlayerPop, General);
        int afterReconcile = PlayerPop != null ? PlayerPop.GetTotalPopulation() : -1;
        LogFamilyPassDelta("DeathReconciliation", totalBefore, afterReconcile);

        if (_pairingSvc is PairingService ps)
            ps.CleanupInvalidPairs();

        BuildAdvanceFamilyWorkOrder();

        _advanceFamilyCursor = 0;
        _advanceProcessedUnassigned = false;
        _advanceStagesChanged = false;
        _advanceAnyTaskAvailabilityChanged = false;
        _advanceTurnInProgress = true;

        if (_advanceFamilyOrder.Count == 0 && _advanceUnassignedMembers.Count == 0)
            FinalizeAdvanceFamiliesTurn();
    }

    public bool TickAdvanceFamiliesBatch(int maxFamiliesToProcess)
    {
        if (!_advanceTurnInProgress)
            return true;

        if (maxFamiliesToProcess <= 0)
            maxFamiliesToProcess = 1;

        int processed = 0;

        while (_advanceFamilyCursor < _advanceFamilyOrder.Count && processed < maxFamiliesToProcess)
        {
            string familyId = _advanceFamilyOrder[_advanceFamilyCursor++];

            if (_advanceMembersByFamily.TryGetValue(familyId, out var members) &&
                members != null &&
                members.Count > 0)
            {
                ProcessIndividualsAgingBatch(
                    members,
                    ref _advanceStagesChanged,
                    ref _advanceAnyTaskAvailabilityChanged);
            }

            processed++;
        }

        if (_advanceFamilyCursor >= _advanceFamilyOrder.Count &&
            !_advanceProcessedUnassigned &&
            processed < maxFamiliesToProcess)
        {
            if (_advanceUnassignedMembers.Count > 0)
            {
                ProcessIndividualsAgingBatch(
                    _advanceUnassignedMembers,
                    ref _advanceStagesChanged,
                    ref _advanceAnyTaskAvailabilityChanged);
            }

            _advanceProcessedUnassigned = true;
        }

        bool done =
            _advanceFamilyCursor >= _advanceFamilyOrder.Count &&
            _advanceProcessedUnassigned;

        if (done)
            FinalizeAdvanceFamiliesTurn();

        return done;
    }

    private void BuildAdvanceFamilyWorkOrder()
    {
        _advanceFamilyOrder.Clear();
        _advanceMembersByFamily.Clear();
        _advanceUnassignedMembers.Clear();
        _tmpAddedFamilyIds.Clear();

        var all = _indRepo.All;
        for (int i = 0; i < all.Count; i++)
        {
            var person = all[i];
            if (person == null || !person.IsAlive)
                continue;

            if (string.IsNullOrWhiteSpace(person.FamilyId))
            {
                _advanceUnassignedMembers.Add(person);
                continue;
            }

            if (!_advanceMembersByFamily.TryGetValue(person.FamilyId, out var members))
            {
                members = new List<Individual>();
                _advanceMembersByFamily[person.FamilyId] = members;
            }

            members.Add(person);
        }

        var families = _famRepo.All;
        for (int i = 0; i < families.Count; i++)
        {
            var fam = families[i];
            if (fam == null || string.IsNullOrWhiteSpace(fam.FamilyId))
                continue;

            if (!_advanceMembersByFamily.ContainsKey(fam.FamilyId))
                continue;

            if (_tmpAddedFamilyIds.Add(fam.FamilyId))
                _advanceFamilyOrder.Add(fam.FamilyId);
        }

        foreach (var kvp in _advanceMembersByFamily)
        {
            if (string.IsNullOrWhiteSpace(kvp.Key))
                continue;

            if (_tmpAddedFamilyIds.Add(kvp.Key))
                _advanceFamilyOrder.Add(kvp.Key);
        }
    }

    private void FinalizeAdvanceFamiliesTurn()
    {
        if (_advanceAnyTaskAvailabilityChanged)
            FinalizeTaskAvailabilityChanges();

        if (_advanceStagesChanged)
            _householdSvc.EmancipateTeensToOwnFamilies(_indRepo, _famRepo, GenerateUniqueFamilyName);

        int beforeHouseholds = PlayerPop != null ? PlayerPop.GetTotalPopulation() : -1;
        _householdSvc.MaintainHouseholds(_indRepo, _famRepo, GenerateUniqueFamilyName);
        int afterHouseholds = PlayerPop != null ? PlayerPop.GetTotalPopulation() : -1;
        LogFamilyPassDelta("HouseholdMaintenance", beforeHouseholds, afterHouseholds);

        int beforePregnancy = PlayerPop != null ? PlayerPop.GetTotalPopulation() : -1;
        _pregnancySvc.TickOneTurn();
        int afterPregnancy = PlayerPop != null ? PlayerPop.GetTotalPopulation() : -1;
        LogFamilyPassDelta("PregnancyTick", beforePregnancy, afterPregnancy);

        MarkPopulationDirty();
        RefreshDebugViews();

        _advanceFamilyCursor = 0;
        _advanceTurnInProgress = false;
        _advanceProcessedUnassigned = false;
        _advanceStagesChanged = false;
        _advanceAnyTaskAvailabilityChanged = false;

        _advanceFamilyOrder.Clear();
        _advanceMembersByFamily.Clear();
        _advanceUnassignedMembers.Clear();
    }

    // -------- Public API --------
    public IReadOnlyList<Individual> GetIndividuals() => _indRepo.All;
    public IReadOnlyList<Family> GetFamilies() => _famRepo.All;
    public Family GetFamilyById(string familyId) => _famRepo.GetById(familyId);

    public void AddIndividualForSystems(Individual i)
    {
        _indRepo.TryAdd(i);
    }

    public bool FamilyHasEligibleMother(string familyId)
    {
        if (string.IsNullOrEmpty(familyId))
            return false;

        float minH = config ? config.minHealthForBirth : 0.6f;
        int minA = config ? config.minAdultAgeForBirthTurns : 180;
        int maxA = config ? config.maxAdultAgeForBirthTurns : 525;

        _tmpLivingPeople.Clear();
        _indRepo.CopyAliveByFamilyTo(familyId, _tmpLivingPeople);

        for (int i = 0; i < _tmpLivingPeople.Count; i++)
        {
            var p = _tmpLivingPeople[i];
            if (p == null)
                continue;

            if (p.Gender != Gender.Female)
                continue;

            if (p.AggregatedAgeGroup != AgeGroup.Adult)
                continue;

            if (p.AgeInTurns < minA || p.AgeInTurns > maxA)
                continue;

            if (p.Health01 < minH)
                continue;

            return true;
        }

        return false;
    }

    public bool CanStartPregnancy(Individual mother, Individual father) =>
        _pregnancySvc.CanStartPregnancy(mother, father);

    public void BeginPregnancy(Individual mother) =>
        _pregnancySvc.BeginPregnancy(mother);

    public void AbortPregnancy(string motherId) =>
        _pregnancySvc.AbortPregnancy(motherId);

    public bool TryStartPregnancyWithReservation(Individual mother, Individual father, int gestationTurns, out string reservationId) =>
        _pregnancySvc.TryStartPregnancyWithReservation(mother, father, gestationTurns, out reservationId);

    public int ResolveBirthAndReturnChildrenCount(Individual mother, Individual father) =>
        _pregnancySvc.ResolveBirthAndReturnChildrenCount(mother, father);

    public bool TryCreateNewbornFromParents(Individual mother, Individual father, out Individual baby, out PopulationGroup groupOut) =>
        _birthSvc.TryCreateNewbornFromParents(mother, father, out baby, out groupOut);

    public void SetConfig(FamilySimConfig cfg)
    {
        config = cfg;
    }

    public void ApplyPatch(FamilySimConfigPatch p)
    {
        if (config != null)
            config.ApplyPatch(p);
    }

    public FamilySimConfig GetConfig() => config;

    public void ApplyDeathsToIndividuals(Guid aggregatedGroupId, int deaths)
    {
        if (deaths <= 0)
            return;

        _tmpDeathCandidates.Clear();
        _indRepo.CopyAliveByGroupTo(aggregatedGroupId, _tmpDeathCandidates);

        if (_tmpDeathCandidates.Count == 0)
            return;

        _indRepo.Shuffle(_tmpDeathCandidates);

        int kill = Mathf.Min(deaths, _tmpDeathCandidates.Count);
        for (int i = 0; i < kill; i++)
            _indRepo.Kill(_tmpDeathCandidates[i]);

        _tmpDeathCandidates.Clear();
    }

    public bool TryPickParentsForFamilies(
        IList<string> allowedFamilyIds,
        float minHealth,
        int minAgeTurns,
        int maxAgeTurns,
        out Individual mother,
        out Individual father) =>
        _pairingSvc.TryPickParentsForFamilies(allowedFamilyIds, minHealth, minAgeTurns, maxAgeTurns, out mother, out father);

    public bool TryPickParentsForFamilies(
        IList<string> allowedFamilyIds,
        out Individual mother,
        out Individual father) =>
        _pairingSvc.TryPickParentsForFamilies(
            allowedFamilyIds,
            config != null ? config.minHealthForBirth : 0.6f,
            config != null ? config.minAdultAgeForBirthTurns : 180,
            config != null ? config.maxAdultAgeForBirthTurns : 525,
            out mother,
            out father);

    private bool UpdateIndividualsAgingAndGroups()
    {
        bool anyChanged = false;
        bool anyTaskAvailabilityChanged = false;

        ProcessIndividualsAgingBatch(_indRepo.All, ref anyChanged, ref anyTaskAvailabilityChanged);

        if (anyTaskAvailabilityChanged)
            FinalizeTaskAvailabilityChanges();

        return anyChanged;
    }

    private void ProcessIndividualsAgingBatch(
        IReadOnlyList<Individual> people,
        ref bool anyChanged,
        ref bool anyTaskAvailabilityChanged)
    {
        if (people == null)
            return;

        var gen = General;
        var pop = PlayerPop;

        for (int i = 0; i < people.Count; i++)
        {
            var p = people[i];
            if (p == null || !p.IsAlive)
                continue;

            AgeGroup oldGroup = p.AggregatedAgeGroup;

            p.AgeOneTurn();

            AgeGroup newGroup = PlayerHealthRulebook.Instance != null
                ? PlayerHealthRulebook.Instance.GetAgeGroupForTotalAge(p.AgeInTurns)
                : gen.GetAgeGroupForTotalAge(p.AgeInTurns);

            if (newGroup != oldGroup)
            {
                _indRepo.SetAggregatedAgeGroup(p, newGroup);
                anyChanged = true;

                bool wasTaskCapable = oldGroup == AgeGroup.Teen || oldGroup == AgeGroup.Adult;
                bool isTaskCapable = newGroup == AgeGroup.Teen || newGroup == AgeGroup.Adult;

                if (wasTaskCapable != isTaskCapable)
                {
                    anyTaskAvailabilityChanged = true;

                    //Debug.Log(
                        //$"[POP AGE TASK CHANGE] " +
                        //$"Person={p.Id} | {oldGroup}->{newGroup} | " +
                        //$"Alive={p.IsAlive} | Busy={p.IsBusy}");

                    if (pop != null && wasTaskCapable && !isTaskCapable)
                    {
                        bool replaced;
                        pop.TryDetachIndividualFromExistingReservations(p.Id, out replaced);

                        //Debug.Log(
                            //$"[POP AGE TASK CHANGE] Removed aged-out worker from reservations. " +
                            //$"Person={p.Id} | Replaced={replaced}");
                    }
                }
            }
        }
    }

    private void FinalizeTaskAvailabilityChanges()
    {
        var pop = PlayerPop;
        if (pop == null)
            return;

        pop.MarkUIDirty();

        if (_cachedProductionBuildings == null || _cachedProductionBuildings.Length == 0)
            CacheProductionBuildings();

        for (int i = 0; i < _cachedProductionBuildings.Length; i++)
        {
            var b = _cachedProductionBuildings[i];
            if (b == null)
                continue;

            b.HandlePopulationAvailabilityChanged();
        }
    }

    public int CollectPairsForFamilies(
        IList<string> familyIds,
        float minHealth,
        int minAgeTurns,
        int maxAgeTurns,
        List<(Individual mother, Individual father)> outPairs,
        int maxPairs)
    {
        if (_pairingSvc == null)
            return 0;

        return _pairingSvc.CollectPairsForFamilies(
            familyIds,
            minHealth,
            minAgeTurns,
            maxAgeTurns,
            outPairs,
            maxPairs);
    }

    // === Busy integration ===
    public bool SetIndividualBusy(string individualId, bool busy)
    {
        return _indRepo.SetBusyById(individualId, busy);
    }

    public void ReleaseBusyIndividuals(string reservationId, IEnumerable<Individual> individuals)
    {
        PlayerPop.ReleaseReservation(reservationId);
    }

    public bool TryPickRandomNonBusyTaskIndividuals(
        int amount,
        out List<Individual> picked,
        out string reservationId)
    {
        picked = null;
        reservationId = null;

        if (amount <= 0)
            return false;

        var pop = PlayerPop;
        if (pop == null)
            return false;

        _tmpAvailByGroup.Clear();

        var groups = pop.AllPopulations;
        for (int i = 0; i < groups.Count; i++)
        {
            var g = groups[i];
            if (g == null)
                continue;

            if (g.ageGroup != AgeGroup.Teen && g.ageGroup != AgeGroup.Adult)
                continue;

            _tmpAvailByGroup[g.GroupID] = pop.GetAvailableTaskPopulationForGroup(g.GroupID);
        }

        _tmpTaskCandidates.Clear();
        _indRepo.CopyTaskCapableNonBusyTo(
            _tmpTaskCandidates,
            p =>
                _tmpAvailByGroup.TryGetValue(p.AggregatedGroupGuid, out var a) &&
                a > 0 &&
                !pop.IsIndividualReservedAnywhere(p.Id));

        if (_tmpTaskCandidates.Count < amount)
            return false;

        _indRepo.Shuffle(_tmpTaskCandidates);

        picked = new List<Individual>(amount);

        for (int i = 0; i < _tmpTaskCandidates.Count && picked.Count < amount; i++)
        {
            var c = _tmpTaskCandidates[i];

            if (!_tmpAvailByGroup.TryGetValue(c.AggregatedGroupGuid, out var a) || a <= 0)
                continue;

            picked.Add(c);
            _tmpAvailByGroup[c.AggregatedGroupGuid] = a - 1;
        }

        if (picked.Count != amount)
        {
            picked = null;
            reservationId = null;
            return false;
        }

        if (!pop.TryReservePopulationForIndividuals(picked, out reservationId))
        {
            picked = null;
            reservationId = null;
            return false;
        }

        return true;
    }

    public int GetParentCooldownTurnsLeft(string individualId)
    {
        if (_pregnancySvc is PregnancyService ps)
            return ps.GetParentCooldownTurnsLeft(individualId);

        return 0;
    }

    public bool TryReservePopulationForProduction(
        int amount,
        out List<Individual> picked,
        out string reservationId)
    {
        return TryPickRandomNonBusyTaskIndividuals(amount, out picked, out reservationId);
    }

    public void UnbusyReservationOnly(string reservationId)
    {
        if (string.IsNullOrEmpty(reservationId))
            return;

        PlayerPop.UnbusyReservationOnly(reservationId);
    }

    public void RebusyReservation(string reservationId)
    {
        if (string.IsNullOrEmpty(reservationId))
            return;

        PlayerPop.RebusyReservation(reservationId);
    }

    public bool TryAddImmigrantFamily(
        int adults,
        int children,
        TaskSuccessPopulationRewardConfig cfg,
        out int addedTotal,
        out string createdFamilyId)
    {
        createdFamilyId = null;
        addedTotal = 0;

        if (_indRepo == null || _famRepo == null || cfg == null)
            return false;

        adults = Mathf.Max(1, adults);
        children = Mathf.Max(0, children);

        int capacityLeft = Mathf.Max(0, _indRepo.MaxIndividuals - _indRepo.Count);
        int wanted = adults + children;

        if (capacityLeft <= 0)
            return false;

        if (wanted > capacityLeft)
        {
            int overflow = wanted - capacityLeft;
            int trimKids = Mathf.Min(children, overflow);
            children -= trimKids;
            overflow -= trimKids;

            if (overflow > 0)
                adults = Mathf.Max(1, adults - overflow);
        }

        Individual adultA = null;
        Individual adultB = null;

        if (adults >= 2)
        {
            adultA = CreateImmigrantIndividual(AgeGroup.Adult, Gender.Male, cfg);
            adultB = CreateImmigrantIndividual(AgeGroup.Adult, Gender.Female, cfg);

            if (adultA == null && adultB == null)
                return false;
        }
        else
        {
            var g = UnityEngine.Random.value < 0.5f ? Gender.Male : Gender.Female;
            adultA = CreateImmigrantIndividual(AgeGroup.Adult, g, cfg);

            if (adultA == null)
                return false;
        }

        string famName = GenerateUniqueFamilyName();

        string partnerAId = (adultA != null && adultA.Gender == Gender.Male) ? adultA.Id : null;
        string partnerBId = (adultA != null && adultA.Gender == Gender.Female) ? adultA.Id : null;

        if (adultB != null)
        {
            if (adultB.Gender == Gender.Male)
                partnerAId = adultB.Id;
            else
                partnerBId = adultB.Id;
        }

        var fam = _famRepo.CreateFamily(partnerAId, partnerBId, famName);
        createdFamilyId = fam != null ? fam.FamilyId : null;

        if (adultA != null)
            _indRepo.SetFamily(adultA, fam.FamilyId, fam.FamilyName);

        if (adultB != null)
            _indRepo.SetFamily(adultB, fam.FamilyId, fam.FamilyName);

        for (int i = 0; i < children; i++)
        {
            var g = UnityEngine.Random.value < 0.5f ? Gender.Male : Gender.Female;
            var kid = CreateImmigrantIndividual(AgeGroup.Child, g, cfg);
            if (kid == null)
                break;

            _indRepo.SetFamily(kid, fam.FamilyId, fam.FamilyName);

            if (fam.ChildrenIds != null)
                fam.ChildrenIds.Add(kid.Id);

            addedTotal++;
        }

        if (adultA != null)
            addedTotal++;

        if (adultB != null)
            addedTotal++;

        RefreshDebugViews();
        return addedTotal > 0 && !string.IsNullOrEmpty(createdFamilyId);
    }

    public bool TryAddImmigrantFamily(int adults, int children, TaskSuccessPopulationRewardConfig cfg, out int addedTotal)
    {
        return TryAddImmigrantFamily(adults, children, cfg, out addedTotal, out _);
    }

    public bool TryAddImmigrantIndividuals(int count, TaskSuccessPopulationRewardConfig cfg, out int addedTotal)
    {
        addedTotal = 0;

        if (_indRepo == null || _famRepo == null || cfg == null)
            return false;

        count = Mathf.Max(1, count);

        int capacityLeft = Mathf.Max(0, _indRepo.MaxIndividuals - _indRepo.Count);
        if (capacityLeft <= 0)
            return false;

        count = Mathf.Min(count, capacityLeft);

        Family attach = PickRandomFamilyOrCreatePlaceholder();

        for (int i = 0; i < count; i++)
        {
            var g = UnityEngine.Random.value < 0.5f ? Gender.Male : Gender.Female;
            var ind = CreateImmigrantIndividual(AgeGroup.Adult, g, cfg);
            if (ind == null)
                break;

            if (attach != null)
                _indRepo.SetFamily(ind, attach.FamilyId, attach.FamilyName);

            addedTotal++;
        }

        if (addedTotal > 0)
            RefreshDebugViews();

        return addedTotal > 0;
    }

    private Family PickRandomFamilyOrCreatePlaceholder()
    {
        int count = _famRepo != null ? _famRepo.Count : 0;

        if (count > 0)
        {
            var fams = _famRepo.All;
            return fams[UnityEngine.Random.Range(0, count)];
        }

        return _famRepo.CreateFamily(null, null, GenerateUniqueFamilyName());
    }

    private Individual CreateImmigrantIndividual(AgeGroup ageGroup, Gender gender, TaskSuccessPopulationRewardConfig cfg)
    {
        if (_indRepo.Count >= _indRepo.MaxIndividuals)
            return null;

        PopulationGroup group = FindOrCreatePopulationGroup(ageGroup, gender);
        if (group == null)
            return null;

        int ageTurns = ageGroup == AgeGroup.Child ? cfg.RollChildAgeTurns() : cfg.RollAdultAgeTurns();
        float health = cfg.RollHealth01();

        var ind = new Individual(gender, ageTurns, health, ageGroup, group.GroupID, 0)
        {
            LineageId = LineageUtils.NewGene(32, _rng)
        };

        if (!_indRepo.TryAdd(ind))
            return null;

        group.count += 1;
        return ind;
    }

    private PopulationGroup FindOrCreatePopulationGroup(AgeGroup ageGroup, Gender gender)
    {
        var all = PlayerPop.AllPopulations;
        if (all == null)
            return null;

        for (int i = 0; i < all.Count; i++)
        {
            var g = all[i];
            if (g == null)
                continue;

            if (g.ageGroup == ageGroup && g.gender == gender)
                return g;
        }

        var created = new PopulationGroup(ageGroup, gender, 0, 0, averageAgeInTurns: 0, averageHealth: 1f);

        if (all is IList<PopulationGroup> list)
            list.Add(created);

        return created;
    }

    public bool IsProductionReservationStillValid(string reservationId, int requiredCount)
    {
        if (string.IsNullOrWhiteSpace(reservationId))
            return false;

        if (requiredCount <= 0)
            return true;

        var pop = PlayerPop;
        if (pop == null)
            return false;

        if (!pop.TryGetReservedIndividualIds(reservationId, out var reservedIds) || reservedIds == null)
            return false;

        if (reservedIds.Count < requiredCount)
            return false;

        int validCount = 0;

        for (int i = 0; i < reservedIds.Count; i++)
        {
            var person = FindIndividualById(reservedIds[i]);
            if (person == null || !person.IsAlive)
                return false;

            if (person.AggregatedAgeGroup != AgeGroup.Teen &&
                person.AggregatedAgeGroup != AgeGroup.Adult)
            {
                return false;
            }

            validCount++;
        }

        return validCount >= requiredCount;
    }

    private Individual FindIndividualById(string individualId)
    {
        return _indRepo.FindById(individualId);
    }

    public bool IsIndividualCurrentlyPregnant(string individualId)
    {
        if (string.IsNullOrWhiteSpace(individualId))
            return false;

        if (_pregnancySvc is PregnancyService ps)
            return ps.IsMotherCurrentlyPregnant(individualId);

        return false;
    }

    public PlayerFamilySimulationSaveData SaveState()
    {
        PlayerFamilySimulationSaveData data = new PlayerFamilySimulationSaveData();

        if (_indRepo != null)
        {
            var all = _indRepo.All;
            for (int i = 0; i < all.Count; i++)
            {
                Individual p = all[i];
                if (p == null)
                    continue;

                Guid aggregatedGroupGuid = SaveReflectionUtil.Get(p, "AggregatedGroupGuid", Guid.Empty);

                data.individuals.Add(new IndividualSaveData
                {
                    id = SaveReflectionUtil.Get<string>(p, "Id", string.Empty),
                    gender = SaveReflectionUtil.Get(p, "Gender", Gender.Male),
                    ageInTurns = SaveReflectionUtil.Get(p, "AgeInTurns", 0),
                    health01 = SaveReflectionUtil.Get(p, "Health01", 1f),
                    aggregatedAgeGroup = SaveReflectionUtil.Get(p, "AggregatedAgeGroup", AgeGroup.Child),
                    aggregatedGroupGuid = aggregatedGroupGuid != Guid.Empty ? aggregatedGroupGuid.ToString() : string.Empty,
                    isAlive = SaveReflectionUtil.Get(p, "IsAlive", true),
                    isBusy = SaveReflectionUtil.Get(p, "IsBusy", false),
                    familyId = SaveReflectionUtil.Get<string>(p, "FamilyId", null),
                    surname = SaveReflectionUtil.Get<string>(p, "Surname", null),
                    lineageId = SaveReflectionUtil.Get<string>(p, "LineageId", null)
                });
            }
        }

        if (_famRepo != null)
        {
            var families = _famRepo.All;
            for (int i = 0; i < families.Count; i++)
            {
                Family f = families[i];
                if (f == null)
                    continue;

                List<string> children = SaveReflectionUtil.Get(f, "ChildrenIds", new List<string>());

                data.families.Add(new FamilySaveData
                {
                    familyId = SaveReflectionUtil.Get<string>(f, "FamilyId", string.Empty),
                    familyName = SaveReflectionUtil.Get<string>(f, "FamilyName", string.Empty),
                    partnerAId = SaveReflectionUtil.Get<string>(f, "PartnerAId", null),
                    partnerBId = SaveReflectionUtil.Get<string>(f, "PartnerBId", null),
                    childrenIds = children != null ? new List<string>(children) : new List<string>()
                });
            }
        }

        if (_pregnancySvc is PregnancyService ps)
            data.pregnancyData = ps.SaveState();

        return data;
    }

    public void LoadState(PlayerFamilySimulationSaveData data)
    {
        if (data == null)
            return;

        EnsureRepositoriesExist();
        EnsureServicesExist();

        _indRepo.Clear();
        _famRepo.Clear();
        _usedFamilyNames.Clear();

        if (data.individuals != null)
        {
            for (int i = 0; i < data.individuals.Count; i++)
            {
                IndividualSaveData saved = data.individuals[i];
                if (saved == null)
                    continue;

                Guid groupGuid = Guid.Empty;
                if (!string.IsNullOrWhiteSpace(saved.aggregatedGroupGuid))
                    Guid.TryParse(saved.aggregatedGroupGuid, out groupGuid);

                Individual ind = new Individual(
                    saved.gender,
                    saved.ageInTurns,
                    saved.health01,
                    saved.aggregatedAgeGroup,
                    groupGuid,
                    0
                );

                SaveReflectionUtil.Set(ind, "Id", saved.id);
                SaveReflectionUtil.Set(ind, "AggregatedGroupGuid", groupGuid);
                SaveReflectionUtil.Set(ind, "AggregatedAgeGroup", saved.aggregatedAgeGroup);
                SaveReflectionUtil.Set(ind, "FamilyId", saved.familyId);
                SaveReflectionUtil.Set(ind, "Surname", saved.surname);
                SaveReflectionUtil.Set(ind, "LineageId", saved.lineageId);
                SaveReflectionUtil.Set(ind, "IsAlive", saved.isAlive);
                SaveReflectionUtil.Set(ind, "IsBusy", saved.isBusy);

                _indRepo.TryAdd(ind);
            }
        }

        _indRepo.RebuildIndexes();

        if (data.families != null)
        {
            for (int i = 0; i < data.families.Count; i++)
            {
                FamilySaveData saved = data.families[i];
                if (saved == null)
                    continue;

                Family fam = _famRepo.CreateFamily(
                    saved.partnerAId,
                    saved.partnerBId,
                    saved.familyName
                );

                SaveReflectionUtil.Set(fam, "FamilyId", saved.familyId);
                SaveReflectionUtil.Set(fam, "FamilyName", saved.familyName);
                SaveReflectionUtil.Set(fam, "PartnerAId", saved.partnerAId);
                SaveReflectionUtil.Set(fam, "PartnerBId", saved.partnerBId);

                List<string> childrenIds = SaveReflectionUtil.Get(fam, "ChildrenIds", new List<string>());
                if (childrenIds != null)
                {
                    childrenIds.Clear();

                    if (saved.childrenIds != null)
                    {
                        for (int j = 0; j < saved.childrenIds.Count; j++)
                        {
                            string childId = saved.childrenIds[j];
                            if (!string.IsNullOrWhiteSpace(childId))
                                childrenIds.Add(childId);
                        }
                    }
                }
                else
                {
                    SaveReflectionUtil.Set(
                        fam,
                        "ChildrenIds",
                        saved.childrenIds != null ? new List<string>(saved.childrenIds) : new List<string>());
                }
            }
        }

        _famRepo.RebuildIndexes();

        if (_pregnancySvc is PregnancyService ps)
            ps.LoadState(data.pregnancyData);

        RebuildUsedFamilyNames();
        CacheProductionBuildings();
        RefreshDebugViews();
    }

    private void RefreshDebugViews()
    {
        debugIndividualsView.Clear();
        debugFamiliesView.Clear();

        if (_indRepo != null)
        {
            var people = _indRepo.All;
            for (int i = 0; i < people.Count; i++)
            {
                if (people[i] != null)
                    debugIndividualsView.Add(people[i]);
            }
        }

        if (_famRepo != null)
        {
            var families = _famRepo.All;
            for (int i = 0; i < families.Count; i++)
            {
                if (families[i] != null)
                    debugFamiliesView.Add(families[i]);
            }
        }
    }

    public void InstallExternalManagers(PlayersPopulationManager newPlayerPop, GeneralPopulationManager newGeneral)
    {
        if (newPlayerPop != null)
            playerPop = newPlayerPop;

        if (newGeneral != null)
            general = newGeneral;
    }

    private void LogFamilyPassDelta(string stage, int before, int after)
    {
        if (!debugFamilyPassPopulationDelta)
            return;

        if (before == after)
            return;

        //Debug.LogWarning(
            //$"[POP FAMILY PASS] " +
            //$"Stage={stage} | " +
            //$"Turn={(TurnSystem.Instance != null ? TurnSystem.GetCurrentTurn() : -1)} | " +
            //$"TotalPopulation {before}->{after} | " +
            //$"Delta={after - before}");
    }
    public bool TryKillIndividualsById(IEnumerable<string> individualIds, out int killedCount)
    {
        killedCount = 0;

        if (individualIds == null)
            return false;

        _tmpKillIds.Clear();

        foreach (string id in individualIds)
        {
            if (string.IsNullOrWhiteSpace(id))
                continue;

            if (!_tmpKillIds.Contains(id))
                _tmpKillIds.Add(id);
        }

        if (_tmpKillIds.Count == 0)
            return false;

        Dictionary<Guid, int> deathsByGroup = new Dictionary<Guid, int>();

        for (int i = 0; i < _tmpKillIds.Count; i++)
        {
            string id = _tmpKillIds[i];
            Individual person = FindIndividualById(id);

            if (person == null || !person.IsAlive)
                continue;

            Guid groupId = person.AggregatedGroupGuid;

            _indRepo.Kill(person);
            killedCount++;

            if (!deathsByGroup.TryAdd(groupId, 1))
                deathsByGroup[groupId]++;
        }

        if (killedCount <= 0)
            return false;

        if (PlayerPop != null && deathsByGroup.Count > 0)
        {
            List<PopulationGroup> allGroups = PlayerPop.AllPopulations;

            for (int i = 0; i < allGroups.Count; i++)
            {
                PopulationGroup group = allGroups[i];
                if (group == null)
                    continue;

                if (!deathsByGroup.TryGetValue(group.GroupID, out int deaths))
                    continue;

                if (deaths <= 0)
                    continue;

                group.ApplyPopulationLoss(deaths);
            }

            PlayerPop.PruneDeadOrEmptyGroups();
            PlayerPop.MarkUIDirty();
        }

        if (_deathReconSvc != null)
            _deathReconSvc.Reconcile(_indRepo, _famRepo, PlayerPop, General);

        MarkPopulationDirty();
        RefreshDebugViews();
        return true;
    }
}
