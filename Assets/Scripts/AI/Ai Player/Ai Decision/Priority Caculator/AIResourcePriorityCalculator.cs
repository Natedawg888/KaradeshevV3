// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;

// [System.Serializable]
// public class SerializableResourcePriority
// {
//     public string resourceID;
//     public float priority;

//     public SerializableResourcePriority(string id, float priorityValue)
//     {
//         resourceID = id;
//         priority = priorityValue;
//     }
// }

// public class AIResourcePriorityCalculator : MonoBehaviour
// {
//     [SerializeField] private List<SerializableResourcePriority> priorityList = new List<SerializableResourcePriority>();
//     private Dictionary<string, float> resourcePriorities = new Dictionary<string, float>();

//     public float defaultPriority = 40f;
//     public float foodPriorityMultiplier = 1.5f;
//     public float waterPriorityMultiplier = 2f;
//     public float gfdMultiplier = 2f;

//     private AIInventoryManager aiInventory;
//     private AIPlayer aiPlayer;
//     private AIResourceTileTracker resourceTileTracker;
//     private AITileTracker tileTracker;
//     private AIPopulationManager populationManager;
//     private AIResourceManager aiResourceManager;

//     private void Awake()
//     {
//         aiPlayer = GetComponentInParent<AIPlayer>();
//         if (aiPlayer == null)
//         {
//             //Debug.LogError("[AIResourcePriority] AIPlayer not found!");
//             enabled = false;
//             return;
//         }

//         aiInventory = aiPlayer.GetComponentInChildren<AIInventoryManager>();
//         resourceTileTracker = aiPlayer.GetComponentInChildren<AIResourceTileTracker>();
//         tileTracker = aiPlayer.GetComponentInChildren<AITileTracker>();
//         populationManager = aiPlayer.GetComponentInChildren<AIPopulationManager>();
//         aiResourceManager = aiPlayer.GetComponentInChildren<AIResourceManager>();

//         if (aiInventory == null || aiResourceManager == null)
//         {
//             //Debug.LogError("[AIResourcePriority] Missing essential components!");
//             enabled = false;
//             return;
//         }

//         CalculatePriorities();
//     }

//     public void CalculatePriorities()
//     {
//         resourcePriorities.Clear();
//         priorityList.Clear();

//         float foodAmount = aiInventory.GetTotalNonWaterFoodAmount();
//         float waterAmount = aiInventory.GetResourceAmount("WFR");

//         float survivalFactor = GetSurvivalFactor();

//         foreach (var resource in GetAvailableResourcesForLevel(aiPlayer.aiLevel))
//         {
//             float priority = defaultPriority;

//             // For Food (excluding water) and Water resources, use existing multipliers.
//             if (resource.resourceType == ResourceType.Food && resource.resourceID != "WFR")
//             {
//                 priority *= GetSupplyMultiplier(foodAmount) * survivalFactor;
//             }
//             else if (resource.resourceID == "WFR")
//             {
//                 priority *= GetSupplyMultiplier(waterAmount);
//             }
//             // For Material resources, we start with zero priority.
//             else if (resource.resourceType == ResourceType.Material)
//             {
//                 priority = 5f;
//             }

//             // Set priority to 0 for specific resource IDs.
//             if (resource.resourceID == "SPF" || resource.resourceID == "WCT")
//             {
//                 priority = 0f;
//             }

//             resourcePriorities[resource.resourceID] = priority;
//             priorityList.Add(new SerializableResourcePriority(resource.resourceID, priority));
//         }

//         //Debug.Log($"[AIResourcePriority] Updated {priorityList.Count} resource priorities.");
//     }

//     private float GetSurvivalFactor()
//     {
//         int totalFood = aiInventory.GetTotalNonWaterFoodAmount();
//         int totalWater = aiInventory.GetResourceAmount("WFR");
//         int requiredFoodPerTurn = populationManager.GetCurrentPopulation() * populationManager.foodConsumptionPerPerson;
//         int requiredWaterPerTurn = populationManager.GetCurrentPopulation() * populationManager.waterConsumptionPerPerson;

//         if (requiredFoodPerTurn == 0 || requiredWaterPerTurn == 0)
//             return 1.0f;

//         int foodTurns = totalFood / requiredFoodPerTurn;
//         int waterTurns = totalWater / requiredWaterPerTurn;

//         // Critical Need (less than 1 turn)
//         if (foodTurns < 1 || waterTurns < 1)
//         {
//             return 2.0f;
//         }

//         // Moderate Need (less than 3 turns)
//         if (foodTurns < 3 || waterTurns < 3)
//         {
//             return 1.5f;
//         }

//         // Surplus: Lower priority if food is sufficient for many turns
//         if (foodTurns >= 6)
//         {
//             return 0.25f; // Surplus for 6+ turns, lower priority significantly
//         }
//         else if (foodTurns >= 5)
//         {
//             return 0.5f; // Surplus for 5 turns, reduce priority slightly
//         }

//         return 1.0f; // Balanced state, no adjustment needed
//     }

//     private float GetSupplyMultiplier(float amount)
//     {
//         if (amount <= 25) return 2f;
//         if (amount <= 50) return 1.5f;
//         if (amount <= 100) return 1.25f;
//         return 0.5f;
//     }

//     public List<GameObject> GetAvailableResourceTiles()
//     {
//         List<GameObject> tileObjects = new List<GameObject>();

//         if (resourceTileTracker == null || tileTracker == null) return tileObjects;

//         foreach (var trackedTile in resourceTileTracker.GetResourceTiles())
//         {
//             GameObject tileObject = tileTracker.GetTileGameObjectByPosition(trackedTile.tilePosition);
//             if (tileObject != null) tileObjects.Add(tileObject);
//         }

