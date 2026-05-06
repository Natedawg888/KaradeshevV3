// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;

// public class AITechnologyPriorityCalculator : MonoBehaviour
// {
//     private AIPlayer aiPlayer;
//     private AITechnologyManager technologyManager;
//     private AIResourceManager resourceManager;
//     private AIPopulationManager populationManager;
//     private AIInventoryManager inventoryManager;

//     private void Start()
//     {
//         Transform aiPlayer = transform.parent;

//         if (aiPlayer == null)
//         {
//             //Debug.LogWarning("[AIDecisionMaker] AI Player (parent) is missing.");
//             enabled = false;
//             return;
//         }

//         technologyManager = aiPlayer.GetComponentInChildren<AITechnologyManager>();
//         resourceManager = aiPlayer.GetComponentInChildren<AIResourceManager>();
//         populationManager = aiPlayer.GetComponentInChildren<AIPopulationManager>();
//         inventoryManager = aiPlayer.GetComponentInChildren<AIInventoryManager>();
//     }

//     public int CalculateTechnologyPriority(Technology tech)
//     {
//         int basePriority = 1000; // Default priority

//         //Debug.Log($"[AITechnologyPriorityCalculator] Calculating priority for **{tech.technologyName}** (Base Priority: {basePriority})");

//         // ✅ Reduce priority if AI lacks required resources for this research
//         float resourceAvailabilityFactor = GetResourceAvailabilityFactor(tech);
//         int resourceAdjustedPriority = Mathf.RoundToInt(basePriority * resourceAvailabilityFactor);
//         //Debug.Log($"[AITechnologyPriorityCalculator] Resource Availability Factor: {resourceAvailabilityFactor} → Adjusted Priority: {resourceAdjustedPriority}");

//         // ✅ **Increase priority if technology unlocks new resources**
//         int resourceUnlockBoost = GetResourceUnlockPriorityBoost(tech);
//         int boostedPriority = resourceAdjustedPriority + resourceUnlockBoost;
//         //Debug.Log($"[AITechnologyPriorityCalculator] Resource Unlock Boost: +{resourceUnlockBoost} → Boosted Priority: {boostedPriority}");

//         int adjustedPriority = Mathf.Max(1, boostedPriority); // Ensure priority never reaches zero
//         //Debug.Log($"[AITechnologyPriorityCalculator] Adjusted Priority for **{tech.technologyName}**: {adjustedPriority}");
        
//         // ✅ Reduce priority if AI lacks food/water for population upkeep
//         float survivalFactor = GetSurvivalFactor();
//         int survivalAdjustedPriority = Mathf.RoundToInt(adjustedPriority * survivalFactor);
//         //Debug.Log($"[AITechnologyPriorityCalculator] Survival Factor: {survivalFactor} → Adjusted Priority: {survivalAdjustedPriority}");

//         int finalPriority = Mathf.Max(1, survivalAdjustedPriority); // Ensure priority never reaches zero
//         //Debug.Log($"[AITechnologyPriorityCalculator] Priority after resource and survival adjustments for **{tech.technologyName}**: {finalPriority}");

//         // NEW: Penalize research if overall population is below half of max.
//         if (populationManager != null)
//         {
//             int currentPopulation = populationManager.GetCurrentPopulation();
//             int maxPopulation = populationManager.maxPopulation;
//             if (maxPopulation > 0 && currentPopulation < (maxPopulation / 2))
//             {
//                 finalPriority = Mathf.RoundToInt(finalPriority * 0.5f);
//                 //Debug.Log("[AITechnologyPriorityCalculator] Population is below half capacity. Research priority halved.");
//             }
            
