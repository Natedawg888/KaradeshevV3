using System;
using System.Collections.Generic;

[Serializable]
public class EnvironmentSaveMeta
{
    public bool isChunkedSave = true;
    public int version = 2;

    public int tileChunkCount;

    public bool hasBuildings;
    public bool hasConstruction;
    public bool hasCoreSystems;
    public bool hasKnowledge;
    public bool hasPopulation;
    public bool hasWorldSim;
    public bool hasJobs;
    public bool hasNotifications;
}

[Serializable]
public class TileChunkSaveData
{
    public List<TileSaveData> tiles = new List<TileSaveData>();
}

[Serializable]
public class BuildingSectionSaveData
{
    public List<BuildingTileSaveData> buildings = new List<BuildingTileSaveData>();
}

[Serializable]
public class ConstructionSectionSaveData
{
    public List<ConstructionTileSaveData> constructionTiles = new List<ConstructionTileSaveData>();
}

[Serializable]
public class CoreSystemsSectionSaveData
{
    public CameraPoseSaveData cameraPoseData;
    public TurnSystemSaveData turnData;
    public SeasonManagerSaveData seasonData;
    public ClimateManagerSaveData climateData;
    public WeatherSystemsSaveData weatherData;
    public PlayerLevelSaveData playerLevelData;
    public PlayerProfileSaveData playerProfileData;
    public CivilizationStateSaveData civilizationStateData;
    public int currentScore;
    public string gameId;

    public bool musicMuted;
    public float masterVolume = 1f;

    public float brightness = 0.5f;
}

[Serializable]
public class KnowledgeSectionSaveData
{
    public PlayerInventorySaveData inventoryData;
    public PlayerKnownResourcesSaveData knownResourcesData;
    public PlayerKnownCraftingSaveData knownCraftingData;
    public PlayerKnownProductionSaveData knownProductionData;
    public PlayerKnownBuildingsSaveData knownBuildingsData;
    public PlayerKnownUnitsSaveData knownUnitsData;
    public PlayerKnownTechnologySaveData knownTechnologyData;
    public PlayerResearchSaveData playerResearchData;
    public PlayerKnownSpiritsSaveData knownSpiritsData;
    public PlayerKnownRitualsSaveData knownRitualsData;
    public PlayerReligionSaveData playerReligionData;
}

[Serializable]
public class PopulationSectionSaveData
{
    public PlayersPopulationSaveData playersPopulationData;
    public PlayerFamilySimulationSaveData playerFamilySimulationData;
    public PlayerPopulationStatisticSaveData playerPopulationStatisticData;
    public PlayerDiseaseSaveData playerDiseaseData;
}

[Serializable]
public class WorldSimSectionSaveData
{
    public AnimalSimulationSaveData animalSimulationData;
    public PlayerUnitsSaveData playerUnitsData;
    public PlayerTrainingSaveData playerTrainingData;
    public LavaOverlaySaveData lavaOverlayData;
    public FloodSimulationSaveData floodSimulationData;

    public EarthquakeFaultLineSaveData earthquakeFaultLineData;
    public EarthquakeSimulationSaveData earthquakeSimulationData;

    public FireSimulationSaveData fireSimulationData;

    public TsunamiSimulationSaveData tsunamiSimulationData;

    public VolcanoManagerSaveData volcanoManagerData;

    public SolarStormSaveData solarStormData;
}

[Serializable]
public class JobsSectionSaveData
{
    public PlayerDiscoverySaveData playerDiscoveryData;
    public PlayerSurveySaveData playerSurveyData;
    public PlayerGatheringSaveData playerGatheringData;
    public PlayerClearingSaveData playerClearingData;
    public PlayerCraftingSaveData playerCraftingData;
    public PlayerProductionSaveData playerProductionData;
    public PlayerShelterSaveData playerShelterData;
    public PlayerStorageSaveData playerStorageData;
    public PlayerReligionBuildingsSaveData playerReligionBuildingsData;
    public PlayerTradeBuildingsSaveData playerTradeBuildingsData;
}