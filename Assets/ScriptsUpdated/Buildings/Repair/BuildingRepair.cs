using System;
using System.Collections.Generic;
using UnityEngine;

public class BuildingRepair : MonoBehaviour, IBuildingTurnTickable
{
    [Header("Policy")]
    [Tooltip("If false, Destroyed buildings cannot be repaired via this component.")]
    public bool allowRepairWhenDestroyed = false;

    [Header("UI")]
    public GameObject repairIconRoot;
    public TimerUI repairTimerUI;

    [SerializeField] private int _reservedPopulationCount = 0;
    [SerializeField] private int _targetHealthAfterRepair = 0;

    private BuildingControl _control;
    private BuildingHealth  _health;
    private BuildingStatus  _status;

    // In-progress job state
    [SerializeField] private bool         _isRepairing = false;
    [SerializeField] private int          _turnsRemaining = 0;
    [SerializeField] private int          _turnsTotal = 0;
    [SerializeField] private RepairOption _activeOption;
    [SerializeField] private string       _reservationId;

    // gradual healing accumulator
    private float _totalHealPoints;   // total HP to heal this job
    private float _perTurnHeal;       // float per-turn HP
    private float _healAcc;           // carry precision, apply ceil ints

    public bool IsRepairing   => _isRepairing;
    public int  TurnsRemaining=> Mathf.Max(0, _turnsRemaining);

    public event Action<RepairOption,int> OnRepairStarted;   // (option, totalTurns)
    public event Action<int>              OnRepairProgress;  // turns left
    public event Action                   OnRepairCompleted;

    private void Awake()
    {
        _control = GetComponent<BuildingControl>();
        _health  = GetComponent<BuildingHealth>();
        _status  = GetComponent<BuildingStatus>();

        if (repairIconRoot) repairIconRoot.SetActive(false);
    }

    private void Start()
    {
        BuildingTickManager.Instance?.Register(this);
    }

    private void OnDestroy()
    {
        BuildingTickManager.Instance?.Unregister(this);
        TryReleaseReservation();
        if (_isRepairing) _health?.SetDegenerationPaused(false);
    }

    public void TurnTick()
    {
        if (!_isRepairing) return;

        _healAcc += _perTurnHeal;

        int stepHeal = Mathf.FloorToInt(_healAcc);
        int remainingToTarget = Mathf.Max(0, _targetHealthAfterRepair - _health.CurrentHealth);

        if (stepHeal > 0 && remainingToTarget > 0)
        {
            stepHeal = Mathf.Min(stepHeal, remainingToTarget);
            _healAcc -= stepHeal;
            _health.RepairAbsolute(stepHeal);
        }

        _turnsRemaining = Mathf.Max(0, _turnsRemaining - 1);
        repairTimerUI?.UpdateTimer(_turnsRemaining);
        OnRepairProgress?.Invoke(_turnsRemaining);

        if (_turnsRemaining <= 0)
        {
            int finalMissingToTarget = Mathf.Max(0, _targetHealthAfterRepair - _health.CurrentHealth);
            if (finalMissingToTarget > 0)
                _health.RepairAbsolute(finalMissingToTarget);

            FinishJob();
        }
    }

    // ===== External API =====

    public List<CalculatedCost> GetRepairCosts(RepairOption option)
        => CalculateCosts(option);

    public bool CanAfford(RepairOption option)
        => CanAfford(CalculateCosts(option));

    /// Returns scaled work requirements (turns, population) for a given tier.
    public (int turns, int population) GetScaledWork(RepairOption option)
    {
        var def = BuildingManager.Instance?.GetBuildingByID(_control.buildingID);
        if (def == null)
            return (1, 1);

        float mult = GetTierCostMultiplier(option); // 0.10, 0.50, 0.90

        int turns = Mathf.Max(1, Mathf.RoundToInt(def.buildTurnsRequired      * mult));
        int pop   = Mathf.Max(1, Mathf.RoundToInt(def.requireBuildPopulation  * mult));
        return (turns, pop);
    }

