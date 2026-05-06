using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public enum PopulationReservationKind
{
    None = 0,
    GenericTask = 1,
    Discovery = 2,
    Survey = 3,
    Gathering = 4,
    Construction = 5,
    Clearing = 6,
    Crafting = 7,
    Training = 8,
    UnitGroup = 9,
    Pregnancy = 10,
    Production = 11,
    Research = 12,
    Religion = 13,
    Other = 100
}

[Serializable]
public class PopulationReservationMeta
{
    public string reservationId;
    public PopulationReservationKind kind = PopulationReservationKind.GenericTask;
    public string ownerId;
    public string ownerType;
    public bool isBusyActive;
}

public class PlayersPopulationManager : MonoBehaviour
{
    public static PlayersPopulationManager Instance { get; private set; }

    [Header("Population Settings")]
    public int maxPopulation = 100;
    public int startingPopulation = 10;

    [Header("Population Cap Rules")]
    [Tooltip("If true, maxPopulation is ignored during initial spawn.")]
    public bool ignoreMaxDuringInitialization = true;

    [Tooltip("If true, maxPopulation <= 0 means unlimited population cap.")]
    public bool zeroOrLessMaxMeansUnlimited = true;

    [Header("Family Settings")]
    [Tooltip("If <= 0, this will be computed as startingPopulation / 3 (min 1).")]
    public int startingFamilyCount = 0;

    [Header("Age Group Population")]
    [SerializeField] private List<PopulationGroup> allPopulations = new List<PopulationGroup>();
    public List<PopulationGroup> AllPopulations => allPopulations;

    [Header("Population Text")]
    public TMP_Text populationDisplayText;
    public TMP_Text availableText;

    [SerializeField] private bool debugPopulationRemoval = true;
    [SerializeField] private bool debugPopulationRemovalStack = true;

    private readonly HashSet<string> activelyBusyReservations = new(StringComparer.Ordinal);

    private GeneralPopulationManager generalPopulationManager;

    // reservationId -> (group -> reserved amount)
    private readonly Dictionary<string, Dictionary<PopulationGroup, int>> reservations = new(StringComparer.Ordinal);

    // reservationId -> reserved individual ids
    private readonly Dictionary<string, List<string>> busyByReservation = new(StringComparer.Ordinal);

    private readonly Dictionary<string, int> reservationExpiryTurns = new(StringComparer.Ordinal);

    // reservationId -> metadata
    private readonly Dictionary<string, PopulationReservationMeta> _reservationMetaById =
        new(StringComparer.Ordinal);

    // reservation kind -> reservation ids
    private readonly Dictionary<PopulationReservationKind, HashSet<string>> _reservationIdsByKind =
        new();

    // Indexed lookups for scale
    private readonly Dictionary<Guid, PopulationGroup> _groupsById = new();
    private readonly Dictionary<string, string> _reservationByIndividualId = new(StringComparer.Ordinal);

    // reusable temp buffers
    private readonly List<Individual> _tmpAvailableWorkers = new(256);
    private readonly List<PopulationGroup> _tmpPopulationGroups = new(64);
    private readonly List<string> _tmpReservationIds = new(32);
    private readonly List<string> _tmpReservationIdsByKind = new(32);

    public event Action OnPopulationChanged;

    private bool _uiDirty;
    private int _batchDepth;

    private void OnEnable()
    {
        TurnSystem.SubscribeToStartOfTurn(BeginTurnBatch);
        TurnSystem.SubscribeToEndOfTurn(EndTurnBatch);
    }

    private void OnDisable()
    {
        TurnSystem.UnsubscribeFromStartOfTurn(BeginTurnBatch);
        TurnSystem.UnsubscribeFromEndOfTurn(EndTurnBatch);
    }

    private void BeginTurnBatch() => BeginBatch();
    private void EndTurnBatch() => EndBatch();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        generalPopulationManager = FindObjectOfType<GeneralPopulationManager>();

        if (startingFamilyCount <= 0)
            startingFamilyCount = Mathf.Max(1, startingPopulation / 3);

