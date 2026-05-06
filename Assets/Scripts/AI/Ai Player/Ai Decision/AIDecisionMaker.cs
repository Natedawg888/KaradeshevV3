// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
// using System.Linq;

// public class AIDecisionMaker : MonoBehaviour
// {
//     private AIPlanner aiPlanner;
//     private AITileDiscoveryManager discoveryManager;
//     private AIGatheringManager gatheringManager;
//     private AIPopulationManager populationManager;
//     private AIPopulationIncreasePlan populationIncreasePlan;
//     private AIInventoryManager inventoryManager;
//     private AIBuildingManager aiBuildingManager;
//     private AIBuildingPlacement aiBuildingPlacement;
//     private AIResourcePriorityCalculator resourcePriorityCalculator;
//     private AITechnologyManager technologyManager;

//     private int reservedPopulation;

//     private void Awake()
//     {
//         Transform aiPlayer = transform.parent;
//         if (aiPlayer == null)
//         {
//             // If there is no parent AIPlayer, disable this component.
//             enabled = false;
//             return;
//         }

//         aiPlanner = aiPlayer.GetComponentInChildren<AIPlanner>();
//         discoveryManager = aiPlayer.GetComponentInChildren<AITileDiscoveryManager>();
//         gatheringManager = aiPlayer.GetComponentInChildren<AIGatheringManager>();
//         populationManager = aiPlayer.GetComponentInChildren<AIPopulationManager>();
//         populationIncreasePlan = aiPlayer.GetComponentInChildren<AIPopulationIncreasePlan>();
//         inventoryManager = aiPlayer.GetComponentInChildren<AIInventoryManager>();
//         aiBuildingManager = aiPlayer.GetComponentInChildren<AIBuildingManager>();
//         aiBuildingPlacement = aiPlayer.GetComponentInChildren<AIBuildingPlacement>();
//         resourcePriorityCalculator = aiPlayer.GetComponentInChildren<AIResourcePriorityCalculator>();
//         technologyManager = aiPlayer.GetComponentInChildren<AITechnologyManager>();
//     }

//     public void ExecuteTurn() 
//     {
//         StartCoroutine(ExecuteTurnCoroutine());
//     }

//     public IEnumerator ExecuteTurnCoroutine()
//     {
//         if (aiPlanner == null || populationManager == null || technologyManager == null)
//             yield break;

//         // Clean up and sort plans.
//         aiPlanner.CleanInvalidPlans();
//         List<AIPlan> plans = aiPlanner.GetAllPlannedActions();
//         plans = plans.OrderByDescending(p => p.priority).ToList();

//         // Calculate reserved population for high-priority plans.
//         int reservedPop = 0;
//         for (int i = 0; i < plans.Count; i++)
//         {
//             AIPlan p = plans[i];
//             if (p == null || p.target == null || !p.target)
//                 continue;
//             if (p.priority > 300)
//             {
//                 int req = GetPlanPopulationRequirement(p);
//                 reservedPop = Mathf.Max(reservedPop, req);
//             }
//             if (i % 10 == 0)
//                 yield return null; // Yield every 10 iterations.
//         }

//         reservedPopulation = reservedPop;

//         // Calculate reserved resources for high-priority plans.
//         Dictionary<string, int> reservedResources = new Dictionary<string, int>();
//         foreach (AIPlan p in plans)
//         {
//             if (p == null || p.target == null || !p.target)
//                 continue;
//             if (p.priority > 300)
//             {
//                 List<ResourceRequirement> reqs = GetPlanResourceRequirements(p);
//                 foreach (var req in reqs)
//                 {
//                     if (!reservedResources.ContainsKey(req.resourceID))
//                         reservedResources[req.resourceID] = req.amount;
//                     else
//                         reservedResources[req.resourceID] = Mathf.Max(reservedResources[req.resourceID], req.amount);
//                 }
//             }
//         }
//         yield return null;

//         // Get available population.
//         int availablePop = populationManager.GetAvailablePopulation();
//         if (availablePop < reservedPop)
//         {
//             reservedPop = 0; // Skip reservation if available population is too low.
//         }
        
