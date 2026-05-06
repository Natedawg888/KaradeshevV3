// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
// using System.Linq;

// public class DiscoveryPlanEvaluator : MonoBehaviour, IEvaluator<EnvironmentControl>
// {
//     public AIResourcePriorityCalculator priorityCalculator;
//     public AIEnvironmentPriorityCalculator environmentPriorityCalculator;
//     public AITileTracker tileTracker;
//     public AIInventoryManager inventoryManager;
//     public AIPopulationManager populationManager;
//     public AIGatheringManager gatheringManager;
//     public AIBuildingManager aiBuildingManager;
    
//     // Tunable weights – these could even be ScriptableObjects!
//     public int highFailureBonus = 150;
//     public int moderateFailureBonus = 80;
//     public int lowFailureBonus = 50;
//     public int hardFailurePenalty = -80;
//     public int waterBonus = 100;
//     public int baseDiscovery = 10;
//     public int bfsWeight = 5;

//     public int Evaluate(EnvironmentControl envControl)
//     {
//         if (envControl == null)
//             return 0;
            
//         int priority = baseDiscovery;
//         float failureChance = envControl.initialDiscoveryFailureChance;
        
//         // Adjust priority based on failure chance:
//         if (failureChance < 10f) priority += highFailureBonus;
//         else if (failureChance < 15f) priority += moderateFailureBonus;
//         else if (failureChance < 20f) priority += lowFailureBonus;
//         else if (failureChance > 30f) priority += hardFailurePenalty;

//         if (envControl.resources.Any(r => r.resourceID == "WFR"))
//         {
//             priority += waterBonus;
//         }

//         // Increase Priority if the Tile Can Support Buildings.
//         priority += EvaluateBuildingPotential(envControl);

//         // Adjust based on available working population.
//         int maxPopulation = populationManager.maxPopulation;
//         int workingPopulation = populationManager.GetAvailablePopulation(); // Excludes elders & children.
//         float populationRatio = (float)workingPopulation / maxPopulation;
//         if (populationRatio <= 0.25f)
//         {
//             priority -= 400; // Large penalty if AI has only 25% of max working population.
//         }
//         else if (populationRatio <= 0.5f)
//         {
//             priority -= 100; // Smaller penalty if AI has 50% of max working population.
//         }

//         // Add resource-based boost:
//         priority += EvaluateResourcePriority(envControl);

//         // Include an environmental type bonus:
//         priority += environmentPriorityCalculator.GetEnvironmentPriority(envControl.environmentType);

//         // NEW: Adjust discovery priority based on food/water survival.
//         float survivalFactor = GetSurvivalFactor();
//         priority = Mathf.RoundToInt(priority * survivalFactor);

//         return Mathf.Max(priority, 0);
//     }
    
//     private int EvaluateResourcePriority(EnvironmentControl envControl)
//     {
//         int boost = 0;
//         var resourcePriorities = priorityCalculator.GetResourcePriorities();
//         foreach (var resource in envControl.resources)
//         {
//             if (resourcePriorities.TryGetValue(resource.resourceID, out float value))
//             {
//                 if (value > 200) boost += 200;
//                 else if (value > 100) boost += 100;
//                 else if (value > 50) boost += 50;
//                 else if (value > 20) boost += 20;
//             }
//         }
//         return boost;
//     }

//     private int EvaluateBuildingPotential(EnvironmentControl envControl)
//     {
//         if (envControl == null || aiBuildingManager == null)
//             return 0;

//         int buildingBoost = 0;
//         List<Building> availableBuildings = aiBuildingManager.GetAvailableBuildings();
//         List<GameObject> ownedBuildings = aiBuildingManager.GetOwnedBuildings();

//         foreach (Building building in availableBuildings)
//         {
//             if (building.requiredEnvironmentTypes.Contains(envControl.environmentType))
//             {
//                 // Use the first three characters of the buildingID as the type prefix.
//                 string candidatePrefix = building.buildingID.Substring(0, 3);

//                 // Check if any owned building already has the same prefix.
//                 bool alreadyOwned = ownedBuildings.Any(ob =>
//                 {
//                     AIBuildingControl aibc = ob.GetComponent<AIBuildingControl>();
//                     return aibc != null && aibc.buildingID.StartsWith(candidatePrefix);
//                 });

//                 // Only add a boost if the candidate building's type is not already owned.
//                 if (!alreadyOwned)
//                 {
//                     buildingBoost += 100; // Adjust boost value as needed.
//                 }
//             }
//         }

//         return buildingBoost;
//     }

//     private float GetSurvivalFactor()
//     {
//         // Similar to your technology priority, get the AI's inventory and population data.
//         AIPlayer aiPlayer = GetComponentInParent<AIPlayer>();
//         if (aiPlayer == null)
//             return 1.0f;
        
//         AIPopulationManager popManager = aiPlayer.GetComponentInChildren<AIPopulationManager>();
//         AIInventoryManager invManager = aiPlayer.GetComponentInChildren<AIInventoryManager>();
//         if (popManager == null || invManager == null)
//             return 1.0f;
        
//         int currentPopulation = popManager.GetCurrentPopulation();
//         int requiredFoodPerTurn = currentPopulation * popManager.foodConsumptionPerPerson;
//         int requiredWaterPerTurn = currentPopulation * popManager.waterConsumptionPerPerson;
//         if (requiredFoodPerTurn == 0 || requiredWaterPerTurn == 0)
//             return 1.0f;
        
//         int totalFood = invManager.GetTotalNonWaterFoodAmount();
//         int totalWater = invManager.GetResourceAmount("WFR"); // "WFR" is assumed to be fresh water.
//         int foodFor3Turns = requiredFoodPerTurn * 3;
//         int waterFor3Turns = requiredWaterPerTurn * 3;
        
//         bool needsFood = totalFood < foodFor3Turns;
//         bool needsWater = totalWater < waterFor3Turns;
        
//         // If water is needed, check whether any gatherable tile has fresh water available.
//         if (needsWater)
//         {
//             AIGatheringManager gatheringManager = GetComponentInParent<AIGatheringManager>();
//             if (gatheringManager != null)
//             {
//                 List<GameObject> gatherableTiles = gatheringManager.GetGatherableTiles();
//                 bool waterTileFound = false;
//                 foreach (GameObject tile in gatherableTiles)
//                 {
//                     EnvironmentControl envControl = tile.GetComponent<EnvironmentControl>();
//                     if (envControl != null)
//                     {
//                         foreach (ResourceAmount resource in envControl.resources)
//                         {
//                             if (resource.resourceID == "WFR" && resource.currentAmount > 0)
//                             {
//                                 waterTileFound = true;
//                                 break;
//                             }
//                         }
//                     }
//                     if (waterTileFound)
//                         break;
//                 }
//                 // If no gatherable tile has fresh water, ignore the water shortage.
//                 if (!waterTileFound)
//                 {
//                     needsWater = false;
//                 }
//             }
//         }
        
//         if (needsFood || needsWater)
//         {
//             if (totalFood < requiredFoodPerTurn || totalWater < requiredWaterPerTurn)
//             {
//                 // AI is in dire need; heavily reduce survival factor.
//                 return 0.5f;
//             }
//             else
//             {
//                 // AI has low reserves; moderately reduce survival factor.
//                 return 0.75f;
//             }
//         }
//         else
//         {
//             if (totalFood > foodFor3Turns || totalWater > waterFor3Turns)
//             {
//                 // AI has surplus; boost survival factor.
//                 return 1.25f;
//             }
//         }
        
//         return 1.0f;
//     }
// }