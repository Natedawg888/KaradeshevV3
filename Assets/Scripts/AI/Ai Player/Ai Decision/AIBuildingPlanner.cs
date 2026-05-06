// using System.Collections;
// using System.Collections.Generic;
// using System.Linq;
// using UnityEngine;

// public class AIBuildingPlanner : MonoBehaviour
// {
//     private AIPlayer aiPlayer;
//     private AIBuildingManager buildingManager;
//     private AIInventoryManager inventoryManager;
//     private AIPlanner aiPlanner;
//     private AITechnologyManager technologyManager;
//     private AITechnologyPriorityCalculator techPriorityCalculator;
//     private AIRepairPriorityCalculator repairPriorityCalculator;
//     public AIResourcePriorityCalculator resourcePriorityCalculator;

//     [Header("AI Building Planning")]
//     [SerializeField] private List<AIPlan> plannedBuildingActions = new List<AIPlan>(); // AI construction/repair plans

//     private void Start()
//     {
//         aiPlayer = GetComponentInParent<AIPlayer>();
//         buildingManager = aiPlayer?.GetComponentInChildren<AIBuildingManager>();
//         inventoryManager = aiPlayer?.GetComponentInChildren<AIInventoryManager>();
//         aiPlanner = aiPlayer?.GetComponentInChildren<AIPlanner>();
//         technologyManager = aiPlayer?.GetComponentInChildren<AITechnologyManager>();
//         techPriorityCalculator = GetComponent<AITechnologyPriorityCalculator>();
//         repairPriorityCalculator = GetComponent<AIRepairPriorityCalculator>();

//         if (buildingManager == null || inventoryManager == null || aiPlanner == null || technologyManager == null)
//         {
//             //Debug.LogWarning("[AIBuildingPlanner] Missing required components.");
//             return;
//         }
//     }

//     public void PlanBuildActions() 
//     {
//         StartCoroutine(PlanBuildingActionsCoroutine());
//         StartCoroutine(PlanResearchActionsCoroutine());
//         StartCoroutine(PlanRepairActionsCoroutine());
//     }

//     /// **🔹 Plan AI building actions based on available tiles and needs**
//     public IEnumerator PlanBuildingActionsCoroutine()
//     {
//         plannedBuildingActions.Clear();
//         List<Building> availableBuildings = buildingManager.GetAvailableBuildings();
//         HashSet<string> plannedBuildingTypes = new HashSet<string>();
//         HashSet<GameObject> usedTiles = new HashSet<GameObject>();

//         // Track existing building prefixes and tiles already in the AI planner.
//         HashSet<string> existingBuildingPrefixes = new HashSet<string>();
//         foreach (var plan in aiPlanner.GetAllPlannedActions().Where(p => p.planType == AIPlanType.Building))
//         {
//             if (plan.selectedBuilding != null)
//             {
//                 string prefix = plan.selectedBuilding.buildingID.Substring(0, 3);
//                 existingBuildingPrefixes.Add(prefix);
//             }

//             if (plan.target != null)
//             {
//                 usedTiles.Add(plan.target);
//             }
//         }

//         List<AIPlan> plansToAdd = new List<AIPlan>();
//         List<GameObject> availableTiles = buildingManager.GetAvailableBuildingTiles();

//         for (int i = 0; i < availableTiles.Count; i++)
//         {
//             GameObject tile = availableTiles[i];
//             if (tile == null || usedTiles.Contains(tile))
//                 continue;

//             EnvironmentControl envControl = tile.GetComponent<EnvironmentControl>();
//             if (envControl == null)
//                 continue;

//             Building selectedBuilding = SelectBestBuildingForTile(envControl, availableBuildings);
//             if (selectedBuilding != null)
//             {
//                 string buildingTypePrefix = selectedBuilding.buildingID.Substring(0, 3);

//                 // Skip if max limit reached.
//                 if (!buildingManager.CanBuild(selectedBuilding.buildingID, selectedBuilding.buildingLimit))
//                     continue;

//                 // Skip duplicate plans by building type or already used tiles.
//                 if (plannedBuildingTypes.Contains(buildingTypePrefix) || existingBuildingPrefixes.Contains(buildingTypePrefix))
//                     continue;

