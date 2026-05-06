// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
// using System.Linq;

// public class AIBuildingPriorityCalculator : MonoBehaviour
// {
//     private AIPlayer aiPlayer;
//     private AIBuildingManager aiBuildingManager;
//     private AITechnologyManager technologyManager;

//     private AICraftingBuildingPriorityCalculator craftingPriorityCalculator;
//     private AIStoragePriorityCalculator storagePriorityCalculator;
//     private AIProductionPriorityCalculator productionPriorityCalculator;
//     private AIShelterPriorityCalculator shelterPriorityCalculator;
//     private AIKineticWarfareBuildingPriorityCalculator kineticWarfarePriorityCalculator;

//     private void Start()
//     {
//         aiPlayer = GetComponentInParent<AIPlayer>();
//         aiBuildingManager = aiPlayer?.GetComponentInChildren<AIBuildingManager>();
//         craftingPriorityCalculator = GetComponent<AICraftingBuildingPriorityCalculator>();
//         technologyManager = aiPlayer?.GetComponentInChildren<AITechnologyManager>();
//         storagePriorityCalculator = aiPlayer?.GetComponentInChildren<AIStoragePriorityCalculator>();
//         productionPriorityCalculator = aiPlayer?.GetComponentInChildren<AIProductionPriorityCalculator>();
//         shelterPriorityCalculator = aiPlayer?.GetComponentInChildren<AIShelterPriorityCalculator>();
//         kineticWarfarePriorityCalculator = GetComponent<AIKineticWarfareBuildingPriorityCalculator>();
//     }

//     public int CalculateBuildingPriority(Building building)
//     {
//         if (aiBuildingManager == null || technologyManager == null || aiPlayer == null)
//         {
//             //Debug.LogWarning("[AIBuildingPriorityCalculator] AI Building Manager, Technology Manager, or AI Player is NULL.");
//             return 0;
//         }

//         // Get base priority based on building type.
//         int basePriority = GetBasePriority(building);
        
//         // Count buildings with the same prefix.
//         string prefix = building.buildingID.Substring(0, 3); // Get first 3-character prefix
//         int currentCount = aiBuildingManager.GetBuildingCount(prefix);
//         int maxLimit = building.buildingLimit;

//         if (maxLimit <= 0)
//             return basePriority; // Prevent division by zero

//         // Start with base priority.
//         float adjustedPriority = basePriority;

//         // ✅ Increase priority if the building unlocks researchable technologies.
//         int techBonus = GetTechnologyBasedPriorityIncrease(building);
//         adjustedPriority += techBonus;

//         // **Apply priority scaling based on current count vs. max limit.**
//         for (int i = 0; i < currentCount; i++)
//         {
//             adjustedPriority /= maxLimit;
//         }

//         // ✅ Reduce priority based on food & water availability.
//         float survivalFactor = GetSurvivalFactor();
//         adjustedPriority *= survivalFactor;

//         // ✅ Reduce priority if AI lacks resources for this building.
//         float resourceAvailabilityFactor = GetResourceAvailabilityFactor(building);
//         adjustedPriority *= resourceAvailabilityFactor;

//         // Ensure priority never reaches zero.
//         int finalPriority = Mathf.Max(Mathf.RoundToInt(adjustedPriority));

//         return finalPriority;
//     }
//     /// **🔹 Calculates additional priority based on researchable technologies**
//     private int GetTechnologyBasedPriorityIncrease(Building building)
//     {
//         if (technologyManager == null)
//         {
//             //Debug.LogWarning("[AIBuildingPriorityCalculator] Technology Manager is NULL.");
//             return 0;
//         }

//         List<Technology> researchableTechnologies = technologyManager.GetTechnologiesResearchableOnBuilding(building.buildingID);
//         if (researchableTechnologies.Count == 0) return 0; // No researchable technologies

//         int priorityIncrease = 0;
//         // Get the list of already researched technologies.
//         List<Technology> researchedTechs = technologyManager.GetResearchedTechnologies();

//         foreach (Technology tech in researchableTechnologies)
//         {
//             // Skip any technology that is already researched, by checking the researchedTechs list.
//             if (researchedTechs.Any(t => t.technologyID == tech.technologyID))
//             {
//                 //Debug.Log($"[AIBuildingPriorityCalculator] {tech.technologyName} is already researched; skipping.");
//                 continue;
//             }

