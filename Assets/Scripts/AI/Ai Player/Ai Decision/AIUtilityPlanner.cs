// using System.Collections;
// using System.Collections.Generic;
// using System.Linq;
// using UnityEngine;

// public class AIUtilityPlanner : MonoBehaviour
// {
//     private AIPlayer aiPlayer;
//     private AIPlanner aiPlanner;
//     private AIInventoryManager inventoryManager;
//     private AIBuildingManager buildingManager;
//     private AIPopulationManager populationManager;
//     private AIDecisionMaker aiDecisionMaker;
//     public AIResourcePriorityCalculator resourcePriorityCalculator;
//     public CancelProductionPlanEvaluator cancelEvaluator;

//     // Evaluator for crafting plans.
//     [SerializeField] private CraftingPlanEvaluator craftingPlanEvaluator;
//     [SerializeField] private ProductionPlanEvaluator productionPlanEvaluator;
//     [SerializeField] private UnitOrderPlanEvaluator unitOrderPlanEvaluator;

//     // Local list for crafting plans.
//     private List<AIPlan> plannedCraftingPlans = new List<AIPlan>();

//     private void Start()
//     {
//         aiPlayer = GetComponentInParent<AIPlayer>();
//         if (aiPlayer == null)
//             return;

//         aiPlanner = aiPlayer.GetComponentInChildren<AIPlanner>();
//         inventoryManager = aiPlayer.GetComponentInChildren<AIInventoryManager>();
//         buildingManager = aiPlayer.GetComponentInChildren<AIBuildingManager>();
//         populationManager = aiPlayer.GetComponentInChildren<AIPopulationManager>();
//         aiDecisionMaker = aiPlayer.GetComponentInChildren<AIDecisionMaker>();

//         if (aiPlanner == null || inventoryManager == null || buildingManager == null || populationManager == null || aiDecisionMaker == null)
//             return;

//         if (craftingPlanEvaluator == null)
//             craftingPlanEvaluator = GetComponent<CraftingPlanEvaluator>();

//         if (unitOrderPlanEvaluator == null)
//             unitOrderPlanEvaluator = GetComponent<UnitOrderPlanEvaluator>();
//     }

//     public void PlanUtilityActions()
//     {
//         StartCoroutine(PlanCraftingActionsCoroutine());
//         StartCoroutine(PlanStorageActionsCoroutine());
//         StartCoroutine(PlanProductionActionsCoroutine());
//         StartCoroutine(PlanUnitOrderActionsCoroutine());
//     }

//     // ***********************************************
//     // *  PlanCraftingActionsCoroutine (Crafting)    *
//     // ***********************************************
//     public IEnumerator PlanCraftingActionsCoroutine()
//     {
//         plannedCraftingPlans.Clear();
//         List<GameObject> ownedBuildings = buildingManager.GetOwnedBuildings();

//         Dictionary<GameObject, CraftedItem> selectedItemsForBuildings = new Dictionary<GameObject, CraftedItem>();

//         for (int i = 0; i < ownedBuildings.Count; i++)
//         {
//             GameObject building = ownedBuildings[i];
//             if (building == null)
//                 continue;

//             CraftingBuildingControl craftingControl = building.GetComponent<CraftingBuildingControl>();
//             if (craftingControl != null)
//             {
//                 List<CraftedItem> craftableItems = craftingControl.GetCraftableItems();
//                 if (craftableItems != null && craftableItems.Count > 0)
//                 {
//                     // Sort craftable items by priority (highest priority first)
//                     CraftedItem bestItem = null;
//                     int highestPriority = 0;

//                     foreach (CraftedItem item in craftableItems)
//                     {
//                         if (item == null || !item.isUnlocked)
//                             continue;

//                         int itemPriority = Mathf.RoundToInt(craftingPlanEvaluator.EvaluateCraftableItem(item));