//         List<AIPlan> executedPlans = new List<AIPlan>();

//         // Process each plan in batches.
//         for (int i = 0; i < plans.Count; i++)
//         {
//             AIPlan plan = plans[i];
//             if (plan == null)
//                 continue;
//             // For non-PopIncrease plans, ensure a valid target.
//             if (plan.planType != AIPlanType.PopIncrease && (plan.target == null || !plan.target))
//                 continue;
//             if (availablePop <= 0)
//                 break;

//             int planRequiredPop = GetPlanPopulationRequirement(plan);

//             // Skip lower-priority plans if executing them would drop available population below the reserved population.
//             if (reservedPop > 0 && plan.priority < 100 && (availablePop - planRequiredPop) < reservedPop)
//             {
//                 continue;
//             }

//             // Resource Reservation Check for plans (only apply to lower-priority tasks).
//             if (plan.priority < 100)
//             {
//                 List<ResourceRequirement> planReqs = GetPlanResourceRequirements(plan);
//                 bool skipDueToResources = false;
//                 foreach (var req in planReqs)
//                 {
//                     int availableAmount = inventoryManager.GetResourceAmount(req.resourceID);
//                     int reservedAmount = reservedResources.ContainsKey(req.resourceID) ? reservedResources[req.resourceID] : 0;
//                     if (availableAmount - req.amount < reservedAmount)
//                     {
//                         skipDueToResources = true;
//                         break;
//                     }
//                 }
//                 if (skipDueToResources)
//                     continue;
//             }

//             // Execute the plan based on its type.
//             switch (plan.planType)
//             {
//                 case AIPlanType.Gathering:
//                     {
//                         EnvironmentControl env = plan.target.GetComponent<EnvironmentControl>();
//                         if (env == null)
//                         {
//                             aiPlanner.RemovePlan(plan);
//                             continue;
//                         }
//                         if (env.isBeingAIGathered)
//                             continue;
//                         if (availablePop >= env.requiredGatheringPopulation)
//                         {
//                             gatheringManager.AttemptGathering(plan);
//                             availablePop -= env.requiredGatheringPopulation;
//                             executedPlans.Add(plan);
//                         }
//                     }
//                     break;

//                 case AIPlanType.Discovery:
//                     {
//                         EnvironmentControl env = plan.target.GetComponent<EnvironmentControl>();
//                         if (env == null || discoveryManager.GetDiscoveredTiles().Contains(plan.target))
//                             continue;
//                         if (availablePop >= env.requiredPopulation)
//                         {
//                             discoveryManager.ExecuteDiscovery(plan.target);
//                             availablePop -= env.requiredPopulation;
//                             executedPlans.Add(plan);
//                         }
//                     }
//                     break;

//                 case AIPlanType.PopIncrease:
//                     {
//                         int surplusFood = inventoryManager.GetTotalNonWaterFoodAmount() -
//                                            (populationManager.GetCurrentPopulation() * populationManager.foodConsumptionPerPerson * 2);
//                         int foodPerOrder = populationIncreasePlan.foodForPopulationIncrease;
//                         int maxOrders = Mathf.Min(surplusFood / foodPerOrder, availablePop, populationManager.maxPopulation - populationManager.GetCurrentPopulation());
//                         if (maxOrders > 0)
//                         {
//                             for (int j = 0; j < maxOrders; j++)
//                             {
//                                 populationIncreasePlan.AttemptPopulationIncrease();
//                                 availablePop--;
//                             }
//                             executedPlans.Add(plan);
//                         }
//                     }
//                     break;

//                 case AIPlanType.Building:
//                     {
//                         if (!HasRequiredResources(plan.selectedBuilding))
//                         {
//                             IncreaseResourcePriorities(plan.selectedBuilding);
//                             continue;
//                         }
//                         if (!HasRequiredPopulation(plan.selectedBuilding))
//                             continue;

//                         aiBuildingPlacement.PlaceBuilding(plan);
//                         availablePop -= plan.selectedBuilding.requiredPopulation;
//                         executedPlans.Add(plan);
//                     }
//                     break;

