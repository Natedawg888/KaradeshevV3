// using System.Collections.Generic;
// using UnityEngine;

// public static class AIIdlePlanEvaluator
// {
//     public static (UnitControl poiControl, int priority) GetBestIdleTargetWithPriority(MilitiaUnitGroup group, AIPointsOfInterestManager poiManager, float idleDistanceThreshold, float neighborDetectionMultiplier)
//     {
//         UnitControl currentTile = AIUnitIdlePlanner.GetCurrentUnitControl(group);
//         if (currentTile == null || poiManager == null || poiManager.pointsOfInterest.Count == 0)
//             return (null, -1);

//         GameObject closestPOI = null;
//         float minDistance = Mathf.Infinity;

//         foreach (PointOfInterest poi in poiManager.pointsOfInterest)
//         {
//             if (poi.tile == null) continue;
//             float dist = Vector3.Distance(currentTile.transform.position, poi.tile.transform.position);
//             if (dist < minDistance)
//             {
//                 minDistance = dist;
//                 closestPOI = poi.tile;
//             }
//         }

//         if (closestPOI == null) return (null, -1);

//         UnitControl poiControl = closestPOI.GetComponent<UnitControl>();
//         if (poiControl == null) return (null, -1);

//         int priority = EvaluateIdlePlanPriority(poiControl, currentTile, idleDistanceThreshold, neighborDetectionMultiplier);
//         if (priority < 0) return (null, -1);

//         return (poiControl, priority);
//     }

//     public static int EvaluateIdlePlanPriority(UnitControl poiControl, UnitControl currentTile, float idleDistanceThreshold, float neighborDetectionMultiplier)
//     {
//         float minDistance = Vector3.Distance(currentTile.transform.position, poiControl.transform.position);
//         if (minDistance > idleDistanceThreshold)
//             return -1;

//         int totalNearbyUnits = 0;
//         int nearbyGroupCount = 0;

//         List<MilitiaUnitGroup> poiGroups = poiControl.GetStoredUnitGroups();
//         foreach (MilitiaUnitGroup g in poiGroups)
//         {
//             if (g.movementPathPositions == null || g.movementPathPositions.Count == 0)
//             {
//                 totalNearbyUnits += g.totalUnits;
//                 nearbyGroupCount++;
//             }
//         }

//         List<UnitControl> neighborControls = AIUnitIdlePlanner.GetNeighborUnitControls(poiControl, neighborDetectionMultiplier);
//         foreach (UnitControl nc in neighborControls)
//         {
//             foreach (MilitiaUnitGroup g in nc.GetStoredUnitGroups())
//             {
//                 if (g.movementPathPositions == null || g.movementPathPositions.Count == 0)
//                 {
//                     totalNearbyUnits += g.totalUnits;
//                     nearbyGroupCount++;
//                 }
//             }
//         }

//         if (poiGroups.Count + neighborControls.Count < 1)
//             return -1;

//         float distanceFactor = Mathf.Max(0f, 100f - (minDistance * 10f));
//         float crowdPenalty = (nearbyGroupCount * 10f) + (totalNearbyUnits * 0.5f);
//         return Mathf.RoundToInt(distanceFactor - crowdPenalty);
//     }
// }
