// using System;
// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
// using UnityEngine.UI;

// public class AIBuildingControl : MonoBehaviour
// {
//     [Header("Building Info Settings")]
//     public string buildingID;
//     public string uniqueInstanceID;
//     public BuildingType buildingType;
//     public GameObject buildingCanvas;
//     public GameObject lights;

//     [Header("Health Settings")]
//     public int health = 100;
//     public Slider healthSlider;

//     [Header("Degeneration Settings")]
//     public int degenerationAmount = 5;
//     public int degenerationIntervalTurns = 3;

//     [Header("State Prefabs")]
//     public GameObject damagedPrefab;
//     public GameObject destroyedPrefab;

//     [Header("Damaged and Destroyed Building Settings")]
//     public bool shouldForceEnvironment;
//     public Sprite damagedIcon;
//     public Sprite destroyedIcon;
//     public float destroyedClearTurns = 3f;

//     [Header("Repair Settings")]
//     public int peopleRequiredForRepair = 5;
//     public int repairPercentage = 10;

//     [Header("Combat State")]
//     public bool isUnderAttack = false;

//     [Header("AI Ownership")]
//     public AIPlayer aiOwner; // ✅ Reference to AI that owns this building

//     [Header("Environment Settings")]
//     [SerializeField] public GameObject environmentBaseTilePrefab;
//     [SerializeField] public EnvironmentType forcedEnvironmentType = EnvironmentType.Cleared;
//     public EnvironmentType ForcedEnvironmentType => forcedEnvironmentType;

//     [Header("Available Research Technologies")]
//     public List<string> availableTechnologyIDs = new List<string>();

//     private BuildingState currentState = BuildingState.Normal;
//     private int turnsSinceLastDegeneration = 0;
//     private TileControl tileControl;
//     public bool isBeingRepaired = false;
//     private float destroyedTurnCounter = 0f;

//     public GameObject damagedInstance; // Transferred from BuildingControl
//     public GameObject destroyedInstance; // Transferred from BuildingControl

//     public GameObject DamagedInstance => damagedInstance;
//     public GameObject DestroyedInstance => destroyedInstance;

//     private void Awake()
//     {
//         uniqueInstanceID = Guid.NewGuid().ToString();
//         tileControl = GetComponent<TileControl>();
//         isUnderAttack = false;

//         if (healthSlider != null)
//         {
//             healthSlider.maxValue = health;
//             healthSlider.value = health;
//         }

//         if (buildingCanvas != null)
//         {
//             buildingCanvas.SetActive(true);
//         }

//         SetState(BuildingState.Normal);
//         TurnSystem.SubscribeToEndOfTurn(OnTurnEnded);
//     }

//     private void OnDestroy()
//     {
//         TurnSystem.UnsubscribeFromEndOfTurn(OnTurnEnded);
//     }
//     private void OnTurnEnded()
//     {
//         if (isBeingRepaired)
//         {
//             ReleasePopulationToOwner();
//             isBeingRepaired = false;
//         }

//         switch (currentState)
//         {
//             case BuildingState.Normal:
//             case BuildingState.Damaged:
//                 turnsSinceLastDegeneration++;
//                 if (turnsSinceLastDegeneration >= degenerationIntervalTurns)
//                 {
//                     turnsSinceLastDegeneration = 0;
//                     DegenerateHealth();
//                 }
//                 break;

//             case BuildingState.Destroyed:
//                 destroyedTurnCounter += 1;
//                 if (destroyedTurnCounter >= destroyedClearTurns)
//                 {
//                     ClearDestroyedBuilding();
//                 }
//                 break;
//         }
//     }

//     public BuildingState CurrentState
//     {
//         get { return currentState; }
//         set { currentState = value; }
//     }

//     private void DegenerateHealth()
//     {
//         health -= degenerationAmount;
//         UpdateHealthSlider(health);
//         Debug.Log($"[AIBuildingControl] Health degenerated to: {health}, CurrentState: {currentState}");

//         if (health <= 0 && currentState != BuildingState.Destroyed)
//         {
//             Debug.Log("[AIBuildingControl] Transitioning to Destroyed");
//             TransitionToDestroyed();
//         }
//         else if (health <= (int)(healthSlider.maxValue / 3f) && currentState == BuildingState.Normal)
//         {
//             Debug.Log("[AIBuildingControl] Transitioning to Damaged");
//             TransitionToDamaged();
//         }
//     }

//     private void TransitionToDamaged()
//     {
//         if (lights != null)
//         {
//             lights.SetActive(true);
//         }
//         currentState = BuildingState.Damaged;

//         SetState(BuildingState.Damaged);

//         // ✅ Ensure damagedInstance exists before applying AI state
//         if (damagedInstance == null && damagedPrefab != null)
//         {
//             damagedInstance = Instantiate(damagedPrefab, transform.position, Quaternion.identity);
//             damagedInstance.transform.SetParent(transform);
//         }

