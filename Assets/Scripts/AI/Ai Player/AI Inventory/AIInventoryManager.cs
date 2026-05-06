// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;

// [System.Serializable]
// public class AIInventoryItem
// {
//     public string resourceID;
//     public string resourceName;
//     public int amount;
//     public Sprite resourceIcon;
//     public int spoilageInterval; // Total interval between spoilages
//     public int currentInterval;  // Current countdown for spoilage

//     public AIInventoryItem(string id, string name, int amount, Sprite icon, int spoilageInterval, int currentSpoilageInterval)
//     {
//         resourceID = id;
//         resourceName = name;
//         this.amount = amount;
//         resourceIcon = icon;
//         this.spoilageInterval = spoilageInterval;
//         this.currentInterval = currentSpoilageInterval; // Initialize the spoilage countdown
//     }
// }

// public class AIInventoryManager : MonoBehaviour
// {
//     [Header("AI Inventory Settings")]
//     public int maxFoodInventory = 100;
//     public int maxMaterialInventory = 200;

//     [Header("AI Inventory Lists")]
//     [SerializeField] private List<AIInventoryItem> foodInventory = new List<AIInventoryItem>();
//     [SerializeField] private List<AIInventoryItem> materialInventory = new List<AIInventoryItem>();

//     public AIResourcePriorityCalculator resourcePriorityCalculator;
//     public AIPopulationManager populationManager;
//     public AIResourceManager aiResourceManager;

//     public List<AIInventoryItem> FoodInventory { get { return foodInventory; } }
//     public List<AIInventoryItem> MaterialInventory { get { return materialInventory; } }

//     private void Start()
//     {
//         Transform aiPlayer = transform.parent;

//         if (aiPlayer == null)
//         {
//             //Debug.LogError("[AIInventoryManager] AIPlayer (parent) is missing.");
//             return;
//         }

//         resourcePriorityCalculator = aiPlayer.GetComponentInChildren<AIResourcePriorityCalculator>();
//         populationManager = aiPlayer.GetComponentInChildren<AIPopulationManager>();
//         aiResourceManager = aiPlayer.GetComponentInChildren<AIResourceManager>();

//         TurnSystem.SubscribeToEndOfTurn(OnTurnEnded);
//     }

//     private void OnDestroy()
//     {
//         TurnSystem.UnsubscribeFromEndOfTurn(OnTurnEnded);
//     }

//     private void OnTurnEnded()
//     {
//         HandleSpoilage();
//         RemoveWaterSurplus();
//         RemoveFoodSurplus();
//         RemoveMaterialSurplus();
//         resourcePriorityCalculator.CalculatePriorities();
//     }

//     public void RemoveWaterSurplus()
//     {
//         if (populationManager == null)
//             return;

//         int totalWater = GetResourceAmount("WFR"); // Fresh Water
//         int requiredWaterPerTurn = populationManager.GetCurrentPopulation() * populationManager.waterConsumptionPerPerson;

//         if (requiredWaterPerTurn == 0)
//             return;

//         int maxStoredWater = requiredWaterPerTurn * Random.Range(8, 10); // ✅ Keep 4 to 5 turns' worth
//         if (totalWater > maxStoredWater)
//         {
//             int surplusAmount = totalWater - maxStoredWater;
//             //Debug.Log($"[AIInventoryManager] AI has {totalWater} WFR, removing surplus of {surplusAmount}.");
//             RemoveResource("WFR", surplusAmount);
//         }
//     }

//     public void RemoveFoodSurplus()
//     {
//         if (populationManager == null)
//             return;

//         int requiredFoodPerTurn = populationManager.GetCurrentPopulation() * populationManager.foodConsumptionPerPerson;

//         if (requiredFoodPerTurn == 0)
//             return;

//         int maxStoredFoodPerResource = requiredFoodPerTurn * Random.Range(6, 8); // ✅ Keep 6 to 7 turns' worth

//         List<AIInventoryItem> surplusFoodItems = new List<AIInventoryItem>();

//         foreach (var item in foodInventory)
//         {
//             // ✅ Exclude water-related resources
//             if (item.resourceID == "WFR" || item.resourceID == "WCT" || item.resourceID == "SPF")
//                 continue;

