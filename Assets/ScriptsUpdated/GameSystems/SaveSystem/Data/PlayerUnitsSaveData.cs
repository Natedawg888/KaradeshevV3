using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PendingLootStackSaveData
{
    public string resourceID;
    public int amount;
}

[Serializable]
public class TileUnitGroupSaveData
{
    public string tileSaveableID;
    public Vector2Int tileGridPosition;

    public string groupId;
    public string unitTypeID;
    public string groupName;

    public int unitCount;
    public int maxHealth;
    public int currentHealth;

    public int skillLevel;

    public int bonusHealth;
    public float bonusMovementSpeed;
    public int bonusPower;
    public int bonusDefense;
    public int bonusAgility;
    public int bonusAccuracy;
    public int bonusRange;
    public int bonusStealth;

    public string populationReservationId;
    public int reservedPopulation;

    public int expiryTurn;
    public int missedUpkeepTurns;
    public int upkeepStartTurn;

    public List<Vector2Int> plannedPathGridPositions = new();
    public List<float> plannedStepTurnCosts = new();
    public int currentPathIndex;
    public float remainingTurnCostOnCurrentStep;

    public bool isPatrolling;
    public List<Vector2Int> patrolLoopGridPositions = new();
    public List<float> patrolLoopStepTurnCosts = new();

    public string activeActionAssetName;
    public Vector2Int activeActionTargetGrid;
    public int remainingActionTurns;

    public bool hasPendingScoutResults;

    public bool hasPendingTrackingResults;
    public int lastTrackingMarkerTurns;

    public MeleeTargetType activeMeleeTargetType;
    public int activeMeleeTargetAnimalId;
    public string activeMeleeTargetUnitGroupId;

    public bool meleeRetaliatedLastTick;
    public bool meleeTargetFledLastTick;

    public List<PendingLootStackSaveData> pendingLoot = new();
}

[Serializable]
public class PlayerUnitsSaveData
{
    public List<TileUnitGroupSaveData> groups = new();
}