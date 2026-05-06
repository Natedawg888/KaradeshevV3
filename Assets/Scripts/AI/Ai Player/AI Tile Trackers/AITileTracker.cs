// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;


// public class AITileTracker : MonoBehaviour
// {
//     [Header("BFS Settings")]
//     // Maximum depth (number of hops) to search outward from the starter tile.
//     public int maxDepth = 3;
//     // Multiplier for the BoxCollider's extents.
//     public float detectionMultiplier = 1.0f;
//     // Fallback detection radius (used if a tile doesn't have a BoxCollider).
//     public float fallbackDetectionRadius = 2f;

//     [Header("Delay Settings")]
//     // Delay (in seconds) before starting the BFS.
//     private float bfsDelay = 0.1f;

//     // This list will store references to all environment tile GameObjects discovered by the BFS.
//     [SerializeField] private List<GameObject> trackedTiles = new List<GameObject>();

//     [Header("Tracked Tile Infos")]
//     // This list shows information about each tracked tile in the Inspector.
//     [SerializeField] private List<TrackedTileInfo> trackedTileInfos = new List<TrackedTileInfo>();

//     // Reference to the AI starter tile (passed from the AIPlayer script).
//     private GameObject starterTile;

//     public void SetStarterTile(GameObject tile)
//     {
//         starterTile = tile;
//         StartCoroutine(DelayedBFS());
//     }

//     private IEnumerator DelayedBFS()
//     {
//         yield return new WaitForSeconds(bfsDelay);
//         PerformBreadthFirstSearch();
//     }

//     public void RemoveTrackedTile(GameObject tile)
//     {

//         if (trackedTiles.Contains(tile))
//         {
//             trackedTiles.Remove(tile);
//             //Debug.Log($"[AITileTracker] Removed {tile.name} from tracked tiles.");
//         }

//         // ✅ Remove from tracked tile info as well
//         trackedTileInfos = GetTrackedTileInfos();
//     }

//     public void PerformBreadthFirstSearch()
//     {
//         trackedTiles.Clear();
//         if (starterTile == null)
//         {
//             //Debug.LogWarning("AITileTracker: Starter tile is not set. Cannot perform BFS.");
//             return;
//         }

//         // Use a queue where each entry holds a tile GameObject and its current depth.
//         Queue<(GameObject tile, int depth)> queue = new Queue<(GameObject, int)>();
//         HashSet<GameObject> visited = new HashSet<GameObject>();

//         // Start from the starter tile at depth 0.
//         queue.Enqueue((starterTile, 0));
//         visited.Add(starterTile);

//         while (queue.Count > 0)
//         {
//             (GameObject currentTile, int depth) = queue.Dequeue();

//             // If not the starter tile (depth > 0), add it to the tracked list.
//             if (depth > 0)
//             {
//                 trackedTiles.Add(currentTile);
//             }

//             // If we haven't reached maxDepth, search for neighbors.
//             if (depth < maxDepth)
//             {
//                 Collider[] hits = null;
//                 // Try to get a BoxCollider from the current tile.
//                 BoxCollider boxCollider = currentTile.GetComponent<BoxCollider>();
//                 if (boxCollider != null)
//                 {
//                     Vector3 center = boxCollider.bounds.center;
//                     Vector3 halfExtents = boxCollider.bounds.extents * detectionMultiplier;
//                     hits = Physics.OverlapBox(center, halfExtents, currentTile.transform.rotation);
//                 }

//                 foreach (Collider hit in hits)
//                 {
//                     GameObject hitObj = hit.gameObject;
//                     if (visited.Contains(hitObj))
//                         continue;

//                     // Only consider objects that have an EnvironmentControl component.
//                     if (hitObj.GetComponent<EnvironmentControl>() != null)
//                     {
//                         visited.Add(hitObj);
//                         queue.Enqueue((hitObj, depth + 1));
//                     }
//                 }
//             }
//         }

//         // Remove any null entries from the tracked list.
//         trackedTiles.RemoveAll(item => item == null);
//         // Update the tracked tile infos so they appear in the Inspector.
//         trackedTileInfos = GetTrackedTileInfos();
//     }

//     public void PerformBreadthSearchFromTile(GameObject discoveredTile)
//     {
//         if (discoveredTile == null)
//         {
//             //Debug.LogWarning("AITileTracker: Provided discoveredTile is null. Cannot perform BFS.");
//             return;
//         }

//         //Debug.Log($"[AITileTracker] 🔍 Starting BFS from newly discovered tile: {discoveredTile.name}");

//         // Create a separate tracking list for new discoveries
//         List<GameObject> newTrackedTiles = new List<GameObject>();

//         // BFS queue setup (newly discovered tile starts at depth 0)
//         Queue<(GameObject tile, int depth)> queue = new Queue<(GameObject, int)>();
//         HashSet<GameObject> visited = new HashSet<GameObject>(); // Ensure no duplicate visits

//         queue.Enqueue((discoveredTile, 0));
//         visited.Add(discoveredTile);

//         while (queue.Count > 0)
//         {
//             (GameObject currentTile, int depth) = queue.Dequeue();

//             // Log tile being processed
//             //Debug.Log($"[AITileTracker] Processing tile: {currentTile.name} at depth {depth}");

//             // Stop expansion if max depth reached
//             if (depth >= maxDepth)
//             {
//                 //Debug.Log($"[AITileTracker] ❌ Reached max depth at tile: {currentTile.name}");
//                 continue;
//             }

