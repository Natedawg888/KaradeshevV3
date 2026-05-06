// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;

// public class AIBuildingConstruction : MonoBehaviour
// {
//     public List<GameObject> constructionStages;
//     private int turnsToComplete;
//     public GameObject constructionCanvas;
//     public TimerUI constructTimerUI;
//     private int currentTurn = 0;
//     private GameObject currentStagePrefab;
//     private Building associatedBuilding;
//     private int populationUsedForConstruction;

//     private AIBuildingManager aiBuildingManager;

//     private string aiPlayerID; // ✅ Store AI Player ID
//     private bool isDestroyed = false; // ✅ Prevent updates after destruction

//     private void Start()
//     {
//         if (constructionCanvas != null)
//         {
//             constructionCanvas.gameObject.SetActive(false);
//         }
//     }

//     public void SetAIPlayerID(string id)
//     {
//         aiPlayerID = id;
//     }

//     public string GetAIPlayerID()
//     {
//         return aiPlayerID;
//     }

//     /// **🔹 Initialize AI building construction**
//     public void InitializeBuildingConstruction(Building building, int populationRequirement, string aiID)
//     {
//         associatedBuilding = building;
//         aiPlayerID = aiID; // ✅ Store AI ID

//         aiBuildingManager = AIPlayerRegistry.Instance.GetAIPlayerByID(aiPlayerID)?.GetComponentInChildren<AIBuildingManager>();

//         if (associatedBuilding != null)
//         {
//             turnsToComplete = associatedBuilding.requiredTurnsToComplete;

//             // ✅ Copy construction stages from the building's prefab data
//             BuildingConstruction prefabConstruction = associatedBuilding.buildingPrefab.GetComponent<BuildingConstruction>();
//             if (prefabConstruction != null)
//             {
//                 constructionStages = new List<GameObject>(prefabConstruction.constructionStages);
//             }
//             else
//             {
//                 Debug.LogError($"[AIBuildingConstruction] Missing BuildingConstruction script on {associatedBuilding.buildingPrefab.name}.");
//             }
//         }

//         populationUsedForConstruction = populationRequirement;
//     }

//     /// **🔹 Start AI building construction**
//     public void StartConstruction()
//     {
//         currentTurn = 0;

//         if (constructionStages == null || constructionStages.Count == 0)
//         {
//             Debug.LogError("[AIBuildingConstruction] No construction stages assigned.");
//             return;
//         }

//         if (constructionCanvas != null)
//         {
//             constructionCanvas.gameObject.SetActive(true);
//         }

//         if (constructTimerUI != null)
//         {
//             constructTimerUI.Initialize(turnsToComplete);
//         }

//         // ✅ Register this building as "Under Construction"
//         aiBuildingManager.RegisterBuildingUnderConstruction(gameObject, associatedBuilding.buildingID);

//         UpdateConstructionStage();
//         TurnSystem.SubscribeToEndOfTurn(OnTurnEnded);
//     }

//     /// **🔹 AI construction progress each turn**
//     private void OnTurnEnded()
//     {
//         if (isDestroyed) return; // ✅ Prevent updates if already destroyed

//         currentTurn++;

//         if (currentTurn < turnsToComplete)
//         {
//             UpdateConstructionStage();
//             if (constructTimerUI != null)
//             {
//                 constructTimerUI.UpdateTimer(turnsToComplete - currentTurn);
//             }
//         }
//         else
//         {
//             CompleteConstruction();
//         }
//     }

//     /// **🔹 Update construction progress visually**
//     public void UpdateConstructionStage()
//     {
//         if (isDestroyed) return; // ✅ Prevent errors if already destroyed

//         if (currentStagePrefab != null)
//         {
//             Destroy(currentStagePrefab);
//         }

//         int stageIndex = Mathf.Clamp(currentTurn, 0, constructionStages.Count - 1);
//         currentStagePrefab = Instantiate(constructionStages[stageIndex], transform);
//         currentStagePrefab.transform.localPosition = Vector3.zero;
//         currentStagePrefab.transform.localRotation = Quaternion.identity;

//         ApplyAIBuildingState(currentStagePrefab);
//     }

//     /// **🔹 Apply `AIBuildingState` to the current construction stage**
//     private void ApplyAIBuildingState(GameObject stage)
//     {
//         if (stage == null) return;

