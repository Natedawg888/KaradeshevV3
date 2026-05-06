using System;
using System.Collections.Generic;

[Serializable]
public class PlayerKnownResourcesSaveData
{
    public List<string> knownResourceIDs = new();
}

[Serializable]
public class PlayerKnownCraftingSaveData
{
    public List<string> knownCraftingIDs = new();
}

[Serializable]
public class PlayerKnownProductionSaveData
{
    public List<string> knownProductionIDs = new();
}

[Serializable]
public class PlayerKnownBuildingsSaveData
{
    public List<string> knownBuildingIDs = new();
}

[Serializable]
public class PlayerKnownUnitsSaveData
{
    public List<string> knownUnitIDs = new();
}

[Serializable]
public class PlayerKnownTechnologySaveData
{
    public List<string> knownTechnologyIDs = new();
}