// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
// using System.Linq;

// public class AIPopulationIncreasePlanEvaluator : MonoBehaviour, IEvaluator
// {
//     private AIPopulationManager populationManager;
//     private AIInventoryManager inventoryManager;
//     private AIResourcePriorityCalculator resourcePriorityCalculator;
//     private AIPopulationIncreasePlan populationIncreasePlan;

//     [Header("Evaluation Settings")]
//     public float maxPopulationPenaltyWeight = 2f;
//     public float lowPopulationBoost = 3.0f;
//     public float lowChildPopulationBoost = 2f;
//     public float underHalfBoostFactor = 30f; 
//     public float activeIncreaseOrderPenalty = 50f;
//     public float overpopulationHardCap = 0.95f; // AI stops growing if above 95% max population
//     public float minimumPopulationThresholdPercentage = 0.25f; // **Now a percentage (e.g., 25% of max population)**

//     private void Awake()
//     {
//         Transform aiPlayer = transform.parent;
//         if (aiPlayer == null)
//         {
//             //Debug.LogWarning("[AIPopulationIncreasePlanEvaluator] AI Player (parent) is missing.");
//             enabled = false;
//             return;
//         }

//         populationManager = aiPlayer.GetComponentInChildren<AIPopulationManager>();
//         inventoryManager = aiPlayer.GetComponentInChildren<AIInventoryManager>();
//         resourcePriorityCalculator = aiPlayer.GetComponentInChildren<AIResourcePriorityCalculator>();
//         populationIncreasePlan = aiPlayer.GetComponentInChildren<AIPopulationIncreasePlan>();

//         if (populationManager == null || inventoryManager == null || resourcePriorityCalculator == null || populationIncreasePlan == null)
//         {
//             //Debug.LogWarning("[AIPopulationIncreasePlanEvaluator] Missing required components. Disabling script.");
//             enabled = false;
//         }
//     }

//     // IEvaluator interface implementation
//     public int Evaluate()
//     {
//         return EvaluatePopulationIncreasePriority();
//     }

//     public int EvaluatePopulationIncreasePriority()
//     {
//         if (populationManager == null || inventoryManager == null || populationIncreasePlan == null)
//             return 0;

//         int priority = 0;
//         int currentPopulation = populationManager.GetCurrentPopulation();
//         int maxPopulation = populationManager.maxPopulation;

//         // Hard block on growth if at or above maximum.
//         if (currentPopulation >= maxPopulation)
//         {
//             //Debug.Log($"[AIPopulationIncreasePlanEvaluator] AI has reached max population ({currentPopulation}/{maxPopulation}). Stopping growth.");
//             return -999;
//         }

//         // Evaluate various factors.
//         priority += EvaluateLowPopulationBoost();
//         priority += EvaluateFoodSurplus();
//         priority += EvaluatePopulationCapacity();
//         priority += EvaluatePopulationAgeDistribution();
//         priority -= EvaluateActiveIncreaseOrders();

//         return Mathf.Max(priority, 0);
//     }

//     // Increase priority if population is too low.
//     private int EvaluateLowPopulationBoost()
//     {
//         if (populationManager == null)
//             return 0;

//         int maxPopulation = populationManager.maxPopulation;
//         int minimumPopulationThreshold = Mathf.CeilToInt(maxPopulation * minimumPopulationThresholdPercentage);
//         int currentPopulation = populationManager.GetCurrentPopulation();
//         int priority = 0;

//         // Boost if below the minimum threshold.
//         if (currentPopulation < minimumPopulationThreshold)
//         {
//             int deficit = minimumPopulationThreshold - currentPopulation;
//             priority += (int)(deficit * 20 * lowPopulationBoost);
//             //Debug.Log($"[AIPopulationIncreasePlanEvaluator] Population below threshold ({currentPopulation}/{minimumPopulationThreshold}). Boost: {priority}.");
//         }
//         // Additional boost if below half of max population.
//         int halfPopulation = maxPopulation / 2;
//         if (currentPopulation < halfPopulation)
//         {
//             int extraDeficit = halfPopulation - currentPopulation;
//             int extraBoost = (int)(extraDeficit * underHalfBoostFactor);
//             priority += extraBoost;
//             //Debug.Log($"[AIPopulationIncreasePlanEvaluator] Population under half capacity ({currentPopulation}/{halfPopulation}). Extra boost: {extraBoost}.");
//         }
//         return priority;
//     }

//     // Evaluate food and water surplus. If insufficient food for 3 turns, stop growth.
//     private int EvaluateFoodSurplus()
//     {
//         if (populationManager == null || inventoryManager == null)
//             return 0;

