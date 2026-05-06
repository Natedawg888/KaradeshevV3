using System;
using System.Collections.Generic;
using UnityEngine;
using CloudDensity = CloudSimulationSystem.CloudDensity;

public enum DiseaseTaskResultType
{
    GatheringFailure = 0,
    DiscoveryFailure = 1,

    GatheringSuccess = 10,
    DiscoverySuccess = 11,

    CraftingBuildingWeatherExposure = 20,
    ShelterBuildingWeatherExposure = 21,

    ProductionInternalWeatherExposure = 30,
    ProductionExtractorTileExposure = 31
}

public enum EnvironmentalCellMatchMode
{
    AnyCoveredCell = 0,
    MajorityCoveredCells = 1,
    AllCoveredCells = 2
}

[Serializable]
public class EnvironmentalDiseaseRisk
{
    [Header("Environment Disease Name")]
    public string environmentDisease;

    [Header("Debug")]
    public string debugLabel;

    [Header("Disease")]
    public DiseaseDefinitionSO disease;

    [Range(0f, 1f)]
    public float infectionChancePerWorker = 0.08f;

    [Range(0f, 1f)]
    public float exposureStrength01 = 0.6f;

    [Min(0)]
    public int maxWorkersToCheck = 4;

    [Header("Task Type")]
    public bool applyOnGatheringFailure = true;
    public bool applyOnDiscoveryFailure = true;

    [Tooltip("Lower-risk exposure when gathering succeeds.")]
    public bool applyOnGatheringSuccess = true;

    [Tooltip("Lower-risk exposure when discovery succeeds.")]
    public bool applyOnDiscoverySuccess = true;

    [Header("Success Exposure Scaling")]
    [Tooltip("Successful tasks use this fraction of the normal infection chance. 0.25 = 25% of failure risk.")]
    [Range(0f, 1f)]
    public float successInfectionChanceMultiplier = 0.25f;

    [Tooltip("Successful tasks use this fraction of the normal exposure strength. 0.5 = half exposure.")]
    [Range(0f, 1f)]
    public float successExposureStrengthMultiplier = 0.5f;

    [Tooltip("Optional lower worker cap for successful tasks. 0 means use maxWorkersToCheck.")]
    [Min(0)]
    public int maxWorkersToCheckOnSuccess = 2;

    [Header("Building Weather Exposure")]
    [Tooltip("Applies this environmental disease to active crafting workers when the building footprint is exposed.")]
    public bool applyOnCraftingBuildingWeatherExposure = false;

    [Tooltip("Applies this environmental disease to housed unbusy shelter population when the shelter footprint is exposed.")]
    public bool applyOnShelterBuildingWeatherExposure = false;

    [Tooltip("Crafting buildings give some cover, so this lowers infection chance compared with outdoor task failure.")]
    [Range(0f, 1f)]
    public float craftingBuildingInfectionChanceMultiplier = 0.60f;

    [Tooltip("Crafting buildings reduce exposure strength compared with being outside.")]
    [Range(0f, 1f)]
    public float craftingBuildingExposureStrengthMultiplier = 0.75f;

    [Tooltip("0 means use maxWorkersToCheck.")]
    [Min(0)]
    public int maxWorkersToCheckOnCraftingWeather = 3;

    [Tooltip("Shelters protect housed population, so this should usually be lower than crafting.")]
    [Range(0f, 1f)]
    public float shelterBuildingInfectionChanceMultiplier = 0.30f;

    [Tooltip("Shelters reduce exposure strength compared with being outside.")]
    [Range(0f, 1f)]
    public float shelterBuildingExposureStrengthMultiplier = 0.50f;

    [Tooltip("0 means use maxWorkersToCheck.")]
    [Min(0)]
    public int maxPeopleToCheckOnShelterWeather = 5;

    [Header("Production Weather Exposure")]
    [Tooltip("Applies this environmental disease to internal production workers from the building footprint.")]
    public bool applyOnProductionInternalWeatherExposure = false;

    [Tooltip("Applies this environmental disease to extractor production workers from extraction tiles.")]
    public bool applyOnProductionExtractorTileExposure = false;