//                         // 🔹 **Boost crafting priority based on output resource priorities**
//                         foreach (var output in item.outputResources)
//                         {
//                             float resourcePriority = resourcePriorityCalculator.GetResourcePriority(output.resourceID);

//                             if (resourcePriority > 500)
//                                 itemPriority = Mathf.RoundToInt(itemPriority * 2f); // 🚀 Double priority for extreme demand
//                             else if (resourcePriority > 300)
//                                 itemPriority = Mathf.RoundToInt(itemPriority * 1.5f); // 🔥 Double priority boost
//                             else if (resourcePriority > 200)
//                                 itemPriority = Mathf.RoundToInt(itemPriority * 0.75f); // 🚀 75% boost
//                             else if (resourcePriority > 100)
//                                 itemPriority = Mathf.RoundToInt(itemPriority * 0.5f); // ⚡ 50% boost
//                         }

//                         if (itemPriority > highestPriority)
//                         {
//                             highestPriority = itemPriority;
//                             bestItem = item;
//                         }
//                     }

//                     if (bestItem != null)
//                     {
//                         selectedItemsForBuildings[building] = bestItem;
//                     }
//                 }
//             }
//             if (i % 10 == 0)
//                 yield return null;
//         }

//         // Create crafting plans for selected items
//         foreach (var kvp in selectedItemsForBuildings)
//         {
//             GameObject building = kvp.Key;
//             CraftedItem item = kvp.Value;

//             AIPlan existingPlan = aiPlanner.GetAllPlannedActions()
//                 .FirstOrDefault(p => p.planType == AIPlanType.Crafting &&
//                                     p.craftedItem != null &&
//                                     p.craftedItem.itemName == item.itemName &&
//                                     p.target == building);

//             int itemPriority = Mathf.RoundToInt(craftingPlanEvaluator.EvaluateCraftableItem(item));

//             // 🔹 **Reapply the priority boost here**
//             foreach (var output in item.outputResources)
//             {
//                 float resourcePriority = resourcePriorityCalculator.GetResourcePriority(output.resourceID);

//                 if (resourcePriority > 500)
//                     itemPriority = Mathf.RoundToInt(itemPriority * 2f); 
//                 else if (resourcePriority > 300)
//                     itemPriority = Mathf.RoundToInt(itemPriority * 1.5f); 
//                 else if (resourcePriority > 200)
//                     itemPriority = Mathf.RoundToInt(itemPriority * 1.25f); 
//                 else if (resourcePriority > 100)
//                     itemPriority = Mathf.RoundToInt(itemPriority * 1.1f); 
//             }

//             if (existingPlan != null)
//             {
//                 existingPlan.priority = itemPriority; // Update priority if it already exists
//             }
//             else
//             {
//                 AIPlan craftingPlan = new AIPlan(AIPlanType.Crafting, building, itemPriority);
//                 craftingPlan.craftedItem = item;
//                 plannedCraftingPlans.Add(craftingPlan);
//             }
//         }

//         // Adjust existing crafting plans based on resource availability.
//         List<AIPlan> existingCraftingPlans = aiPlanner.GetAllPlannedActions()
//             .Where(p => p.planType == AIPlanType.Crafting)
//             .ToList();

//         foreach (AIPlan plan in existingCraftingPlans)
//         {
//             if (plan.craftedItem == null)
//                 continue;

//             bool hasAllResources = true;
//             foreach (var req in plan.craftedItem.resourceCost)
//             {
//                 if (!inventoryManager.HasEnoughResource(req.resourceID, req.amount))
//                 {
//                     hasAllResources = false;
//                     resourcePriorityCalculator.IncreaseResourcePriority(req.resourceID, 200f);
//                 }
//             }

