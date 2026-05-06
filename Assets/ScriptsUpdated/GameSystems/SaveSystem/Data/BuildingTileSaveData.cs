using System;

[Serializable]
public class BuildingTileSaveData
{
    public SaveData saveData;
    public string prefabName;

    public BuildingTileSaveData(SaveData saveData, string prefabName)
    {
        this.saveData = saveData;
        this.prefabName = prefabName;
    }
}