    [Tooltip("Internal production buildings give cover, so this should usually be lower than outdoor exposure.")]
    [Range(0f, 1f)]
    public float productionInternalInfectionChanceMultiplier = 0.55f;

    [Tooltip("Internal production buildings reduce exposure strength.")]
    [Range(0f, 1f)]
    public float productionInternalExposureStrengthMultiplier = 0.70f;

    [Tooltip("0 means use maxWorkersToCheck.")]
    [Min(0)]
    public int maxWorkersToCheckOnProductionInternalWeather = 3;

    [Tooltip("Extractor workers are outside, so this can be close to gathering exposure.")]
    [Range(0f, 1f)]
    public float productionExtractorInfectionChanceMultiplier = 0.90f;

    [Tooltip("Extractor workers are outside, so this can be close to gathering exposure.")]
    [Range(0f, 1f)]
    public float productionExtractorExposureStrengthMultiplier = 0.95f;

    [Tooltip("0 means use maxWorkersToCheck.")]
    [Min(0)]
    public int maxWorkersToCheckOnProductionExtractorTile = 4;

    [Header("Source")]
    public DiseaseSourceType sourceType = DiseaseSourceType.Unknown;

    [Tooltip("If true, acid rain / ash fall can override the source type automatically.")]
    public bool autoSourceTypeFromWeather = true;

    [Header("Environment Type Filter")]
    public bool matchAnyEnvironmentType = true;
    public List<EnvironmentType> allowedEnvironmentTypes = new();

    [Header("Tile Type Filter")]
    public bool matchAnyTileType = true;
    public List<EnvironmentTileType> allowedTileTypes = new();

    [Header("Weather Required")]
    public bool requireValidWeatherSample = false;

    [Header("Humidity Filter")]
    public bool useHumidityRange = false;
    [Range(0f, 1f)] public float minHumidity01 = 0f;
    [Range(0f, 1f)] public float maxHumidity01 = 1f;

    [Header("Temperature Filter")]
    public bool useTemperatureRange = false;
    public float minTemperatureC = -50f;
    public float maxTemperatureC = 80f;

    [Header("Cloud Filter")]
    public bool requireCloudDensity = false;
    public EnvironmentalCellMatchMode cloudMatchMode = EnvironmentalCellMatchMode.AnyCoveredCell;
    public List<CloudDensity> allowedCloudDensities = new();

    [Header("Volcanic / Ash / Acid Rain Filter")]
    public bool requireAnyVolcanicPrecipitation = false;
    public bool requireAcidRain = false;
    public bool requireAshFall = false;
    public EnvironmentalCellMatchMode volcanicMatchMode = EnvironmentalCellMatchMode.AnyCoveredCell;

    [Header("Weather Strength")]
    [Tooltip("If true, final chance is multiplied by weather strength. This makes weak exposure less dangerous.")]
    public bool multiplyChanceByWeatherStrength = true;

    [Tooltip("Minimum weather strength when this risk matches. Stops tiny effects from becoming meaningless.")]
    [Range(0f, 1f)]
    public float minimumWeatherStrength01 = 0.25f;

    [Range(0f, 1f)] public float humidityStrengthWeight = 0.25f;
    [Range(0f, 1f)] public float temperatureStrengthWeight = 0.25f;
    [Range(0f, 1f)] public float cloudStrengthWeight = 0.20f;
    [Range(0f, 1f)] public float volcanicStrengthWeight = 0.60f;

    public bool MatchesTask(DiseaseTaskResultType taskType)
    {
        return taskType switch
        {
            DiseaseTaskResultType.GatheringFailure => applyOnGatheringFailure,
            DiseaseTaskResultType.DiscoveryFailure => applyOnDiscoveryFailure,
            DiseaseTaskResultType.GatheringSuccess => applyOnGatheringSuccess,
            DiseaseTaskResultType.DiscoverySuccess => applyOnDiscoverySuccess,

            DiseaseTaskResultType.CraftingBuildingWeatherExposure => applyOnCraftingBuildingWeatherExposure,
            DiseaseTaskResultType.ShelterBuildingWeatherExposure => applyOnShelterBuildingWeatherExposure,

            _ => false
        };
    }

