// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;

// public class AIShelterPriorityCalculator : MonoBehaviour
// {
//     private AIPlayer aiPlayer;
//     private AIPopulationManager populationManager;
//     private AIInventoryManager inventoryManager;

//     private void Start()
//     {
//         aiPlayer = GetComponentInParent<AIPlayer>();
//         if (aiPlayer != null)
//         {
//             populationManager = aiPlayer.GetComponentInChildren<AIPopulationManager>();
//             inventoryManager = aiPlayer.GetComponentInChildren<AIInventoryManager>();
//         }
//     }

//     /// Calculates the priority for building or upgrading shelters.
//     public int CalculateShelterPriority()
//     {
//         if (populationManager == null || inventoryManager == null)
//             return 0;

//         int basePriority = 50;
//         int populationBonus = GetPopulationProximityBonus();
//         float survivalFactor = GetSurvivalFactor();
//         float populationFactor = GetPopulationFactor();

//         // Final calculated priority
//         float calculatedPriority = (basePriority + populationBonus) * survivalFactor * populationFactor;

//         return Mathf.RoundToInt(calculatedPriority);
//     }

//     /// Increases priority if population is near the max capacity.
//     private int GetPopulationProximityBonus()
//     {
//         float populationRatio = (float)populationManager.GetCurrentPopulation() / populationManager.maxPopulation;

//         if (populationRatio >= 0.9f)
//         {
//             return 300; // Very close to max, high priority.
//         }
//         else if (populationRatio >= 0.75f)
//         {
//             return 150; // Moderate proximity, medium priority.
//         }
//         else if (populationRatio >= 0.5f)
//         {
//             return 50; // Safe buffer, low priority.
//         }

//         return 0; // No priority increase if population is well under max.
//     }

//     /// Reduces priority if hunger or thirst levels are critical.
//     private float GetSurvivalFactor()
//     {
//         if (populationManager == null || inventoryManager == null)
//             return 1.0f;

//         int totalFood = inventoryManager.GetTotalNonWaterFoodAmount();
//         int totalWater = inventoryManager.GetResourceAmount("WFR"); // Fresh Water
//         int requiredFoodPerTurn = populationManager.GetCurrentPopulation() * populationManager.foodConsumptionPerPerson;
//         int requiredWaterPerTurn = populationManager.GetCurrentPopulation() * populationManager.waterConsumptionPerPerson;

//         if (requiredFoodPerTurn == 0 || requiredWaterPerTurn == 0)
//             return 1.0f;

//         int foodFor3Turns = requiredFoodPerTurn * 3;
//         int waterFor3Turns = requiredWaterPerTurn * 3;

//         bool aiNeedsFood = totalFood < foodFor3Turns;
//         bool aiNeedsWater = totalWater < waterFor3Turns;

//         if (aiNeedsFood || aiNeedsWater)
//         {
//             Debug.Log($"[AIShelterPriorityCalculator] AI has low food/water! Adjusting shelter priority.");

//             if (totalFood < requiredFoodPerTurn || totalWater < requiredWaterPerTurn)
//             {
//                 Debug.Log($"[AIShelterPriorityCalculator] 🚨 AI is **starving**! Shelter priority heavily reduced.");
//                 return 0.1f; // Severe reduction, survival first.
//             }
//             else if (totalFood < foodFor3Turns || totalWater < waterFor3Turns)
//             {
//                 Debug.Log($"[AIShelterPriorityCalculator] ⚠️ AI has low reserves. Shelter priority reduced.");
//                 return 0.25f; // Moderate reduction, reserves are low.
//             }
//         }

//         return 1.0f; // No reduction if food & water are sufficient.
//     }

//     private float GetPopulationFactor()
//     {
//         int currentPopulation = populationManager.GetCurrentPopulation();
//         if (currentPopulation < 10)
//         {
//             return 0.5f; // Population too low, deprioritize shelter expansion.
//         }
//         else if (currentPopulation < 20)
//         {
//             return 0.75f;
//         }

//         return 1.0f; // Full priority if population is sustainable.
//     }
// }