//                 case AIPlanType.Research:
//                     {
//                         AIBuildingControl researchBuilding = plan.target.GetComponent<AIBuildingControl>();
//                         if (researchBuilding == null)
//                         {
//                             executedPlans.Add(plan);
//                             continue;
//                         }
//                         List<Technology> availableTechs = technologyManager.GetTechnologiesResearchableOnBuilding(researchBuilding.buildingID);
//                         if (availableTechs.Count == 0)
//                         {
//                             executedPlans.Add(plan);
//                             continue;
//                         }
//                         // For each technology candidate (ordered by level required):
//                         foreach (Technology tech in availableTechs.OrderByDescending(t => t.levelRequired))
//                         {
//                             if (technologyManager.GetActiveResearchCount() >= technologyManager.GetMaxConcurrentResearch())
//                                 break;
//                             if (availablePop < tech.populationRequired)
//                                 continue;
                            
//                             // Before starting research, check resource reservation for research.
//                             List<ResourceRequirement> researchReqs = GetPlanResourceRequirements(plan);
//                             bool skipDueToResearchResources = false;
//                             foreach (var req in researchReqs)
//                             {
//                                 int availableAmount = inventoryManager.GetResourceAmount(req.resourceID);
//                                 int reservedAmount = reservedResources.ContainsKey(req.resourceID) ? reservedResources[req.resourceID] : 0;
//                                 if (availableAmount - req.amount < reservedAmount)
//                                 {
//                                     skipDueToResearchResources = true;
//                                     break;
//                                 }
//                             }
//                             if (skipDueToResearchResources)
//                                 continue;

//                             if (technologyManager.StartResearch(tech))
//                             {
//                                 availablePop -= tech.populationRequired;
//                                 executedPlans.Add(plan);
//                             }
//                             else
//                             {
//                                 IncreaseResourcePriorities(tech);
//                             }
//                         }
//                     }
//                     break;

//                 case AIPlanType.Repair:
//                     {
//                         AIBuildingControl building = plan.target.GetComponent<AIBuildingControl>();
//                         if (building == null || building.health >= (int)building.healthSlider.maxValue || building.CurrentState == BuildingState.Destroyed)
//                         {
//                             executedPlans.Add(plan);
//                             continue;
//                         }
//                         int requiredPop = building.peopleRequiredForRepair;
//                         List<ResourceRequirement> repairCosts = building.GetRepairCosts();
//                         if (availablePop >= requiredPop && HasRequiredResourcesForRepair(repairCosts))
//                         {
//                             DeductResourcesForRepair(repairCosts);
//                             building.aiOwner.GetComponentInChildren<AIPopulationManager>().UsePopulation(requiredPop);
//                             int maxHealth = (int)building.healthSlider.maxValue;
//                             // Determine current health ratio
//                             float healthRatio = (building.healthSlider != null && building.healthSlider.maxValue > 0) 
//                                 ? (float)building.health / building.healthSlider.maxValue 
//                                 : 1f;
//                             float repairFraction = (healthRatio < 0.33f) ? 1f 
//                               : (healthRatio < 0.66f ? 0.5f : 0.1f);
//                                 int repairAmount = Mathf.CeilToInt(maxHealth * repairFraction);
//                             building.health += repairAmount;
//                                 if (building.health > maxHealth)
//                                     building.health = maxHealth;
//                                 building.UpdateHealthSlider(building.health);
//                                 if (building.health >= maxHealth)
//                                     building.SetState(BuildingState.Normal);
//                                 building.isBeingRepaired = true;
//                                 executedPlans.Add(plan);
//                         }
//                         else
//                         {
//                             foreach (var req in repairCosts)
//                             {
//                                 resourcePriorityCalculator.IncreaseResourcePriority(req.resourceID, 200f);
//                             }
//                         }
//                     }
//                     break;