//             if (!hasAllResources)
//             {
//                 plan.turnsWithoutResources++;
//                 if (plan.turnsWithoutResources >= 6)
//                 {
//                     aiPlanner.RemovePlan(plan);
//                 }
//                 else
//                 {
//                     int oldPriority = plan.priority;
//                     plan.priority = Mathf.RoundToInt(plan.priority * (1f - 0.2f * plan.turnsWithoutResources));
//                 }
//             }
//             else
//             {
//                 plan.turnsWithoutResources = 0;
//             }
//         }

//         // Order crafting plans by priority and add them to the global AIPlanner.
//         plannedCraftingPlans = plannedCraftingPlans.OrderByDescending(p => p.priority).ToList();
//         foreach (var plan in plannedCraftingPlans)
//         {
//             aiPlanner.AddPlan(plan);
//         }

//         yield return null;
//     }

//     // ***********************************************
//     // *  PlanStorageActionsCoroutine (Storage)      *
//     // ***********************************************
//     public IEnumerator PlanStorageActionsCoroutine()
//     {
//         List<AIPlan> storageInPlans = GetStorageInPlans();
//         List<AIPlan> storageOutPlans = GetStorageOutPlans();

//         foreach (var plan in storageInPlans)
//             aiPlanner.AddPlan(plan);
//         foreach (var plan in storageOutPlans)
//             aiPlanner.AddPlan(plan);

//         yield return null;
//     }

//     private int GetPendingRequirementForResource(string resID)
//     {
//         int total = 0;
//         List<AIPlan> allPlans = aiPlanner.GetAllPlannedActions();
//         foreach (AIPlan plan in allPlans)
//         {
//             if (plan == null)
//                 continue;

//             switch (plan.planType)
//             {
//                 case AIPlanType.Building:
//                     if (plan.selectedBuilding != null && plan.selectedBuilding.requiredResources != null)
//                     {
//                         foreach (ResourceRequirement req in plan.selectedBuilding.requiredResources)
//                         {
//                             if (req.resourceID == resID)
//                                 total += req.amount;
//                         }
//                     }
//                     break;
//                 case AIPlanType.Research:
//                     if (plan.selectedTechnology != null && plan.selectedTechnology.resourceRequirements != null)
//                     {
//                         foreach (ResourceRequirement req in plan.selectedTechnology.resourceRequirements)
//                         {
//                             if (req.resourceID == resID)
//                                 total += req.amount;
//                         }
//                     }
//                     break;
//                 case AIPlanType.Repair:
//                     {
//                         AIBuildingControl bc = plan.target.GetComponent<AIBuildingControl>();
//                         if (bc != null)
//                         {
//                             List<ResourceRequirement> repairReqs = bc.GetRepairCosts();
//                             foreach (ResourceRequirement req in repairReqs)
//                             {
//                                 if (req.resourceID == resID)
//                                     total += req.amount;
//                             }
//                         }
//                     }
//                     break;
//                 case AIPlanType.Crafting:
//                     if (plan.craftedItem != null && plan.craftedItem.resourceCost != null)
//                     {
//                         foreach (ResourceRequirement req in plan.craftedItem.resourceCost)
//                         {
//                             if (req.resourceID == resID)
//                                 total += req.amount;
//                         }
//                     }
//                     break;
//             }
//         }
//         return total;
//     }

//     // Updated GetStorageInPlans: Now the desired amount includes the pending requirement.
//     private List<AIPlan> GetStorageInPlans()
//     {
//         var storageInPlans = new List<AIPlan>();
//         var pendingRequirements = GetAllPendingRequirements();
//         var ownedBuildings = buildingManager.GetOwnedBuildings();

//         foreach (var building in ownedBuildings)
//         {
//             if (building == null) continue;

//             var storageControl = building.GetComponent<StorageBuildingControl>();
//             if (storageControl == null) continue;

//             var plan = new AIPlan(AIPlanType.StorageIn, building, 0);
//             plan.storageEntries = new List<StorageResourceEntry>();
//             int aggregatedPriority = 0;

