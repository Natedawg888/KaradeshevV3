// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
// using System.Linq;

// public class CraftingPlanEvaluator : MonoBehaviour
// {
//     private AIPlayer aiPlayer;
//     private AIPlanner aiPlanner;
//     public AIResourcePriorityCalculator resourcePriorityCalculator;
//     public AITechnologyManager technologyManager;
//     public AIInventoryManager inventoryManager;
//     public AIPopulationManager populationManager;
//     public AIBuildingManager aiBuildingManager; // ✅ Add a public reference to AIBuildingManager

//     private void Awake()
//     {
//         aiPlayer = GetComponentInParent<AIPlayer>();
//         if (aiPlayer == null)
//             return;
        
//         aiPlanner = aiPlayer.GetComponentInChildren<AIPlanner>();

//         // Ensure AI Player's Building Manager is assigned.
//         if (aiBuildingManager == null)
//         {
//             aiBuildingManager = GetComponentInParent<AIBuildingManager>();
//         }
//     }

//     public int EvaluateCraftableItem(CraftedItem item)
//     {
//         if (item == null || !item.isUnlocked)
//             return 0;

//         int totalPriority = 0;

//         // ✅ Use the reference to AI's Building Manager instead of FindObjectsOfType
//         List<GameObject> ownedBuildings = aiBuildingManager?.GetOwnedBuildings();

//         if (item.outputResources != null)
//         {
//             foreach (var output in item.outputResources)
//             {
//                 Resource outputResource = ResourceManager.Instance.GetResourceByID(output.resourceID);
//                 float resPriority = resourcePriorityCalculator.GetResourcePriority(output.resourceID);

//                 // 🔺 Boost if resource priority is high
//                 if (resPriority > 400)
//                     resPriority *= 2f;
//                 else if (resPriority > 250)
//                     resPriority *= 1.5f;
//                 else if (resPriority > 150)
//                     resPriority *= 1.3f;
//                 else if (resPriority > 100)
//                     resPriority *= 1.25f;

//                 // 🔹 Boost food crafting priority if AI is low on food
//                 if (outputResource != null && outputResource.resourceType == ResourceType.Food)
//                 {
//                     int totalFood = inventoryManager.GetTotalNonWaterFoodAmount();
//                     int requiredFoodPerTurn = populationManager.GetCurrentPopulation() * populationManager.foodConsumptionPerPerson;
//                     int foodFor3Turns = requiredFoodPerTurn * 3;

//                     if (totalFood < foodFor3Turns)
//                         resPriority *= 1.5f;  // 🚨 AI has **low food**, increase priority
//                 }

//                 // 🔹 Boost if the resource is needed for an active production building
//                 if (IsResourceNeededForProduction(output.resourceID, ownedBuildings))
//                     resPriority *= 1.5f; // Increase by 30%

//                 // 🔺 Boost if resource is used in research
//                 if (IsResourceUsedInResearch(output.resourceID))
//                     resPriority *= 2f; // Increase by 20%

//                 // 🔹 **NEW: Boost priority if the resource is required for a building plan**
//                 if (IsResourceNeededForBuildingPlan(output.resourceID))
//                     resPriority *= 2f; // 🚀 **Double priority for building materials**
                
//                 if (IsResourceNeededForRepair(output.resourceID, ownedBuildings))
//                     resPriority *= 0.5f;

//                 totalPriority += Mathf.RoundToInt(resPriority * 10f);
//             }
//         }

//         // 🔻 Reduce priority if the crafting item's cost shares resources with a higher priority item
//         float sharedCostPenalty = CalculateSharedCostPenalty(item);
//         totalPriority = Mathf.Max(totalPriority - Mathf.RoundToInt(sharedCostPenalty), 0);

//         return totalPriority;
//     }

//     private float CalculateSharedCostPenalty(CraftedItem item)
//     {
//         float penalty = 0f;