//                 case AIPlanType.Crafting:
//                     {
//                         CraftingBuildingControl craftingControl = plan.target.GetComponent<CraftingBuildingControl>();
//                         if (craftingControl == null)
//                         {
//                             executedPlans.Add(plan);
//                             continue;
//                         }
//                         bool hasEnough = true;
//                         foreach (var req in plan.craftedItem.resourceCost)
//                         {
//                             if (!inventoryManager.HasEnoughResource(req.resourceID, req.amount))
//                             {
//                                 hasEnough = false;
//                                 resourcePriorityCalculator.IncreaseResourcePriority(req.resourceID, 200f);
//                             }
//                         }
//                         if (!hasEnough)
//                             continue;
//                         if (craftingControl.StartCrafting(plan.craftedItem))
//                         {
//                             executedPlans.Add(plan);
//                         }
//                     }
//                     break;

//                     // **** Handle StorageIn plans ****
//             case AIPlanType.StorageIn:
//                 {
//                     StorageBuildingControl storageControl = plan.target.GetComponent<StorageBuildingControl>();
//                     if (storageControl == null)
//                     {
//                         aiPlanner.RemovePlan(plan);
//                         continue;
//                     }
//                     foreach (StorageResourceEntry entry in plan.storageEntries)
//                     {
//                         int invAmount = inventoryManager.GetResourceAmount(entry.resourceID);
//                         int availableSpace = storageControl.GetAvailableSpaceForResource(entry.resourceID);
//                         int amountToStore = Mathf.Min(entry.amount, invAmount, availableSpace);
//                         if (amountToStore > 0)
//                         {
//                             // Remove from global inventory and add to the storage building.
//                             inventoryManager.RemoveResource(entry.resourceID, amountToStore);
//                             storageControl.AddResource(entry.resourceID, amountToStore);
//                         }
//                     }
//                     executedPlans.Add(plan);
//                 }
//                 break;

//             // **** Handle StorageOut plans ****
//             case AIPlanType.StorageOut:
//                 {
//                     StorageBuildingControl storageControl = plan.target.GetComponent<StorageBuildingControl>();
//                     if (storageControl == null)
//                     {
//                         aiPlanner.RemovePlan(plan);
//                         continue;
//                     }
//                     foreach (StorageResourceEntry entry in plan.storageEntries)
//                     {
//                         StorageItem storedItem = storageControl.storedResources.FirstOrDefault(item => item.resourceID == entry.resourceID);
//                         if (storedItem != null)
//                         {
//                             int amountAvailable = storedItem.amount;
//                             int amountToRetrieve = Mathf.Min(entry.amount, amountAvailable);
//                             if (amountToRetrieve > 0)
//                             {
//                                 // Add to global inventory and remove from the storage building.
//                                 inventoryManager.AddResource(entry.resourceID, amountToRetrieve);
//                                 storageControl.RemoveResource(entry.resourceID, amountToRetrieve);
//                             }
//                         }
//                     }
//                     executedPlans.Add(plan);
//                 }
//                 break;
            
//             case AIPlanType.StartProduction:
//                 {
//                     ProductionBuildingControl productionControl = plan.target.GetComponent<ProductionBuildingControl>();
//                     if (productionControl != null)
//                     {
//                         // First check if there is enough population.
//                         if (populationManager.GetAvailablePopulation() < plan.productionItem.requiredPopulation)
//                         {
//                             // Not enough population—skip this plan.
//                             continue;
//                         }
//                         else if (productionControl.SetProductionItem(plan.productionItem))
//                         {
//                             executedPlans.Add(plan);
//                         }
//                         else
//                         {
//                             // Debug.LogWarning("Failed to start production on building " + plan.target.name);
//                             // Increase priority for each resource that is lacking.
//                             if (plan.productionItem.costsResourcesToProduce)
//                             {
//                                 foreach (var req in plan.productionItem.resourceCost)
//                                 {
//                                     // Check if the AI's inventory doesn't have enough of this resource.
//                                     if (!inventoryManager.HasEnoughResource(req.resourceID, req.amount))
//                                     {
//                                         resourcePriorityCalculator.IncreaseResourcePriority(req.resourceID, 200f);
//                                     }
//                                 }
//                             }
//                         }
//                     }
//                 }
//                 break;

