using System;

[Serializable]
public class BuildingConstructionRuntimeSaveData
{
    public string buildingID;
    public string finalBuildingOverridePrefabName;

    public int turnsToComplete;
    public int turnsLeft;
    public int currentStageIndex;

    public string reservationId;
    public int reservedPopulation;
    public bool isActive;
    public bool startInMiddle;
}

[Serializable]
public class ConstructionTileSaveData
{
    public SaveData constructionTileData;
    public string constructionTilePrefabName;
    public BuildingConstructionRuntimeSaveData runtimeData;

    public ConstructionTileSaveData(
        SaveData constructionTileData,
        string constructionTilePrefabName,
        BuildingConstructionRuntimeSaveData runtimeData)
    {
        this.constructionTileData = constructionTileData;
        this.constructionTilePrefabName = constructionTilePrefabName;
        this.runtimeData = runtimeData;
    }
}