//                 // Calculate priority.
//                 int priority = CalculateBuildingPriority(selectedBuilding);

//                 // Reduce priority if buildings are under construction.
//                 if (buildingManager.IsBuildingUnderConstruction(buildingTypePrefix))
//                 {
//                     int countUnderConstruction = buildingManager.GetBuildingCount(buildingTypePrefix);
//                     float reductionFactor = Mathf.Pow(0.5f, countUnderConstruction);
//                     priority = Mathf.RoundToInt(priority * reductionFactor);
//                 }

//                 if (priority > 0)
//                 {
//                     AIPlan newPlan = new AIPlan(AIPlanType.Building, tile, priority)
//                     {
//                         selectedBuilding = selectedBuilding,
//                         turnsWithoutResources = 0
//                     };

//                     plansToAdd.Add(newPlan);
//                     plannedBuildingTypes.Add(buildingTypePrefix);
//                     usedTiles.Add(tile);  // Mark this tile as used
//                 }
//             }

//             // Yield every 10 iterations to avoid blocking the main thread.
//             if (i % 10 == 0)
//                 yield return null;
//         }

//         // Validate and remove invalid plans (like null targets).
//         plansToAdd.RemoveAll(plan => plan.target == null);

//         // --- Process Resource Availability for Building Plans ---
//         foreach (AIPlan plan in plansToAdd)
//         {
//             bool hasAllResources = true;

//             if (plan.selectedBuilding != null && plan.selectedBuilding.requiredResources != null)
//             {
//                 foreach (var req in plan.selectedBuilding.requiredResources)
//                 {
//                     if (!inventoryManager.HasEnoughResource(req.resourceID, req.amount))
//                     {
//                         hasAllResources = false;
//                         resourcePriorityCalculator.IncreaseResourcePriority(req.resourceID, 200f);
//                     }
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

//         // Sort plans by descending priority.
//         plansToAdd = plansToAdd.OrderByDescending(p => p.priority).ToList();

//         // Add the new plans to the global AIPlanner.
//         foreach (var plan in plansToAdd)
//         {
//             aiPlanner.AddPlan(plan);
//         }

//         yield return null;
//     }

//     public IEnumerator PlanResearchActionsCoroutine()
//     {
//         if (technologyManager == null || techPriorityCalculator == null)
//             yield break;

//         List<Technology> availableTechnologies = technologyManager.GetAvailableTechnologies();
//         if (availableTechnologies == null || availableTechnologies.Count == 0)
//             yield break;

//         HashSet<string> researchedTechs = new HashSet<string>(
//             technologyManager.GetResearchedTechnologies().Select(t => t.technologyID)
//         );
//         HashSet<string> activeResearch = new HashSet<string>(
//             technologyManager.GetActiveResearchTechnologies()
//         );

//         List<AIPlan> candidatePlans = new List<AIPlan>();

//         for (int i = 0; i < availableTechnologies.Count; i++)
//         {
//             Technology tech = availableTechnologies[i];

//             if (researchedTechs.Contains(tech.technologyID) || activeResearch.Contains(tech.technologyID))
//                 continue;

//             if (tech.levelRequired > aiPlayer.aiLevel)
//             {
//                 Debug.Log($"[AITechnologyManager] Cannot research {tech.technologyName} because its required level ({tech.levelRequired}) is above the AI player's level ({aiPlayer.aiLevel}).");
//                 continue;
//             }

//             if (!HasUnlockedResourcesForTechnology(tech))
//                 continue;

//             GameObject researchBuilding = GetBuildingForTechnology(tech);
//             if (researchBuilding != null)
//             {
//                 int candidatePriority = techPriorityCalculator.CalculateTechnologyPriority(tech);

//                 AIBuildingControl buildingControl = researchBuilding.GetComponent<AIBuildingControl>();
//                 if (buildingControl != null && buildingControl.healthSlider != null)
//                 {
//                     float healthPercent = (float)buildingControl.health / buildingControl.healthSlider.maxValue;
//                     if (healthPercent < 0.75f)
//                     {
//                         candidatePriority = Mathf.RoundToInt(candidatePriority * healthPercent);
//                     }
//                 }

