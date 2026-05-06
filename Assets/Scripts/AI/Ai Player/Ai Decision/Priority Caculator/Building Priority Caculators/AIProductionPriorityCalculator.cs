// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
// using System.Linq;

// public class AIProductionPriorityCalculator : MonoBehaviour
// {
//     private AIPlayer aiPlayer;
//     private AIInventoryManager inventoryManager;
//     private AIPopulationManager populationManager;
//     private AIResourcePriorityCalculator resourcePriorityCalculator;

//     private void Start()
//     {
//         aiPlayer = GetComponentInParent<AIPlayer>();
//         if (aiPlayer != null)
//         {
//             inventoryManager = aiPlayer.GetComponentInChildren<AIInventoryManager>();
//             populationManager = aiPlayer.GetComponentInChildren<AIPopulationManager>();
//             resourcePriorityCalculator = aiPlayer.GetComponentInChildren<AIResourcePriorityCalculator>();
//         }
//     }

//     public int CalculateFoodProductionBasePriority(ProductionBuildingControl prodControl)
//     {
//         if (prodControl == null)
//             return 0;

//         int basePriority = 50;
//         int totalFoodOutput = 0;
//         int materialBonus = 0;
//         int waterBonus = 0;
//         int highPriorityResourceBonus = 0;
//         bool foundHighPriority = false;

//         List<ProductionItem> allItems = GetAllProductionItems(prodControl);

//         foreach (ProductionItem item in allItems)
//         {
//             foreach (ResourceRequirement output in item.outputResources)
//             {
//                 Resource res = ResourceManager.Instance.GetResourceByID(output.resourceID);
//                 if (res == null) continue;

//                 if (res.resourceType == ResourceType.Food)
//                     totalFoodOutput += output.amount;
//                 else if (res.resourceType == ResourceType.Material)
//                     materialBonus += Mathf.RoundToInt(output.amount * 0.5f);
//                 else if (output.resourceID == "WFR")
//                     waterBonus += 20;

//                 // Award bonus if this output is considered high priority.
//                 if (resourcePriorityCalculator.GetResourcePriority(output.resourceID) >= 120)
//                 {
//                     highPriorityResourceBonus += 50;
//                     foundHighPriority = true;
//                     Debug.Log($"[AIProductionPriorityCalculator] Production item produces high-priority resource '{output.resourceID}'. Bonus +50.");
//                 }
//             }
//         }

//         // Apply a penalty if none of the production items produce a high-priority output.
//         if (!foundHighPriority)
//         {
//             Debug.Log("[AIProductionPriorityCalculator] No high priority output found among production items.");
//             highPriorityResourceBonus = -50;
//         }

//         int calculatedPriority = basePriority + 100 + totalFoodOutput + materialBonus + waterBonus + highPriorityResourceBonus;
//         return calculatedPriority;
//     }

//     public int CalculateMaterialProductionBasePriority(ProductionBuildingControl prodControl)
//     {
//         if (prodControl == null)
//             return 0;

//         int basePriority = 50;
//         int totalMaterialOutput = 0;
//         int foodBonus = 0;
//         int waterBonus = 0;
//         int highPriorityResourceBonus = 0;
//         bool foundHighPriority = false;

//         List<ProductionItem> allItems = GetAllProductionItems(prodControl);

//         foreach (ProductionItem item in allItems)
//         {
//             foreach (ResourceRequirement output in item.outputResources)
//             {
//                 Resource res = ResourceManager.Instance.GetResourceByID(output.resourceID);
//                 if (res == null) continue;

//                 if (res.resourceType == ResourceType.Material)
//                     totalMaterialOutput += output.amount;
//                 else if (res.resourceType == ResourceType.Food)
//                     foodBonus += Mathf.RoundToInt(output.amount * 0.3f);
//                 else if (output.resourceID == "WFR")
//                     waterBonus += 15;

//                 if (resourcePriorityCalculator.GetResourcePriority(output.resourceID) >= 120)
//                 {
//                     highPriorityResourceBonus += 50;
//                     foundHighPriority = true;
//                     Debug.Log($"[AIProductionPriorityCalculator] Production item produces high-priority resource '{output.resourceID}'. Bonus +50.");
//                 }
//             }
//         }

//         if (!foundHighPriority)
//         {
//             Debug.Log("[AIProductionPriorityCalculator] No high priority output found among production items.");
//             highPriorityResourceBonus = -50;
//         }

//         int calculatedPriority = basePriority + 80 + totalMaterialOutput + foodBonus + waterBonus + highPriorityResourceBonus;
//         return calculatedPriority;
//     }

//     public int CalculateWaterProductionBasePriority(ProductionBuildingControl prodControl)
//     {
//         if (prodControl == null)
//             return 0;

//         int basePriority = 50;
//         int totalWaterOutput = 0;
//         int highPriorityResourceBonus = 0;
//         bool foundHighPriority = false;

//         List<ProductionItem> allItems = GetAllProductionItems(prodControl);

//         foreach (ProductionItem item in allItems)
//         {
//             foreach (ResourceRequirement output in item.outputResources)
//             {
//                 if (output.resourceID == "WFR")
//                     totalWaterOutput += Mathf.RoundToInt(output.amount * 3f);

//                 if (resourcePriorityCalculator.GetResourcePriority(output.resourceID) >= 120)
//                 {
//                     highPriorityResourceBonus += 50;
//                     foundHighPriority = true;
//                     Debug.Log($"[AIProductionPriorityCalculator] Production item produces high-priority resource '{output.resourceID}'. Bonus +50.");
//                 }
//             }
//         }

//         if (!foundHighPriority)
//         {
//             Debug.Log("[AIProductionPriorityCalculator] No high priority output found among production items.");
//             highPriorityResourceBonus = -50;
//         }

//         int calculatedPriority = basePriority + 100 + totalWaterOutput + highPriorityResourceBonus;
//         return calculatedPriority;
//     }

//     // Combines available and unlocked production items, removing duplicates.
//     private List<ProductionItem> GetAllProductionItems(ProductionBuildingControl prodControl)
//     {
//         List<ProductionItem> allItems = new List<ProductionItem>();
//         if (prodControl.availableProductionItems != null)
//             allItems.AddRange(prodControl.availableProductionItems);
//         if (prodControl.GetUnlockedProductionItems() != null)
//             allItems.AddRange(prodControl.GetUnlockedProductionItems());
//         return allItems.GroupBy(i => i.itemID).Select(g => g.First()).ToList();
//     }
// }