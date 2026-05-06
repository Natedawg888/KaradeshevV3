// using System;
// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
// using System.Linq;

// public class AIBuildingManager : MonoBehaviour
// {
//     private AIPlayer aiPlayer;
//     private BuildingManager buildingManager;
//     private AIInventoryManager inventoryManager;
//     private AIPopulationManager populationManager;
//     private AITileDiscoveryManager tileDiscoveryManager;
//     private AITechnologyManager technologyManager;

//     private GameObject aiStarterTile;

//     [Header("AI Building Data")]
//     [SerializeField] private List<GameObject> availableBuildingTiles = new List<GameObject>(); // Tiles where AI can build
//     [SerializeField] private List<GameObject> ownedBuildings = new List<GameObject>(); // Buildings owned by AI

//     [Header("Tracking Buildings Under Construction")]
//     private Dictionary<GameObject, string> buildingsUnderConstruction = new Dictionary<GameObject, string>(); // Tracks buildings currently being constructed

//     [Header("Available AI Buildings (Inspector Debug)")]
//     [SerializeField] private List<Building> availableBuildingsList = new List<Building>(); // Serialized list for inspector display

//     [Header("Building Count Tracking")]
//     private Dictionary<string, int> aiBuildingCounts = new Dictionary<string, int>(); // Tracks AI building counts

//     private void Start()
//     {
//         aiPlayer = GetComponentInParent<AIPlayer>();
//         buildingManager = BuildingManager.Instance;
//         inventoryManager = aiPlayer?.GetComponentInChildren<AIInventoryManager>();
//         technologyManager = aiPlayer?.GetComponentInChildren<AITechnologyManager>();
//         populationManager = aiPlayer?.GetComponentInChildren<AIPopulationManager>();
//         tileDiscoveryManager = aiPlayer?.GetComponentInChildren<AITileDiscoveryManager>();

//         if (buildingManager == null || inventoryManager == null || populationManager == null || tileDiscoveryManager == null)
//         {
//             //Debug.LogWarning("[AIBuildingManager] Missing required components.");
//             return;
//         }

//         RegisterStarterTile();
//         UpdateAvailableBuildings();
//         UpdateAvailableBuildingTiles();
//     }

//     /// **🔹 Removes a tile from available building tiles**
//     public void RemoveAvailableBuildingTile(GameObject tile)
//     {
//         if (tile == null) return;

//         if (availableBuildingTiles.Contains(tile))
//         {
//             availableBuildingTiles.Remove(tile);
//             UpdateAvailableBuildingTiles();
//             //Debug.Log($"[AIBuildingManager] Removed {tile.name} from available building tiles.");
//         }
//     }

//     public List<GameObject> GetOwnedBuildings()
//     {
//         return ownedBuildings;
//     }

//     public void SetStarterTile(GameObject tile)
//     {
//         aiStarterTile = tile;
//     }

//     /// **🔹 Adds the AI's starter tile to the owned buildings list**
//     private void RegisterStarterTile()
//     {
//         if (!ownedBuildings.Contains(aiStarterTile))
//         {
//             ownedBuildings.Add(aiStarterTile);
//             //Debug.Log($"[AIBuildingManager] AI Starter Tile added to owned buildings.");
//         }
//     }

//     /// **🔹 Updates AI's list of available building tiles based on discovered tiles**
//     public void UpdateAvailableBuildingTiles()
//     {
//         availableBuildingTiles.Clear();
//         List<GameObject> discoveredTiles = tileDiscoveryManager.GetDiscoveredTiles();

//         foreach (GameObject tile in discoveredTiles)
//         {
//             EnvironmentControl envControl = tile.GetComponent<EnvironmentControl>();

//             if (envControl == null)
//                 continue;

//             foreach (Building building in availableBuildingsList)
//             {
//                 if (building.requiredEnvironmentTypes.Contains(envControl.environmentType))
//                 {
//                     availableBuildingTiles.Add(tile);
//                     break;
//                 }
//             }
//         }

//         //Debug.Log($"[AIBuildingManager] Updated available building tiles. Count: {availableBuildingTiles.Count}");
//     }

//     /// **🔹 Retrieves the list of available AI buildings**
//     public List<Building> GetAvailableBuildings()
//     {
//         return new List<Building>(availableBuildingsList);
//     }

