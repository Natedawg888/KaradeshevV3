using System;

[Serializable]
public class VolcanoTileRuntimeSaveData
{
    public int version = 1;

    // 0 Mountain, 1 Dormant, 2 Erupting.
    public int activityStateValue;

    public bool seeded;
    public bool canBecomeVolcano;
    public bool runtimeInitialized;

    public float energy01;

    public int stateTurns;
    public int eruptionTurnsRemaining;
    public int lowEnergyTurns;

    // Useful for debugging / matching.
    public int primaryCellX;
    public int primaryCellY;
    public bool hasPrimaryCell;
}