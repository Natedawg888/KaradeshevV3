// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;

// public class AIBuildingPlacement : MonoBehaviour
// {
//     private AIPlayer aiPlayer;
//     private AIPlanner aiPlanner;
//     private AIBuildingManager aiBuildingManager;
//     private AIInventoryManager inventoryManager;
//     private AIPopulationManager populationManager;

//     private void Start()
//     {
//         aiPlayer = GetComponentInParent<AIPlayer>();
//         aiBuildingManager = aiPlayer?.GetComponentInChildren<AIBuildingManager>();
//         populationManager = aiPlayer?.GetComponentInChildren<AIPopulationManager>();
//         inventoryManager = aiPlayer?.GetComponentInChildren<AIInventoryManager>();
//         aiPlanner = aiPlayer?.GetComponentInChildren<AIPlanner>();
//     }

//     public void PlaceBuilding(AIPlan buildingPlan)
//     {
//         if (buildingPlan == null || buildingPlan.target == null || buildingPlan.selectedBuilding == null)
//         {
//             //Debug.LogWarning("[AIBuildingPlacement] Invalid building plan or target.");
//             return;
//         }

//         GameObject tile = buildingPlan.target;
//         Building buildingData = buildingPlan.selectedBuilding;

//         // ✅ Spawn the building prefab
//         GameObject buildingInstance = Instantiate(buildingData.buildingPrefab, tile.transform.position, Quaternion.identity);

//         if (buildingInstance == null)
//         {
//             //Debug.LogError("[AIBuildingPlacement] Failed to instantiate building.");
//             return;
//         }

//         // ✅ Try to find a valid rotation
//         if (!TryPlaceBuilding(buildingInstance, tile, buildingData))
//         {
//             //Debug.LogWarning($"[AIBuildingPlacement] Could not place {buildingData.buildingName} after all rotations. Cancelling.");
//             Destroy(buildingInstance);
//             return;
//         }

//         // ✅ Remove `BuildingConstruction` if it exists and replace it with `AIBuildingConstruction`
//         ReplaceBuildingConstructionScript(buildingInstance, buildingData, aiPlayer.aiPlayerID);

//         AttachAIBuildingState(buildingInstance);

//         // ✅ Deduct AI's resources and population
//         ConsumeResources(buildingData);
//         populationManager.UsePopulation(buildingData.requiredPopulation);

//          // ✅ Remove the environment tile before placing the building
//         RemoveAndDestroyEnvironmentTile(tile);

//         //Debug.Log($"[AIBuildingPlacement] AI successfully placed {buildingData.buildingName} at {tile.name}.");
//     }

//     /// **🔹 Replaces `BuildingConstruction` with `AIBuildingConstruction`**
//     private void ReplaceBuildingConstructionScript(GameObject building, Building buildingData, string aiPlayerID)
//     {
//         // ✅ Add `AIBuildingConstruction` and initialize it with AI Player ID
//         AIBuildingConstruction aiConstruction = building.AddComponent<AIBuildingConstruction>();
//         aiConstruction.InitializeBuildingConstruction(buildingData, buildingData.requiredPopulation, aiPlayerID);
//         aiConstruction.StartConstruction();

//         // Remove `BuildingConstruction` if it exists
//         BuildingConstruction playerConstruction = building.GetComponent<BuildingConstruction>();
//         if (playerConstruction != null)
//         {
//             Destroy(playerConstruction);
//         }

//         ConstructionTileSaveable constructionTileSaveable = building.GetComponent<ConstructionTileSaveable>();
//         if (constructionTileSaveable != null)
//         {
//             Destroy(constructionTileSaveable);
//         }

//         //Debug.Log($"[AIBuildingPlacement] Replaced BuildingConstruction with AIBuildingConstruction for {buildingData.buildingName}, AI ID: {aiPlayerID}.");
//     }

//     /// **🔹 Attempts placement with 90-degree rotation checks**
//     private bool TryPlaceBuilding(GameObject building, GameObject tile, Building buildingData)
//     {
//         for (int i = 0; i < 4; i++) // Try 0°, 90°, 180°, 270°
//         {
//             if (ValidatePlacement(building, tile, buildingData))
//             {
//                 // ✅ If valid, apply a final random rotation (0°, 90°, 180°, 270°)
//                 building.transform.Rotate(0, Random.Range(0, 4) * 90, 0);
//                 return true;
//             }

//             // 🔄 Rotate 90 degrees and try again
//             building.transform.Rotate(0, 90, 0);
//         }

//         // ❌ If no valid placement was found, return false
//         return false;
//     }

//     /// **🔹 Checks if AI building placement is valid**
//     private bool ValidatePlacement(GameObject building, GameObject tile, Building buildingData)
//     {
//         Collider buildingCollider = building.GetComponent<Collider>();
//         if (buildingCollider == null)
//         {
//             //Debug.LogError($"[AIBuildingPlacement] {building.name} has NO collider! Placement will always fail.");
//             return false;
//         }

