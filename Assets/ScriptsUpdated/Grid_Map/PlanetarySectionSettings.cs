using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class TileSizeWeightAdjustment
{
    public TileSize tileSize;

    [Range(0, 20)]
    public int weightAdjustment = 1; // 0 = effectively disable that size
}

[System.Serializable]
public class PlanetarySectionSettings
{
    [Header("Latitude section of planet")]
    [Range(-90f, 90f)] public float minLatitudeDeg = -10f;
    [Range(-90f, 90f)] public float maxLatitudeDeg = 10f;

    [Header("Base temperatures (°C) on the planet")]
    public float equatorTemperature = 28f;
    public float poleTemperature = -10f;

    [Header("Base humidity (0..1) on the planet")]
    [Range(0f, 1f)] public float equatorHumidity = 0.8f;
    [Range(0f, 1f)] public float poleHumidity = 0.3f;

    [Header("Local variation INSIDE this map section")]
    public float localTemperatureRange = 2f;
    [Range(0f, 0.5f)] public float localHumidityRange = 0.1f;

    [Header("Seasonal strength")]
    [Range(0f, 2f)] public float seasonalTemperatureStrength = 1f;
    [Range(0f, 2f)] public float seasonalHumidityStrength = 1f;

    // =========================
    // NEW: MAP GENERATION OVERRIDES
    // =========================
    [Header("Map generation (noise)")]
    public float noiseScale = 0.1f;
    [Range(1, 8)] public int octaves = 4;
    [Range(0f, 1f)] public float persistence = 0.5f;
    public float lacunarity = 2f;

    [Header("Land mass / threshold")]
    [Range(0f, 0.6f)] public float landThreshold = 0.5f;

    [Header("Noise cleanup")]
    [Range(4, 8)] public int flipLimit = 5;

    // =========================
    // NEW: TILE SIZE WEIGHTS (for MapTilePlacer land-fill)
    // =========================
    [Header("Land-fill tile size weights")]
    public List<TileSizeWeightAdjustment> tileSizeWeightAdjustments = new();

    [Header("Rivers")]
    public bool placeRivers = true;

    [Tooltip("If true, MapTilePlacer.branchSplitChance will be overridden by this preset.")]
    public bool overrideBranchSplitChance = false;

    [Range(0f, 1f)]
    public float presetBranchSplitChance = 0.1f;

    [Header("Seasonal Task Difficulty (Preset)")]
    public bool useSeasonalTaskDifficulty = true;

    [Header("Failure")]
    // % points added to failure chance
    [Range(-50f, 50f)] public float springFailAdd = 0f;
    [Range(-50f, 50f)] public float summerFailAdd = -5f;
    [Range(-50f, 50f)] public float autumnFailAdd = 2f;
    [Range(-50f, 50f)] public float winterFailAdd = 8f;

    [Header("Turns")]
    // turns multiplier
    [Range(0.25f, 3f)] public float springTurnsMult = 1f;
    [Range(0.25f, 3f)] public float summerTurnsMult = 0.9f;
    [Range(0.25f, 3f)] public float autumnTurnsMult = 1.05f;
    [Range(0.25f, 3f)] public float winterTurnsMult = 1.2f;

    public PlanetaryForcingSettings planetaryForcing = new PlanetaryForcingSettings();

    // =========================
    // EARTHQUAKES / FAULT LINES
    // =========================
    [Header("Earthquakes / Fault Lines")]
    public bool canHaveFaultLines = true;

    [Range(0f, 1f)]
    public float faultLineMapChance = 0.65f;

    [Min(0)] public int minFaultLines = 1;
    [Min(0)] public int maxFaultLines = 2;

    [Min(1)] public int faultLineWidthBlocks = 1;

    [Range(0f, 1f)]
    public float faultLineWiggleChance = 0.35f;

    [Range(0f, 1f)]
    public float earthquakeChancePerTurn = 0.015f;

    [Header("Earthquake Magnitude")]
    public Vector2 earthquakeMagnitudeRange = new Vector2(2.5f, 8.5f);
}

[System.Serializable]
public class AnimalSpawnPresetSettings
{
    [Header("Initial World Spawn")]
    [Range(0f, 1f)] public float initialSpawnChancePerTile = 0.25f;
    [Range(0f, 1f)] public float initialCarnivoreChanceRelativeToHerbivores = 0.3f;
    [Min(0)] public int maxInitialSpawnGroups = 80;
    public Vector2Int initialGroupsPerTileRange = new Vector2Int(0, 2);

