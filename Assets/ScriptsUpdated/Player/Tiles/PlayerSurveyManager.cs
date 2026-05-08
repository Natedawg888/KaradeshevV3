using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerSurveyManager : MonoBehaviour
{
    public static PlayerSurveyManager Instance { get; private set; }

    [Header("Dependencies")]
    public PlayersPopulationManager populationManager;
    [Tooltip("If left empty, will use PlayerKnownResourcesManager.Instance at runtime.")]
    public PlayerKnownResourcesManager knownManager;

    [Header("Performance")]
    [Tooltip("How many in-progress surveys to advance per frame when processing end-of-turn work.")]
    [SerializeField, Min(1)] private int maxSurveysPerFrame = 10;

    [Header("Tornado Survey Interruption")]
    public bool tornadoCancelsSurvey = true;

    [Range(0f, 1f)] public float tornadoTeenSurveyDeathChance = 0.08f;
    [Range(0f, 1f)] public float tornadoAdultSurveyDeathChance = 0.06f;

    [Tooltip("Extra multiplier applied to tornado survey death chance.")]
    [Min(0f)] public float tornadoSurveyDeathChanceMultiplier = 1f;

    // Events
    public event Action<EnvironmentControl> OnSurveyStarted;
    public event Action<EnvironmentControl> OnSurveyFailed;
    public event Action<EnvironmentControl> OnSurveyCompleted;

    // Known-only survey completion payload
    public event Action<EnvironmentControl, List<(ResourceDefinition def, int amount)>> OnSurveyCompletedKnown;

    // Internal tracking
    private readonly Dictionary<EnvironmentControl, SurveyInfo> inProgress = new();
    private readonly List<EnvironmentControl> surveyed = new();

    // reusable temp buffers
    private readonly List<EnvironmentControl> _tmpInProgressKeys = new();
    private readonly List<SurveyInfo> _tmpSurveySnapshot = new();
    private readonly List<EnvironmentControl> _tmpToUnsurvey = new();

    [Serializable]
    private class SurveyInfo
    {
        public EnvironmentControl env;
        public int turnsCompleted;
        public string reservationId;
    }

    [Header("Debug (Read Only)")]
    [SerializeField] private List<EnvironmentControl> inProgressInspector = new();
    [SerializeField] private List<EnvironmentControl> surveyedInspector = new();

    private Coroutine surveyCoroutine;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        if (!knownManager)
            knownManager = PlayerKnownResourcesManager.Instance;

        if (!populationManager)
            populationManager = PlayersPopulationManager.Instance;
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

    private void SyncInspectorLists()
    {
        inProgressInspector.Clear();
        surveyedInspector.Clear();

        foreach (var kv in inProgress)
        {
            if (kv.Key != null)
                inProgressInspector.Add(kv.Key);
        }

        for (int i = 0; i < surveyed.Count; i++)
        {
            if (surveyed[i] != null)
                surveyedInspector.Add(surveyed[i]);
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

    public bool StartSurvey(EnvironmentControl env)
    {
        if (env == null)
            return false;

        if (env.IsSurveyed || inProgress.ContainsKey(env))
            return false;

        if (populationManager == null)
            populationManager = PlayersPopulationManager.Instance;

        if (populationManager == null)
            return false;

        int requiredPop = Mathf.Max(1, env.requireSurveyPopulation);
        string ownerId = GetReservationOwnerId(env);

        if (!populationManager.TryPickRandomNonBusyTaskIndividuals(
                requiredPop,
                PopulationReservationKind.Survey,
                ownerId,
                nameof(EnvironmentControl),
                out List<Individual> picked,
                out string resId))
        {
            return false;
        }

        // strict safety check
        if (picked == null || picked.Count != requiredPop)
        {
            if (!string.IsNullOrWhiteSpace(resId))
                populationManager.ReleaseReservation(resId);

            return false;
        }

        PlayersPopulationManager.Instance?.ForceSyncUI();

        env.BeginSurveyVisuals();

        inProgress[env] = new SurveyInfo
        {
            env = env,
            turnsCompleted = 0,
            reservationId = resId
        };

        MarkJobsDirty();
        OnSurveyStarted?.Invoke(env);
        return true;
    }

    public void CancelSurvey(EnvironmentControl env)
    {
        if (env == null || !inProgress.TryGetValue(env, out var info))
            return;

        env.isSurveying = false;
        env.surveyTurnsLeft = env.surveyTurnsRequired;

        if (env.surveyTimerUI != null)
            env.surveyTimerUI.gameObject.SetActive(false);

        if (env.canvas != null)
            env.canvas.SetActive(false);

        if (!string.IsNullOrWhiteSpace(info.reservationId))
            populationManager.ReleaseReservation(info.reservationId);

        inProgress.Remove(env);
        MarkJobsDirty();
        OnSurveyFailed?.Invoke(env);
    }

    private void OnTurnEnded()
    {
        _tmpToUnsurvey.Clear();

        for (int i = 0; i < surveyed.Count; i++)
        {
            var env = surveyed[i];
            if (env == null)
                continue;

            env.AdvanceResurveyTurn();
            if (env.needsResurvey)
                _tmpToUnsurvey.Add(env);
        }

        for (int i = 0; i < _tmpToUnsurvey.Count; i++)
            surveyed.Remove(_tmpToUnsurvey[i]);

        _tmpSurveySnapshot.Clear();
        foreach (var kv in inProgress)
        {
            if (kv.Value != null)
                _tmpSurveySnapshot.Add(kv.Value);
        }

        if (surveyCoroutine != null)
            StopCoroutine(surveyCoroutine);

        surveyCoroutine = StartCoroutine(ProcessSurveys(_tmpSurveySnapshot));
        MarkJobsDirty();
    }

    private IEnumerator ProcessSurveys(List<SurveyInfo> pending)
    {
        var toRemove = new List<EnvironmentControl>();
        int idx = 0;

        while (idx < pending.Count)
        {
            int end = Mathf.Min(idx + maxSurveysPerFrame, pending.Count);

            for (int i = idx; i < end; i++)
            {
                var info = pending[i];
                var env = info.env;

                if (env == null || !env.isSurveying)
                    continue;

                info.turnsCompleted++;
                env.AdvanceSurveyTurn();

                if (env.surveyTurnsLeft <= 0)
                {
                    if (!string.IsNullOrWhiteSpace(info.reservationId))
                        populationManager.ReleaseReservation(info.reservationId);

                    PlayersPopulationManager.Instance?.ForceSyncUI();

                    env.CompleteSurveyVisuals();

                    if (!surveyed.Contains(env))
                        surveyed.Add(env);

                    OnSurveyCompleted?.Invoke(env);

                    var knownList = GetKnownSurveyEntries(env);
                    OnSurveyCompletedKnown?.Invoke(env, knownList);

                    toRemove.Add(env);
                }
            }

            for (int i = 0; i < toRemove.Count; i++)
                inProgress.Remove(toRemove[i]);

            toRemove.Clear();

            idx = end;
            yield return null;
        }

        surveyCoroutine = null;
    }

    public List<(ResourceDefinition def, int amount)> GetKnownSurveyEntries(EnvironmentControl env)
    {
        var outList = new List<(ResourceDefinition, int)>();
        if (!env)
            return outList;

        var node = env.GetComponent<EnvironmentResourceNode>();
        if (!node || node.SpawnedResources == null)
            return outList;

        var km = knownManager ? knownManager : PlayerKnownResourcesManager.Instance;

        for (int i = 0; i < node.SpawnedResources.Count; i++)
        {
            var e = node.SpawnedResources[i];
            if (e == null || e.definition == null)
                continue;

            if (e.amount <= 0)
                continue;

            if (km != null && !km.IsKnown(e.definition))
                continue;

            outList.Add((e.definition, e.amount));
        }

        return outList;
    }

    public IReadOnlyList<EnvironmentControl> GetSurveysInProgress()
    {
        _tmpInProgressKeys.Clear();

        foreach (var kv in inProgress)
        {
            if (kv.Key != null)
                _tmpInProgressKeys.Add(kv.Key);
        }

        return _tmpInProgressKeys;
    }

    public IReadOnlyList<EnvironmentControl> GetSurveyed() => surveyed;

    public PlayerSurveySaveData SaveState()
    {
        PlayerSurveySaveData data = new PlayerSurveySaveData();

        foreach (var kv in inProgress)
        {
            SurveyInfo info = kv.Value;
            if (info == null || info.env == null || string.IsNullOrWhiteSpace(info.env.EnvironmentID))
                continue;

            data.activeSurveys.Add(new ActiveSurveySaveData
            {
                environmentID = info.env.EnvironmentID,
                turnsCompleted = info.turnsCompleted,
                reservationId = info.reservationId
            });
        }

        for (int i = 0; i < surveyed.Count; i++)
        {
            EnvironmentControl env = surveyed[i];
            if (env == null || string.IsNullOrWhiteSpace(env.EnvironmentID))
                continue;

            data.surveyedEnvironmentIDs.Add(env.EnvironmentID);
        }

        return data;
    }

    public void LoadState(PlayerSurveySaveData data)
    {
        if (surveyCoroutine != null)
        {
            StopCoroutine(surveyCoroutine);
            surveyCoroutine = null;
        }

        if (!knownManager)
            knownManager = PlayerKnownResourcesManager.Instance;

        if (!populationManager)
            populationManager = PlayersPopulationManager.Instance;

        inProgress.Clear();
        surveyed.Clear();

        EnvironmentControl[] envs = FindObjectsOfType<EnvironmentControl>(true);
        Dictionary<string, EnvironmentControl> envById = new Dictionary<string, EnvironmentControl>(StringComparer.Ordinal);

        for (int i = 0; i < envs.Length; i++)
        {
            EnvironmentControl env = envs[i];
            if (env == null || string.IsNullOrWhiteSpace(env.EnvironmentID))
                continue;

            if (!envById.ContainsKey(env.EnvironmentID))
                envById.Add(env.EnvironmentID, env);
        }

        // Restore completed surveys
        if (data != null && data.surveyedEnvironmentIDs != null)
        {
            for (int i = 0; i < data.surveyedEnvironmentIDs.Count; i++)
            {
                string envId = data.surveyedEnvironmentIDs[i];
                if (string.IsNullOrWhiteSpace(envId))
                    continue;

                if (!envById.TryGetValue(envId, out EnvironmentControl env) || env == null)
                {
                    //Debug.LogWarning($"[PlayerSurveyManager] Could not resolve surveyed environment '{envId}' while loading.");
                    continue;
                }

                if (!surveyed.Contains(env))
                    surveyed.Add(env);

                env.isSurveying = false;
                env.IsSurveyed = true;
                env.surveyTurnsLeft = 0;
                env.RebuildRuntimeUIState();
            }
        }

        // Restore active surveys
        if (data != null && data.activeSurveys != null)
        {
            for (int i = 0; i < data.activeSurveys.Count; i++)
            {
                ActiveSurveySaveData saved = data.activeSurveys[i];
                if (saved == null || string.IsNullOrWhiteSpace(saved.environmentID))
                    continue;

                if (!envById.TryGetValue(saved.environmentID, out EnvironmentControl env) || env == null)
                {
                    //Debug.LogWarning($"[PlayerSurveyManager] Could not resolve active survey environment '{saved.environmentID}' while loading.");
                    continue;
                }

                if (env.IsSurveyed)
                    continue;

                SurveyInfo info = new SurveyInfo
                {
                    env = env,
                    turnsCompleted = Mathf.Max(0, saved.turnsCompleted),
                    reservationId = saved.reservationId
                };

                inProgress[env] = info;

                if (!string.IsNullOrWhiteSpace(info.reservationId) && populationManager != null)
                {
                    populationManager.UpdateReservationMetadata(
                        info.reservationId,
                        PopulationReservationKind.Survey,
                        GetReservationOwnerId(env),
                        nameof(EnvironmentControl));
                }

                env.isSurveying = true;
                env.IsSurveyed = false;

                int requiredTurns = Mathf.Max(1, env.surveyTurnsRequired);
                int derivedTurnsLeft = Mathf.Clamp(
                    requiredTurns - info.turnsCompleted,
                    0,
                    requiredTurns
                );

                env.surveyTurnsLeft = derivedTurnsLeft;
                env.RebuildRuntimeUIState();
            }
        }

        SyncInspectorLists();
    }

    private float GetTornadoSurveyDeathChance(AgeGroup ageGroup)
    {
        return ageGroup switch
        {
            AgeGroup.Teen => tornadoTeenSurveyDeathChance,
            AgeGroup.Adult => tornadoAdultSurveyDeathChance,
            _ => 0f
        };
    }

    private Individual FindSurveyIndividualById(string individualId)
    {
        if (string.IsNullOrWhiteSpace(individualId))
            return null;

        var familySim = PlayerFamilySimulationManager.Instance;
        if (familySim == null)
            return null;

        var people = familySim.GetIndividuals();
        if (people == null)
            return null;

        for (int i = 0; i < people.Count; i++)
        {
            var person = people[i];
            if (person != null && person.Id == individualId)
                return person;
        }

        return null;
    }

    private int RollTornadoSurveyDeaths(SurveyInfo info, bool debugLogging = false)
    {
        if (info == null || string.IsNullOrWhiteSpace(info.reservationId))
            return 0;

        var pop = populationManager != null ? populationManager : PlayersPopulationManager.Instance;
        var familySim = PlayerFamilySimulationManager.Instance;

        if (pop == null || familySim == null)
            return 0;

        if (!pop.TryGetReservedIndividualIds(info.reservationId, out var reservedIds) ||
            reservedIds == null ||
            reservedIds.Count == 0)
        {
            return 0;
        }

        HashSet<string> killIds = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < reservedIds.Count; i++)
        {
            string individualId = reservedIds[i];
            Individual person = FindSurveyIndividualById(individualId);

            if (person == null || !person.IsAlive)
                continue;

            float chance = GetTornadoSurveyDeathChance(person.AggregatedAgeGroup);
            if (chance <= 0f)
                continue;

            chance *= tornadoSurveyDeathChanceMultiplier;
            chance = Mathf.Clamp01(chance);

            if (UnityEngine.Random.value <= chance)
                killIds.Add(person.Id);
        }

        if (killIds.Count <= 0)
            return 0;

        if (familySim.TryKillIndividualsById(killIds, out int killedCount))
        {
            if (debugLogging)
            {
                //Debug.Log(
                    //$"[PlayerSurveyManager] Tornado killed {killedCount} surveying worker(s) on '{info.env?.name}'."
                //);
            }

            return killedCount;
        }

        return 0;
    }

    private void CancelSurveyDueToTornado(SurveyInfo info, bool debugLogging = false)
    {
        if (info == null || info.env == null)
            return;

        EnvironmentControl env = info.env;

        int killed = RollTornadoSurveyDeaths(info, debugLogging);

        env.isSurveying = false;
        env.surveyTurnsLeft = env.surveyTurnsRequired;
        env.IsSurveyed = false;
        env.RebuildRuntimeUIState();

        if (!string.IsNullOrWhiteSpace(info.reservationId))
            populationManager.ReleaseReservation(info.reservationId);

        PlayersPopulationManager.Instance?.ForceSyncUI();

        inProgress.Remove(env);
        MarkJobsDirty();
        OnSurveyFailed?.Invoke(env);

        if (debugLogging)
        {
            //Debug.Log(
                //$"[PlayerSurveyManager] Survey on '{env.name}' cancelled by tornado. " +
                //$"PopulationLost={killed}"
            //);
        }
    }
}
