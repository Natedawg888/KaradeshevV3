// using System.Collections;
// using System.Collections.Generic;
// using System.Linq;
// using UnityEngine;

// public class ProductionPlanEvaluator : MonoBehaviour
// {
//     public AIResourcePriorityCalculator resourcePriorityCalculator;
//     public AIInventoryManager inventoryManager;
//     public AIPopulationManager populationManager;
//     public AITechnologyManager technologyManager;
//     public AIBuildingManager aiBuildingManager; // Reference to AI's Building Manager
//     public AIPlanner planner;

//     private void Awake()
//     {
//         if (aiBuildingManager == null)
//         {
//             aiBuildingManager = GetComponentInParent<AIBuildingManager>();
//         }
//     }

//     public int EvaluateProductionItem(ProductionItem item)
//     {
//         if (item == null || !item.isUnlocked)
//         {
//             Debug.LogWarning("[ProductionPlanEvaluator] Skipping evaluation: Item is null or locked.");
//             return 0;
//         }

//         // Calculate base output priority based on the production item’s outputs.
//         int outputPriority = CalculateOutputPriority(item);

//         // Evaluate production cost and reduce overall priority if the AI lacks required resources.
//         int totalCostPenalty = 0;
//         if (item.resourceCost != null)
//         {
//             foreach (var req in item.resourceCost)
//             {
//                 int available = inventoryManager.GetResourceAmount(req.resourceID);
//                 int deficit = req.amount - available;
//                 if (deficit > 0)
//                 {
//                     totalCostPenalty += deficit * 100;
//                     Debug.LogWarning($"[Cost Penalty] Resource: {req.resourceID}, Required: {req.amount}, Available: {available}, Penalty: {deficit * 100}");
//                 }
//             }
//         }

//         // New: Check if we have at least 3× the cost for every resource.
//         if (item.resourceCost != null)
//         {
//             foreach (var req in item.resourceCost)
//             {
//                 int available = inventoryManager.GetResourceAmount(req.resourceID);
//                 int required3x = 3 * req.amount;
//                 if (available < required3x)
//                 {
//                     resourcePriorityCalculator.IncreaseResourcePriority(req.resourceID, 200f);
//                     Debug.LogWarning($"[Resource Check] {item.itemName} does not have 3× the required {req.resourceID}. Dropping priority to 0.");
//                     return 0;
//                 }
//             }
//         }

//         int finalPriority = Mathf.Max(outputPriority - totalCostPenalty, 0);
//         Debug.Log($"[Final Priority] Item: {item.itemName}, Output Priority: {outputPriority}, Total Penalty: {totalCostPenalty}, Final Priority: {finalPriority}");

//         // --- New Logic: Adjust priority based on current productions ---
//         List<GameObject> ownedBuildings = aiBuildingManager?.GetOwnedBuildings();
//         if (ownedBuildings != null)
//         {
//             bool duplicateFound = false;
//             int highestOtherOutputPriority = 0;

//             foreach (var building in ownedBuildings)
//             {
//                 ProductionBuildingControl productionControl = building.GetComponent<ProductionBuildingControl>();
//                 if (productionControl != null && productionControl.IsProducing() && productionControl.CurrentProductionItem != null)
//                 {
//                     ProductionItem otherItem = productionControl.CurrentProductionItem;
//                     // Check for duplicate production (same itemID)
//                     if (otherItem.itemID == item.itemID)
//                     {
//                         duplicateFound = true;
//                         Debug.Log($"[Duplicate Production] {item.itemName} is already in production at {building.name}.");
//                     }
//                     else
//                     {
//                         // For competing production items, we compare their output priorities.
//                         int otherOutputPriority = CalculateOutputPriority(otherItem);
//                         if (otherOutputPriority > highestOtherOutputPriority)
//                         {
//                             highestOtherOutputPriority = otherOutputPriority;
//                         }
//                     }
//                 }
//             }

//             // If the item is already being produced, reduce priority (for example, by 50%).
//             if (duplicateFound)
//             {
//                 finalPriority = Mathf.RoundToInt(finalPriority * 0.5f);
//                 Debug.Log($"[Duplicate Reduction] Duplicate production found. New Final Priority: {finalPriority}");
//             }

//             // If there is a competing production item with a higher output priority, lower this item’s priority further.
//             if (highestOtherOutputPriority > outputPriority)
//             {
//                 finalPriority = Mathf.RoundToInt(finalPriority * 0.75f);
//                 Debug.Log($"[Competing Production] Another production item has a higher output priority. Further reducing Final Priority to: {finalPriority}");
//             }
//         }

//         // --- New: Adjust using average output resource priority (with research boost) ---
//         float sumOutputPriority = 0f;
//         int outputCount = 0;
//         if (item.outputResources != null)
//         {
//             foreach (var output in item.outputResources)
//             {
//                 float rp = resourcePriorityCalculator.GetResourcePriority(output.resourceID);
//                 // Apply research dependency boost if applicable.
//                 if (IsResourceUsedInResearch(output.resourceID))
//                 {
//                     rp *= 5f;
//                 }
//                 sumOutputPriority += rp;
//                 outputCount++;
//             }
//         }
//         float avgOutputPriority = (outputCount > 0) ? sumOutputPriority / outputCount : 0f;
//         // If the average output priority is below 100, scale down the final priority.
//         if (avgOutputPriority < 100f)
//         {
//             int oldFinalPriority = finalPriority;
//             finalPriority = Mathf.RoundToInt(finalPriority * (avgOutputPriority / 100f));
//             Debug.Log($"[Avg Priority Adjustment] Average output priority: {avgOutputPriority:F2}. Adjusting final priority from {oldFinalPriority} to {finalPriority}.");
//         }

//         return finalPriority;
//     }

//     // Helper method to calculate output priority from a production item.
//     private int CalculateOutputPriority(ProductionItem item)
//     {
//         float totalOutputPriority = 0f;

//         // Get AI's current food and material levels.
//         int totalFood = inventoryManager.GetTotalNonWaterFoodAmount();
//         int totalMaterials = inventoryManager.GetTotalMaterialAmount();

//         if (item.outputResources != null)
//         {
//             foreach (var output in item.outputResources)
//             {
//                 Resource outputResource = ResourceManager.Instance.GetResourceByID(output.resourceID);
//                 float baseResPriority = resourcePriorityCalculator.GetResourcePriority(output.resourceID);
//                 float resPriority = baseResPriority;

//                 // Boost based on existing resource priority.
//                 if (baseResPriority > 500) resPriority *= 5f;
//                 else if (baseResPriority > 300) resPriority *= 3f;
//                 else if (baseResPriority > 150) resPriority *= 2f;
//                 else if (baseResPriority < 50) resPriority *= 0.25f;

//                 // Research dependency boost.
//                 if (IsResourceUsedInResearch(output.resourceID))
//                 {
//                     resPriority *= 5f;
//                 }

//                 totalOutputPriority += Mathf.RoundToInt(resPriority * 100f);
//             }
//         }

//         return Mathf.RoundToInt(totalOutputPriority);
//     }

//     private bool IsResourceUsedInResearch(string resourceID)
//     {
//         // Filter out only research plans that have a selected technology.
//         var researchPlans = planner.GetAllPlannedActions()
//                                 .Where(plan => plan.planType == AIPlanType.Research && plan.selectedTechnology != null);

//         // Check each planned research for the resource requirement.
//         foreach (var plan in researchPlans)
//         {
//             foreach (var req in plan.selectedTechnology.resourceRequirements)
//             {
//                 if (req.resourceID == resourceID)
//                     return true;
//             }
//         }
//         return false;
//     }
// }