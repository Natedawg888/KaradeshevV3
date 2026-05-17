using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerDiscoveryManager : MonoBehaviour
{
    public static PlayerDiscoveryManager Instance { get; private set; }

    [Header("Dependencies")]
    public PlayersPopulationManager populationManager;

    [Header("Performance")]
    [Tooltip("How many in-progress discoveries to advance per frame when processing end-of-turn work.")]
    [SerializeField, Min(1)] private int maxDiscoveriesPerFrame = 10;

    // Events
    public event Action<EnvironmentControl> OnDiscoveryStarted;
    public event Action<EnvironmentControl> OnDiscoveryFailed;
    public event Action<EnvironmentControl> OnDiscoveryCompleted;
    public event Action<EnvironmentControl, int> OnDiscoveryFailedDetailed;

    // Internal tracking
    private readonly List<EnvironmentControl> allEnvironments = new();
    private readonly List<EnvironmentControl> discovered = new();
    private readonly Dictionary<EnvironmentControl, DiscoveryInfo> inProgress = new();

    // reusable temp buffers
    private readonly List<EnvironmentControl> _tmpUndiscovered = new();
    private readonly List<EnvironmentControl> _tmpInProgress = new();
    private readonly List<DiscoveryInfo> _turnSnapshot = new();

    [Serializable]
    private class DiscoveryInfo
    {
        public EnvironmentControl env;
        public int turnsCompleted;

        public float effectiveFailureChance;
        public int effectiveTurnsRequired;

        public int originalTurnsRequired;
        public int requiredPopulation;

        public string reservationId;

        public List<string> reservedIndividualIds = new();
    }

    // Inspector mirrors (optional)
    [Header("Debug (Read Only)")]
    [SerializeField] private List<EnvironmentControl> allEnvironmentsInspector = new();
    [SerializeField] private List<EnvironmentControl> discoveredInspector = new();
    [SerializeField] private List<DiscoveryInfoInspector> inProgressInspector = new();

    [Serializable]
    private class DiscoveryInfoInspector
    {
        public EnvironmentControl env;
        public int turnsCompleted;
        public float effectiveFailureChance;
        public int effectiveTurnsRequired;
        public string reservationId;
    }

    private Coroutine discoveryProcessingCoroutine;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this)
        {
            //Debug.LogWarning("Multiple PlayerDiscoveryManager instances detected; destroying duplicate.");
            Destroy(gameObject);
            return;
        }
    }

    private void OnEnable()
    {
        TurnSystem.SubscribeToEndOfTurn(OnTurnEnded);
    }

    private void OnDisable()
    {
        TurnSystem.UnsubscribeFromEndOfTurn(OnTurnEnded);
    }

    private void LateUpdate()
    {
        SyncInspectorLists();
    }

    private void MarkJobsDirty()
    {
        SaveSystem.MarkSectionDirty(SaveSectionKeys.Jobs);
    }

    private void MarkDiscoveryStateDirty()
    {
        SaveSystem.MarkSectionDirty(SaveSectionKeys.Jobs);
        SaveSystem.MarkSectionDirty(SaveSectionKeys.WorldObjects);
    }

    private void SyncInspectorLists()
    {
        allEnvironmentsInspector.Clear();
        allEnvironmentsInspector.AddRange(allEnvironments);

        discoveredInspector.Clear();
        discoveredInspector.AddRange(discovered);

        inProgressInspector.Clear();
        foreach (var kv in inProgress)
        {
            var info = kv.Value;
            inProgressInspector.Add(new DiscoveryInfoInspector
            {
                env = info.env,
                turnsCompleted = info.turnsCompleted,
                effectiveFailureChance = info.effectiveFailureChance,
                effectiveTurnsRequired = info.effectiveTurnsRequired,
                reservationId = info.reservationId
            });
        }
    }

    private string GetReservationOwnerId(EnvironmentControl env)
    {
        if (env == null)
            return null;

        if (!string.IsNullOrWhiteSpace(env.EnvironmentID))
            return env.EnvironmentID;

        return env.GetInstanceID().ToString();
    }

    public void RegisterEnvironment(EnvironmentControl env)
    {
        if (env == null) return;
        if (!allEnvironments.Contains(env))
        {
            allEnvironments.Add(env);
            if (env.IsDiscovered)
                MarkDiscoveredInternal(env);
        }
    }

    public IReadOnlyList<EnvironmentControl> GetUndiscovered()
    {
        _tmpUndiscovered.Clear();

        for (int i = 0; i < allEnvironments.Count; i++)
        {
            var env = allEnvironments[i];
            if (env == null)
                continue;

            if (env.IsDiscovered)
                continue;

            if (inProgress.ContainsKey(env))
                continue;

            _tmpUndiscovered.Add(env);
        }

        return _tmpUndiscovered;
    }

    public IReadOnlyList<EnvironmentControl> GetInProgress()
    {
        _tmpInProgress.Clear();

        foreach (var kv in inProgress)
        {
            if (kv.Key != null)
                _tmpInProgress.Add(kv.Key);
        }

        return _tmpInProgress;
    }

    public IReadOnlyList<EnvironmentControl> GetDiscovered()
        => discovered;

    private void SetTileInteractable(EnvironmentControl env, bool interactable)
    {
        if (env == null) return;

        var tileControl = env.GetComponentInParent<TileControl>();
        if (tileControl != null)
        {
            tileControl.isInteractable = interactable;
        }
        else
        {
            //Debug.LogWarning("PlayerDiscoveryManager: Could not find TileControl for env " + env.name);
        }
    }

    public bool StartDiscovery(EnvironmentControl env)
    {
        if (env == null) return false;
        if (!env.canExplore) return false;
        if (env.IsDiscovered || inProgress.ContainsKey(env)) return false;

        if (populationManager == null)
        {
            //Debug.LogWarning("PlayerDiscoveryManager: populationManager not assigned.");
            return false;
        }

        var status = env.GetComponent<EnvironmentStatus>();
        if (status == null || !status.isDiscoverable) return false;

        int requiredPop = Mathf.Max(1, env.requireDiscoveryPopulation);
        var buffs = PlayerTechBuffs.Instance;
        if (buffs != null)
            requiredPop = buffs.GetDiscoveryRequiredPopEffective(env, requiredPop);

        requiredPop = Mathf.Max(1, requiredPop);

        string ownerId = GetReservationOwnerId(env);

        if (!populationManager.TryPickRandomNonBusyTaskIndividuals(
                requiredPop,
                PopulationReservationKind.Discovery,
                ownerId,
                nameof(EnvironmentControl),
                out List<Individual> picked,
                out string reservationId))
        {
            return false;
        }

        PlayersPopulationManager.Instance?.ForceSyncUI();

        env.GetEffectiveDiscovery(out int effectiveTurns, out float effectiveFail);
        effectiveTurns = Mathf.Max(1, effectiveTurns);

        var info = new DiscoveryInfo
        {
            env = env,
            turnsCompleted = 0,
            effectiveFailureChance = effectiveFail,
            effectiveTurnsRequired = effectiveTurns,
            originalTurnsRequired = env.BaseDiscoveryTurnsRequired,
            requiredPopulation = requiredPop,
            reservationId = reservationId,
            reservedIndividualIds = ExtractIndividualIds(picked)
        };

        inProgress[env] = info;

        env.isBeingDiscovered = true;
        env.discoveryTurnsLeft = effectiveTurns;
        env.discoveryTurnsRequired = effectiveTurns;

        SetTileInteractable(env, false);

        env.BeginDiscoveryVisuals();
        OnDiscoveryStarted?.Invoke(env);
        MarkDiscoveryStateDirty();
        return true;
    }

    public void CancelDiscovery(EnvironmentControl env)
    {
        if (env == null) return;
        if (!inProgress.ContainsKey(env)) return;

        var info = inProgress[env];

        env.isBeingDiscovered = false;
        env.discoveryTurnsLeft = info.effectiveTurnsRequired;

        if (!string.IsNullOrEmpty(info.reservationId))
            populationManager.ReleaseReservation(info.reservationId);

        PlayersPopulationManager.Instance?.ForceSyncUI();

        env.discoveryTurnsRequired = env.BaseDiscoveryTurnsRequired;

        inProgress.Remove(env);

        SetTileInteractable(env, true);

        PostDiscoveryNotification(NotificationType.DiscoveryFailed, env, 0, "Discovery Failed",
            $"Discovery at {env.environmentName} was cancelled.");

        OnDiscoveryFailed?.Invoke(env);
        env.FailDiscoveryVisuals();

        MarkDiscoveryStateDirty();
    }

    private void OnTurnEnded()
    {
        _turnSnapshot.Clear();

        foreach (var kv in inProgress)
        {
            if (kv.Value != null)
                _turnSnapshot.Add(kv.Value);
        }

        if (discoveryProcessingCoroutine != null)
            StopCoroutine(discoveryProcessingCoroutine);

        discoveryProcessingCoroutine = StartCoroutine(ProcessDiscoveryUpdates(_turnSnapshot));
        MarkDiscoveryStateDirty();
    }

    private IEnumerator ProcessDiscoveryUpdates(List<DiscoveryInfo> pendingInfos)
    {
        var toRemove = new List<EnvironmentControl>();

        int idx = 0;
        while (idx < pendingInfos.Count)
        {
            int end = Mathf.Min(idx + maxDiscoveriesPerFrame, pendingInfos.Count);
            for (int i = idx; i < end; i++)
            {
                var info = pendingInfos[i];
                var env = info.env;
                if (env == null || !env.isBeingDiscovered)
                    continue;

                var status = env.GetComponent<EnvironmentStatus>();
                if (status != null)
                {
                    if (info.turnsCompleted == 0)
                        status.StartPartialReveal(info.effectiveTurnsRequired);

                    status.AdvancePartialReveal();
                }

                info.turnsCompleted++;
                env.discoveryTurnsLeft = Mathf.Max(0, env.discoveryTurnsLeft - 1);

                DiseaseManager.Instance?.TrySpreadContagiousVirusesWithinGroup(
                    info.reservedIndividualIds,
                    "Discovery",
                    env != null ? env.name : null,
                    1f);

                env.UpdateDiscoveryTimerUI();

                env.GetEffectiveDiscovery(out _, out float currentFailureChance);
                currentFailureChance = Mathf.Clamp(currentFailureChance, 0f, 100f);

                float baseAdjustedFailureChance = currentFailureChance / (info.turnsCompleted + 1);

                float diseaseFailureAdd = DiseaseManager.Instance != null
                    ? DiseaseManager.Instance.GetTaskFailureChanceAddPercentForIndividuals(
                        info.reservedIndividualIds,
                        "Discovery",
                        env != null ? env.name : null)
                    : 0f;

                float adjustedFailureChance = Mathf.Clamp(baseAdjustedFailureChance + diseaseFailureAdd, 0f, 100f);

                if (UnityEngine.Random.value <= adjustedFailureChance / 100f)
                {
                    int lost = 0;

                    if (!string.IsNullOrEmpty(info.reservationId))
                    {
                        float penaltyChance = currentFailureChance / 100f;

                        int effectivePenalty = Mathf.Max(0, env.DiscoveryPopPenaltyOnFailure);
                        var buffs = PlayerTechBuffs.Instance;
                        if (buffs != null)
                            effectivePenalty = buffs.GetDiscoveryPenaltyEffective(env, effectivePenalty);

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
                            DiseaseTaskResultType.DiscoveryFailure);

                        populationManager.ReleaseReservation(info.reservationId);
                    }

                    PlayersPopulationManager.Instance?.ForceSyncUI();

                    OnDiscoveryFailedDetailed?.Invoke(env, lost);

                    SetTileInteractable(env, true);

                    status?.ResetReveal();
                    env.FailDiscoveryVisuals(lost);

                    env.discoveryTurnsRequired = env.BaseDiscoveryTurnsRequired;

                    HandleFailure(env, lost);
                    MarkDiscoveryStateDirty();

                    toRemove.Add(env);
                    continue;
                }

                if (env.discoveryTurnsLeft <= 0)
                {
                    if (!string.IsNullOrEmpty(info.reservationId))
                    {
                        DiseaseManager.Instance?.TryApplyEnvironmentalDiseaseRiskForTaskResult(
                            env,
                            info.reservationId,
                            DiseaseTaskResultType.DiscoverySuccess);

                        populationManager.ReleaseReservation(info.reservationId);
                    }

                    PlayersPopulationManager.Instance?.ForceSyncUI();

                    SetTileInteractable(env, true);
                    MarkDiscoveryStateDirty();
                    HandleSuccess(env, info);
                    env.CompleteDiscoveryVisuals();

                    env.discoveryTurnsRequired = env.BaseDiscoveryTurnsRequired;

                    toRemove.Add(env);
                }
            }

            for (int i = 0; i < toRemove.Count; i++)
                inProgress.Remove(toRemove[i]);

            toRemove.Clear();

            idx = end;
            yield return null;
        }

        discoveryProcessingCoroutine = null;
    }

    private void HandleFailure(EnvironmentControl env, int populationLost = 0)
    {
        env.isBeingDiscovered = false;

        var box = env.GetComponent<BoxCollider>();
        if (box != null) box.enabled = true;

        string lossNote = populationLost > 0 ? $" {populationLost} population lost." : "";
        PostDiscoveryNotification(NotificationType.DiscoveryFailed, env, populationLost, "Discovery Failed",
            $"Discovery at {env.environmentName} failed.{lossNote}");

        CivilizationHappinessSystem.Instance?.NotifyTaskResult(success: false, weight: 2f);
        MarkDiscoveryStateDirty();
        OnDiscoveryFailed?.Invoke(env);
    }

    private void HandleSuccess(EnvironmentControl env, DiscoveryInfo info)
    {
        env.isBeingDiscovered = false;

        var status = env.GetComponent<EnvironmentStatus>();
        status?.CompleteDiscovery();

        MarkDiscoveredInternal(env);

        var box = env.GetComponent<BoxCollider>();
        if (box != null) box.enabled = true;

        int xp = EnvironmentXPCalculator.CalculateDiscoveryXPFromFailChance(
            env.environmentType,
            env.environmentTileType,
            info.requiredPopulation,
            info.effectiveFailureChance
        );

        if (xp > 0)
            PlayerLevel.Instance?.AddXP(xp);

        MarkDiscoveryStateDirty();
        CivilizationHappinessSystem.Instance?.NotifyTaskResult(success: true, weight: 1f);

        PostDiscoveryNotification(NotificationType.DiscoveryCompleted, env, 0, "Discovery Complete",
            $"{env.environmentName} has been discovered.");

        OnDiscoveryCompleted?.Invoke(env);
    }

    private void MarkDiscoveredInternal(EnvironmentControl env)
    {
        if (!discovered.Contains(env))
            discovered.Add(env);

        env.isBeingDiscovered = false;
        env.discoveryTurnsLeft = 0;

        EnvironmentStatus status = env.GetComponent<EnvironmentStatus>();
        status?.SetDiscovered(true);
    }

    public PlayerDiscoverySaveData SaveState()
    {
        PlayerDiscoverySaveData data = new PlayerDiscoverySaveData();

        foreach (var kv in inProgress)
        {
            DiscoveryInfo info = kv.Value;
            if (info == null || info.env == null || string.IsNullOrWhiteSpace(info.env.EnvironmentID))
                continue;

            data.activeDiscoveries.Add(new ActiveDiscoverySaveData
            {
                environmentID = info.env.EnvironmentID,
                turnsCompleted = info.turnsCompleted,
                effectiveFailureChance = info.effectiveFailureChance,
                effectiveTurnsRequired = info.effectiveTurnsRequired,
                requiredPopulation = info.requiredPopulation,
                reservationId = info.reservationId
            });
        }

        return data;
    }

    public void LoadState(PlayerDiscoverySaveData data)
    {
        if (discoveryProcessingCoroutine != null)
        {
            StopCoroutine(discoveryProcessingCoroutine);
            discoveryProcessingCoroutine = null;
        }

        allEnvironments.Clear();
        discovered.Clear();
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

        if (data != null && data.activeDiscoveries != null)
        {
            for (int i = 0; i < data.activeDiscoveries.Count; i++)
            {
                ActiveDiscoverySaveData saved = data.activeDiscoveries[i];
                if (saved == null || string.IsNullOrWhiteSpace(saved.environmentID))
                    continue;

                if (!envById.TryGetValue(saved.environmentID, out EnvironmentControl env) || env == null)
                {
                    //Debug.LogWarning($"[PlayerDiscoveryManager] Could not resolve environment '{saved.environmentID}' while loading active discovery.");
                    continue;
                }

                if (env.IsDiscovered)
                    continue;

                DiscoveryInfo info = new DiscoveryInfo
                {
                    env = env,
                    turnsCompleted = Mathf.Max(0, saved.turnsCompleted),
                    effectiveFailureChance = Mathf.Clamp(saved.effectiveFailureChance, 0f, 100f),
                    effectiveTurnsRequired = Mathf.Max(1, saved.effectiveTurnsRequired),
                    originalTurnsRequired = env.BaseDiscoveryTurnsRequired,
                    requiredPopulation = Mathf.Max(1, saved.requiredPopulation),
                    reservationId = saved.reservationId
                };

                inProgress[env] = info;

                if (!string.IsNullOrWhiteSpace(info.reservationId) && populationManager != null)
                {
                    populationManager.UpdateReservationMetadata(
                        info.reservationId,
                        PopulationReservationKind.Discovery,
                        GetReservationOwnerId(env),
                        nameof(EnvironmentControl));
                }

                env.isBeingDiscovered = true;
                env.discoveryTurnsRequired = info.effectiveTurnsRequired;

                int derivedTurnsLeft = Mathf.Clamp(
                    info.effectiveTurnsRequired - info.turnsCompleted,
                    0,
                    info.effectiveTurnsRequired
                );

                env.discoveryTurnsLeft = derivedTurnsLeft;

                SetTileInteractable(env, false);

                EnvironmentStatus status = env.GetComponent<EnvironmentStatus>();
                if (status != null)
                {
                    status.ResetReveal();
                    status.StartPartialReveal(info.effectiveTurnsRequired);

                    for (int step = 0; step < info.turnsCompleted; step++)
                        status.AdvancePartialReveal();
                }

                env.UpdateDiscoveryTimerUI();
                env.RebuildRuntimeUIState();
            }
        }

        SyncInspectorLists();
        MarkDiscoveryStateDirty();
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

    private static void PostDiscoveryNotification(NotificationType type, EnvironmentControl env,
        int populationLost, string fallbackTitle, string fallbackMessage)
    {
        if (NotificationManager.Instance == null) return;

        string title, message;
        if (NotificationMessageCrafterManager.Instance != null)
        {
            (title, message) = NotificationMessageCrafterManager.Instance.Craft(type, env, populationLost);
        }
        else if (type == NotificationType.DiscoveryFailed && TaskFailureStoryManager.Instance != null)
        {
            title   = fallbackTitle;
            message = TaskFailureStoryManager.Instance.BuildStory(env, TaskFailureType.Discovery, populationLost);
            if (string.IsNullOrWhiteSpace(message)) message = fallbackMessage;
        }
        else
        {
            title   = fallbackTitle;
            message = fallbackMessage;
        }

        NotificationManager.Instance.AddNotification(type, title, message, env.transform.position);
    }
}
