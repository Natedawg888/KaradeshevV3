using System;
using System.Collections.Generic;

[Serializable]
public class PregnancyRecordSaveData
{
    public string motherId;
    public string fatherId;
    public int totalTurns;
    public int remainingTurns;
}

[Serializable]
public class PregnancyServiceSaveData
{
    public List<PregnancyRecordSaveData> pregnancies = new List<PregnancyRecordSaveData>();
    public List<StringIntPairSaveData> parentCooldowns = new List<StringIntPairSaveData>();
    public List<StringStringPairSaveData> gestationReservationByMother = new List<StringStringPairSaveData>();
    public List<StringStringPairSaveData> preferredBirthFamily = new List<StringStringPairSaveData>();
    public List<string> pregnantMothers = new List<string>();
}

[Serializable]
public class StringIntPairSaveData
{
    public string key;
    public int value;
}

[Serializable]
public class StringStringPairSaveData
{
    public string key;
    public string value;
}

[Serializable]
public class PopulationGroupSaveData
{
    public string groupId;
    public AgeGroup ageGroup;
    public Gender gender;
    public int count;
    public int additionTurn;
    public int averageAgeInTurns;
    public float averageHealth;
    public int maxHealthPerIndividual;
    public float hungerLevel;
    public float thirstLevel;
}

[Serializable]
public class PopulationReservationAllocationSaveData
{
    public string groupId;
    public int amount;
}

[Serializable]
public class PopulationReservationSaveData
{
    public string reservationId;
    public List<PopulationReservationAllocationSaveData> allocations = new List<PopulationReservationAllocationSaveData>();
    public List<string> reservedIndividualIds = new List<string>();
    public bool hasExpiryTurn;
    public int expiryTurn;
    public int reservationKind;
    public string reservationOwnerId;
    public string reservationOwnerType;
    public bool isBusyActive;
}

[Serializable]
public class PlayersPopulationSaveData
{
    public int maxPopulation;
    public int startingPopulation;
    public bool ignoreMaxDuringInitialization;
    public bool zeroOrLessMaxMeansUnlimited;
    public int startingFamilyCount;

    public List<PopulationGroupSaveData> groups = new List<PopulationGroupSaveData>();
    public List<PopulationReservationSaveData> reservations = new List<PopulationReservationSaveData>();
}

[Serializable]
public class IndividualSaveData
{
    public string id;
    public Gender gender;
    public int ageInTurns;
    public float health01;
    public AgeGroup aggregatedAgeGroup;
    public string aggregatedGroupGuid;
    public bool isAlive;
    public bool isBusy;
    public string familyId;
    public string surname;
    public string lineageId;
}

[Serializable]
public class FamilySaveData
{
    public string familyId;
    public string familyName;
    public string partnerAId;
    public string partnerBId;
    public List<string> childrenIds = new List<string>();
}

[Serializable]
public class PlayerFamilySimulationSaveData
{
    public List<IndividualSaveData> individuals = new List<IndividualSaveData>();
    public List<FamilySaveData> families = new List<FamilySaveData>();
    public PregnancyServiceSaveData pregnancyData;
}