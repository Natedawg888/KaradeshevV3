using System;
using System.Collections.Generic;
using UnityEngine;

public class DiseaseManager : MonoBehaviour
{
    public static DiseaseManager Instance { get; private set; }

    [Header("Definitions")]
    public List<DiseaseDefinitionSO> diseaseDefinitions = new();
    public List<PathogenCauseDefinitionSO> causeDefinitions = new();

    [Header("Consumed Resource Disease Risks")]
    [Tooltip("Disease risks triggered when population consumes a matching ResourceDefinition.resourceID.")]
    public List<ConsumedResourceDiseaseRisk> consumedResourceDiseaseRisks = new();

    [Header("Environmental Disease Risks")]
    [Tooltip("Disease risks triggered when gathering/discovery fails in risky environments or weather.")]
    public List<EnvironmentalDiseaseRisk> environmentalDiseaseRisks = new();

    public bool debugEnvironmentalDiseaseRisk = true;

    [Header("Task Failure Effects")]
    public bool enableDiseaseTaskFailureEffects = true;

    [Tooltip("Maximum extra task failure chance from disease. 0.35 = +35%.")]
    [Range(0f, 1f)]
    public float maxDiseaseTaskFailureChanceAdd01 = 0.35f;

    [Tooltip("Extra task failure chance if a disease has preventsWork enabled. 0.25 = +25%.")]
    [Range(0f, 1f)]
    public float preventsWorkFailureChanceAdd01 = 0.25f;

    public bool debugTaskFailureEffects = true;

    [Header("Work Efficiency Effects")]
    public bool enableDiseaseWorkEfficiencyEffects = true;

    [Header("Virus Spread")]
    public bool enableVirusContextSpread = true;

    [Tooltip("Global multiplier for all shelter virus spread.")]
    [Min(0f)] public float globalShelterVirusSpreadMultiplier = 1f;

    [Tooltip("Global multiplier for all task group virus spread.")]
    [Min(0f)] public float globalTaskGroupVirusSpreadMultiplier = 1f;

    [Tooltip("Extra cap so one infected person cannot spread too much in one context tick.")]
    [Min(1)] public int maxVirusSpreadAttemptsPerSource = 1;

    public bool debugVirusContextSpread = true;

    [Header("Virus Mutation")]
    public bool enableVirusMutation = true;
    public bool debugVirusMutation = true;

    public bool debugWorkEfficiencyEffects = true;

    [Header("Turns")]
    public bool processOnEndOfTurn = true;

    [Header("Debug")]
    public bool enableDebugLogs = true;
    public bool debugConsumedResourceDiseaseRisk = true;
    public bool debugSpread = true;
    public bool debugRecovery = true;
    public bool debugDeath = true;

    [Header("Runtime - Read Only")]
    [SerializeField] private List<IndividualDiseaseState> activeIndividualDiseases = new();

    private readonly Dictionary<string, DiseaseDefinitionSO> _diseaseById =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, PathogenCauseDefinitionSO> _causeByType =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, List<IndividualDiseaseState>> _statesByTargetKey =
        new(StringComparer.Ordinal);

    private readonly Dictionary<string, Dictionary<string, int>> _immunityTurnsByTargetKey =
        new(StringComparer.Ordinal);

