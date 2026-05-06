// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;

// public class AICraftingBuildingPriorityCalculator : MonoBehaviour
// {
//     private AIPlayer aiPlayer;
//     private AIResourcePriorityCalculator resourcePriorityCalculator;

//     private void Start()
//     {
//         aiPlayer = GetComponentInParent<AIPlayer>();

//         resourcePriorityCalculator = aiPlayer?.GetComponentInChildren<AIResourcePriorityCalculator>();

//         if (resourcePriorityCalculator == null)
//         {
//             //Debug.LogError("[AICraftingBuildingPriorityCalculator] AIResourcePriorityCalculator is missing!");
//             enabled = false;
//         }
//     }

//     /// **🔹 Calculates crafting building priority**
//     public int CalculateCraftingBuildingPriority(Building building, int basePriority)
//     {
//         if (building.buildingType != BuildingType.Crafting)
//         {
//             return basePriority; // ✅ Only adjust priority for crafting buildings
//         }

//         GameObject finalBuildingPrefab = GetFinalBuildingPrefab(building);
//         if (finalBuildingPrefab == null)
//         {
//             //Debug.LogError($"[AICraftingBuildingPriorityCalculator] Failed to get final prefab for {building.buildingID}.");
//             return basePriority;
//         }

//         CraftingBuildingControl craftingControl = finalBuildingPrefab.GetComponent<CraftingBuildingControl>();
//         if (craftingControl == null)
//         {
//             //Debug.LogError($"[AICraftingBuildingPriorityCalculator] No CraftingBuildingControl found on {finalBuildingPrefab.name}.");
//             return basePriority;
//         }

//         // ✅ Get the list of crafting recipes available in this building
//         List<CraftedItem> availableRecipes = craftingControl.GetAllCraftableItems();
//         if (availableRecipes == null || availableRecipes.Count == 0)
//         {
//             //Debug.Log($"[AICraftingBuildingPriorityCalculator] No crafting recipes available for {building.buildingID}.");
//             return basePriority;
//         }

//         // ✅ Check if any recipe produces a high-priority resource
//         float maxPriority = basePriority;
//         foreach (CraftedItem recipe in availableRecipes)
//         {
//             foreach (ResourceRequirement outputResource in recipe.outputResources) // ✅ FIXED: Use `ResourceRequirement`
//             {
//                 float resourcePriority = resourcePriorityCalculator.GetResourcePriority(outputResource.resourceID); // ✅ FIXED: Extract `resourceID`

//                 if (resourcePriority > 0)  // Only consider needed resources
//                 {
//                     float adjustedPriority = basePriority + (resourcePriority * 10f); // ✅ Increase priority based on resource need
//                     if (adjustedPriority > maxPriority)
//                     {
//                         maxPriority += adjustedPriority;
//                     }
//                 }
//             }
//         }

//         return Mathf.RoundToInt(maxPriority); // ✅ Ensure priority is an integer
//     }

//     /// **🔹 Gets the final crafting building prefab from the BuildingConstruction script**
//     private GameObject GetFinalBuildingPrefab(Building building)
//     {
//         if (building.buildingPrefab == null)
//         {
//             //Debug.LogError($"[AICraftingBuildingPriorityCalculator] Building prefab is NULL for {building.buildingID}.");
//             return null;
//         }

//         BuildingConstruction construction = building.buildingPrefab.GetComponent<BuildingConstruction>();
//         if (construction == null || construction.constructionStages == null || construction.constructionStages.Count == 0)
//         {
//             //Debug.LogError($"[AICraftingBuildingPriorityCalculator] No valid construction stages for {building.buildingID}.");
//             return null;
//         }

//         return construction.constructionStages[construction.constructionStages.Count - 1]; // ✅ Get final stage of construction
//     }
// }