    [Header("Initial Species Diversity")]
    [Min(1)] public int maxSupportedSpeciesOnMap = 12;
    [Min(1)] public int maxSpeciesPerDietSizeBucket = 2;

    [Header("Species Group Cap Scaling")]
    [Tooltip("Multiplies each species' maxLiveGroupsOnMap for this world. 1 = default, 0.5 = half, 2 = double.")]
    [Min(0.1f)] public float worldSpeciesGroupCapMultiplier = 1f;

    [Header("Population Cap (overall live groups)")]
    [Min(1)] public int maxTotalGroups = 150;
}

// Jupiter's gravity amplitude-modulates Earth's eccentricity cycle (dominant 100kyr driver)
// and contributes to apsidal precession (perihelion timing drift).
[System.Serializable]
public class JupiterGravitationalInfluence
{
    public bool enabled = true;
    [Range(0f, 1f)] public float strength = 1f;

    [Header("Eccentricity Amplitude Modulation")]
    [Tooltip("Frequency of Jupiter's slow amplitude modulation envelope on the eccentricity cycle.")]
    public float eccModFrequency = 0.15f;
    [Tooltip("How deeply Jupiter modulates eccentricity amplitude (0=none, 1=can double or zero it).")]
    [Range(0f, 1f)] public float eccModDepth = 0.65f;

    [Header("Apsidal Precession Contribution")]
    [Tooltip("Jupiter's contribution to Earth's apsidal / perihelion precession rate.")]
    public float apsidalFrequency = 0.45f;
    [Range(0f, 1f)] public float apsidalAmplitude = 0.35f;
}

// Venus has the strongest close-approach perturbation of any planet.
// It drives the long ~400kyr eccentricity envelope and couples into the obliquity cycle.
[System.Serializable]
public class VenusGravitationalInfluence
{
    public bool enabled = true;
    [Range(0f, 1f)] public float strength = 1f;

    [Header("Long-Period Eccentricity Envelope")]
    [Tooltip("Frequency of Venus's very slow (~4x slower than base ecc cycle) amplitude envelope.")]
    public float longEccFrequency = 0.09f;
    [Tooltip("Depth of Venus's long eccentricity modulation.")]
    [Range(0f, 1f)] public float longEccDepth = 0.45f;

    [Header("Obliquity Coupling")]
    [Tooltip("Venus's orbital plane coupling introduces a secondary wobble into the obliquity signal.")]
    public float obliquityInfluenceFrequency = 0.72f;
    [Range(0f, 1f)] public float obliquityInfluenceAmplitude = 0.22f;
}

// Mars drives fast, small eccentricity perturbations and a near-resonance beat
// from its ~1.88 yr orbit. Its inclination also weakly couples into obliquity.
[System.Serializable]
public class MarsGravitationalInfluence
{
    public bool enabled = true;
    [Range(0f, 1f)] public float strength = 1f;

    [Header("Fast Eccentricity Perturbation")]
    [Tooltip("Mars drives higher-frequency, lower-amplitude eccentricity oscillations.")]
    public float eccPerturbFrequency = 2.35f;
    [Range(0f, 1f)] public float eccPerturbAmplitude = 0.18f;

    [Header("Inclination / Obliquity Coupling")]
    [Tooltip("Mars's orbital inclination weakly couples into Earth's obliquity signal.")]
    public float oblPerturbFrequency = 1.65f;
    [Range(0f, 1f)] public float oblPerturbAmplitude = 0.12f;

    [Header("Near-Resonance Beat (2:1)")]
    [Tooltip("The near 2:1 Earth-Mars orbital resonance creates a high-frequency beat on both ecc and precession.")]
    public float resonanceBeatFrequency = 3.8f;
    [Range(0f, 1f)] public float resonanceBeatAmplitude = 0.08f;
}

// Saturn works with Jupiter to drive eccentricity sub-cycles (~95kyr and ~125kyr bands).
// Its near 5:2 resonance with Jupiter creates the long-period great inequality beat,
// and it provides the second-largest contribution to Earth's apsidal precession rate.
[System.Serializable]
public class SaturnGravitationalInfluence
{
    public bool enabled = true;
    [Range(0f, 1f)] public float strength = 1f;

    [Header("Secondary Eccentricity Modulation")]
    [Tooltip("Saturn's amplitude modulation on the eccentricity cycle. Different frequency from Jupiter gives the ~95/125kyr sub-cycles.")]
    public float eccModFrequency = 0.22f;
    [Tooltip("Depth of Saturn's eccentricity modulation. Weaker than Jupiter due to greater distance.")]
    [Range(0f, 1f)] public float eccModDepth = 0.40f;