//         // ✅ Apply AI state color if AI-owned
//         if (damagedInstance != null && aiOwner != null)
//         {
//             if (!damagedInstance.TryGetComponent<AIBuildingState>(out var state))
//             {
//                 state = damagedInstance.AddComponent<AIBuildingState>();
//                 state.SetDiscovered(aiOwner.isDiscovered, aiOwner);
//                 Debug.Log($"[AIBuildingControl] Added AIBuildingState to {damagedInstance.name}");
//             }
//         }

//         // Notify production system to cancel production
//         ProductionBuildingControl productionControl = GetComponent<ProductionBuildingControl>();
//         if (productionControl != null)
//         {
//             productionControl.HandleBuildingDamaged();
//         }
//     }

//     private void TransitionToDestroyed()
//     {
//         if (lights != null)
//         {
//             lights.SetActive(false);
//         }
//         currentState = BuildingState.Destroyed;

//         SetState(BuildingState.Destroyed);

//         // ✅ Ensure destroyedInstance exists before applying AI state
//         if (destroyedInstance == null && destroyedPrefab != null)
//         {
//             destroyedInstance = Instantiate(destroyedPrefab, transform.position, Quaternion.identity);
//             destroyedInstance.transform.SetParent(transform);
//         }

//         // ✅ Apply AI state color if AI-owned
//         if (destroyedInstance != null && aiOwner != null)
//         {
//             if (!destroyedInstance.TryGetComponent<AIBuildingState>(out var state))
//             {
//                 state = destroyedInstance.AddComponent<AIBuildingState>();
//                 state.SetDiscovered(aiOwner.isDiscovered, aiOwner);
//                 Debug.Log($"[AIBuildingControl] Added AIBuildingState to {destroyedInstance.name}");
//             }
//         }

//         // if (buildingCanvas != null)
//         // {
//         //     buildingCanvas.SetActive(false);
//         // }
        
//         Debug.Log($"[AIBuildingControl] AI building {buildingID} destroyed.");
//     }

//     /// **🔹 Removes the building from AI Building Manager when destroyed**
//     private void RemoveBuildingFromAI()
//     {
//         if (aiOwner == null) return;

//         AIBuildingManager aiBuildingManager = AIPlayerRegistry.Instance.GetAIPlayerByID(aiOwner.aiPlayerID)?.GetComponentInChildren<AIBuildingManager>();

//         if (aiBuildingManager != null)
//         {
//             aiBuildingManager.RemoveAIBuilding(gameObject);
//         }
//     }

//     public void SetState(BuildingState newState)
//     {
//         Debug.Log($"[AIBuildingControl] Switching to state: {newState}, damagedInstance: {damagedInstance}, destroyedInstance: {destroyedInstance}");
//         switch (newState)
//         {
//             case BuildingState.Normal:
//                 EnableNormalBuildingMesh(true);
//                 if (damagedInstance != null) damagedInstance.SetActive(false);
//                 if (destroyedInstance != null) destroyedInstance.SetActive(false);
//                 break;

//             case BuildingState.Damaged:
//                 EnableNormalBuildingMesh(false);
//                 if (damagedInstance != null) damagedInstance.SetActive(true);
//                 if (destroyedInstance != null) destroyedInstance.SetActive(false);
//                 break;

//             case BuildingState.Destroyed:
//                 EnableNormalBuildingMesh(false);
//                 if (damagedInstance != null) damagedInstance.SetActive(false);
//                 if (destroyedInstance != null) destroyedInstance.SetActive(true);
//                 break;
//         }
//     }

//     private void EnableNormalBuildingMesh(bool enable)
//     {
//         MeshRenderer mr = GetComponent<MeshRenderer>();
//         if (mr != null)
//         {
//             mr.enabled = enable;
//         }
//     }

//     public void UpdateHealthSlider(int sharedHealth)
//     {
//         health = sharedHealth;
//         if (healthSlider != null)
//         {
//             healthSlider.value = health;
//         }
        
//         // If the building is damaged or being repaired and its health is now above one-third of max, revert back to Normal.
//         if ((isBeingRepaired || currentState == BuildingState.Damaged) && health > (int)(healthSlider.maxValue / 3f))
//         {
//             SetState(BuildingState.Normal);
//             Debug.Log($"[AIBuildingControl] Building {buildingID} repaired above one-third health; reverting to Normal state.");
//         }
//     }

//     private bool TryUsePopulationFromOwner()
//     {
//         if (aiOwner == null) return false;

//         AIPopulationManager aiPopManager = aiOwner.GetComponentInChildren<AIPopulationManager>();
//         if (aiPopManager == null) return false;

//         if (aiPopManager.GetAvailablePopulation() >= peopleRequiredForRepair)
//         {
//             aiPopManager.UsePopulation(peopleRequiredForRepair);
//             return true;
//         }
//         return false;
//     }

//     public float GetHealthPercentage()
//     {
//         if (healthSlider != null && healthSlider.maxValue > 0)
//         {
//             return healthSlider.value / healthSlider.maxValue; // Returns a value between 0 and 1
//         }
//         return 1f; // Default to full health if no slider exists
//     }

//     private void ReleasePopulationToOwner()
//     {
//         if (aiOwner == null) return;