//     /// **🔹 Retrieves the list of available building tiles**
//     public List<GameObject> GetAvailableBuildingTiles()
//     {
//         return new List<GameObject>(availableBuildingTiles);
//     }


//     /// **🔹 Registers a building under construction**
//     public void RegisterBuildingUnderConstruction(GameObject building, string buildingID)
//     {
//         if (building == null || string.IsNullOrEmpty(buildingID)) return;

//         if (!buildingsUnderConstruction.ContainsKey(building))
//         {
//             buildingsUnderConstruction.Add(building, buildingID);
//             //Debug.Log($"[AIBuildingManager] Registered {buildingID} as under construction.");
//         }
//     }

//     /// **🔹 Removes a completed building from the construction list and registers it as owned**
//     public void CompleteBuildingConstruction(GameObject building)
//     {
//         if (building == null)
//         {
//             //Debug.LogError("[AIBuildingManager] CompleteBuildingConstruction FAILED: Building is NULL!");
//             return;
//         }

//         BuildingSaveable bs = building.GetComponent<BuildingSaveable>();
//         if (bs != null)
//         {
//             Destroy(bs);
//         }

//         // ✅ Ensure this is the **final constructed building**
//         AIBuildingControl aiBuildingControl = building.GetComponent<AIBuildingControl>();
//         if (aiBuildingControl == null)
//         {
//             //Debug.LogError($"[AIBuildingManager] ERROR: {building.name} is missing AIBuildingControl! Construction likely failed.");
//             return;
//         }

//         string buildingID = aiBuildingControl.buildingID;
//         string prefix = buildingID.Substring(0, 3);

//         if (!aiBuildingCounts.ContainsKey(prefix))
//         {
//             aiBuildingCounts[prefix] = 0;
//         }

//         technologyManager.ApplyBuildingUpgradesToNewBuilding(building);

//         aiBuildingCounts[prefix]++;
//         ownedBuildings.Add(building);
//         technologyManager.UpdateAvailableTech();

//         //Debug.Log($"[AIBuildingManager] AI placed {building.name}. Count for {prefix}: {aiBuildingCounts[prefix]}");
//     }

//     /// **🔹 Retrieves AI-available buildings based on level and research**
//     public void UpdateAvailableBuildings()
//     {
//         int aiLevel = aiPlayer.aiLevel;

//         availableBuildingsList.Clear();

//         foreach (Building building in buildingManager.GetAvailableBuildings())
//         {
//             if (building.availableLevels.Contains(aiLevel) && (!building.requiresResearch || building.isResearched))
//             {
//                 availableBuildingsList.Add(building);
//             }
//         }

//         //Debug.Log($"[AIBuildingManager] Retrieved {availableBuildingsList.Count} available buildings for AI level {aiLevel}.");
//     }

//     /// **🔹 Removes an AI building and updates count**
//     public void RemoveAIBuilding(GameObject building)
//     {
//         if (building == null) return;

//         AIBuildingControl aiBuildingControl = building.GetComponent<AIBuildingControl>();
//         if (aiBuildingControl == null) return;

//         string prefix = aiBuildingControl.buildingID.Substring(0, 3);

//         if (aiBuildingCounts.ContainsKey(prefix) && aiBuildingCounts[prefix] > 0)
//         {
//             aiBuildingCounts[prefix]--;
//         }

//         ownedBuildings.Remove(building);
//         technologyManager.UpdateAvailableTech();
        
//         //Debug.Log($"[AIBuildingManager] Removed {building.name}. Count for {prefix}: {aiBuildingCounts[prefix]}");
//     }

//     /// **🔹 Checks if AI can build a certain building (based on limit)**
//     public bool CanBuild(string buildingID, int maxLimit)
//     {
//         string prefix = buildingID.Substring(0, 3);
//         return !aiBuildingCounts.ContainsKey(prefix) || aiBuildingCounts[prefix] < maxLimit;
//     }

//     public Dictionary<GameObject, string> GetBuildingsUnderConstruction()
//     {
//         return new Dictionary<GameObject, string>(buildingsUnderConstruction);
//     }

