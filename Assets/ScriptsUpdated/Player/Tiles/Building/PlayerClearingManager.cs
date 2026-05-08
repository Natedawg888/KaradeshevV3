using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerClearingManager : MonoBehaviour
{
    public static PlayerClearingManager Instance { get; private set; }

    [Header("Dependencies")]
    [SerializeField] private PlayersPopulationManager populationManager;
    [SerializeField] private PlayerInventoryManager inventoryManager;

    [Header("Performance")]
    [Min(1)] public int maxClearsPerFrame = 100;

    private readonly HashSet<ManualClearJob> active = new();
    private readonly Dictionary<ManualClearJob, string> reservationByJob = new();
    private readonly Dictionary<ManualClearJob, List<ResourceAmount>> rewardsByJob = new();

    private Coroutine processingCoroutine;
    private bool _subscribedToTurnEnd;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        if (populationManager == null)
            populationManager = PlayersPopulationManager.Instance;
    }

    private void OnEnable()
    {
        if (_subscribedToTurnEnd)
            return;

        TurnSystem.SubscribeToEndOfTurn(OnTurnEnded);
        _subscribedToTurnEnd = true;
    }

    private void OnDisable()
    {
        if (!_subscribedToTurnEnd)
            return;

        TurnSystem.UnsubscribeFromEndOfTurn(OnTurnEnded);
        _subscribedToTurnEnd = false;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (_subscribedToTurnEnd)
        {
            TurnSystem.UnsubscribeFromEndOfTurn(OnTurnEnded);
            _subscribedToTurnEnd = false;
        }
    }

    private string GetReservationOwnerId(ManualClearJob job)
    {
        if (job == null)
            return null;

        Saveable saveable = job.GetComponent<Saveable>();
        if (saveable == null)
            saveable = job.GetComponentInParent<Saveable>();

        if (saveable != null && !string.IsNullOrWhiteSpace(saveable.uniqueID))
            return saveable.uniqueID;

        return job.gameObject.GetInstanceID().ToString();
    }

    private void TagClearingReservation(ManualClearJob job, string reservationId)
    {
        if (populationManager == null || job == null || string.IsNullOrWhiteSpace(reservationId))
            return;

        populationManager.UpdateReservationMetadata(
            reservationId,
            PopulationReservationKind.Clearing,
            GetReservationOwnerId(job),
            nameof(ManualClearJob));
    }

    /// <summary>
    /// Starts a manual clear on a destroyed building.
    /// Handles: afford check, spend, reserve population, initialize job, and queue it.
    /// </summary>
    public bool StartManualClear(BuildingControl target, Building def)
    {
        if (!target || def == null)
        {
            //Debug.LogError("[ClearingManager] StartManualClear: invalid args.");
            return false;
        }

        BuildingStatus status = target.GetComponent<BuildingStatus>();
        if (!status || status.CurrentState != BuildingState.Destroyed)
        {
            //Debug.LogWarning("[ClearingManager] Target not in Destroyed state.");
            return false;
        }

        ManualClearJob job = target.GetComponent<ManualClearJob>();
        if (!job)
        {
            //Debug.LogError("[ClearingManager] ManualClearJob missing on target.");
            return false;
        }

        if (job.IsActive)
        {
            //Debug.LogWarning("[ClearingManager] ManualClearJob already running on this building.");
            return false;
        }

        if (populationManager == null)
            populationManager = PlayersPopulationManager.Instance;

        // Spend costs
        if (!SpendCosts(def.manualClearCosts))
        {
            //Debug.LogWarning("[ClearingManager] Cannot afford manual clear costs.");
            return false;
        }

        // Reserve population
        string reservationId = null;
        int needPop = Mathf.Max(0, def.manualClearPopulation);

        if (needPop > 0)
        {
            if (populationManager == null)
            {
                //Debug.LogError("[ClearingManager] populationManager not assigned.");
                RefundCosts(def.manualClearCosts);
                return false;
            }

            if (!populationManager.TryReservePopulation(
                    needPop,
                    PopulationReservationKind.Clearing,
                    GetReservationOwnerId(job),
                    nameof(ManualClearJob),
                    out reservationId))
            {
                //Debug.LogWarning($"[ClearingManager] Could not reserve population (need {needPop}).");
                RefundCosts(def.manualClearCosts);
                return false;
            }
        }

        job.Initialize(def.manualClearTurns, def.manualClearRewards);
        job.Begin();

        active.Add(job);
        rewardsByJob[job] = def.manualClearRewards != null
            ? new List<ResourceAmount>(def.manualClearRewards)
            : new List<ResourceAmount>();

        if (!string.IsNullOrEmpty(reservationId))
        {
            reservationByJob[job] = reservationId;
            TagClearingReservation(job, reservationId);
        }

        return true;
    }

    private void OnTurnEnded()
    {
        List<ManualClearJob> snapshot = new List<ManualClearJob>(active.Count);
        foreach (ManualClearJob job in active)
            snapshot.Add(job);

        if (processingCoroutine != null)
            StopCoroutine(processingCoroutine);

        processingCoroutine = StartCoroutine(ProcessClears(snapshot));
    }

    private IEnumerator ProcessClears(List<ManualClearJob> pending)
    {
        int idx = 0;
        List<ManualClearJob> toRemove = new List<ManualClearJob>();

        while (idx < pending.Count)
        {
            int end = Mathf.Min(idx + maxClearsPerFrame, pending.Count);

            for (int i = idx; i < end; i++)
            {
                ManualClearJob job = pending[i];
                if (job == null || !job.IsActive)
                {
                    toRemove.Add(job);
                    continue;
                }

                bool completed = job.AdvanceOneTurn();
                if (completed)
                {
                    if (reservationByJob.TryGetValue(job, out string resId) && !string.IsNullOrEmpty(resId))
                        populationManager?.ReleaseReservation(resId);

                    reservationByJob.Remove(job);

                    GrantRewards(rewardsByJob.TryGetValue(job, out List<ResourceAmount> rw) ? rw : null);
                    rewardsByJob.Remove(job);

                    job.CompleteAndClear();

                    active.Remove(job);
                    toRemove.Add(job);
                }
            }

            idx = end;
            yield return null;
        }

        for (int i = 0; i < toRemove.Count; i++)
            active.Remove(toRemove[i]);

        processingCoroutine = null;
    }

    private PlayerInventoryManager Inv => inventoryManager;

    private bool SpendCosts(List<ResourceCost> costs)
    {
        if (costs == null || costs.Count == 0)
            return true;

        if (Inv == null)
        {
            //Debug.LogError("[ClearingManager] inventoryManager reference is null.");
            return false;
        }

        if (!InventoryQuery.CanAfford(costs))
            return false;

        List<ResourceCost> rollback = new List<ResourceCost>();

        foreach (ResourceCost c in costs)
        {
            if (c == null || c.resource == null || c.amount <= 0)
                continue;

            bool ok = c.resource.isGroup
                ? Inv.TryRemoveGroup(c.resource, c.amount)
                : Inv.TryRemove(c.resource, c.amount);

            if (!ok)
            {
                for (int r = 0; r < rollback.Count; r++)
                    Inv.TryAdd(rollback[r].resource, rollback[r].amount);

                //Debug.LogWarning("[ClearingManager] SpendCosts failed; rolled back.");
                return false;
            }

            if (!c.resource.isGroup)
                rollback.Add(c);
        }

        return true;
    }

    private void RefundCosts(List<ResourceCost> costs)
    {
        if (Inv == null || costs == null)
            return;

        foreach (ResourceCost c in costs)
        {
            if (c?.resource != null && c.amount > 0 && !c.resource.isGroup)
                Inv.TryAdd(c.resource, c.amount);
        }
    }

    private void GrantRewards(List<ResourceAmount> rewards)
    {
        if (Inv == null || rewards == null)
            return;

        for (int i = 0; i < rewards.Count; i++)
        {
            ResourceAmount r = rewards[i];
            if (r?.resource != null && r.amount > 0)
                Inv.TryAdd(r.resource, r.amount);
        }
    }

    public PlayerClearingSaveData SaveState()
    {
        PlayerClearingSaveData data = new PlayerClearingSaveData();

        foreach (ManualClearJob job in active)
        {
            if (job == null)
                continue;

            bool isActiveJob =
                SaveReflectionUtil.Get(job, "IsActive", false) ||
                SaveReflectionUtil.Get(job, "isActive", false);

            if (!isActiveJob)
                continue;

            Saveable saveable = job.GetComponent<Saveable>();
            if (saveable == null)
                saveable = job.GetComponentInParent<Saveable>();

            if (saveable == null || string.IsNullOrWhiteSpace(saveable.uniqueID))
                continue;

            int totalTurns = SaveReflectionUtil.Get(job, "turnsToComplete", 0);
            if (totalTurns <= 0)
                totalTurns = SaveReflectionUtil.Get(job, "totalTurns", 1);

            int turnsLeft = SaveReflectionUtil.Get(job, "turnsLeft", totalTurns);

            ActiveManualClearSaveData saved = new ActiveManualClearSaveData
            {
                buildingSaveableID = saveable.uniqueID,
                totalTurns = Mathf.Max(1, totalTurns),
                turnsLeft = Mathf.Clamp(turnsLeft, 0, Mathf.Max(1, totalTurns))
            };

            if (reservationByJob.TryGetValue(job, out string reservationId))
                saved.reservationId = reservationId;

            if (rewardsByJob.TryGetValue(job, out List<ResourceAmount> rewards) && rewards != null)
            {
                for (int i = 0; i < rewards.Count; i++)
                {
                    ResourceAmount r = rewards[i];
                    if (r == null || r.resource == null || r.amount <= 0)
                        continue;

                    saved.rewards.Add(new ManualClearRewardSaveData
                    {
                        resourceID = r.resource.resourceID,
                        amount = r.amount
                    });
                }
            }

            data.activeClears.Add(saved);
        }

        return data;
    }

    public void LoadState(PlayerClearingSaveData data)
    {
        if (processingCoroutine != null)
        {
            StopCoroutine(processingCoroutine);
            processingCoroutine = null;
        }

        if (populationManager == null)
            populationManager = PlayersPopulationManager.Instance;

        active.Clear();
        reservationByJob.Clear();
        rewardsByJob.Clear();

        if (data == null || data.activeClears == null || data.activeClears.Count == 0)
            return;

        Dictionary<string, ManualClearJob> jobsBySaveableId = BuildJobLookup();

        for (int i = 0; i < data.activeClears.Count; i++)
        {
            ActiveManualClearSaveData saved = data.activeClears[i];
            if (saved == null || string.IsNullOrWhiteSpace(saved.buildingSaveableID))
                continue;

            if (!jobsBySaveableId.TryGetValue(saved.buildingSaveableID, out ManualClearJob job) || job == null)
            {
                //Debug.LogWarning($"[ClearingManager] Could not resolve ManualClearJob for building saveable '{saved.buildingSaveableID}' while loading.");
                continue;
            }

            RestoreLoadedJob(job, saved);
        }
    }

    private Dictionary<string, ManualClearJob> BuildJobLookup()
    {
        Dictionary<string, ManualClearJob> map = new Dictionary<string, ManualClearJob>(StringComparer.Ordinal);

        ManualClearJob[] jobs = FindObjectsOfType<ManualClearJob>(true);
        for (int i = 0; i < jobs.Length; i++)
        {
            ManualClearJob job = jobs[i];
            if (job == null)
                continue;

            Saveable saveable = job.GetComponent<Saveable>();
            if (saveable == null)
                saveable = job.GetComponentInParent<Saveable>();

            if (saveable == null || string.IsNullOrWhiteSpace(saveable.uniqueID))
                continue;

            if (!map.ContainsKey(saveable.uniqueID))
                map.Add(saveable.uniqueID, job);
        }

        return map;
    }

    private void RestoreLoadedJob(ManualClearJob job, ActiveManualClearSaveData saved)
    {
        int totalTurns = Mathf.Max(1, saved.totalTurns);
        int turnsLeft = Mathf.Clamp(saved.turnsLeft, 0, totalTurns);

        List<ResourceAmount> rewards = ResolveRewards(saved.rewards);

        job.Initialize(totalTurns, rewards);
        job.Begin();

        bool wroteTurnsLeft = SaveReflectionUtil.Set(job, "turnsLeft", turnsLeft);
        if (!wroteTurnsLeft)
            SaveReflectionUtil.Set(job, "remainingTurns", turnsLeft);

        bool wroteTotalTurns = SaveReflectionUtil.Set(job, "turnsToComplete", totalTurns);
        if (!wroteTotalTurns)
            SaveReflectionUtil.Set(job, "totalTurns", totalTurns);

        SaveReflectionUtil.Set(job, "IsActive", true);
        SaveReflectionUtil.Set(job, "isActive", true);

        BuildingStatus status = job.GetComponent<BuildingStatus>();
        if (status == null)
            status = job.GetComponentInParent<BuildingStatus>();

        if (status != null && status.manualClearTimerUIRef != null)
        {
            status.manualClearTimerUIRef.gameObject.SetActive(true);
            status.manualClearTimerUIRef.Initialize(totalTurns);
            status.manualClearTimerUIRef.UpdateTimer(turnsLeft);
        }

        active.Add(job);

        if (!string.IsNullOrWhiteSpace(saved.reservationId))
        {
            reservationByJob[job] = saved.reservationId;
            TagClearingReservation(job, saved.reservationId);
        }

        rewardsByJob[job] = rewards ?? new List<ResourceAmount>();
    }

    private List<ResourceAmount> ResolveRewards(List<ManualClearRewardSaveData> savedRewards)
    {
        List<ResourceAmount> result = new List<ResourceAmount>();
        if (savedRewards == null)
            return result;

        for (int i = 0; i < savedRewards.Count; i++)
        {
            ManualClearRewardSaveData saved = savedRewards[i];
            if (saved == null || string.IsNullOrWhiteSpace(saved.resourceID) || saved.amount <= 0)
                continue;

            ResourceDefinition def = ResolveResource(saved.resourceID);
            if (def == null)
                continue;

            result.Add(new ResourceAmount
            {
                resource = def,
                amount = saved.amount
            });
        }

        return result;
    }

    private static Dictionary<string, ResourceDefinition> _resourceById;

    private static ResourceDefinition ResolveResource(string resourceID)
    {
        if (string.IsNullOrWhiteSpace(resourceID))
            return null;

        if (_resourceById == null)
        {
            _resourceById = new Dictionary<string, ResourceDefinition>(StringComparer.Ordinal);
            ResourceDefinition[] defs = Resources.LoadAll<ResourceDefinition>(string.Empty);

            for (int i = 0; i < defs.Length; i++)
            {
                ResourceDefinition def = defs[i];
                if (def == null || string.IsNullOrWhiteSpace(def.resourceID))
                    continue;

                string id = def.resourceID.Trim();
                if (!_resourceById.ContainsKey(id))
                    _resourceById.Add(id, def);
            }
        }

        _resourceById.TryGetValue(resourceID.Trim(), out ResourceDefinition result);
        return result;
    }
}