//             // NEW: Further reduce research priority if child population is too low relative to teens/adults/elders.
//             int childCount = populationManager.GetPopulationCountByAgeGroup(AgeGroup.Child);
//             int teenCount = populationManager.GetPopulationCountByAgeGroup(AgeGroup.Teen);
//             int adultCount = populationManager.GetPopulationCountByAgeGroup(AgeGroup.Adult);
//             int elderCount = populationManager.GetPopulationCountByAgeGroup(AgeGroup.Elder);
//             int totalNonChild = teenCount + adultCount + elderCount;
//             if (totalNonChild > 0)
//             {
//                 float childRatio = (float)childCount / totalNonChild;
//                 if (childRatio < 0.2f)
//                 {
//                     finalPriority = Mathf.Max(1, finalPriority - 200);
//                     //Debug.Log("[AITechnologyPriorityCalculator] Very low child population relative to older groups. Research priority reduced by 200.");
//                 }
//                 else if (childRatio < 0.4f)
//                 {
//                     finalPriority = Mathf.Max(1, finalPriority - 100);
//                     //Debug.Log("[AITechnologyPriorityCalculator] Low child population relative to older groups. Research priority reduced by 100.");
//                 }
//             }
//         }

//         //Debug.Log($"[AITechnologyPriorityCalculator] Final Priority for **{tech.technologyName}**: {finalPriority}");
//         return finalPriority;
//     }

//     /// **🔹 Adjusts research priority based on food & water availability**
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
//             //Debug.Log($"[AITechnologyPriorityCalculator] AI has low food/water! Adjusting research priority.");

//             if (totalFood < requiredFoodPerTurn || totalWater < requiredWaterPerTurn)
//             {
//                 //Debug.Log($"[AITechnologyPriorityCalculator] 🚨 AI is **starving**! Research priority heavily reduced.");
//                 return 0.1f; // **Severe reduction (Only research survival tech)**
//             }
//             else if (totalFood < foodFor3Turns || totalWater < waterFor3Turns)
//             {
//                 //Debug.Log($"[AITechnologyPriorityCalculator] ⚠️ AI has low reserves. Research priority reduced.");
//                 return 0.25f; // **Moderate reduction**
//             }
//         }

//         return 1.0f; // **No impact if food & water are sufficient**
//     }

//     /// **🔹 Checks if the AI has access to the resources required for research**
//     private float GetResourceAvailabilityFactor(Technology tech)
//     {
//         if (tech.resourceRequirements == null || tech.resourceRequirements.Count == 0)
//             return 1.0f; // ✅ Full priority if no resources are needed

//         int totalResources = tech.resourceRequirements.Count;
//         int availableResources = 0;

//         foreach (var requiredResource in tech.resourceRequirements)
//         {
//             if (resourceManager.GetAIResourceByID(requiredResource.resourceID) != null)
//             {
//                 availableResources++;
//             }
//         }

//         // ✅ If all required resources are available, full priority (1.0)
//         if (availableResources == totalResources) return 1.0f;

//         // ✅ Otherwise, decrease priority proportional to missing resources
//         return (float)availableResources / totalResources;
//     }

//     /// **🔹 Checks if the technology is already available in other buildings**
//     private bool IsTechnologyAlreadyAvailable(Technology tech)
//     {
//         foreach (Technology researchedTech in technologyManager.GetResearchedTechnologies())
//         {
//             if (researchedTech.technologyID == tech.technologyID)
//             {
//                 return true;
//             }
//         }
//         return false;
//     }

//     /// **🔹 Calculates priority boost if technology unlocks resources**
//     private int GetResourceUnlockPriorityBoost(Technology tech)
//     {
//         if (tech == null || tech.worldUpgrades == null || tech.worldUpgrades.Count == 0)
//             return 0; // ✅ No boost if no world upgrades are present

//         int resourceUnlockCount = 0;
//         int priorityBoost = 0;

//         // ✅ Check each world upgrade for ResourceUnlock
//         foreach (WorldUpgrade upgrade in tech.worldUpgrades)
//         {
//             if (upgrade.upgradeType == WorldUpgradeType.ResourceUnlock)
//             {
//                 foreach (string resourceID in upgrade.resourceIDs)
//                 {
//                     // ✅ Check if the AI does not already have this resource
//                     if (resourceManager.GetAIResourceByID(resourceID) == null)
//                     {
//                         resourceUnlockCount++;
//                         priorityBoost += 50; // **Increase priority by 50 for each resource unlocked**
//                         //Debug.Log($"[AITechnologyPriorityCalculator] {tech.technologyName} unlocks new resource: {resourceID} (+50 Priority)");
//                     }
//                 }
//             }
//         }

//         return priorityBoost;
//     }

// }