//         int totalFood = inventoryManager.GetTotalNonWaterFoodAmount();
//         int totalWater = inventoryManager.GetResourceAmount("WFR");
//         int requiredFoodPerTurn = populationManager.GetCurrentPopulation() * populationManager.foodConsumptionPerPerson;
//         int requiredWaterPerTurn = populationManager.GetCurrentPopulation() * populationManager.waterConsumptionPerPerson;

//         if (requiredFoodPerTurn == 0 || requiredWaterPerTurn == 0)
//             return 0;

//         int foodFor3Turns = requiredFoodPerTurn * 3;
//         int waterFor3Turns = requiredWaterPerTurn * 3;

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

//         int priority = 0;

//         //Debug.Log($"[AIPopulationIncreasePlanEvaluator] Food: {totalFood}, Water: {totalWater}");
//         //Debug.Log($"[AIPopulationIncreasePlanEvaluator] Required Food: {requiredFoodPerTurn}");
//         // ✅ **Food Surplus Adjustments**
//             if (!aiNeedsFood) 
//             {
//                 if (totalFood >= foodFor10Turns) 
//                 {
//                     priority += 200; // Large penalty if AI has food for 10+ turns
//                 }
//                 else if (totalFood >= foodFor8Turns) 
//                 {
//                     priority += 150; 
//                 }
//                 else if (totalFood >= foodFor6Turns) 
//                 {
//                     priority += 100; 
//                 }
//                 else if (totalFood >= foodFor4Turns) 
//                 {
//                     priority += 50; 
//                 }
//             }

//             if (aiNeedsWater) 
//             {
//                 if (totalWater >= waterFor10Turns) 
//                 {
//                     priority -= 200; // Large penalty if AI has water for 10+ turns
//                 }
//                 else if (totalWater >= waterFor8Turns) 
//                 {
//                     priority -= 150; 
//                 }
//                 else if (totalWater >= waterFor6Turns) 
//                 {
//                     priority -= 100; 
//                 }
//                 else if (totalWater >= waterFor4Turns) 
//                 {
//                     priority -= 50; 
//                 }
//             }

//         return priority;
//     }

//     // Evaluate population capacity: if near max, lower growth priority.
//     private int EvaluatePopulationCapacity()
//     {
//         if (populationManager == null)
//             return 0;

//         int currentPopulation = populationManager.GetCurrentPopulation();
//         int maxPopulation = populationManager.maxPopulation;
//         if (maxPopulation == 0)
//             return 0;
//         float ratio = (float)currentPopulation / maxPopulation;
//         int priority = 0;
//         if (ratio >= overpopulationHardCap)
//         {
//             //Debug.Log($"[AIPopulationIncreasePlanEvaluator] Above capacity ({ratio * 100}%).");
//             return -500;
//         }
//         priority += (ratio < 0.5f) ? 50 : 0;
//         priority -= (ratio > 0.9f) ? (int)(40 * maxPopulationPenaltyWeight) :
//                     (ratio > 0.75f) ? (int)(20 * maxPopulationPenaltyWeight) : 0;
//         return priority;
//     }

//     // Evaluate age distribution for a healthy population mix.
//     private int EvaluatePopulationAgeDistribution()
//     {
//         if (populationManager == null)
//             return 0;

//         int childCount = populationManager.GetPopulationCountByAgeGroup(AgeGroup.Child);
//         int teenCount = populationManager.GetPopulationCountByAgeGroup(AgeGroup.Teen);
//         int adultCount = populationManager.GetPopulationCountByAgeGroup(AgeGroup.Adult);
//         int elderCount = populationManager.GetPopulationCountByAgeGroup(AgeGroup.Elder);
//         int totalNonChild = teenCount + adultCount + elderCount;
//         if (totalNonChild == 0)
//             return 0;
//         float childRatio = (float)childCount / totalNonChild;
//         int priority = 0;
//         priority += (childRatio < 0.2f) ? (int)(120 * lowChildPopulationBoost) :
//                     (childRatio < 0.4f) ? (int)(60 * lowChildPopulationBoost) :
//                     (childRatio < 0.6f) ? (int)(30 * lowChildPopulationBoost) : 0;
//         return priority;
//     }

//     // Penalize if there are many active increase orders.
//     private int EvaluateActiveIncreaseOrders()
//     {
//         if (populationIncreasePlan == null)
//             return 0;
//         int totalActiveOrders = 0;
//         List<AIPopulationIncreasePlan.PopulationIncreaseOrder> activeOrders = populationIncreasePlan.GetActivePopulationOrders();
//         foreach (var order in activeOrders)
//         {
//             totalActiveOrders += order.orderCount;
//         }
//         if (totalActiveOrders > 0)
//         {
//             int penalty = totalActiveOrders * (int)(activeIncreaseOrderPenalty * 2f);
//             //Debug.Log($"[AIPopulationIncreasePlanEvaluator] Reducing priority by {penalty} due to {totalActiveOrders} active orders.");
//             return penalty;
//         }
//         return 0;
//     }
// }