    /// Starts timed repair: spends resources, reserves population, heals gradually each turn.
    public bool TryRepair(RepairOption option)
    {
        if (_health == null) return false;
        if (_isRepairing) return false;

        if (!allowRepairWhenDestroyed && _status != null && _status.CurrentState == BuildingState.Destroyed)
        {
            //Debug.LogWarning("[BuildingRepair] Cannot repair: building is Destroyed.");
            return false;
        }

        if (_health.CurrentHealth >= _health.maxHealth)
            return false;

        var costs = CalculateCosts(option);
        if (!CanAfford(costs))
            return false;

        var (turns, pop) = GetScaledWork(option);
        turns = Mathf.Max(1, turns);
        pop = Mathf.Max(1, pop);

        if (!TryReservePopulation(pop, out _reservationId))
        {
            //Debug.LogWarning("[BuildingRepair] Not enough population available for repair.");
            return false;
        }

        _reservedPopulationCount = pop;
        PlayersPopulationManager.Instance?.ForceSyncUI();

        if (!Spend(costs))
        {
            TryReleaseReservation();
            return false;
        }

        _activeOption = option;
        _turnsTotal = turns;
        _turnsRemaining = turns;
        _isRepairing = true;

        float pct = GetRepairPercent(option);

        int currentHealth = _health.CurrentHealth;
        int missingHealth = Mathf.Max(0, _health.maxHealth - currentHealth);

        // heal amount is based on tier, but never exceeds what is actually missing
        int targetHealAmount = Mathf.Clamp(
            Mathf.CeilToInt(_health.maxHealth * pct),
            0,
            missingHealth
        );

        if (targetHealAmount <= 0)
        {
            TryReleaseReservation();
            _isRepairing = false;
            return false;
        }

        _totalHealPoints = targetHealAmount;
        _targetHealthAfterRepair = currentHealth + targetHealAmount;
        _perTurnHeal = _totalHealPoints / _turnsTotal;
        _healAcc = 0f;

        _health.SetDegenerationPaused(true);

        if (repairIconRoot) repairIconRoot.SetActive(true);

        if (repairTimerUI)
        {
            repairTimerUI.Initialize(_turnsTotal);
            repairTimerUI.UpdateTimer(_turnsRemaining);
        }

        OnRepairStarted?.Invoke(option, _turnsTotal);
        return true;
    }

    private static float GetRepairPercent(RepairOption option) => option switch
    {
        RepairOption.TenPercent => 0.10f,
        RepairOption.FiftyPercent => 0.50f,
        RepairOption.Full => 1.00f,
        _ => 0f
    };

    // ===== Internals =====

    private void FinishJob()
    {
        _isRepairing = false;
        _turnsRemaining = 0;
        _turnsTotal = 0;
        _totalHealPoints = 0f;
        _perTurnHeal = 0f;
        _healAcc = 0f;
        _targetHealthAfterRepair = 0;

        if (repairIconRoot) repairIconRoot.SetActive(false);

        TryReleaseReservation();
        _health?.SetDegenerationPaused(false);

        OnRepairCompleted?.Invoke();
        ScoreManager.NotifyBuildingRepaired();
    }

    private List<CalculatedCost> CalculateCosts(RepairOption option)
    {
        var outList = new List<CalculatedCost>();
        var def = BuildingManager.Instance?.GetBuildingByID(_control.buildingID);
        if (def == null || def.buildCosts == null || def.buildCosts.Count == 0)
            return outList;

        float mult = GetTierCostMultiplier(option); // 0.10, 0.50, 0.90
        for (int i = 0; i < def.buildCosts.Count; i++)
        {
            var line = def.buildCosts[i];
            if (line == null || line.resource == null) continue;

            int amt = Mathf.FloorToInt(Mathf.Max(0f, line.amount * mult));
            if (amt < 1) continue; // under-1 → none

            outList.Add(new CalculatedCost { resource = line.resource, amount = amt });
        }
        return outList;
    }

    private static float GetTierCostMultiplier(RepairOption option) => option switch
    {
        RepairOption.TenPercent   => 0.10f,
        RepairOption.FiftyPercent => 0.50f,
        RepairOption.Full         => 0.90f,
        _                         => 1f
    };

    // ---- Population reservation (reflection-friendly, same patterns as before) ----