//             foreach (var resID in storageControl.supportedResourceIDs)
//             {
//                 int invAmount = inventoryManager.GetResourceAmount(resID);
//                 var res = ResourceManager.Instance.GetResourceByID(resID);
//                 if (res == null) continue;

//                 int baselineDesired = res.resourceType == ResourceType.Food
//                     ? populationManager.GetCurrentPopulation() * populationManager.foodConsumptionPerPerson * 3
//                     : Mathf.FloorToInt(inventoryManager.maxMaterialInventory * (resourcePriorityCalculator.GetResourcePriority(resID) / 100f) / 2);

//                 int pendingReq = pendingRequirements.ContainsKey(resID) ? pendingRequirements[resID] : 0;
//                 int desiredAmount = baselineDesired - pendingReq;

//                 if (invAmount > desiredAmount)
//                 {
//                     int surplus = invAmount - desiredAmount;
//                     int availableSpace = storageControl.GetAvailableSpaceForResource(resID);
//                     if (availableSpace > 0)
//                     {
//                         int amountToStore = Mathf.Min(surplus, availableSpace);
//                         int entryPriority = Mathf.RoundToInt(resourcePriorityCalculator.GetResourcePriority(resID) * 10f);
//                         plan.storageEntries.Add(new StorageResourceEntry(resID, amountToStore));
//                         aggregatedPriority += entryPriority;
//                     }
//                 }
//             }

//             if (plan.storageEntries.Count > 0)
//             {
//                 plan.priority = aggregatedPriority;
//                 storageInPlans.Add(plan);
//             }
//         }

//         return storageInPlans;
//     }

//     // Updated GetStorageOutPlans: The desired amount now factors in pending resource needs.
//     private List<AIPlan> GetStorageOutPlans()
//     {
//         var storageOutPlans = new List<AIPlan>();
//         var pendingRequirements = GetAllPendingRequirements();
//         var ownedBuildings = buildingManager.GetOwnedBuildings();

//         foreach (var building in ownedBuildings)
//         {
//             if (building == null) continue;

//             var storageControl = building.GetComponent<StorageBuildingControl>();
//             if (storageControl == null) continue;

//             var plan = new AIPlan(AIPlanType.StorageOut, building, 0);
//             plan.storageEntries = new List<StorageResourceEntry>();
//             int aggregatedPriority = 0;

//             foreach (var resID in storageControl.supportedResourceIDs)
//             {
//                 int invAmount = inventoryManager.GetResourceAmount(resID);
//                 var res = ResourceManager.Instance.GetResourceByID(resID);
//                 if (res == null) continue;

//                 int baselineDesired = res.resourceType == ResourceType.Food
//                     ? populationManager.GetCurrentPopulation() * populationManager.foodConsumptionPerPerson * 3
//                     : Mathf.FloorToInt(inventoryManager.maxMaterialInventory * (resourcePriorityCalculator.GetResourcePriority(resID) / 100f) / 2);

//                 int pendingReq = pendingRequirements.ContainsKey(resID) ? pendingRequirements[resID] : 0;
//                 int desiredAmount = baselineDesired + pendingReq;

//                 if (invAmount < desiredAmount)
//                 {
//                     int deficiency = desiredAmount - invAmount;
//                     var storedItem = storageControl.storedResources.FirstOrDefault(item => item.resourceID == resID);
//                     int storedAmount = storedItem?.amount ?? 0;
//                     if (storedAmount > 0)
//                     {
//                         int amountToRetrieve = Mathf.Min(deficiency, storedAmount);
//                         int entryPriority = Mathf.RoundToInt(resourcePriorityCalculator.GetResourcePriority(resID) * 10f);
//                         plan.storageEntries.Add(new StorageResourceEntry(resID, amountToRetrieve));
//                         aggregatedPriority += entryPriority;
//                     }
//                 }
//             }

//             if (plan.storageEntries.Count > 0)
//             {
//                 plan.priority = aggregatedPriority;
//                 storageOutPlans.Add(plan);
//             }
//         }

