using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PlayerGatheringManager : MonoBehaviour
{
    public static PlayerGatheringManager Instance { get; private set; }

    [Header("Dependencies")]
    public PlayersPopulationManager populationManager;
    [Tooltip("If left empty, will use PlayerKnownResourcesManager.Instance at runtime.")]
    public PlayerKnownResourcesManager knownManager;

    [Header("Carry Settings")]
    [Tooltip("How many SPACE units (weight*size) 1 population can carry in a single gather task.")]
    public float carrySpacePerPopulation = 12f;

    [Tooltip("Max number of distinct resource types chosen per task.")]
    [Min(1)] public int maxVarietyPerGather = 3;

    [Header("Performance")]
    [Tooltip("How many in-progress gatherings to advance per frame at end of turn.")]
    [Min(1)] public int maxGatheringsPerFrame = 10;

    [Header("Gathering Bias (Selection + Amount)")]
    [Tooltip("Always biases gathering toward Food even when hunger is low.")]
    public float baseFoodBias = 1.35f;

    [Tooltip("Always biases gathering toward Water even when thirst is low.")]
    public float baseWaterBias = 1.35f;

    [Tooltip("Penalizes Material so it loses out to Food/Water more often.")]
    public float materialBias = 0.65f;

    [Tooltip("Fallback for any other resource types.")]
    public float otherBias = 1f;

    // Optional: if you want at least one of each when available
    public bool alwaysIncludeFoodIfAvailable = false;
    public bool alwaysIncludeWaterIfAvailable = false;

    // Events
    public event Action<EnvironmentControl> OnGatheringStarted;
    public event Action<EnvironmentControl> OnGatheringFailed;
    public event Action<EnvironmentControl, List<(ResourceDefinition def, int amount)>> OnGatheringCompleted;
    public event Action<EnvironmentControl, int> OnGatheringFailedDetailed;

    // Internals
    private readonly List<EnvironmentControl> allEnvironments = new();
    private readonly Dictionary<EnvironmentControl, GatheringInfo> inProgress = new();

    [Serializable]
    private class GatheringInfo
    {
        public EnvironmentControl env;
        public int turnsCompleted;
        public float effectiveFailureChance;
        public int effectiveTurnsRequired;
        public int originalTurnsRequired;
        public int requiredPopulation;
        public string reservationId;
        public int reservedPopulation;

        public List<string> reservedIndividualIds = new();
    }

    // Inspector mirrors (debug)
    [Header("Debug (Read Only)")]
    [SerializeField] private List<EnvironmentControl> allEnvironmentsInspector = new();
    [SerializeField] private List<GatheringInfoInspector> inProgressInspector = new();

    [Serializable]
    private class GatheringInfoInspector
    {
        public EnvironmentControl env;
        public int turnsCompleted;
        public string reservationId;
        public int reservedPopulation;
        public float effectiveFailureChance;
        public int effectiveTurnsRequired;
    }

    private Coroutine processingCoroutine;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        if (!knownManager) knownManager = PlayerKnownResourcesManager.Instance;
    }

    private void OnEnable()
    {
        TurnSystem.SubscribeToEndOfTurn(OnTurnEnded);
    }

    private void OnDisable()
    {
        TurnSystem.UnsubscribeFromEndOfTurn(OnTurnEnded);
    }

    private void MarkJobsDirty()
    {
        SaveSystem.MarkSectionDirty(SaveSectionKeys.Jobs);
        SaveSystem.MarkSectionDirty(SaveSectionKeys.WorldObjects);
    }

    private void LateUpdate()
    {
        allEnvironmentsInspector.Clear();
        allEnvironmentsInspector.AddRange(allEnvironments);

        inProgressInspector.Clear();
        foreach (var kv in inProgress)
        {
            var info = kv.Value;
            inProgressInspector.Add(new GatheringInfoInspector
            {
                env = info.env,
                turnsCompleted = info.turnsCompleted,
                reservationId = info.reservationId,
                reservedPopulation = info.reservedPopulation,
                effectiveFailureChance = info.effectiveFailureChance,
                effectiveTurnsRequired = info.effectiveTurnsRequired
            });
        }
    }

    // -------- Registration / Queries --------

    public void RegisterEnvironment(EnvironmentControl env)
    {
        if (env == null) return;
        if (!allEnvironments.Contains(env))
            allEnvironments.Add(env);
    }

    public IReadOnlyList<EnvironmentControl> GetGatherable()
        => allEnvironments.Where(CanGatherFrom).ToList();

    private bool CanGatherFrom(EnvironmentControl env)
    {
        if (env == null) return false;
        if (!env.canExplore) return false;
        if (!env.IsDiscovered) return false;
        if (env.isGathering) return false;

        var node = env.GetComponent<EnvironmentResourceNode>();
        if (node == null || node.SpawnedResources == null || node.SpawnedResources.Count == 0)
            return false;

        var km = knownManager ? knownManager : PlayerKnownResourcesManager.Instance;

        // Must have at least one KNOWN resource with amount > 0
        return node.SpawnedResources.Any(e =>
            e != null && e.definition != null && e.amount > 0 &&
            (km == null || km.IsKnown(e.definition)));
    }

    // -------- Task Lifecycle --------

    public bool StartGathering(EnvironmentControl env, int populationToReserve)
    {
        if (env == null)
        {
            Debug.Log("[GatherMgr] Blocked: env is null");
            return false;
        }

        if (!env.canExplore)
        {
            Debug.Log($"[GatherMgr] Blocked on {env.name}: canExplore == false");
            return false;
        }

        if (!env.IsDiscovered)
        {
            Debug.Log($"[GatherMgr] Blocked on {env.name}: IsDiscovered == false");
            return false;
        }

        if (env.isGathering)
        {
            Debug.Log($"[GatherMgr] Blocked on {env.name}: already gathering");
            return false;
        }

        if (inProgress.ContainsKey(env))
        {
            Debug.Log($"[GatherMgr] Blocked on {env.name}: already inProgress dictionary");
            return false;
        }

        if (populationManager == null)
        {
            Debug.Log("[GatherMgr] Blocked: populationManager is null");
            return false;
        }

        var node = env.GetComponent<EnvironmentResourceNode>();
        if (node == null)
        {
            Debug.Log($"[GatherMgr] Blocked on {env.name}: no EnvironmentResourceNode");
            return false;
        }

        if (node.SpawnedResources == null || node.SpawnedResources.Count == 0)
        {
            Debug.Log($"[GatherMgr] Blocked on {env.name}: no spawned resources");
            return false;
        }

        var km = knownManager ? knownManager : PlayerKnownResourcesManager.Instance;
        bool hasKnownResource = node.SpawnedResources.Any(e =>
            e != null && e.definition != null && e.amount > 0 &&
            (km == null || km.IsKnown(e.definition)));

        if (!hasKnownResource)
        {
            Debug.Log($"[GatherMgr] Blocked on {env.name}: no KNOWN resources with amount > 0");
            return false;
        }

        int requiredPop = Mathf.Max(1, env.requireGatheringPopulation);

        var buffs = PlayerTechBuffs.Instance;
        if (buffs != null)
            requiredPop = buffs.GetGatheringRequiredPopEffective(env, requiredPop);

        requiredPop = Mathf.Max(1, requiredPop);

        int available = populationManager.GetAvailableTaskPopulation();
        Debug.Log($"[GatherMgr] {env.name}: trying to reserve {requiredPop}, availableTextShows={available}");

        if (!populationManager.TryPickRandomNonBusyTaskIndividuals(
                requiredPop, out var picked, out var reservationId))
        {
            Debug.Log($"[GatherMgr] Blocked on {env.name}: TryPickRandomNonBusyTaskIndividuals failed for required={requiredPop}");
            return false;
        }

        Debug.Log($"[GatherMgr] {env.name}: reserved {picked.Count} workers, reservation={reservationId}");

        PlayersPopulationManager.Instance?.ForceSyncUI();

        env.GetEffectiveGathering(out int effectiveTurns, out float effectiveFail);
        effectiveTurns = Mathf.Max(1, effectiveTurns);

        var info = new GatheringInfo
        {
            env = env,
            turnsCompleted = 0,
            reservationId = reservationId,
            reservedPopulation = picked.Count,
            requiredPopulation = requiredPop,
            effectiveFailureChance = effectiveFail,
            effectiveTurnsRequired = effectiveTurns,
            reservedIndividualIds = ExtractIndividualIds(picked)
        };
        inProgress[env] = info;

        env.isGathering = true;
        env.gatheringTurnsRequired = effectiveTurns;
        env.gatheringTurnsLeft = effectiveTurns;
        env.BeginGatheringVisuals();

        OnGatheringStarted?.Invoke(env);

        MarkJobsDirty();
        return true;
    }

    public void CancelGathering(EnvironmentControl env)
    {
        if (env == null) return;
        if (!inProgress.TryGetValue(env, out var info)) return;

        env.isGathering = false;
        env.gatheringTurnsLeft = info.effectiveTurnsRequired;
        env.FailGatheringVisuals();

        // Restore original
        env.gatheringTurnsRequired = env.BaseGatheringTurnsRequired;

        if (!string.IsNullOrEmpty(info.reservationId))
            populationManager.ReleaseReservation(info.reservationId);

        PlayersPopulationManager.Instance?.ForceSyncUI();

        inProgress.Remove(env);
        MarkJobsDirty();
        OnGatheringFailed?.Invoke(env);
    }

    private void OnTurnEnded()
    {
        var snapshot = inProgress.Values.ToList();
        if (processingCoroutine != null)
            StopCoroutine(processingCoroutine);
        processingCoroutine = StartCoroutine(ProcessGatheringUpdates(snapshot));
        MarkJobsDirty();
    }

    private IEnumerator ProcessGatheringUpdates(List<GatheringInfo> pending)
    {
        var toRemove = new List<EnvironmentControl>();

        int idx = 0;
        while (idx < pending.Count)
        {
            int end = Mathf.Min(idx + maxGatheringsPerFrame, pending.Count);
            for (int i = idx; i < end; i++)
            {
                var info = pending[i];
                var env = info.env;
                if (!env.isGathering) continue;

                // progress
                info.turnsCompleted++;
                env.gatheringTurnsLeft = Mathf.Max(0, env.gatheringTurnsLeft - 1);

                DiseaseManager.Instance?.TrySpreadContagiousVirusesWithinGroup(
                    info.reservedIndividualIds,
                    "Gathering",
                    env != null ? env.name : null,
                    1f);
                env.UpdateGatheringTimerUI();
                MarkJobsDirty();

                // failure roll shrinks over time
                float currentFailureChance = info.effectiveFailureChance;

                float baseAdjustedFailure = currentFailureChance / (info.turnsCompleted + 1);

                float diseaseFailureAdd = DiseaseManager.Instance != null
                    ? DiseaseManager.Instance.GetTaskFailureChanceAddPercentForIndividuals(
                        info.reservedIndividualIds,
                        "Gathering",
                        env != null ? env.name : null)
                    : 0f;

                float adjustedFailure = Mathf.Clamp(baseAdjustedFailure + diseaseFailureAdd, 0f, 100f);

                if (UnityEngine.Random.value <= adjustedFailure / 100f)
                {
                    int lost = 0;

                    if (!string.IsNullOrEmpty(info.reservationId))
                    {
                        float penaltyChance = currentFailureChance / 100f;

                        int effectivePenalty = Mathf.Max(0, env.GatheringPopPenaltyOnFailure);
                        var buffs = PlayerTechBuffs.Instance;
                        if (buffs != null)
                            effectivePenalty = buffs.GetGatheringPenaltyEffective(env, effectivePenalty);

                        if (effectivePenalty > 0 && UnityEngine.Random.value <= penaltyChance)
                        {
                            lost = populationManager.ApplyPenaltyFromReservation(
                                info.reservationId,
                                effectivePenalty
                            );
                        }

                        DiseaseManager.Instance?.TryApplyEnvironmentalDiseaseRiskForTaskResult(
                            env,
                            info.reservationId,
                            DiseaseTaskResultType.GatheringFailure);

                        populationManager.ReleaseReservation(info.reservationId);
                    }

                    MarkJobsDirty();
                    env.FailGatheringVisuals(lost);
                    env.isGathering = false;

                    // Restore original
                    env.gatheringTurnsRequired = env.BaseGatheringTurnsRequired;

                    CivilizationHappinessSystem.Instance?.NotifyTaskResult(success: false, weight: 2f);

                    OnGatheringFailedDetailed?.Invoke(env, lost);
                    OnGatheringFailed?.Invoke(env);

                    toRemove.Add(env);
                    continue;
                }

                if (env.gatheringTurnsLeft <= 0)
                {
                    // Complete: collect KNOWN ONLY
                    var loot = ExecuteCollection(env, info.reservedPopulation);
                    env.StorePendingLoot(loot);

                    if (!string.IsNullOrEmpty(info.reservationId))
                    {
                        DiseaseManager.Instance?.TryApplyEnvironmentalDiseaseRiskForTaskResult(
                            env,
                            info.reservationId,
                            DiseaseTaskResultType.GatheringSuccess);

                        populationManager.ReleaseReservation(info.reservationId);
                    }

                    PlayersPopulationManager.Instance?.ForceSyncUI();

                    int xp = EnvironmentXPCalculator.CalculateGatheringXPFromFailChance(
                        env.environmentType,
                        env.environmentTileType,
                        info.requiredPopulation,
                        info.effectiveFailureChance
                    );

                    if (xp > 0)
                        PlayerLevel.Instance?.AddXP(xp);

                    env.CompleteGatheringVisuals();
                    env.isGathering = false;

                    MarkJobsDirty();

                    // Restore original
                    env.gatheringTurnsRequired = env.BaseGatheringTurnsRequired;

                    CivilizationHappinessSystem.Instance?.NotifyTaskResult(success: true, weight: 1f);

                    OnGatheringCompleted?.Invoke(env, loot);
                    toRemove.Add(env);
                }
            }

            foreach (var env in toRemove)
                inProgress.Remove(env);
            toRemove.Clear();

            idx = end;
            yield return null;
        }

        processingCoroutine = null;
    }

    // -------- Loot Logic (KNOWN-ONLY filter applied) --------
    private List<(ResourceDefinition def, int amount)> ExecuteCollection(EnvironmentControl env, int reservedPop)
    {
        var results = new List<(ResourceDefinition, int)>();

        var node = env.GetComponent<EnvironmentResourceNode>();
        if (node == null || node.SpawnedResources == null || node.SpawnedResources.Count == 0)
            return results;

        var km = knownManager ? knownManager : PlayerKnownResourcesManager.Instance;

        // Only consider KNOWN resources with amount > 0
        var available = node.SpawnedResources
            .Where(e => e != null && e.definition != null && e.amount > 0 && (km == null || km.IsKnown(e.definition)))
            .ToList();

        if (available.Count == 0) return results;

        (float avgHunger, float avgThirst) = GetAverageNeeds();
        float foodNeedBoost = 1f + 2f * Mathf.Clamp01(avgHunger);
        float waterNeedBoost = 1f + 2f * Mathf.Clamp01(avgThirst);

        float WeightFor(ResourceType rt)
        {
            switch (rt)
            {
                case ResourceType.Food: return baseFoodBias * foodNeedBoost;
                case ResourceType.Water: return baseWaterBias * waterNeedBoost;
                case ResourceType.Material: return materialBias;
                default: return otherBias;
            }
        }

        int varietyMax = Mathf.Min(maxVarietyPerGather, available.Count);
        int variety = UnityEngine.Random.Range(1, varietyMax + 1);

        var chosen = WeightedSample(available, e => WeightFor(e.definition.resourceType), variety);

        // Optional “always include” swaps (independent of hunger/thirst)
        if (alwaysIncludeFoodIfAvailable && available.Any(a => a.definition.resourceType == ResourceType.Food)
            && !chosen.Any(c => c.definition.resourceType == ResourceType.Food))
        {
            var foodPool = available.Where(a => a.definition.resourceType == ResourceType.Food && !chosen.Contains(a)).ToList();
            if (foodPool.Count > 0)
            {
                var swapIn = foodPool[UnityEngine.Random.Range(0, foodPool.Count)];
                int idx = chosen.FindIndex(c => c.definition.resourceType == ResourceType.Material);
                if (idx < 0) idx = chosen.Count - 1;
                chosen[idx] = swapIn;
            }
        }

        if (alwaysIncludeWaterIfAvailable && available.Any(a => a.definition.resourceType == ResourceType.Water)
            && !chosen.Any(c => c.definition.resourceType == ResourceType.Water))
        {
            var waterPool = available.Where(a => a.definition.resourceType == ResourceType.Water && !chosen.Contains(a)).ToList();
            if (waterPool.Count > 0)
            {
                var swapIn = waterPool[UnityEngine.Random.Range(0, waterPool.Count)];
                int idx = chosen.FindIndex(c => c.definition.resourceType == ResourceType.Material);
                if (idx < 0) idx = chosen.Count - 1;
                chosen[idx] = swapIn;
            }
        }

        float carrySpace = Mathf.Max(0f, carrySpacePerPopulation * Mathf.Max(1, reservedPop));

        var weights = RandomWeights(chosen.Count);
        for (int i = 0; i < chosen.Count; i++)
        {
            weights[i] *= WeightFor(chosen[i].definition.resourceType);
        }
        float sumW = weights.Sum();
        if (sumW <= 1e-6f) { for (int i = 0; i < weights.Length; i++) weights[i] = 1f / chosen.Count; }
        else { for (int i = 0; i < weights.Length; i++) weights[i] /= sumW; }

        for (int i = 0; i < chosen.Count; i++)
        {
            var entry = chosen[i];
            var def = entry.definition;
            float unitSpace = Mathf.Max(0.0001f, def.weightPerUnit * def.sizePerUnit);

            float targetSpace = carrySpace * weights[i];
            int desiredUnits = Mathf.Max(0, Mathf.FloorToInt(targetSpace / unitSpace));
            if (desiredUnits <= 0) desiredUnits = (targetSpace > 0f ? 1 : 0);

            int amount = Mathf.Min(desiredUnits, entry.amount);
            if (amount <= 0) continue;

            int taken = node.Consume(entry, amount);
            if (taken > 0) results.Add((def, taken));
        }

        CleanupZeroEntries(node);
        return results;
    }

    // --- helpers (unchanged below this line) ---
    private (float hungerAvg, float thirstAvg) GetAverageNeeds()
    {
        if (populationManager == null || populationManager.AllPopulations == null || populationManager.AllPopulations.Count == 0)
            return (0f, 0f);

        int total = populationManager.AllPopulations.Sum(g => Mathf.Max(0, g.count));
        if (total <= 0) return (0f, 0f);

        float hungerSum = 0f, thirstSum = 0f;
        foreach (var g in populationManager.AllPopulations)
        {
            int c = Mathf.Max(0, g.count);
            hungerSum += g.hungerLevel * c;
            thirstSum += g.thirstLevel * c;
        }
        return (hungerSum / total, thirstSum / total);
    }

    private static List<ResourceSpawnEntry> WeightedSample(List<ResourceSpawnEntry> source, Func<ResourceSpawnEntry, float> weightFn, int n)
    {
        n = Mathf.Clamp(n, 0, source.Count);
        var pool = new List<ResourceSpawnEntry>(source);
        var picked = new List<ResourceSpawnEntry>(n);

        for (int k = 0; k < n && pool.Count > 0; k++)
        {
            float sum = 0f;
            for (int i = 0; i < pool.Count; i++)
                sum += Mathf.Max(0f, weightFn(pool[i]));

            if (sum <= 1e-6f)
            {
                int idx = UnityEngine.Random.Range(0, pool.Count);
                picked.Add(pool[idx]);
                pool.RemoveAt(idx);
                continue;
            }

            float r = UnityEngine.Random.Range(0f, sum);
            float acc = 0f;
            int pickIdx = 0;
            for (; pickIdx < pool.Count; pickIdx++)
            {
                acc += Mathf.Max(0f, weightFn(pool[pickIdx]));
                if (r <= acc) break;
            }
            pickIdx = Mathf.Clamp(pickIdx, 0, pool.Count - 1);
            picked.Add(pool[pickIdx]);
            pool.RemoveAt(pickIdx);
        }
        return picked;
    }

    private void CleanupZeroEntries(EnvironmentResourceNode node)
    {
        var listField = node.GetType().GetField("spawnedResources", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (listField != null)
        {
            var list = listField.GetValue(node) as IList<ResourceSpawnEntry>;
            if (list != null)
            {
                for (int i = list.Count - 1; i >= 0; i--)
                    if (list[i].amount <= 0) list.RemoveAt(i);
            }
        }
    }

    private static float[] RandomWeights(int count)
    {
        if (count <= 0) return Array.Empty<float>();
        var vals = new float[count];
        float sum = 0f;
        for (int i = 0; i < count; i++)
        {
            vals[i] = UnityEngine.Random.Range(0.4f, 1.6f);
            sum += vals[i];
        }
        if (sum <= 0f) { for (int i = 0; i < count; i++) vals[i] = 1f / count; return vals; }
        for (int i = 0; i < count; i++) vals[i] /= sum;
        return vals;
    }

    public PlayerGatheringSaveData SaveState()
    {
        PlayerGatheringSaveData data = new PlayerGatheringSaveData();

        foreach (var kv in inProgress)
        {
            GatheringInfo info = kv.Value;
            if (info == null || info.env == null || string.IsNullOrWhiteSpace(info.env.EnvironmentID))
                continue;

            data.activeGatherings.Add(new ActiveGatheringSaveData
            {
                environmentID = info.env.EnvironmentID,
                turnsCompleted = info.turnsCompleted,
                effectiveFailureChance = info.effectiveFailureChance,
                effectiveTurnsRequired = info.effectiveTurnsRequired,
                originalTurnsRequired = info.originalTurnsRequired,
                requiredPopulation = info.requiredPopulation,
                reservationId = info.reservationId,
                reservedPopulation = info.reservedPopulation
            });
        }

        return data;
    }

    public void LoadState(PlayerGatheringSaveData data)
    {
        if (processingCoroutine != null)
        {
            StopCoroutine(processingCoroutine);
            processingCoroutine = null;
        }

        if (!knownManager)
            knownManager = PlayerKnownResourcesManager.Instance;

        allEnvironments.Clear();
        inProgress.Clear();

        EnvironmentControl[] envs = FindObjectsOfType<EnvironmentControl>(true);
        Dictionary<string, EnvironmentControl> envById = new Dictionary<string, EnvironmentControl>(StringComparer.Ordinal);

        for (int i = 0; i < envs.Length; i++)
        {
            EnvironmentControl env = envs[i];
            if (env == null)
                continue;

            RegisterEnvironment(env);

            if (!string.IsNullOrWhiteSpace(env.EnvironmentID) && !envById.ContainsKey(env.EnvironmentID))
                envById.Add(env.EnvironmentID, env);
        }

        if (data == null || data.activeGatherings == null)
            return;

        for (int i = 0; i < data.activeGatherings.Count; i++)
        {
            ActiveGatheringSaveData saved = data.activeGatherings[i];
            if (saved == null || string.IsNullOrWhiteSpace(saved.environmentID))
                continue;

            if (!envById.TryGetValue(saved.environmentID, out EnvironmentControl env) || env == null)
            {
                Debug.LogWarning($"[PlayerGatheringManager] Could not resolve active gathering environment '{saved.environmentID}' while loading.");
                continue;
            }

            GatheringInfo info = new GatheringInfo
            {
                env = env,
                turnsCompleted = Mathf.Max(0, saved.turnsCompleted),
                effectiveFailureChance = Mathf.Clamp(saved.effectiveFailureChance, 0f, 100f),
                effectiveTurnsRequired = Mathf.Max(1, saved.effectiveTurnsRequired),
                originalTurnsRequired = Mathf.Max(1, saved.originalTurnsRequired),
                requiredPopulation = Mathf.Max(1, saved.requiredPopulation),
                reservationId = saved.reservationId,
                reservedPopulation = Mathf.Max(0, saved.reservedPopulation)
            };

            inProgress[env] = info;

            env.isGathering = true;
            env.gatheringTurnsRequired = info.effectiveTurnsRequired;

            int derivedTurnsLeft = Mathf.Clamp(
                info.effectiveTurnsRequired - info.turnsCompleted,
                0,
                info.effectiveTurnsRequired
            );

            env.gatheringTurnsLeft = derivedTurnsLeft;
            env.UpdateGatheringTimerUI();
            env.RebuildRuntimeUIState();
        }

        LateUpdate();
        MarkJobsDirty();
    }

    private static List<string> ExtractIndividualIds(List<Individual> picked)
    {
        List<string> ids = new List<string>();

        if (picked == null)
            return ids;

        for (int i = 0; i < picked.Count; i++)
        {
            Individual person = picked[i];

            if (person == null)
                continue;

            if (string.IsNullOrWhiteSpace(person.Id))
                continue;

            ids.Add(person.Id);
        }

        return ids;
    }
}