//             // Handle resuming production.
//             case AIPlanType.ResumeProduction:
//                 {
//                     ProductionBuildingControl productionControl = plan.target.GetComponent<ProductionBuildingControl>();
//                     if (productionControl != null && productionControl.IsPaused())
//                     {
//                         bool canResume = true;
//                         // Check if the current production item requires resources to resume.
//                         if (productionControl.CurrentProductionItem != null && productionControl.CurrentProductionItem.costsResourcesToProduce)
//                         {
//                             foreach (var req in productionControl.CurrentProductionItem.resourceCost)
//                             {
//                                 if (!inventoryManager.HasEnoughResource(req.resourceID, req.amount))
//                                 {
//                                     canResume = false;
//                                     resourcePriorityCalculator.IncreaseResourcePriority(req.resourceID, 200f);
//                                     // Debug.LogWarning("Insufficient resource " + req.resourceID + " to resume production on building " + plan.target.name);
//                                 }
//                             }
//                         }
//                         // Only resume if all resource requirements are met.
//                         if (canResume)
//                         {
//                             productionControl.ResumeProduction();
//                             executedPlans.Add(plan);
//                         }
//                     }
//                 }
//                 break;

//             // Handle canceling production.
//             case AIPlanType.CancelProduction:
//                 {
//                     ProductionBuildingControl productionControl = plan.target.GetComponent<ProductionBuildingControl>();
//                     if (productionControl != null && productionControl.IsProducing())
//                     {
//                         productionControl.ClearProductionItem();
//                         executedPlans.Add(plan);
//                     }
//                 }
//                 break;

//             // NEW: Handle collecting produced goods.
//             case AIPlanType.CollectProducedGoods:
//                 {
//                     ProductionBuildingControl productionControl = plan.target.GetComponent<ProductionBuildingControl>();
//                     if (productionControl != null && productionControl.totalStoredAmount > 0)
//                     {
//                         productionControl.CollectResources();
//                         executedPlans.Add(plan);
//                     }
//                 }
//                 break;

//                 case AIPlanType.OrderUnit:
//                 {
//                     // Get the KineticWarfareControl component from the plan's target building.
//                     KineticWarfareControl kwControl = plan.target.GetComponent<KineticWarfareControl>();
//                     if (kwControl == null)
//                     {
//                         // If the building does not support unit orders, remove the plan.
//                         aiPlanner.RemovePlan(plan);
//                         continue;
//                     }
//                     // Attempt to start training the unit using the stored unit type and multiplier.
//                     bool started = kwControl.StartTraining(plan.unitToOrder, plan.orderMultiplier);
//                     if (started)
//                     {
//                         executedPlans.Add(plan);
//                     }
//                     else
//                     {
//                         // Optionally, if training fails due to resource shortages, boost resource priorities.
//                         foreach (var req in plan.unitToOrder.resourceCost)
//                         {
//                             resourcePriorityCalculator.IncreaseResourcePriority(req.resourceID, 200f);
//                         }
//                     }
//                 }
//                 break;
//             }

//             // Yield periodically (every 5 plans) to keep the frame smooth.
//             if (i % 5 == 0)
//                 yield return null;
//         }

//         // Remove executed plans.
//         foreach (AIPlan executed in executedPlans)
//         {
//             aiPlanner.RemovePlan(executed);
//         }

//         yield break;
//     }

//     // Helper: Return required population for a plan.
//     private int GetPlanPopulationRequirement(AIPlan plan)
//     {
//         if (plan == null)
//             return 0;

//         switch (plan.planType)
//         {
//             case AIPlanType.Gathering:
//                 {
//                     EnvironmentControl env = plan.target.GetComponent<EnvironmentControl>();
//                     return env != null ? env.requiredGatheringPopulation : 0;
//                 }
//             case AIPlanType.Discovery:
//                 {
//                     EnvironmentControl env = plan.target.GetComponent<EnvironmentControl>();
//                     return env != null ? env.requiredPopulation : 0;
//                 }
//             case AIPlanType.Building:
//                 return plan.selectedBuilding != null ? plan.selectedBuilding.requiredPopulation : 0;
//             case AIPlanType.Repair:
//                 {
//                     AIBuildingControl bc = plan.target.GetComponent<AIBuildingControl>();
//                     return bc != null ? bc.peopleRequiredForRepair : 0;
//                 }
//             case AIPlanType.Research:
//                 return plan.selectedTechnology != null ? plan.selectedTechnology.populationRequired : 0;
//             case AIPlanType.Crafting:
//                 return plan.craftedItem != null ? plan.craftedItem.requiredPopulation : 0;
//             case AIPlanType.PopIncrease:
//                 return 2;
//             case AIPlanType.StartProduction:
//             case AIPlanType.ResumeProduction:
//                 {
//                     ProductionBuildingControl productionControl = plan.target.GetComponent<ProductionBuildingControl>();
//                     return productionControl != null && plan.productionItem != null ? plan.productionItem.requiredPopulation : 0;
//                 }
//             default:
//                 return 0;
//         }
//     }

