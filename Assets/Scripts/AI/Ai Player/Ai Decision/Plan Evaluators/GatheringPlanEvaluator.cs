// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
// using System.Linq;

// public class GatheringPlanEvaluator : MonoBehaviour, IEvaluator<EnvironmentControl>
// {
//     public AIResourcePriorityCalculator priorityCalculator;
//     public AIInventoryManager inventoryManager;
//     public AIPopulationManager populationManager;
//     public AIGatheringManager gatheringManager;
//     public AIResourceManager aiResourceManager;

//     private ResourceManager resourceManager;

//     // Tunable weights:
//     public int baseGathering = 10;
//     public int waterUrgencyBonus = 500;
//     public int foodUrgencyBonus = 500;

//     // Food level thresholds & priority adjustments
//     public int tileFoodThresholdLow = 50;
//     public int tileFoodThresholdMed = 100;
//     public int tileFoodThresholdHigh = 200;
//     public int tileFoodModifierVeryLow = -200;
//     public int tileFoodModifierLow = 10;
//     public int tileFoodModifierMed = 50;
//     public int tileFoodModifierHigh = 100;

//     // Water level thresholds & priority adjustments
//     public int tileWaterThresholdLow = 50;
//     public int tileWaterThresholdMed = 100;
//     public int tileWaterThresholdHigh = 200;
//     public int tileWaterModifierVeryLow = -200;
//     public int tileWaterModifierLow = 10;
//     public int tileWaterModifierMed = 50;
//     public int tileWaterModifierHigh = 100;

//     private void Awake()
//     {
//         resourceManager = FindObjectOfType<ResourceManager>(); // ✅ Initialize reference
//         if (resourceManager == null)
//         {
//             //Debug.LogError("[GatheringPlanEvaluator] ResourceManager not found!");
//             enabled = false;
//         }
//     }

//     public int Evaluate(EnvironmentControl envControl)
//     {
//         if (envControl == null)
//             return 0;

//         int priority = baseGathering;
//         int totalAvailableFood = GetTotalAvailableFood(envControl);
//         int totalAvailableWater = GetTotalAvailableWater(envControl);

//         // **Priority Modifiers Based on Available Food**
//         if (totalAvailableFood < tileFoodThresholdLow)
//         {
//             priority += tileFoodModifierVeryLow;
//         }
//         else if (totalAvailableFood >= tileFoodThresholdLow && totalAvailableFood < tileFoodThresholdMed)
//         {
//             priority += tileFoodModifierLow;
//         }
//         else if (totalAvailableFood >= tileFoodThresholdMed && totalAvailableFood < tileFoodThresholdHigh)
//         {
//             priority += tileFoodModifierMed;
//         }
//         else if (totalAvailableFood >= tileFoodThresholdHigh)
//         {
//             priority += tileFoodModifierHigh;
//         }

//         if(envControl.resources.Exists(r => r.resourceID == "WFR"))
//         {
//             // **Priority Modifiers Based on Available Water**
//             if (totalAvailableWater < tileWaterThresholdLow)
//             {
//                 priority += tileWaterModifierVeryLow;
//             }
//             else if (totalAvailableWater >= tileWaterThresholdLow && totalAvailableWater < tileFoodThresholdMed)
//             {
//                 priority += tileWaterModifierLow;
//             }
//             else if (totalAvailableWater >= tileFoodThresholdMed && totalAvailableWater < tileWaterThresholdHigh)
//             {
//                 priority += tileWaterModifierMed;
//             }
//             else if (totalAvailableWater >= tileWaterThresholdHigh)
//             {
//                 priority += tileWaterModifierHigh;
//             }
//         }

//         // **Adjust priority based on the tile's gathering failure chance**
//         float failureChance = envControl.initialGatheringFailureChance;
//         if (failureChance < 5f) priority += 200;
//         else if (failureChance < 10f) priority += 100;
//         else if (failureChance < 15f) priority += 60;
//         else if (failureChance < 20f) priority += 30;
//         else if (failureChance > 30f) priority -= 80;


//         // **Urgency Checks**
//         if (priorityCalculator.DoesAIHaveFoodNeed()) 
//         {
//             priority += foodUrgencyBonus;
//         }
//         if (priorityCalculator.DoesAIHaveWaterNeed()) 
//         {
//             if(envControl.resources.Exists(r => r.resourceID == "WFR"))
//             {
//                 priority += waterUrgencyBonus;
//             }
//         }
//         // Add resource-based boost:
//         priority += EvaluateResourcePriority(envControl);

//         // **Factor in Food/Water Surplus Adjustments**
//         priority += EvaluateFoodWaterSurplus(envControl);

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
//                 int resourceBoost = 0;

//                 if (value > 500) 
//                     resourceBoost = 500 * resource.currentAmount;
//                 else if (value > 320) 
//                     resourceBoost = 320 * resource.currentAmount;
//                 else if (value > 160) 
//                     resourceBoost = 160 * resource.currentAmount;
//                 else if (value > 80) 
//                     resourceBoost = 80 * resource.currentAmount;
//                 else if (value > 40) 
//                     resourceBoost = 40 * resource.currentAmount;
//                 else if (value > 20) 
//                     resourceBoost = 20 * resource.currentAmount;

