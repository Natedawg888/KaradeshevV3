using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

// how much to bias each biome
[System.Serializable]
public class EnvironmentWeightAdjustment
{
    public EnvironmentType environmentType;
    [Range(0, 20)]
    public int weightAdjustment = 1;
}

// how much to bias each tile-shape
[System.Serializable]
public class TileTypeWeightAdjustment
{
    public EnvironmentTileType tileType;
    [Range(0, 20)]
    public int weightAdjustment = 1;
}

public enum SeasonVisualType
{
    Custom = 0,
    Spring = 1,
    Summer = 2,
    Autumn = 3,
    Winter = 4,
    Wet = 5,
    Dry = 6,
    Rainy = 7,
    Monsoon = 8,
    Storm = 9,
    Heatwave = 10,
    ColdSnap = 11,
    Frozen = 12
}

[System.Serializable]
public class ClimateShiftSettings
{
    public string shiftName = "Normal";

    [Header("Offsets while this season is active")]
    public float seasonTemperatureOffset = 0f;
    [Range(-1f, 1f)]
    public float seasonHumidityOffset = 0f;

    [Header("Persistent drift applied when this season starts")]
    public float temperatureDriftOnEnter = 0f;
    public float humidityDriftOnEnter = 0f;

    [Header("Latitudinal rebalance for larger world shifts")]
    public float equatorTemperatureOffset = 0f;
    public float poleTemperatureOffset = 0f;

    [Range(-1f, 1f)]
    public float equatorHumidityOffset = 0f;
    [Range(-1f, 1f)]
    public float poleHumidityOffset = 0f;

    [Header("Multiplier for planetary seasonal strength")]
    public float seasonalTemperatureStrengthMultiplier = 1f;
    public float seasonalHumidityStrengthMultiplier = 1f;
}

[System.Serializable]
public class SeasonDefinition
{
    public string seasonID = "summer";
    public string displayName = "Summer";
    public SeasonVisualType visualType = SeasonVisualType.Summer;

    [Min(1)]
    public int turns = 30;

    [Header("UI")]
    public Sprite iconSprite;
    public Sprite fillSprite;

    [Header("Climate")]
    public ClimateShiftSettings climateShift = new ClimateShiftSettings();
}

[System.Serializable]
public class SeasonCycleEntry
{
    public SeasonDefinition season = new SeasonDefinition();

    [Min(1)]
    public int repeatCount = 1;
}

[System.Serializable]
public class PresetSeasonSet
{
    public string setName = "Default";
    public int setID = 0;
    public bool isDefault = true;

    [Tooltip("The cycle order. RepeatCount lets you do Summer, Summer, Rainy, Rainy without duplicating data.")]
    public List<SeasonCycleEntry> cycle = new();
}

[System.Serializable]
public class EnvironmentPreset
{
    public string presetName;
    public int presetID;
    public bool isMainPreset;

    public List<EnvironmentWeightAdjustment> environmentWeightAdjustments = new();
    public List<TileTypeWeightAdjustment> tileTypeWeightAdjustments = new();

    public List<AnimalDefinition> animalsForThisPreset = new();

    [Header("Animal Spawn / Population")]
    public AnimalSpawnPresetSettings animalSpawnSettings = new();

    [Header("Planetary climate section")]
    public PlanetarySectionSettings planetarySection;

    [Header("Season Sets")]
    public List<PresetSeasonSet> seasonSets = new();

    [Header("Season Start")]
    [Tooltip("If true, a new map using this preset starts at a random season in the season cycle instead of always index 0.")]
    public bool randomizeStartingSeason = true;

    [Tooltip("If true, the starting season can begin part-way through its duration.")]
    public bool randomizeTurnsIntoStartingSeason = false;

    [Header("Weather / Disasters")]
    public WeatherPresetSettings weatherSettings = new();
}

public class EnvironmentPresetManager : MonoBehaviour
{
    public static EnvironmentPresetManager Instance { get; private set; }

    [Header("All Your Presets")]
    public List<EnvironmentPreset> environmentPresets;

    [Header("Weather Preset Application")]
    [SerializeField] private bool applyWeatherSettingsOnStart = true;
    [SerializeField] private bool applyWeatherSettingsWhenPresetChanges = true;
    [SerializeField] private bool debugWeatherPresetApply = false;

    private EnvironmentPreset _currentMainPreset;

    private readonly AnimalSpawnPresetSettings _defaultAnimalSpawnSettings = new AnimalSpawnPresetSettings();