//             if (item.amount > maxStoredFoodPerResource)
//             {
//                 int surplusAmount = item.amount - maxStoredFoodPerResource;
//                 //Debug.Log($"[AIInventoryManager] AI has {item.amount} of {item.resourceName}, removing surplus of {surplusAmount}.");
//                 surplusFoodItems.Add(new AIInventoryItem(item.resourceID, item.resourceName, surplusAmount, item.resourceIcon, item.spoilageInterval, item.currentInterval));
//             }
//         }

//         // **Remove surplus food items from inventory**
//         foreach (var item in surplusFoodItems)
//         {
//             RemoveResource(item.resourceID, item.amount);
//         }
//     }

//     public void RemoveMaterialSurplus()
//     {
//         // Iterate over a copy of the material list so that modifications don't interfere with the loop.
//         List<AIInventoryItem> materialItems = new List<AIInventoryItem>(materialInventory);
        
//         foreach (var item in materialItems)
//         {
//             // Assume your AIResourcePriorityCalculator has a method that returns a normalized priority for the resource.
//             float priority = resourcePriorityCalculator.GetResourcePriority(item.resourceID);
            
//             // Calculate the desired amount.
//             // For instance, if a resource is of low priority (priority near 0), the AI doesn't need many.
//             // Here we assume the desired amount is a fraction of maxMaterialInventory.
//             int desiredAmount = Mathf.FloorToInt(maxMaterialInventory * (priority / 100) / 2);

//             if (item.amount > desiredAmount)
//             {
//                 // Calculate the surplus and remove a percentage of it.
//                 int surplus = item.amount - desiredAmount;
//                 // Remove 50% of the surplus (adjust removalPercentage as needed)
//                 int removalAmount = Mathf.FloorToInt(surplus * 0.5f);

//                 if (removalAmount > 0)
//                 {
//                     RemoveResource(item.resourceID, removalAmount);
//                     //Debug.Log($"[AIInventoryManager] Removed {removalAmount} of {item.resourceName} surplus (priority: {priority}).");
//                 }
//             }
//         }
//     }

//     public void SetInventoryCapacity(int foodCapacity, int materialCapacity)
//     {
//         maxFoodInventory = foodCapacity;
//         maxMaterialInventory = materialCapacity;
//         //Debug.Log($"[AIInventory] Inventory capacity set. Food: {maxFoodInventory}, Material: {maxMaterialInventory}");
//     }

//     public void AddResource(string resourceID, int amount)
//     {
//         if (aiResourceManager == null)
//         {
//             //Debug.LogError("[AIInventoryManager] AIResourceManager not found! Cannot retrieve spoilage rates.");
//             return;
//         }

//         Resource aiResource = aiResourceManager.GetAIResourceByID(resourceID);
//         if (aiResource == null)
//         {
//             //Debug.LogWarning($"[AIInventoryManager] Resource with ID {resourceID} not found in AI resources!");
//             return;
//         }

//         List<AIInventoryItem> inventory = (aiResource.resourceType == ResourceType.Food) ? foodInventory : materialInventory;
//         int maxInventory = (aiResource.resourceType == ResourceType.Food) ? maxFoodInventory : maxMaterialInventory;

//         AIInventoryItem item = inventory.Find(i => i.resourceID == resourceID);
//         int totalAmount = (item != null ? item.amount : 0) + amount;

//         if (totalAmount > maxInventory)
//         {
//             //Debug.LogWarning($"[AIInventoryManager] Cannot add {amount} of {aiResource.resourceName}. Inventory limit reached.");
//             amount = maxInventory - (item != null ? item.amount : 0);
//         }

//         // ✅ Retrieve updated spoilage rate & interval from AIResourceManager
//         float updatedSpoilageRate = aiResource.spoilageRate;
//         float updatedSpoilageInterval = aiResource.spoilageInterval;

//         // ✅ Explicitly cast to integers
//         int spoilageIntervalInt = Mathf.RoundToInt(updatedSpoilageInterval);
//         int spoilageRateInt = Mathf.RoundToInt(updatedSpoilageRate);

//         if (item != null)
//         {
//             item.amount += amount;
//         }
//         else if (amount > 0)
//         {
//             inventory.Add(new AIInventoryItem(resourceID, aiResource.resourceName, amount, aiResource.resourceIcon, spoilageIntervalInt, spoilageIntervalInt));
//         }

//         //Debug.Log($"[AIInventoryManager] Added {amount} of {aiResource.resourceName} with spoilage interval {spoilageIntervalInt} turns.");
//     }

//     public bool RemoveGenericFood(int amount)
//     {
//         int totalRemoved = 0;
//         List<AIInventoryItem> foodItems = new List<AIInventoryItem>();

