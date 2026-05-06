// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;

// public class AIUnitPlanner : MonoBehaviour
// {
//     [Header("References")]
//     // Reference to the manager holding AI unit groups.
//     public AIPlayer              aiPlayer;
//     public AIUnitManager         aiUnitManager;
//     public AIPopulationManager   populationManager;
//     public AIInventoryManager    inventoryManager;
//     public AIBuildingManager     aiBuildingManager;
//     public AIPointsOfInterestManager poiManager;
//     public AITileDiscoveryManager discoveryManager;
    
//         [Header("Planning Settings")]
//     // Delay between planning for each unit group.
//     public float planningDelay = 0.1f;
//     public float startPlanningDelay = 1.0f;
//     // Multiplier for neighbor detection (for movement planning).
//     public float detectionMultiplier = 1.1f;
//     // Idle planning parameters:
//     public float idleDistanceThreshold = 5f;         // Maximum distance from a POI to consider idling.
//     public float idleNeighborDetectionMultiplier = 1.1f; // Detection multiplier used when checking neighbors in idle planning.


//     private void Start()
//     {
//         // Subscribe to the turn start event.
//         TurnSystem.SubscribeToStartOfTurn(StartTurnPlanning);
//         Debug.Log("[AIUnitPlanner] Subscribed to StartTurnPlanning.");
//     }

//     private void OnDestroy()
//     {
//         // Unsubscribe to avoid memory leaks.
//         TurnSystem.UnsubscribeFromStartOfTurn(StartTurnPlanning);
//         Debug.Log("[AIUnitPlanner] Unsubscribed from StartTurnPlanning.");
//     }

//     // Called at the start of each turn.
//     private void StartTurnPlanning()
//     {
//         if (aiUnitManager != null)
//         {
//             List<MilitiaUnitGroup> groups = aiUnitManager.GetAIUnitGroups();
//             Debug.Log($"[AIUnitPlanner] Found {groups.Count} unit groups in AIUnitManager.");
//             if (groups.Count > 0)
//             {
//                 StartCoroutine(PlanUnits());
//             }
//         }
//         else
//         {
//             Debug.LogWarning("[AIUnitPlanner] AIUnitManager is null.");
//         }
//     }

//     // Iterates over all AI unit groups and creates a unit plan for each.
//     IEnumerator PlanUnits()
//     {
//         yield return new WaitForSeconds(startPlanningDelay);
//         Debug.Log("[AIUnitPlanner] Starting planning after initial delay.");

//         List<MilitiaUnitGroup> groups = aiUnitManager.GetAIUnitGroups();
//         foreach (MilitiaUnitGroup group in groups)
//         {
//             // Skip planning if a movement plan is already set for this group.
//             if (group.currentPlan != null && (group.currentPlan.planType == UnitPlanType.Move))
//             {
//                 Debug.Log($"[AIUnitPlanner] Group {group.groupID} already has a movement plan, skipping planning.");
//                 continue;
//             }

//             // —— 1) Check Disbanding first ——
//             int disbandPrio = AIDisbandPlanEvaluator.CalculateDisbandPriority(
//                 group,
//                 populationManager,
//                 inventoryManager
//             );
//             if (disbandPrio > 200)
//             {
//                 DisbandGroup(group);
//                 yield return new WaitForSeconds(planningDelay);
//                 continue;
//             }
            
//             // —— 2) Check StopPatrol first ——
//             int stopPriority = AIStopPatrolPlanEvaluator.CalculateStopPatrolPriority(
//                 group,
//                 aiBuildingManager,
//                 aiUnitManager
//             );
//             if (stopPriority > 200)
//             {
//                 // 1) clear out any existing movement/idle state
//                 group.currentPlan = null;
//                 group.leftoverMovement = 0;
//                 group.turnsToMoveStep = 0;
//                 group.isPatrolling = false;
//                 group.initialTurnsForMovementStep = 0;
//                 group.movementPathPositions.Clear();
//                 group.originalPatrolRoute.Clear();

//                 // 2) assign StopPatrol plan
//                 var stopPlan = new UnitPlan(group, UnitPlanType.StopPatrol)
//                 {
//                     priority = stopPriority
//                 };
//                 group.currentPlan = stopPlan;

//                 Debug.Log($"[AIUnitPlanner] Group {group.groupID} STOP PATROL (priority {stopPriority})");

//                 yield return new WaitForSeconds(planningDelay);
//                 continue;
//             }
            
//             Debug.Log($"[AIUnitPlanner] Planning orders for group {group.groupID}.");
            
//             UnitPlan idlePlan = null;
//             UnitPlan movePlan = null;
//             UnitPlan patrolPlan = null;
            
