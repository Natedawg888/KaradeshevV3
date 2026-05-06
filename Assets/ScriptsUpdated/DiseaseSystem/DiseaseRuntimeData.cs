using System;
using UnityEngine;

[Serializable]
public class DiseaseExposureInfo
{
    public DiseaseSourceType sourceType = DiseaseSourceType.Unknown;
    public string sourceId;

    public bool hasSourceTile;
    public TileCoord sourceTileCoord;

    [Range(0f, 1f)]
    public float exposureStrength01 = 1f;

    public string notes;

    [Header("Virus Strain Inheritance")]
    public bool inheritVirusStrain = false;
    public int inheritedMutationGeneration = 0;
    public string inheritedMutationRomanNumeral;
    public string inheritedMutationCode4;
    public float inheritedStrainContagionMultiplier = 1f;
}

[Serializable]
public class IndividualDiseaseState
{
    public string stateId;
    public string targetId;
    public string diseaseId;

    public int turnsRemaining;
    public int turnsInfected;

    public InfectionStage infectionStage = InfectionStage.Active;

    [Range(0f, 1f)]
    public float severity01 = 1f;

    public DiseaseSourceType sourceType = DiseaseSourceType.Unknown;
    public string sourceId;

    public bool isContagious;
    public bool isRecovering;

    [Header("Virus Strain Runtime")]
    public int mutationGeneration = 0;
    public string mutationRomanNumeral;
    public string mutationCode4;

    [Min(0f)]
    public float strainContagionMultiplier = 1f;

    public IndividualDiseaseState() { }

    public IndividualDiseaseState(
        string targetId,
        string diseaseId,
        int durationTurns,
        float severity01,
        DiseaseSourceType sourceType,
        string sourceId,
        bool isContagious)
    {
        this.stateId = Guid.NewGuid().ToString();
        this.targetId = targetId;
        this.diseaseId = diseaseId;

        this.turnsRemaining = Mathf.Max(1, durationTurns);
        this.turnsInfected = 0;

        this.infectionStage = InfectionStage.Active;
        this.severity01 = Mathf.Clamp01(severity01);

        this.sourceType = sourceType;
        this.sourceId = sourceId;

        this.isContagious = isContagious;
        this.isRecovering = false;
    }

    public bool HasMutationLabel =>
    mutationGeneration > 0 &&
    !string.IsNullOrWhiteSpace(mutationRomanNumeral) &&
    !string.IsNullOrWhiteSpace(mutationCode4);

    public string GetDisplayName(DiseaseDefinitionSO disease)
    {
        string baseName = disease != null && !string.IsNullOrWhiteSpace(disease.displayName)
            ? disease.displayName
            : diseaseId;

        if (!HasMutationLabel)
            return baseName;

        return $"{baseName} {mutationRomanNumeral}-{mutationCode4}";
    }

    public void CopyVirusStrainFromExposure(DiseaseExposureInfo exposure)
    {
        if (exposure == null || !exposure.inheritVirusStrain)
            return;

        mutationGeneration = Mathf.Max(0, exposure.inheritedMutationGeneration);
        mutationRomanNumeral = exposure.inheritedMutationRomanNumeral;
        mutationCode4 = exposure.inheritedMutationCode4;
        strainContagionMultiplier = Mathf.Max(0f, exposure.inheritedStrainContagionMultiplier);
    }

    public void ExportVirusStrainToExposure(DiseaseExposureInfo exposure)
    {
        if (exposure == null)
            return;

        exposure.inheritVirusStrain = true;
        exposure.inheritedMutationGeneration = mutationGeneration;
        exposure.inheritedMutationRomanNumeral = mutationRomanNumeral;
        exposure.inheritedMutationCode4 = mutationCode4;
        exposure.inheritedStrainContagionMultiplier = Mathf.Max(0f, strainContagionMultiplier);
    }
}

// Not used by V1 yet, but kept as the future bridge for group/pool disease tracking.
[Serializable]
public class GroupDiseaseState
{
    public string targetGroupId;
    public string diseaseId;

    public int infectedCount;
    public int exposedCount;
    public int recoveredCount;
    public int deadCount;

    [Range(0f, 1f)] public float infectedPercent01;
    [Range(0f, 1f)] public float severity01;

    public int turnsActive;
}