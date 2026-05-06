// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
// using System.Linq;

// public class AIUnitMovementPlanner : MonoBehaviour
// {
//     public static IEnumerator GenerateMovementPlan(
//     MilitiaUnitGroup group,
//     AIPointsOfInterestManager poiManager,
//     float detectionMultiplier,
//     System.Action<UnitPlan> callback)
//     {
//         // Clear any previously stored movement path.
//         group.movementPathPositions.Clear();

//         // Skip planning if the group already has a movement path.
//         if (group.movementPathPositions != null && group.movementPathPositions.Count > 0)
//         {
//             Debug.Log($"[Planner] Group {group.groupID} already has a movement path. Skipping.");
//             callback(null);
//             yield break;
//         }

//         var existing = group.currentPlan;
//         if (existing != null &&
//             (existing.planType == UnitPlanType.Patrol))
//         {
//             Debug.Log($"[Planner] Group {group.groupID} already has a patrol or move plan ({existing.planType}).");
//             callback(null);
//             yield break;
//         }

//         UnitPlan movementPlan = new UnitPlan(group, UnitPlanType.Move);
//         UnitControl globalTargetTile = GetGlobalTargetTile(poiManager);
//         if (globalTargetTile == null)
//         {
//             Debug.LogWarning("[Planner] No global target tile found.");
//             group.leftoverMovement = 0;
//             group.turnsToMoveStep = 0;
//             callback(null);
//             yield break;
//         }

//         UnitControl currentTile = GetCurrentUnitControl(group);
//         if (currentTile == null)
//         {
//             group.leftoverMovement = 0;
//             group.turnsToMoveStep = 0;
//             Debug.LogWarning($"[Planner] Could not find current tile for group {group.groupID}.");
//             callback(null);
//             yield break;
//         }
        
//         // Use A* pathfinding to determine the best path.
//         List<UnitControl> path = AIPathfinder.FindPath(group, currentTile, globalTargetTile, detectionMultiplier);
//         if (path == null || path.Count == 0)
//         {
//             Debug.Log($"[Planner] No path returned. Clearing movement budget for group {group.groupID}.");
//             group.leftoverMovement = 0;
//             group.turnsToMoveStep = 0;
//             callback(null);
//             yield break;
//         }

//         if(group.leftoverMovement <= 0)
//         {
//             group.leftoverMovement = (int)group.movementSpeed;
//         }
        
//         // Restrict the movement plan length to the unit's allowed steps.
//         int stepsAvailable = Mathf.Min(path.Count - 1, group.leftoverMovement);
//         for (int step = 1; step <= stepsAvailable; step++)
//         {
//             movementPlan.movementPath.Add(path[step].transform.position);
//         }
        
//         // —— NEW: reset the timer for the first hop ——
//         if (movementPlan.movementPath.Count > 0)
//         {
//             // find the UnitControl for the first position
//             Vector3 nextPos = movementPlan.movementPath[0];
//             UnitControl nextUC = FindUnitControlAtPosition(nextPos);
//             if (nextUC != null)
//                 group.ResetMovementTimer(nextUC);
//         }

//         // register & callback as before
//         yield return null;
//         AIPlayer aiPlayer = group.unitControl.GetComponentInParent<AIPlayer>();
//         aiPlayer?.movementTracker?.RegisterMovement(group, globalTargetTile);
//         callback(movementPlan);
//     }
    
//     public static UnitControl GetGlobalTargetTile(AIPointsOfInterestManager poiManager)
//     {
//         UnitControl globalTargetTile = null;
//         UnitControl[] allUnitControls = FindObjectsOfType<UnitControl>();
//         if (poiManager != null && poiManager.pointsOfInterest.Count > 0)
//         {
//             PointOfInterest poi = poiManager.pointsOfInterest.FirstOrDefault();
//             globalTargetTile = poi != null && poi.tile != null ? poi.tile.GetComponent<UnitControl>() : null;
//         }
//         if (globalTargetTile == null && allUnitControls.Length > 0)
//         {
//             globalTargetTile = allUnitControls[Random.Range(0, allUnitControls.Length)];
//         }
//         return globalTargetTile;
//     }
    
//     private static UnitControl GetCurrentUnitControl(MilitiaUnitGroup group)
//     {
//         // If the group already has a reference to its current tile, return it.
//         if (group.unitControl != null)
//             return group.unitControl;
        
//         // Otherwise, look through all UnitControls in the scene.
//         UnitControl[] allUnitControls = FindObjectsOfType<UnitControl>();
//         foreach (UnitControl uc in allUnitControls)
//         {
//             if (uc.GetStoredUnitGroups().Contains(group))
//                 return uc;
//         }
//         return null;
//     }

//     // helper to map a world‐position back to its UnitControl
//     private static UnitControl FindUnitControlAtPosition(Vector3 pos)
//     {
//         foreach (var uc in GameObject.FindObjectsOfType<UnitControl>())
//         {
//             if (Vector3.Distance(uc.transform.position, pos) < 0.1f)
//                 return uc;
//         }
//         return null;
//     }
    
//     private static List<UnitControl> GetNeighborUnitControls(UnitControl currentUC, float detectionMultiplier)
//     {
//         List<UnitControl> neighbors = new List<UnitControl>();
//         BoxCollider boxCollider = currentUC.GetComponent<BoxCollider>();
//         if (boxCollider == null)
//         {
//             Debug.LogWarning($"[AIUnitMovementPlanner] No BoxCollider found on {currentUC.name}");
//             return neighbors;
//         }
//         Vector3 center = boxCollider.bounds.center;
//         Vector3 halfExtents = boxCollider.bounds.extents * detectionMultiplier;
//         Collider[] hits = Physics.OverlapBox(center, halfExtents, currentUC.transform.rotation);
//         foreach (Collider hit in hits)
//         {
//             UnitControl neighborUC = hit.GetComponent<UnitControl>();
//             if (neighborUC != null && neighborUC != currentUC)
//                 neighbors.Add(neighborUC);
//         }
//         return neighbors;
//     }
// }