    public bool IsSuccessResult(DiseaseTaskResultType taskType)
    {
        return taskType == DiseaseTaskResultType.GatheringSuccess ||
               taskType == DiseaseTaskResultType.DiscoverySuccess;
    }

    public float GetResultInfectionChanceMultiplier(DiseaseTaskResultType taskType)
    {
        return taskType switch
        {
            DiseaseTaskResultType.GatheringSuccess => Mathf.Clamp01(successInfectionChanceMultiplier),
            DiseaseTaskResultType.DiscoverySuccess => Mathf.Clamp01(successInfectionChanceMultiplier),

            DiseaseTaskResultType.CraftingBuildingWeatherExposure => Mathf.Clamp01(craftingBuildingInfectionChanceMultiplier),
            DiseaseTaskResultType.ShelterBuildingWeatherExposure => Mathf.Clamp01(shelterBuildingInfectionChanceMultiplier),

            DiseaseTaskResultType.ProductionInternalWeatherExposure => Mathf.Clamp01(productionInternalInfectionChanceMultiplier),
            DiseaseTaskResultType.ProductionExtractorTileExposure => Mathf.Clamp01(productionExtractorInfectionChanceMultiplier),

            _ => 1f
        };
    }

    public float GetResultExposureStrengthMultiplier(DiseaseTaskResultType taskType)
    {
        return taskType switch
        {
            DiseaseTaskResultType.GatheringSuccess => Mathf.Clamp01(successExposureStrengthMultiplier),
            DiseaseTaskResultType.DiscoverySuccess => Mathf.Clamp01(successExposureStrengthMultiplier),

            DiseaseTaskResultType.CraftingBuildingWeatherExposure => Mathf.Clamp01(craftingBuildingExposureStrengthMultiplier),
            DiseaseTaskResultType.ShelterBuildingWeatherExposure => Mathf.Clamp01(shelterBuildingExposureStrengthMultiplier),

            DiseaseTaskResultType.ProductionInternalWeatherExposure => Mathf.Clamp01(productionInternalExposureStrengthMultiplier),
            DiseaseTaskResultType.ProductionExtractorTileExposure => Mathf.Clamp01(productionExtractorExposureStrengthMultiplier),

            _ => 1f
        };
    }

    public int GetMaxWorkersToCheckForResult(DiseaseTaskResultType taskType, int availableWorkerCount)
    {
        int baseMax = maxWorkersToCheck <= 0
            ? availableWorkerCount
            : Mathf.Min(maxWorkersToCheck, availableWorkerCount);

        switch (taskType)
        {
            case DiseaseTaskResultType.GatheringSuccess:
            case DiseaseTaskResultType.DiscoverySuccess:
                if (maxWorkersToCheckOnSuccess > 0)
                    return Mathf.Min(baseMax, maxWorkersToCheckOnSuccess);
                return baseMax;

            case DiseaseTaskResultType.CraftingBuildingWeatherExposure:
                if (maxWorkersToCheckOnCraftingWeather > 0)
                    return Mathf.Min(baseMax, maxWorkersToCheckOnCraftingWeather);
                return baseMax;

            case DiseaseTaskResultType.ShelterBuildingWeatherExposure:
                if (maxPeopleToCheckOnShelterWeather > 0)
                    return Mathf.Min(baseMax, maxPeopleToCheckOnShelterWeather);
                return baseMax;

            case DiseaseTaskResultType.ProductionInternalWeatherExposure:
                if (maxWorkersToCheckOnProductionInternalWeather > 0)
                    return Mathf.Min(baseMax, maxWorkersToCheckOnProductionInternalWeather);
                return baseMax;

            case DiseaseTaskResultType.ProductionExtractorTileExposure:
                if (maxWorkersToCheckOnProductionExtractorTile > 0)
                    return Mathf.Min(baseMax, maxWorkersToCheckOnProductionExtractorTile);
                return baseMax;

            default:
                return baseMax;
        }
    }

