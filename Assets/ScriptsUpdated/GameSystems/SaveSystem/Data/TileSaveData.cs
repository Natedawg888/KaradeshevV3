using System;
using UnityEngine;

[Serializable]
public class TileSaveData
{
    public SaveData tileData;
    public string tilePrefabName;
    public EnvironmentRuntimeSaveData environmentData;

    public TileSaveData(SaveData tileData, string tilePrefabName, EnvironmentRuntimeSaveData environmentData)
    {
        this.tileData = tileData;
        this.tilePrefabName = tilePrefabName;
        this.environmentData = environmentData;
    }
}