//         return storageOutPlans;
//     }

//     // ***********************************************
//     // *  PlanProductionActionsCoroutine (Production)      *
//     // ***********************************************

//     public IEnumerator PlanProductionActionsCoroutine()
//     {
//         List<AIPlan> plannedProductionPlans = new List<AIPlan>();
//         const int resumeThreshold = 150;

//         if (buildingManager == null || populationManager == null || aiPlanner == null || inventoryManager == null || resourcePriorityCalculator == null)
//         {
//             Debug.LogError("One or more required AI components are missing!");
//             yield break;
//         }

//         var ownedBuildings = buildingManager.GetOwnedBuildings();
//         var selectedItemsForBuildings = new Dictionary<GameObject, ProductionItem>();
//         Dictionary<string, int> resourceProductionCount = new Dictionary<string, int>();

//         for (int i = 0; i < ownedBuildings.Count; i++)
//         {
//             GameObject building = ownedBuildings[i];
//             if (building == null || !building.activeInHierarchy) continue;

//             var productionControl = building.GetComponent<ProductionBuildingControl>();
//             if (productionControl == null) continue;

//             productionControl.UpdateUnlockedProductionItems();
//             var availableItems = productionControl.GetAvailableProductionItems();
//             if (availableItems == null || availableItems.Count == 0) continue;

//             if (productionControl.totalStoredAmount > 0)
//             {
//                 int collectPriority = productionControl.totalStoredAmount;
//                 // Directly create and add a new collection plan for this building.
//                 var collectPlan = new AIPlan(AIPlanType.CollectProducedGoods, building, collectPriority);
//                 plannedProductionPlans.Add(collectPlan);
//             }

//             ProductionItem bestItem = null;
//             int highestPriority = 0;

//             foreach (var item in availableItems)
//             {
//                 if (item == null || !item.isUnlocked) continue;

//                 // Skip if overproduced or resource-limited
//                 if (IsItemOverproduced(item) || IsItemResourceLimited(item, resourceProductionCount)) continue;

//                 int basePriority = productionPlanEvaluator.EvaluateProductionItem(item);
//                 float priorityBoost = CalculatePriorityBoost(item);

//                 int adjustedPriority = Mathf.RoundToInt(basePriority * priorityBoost);
//                 if (adjustedPriority > highestPriority)
//                 {
//                     highestPriority = adjustedPriority;
//                     bestItem = item;
//                 }
//             }

//             if (bestItem != null)
//             {
//                 selectedItemsForBuildings[building] = bestItem;
//                 RegisterItemProduction(bestItem, resourceProductionCount);
//             }

//             if (i % 10 == 0)
//                 yield return null;
//         }

//         // Process Production Plans
//         foreach (var kvp in selectedItemsForBuildings)
//         {
//             GameObject building = kvp.Key;
//             ProductionItem bestItem = kvp.Value;

//             var productionControl = building?.GetComponent<ProductionBuildingControl>();
//             if (productionControl == null) continue;

//             var currentItem = productionControl.CurrentProductionItem;

//             if (currentItem != null)
//             {
//                 int evaluatedPriority = productionPlanEvaluator.EvaluateProductionItem(currentItem);
//                 bool canResumeResourcesAvailable = HasResourcesForItem(currentItem);

//                 // Resume Logic
//                 if (!productionControl.IsProducing() && evaluatedPriority >= resumeThreshold && canResumeResourcesAvailable)
//                 {
//                     var resumePlan = new AIPlan(AIPlanType.ResumeProduction, building, evaluatedPriority + 50)
//                     {
//                         productionItem = currentItem
//                     };
//                     plannedProductionPlans.Add(resumePlan);
//                 }
//                 // Cancel Logic using the CancelProductionPlanEvaluator:
//                 else
//                 {
//                     int reservedPopulation = aiDecisionMaker.GetReservedPopulation();
//                     int cancelScore = cancelEvaluator.EvaluateCancelProductionPlan(currentItem, productionControl, reservedPopulation);
//                     // If the cancel score meets our threshold (e.g., >= 100), create a cancel plan.
//                     if (cancelScore >= 100)
//                     {
//                         var cancelPlan = new AIPlan(AIPlanType.CancelProduction, building, cancelScore)
//                         {
//                             productionItem = currentItem
//                         };
//                         plannedProductionPlans.Add(cancelPlan);
//                     }
//                 }
//             }

