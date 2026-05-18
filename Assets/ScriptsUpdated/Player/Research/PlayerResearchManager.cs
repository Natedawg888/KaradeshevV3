using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PlayerResearchManager : MonoBehaviour
{
    public static PlayerResearchManager Instance { get; private set; }

    [Header("Dependencies")]
    public TechnologyManager technologyManager; // optional; auto-find if null

    [Header("UI")]
    [Tooltip("Content/Vertical group under your main canvas where research task rows appear.")]
    public Transform researchTasksContentRoot;
    public ResearchTaskEntry researchTaskEntryPrefab;

    [Header("Research Failure Tuning")]
    [Range(0f, 1f)] public float maxFailAtThreshold = 0.60f;
    [Range(0f, 1f)] public float minFailAtHighMargin = 0.05f;
    [Min(1)] public int marginForMinFail = 20;

    [Header("Performance")]
    [Min(1)] public int maxResearchesAdvancedPerFrame = 20;

    private readonly HashSet<string> _researched = new();   // techIDs
    private readonly List<ActiveResearch> _active = new();

    // Cache of technologies available at the current player level (level gate only)
    private readonly List<Technology> _availableByPlayerLevel = new();
    public IReadOnlyList<Technology> AvailableByPlayerLevel => _availableByPlayerLevel;

    public IReadOnlyCollection<string> GetResearchedIDs() => _researched;

    [Header("Debug (Inspector Only)")]
    [SerializeField] private List<string> levelAvailableDebug = new();
    public IReadOnlyList<string> LevelAvailableDebug => levelAvailableDebug;

    [Header("Debug Tech Unlocks")]
    [SerializeField] private bool applyDebugResearchedTechsOnStart = true;
    [SerializeField] private bool debugUnlockOnlyInEditor = true;

    [Tooltip("Apply normal tech effects (world/building/health/environment) when debug-unlocking.")]
    [SerializeField] private bool debugApplyUnlockedTechEffects = true;

    [Tooltip("Also grant the tech's normal knowledge/xp rewards when debug-unlocking.")]
    [SerializeField] private bool debugGrantRewardsForUnlockedTechs = false;

    [Tooltip("Tech IDs to treat as already researched when play starts.")]
    [SerializeField] private List<string> debugStartWithResearched = new();

    [Header("Debug (Inspector Preview)")]
    [SerializeField] private List<string> debugStartWithResearchedPreview = new();

    private Coroutine _tickCoroutine;

    private class ActiveResearch
    {
        public Technology tech;
        public int totalTurns;
        public int turnsLeft;
        public ResearchTaskEntry ui;
        public BuildingControl station;
        public string reservationId;
        public float baseFail;
        public BuildingStatus stationStatus;
        public System.Action<BuildingState> stationListener;
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    private void MarkKnowledgeDirty()
    {
        SaveSystem.MarkSectionDirty(SaveSectionKeys.Knowledge);
    }

    private void Start()
    {
        if (!technologyManager) technologyManager = TechnologyManager.Instance;

        ApplyDebugResearchedTechsIfNeeded();

        RefreshAvailableByPlayerLevel();

        TurnSystem.SubscribeToEndOfTurn(OnEndTurn);

        if (PlayerLevel.Instance != null)
            PlayerLevel.Instance.OnLevelUp += HandlePlayerLevelUp;
    }

    private void OnDestroy()
    {
        TurnSystem.UnsubscribeFromEndOfTurn(OnEndTurn);

        if (PlayerLevel.Instance != null)
            PlayerLevel.Instance.OnLevelUp -= HandlePlayerLevelUp;
    }

    private string GetResearchReservationOwnerId(Technology tech)
    {
        if (tech != null && !string.IsNullOrWhiteSpace(tech.techID))
            return tech.techID;

        return gameObject.GetInstanceID().ToString();
    }

    private string GetResearchReservationOwnerType()
    {
        return nameof(PlayerResearchManager);
    }

    private void TagResearchReservation(Technology tech, string reservationId)
    {
        if (string.IsNullOrWhiteSpace(reservationId))
            return;

        PlayersPopulationManager.Instance?.UpdateReservationMetadata(
            reservationId,
            PopulationReservationKind.Research,
            GetResearchReservationOwnerId(tech),
            GetResearchReservationOwnerType());
    }

    public void RetagActiveResearchReservations()
    {
        for (int i = 0; i < _active.Count; i++)
        {
            ActiveResearch ar = _active[i];
            if (ar == null || string.IsNullOrWhiteSpace(ar.reservationId))
                continue;

            TagResearchReservation(ar.tech, ar.reservationId);
        }
    }

    private void HandlePlayerLevelUp(int newLevel)
    {
        RefreshAvailableByPlayerLevel();
    }

    public void RefreshAvailableByPlayerLevel()
    {
        _availableByPlayerLevel.Clear();
        if (!technologyManager) return;

        int playerLevel = PlayerLevel.Instance ? PlayerLevel.Instance.GetCurrentLevel() : 0;

        foreach (var t in technologyManager.GetAll())
        {
            if (t == null || string.IsNullOrWhiteSpace(t.techID)) continue;
            if (_researched.Contains(t.techID)) continue;

            if (t.requiredPlayerLevel <= playerLevel)
                _availableByPlayerLevel.Add(t);
        }

        _availableByPlayerLevel.Sort((a, b) =>
        {
            int lvl = a.requiredPlayerLevel.CompareTo(b.requiredPlayerLevel);
            if (lvl != 0) return lvl;
            return string.Compare(a.techName ?? a.techID, b.techName ?? b.techID, System.StringComparison.OrdinalIgnoreCase);
        });

        levelAvailableDebug.Clear();
        for (int i = 0; i < _availableByPlayerLevel.Count; i++)
        {
            var t = _availableByPlayerLevel[i];
            if (t == null) continue;
            levelAvailableDebug.Add(
                $"{t.techName ?? t.techID}  [id:{t.techID}]  Lvl≥{t.requiredPlayerLevel}  K≥{t.requiredKnowledge}%  Turns:{t.turnsRequired}"
            );
        }
    }

    public List<Technology> GetAvailableToResearch(BuildingControl optionalStation = null)
    {
        var list = new List<Technology>();
        if (!technologyManager) return list;

        string stationId = optionalStation ? optionalStation.buildingID : null;
        int playerLevel = PlayerLevel.Instance ? PlayerLevel.Instance.GetCurrentLevel() : int.MaxValue;
        int knowledge = GetCurrentKnowledge();

        foreach (var t in technologyManager.GetAll())
        {
            if (t == null || string.IsNullOrWhiteSpace(t.techID)) continue;
            if (_researched.Contains(t.techID)) continue;
            if (_active.Any(a => a.tech.techID == t.techID)) continue;

            if (!t.IsEligibleForLevel(playerLevel)) continue;
            if (!t.IsEligibleForKnowledge(knowledge)) continue;
            if (!t.IsResearchableBy(stationId)) continue;
            if (!InventoryQuery.CanAfford(t.researchCosts)) continue;

            list.Add(t);
        }

        return list;
    }

    public List<Technology> GetResearched()
    {
        if (!technologyManager) return new List<Technology>();
        var set = new HashSet<string>(_researched);
        return technologyManager.GetAll().Where(t => t != null && set.Contains(t.techID)).ToList();
    }

    public bool IsResearched(string techID) => !string.IsNullOrWhiteSpace(techID) && _researched.Contains(techID);

    public bool StartResearch(Technology tech, BuildingControl station = null)
    {
        if (tech == null) return false;
        if (_researched.Contains(tech.techID)) return false;
        if (_active.Any(a => a.tech.techID == tech.techID)) return false;

        string stationId = station ? station.buildingID : null;

        int playerLevel = PlayerLevel.Instance ? PlayerLevel.Instance.GetCurrentLevel() : int.MaxValue;
        if (!tech.IsEligibleForLevel(playerLevel)) return false;

        int knowledge = GetCurrentKnowledge();
        if (!tech.IsEligibleForKnowledge(knowledge)) return false;

        if (!tech.IsResearchableBy(stationId)) return false;

        string reservationId = null;
        int needPop = Mathf.Max(0, tech.requiredPopulation);

        if (needPop > 0)
        {
            var ppm = PlayersPopulationManager.Instance;
            if (ppm == null) return false;

            if (!ppm.TryPickRandomNonBusyTaskIndividuals(
                    needPop,
                    PopulationReservationKind.Research,
                    GetResearchReservationOwnerId(tech),
                    GetResearchReservationOwnerType(),
                    out var picked,
                    out reservationId))
            {
                return false;
            }

            if (picked == null || picked.Count != needPop)
            {
                if (!string.IsNullOrEmpty(reservationId))
                    ppm.ReleaseReservation(reservationId);

                return false;
            }

            TagResearchReservation(tech, reservationId);
        }

        if (!SpendCosts(tech.researchCosts))
        {
            if (!string.IsNullOrEmpty(reservationId))
                PlayersPopulationManager.Instance?.ReleaseReservation(reservationId);
            return false;
        }

        ResearchTaskEntry entry = null;
        if (researchTaskEntryPrefab && researchTasksContentRoot)
        {
            entry = Instantiate(researchTaskEntryPrefab, researchTasksContentRoot);
            int turns = Mathf.Max(1, tech.turnsRequired);
            entry.Bind(turns, turns);
        }

        var ar = new ActiveResearch
        {
            tech = tech,
            totalTurns = Mathf.Max(1, tech.turnsRequired),
            turnsLeft = Mathf.Max(1, tech.turnsRequired),
            ui = entry,
            station = station,
            reservationId = reservationId,
            baseFail = ComputeFailureChance(tech),
        };

        if (station != null)
        {
            var status = station.GetComponent<BuildingStatus>();
            if (status != null)
            {
                System.Action<BuildingState> listener = null;
                listener = (s) =>
                {
                    if (s == BuildingState.Destroyed)
                    {
                        if (_active.Contains(ar))
                        {
                            Fail(ar, failThisTick: 1f);
                            _active.Remove(ar);
                        }
                    }
                };

                status.OnStateChanged += listener;
                ar.stationStatus = status;
                ar.stationListener = listener;
            }
        }

        _active.Add(ar);

        PlayersPopulationManager.Instance?.ForceSyncUI();
        MarkKnowledgeDirty();
        return true;
    }

    private void OnEndTurn()
    {
        var snapshot = _active.ToList();
        if (_tickCoroutine != null) StopCoroutine(_tickCoroutine);
        _tickCoroutine = StartCoroutine(ProcessResearches(snapshot));
    }

    private IEnumerator ProcessResearches(List<ActiveResearch> pending)
    {
        int idx = 0;
        var toRemove = new List<ActiveResearch>();

        while (idx < pending.Count)
        {
            int end = Mathf.Min(idx + maxResearchesAdvancedPerFrame, pending.Count);
            for (int i = idx; i < end; i++)
            {
                var ar = pending[i];
                if (ar == null || ar.tech == null)
                {
                    toRemove.Add(ar);
                    continue;
                }

                if (!_active.Contains(ar)) continue;

                TagResearchReservation(ar.tech, ar.reservationId);

                if (!ReconcileResearchReservation(ar))
                {
                    Fail(ar, 1f);
                    toRemove.Add(ar);
                    continue;
                }

                if (ar.stationStatus == null)
                {
                    if (ar.station != null)
                    {
                        Fail(ar, 1f);
                        toRemove.Add(ar);
                        continue;
                    }
                }
                else if (ar.stationStatus.CurrentState == BuildingState.Destroyed)
                {
                    Fail(ar, 1f);
                    toRemove.Add(ar);
                    continue;
                }

                ar.turnsLeft = Mathf.Max(0, ar.turnsLeft - 1);
                if (ar.ui) ar.ui.UpdateTurns(ar.turnsLeft);

                float progress01 = 1f - (ar.turnsLeft / (float)ar.totalTurns);
                float failThisTick = Mathf.Lerp(ar.baseFail, minFailAtHighMargin, Mathf.Clamp01(progress01));

                if (Random.value < failThisTick)
                {
                    Fail(ar, failThisTick);
                    toRemove.Add(ar);
                    continue;
                }

                if (ar.turnsLeft <= 0)
                {
                    Complete(ar);
                    toRemove.Add(ar);
                }

                MarkKnowledgeDirty();
            }

            idx = end;
            yield return null;
        }

        for (int i = 0; i < toRemove.Count; i++)
            _active.Remove(toRemove[i]);

        MarkKnowledgeDirty();
        _tickCoroutine = null;
    }

    private void Complete(ActiveResearch ar)
    {
        if (ar == null || ar.tech == null) return;

        UnsubscribeStation(ar);

        _researched.Add(ar.tech.techID);

        PlayerWorldTechApplier.Instance?.ApplyFor(ar.tech.techID);
        PlayerHealthTechApplier.Instance?.ApplyFor(ar.tech.techID);
        PlayerBuildingTechApplier.Instance?.ApplyFor(ar.tech.techID);
        PlayerEnvironmentTechApplier.Instance?.ApplyFor(ar.tech.techID);

        if (ar.ui) Destroy(ar.ui.gameObject);

        if (!string.IsNullOrEmpty(ar.reservationId))
            PlayersPopulationManager.Instance?.ReleaseReservation(ar.reservationId);

        PlayersPopulationManager.Instance?.ForceSyncUI();

        if (CivilizationStateManager.Instance != null && ar.tech.knowledgeReward > 0)
        {
            float add01 = Mathf.Clamp01(ar.tech.knowledgeReward / 100f);
            CivilizationStateManager.Instance.AdjustKnowledge(add01);
        }

        if (PlayerLevel.Instance != null)
        {
            int xp = (ar.tech.xpReward > 0) ? ar.tech.xpReward : Mathf.RoundToInt(ar.totalTurns * 2f);
            PlayerLevel.Instance.AddXP(xp);
        }

        PostResearchNotification(NotificationType.ResearchCompleted, ar.tech);
        ScoreManager.NotifyResearchCompleted();

        MarkKnowledgeDirty();
    }

    private bool SpendCosts(List<ResourceCost> costs)
    {
        if (costs == null || costs.Count == 0) return true;

        var pim = PlayerInventoryManager.Instance;
        if (!pim)
        {
            //Debug.LogError("[PlayerResearchManager] No PlayerInventoryManager.");
            return false;
        }

        if (!InventoryQuery.CanAfford(costs)) return false;

        var rollback = new List<ResourceCost>();
        for (int i = 0; i < costs.Count; i++)
        {
            var c = costs[i];
            if (c == null || c.resource == null || c.amount <= 0) continue;

            bool ok = c.resource.isGroup ? pim.TryRemoveGroup(c.resource, c.amount)
                                         : pim.TryRemove(c.resource, c.amount);
            if (!ok)
            {
                for (int r = 0; r < rollback.Count; r++)
                    pim.TryAdd(rollback[r].resource, rollback[r].amount);

                //Debug.LogWarning("[PlayerResearchManager] Spend failed; rolled back.");
                return false;
            }
            if (!c.resource.isGroup) rollback.Add(c);
        }

        return true;
    }

    private int GetCurrentKnowledge()
    {
        try
        {
            if (CivilizationStateManager.Instance != null)
                return Mathf.Max(0, Mathf.RoundToInt(CivilizationStateManager.Instance.knowledge01 * 100f));

            return 0;
        }
        catch
        {
            return 0;
        }
    }

    private string ResolveStationName(BuildingControl station)
    {
        if (!station) return "";
        var def = BuildingManager.Instance?.GetBuildingByID(station.buildingID);
        string displayName = !string.IsNullOrWhiteSpace(station.buildingName)
            ? station.buildingName
            : (def?.buildingName ?? station.buildingID);
        return displayName;
    }

    private void UnsubscribeStation(ActiveResearch ar)
    {
        if (ar?.stationStatus != null && ar.stationListener != null)
        {
            ar.stationStatus.OnStateChanged -= ar.stationListener;
            ar.stationListener = null;
            ar.stationStatus = null;
        }
    }

    private float ComputeFailureChance(Technology tech)
    {
        if (tech == null)
            return maxFailAtThreshold;

        int req = Mathf.Max(0, tech.requiredKnowledge);
        int cur = GetCurrentKnowledge();
        int margin = Mathf.Max(0, cur - req);

        float t = 1f - Mathf.Clamp01(margin / Mathf.Max(1f, (float)marginForMinFail));
        float fail = Mathf.Lerp(minFailAtHighMargin, maxFailAtThreshold, t);

        PlayerReligionManager religion = PlayerReligionManager.Instance;
        if (religion != null)
        {
            float additive = religion.GetAdditiveSum(SpiritEffectType.ResearchFailureChanceModifier);
            float multiplier = religion.GetMultiplierProduct(SpiritEffectType.ResearchFailureChanceModifier);

            fail += additive;
            fail *= multiplier;
        }

        return Mathf.Clamp01(fail);
    }

    private void Fail(ActiveResearch ar, float failThisTick)
    {
        if (ar == null) return;

        int completedTurns = Mathf.Max(0, ar.totalTurns - ar.turnsLeft);
        float progress01 = (ar.totalTurns > 0) ? Mathf.Clamp01(completedTurns / (float)ar.totalTurns) : 0f;

        if (CivilizationStateManager.Instance != null && ar.tech != null && ar.tech.knowledgeReward > 0)
        {
            float fullKnowledge01 = Mathf.Clamp01(ar.tech.knowledgeReward / 100f);
            float partialKnowledge01 = fullKnowledge01 * progress01;
            if (partialKnowledge01 > 0f)
                CivilizationStateManager.Instance.AdjustKnowledge(partialKnowledge01);
        }

        if (PlayerLevel.Instance != null && ar.tech != null)
        {
            int fullXp = (ar.tech.xpReward > 0) ? ar.tech.xpReward : Mathf.RoundToInt(ar.totalTurns * 2f);
            int partialXp = Mathf.RoundToInt(fullXp * progress01);
            if (partialXp > 0)
                PlayerLevel.Instance.AddXP(partialXp);
        }

        UnsubscribeStation(ar);

        if (ar.ui) Destroy(ar.ui.gameObject);

        if (!string.IsNullOrEmpty(ar.reservationId))
            PlayersPopulationManager.Instance?.ReleaseReservation(ar.reservationId);

        PlayersPopulationManager.Instance?.ForceSyncUI();

        //Debug.Log($"[Research] FAILED: '{ar.tech?.techName ?? ar?.tech?.techID}' " +
                //$"(reason={(failThisTick >= 1f ? "StationDestroyed" : "TickFail")}, " +
                //$"p={failThisTick:P0}, progress={progress01:P0})");

        PostResearchNotification(NotificationType.ResearchFailed, ar.tech);
    }

    public List<Technology> FilterOutResearchedAndActive(IEnumerable<Technology> source, BuildingControl optionalStation = null)
    {
        var result = new List<Technology>();
        if (source == null) return result;

        string stationId = optionalStation ? optionalStation.buildingID : null;

        foreach (var t in source)
        {
            if (t == null || string.IsNullOrWhiteSpace(t.techID)) continue;

            if (_researched.Contains(t.techID)) continue;
            if (_active.Any(a => a.tech != null && a.tech.techID == t.techID)) continue;
            if (!string.IsNullOrEmpty(stationId) && !t.IsResearchableBy(stationId)) continue;

            result.Add(t);
        }
        return result;
    }

    public float PreviewFailureChance(Technology tech)
    {
        if (tech == null) return -1f;
        return Mathf.Clamp01(ComputeFailureChance(tech));
    }

    public bool RevokeResearched(string techID, bool undoBuffs = true)
    {
        if (string.IsNullOrWhiteSpace(techID)) return false;
        if (!_researched.Remove(techID)) return false;

        if (undoBuffs)
        {
            PlayerWorldTechApplier.Instance?.RemoveFor(techID);
            PlayerBuildingTechApplier.Instance?.RemoveFor(techID);
            PlayerHealthTechApplier.Instance?.RemoveFor(techID);
            PlayerEnvironmentTechApplier.Instance?.RemoveFor(techID);
        }

        RefreshAvailableByPlayerLevel();
        PlayersPopulationManager.Instance?.ForceSyncUI();
        return true;
    }

    private bool ReconcileResearchReservation(ActiveResearch ar)
    {
        if (ar == null || ar.tech == null)
            return false;

        int required = Mathf.Max(0, ar.tech.requiredPopulation);
        if (required <= 0)
            return true;

        var familySim = PlayerFamilySimulationManager.Instance;
        var pop = PlayersPopulationManager.Instance;

        if (familySim == null || pop == null)
            return false;

        if (familySim.IsProductionReservationStillValid(ar.reservationId, required))
        {
            TagResearchReservation(ar.tech, ar.reservationId);
            return true;
        }

        bool replacedAll = TryBackfillInvalidResearchers(ar, pop, familySim);

        if (replacedAll &&
            familySim.IsProductionReservationStillValid(ar.reservationId, required))
        {
            familySim.RebusyReservation(ar.reservationId);
            TagResearchReservation(ar.tech, ar.reservationId);
            return true;
        }

        return false;
    }

    private bool TryBackfillInvalidResearchers(
        ActiveResearch ar,
        PlayersPopulationManager pop,
        PlayerFamilySimulationManager familySim)
    {
        if (ar == null || string.IsNullOrEmpty(ar.reservationId))
            return false;

        if (!pop.TryGetReservedIndividualIds(ar.reservationId, out var reservedIds) ||
            reservedIds == null || reservedIds.Count == 0)
        {
            return false;
        }

        bool allReplaced = true;
        var snapshot = reservedIds.ToList();

        for (int i = 0; i < snapshot.Count; i++)
        {
            string id = snapshot[i];
            var person = familySim.GetIndividuals().FirstOrDefault(p => p != null && p.Id == id);

            bool shouldReplace =
                person == null ||
                !person.IsAlive ||
                (person.AggregatedAgeGroup != AgeGroup.Teen &&
                 person.AggregatedAgeGroup != AgeGroup.Adult);

            if (!shouldReplace)
                continue;

            bool replaced;
            if (!pop.TryDetachIndividualFromExistingReservations(id, out replaced) || !replaced)
                allReplaced = false;
        }

        return allReplaced;
    }

    private void ApplyDebugResearchedTechsIfNeeded()
    {
        if (!applyDebugResearchedTechsOnStart)
            return;

        if (debugUnlockOnlyInEditor && !Application.isEditor)
            return;

        ApplyDebugResearchedTechs();
    }

    [ContextMenu("Apply Debug Researched Techs (Play Mode)")]
    private void ApplyDebugResearchedTechs()
    {
        if (!Application.isPlaying)
        {
            //Debug.LogWarning("[PlayerResearchManager] Enter Play Mode before applying debug researched techs.");
            return;
        }

        if (!technologyManager)
            technologyManager = TechnologyManager.Instance;

        if (technologyManager == null)
        {
            //Debug.LogWarning("[PlayerResearchManager] No TechnologyManager found.");
            return;
        }

        if (debugStartWithResearched == null || debugStartWithResearched.Count == 0)
            return;

        for (int i = 0; i < debugStartWithResearched.Count; i++)
        {
            string techID = debugStartWithResearched[i];
            if (string.IsNullOrWhiteSpace(techID))
                continue;

            var tech = ResolveTechnologyByID(techID);
            if (tech == null)
            {
                //Debug.LogWarning($"[PlayerResearchManager] Debug tech ID not found: {techID}");
                continue;
            }

            ForceSetResearchedForDebug(
                tech,
                applyEffects: debugApplyUnlockedTechEffects,
                grantRewards: debugGrantRewardsForUnlockedTechs
            );
        }

        RefreshAvailableByPlayerLevel();
        PlayersPopulationManager.Instance?.ForceSyncUI();
    }

    public bool ForceSetResearchedForDebug(Technology tech, bool applyEffects = true, bool grantRewards = false)
    {
        if (tech == null || string.IsNullOrWhiteSpace(tech.techID))
            return false;

        CancelActiveResearchByTechID(tech.techID);

        if (_researched.Contains(tech.techID))
            return false;

        _researched.Add(tech.techID);

        if (applyEffects)
        {
            PlayerWorldTechApplier.Instance?.ApplyFor(tech.techID);
            PlayerHealthTechApplier.Instance?.ApplyFor(tech.techID);
            PlayerBuildingTechApplier.Instance?.ApplyFor(tech.techID);
            PlayerEnvironmentTechApplier.Instance?.ApplyFor(tech.techID);
        }

        if (grantRewards)
        {
            if (CivilizationStateManager.Instance != null && tech.knowledgeReward > 0)
            {
                float add01 = Mathf.Clamp01(tech.knowledgeReward / 100f);
                CivilizationStateManager.Instance.AdjustKnowledge(add01);
            }

            if (PlayerLevel.Instance != null)
            {
                int xp = (tech.xpReward > 0) ? tech.xpReward : Mathf.RoundToInt(Mathf.Max(1, tech.turnsRequired) * 2f);
                PlayerLevel.Instance.AddXP(xp);
            }
        }

        //Debug.Log($"[PlayerResearchManager] Debug unlocked tech: {tech.techName ?? tech.techID}");
        return true;
    }

    private void CancelActiveResearchByTechID(string techID)
    {
        if (string.IsNullOrWhiteSpace(techID))
            return;

        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var ar = _active[i];
            if (ar == null || ar.tech == null)
                continue;

            if (ar.tech.techID != techID)
                continue;

            UnsubscribeStation(ar);

            if (ar.ui) Destroy(ar.ui.gameObject);

            if (!string.IsNullOrEmpty(ar.reservationId))
                PlayersPopulationManager.Instance?.ReleaseReservation(ar.reservationId);

            _active.RemoveAt(i);
        }
    }

    private void OnValidate()
    {
        debugStartWithResearchedPreview.Clear();

        if (debugStartWithResearched == null || debugStartWithResearched.Count == 0)
            return;

        if (!technologyManager)
            technologyManager = TechnologyManager.Instance != null
                ? TechnologyManager.Instance
                : FindObjectOfType<TechnologyManager>();

        for (int i = 0; i < debugStartWithResearched.Count; i++)
        {
            string techID = debugStartWithResearched[i];
            if (string.IsNullOrWhiteSpace(techID))
                continue;

            Technology tech = ResolveTechnologyByID(techID);

            if (tech != null)
            {
                debugStartWithResearchedPreview.Add(
                    $"{tech.techName ?? tech.techID}  [id:{tech.techID}]  Lvl≥{tech.requiredPlayerLevel}  K≥{tech.requiredKnowledge}%  Turns:{tech.turnsRequired}"
                );
            }
            else
            {
                debugStartWithResearchedPreview.Add($"MISSING TECH ID: {techID}");
            }
        }
    }

    private Technology ResolveTechnologyByID(string techID)
    {
        if (string.IsNullOrWhiteSpace(techID))
            return null;

        if (!technologyManager)
            technologyManager = TechnologyManager.Instance != null
                ? TechnologyManager.Instance
                : FindObjectOfType<TechnologyManager>();

        if (!technologyManager)
            return null;

        var tech = technologyManager.GetByID(techID);
        if (tech != null)
            return tech;

        var all = technologyManager.GetAll();
        for (int i = 0; i < all.Count; i++)
        {
            var t = all[i];
            if (t != null && t.techID == techID)
                return t;
        }

        return null;
    }

    private string GetStationSaveableID(BuildingControl station)
    {
        if (!station) return null;

        Saveable saveable = station.GetComponent<Saveable>();
        if (saveable == null)
            saveable = station.GetComponentInParent<Saveable>();

        return saveable != null ? saveable.uniqueID : null;
    }

    private BuildingControl ResolveStationBySaveableID(string saveableID)
    {
        if (string.IsNullOrWhiteSpace(saveableID))
            return null;

        BuildingControl[] allStations = FindObjectsOfType<BuildingControl>(true);
        for (int i = 0; i < allStations.Length; i++)
        {
            BuildingControl station = allStations[i];
            if (!station) continue;

            Saveable saveable = station.GetComponent<Saveable>();
            if (saveable == null)
                saveable = station.GetComponentInParent<Saveable>();

            if (saveable != null && saveable.uniqueID == saveableID)
                return station;
        }

        return null;
    }

    private void AttachStationListener(ActiveResearch ar)
    {
        if (ar == null || ar.station == null)
            return;

        var status = ar.station.GetComponent<BuildingStatus>();
        if (status == null)
            return;

        System.Action<BuildingState> listener = null;
        listener = (s) =>
        {
            if (s == BuildingState.Destroyed)
            {
                if (_active.Contains(ar))
                {
                    Fail(ar, failThisTick: 1f);
                    _active.Remove(ar);
                }
            }
        };

        status.OnStateChanged += listener;
        ar.stationStatus = status;
        ar.stationListener = listener;
    }

    private ResearchTaskEntry CreateResearchUI(int totalTurns, int turnsLeft)
    {
        if (!researchTaskEntryPrefab || !researchTasksContentRoot)
            return null;

        ResearchTaskEntry entry = Instantiate(researchTaskEntryPrefab, researchTasksContentRoot);
        entry.Bind(Mathf.Max(1, totalTurns), Mathf.Max(0, turnsLeft));
        return entry;
    }

    public PlayerResearchSaveData SaveState()
    {
        PlayerResearchSaveData data = new PlayerResearchSaveData();

        foreach (string techID in _researched)
        {
            if (!string.IsNullOrWhiteSpace(techID))
                data.researchedTechIDs.Add(techID);
        }

        for (int i = 0; i < _active.Count; i++)
        {
            ActiveResearch ar = _active[i];
            if (ar == null || ar.tech == null || string.IsNullOrWhiteSpace(ar.tech.techID))
                continue;

            data.activeResearches.Add(new ActiveResearchSaveData
            {
                techID = ar.tech.techID,
                totalTurns = ar.totalTurns,
                turnsLeft = ar.turnsLeft,
                stationSaveableID = GetStationSaveableID(ar.station),
                reservationId = ar.reservationId,
                baseFail = ar.baseFail
            });
        }

        return data;
    }

    public void LoadState(PlayerResearchSaveData data)
    {
        if (!technologyManager)
            technologyManager = TechnologyManager.Instance;

        ClearActiveResearchesForLoad();

        List<string> oldResearched = _researched.ToList();
        for (int i = 0; i < oldResearched.Count; i++)
        {
            string techID = oldResearched[i];
            if (string.IsNullOrWhiteSpace(techID))
                continue;

            PlayerWorldTechApplier.Instance?.RemoveFor(techID);
            PlayerBuildingTechApplier.Instance?.RemoveFor(techID);
            PlayerHealthTechApplier.Instance?.RemoveFor(techID);
            PlayerEnvironmentTechApplier.Instance?.RemoveFor(techID);
        }

        _researched.Clear();

        if (data != null && data.researchedTechIDs != null)
        {
            for (int i = 0; i < data.researchedTechIDs.Count; i++)
            {
                string rawId = data.researchedTechIDs[i];
                string techID = string.IsNullOrWhiteSpace(rawId) ? null : rawId.Trim();

                if (!string.IsNullOrEmpty(techID))
                    _researched.Add(techID);
            }
        }

        foreach (string techID in _researched)
        {
            if (string.IsNullOrWhiteSpace(techID))
                continue;

            PlayerWorldTechApplier.Instance?.ApplyFor(techID);
            PlayerHealthTechApplier.Instance?.ApplyFor(techID);
            PlayerBuildingTechApplier.Instance?.ApplyFor(techID);
            PlayerEnvironmentTechApplier.Instance?.ApplyFor(techID);
        }

        if (data != null && data.activeResearches != null)
        {
            for (int i = 0; i < data.activeResearches.Count; i++)
            {
                ActiveResearchSaveData saved = data.activeResearches[i];
                if (saved == null || string.IsNullOrWhiteSpace(saved.techID))
                    continue;

                Technology tech = ResolveTechnologyByID(saved.techID);
                if (tech == null)
                {
                    //Debug.LogWarning($"[PlayerResearchManager] Could not resolve active research tech '{saved.techID}' on load.");
                    continue;
                }

                if (_researched.Contains(tech.techID))
                    continue;

                BuildingControl station = ResolveStationBySaveableID(saved.stationSaveableID);

                ActiveResearch ar = new ActiveResearch
                {
                    tech = tech,
                    totalTurns = Mathf.Max(1, saved.totalTurns),
                    turnsLeft = Mathf.Clamp(saved.turnsLeft, 0, Mathf.Max(1, saved.totalTurns)),
                    ui = CreateResearchUI(saved.totalTurns, saved.turnsLeft),
                    station = station,
                    reservationId = saved.reservationId,
                    baseFail = Mathf.Clamp01(saved.baseFail)
                };

                AttachStationListener(ar);

                if (!string.IsNullOrWhiteSpace(ar.reservationId))
                    TagResearchReservation(ar.tech, ar.reservationId);

                _active.Add(ar);
            }
        }

        RefreshAvailableByPlayerLevel();
        PlayersPopulationManager.Instance?.ForceSyncUI();
    }

    private void ClearActiveResearchesForLoad()
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            ActiveResearch ar = _active[i];
            if (ar == null)
                continue;

            UnsubscribeStation(ar);

            if (ar.ui)
                Destroy(ar.ui.gameObject);

            if (!string.IsNullOrEmpty(ar.reservationId))
                PlayersPopulationManager.Instance?.ReleaseReservation(ar.reservationId);
        }

        _active.Clear();

        if (_tickCoroutine != null)
        {
            StopCoroutine(_tickCoroutine);
            _tickCoroutine = null;
        }
    }

    public void InstallRuntimeRefs(
        TechnologyManager newTechnologyManager = null,
        Transform newResearchTasksContentRoot = null)
    {
        if (newTechnologyManager != null)
            technologyManager = newTechnologyManager;

        if (newResearchTasksContentRoot != null)
            researchTasksContentRoot = newResearchTasksContentRoot;
    }

    private static void PostResearchNotification(NotificationType type, Technology tech)
    {
        if (NotificationManager.Instance == null || tech == null) return;

        string techName = !string.IsNullOrEmpty(tech.techName) ? tech.techName : tech.techID;

        string title, message;
        if (NotificationMessageCrafterManager.Instance != null)
            (title, message) = NotificationMessageCrafterManager.Instance.CraftResearch(type, techName);
        else
        {
            title   = type == NotificationType.ResearchFailed ? "Research Failed"   : "Research Complete";
            message = type == NotificationType.ResearchFailed
                ? $"Research on {techName} has failed."
                : $"{techName} has been researched.";
        }

        // No world position — Go To button will not show for research notifications.
        NotificationManager.Instance.AddNotification(type, title, message);
    }
}
