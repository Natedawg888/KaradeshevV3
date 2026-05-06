using System;
using System.Collections.Generic;
using UnityEngine;

public class IndividualRepository
{
    public int MaxIndividuals { get; private set; }

    private readonly List<Individual> _individuals;
    private readonly Dictionary<string, Individual> _byId;
    private readonly Dictionary<Guid, HashSet<string>> _aliveIdsByGroup;
    private readonly Dictionary<string, HashSet<string>> _aliveIdsByFamily;
    private readonly HashSet<string> _taskCapableNonBusyIds;
    private readonly System.Random _rng;

    private readonly List<Individual> _tmp = new List<Individual>(128);

    public IndividualRepository(int maxIndividuals, System.Random rng)
    {
        MaxIndividuals = Mathf.Max(1, maxIndividuals);
        _rng = rng ?? new System.Random();

        int cap = Mathf.Min(MaxIndividuals, 2048);

        _individuals = new List<Individual>(cap);
        _byId = new Dictionary<string, Individual>(cap, StringComparer.Ordinal);
        _aliveIdsByGroup = new Dictionary<Guid, HashSet<string>>();
        _aliveIdsByFamily = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        _taskCapableNonBusyIds = new HashSet<string>(StringComparer.Ordinal);
    }

    public IReadOnlyList<Individual> All => _individuals;
    public int Count => _individuals.Count;
    public int TaskCapableNonBusyCount => _taskCapableNonBusyIds.Count;

    public void Clear()
    {
        _individuals.Clear();
        _byId.Clear();
        _aliveIdsByGroup.Clear();
        _aliveIdsByFamily.Clear();
        _taskCapableNonBusyIds.Clear();
    }

    public void RebuildIndexes()
    {
        _byId.Clear();
        _aliveIdsByGroup.Clear();
        _aliveIdsByFamily.Clear();
        _taskCapableNonBusyIds.Clear();

        for (int i = 0; i < _individuals.Count; i++)
        {
            var p = _individuals[i];
            if (p == null || string.IsNullOrEmpty(p.Id))
                continue;

            _byId[p.Id] = p;
            IndexCurrentState(p);
        }
    }

    public bool TryAdd(Individual ind)
    {
        if (ind == null || string.IsNullOrEmpty(ind.Id))
            return false;

        if (_individuals.Count >= MaxIndividuals)
            return false;

        if (_byId.ContainsKey(ind.Id))
            return false;

        _individuals.Add(ind);
        _byId.Add(ind.Id, ind);
        IndexCurrentState(ind);
        return true;
    }

    public Individual FindById(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        Individual found;
        return _byId.TryGetValue(id, out found) ? found : null;
    }

    public void Kill(Individual ind)
    {
        if (ind == null || !ind.IsAlive)
            return;

        UnindexCurrentState(ind);
        ind.IsAlive = false;
        ind.IsBusy = false;
    }

    public bool SetBusyById(string id, bool busy)
    {
        var p = FindById(id);
        if (p == null || !p.IsAlive)
            return false;

        if (p.IsBusy == busy)
            return true;

        if (IsTaskCapableNonBusy(p))
            _taskCapableNonBusyIds.Remove(p.Id);

        p.IsBusy = busy;

        if (IsTaskCapableNonBusy(p))
            _taskCapableNonBusyIds.Add(p.Id);

        return true;
    }

    public void SetFamily(Individual person, string familyId, string surname = null)
    {
        if (person == null)
            return;

        string oldFamilyId = person.FamilyId;
        if (string.Equals(oldFamilyId, familyId, StringComparison.Ordinal))
        {
            if (surname != null)
                person.Surname = surname;
            return;
        }

        if (person.IsAlive && !string.IsNullOrEmpty(oldFamilyId))
        {
            HashSet<string> oldSet;
            if (_aliveIdsByFamily.TryGetValue(oldFamilyId, out oldSet))
            {
                oldSet.Remove(person.Id);
                if (oldSet.Count == 0)
                    _aliveIdsByFamily.Remove(oldFamilyId);
            }
        }

        person.FamilyId = familyId;
        if (surname != null)
            person.Surname = surname;

        if (person.IsAlive && !string.IsNullOrEmpty(familyId))
        {
            HashSet<string> newSet;
            if (!_aliveIdsByFamily.TryGetValue(familyId, out newSet))
            {
                newSet = new HashSet<string>(StringComparer.Ordinal);
                _aliveIdsByFamily.Add(familyId, newSet);
            }

            newSet.Add(person.Id);
        }
    }