//             // Start New Production
//             if (HasResourcesForItem(bestItem))
//             {
//                 var productionPlan = new AIPlan(AIPlanType.StartProduction, building, productionPlanEvaluator.EvaluateProductionItem(bestItem))
//                 {
//                     productionItem = bestItem
//                 };
//                 plannedProductionPlans.Add(productionPlan);
//             }
//         }

//         // Clean Up Old Plans
//         var existingPlans = aiPlanner.GetAllPlannedActions()
//             .Where(p => p != null &&
//                         (p.planType == AIPlanType.StartProduction ||
//                         p.planType == AIPlanType.ResumeProduction ||
//                         p.planType == AIPlanType.CancelProduction))
//             .ToList();

//         foreach (var plan in existingPlans)
//         {
//             if (!plannedProductionPlans.Contains(plan))
//             {
//                 aiPlanner.RemovePlan(plan);
//             }
//         }

//         // Finalize and add plans
//         plannedProductionPlans = plannedProductionPlans.OrderByDescending(p => p.priority).ToList();
//         foreach (var plan in plannedProductionPlans)
//         {
//             aiPlanner.AddPlan(plan);
//         }

//         yield return null;
//     }

//     private float CalculatePriorityBoost(ProductionItem item)
//     {
//         float priorityBoost = 1f;
//         foreach (var output in item.outputResources)
//         {
//             float resourcePriority = resourcePriorityCalculator.GetResourcePriority(output.resourceID);
//             if (resourcePriority > 500) priorityBoost *= 4f;
//             else if (resourcePriority > 300) priorityBoost *= 2f;
//             else if (resourcePriority > 150) priorityBoost *= 1.5f;
//         }
//         return priorityBoost;
//     }

//     private bool HasResourcesForItem(ProductionItem item)
//     {
//         // Check if resourceCost is null.
//         if (item == null || item.resourceCost == null)
//             return false;

//         bool hasAllResources = true;
//         foreach (var req in item.resourceCost)
//         {
//             if (req == null)
//                 continue;

//             // If the resource is insufficient, boost its priority and mark the check as failed.
//             if (!inventoryManager.HasEnoughResource(req.resourceID, req.amount))
//             {
//                 resourcePriorityCalculator.IncreaseResourcePriority(req.resourceID, 200f);
//                 hasAllResources = false;
//             }
//         }
//         return hasAllResources;
//     }

//     private bool IsItemOverproduced(ProductionItem item)
//     {
//         if (item == null || item.outputResources == null)
//             return false;

//         foreach (var output in item.outputResources)
//         {
//             if (output == null) continue;

//             int currentSupply = inventoryManager.GetResourceAmount(output.resourceID);
//             int threshold = CalculateSupplyThreshold(resourcePriorityCalculator.GetResourcePriority(output.resourceID));

//             if (currentSupply >= threshold)
//                 return true;
//         }
//         return false;
//     }

//     private int CalculateSupplyThreshold(float priority)
//     {
//         if (priority > 500) return 100;
//         if (priority > 300) return 200;
//         if (priority > 150) return 400;
//         return 600;
//     }

//     private void RegisterItemProduction(ProductionItem item, Dictionary<string, int> count)
//     {
//         if (item?.resourceCost == null) return;

//         foreach (var req in item.resourceCost)
//         {
//             if (req == null) continue;