//                 AIPlan candidatePlan = new AIPlan(AIPlanType.Research, researchBuilding, candidatePriority)
//                 {
//                     selectedTechnology = tech
//                 };

//                 candidatePlans.Add(candidatePlan);
//             }

//             if (i % 10 == 0)
//                 yield return null;
//         }

//         foreach (AIPlan plan in candidatePlans)
//         {
//             if (plan.selectedTechnology == null)
//                 continue;

//             bool hasAllResources = true;

//             if (plan.selectedTechnology.resourceRequirements != null)
//             {
//                 foreach (var req in plan.selectedTechnology.resourceRequirements)
//                 {
//                     if (!inventoryManager.HasEnoughResource(req.resourceID, req.amount))
//                     {
//                         hasAllResources = false;
//                         resourcePriorityCalculator.IncreaseResourcePriority(req.resourceID, 200f);
//                     }
//                 }
//             }

//             if (!hasAllResources)
//             {
//                 plan.turnsWithoutResources++;
//                 int priorityBoost = Mathf.RoundToInt(plan.turnsWithoutResources * 100); // Increase priority by 50 per turn without resources
//                 int oldPriority = plan.priority;
//                 plan.priority += priorityBoost;
//                 Debug.Log($"[AIBuildingPlanner] Boosted research plan priority for {plan.selectedTechnology.technologyName} from {oldPriority} to {plan.priority} (turns without resources: {plan.turnsWithoutResources}).");
//             }
//             else
//             {
//                 plan.turnsWithoutResources = 0; // Reset the counter when resources become available.
//             }
//         }

//         // Sort candidates by updated priority and select up to two best plans.
//         candidatePlans = candidatePlans.OrderByDescending(p => p.priority).ToList();
//         List<AIPlan> bestResearchPlans = candidatePlans.Take(2).ToList();

//         List<AIPlan> existingResearchPlans = aiPlanner.GetAllPlannedActions()
//             .Where(p => p.planType == AIPlanType.Research)
//             .ToList();

//         // Update or add research plans.
//         foreach (AIPlan newPlan in bestResearchPlans)
//         {
//             AIPlan existing = existingResearchPlans.FirstOrDefault(p =>
//                 p.selectedTechnology != null &&
//                 p.selectedTechnology.technologyID == newPlan.selectedTechnology.technologyID);

//             if (existing != null)
//             {
//                 if (existing.priority != newPlan.priority || existing.target != newPlan.target)
//                 {
//                     existing.priority = newPlan.priority;
//                     existing.target = newPlan.target;
//                 }
//             }
//             else
//             {
//                 aiPlanner.AddPlan(newPlan);
//             }
//         }

//         // Remove outdated research plans that are no longer valid.
//         foreach (AIPlan plan in existingResearchPlans)
//         {
//             if (plan.selectedTechnology == null ||
//                 !bestResearchPlans.Any(p => p.selectedTechnology.technologyID == plan.selectedTechnology.technologyID))
//             {
//                 aiPlanner.RemovePlan(plan);
//             }
//         }

//         yield return null;
//     }

//     /// **🔹 Finds an existing AI building where the AI can research a technology**
//     private GameObject GetBuildingForTechnology(Technology tech)
//     {
//         List<GameObject> ownedBuildings = buildingManager.GetOwnedBuildings();
        
//         if (ownedBuildings == null || ownedBuildings.Count == 0)
//         {
//             //Debug.LogWarning("[AIBuildingPlanner] AI has no buildings. Cannot research technologies.");
//             return null;
//         }

//         foreach (GameObject building in ownedBuildings)
//         {
//             if (building == null) continue;

//             // ✅ Check for AIBuildingControl instead of BuildingControl
//             AIBuildingControl aiBuildingControl = building.GetComponent<AIBuildingControl>();

//             if (aiBuildingControl == null)
//             {
//                 //Debug.Log($"[AIBuildingPlanner] {building.name} has no AIBuildingControl. Skipping.");
//                 continue;
//             }

//             string buildingID = aiBuildingControl.buildingID;

//             if (string.IsNullOrEmpty(buildingID))
//             {
//                 //Debug.Log($"[AIBuildingPlanner] {building.name} has no valid building ID. Skipping.");
//                 continue;
//             }

