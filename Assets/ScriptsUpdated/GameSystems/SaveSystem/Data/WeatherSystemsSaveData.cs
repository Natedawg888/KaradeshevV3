using System;
using System.Collections.Generic;

[Serializable]
public class WeatherSystemsSaveData
{
    public int version = 1;

    public WeatherGridManagerSaveData weatherGridData;
    public CloudSimulationSaveData cloudData;
    public RainSimulationSaveData rainData;
    public StormSimulationSaveData stormData;

    // Optional, if these systems exist in your project.
    public TornadoSimulationSaveData tornadoData;
    public LightningSimulationSaveData lightningData;
}

[Serializable]
public class WeatherGridManagerSaveData
{
    public int version = 1;

    // Usually do NOT save temperature/humidity if it comes from ClimateManager.
    // Save only runtime weather-grid flags/cooldowns if needed.
    public bool gridWasInitialized;
}

[Serializable]
public class CloudSimulationSaveData
{
    public int version = 2;

    public int windDirectionValue;
    public int windSpeedTilesPerStep;
    public bool seededFromValidWeather;

    public List<CloudCellSaveData> clouds = new List<CloudCellSaveData>();
}

[Serializable]
public class CloudCellSaveData
{
    public int x;
    public int y;

    // Main cloud state.
    // 0 = None, 1 = Low, 2 = Mid, 3 = High.
    public int densityValue;

    // Kept for old compatibility/debug. Not required for the enum restore.
    public float density01;

    // Visual height restore.
    public bool heightOffsetAssigned;
    public float heightOffset;

    // Runtime cloud modifiers.
    public float volcanicSoot01;
    public float externalDarkness01;
    public float stormDarkness01;
}

[Serializable]
public class RainSimulationSaveData
{
    public int version = 2;

    public int activeRainCellCount;

    public List<RainCellSaveData> rainCells = new List<RainCellSaveData>();
}

[Serializable]
public class RainCellSaveData
{
    public int x;
    public int y;

    // Old compatibility field. In v2 this mirrors intensity when raining.
    public float rain01;

    public float rainCharge01;

    public bool isRaining;
    public float rainIntensity01;

    // Debug/restore helper only. Actual visual kind is still derived after load.
    // 0 None, 1 NormalRain, 2 AcidRain, 3 AshFall.
    public int visualKindValue;
}

[Serializable]
public class StormSimulationSaveData
{
    public int version = 2;

    public int activeStormCellCount;

    public List<StormCellSaveData> stormCells = new List<StormCellSaveData>();
}

[Serializable]
public class StormCellSaveData
{
    public int x;
    public int y;

    public float intensity01;
    public bool isActive;

    // Kept for old compatibility. Your current StormSimulationSystem does not use storm lifetimes.
    public int remainingTurns;
}

[Serializable]
public class TornadoSimulationSaveData
{
    public int version = 1;

    public int nextTornadoId = 1;

    public List<TornadoCellSaveData> tornadoes = new List<TornadoCellSaveData>();
}

[Serializable]
public class TornadoCellSaveData
{
    public int tornadoId;
    public int x;
    public int y;
    public int lifetimeRemaining;
    public float strength01;
}

[Serializable]
public class LightningSimulationSaveData
{
    public int version = 1;

    public int nextBurstId = 1;

    public List<LightningChargeCellSaveData> chargeCells = new List<LightningChargeCellSaveData>();
    public List<QueuedLightningBurstSaveData> queuedBursts = new List<QueuedLightningBurstSaveData>();
}

[Serializable]
public class LightningChargeCellSaveData
{
    public int x;
    public int y;
    public float charge;
    public bool ready;
}

[Serializable]
public class QueuedLightningBurstSaveData
{
    public int burstId;

    public int originCellX;
    public int originCellY;

    public int totalStrikes;
    public int nextStrikeIndex;

    public float strikeIntervalSeconds;

    // Store remaining delay instead of absolute Time.time.
    public float remainingDelaySeconds;

    public float sourceStormIntensity01;
    public float sourceCloudSupport01;
    public float sourceRainSupport01;
    public float sourceCharge;

    public int preferredDirectionX;
    public int preferredDirectionY;

    public float directionalShiftChance;
}