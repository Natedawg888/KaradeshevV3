// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
// using System.Linq;


// public class AIPlanner : MonoBehaviour
// {
//     [Header("AI Planning Settings")]
//     [SerializeField] private int maxPlannedMoves = 5; // Maximum number of planned moves

//     private AITileTracker tileTracker;
//     private AITileDiscoveryManager discoveryManager;
//     private AIGatheringManager gatheringManager;
//     private AIBuildingPlanner buildingPlanner;
//     private AIUtilityPlanner utilityPlanner;
//     private AIPopulationManager populationManager;
//     private AIPopulationIncreasePlanEvaluator populationIncreaseEvaluator;
//     private DiscoveryPlanEvaluator discoveryPlanEvaluator;
//     private GatheringPlanEvaluator gatheringPlanEvaluator;
//     private AIDecisionMaker decisionMaker;
//     private AIResourcePriorityCalculator priorityCalculator;
//     private AITechnologyManager technologyManager;
//     private AIPointsOfInterestManager pointsOfInterestManager;

//     [Header("Planned Actions (Sorted by Priority)")]
//     [SerializeField] private List<AIPlan> plannedActions = new List<AIPlan>();

//     private void Start()
//     {
//         Transform aiPlayer = transform.parent;

//         if (aiPlayer == null)
//         {
//             //Debug.LogWarning("[AIPlanner] AI Player (parent) is missing.");
//             return;
//         }

//         tileTracker = aiPlayer.GetComponentInChildren<AITileTracker>();
//         discoveryManager = aiPlayer.GetComponentInChildren<AITileDiscoveryManager>();
//         gatheringManager = aiPlayer.GetComponentInChildren<AIGatheringManager>();
//         buildingPlanner = aiPlayer.GetComponentInChildren<AIBuildingPlanner>();
//         utilityPlanner = aiPlayer.GetComponentInChildren<AIUtilityPlanner>();
//         populationManager = aiPlayer.GetComponentInChildren<AIPopulationManager>();
//         populationIncreaseEvaluator = aiPlayer.GetComponentInChildren<AIPopulationIncreasePlanEvaluator>();
//         discoveryPlanEvaluator = aiPlayer.GetComponentInChildren<DiscoveryPlanEvaluator>();
//         gatheringPlanEvaluator = aiPlayer.GetComponentInChildren<GatheringPlanEvaluator>();
//         decisionMaker = aiPlayer.GetComponentInChildren<AIDecisionMaker>();
//         priorityCalculator = aiPlayer.GetComponentInChildren<AIResourcePriorityCalculator>();
//         technologyManager = aiPlayer.GetComponentInChildren<AITechnologyManager>();
//         pointsOfInterestManager = aiPlayer.GetComponentInChildren<AIPointsOfInterestManager>();

//         if (tileTracker == null || discoveryManager == null || gatheringManager == null ||
//             populationManager == null || discoveryPlanEvaluator == null || gatheringPlanEvaluator == null ||
//             populationIncreaseEvaluator == null)

//         // Initial planning with delay to allow BFS completion
//         StartCoroutine(DelayedPlanning());
//         TurnSystem.SubscribeToStartOfTurn(OnTurnStart);
//     }

//     private void OnDestroy()
//     {
//         TurnSystem.UnsubscribeFromStartOfTurn(OnTurnStart);
//     }

//     private IEnumerator DelayedPlanning()
//     {
//         yield return new WaitForSeconds(0.03f);
//         PlanActionsCoroutine();
//     }

//     private void OnTurnStart()
//     {
//         StartCoroutine(PlanActionsCoroutine());
//         pointsOfInterestManager.UpdatePointsOfInterest();
//     }

//     public IEnumerator PlanActionsCoroutine()
//     {
//         CleanInvalidPlans();
        
//         buildingPlanner.PlanBuildActions();
//         utilityPlanner.PlanUtilityActions();