    [Header("Jupiter-Saturn Great Inequality Beat")]
    [Tooltip("The near 5:2 orbital resonance between Jupiter and Saturn creates a very slow long-period beat on eccentricity.")]
    public float greatInequalityFrequency = 0.06f;
    [Range(0f, 1f)] public float greatInequalityEccAmplitude = 0.30f;

    [Header("Obliquity Contribution")]
    [Tooltip("Saturn's mass contribution to the solar system invariable plane slowly modulates Earth's obliquity.")]
    public float obliquityInfluenceFrequency = 0.55f;
    [Range(0f, 1f)] public float obliquityInfluenceAmplitude = 0.15f;
}

[System.Serializable]
public class PlanetaryForcingSettings
{
    public bool enabled = true;

    [Header("Timing")]
    [Min(1)] public int masterCycleTurns = 320;
    [Min(0)] public int rebuildIntervalTurns = 8;
    [Range(0f, 1f)] public float phaseOffset01 = 0f;

    [Header("Cycle Frequencies (relative to master cycle)")]
    public float eccentricityFrequency = 0.35f;
    public float obliquityFrequency = 1.00f;
    public float precessionFrequency = 1.80f;

    [Header("Eccentricity (orbital shape)")]
    public float eccentricityMeanTempAmplitude = 2.0f;
    [Range(0f, 1f)] public float eccentricityMeanHumidityAmplitude = 0.06f;
    public float eccentricitySeasonStrengthAmplitude = 0.35f;

    [Header("Obliquity / Axial Tilt")]
    public float obliquityEquatorTempAmplitude = -2.0f;
    public float obliquityPoleTempAmplitude = 6.0f;
    [Range(0f, 1f)] public float obliquityEquatorHumidityAmplitude = -0.03f;
    [Range(0f, 1f)] public float obliquityPoleHumidityAmplitude = 0.06f;
    public float obliquitySeasonStrengthAmplitude = 0.55f;

    [Header("Precession / Axis Orientation")]
    public float precessionNorthSouthTempBiasAmplitude = 3.0f;
    [Range(0f, 1f)] public float precessionNorthSouthHumidityBiasAmplitude = 0.08f;
    public float precessionMeanTempAmplitude = 0.75f;
    [Range(0f, 1f)] public float precessionMeanHumidityAmplitude = 0.03f;

    [Header("Planetary Gravitational Perturbations")]
    public JupiterGravitationalInfluence jupiterGravity = new JupiterGravitationalInfluence();
    public VenusGravitationalInfluence venusGravity = new VenusGravitationalInfluence();
    public MarsGravitationalInfluence marsGravity = new MarsGravitationalInfluence();
    public SaturnGravitationalInfluence saturnGravity = new SaturnGravitationalInfluence();
}

[System.Serializable]
public struct PlanetaryForcingSample
{
    public float meanTemperatureOffset;
    public float meanHumidityOffset;

    public float equatorTemperatureOffset;
    public float poleTemperatureOffset;

    public float equatorHumidityOffset;
    public float poleHumidityOffset;

    public float northSouthTemperatureBias;
    public float northSouthHumidityBias;

    public float seasonalTemperatureStrengthMultiplier;
    public float seasonalHumidityStrengthMultiplier;

    public static PlanetaryForcingSample Default => new PlanetaryForcingSample
    {
        meanTemperatureOffset = 0f,
        meanHumidityOffset = 0f,
        equatorTemperatureOffset = 0f,
        poleTemperatureOffset = 0f,
        equatorHumidityOffset = 0f,
        poleHumidityOffset = 0f,
        northSouthTemperatureBias = 0f,
        northSouthHumidityBias = 0f,
        seasonalTemperatureStrengthMultiplier = 1f,
        seasonalHumidityStrengthMultiplier = 1f
    };
}

[System.Serializable]
public class WeatherPresetSettings
{
    [Header("Clouds")]
    public CloudPresetSettings clouds = new();

    [Header("Rain")]
    public RainPresetSettings rain = new();

    [Header("Storms")]
    public StormPresetSettings storms = new();

    [Header("Tornados")]
    public TornadoPresetSettings tornados = new();

    [Header("Fire")]
    public FirePresetSettings fire = new();

    [Header("Tsunamis")]
    public TsunamiPresetSettings tsunamis = new();

