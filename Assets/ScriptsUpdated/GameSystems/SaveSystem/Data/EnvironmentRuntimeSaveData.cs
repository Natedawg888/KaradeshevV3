using System;
using System.Collections.Generic;
using UnityEngine;

using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PendingLootSaveData
{
    public string resourceKey;
    public int amount;
}

[Serializable]
public class EnvironmentNodeResourceEntrySaveData
{
    public string resourceKey;
    public int amount;
}

[Serializable]
public class EnvironmentResourceNodeRuntimeSaveData
{
    public int maxVarietyCap;
    public int totalCapacity;

    public int maxEnvironmentHealth;
    public int currentEnvironmentHealth;
    public int environmentRecoveryPerTick;

    public int barrenRecoveryTurns;
    public int barrenRecoveryIncreasePerUse;
    public int barrenRecoveryClearThreshold;
    public bool allowImmediateClearOnOveruse;

    public bool isBarren;
    public int barrenTurnsLeft;

    public int turnsSinceLastExtraSpawn;
    public float outOfSeasonFavorMultiplier;

    public List<EnvironmentNodeResourceEntrySaveData> spawnedResources = new();
}

[Serializable]
public class EnvironmentRuntimeSaveData
{
    public string environmentID;
    public string environmentName;

    public EnvironmentType environmentType;
    public EnvironmentTileType environmentTileType;
    public TileSize tileSize;

    public string spawnedPrefabName;
    public float localYRotation;

    public VolcanoTileRuntimeSaveData volcanoData;

    public bool isDiscovered;
    public bool isSurveyed;
    public bool needsResurvey;

    public bool canExplore;
    public bool canBeManuallyCleared;

    public int discoveryTurnsRequired;
    public int requireDiscoveryPopulation;
    public float discoveryFailureChance;
    public int discoveryPopPenaltyOnFailure;
    public int discoveryTurnsLeft;
    public bool isBeingDiscovered;

    public int gatheringTurnsRequired;
    public int requireGatheringPopulation;
    public float gatheringFailureChance;
    public int gatheringPopPenaltyOnFailure;
    public int gatheringTurnsLeft;
    public bool isGathering;

    public int surveyTurnsRequired;
    public int requireSurveyPopulation;
    public int surveyTurnsLeft;
    public bool isSurveying;

    public int resurveyInterval;
    public int resurveyTurnsLeft;

    public bool pendingDiscoveryFailIcon;
    public bool pendingGatheringFailIcon;

    public List<TaskFailureData> pendingTaskFailures = new();
    public List<PendingLootSaveData> pendingLoot = new();

    public EnvironmentResourceNodeRuntimeSaveData resourceNodeData;
}