//     public void RemoveBuildingUnderConstruction(GameObject building)
//     {
//         if (buildingsUnderConstruction.ContainsKey(building))
//         {
//             buildingsUnderConstruction.Remove(building);
//             //Debug.Log($"[AIBuildingManager] Removed {building.name} from buildings under construction.");
//         }
//     }

//     /// **🔹 Returns the number of AI buildings with a given 3-character prefix**
//     public int GetBuildingCount(string prefix)
//     {
//         if (aiBuildingCounts.ContainsKey(prefix))
//         {
//             return aiBuildingCounts[prefix];
//         }
//         return 0;
//     }

//     public bool IsBuildingUnderConstruction(string buildingPrefix)
//     {
//         foreach (var buildingID in buildingsUnderConstruction.Values)
//         {
//             if (buildingID.StartsWith(buildingPrefix))
//             {
//                 return true;
//             }
//         }
//         return false;
//     }

//     /// **🔹 Unlocks new buildings for AI without modifying BuildingManager**
//     public void ApplyBuildingUnlock(WorldUpgrade upgrade)
//     {
//         if (upgrade.buildingIDs == null || upgrade.buildingIDs.Count == 0) 
//             return;

//         int unlockedCount = 0;
//         foreach (string buildingID in upgrade.buildingIDs)
//         {
//             // Retrieve the building from the global BuildingManager.
//             Building building = BuildingManager.Instance.GetBuildingByID(buildingID);
//             if (building != null && !availableBuildingsList.Contains(building))
//             {
//                 // Instead of setting building.isResearched (which affects the global instance),
//                 // simply add the building to the AI's local availableBuildingsList.
//                 availableBuildingsList.Add(building);
//                 unlockedCount++;
//             }
//         }
//     }

//     private GameObject FindObjectByID(string id)
//     {
//         foreach (var obj in FindObjectsOfType<GameObject>())
//         {
//             if (obj.GetInstanceID().ToString() == id)
//                 return obj;
//         }
//         return null;
//     }

//     public AIBuildingManagerSaveData SaveState()
//     {
//         AIBuildingManagerSaveData data = new AIBuildingManagerSaveData();

//         // 1) Save owned buildings
//         foreach (GameObject buildingObj in ownedBuildings)
//         {
//             // Skip the starter tile
//             if (buildingObj == aiStarterTile)
//             continue;

//             if (buildingObj == null) continue;
            
//             AIBuildingTileData tileData = new AIBuildingTileData();
            
//             // Save the actual GameObject's name without "(Clone)"
//             tileData.prefabName = buildingObj.name.Replace("(Clone)", "").Trim();
            
//             tileData.position = buildingObj.transform.position;
//             tileData.rotation = buildingObj.transform.rotation;
            
//             // Save from AIBuildingControl
//             AIBuildingControl aiBControl = buildingObj.GetComponent<AIBuildingControl>();
//             if (aiBControl != null)
//             {
//                 tileData.aiBuildingID = aiBControl.buildingID;
//                 tileData.health = aiBControl.health;
//                 tileData.healthSliderValue = (aiBControl.healthSlider != null ? aiBControl.healthSlider.value : 0f);
//                 tileData.degenerationAmount = aiBControl.degenerationAmount;
//                 tileData.degenerationIntervalTurns = aiBControl.degenerationIntervalTurns;
//                 tileData.peopleRequiredForRepair = aiBControl.peopleRequiredForRepair;
//                 tileData.repairPercentage = aiBControl.repairPercentage;
//             }
            
//             // Save AI building state
//             AIBuildingState aiBState = buildingObj.GetComponent<AIBuildingState>();
//             if (aiBState != null)
//             {
//                 tileData.isDiscovered = aiBState.isDiscovered;
//                 tileData.undiscoveredAIMaterialName = aiBState.undiscoveredAIMaterial != null ? aiBState.undiscoveredAIMaterial.name : "";
//                 tileData.glowingUndiscoveredAIMaterialName = aiBState.glowingUndiscoveredAIMaterial != null ? aiBState.glowingUndiscoveredAIMaterial.name : "";
//             }
            
//             // Save the owner from the AIPlayer.
//             if (aiPlayer != null)
//             {
//                 tileData.ownerID = aiPlayer.aiPlayerID;
//             }
//             else
//             {
//                 tileData.ownerID = "Unknown";
//             }