//         // Exclude "SPF" (Spoiled Food), "WFR" (Fresh Water), and "WCT" (Contaminated Water)
//         foreach (var item in foodInventory)
//         {
//             if (item.resourceID != "WFR" && item.resourceID != "WCT" && item.resourceID != "SPF")
//             {
//                 foodItems.Add(item);
//             }
//         }

//         // Shuffle the list for randomness
//         System.Random random = new System.Random();
//         foodItems.Sort((a, b) => random.Next(-1, 2));

//         foreach (var item in foodItems)
//         {
//             int removeAmount = Mathf.Min(item.amount, amount - totalRemoved);
//             item.amount -= removeAmount;
//             totalRemoved += removeAmount;

//             if (item.amount <= 0)
//             {
//                 foodInventory.Remove(item);
//             }

//             if (totalRemoved >= amount)
//             {
//                 break;
//             }
//         }

//         return totalRemoved >= amount;
//     }

//     public int GetTotalNonWaterFoodAmount()
//     {
//         int totalFoodAmount = 0;
//         foreach (var item in foodInventory)
//         {
//             if (item.resourceID != "WFR" && item.resourceID != "WCT" && item.resourceID != "SPF")
//             {
//                 totalFoodAmount += item.amount;
//             }
//         }
//         //Debug.Log($"[AIInventory] Total non-water food amount: {totalFoodAmount}");
//         return totalFoodAmount;
//     }

//     public int GetTotalFoodAmount()
//     {
//         int totalFoodAmount = 0;
//         foreach (var item in foodInventory)
//         {
//             totalFoodAmount += item.amount;
//         }
//         //Debug.Log($"[AIInventory] Total non-water food amount: {totalFoodAmount}");
//         return totalFoodAmount;
//     }

//     public int GetTotalMaterialAmount()
//     {
//         int totalMaterialAmount = 0;
        
//         foreach (var item in materialInventory)
//         {
//             totalMaterialAmount += item.amount;
//         }

//         //Debug.Log($"[AIInventoryManager] Total material amount: {totalMaterialAmount}");
//         return totalMaterialAmount;
//     }


//     public bool HasEnoughResource(string resourceID, int requiredAmount)
//     {
//         if (resourceID == "GFD") // Generic Food
//         {
//             int totalNonWaterFood = GetTotalNonWaterFoodAmount();
//             //Debug.Log($"[AIInventory] Total non-water food available: {totalNonWaterFood}, required: {requiredAmount}");
//             return totalNonWaterFood >= requiredAmount;
//         }
//         else
//         {
//             int currentAmount = GetResourceAmount(resourceID);
//             //Debug.Log($"[AIInventory] Resource {resourceID} available: {currentAmount}, required: {requiredAmount}");
//             return currentAmount >= requiredAmount;
//         }
//     }

//     public int GetResourceAmount(string resourceID)
//     {
//         if (resourceID == "GFD") // Generic Food (excluding WFR, WCT, SPF)
//         {
//             return GetTotalNonWaterFoodAmount();
//         }

//         Resource resource = ResourceManager.Instance.GetResourceByID(resourceID);
//         if (resource == null)
//         {
//             //Debug.LogWarning($"[AIInventory] Resource with ID {resourceID} not found!");
//             return 0;
//         }

//         List<AIInventoryItem> inventory = resource.resourceType == ResourceType.Food ? foodInventory : materialInventory;
//         AIInventoryItem item = inventory.Find(i => i.resourceID == resourceID);

//         return item != null ? item.amount : 0;
//     }

//     public void RemoveResource(string resourceID, int amount)
//     {
//         if (resourceID == "GFD")
//         {
//             RemoveGenericFood(amount);
//             return;
//         }

//         Resource resource = ResourceManager.Instance.GetResourceByID(resourceID);
//         if (resource == null)
//         {
//             //Debug.LogWarning($"[AIInventory] Resource with ID {resourceID} not found!");
//             return;
//         }

//         List<AIInventoryItem> inventory = resource.resourceType == ResourceType.Food ? foodInventory : materialInventory;
//         AIInventoryItem item = inventory.Find(i => i.resourceID == resourceID);

//         if (item != null)
//         {
//             item.amount -= amount;
//             if (item.amount <= 0)
//             {
//                 inventory.Remove(item);
//             }
//         }
//     }

