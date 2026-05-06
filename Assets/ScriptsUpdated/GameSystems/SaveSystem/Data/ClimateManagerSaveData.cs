using System;
using System.Collections.Generic;

[Serializable]
public class ClimateJobSaveData
{
    public int x;
    public int y;
    public EnvironmentType newEnvironment;
}

[Serializable]
public class ClimateManagerSaveData
{
    public int cols;
    public int rows;

    public float globalTemperatureOffset;
    public float globalHumidityOffset;

    public bool baseClimateInitialized;
    public bool hasValidInitialClimate;

    public int pendingJobIndex;

    public int planetaryForcingTurnCounter;
    public int lastPlanetaryForcedRebuildTurn;

    public float[] temperature;
    public float[] humidity;
    public bool[] temperatureValid;
    public bool[] humidityValid;

    public float[] baseTemperatureField;
    public float[] baseHumidityField;
    public float[] waterHumidityBoost;

    public int[] currentEnvironment;

    public List<ClimateJobSaveData> pendingJobs = new();
}