    [Header("Floods")]
    public FloodPresetSettings floods = new();

    [Header("Earthquakes")]
    public EarthquakeWeatherPresetSettings earthquakes = new();
}

[System.Serializable]
public class CloudPresetSettings
{
    public bool overrideClouds = true;

    [Range(0f, 1f)] public float lowCloudHumidityThreshold = 0.45f;
    [Range(0f, 1f)] public float midCloudHumidityThreshold = 0.65f;
    [Range(0f, 1f)] public float highCloudHumidityThreshold = 0.82f;

    [Range(0f, 1f)] public float baseFormationChance = 0.08f;
    [Range(0f, 1f)] public float neighbourFormationBonus = 0.12f;
    [Range(0f, 1f)] public float dryDissipationChanceMultiplier = 0.35f;
    [Range(0f, 1f)] public float humidGrowthChanceMultiplier = 0.15f;

    [Min(0)] public int windSpeedTilesPerStep = 1;
    [Range(0f, 1f)] public float lateralShuffleChance = 0.15f;
    [Range(0f, 1f)] public float windDirectionChangeChancePerTurn = 0.2f;
}

[System.Serializable]
public class RainPresetSettings
{
    public bool overrideRain = true;

    [Range(0f, 1f)] public float rainHumidityThreshold = 0.45f;
    [Range(0f, 1f)] public float initialRainChargeFromHumidity = 0.10f;
    [Range(0f, 1f)] public float rainChargeGainPerStep = 0.20f;
    [Range(0f, 1f)] public float rainChargeLossPerStepWhenRaining = 0.35f;
    [Range(0f, 1f)] public float rainChargePassiveLossPerStep = 0.02f;
    [Range(0f, 1f)] public float rainStartChargeThreshold = 0.80f;
    [Range(0f, 1f)] public float rainStopChargeThreshold = 0.45f;

    public bool allowLowCloudsToStartRain = false;
    [Min(0)] public int maxNewRainCellsPerStep = 12;

    public bool useRainIntensity = true;
    [Range(0f, 1f)] public float minimumActiveRainIntensity = 0.25f;
    [Range(0f, 1f)] public float lightRainMaxIntensity = 0.45f;
    [Range(0f, 1f)] public float heavyRainMinIntensity = 0.75f;
}

[System.Serializable]
public class StormPresetSettings
{
    public bool overrideStorms = true;

    [Range(0f, 1f)] public float stormHumidityThreshold = 0.70f;
    public float stormTemperatureDifferenceThreshold = 6f;
    [Range(0f, 1f)] public float initialStormIntensityFromWeather = 0.35f;
    [Range(0f, 1f)] public float stormStartIntensityThreshold = 0.65f;
    [Range(0f, 1f)] public float stormStopIntensityThreshold = 0.35f;
    [Range(0f, 1f)] public float stormIntensityGainPerStep = 0.18f;
    [Range(0f, 1f)] public float stormIntensityLossPerStep = 0.10f;

    [Range(0f, 1f)] public float stormCloudDarknessStrength = 0.35f;
    [Min(1)] public int maxStormBandCentersPerStep = 24;
    [Range(0f, 1f)] public float minStormIntensityForBands = 0.55f;
}

[System.Serializable]
public class TornadoPresetSettings
{
    public bool overrideTornados = true;

    [Range(0f, 1f)] public float tornadoBaseSpawnChancePerCandidateStep = 0.01f;
    [Range(0f, 1f)] public float tornadoStormIntensityThreshold = 0.70f;
    [Range(0f, 1f)] public float tornadoHumidityThreshold = 0.72f;
    public float tornadoTemperatureDifferenceThreshold = 8f;
    public CloudSimulationSystem.CloudDensity minimumCloudDensityToSpawn = CloudSimulationSystem.CloudDensity.Mid;
    [Range(0f, 1f)] public float highCloudSpawnChanceBonus = 0.05f;

    [Min(1)] public int tornadoMinLifetimeTurns = 2;
    [Min(1)] public int tornadoMaxLifetimeTurns = 5;
    [Min(1)] public int maxActiveTornadoes = 1;
    [Min(0)] public int minTornadoSpacingCells = 4;
    [Min(1)] public int maxNewTornadoesPerStep = 1;
    [Min(1)] public int maxSpawnCandidatesPerStep = 24;
}

[System.Serializable]
public class FirePresetSettings
{
    public bool overrideFire = true;

    public bool lightningCanStartFires = true;
    [Range(0f, 1f)] public float lightningFireStartChance = 0.25f;

