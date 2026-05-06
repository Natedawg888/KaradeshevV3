// using System.Collections;
// using System.Collections.Generic;
// using System.Linq;
// using UnityEngine;

// public class AIUnitPatrolPlanner : MonoBehaviour
// {
//     public static IEnumerator GeneratePatrolPlan(
//         MilitiaUnitGroup           group,
//         AIPointsOfInterestManager  poiManager,
//         AITileDiscoveryManager     discoveryManager,
//         AIBuildingManager          buildingManager,
//         AIUnitManager              aiUnitManager,
//         float                      detectionMultiplier,
//         System.Action<UnitPlan>    callback)
//     {
//         // —— NEW: don’t re‐plan if we already have a Patrol plan ——  
//         if (group.currentPlan != null 
//             && group.currentPlan.planType == UnitPlanType.Patrol 
//             && group.movementPathPositions != null
//             && group.movementPathPositions.Count > 0)
//         {
//             Debug.Log($"[PatrolPlanner] Group {group.groupID} still has {group.movementPathPositions.Count} steps left—skip replanning.");
//             callback(null);
//             yield break;
//         }

//         // 1) Gather all waypoints: POIs, discovered, AND owned buildings
//         var waypoints = new List<UnitControl>();

//         if (poiManager != null)
//             waypoints.AddRange(
//                 poiManager.pointsOfInterest
//                           .Select(p => p.tile.GetComponent<UnitControl>())
//                           .Where(uc => uc != null)
//             );

//         if (discoveryManager != null)
//             waypoints.AddRange(
//                 discoveryManager.GetDiscoveredTiles()
//                                 .Select(go => go.GetComponent<UnitControl>())
//                                 .Where(uc => uc != null)
//             );

//         if (buildingManager != null)
//             waypoints.AddRange(
//                 buildingManager.GetOwnedBuildings()   // returns List<GameObject>
//                                .Select(go => go.GetComponent<UnitControl>())
//                                .Where(uc => uc != null)
//             );

//         // 1d) Filter: only keep tiles that are either this group’s current tile
//         //     or can store the group (category, ID, capacity)
//         waypoints = waypoints
//             .Where(uc =>
//                 uc == group.unitControl
//                 || uc.CanStoreUnitGroup(group)
//             )
//             .Distinct()
//             .ToList();

//         if (waypoints.Count < 2)
//         {
//             callback(null);
//             yield break;
//         }

//         // 2) Start from the group's tile
//         UnitControl current = group.unitControl;
//         if (current == null)
//         {
//             callback(null);
//             yield break;
//         }

//         // 3) Nearest‐neighbor TSP ordering
//         var ordered = new List<UnitControl>();
//         var remaining = new List<UnitControl>(waypoints);
//         while (remaining.Count > 0)
//         {
//             var next = remaining
//                 .OrderBy(w => Vector3.Distance(current.transform.position, w.transform.position))
//                 .First();
//             ordered.Add(next);
//             remaining.Remove(next);
//             current = next;
//         }

//         // 3d) Stagger each group's loop start so they don't overlap exactly
//         if (ordered.Count > 1)
//         {
//             int offset = Mathf.Abs(group.groupID.GetHashCode()) % ordered.Count;
//             ordered = ordered.Skip(offset).Concat(ordered.Take(offset)).ToList();
//         }

//         // 4) Build forward‑then‑reverse patrol path up to 2× movementSpeed steps
//         var patrol = new UnitPlan(group, UnitPlanType.Patrol);

//         // total steps = 2×movementSpeed (or leftoverMovement if smaller)
//         int maxSteps = Mathf.RoundToInt(group.movementSpeed * 2f);
//         int budget = maxSteps;

//         // build a full list of waypoints: go out, then come back
//         var fullRoute = ordered
//             .Concat(ordered.AsEnumerable().Reverse())
//             .ToList();

//         // walk the fullRoute until we run out of step‑budget
//         foreach (var dest in fullRoute)
//         {
//             if (budget <= 0) break;

//             var segment = AIPathfinder.FindPath(group, current, dest, detectionMultiplier);
//             if (segment != null && segment.Count > 1)
//             {
//                 int take = Mathf.Min(segment.Count - 1, budget);
//                 // add up to 'take' new positions
//                 for (int s = 1; s <= take; s++)
//                     patrol.movementPath.Add(segment[s].transform.position);

//                 budget -= take;
//                 current = dest;
//             }
//             else
//             {
//                 // no path found or already there: jump to next waypoint
//                 current = dest;
//             }
//         }

//         // 5) Compute priority (now passing in both poiManager & aiUnitManager)
//         patrol.priority = AIUnitPatrolPriorityEvaluator.CalculatePatrolPriority(
//             group,
//             ordered,
//             poiManager,
//             aiUnitManager
//         );

//         // —— NEW: reset the timer for the first patrol hop ——
//         if (patrol.movementPath.Count > 0)
//         {
//             Vector3 nextPos = patrol.movementPath[0];
//             UnitControl nextUC = FindUnitControlAtPosition(nextPos);
//             if (nextUC != null)
//                 group.ResetMovementTimer(nextUC);
//         }

//         // compute priority & callback
//         patrol.priority = AIUnitPatrolPriorityEvaluator.CalculatePatrolPriority(
//             group, ordered, poiManager, aiUnitManager
//         );
//         callback(patrol);

//         // 1. save the forward‑then‑reverse ping‑pong loop
//         group.originalPatrolRoute = new List<Vector3>( patrol.movementPath );

//         // 2. flag them as patrolling
//         group.isPatrolling = true;

//         // 3. apply this plan to the group so movementPathPositions gets initialized
//         group.currentPlan = patrol;
//         group.movementPathPositions = new List<Vector3>( patrol.movementPath );
        
//         yield return null;
//     }

//     private static UnitControl FindUnitControlAtPosition(Vector3 pos)
//     {
//         foreach (var uc in GameObject.FindObjectsOfType<UnitControl>())
//         {
//             if (Vector3.Distance(uc.transform.position, pos) < 0.1f)
//                 return uc;
//         }
//         return null;
//     }
// }