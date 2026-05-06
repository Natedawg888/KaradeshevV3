// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;

// public class AIResourceTileTracker : MonoBehaviour
// {
//     [Header("Tracked Resource Tiles")]
//     // Stores resource data grouped by tile.
//     [SerializeField] private List<TrackedResourceTile> resourceTiles = new List<TrackedResourceTile>();

//     // Reference to the AITileTracker component
//     private AITileTracker aiTileTracker;

//     private void Start()
//     {
//         // Get the AITileTracker component on the same GameObject.
//         aiTileTracker = GetComponent<AITileTracker>();
//         if (aiTileTracker == null)
//         {
//             //Debug.LogError("AIResourceTileTracker: No AITileTracker component found on the same GameObject.");
//             return;
//         }

//         // Start tracking resources after a short delay to allow BFS to complete.
//         StartCoroutine(DelayedResourceTracking());
//     }

//     private IEnumerator DelayedResourceTracking()
//     {
//         yield return new WaitForSeconds(0.1f);
//         GatherResourceData();
//     }

//     public void GatherResourceData()
//     {
//         resourceTiles.Clear();

//         // Get all tracked environment tiles from the AITileTracker
//         List<GameObject> trackedTiles = aiTileTracker.GetTrackedTiles();

//         foreach (GameObject tileObj in trackedTiles)
//         {
//             if (tileObj == null)
//                 continue;

//             EnvironmentControl envControl = tileObj.GetComponent<EnvironmentControl>();
//             if (envControl != null && envControl.resources != null && envControl.resources.Count > 0)
//             {
//                 // Create a new tracked tile entry
//                 TrackedResourceTile trackedTile = new TrackedResourceTile
//                 {
//                     environmentType = envControl.environmentType,
//                     tilePosition = tileObj.transform.position,
//                     resources = new List<ResourceData>()
//                 };

//                 // Add each resource to the tracked tile entry
//                 foreach (ResourceAmount resource in envControl.resources)
//                 {
//                     ResourceData resourceData = new ResourceData
//                     {
//                         resourceID = resource.resourceID,
//                         currentAmount = resource.currentAmount,
//                     };

//                     trackedTile.resources.Add(resourceData);
//                 }

//                 // Store the tracked tile with its resources
//                 resourceTiles.Add(trackedTile);
//             }
//         }
//     }

//     public List<TrackedResourceTile> GetResourceTiles()
//     {
//         return resourceTiles;
//     }

//     public void UpdateResourceTracking()
//     {
//         GatherResourceData();
//     }

//     [System.Serializable]
//     public class TrackedResourceTile
//     {
//         public EnvironmentType environmentType;
//         public Vector3 tilePosition;
//         public List<ResourceData> resources;
//     }

//     [System.Serializable]
//     public class ResourceData
//     {
//         public string resourceID;
//         public int currentAmount;
//     }

//     public List<GameObject> GetGatherableTilesByResource(string resourceID)
//     {
//         List<GameObject> gatherableTiles = new List<GameObject>();

//         foreach (TrackedResourceTile trackedTile in resourceTiles)
//         {
//             foreach (ResourceData resource in trackedTile.resources)
//             {
//                 if (resource.resourceID == resourceID && resource.currentAmount > 0)
//                 {
//                     // Convert tile position back to a GameObject (ensure you have a way to map positions back to objects)
//                     GameObject tileObj = aiTileTracker.GetTileGameObjectByPosition(trackedTile.tilePosition);
//                     if (tileObj != null)
//                     {
//                         gatherableTiles.Add(tileObj);
//                     }
//                 }
//             }
//         }

//         return gatherableTiles;
//     }
// }