//     // Helper: Return resource requirements for a plan.
//     private List<ResourceRequirement> GetPlanResourceRequirements(AIPlan plan)
//     {
//         if (plan == null)
//             return new List<ResourceRequirement>();

//         switch (plan.planType)
//         {
//             case AIPlanType.Building:
//                 return plan.selectedBuilding != null ? plan.selectedBuilding.requiredResources : new List<ResourceRequirement>();
//             case AIPlanType.Repair:
//                 {
//                     AIBuildingControl bc = plan.target.GetComponent<AIBuildingControl>();
//                     return bc != null ? bc.GetRepairCosts() : new List<ResourceRequirement>();
//                 }
//             case AIPlanType.Crafting:
//                 return plan.craftedItem != null ? plan.craftedItem.resourceCost : new List<ResourceRequirement>();
//             case AIPlanType.Research:
//                 return plan.selectedTechnology != null && plan.selectedTechnology.resourceRequirements != null
//                     ? plan.selectedTechnology.resourceRequirements
//                     : new List<ResourceRequirement>();
//             case AIPlanType.StartProduction:
//             case AIPlanType.ResumeProduction:
//                 {
//                     ProductionBuildingControl productionControl = plan.target.GetComponent<ProductionBuildingControl>();
//                     return (productionControl != null && plan.productionItem != null && plan.productionItem.costsResourcesToProduce) 
//                         ? plan.productionItem.resourceCost 
//                         : new List<ResourceRequirement>();
//                 }
//             default:
//                 return new List<ResourceRequirement>();
//         }
//     }

//     private bool HasRequiredResources(Building building)
//     {
//         return building.requiredResources.All(req => inventoryManager.HasEnoughResource(req.resourceID, req.amount));
//     }

//     private bool HasRequiredPopulation(Building building)
//     {
//         return populationManager.GetAvailablePopulation() >= building.requiredPopulation;
//     }

//     private void IncreaseResourcePriorities(Building building)
//     {
//         if (resourcePriorityCalculator == null)
//             return;

//         foreach (var requirement in building.requiredResources)
//         {
//             if (!inventoryManager.HasEnoughResource(requirement.resourceID, requirement.amount))
//             {
//                 resourcePriorityCalculator.IncreaseResourcePriority(requirement.resourceID, 200f);
//             }
//         }
//     }

//     // Repair helper methods.
//     private bool HasRequiredResourcesForRepair(List<ResourceRequirement> repairCosts)
//     {
//         return repairCosts.All(req => inventoryManager.HasEnoughResource(req.resourceID, req.amount));
//     }

//     private void IncreaseResourcePriorities(Technology tech)
//     {
//         if (resourcePriorityCalculator == null)
//             return;

//         foreach (var requirement in tech.resourceRequirements)
//         {
//             if (!inventoryManager.HasEnoughResource(requirement.resourceID, requirement.amount))
//             {
//                 resourcePriorityCalculator.IncreaseResourcePriority(requirement.resourceID, 200f);
//             }
//         }
//     }

//     private void DeductResourcesForRepair(List<ResourceRequirement> repairCosts)
//     {
//         foreach (var req in repairCosts)
//         {
//             inventoryManager.RemoveResource(req.resourceID, req.amount);
//         }
//     }
    
//     public int GetReservedPopulation()
//     {
//         return reservedPopulation;
//     }
// }