//             // -------------------------------
//             // 3. Evaluate idle plan potential.
//             // -------------------------------
//             (UnitControl idleTarget, int idlePotentialPriority) = AIIdlePlanEvaluator.GetBestIdleTargetWithPriority(
//                 group,
//                 poiManager,
//                 idleDistanceThreshold,
//                 idleNeighborDetectionMultiplier
//             );

//             // Generate idle plan if there's any potential.
//             if (idlePotentialPriority > 0 && idleTarget != null)
//             {
//                 yield return StartCoroutine(AIUnitIdlePlanner.GenerateIdlePlan(
//                     group,
//                     poiManager,
//                     idleDistanceThreshold,
//                     idleNeighborDetectionMultiplier,
//                     (plan) => { idlePlan = plan; }
//                 ));
//             }

//             // --------------------------------
//             // 4. Evaluate movement plan potential.
//             // --------------------------------
//             UnitControl globalTarget = AIUnitMovementPlanner.GetGlobalTargetTile(poiManager);
//             int movementPotentialPriority = AIUnitMovementPriorityEvaluator.CalculatePotentialPriority(
//                 group, 
//                 globalTarget, 
//                 detectionMultiplier, 
//                 poiManager);

//             // Log the evaluated priorities for debugging.
//             Debug.Log($"[AIUnitPlanner] Group {group.groupID} idle potential: {idlePotentialPriority}, movement potential: {movementPotentialPriority}");
            
//             // Only attempt to generate a movement plan if its evaluated potential is higher.
//             if (movementPotentialPriority > idlePotentialPriority)
//             {
//                 yield return StartCoroutine(AIUnitMovementPlanner.GenerateMovementPlan(
//                     group,
//                     poiManager,
//                     detectionMultiplier,
//                     (plan) => { movePlan = plan; }
//                 ));
//             }

//             // --------------------------------
//             // 5. Evaluate patrol plan potential.
//             // --------------------------------
            
//             yield return StartCoroutine(AIUnitPatrolPlanner.GeneratePatrolPlan(
//                 group,
//                 poiManager,
//                 discoveryManager,
//                 aiBuildingManager,
//                 aiUnitManager,
//                 detectionMultiplier,
//                 plan => patrolPlan = plan
//             ));
            
//             // -------------------------------
//             // 6. Compare final plans and apply the highest.
//             // -------------------------------
//             // Determine actual plan priorities (for example, after plan generation, the plan might assign its own final score).
//             int idlePriority = (idlePlan != null) ? idlePlan.priority : 0;
//             int movePriority = movementPotentialPriority;
//             int patrolPriority = (patrolPlan != null) ? patrolPlan.priority : 0;
//             Debug.Log($"[AIUnitPlanner] Group {group.groupID} final idle priority: {idlePriority}, final movement priority: {movePriority}, final patrol priority: {patrolPriority}.");
            
//             // Apply the plan with the highest priority.
//             if (idlePriority >= movePriority && idlePriority >= patrolPriority && idlePlan != null)
//             {
//                 group.leftoverMovement = 0;
//                 group.turnsToMoveStep = 0;
//                 group.initialTurnsForMovementStep = 0;
//                 group.currentPlan = idlePlan;
//                 Debug.Log($"[AIUnitPlanner] Group {group.groupID} assigned idle plan.");
//             }
//             else if (movePriority >= idlePriority && movePriority >= patrolPriority && movePlan != null)
//             {
//                 group.movementPathPositions = new List<Vector3>(movePlan.movementPath);
//                 group.currentPlan = movePlan;
//                 Debug.Log($"[AIUnitPlanner] Group {group.groupID} assigned movement plan.");
//             }

//             else if (patrolPlan != null)
//             {
//                 group.movementPathPositions = new List<Vector3>(patrolPlan.movementPath);
//                 group.currentPlan           = patrolPlan;
//                 Debug.Log($"[AIUnitPlanner] Group {group.groupID} assigned patrol plan.");
//             }
            
//             yield return new WaitForSeconds(planningDelay);
//         }
//     }

//     private void DisbandGroup(MilitiaUnitGroup group)
//     {
//         // 1) refund population
//         int popToRelease = group.unitType.requiredPopulation * group.totalUnits;
//         populationManager.ReleasePopulation(popToRelease);

//         // 3) refund equipment if needed
//         group.ProcessEquipmentRefund();

//         // 4) remove from tile
//         group.unitControl?.RemoveUnitGroup(group);

//         // 5) remove from AI registry
//         aiUnitManager.RemoveAIUnitGroup(group);

//         Debug.Log($"[AIUnitPlanner] Disbanded group {group.groupID}, refunded pop={popToRelease}");
//     }
// }