//         // Get all colliders that the building would overlap
//         Collider[] overlappingColliders = Physics.OverlapBox(buildingCollider.bounds.center, buildingCollider.bounds.extents, buildingCollider.transform.rotation);

//         bool isValidPlacement = true;
//         List<TileControl> currentlyDisabledTiles = new List<TileControl>();

//         foreach (Collider col in overlappingColliders)
//         {
//             TileControl tileControl = col.GetComponent<TileControl>();
//             if (tileControl != null)
//             {
//                 EnvironmentControl environmentControl = tileControl.GetComponent<EnvironmentControl>();
//                 if (environmentControl != null)
//                 {
//                     // ✅ Check if the tile’s environment type & size match the building's requirements
//                     if (buildingData.requiredEnvironmentTypes.Contains(environmentControl.environmentType) &&
//                         buildingData.requiredTileSize == environmentControl.tileSize)
//                     {
//                         // ✅ Temporarily disable tile renderer to indicate placement is valid
//                         tileControl.GetComponent<Renderer>().enabled = false;
//                         currentlyDisabledTiles.Add(tileControl);
//                     }
//                     else
//                     {
//                         // ❌ Invalid if any tile does not match the required environment type or tile size
//                         isValidPlacement = false;
//                         break;
//                     }
//                 }
//             }
//         }

//         // ✅ Only allow placement if all overlapping tiles are valid
//         return isValidPlacement && currentlyDisabledTiles.Count > 0;
//     }

//     /// **🔹 Checks if AI has enough resources for the building**
//     private bool HasRequiredResources(Building building)
//     {
//         foreach (var requirement in building.requiredResources)
//         {
//             if (!inventoryManager.HasEnoughResource(requirement.resourceID, requirement.amount))
//             {
//                 return false;
//             }
//         }
//         return true;
//     }

//     /// **🔹 Checks if AI has enough population for the building**
//     private bool HasRequiredPopulation(Building building)
//     {
//         return populationManager.GetAvailablePopulation() >= building.requiredPopulation;
//     }

//     /// **🔹 Deducts resources from AI inventory**
//     private void ConsumeResources(Building building)
//     {
//         foreach (var requirement in building.requiredResources)
//         {
//             inventoryManager.RemoveResource(requirement.resourceID, requirement.amount);
//         }
//     }

//     /// **🔹 Removes the environment tile from discovered/gatherable lists and destroys it**
//     private void RemoveAndDestroyEnvironmentTile(GameObject tile)
//     {
//         if (tile == null)
//         {
//             //Debug.LogWarning("[AIBuildingPlacement] Attempted to remove a null environment tile.");
//             return;
//         }

//         // ✅ Remove tile from discovered tiles
//         AITileDiscoveryManager discoveryManager = aiPlayer.GetComponentInChildren<AITileDiscoveryManager>();
//         if (discoveryManager != null)
//         {
//             discoveryManager.RemoveDiscoveredTile(tile);
//             //Debug.Log($"[AIBuildingPlacement] Removed {tile.name} from discovered tiles.");
//         }

//         // ✅ Remove tile from gatherable tiles
//         AIGatheringManager gatheringManager = aiPlayer.GetComponentInChildren<AIGatheringManager>();
//         if (gatheringManager != null)
//         {
//             gatheringManager.RemoveGatherableTile(tile);
//             //Debug.Log($"[AIBuildingPlacement] Removed {tile.name} from gatherable tiles.");
//         }

//         // ✅ Remove tile from available building tiles
//         AIBuildingManager buildingManager = aiPlayer.GetComponentInChildren<AIBuildingManager>();
//         if (buildingManager != null)
//         {
//             buildingManager.RemoveAvailableBuildingTile(tile);
//             //Debug.Log($"[AIBuildingPlacement] Removed {tile.name} from available building tiles.");
//         }

//         // ✅ Remove tile from AI tile tracker
//         AITileTracker tileTracker = aiPlayer.GetComponentInChildren<AITileTracker>();
//         if (tileTracker != null)
//         {
//             tileTracker.RemoveTrackedTile(tile);
//             //Debug.Log($"[AIBuildingPlacement] Removed {tile.name} from AI tile tracker.");
//         }

//         // ✅ Destroy the tile
//         Destroy(tile);
//         //Debug.Log($"[AIBuildingPlacement] Destroyed environment tile {tile.name}.");
//     }

//     /// **🔹 Attaches `AIBuildingState` and updates its materials**
//     private void AttachAIBuildingState(GameObject building)
//     {
//         if (building == null) return;

//         AIBuildingState aiBuildingState = building.GetComponent<AIBuildingState>();
        
//         // ✅ If the script does not exist, add it
//         if (aiBuildingState == null)
//         {
//             aiBuildingState = building.AddComponent<AIBuildingState>();
//         }

//         // ✅ Update the building material to match AI's color
//         aiBuildingState.SetDiscovered(false, aiPlayer);
//     }
// }