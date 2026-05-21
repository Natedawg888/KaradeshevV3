using System;

[Serializable]
public class SolarStormSaveData
{
    public int version = 1;

    public bool isActive;
    public int severityValue;
    public int turnsRemaining;
    public int totalTurns;
}