//             if (!count.ContainsKey(req.resourceID))
//                 count[req.resourceID] = 0;
            
//             count[req.resourceID]++;
//         }
//     }

//     private bool IsItemResourceLimited(ProductionItem item, Dictionary<string, int> count)
//     {
//         if (item?.resourceCost == null)
//             return false;

//         foreach (var req in item.resourceCost)
//         {
//             if (req == null) continue;

//             int available = inventoryManager.GetResourceAmount(req.resourceID);
//             if (available < req.amount * (count.ContainsKey(req.resourceID) ? count[req.resourceID] + 1 : 1))
//                 return true;
//         }
//         return false;
//     }

//     private Dictionary<string, int> GetAllPendingRequirements()
//     {
//         var pendingRequirements = new Dictionary<string, int>();
//         List<AIPlan> allPlans = aiPlanner.GetAllPlannedActions();

//         foreach (AIPlan plan in allPlans)
//         {
//             if (plan == null)
//                 continue;

//             List<ResourceRequirement> requirements = plan.planType switch
//             {
//                 AIPlanType.Building => plan.selectedBuilding?.requiredResources,
//                 AIPlanType.Research => plan.selectedTechnology?.resourceRequirements,
//                 AIPlanType.Crafting => plan.craftedItem?.resourceCost,
//                 AIPlanType.Repair => plan.target?.GetComponent<AIBuildingControl>()?.GetRepairCosts(),
//                 _ => null
//             };

//             if (requirements == null) continue;

//             foreach (var req in requirements)
//             {
//                 if (req == null) continue;
//                 if (!pendingRequirements.ContainsKey(req.resourceID))
//                     pendingRequirements[req.resourceID] = 0;

//                 pendingRequirements[req.resourceID] += req.amount;
//             }
//         }

//         return pendingRequirements;
//     }

//     // ***********************************************
//     // *  PlanUnitOrderActionsCoroutine (Unit Orders) *
//     // ***********************************************
//     public IEnumerator PlanUnitOrderActionsCoroutine()
//     {
//         List<AIPlan> plannedUnitOrderPlans = new List<AIPlan>();
//         const int orderPriorityThreshold = 50; // Adjust this threshold as needed.

//         // Get all owned buildings.
//         var ownedBuildings = buildingManager.GetOwnedBuildings();
//         for (int i = 0; i < ownedBuildings.Count; i++)
//         {
//             GameObject building = ownedBuildings[i];
//             if (building == null || !building.activeInHierarchy)
//                 continue;

//             // Check if this building can order units by verifying it has a KineticWarfareControl.
//             KineticWarfareControl kwControl = building.GetComponent<KineticWarfareControl>();
//             if (kwControl == null)
//                 continue;

//             // Loop through all trainable militia units for this building.
//             foreach (MilitiaUnit unit in kwControl.trainableUnits)
//             {
//                 if (unit == null)
//                     continue;

//                 // First pass: evaluate the unit order with a base multiplier of 1.
//                 int baseEvaluation = unitOrderPlanEvaluator.EvaluateUnitOrderPlan(unit, 1);
//                 // Calculate the multiplier based on evaluation, food/water availability, and militiaGroupLimit.
//                 int multiplier = CalculateMultiplier(kwControl, unit, baseEvaluation);
//                 // Recalculate the evaluation priority using the new multiplier.
//                 int evaluationPriority = unitOrderPlanEvaluator.EvaluateUnitOrderPlan(unit, multiplier);

//                 if (evaluationPriority >= orderPriorityThreshold)
//                 {
//                     // Create a new AIPlan for ordering this unit.
//                     AIPlan unitOrderPlan = new AIPlan(AIPlanType.OrderUnit, building, evaluationPriority)
//                     {
//                         unitToOrder = unit,          // Field added to AIPlan for unit orders.
//                         orderMultiplier = multiplier // Field added to AIPlan for unit orders.
//                     };
//                     plannedUnitOrderPlans.Add(unitOrderPlan);
//                 }
//             }
//             if (i % 10 == 0)
//                 yield return null;
//         }