//         AIPopulationManager aiPopManager = aiOwner.GetComponentInChildren<AIPopulationManager>();
//         if (aiPopManager != null)
//         {
//             aiPopManager.ReleasePopulation(peopleRequiredForRepair);
//         }
//     }

//     /// **🔹 Clears the destroyed AI building and replaces it with an environment tile**
//     private void ClearDestroyedBuilding()
//     {
//         if (tileControl == null)
//         {
//             Debug.LogError("[AIBuildingControl] TileControl is NULL. Cannot clear destroyed building.");
//             return;
//         }

//         // Remove the AI building from the AIPlayer's records
//         RemoveBuildingFromAI();

//         if (environmentBaseTilePrefab != null)
//         {
//             // Instantiate the base environment tile prefab
//             GameObject baseTile = Instantiate(
//                 environmentBaseTilePrefab,
//                 transform.position,
//                 Quaternion.identity
//             );
//             baseTile.transform.SetParent(transform.parent);

//             // Set up the tile using its TileScript
//             // TileScript tileScript = baseTile.GetComponent<TileScript>();
//             // if (tileScript != null)
//             // {
//             //     tileScript.skipAutomaticPlacement = true;
//             //     tileScript.ForceEnvironmentType(forcedEnvironmentType); // Force AI's environment type

//             //     // Now get the actual environment tile (the one with EnvironmentControl)
//             //     EnvironmentControl envControl = baseTile.GetComponentInChildren<EnvironmentControl>();
//             //     if (envControl != null)
//             //     {
//             //         // We pass the environment tile (not the baseTile) to the AI discovery manager
//             //         GameObject environmentTile = envControl.gameObject;

//             //         AIPlayer aiPlayer = aiOwner ?? GetComponentInParent<AIPlayer>(); // Ensure we have an AIPlayer reference
//             //         if (aiPlayer != null)
//             //         {
//             //             AITileDiscoveryManager discoveryManager = aiPlayer.GetComponentInChildren<AITileDiscoveryManager>();
//             //             if (discoveryManager != null)
//             //             {
//             //                 discoveryManager.HandleAIDestroyedEnvironmentTile(environmentTile);
//             //                 Debug.Log($"[AIBuildingControl] AI processed environment tile at {environmentTile.transform.position}.");
//             //             }
//             //             else
//             //             {
//             //                 Debug.LogError("[AIBuildingControl] AITileDiscoveryManager not found! AI cannot process new tile.");
//             //             }
//             //         }
//             //         else
//             //         {
//             //             Debug.LogError("[AIBuildingControl] AIPlayer reference is NULL! AI cannot process new tile.");
//             //         }
//             //     }
//             //     else
//             //     {
//             //         Debug.LogWarning("[AIBuildingControl] EnvironmentControl not found on the instantiated environment tile (or its children).");
//             //     }
//             // }
//             // else
//             // {
//             //     Debug.LogWarning("[AIBuildingControl] TileScript is missing on the instantiated environment base tile.");
//             // }
//         }
//         else
//         {
//             Debug.LogWarning("[AIBuildingControl] No environment base tile prefab set in AIBuildingControl.");
//         }

//         Debug.Log($"[AIBuildingControl] AI building {buildingID} destroyed and replaced with an environment tile.");

//         // Finally, destroy the AI building gameObject
//         Destroy(gameObject);
//     }

//     /// **🔹 Retrieves the required resources for repairing the AI building**
//     public List<ResourceRequirement> GetRepairCosts()
//     {
//         Building buildingData = BuildingManager.Instance.GetBuildingByID(buildingID);
//         if (buildingData == null)
//         {
//             Debug.LogError($"[AIBuildingControl] Building data not found for ID {buildingID}");
//             return new List<ResourceRequirement>();
//         }
        
//         // Calculate the current health ratio (assumes healthSlider is properly set)
//         float healthRatio = (healthSlider != null && healthSlider.maxValue > 0) ? (float)health / healthSlider.maxValue : 1f;
        
//         // Determine repair percentage based solely on the building's health ratio.
//         // - Below 33%: repair at 100% cost
//         // - Between 33% and 66%: repair at 50% cost
//         // - Above 66%: repair at 10% cost
//         float repairPercentage = (healthRatio < 0.33f) ? 100f : (healthRatio < 0.66f ? 50f : 10f);
        
//         Debug.Log($"[AIBuildingControl] Building {buildingID} has health ratio: {healthRatio:F2} -> using {repairPercentage}% repair cost.");
        
//         // Calculate repair costs as a percentage of the original construction cost.
//         List<ResourceRequirement> repairCosts = new List<ResourceRequirement>();
//         foreach (var resource in buildingData.requiredResources)
//         {
//             int repairAmount = Mathf.CeilToInt(resource.amount * (repairPercentage / 100f));
//             repairCosts.Add(new ResourceRequirement(resource.resourceID, repairAmount));
//         }
        
//         return repairCosts;
//     }

//     public int TurnsSinceLastDegeneration
//     {
//         get { return turnsSinceLastDegeneration; }
//         set { turnsSinceLastDegeneration = value; }
//     }
// }