//         AIBuildingState aiBuildingState = stage.GetComponent<AIBuildingState>();

//         if (aiBuildingState == null)
//         {
//             aiBuildingState = stage.AddComponent<AIBuildingState>();
//         }

//         AIPlayer aiPlayer = AIPlayerRegistry.Instance.GetAIPlayerByID(aiPlayerID);
//         if (aiPlayer != null)
//         {
//             aiBuildingState.SetDiscovered(aiPlayer.isDiscovered, aiPlayer);
//         }
//     }

//     /// **🔹 AI completes building construction**
//     private void CompleteConstruction()
//     {
//         TurnSystem.UnsubscribeFromEndOfTurn(OnTurnEnded);
//         isDestroyed = true;

//         if (currentStagePrefab != null)
//         {
//             Destroy(currentStagePrefab);
//         }

//         GameObject finalBuilding = null;
//         if (constructionStages.Count > 0)
//         {
//             float currentYRotation = transform.rotation.eulerAngles.y;
//             finalBuilding = Instantiate(constructionStages[constructionStages.Count - 1], transform.position, Quaternion.Euler(0, currentYRotation, 0));
//             finalBuilding.transform.SetParent(null);
//         }

//         if (finalBuilding == null)
//         {
//             Debug.LogError("[AIBuildingConstruction] Final AI building instantiation failed.");
//             return;
//         }

//         // ✅ Change Tile Type to AI Building
//         TileControl tileControl = finalBuilding.GetComponent<TileControl>();
//         if (tileControl != null)
//         {
//             tileControl.SetTileType(TileType.AiBuilding);
//             Debug.Log($"[AIBuildingConstruction] Tile type changed to AiBuilding for {finalBuilding.name}.");
//         }

//         // ✅ Convert the building to an AI-controlled building
//         ConvertToAIBuilding(finalBuilding);

//         // ✅ Register the **final** AI building with AIBuildingManager
//         RegisterBuilding(finalBuilding);

//         ReleasePopulation();

//         ApplyAIBuildingState(finalBuilding);

//         if (constructionCanvas != null)
//         {
//             constructionCanvas.gameObject.SetActive(false);
//         }

//         aiBuildingManager.RemoveBuildingUnderConstruction(gameObject);
//                 Debug.Log($"[AIBuildingManager] Removed {gameObject} from buildings under construction.");

//         Debug.Log($"[AIBuildingConstruction] AI construction completed: {associatedBuilding.buildingName}!");

//         // ✅ Destroy the construction site (this object)
//         Destroy(gameObject);
//     }

//     /// **🔹 Converts a player building into an AI building**
//     private void ConvertToAIBuilding(GameObject building)
//     {
//         if (building == null)
//         {
//             Debug.LogError("[ConvertToAIBuilding] Building is NULL. Cannot convert.");
//             return;
//         }

//         BuildingControl oldControl = building.GetComponent<BuildingControl>();
//         if (oldControl == null)
//         {
//             Debug.LogError("[ConvertToAIBuilding] No BuildingControl found on the building. Conversion failed.");
//             return;
//         }

//         BuildingSaveable buildingSaveable = building.GetComponent<BuildingSaveable>();
//         if (buildingSaveable == null)
//         {
//             return;
//         }

//         // ✅ Add `AIBuildingControl`
//         AIBuildingControl aiBuildingControl = building.AddComponent<AIBuildingControl>();

//         // ✅ Copy Data from `BuildingControl`
//         aiBuildingControl.buildingID = oldControl.buildingID;
//         aiBuildingControl.uniqueInstanceID = oldControl.uniqueInstanceID;
//         aiBuildingControl.buildingType = oldControl.buildingType;
//         aiBuildingControl.buildingCanvas = oldControl.buildingCanvas;
//         aiBuildingControl.lights = oldControl.lights;

//         aiBuildingControl.health = oldControl.health;
//         aiBuildingControl.healthSlider = oldControl.healthSlider;
//         aiBuildingControl.degenerationAmount = oldControl.degenerationAmount;
//         aiBuildingControl.degenerationIntervalTurns = oldControl.degenerationIntervalTurns;

