using System;
using System.Collections.Generic;

[Serializable]
public class StorageItemSaveData
{
    public string resourceID;
    public int amount;
    public int spoilageInterval;
    public int currentInterval;
}

[Serializable]
public class StorageBuildingSaveData
{
    public string buildingSaveableID;
    public int maxStorageCapacity;
    public float spoilageModifier;
    public string spoiledFoodResourceID;
    public List<StorageItemSaveData> storedResources = new List<StorageItemSaveData>();
}

[Serializable]
public class PlayerStorageSaveData
{
    public List<StorageBuildingSaveData> buildings = new List<StorageBuildingSaveData>();
}