//             bool isAlreadyAvailable = IsTechnologyAlreadyResearchable(tech);
//             if (!isAlreadyAvailable)
//             {
//                 priorityIncrease += 25; // Increase priority for unique tech unlocks
//                 //Debug.Log($"[AIBuildingPriorityCalculator] {building.buildingName} gets +25 priority for unique tech: {tech.technologyName}");
//             }
//             else
//             {
//                 priorityIncrease -= 25; // Increase priority for unique tech unlocks
//                 //Debug.Log($"[AIBuildingPriorityCalculator] {building.buildingName} gets -25 priority for already researchable tech: {tech.technologyName}");
//             }
//             // You might decide not to subtract priority for tech that are already available.
//         }

//         return priorityIncrease;
//     }

//     private bool IsTechnologyAlreadyResearchable(Technology tech)
//     {
//         // Check finished buildings with AIBuildingControl
//         foreach (GameObject ownedBuilding in aiBuildingManager.GetOwnedBuildings())
//         {
//             // Try AIBuildingControl first.
//             AIBuildingControl aiBuildingControl = ownedBuilding.GetComponent<AIBuildingControl>();
//             if (aiBuildingControl != null)
//             {
//                 List<Technology> techsOnBuilding = technologyManager.GetTechnologiesResearchableOnBuilding(aiBuildingControl.buildingID);
//                 if (techsOnBuilding.Contains(tech))
//                 {
//                     return true; // Technology already available
//                 }
//             }
//             else
//             {
//                 // If finished building control is not available, try BuildingControl (for buildings under construction)
//                 BuildingControl buildingControl = ownedBuilding.GetComponent<BuildingControl>();
//                 if (buildingControl != null)
//                 {
//                     List<Technology> techsOnBuilding = technologyManager.GetTechnologiesResearchableOnBuilding(buildingControl.buildingID);
//                     if (techsOnBuilding.Contains(tech))
//                     {
//                         return true;
//                     }
//                 }
//             }
//         }
        
//         // Also, check buildings under construction.
//         // (Assuming you expose the buildingsUnderConstruction collection via a public method or property.)
//         foreach (var kvp in aiBuildingManager.GetBuildingsUnderConstruction())
//         {
//             // kvp.Key is the construction GameObject, kvp.Value is the building ID.
//             string constructionBuildingID = kvp.Value;
//             List<Technology> techsOnConstruction = technologyManager.GetTechnologiesResearchableOnBuilding(constructionBuildingID);
//             if (techsOnConstruction.Contains(tech))
//             {
//                 return true;
//             }
//         }
        
//         return false;
//     }

//     /// **🔹 Base priorities based on building**
//     private int GetBasePriority(Building building)
//     {
//         int basePriority = 50; // Default base priority for all buildings

//         switch (building.buildingType)
//         {
//             case BuildingType.Shelter:
//                 return shelterPriorityCalculator.CalculateShelterPriority();
            
//             case BuildingType.FoodProduction:
//             case BuildingType.WaterProduction:
//             case BuildingType.MaterialProduction:
//                 if (productionPriorityCalculator != null && building.buildingPrefab != null)
//                 {
//                     // Try to get the BuildingConstruction component from the construction tile.
//                     BuildingConstruction construction = building.buildingPrefab.GetComponent<BuildingConstruction>();
//                     GameObject finalPrefab = null;
//                     // finalPrefab is the last building in constructionStages.
//                     if (construction != null && construction.constructionStages != null && construction.constructionStages.Count > 0)
//                     {
//                         finalPrefab = construction.constructionStages[construction.constructionStages.Count - 1];
//                     }
//                     else
//                     {
//                         // Fallback: use the construction tile itself.
//                         finalPrefab = building.buildingPrefab;
//                     }

//                     if (finalPrefab != null)
//                     {
//                         ProductionBuildingControl prodControl = finalPrefab.GetComponent<ProductionBuildingControl>();
//                         if (prodControl != null)
//                         {
//                             switch (building.buildingType)
//                             {
//                                 case BuildingType.FoodProduction:
//                                     return productionPriorityCalculator.CalculateFoodProductionBasePriority(prodControl);
//                                 case BuildingType.WaterProduction:
//                                     return productionPriorityCalculator.CalculateWaterProductionBasePriority(prodControl);
//                                 case BuildingType.MaterialProduction:
//                                     return productionPriorityCalculator.CalculateMaterialProductionBasePriority(prodControl);
//                             }
//                         }
//                     }
//                 }
//             return basePriority;
            