        BeginBatch();
        InitializePopulation();
        RebuildIndexes();
        EndBatch();
    }

    private void MarkPopulationDirty()
    {
        SaveSystem.MarkSectionDirty(SaveSectionKeys.Population);
    }

    public void BeginBatch()
    {
        _batchDepth++;
    }

    public void EndBatch()
    {
        if (_batchDepth > 0)
            _batchDepth--;

        if (_batchDepth == 0 && _uiDirty)
            ForceSyncUI();
    }

    public void MarkUIDirty()
    {
        _uiDirty = true;
        if (_batchDepth == 0)
            ForceSyncUI();

        MarkPopulationDirty();
    }

    public void ForceSyncUI()
    {
        _uiDirty = false;
        SyncInspectorList();
    }

    private void SyncInspectorList()
    {
        if (populationDisplayText != null)
        {
            int total = GetTotalPopulation();
            populationDisplayText.text = HasPopulationCap()
                ? $"{total} / {maxPopulation}"
                : $"{total} / ∞";
        }

        if (availableText != null)
            availableText.text = $"{GetAvailableTaskPopulation()} / {GetTotalTaskPool()}";

        OnPopulationChanged?.Invoke();
    }

    private bool HasPopulationCap()
    {
        if (zeroOrLessMaxMeansUnlimited && maxPopulation <= 0)
            return false;

        return maxPopulation > 0;
    }

    private int GetRemainingPopulationCapacity()
    {
        if (!HasPopulationCap())
            return int.MaxValue;

        return Mathf.Max(0, maxPopulation - GetTotalPopulation());
    }

    private void RebuildIndexes()
    {
        RebuildGroupIndex();
        RebuildReservationIndex();
        RebuildReservationKindIndex();
    }

    private void RebuildGroupIndex()
    {
        _groupsById.Clear();

        for (int i = 0; i < allPopulations.Count; i++)
        {
            var g = allPopulations[i];
            if (g == null)
                continue;

            _groupsById[g.GroupID] = g;
        }
    }

    private void RebuildReservationIndex()
    {
        _reservationByIndividualId.Clear();

        foreach (var kv in busyByReservation)
        {
            string reservationId = kv.Key;
            var ids = kv.Value;
            if (ids == null)
                continue;

            for (int i = 0; i < ids.Count; i++)
            {
                string id = ids[i];
                if (!string.IsNullOrWhiteSpace(id))
                    _reservationByIndividualId[id] = reservationId;
            }
        }
    }

    private void RebuildReservationKindIndex()
    {
        _reservationIdsByKind.Clear();

        foreach (var kv in _reservationMetaById)
        {
            var meta = kv.Value;
            if (meta == null)
                continue;

            AddReservationIdToKindIndex(meta.kind, kv.Key);
        }
    }

    private void AddReservationIdToKindIndex(PopulationReservationKind kind, string reservationId)
    {
        if (string.IsNullOrWhiteSpace(reservationId))
            return;

        if (!_reservationIdsByKind.TryGetValue(kind, out var ids))
        {
            ids = new HashSet<string>(StringComparer.Ordinal);
            _reservationIdsByKind[kind] = ids;
        }

        ids.Add(reservationId);
    }

    private void RemoveReservationIdFromKindIndex(PopulationReservationKind kind, string reservationId)
    {
        if (string.IsNullOrWhiteSpace(reservationId))
            return;

        if (!_reservationIdsByKind.TryGetValue(kind, out var ids) || ids == null)
            return;

        ids.Remove(reservationId);

        if (ids.Count == 0)
            _reservationIdsByKind.Remove(kind);
    }

    private void AddIndividualToReservationIndex(string reservationId, string individualId)
    {
        if (string.IsNullOrWhiteSpace(reservationId) || string.IsNullOrWhiteSpace(individualId))
            return;

        _reservationByIndividualId[individualId] = reservationId;
    }

    private void RemoveIndividualFromReservationIndex(string reservationId, string individualId)
    {
        if (string.IsNullOrWhiteSpace(individualId))
            return;

        if (_reservationByIndividualId.TryGetValue(individualId, out var existing) &&
            string.Equals(existing, reservationId, StringComparison.Ordinal))
        {
            _reservationByIndividualId.Remove(individualId);
        }
    }

    private void TrackGroup(PopulationGroup group)
    {
        if (group == null)
            return;

        _groupsById[group.GroupID] = group;
    }

    private void UntrackGroup(PopulationGroup group)
    {
        if (group == null)
            return;

        _groupsById.Remove(group.GroupID);
    }

    private void InitializePopulation()
    {
        int currentTurn = 0;
        if (TurnSystem.Instance != null)
            currentTurn = TurnSystem.GetCurrentTurn();

        int minAdults = Mathf.CeilToInt(startingPopulation / 2f);
        int adultCount = UnityEngine.Random.Range(minAdults, startingPopulation);
        int childCount = startingPopulation - adultCount;

        int adultMale = adultCount / 2;
        int adultFemale = adultCount - adultMale;
        int childMale = childCount / 2;
        int childFemale = childCount - childMale;

        if (childMale > 0)
            AddPopulationGroup(AgeGroup.Child, childMale, currentTurn, Gender.Male, 0, ignoreMaxDuringInitialization);

        if (childFemale > 0)
            AddPopulationGroup(AgeGroup.Child, childFemale, currentTurn, Gender.Female, 0, ignoreMaxDuringInitialization);

        if (adultMale > 0)
        {
            int adultStartAge = 0;
            if (generalPopulationManager != null)
            {
                adultStartAge = PlayerHealthRulebook.Instance
                    ? PlayerHealthRulebook.Instance.teenToAdultAge
                    : generalPopulationManager.teenToAdultAge;
            }

            AddPopulationGroup(AgeGroup.Adult, adultMale, currentTurn, Gender.Male, adultStartAge, ignoreMaxDuringInitialization);
        }

        if (adultFemale > 0)
        {
            int adultStartAge = 0;
            if (generalPopulationManager != null)
            {
                adultStartAge = PlayerHealthRulebook.Instance
                    ? PlayerHealthRulebook.Instance.teenToAdultAge
                    : generalPopulationManager.teenToAdultAge;
            }

            AddPopulationGroup(AgeGroup.Adult, adultFemale, currentTurn, Gender.Female, adultStartAge, ignoreMaxDuringInitialization);
        }
    }

    private void AddPopulationGroup(
        AgeGroup ageGroup,
        int count,
        int additionTurn,
        Gender gender,
        int averageAgeInTurns = 0,
        bool ignoreMaxCap = false)
    {
        if (count <= 0)
            return;

        if (!ignoreMaxCap)
        {
            int available = GetRemainingPopulationCapacity();
            if (count > available)
                count = available;

            if (count <= 0)
                return;
        }

        int baseHealth = GetBaseHealthForAge(ageGroup);
        float averageHealthNormalized = 1f;

        var group = new PopulationGroup(
            ageGroup,
            gender,
            count,
            additionTurn,
            averageAgeInTurns,
            averageHealthNormalized,
            baseHealth
        );

        allPopulations.Add(group);
        TrackGroup(group);
        MarkUIDirty();
    }

    private int GetBaseHealthForAge(AgeGroup ageGroup)
    {
        if (generalPopulationManager != null)
        {
            switch (ageGroup)
            {
                case AgeGroup.Child: return generalPopulationManager.baseChildHealth;
                case AgeGroup.Teen: return generalPopulationManager.baseTeenHealth;
                case AgeGroup.Adult: return generalPopulationManager.baseAdultHealth;
                case AgeGroup.Elder: return generalPopulationManager.baseElderHealth;
            }
        }

        return ageGroup switch
        {
            AgeGroup.Child => 50,
            AgeGroup.Teen => 100,
            AgeGroup.Adult => 100,
            AgeGroup.Elder => 75,
            _ => 100
        };
    }

    public PopulationGroup AddBirthAndReturnGroup(Gender gender)
    {
        PopulationGroup g = null;

        for (int i = 0; i < allPopulations.Count; i++)
        {
            var pg = allPopulations[i];
            if (pg == null)
                continue;

            if (pg.ageGroup == AgeGroup.Child &&
                pg.gender == gender &&
                pg.averageAgeInTurns == 0)
            {
                g = pg;
                break;
            }
        }

        if (g == null)
        {
            int currentTurn = TurnSystem.Instance != null ? TurnSystem.GetCurrentTurn() : 0;
            int baseHealth = PlayerHealthRulebook.Instance
                ? PlayerHealthRulebook.Instance.GetBaseHealth(AgeGroup.Child)
                : GetBaseHealthForAge(AgeGroup.Child);

            g = new PopulationGroup(
                AgeGroup.Child,
                gender,
                count: 0,
                additionTurn: currentTurn,
                averageAgeInTurns: 0,
                averageHealth: 1f,
                maxHealthPerIndividual: baseHealth
            );

            allPopulations.Add(g);
            TrackGroup(g);
        }

        if (!HasPopulationCap() || GetTotalPopulation() < maxPopulation)
        {
            g.count += 1;
            MarkUIDirty();
            return g;
        }

        Debug.Log("Birth blocked: reached maxPopulation.");
        return null;
    }

    private bool IsTaskCapableGroup(PopulationGroup g)
    {
        return g != null && (g.ageGroup == AgeGroup.Teen || g.ageGroup == AgeGroup.Adult);
    }

    private PlayerFamilySimulationManager GetFamilySim()
    {
        return PlayerFamilySimulationManager.Instance;
    }

    private Individual FindIndividualById(IReadOnlyList<Individual> people, string individualId)
    {
        if (people == null || string.IsNullOrEmpty(individualId))
            return null;

        for (int i = 0; i < people.Count; i++)
        {
            var p = people[i];
            if (p != null && p.Id == individualId)
                return p;
        }

        return null;
    }

    private PopulationGroup FindPopulationGroupById(Guid groupId)
    {
        _groupsById.TryGetValue(groupId, out var group);
        return group;
    }

    private bool IsActuallyAvailableTaskWorker(Individual person, string excludeReservationId = null)
    {
        if (person == null || !person.IsAlive)
            return false;

        if (person.AggregatedAgeGroup != AgeGroup.Teen &&
            person.AggregatedAgeGroup != AgeGroup.Adult)
            return false;

        if (person.IsBusy)
            return false;

        if (IsIndividualReservedAnywhere(person.Id, excludeReservationId))
            return false;

        return true;
    }

    private void FillActuallyAvailableTaskWorkers(
        List<Individual> outList,
        Func<Individual, bool> extraFilter = null,
        string excludeReservationId = null)
    {
        outList.Clear();

        var sim = GetFamilySim();
        if (sim == null)
            return;

        var people = sim.GetIndividuals();
        if (people == null)
            return;

        for (int i = 0; i < people.Count; i++)
        {
            var p = people[i];
            if (!IsActuallyAvailableTaskWorker(p, excludeReservationId))
                continue;

            if (extraFilter != null && !extraFilter(p))
                continue;

            outList.Add(p);
        }
    }

    private static void ShuffleInPlace<T>(List<T> list)
    {
        if (list == null || list.Count <= 1)
            return;

        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private int EffectiveAvailableForTaskAggregateFallback(PopulationGroup g)
    {
        if (!IsTaskCapableGroup(g))
            return 0;

        int reserved = Mathf.Max(0, g.reservedCount);
        return Mathf.Max(0, g.count - reserved);
    }

    public int GetAvailableTaskPopulation()
    {
        var sim = GetFamilySim();
        if (sim == null)
        {
            int sum = 0;
            for (int i = 0; i < allPopulations.Count; i++)
            {
                var g = allPopulations[i];
                if (g == null || !IsTaskCapableGroup(g))
                    continue;

                sum += EffectiveAvailableForTaskAggregateFallback(g);
            }

            return sum;
        }

        int sumActual = 0;
        var people = sim.GetIndividuals();
        if (people == null)
            return 0;

        for (int i = 0; i < people.Count; i++)
        {
            if (IsActuallyAvailableTaskWorker(people[i]))
                sumActual++;
        }

        return sumActual;
    }

    public int GetAvailableTaskPopulationForGroup(Guid groupId)
    {
        var sim = GetFamilySim();
        if (sim == null)
        {
            var group = FindPopulationGroupById(groupId);
            return EffectiveAvailableForTaskAggregateFallback(group);
        }

        int sum = 0;
        var people = sim.GetIndividuals();
        if (people == null)
            return 0;

        for (int i = 0; i < people.Count; i++)
        {
            var p = people[i];
            if (p == null)
                continue;

            if (p.AggregatedGroupGuid != groupId)
                continue;

            if (IsActuallyAvailableTaskWorker(p))
                sum++;
        }

        return sum;
    }

    public int GetTotalReservedForTasks()
    {
        int total = 0;

        foreach (var kv in reservations)
        {
            var alloc = kv.Value;
            if (alloc == null)
                continue;

            foreach (var entry in alloc)
            {
                if (entry.Key == null || !IsTaskCapableGroup(entry.Key))
                    continue;

                total += Mathf.Max(0, entry.Value);
            }
        }

        return total;
    }

    public int GetTotalTaskPool()
    {
        var sim = GetFamilySim();
        if (sim == null)
        {
            int total = 0;
            for (int i = 0; i < allPopulations.Count; i++)
            {
                var g = allPopulations[i];
                if (g == null || !IsTaskCapableGroup(g))
                    continue;

                total += Mathf.Max(0, g.count);
            }

            return total;
        }

        int peopleTotal = 0;
        var people = sim.GetIndividuals();
        if (people == null)
            return 0;

        for (int i = 0; i < people.Count; i++)
        {
            var p = people[i];
            if (p == null || !p.IsAlive)
                continue;

            if (p.AggregatedAgeGroup == AgeGroup.Teen ||
                p.AggregatedAgeGroup == AgeGroup.Adult)
            {
                peopleTotal++;
            }
        }

        return peopleTotal;
    }

    public int GetUsedTaskPopulation()
    {
        return Mathf.Max(0, GetTotalTaskPool() - GetAvailableTaskPopulation());
    }

    // -------------------------------------------------
    // Reservation metadata API
    // -------------------------------------------------

    private PopulationReservationMeta GetOrCreateReservationMetaInternal(string reservationId)
    {
        if (string.IsNullOrWhiteSpace(reservationId))
            return null;

        if (!_reservationMetaById.TryGetValue(reservationId, out var meta) || meta == null)
        {
            meta = new PopulationReservationMeta
            {
                reservationId = reservationId,
                kind = PopulationReservationKind.GenericTask,
                ownerId = null,
                ownerType = null,
                isBusyActive = activelyBusyReservations.Contains(reservationId)
            };

            _reservationMetaById[reservationId] = meta;
            AddReservationIdToKindIndex(meta.kind, reservationId);
        }

        return meta;
    }

    private void SetReservationMetadataInternal(
        string reservationId,
        PopulationReservationKind kind,
        string ownerId,
        string ownerType)
    {
        if (string.IsNullOrWhiteSpace(reservationId))
            return;

        var meta = GetOrCreateReservationMetaInternal(reservationId);
        if (meta == null)
            return;

        var oldKind = meta.kind;

        meta.kind = kind;
        meta.ownerId = ownerId;
        meta.ownerType = ownerType;
        meta.isBusyActive = activelyBusyReservations.Contains(reservationId);

        if (oldKind != kind)
        {
            RemoveReservationIdFromKindIndex(oldKind, reservationId);
            AddReservationIdToKindIndex(kind, reservationId);
        }
        else
        {
            AddReservationIdToKindIndex(kind, reservationId);
        }
    }

    private void SetReservationBusyActiveFlag(string reservationId, bool isBusyActive)
    {
        if (string.IsNullOrWhiteSpace(reservationId))
            return;

        var meta = GetOrCreateReservationMetaInternal(reservationId);
        if (meta == null)
            return;

        meta.isBusyActive = isBusyActive;
    }

    private void RemoveReservationMetadataInternal(string reservationId)
    {
        if (string.IsNullOrWhiteSpace(reservationId))
            return;

        if (_reservationMetaById.TryGetValue(reservationId, out var meta) && meta != null)
            RemoveReservationIdFromKindIndex(meta.kind, reservationId);

        _reservationMetaById.Remove(reservationId);
    }

    public bool UpdateReservationMetadata(
        string reservationId,
        PopulationReservationKind kind,
        string ownerId = null,
        string ownerType = null)
    {
        if (string.IsNullOrWhiteSpace(reservationId))
            return false;

        bool exists =
            reservations.ContainsKey(reservationId) ||
            busyByReservation.ContainsKey(reservationId) ||
            _reservationMetaById.ContainsKey(reservationId);

        if (!exists)
            return false;

        SetReservationMetadataInternal(reservationId, kind, ownerId, ownerType);
        MarkPopulationDirty();
        return true;
    }

    public bool TryGetReservationMetadata(string reservationId, out PopulationReservationMeta meta)
    {
        meta = null;

        if (string.IsNullOrWhiteSpace(reservationId))
            return false;

        if (!_reservationMetaById.TryGetValue(reservationId, out var found) || found == null)
            return false;

        meta = new PopulationReservationMeta
        {
            reservationId = found.reservationId,
            kind = found.kind,
            ownerId = found.ownerId,
            ownerType = found.ownerType,
            isBusyActive = found.isBusyActive
        };

        return true;
    }

    public PopulationReservationKind GetReservationKind(string reservationId)
    {
        if (string.IsNullOrWhiteSpace(reservationId))
            return PopulationReservationKind.None;

        if (_reservationMetaById.TryGetValue(reservationId, out var meta) && meta != null)
            return meta.kind;

        return PopulationReservationKind.None;
    }

    public void GetReservationIdsForKind(PopulationReservationKind kind, List<string> outIds)
    {
        if (outIds == null)
            return;

        outIds.Clear();

        if (!_reservationIdsByKind.TryGetValue(kind, out var ids) || ids == null)
            return;

        foreach (var id in ids)
            outIds.Add(id);
    }

    public int CountReservationsOfKind(PopulationReservationKind kind)
    {
        return _reservationIdsByKind.TryGetValue(kind, out var ids) && ids != null
            ? ids.Count
            : 0;
    }

    public int CountBusyReservationsOfKind(PopulationReservationKind kind)
    {
        if (!_reservationIdsByKind.TryGetValue(kind, out var ids) || ids == null || ids.Count == 0)
            return 0;

        int count = 0;
        foreach (var id in ids)
        {
            if (activelyBusyReservations.Contains(id))
                count++;
        }

        return count;
    }

    public int GetReservedPopulationForKind(PopulationReservationKind kind)
    {
        if (!_reservationIdsByKind.TryGetValue(kind, out var ids) || ids == null || ids.Count == 0)
            return 0;

        int total = 0;
        foreach (var id in ids)
        {
            if (!reservations.TryGetValue(id, out var alloc) || alloc == null)
                continue;

            foreach (var kv in alloc)
                total += Mathf.Max(0, kv.Value);
        }

        return total;
    }

    // -------------------------------------------------
    // Reservation creation
    // -------------------------------------------------

    public bool TryReservePopulation(int amount, out string reservationId)
    {
        return TryReservePopulation(
            amount,
            PopulationReservationKind.GenericTask,
            ownerId: null,
            ownerType: null,
            out reservationId);
    }

    public bool TryReservePopulation(
        int amount,
        PopulationReservationKind kind,
        string ownerId,
        string ownerType,
        out string reservationId)
    {
        reservationId = null;
        if (amount <= 0)
            return false;

        var sim = GetFamilySim();
        if (sim != null)
        {
            FillActuallyAvailableTaskWorkers(_tmpAvailableWorkers);
            if (_tmpAvailableWorkers.Count < amount)
                return false;

            ShuffleInPlace(_tmpAvailableWorkers);

            var picked = new List<Individual>(amount);
            for (int i = 0; i < amount; i++)
                picked.Add(_tmpAvailableWorkers[i]);

            return TryReservePopulationForIndividuals(
                picked,
                kind,
                ownerId,
                ownerType,
                out reservationId);
        }

        int available = GetAvailableTaskPopulation();
        if (available < amount)
            return false;

        _tmpPopulationGroups.Clear();

        for (int i = 0; i < allPopulations.Count; i++)
        {
            var group = allPopulations[i];
            if (group == null || !IsTaskCapableGroup(group))
                continue;

            _tmpPopulationGroups.Add(group);
        }

        _tmpPopulationGroups.Sort((a, b) =>
            EffectiveAvailableForTaskAggregateFallback(b).CompareTo(EffectiveAvailableForTaskAggregateFallback(a)));

        int remaining = amount;
        var allocation = new Dictionary<PopulationGroup, int>();

        for (int i = 0; i < _tmpPopulationGroups.Count && remaining > 0; i++)
        {
            var group = _tmpPopulationGroups[i];
            int free = EffectiveAvailableForTaskAggregateFallback(group);
            if (free <= 0)
                continue;

            int take = Mathf.Min(free, remaining);
            group.reservedCount += take;
            allocation[group] = take;
            remaining -= take;
        }

        if (remaining > 0)
        {
            foreach (var kv in allocation)
                kv.Key.reservedCount = Mathf.Max(0, kv.Key.reservedCount - kv.Value);

            return false;
        }

        reservationId = Guid.NewGuid().ToString();
        reservations[reservationId] = allocation;
        SetReservationMetadataInternal(reservationId, kind, ownerId, ownerType);
        MarkUIDirty();
        return true;
    }

    public bool TryReservePopulationFromGroup(Guid groupId, int amount, out string reservationId)
    {
        return TryReservePopulationFromGroup(
            groupId,
            amount,
            PopulationReservationKind.GenericTask,
            ownerId: null,
            ownerType: null,
            out reservationId);
    }

    public bool TryReservePopulationFromGroup(
        Guid groupId,
        int amount,
        PopulationReservationKind kind,
        string ownerId,
        string ownerType,
        out string reservationId)
    {
        reservationId = null;
        if (amount <= 0)
            return false;

        var sim = GetFamilySim();
        if (sim != null)
        {
            FillActuallyAvailableTaskWorkers(
                _tmpAvailableWorkers,
                p => p.AggregatedGroupGuid == groupId);

            if (_tmpAvailableWorkers.Count < amount)
                return false;

            ShuffleInPlace(_tmpAvailableWorkers);

            var picked = new List<Individual>(amount);
            for (int i = 0; i < amount; i++)
                picked.Add(_tmpAvailableWorkers[i]);

            return TryReservePopulationForIndividuals(
                picked,
                kind,
                ownerId,
                ownerType,
                out reservationId);
        }

        var group = FindPopulationGroupById(groupId);
        if (group == null || !IsTaskCapableGroup(group))
            return false;

        int free = EffectiveAvailableForTaskAggregateFallback(group);
        if (free < amount)
            return false;

        group.reservedCount += amount;

        reservationId = Guid.NewGuid().ToString();
        reservations[reservationId] = new Dictionary<PopulationGroup, int>
        {
            { group, amount }
        };

        SetReservationMetadataInternal(reservationId, kind, ownerId, ownerType);
        MarkUIDirty();
        return true;
    }

    public bool TryReservePopulationForIndividuals(
        IReadOnlyList<Individual> inds,
        out string reservationId)
    {
        return TryReservePopulationForIndividuals(
            inds,
            PopulationReservationKind.GenericTask,
            ownerId: null,
            ownerType: null,
            out reservationId);
    }

    public bool TryReservePopulationForIndividuals(
        IReadOnlyList<Individual> inds,
        PopulationReservationKind kind,
        string ownerId,
        string ownerType,
        out string reservationId)
    {
        reservationId = null;
        if (inds == null || inds.Count == 0)
            return false;

        var allocation = new Dictionary<PopulationGroup, int>();

        for (int i = 0; i < inds.Count; i++)
        {
            var ind = inds[i];
            if (ind == null || !ind.IsAlive)
                return false;

            if (ind.AggregatedAgeGroup != AgeGroup.Teen &&
                ind.AggregatedAgeGroup != AgeGroup.Adult)
                return false;

            if (ind.IsBusy)
                return false;

            if (IsIndividualReservedAnywhere(ind.Id))
                return false;

            var group = FindPopulationGroupById(ind.AggregatedGroupGuid);
            if (group == null)
                return false;

            if (!allocation.TryAdd(group, 1))
                allocation[group] += 1;
        }

        foreach (var kv in allocation)
            kv.Key.reservedCount += kv.Value;

        reservationId = Guid.NewGuid().ToString();
        reservations[reservationId] = allocation;
        SetReservationMetadataInternal(reservationId, kind, ownerId, ownerType);

        RegisterBusyForReservation(reservationId, inds);
        MarkUIDirty();
        MarkPopulationDirty();
        return true;
    }

    public bool TryPickRandomNonBusyTaskIndividuals(
        int amount,
        out List<Individual> picked,
        out string reservationId)
    {
        return TryPickRandomNonBusyTaskIndividuals(
            amount,
            PopulationReservationKind.GenericTask,
            ownerId: null,
            ownerType: null,
            out picked,
            out reservationId);
    }

    public bool TryPickRandomNonBusyTaskIndividuals(
        int amount,
        PopulationReservationKind kind,
        string ownerId,
        string ownerType,
        out List<Individual> picked,
        out string reservationId)
    {
        picked = null;
        reservationId = null;

        if (amount <= 0)
            return false;

        var sim = GetFamilySim();
        if (sim == null)
            return false;

        FillActuallyAvailableTaskWorkers(_tmpAvailableWorkers);
        if (_tmpAvailableWorkers.Count < amount)
            return false;

        ShuffleInPlace(_tmpAvailableWorkers);

        picked = new List<Individual>(amount);
        for (int i = 0; i < amount; i++)
            picked.Add(_tmpAvailableWorkers[i]);

        if (!TryReservePopulationForIndividuals(
                picked,
                kind,
                ownerId,
                ownerType,
                out reservationId))
        {
            picked = null;
            reservationId = null;
            return false;
        }

        return true;
    }

    // -------------------------------------------------
    // Reservation lifecycle
    // -------------------------------------------------

    public void ReleaseReservation(string reservationId)
    {
        if (string.IsNullOrEmpty(reservationId))
            return;

        if (!reservations.TryGetValue(reservationId, out var allocation))
            return;

        UnbusyForReservation(reservationId);

        foreach (var kv in allocation)
        {
            var group = kv.Key;
            int reserved = kv.Value;
            if (group == null)
                continue;

            group.reservedCount = Mathf.Max(0, group.reservedCount - reserved);
        }

        reservations.Remove(reservationId);
        busyByReservation.Remove(reservationId);
        activelyBusyReservations.Remove(reservationId);
        reservationExpiryTurns.Remove(reservationId);
        RemoveReservationMetadataInternal(reservationId);

        MarkUIDirty();
        MarkPopulationDirty();
    }

    public void RegisterBusyForReservation(string reservationId, IEnumerable<Individual> inds)
    {
        if (string.IsNullOrEmpty(reservationId) || inds == null)
            return;

        if (!busyByReservation.TryGetValue(reservationId, out var list))
        {
            list = new List<string>();
            busyByReservation[reservationId] = list;
        }

        var sim = GetFamilySim();

        foreach (var ind in inds)
        {
            if (ind == null || !ind.IsAlive)
                continue;

            if (ind.AggregatedAgeGroup != AgeGroup.Teen &&
                ind.AggregatedAgeGroup != AgeGroup.Adult)
                continue;

            bool alreadyExists = false;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == ind.Id)
                {
                    alreadyExists = true;
                    break;
                }
            }

            if (!alreadyExists)
            {
                list.Add(ind.Id);
                AddIndividualToReservationIndex(reservationId, ind.Id);
            }

            if (sim != null)
                sim.SetIndividualBusy(ind.Id, true);
            else
                ind.IsBusy = true;
        }

        activelyBusyReservations.Add(reservationId);
        SetReservationBusyActiveFlag(reservationId, true);
        MarkUIDirty();
    }

    private void SetBusyForReservationInternal(string reservationId, bool busy, bool clearEntry)
    {
        if (string.IsNullOrEmpty(reservationId))
            return;

        if (!busyByReservation.TryGetValue(reservationId, out var list) || list == null)
            return;

        var sim = GetFamilySim();
        if (sim != null)
        {
            for (int i = 0; i < list.Count; i++)
                sim.SetIndividualBusy(list[i], busy);
        }

        if (busy)
            activelyBusyReservations.Add(reservationId);
        else
            activelyBusyReservations.Remove(reservationId);

        SetReservationBusyActiveFlag(reservationId, busy);

        if (clearEntry)
        {
            for (int i = 0; i < list.Count; i++)
                RemoveIndividualFromReservationIndex(reservationId, list[i]);

            busyByReservation.Remove(reservationId);
        }

        MarkUIDirty();
    }

    private void UnbusyForReservation(string reservationId)
    {
        SetBusyForReservationInternal(reservationId, busy: false, clearEntry: true);
    }

    public void UnbusyReservationOnly(string reservationId)
    {
        SetBusyForReservationInternal(reservationId, busy: false, clearEntry: false);
    }

    public void RebusyReservation(string reservationId)
    {
        SetBusyForReservationInternal(reservationId, busy: true, clearEntry: false);
    }

    public bool SetIndividualBusy(string individualId, bool busy)
    {
        var sim = GetFamilySim();
        return sim != null && sim.SetIndividualBusy(individualId, busy);
    }

    public bool TryGetReservedIndividualIds(string reservationId, out List<string> individualIds)
    {
        individualIds = null;

        if (string.IsNullOrEmpty(reservationId))
            return false;

        if (!busyByReservation.TryGetValue(reservationId, out var ids) || ids == null || ids.Count == 0)
            return false;

        individualIds = new List<string>(ids);
        return true;
    }

    public void ReleaseBusyIndividuals(string reservationId, IEnumerable<Individual> inds)
    {
        var sim = GetFamilySim();
        sim?.ReleaseBusyIndividuals(reservationId, inds);
    }

    public int GetTotalPopulation()
    {
        int total = 0;

        for (int i = 0; i < allPopulations.Count; i++)
        {
            var g = allPopulations[i];
            if (g != null)
                total += Mathf.Max(0, g.count);
        }

        return total;
    }

    private int SumAllocation(Dictionary<PopulationGroup, int> allocation)
    {
        if (allocation == null)
            return 0;

        int total = 0;
        foreach (var kv in allocation)
            total += Mathf.Max(0, kv.Value);

        return total;
    }

    public int ApplyPenaltyFromReservation(string reservationId, int penaltyAmount)
    {
        if (string.IsNullOrEmpty(reservationId))
            return 0;

        if (!reservations.TryGetValue(reservationId, out var allocation))
            return 0;

        if (penaltyAmount <= 0)
            return 0;

        var fam = GetFamilySim();

        int remainingPenalty = penaltyAmount;
        int lost = 0;

        var deathsByGroup = new Dictionary<Guid, int>();

        while (remainingPenalty > 0 && allocation.Count > 0)
        {
            int totalReserved = SumAllocation(allocation);
            if (totalReserved <= 0)
                break;

            int roll = UnityEngine.Random.Range(1, totalReserved + 1);
            int cumulative = 0;
            PopulationGroup selected = null;

            foreach (var kv in allocation)
            {
                cumulative += kv.Value;
                if (roll <= cumulative)
                {
                    selected = kv.Key;
                    break;
                }
            }

            if (selected == null)
                break;

            bool removedSpecificReservedVictim = false;
            if (fam != null)
                removedSpecificReservedVictim = TryRemoveReservedVictimFromGroup(reservationId, selected, fam);

            int totalBefore = GetTotalPopulation();
            int groupBefore = selected.count;

            selected.ApplyPopulationLoss(1);

            int groupAfter = selected.count;
            int totalAfter = GetTotalPopulation();

            if (groupAfter < groupBefore)
            {
                int amountLost = groupBefore - groupAfter;

                LogPopulationRemoval(
                    "ApplyPenaltyFromReservation",
                    selected,
                    amountLost,
                    totalBefore,
                    totalAfter,
                    groupBefore,
                    groupAfter
                );

                lost += amountLost;

                if (!deathsByGroup.TryAdd(selected.GroupID, amountLost))
                    deathsByGroup[selected.GroupID] += amountLost;
            }

            if (!removedSpecificReservedVictim)
            {
                if (allocation.TryGetValue(selected, out var reservedForGroup))
                {
                    int newAlloc = Mathf.Max(0, reservedForGroup - 1);
                    if (newAlloc > 0) allocation[selected] = newAlloc;
                    else allocation.Remove(selected);
                }

                selected.reservedCount = Mathf.Max(0, selected.reservedCount - 1);
            }

            remainingPenalty--;
        }

        if (fam != null)
        {
            foreach (var kv in deathsByGroup)
                fam.ApplyDeathsToIndividuals(kv.Key, kv.Value);
        }

        MarkPopulationDirty();
        MarkUIDirty();
        return lost;
    }

    private void DetachGroupFromReservations(PopulationGroup group)
    {
        if (group == null)
            return;

        var sim = GetFamilySim();
        var people = sim != null ? sim.GetIndividuals() : null;

        _tmpReservationIds.Clear();

        foreach (var kv in reservations)
        {
            string reservationId = kv.Key;
            var alloc = kv.Value;
            if (alloc == null)
                continue;

            if (!alloc.Remove(group))
                continue;

            if (busyByReservation.TryGetValue(reservationId, out var ids) && ids != null)
            {
                for (int i = ids.Count - 1; i >= 0; i--)
                {
                    string id = ids[i];
                    var person = FindIndividualById(people, id);

                    if (person == null || person.AggregatedGroupGuid == group.GroupID)
                    {
                        RemoveIndividualFromReservationIndex(reservationId, id);
                        ids.RemoveAt(i);
                    }
                }

                if (ids.Count == 0)
                    busyByReservation.Remove(reservationId);
            }

            if (alloc.Count == 0)
                _tmpReservationIds.Add(reservationId);
        }

        group.reservedCount = 0;

        for (int i = 0; i < _tmpReservationIds.Count; i++)
        {
            string id = _tmpReservationIds[i];
            reservations.Remove(id);
            busyByReservation.Remove(id);
            activelyBusyReservations.Remove(id);
            reservationExpiryTurns.Remove(id);
            RemoveReservationMetadataInternal(id);
        }
    }

    public void RemovePopulationGroup(PopulationGroup group)
    {
        if (group == null)
            return;

        DetachGroupFromReservations(group);
        allPopulations.Remove(group);
        UntrackGroup(group);

        MarkUIDirty();
        MarkPopulationDirty();
    }

    public void PruneDeadOrEmptyGroups()
    {
        var general = GeneralPopulationManager.Instance;
        bool removedAny = false;

        for (int i = allPopulations.Count - 1; i >= 0; i--)
        {
            var g = allPopulations[i];
            bool endOfLife = general != null && g.averageAgeInTurns >= general.lifespan;
            bool zeroHealth = g.averageHealth <= 0f;

            if (g.count <= 0 || endOfLife || zeroHealth)
            {
                string reason = g.count <= 0
                    ? "Count<=0"
                    : endOfLife
                        ? "EndOfLife"
                        : "ZeroHealth";

                Debug.LogWarning(
                    $"[POP PRUNE] " +
                    $"Reason={reason} | " +
                    $"GroupID={g.GroupID} | " +
                    $"AgeGroup={g.ageGroup} | Gender={g.gender} | " +
                    $"Count={g.count} | " +
                    $"AvgAgeTurns={g.averageAgeInTurns} | " +
                    $"Health01={g.averageHealth:F3}");

                DetachGroupFromReservations(g);
                allPopulations.RemoveAt(i);
                UntrackGroup(g);
                removedAny = true;
            }
        }

        if (removedAny)
        {
            MarkUIDirty();
            MarkPopulationDirty();
        }
    }

    public void ApplyPoisonToPeople(int peopleCount, float damage01)
    {
        if (peopleCount <= 0 || damage01 <= 0f || allPopulations.Count == 0)
            return;

        int total = GetTotalPopulation();
        if (total <= 0)
            return;

        peopleCount = Mathf.Min(peopleCount, total);

        _tmpPopulationGroups.Clear();
        for (int i = 0; i < allPopulations.Count; i++)
        {
            var g = allPopulations[i];
            if (g != null && g.count > 0)
                _tmpPopulationGroups.Add(g);
        }

        if (_tmpPopulationGroups.Count == 0)
            return;

        var affectedPerGroup = new Dictionary<PopulationGroup, int>(_tmpPopulationGroups.Count);
        int remaining = peopleCount;

        while (remaining > 0 && _tmpPopulationGroups.Count > 0)
        {
            int totalWeight = 0;
            for (int i = 0; i < _tmpPopulationGroups.Count; i++)
                totalWeight += Mathf.Max(0, _tmpPopulationGroups[i].count);

            if (totalWeight <= 0)
                break;

            int roll = UnityEngine.Random.Range(1, totalWeight + 1);
            int cum = 0;
            PopulationGroup chosen = null;

            for (int i = 0; i < _tmpPopulationGroups.Count; i++)
            {
                var g = _tmpPopulationGroups[i];
                cum += g.count;
                if (roll <= cum)
                {
                    chosen = g;
                    break;
                }
            }

            if (chosen == null)
                break;

            if (!affectedPerGroup.TryAdd(chosen, 1))
                affectedPerGroup[chosen] += 1;

            remaining--;
        }

        foreach (var kv in affectedPerGroup)
        {
            var g = kv.Key;
            int hits = kv.Value;
            if (g.count <= 0 || hits <= 0)
                continue;

            float frac = (float)hits / g.count;
            float groupDelta = -damage01 * frac;
            g.AdjustHealth(groupDelta);
        }

        MarkPopulationDirty();
        MarkUIDirty();
    }

    public bool TryGetReservationExpiryTurn(string reservationId, out int expiryTurn)
    {
        if (string.IsNullOrEmpty(reservationId))
        {
            expiryTurn = -1;
            return false;
        }

        if (reservationExpiryTurns.TryGetValue(reservationId, out expiryTurn))
            return true;

        expiryTurn = -1;
        return false;
    }

    public bool TryComputeAndStoreReservationExpiryTurn(string reservationId, out int expiryTurn)
    {
        expiryTurn = -1;

        if (string.IsNullOrEmpty(reservationId))
            return false;

        if (!reservations.TryGetValue(reservationId, out var allocation) || allocation == null || allocation.Count == 0)
            return false;

        int currentTurn = TurnSystem.Instance != null ? TurnSystem.GetCurrentTurn() : 0;

        var rulebook = PlayerHealthRulebook.Instance;
        var general = GeneralPopulationManager.Instance;

        int elderAge;
        if (rulebook != null)
            elderAge = rulebook.adultToElderAge;
        else if (general != null)
            elderAge = general.adultToElderAge;
        else
            elderAge = 100;

        long weightedAgeSum = 0;
        int totalReserved = 0;

        foreach (var kv in allocation)
        {
            var group = kv.Key;
            int amount = kv.Value;
            if (group == null || amount <= 0)
                continue;

            weightedAgeSum += (long)group.averageAgeInTurns * amount;
            totalReserved += amount;
        }

        if (totalReserved <= 0)
            return false;

        float avgAge = (float)weightedAgeSum / totalReserved;

        int remaining = Mathf.Max(1, elderAge - Mathf.RoundToInt(avgAge));
        expiryTurn = currentTurn + remaining;

        reservationExpiryTurns[reservationId] = expiryTurn;
        return true;
    }

    public bool IsIndividualReservedAnywhere(string individualId, string excludeReservationId = null)
    {
        if (string.IsNullOrWhiteSpace(individualId))
            return false;

        if (!_reservationByIndividualId.TryGetValue(individualId, out var reservationId))
            return false;

        if (string.IsNullOrWhiteSpace(excludeReservationId))
            return true;

        return !string.Equals(reservationId, excludeReservationId, StringComparison.Ordinal);
    }

    public bool TryDetachIndividualFromExistingReservations(string individualId, out bool allReservationsReplaced)
    {
        allReservationsReplaced = true;

        if (string.IsNullOrEmpty(individualId))
            return false;

        var sim = GetFamilySim();
        if (sim == null)
            return false;

        var person = FindIndividualById(sim.GetIndividuals(), individualId);
        if (person == null)
            return false;

        if (!_reservationByIndividualId.TryGetValue(individualId, out var reservationId) ||
            string.IsNullOrWhiteSpace(reservationId))
        {
            return true;
        }

        bool replaced = TryReplaceReservedIndividualInternal(reservationId, person, sim);
        if (!replaced)
            allReservationsReplaced = false;

        MarkPopulationDirty();
        MarkUIDirty();
        return true;
    }

    private bool TryReplaceReservedIndividualInternal(
        string reservationId,
        Individual outgoing,
        PlayerFamilySimulationManager sim)
    {
        if (string.IsNullOrEmpty(reservationId) || outgoing == null || sim == null)
            return false;

        if (!busyByReservation.TryGetValue(reservationId, out var reservedIds) || reservedIds == null)
            return false;

        int outgoingIndex = reservedIds.IndexOf(outgoing.Id);
        if (outgoingIndex < 0)
            return true;

        bool reservationCurrentlyBusy = false;
        var people = sim.GetIndividuals();

        for (int i = 0; i < reservedIds.Count; i++)
        {
            string id = reservedIds[i];
            if (id == outgoing.Id)
                continue;

            var other = FindIndividualById(people, id);
            if (other != null && other.IsAlive && other.IsBusy)
            {
                reservationCurrentlyBusy = true;
                break;
            }
        }

        RemoveIndividualFromReservationInternal(reservationId, outgoing, reservedIds);

        var replacement = FindReplacementCandidateForReservation(
            sim,
            reservedIds,
            outgoing.Id,
            reservationId,
            outgoing.AggregatedGroupGuid
        );

        if (replacement == null)
        {
            Debug.Log($"[PlayersPopulationManager] Reservation {reservationId} lost worker {outgoing.Id} and could not be backfilled.");
            return false;
        }

        reservedIds.Add(replacement.Id);
        AddIndividualToReservationInternal(reservationId, replacement);
        AddIndividualToReservationIndex(reservationId, replacement.Id);

        if (reservationCurrentlyBusy)
            sim.SetIndividualBusy(replacement.Id, true);

        Debug.Log($"[PlayersPopulationManager] Reservation {reservationId} replaced {outgoing.Id} with {replacement.Id}.");

        MarkPopulationDirty();
        return true;
    }

    private Individual FindReplacementCandidateForReservation(
        PlayerFamilySimulationManager sim,
        List<string> idsAlreadyInReservation,
        string outgoingIndividualId,
        string reservationId,
        Guid preferredGroupId)
    {
        if (sim == null)
            return null;

        var excluded = new HashSet<string>(StringComparer.Ordinal);

        if (idsAlreadyInReservation != null)
        {
            for (int i = 0; i < idsAlreadyInReservation.Count; i++)
            {
                var id = idsAlreadyInReservation[i];
                if (!string.IsNullOrEmpty(id))
                    excluded.Add(id);
            }
        }

        if (!string.IsNullOrEmpty(outgoingIndividualId))
            excluded.Add(outgoingIndividualId);

        var people = sim.GetIndividuals();
        Individual fallback = null;

        for (int i = 0; i < people.Count; i++)
        {
            var p = people[i];
            if (!IsValidReplacementCandidate(p, excluded, reservationId))
                continue;

            if (p.AggregatedGroupGuid == preferredGroupId)
                return p;

            if (fallback == null)
                fallback = p;
        }

        return fallback;
    }

    private bool IsValidReplacementCandidate(
        Individual person,
        HashSet<string> excludedIds,
        string reservationId)
    {
        if (person == null || !person.IsAlive)
            return false;

        if (excludedIds != null && excludedIds.Contains(person.Id))
            return false;

        if (person.IsBusy)
            return false;

        if (person.AggregatedAgeGroup != AgeGroup.Teen &&
            person.AggregatedAgeGroup != AgeGroup.Adult)
            return false;

        if (IsIndividualReservedAnywhere(person.Id, reservationId))
            return false;

        return true;
    }

    private void RemoveIndividualFromReservationInternal(
        string reservationId,
        Individual person,
        List<string> reservedIds)
    {
        if (person == null)
            return;

        if (reservedIds != null)
            reservedIds.Remove(person.Id);

        RemoveIndividualFromReservationIndex(reservationId, person.Id);

        if (!reservations.TryGetValue(reservationId, out var allocation) || allocation == null)
            return;

        PopulationGroup group = FindPopulationGroupById(person.AggregatedGroupGuid);
        if (group == null)
            return;

        if (allocation.TryGetValue(group, out var amount))
        {
            amount = Mathf.Max(0, amount - 1);
            if (amount > 0)
                allocation[group] = amount;
            else
                allocation.Remove(group);
        }

        group.reservedCount = Mathf.Max(0, group.reservedCount - 1);
        MarkPopulationDirty();
    }

    private void AddIndividualToReservationInternal(string reservationId, Individual person)
    {
        if (string.IsNullOrEmpty(reservationId) || person == null)
            return;

        if (!reservations.TryGetValue(reservationId, out var allocation) || allocation == null)
        {
            allocation = new Dictionary<PopulationGroup, int>();
            reservations[reservationId] = allocation;
        }

        PopulationGroup group = FindPopulationGroupById(person.AggregatedGroupGuid);
        if (group == null)
            return;

        if (!allocation.TryAdd(group, 1))
            allocation[group] += 1;

        group.reservedCount += 1;
        AddIndividualToReservationIndex(reservationId, person.Id);
        MarkPopulationDirty();
    }

    public bool TryTopUpReservationToRequiredCount(string reservationId, int requiredCount)
    {
        if (string.IsNullOrEmpty(reservationId) || requiredCount <= 0)
            return false;

        if (!reservations.TryGetValue(reservationId, out var allocation) || allocation == null)
            return false;

        var sim = GetFamilySim();
        if (sim == null)
            return false;

        if (!busyByReservation.TryGetValue(reservationId, out var reservedIds) || reservedIds == null)
        {
            reservedIds = new List<string>();
            busyByReservation[reservationId] = reservedIds;
        }

        int currentReserved = SumAllocation(allocation);
        int missing = requiredCount - currentReserved;
        if (missing <= 0)
            return true;

        bool reservationCurrentlyBusy = false;
        var people = sim.GetIndividuals();

        for (int i = 0; i < reservedIds.Count; i++)
        {
            var person = FindIndividualById(people, reservedIds[i]);
            if (person != null && person.IsAlive && person.IsBusy)
            {
                reservationCurrentlyBusy = true;
                break;
            }
        }

        var excluded = new HashSet<string>(reservedIds, StringComparer.Ordinal);

        _tmpAvailableWorkers.Clear();

        for (int i = 0; i < people.Count; i++)
        {
            var p = people[i];
            if (p == null || !p.IsAlive)
                continue;

            if (excluded.Contains(p.Id))
                continue;

            if (p.IsBusy)
                continue;

            if (p.AggregatedAgeGroup != AgeGroup.Teen &&
                p.AggregatedAgeGroup != AgeGroup.Adult)
                continue;

            if (IsIndividualReservedAnywhere(p.Id, reservationId))
                continue;

            _tmpAvailableWorkers.Add(p);
        }

        if (_tmpAvailableWorkers.Count < missing)
            return false;

        ShuffleInPlace(_tmpAvailableWorkers);

        for (int i = 0; i < missing; i++)
        {
            var replacement = _tmpAvailableWorkers[i];
            reservedIds.Add(replacement.Id);
            AddIndividualToReservationInternal(reservationId, replacement);

            if (reservationCurrentlyBusy)
                sim.SetIndividualBusy(replacement.Id, true);
        }

        MarkPopulationDirty();
        MarkUIDirty();
        return true;
    }

    private bool TryRemoveReservedVictimFromGroup(
        string reservationId,
        PopulationGroup group,
        PlayerFamilySimulationManager sim)
    {
        if (string.IsNullOrEmpty(reservationId) || group == null || sim == null)
            return false;

        if (!busyByReservation.TryGetValue(reservationId, out var reservedIds) ||
            reservedIds == null || reservedIds.Count == 0)
        {
            return false;
        }

        var people = sim.GetIndividuals();

        for (int i = 0; i < reservedIds.Count; i++)
        {
            string id = reservedIds[i];
            var person = FindIndividualById(people, id);

            if (person == null || !person.IsAlive)
            {
                RemoveIndividualFromReservationIndex(reservationId, id);
                reservedIds.RemoveAt(i);
                i--;
                continue;
            }

            if (person.AggregatedGroupGuid != group.GroupID)
                continue;

            sim.SetIndividualBusy(person.Id, false);
            RemoveIndividualFromReservationInternal(reservationId, person, reservedIds);
            return true;
        }

        return false;
    }

    public PlayersPopulationSaveData SaveState()
    {
        PlayersPopulationSaveData data = new PlayersPopulationSaveData
        {
            maxPopulation = maxPopulation,
            startingPopulation = startingPopulation,
            ignoreMaxDuringInitialization = ignoreMaxDuringInitialization,
            zeroOrLessMaxMeansUnlimited = zeroOrLessMaxMeansUnlimited,
            startingFamilyCount = startingFamilyCount
        };

        for (int i = 0; i < allPopulations.Count; i++)
        {
            PopulationGroup g = allPopulations[i];
            if (g == null)
                continue;

            Guid groupGuid = SaveReflectionUtil.Get(g, "GroupID", Guid.Empty);
            int additionTurn = SaveReflectionUtil.Get(g, "additionTurn", 0);

            data.groups.Add(new PopulationGroupSaveData
            {
                groupId = groupGuid != Guid.Empty ? groupGuid.ToString() : string.Empty,
                ageGroup = g.ageGroup,
                gender = g.gender,
                count = g.count,
                additionTurn = additionTurn,
                averageAgeInTurns = g.averageAgeInTurns,
                averageHealth = g.averageHealth,
                maxHealthPerIndividual = g.maxHealthPerIndividual,
                hungerLevel = g.hungerLevel,
                thirstLevel = g.thirstLevel
            });
        }

        foreach (var kv in reservations)
        {
            string reservationId = kv.Key;
            Dictionary<PopulationGroup, int> allocation = kv.Value;

            PopulationReservationSaveData savedReservation = new PopulationReservationSaveData
            {
                reservationId = reservationId
            };

            if (allocation != null)
            {
                foreach (var alloc in allocation)
                {
                    if (alloc.Key == null || alloc.Value <= 0)
                        continue;

                    Guid groupGuid = SaveReflectionUtil.Get(alloc.Key, "GroupID", Guid.Empty);

                    savedReservation.allocations.Add(new PopulationReservationAllocationSaveData
                    {
                        groupId = groupGuid != Guid.Empty ? groupGuid.ToString() : string.Empty,
                        amount = alloc.Value
                    });
                }
            }

            if (busyByReservation.TryGetValue(reservationId, out List<string> ids) && ids != null)
                savedReservation.reservedIndividualIds = new List<string>(ids);

            savedReservation.isBusyActive = activelyBusyReservations.Contains(reservationId);

            if (reservationExpiryTurns.TryGetValue(reservationId, out int expiryTurn))
            {
                savedReservation.hasExpiryTurn = true;
                savedReservation.expiryTurn = expiryTurn;
            }

            if (_reservationMetaById.TryGetValue(reservationId, out var meta) && meta != null)
            {
                savedReservation.reservationKind = (int)meta.kind;
                savedReservation.reservationOwnerId = meta.ownerId;
                savedReservation.reservationOwnerType = meta.ownerType;
            }
            else
            {
                savedReservation.reservationKind = (int)PopulationReservationKind.GenericTask;
                savedReservation.reservationOwnerId = null;
                savedReservation.reservationOwnerType = null;
            }

            data.reservations.Add(savedReservation);
        }

        return data;
    }

    public void LoadState(PlayersPopulationSaveData data)
    {
        if (data == null)
            return;

        maxPopulation = data.maxPopulation;
        startingPopulation = data.startingPopulation;
        ignoreMaxDuringInitialization = data.ignoreMaxDuringInitialization;
        zeroOrLessMaxMeansUnlimited = data.zeroOrLessMaxMeansUnlimited;
        startingFamilyCount = data.startingFamilyCount;

        reservations.Clear();
        busyByReservation.Clear();
        reservationExpiryTurns.Clear();
        activelyBusyReservations.Clear();
        _reservationByIndividualId.Clear();
        _reservationMetaById.Clear();
        _reservationIdsByKind.Clear();
        _groupsById.Clear();
        allPopulations.Clear();

        Dictionary<Guid, PopulationGroup> groupByGuid = new Dictionary<Guid, PopulationGroup>();

        if (data.groups != null)
        {
            for (int i = 0; i < data.groups.Count; i++)
            {
                PopulationGroupSaveData saved = data.groups[i];
                if (saved == null)
                    continue;

                PopulationGroup group = new PopulationGroup(
                    saved.ageGroup,
                    saved.gender,
                    Mathf.Max(0, saved.count),
                    saved.additionTurn,
                    saved.averageAgeInTurns,
                    saved.averageHealth,
                    Mathf.Max(1, saved.maxHealthPerIndividual)
                );

                group.hungerLevel = Mathf.Clamp01(saved.hungerLevel);
                group.thirstLevel = Mathf.Clamp01(saved.thirstLevel);
                group.reservedCount = 0;

                if (Guid.TryParse(saved.groupId, out Guid parsedGuid))
                {
                    SaveReflectionUtil.Set(group, "GroupID", parsedGuid);
                    groupByGuid[parsedGuid] = group;
                }

                allPopulations.Add(group);
            }
        }

        if (data.reservations != null)
        {
            for (int i = 0; i < data.reservations.Count; i++)
            {
                PopulationReservationSaveData savedReservation = data.reservations[i];
                if (savedReservation == null || string.IsNullOrWhiteSpace(savedReservation.reservationId))
                    continue;

                Dictionary<PopulationGroup, int> allocation = new Dictionary<PopulationGroup, int>();

                if (savedReservation.allocations != null)
                {
                    for (int j = 0; j < savedReservation.allocations.Count; j++)
                    {
                        PopulationReservationAllocationSaveData savedAlloc = savedReservation.allocations[j];
                        if (savedAlloc == null || savedAlloc.amount <= 0)
                            continue;

                        if (!Guid.TryParse(savedAlloc.groupId, out Guid gid))
                            continue;

                        if (!groupByGuid.TryGetValue(gid, out PopulationGroup group) || group == null)
                            continue;

                        int amount = Mathf.Max(0, savedAlloc.amount);
                        if (amount <= 0)
                            continue;

                        allocation[group] = amount;
                        group.reservedCount += amount;
                    }
                }

                reservations[savedReservation.reservationId] = allocation;

                busyByReservation[savedReservation.reservationId] =
                    savedReservation.reservedIndividualIds != null
                        ? new List<string>(savedReservation.reservedIndividualIds)
                        : new List<string>();

                if (savedReservation.isBusyActive)
                    activelyBusyReservations.Add(savedReservation.reservationId);

                if (savedReservation.hasExpiryTurn)
                    reservationExpiryTurns[savedReservation.reservationId] = savedReservation.expiryTurn;

                var kind = Enum.IsDefined(typeof(PopulationReservationKind), savedReservation.reservationKind)
                    ? (PopulationReservationKind)savedReservation.reservationKind
                    : PopulationReservationKind.GenericTask;

                SetReservationMetadataInternal(
                    savedReservation.reservationId,
                    kind,
                    savedReservation.reservationOwnerId,
                    savedReservation.reservationOwnerType);

                SetReservationBusyActiveFlag(savedReservation.reservationId, savedReservation.isBusyActive);
            }
        }

        RebuildIndexes();

        MarkPopulationDirty();
        MarkUIDirty();
    }

    public void ReapplyBusyFlagsFromReservations()
    {
        PlayerFamilySimulationManager sim = GetFamilySim();
        if (sim == null)
        {
            MarkUIDirty();
            return;
        }

        foreach (var kv in busyByReservation)
        {
            if (!activelyBusyReservations.Contains(kv.Key))
                continue;

            List<string> ids = kv.Value;
            if (ids == null)
                continue;

            for (int i = 0; i < ids.Count; i++)
            {
                string id = ids[i];
                if (!string.IsNullOrWhiteSpace(id))
                    sim.SetIndividualBusy(id, true);
            }
        }

        MarkPopulationDirty();
        MarkUIDirty();
    }

    public int RemoveIndividualsFromReservation(
        string reservationId,
        IEnumerable<string> individualIds,
        bool unbusyRemovedIndividuals)
    {
        if (string.IsNullOrWhiteSpace(reservationId) || individualIds == null)
            return 0;

        var sim = GetFamilySim();
        if (sim == null)
            return 0;

        if (!busyByReservation.TryGetValue(reservationId, out var reservedIds) ||
            reservedIds == null || reservedIds.Count == 0)
            return 0;

        var idsToRemove = new HashSet<string>(individualIds, StringComparer.Ordinal);
        if (idsToRemove.Count == 0)
            return 0;

        var people = sim.GetIndividuals();
        int removed = 0;

        for (int i = reservedIds.Count - 1; i >= 0; i--)
        {
            string id = reservedIds[i];
            if (!idsToRemove.Contains(id))
                continue;

            var person = FindIndividualById(people, id);
            if (person == null)
                continue;

            if (unbusyRemovedIndividuals)
                sim.SetIndividualBusy(person.Id, false);

            RemoveIndividualFromReservationInternal(reservationId, person, reservedIds);
            removed++;
        }

        if (reservedIds.Count == 0)
        {
            busyByReservation.Remove(reservationId);
            activelyBusyReservations.Remove(reservationId);
            SetReservationBusyActiveFlag(reservationId, false);
        }

        if (reservations.TryGetValue(reservationId, out var allocation) &&
            (allocation == null || allocation.Count == 0))
        {
            reservations.Remove(reservationId);
            busyByReservation.Remove(reservationId);
            activelyBusyReservations.Remove(reservationId);
            reservationExpiryTurns.Remove(reservationId);
            RemoveReservationMetadataInternal(reservationId);
        }

        MarkPopulationDirty();
        MarkUIDirty();
        return removed;
    }

    private string GetExternalPopulationCaller()
    {
        var trace = new System.Diagnostics.StackTrace(true);

        for (int i = 0; i < trace.FrameCount; i++)
        {
            var frame = trace.GetFrame(i);
            var method = frame?.GetMethod();
            if (method == null)
                continue;

            var type = method.DeclaringType;
            if (type == null)
                continue;

            if (type == typeof(PlayersPopulationManager))
                continue;

            string file = frame.GetFileName();
            int line = frame.GetFileLineNumber();

            string fileText = string.IsNullOrEmpty(file)
                ? "unknown file"
                : System.IO.Path.GetFileName(file);

            return $"{type.Name}.{method.Name} ({fileText}:{line})";
        }

        return "Unknown external caller";
    }

    private void LogPopulationRemoval(
        string reason,
        PopulationGroup group,
        int amountRemoved,
        int totalBefore,
        int totalAfter,
        int groupBefore,
        int groupAfter)
    {
        if (!debugPopulationRemoval)
            return;

        string caller = GetExternalPopulationCaller();

        string message =
            $"[POP REMOVE] Reason={reason} | " +
            $"Caller={caller} | " +
            $"AmountRemoved={amountRemoved} | " +
            $"TotalPopulation {totalBefore} -> {totalAfter} | " +
            $"GroupCount {groupBefore} -> {groupAfter} | " +
            $"AgeGroup={(group != null ? group.ageGroup.ToString() : "null")} | " +
            $"Gender={(group != null ? group.gender.ToString() : "null")} | " +
            $"GroupID={(group != null ? group.GroupID.ToString() : "null")}";

        if (debugPopulationRemovalStack)
            message += $"\nStack Trace:\n{Environment.StackTrace}";

        Debug.LogWarning(message);
    }
}