    public event Action<EnvironmentPreset> OnPresetApplied;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _currentMainPreset = environmentPresets.FirstOrDefault(p => p.isMainPreset);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (applyWeatherSettingsOnStart)
            ApplyCurrentWeatherSettingsToScene();
    }

    public void ApplyPreset(int presetID)
    {
        var p = environmentPresets.Find(x => x.presetID == presetID);
        if (p != null)
        {
            _currentMainPreset = p;

            if (applyWeatherSettingsWhenPresetChanges)
                ApplyCurrentWeatherSettingsToScene();

            OnPresetApplied?.Invoke(_currentMainPreset);
        }
    }

    public EnvironmentPreset GetCurrentPreset()
    {
        return _currentMainPreset;
    }

    public PresetSeasonSet GetDefaultSeasonSet()
    {
        if (_currentMainPreset == null || _currentMainPreset.seasonSets == null || _currentMainPreset.seasonSets.Count == 0)
            return null;

        var set = _currentMainPreset.seasonSets.FirstOrDefault(x => x.isDefault);
        return set ?? _currentMainPreset.seasonSets[0];
    }

    public PresetSeasonSet GetSeasonSet(int setID)
    {
        if (_currentMainPreset == null || _currentMainPreset.seasonSets == null)
            return null;

        return _currentMainPreset.seasonSets.FirstOrDefault(x => x.setID == setID);
    }

    public AnimalSpawnPresetSettings GetAnimalSpawnSettings()
    {
        if (_currentMainPreset == null || _currentMainPreset.animalSpawnSettings == null)
            return _defaultAnimalSpawnSettings;

        return _currentMainPreset.animalSpawnSettings;
    }

    public float GetInitialSpawnChancePerTile()
        => GetAnimalSpawnSettings().initialSpawnChancePerTile;

    public float GetInitialCarnivoreChanceRelativeToHerbivores()
        => GetAnimalSpawnSettings().initialCarnivoreChanceRelativeToHerbivores;

    public int GetMaxInitialSpawnGroups()
        => GetAnimalSpawnSettings().maxInitialSpawnGroups;

    public Vector2Int GetInitialGroupsPerTileRange()
        => GetAnimalSpawnSettings().initialGroupsPerTileRange;

    public int GetMaxSupportedSpeciesOnMap()
        => GetAnimalSpawnSettings().maxSupportedSpeciesOnMap;

    public int GetMaxSpeciesPerDietSizeBucket()
        => GetAnimalSpawnSettings().maxSpeciesPerDietSizeBucket;

    public float GetWorldSpeciesGroupCapMultiplier()
        => GetAnimalSpawnSettings().worldSpeciesGroupCapMultiplier;

    public int GetMaxTotalGroups()
        => GetAnimalSpawnSettings().maxTotalGroups;

    public int GetEnvironmentWeight(EnvironmentType env)
    {
        if (_currentMainPreset == null) return 1;
        var adj = _currentMainPreset.environmentWeightAdjustments
            .FirstOrDefault(x => x.environmentType == env);
        return (adj != null ? adj.weightAdjustment : 1);
    }

    public int GetTileTypeWeight(EnvironmentTileType ttype)
    {
        if (_currentMainPreset == null) return 1;
        var adj = _currentMainPreset.tileTypeWeightAdjustments
            .FirstOrDefault(x => x.tileType == ttype);
        return (adj != null ? adj.weightAdjustment : 1);
    }

    public int GetTileSizeWeight(TileSize size)
    {
        if (_currentMainPreset == null) return 1;
        var ps = _currentMainPreset.planetarySection;
        if (ps == null || ps.tileSizeWeightAdjustments == null) return 1;

        var adj = ps.tileSizeWeightAdjustments.FirstOrDefault(x => x.tileSize == size);
        return (adj != null ? adj.weightAdjustment : 1);
    }

    public string GetCurrentPresetName()
    {
        if (_currentMainPreset == null || string.IsNullOrWhiteSpace(_currentMainPreset.presetName))
            return "Unknown";

        return _currentMainPreset.presetName;
    }

    [ContextMenu("Apply Current Weather Settings To Scene")]
    public void ApplyCurrentWeatherSettingsToScene()
    {
        if (_currentMainPreset == null)
            _currentMainPreset = environmentPresets.FirstOrDefault(p => p.isMainPreset);

        if (_currentMainPreset == null || _currentMainPreset.weatherSettings == null)
            return;

        WeatherPresetSettings weather = _currentMainPreset.weatherSettings;

        CloudSimulationSystem cloud = FindObjectOfType<CloudSimulationSystem>();
        if (cloud != null)
            cloud.ApplyPresetSettings(weather.clouds);

        RainSimulationSystem rain = FindObjectOfType<RainSimulationSystem>();
        if (rain != null)
            rain.ApplyPresetSettings(weather.rain);

        StormSimulationSystem storm = FindObjectOfType<StormSimulationSystem>();
        if (storm != null)
            storm.ApplyPresetSettings(weather.storms);

        TornadoSimulationSystem tornado = FindObjectOfType<TornadoSimulationSystem>();
        if (tornado != null)
            tornado.ApplyPresetSettings(weather.tornados);

        WeatherFireSystem fire = FindObjectOfType<WeatherFireSystem>();
        if (fire != null)
            fire.ApplyPresetSettings(weather.fire);

        TsunamiSimulationSystem tsunami = FindObjectOfType<TsunamiSimulationSystem>();
        if (tsunami != null)
            tsunami.ApplyPresetSettings(weather.tsunamis);

        FloodSimulationSystem flood = FindObjectOfType<FloodSimulationSystem>();
        if (flood != null)
            flood.ApplyPresetSettings(weather.floods);

        EarthquakeSimulationSystem earthquake = FindObjectOfType<EarthquakeSimulationSystem>();
        if (earthquake != null)
            earthquake.ApplyPresetSettings(weather.earthquakes);

        if (debugWeatherPresetApply)
        {
            Debug.Log(
                $"[EnvironmentPresetManager] Applied weather settings for preset '{GetCurrentPresetName()}'.");
        }
    }
}