//         // Get all existing crafting plans
//         List<AIPlan> existingCraftingPlans = aiPlanner.GetAllPlannedActions()
//             .Where(plan => plan.planType == AIPlanType.Crafting && plan.craftedItem != null && plan.craftedItem != item)
//             .ToList();

//         foreach (var plan in existingCraftingPlans)
//         {
//             foreach (var req in plan.craftedItem.resourceCost)
//             {
//                 if (item.resourceCost.Any(ownReq => ownReq.resourceID == req.resourceID))
//                 {
//                     // If a shared resource is found, reduce the current item's priority based on the existing plan's priority
//                     penalty += plan.priority * 0.5f; // Reduce by 50% of the shared plan's priority
//                 }
//             }
//         }

//         return penalty;
//     }

//     private bool IsResourceUsedInResearch(string resourceID)
//     {
//         if (technologyManager == null)
//         {
//             technologyManager = GetComponentInParent<AITechnologyManager>();
//             if (technologyManager == null)
//                 return false;
//         }

//         List<Technology> relevantTechs = new List<Technology>();

//         foreach (ResearchPlan plan in technologyManager.activeResearchPlans)
//         {
//             if (plan.technology != null && !relevantTechs.Contains(plan.technology))
//             {
//                 relevantTechs.Add(plan.technology);
//             }
//         }

//         List<Technology> availableTechs = technologyManager.GetAvailableTechnologies();
//         foreach (Technology tech in availableTechs)
//         {
//             if (tech != null && !relevantTechs.Contains(tech))
//             {
//                 relevantTechs.Add(tech);
//             }
//         }

//         foreach (Technology tech in relevantTechs)
//         {
//             foreach (var requirement in tech.resourceRequirements)
//             {
//                 if (requirement.resourceID == resourceID)
//                 {
//                     return true;
//                 }
//             }
//         }
//         return false;
//     }

//     private bool IsResourceNeededForProduction(string resourceID, List<GameObject> ownedBuildings)
//     {
//         if (ownedBuildings == null) return false;

//         foreach (var building in ownedBuildings)
//         {
//             ProductionBuildingControl productionControl = building.GetComponent<ProductionBuildingControl>();
//             if (productionControl != null && productionControl.IsProducing())
//             {
//                 ProductionItem currentProduction = productionControl.CurrentProductionItem;
//                 if (currentProduction != null)
//                 {
//                     foreach (var req in currentProduction.resourceCost)
//                     {
//                         if (req.resourceID == resourceID)
//                             return true;
//                     }
//                 }
//             }
//         }
//         return false;
//     }

//     private bool IsResourceNeededForBuildingPlan(string resourceID)
//     {
//         List<AIPlan> buildingPlans = aiPlanner.GetAllPlannedActions()
//             .Where(plan => plan.planType == AIPlanType.Building)
//             .ToList();

//         foreach (var plan in buildingPlans)
//         {
//             if (plan.selectedBuilding != null && plan.selectedBuilding.requiredResources != null)
//             {
//                 foreach (var req in plan.selectedBuilding.requiredResources)
//                 {
//                     if (req.resourceID == resourceID)
//                         return true;
//                 }
//             }
//         }
//         return false;
//     }

//     public bool IsResourceNeededForRepair(string resourceID, List<GameObject> ownedBuildings)
//     {
//         if (ownedBuildings == null)
//             return false;

//         foreach (var building in ownedBuildings)
//         {
//             if (building == null)
//                 continue;

//             var buildingControl = building.GetComponent<AIBuildingControl>();
//             if (buildingControl == null)
//                 continue;

//             var repairCosts = buildingControl.GetRepairCosts();
//             if (repairCosts == null)
//                 continue;

//             foreach (var req in repairCosts)
//             {
//                 if (req == null)
//                     continue;

//                 if (req.resourceID == resourceID)
//                     return true;
//             }
//         }

//         return false;
//     }
// }