using System;
using System.Collections.Generic;

[Serializable]
public class EnvironmentGameSaveData
{
    public List<TileSaveData> tiles = new List<TileSaveData>();
    public List<BuildingTileSaveData> buildings = new List<BuildingTileSaveData>();
    public List<ConstructionTileSaveData> constructionTiles = new List<ConstructionTileSaveData>();

    public TurnSystemSaveData turnData;
    public SeasonManagerSaveData seasonData;
    public PlayerLevelSaveData playerLevelData;
    public PlayerInventorySaveData inventoryData;

    public PlayerKnownResourcesSaveData knownResourcesData;
    public PlayerKnownCraftingSaveData knownCraftingData;
    public PlayerKnownProductionSaveData knownProductionData;
    public PlayerKnownBuildingsSaveData knownBuildingsData;
    public PlayerKnownUnitsSaveData knownUnitsData;
    public PlayerKnownTechnologySaveData knownTechnologyData;

    public PlayersPopulationSaveData playersPopulationData;
    public PlayerFamilySimulationSaveData playerFamilySimulationData;

    public PlayerResearchSaveData playerResearchData;
    public PlayerPopulationStatisticSaveData playerPopulationStatisticData;
    public PlayerProfileSaveData playerProfileData;
    public CivilizationStateSaveData civilizationStateData;

    public AnimalSimulationSaveData animalSimulationData;
    public PlayerUnitsSaveData playerUnitsData;

    public PlayerDiscoverySaveData playerDiscoveryData;
    public PlayerSurveySaveData playerSurveyData;
    public PlayerGatheringSaveData playerGatheringData;

    public PlayerClearingSaveData playerClearingData;
    public PlayerCraftingSaveData playerCraftingData;
    public PlayerProductionSaveData playerProductionData;
    public PlayerShelterSaveData playerShelterData;
    public PlayerStorageSaveData playerStorageData;
    public PlayerTrainingSaveData playerTrainingData;
    public ClimateManagerSaveData climateData;
    public CameraPoseSaveData cameraPoseData;

    public EnvironmentGameSaveData(
        List<TileSaveData> tiles,
        List<BuildingTileSaveData> buildings,
        List<ConstructionTileSaveData> constructionTiles,
        TurnSystemSaveData turnData,
        SeasonManagerSaveData seasonData,
        PlayerLevelSaveData playerLevelData,
        PlayerInventorySaveData inventoryData,
        PlayerKnownResourcesSaveData knownResourcesData,
        PlayerKnownCraftingSaveData knownCraftingData,
        PlayerKnownProductionSaveData knownProductionData,
        PlayerKnownBuildingsSaveData knownBuildingsData,
        PlayerKnownUnitsSaveData knownUnitsData,
        PlayerKnownTechnologySaveData knownTechnologyData,
        PlayersPopulationSaveData playersPopulationData,
        PlayerFamilySimulationSaveData playerFamilySimulationData,
        PlayerResearchSaveData playerResearchData,
        PlayerPopulationStatisticSaveData playerPopulationStatisticData,
        PlayerProfileSaveData playerProfileData,
        CivilizationStateSaveData civilizationStateData,
        AnimalSimulationSaveData animalSimulationData,
        PlayerUnitsSaveData playerUnitsData,
        PlayerDiscoverySaveData playerDiscoveryData,
        PlayerSurveySaveData playerSurveyData,
        PlayerGatheringSaveData playerGatheringData,
        PlayerClearingSaveData playerClearingData,
        PlayerCraftingSaveData playerCraftingData,
        PlayerProductionSaveData playerProductionData,
        PlayerShelterSaveData playerShelterData,
        PlayerStorageSaveData playerStorageData,
        PlayerTrainingSaveData playerTrainingData,
        ClimateManagerSaveData climateData,
        CameraPoseSaveData cameraPoseData)
    {
        this.tiles = tiles;
        this.buildings = buildings;
        this.constructionTiles = constructionTiles;
        this.turnData = turnData;
        this.seasonData = seasonData;
        this.playerLevelData = playerLevelData;
        this.inventoryData = inventoryData;

        this.knownResourcesData = knownResourcesData;
        this.knownCraftingData = knownCraftingData;
        this.knownProductionData = knownProductionData;
        this.knownBuildingsData = knownBuildingsData;
        this.knownUnitsData = knownUnitsData;
        this.knownTechnologyData = knownTechnologyData;

        this.playersPopulationData = playersPopulationData;
        this.playerFamilySimulationData = playerFamilySimulationData;

        this.playerResearchData = playerResearchData;
        this.playerPopulationStatisticData = playerPopulationStatisticData;
        this.playerProfileData = playerProfileData;
        this.civilizationStateData = civilizationStateData;

        this.animalSimulationData = animalSimulationData;
        this.playerUnitsData = playerUnitsData;

        this.playerDiscoveryData = playerDiscoveryData;
        this.playerSurveyData = playerSurveyData;
        this.playerGatheringData = playerGatheringData;

        this.playerClearingData = playerClearingData;
        this.playerCraftingData = playerCraftingData;
        this.playerProductionData = playerProductionData;
        this.playerShelterData = playerShelterData;
        this.playerStorageData = playerStorageData;
        this.playerTrainingData = playerTrainingData;
        this.climateData = climateData;
        this.cameraPoseData = cameraPoseData;
    }
}