//             case BuildingType.Storage:
//                 if (storagePriorityCalculator != null && building.buildingPrefab != null)
//                 {
//                     // Try to get the BuildingConstruction component from the construction tile.
//                     BuildingConstruction construction = building.buildingPrefab.GetComponent<BuildingConstruction>();
//                     GameObject finalPrefab = null;
//                     // finalBuildingPrefab is the last building in constructionStages.
//                     if (construction != null && construction.constructionStages != null && construction.constructionStages.Count > 0)
//                     {
//                         finalPrefab = construction.constructionStages[construction.constructionStages.Count - 1];
//                     }
//                     else
//                     {
//                         // Fallback: use the construction tile itself.
//                         finalPrefab = building.buildingPrefab;
//                     }

//                     if (finalPrefab != null)
//                     {
//                         StorageBuildingControl storageControl = finalPrefab.GetComponent<StorageBuildingControl>();
//                         if (storageControl != null)
//                         {
//                             return storagePriorityCalculator.CalculateStorageBuildingPriority(storageControl, basePriority);
//                         }
//                     }
//                 }
//                 return basePriority;
            
//             case BuildingType.Crafting:
//                 return craftingPriorityCalculator.CalculateCraftingBuildingPriority(building, basePriority);
            
//             case BuildingType.Trade:
//                 return basePriority + 70; // Trade allows AI to exchange goods
            
//             case BuildingType.KineticWarfare:
//                 return kineticWarfarePriorityCalculator.CalculateKineticWarfareBuildingPriority(building, basePriority);
            
//             case BuildingType.Health:
//                 return basePriority + 50; // Health facilities prevent AI deaths
            
//             case BuildingType.Culture:
//                 return basePriority + 40; // Culture buildings improve AI status & happiness
            
//             default:
//                 return basePriority; // Lowest priority for unknown building types
//         }
//     }

//     private float GetSurvivalFactor()
//     {
//         if (aiPlayer == null || aiPlayer.GetComponentInChildren<AIPopulationManager>() == null || 
//             aiPlayer.GetComponentInChildren<AIInventoryManager>() == null)
//             return 1.0f;

//         AIPopulationManager populationManager = aiPlayer.GetComponentInChildren<AIPopulationManager>();
//         AIInventoryManager inventoryManager = aiPlayer.GetComponentInChildren<AIInventoryManager>();

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
//             //Debug.Log($"[AIBuildingPriorityCalculator] AI has low food/water! Adjusting building priority.");

//             if (totalFood < requiredFoodPerTurn || totalWater < requiredWaterPerTurn)
//             {
//                 //Debug.Log($"[AIBuildingPriorityCalculator] 🚨 AI is **starving**! Building priority heavily reduced.");
//                 return 0.1f; // **Severe reduction (Only build survival structures)**
//             }
//             else if (totalFood < foodFor3Turns || totalWater < waterFor3Turns)
//             {
//                 //Debug.Log($"[AIBuildingPriorityCalculator] ⚠️ AI has low reserves. Building priority reduced.");
//                 return 0.5f; // **Moderate reduction**
//             }
//         }

//         return 1.0f; // **No impact if food & water are sufficient**
//     }

//     private float GetResourceAvailabilityFactor(Building building)
//     {
//         if (building.requiredResources == null || building.requiredResources.Count == 0)
//             return 1.0f; // ✅ Full priority if no resources are needed

//         AIInventoryManager inventoryManager = aiPlayer.GetComponentInChildren<AIInventoryManager>();

//         int totalResources = building.requiredResources.Count;
//         int availableResources = 0;

//         foreach (var requiredResource in building.requiredResources)
//         {
//             if (inventoryManager.HasEnoughResource(requiredResource.resourceID, requiredResource.amount))
//             {
//                 availableResources++;
//             }
//         }

//         // ✅ If all required resources are available, full priority (1.0)
//         if (availableResources == totalResources) return 1.0f;

//         // ✅ Otherwise, decrease priority proportional to missing resources
//         return (float)availableResources / totalResources;
//     }

// }