//         return tileObjects;
//     }

//     public void IncreaseResourcePriority(string resourceID, float amount)
//     {
//         Resource res = aiResourceManager.GetAIResourceByID(resourceID);
        
//         // If the resource is "GFD", handle with the special method.
//         if (resourceID == "GFD")
//         {
//             IncreasePriorityForGFD();
//             return;
//         }

//         // Skip locked resources.
//         if (res == null || !aiResourceManager.IsResourceUnlocked(res))
//         {
//             Debug.Log($"[AIResourcePriorityCalculator] Resource {resourceID} is locked or missing. Skipping priority increase.");
//             return;
//         }

//         // Increase priority and log the change.
//         if (resourcePriorities.ContainsKey(resourceID))
//         {
//             resourcePriorities[resourceID] += amount;
//         }
//         else
//         {
//             resourcePriorities[resourceID] = defaultPriority + amount;
//         }

//         Debug.Log($"[AIResourcePriorityCalculator] Increased priority for {resourceID} by {amount}. New Priority: {resourcePriorities[resourceID]}");

//         UpdatePriorityList();
//     }

//     private void AdjustPrioritiesBasedOnSupply()
//     {
//         // Iterate over a copy of the keys.
//         foreach (var key in new List<string>(resourcePriorities.Keys))
//         {
//             // Skip adjustments for GFD and WFR.
//             if (key == "GFD" || key == "WFR")
//                 continue;

//             // Get the current supply for this resource.
//             float currentSupply = aiInventory.GetResourceAmount(key);
//             // Use the supply multiplier to decide the adjustment.
//             float supplyMultiplier = GetSupplyMultiplier(currentSupply);

//             // Only adjust downward when supply is high (i.e. multiplier < 1).
//             if (supplyMultiplier < 1f)
//             {
//                 // Multiply the current priority by the supply multiplier.
//                 resourcePriorities[key] *= supplyMultiplier;
//                 Debug.Log($"[AIResourcePriority] Adjusted priority for {key}: supply = {currentSupply}, multiplier = {supplyMultiplier}, new priority = {resourcePriorities[key]}");
//             }
//         }
//     }

//     public void IncreasePriorityForGFD()
//     {
//         foreach (var resource in aiInventory.GetAvailableResources())
//         {
//             Resource r = aiResourceManager.GetAIResourceByID(resource.Key);
//             if (r == null || !aiResourceManager.IsResourceUnlocked(r))
//                 continue;

//             if (resource.Key != "SPF" && resource.Key != "WFR" && resource.Key != "WCT")
//             {
//                 if (resourcePriorities.ContainsKey(resource.Key))
//                 {
//                     resourcePriorities[resource.Key] += gfdMultiplier;
//                 }
//                 else
//                 {
//                     resourcePriorities[resource.Key] = defaultPriority + gfdMultiplier;
//                 }

//                 Debug.Log($"[AIResourcePriorityCalculator] GFD Boost for {resource.Key}: New Priority = {resourcePriorities[resource.Key]}");
//             }
//         }

//         UpdatePriorityList();
//     }

//     private List<Resource> GetAvailableResourcesForLevel(int level)
//     {
//         List<Resource> unlockedResources = new List<Resource>();

//         if (aiResourceManager == null)
//         {
//             //Debug.LogError("[AIResourcePriority] AIResourceManager not found!");
//             return unlockedResources;
//         }

//         unlockedResources.AddRange(aiResourceManager.GetResourcesByType(ResourceType.Food));
//         unlockedResources.AddRange(aiResourceManager.GetResourcesByType(ResourceType.Material));

//         return unlockedResources;
//     }

//     public Dictionary<string, float> GetResourcePriorities() => new Dictionary<string, float>(resourcePriorities);

//     private void UpdatePriorityList()
//     {
//         priorityList.Clear();
//         foreach (var kvp in resourcePriorities)
//         {
//             priorityList.Add(new SerializableResourcePriority(kvp.Key, kvp.Value));
//         }

//         AdjustPrioritiesBasedOnSupply();

//         Debug.Log($"[AIResourcePriorityCalculator] Updated priority list with {priorityList.Count} resources.");
//     }

//     public bool DoesAIHaveWaterNeed()
//     {
//         return aiInventory.GetResourceAmount("WFR") < populationManager.waterConsumptionPerPerson * 3;
//     }

//     public bool DoesAIHaveFoodNeed()
//     {
//         return aiInventory.GetTotalNonWaterFoodAmount() < populationManager.foodConsumptionPerPerson * 3;
//     }

//     /// **🔹 Retrieves the priority of a given resource**
//     public float GetResourcePriority(string resourceID)
//     {
//         if (resourcePriorities.ContainsKey(resourceID))
//         {
//             return resourcePriorities[resourceID];
//         }
//         return 0f; // Default to 0 if the resource has no priority
//     }

//     public AIResourcePriorityCalculatorSaveData SaveState()
//     {
//         AIResourcePriorityCalculatorSaveData data = new AIResourcePriorityCalculatorSaveData();
//         // Save a copy of the current priority list.
//         data.priorityList = new List<SerializableResourcePriority>(priorityList);
//         return data;
//     }

//     public void LoadState(AIResourcePriorityCalculatorSaveData data)
//     {
//         if (data == null) return;
        
//         // Replace our current priority list with the saved one.
//         priorityList = new List<SerializableResourcePriority>(data.priorityList);
        
//         // Update our internal dictionary to match.
//         resourcePriorities.Clear();
//         foreach (var entry in priorityList)
//         {
//             resourcePriorities[entry.resourceID] = entry.priority;
//         }
        
//         Debug.Log($"[AIResourcePriorityCalculator] Loaded {priorityList.Count} resource priorities.");
//     }
// }