// using System;
// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
// using System.Linq;

// [System.Serializable]
// public class PointOfInterest
// {
//     public GameObject tile;
//     public int priority;
// }

// public class AIPointsOfInterestManager : MonoBehaviour
// {
//     [Header("References")]
//     public AIBuildingManager aiBuildingManager;
//     public AITileTracker aiTileTracker;
//     public AITileDiscoveryManager aiTileDiscoveryManager;
//     public AIResourcePriorityCalculator aiResourcePriorityCalculator;

//     public List<PointOfInterest> pointsOfInterest = new List<PointOfInterest>();

//     public void UpdatePointsOfInterest()
//     {
//         pointsOfInterest.Clear();

//         if (aiTileTracker == null || aiTileDiscoveryManager == null || aiBuildingManager == null)
//         {
//             Debug.LogWarning("[AIPointsOfInterestManager] Missing references.");
//             return;
//         }

//         AIPlayer thisAI = GetComponentInParent<AIPlayer>();
//         if (thisAI == null)
//         {
//             Debug.LogError("[AIPointsOfInterestManager] Could not find AIPlayer.");
//             return;
//         }

//         List<GameObject> trackedTiles = aiTileTracker.GetTrackedTiles();
//         List<GameObject> discoveredTiles = aiTileDiscoveryManager.GetDiscoveredTiles();
//         List<AITileDiscoveryManager.DiscoveryProcess> activeDiscoveryProcesses
//             = aiTileDiscoveryManager.GetBeingDiscoveredTiles();

//         HashSet<GameObject> allTiles = new HashSet<GameObject>(trackedTiles);

//         if (thisAI.StarterTileInstance != null)
//             allTiles.Add(thisAI.StarterTileInstance);

//         List<GameObject> ownedBuildings = aiBuildingManager.GetOwnedBuildings();
//         foreach (GameObject building in ownedBuildings)
//         {
//             if (building != null)
//                 allTiles.Add(building);
//         }

//         // ✅ Temporary dictionary to hold tile → priority
//         Dictionary<GameObject, int> tilePriorityMap = new Dictionary<GameObject, int>();

//         foreach (GameObject tile in allTiles)
//         {
//             if (tile == null)
//                 continue;

//             EnvironmentControl env = tile.GetComponent<EnvironmentControl>();
//             TileControl tileControl = tile.GetComponent<TileControl>();
//             if (tileControl == null)
//                 continue;

//             int priority = 0;

//             // Skip completely undiscovered environment tiles
//             if (tileControl.tileType == TileType.Environment && env != null && !env.isDiscovered && !env.isAIDiscovered)
//                 continue;

//             // --- Starter Tile ---
//             if (tile == thisAI.StarterTileInstance)
//                 priority += 50;

//             // --- Environment Tiles ---
//             if (tileControl.tileType == TileType.Environment && env != null)
//             {
//                 if (env.isAIDiscovered && !discoveredTiles.Contains(tile))
//                 {
//                     priority += 50;
//                 }
//                 if (env.isBeingAIDiscovered 
//                 && !activeDiscoveryProcesses.Any(proc => proc.tile == tile))
//                 {
//                     float aggressionMultiplier = Mathf.Lerp(10, 200, (thisAI.aggressionLevel - 1) / 19f);
//                     priority += Mathf.RoundToInt(aggressionMultiplier);
//                 }

//                 // ai towards Player
//                 if (env.isDiscovered)
//                 {
//                     float aggressionMultiplier = Mathf.Lerp(10, 200, (thisAI.aggressionLevel - 1) / 19f);
//                     priority += Mathf.RoundToInt(aggressionMultiplier);
//                 }
//                 if (env.isBeingDiscovered)
//                 {
//                     float aggressionMultiplier = Mathf.Lerp(10, 200, (thisAI.aggressionLevel - 1) / 19f);
//                     priority += Mathf.RoundToInt(aggressionMultiplier);
//                 }
//                 if (env.isBeingGathered)
//                 {
//                     float aggressionMultiplier = Mathf.Lerp(10, 200, (thisAI.aggressionLevel - 1) / 19f);
//                     priority += Mathf.RoundToInt(aggressionMultiplier);
//                 }
//             }

//             // --- Building or AiBuilding Tiles ---
//             if (tileControl.tileType == TileType.Building || 
//             tileControl.tileType == TileType.AiBuilding)
//             {
//                 AIBuildingControl building = tile.GetComponent<AIBuildingControl>();
//                 if (building != null)
//                 {
//                     bool isEnemyOwned = building.aiOwner != null && building.aiOwner != thisAI;

//                     if (building.aiOwner == thisAI)
//                         priority += 20;

//                     if (tileControl.tileType == TileType.AiBuilding)
//                     {
//                         if (isEnemyOwned)
//                         {
//                             float aggressionMultiplier = Mathf.Lerp(10, 200, (thisAI.aggressionLevel - 1) / 19f);
//                             priority += Mathf.RoundToInt(aggressionMultiplier);
//                         }

//                         if (building.isUnderAttack && building.aiOwner == thisAI)
//                             priority += 300;
//                     }
//                 }
//                 else
//                 {
//                     float aggressionMultiplier = Mathf.Lerp(10, 200, (thisAI.aggressionLevel - 1) / 19f);
//                     priority += Mathf.RoundToInt(aggressionMultiplier);
//                 }
//             }

//             if (priority > 0)
//             {
//                 tilePriorityMap[tile] = priority;
//                 Debug.Log($"[POI Manager] Added {tile.name} as POI with priority {priority}");
//             }
//         }

//         // ✅ Sort by priority descending and update the pointsOfInterest list
//         pointsOfInterest = tilePriorityMap
//         .OrderByDescending(pair => pair.Value)
//         .Select(pair => new PointOfInterest
//         {
//             tile = pair.Key,
//             priority = pair.Value
//         })
//         .ToList();
//     }
// }