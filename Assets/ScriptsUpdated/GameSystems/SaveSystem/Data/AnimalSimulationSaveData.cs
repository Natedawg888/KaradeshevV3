using System;
using System.Collections.Generic;

[Serializable]
public class AnimalGroupSaveData
{
    public int id;
    public string speciesAssetName;

    public int size;
    public int ageInTurns;
    public int currentHealth;

    public float hunger;
    public float thirst;

    public TileCoord tile;

    public AnimalActionType lastAction;
    public int nextUpdateTurn;

    public bool isLeader;
    public int herdId;
    public int leaderGroupId;

    public bool isHunting;
    public int huntingTargetGroupId;
    public bool isTargetedByPredator;
    public int huntingEscapeCount;

    public int nextReproductionTurn;
    public bool isOnReproductionCooldown;

    public bool isInPredatorConflict;
    public int predatorConflictTargetGroupId;

    public int targetedByPredatorGroupId;

    public bool isFleeingFromThreat;
    public int fleeFromPredatorGroupId;
    public int fleeUntilDistanceTiles;
    public TileCoord fleeThreatLastKnownTile;
    public int fleeStepsRemaining;

    public bool hasWaterSearchMemory;
    public TileCoord lastWaterSearchPreviousTile;
    public TileCoord secondLastWaterSearchPreviousTile;
    public int waterSearchBacktrackAvoidanceTurns;

    public bool isRaidingPlayerTile;
    public TileCoord raidTargetTile;

    public bool isHuntingHumanUnits;
    public string huntingHumanUnitGroupId;

    public int resolvedHealthPerAnimal;
    public float resolvedAggression;
    public float resolvedFlightiness;
    public float resolvedHerding;
    public float resolvedStrength;
    public float resolvedDefense;
    public float resolvedSpeed;
    public float resolvedSense;
    public float resolvedStealth;
    public float resolvedBreedingFemaleFraction;

    public bool isTargetedByHumanUnits;
}

[Serializable]
public class AnimalSimulationSaveData
{
    public int nextGroupId;
    public bool hasCompletedInitialAnimalSpawn;
    public List<AnimalGroupSaveData> groups = new List<AnimalGroupSaveData>();
    public List<AnimalGroupSaveData> pendingSpawnTemplates = new List<AnimalGroupSaveData>();
}