    [Range(0f, 1f)] public float minIgnitionMultiplierAtFullRain = 0.20f;
    [Range(0f, 1f)] public float stormDampeningStrength = 0.35f;

    [Min(1)] public int environmentBurnTurns = 3;
    [Range(0f, 1f)] public float environmentDrynessIgnitionBonus = 0.35f;
    [Range(0f, 1f)] public float environmentHeatIgnitionBonus = 0.20f;
    [Range(0f, 1f)] public float environmentRainExtinguishChanceAtFullRain = 0.25f;

    public bool fireCanIgniteBuildings = true;
    [Min(1)] public int buildingBurnTurns = 3;
    [Min(0)] public int buildingDamagePerStep = 8;

    public bool fireCanSpread = true;
    public bool fireSpreadIncludesDiagonals = true;
    [Range(0f, 1f)] public float fireSpreadChanceOrthogonal = 0.20f;
    [Range(0f, 1f)] public float fireSpreadChanceDiagonal = 0.10f;
    [Range(0f, 1f)] public float fireSpreadRainPenaltyStrength = 0.75f;
    [Range(0f, 2f)] public float fireSpreadWindBiasStrength = 0.75f;
}

[System.Serializable]
public class TsunamiPresetSettings
{
    public bool overrideTsunamis = true;

    public bool canHaveTsunamis = true;
    [Range(0f, 1f)] public float tsunamiChancePerTurn = 0.005f;

    public float minStartEnergy = 8f;
    public float maxStartEnergy = 18f;
    [Min(0f)] public float energyLossPerStep = 1f;
    [Min(0f)] public float landEnergyLossMultiplier = 2f;
    [Min(0f)] public float extraEnergyLossPerCellCount = 0.01f;

    [Min(1)] public int maxStepsPerTsunami = 30;
    [Min(1)] public int waveWidthCells = 5;
    [Range(0f, 1f)] public float sideSpreadChance = 0.35f;

    public bool allowDiagonalDirections = false;
    public bool allowTsunamiIntoLakes = false;
}

[System.Serializable]
public class FloodPresetSettings
{
    public bool overrideFloods = true;

    public bool enableFlooding = true;
    public bool enableRainFlooding = true;
    public bool enableTsunamiFlooding = true;

    [Min(1)] public int maxActiveFloodCells = 2500;
    [Range(0f, 1f)] public float floodSpreadThreshold = 0.28f;
    [Range(0f, 1f)] public float floodSpreadAmount = 0.16f;
    [Range(0f, 1f)] public float floodSpreadLossMultiplier = 0.65f;
    [Range(0f, 1f)] public float landAbsorptionMultiplier = 0.25f;
    [Range(0f, 1f)] public float beachAbsorptionMultiplier = 0.10f;

    [Range(0f, 1f)] public float baseDrainPerTurn = 0.04f;
    [Range(0f, 1f)] public float evaporationPerTurn = 0.015f;

    [Range(0f, 1f)] public float rainfallAccumulationPerRain01 = 0.20f;
    [Range(0f, 5f)] public float rainFloodThreshold = 0.80f;
    [Range(0f, 1f)] public float maxRainFloodInputPerTurn = 0.22f;

    [Range(0f, 2f)] public float tsunamiFloodInputMultiplier = 0.55f;
    [Range(0f, 1f)] public float tsunamiMaxFloodDepth = 0.85f;
}

[System.Serializable]
public class EarthquakeWeatherPresetSettings
{
    public bool overrideEarthquakes = true;

    [Range(0f, 1f)] public float earthquakeChancePerTurn = 0.015f;

    [Range(0f, 1f)] public float minEnergyGainPerTurn = 0.015f;
    [Range(0f, 1f)] public float maxEnergyGainPerTurn = 0.045f;
    [Min(0f)] public float faultLineEnergyGainMultiplier = 1.25f;
    [Range(0f, 1f)] public float stressSpikeChancePerTurn = 0.08f;
    public Vector2 stressSpikeEnergyGainRange = new Vector2(0.03f, 0.12f);

    [Range(0f, 1f)] public float maxEnergyChanceBonus = 0.35f;
    public Vector2 magnitudeRange = new Vector2(2.5f, 8.5f);
    [Range(0f, 1f)] public float energyMagnitudeWeight = 0.75f;

    public float minRadiusBlocks = 1.5f;
    public float maxRadiusBlocks = 7f;

    public bool requireFaultForNaturalEarthquakes = true;
}