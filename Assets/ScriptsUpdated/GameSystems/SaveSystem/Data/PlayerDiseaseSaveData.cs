using System;
using System.Collections.Generic;

[Serializable]
public class PlayerDiseaseSaveData
{
    public int version = 1;

    public List<IndividualDiseaseStateSaveData> activeIndividualDiseases = new();
    public List<DiseaseImmunityTargetSaveData> immunityTargets = new();
}

[Serializable]
public class IndividualDiseaseStateSaveData
{
    public string targetId;
    public string diseaseId;

    public int turnsRemaining;
    public int turnsInfected;

    public float severity01;

    public int sourceTypeValue;
    public string sourceId;

    public bool isContagious;
    public bool isRecovering;

    // Virus mutation / strain data
    public float strainContagionMultiplier = 1f;
    public int mutationGeneration;
    public string mutationRomanNumeral;
    public string mutationCode4;
}

[Serializable]
public class DiseaseImmunityTargetSaveData
{
    public string targetKey;
    public List<DiseaseImmunityEntrySaveData> diseases = new();
}

[Serializable]
public class DiseaseImmunityEntrySaveData
{
    public string diseaseId;
    public int turnsRemaining;
}