//         int plannedGathering = plannedActions.Count(plan => plan.planType == AIPlanType.Gathering);
//         int plannedDiscovery = plannedActions.Count(plan => plan.planType == AIPlanType.Discovery);

//         int maxGatheringPlans = Mathf.CeilToInt(maxPlannedMoves * 0.6f);
//         int maxDiscoveryPlans = Mathf.CeilToInt(maxPlannedMoves * 0.4f);

//         // Process discovery plans in batches.
//         List<GameObject> undiscoveredTiles = discoveryManager.GetUndiscoveredTiles().ToList();
//         for (int i = 0; i < undiscoveredTiles.Count; i++)
//         {
//             GameObject tile = undiscoveredTiles[i];
//             if (tile == null)
//             {
//                 //Debug.LogWarning("[AIPlanner] Skipping a null tile in discovery evaluation.");
//                 continue;
//             }

//             if (!IsTileAlreadyPlanned(tile) && !discoveryManager.GetDiscoveredTiles().Contains(tile))
//             {
//                 EnvironmentControl envControl = tile.GetComponent<EnvironmentControl>();
//                 if (envControl != null)
//                 {
//                     int priorityScore = discoveryPlanEvaluator.Evaluate(envControl);
//                     if (priorityScore > 0)
//                     {
//                         AddPlan(new AIPlan(AIPlanType.Discovery, tile, priorityScore));
//                     }
//                 }
//             }

//             // Yield every 10 iterations to prevent frame drop.
//             if (i % 10 == 0)
//                 yield return null;
//         }

//         // Process gathering plans in batches.
//         List<GameObject> gatherableTiles = gatheringManager.GetGatherableTiles();
//         for (int i = 0; i < gatherableTiles.Count && plannedGathering < maxGatheringPlans; i++)
//         {
//             GameObject tile = gatherableTiles[i];
//             EnvironmentControl envControl = tile.GetComponent<EnvironmentControl>();
//             if (envControl != null && !IsTileAlreadyPlanned(tile) && !envControl.isBeingAIGathered)
//             {
//                 int priorityScore = gatheringPlanEvaluator.Evaluate(envControl);
//                 if (priorityScore > 0)
//                 {
//                     AddPlan(new AIPlan(AIPlanType.Gathering, tile, priorityScore));
//                     plannedGathering++;
//                 }
//             }

//             // Yield every 10 iterations.
//             if (i % 10 == 0)
//                 yield return null;
//         }

//         // **Ensure only one PopIncrease plan exists in the action list**
//         if (!plannedActions.Any(plan => plan.planType == AIPlanType.PopIncrease))
//         {
//             int popIncreasePriority = populationIncreaseEvaluator.Evaluate();
//             if (popIncreasePriority > 0)
//                 AddPlan(new AIPlan(AIPlanType.PopIncrease, null, popIncreasePriority));
//         }

//         // Process other plan types here as needed...
        
//         CleanInvalidPlans();
        
//         plannedActions = plannedActions.OrderByDescending(p => p.priority).ToList();
//         //Debug.Log($"[AIPlanner] Planned {plannedActions.Count} actions (Gathering: {plannedGathering}, Discovery: {plannedDiscovery}).");

//         // Now execute the turn
//         decisionMaker.ExecuteTurn();
//     }

//     public void AddPlan(AIPlan newPlan)
//     {
//         // ✅ Prevent adding plans with invalid priority
//         if (newPlan == null || newPlan.priority <= 0)
//         {
//             //Debug.LogWarning("[AIPlanner] Attempted to add an invalid plan. Ignoring.");
//             return;
//         }

//         // **Ensure only one PopIncrease plan exists at a time**
//         if (newPlan.planType == AIPlanType.PopIncrease)
//         {
//             // Remove any existing PopIncrease plans before adding the new one
//             plannedActions.RemoveAll(plan => plan.planType == AIPlanType.PopIncrease);
//         }