    private bool TryReservePopulation(int count, out string reservationId)
    {
        reservationId = null;

        // Preferred: use the concrete unified API (picks exact workers and marks them busy)
        var popMgr = PlayersPopulationManager.Instance;
        if (popMgr != null)
        {
            if (popMgr.TryPickRandomNonBusyTaskIndividuals(count, out var picked, out reservationId)
                && !string.IsNullOrEmpty(reservationId))
            {
                // Workers are already marked busy and linked to this reservation.
                return true;
            }
            return false; // not enough available / couldn’t pick
        }

        var pop = typeof(PlayersPopulationManager).Assembly
                    ?.GetType("PlayersPopulationManager")?
                    .GetProperty("Instance")?
                    .GetValue(null);
        if (pop == null) return false;

        var t = pop.GetType();

        // TryPickRandomNonBusyTaskIndividuals(int, out List<Individual>, out string)
        var mPick = t.GetMethod("TryPickRandomNonBusyTaskIndividuals",
            new[] { typeof(int), typeof(List<Individual>).MakeByRefType(), typeof(string).MakeByRefType() });
        if (mPick != null)
        {
            object[] args = { count, null, null };
            bool ok = (bool)mPick.Invoke(pop, args);
            reservationId = (string)args[2];
            return ok && !string.IsNullOrEmpty(reservationId);
        }

        // Legacy fallbacks — reserve without picking/busying individuals
        var mTryReserve = t.GetMethod("TryReservePopulation", new[] { typeof(int), typeof(string).MakeByRefType() })
                        ?? t.GetMethod("TryReserve",           new[] { typeof(int), typeof(string).MakeByRefType() });
        if (mTryReserve != null)
        {
            object[] args = { count, null };
            bool ok = (bool)mTryReserve.Invoke(pop, args);
            reservationId = (string)args[1];
            return ok && !string.IsNullOrEmpty(reservationId);
        }

        var mReserve = t.GetMethod("Reserve", new[] { typeof(int) });
        if (mReserve != null)
        {
            var id = mReserve.Invoke(pop, new object[] { count }) as string;
            reservationId = id;
            return !string.IsNullOrEmpty(id);
        }

        var mReserveTask = t.GetMethod("ReserveTaskPopulation", new[] { typeof(int), typeof(string).MakeByRefType() });
        if (mReserveTask != null)
        {
            object[] args = { count, null };
            bool ok = (bool)mReserveTask.Invoke(pop, args);
            reservationId = (string)args[1];
            return ok && !string.IsNullOrEmpty(reservationId);
        }

        //Debug.LogWarning("[BuildingRepair] No compatible reservation API found.");
        return false;
    }

    private void TryReleaseReservation()
    {
        if (string.IsNullOrEmpty(_reservationId))
        {
            _reservedPopulationCount = 0;
            PlayersPopulationManager.Instance?.ForceSyncUI();
            return;
        }

        var popMgr = PlayersPopulationManager.Instance;
        if (popMgr != null)
        {
            popMgr.ReleaseReservation(_reservationId);
            _reservationId = null;
            _reservedPopulationCount = 0;
            PlayersPopulationManager.Instance?.ForceSyncUI();
            return;
        }

        var pop = typeof(PlayersPopulationManager).Assembly
                    ?.GetType("PlayersPopulationManager")?
                    .GetProperty("Instance")?
                    .GetValue(null);

        if (pop == null)
        {
            _reservationId = null;
            _reservedPopulationCount = 0;
            return;
        }

        var t = pop.GetType();
        var mRelease = t.GetMethod("ReleaseReservation", new[] { typeof(string) })
                    ?? t.GetMethod("FreeReservation", new[] { typeof(string) })
                    ?? t.GetMethod("Release", new[] { typeof(string) });

        if (mRelease != null)
            mRelease.Invoke(pop, new object[] { _reservationId });
        else {}
            //Debug.LogWarning("[BuildingRepair] Could not find a reservation release method.");

        _reservationId = null;
        _reservedPopulationCount = 0;
        PlayersPopulationManager.Instance?.ForceSyncUI();
    }

    // ---- Inventory helpers unchanged (CanAfford / Spend) ----
    private bool CanAfford(List<CalculatedCost> costs)
    {
        if (costs == null || costs.Count == 0) return true;

        foreach (var c in costs)
        {
            if (c.resource == null || c.amount <= 0) continue;
            if (InventoryQuery.GetOwned(c.resource) < c.amount)
                return false;
        }
        return true;
    }

    private bool Spend(List<CalculatedCost> costs)
    {
        if (costs == null || costs.Count == 0) return true;

        var inv = PlayerInventoryManager.Instance;
        if (inv == null)
        {
            //Debug.LogWarning("[BuildingRepair] No PlayerInventoryManager; cannot spend.");
            return false;
        }

        foreach (var c in costs)
        {
            if (c.resource == null || c.amount <= 0) continue;

            // TryRemove handles both group + normal resources
            if (!inv.TryRemove(c.resource, c.amount))
            {
                //Debug.LogWarning($"[BuildingRepair] Spend failed for {c.resource?.name} x{c.amount} (Owned={inv.GetAmount(c.resource)}).");
                return false;
            }
        }

        return true;
    }
}
