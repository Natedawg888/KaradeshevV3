using System;
using System.Collections.Generic;
using UnityEngine;

public enum BuildingDiseaseTargetMode
{
    ShelterUnbusyHousedPopulation = 0,
    ActiveCraftingWorkers = 1,
    ActiveProductionWorkers = 2,
    AnyProvidedWorkers = 3,
    ShelterAnyHousedPopulation = 4
}

public enum BuildingDiseaseTriggerTiming
{
    EveryTurn = 0,
    OnCompletedCycle = 1
}

[Serializable]
public class BuildingSpecificDiseaseRisk
{
    [Header("Debug")]
    public string debugLabel;

    [Header("Disease")]
    public DiseaseDefinitionSO disease;

    [Header("Target Mode")]
    public BuildingDiseaseTargetMode targetMode = BuildingDiseaseTargetMode.AnyProvidedWorkers;

    [Header("Timing")]
    public BuildingDiseaseTriggerTiming triggerTiming = BuildingDiseaseTriggerTiming.EveryTurn;

    [Header("Risk")]
    [Tooltip("Base chance per checked person.")]
    [Range(0f, 1f)]
    public float infectionChancePerPerson = 0.05f;

    [Tooltip("Exposure strength passed into the disease state.")]
    [Range(0f, 1f)]
    public float exposureStrength01 = 0.6f;

    [Tooltip("0 means check all valid targets.")]
    [Min(0)]
    public int maxPeopleChecked = 3;

    [Header("Building State Multipliers")]
    [Tooltip("Used when the building is normal.")]
    [Min(0f)] public float normalStateChanceMultiplier = 1f;

    [Tooltip("Used when the building is damaged.")]
    [Min(0f)] public float damagedStateChanceMultiplier = 1.5f;

    [Tooltip("Used when the building is destroyed. Usually 0 because destroyed buildings should not run.")]
    [Min(0f)] public float destroyedStateChanceMultiplier = 0f;

    [Header("Extra Scaling")]
    [Tooltip("Extra multiplier from the component or building script.")]
    [Min(0f)] public float extraChanceMultiplier = 1f;

    [Tooltip("Extra exposure multiplier from the component or building script.")]
    [Min(0f)] public float extraExposureMultiplier = 1f;

    [Header("Source")]
    public DiseaseSourceType sourceType = DiseaseSourceType.BuildingCrowding;

    public bool CanRunForMode(BuildingDiseaseTargetMode mode)
    {
        return targetMode == mode || targetMode == BuildingDiseaseTargetMode.AnyProvidedWorkers;
    }

    public bool CanRunForTiming(BuildingDiseaseTriggerTiming timing)
    {
        return triggerTiming == timing;
    }

    public float GetBuildingStateMultiplier(BuildingStatus status)
    {
        if (status == null)
            return normalStateChanceMultiplier;

        return status.CurrentState switch
        {
            BuildingState.Normal => normalStateChanceMultiplier,
            BuildingState.Damaged => damagedStateChanceMultiplier,
            BuildingState.Destroyed => destroyedStateChanceMultiplier,
            _ => normalStateChanceMultiplier
        };
    }

    public float GetFinalChance01(BuildingStatus status, float callerChanceMultiplier)
    {
        float chance =
            infectionChancePerPerson *
            GetBuildingStateMultiplier(status) *
            extraChanceMultiplier *
            Mathf.Max(0f, callerChanceMultiplier);

        return Mathf.Clamp01(chance);
    }

    public float GetFinalExposure01(float callerExposureMultiplier)
    {
        float exposure =
            exposureStrength01 *
            extraExposureMultiplier *
            Mathf.Max(0f, callerExposureMultiplier);

        return Mathf.Clamp01(exposure);
    }
}

[DisallowMultipleComponent]
public class BuildingDiseaseExposureSource : MonoBehaviour
{
    [Header("Building Disease")]
    public bool enableBuildingDiseaseExposure = true;