//         if (plannedActions.Count >= maxPlannedMoves)
//         {
//             AIPlan lowestPriorityPlan = plannedActions.OrderBy(p => p.priority).FirstOrDefault();

//             if (lowestPriorityPlan != null && lowestPriorityPlan.priority < newPlan.priority)
//             {
//                 plannedActions.Remove(lowestPriorityPlan);
//                 //Debug.Log($"[AIPlanner] Replacing lowest priority plan: {lowestPriorityPlan.planType} with {newPlan.planType}");
//                 plannedActions.Add(newPlan);
//             }
//         }
//         else
//         {
//             plannedActions.Add(newPlan);
//         }
//     }

//     public void CleanInvalidPlans()
//     {
//         int initialCount = plannedActions.Count;

//         // Remove plans with no target or non-positive priority.
//         plannedActions.RemoveAll(plan => plan == null || (plan.target == null && plan.priority <= 0));

//         // Remove any building plans with a null target.
//         plannedActions.RemoveAll(plan => plan.planType == AIPlanType.Building && plan.target == null);
        
//         // Remove discovery plans if the target tile is already discovered.
//         plannedActions.RemoveAll(plan =>
//             plan.planType == AIPlanType.Discovery &&
//             plan.target != null &&
//             discoveryManager.GetDiscoveredTiles().Contains(plan.target)
//         );

//         // Remove research plans for already researched or active technologies.
//         plannedActions.RemoveAll(plan =>
//             plan.planType == AIPlanType.Research &&
//             (
//                 IsTechnologyAlreadyResearched(plan.selectedTechnology) || 
//                 IsTechnologyActive(plan.selectedTechnology)
//             )
//         );

//         // Ensure only one population increase plan exists.
//         bool popIncreaseExists = false;
//         plannedActions.RemoveAll(plan =>
//         {
//             if (plan.planType == AIPlanType.PopIncrease)
//             {
//                 if (popIncreaseExists) return true; // Remove additional pop increase plans.
//                 popIncreaseExists = true; // Mark the first valid pop increase plan as existing.
//             }
//             return false;
//         });

//         // Remove any Crafting, Research, or Repair plans that have a null target.
//         plannedActions.RemoveAll(plan =>
//             (plan.planType == AIPlanType.Discovery ||
//             plan.planType == AIPlanType.Gathering ||
//             plan.planType == AIPlanType.Crafting ||
//             plan.planType == AIPlanType.Research ||
//             plan.planType == AIPlanType.Repair ||
//             plan.planType == AIPlanType.StartProduction ||
//             plan.planType == AIPlanType.ResumeProduction ||
//             plan.planType == AIPlanType.CancelProduction ||
//             plan.planType == AIPlanType.CollectProducedGoods) &&
//             plan.target == null
//         );

//         // Remove `StartProduction` plans if the target building is already producing.
//         plannedActions.RemoveAll(plan =>
//             plan.planType == AIPlanType.StartProduction &&
//             plan.target != null &&
//             plan.target.GetComponent<ProductionBuildingControl>()?.IsProducing() == true
//         );

//         // ✅ Remove `ResumeProduction` and `CancelProduction` plans if the target building **is no longer producing**.
//         plannedActions.RemoveAll(plan =>
//             (plan.planType == AIPlanType.ResumeProduction || plan.planType == AIPlanType.CollectProducedGoods || plan.planType == AIPlanType.CancelProduction) &&
//             plan.target != null &&
//             plan.target.GetComponent<ProductionBuildingControl>()?.IsProducing() == false
//         );

//         // Remove duplicate building plans (keep only the highest priority plan per building prefix)
//         List<AIPlan> duplicatesToRemove = new List<AIPlan>();

//         var buildingPlanGroups = plannedActions
//             .Where(plan => plan.planType == AIPlanType.Building && plan.selectedBuilding != null)
//             .GroupBy(plan => plan.selectedBuilding.buildingID.Substring(0, 3));