//     private void HandleSpoilage()
//     {
//         List<AIInventoryItem> spoiledFoodItems = new List<AIInventoryItem>();
//         List<AIInventoryItem> spoiledMaterialItems = new List<AIInventoryItem>();

//         for (int i = foodInventory.Count - 1; i >= 0; i--)
//         {
//             AIInventoryItem item = foodInventory[i];
//             Resource resource = aiResourceManager.GetAIResourceByID(item.resourceID);

//             if (resource != null && resource.spoilageRate > 0)
//             {
//                 if (item.currentInterval > 0)
//                 {
//                     item.currentInterval--;
//                 }

//                 if (item.currentInterval <= 0)
//                 {
//                     int spoilageAmount = Mathf.Max(1, Mathf.FloorToInt(item.amount * (resource.spoilageRate / 100f)));
//                     item.amount -= spoilageAmount;

//                     if (item.amount <= 0)
//                     {
//                         spoiledFoodItems.Add(item);
//                     }

//                     item.currentInterval = item.spoilageInterval;
//                 }
//             }
//         }

//         for (int i = materialInventory.Count - 1; i >= 0; i--)
//         {
//             AIInventoryItem item = materialInventory[i];
//             Resource resource = aiResourceManager.GetAIResourceByID(item.resourceID);

//             if (resource != null && resource.spoilageRate > 0)
//             {
//                 if (item.currentInterval > 0)
//                 {
//                     item.currentInterval--;
//                 }

//                 if (item.currentInterval <= 0)
//                 {
//                     int spoilageAmount = Mathf.Max(1, Mathf.FloorToInt(item.amount * (resource.spoilageRate / 100f)));
//                     item.amount -= spoilageAmount;

//                     if (item.amount <= 0)
//                     {
//                         spoiledMaterialItems.Add(item);
//                     }

//                     item.currentInterval = item.spoilageInterval;
//                 }
//             }
//         }

//         foodInventory.RemoveAll(item => item.resourceID == "SPF" || item.resourceID == "WCT");

//         foreach (var item in spoiledFoodItems)
//         {
//             foodInventory.Remove(item);
//         }

//         foreach (var item in spoiledMaterialItems)
//         {
//             materialInventory.Remove(item);
//         }

//         resourcePriorityCalculator.CalculatePriorities();
//     }

//     public Dictionary<string, int> GetAvailableResources()
//     {
//         Dictionary<string, int> availableResources = new Dictionary<string, int>();

//         // Add food resources
//         foreach (var item in foodInventory)
//         {
//             if (!availableResources.ContainsKey(item.resourceID))
//             {
//                 availableResources[item.resourceID] = item.amount;
//             }
//             else
//             {
//                 availableResources[item.resourceID] += item.amount;
//             }
//         }

//         // Add material resources
//         foreach (var item in materialInventory)
//         {
//             if (!availableResources.ContainsKey(item.resourceID))
//             {
//                 availableResources[item.resourceID] = item.amount;
//             }
//             else
//             {
//                 availableResources[item.resourceID] += item.amount;
//             }
//         }

//         return availableResources;
//     }

//     public void IncreaseFoodInventoryCapacity(int amount)
//     {
//         maxFoodInventory += amount;
//         //Debug.Log($"[AIInventoryManager] Food inventory capacity increased by {amount} to {maxFoodInventory}.");
//     }

//     public void IncreaseMaterialInventoryCapacity(int amount)
//     {
//         maxMaterialInventory += amount;
//         //Debug.Log($"[AIInventoryManager] Material inventory capacity increased by {amount} to {maxMaterialInventory}.");
//     }

//     public void DecreaseFoodInventoryCapacity(int amount)
//     {
//         maxFoodInventory = Mathf.Max(0, maxFoodInventory - amount);
//         //Debug.Log($"[AIInventoryManager] Food inventory capacity decreased by {amount} to {maxFoodInventory}.");
//     }

//     public void DecreaseMaterialInventoryCapacity(int amount)
//     {
//         maxMaterialInventory = Mathf.Max(0, maxMaterialInventory - amount);
//         //Debug.Log($"[AIInventoryManager] Material inventory capacity decreased by {amount} to {maxMaterialInventory}.");
//     }

//     public void AddResourceWithSpoilage(string resourceID, int amount, int spoilageInterval, int currentInterval)
//     {
//         Resource aiResource = aiResourceManager.GetAIResourceByID(resourceID);
//         if (aiResource == null)
//         {
//             //Debug.LogWarning($"[AIInventoryManager] Resource with ID {resourceID} not found!");
//             return;
//         }
        
