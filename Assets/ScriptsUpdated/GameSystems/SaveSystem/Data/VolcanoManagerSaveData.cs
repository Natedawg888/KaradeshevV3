using System;
using System.Collections.Generic;

[Serializable]
public class VolcanoManagerSaveData
{
    public int version = 2;

    public int queuedAdvanceTurns;
    public int mountainAwakeningsUsedThisTurn;
    public int newEruptionsUsedThisTurn;

    public int registeredVolcanoCount;
    public int eruptingVolcanoCount;

    // Actual volcano runtime states.
    public List<VolcanoTileRuntimeSaveData> volcanoStates = new List<VolcanoTileRuntimeSaveData>();
}