//         foreach (var group in buildingPlanGroups)
//         {
//             if (group.Count() > 1)
//             {
//                 AIPlan highestPlan = group.OrderByDescending(p => p.priority).First();
//                 foreach (var plan in group)
//                 {
//                     if (plan != highestPlan)
//                         duplicatesToRemove.Add(plan);
//                 }
//             }
//         }

//         plannedActions.RemoveAll(plan =>
//             plan.planType == AIPlanType.Research &&
//             plan.selectedTechnology != null &&
//             (
//                 IsTechnologyAlreadyResearched(plan.selectedTechnology) || 
//                 IsTechnologyActive(plan.selectedTechnology) || 
//                 plan.selectedTechnology.levelRequired > technologyManager.aiPlayer.aiLevel
//             )
//         );

//         plannedActions.RemoveAll(plan => duplicatesToRemove.Contains(plan));
//         //Debug.Log($"[AIPlanner] Removed {duplicatesToRemove.Count} duplicate building plan(s), keeping only the highest priority per group.");

//         // *** NEW: Remove duplicate repair plans, keeping only the highest priority repair plan per building ***
//         List<AIPlan> repairDuplicatesToRemove = new List<AIPlan>();

//         var repairPlanGroups = plannedActions
//             .Where(plan => plan.planType == AIPlanType.Repair && plan.target != null)
//             .GroupBy(plan =>
//             {
//                 // Group by the unique building ID (assuming AIBuildingControl is on the target)
//                 AIBuildingControl buildingControl = plan.target.GetComponent<AIBuildingControl>();
//                 return buildingControl != null ? buildingControl.buildingID : plan.target.name;
//             });

//         foreach (var group in repairPlanGroups)
//         {
//             if (group.Count() > 1)
//             {
//                 AIPlan highestPlan = group.OrderByDescending(p => p.priority).First();
//                 foreach (var plan in group)
//                 {
//                     if (plan != highestPlan)
//                         repairDuplicatesToRemove.Add(plan);
//                 }
//             }
//         }
//         plannedActions.RemoveAll(plan => repairDuplicatesToRemove.Contains(plan));
//         //Debug.Log($"[AIPlanner] Removed {repairDuplicatesToRemove.Count} duplicate repair plan(s), keeping only the highest priority per building.");
//     }

//     private bool IsTileAlreadyPlanned(GameObject tile)
//     {
//         return plannedActions.Any(plan => plan.target == tile);
//     }

//     public AIPlan GetNextPlannedAction()
//     {
//         return plannedActions.Count > 0 ? plannedActions[0] : null;
//     }

//     public void RemovePlan(AIPlan plan)
//     {
//         plannedActions.Remove(plan);
//     }

//     public List<AIPlan> GetAllPlannedActions()
//     {
//         return new List<AIPlan>(plannedActions);
//     }

//     private bool IsTechnologyAlreadyResearched(Technology tech)
//     {
//         if (tech == null) return false;
//         if (technologyManager == null)
//         {
//             //Debug.LogError("[AIPlanner] AITechnologyManager is NULL! Cannot check if technology is researched.");
//             return false;
//         }

//         var researchedTechs = technologyManager.GetResearchedTechnologies();
//         bool isResearched = researchedTechs.Any(t => t.technologyID == tech.technologyID);

//         return isResearched;
//     }

//     private bool IsTechnologyActive(Technology tech)
//     {
//         if (tech == null) return false;
//         if (technologyManager == null)
//         {
//             //Debug.LogError("[AIPlanner] AITechnologyManager is NULL! Cannot check active research status.");
//             return false;
//         }

//         var activeResearch = technologyManager.GetActiveResearchTechnologies();
//         bool isActive = activeResearch.Contains(tech.technologyID);

//         return isActive;
//     }

//     public int GetReservedPopulation()
//     {
//         return decisionMaker != null ? decisionMaker.GetReservedPopulation() : 0;
//     }
// }