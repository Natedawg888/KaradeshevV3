using System;
using System.Collections.Generic;
using UnityEngine;

public enum MilitiaUnitCategory
{
    Land,
    Sea,
    Air
}

[Serializable]
public class MilitiaUnitAdvancementOption
{
    [Header("Target")]
    public MilitiaUnit targetUnit;

    [Header("Required Stats (effective group stats incl. bonuses)")]
    public bool  requireHealth;
    public int   minHealth;

    public bool  requireMovement;
    public float minMovement;

    public bool  requirePower;
    public int   minPower;

    public bool  requireDefense;
    public int   minDefense;

    public bool  requireAgility;
    public int   minAgility;

    public bool  requireAccuracy;
    public int   minAccuracy;

    public bool  requireRange;
    public int   minRange;

    public bool  requireStealth;
    public int   minStealth;
}

[Serializable]
public class EnvironmentMoveModifier
{
    public EnvironmentType environmentType;

    [Tooltip("Multiplier on movement cost for this environment ( <1 = faster, >1 = slower ).")]
    public float costMultiplier = 1f;
}

[Serializable]
public class TileTypeMoveModifier
{
    public EnvironmentTileType tileType;

    [Tooltip("Multiplier on movement cost for this tile type ( <1 = faster, >1 = slower ).")]
    public float costMultiplier = 1f;
}

[CreateAssetMenu(menuName = "Kardashev/Militia Unit", fileName = "NewMilitiaUnit")]
public class MilitiaUnit : ScriptableObject
{
    [Header("Identity")]
    public string unitID;
    public string unitName;
    public Sprite unitIcon;

    [Header("Core Stats")]
    public int maxHealth = 10;
    public bool isHuman = true;
    public bool isEquipment = false;

    [Header("Training")]
    public int populationToTrain = 1;
    public List<ResourceCost> trainingCosts = new();
    public int trainingTurns = 1;
    public int outputUnits = 1;

    [Header("Category")]
    public MilitiaUnitCategory category = MilitiaUnitCategory.Land;

    [Header("Upkeep (per turn)")]
    public List<ResourceCost> upkeepPerTurn = new();

    [Header("Upkeep tolerance")]
    [Min(0)]
    public int maxMissedUpkeepTurns = 0;

    [Header("Skill / XP")]
    public int startingSkillLevel = 0;
    public int maxSkillLevel = 5;

    public int trainingStatPointsPerLevel = 5;

    [Header("Movement & Combat")]
    public float movementSpeed = 1f;
    public int power = 1;
    public int defense = 1;
    public int agility = 1;
    public int accuracy = 1;
    public int range = 1;
    public int stealth = 0;

    [Header("Grouping")]
    public int maxGroupSize = 0;

    [Header("Trainable Stats")]
    public bool canTrainHealth   = true;
    public bool canTrainMovement = true;
    public bool canTrainPower    = true;
    public bool canTrainDefense  = true;
    public bool canTrainAgility  = true;
    public bool canTrainAccuracy = true;
    public bool canTrainRange    = true;
    public bool canTrainStealth  = true;

    [Header("Advancement")]
    public List<MilitiaUnitAdvancementOption> advancementOptions = new();

    [Header("Terrain Movement Rules")]
    public bool restrictByEnvironmentType = false;
    public List<EnvironmentType> allowedEnvironmentTypes = new();
    public bool restrictByTileType = false;
    public List<EnvironmentTileType> allowedTileTypes = new();
    public List<EnvironmentMoveModifier> environmentMoveModifiers = new();
    public List<TileTypeMoveModifier> tileTypeMoveModifiers = new();

    [Header("Unit Actions")]
    public List<UnitActionDefinitionSO> actions = new();

    [Header("Loot (per unit killed)")]
    public List<ResourceLootEntry> lootPerUnitKilled = new();
}