    public bool MatchesEnvironment(EnvironmentControl env)
    {
        if (env == null)
            return false;

        if (!matchAnyEnvironmentType)
        {
            if (allowedEnvironmentTypes == null || allowedEnvironmentTypes.Count == 0)
                return false;

            if (!allowedEnvironmentTypes.Contains(env.environmentType))
                return false;
        }

        if (!matchAnyTileType)
        {
            if (allowedTileTypes == null || allowedTileTypes.Count == 0)
                return false;

            if (!allowedTileTypes.Contains(env.environmentTileType))
                return false;
        }

        return true;
    }

    public bool TryEvaluateWeather(
        bool hasWeatherSample,
        WeatherAreaSample sample,
        List<TileCoord> coveredCells,
        CloudSimulationSystem cloudSystem,
        RainSimulationSystem rainSystem,
        out float weatherStrength01,
        out DiseaseSourceType resolvedSourceType,
        out string weatherSummary)
    {
        weatherStrength01 = 1f;
        resolvedSourceType = sourceType;
        weatherSummary = string.Empty;

        if (requireValidWeatherSample && (!hasWeatherSample || !sample.hasAnyValidCell))
            return false;

        float humidityStrength = 0f;
        float temperatureStrength = 0f;
        float cloudStrength = 0f;
        float volcanicStrength = 0f;

        if (hasWeatherSample && sample.hasAnyValidCell)
        {
            float humidity = Mathf.Clamp01(sample.averageHumidity01);
            float temp = sample.averageTemperatureC;

            if (useHumidityRange)
            {
                if (humidity < minHumidity01 || humidity > maxHumidity01)
                    return false;

                humidityStrength = Mathf.InverseLerp(minHumidity01, maxHumidity01, humidity);
            }

            if (useTemperatureRange)
            {
                if (temp < minTemperatureC || temp > maxTemperatureC)
                    return false;

                temperatureStrength = Mathf.InverseLerp(minTemperatureC, maxTemperatureC, temp);
            }

            weatherSummary += $"Temp={temp:F1}C Humidity={humidity:F2} ";
        }

        if (requireCloudDensity)
        {
            if (!EvaluateCloudMatch(coveredCells, cloudSystem, out cloudStrength))
                return false;

            weatherSummary += $"CloudStrength={cloudStrength:F2} ";
        }

        if (requireAnyVolcanicPrecipitation || requireAcidRain || requireAshFall)
        {
            if (!EvaluateVolcanicPrecipitationMatch(
                    coveredCells,
                    rainSystem,
                    out volcanicStrength,
                    out DiseaseSourceType volcanicSourceType))
            {
                return false;
            }

            if (autoSourceTypeFromWeather)
                resolvedSourceType = volcanicSourceType;

            weatherSummary += $"VolcanicStrength={volcanicStrength:F2} ";
        }

        float weightedStrength = 0f;
        float totalWeight = 0f;

        AddWeighted(ref weightedStrength, ref totalWeight, humidityStrength, humidityStrengthWeight, useHumidityRange);
        AddWeighted(ref weightedStrength, ref totalWeight, temperatureStrength, temperatureStrengthWeight, useTemperatureRange);
        AddWeighted(ref weightedStrength, ref totalWeight, cloudStrength, cloudStrengthWeight, requireCloudDensity);
        AddWeighted(ref weightedStrength, ref totalWeight, volcanicStrength, volcanicStrengthWeight, requireAnyVolcanicPrecipitation || requireAcidRain || requireAshFall);

        weatherStrength01 = totalWeight > 0f
            ? Mathf.Clamp01(weightedStrength / totalWeight)
            : 1f;

        if (weatherStrength01 > 0f)
            weatherStrength01 = Mathf.Max(weatherStrength01, minimumWeatherStrength01);

        return true;
    }

    public float GetFinalInfectionChance01(float weatherStrength01)
    {
        float chance = Mathf.Clamp01(infectionChancePerWorker);

        if (multiplyChanceByWeatherStrength)
            chance *= Mathf.Clamp01(weatherStrength01);

        return Mathf.Clamp01(chance);
    }

    public float GetFinalExposureStrength01(float weatherStrength01)
    {
        return Mathf.Clamp01(exposureStrength01 * Mathf.Max(minimumWeatherStrength01, weatherStrength01));
    }

