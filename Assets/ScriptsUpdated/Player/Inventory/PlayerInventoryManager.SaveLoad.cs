using System.Collections.Generic;
using UnityEngine;

public partial class PlayerInventoryManager
{
    public PlayerInventorySaveData SaveState()
    {
        return new PlayerInventorySaveData
        {
            starterGranted = _starterGranted,

            maxMaterialsSpace = maxMaterialsSpace,
            maxFoodSpace = maxFoodSpace,
            maxWaterSpace = maxWaterSpace,

            materials = CaptureStacks(_materials),
            food = CaptureStacks(_food),
            water = CaptureStacks(_water),

            foodIdsConsumedThisTurn = new List<string>(_foodIdsConsumedThisTurn),
            foodUnitsConsumedThisTurn = _foodUnitsConsumedThisTurn,
            nutritionPointsConsumedThisTurn = _nutritionPointsConsumedThisTurn,

            spoiledFoodUnitsConsumedThisTurn = _spoiledFoodUnitsConsumedThisTurn,
            spoiledNutritionPointsConsumedThisTurn = _spoiledNutritionPointsConsumedThisTurn,

            nonSpoiledGradeWeightedSumThisTurn = _nonSpoiledGradeWeightedSumThisTurn,
            nonSpoiledNutritionPointsThisTurn = _nonSpoiledNutritionPointsThisTurn,
            maxNonSpoiledGradeConsumedThisTurn = _maxNonSpoiledGradeConsumedThisTurn
        };
    }

    public void LoadState(PlayerInventorySaveData data)
    {
        if (data == null)
            return;

        _starterGranted = data.starterGranted;

        maxMaterialsSpace = data.maxMaterialsSpace;
        maxFoodSpace = data.maxFoodSpace;
        maxWaterSpace = data.maxWaterSpace;

        _materials.Clear();
        _food.Clear();
        _water.Clear();

        RestoreStacks(_materials, data.materials);
        RestoreStacks(_food, data.food);
        RestoreStacks(_water, data.water);

        _foodIdsConsumedThisTurn.Clear();
        if (data.foodIdsConsumedThisTurn != null)
        {
            foreach (string id in data.foodIdsConsumedThisTurn)
            {
                if (!string.IsNullOrWhiteSpace(id))
                    _foodIdsConsumedThisTurn.Add(id);
            }
        }

        _foodUnitsConsumedThisTurn = data.foodUnitsConsumedThisTurn;
        _nutritionPointsConsumedThisTurn = data.nutritionPointsConsumedThisTurn;

        _spoiledFoodUnitsConsumedThisTurn = data.spoiledFoodUnitsConsumedThisTurn;
        _spoiledNutritionPointsConsumedThisTurn = data.spoiledNutritionPointsConsumedThisTurn;

        _nonSpoiledGradeWeightedSumThisTurn = data.nonSpoiledGradeWeightedSumThisTurn;
        _nonSpoiledNutritionPointsThisTurn = data.nonSpoiledNutritionPointsThisTurn;
        _maxNonSpoiledGradeConsumedThisTurn = data.maxNonSpoiledGradeConsumedThisTurn;

        _spoiledDef = FindResourceById(spoiledFoodResourceId);

        inventoryPanel?.Refresh();
    }

    private List<InventoryStackSaveData> CaptureStacks(List<InventoryStack> source)
    {
        List<InventoryStackSaveData> result = new List<InventoryStackSaveData>();

        if (source == null)
            return result;

        foreach (InventoryStack stack in source)
        {
            if (stack == null || stack.definition == null || stack.amount <= 0)
                continue;

            result.Add(new InventoryStackSaveData
            {
                resourceID = stack.definition.resourceID,
                amount = stack.amount,
                remainingSpoilageTurns = stack.remainingSpoilageTurns
            });
        }

        return result;
    }

    private void RestoreStacks(List<InventoryStack> target, List<InventoryStackSaveData> saved)
    {
        if (target == null || saved == null)
            return;

        foreach (InventoryStackSaveData savedStack in saved)
        {
            if (savedStack == null || string.IsNullOrWhiteSpace(savedStack.resourceID) || savedStack.amount <= 0)
                continue;

            ResourceDefinition def = FindResourceById(savedStack.resourceID);
            if (def == null)
            {
                Debug.LogWarning($"[INV SAVE] Could not resolve resource ID '{savedStack.resourceID}' while loading inventory.");
                continue;
            }

            InventoryStack stack = new InventoryStack(def, savedStack.amount)
            {
                remainingSpoilageTurns = savedStack.remainingSpoilageTurns
            };

            target.Add(stack);
        }
    }
}