    [Tooltip("Risks this building can apply.")]
    public List<BuildingSpecificDiseaseRisk> risks = new();

    [Header("Balancing")]
    [Tooltip("Global chance multiplier for all risks on this building.")]
    [Min(0f)] public float globalChanceMultiplier = 1f;

    [Tooltip("Global exposure multiplier for all risks on this building.")]
    [Min(0f)] public float globalExposureMultiplier = 1f;

    [Header("Debug")]
    public bool debugBuildingDiseaseExposure = false;

    private BuildingStatus _status;

    private readonly List<string> _tmpTargetIds = new();
    private readonly List<string> _tmpUniqueIds = new();

    private void Awake()
    {
        _status = GetComponent<BuildingStatus>();
    }

    public int TryApplyToShelterUnbusyPopulation(
        ShelterControl shelter,
        BuildingDiseaseTriggerTiming timing = BuildingDiseaseTriggerTiming.EveryTurn)
    {
        if (shelter == null)
            return 0;

        IReadOnlyList<string> housedIds = shelter.HousedIndividualIds;
        if (housedIds == null || housedIds.Count == 0)
            return 0;

        PlayersPopulationManager pop = PlayersPopulationManager.Instance;

        _tmpTargetIds.Clear();

        for (int i = 0; i < housedIds.Count; i++)
        {
            string id = housedIds[i];

            if (string.IsNullOrWhiteSpace(id))
                continue;

            Individual person = FindIndividualById(id);

            if (person == null || !person.IsAlive)
                continue;

            // Shelter version only affects unbusy population.
            if (person.IsBusy)
                continue;

            if (pop != null && pop.IsIndividualReservedAnywhere(person.Id))
                continue;

            _tmpTargetIds.Add(person.Id);
        }

        return TryApplyToTargetIds(
            _tmpTargetIds,
            BuildingDiseaseTargetMode.ShelterUnbusyHousedPopulation,
            timing,
            contextName: shelter.name);
    }

    public int TryApplyToActiveCraftingWorkers(
        IReadOnlyList<string> workerIds,
        BuildingDiseaseTriggerTiming timing = BuildingDiseaseTriggerTiming.EveryTurn,
        string contextName = null)
    {
        return TryApplyToTargetIds(
            workerIds,
            BuildingDiseaseTargetMode.ActiveCraftingWorkers,
            timing,
            string.IsNullOrWhiteSpace(contextName) ? name : contextName);
    }

    public int TryApplyToActiveProductionWorkers(
        IReadOnlyList<string> workerIds,
        BuildingDiseaseTriggerTiming timing = BuildingDiseaseTriggerTiming.OnCompletedCycle,
        string contextName = null)
    {
        return TryApplyToTargetIds(
            workerIds,
            BuildingDiseaseTargetMode.ActiveProductionWorkers,
            timing,
            string.IsNullOrWhiteSpace(contextName) ? name : contextName);
    }

