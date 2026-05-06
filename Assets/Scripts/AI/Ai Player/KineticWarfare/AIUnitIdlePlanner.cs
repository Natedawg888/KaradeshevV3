// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;

// public class AIUnitIdlePlanner : MonoBehaviour
// {
//     public static IEnumerator GenerateIdlePlan(MilitiaUnitGroup group, AIPointsOfInterestManager poiManager, float idleDistanceThreshold, float neighborDetectionMultiplier, System.Action<UnitPlan> callback)
//         {
//             if (group.movementPathPositions != null && group.movementPathPositions.Count > 0)
//             {
//                 callback(null);
//                 yield break;
//             }

//             var existing = group.currentPlan;
//             if (existing != null &&
//                 (existing.planType == UnitPlanType.Patrol || 
//                 existing.planType == UnitPlanType.Move))
//             {
//                 Debug.Log($"[Planner] Group {group.groupID} already has a patrol or move plan ({existing.planType}).");
//                 callback(null);
//                 yield break;
//             }

//             UnitControl currentTile = GetCurrentUnitControl(group);
//             if (currentTile == null || poiManager == null || poiManager.pointsOfInterest.Count == 0)
//             {
//                 callback(null);
//                 yield break;
//             }

//             GameObject closestPOI = null;
//             float minDistance = Mathf.Infinity;

//             foreach (PointOfInterest poi in poiManager.pointsOfInterest)
//             {
//                 if (poi.tile == null) continue;
//                 float dist = Vector3.Distance(currentTile.transform.position, poi.tile.transform.position);
//                 if (dist < minDistance)
//                 {
//                     minDistance = dist;
//                     closestPOI = poi.tile;
//                 }
//             }

//             if (closestPOI == null)
//             {
//                 callback(null);
//                 yield break;
//             }

//             UnitControl poiControl = closestPOI.GetComponent<UnitControl>();
//             if (poiControl == null)
//             {
//                 callback(null);
//                 yield break;
//             }

//             int priority = AIIdlePlanEvaluator.EvaluateIdlePlanPriority(poiControl, currentTile, idleDistanceThreshold, neighborDetectionMultiplier);
//             if (priority < 0)
//             {
//                 callback(null);
//                 yield break;
//             }

//             UnitPlan idlePlan = new UnitPlan(group, UnitPlanType.Idle);
//             idlePlan.targetUnitControl = poiControl;
//             idlePlan.priority = priority;

//             group.movementPathPositions = new List<Vector3>();
//             callback(idlePlan);
//             yield return null;
//         }

//         public static UnitControl GetCurrentUnitControl(MilitiaUnitGroup group)
//         {
//             if (group.unitControl != null)
//                 return group.unitControl;

//             foreach (UnitControl uc in FindObjectsOfType<UnitControl>())
//             {
//                 if (uc.GetStoredUnitGroups().Contains(group))
//                     return uc;
//             }
//             return null;
//         }

//         public static List<UnitControl> GetNeighborUnitControls(UnitControl currentUC, float detectionMultiplier)
//         {
//             List<UnitControl> neighbors = new List<UnitControl>();
//             BoxCollider box = currentUC.GetComponent<BoxCollider>();
//             if (box == null) return neighbors;

//             Collider[] hits = Physics.OverlapBox(box.bounds.center, box.bounds.extents * detectionMultiplier, currentUC.transform.rotation);
//             foreach (Collider hit in hits)
//             {
//                 UnitControl uc = hit.GetComponent<UnitControl>();
//                 if (uc != null && uc != currentUC)
//                     neighbors.Add(uc);
//             }
//             return neighbors;
//         }
// }