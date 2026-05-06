using System;
using System.Collections.Generic;

[Serializable]
public class PlayerPopulationStatisticSaveData
{
    public int historyLimit;
    public List<PopulationSnapshot> history = new List<PopulationSnapshot>();
}