//         // Choose the proper inventory list based on resource type.
//         List<AIInventoryItem> inventory = aiResource.resourceType == ResourceType.Food ? foodInventory : materialInventory;
//         AIInventoryItem item = inventory.Find(i => i.resourceID == resourceID);
//         if (item != null)
//         {
//             item.amount += amount;
//             // Optionally update spoilage details (or keep the existing ones)
//             item.spoilageInterval = spoilageInterval;
//             item.currentInterval = currentInterval;
//         }
//         else if(amount > 0)
//         {
//             inventory.Add(new AIInventoryItem(resourceID, aiResource.resourceName, amount, aiResource.resourceIcon, spoilageInterval, currentInterval));
//         }
//     }

//     public bool HasEnoughResources(List<ResourceRequirement> resourceRequirements)
//     {
//         foreach (var requirement in resourceRequirements)
//         {
//             if (!HasEnoughResource(requirement.resourceID, requirement.amount))
//             {
//                 return false;
//             }
//         }
//         return true;
//     }
//     public void DeductResources(List<ResourceRequirement> resourceRequirements)
//     {
//         foreach (var requirement in resourceRequirements)
//         {
//             RemoveResource(requirement.resourceID, requirement.amount);
//         }
//     }

//     public AIInventoryManagerSaveData SaveState()
//     {
//         AIInventoryManagerSaveData data = new AIInventoryManagerSaveData();
//         data.maxFoodInventory = this.maxFoodInventory;
//         data.maxMaterialInventory = this.maxMaterialInventory;
        
//         // Save food inventory
//         foreach (AIInventoryItem item in foodInventory)
//         {
//             AIInventoryItemSaveData itemData = new AIInventoryItemSaveData
//             {
//                 resourceID = item.resourceID,
//                 resourceName = item.resourceName,
//                 amount = item.amount,
//                 spoilageInterval = item.spoilageInterval,
//                 currentInterval = item.currentInterval
//             };
//             data.savedFoodInventory.Add(itemData);
//         }
        
//         // Save material inventory
//         foreach (AIInventoryItem item in materialInventory)
//         {
//             AIInventoryItemSaveData itemData = new AIInventoryItemSaveData
//             {
//                 resourceID = item.resourceID,
//                 resourceName = item.resourceName,
//                 amount = item.amount,
//                 spoilageInterval = item.spoilageInterval,
//                 currentInterval = item.currentInterval
//             };
//             data.savedMaterialInventory.Add(itemData);
//         }
        
//         return data;
//     }

//     public void LoadState(AIInventoryManagerSaveData data)
//     {
//         if (data == null) return;

//         // Clear current lists
//         foodInventory.Clear();
//         materialInventory.Clear();
        
//         // Restore inventory capacities
//         maxFoodInventory = data.maxFoodInventory;
//         maxMaterialInventory = data.maxMaterialInventory;
        
//         // Rebuild food inventory
//         foreach (AIInventoryItemSaveData savedItem in data.savedFoodInventory)
//         {
//             // Use aiResourceManager to look up the resource and get the icon if desired
//             Resource aiResource = aiResourceManager?.GetAIResourceByID(savedItem.resourceID);
//             Sprite icon = aiResource != null ? aiResource.resourceIcon : null;
            
//             AIInventoryItem newItem = new AIInventoryItem(
//                 savedItem.resourceID,
//                 savedItem.resourceName,
//                 savedItem.amount,
//                 icon,
//                 savedItem.spoilageInterval,
//                 savedItem.currentInterval
//             );
//             foodInventory.Add(newItem);
//         }
        
//         // Rebuild material inventory
//         foreach (AIInventoryItemSaveData savedItem in data.savedMaterialInventory)
//         {
//             Resource aiResource = aiResourceManager?.GetAIResourceByID(savedItem.resourceID);
//             Sprite icon = aiResource != null ? aiResource.resourceIcon : null;
            
//             AIInventoryItem newItem = new AIInventoryItem(
//                 savedItem.resourceID,
//                 savedItem.resourceName,
//                 savedItem.amount,
//                 icon,
//                 savedItem.spoilageInterval,
//                 savedItem.currentInterval
//             );
//             materialInventory.Add(newItem);
//         }
        
//         Debug.Log("[AIInventoryManager] AI inventory restored from save data.");
//     }
// }