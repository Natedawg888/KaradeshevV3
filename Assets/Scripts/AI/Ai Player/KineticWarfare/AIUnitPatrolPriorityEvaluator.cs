// using System.Collections.Generic;
// using UnityEngine;
// using System.Linq;

// public static class AIUnitPatrolPriorityEvaluator
// {
//     public static int CalculatePatrolPriority(
//         MilitiaUnitGroup   group,
//         List<UnitControl>  patrolWaypoints,
//         AIPointsOfInterestManager poiManager,
//         AIUnitManager      aiUnitManager)
//     {
//         if (group == null 
//             || patrolWaypoints == null 
//             || patrolWaypoints.Count == 0
//             || aiUnitManager == null)
//             return 0;

//         // 1) Base priority from group size
//         float basePriority = (group.totalUnits + 1) * 5f;

//         // 2) Bonus for friendly units already on POIs
//         int unitsOnPOIs = 0;
//         if (poiManager != null)
//         {
//             foreach (var poi in poiManager.pointsOfInterest)
//             {
//                 var uc = poi.tile.GetComponent<UnitControl>();
//                 if (uc == null) continue;
//                 foreach (var mg in uc.GetStoredUnitGroups())
//                     if (mg.aiPlayerID == group.aiPlayerID)
//                         unitsOnPOIs += mg.totalUnits;
//             }
//         }
//         float poiUnitBonus = unitsOnPOIs * 10f;

//         // 3) Bonus if no other AI group is currently patrolling
//         bool anyOthersPatrol =
//             aiUnitManager.GetAIUnitGroups()
//                          .Any(mg =>
//                               mg.aiPlayerID == group.aiPlayerID
//                            && mg.groupID     != group.groupID
//                            && mg.currentPlan != null
//                            && mg.currentPlan.planType == UnitPlanType.Patrol
//                          );
//         float noPatrolBonus = anyOthersPatrol ? 0f : basePriority;

//         float final = basePriority + poiUnitBonus + noPatrolBonus;
//         Debug.Log($"[PatrolPriority] G{group.groupID}: base={basePriority}, poiBonus={poiUnitBonus}, noPatrolBonus={noPatrolBonus} => {final}");
//         return Mathf.RoundToInt(final);
//     }
// }
