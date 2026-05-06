using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PlayerPopulationStatistic : MonoBehaviour
{
    [Header("Source")]
    public PlayersPopulationManager populationManager;

    [Header("History")]
    [Tooltip("How many turn-by-turn snapshots to keep.")]
    [Min(1)] public int historyLimit = 120;

    [SerializeField] private List<PopulationSnapshot> history = new();
    public IReadOnlyList<PopulationSnapshot> History => history;

    /// Fired whenever we record a new snapshot (usually once per turn).
    public event Action<PopulationSnapshot> OnSnapshotRecorded;

    private void Awake()
    {
        PlayerSetupInstaller installer = FindFirstObjectByType<PlayerSetupInstaller>(FindObjectsInactive.Include);
        if (installer != null)
            installer.RegisterPopulationStatistic(this);
    }

    private void OnEnable()
    {
        if (populationManager != null)
            populationManager.OnPopulationChanged += RecomputeNow;

        TurnSystem.SubscribeToEndOfTurn(OnTurnEnded);

        // Seed once on enable
        RecomputeNow();
        RecordSnapshotForCurrentTurn();
    }

    private void OnDisable()
    {
        if (populationManager != null)
            populationManager.OnPopulationChanged -= RecomputeNow;

        TurnSystem.UnsubscribeFromEndOfTurn(OnTurnEnded);
    }

    private void MarkPopulationDirty()
    {
        SaveSystem.MarkSectionDirty(SaveSectionKeys.Population);
    }

    private void OnTurnEnded()
    {
        // Take a snapshot each turn so we can measure growth/decline
        RecordSnapshotForCurrentTurn();
        MarkPopulationDirty();
    }

    // ---------- Public read helpers ----------

    public PopulationSnapshot Latest => history.Count > 0 ? history[^1] : default;

    /// Returns (deltaCount, deltaPercent) versus the previous snapshot.
    public (int delta, float percent) LatestGrowth()
    {
        if (history.Count < 2) return (0, 0f);
        var a = history[^1];
        var b = history[^2];
        int delta = a.total - b.total;
        float percent = (b.total > 0) ? (delta / (float)b.total) * 100f : 0f;
        return (delta, percent);
    }

    public AgeRatios GetAgeRatios()
    {
        var s = Latest;
        float total = Mathf.Max(1, s.total);
        return new AgeRatios
        {
            child = s.child / total,
            teen  = s.teen  / total,
            adult = s.adult / total,
            elder = s.elder / total
        };
    }

    public GenderRatios GetGenderRatios()
    {
        var s = Latest;
        float total = Mathf.Max(1, s.total);
        return new GenderRatios
        {
            male   = s.male   / total,
            female = s.female / total
        };
    }

    public float GetAverageHealth(AgeGroup ageGroup)
    {
        var s = Latest;
        return ageGroup switch
        {
            AgeGroup.Child => s.healthChild,
            AgeGroup.Teen  => s.healthTeen,
            AgeGroup.Adult => s.healthAdult,
            AgeGroup.Elder => s.healthElder,
            _ => 0f
        };
    }

    /// Force creating a snapshot right now (e.g., after big changes).
    public void ForceSnapshot() => RecordSnapshotForCurrentTurn();

    // ---------- Internal compute / snapshot ----------

    private void RecomputeNow()
    {
        // No UI—just ensure Latest reflects current state if someone queries immediately.
        // We don’t push to history here; history is per-turn (via OnTurnEnded) or ForceSnapshot.
        _ = BuildSnapshotFromManager(TurnSystem.GetCurrentTurn());
    }

    private void RecordSnapshotForCurrentTurn()
    {
        var s = BuildSnapshotFromManager(TurnSystem.GetCurrentTurn());

        // If last snapshot is same turn, replace it (idempotent per turn)
        if (history.Count > 0 && history[^1].turn == s.turn)
            history[^1] = s;
        else
            history.Add(s);

        // Trim
        while (history.Count > historyLimit)
            history.RemoveAt(0);

        OnSnapshotRecorded?.Invoke(s);
    }

    private PopulationSnapshot BuildSnapshotFromManager(int turn)
    {
        var s = new PopulationSnapshot { turn = turn };
        if (populationManager == null)
            return s;

        var pops = populationManager.AllPopulations;
        s.total  = pops.Sum(g => g.count);

        s.child  = Sum(pops, AgeGroup.Child);
        s.teen   = Sum(pops, AgeGroup.Teen);
        s.adult  = Sum(pops, AgeGroup.Adult);
        s.elder  = Sum(pops, AgeGroup.Elder);

        s.male   = pops.Where(g => g.gender == Gender.Male).Sum(g => g.count);
        s.female = pops.Where(g => g.gender == Gender.Female).Sum(g => g.count);

        s.healthChild = WeightedHealth(pops, AgeGroup.Child);
        s.healthTeen  = WeightedHealth(pops, AgeGroup.Teen);
        s.healthAdult = WeightedHealth(pops, AgeGroup.Adult);
        s.healthElder = WeightedHealth(pops, AgeGroup.Elder);

        return s;
    }

    private int Sum(List<PopulationGroup> pops, AgeGroup ag)
        => pops.Where(g => g.ageGroup == ag).Sum(g => g.count);

    private float WeightedHealth(List<PopulationGroup> pops, AgeGroup ag)
    {
        int total = 0;
        float weighted = 0f;
        foreach (var g in pops)
        {
            if (g.ageGroup != ag) continue;
            total += g.count;
            weighted += g.averageHealth * g.count; // averageHealth in 0..1
        }
        return total > 0 ? weighted / total : 0f;
    }

    public PlayerPopulationStatisticSaveData SaveState()
    {
        return new PlayerPopulationStatisticSaveData
        {
            historyLimit = Mathf.Max(1, historyLimit),
            history = new List<PopulationSnapshot>(history)
        };
    }

    public void LoadState(PlayerPopulationStatisticSaveData data)
    {
        if (data == null)
            return;

        historyLimit = Mathf.Max(1, data.historyLimit);

        history.Clear();
        if (data.history != null)
            history.AddRange(data.history);

        if (populationManager == null)
            populationManager = PlayersPopulationManager.Instance;

        if (history.Count == 0)
        {
            ForceSnapshot();
        }
        else
        {
            OnSnapshotRecorded?.Invoke(history[^1]);
        }
    }
}

[Serializable]
public struct PopulationSnapshot
{
    public int turn;
    public int total;

    // counts
    public int child, teen, adult, elder;
    public int male, female;

    // average health (0..1) by age group (weighted by headcount)
    public float healthChild, healthTeen, healthAdult, healthElder;
}

[Serializable]
public struct AgeRatios
{
    public float child, teen, adult, elder; // 0..1
}

[Serializable]
public struct GenderRatios
{
    public float male, female; // 0..1
}