//                 boost += resourceBoost;
//             }
//         }
//         return boost;
//     }

//     /// **🔹 Get the total available food on the environment tile**
//     private int GetTotalAvailableFood(EnvironmentControl envControl)
//     {
//         int totalFood = 0;

//         foreach (var resource in envControl.resources)
//         {
//             // ✅ Ensure AI has unlocked this resource
//             if (aiResourceManager.GetAIResourceByID(resource.resourceID) == null)
//                 continue;

//             // ✅ Retrieve the resource details from the resource manager
//             Resource resourceDetails = resourceManager.GetResourceByID(resource.resourceID);
//             if (resourceDetails == null)
//                 continue;

//             // ✅ Ensure it's a food resource (skip materials and crafted resources)
//             if (resourceDetails.resourceType != ResourceType.Food)
//                 continue;

//             // ✅ Exclude water-related resources (WFR, WCT, SPF)
//             if (resource.resourceID == "WFR" || resource.resourceID == "WCT" || resource.resourceID == "SPF")
//                 continue;

//             // ✅ Sum all available food resources on the tile
//             totalFood += resource.currentAmount;
//         }

//         return totalFood;
//     }

//     private int GetTotalAvailableWater(EnvironmentControl envControl)
//     {
//         int totalWater = 0;

//         foreach (var resource in envControl.resources)
//         {
//             // ✅ Ensure AI has unlocked this resource
//             if (aiResourceManager.GetAIResourceByID(resource.resourceID) == null)
//                 continue;

//             // ✅ Only count "WFR" (Fresh Water)
//             if (resource.resourceID == "WFR")
//             {
//                 totalWater += resource.currentAmount;
//             }
//         }

//         return totalWater;
//     }

//     private int EvaluateFoodWaterSurplus(EnvironmentControl envControl)
//     {
//         int priority = 0;

//         if (populationManager == null || inventoryManager == null)
//             return 0;

//         int totalFood = inventoryManager.GetTotalNonWaterFoodAmount();
//         int totalWater = inventoryManager.GetResourceAmount("WFR");
//         int requiredFoodPerTurn = populationManager.GetCurrentPopulation() * populationManager.foodConsumptionPerPerson;
//         int requiredWaterPerTurn = populationManager.GetCurrentPopulation() * populationManager.waterConsumptionPerPerson;
        
//         int foodFor3Turns = requiredFoodPerTurn * 3;
//         int waterFor3Turns = requiredWaterPerTurn * 3;

//         if (totalFood <= foodFor3Turns) 
//             {
//                 priority += 200; // Increase if AI has less food for 3 turns
//             }

//         if (totalWater <= waterFor3Turns) 
//             {
//                 if(envControl.resources.Exists(r => r.resourceID == "WFR"))
//                 {
//                     priority += 300;
//                 }
//             }

//         bool aiNeedsFood = totalFood < foodFor3Turns;
//         bool aiNeedsWater = totalWater < waterFor3Turns;

//         // Calculate food & water needs for multiple turns
//         int foodFor4Turns = requiredFoodPerTurn * 4;
//         int foodFor6Turns = requiredFoodPerTurn * 6;
//         int foodFor8Turns = requiredFoodPerTurn * 8;
//         int foodFor10Turns = requiredFoodPerTurn * 10;

//         int waterFor4Turns = requiredWaterPerTurn * 4;
//         int waterFor6Turns = requiredWaterPerTurn * 6;
//         int waterFor8Turns = requiredWaterPerTurn * 8;
//         int waterFor10Turns = requiredWaterPerTurn * 10;
        
//         if (!aiNeedsWater) 
//         {
//             if (!aiNeedsFood) 
//             {
//                 // ✅ **Food Surplus Adjustments**
//                 if (totalFood >= foodFor10Turns) 
//                 {
//                     priority -= 500; // Large penalty if AI has food for 10+ turns
//                 }
//                 else if (totalFood >= foodFor8Turns) 
//                 {
//                     priority -= 300; 
//                 }
//                 else if (totalFood >= foodFor6Turns) 
//                 {
//                     priority -= 200; 
//                 }
//                 else if (totalFood >= foodFor4Turns) 
//                 {
//                     priority -= 100; 
//                 }
//             }
//         }

//         if (!aiNeedsFood)
//         {
//             if (!aiNeedsWater) 
//             {
//                 if(envControl.resources.Exists(r => r.resourceID == "WFR"))
//                 {
//                     if (totalWater >= waterFor10Turns) 
//                     {
//                         priority -= 500; // Large penalty if AI has water for 10+ turns
//                     }
//                     else if (totalWater >= waterFor8Turns) 
//                     {
//                         priority -= 300; 
//                     }
//                     else if (totalWater >= waterFor6Turns) 
//                     {
//                         priority -= 200; 
//                     }
//                     else if (totalWater >= waterFor4Turns) 
//                     {
//                         priority -= 100; 
//                     }
//                 }
//             }
//         }

//         return priority;
//     }
// }