//             // Get all adjacent tiles
//             Collider[] hits = Physics.OverlapBox(
//                 currentTile.transform.position,
//                 currentTile.GetComponent<BoxCollider>() != null
//                     ? currentTile.GetComponent<BoxCollider>().bounds.extents * detectionMultiplier
//                     : Vector3.one * fallbackDetectionRadius,
//                 Quaternion.identity
//             );

//             if (hits.Length == 0)
//             {
//                 // Fallback: use OverlapSphere if OverlapBox fails
//                 hits = Physics.OverlapSphere(currentTile.transform.position, fallbackDetectionRadius);
//             }

//             //Debug.Log($"[AITileTracker] Found {hits.Length} adjacent tiles around {currentTile.name}");

//             foreach (Collider hit in hits)
//             {
//                 GameObject hitObj = hit.gameObject;

//                 // Skip if already visited in this BFS run
//                 if (visited.Contains(hitObj))
//                 {
//                     //Debug.Log($"[AITileTracker] Skipping {hitObj.name} (Already visited in BFS)");
//                     continue;
//                 }

//                 // Ensure the tile has an EnvironmentControl component
//                 if (hitObj.GetComponent<EnvironmentControl>() != null)
//                 {
//                     visited.Add(hitObj);
//                     queue.Enqueue((hitObj, depth + 1));
//                     newTrackedTiles.Add(hitObj);
//                     //Debug.Log($"[AITileTracker] ✅ Added {hitObj.name} to BFS queue at depth {depth + 1}");
//                 }
//             }
//         }

//         // **Now merge only newly discovered tiles into `trackedTiles`**
//         int newTilesAdded = 0;
//         foreach (GameObject newTile in newTrackedTiles)
//         {
//             if (!trackedTiles.Contains(newTile))
//             {
//                 trackedTiles.Add(newTile);
//                 newTilesAdded++;
//                 //Debug.Log($"[AITileTracker] 🔥 Added new tile to trackedTiles: {newTile.name}");
//             }
//         }

//         //Debug.Log($"[AITileTracker] ✅ BFS completed. Added {newTilesAdded} new tiles. Total tracked tiles: {trackedTiles.Count}");

//         // Update tile information
//         trackedTileInfos = GetTrackedTileInfos();
//     }

//     public List<GameObject> GetTrackedTiles()
//     {
//         return trackedTiles;
//     }

//     public GameObject GetTileGameObjectByPosition(Vector3 position)
//     {
//         foreach (GameObject tile in trackedTiles) // trackedTiles is assumed to be your list of tile objects
//         {
//             if (tile.transform.position == position)
//             {
//                 return tile;
//             }
//         }
//         return null;
//     }

//     public List<GameObject> GetNeighboringTiles(GameObject tile)
//     {
//         List<GameObject> neighboringTiles = new List<GameObject>();

//         if (tile == null)
//         {
//             //Debug.LogWarning("[AITileTracker] GetNeighboringTiles called with null tile.");
//             return neighboringTiles;
//         }

//         Collider[] hits;
//         BoxCollider boxCollider = tile.GetComponent<BoxCollider>();

//         if (boxCollider != null)
//         {
//             Vector3 center = boxCollider.bounds.center;
//             Vector3 halfExtents = boxCollider.bounds.extents * detectionMultiplier;
//             hits = Physics.OverlapBox(center, halfExtents, tile.transform.rotation);
//         }
//         else
//         {
//             // Fallback to OverlapSphere if no BoxCollider is found
//             hits = Physics.OverlapSphere(tile.transform.position, fallbackDetectionRadius);
//         }

//         foreach (Collider hit in hits)
//         {
//             GameObject hitObj = hit.gameObject;
//             if (hitObj != tile && hitObj.GetComponent<EnvironmentControl>() != null)
//             {
//                 neighboringTiles.Add(hitObj);
//             }
//         }

//         //Debug.Log($"[AITileTracker] Found {neighboringTiles.Count} neighbors for {tile.name}");
//         return neighboringTiles;
//     }

//     [System.Serializable]
//     public struct TrackedTileInfo
//     {
//         public EnvironmentType environmentType;
//         public int discoveryTurnsRequired;
//         public float initialDiscoveryFailureChance;
//         public int gatheringTurnsRequired;
//         public float initialGatheringFailureChance;
//     }

//     public List<TrackedTileInfo> GetTrackedTileInfos()
//     {
//         List<TrackedTileInfo> infos = new List<TrackedTileInfo>();
//         foreach (GameObject tileObj in trackedTiles)
//         {
//             if (tileObj == null)
//                 continue;
//             EnvironmentControl envControl = tileObj.GetComponent<EnvironmentControl>();
//             if (envControl != null)
//             {
//                 TrackedTileInfo info = new TrackedTileInfo()
//                 {
//                     environmentType = envControl.environmentType,
//                     discoveryTurnsRequired = envControl.discoveryTurnsRequired,
//                     initialDiscoveryFailureChance = envControl.initialDiscoveryFailureChance,
//                     gatheringTurnsRequired = envControl.gatheringTurnsRequired,
//                     initialGatheringFailureChance = envControl.initialGatheringFailureChance
//                 };
//                 infos.Add(info);
//             }
//         }
//         return infos;
//     }
// }