    private readonly List<Individual> _tmpAlivePeople = new(256);
    private readonly List<Individual> _tmpDiseaseExposureTargets = new(64);
    private readonly List<Individual> _tmpDiseaseExposureCandidates = new(256);
    private readonly List<TileCoord> _tmpEnvironmentalDiseaseCells = new(16);
    private readonly List<string> _tmpEnvironmentalDiseaseWorkerIds = new(16);
    private readonly List<TileCoord> _tmpBuildingDiseaseCells = new(16);
    private readonly List<string> _tmpBuildingDiseaseTargetIds = new(64);
    private readonly List<string> _tmpVirusContactIds = new(64);
    private readonly List<Individual> _tmpVirusVictimCandidates = new(64);
    private readonly List<PlayerDiseaseSummary> _tmpPlayerDiseaseSummaries = new();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        RebuildDefinitionCache();
        RebuildRuntimeIndex();
    }

    private void OnEnable()
    {
        if (processOnEndOfTurn)
            TurnSystem.SubscribeToEndOfTurn(ProcessDiseaseTurn);
    }

    private void OnDisable()
    {
        if (processOnEndOfTurn)
            TurnSystem.UnsubscribeFromEndOfTurn(ProcessDiseaseTurn);
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
            RebuildDefinitionCache();
    }

    public void RebuildDefinitionCache()
    {
        _diseaseById.Clear();

        for (int i = 0; i < diseaseDefinitions.Count; i++)
        {
            DiseaseDefinitionSO def = diseaseDefinitions[i];
            if (def == null || string.IsNullOrWhiteSpace(def.diseaseId))
                continue;

            if (!_diseaseById.ContainsKey(def.diseaseId))
                _diseaseById.Add(def.diseaseId, def);
        }

        _causeByType.Clear();

        for (int i = 0; i < causeDefinitions.Count; i++)
        {
            PathogenCauseDefinitionSO cause = causeDefinitions[i];
            if (cause == null)
                continue;

            string key = cause.causeType.ToString();

            if (!_causeByType.ContainsKey(key))
                _causeByType.Add(key, cause);
        }
    }

    private void RebuildRuntimeIndex()
    {
        _statesByTargetKey.Clear();

        for (int i = 0; i < activeIndividualDiseases.Count; i++)
        {
            IndividualDiseaseState state = activeIndividualDiseases[i];
            if (state == null || string.IsNullOrWhiteSpace(state.targetId))
                continue;

            string key = DiseaseTargetKey.Build(DiseaseTargetType.Individual, state.targetId);

            if (!_statesByTargetKey.TryGetValue(key, out List<IndividualDiseaseState> list))
            {
                list = new List<IndividualDiseaseState>();
                _statesByTargetKey[key] = list;
            }

            list.Add(state);
        }
    }

    public DiseaseDefinitionSO GetDiseaseDefinition(string diseaseId)
    {
        if (string.IsNullOrWhiteSpace(diseaseId))
            return null;

        _diseaseById.TryGetValue(diseaseId, out DiseaseDefinitionSO def);
        return def;
    }

    public bool TryInfectRandomPopulationMember(
        DiseaseDefinitionSO disease,
        float infectionChance01 = 1f,
        DiseaseExposureInfo exposure = null)
    {
        Individual person = PickRandomAliveIndividual();
        if (person == null)
            return false;

        return TryInfectIndividual(person, disease, infectionChance01, exposure);
    }

    public bool TryInfectRandomPopulationMember(
        string diseaseId,
        float infectionChance01 = 1f,
        DiseaseExposureInfo exposure = null)
    {
        DiseaseDefinitionSO disease = GetDiseaseDefinition(diseaseId);
        return TryInfectRandomPopulationMember(disease, infectionChance01, exposure);
    }

    public bool TryInfectIndividual(
        Individual person,
        DiseaseDefinitionSO disease,
        float infectionChance01 = 1f,
        DiseaseExposureInfo exposure = null)
    {
        if (person == null)
            return false;

        IndividualDiseaseTarget target = new IndividualDiseaseTarget(person);
        return TryInfectDiseaseTarget(target, disease, infectionChance01, exposure);
    }

    public bool TryInfectDiseaseTarget(
    IDiseaseTarget target,
    DiseaseDefinitionSO disease,
    float infectionChance01 = 1f,
    DiseaseExposureInfo exposure = null)
    {
        if (target == null || disease == null)
            return false;

        if (target.TargetType != DiseaseTargetType.Individual)
        {
            Log($"[DiseaseManager] Target type {target.TargetType} is reserved for later group/pool disease tracking.");
            return false;
        }

        if (!target.CanReceiveDisease(disease))
            return false;

        string targetKey = DiseaseTargetKey.Build(target.TargetType, target.TargetId);

        if (HasDiseaseImmunity(targetKey, disease.diseaseId))
        {
            Log($"[DiseaseManager] Infection blocked by immunity. Target={targetKey}, Disease={disease.displayName}");
            return false;
        }

        float exposureStrength = exposure != null
            ? Mathf.Clamp01(exposure.exposureStrength01)
            : 1f;

        float finalChance = Mathf.Clamp01(infectionChance01 * exposureStrength);

        if (!Roll(finalChance))
            return false;

        IndividualDiseaseState existing = FindState(targetKey, disease.diseaseId);

        if (existing != null)
        {
            existing.turnsRemaining = Mathf.Max(existing.turnsRemaining, disease.RollDuration());
            existing.severity01 = Mathf.Max(existing.severity01, exposureStrength);

            if (exposure != null && exposure.inheritVirusStrain)
            {
                existing.CopyVirusStrainFromExposure(exposure);
                existing.strainContagionMultiplier = Mathf.Max(
                    existing.strainContagionMultiplier,
                    exposure.inheritedStrainContagionMultiplier);
            }

            MarkDiseaseSaveDirty();

            Log(
                $"[DiseaseManager] Refreshed existing disease. " +
                $"Target={targetKey}, Disease={existing.GetDisplayName(disease)}");

            return true;
        }

        DiseaseSourceType sourceType = exposure != null
            ? exposure.sourceType
            : DiseaseSourceType.Unknown;

        string sourceId = exposure != null
            ? exposure.sourceId
            : null;

        IndividualDiseaseState state = new IndividualDiseaseState(
            target.TargetId,
            disease.diseaseId,
            disease.RollDuration(),
            Mathf.Max(0.1f, exposureStrength),
            sourceType,
            sourceId,
            disease.contagious
        );

        if (exposure != null && exposure.inheritVirusStrain)
        {
            state.CopyVirusStrainFromExposure(exposure);
        }
        else
        {
            state.strainContagionMultiplier = 1f;
        }

        bool isFirstCase = !HasAnyActiveCaseOfDisease(disease.diseaseId);

        activeIndividualDiseases.Add(state);

        if (!_statesByTargetKey.TryGetValue(targetKey, out List<IndividualDiseaseState> list))
        {
            list = new List<IndividualDiseaseState>();
            _statesByTargetKey[targetKey] = list;
        }

        list.Add(state);

        MarkDiseaseSaveDirty();

        Log(
            $"[DiseaseManager] Infected target. " +
            $"Target={targetKey}, Disease={state.GetDisplayName(disease)}, Source={sourceType}, Chance={finalChance:F2}");

        if (isFirstCase)
            PostDiseaseOutbreakNotification(disease);

        return true;
    }

    public int TryApplyConsumedResourceDiseaseRisk(
    ResourceDefinition consumedResource,
    int unitsConsumed,
    float pointsConsumed,
    bool wasNutrition,
    bool wasHydration)
    {
        if (consumedResource == null)
            return 0;

        if (unitsConsumed <= 0 || pointsConsumed <= 0f)
            return 0;

        if (consumedResourceDiseaseRisks == null || consumedResourceDiseaseRisks.Count == 0)
            return 0;

        GeneralPopulationManager general = GeneralPopulationManager.Instance;
        float pointsPerPersonScale = general != null
            ? Mathf.Max(0.0001f, general.pointsPerPersonScale)
            : 1f;

        int totalInfections = 0;

        for (int i = 0; i < consumedResourceDiseaseRisks.Count; i++)
        {
            ConsumedResourceDiseaseRisk risk = consumedResourceDiseaseRisks[i];

            if (risk == null)
                continue;

            if (!risk.Matches(consumedResource))
                continue;

            if (!risk.MatchesConsumptionMode(wasNutrition, wasHydration))
                continue;

            if (risk.disease == null)
            {
                if (debugConsumedResourceDiseaseRisk)
                {
                    //Debug.LogWarning(
                        //$"[DiseaseManager] Resource disease risk matched '{consumedResource.resourceID}', " +
                        //$"but no disease was assigned.");
                }

                continue;
            }

            float exposedPeopleFloat = risk.CalculateExposedPeopleFloat(
                pointsConsumed,
                pointsPerPersonScale);

            int peopleToCheck = risk.CalculatePeopleToCheck(exposedPeopleFloat);

            if (peopleToCheck <= 0)
            {
                if (debugConsumedResourceDiseaseRisk)
                {
                    //Debug.Log(
                        //$"[DiseaseManager] Consumed resource disease skipped. " +
                        //$"Resource={consumedResource.resourceID}, " +
                        //$"Points={pointsConsumed:F2}, " +
                        //$"PointsPerPerson={pointsPerPersonScale:F2}, " +
                        //$"ExposedPeopleFloat={exposedPeopleFloat:F3}, " +
                        //$"CalculatedPeople=0");
                }

                continue;
            }

            int targetsFound = PickDistinctDiseaseExposureTargets(
                peopleToCheck,
                risk.disease,
                risk.preferDifferentPeople,
                risk.skipPeopleAlreadyInfectedWithThisDisease,
                _tmpDiseaseExposureTargets);

            int infectionsFromThisRisk = 0;

            for (int p = 0; p < targetsFound; p++)
            {
                Individual person = _tmpDiseaseExposureTargets[p];

                if (person == null || !person.IsAlive)
                    continue;

                float targetExposureStrength01 = risk.CalculateTargetExposureStrength(
                    exposedPeopleFloat,
                    p);

                if (targetExposureStrength01 <= 0f)
                    continue;

                DiseaseExposureInfo exposure = new DiseaseExposureInfo
                {
                    sourceType = risk.sourceType,
                    sourceId = consumedResource.resourceID,
                    exposureStrength01 = targetExposureStrength01,
                    notes =
                        $"Consumed resource disease risk. " +
                        $"Resource={consumedResource.resourceID}, " +
                        $"Units={unitsConsumed}, " +
                        $"Points={pointsConsumed:F2}, " +
                        $"PointsPerPerson={pointsPerPersonScale:F2}, " +
                        $"ExposedPeopleFloat={exposedPeopleFloat:F3}, " +
                        $"TargetIndex={p}, " +
                        $"TargetExposure={targetExposureStrength01:F3}, " +
                        $"Nutrition={wasNutrition}, Hydration={wasHydration}"
                };

                bool infected = TryInfectIndividual(
                    person,
                    risk.disease,
                    risk.infectionChancePerPerson,
                    exposure);

                if (infected)
                {
                    infectionsFromThisRisk++;
                    totalInfections++;
                }
            }

            if (debugConsumedResourceDiseaseRisk)
            {
                string label = string.IsNullOrWhiteSpace(risk.debugLabel)
                    ? consumedResource.resourceID
                    : risk.debugLabel;

                float firstTargetExposure = risk.CalculateTargetExposureStrength(
                    exposedPeopleFloat,
                    0);

                float firstTargetFinalChance =
                    risk.infectionChancePerPerson * firstTargetExposure;

                //Debug.Log(
                    //$"[DiseaseManager] Consumed resource disease roll. " +
                    //$"Resource={label}, " +
                    //$"ResourceID={consumedResource.resourceID}, " +
                    //$"Disease={risk.disease.displayName}, " +
                    //$"Units={unitsConsumed}, " +
                    //$"Points={pointsConsumed:F2}, " +
                    //$"PointsPerPerson={pointsPerPersonScale:F2}, " +
                    //$"ExposedPeopleFloat={exposedPeopleFloat:F3}, " +
                    //$"PeopleWanted={peopleToCheck}, " +
                    //$"PeopleFound={targetsFound}, " +
                    //$"BaseChancePerPerson={risk.infectionChancePerPerson:F2}, " +
                    //$"FirstTargetExposure={firstTargetExposure:F3}, " +
                    //$"FirstTargetFinalChance={firstTargetFinalChance:F3}, " +
                    //$"Infections={infectionsFromThisRisk}");
            }
        }

        _tmpDiseaseExposureTargets.Clear();

        return totalInfections;
    }

    private int PickDistinctDiseaseExposureTargets(
    int wantedCount,
    DiseaseDefinitionSO disease,
    bool preferDifferentPeople,
    bool skipAlreadyInfectedWithDisease,
    List<Individual> outTargets)
    {
        outTargets.Clear();

        if (wantedCount <= 0)
            return 0;

        PlayerFamilySimulationManager family = PlayerFamilySimulationManager.Instance;
        if (family == null)
            return 0;

        IReadOnlyList<Individual> people = family.GetIndividuals();
        if (people == null || people.Count == 0)
            return 0;

        _tmpDiseaseExposureCandidates.Clear();

        for (int i = 0; i < people.Count; i++)
        {
            Individual person = people[i];

            if (person == null || !person.IsAlive)
                continue;

            if (disease != null && skipAlreadyInfectedWithDisease)
            {
                string targetKey = DiseaseTargetKey.Build(DiseaseTargetType.Individual, person.Id);

                if (FindState(targetKey, disease.diseaseId) != null)
                    continue;

                if (HasDiseaseImmunity(targetKey, disease.diseaseId))
                    continue;
            }

            _tmpDiseaseExposureCandidates.Add(person);
        }

        if (_tmpDiseaseExposureCandidates.Count == 0)
            return 0;

        if (!preferDifferentPeople)
        {
            for (int i = 0; i < wantedCount; i++)
            {
                Individual picked = _tmpDiseaseExposureCandidates[
                    UnityEngine.Random.Range(0, _tmpDiseaseExposureCandidates.Count)];

                outTargets.Add(picked);
            }

            return outTargets.Count;
        }

        ShuffleInPlace(_tmpDiseaseExposureCandidates);

        int take = Mathf.Min(wantedCount, _tmpDiseaseExposureCandidates.Count);

        for (int i = 0; i < take; i++)
            outTargets.Add(_tmpDiseaseExposureCandidates[i]);

        return outTargets.Count;
    }

    private void ShuffleInPlace<T>(List<T> list)
    {
        if (list == null || list.Count <= 1)
            return;

        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    public void ProcessDiseaseTurn()
    {
        TickImmunity();

        if (activeIndividualDiseases.Count == 0)
            return;

        for (int i = activeIndividualDiseases.Count - 1; i >= 0; i--)
        {
            IndividualDiseaseState state = activeIndividualDiseases[i];

            if (state == null)
            {
                RemoveStateAt(i);
                continue;
            }

            DiseaseDefinitionSO disease = GetDiseaseDefinition(state.diseaseId);
            if (disease == null)
            {
                RemoveStateAt(i);
                continue;
            }

            Individual person = FindIndividualById(state.targetId);
            if (person == null || !person.IsAlive)
            {
                RemoveStateAt(i);
                continue;
            }

            IndividualDiseaseTarget target = new IndividualDiseaseTarget(person);

            float beforeHealth01 = person.Health01;

            target.ApplyDiseaseEffects(disease, state);

            float afterHealth01 = person.Health01;
            float healthDelta01 = afterHealth01 - beforeHealth01;

            // NEW: individual disease damage must also affect the backing population group average.
            ApplyIndividualHealthDeltaToPopulationGroup(person, healthDelta01, disease, state);

            state.turnsInfected++;
            state.turnsRemaining--;

            bool diedFromHealth = person.Health01 <= 0f;
            bool diedFromRoll = Roll(GetScaledDeathChance(disease, state, person));

            if (diedFromHealth || diedFromRoll)
            {
                KillIndividualFromDisease(person, disease, state);
                RemoveStateAt(i);
                continue;
            }

            bool recovered = state.turnsRemaining <= 0 || Roll(GetScaledRecoveryChance(disease, state, person));

            if (recovered)
            {
                target.RecoverDisease(disease, state);
                GrantImmunityIfNeeded(person.Id, disease);
                RemoveStateAt(i);

                if (debugRecovery)
                    Log($"[DiseaseManager] Recovered. Individual={person.Id}, Disease={disease.displayName}");

                continue;
            }

            if (disease.contagious && state.isContagious)
            {
                if (disease.causeType == PathogenCauseType.Virus && disease.useContextSpreadForVirus)
                {
                    TryMutateVirusState(person, disease, state);
                }
                else
                {
                    TrySpreadFromIndividual(person, disease, state);
                }
            }
        }

        MarkDiseaseSaveDirty();
    }

    private void ApplyIndividualHealthDeltaToPopulationGroup(
        Individual person,
        float individualHealthDelta01,
        DiseaseDefinitionSO disease,
        IndividualDiseaseState state)
    {
        if (person == null)
            return;

        if (Mathf.Abs(individualHealthDelta01) <= 0.00001f)
            return;

        PlayersPopulationManager pop = PlayersPopulationManager.Instance;
        if (pop == null)
            return;

        PopulationGroup group = FindPopulationGroup(person.AggregatedGroupGuid);
        if (group == null || group.count <= 0)
            return;

        // One individual changed by X, so the group average changes by X / group size.
        float groupDelta01 = individualHealthDelta01 / Mathf.Max(1, group.count);

        float beforeGroupHealth = group.averageHealth;
        group.averageHealth = Mathf.Clamp01(group.averageHealth + groupDelta01);

        pop.MarkUIDirty();

        if (enableDebugLogs)
        {
            string diseaseName = disease != null ? disease.displayName : state?.diseaseId;

            //Debug.Log(
                //$"[DiseaseManager] Disease health damage applied. " +
                //$"Individual={person.Id}, " +
                //$"Disease={diseaseName}, " +
                //$"IndividualHealth {person.Health01 - individualHealthDelta01:F3}->{person.Health01:F3}, " +
                //$"Group={group.GroupID}, " +
                //$"GroupHealth {beforeGroupHealth:F3}->{group.averageHealth:F3}, " +
                //$"GroupDelta={groupDelta01:F4}");
        }
    }

    private void TrySpreadFromIndividual(
        Individual sourcePerson,
        DiseaseDefinitionSO disease,
        IndividualDiseaseState sourceState)
    {
        if (sourcePerson == null || disease == null || sourceState == null)
            return;

        float chance = Mathf.Clamp01(
            disease.spreadChancePerTurn * Mathf.Lerp(0.5f, 1.5f, sourceState.severity01));

        if (!Roll(chance))
            return;

        Individual victim = PickRandomAliveIndividual(sourcePerson.Id);
        if (victim == null)
            return;

        DiseaseExposureInfo exposure = new DiseaseExposureInfo
        {
            sourceType = DiseaseSourceType.Unknown,
            sourceId = sourcePerson.Id,
            exposureStrength01 = sourceState.severity01,
            notes = "V1 contagious spread. Later this can become tile/building/group scoped."
        };

        bool infected = TryInfectIndividual(victim, disease, chance, exposure);

        if (infected && debugSpread)
        {
            Log(
                $"[DiseaseManager] Disease spread. " +
                $"From={sourcePerson.Id}, To={victim.Id}, Disease={disease.displayName}");
        }
    }

    private float GetScaledDeathChance(
        DiseaseDefinitionSO disease,
        IndividualDiseaseState state,
        Individual person)
    {
        if (disease == null || person == null)
            return 0f;

        float severity01 = state != null ? state.severity01 : 1f;

        return disease.GetEffectiveDeathChancePerTurn(
            person.AggregatedAgeGroup,
            severity01);
    }

    private void KillIndividualFromDisease(
        Individual person,
        DiseaseDefinitionSO disease,
        IndividualDiseaseState state)
    {
        if (person == null || !person.IsAlive)
            return;

        PlayersPopulationManager pop = PlayersPopulationManager.Instance;

        if (pop != null)
            pop.TryDetachIndividualFromExistingReservations(person.Id, out _);

        person.Health01 = 0f;
        person.IsAlive = false;

        if (pop != null)
        {
            PopulationGroup group = FindPopulationGroup(person.AggregatedGroupGuid);

            if (group != null)
                group.ApplyPopulationLoss(1);

            pop.PruneDeadOrEmptyGroups();
            pop.MarkUIDirty();
        }

        string dName = disease != null ? disease.displayName : (state?.diseaseId ?? "Unknown Disease");
        string pName = !string.IsNullOrWhiteSpace(person.Surname) ? person.Surname : "A citizen";

        if (debugDeath)
            //Debug.LogWarning($"[DiseaseManager] Individual died from disease. Individual={person.Id}, Disease={dName}");

        PostDiseaseDeathNotification(dName, pName);
    }

    private void PostDiseaseDeathNotification(string diseaseName, string surname)
    {
        if (NotificationManager.Instance == null) return;
        string title, message;
        if (NotificationMessageCrafterManager.Instance != null)
            (title, message) = NotificationMessageCrafterManager.Instance.CraftDiseaseKilled(diseaseName, surname);
        else
            (title, message) = ("Death from Disease", $"{surname} has died from {diseaseName}.");
        NotificationManager.Instance.AddNotification(NotificationType.DiseaseKilledPopulation, title, message, true);
    }

    private PopulationGroup FindPopulationGroup(Guid groupId)
    {
        PlayersPopulationManager pop = PlayersPopulationManager.Instance;
        if (pop == null || pop.AllPopulations == null)
            return null;

        for (int i = 0; i < pop.AllPopulations.Count; i++)
        {
            PopulationGroup group = pop.AllPopulations[i];
            if (group != null && group.GroupID == groupId)
                return group;
        }

        return null;
    }

    private Individual PickRandomAliveIndividual(string excludeIndividualId = null)
    {
        _tmpAlivePeople.Clear();

        PlayerFamilySimulationManager family = PlayerFamilySimulationManager.Instance;
        if (family == null)
            return null;

        IReadOnlyList<Individual> people = family.GetIndividuals();
        if (people == null || people.Count == 0)
            return null;

        for (int i = 0; i < people.Count; i++)
        {
            Individual person = people[i];

            if (person == null || !person.IsAlive)
                continue;

            if (!string.IsNullOrEmpty(excludeIndividualId) && person.Id == excludeIndividualId)
                continue;

            _tmpAlivePeople.Add(person);
        }

        if (_tmpAlivePeople.Count == 0)
            return null;

        return _tmpAlivePeople[UnityEngine.Random.Range(0, _tmpAlivePeople.Count)];
    }

    private Individual FindIndividualById(string individualId)
    {
        if (string.IsNullOrWhiteSpace(individualId))
            return null;

        PlayerFamilySimulationManager family = PlayerFamilySimulationManager.Instance;
        if (family == null)
            return null;

        IReadOnlyList<Individual> people = family.GetIndividuals();
        if (people == null)
            return null;

        for (int i = 0; i < people.Count; i++)
        {
            Individual person = people[i];
            if (person != null && person.Id == individualId)
                return person;
        }

        return null;
    }

    private IndividualDiseaseState FindState(string targetKey, string diseaseId)
    {
        if (string.IsNullOrWhiteSpace(targetKey) || string.IsNullOrWhiteSpace(diseaseId))
            return null;

        if (!_statesByTargetKey.TryGetValue(targetKey, out List<IndividualDiseaseState> list))
            return null;

        for (int i = 0; i < list.Count; i++)
        {
            IndividualDiseaseState state = list[i];
            if (state != null && string.Equals(state.diseaseId, diseaseId, StringComparison.OrdinalIgnoreCase))
                return state;
        }

        return null;
    }

    private void RemoveStateAt(int index)
    {
        if (index < 0 || index >= activeIndividualDiseases.Count)
            return;

        IndividualDiseaseState state = activeIndividualDiseases[index];

        if (state != null && !string.IsNullOrWhiteSpace(state.targetId))
        {
            string targetKey = DiseaseTargetKey.Build(DiseaseTargetType.Individual, state.targetId);

            if (_statesByTargetKey.TryGetValue(targetKey, out List<IndividualDiseaseState> list))
            {
                list.Remove(state);

                if (list.Count == 0)
                    _statesByTargetKey.Remove(targetKey);
            }
        }

        activeIndividualDiseases.RemoveAt(index);
    }

    private bool HasDiseaseImmunity(string targetKey, string diseaseId)
    {
        if (string.IsNullOrWhiteSpace(targetKey) || string.IsNullOrWhiteSpace(diseaseId))
            return false;

        if (!_immunityTurnsByTargetKey.TryGetValue(targetKey, out Dictionary<string, int> diseaseMap))
            return false;

        return diseaseMap.TryGetValue(diseaseId, out int turns) && turns > 0;
    }

    private void GrantImmunityIfNeeded(string individualId, DiseaseDefinitionSO disease)
    {
        if (string.IsNullOrWhiteSpace(individualId) || disease == null)
            return;

        if (!disease.grantsTemporaryImmunity || disease.immunityTurns <= 0)
            return;

        string targetKey = DiseaseTargetKey.Build(DiseaseTargetType.Individual, individualId);

        if (!_immunityTurnsByTargetKey.TryGetValue(targetKey, out Dictionary<string, int> diseaseMap))
        {
            diseaseMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _immunityTurnsByTargetKey[targetKey] = diseaseMap;
        }

        diseaseMap[disease.diseaseId] = disease.immunityTurns;
    }

    private void TickImmunity()
    {
        if (_immunityTurnsByTargetKey.Count == 0)
            return;

        List<string> emptyTargets = null;

        foreach (KeyValuePair<string, Dictionary<string, int>> targetEntry in _immunityTurnsByTargetKey)
        {
            Dictionary<string, int> diseaseMap = targetEntry.Value;

            if (diseaseMap == null || diseaseMap.Count == 0)
            {
                emptyTargets ??= new List<string>();
                emptyTargets.Add(targetEntry.Key);
                continue;
            }

            List<string> expiredDiseases = null;
            List<string> diseaseKeys = new List<string>(diseaseMap.Keys);

            for (int i = 0; i < diseaseKeys.Count; i++)
            {
                string diseaseId = diseaseKeys[i];
                int remaining = diseaseMap[diseaseId] - 1;

                if (remaining <= 0)
                {
                    expiredDiseases ??= new List<string>();
                    expiredDiseases.Add(diseaseId);
                }
                else
                {
                    diseaseMap[diseaseId] = remaining;
                }
            }

            if (expiredDiseases != null)
            {
                for (int i = 0; i < expiredDiseases.Count; i++)
                    diseaseMap.Remove(expiredDiseases[i]);
            }

            if (diseaseMap.Count == 0)
            {
                emptyTargets ??= new List<string>();
                emptyTargets.Add(targetEntry.Key);
            }
        }

        if (emptyTargets != null)
        {
            for (int i = 0; i < emptyTargets.Count; i++)
                _immunityTurnsByTargetKey.Remove(emptyTargets[i]);
        }
    }

    private bool Roll(float chance01)
    {
        return UnityEngine.Random.value <= Mathf.Clamp01(chance01);
    }

    private void Log(string message)
    {
        if (enableDebugLogs)
            //Debug.Log(message);
    }

    public bool HasActiveDiseaseInGroup(Guid groupId)
    {
        if (activeIndividualDiseases == null || activeIndividualDiseases.Count == 0)
            return false;

        PlayerFamilySimulationManager family = PlayerFamilySimulationManager.Instance;
        if (family == null)
            return false;

        IReadOnlyList<Individual> people = family.GetIndividuals();
        if (people == null || people.Count == 0)
            return false;

        for (int i = 0; i < activeIndividualDiseases.Count; i++)
        {
            IndividualDiseaseState state = activeIndividualDiseases[i];
            if (state == null || string.IsNullOrWhiteSpace(state.targetId))
                continue;

            for (int p = 0; p < people.Count; p++)
            {
                Individual person = people[p];

                if (person == null || !person.IsAlive)
                    continue;

                if (person.Id != state.targetId)
                    continue;

                if (person.AggregatedGroupGuid == groupId)
                    return true;
            }
        }

        return false;
    }

    private float GetScaledRecoveryChance(
        DiseaseDefinitionSO disease,
        IndividualDiseaseState state,
        Individual person)
    {
        if (disease == null || person == null)
            return 0f;

        float severity01 = state != null ? state.severity01 : 1f;

        return disease.GetEffectiveRecoveryChancePerTurn(
            person.AggregatedAgeGroup,
            severity01);
    }

    public float GetTaskFailureChanceAddPercentForIndividuals(
    IReadOnlyList<string> individualIds,
    string taskType = null,
    string taskName = null)
    {
        return GetTaskFailureChanceAdd01ForIndividuals(individualIds, taskType, taskName) * 100f;
    }

    public float GetTaskFailureChanceAdd01ForIndividuals(
        IReadOnlyList<string> individualIds,
        string taskType = null,
        string taskName = null)
    {
        if (!enableDiseaseTaskFailureEffects)
            return 0f;

        if (individualIds == null || individualIds.Count == 0)
            return 0f;

        float totalAdd01 = 0f;
        int sickWorkerCount = 0;

        for (int i = 0; i < individualIds.Count; i++)
        {
            string individualId = individualIds[i];

            float individualAdd01 = GetTaskFailureChanceAdd01ForIndividual(individualId);

            if (individualAdd01 > 0f)
            {
                sickWorkerCount++;
                totalAdd01 += individualAdd01;
            }
        }

        totalAdd01 = Mathf.Clamp(totalAdd01, 0f, maxDiseaseTaskFailureChanceAdd01);

        if (debugTaskFailureEffects && totalAdd01 > 0f)
        {
            //Debug.Log(
                //$"[DiseaseManager] Disease task failure modifier. " +
                //$"TaskType={taskType}, " +
                //$"Task={taskName}, " +
                //$"Workers={individualIds.Count}, " +
                //$"SickWorkers={sickWorkerCount}, " +
                //$"FailureAdd={totalAdd01 * 100f:F1}%");
        }

        return totalAdd01;
    }

    private float GetTaskFailureChanceAdd01ForIndividual(string individualId)
    {
        if (string.IsNullOrWhiteSpace(individualId))
            return 0f;

        string targetKey = DiseaseTargetKey.Build(DiseaseTargetType.Individual, individualId);

        if (!_statesByTargetKey.TryGetValue(targetKey, out List<IndividualDiseaseState> states))
            return 0f;

        if (states == null || states.Count == 0)
            return 0f;

        Individual person = FindIndividualById(individualId);

        AgeGroup ageGroup = person != null
            ? person.AggregatedAgeGroup
            : AgeGroup.Adult;

        float totalAdd01 = 0f;

        for (int i = 0; i < states.Count; i++)
        {
            IndividualDiseaseState state = states[i];
            if (state == null)
                continue;

            DiseaseDefinitionSO disease = GetDiseaseDefinition(state.diseaseId);
            if (disease == null)
                continue;

            float severity01 = Mathf.Clamp01(state.severity01);

            // MAIN PART:
            // Uses DiseaseDefinitionSO.taskFailureChanceAdd.
            float diseaseTaskAdd01 = disease.GetEffectiveTaskFailureChanceAdd(
                ageGroup,
                severity01);

            if (disease.preventsWork)
                diseaseTaskAdd01 += preventsWorkFailureChanceAdd01;

            totalAdd01 += diseaseTaskAdd01;
        }

        return Mathf.Clamp01(totalAdd01);
    }

    public float GetWorkEfficiencyMultiplierForIndividuals(
    IReadOnlyList<string> individualIds,
    string taskType = null,
    string taskName = null)
    {
        if (!enableDiseaseWorkEfficiencyEffects)
            return 1f;

        if (individualIds == null || individualIds.Count == 0)
            return 1f;

        float totalMultiplier = 0f;
        int countedWorkers = 0;
        int sickWorkers = 0;

        for (int i = 0; i < individualIds.Count; i++)
        {
            string individualId = individualIds[i];

            if (string.IsNullOrWhiteSpace(individualId))
                continue;

            float workerMultiplier = GetWorkEfficiencyMultiplierForIndividual(individualId);

            if (workerMultiplier < 0.999f)
                sickWorkers++;

            totalMultiplier += workerMultiplier;
            countedWorkers++;
        }

        if (countedWorkers <= 0)
            return 1f;

        float finalMultiplier = Mathf.Clamp01(totalMultiplier / countedWorkers);

        if (debugWorkEfficiencyEffects && sickWorkers > 0)
        {
            //Debug.Log(
                //$"[DiseaseManager] Disease work efficiency modifier. " +
                //$"TaskType={taskType}, " +
                //$"Task={taskName}, " +
                //$"Workers={countedWorkers}, " +
                //$"SickWorkers={sickWorkers}, " +
                //$"OutputMultiplier={finalMultiplier:F3}");
        }

        return finalMultiplier;
    }

    private float GetWorkEfficiencyMultiplierForIndividual(string individualId)
    {
        if (string.IsNullOrWhiteSpace(individualId))
            return 1f;

        string targetKey = DiseaseTargetKey.Build(DiseaseTargetType.Individual, individualId);

        if (!_statesByTargetKey.TryGetValue(targetKey, out List<IndividualDiseaseState> states))
            return 1f;

        if (states == null || states.Count == 0)
            return 1f;

        Individual person = FindIndividualById(individualId);

        AgeGroup ageGroup = person != null
            ? person.AggregatedAgeGroup
            : AgeGroup.Adult;

        float finalMultiplier = 1f;

        for (int i = 0; i < states.Count; i++)
        {
            IndividualDiseaseState state = states[i];
            if (state == null)
                continue;

            DiseaseDefinitionSO disease = GetDiseaseDefinition(state.diseaseId);
            if (disease == null)
                continue;

            float severity01 = Mathf.Clamp01(state.severity01);

            float diseaseMultiplier = disease.GetEffectiveWorkEfficiencyMultiplier(
                ageGroup,
                severity01);

            finalMultiplier *= diseaseMultiplier;
        }

        return Mathf.Clamp01(finalMultiplier);
    }

    public int TryApplyEnvironmentalDiseaseRiskForTaskResult(
    EnvironmentControl env,
    string reservationId,
    DiseaseTaskResultType taskType,
    float infectionChanceMultiplier = 1f,
    float exposureStrengthMultiplier = 1f,
    int maxTargetsOverride = 0)
    {
        if (env == null)
            return 0;

        if (string.IsNullOrWhiteSpace(reservationId))
            return 0;

        if (environmentalDiseaseRisks == null || environmentalDiseaseRisks.Count == 0)
            return 0;

        PlayersPopulationManager pop = PlayersPopulationManager.Instance;
        if (pop == null)
            return 0;

        if (!pop.TryGetReservedIndividualIds(reservationId, out var reservedIds) ||
            reservedIds == null ||
            reservedIds.Count == 0)
        {
            return 0;
        }

        WeatherGridManager weatherGrid = WeatherGridManager.Instance;
        CloudSimulationSystem cloudSystem = CloudSimulationSystem.Instance;
        RainSimulationSystem rainSystem = RainSimulationSystem.Instance;

        _tmpEnvironmentalDiseaseCells.Clear();

        bool hasWeatherSample = false;
        WeatherAreaSample sample = default;

        if (weatherGrid != null && weatherGrid.IsInitialized)
        {
            weatherGrid.TryGetEnvironmentCoveredCells(env, _tmpEnvironmentalDiseaseCells);
            hasWeatherSample = weatherGrid.TryGetEnvironmentWeatherSample(env, out sample);
        }

        _tmpEnvironmentalDiseaseWorkerIds.Clear();

        for (int i = 0; i < reservedIds.Count; i++)
        {
            string id = reservedIds[i];
            if (!string.IsNullOrWhiteSpace(id))
                _tmpEnvironmentalDiseaseWorkerIds.Add(id);
        }

        if (_tmpEnvironmentalDiseaseWorkerIds.Count == 0)
            return 0;

        ShuffleInPlace(_tmpEnvironmentalDiseaseWorkerIds);

        int totalInfections = 0;

        for (int r = 0; r < environmentalDiseaseRisks.Count; r++)
        {
            EnvironmentalDiseaseRisk risk = environmentalDiseaseRisks[r];

            if (risk == null || risk.disease == null)
                continue;

            if (!risk.MatchesTask(taskType))
                continue;

            if (!risk.MatchesEnvironment(env))
                continue;

            if (!risk.TryEvaluateWeather(
                    hasWeatherSample,
                    sample,
                    _tmpEnvironmentalDiseaseCells,
                    cloudSystem,
                    rainSystem,
                    out float weatherStrength01,
                    out DiseaseSourceType resolvedSourceType,
                    out string weatherSummary))
            {
                continue;
            }

            float infectionChance01 =
                risk.GetFinalInfectionChance01(weatherStrength01) *
                risk.GetResultInfectionChanceMultiplier(taskType) *
                Mathf.Max(0f, infectionChanceMultiplier);

            float exposureStrength01 =
                risk.GetFinalExposureStrength01(weatherStrength01) *
                risk.GetResultExposureStrengthMultiplier(taskType) *
                Mathf.Max(0f, exposureStrengthMultiplier);

            infectionChance01 = Mathf.Clamp01(infectionChance01);
            exposureStrength01 = Mathf.Clamp01(exposureStrength01);

            if (infectionChance01 <= 0f || exposureStrength01 <= 0f)
                continue;

            int maxWorkers = risk.GetMaxWorkersToCheckForResult(
                taskType,
                _tmpEnvironmentalDiseaseWorkerIds.Count);

            if (maxTargetsOverride > 0)
                maxWorkers = Mathf.Min(maxWorkers, maxTargetsOverride);

            maxWorkers = Mathf.Clamp(maxWorkers, 0, _tmpEnvironmentalDiseaseWorkerIds.Count);

            int infectionsFromRisk = 0;

            for (int i = 0; i < maxWorkers; i++)
            {
                string workerId = _tmpEnvironmentalDiseaseWorkerIds[i];
                Individual person = FindIndividualById(workerId);

                if (person == null || !person.IsAlive)
                    continue;

                DiseaseExposureInfo exposure = new DiseaseExposureInfo
                {
                    sourceType = resolvedSourceType,
                    sourceId = env.EnvironmentID,
                    exposureStrength01 = exposureStrength01,
                    notes =
                        $"Environmental disease from failed {taskType}. " +
                        $"Env={env.name}, " +
                        $"EnvironmentType={env.environmentType}, " +
                        $"TileType={env.environmentTileType}, " +
                        $"WeatherStrength={weatherStrength01:F2}. " +
                        weatherSummary
                };

                if (_tmpEnvironmentalDiseaseCells.Count > 0)
                {
                    exposure.hasSourceTile = true;
                    exposure.sourceTileCoord = _tmpEnvironmentalDiseaseCells[0];
                }

                bool infected = TryInfectIndividual(
                    person,
                    risk.disease,
                    infectionChance01,
                    exposure);

                if (infected)
                {
                    infectionsFromRisk++;
                    totalInfections++;
                }
            }

            if (debugEnvironmentalDiseaseRisk)
            {
                string label = string.IsNullOrWhiteSpace(risk.debugLabel)
                    ? risk.disease.displayName
                    : risk.debugLabel;

                //Debug.Log(
                    //$"[DiseaseManager] Environmental disease risk rolled. " +
                    //$"Task={taskType}, " +
                    //$"Risk={label}, " +
                    //$"Env={env.name}, " +
                    //$"EnvType={env.environmentType}, " +
                    //$"TileType={env.environmentTileType}, " +
                    //$"WorkersChecked={maxWorkers}, " +
                    //$"Chance={infectionChance01:F3}, " +
                    //$"Exposure={exposureStrength01:F3}, " +
                    //$"WeatherStrength={weatherStrength01:F3}, " +
                    //$"Infections={infectionsFromRisk}, " +
                    //$"Weather={weatherSummary}");
            }
        }

        return totalInfections;
    }

    public int TryApplyEnvironmentalDiseaseRiskForBuildingComponent(
    Component buildingComponent,
    IReadOnlyList<string> targetIndividualIds,
    DiseaseTaskResultType exposureType,
    float infectionChanceMultiplier = 1f,
    float exposureStrengthMultiplier = 1f,
    int maxTargetsOverride = 0)
    {
        if (buildingComponent == null)
            return 0;

        if (targetIndividualIds == null || targetIndividualIds.Count == 0)
            return 0;

        if (environmentalDiseaseRisks == null || environmentalDiseaseRisks.Count == 0)
            return 0;

        WeatherGridManager weatherGrid = WeatherGridManager.Instance;
        if (weatherGrid == null || !weatherGrid.IsInitialized)
            return 0;

        WorldBuildingManager.Record record = FindBuildingRecordForComponent(buildingComponent);
        if (record == null || string.IsNullOrWhiteSpace(record.instanceId))
            return 0;

        _tmpBuildingDiseaseCells.Clear();

        bool hasCells = weatherGrid.TryGetBuildingCoveredCells(record, _tmpBuildingDiseaseCells);
        bool hasWeatherSample = weatherGrid.TryGetBuildingWeatherSample(record, out WeatherAreaSample sample);

        if (!hasCells && !hasWeatherSample)
            return 0;

        CloudSimulationSystem cloudSystem = CloudSimulationSystem.Instance;
        RainSimulationSystem rainSystem = RainSimulationSystem.Instance;

        _tmpBuildingDiseaseTargetIds.Clear();

        for (int i = 0; i < targetIndividualIds.Count; i++)
        {
            string id = targetIndividualIds[i];
            if (!string.IsNullOrWhiteSpace(id) && !_tmpBuildingDiseaseTargetIds.Contains(id))
                _tmpBuildingDiseaseTargetIds.Add(id);
        }

        if (_tmpBuildingDiseaseTargetIds.Count == 0)
            return 0;

        ShuffleInPlace(_tmpBuildingDiseaseTargetIds);

        int totalInfections = 0;

        for (int r = 0; r < environmentalDiseaseRisks.Count; r++)
        {
            EnvironmentalDiseaseRisk risk = environmentalDiseaseRisks[r];

            if (risk == null || risk.disease == null)
                continue;

            if (!risk.MatchesTask(exposureType))
                continue;

            // Building exposure does not use EnvironmentControl type filters.
            // It only uses weather / cloud / ash / acid-rain filters.
            if (!risk.TryEvaluateWeather(
                    hasWeatherSample,
                    sample,
                    _tmpBuildingDiseaseCells,
                    cloudSystem,
                    rainSystem,
                    out float weatherStrength01,
                    out DiseaseSourceType resolvedSourceType,
                    out string weatherSummary))
            {
                continue;
            }

            float infectionChance01 =
                risk.GetFinalInfectionChance01(weatherStrength01) *
                risk.GetResultInfectionChanceMultiplier(exposureType) *
                Mathf.Max(0f, infectionChanceMultiplier);

            float exposureStrength01 =
                risk.GetFinalExposureStrength01(weatherStrength01) *
                risk.GetResultExposureStrengthMultiplier(exposureType) *
                Mathf.Max(0f, exposureStrengthMultiplier);

            infectionChance01 = Mathf.Clamp01(infectionChance01);
            exposureStrength01 = Mathf.Clamp01(exposureStrength01);

            if (infectionChance01 <= 0f || exposureStrength01 <= 0f)
                continue;

            int maxTargets = risk.GetMaxWorkersToCheckForResult(
                exposureType,
                _tmpBuildingDiseaseTargetIds.Count);

            if (maxTargetsOverride > 0)
                maxTargets = Mathf.Min(maxTargets, maxTargetsOverride);

            maxTargets = Mathf.Clamp(maxTargets, 0, _tmpBuildingDiseaseTargetIds.Count);

            int infectionsFromRisk = 0;

            for (int i = 0; i < maxTargets; i++)
            {
                Individual person = FindIndividualById(_tmpBuildingDiseaseTargetIds[i]);

                if (person == null || !person.IsAlive)
                    continue;

                DiseaseExposureInfo exposure = new DiseaseExposureInfo
                {
                    sourceType = resolvedSourceType,
                    sourceId = record.instanceId,
                    exposureStrength01 = exposureStrength01,
                    notes =
                        $"Building weather disease exposure. " +
                        $"Context={exposureType}, " +
                        $"Building={buildingComponent.name}, " +
                        $"WeatherStrength={weatherStrength01:F2}. " +
                        weatherSummary
                };

                if (_tmpBuildingDiseaseCells.Count > 0)
                {
                    exposure.hasSourceTile = true;
                    exposure.sourceTileCoord = _tmpBuildingDiseaseCells[0];
                }

                bool infected = TryInfectIndividual(
                    person,
                    risk.disease,
                    infectionChance01,
                    exposure);

                if (infected)
                {
                    infectionsFromRisk++;
                    totalInfections++;
                }
            }

            if (debugEnvironmentalDiseaseRisk)
            {
                string label = string.IsNullOrWhiteSpace(risk.debugLabel)
                    ? risk.disease.displayName
                    : risk.debugLabel;

                //Debug.Log(
                    //$"[DiseaseManager] Building weather disease risk rolled. " +
                    //$"Context={exposureType}, " +
                    //$"Risk={label}, " +
                    //$"Building={buildingComponent.name}, " +
                    //$"TargetsChecked={maxTargets}, " +
                    //$"Chance={infectionChance01:F3}, " +
                    //$"Exposure={exposureStrength01:F3}, " +
                    //$"WeatherStrength={weatherStrength01:F3}, " +
                    //$"Infections={infectionsFromRisk}, " +
                    //$"Weather={weatherSummary}");
            }
        }

        return totalInfections;
    }

    private WorldBuildingManager.Record FindBuildingRecordForComponent(Component component)
    {
        if (component == null)
            return null;

        WorldBuildingManager wbm = WorldBuildingManager.Instance;
        if (wbm == null)
            return null;

        IReadOnlyList<WorldBuildingManager.Record> records = wbm.GetAll();
        if (records == null)
            return null;

        Transform componentTransform = component.transform;

        for (int i = 0; i < records.Count; i++)
        {
            WorldBuildingManager.Record record = records[i];

            if (record == null || record.instance == null)
                continue;

            Transform buildingTransform = record.instance.transform;

            if (buildingTransform == componentTransform)
                return record;

            if (componentTransform.IsChildOf(buildingTransform))
                return record;

            if (buildingTransform.IsChildOf(componentTransform))
                return record;
        }

        return null;
    }

    public int TrySpreadContagiousVirusesWithinGroup(
    IReadOnlyList<string> individualIds,
    string contextType,
    string contextName,
    float contactMultiplier = 1f)
    {
        if (!enableVirusContextSpread)
            return 0;

        if (individualIds == null || individualIds.Count <= 1)
            return 0;

        _tmpVirusContactIds.Clear();

        for (int i = 0; i < individualIds.Count; i++)
        {
            string id = individualIds[i];

            if (string.IsNullOrWhiteSpace(id))
                continue;

            if (_tmpVirusContactIds.Contains(id))
                continue;

            Individual person = FindIndividualById(id);
            if (person == null || !person.IsAlive)
                continue;

            _tmpVirusContactIds.Add(id);
        }

        if (_tmpVirusContactIds.Count <= 1)
            return 0;

        int totalSpread = 0;

        for (int i = 0; i < _tmpVirusContactIds.Count; i++)
        {
            string sourceId = _tmpVirusContactIds[i];
            Individual sourcePerson = FindIndividualById(sourceId);

            if (sourcePerson == null || !sourcePerson.IsAlive)
                continue;

            string sourceKey = DiseaseTargetKey.Build(DiseaseTargetType.Individual, sourceId);

            if (!_statesByTargetKey.TryGetValue(sourceKey, out List<IndividualDiseaseState> sourceStates) ||
                sourceStates == null ||
                sourceStates.Count == 0)
            {
                continue;
            }

            for (int s = 0; s < sourceStates.Count; s++)
            {
                IndividualDiseaseState sourceState = sourceStates[s];

                if (sourceState == null)
                    continue;

                if (!sourceState.isContagious || sourceState.isRecovering)
                    continue;

                DiseaseDefinitionSO disease = GetDiseaseDefinition(sourceState.diseaseId);
                if (disease == null)
                    continue;

                if (disease.causeType != PathogenCauseType.Virus)
                    continue;

                if (!disease.contagious)
                    continue;

                int attempts = Mathf.Min(
                    Mathf.Max(1, disease.maxSpreadAttemptsPerSourcePerContext),
                    Mathf.Max(1, maxVirusSpreadAttemptsPerSource));

                for (int a = 0; a < attempts; a++)
                {
                    if (!TryPickVirusVictimFromContactGroup(
                            _tmpVirusContactIds,
                            sourcePerson.Id,
                            disease,
                            out Individual victim))
                    {
                        continue;
                    }

                    float contextMultiplier = contactMultiplier;

                    if (string.Equals(contextType, "Shelter", StringComparison.OrdinalIgnoreCase))
                        contextMultiplier *= globalShelterVirusSpreadMultiplier * disease.shelterSpreadMultiplier;
                    else
                        contextMultiplier *= globalTaskGroupVirusSpreadMultiplier * disease.taskGroupSpreadMultiplier;

                    float severityScale = disease.GetSeverityScale(sourceState.severity01);

                    float spreadChance01 = disease.spreadChancePerTurn *
                                            severityScale *
                                            Mathf.Max(0f, sourceState.strainContagionMultiplier) *
                                            Mathf.Max(0f, contextMultiplier);

                    spreadChance01 = Mathf.Clamp01(spreadChance01);

                    DiseaseExposureInfo exposure = new DiseaseExposureInfo
                    {
                        sourceType = DiseaseSourceType.Unknown,
                        sourceId = sourcePerson.Id,
                        exposureStrength01 = Mathf.Clamp01(sourceState.severity01),
                        notes =
                            $"Virus context spread. " +
                            $"Context={contextType}, " +
                            $"Name={contextName}, " +
                            $"Source={sourcePerson.Id}, " +
                            $"SpreadChance={spreadChance01:F3}, " +
                            $"StrainContagion={sourceState.strainContagionMultiplier:F2}"
                    };

                    sourceState.ExportVirusStrainToExposure(exposure);

                    bool infected = TryInfectIndividual(
                        victim,
                        disease,
                        spreadChance01,
                        exposure);

                    if (infected)
                    {
                        totalSpread++;

                        if (debugVirusContextSpread)
                        {
                            //Debug.Log(
                                //$"[DiseaseManager] Virus spread in {contextType}. " +
                                //$"Context={contextName}, " +
                                //$"Disease={sourceState.GetDisplayName(disease)}, " +
                                //$"From={sourcePerson.Id}, " +
                                //$"To={victim.Id}, " +
                                //$"Chance={spreadChance01:F3}");
                        }
                    }
                }
            }
        }

        return totalSpread;
    }

    private bool TryPickVirusVictimFromContactGroup(
        IReadOnlyList<string> contactIds,
        string sourceId,
        DiseaseDefinitionSO disease,
        out Individual victim)
    {
        victim = null;

        if (contactIds == null || contactIds.Count == 0 || disease == null)
            return false;

        _tmpVirusVictimCandidates.Clear();

        for (int i = 0; i < contactIds.Count; i++)
        {
            string id = contactIds[i];

            if (string.IsNullOrWhiteSpace(id))
                continue;

            if (id == sourceId)
                continue;

            Individual person = FindIndividualById(id);

            if (person == null || !person.IsAlive)
                continue;

            string targetKey = DiseaseTargetKey.Build(DiseaseTargetType.Individual, person.Id);

            if (FindState(targetKey, disease.diseaseId) != null)
                continue;

            if (HasDiseaseImmunity(targetKey, disease.diseaseId))
                continue;

            _tmpVirusVictimCandidates.Add(person);
        }

        if (_tmpVirusVictimCandidates.Count == 0)
            return false;

        victim = _tmpVirusVictimCandidates[
            UnityEngine.Random.Range(0, _tmpVirusVictimCandidates.Count)];

        return victim != null;
    }

    private void TryMutateVirusState(
        Individual person,
        DiseaseDefinitionSO disease,
        IndividualDiseaseState state)
    {
        if (!enableVirusMutation)
            return;

        if (person == null || disease == null || state == null)
            return;

        if (!disease.CanMutateAsVirus())
            return;

        if (state.turnsInfected < disease.minTurnsBeforeVirusMutation)
            return;

        if (state.mutationGeneration >= disease.maxVirusMutationGeneration)
            return;

        float mutationChance = disease.virusMutationChancePerTurn;

        // More severe infections are slightly more likely to mutate.
        mutationChance *= Mathf.Lerp(0.75f, 1.25f, Mathf.Clamp01(state.severity01));
        mutationChance = Mathf.Clamp01(mutationChance);

        if (!Roll(mutationChance))
            return;

        float oldSeverity = state.severity01;
        float oldContagion = Mathf.Max(0f, state.strainContagionMultiplier);

        bool severityUp = UnityEngine.Random.value <= disease.mutationChanceToIncreaseSeverity;
        bool contagionUp = UnityEngine.Random.value <= disease.mutationChanceToIncreaseContagion;

        float severityStep = UnityEngine.Random.Range(
            disease.mutationSeverityStepMin,
            disease.mutationSeverityStepMax);

        float contagionStep = UnityEngine.Random.Range(
            disease.mutationContagionStepMin,
            disease.mutationContagionStepMax);

        if (!severityUp)
            severityStep *= -1f;

        if (!contagionUp)
            contagionStep *= -1f;

        state.severity01 = Mathf.Clamp01(state.severity01 + severityStep);

        state.strainContagionMultiplier = Mathf.Clamp(
            oldContagion + contagionStep,
            disease.minVirusStrainContagionMultiplier,
            disease.maxVirusStrainContagionMultiplier);

        state.mutationGeneration++;
        state.mutationRomanNumeral = DiseaseDefinitionSO.ToRomanNumeral(state.mutationGeneration);
        state.mutationCode4 = DiseaseDefinitionSO.RollFourDigitMutationCode();

        if (debugVirusMutation)
        {
            //Debug.Log(
                //$"[DiseaseManager] Virus mutated. " +
                //$"Individual={person.Id}, " +
                //$"Disease={state.GetDisplayName(disease)}, " +
                //$"Severity {oldSeverity:F2}->{state.severity01:F2}, " +
                //$"Contagion {oldContagion:F2}->{state.strainContagionMultiplier:F2}");
        }
    }

    public IReadOnlyList<PlayerDiseaseSummary> GetActivePlayerDiseaseSummaries()
    {
        BuildActivePlayerDiseaseSummaries(_tmpPlayerDiseaseSummaries);
        return _tmpPlayerDiseaseSummaries;
    }

    public void BuildActivePlayerDiseaseSummaries(List<PlayerDiseaseSummary> results)
    {
        if (results == null)
            return;

        results.Clear();

        if (activeIndividualDiseases == null || activeIndividualDiseases.Count == 0)
            return;

        for (int i = 0; i < activeIndividualDiseases.Count; i++)
        {
            IndividualDiseaseState state = activeIndividualDiseases[i];

            if (state == null)
                continue;

            if (string.IsNullOrWhiteSpace(state.targetId))
                continue;

            DiseaseDefinitionSO disease = GetDiseaseDefinition(state.diseaseId);
            if (disease == null)
                continue;

            Individual person = FindIndividualById(state.targetId);
            if (person == null || !person.IsAlive)
                continue;

            string displayName = GetDiseaseDisplayNameForSummary(disease, state);
            PlayerDiseaseSummary summary = FindDiseaseSummary(results, disease.diseaseId, displayName);

            if (summary == null)
            {
                summary = new PlayerDiseaseSummary
                {
                    diseaseId = disease.diseaseId,
                    displayName = displayName,
                    description = disease.description,
                    diseaseIcon = disease.diseaseIcon,
                    severity = disease.severity,
                    causeType = disease.causeType,
                    spreadType = disease.spreadType,
                    contagious = disease.contagious,
                    mutationGeneration = state.mutationGeneration,
                    mutationRomanNumeral = state.mutationRomanNumeral,
                    mutationCode4 = state.mutationCode4
                };

                results.Add(summary);
            }

            summary.totalAffected++;

            switch (person.AggregatedAgeGroup)
            {
                case AgeGroup.Child:
                    summary.childAffected++;
                    break;

                case AgeGroup.Teen:
                    summary.teenAffected++;
                    break;

                case AgeGroup.Adult:
                    summary.adultAffected++;
                    break;

                case AgeGroup.Elder:
                    summary.elderAffected++;
                    break;
            }
        }

        results.Sort((a, b) =>
        {
            int countCompare = b.totalAffected.CompareTo(a.totalAffected);
            if (countCompare != 0)
                return countCompare;

            return string.Compare(a.displayName, b.displayName, StringComparison.OrdinalIgnoreCase);
        });
    }

    private PlayerDiseaseSummary FindDiseaseSummary(
        List<PlayerDiseaseSummary> summaries,
        string diseaseId,
        string displayName)
    {
        if (summaries == null)
            return null;

        for (int i = 0; i < summaries.Count; i++)
        {
            PlayerDiseaseSummary summary = summaries[i];

            if (summary == null)
                continue;

            bool sameDisease = string.Equals(
                summary.diseaseId,
                diseaseId,
                StringComparison.OrdinalIgnoreCase);

            bool sameDisplayName = string.Equals(
                summary.displayName,
                displayName,
                StringComparison.OrdinalIgnoreCase);

            if (sameDisease && sameDisplayName)
                return summary;
        }

        return null;
    }

    private string GetDiseaseDisplayNameForSummary(
        DiseaseDefinitionSO disease,
        IndividualDiseaseState state)
    {
        string baseName = disease != null && !string.IsNullOrWhiteSpace(disease.displayName)
            ? disease.displayName
            : state != null ? state.diseaseId : "Unknown Disease";

        if (state == null)
            return baseName;

        bool hasMutation =
            state.mutationGeneration > 0 &&
            !string.IsNullOrWhiteSpace(state.mutationRomanNumeral) &&
            !string.IsNullOrWhiteSpace(state.mutationCode4);

        if (!hasMutation)
            return baseName;

        return $"{baseName} {state.mutationRomanNumeral}-{state.mutationCode4}";
    }

    public int GetActivePlayerDiseaseSummaryCount()
    {
        IReadOnlyList<PlayerDiseaseSummary> summaries = GetActivePlayerDiseaseSummaries();
        return summaries != null ? summaries.Count : 0;
    }

    public int TryApplyResourceHealthRestore(
    ResourceDefinition sourceResource,
    float healthRestoreBudget01,
    int maxTargets = 0,
    bool prioritizeLowestHealth = true)
    {
        if (sourceResource == null)
            return 0;

        if (healthRestoreBudget01 <= 0f)
            return 0;

        PlayerFamilySimulationManager family = PlayerFamilySimulationManager.Instance;
        PlayersPopulationManager pop = PlayersPopulationManager.Instance;

        if (family == null || pop == null)
            return 0;

        IReadOnlyList<Individual> people = family.GetIndividuals();
        if (people == null || people.Count == 0)
            return 0;

        List<Individual> candidates = new();

        for (int i = 0; i < people.Count; i++)
        {
            Individual person = people[i];

            if (person == null || !person.IsAlive)
                continue;

            if (person.Health01 >= 0.999f)
                continue;

            candidates.Add(person);
        }

        if (candidates.Count == 0)
            return 0;

        if (prioritizeLowestHealth)
            candidates.Sort((a, b) => a.Health01.CompareTo(b.Health01));

        int targetLimit = maxTargets <= 0
            ? candidates.Count
            : Mathf.Min(maxTargets, candidates.Count);

        int healedCount = 0;
        float remainingBudget = healthRestoreBudget01;
        float totalRestored = 0f;

        for (int i = 0; i < targetLimit && remainingBudget > 0.0001f; i++)
        {
            Individual person = candidates[i];
            if (person == null || !person.IsAlive)
                continue;

            float beforeHealth = person.Health01;
            float missingHealth = Mathf.Clamp01(1f - beforeHealth);

            if (missingHealth <= 0f)
                continue;

            float restore = Mathf.Min(missingHealth, remainingBudget);
            if (restore <= 0f)
                continue;

            person.Health01 = Mathf.Clamp01(person.Health01 + restore);

            float actualDelta = person.Health01 - beforeHealth;
            if (actualDelta <= 0f)
                continue;

            ApplyIndividualHealthDeltaToPopulationGroup(
                person,
                actualDelta,
                null,
                null);

            remainingBudget -= actualDelta;
            totalRestored += actualDelta;
            healedCount++;
        }

        if (healedCount > 0)
        {
            pop.MarkUIDirty();
            SaveSystem.MarkSectionDirty(SaveSectionKeys.Population);

            if (enableDebugLogs)
            {
                //Debug.Log(
                    //$"[DiseaseManager] Resource restored health. " +
                    //$"Resource={sourceResource.resourceName}, " +
                    //$"PeopleHealed={healedCount}, " +
                    //$"TotalRestored={totalRestored:F3}");
            }
        }

        return healedCount;
    }

    public int TryApplyConsumedResourceRecoveryBoost(
    ResourceDefinition sourceResource,
    float recoveryBoostBudget01,
    int maxTargets = 0)
    {
        if (sourceResource == null)
            return 0;

        if (recoveryBoostBudget01 <= 0f)
            return 0;

        if (activeIndividualDiseases == null || activeIndividualDiseases.Count == 0)
            return 0;

        List<IndividualDiseaseState> candidates = new();

        for (int i = 0; i < activeIndividualDiseases.Count; i++)
        {
            IndividualDiseaseState state = activeIndividualDiseases[i];

            if (state == null)
                continue;

            if (string.IsNullOrWhiteSpace(state.targetId))
                continue;

            DiseaseDefinitionSO disease = GetDiseaseDefinition(state.diseaseId);
            if (disease == null)
                continue;

            Individual person = FindIndividualById(state.targetId);
            if (person == null || !person.IsAlive)
                continue;

            candidates.Add(state);
        }

        if (candidates.Count == 0)
            return 0;

        candidates.Sort((a, b) =>
        {
            int severityCompare = b.severity01.CompareTo(a.severity01);
            if (severityCompare != 0)
                return severityCompare;

            return b.turnsRemaining.CompareTo(a.turnsRemaining);
        });

        int targetLimit = maxTargets <= 0
            ? candidates.Count
            : Mathf.Min(maxTargets, candidates.Count);

        int affected = 0;
        float remainingBudget = recoveryBoostBudget01;

        for (int i = 0; i < targetLimit && remainingBudget > 0.0001f; i++)
        {
            IndividualDiseaseState state = candidates[i];
            if (state == null)
                continue;

            DiseaseDefinitionSO disease = GetDiseaseDefinition(state.diseaseId);
            if (disease == null)
                continue;

            float boostForThisState = recoveryBoostBudget01 / Mathf.Max(1, targetLimit);
            boostForThisState = Mathf.Min(boostForThisState, remainingBudget);

            if (boostForThisState <= 0f)
                continue;

            int turnsReduced = Mathf.Max(1, Mathf.RoundToInt(boostForThisState * 3f));

            int oldTurns = state.turnsRemaining;
            float oldSeverity = state.severity01;

            state.turnsRemaining = Mathf.Max(0, state.turnsRemaining - turnsReduced);

            // Healing resources should not instantly erase severity unless very strong.
            state.severity01 = Mathf.Clamp01(state.severity01 - boostForThisState * 0.35f);

            if (state.severity01 <= 0.25f || state.turnsRemaining <= 1)
                state.isRecovering = true;

            remainingBudget -= boostForThisState;
            affected++;

            if (debugRecovery)
            {
                //Debug.Log(
                    //$"[DiseaseManager] Resource boosted disease recovery. " +
                    //$"Resource={sourceResource.resourceName}, " +
                    //$"Disease={disease.displayName}, " +
                    //$"Target={state.targetId}, " +
                    //$"Turns {oldTurns}->{state.turnsRemaining}, " +
                    //$"Severity {oldSeverity:F2}->{state.severity01:F2}");
            }
        }

        if (affected > 0)
        {
            SaveSystem.MarkSectionDirty(SaveSectionKeys.Population);
        }

        return affected;
    }

    public PlayerDiseaseSaveData CaptureSaveData()
    {
        PlayerDiseaseSaveData data = new PlayerDiseaseSaveData();

        if (activeIndividualDiseases != null)
        {
            for (int i = 0; i < activeIndividualDiseases.Count; i++)
            {
                IndividualDiseaseState state = activeIndividualDiseases[i];

                if (state == null)
                    continue;

                if (string.IsNullOrWhiteSpace(state.targetId))
                    continue;

                if (string.IsNullOrWhiteSpace(state.diseaseId))
                    continue;

                data.activeIndividualDiseases.Add(new IndividualDiseaseStateSaveData
                {
                    targetId = state.targetId,
                    diseaseId = state.diseaseId,

                    turnsRemaining = state.turnsRemaining,
                    turnsInfected = state.turnsInfected,

                    severity01 = state.severity01,

                    sourceTypeValue = (int)state.sourceType,
                    sourceId = state.sourceId,

                    isContagious = state.isContagious,
                    isRecovering = state.isRecovering,

                    strainContagionMultiplier = state.strainContagionMultiplier,
                    mutationGeneration = state.mutationGeneration,
                    mutationRomanNumeral = state.mutationRomanNumeral,
                    mutationCode4 = state.mutationCode4
                });
            }
        }

        if (_immunityTurnsByTargetKey != null && _immunityTurnsByTargetKey.Count > 0)
        {
            foreach (KeyValuePair<string, Dictionary<string, int>> targetEntry in _immunityTurnsByTargetKey)
            {
                if (string.IsNullOrWhiteSpace(targetEntry.Key))
                    continue;

                Dictionary<string, int> diseaseMap = targetEntry.Value;
                if (diseaseMap == null || diseaseMap.Count == 0)
                    continue;

                DiseaseImmunityTargetSaveData targetData = new DiseaseImmunityTargetSaveData
                {
                    targetKey = targetEntry.Key
                };

                foreach (KeyValuePair<string, int> diseaseEntry in diseaseMap)
                {
                    if (string.IsNullOrWhiteSpace(diseaseEntry.Key))
                        continue;

                    if (diseaseEntry.Value <= 0)
                        continue;

                    targetData.diseases.Add(new DiseaseImmunityEntrySaveData
                    {
                        diseaseId = diseaseEntry.Key,
                        turnsRemaining = diseaseEntry.Value
                    });
                }

                if (targetData.diseases.Count > 0)
                    data.immunityTargets.Add(targetData);
            }
        }

        return data;
    }

    public void LoadState(PlayerDiseaseSaveData data)
    {
        activeIndividualDiseases.Clear();
        _immunityTurnsByTargetKey.Clear();

        RebuildDefinitionCache();

        if (data == null)
        {
            RebuildRuntimeIndex();
            return;
        }

        if (data.activeIndividualDiseases != null)
        {
            for (int i = 0; i < data.activeIndividualDiseases.Count; i++)
            {
                IndividualDiseaseStateSaveData saved = data.activeIndividualDiseases[i];

                if (saved == null)
                    continue;

                if (string.IsNullOrWhiteSpace(saved.targetId))
                    continue;

                if (string.IsNullOrWhiteSpace(saved.diseaseId))
                    continue;

                DiseaseDefinitionSO disease = GetDiseaseDefinition(saved.diseaseId);
                if (disease == null)
                {
                    //Debug.LogWarning($"[DiseaseManager] Skipped saved disease with missing definition: {saved.diseaseId}");
                    continue;
                }

                Individual person = FindIndividualById(saved.targetId);
                if (person == null || !person.IsAlive)
                    continue;

                DiseaseSourceType sourceType = DiseaseSourceType.Unknown;

                if (Enum.IsDefined(typeof(DiseaseSourceType), saved.sourceTypeValue))
                    sourceType = (DiseaseSourceType)saved.sourceTypeValue;

                IndividualDiseaseState state = new IndividualDiseaseState(
                    saved.targetId,
                    saved.diseaseId,
                    Mathf.Max(0, saved.turnsRemaining),
                    Mathf.Clamp01(saved.severity01),
                    sourceType,
                    saved.sourceId,
                    saved.isContagious
                );

                state.turnsInfected = Mathf.Max(0, saved.turnsInfected);
                state.isRecovering = saved.isRecovering;

                state.strainContagionMultiplier = Mathf.Max(0f, saved.strainContagionMultiplier);
                state.mutationGeneration = Mathf.Max(0, saved.mutationGeneration);
                state.mutationRomanNumeral = saved.mutationRomanNumeral;
                state.mutationCode4 = saved.mutationCode4;

                activeIndividualDiseases.Add(state);
            }
        }

        if (data.immunityTargets != null)
        {
            for (int i = 0; i < data.immunityTargets.Count; i++)
            {
                DiseaseImmunityTargetSaveData targetData = data.immunityTargets[i];

                if (targetData == null)
                    continue;

                if (string.IsNullOrWhiteSpace(targetData.targetKey))
                    continue;

                if (targetData.diseases == null || targetData.diseases.Count == 0)
                    continue;

                Dictionary<string, int> diseaseMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                for (int d = 0; d < targetData.diseases.Count; d++)
                {
                    DiseaseImmunityEntrySaveData entry = targetData.diseases[d];

                    if (entry == null)
                        continue;

                    if (string.IsNullOrWhiteSpace(entry.diseaseId))
                        continue;

                    if (entry.turnsRemaining <= 0)
                        continue;

                    diseaseMap[entry.diseaseId] = entry.turnsRemaining;
                }

                if (diseaseMap.Count > 0)
                    _immunityTurnsByTargetKey[targetData.targetKey] = diseaseMap;
            }
        }

        RebuildRuntimeIndex();

        if (enableDebugLogs)
        {
            //Debug.Log(
                //$"[DiseaseManager] Loaded disease save data. " +
                //$"ActiveDiseases={activeIndividualDiseases.Count}, " +
                //$"ImmunityTargets={_immunityTurnsByTargetKey.Count}");
        }
    }

    private bool HasAnyActiveCaseOfDisease(string diseaseId)
    {
        for (int i = 0; i < activeIndividualDiseases.Count; i++)
        {
            var s = activeIndividualDiseases[i];
            if (s != null && string.Equals(s.diseaseId, diseaseId, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private void PostDiseaseOutbreakNotification(DiseaseDefinitionSO disease)
    {
        if (NotificationManager.Instance == null) return;
        string diseaseName = disease != null && !string.IsNullOrWhiteSpace(disease.displayName)
            ? disease.displayName
            : "Unknown Disease";
        string causeType = disease != null ? disease.causeType.ToString() : "";
        string title, message;
        if (NotificationMessageCrafterManager.Instance != null)
            (title, message) = NotificationMessageCrafterManager.Instance.CraftDiseaseOutbreak(diseaseName, causeType);
        else
            (title, message) = ("Disease Outbreak!", $"{diseaseName} has appeared in your population.");
        NotificationManager.Instance.AddNotification(NotificationType.DiseaseOutbreak, title, message, false);
    }

    private void MarkDiseaseSaveDirty()
    {
        SaveSystem.MarkSectionDirty(SaveSectionKeys.Population);
    }
}