//             CraftingBuildingControl craftingControl = buildingObj.GetComponent<CraftingBuildingControl>();
//             if (craftingControl != null)
//                 tileData.craftingBuildingState = JsonUtility.ToJson(craftingControl.SaveState());
            
//             HealthBuildingControl healthControl = buildingObj.GetComponent<HealthBuildingControl>();
//             if (healthControl != null)
//                 tileData.healthBuildingState = JsonUtility.ToJson(healthControl.SaveState());
            
//             ProductionBuildingControl productionControl = buildingObj.GetComponent<ProductionBuildingControl>();
//             if (productionControl != null)
//                 tileData.productionBuildingState = JsonUtility.ToJson(productionControl.SaveState());
            
//             ShelterControl shelterControl = buildingObj.GetComponent<ShelterControl>();
//             if (shelterControl != null)
//                 tileData.shelterControlState = JsonUtility.ToJson(shelterControl.SaveState());
            
//             StorageBuildingControl storageControl = buildingObj.GetComponent<StorageBuildingControl>();
//             if (storageControl != null)
//                 tileData.storageBuildingState = JsonUtility.ToJson(storageControl.SaveState());

//             data.ownedBuildings.Add(tileData);
//         }
//         // 2) Save building count tracking (convert dictionary to two parallel lists)
//         foreach (var kvp in aiBuildingCounts)
//         {
//             data.buildingPrefixes.Add(kvp.Key);
//             data.buildingCounts.Add(kvp.Value);
//         }

//         // 3) Save each construction site
//         foreach (var kvp in buildingsUnderConstruction)
//         {
//             GameObject constructionGO = kvp.Key;
//             // Skip if the construction GameObject is already destroyed (null)
//             if (constructionGO == null)
//                 continue;

//             string buildingID = kvp.Value;
//             AIBuildingConstruction construction = constructionGO.GetComponent<AIBuildingConstruction>();
//             if (construction != null)
//             {
//                 AIBuildingConstructionData cData = new AIBuildingConstructionData();

//                 cData.prefabName = constructionGO.name.Replace("(Clone)", "").Trim();
//                 cData.aiBuildingID = buildingID;
//                 cData.position = constructionGO.transform.position;
//                 cData.rotation = constructionGO.transform.rotation;
//                 cData.currentTurn = construction.GetCurrentTurn();
//                 cData.turnsToComplete = construction.GetTurnsToComplete();
//                 cData.populationUsed = construction.GetPopulationUsed();
//                 cData.aiPlayerID = construction.GetAIPlayerID();
//                 // etc.

//                 data.buildingConstructionSites.Add(cData);
//             }
//         }

//         return data;
//     }

//     public void LoadState(AIBuildingManagerSaveData data)
//     {
//         if (data == null) return;

//         ownedBuildings.Clear();
//         buildingsUnderConstruction.Clear();
//         aiBuildingCounts.Clear();

//         GameObject[] allPrefabs = Resources.LoadAll<GameObject>("BuildingTiles");

//         foreach (AIBuildingTileData tileData in data.ownedBuildings)
//         {
//             // 1) Instantiate the “player” prefab
//             GameObject prefab = allPrefabs.FirstOrDefault(p =>
//                 p.name.Equals(tileData.prefabName, StringComparison.Ordinal));
//             if (prefab == null)
//             {
//                 Debug.LogWarning($"[AIBuildingManager] Could not find prefab '{tileData.prefabName}'.");
//                 continue;
//             }

//             GameObject newBuilding = Instantiate(prefab, tileData.position, tileData.rotation);

//             // 2) Grab any existing BuildingControl + BuildingSaveable
//             BuildingControl oldControl = newBuilding.GetComponent<BuildingControl>();
//             BuildingSaveable buildingSaveable = newBuilding.GetComponent<BuildingSaveable>();

//             // 3) If we do find an old BuildingControl, convert it to AI
//             if (oldControl != null)
//             {
//                 // Create or get an AIBuildingControl
//                 AIBuildingControl aiBControl = newBuilding.GetComponent<AIBuildingControl>();
//                 if (aiBControl == null) 
//                     aiBControl = newBuilding.AddComponent<AIBuildingControl>();

