using System;
using System.Collections.Generic;

[Serializable]
public class ShelterBirthOrderSaveData
{
    public string orderId;
    public string motherId;
    public string fatherId;
    public string familyId;
    public int turnsRemaining;
    public string reservationId;
}

[Serializable]
public class ShelterRuntimeSaveData
{
    public string buildingSaveableID;

    public bool pauseBirthing;
    public int turnsUntilNextPairing;

    public List<string> housedFamilyIds = new List<string>();
    public List<string> guestIndividualIds = new List<string>();
    public List<string> movedOutIndividualIds = new List<string>();

    public List<ShelterBirthOrderSaveData> activeOrders = new List<ShelterBirthOrderSaveData>();
}

[Serializable]
public class PlayerShelterSaveData
{
    public List<ShelterRuntimeSaveData> shelters = new List<ShelterRuntimeSaveData>();
}