    public void SetAggregatedAgeGroup(Individual person, AgeGroup newAgeGroup)
    {
        if (person == null || person.AggregatedAgeGroup == newAgeGroup)
            return;

        if (IsTaskCapableNonBusy(person))
            _taskCapableNonBusyIds.Remove(person.Id);

        person.AggregatedAgeGroup = newAgeGroup;

        if (IsTaskCapableNonBusy(person))
            _taskCapableNonBusyIds.Add(person.Id);
    }

    public void SetAggregatedGroup(Individual person, Guid newGroupId)
    {
        if (person == null || person.AggregatedGroupGuid == newGroupId)
            return;

        if (person.IsAlive)
        {
            HashSet<string> oldSet;
            if (_aliveIdsByGroup.TryGetValue(person.AggregatedGroupGuid, out oldSet))
            {
                oldSet.Remove(person.Id);
                if (oldSet.Count == 0)
                    _aliveIdsByGroup.Remove(person.AggregatedGroupGuid);
            }
        }

        person.AggregatedGroupGuid = newGroupId;

        if (person.IsAlive)
        {
            HashSet<string> newSet;
            if (!_aliveIdsByGroup.TryGetValue(newGroupId, out newSet))
            {
                newSet = new HashSet<string>(StringComparer.Ordinal);
                _aliveIdsByGroup.Add(newGroupId, newSet);
            }

            newSet.Add(person.Id);
        }
    }

    public void AgeOneTurn(Func<int, AgeGroup> ageGroupResolver)
    {
        for (int i = 0; i < _individuals.Count; i++)
        {
            var p = _individuals[i];
            if (p == null || !p.IsAlive)
                continue;

            AgeGroup oldGroup = p.AggregatedAgeGroup;
            p.AgeOneTurn();

            if (ageGroupResolver != null)
            {
                AgeGroup newGroup = ageGroupResolver(p.AgeInTurns);
                if (newGroup != oldGroup)
                    SetAggregatedAgeGroup(p, newGroup);
            }
        }
    }

    public void GetEligibleAdultsForPairing(float minHealth01, int minAgeTurns, int maxAgeTurns, List<Individual> outList)
    {
        if (outList == null)
            outList = _tmp;

        outList.Clear();

        for (int i = 0; i < _individuals.Count; i++)
        {
            var p = _individuals[i];
            if (p == null || !p.IsAlive)
                continue;

            if (p.AggregatedAgeGroup != AgeGroup.Adult)
                continue;

            if (p.AgeInTurns < minAgeTurns || p.AgeInTurns > maxAgeTurns)
                continue;

            if (p.Health01 < minHealth01)
                continue;

            outList.Add(p);
        }
    }

    public void CopyAliveByGroupTo(Guid groupId, List<Individual> outList)
    {
        if (outList == null)
            return;

        outList.Clear();

        HashSet<string> ids;
        if (!_aliveIdsByGroup.TryGetValue(groupId, out ids) || ids == null || ids.Count == 0)
            return;

        foreach (string id in ids)
        {
            Individual p;
            if (_byId.TryGetValue(id, out p) && p != null && p.IsAlive)
                outList.Add(p);
        }
    }

    public void CopyAliveByFamilyTo(string familyId, List<Individual> outList)
    {
        if (outList == null)
            return;

        outList.Clear();

        if (string.IsNullOrEmpty(familyId))
            return;

        HashSet<string> ids;
        if (!_aliveIdsByFamily.TryGetValue(familyId, out ids) || ids == null || ids.Count == 0)
            return;

        foreach (string id in ids)
        {
            Individual p;
            if (_byId.TryGetValue(id, out p) && p != null && p.IsAlive)
                outList.Add(p);
        }
    }