//             if (tech.researchableOnBuildings.Contains(buildingID))
//             {
//                 //Debug.Log($"[AIBuildingPlanner] {tech.technologyName} can be researched in {building.name} ({buildingID}).");
//                 return building;
//             }
//         }

//         //Debug.LogWarning($"[AIBuildingPlanner] No valid research building found for {tech.technologyName}.");
//         return null; // No valid building found
//     }

//     private Building SelectBestBuildingForTile(EnvironmentControl envControl, List<Building> availableBuildings)
//     {
//         Building bestCandidate = null;
//         int highestPriority = int.MinValue;
        
//         foreach (Building building in availableBuildings)
//         {
//             if (building.requiredTileSize == envControl.tileSize &&
//                 building.requiredEnvironmentTypes.Contains(envControl.environmentType))
//             {
//                 int candidatePriority = CalculateBuildingPriority(building);
//                 if (candidatePriority > highestPriority)
//                 {
//                     highestPriority = candidatePriority;
//                     bestCandidate = building;
//                 }
//             }
//         }
        
//         return bestCandidate;
//     }

//     private int CalculateBuildingPriority(Building building)
//     {
//         AIBuildingPriorityCalculator priorityCalculator = GetComponent<AIBuildingPriorityCalculator>();
//         return priorityCalculator.CalculateBuildingPriority(building);
//     }

//     private bool HasUnlockedResourcesForTechnology(Technology tech)
//     {
//         if (tech == null || tech.resourceRequirements == null || tech.resourceRequirements.Count == 0)
//             return true; // No required resources, so it's always available

//         foreach (var requirement in tech.resourceRequirements)
//         {
//             // ✅ **Skip checking if the resource is GFD (General Food)**
//             if (requirement.resourceID == "GFD")
//             {
//                 //Debug.Log($"[AIBuildingPlanner] Skipping check for GFD (General Food) for {tech.technologyName}.");
//                 continue;
//             }

//             // If the required resource is **not unlocked**, return false
//             if (aiPlayer.GetComponentInChildren<AIResourceManager>().GetAIResourceByID(requirement.resourceID) == null)
//             {
//                 //Debug.Log($"[AIBuildingPlanner] {tech.technologyName} requires {requirement.resourceID}, which is locked.");
//                 return false;
//             }
//         }
//         return true;
//     }

//     public IEnumerator PlanRepairActionsCoroutine()
//     {
//         if (repairPriorityCalculator == null)
//         {
//             //Debug.LogError("[AIBuildingPlanner] AIRepairPriorityCalculator is missing!");
//             yield break;
//         }

//         List<GameObject> ownedBuildings = buildingManager.GetOwnedBuildings();
//         List<AIPlan> repairPlans = new List<AIPlan>();

//         // Build a set of buildings that already have a repair plan in the AI planner.
//         HashSet<GameObject> buildingsWithRepairPlan = new HashSet<GameObject>(
//             aiPlanner.GetAllPlannedActions()
//                 .Where(plan => plan.planType == AIPlanType.Repair && plan.target != null)
//                 .Select(plan => plan.target)
//         );

//         for (int i = 0; i < ownedBuildings.Count; i++)
//         {
//             GameObject buildingObj = ownedBuildings[i];
//             if (buildingObj == null)
//                 continue;

//             AIBuildingControl building = buildingObj.GetComponent<AIBuildingControl>();

//             // Skip if the building is fully healed.
//             if (building == null || building.health >= building.healthSlider.maxValue)
//                 continue;

//             int priority = repairPriorityCalculator.CalculateRepairPriority(building);
//             AIPlan repairPlan = new AIPlan(AIPlanType.Repair, buildingObj, priority);
//             repairPlans.Add(repairPlan);

//             // Yield every 10 iterations to avoid blocking the main thread.
//             if (i % 10 == 0)
//                 yield return null;
//         }

//         // Add the new repair plans to the AI planner.
//         foreach (var plan in repairPlans)
//         {
//             aiPlanner.AddPlan(plan);
//         }

//         //Debug.Log($"[AIBuildingPlanner] Planned {repairPlans.Count} repair actions.");
//         yield return null;
//     }
// }