    public int TryApplyToTargetIds(
        IReadOnlyList<string> targetIds,
        BuildingDiseaseTargetMode mode,
        BuildingDiseaseTriggerTiming timing,
        string contextName = null,
        float chanceMultiplier = 1f,
        float exposureMultiplier = 1f)
    {
        if (!enableBuildingDiseaseExposure)
            return 0;

        if (DiseaseManager.Instance == null)
            return 0;

        if (risks == null || risks.Count == 0)
            return 0;

        if (targetIds == null || targetIds.Count == 0)
            return 0;

        if (_status != null && _status.CurrentState == BuildingState.Destroyed)
            return 0;

        _tmpUniqueIds.Clear();

        for (int i = 0; i < targetIds.Count; i++)
        {
            string id = targetIds[i];

            if (string.IsNullOrWhiteSpace(id))
                continue;

            if (_tmpUniqueIds.Contains(id))
                continue;

            Individual person = FindIndividualById(id);

            if (person == null || !person.IsAlive)
                continue;

            _tmpUniqueIds.Add(id);
        }

        if (_tmpUniqueIds.Count == 0)
            return 0;

        ShuffleInPlace(_tmpUniqueIds);

        int totalInfections = 0;

        for (int r = 0; r < risks.Count; r++)
        {
            BuildingSpecificDiseaseRisk risk = risks[r];

            if (risk == null || risk.disease == null)
                continue;

            if (!risk.CanRunForMode(mode))
                continue;

            if (!risk.CanRunForTiming(timing))
                continue;

            float finalChance = risk.GetFinalChance01(
                _status,
                globalChanceMultiplier * chanceMultiplier);

            float finalExposure = risk.GetFinalExposure01(
                globalExposureMultiplier * exposureMultiplier);

            if (finalChance <= 0f || finalExposure <= 0f)
                continue;

            int maxTargets = risk.maxPeopleChecked <= 0
                ? _tmpUniqueIds.Count
                : Mathf.Min(risk.maxPeopleChecked, _tmpUniqueIds.Count);

            int infectionsFromRisk = 0;

            for (int i = 0; i < maxTargets; i++)
            {
                Individual person = FindIndividualById(_tmpUniqueIds[i]);

                if (person == null || !person.IsAlive)
                    continue;

                DiseaseExposureInfo exposure = new DiseaseExposureInfo
                {
                    sourceType = risk.sourceType,
                    sourceId = gameObject.name,
                    exposureStrength01 = finalExposure,
                    notes =
                        $"Building disease exposure. " +
                        $"Building={name}, " +
                        $"Context={contextName}, " +
                        $"Mode={mode}, " +
                        $"Timing={timing}, " +
                        $"Chance={finalChance:F3}, " +
                        $"Exposure={finalExposure:F3}"
                };

                bool infected = DiseaseManager.Instance.TryInfectIndividual(
                    person,
                    risk.disease,
                    finalChance,
                    exposure);

                if (infected)
                {
                    infectionsFromRisk++;
                    totalInfections++;
                }
            }

            if (debugBuildingDiseaseExposure)
            {
                string label = string.IsNullOrWhiteSpace(risk.debugLabel)
                    ? risk.disease.displayName
                    : risk.debugLabel;

                //Debug.Log(
                    //$"[BuildingDiseaseExposureSource] Rolled building disease. " +
                    //$"Building={name}, " +
                    //$"Risk={label}, " +
                    //$"Mode={mode}, " +
                    //$"Timing={timing}, " +
                    //$"TargetsChecked={maxTargets}, " +
                    //$"Chance={finalChance:F3}, " +
                    //$"Exposure={finalExposure:F3}, " +
                    //$"Infections={infectionsFromRisk}");
            }
        }

        return totalInfections;
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

    public int TryApplyToShelterAnyHousedPopulation(
    ShelterControl shelter,
    BuildingDiseaseTriggerTiming timing = BuildingDiseaseTriggerTiming.EveryTurn)
    {
        if (shelter == null)
            return 0;

        IReadOnlyList<string> housedIds = shelter.HousedIndividualIds;
        if (housedIds == null || housedIds.Count == 0)
            return 0;

        _tmpTargetIds.Clear();

        for (int i = 0; i < housedIds.Count; i++)
        {
            string id = housedIds[i];

            if (string.IsNullOrWhiteSpace(id))
                continue;

            Individual person = FindIndividualById(id);

            if (person == null || !person.IsAlive)
                continue;

            // Unlike ShelterUnbusyHousedPopulation, this does NOT skip busy/reserved people.
            _tmpTargetIds.Add(person.Id);
        }

        return TryApplyToTargetIds(
            _tmpTargetIds,
            BuildingDiseaseTargetMode.ShelterAnyHousedPopulation,
            timing,
            contextName: shelter.name);
    }
}