    public void CopyTaskCapableNonBusyTo(List<Individual> outList, Func<Individual, bool> filter = null)
    {
        if (outList == null)
            return;

        outList.Clear();

        foreach (string id in _taskCapableNonBusyIds)
        {
            Individual p;
            if (!_byId.TryGetValue(id, out p) || p == null || !p.IsAlive)
                continue;

            if (filter != null && !filter(p))
                continue;

            outList.Add(p);
        }
    }

    public int AliveCount(Guid groupId)
    {
        HashSet<string> ids;
        return _aliveIdsByGroup.TryGetValue(groupId, out ids) && ids != null ? ids.Count : 0;
    }

    public void CullExcessToTarget(Guid groupId, int target)
    {
        _tmp.Clear();
        CopyAliveByGroupTo(groupId, _tmp);

        int excess = Mathf.Max(0, _tmp.Count - Mathf.Max(0, target));
        if (excess <= 0)
            return;

        Shuffle(_tmp);

        for (int i = 0; i < excess; i++)
            Kill(_tmp[i]);

        _tmp.Clear();
    }

    public IEnumerable<Individual> EnumerateTaskCapableNonBusy()
    {
        foreach (string id in _taskCapableNonBusyIds)
        {
            Individual p;
            if (_byId.TryGetValue(id, out p) && p != null && p.IsAlive)
                yield return p;
        }
    }

    public void Shuffle<T>(IList<T> list)
    {
        if (list == null || list.Count <= 1)
            return;

        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = _rng.Next(i + 1);
            T tmpVal = list[i];
            list[i] = list[j];
            list[j] = tmpVal;
        }
    }

    private void IndexCurrentState(Individual p)
    {
        if (p == null || !p.IsAlive)
            return;

        HashSet<string> groupSet;
        if (!_aliveIdsByGroup.TryGetValue(p.AggregatedGroupGuid, out groupSet))
        {
            groupSet = new HashSet<string>(StringComparer.Ordinal);
            _aliveIdsByGroup.Add(p.AggregatedGroupGuid, groupSet);
        }
        groupSet.Add(p.Id);

        if (!string.IsNullOrEmpty(p.FamilyId))
        {
            HashSet<string> familySet;
            if (!_aliveIdsByFamily.TryGetValue(p.FamilyId, out familySet))
            {
                familySet = new HashSet<string>(StringComparer.Ordinal);
                _aliveIdsByFamily.Add(p.FamilyId, familySet);
            }
            familySet.Add(p.Id);
        }

        if (IsTaskCapableNonBusy(p))
            _taskCapableNonBusyIds.Add(p.Id);
    }

    private void UnindexCurrentState(Individual p)
    {
        if (p == null || !p.IsAlive)
            return;

        HashSet<string> groupSet;
        if (_aliveIdsByGroup.TryGetValue(p.AggregatedGroupGuid, out groupSet))
        {
            groupSet.Remove(p.Id);
            if (groupSet.Count == 0)
                _aliveIdsByGroup.Remove(p.AggregatedGroupGuid);
        }

        if (!string.IsNullOrEmpty(p.FamilyId))
        {
            HashSet<string> familySet;
            if (_aliveIdsByFamily.TryGetValue(p.FamilyId, out familySet))
            {
                familySet.Remove(p.Id);
                if (familySet.Count == 0)
                    _aliveIdsByFamily.Remove(p.FamilyId);
            }
        }

        _taskCapableNonBusyIds.Remove(p.Id);
    }

    private static bool IsTaskCapableNonBusy(Individual p)
    {
        if (p == null || !p.IsAlive || p.IsBusy)
            return false;

        return p.AggregatedAgeGroup == AgeGroup.Teen ||
               p.AggregatedAgeGroup == AgeGroup.Adult;
    }
}