using System;
using System.Collections.Generic;

[Serializable]
public class InventoryStackSaveData
{
    public string resourceID;
    public int amount;
    public int remainingSpoilageTurns;
}

[Serializable]
public class PlayerInventorySaveData
{
    public bool starterGranted;

    public float maxMaterialsSpace;
    public float maxFoodSpace;
    public float maxWaterSpace;

    public List<InventoryStackSaveData> materials = new();
    public List<InventoryStackSaveData> food = new();
    public List<InventoryStackSaveData> water = new();

    // Optional but useful if you save/load mid-turn.
    public List<string> foodIdsConsumedThisTurn = new();
    public int foodUnitsConsumedThisTurn;
    public float nutritionPointsConsumedThisTurn;

    public int spoiledFoodUnitsConsumedThisTurn;
    public float spoiledNutritionPointsConsumedThisTurn;

    public float nonSpoiledGradeWeightedSumThisTurn;
    public float nonSpoiledNutritionPointsThisTurn;
    public int maxNonSpoiledGradeConsumedThisTurn;
}