//                 // Copy data from oldControl to aiBControl
//                 aiBControl.buildingID                = oldControl.buildingID;
//                 aiBControl.uniqueInstanceID          = oldControl.uniqueInstanceID;
//                 aiBControl.buildingType              = oldControl.buildingType;
//                 aiBControl.buildingCanvas            = oldControl.buildingCanvas;
//                 aiBControl.lights                    = oldControl.lights;
//                 aiBControl.health                    = oldControl.health;
//                 aiBControl.healthSlider              = oldControl.healthSlider;
//                 aiBControl.degenerationAmount        = oldControl.degenerationAmount;
//                 aiBControl.degenerationIntervalTurns = oldControl.degenerationIntervalTurns;
//                 aiBControl.damagedInstance           = oldControl.DamagedInstance;
//                 aiBControl.destroyedInstance         = oldControl.DestroyedInstance;
//                 aiBControl.damagedPrefab             = oldControl.damagedPrefab;
//                 aiBControl.destroyedPrefab           = oldControl.destroyedPrefab;
//                 aiBControl.damagedIcon               = oldControl.damagedIcon;
//                 aiBControl.destroyedIcon             = oldControl.destroyedIcon;
//                 aiBControl.shouldForceEnvironment    = oldControl.shouldForceEnvironment;
//                 aiBControl.destroyedClearTurns       = oldControl.destroyedClearTurns;
//                 aiBControl.peopleRequiredForRepair   = oldControl.peopleRequiredForRepair;
//                 aiBControl.repairPercentage          = oldControl.repairPercentage;
//                 aiBControl.environmentBaseTilePrefab = oldControl.environmentBaseTilePrefab;
//                 aiBControl.forcedEnvironmentType     = oldControl.forcedEnvironmentType;
//                 aiBControl.availableTechnologyIDs    = new List<string>(oldControl.availableTechnologyIDs);

//                 // Assign this AI's owner
//                 aiBControl.aiOwner = aiPlayer;

//                 // Remove the old control scripts
//                 Destroy(oldControl);
//                 if (buildingSaveable != null) Destroy(buildingSaveable);
//             }
//             else
//             {
//                 // If no BuildingControl was found, ensure there's still an AIBuildingControl
//                 AIBuildingControl aiBControl = newBuilding.GetComponent<AIBuildingControl>();
//                 if (aiBControl == null) 
//                     aiBControl = newBuilding.AddComponent<AIBuildingControl>();
                
//                 // Minimal data assignment, or any extra logic...
//                 aiBControl.buildingID = tileData.aiBuildingID;
//                 aiBControl.aiOwner    = aiPlayer;
//             }

//             // 4) Now also apply anything that was specifically saved for the AI building
//             AIBuildingControl finalAIControl = newBuilding.GetComponent<AIBuildingControl>();
//             finalAIControl.health                 = tileData.health;
//             finalAIControl.degenerationAmount     = tileData.degenerationAmount;
//             finalAIControl.degenerationIntervalTurns = tileData.degenerationIntervalTurns;
//             finalAIControl.peopleRequiredForRepair   = tileData.peopleRequiredForRepair;
//             finalAIControl.repairPercentage          = tileData.repairPercentage;

//             // 5) If you want the building to show AI-colored materials:
//             AIBuildingState aiBState = newBuilding.GetComponent<AIBuildingState>();
//             if (aiBState == null) aiBState = newBuilding.AddComponent<AIBuildingState>();
//             aiBState.UpdateBuildingMaterialForAI(aiPlayer);


//             CraftingBuildingControl craftingControl = newBuilding.GetComponent<CraftingBuildingControl>();
//             if (craftingControl != null && !string.IsNullOrEmpty(tileData.craftingBuildingState))
//             {
//                 var craftingData = JsonUtility.FromJson<CraftingBuildingSaveData>(tileData.craftingBuildingState);
//                 craftingControl.LoadState(craftingData);
//             }

//             HealthBuildingControl healthControl = newBuilding.GetComponent<HealthBuildingControl>();
//             if (healthControl != null && !string.IsNullOrEmpty(tileData.healthBuildingState))
//             {
//                 var healthData = JsonUtility.FromJson<HealthBuildingSaveData>(tileData.healthBuildingState);
//                 healthControl.LoadState(healthData);
//             }

