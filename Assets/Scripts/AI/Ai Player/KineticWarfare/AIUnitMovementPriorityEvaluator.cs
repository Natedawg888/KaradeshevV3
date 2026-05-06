// using System.Collections.Generic;
// using UnityEngine;
// using System.Linq;

// public static class AIUnitMovementPriorityEvaluator
// {
//     public static int CalculatePotentialPriority(
//         MilitiaUnitGroup group,
//         UnitControl targetTile,
//         float detectionMultiplier,
//         AIPointsOfInterestManager poiManager)
//     {
//         if (targetTile == null || group == null || group.unitControl == null)
//         return 0;

//         UnitControl currentTile = group.unitControl;

//         int nearbyGroupCount = 0;
//         int totalNearbyUnits = 0;

//         // 1) build neighbors safely
//         List<UnitControl> nearbyTiles = new List<UnitControl>();
//         if (currentTile != null)
//             nearbyTiles.AddRange(GetNeighborUnitControls(currentTile, detectionMultiplier));
//         nearbyTiles = nearbyTiles
//             .Where(uc => uc != null)       // drop any nulls
//             .ToList();

//         // 2) include the tile itself
//         nearbyTiles.Add(currentTile);

//         foreach (UnitControl uc in nearbyTiles)
//         {
//             foreach (MilitiaUnitGroup g in uc.GetStoredUnitGroups())
//             {
//                 if (g == group) 
//                     continue;

//                 // only count groups that aren’t already moving
//                 if (g.movementPathPositions == null || g.movementPathPositions.Count == 0)
//                 {
//                     nearbyGroupCount++;
//                     totalNearbyUnits += g.totalUnits;
//                 }
//             }
//         }

//         // Base priority derived from group size.
//         float basePriority = (group.totalUnits + 1) * 5f;
//         // Penalty from crowd congestion at current tile.
//         float crowdPenalty = (nearbyGroupCount * 2f) + (totalNearbyUnits * 2f);
//         float finalScore = basePriority + crowdPenalty;

//         // Get the POI priorities for the target and current unit tile.
//         int targetPOIPriority = GetPOIPriorityForTile(targetTile, poiManager);
//         int currentPOIPriority = GetPOIPriorityForTile(currentTile, poiManager);
        
//         // If the target has a higher POI priority, add a bonus factor (multiplied by 2, adjust as desired).
//         int poiDifference = targetPOIPriority - currentPOIPriority;
//         int poiBoost = Mathf.Max(0, poiDifference) * 2;
//         finalScore += poiBoost;

//         // --- New: Decrease finalScore by the total number of units already present on the target POI.
//         int unitsAtPOI = 0;
//         foreach (MilitiaUnitGroup mg in targetTile.GetStoredUnitGroups())
//         {
//             // Only count units that belong to the same AI player.
//             if (mg.aiPlayerID == group.aiPlayerID)
//             {
//                 unitsAtPOI += mg.totalUnits;
//             }
//         }

//         // Use the aiPlayerID from the group to find the corresponding AIPlayer.
//         AIPlayer[] aiPlayers = GameObject.FindObjectsOfType<AIPlayer>();
//         AIPlayer matchingPlayer = null;
//         foreach (AIPlayer ai in aiPlayers)
//         {
//             if (ai.aiPlayerID == group.aiPlayerID)
//             {
//                 matchingPlayer = ai;
//                 break;
//             }
//         }

//         if (matchingPlayer != null && matchingPlayer.movementTracker != null)
//         {
//             List<MilitiaUnitGroup> movingGroups = matchingPlayer.movementTracker.GetGroupsMovingTo(targetTile);
//             foreach (MilitiaUnitGroup mg in movingGroups)
//             {
//                 unitsAtPOI += mg.totalUnits;
//             }
//         }

        
//         // Define a reduction multiplier.
//         float reductionMultiplier = 10f; // Adjust this value as needed.
//         float crowdReduction = unitsAtPOI * reductionMultiplier;
//         finalScore -= crowdReduction;
//         // ------------------------------------

//         Debug.Log($"[PriorityEvaluator] Pre-evaluation for group {group.groupID} = {finalScore} " +
//                   $"(base: {basePriority}, crowd: {crowdPenalty}, POI boost: {poiBoost}, " +
//                   $"crowd reduction: {crowdReduction})");

//         return Mathf.RoundToInt(finalScore);
//     }

//     private static int GetPOIPriorityForTile(UnitControl tile, AIPointsOfInterestManager poiManager)
//     {
//         if (tile == null || poiManager == null || poiManager.pointsOfInterest == null)
//             return 0;

//         // Compare the tile's GameObject against registered POIs.
//         foreach (var poi in poiManager.pointsOfInterest)
//         {
//             if (poi.tile == tile.gameObject)
//             {
//                 return poi.priority;
//             }
//         }
//         return 0;
//     }

//     private static List<UnitControl> GetNeighborUnitControls(UnitControl currentUC, float detectionMultiplier)
//     {
//         List<UnitControl> neighbors = new List<UnitControl>();
//         if (currentUC == null) return neighbors;

//         BoxCollider box = currentUC.GetComponent<BoxCollider>();
//         if (box == null) return neighbors;

//         Collider[] hits = Physics.OverlapBox(box.bounds.center, box.bounds.extents * detectionMultiplier, currentUC.transform.rotation);
//         foreach (var hit in hits)
//         {
//             var uc = hit.GetComponent<UnitControl>();
//             if (uc != null && uc != currentUC)
//                 neighbors.Add(uc);
//         }

//         return neighbors;
//     }
// }
