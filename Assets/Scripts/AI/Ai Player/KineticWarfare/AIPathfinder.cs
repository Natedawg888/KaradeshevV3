// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
// using System.Linq;

// public static class AIPathfinder
// {
//     public static List<UnitControl> FindPath(
//     MilitiaUnitGroup group,
//     UnitControl start,
//     UnitControl goal,
//     float detectionMultiplier)
//     {
//         // Initialize open and closed lists.
//         List<UnitControl> openSet = new List<UnitControl> { start };
//         HashSet<UnitControl> closedSet = new HashSet<UnitControl>();

//         // Dictionaries to track scores and parents.
//         Dictionary<UnitControl, float> gScore = new Dictionary<UnitControl, float>();
//         Dictionary<UnitControl, float> fScore = new Dictionary<UnitControl, float>();
//         Dictionary<UnitControl, UnitControl> cameFrom = new Dictionary<UnitControl, UnitControl>();

//         // Initialize all UnitControls with infinite cost.
//         UnitControl[] allTiles = GameObject.FindObjectsOfType<UnitControl>();
//         foreach (UnitControl tile in allTiles)
//         {
//             gScore[tile] = float.MaxValue;
//             fScore[tile] = float.MaxValue;
//         }
        
//         gScore[start] = 0;
//         fScore[start] = Heuristic(start, goal);
        
//         while (openSet.Count > 0)
//         {
//             // Choose the node from openSet with the lowest fScore.
//             UnitControl current = openSet.OrderBy(x => fScore[x]).First();

//             if (current == goal)
//                 return ReconstructPath(cameFrom, current);
            
//             openSet.Remove(current);
//             closedSet.Add(current);
            
//             // Expand neighbors.
//             foreach (UnitControl neighbor in GetNeighborUnitControls(current, detectionMultiplier))
//             {
//                 if (closedSet.Contains(neighbor))
//                     continue;
//                 // Pass the proper group instead of null.
//                 if (!neighbor.CanStoreUnitGroup(group))
//                     continue;
                
//                 int moveCost = MovementCostCalculator.GetCostForTile(neighbor);
//                 float tentativeG = gScore[current] + moveCost;
                
//                 if (!openSet.Contains(neighbor))
//                 {
//                     openSet.Add(neighbor);
//                 }
//                 else if (tentativeG >= gScore[neighbor])
//                 {
//                     continue;
//                 }
                
//                 cameFrom[neighbor] = current;
//                 gScore[neighbor] = tentativeG;
//                 fScore[neighbor] = gScore[neighbor] + Heuristic(neighbor, goal);
//             }
//         }
        
//         // No path found.
//         return null;
//     }
    
//     private static float Heuristic(UnitControl from, UnitControl to)
//     {
//         // Use Euclidean distance as heuristic. You can modify this to incorporate extra factors.
//         return Vector3.Distance(from.transform.position, to.transform.position);
//     }
    
//     private static List<UnitControl> ReconstructPath(Dictionary<UnitControl, UnitControl> cameFrom, UnitControl current)
//     {
//         List<UnitControl> totalPath = new List<UnitControl> { current };
//         while (cameFrom.ContainsKey(current))
//         {
//             current = cameFrom[current];
//             totalPath.Insert(0, current);
//         }
//         return totalPath;
//     }
    
//     private static List<UnitControl> GetNeighborUnitControls(UnitControl currentUC, float detectionMultiplier)
//     {
//         List<UnitControl> neighbors = new List<UnitControl>();
//         BoxCollider box = currentUC.GetComponent<BoxCollider>();
//         if (box == null)
//         {
//             Debug.LogWarning($"[AIPathfinder] No BoxCollider found on {currentUC.name}");
//             return neighbors;
//         }
        
//         Collider[] hits = Physics.OverlapBox(box.bounds.center, box.bounds.extents * detectionMultiplier, currentUC.transform.rotation);
//         foreach (var hit in hits)
//         {
//             UnitControl uc = hit.GetComponent<UnitControl>();
//             if (uc != null && uc != currentUC)
//                 neighbors.Add(uc);
//         }
        
//         return neighbors;
//     }
// }