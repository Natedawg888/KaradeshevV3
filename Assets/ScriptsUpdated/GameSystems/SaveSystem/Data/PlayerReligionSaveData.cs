using System;
using System.Collections.Generic;

[Serializable]
public class SpiritRuntimeStateSaveData
{
    public string spiritID;
    public bool accepted;
    public int favor;
    public int totalOfferingsGiven;
    public int lastOfferingTurn;
    public List<int> currentSacredAnimalGroupIds = new List<int>();
}

[Serializable]
public class PlayerReligionSaveData
{
    public BeliefSystemType currentBeliefSystem = BeliefSystemType.Animism;
    public int seasonsSinceSacredGroupRotation;
    public List<SpiritRuntimeStateSaveData> activeSpirits = new List<SpiritRuntimeStateSaveData>();
}