//         // Remove any existing unit order plans that are no longer valid.
//         var existingUnitOrderPlans = aiPlanner.GetAllPlannedActions()
//             .Where(p => p != null && p.planType == AIPlanType.OrderUnit)
//             .ToList();
//         foreach (var plan in existingUnitOrderPlans)
//         {
//             if (!plannedUnitOrderPlans.Contains(plan))
//                 aiPlanner.RemovePlan(plan);
//         }

//         // Finalize and add the new unit order plans.
//         plannedUnitOrderPlans = plannedUnitOrderPlans.OrderByDescending(p => p.priority).ToList();
//         foreach (var plan in plannedUnitOrderPlans)
//         {
//             aiPlanner.AddPlan(plan);
//         }
//         yield return null;
//     }

//     private int CalculateMultiplier(KineticWarfareControl kwControl, MilitiaUnit unit, int baseEval)
//     {
//         // Use the base evaluation to generate a starting multiplier.
//         // For example, every 60 points in baseEval adds one training slot.
//         int baseMultiplier = Mathf.Max(1, baseEval / 60);

//         // Adjust for food and water availability.
//         int currentPopulation = populationManager.GetCurrentPopulation();
//         int foodPerTurn = currentPopulation * populationManager.foodConsumptionPerPerson;
//         int waterPerTurn = currentPopulation * populationManager.waterConsumptionPerPerson;
//         int totalFood = inventoryManager.GetTotalNonWaterFoodAmount();
//         int totalWater = inventoryManager.GetResourceAmount("WFR"); // Fresh Water

//         // Define thresholds (e.g., three turns' worth).
//         int foodThreshold = foodPerTurn * 3;
//         int waterThreshold = waterPerTurn * 3;
//         int food5Threshold = foodPerTurn * 5;
//         int water5Threshold = waterPerTurn * 5;

//         int adjustment = 0;
//         if (totalFood > foodThreshold && totalWater > waterThreshold)
//         {
//             // Surplus: add a bonus multiplier.
//             adjustment = 1;
//         }
//         else if (totalFood > food5Threshold && totalWater > water5Threshold)
//         {
//             // Shortage: reduce the multiplier.
//             adjustment = 2;
//         }
//         else if (totalFood < foodPerTurn || totalWater < waterPerTurn)
//         {
//             // Shortage: reduce the multiplier.
//             adjustment = -1;
//         }

//         // Now, check if the AI can afford the unit's upkeep cost.
//         // For each upkeep resource, calculate the total required for the group (using group.totalUnits)
//         // and subtract 1 from the multiplier for each resource that is in deficit.
//         int upkeepPenalty = 0;
//         if (unit.upkeepCost != null)
//         {
//             foreach (ResourceRequirement req in unit.upkeepCost)
//             {
//                 int requiredUpkeep = req.amount * baseMultiplier;
//                 int available = inventoryManager.GetResourceAmount(req.resourceID);
//                 if (available < requiredUpkeep)
//                 {
//                     upkeepPenalty++;
//                     Debug.LogWarning($"[CalculateMultiplier] Upkeep deficit for {req.resourceID}: Required {requiredUpkeep}, Available {available}");
//                 }
//             }
//         }

//         // Calculate the final multiplier.
//         int calculatedMultiplier = baseMultiplier + adjustment - upkeepPenalty;
//         calculatedMultiplier = Mathf.Max(calculatedMultiplier, 1);

//         // Also, do not exceed the unit's maxMultiplier and the building's militiaGroupLimit.
//         calculatedMultiplier = Mathf.Clamp(calculatedMultiplier, 1, Mathf.Min(unit.maxMultiplier, kwControl.militiaGroupLimit));
        
//         return calculatedMultiplier;
//     }
// }