//             ProductionBuildingControl productionControl = newBuilding.GetComponent<ProductionBuildingControl>();
//             if (productionControl != null && !string.IsNullOrEmpty(tileData.productionBuildingState))
//             {
//                 var productionData = JsonUtility.FromJson<ProductionBuildingSaveData>(tileData.productionBuildingState);
//                 productionControl.LoadState(productionData);
//             }

//             ShelterControl shelterControl = newBuilding.GetComponent<ShelterControl>();
//             if (shelterControl != null && !string.IsNullOrEmpty(tileData.shelterControlState))
//             {
//                 var shelterData = JsonUtility.FromJson<ShelterControlSaveData>(tileData.shelterControlState);
//                 shelterControl.LoadState(shelterData);
//             }

//             StorageBuildingControl storageControl = newBuilding.GetComponent<StorageBuildingControl>();
//             if (storageControl != null && !string.IsNullOrEmpty(tileData.storageBuildingState))
//             {
//                 var storageData = JsonUtility.FromJson<StorageBuildingSaveData>(tileData.storageBuildingState);
//                 storageControl.LoadState(storageData);
//             }

//             // 6) Finally, add to the AI’s owned buildings
//             ownedBuildings.Add(newBuilding);
//         }

//         // === 2) Restore building prefix counts ===
//         for (int i = 0; i < data.buildingPrefixes.Count; i++)
//         {
//             string prefix = data.buildingPrefixes[i];
//             int count = data.buildingCounts[i];
//             aiBuildingCounts[prefix] = count;
//         }

//         // 3) Load the under-construction sites
//         foreach (AIBuildingConstructionData ucData in data.buildingConstructionSites)
//         {
//             GameObject prefab = allPrefabs.FirstOrDefault(p =>
//                 string.Equals(p.name, ucData.prefabName, System.StringComparison.Ordinal));
//             if (prefab == null)
//             {
//                 Debug.LogWarning($"[AIBuildingManager] Could not find building under construction prefab '{ucData.prefabName}' in Resources/BuildingTiles.");
//                 continue;
//             }
            
//             GameObject underConst = Instantiate(prefab, ucData.position, ucData.rotation);
            
//             // Remove any existing construction components
//             BuildingConstruction playerConstruction = underConst.GetComponent<BuildingConstruction>();
//             if (playerConstruction != null)
//             {
//                 Destroy(playerConstruction);
//             }

//             ConstructionTileSaveable constructionTileSaveable = underConst.GetComponent<ConstructionTileSaveable>();
//             if (constructionTileSaveable != null)
//             {
//                 Destroy(constructionTileSaveable);
//             }
            
//             // Add AIBuildingConstruction component
//             AIBuildingConstruction aiBConstruction = underConst.GetComponent<AIBuildingConstruction>();
//             if (aiBConstruction == null)
//             {
//                 aiBConstruction = underConst.AddComponent<AIBuildingConstruction>();
//             }
            
//             Building buildingData = BuildingManager.Instance.GetBuildingByID(ucData.aiBuildingID);
//             if(buildingData == null)
//             {
//                 Debug.LogError($"[AIBuildingManager] Prefab '{prefab.name}' is missing a Building component.");
//                 continue;
//             }

//             // Supply the population requirement from buildingData (or a default value)
//             // and use ucData.aiBuildingID as the AI ID
//             aiBConstruction.InitializeBuildingConstruction(buildingData, buildingData.requiredPopulation, ucData.aiPlayerID);
//             aiBConstruction.SetCurrentTurn(ucData.currentTurn);
//             aiBConstruction.UpdateConstructionStage();
//             aiBConstruction.ResumeConstruction(); 
            
//             buildingsUnderConstruction.Add(underConst, ucData.aiBuildingID);
//         }
        
//         Debug.Log("[AIBuildingManager] Finished loading building manager state.");
//     }

//     private GameObject FindBuildingByPosition(Vector3 pos, float tolerance)
//     {
//         // Potentially, search among all building objects
//         BuildingSaveable[] allBuildings = FindObjectsOfType<BuildingSaveable>();
//         foreach (var bldg in allBuildings)
//         {
//             if (Vector3.Distance(bldg.transform.position, pos) <= tolerance)
//             {
//                 return bldg.gameObject;
//             }
//         }
//         return null;
//     }
// }