    private static void AddWeighted(
        ref float weightedStrength,
        ref float totalWeight,
        float value,
        float weight,
        bool enabled)
    {
        if (!enabled)
            return;

        weight = Mathf.Max(0f, weight);
        if (weight <= 0f)
            return;

        weightedStrength += Mathf.Clamp01(value) * weight;
        totalWeight += weight;
    }

    private bool EvaluateCloudMatch(
        List<TileCoord> coveredCells,
        CloudSimulationSystem cloudSystem,
        out float cloudStrength01)
    {
        cloudStrength01 = 0f;

        if (cloudSystem == null || !cloudSystem.IsInitialized)
            return false;

        if (coveredCells == null || coveredCells.Count == 0)
            return false;

        if (allowedCloudDensities == null || allowedCloudDensities.Count == 0)
            return false;

        int checkedCells = 0;
        int matchedCells = 0;
        float strengthSum = 0f;

        for (int i = 0; i < coveredCells.Count; i++)
        {
            TileCoord cell = coveredCells[i];

            CloudDensity density = cloudSystem.GetCloudDensityAtCell(cell.x, cell.y);
            checkedCells++;

            if (!allowedCloudDensities.Contains(density))
                continue;

            matchedCells++;

            // If the rule asks for no clouds, treat that as a strong match.
            if (density == CloudDensity.None)
                strengthSum += 1f;
            else
                strengthSum += Mathf.Clamp01((float)density / (float)CloudDensity.High);
        }

        if (!PassesCellMatchMode(matchedCells, checkedCells, cloudMatchMode))
            return false;

        cloudStrength01 = matchedCells > 0
            ? Mathf.Clamp01(strengthSum / matchedCells)
            : 0f;

        return true;
    }

    private bool EvaluateVolcanicPrecipitationMatch(
        List<TileCoord> coveredCells,
        RainSimulationSystem rainSystem,
        out float volcanicStrength01,
        out DiseaseSourceType volcanicSourceType)
    {
        volcanicStrength01 = 0f;
        volcanicSourceType = sourceType;

        if (rainSystem == null || !rainSystem.IsInitialized)
            return false;

        if (coveredCells == null || coveredCells.Count == 0)
            return false;

        int checkedCells = 0;
        int matchedCells = 0;
        float strengthSum = 0f;
        bool sawAcid = false;
        bool sawAsh = false;

        for (int i = 0; i < coveredCells.Count; i++)
        {
            TileCoord cell = coveredCells[i];
            checkedCells++;

            bool acid = rainSystem.IsAcidRainAtCell(cell.x, cell.y);
            bool ash = rainSystem.IsAshFallAtCell(cell.x, cell.y);

            bool match = false;

            if (requireAcidRain && acid)
                match = true;

            if (requireAshFall && ash)
                match = true;

            if (requireAnyVolcanicPrecipitation && (acid || ash))
                match = true;

            if (!match)
                continue;

            matchedCells++;

            if (acid)
                sawAcid = true;

            if (ash)
                sawAsh = true;

            strengthSum += Mathf.Clamp01(
                rainSystem.GetVolcanicPrecipitationSeverity01AtCell(cell.x, cell.y));
        }

        if (!PassesCellMatchMode(matchedCells, checkedCells, volcanicMatchMode))
            return false;

        volcanicStrength01 = matchedCells > 0
            ? Mathf.Clamp01(strengthSum / matchedCells)
            : 0f;

        if (sawAcid)
            volcanicSourceType = DiseaseSourceType.AcidRainAshExposure;
        else if (sawAsh)
            volcanicSourceType = DiseaseSourceType.AshCloud;

        return true;
    }

    private static bool PassesCellMatchMode(
        int matchedCells,
        int checkedCells,
        EnvironmentalCellMatchMode mode)
    {
        if (checkedCells <= 0)
            return false;

        return mode switch
        {
            EnvironmentalCellMatchMode.AnyCoveredCell => matchedCells > 0,
            EnvironmentalCellMatchMode.MajorityCoveredCells => matchedCells >= Mathf.CeilToInt(checkedCells * 0.5f),
            EnvironmentalCellMatchMode.AllCoveredCells => matchedCells >= checkedCells,
            _ => matchedCells > 0
        };
    }
}