//         // Transfer existing instances instead of relying on prefabs
//         aiBuildingControl.damagedInstance = oldControl.DamagedInstance;
//         aiBuildingControl.destroyedInstance = oldControl.DestroyedInstance;
//         aiBuildingControl.damagedIcon = oldControl.damagedIcon;
//         aiBuildingControl.destroyedIcon = oldControl.destroyedIcon;
//         aiBuildingControl.damagedPrefab = oldControl.damagedPrefab; // Optional, for reference
//         aiBuildingControl.destroyedPrefab = oldControl.destroyedPrefab; // Optional, for reference

//         aiBuildingControl.shouldForceEnvironment = oldControl.shouldForceEnvironment;
//         aiBuildingControl.destroyedClearTurns = oldControl.destroyedClearTurns;

//         aiBuildingControl.peopleRequiredForRepair = oldControl.peopleRequiredForRepair;
//         aiBuildingControl.repairPercentage = oldControl.repairPercentage;

//         // ✅ Copy Environment Settings
//         aiBuildingControl.environmentBaseTilePrefab = oldControl.environmentBaseTilePrefab;
//         aiBuildingControl.forcedEnvironmentType = oldControl.forcedEnvironmentType;

//         // ✅ Assign AI Owner
//         aiBuildingControl.aiOwner = AIPlayerRegistry.Instance.GetAIPlayerByID(aiPlayerID);

//         // ✅ Copy available research technologies
//         aiBuildingControl.availableTechnologyIDs = new List<string>(oldControl.availableTechnologyIDs);
        
//         if (aiBuildingControl.availableTechnologyIDs.Count > 0)
//         {
//             Debug.Log($"[ConvertToAIBuilding] AI Building {building.name} inherits {aiBuildingControl.availableTechnologyIDs.Count} research technologies.");
//         }
//         else
//         {
//             Debug.Log($"[ConvertToAIBuilding] AI Building {building.name} has no research technologies.");
//         }

//         // ✅ Remove `BuildingControl`
//         Destroy(oldControl);
//         Destroy(buildingSaveable);
//         Debug.Log($"[ConvertToAIBuilding] Converted {building.name} to AI Building and removed original BuildingControl.");
//     }

//     /// **🔹 Registers the completed building to the correct AI**
//     private void RegisterBuilding(GameObject finalBuilding)
//     {
//         AIPlayer aiPlayer = AIPlayerRegistry.Instance.GetAIPlayerByID(aiPlayerID);

//         if (aiPlayer != null)
//         {
//             AIBuildingManager aiBuildingManager = aiPlayer.GetComponentInChildren<AIBuildingManager>();

//             if (aiBuildingManager != null)
//             {   
//                 aiBuildingManager.CompleteBuildingConstruction(finalBuilding);
//                 Debug.Log($"[AIBuildingConstruction] Registered {associatedBuilding.buildingName} to {aiPlayer.aiName}.");
//             }
//         }
//     }

//     private void ReleasePopulation()
//     {
//         AIPlayer aiPlayer = AIPlayerRegistry.Instance.GetAIPlayerByID(aiPlayerID);
//         aiPlayer?.GetComponentInChildren<AIPopulationManager>()?.ReleasePopulation(populationUsedForConstruction);
//     }

//     public int GetCurrentTurn()
//     {
//         return currentTurn;
//     }

//     public void SetCurrentTurn(int turn)
//     {
//         currentTurn = turn;
//     }

//     public int GetTurnsToComplete()
//     {
//         return turnsToComplete;
//     }

//     public int GetPopulationUsed()
//     {
//         return populationUsedForConstruction;
//     }

//      /// **🔹 Resume AI construction from a saved state**
//     public void ResumeConstruction()
//     {
//         if (constructionStages == null || constructionStages.Count == 0)
//         {
//             Debug.LogError("No construction stages assigned on resume.");
//             return;
//         }

//         if (constructionCanvas != null)
//         {
//             constructionCanvas.SetActive(true);
//         }

//         // Reactivate timer UI if needed.
//         if (constructTimerUI != null)
//         {
//             constructTimerUI.Initialize(turnsToComplete);
//             constructTimerUI.UpdateTimer(turnsToComplete - currentTurn);
//         }

//         // Re-subscribe to continue construction.
//         TurnSystem.SubscribeToEndOfTurn(OnTurnEnded);
//     }
// }