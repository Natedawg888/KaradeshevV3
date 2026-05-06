// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;

// public class AITileDiscoveryManager : MonoBehaviour
// {
//     [Header("Discovery Settings")]
//     public int discoveryDepth = 3; // Depth of newly discovered tiles

//     [Header("Tracked Tiles")]
//     [SerializeField] private List<GameObject> inspectorUndiscoveredTiles = new List<GameObject>(); // Shown in Inspector
//     [SerializeField] private List<GameObject> inspectorDiscoveredTiles = new List<GameObject>();   // Shown in Inspector

//     private HashSet<GameObject> undiscoveredTiles = new HashSet<GameObject>(); // ✅ Prevent duplicates
//     private HashSet<GameObject> discoveredTiles = new HashSet<GameObject>();   // ✅ Prevent duplicates
//     [SerializeField] private List<DiscoveryProcess> activeDiscoveryProcesses = new List<DiscoveryProcess>();

//     [Header("Delay Settings")]
//     private float bfsDelay = 0.1f; // Delay before starting the BFS

//     private AITileTracker aiTileTracker;
//     private AIPopulationManager aiPopulationManager;
//     private AIGatheringManager aiGatheringManager;
//     private AIBuildingManager aiBuildingManager;
//     private AITechnologyManager technologyManager;
//     private AILevelManager aiLevelManager;
//     private GameObject aiStarterTile;

//     private void Start()
//     {
//         Transform aiPlayer = transform.parent;

//         if (aiPlayer == null) return;

//         aiTileTracker = aiPlayer.GetComponentInChildren<AITileTracker>();
//         aiPopulationManager = aiPlayer.GetComponentInChildren<AIPopulationManager>();
//         aiGatheringManager = aiPlayer.GetComponentInChildren<AIGatheringManager>();
//         aiBuildingManager = aiPlayer.GetComponentInChildren<AIBuildingManager>();
//         technologyManager = aiPlayer.GetComponentInChildren<AITechnologyManager>();
//         aiLevelManager = aiPlayer.GetComponentInChildren<AILevelManager>();

//         if (aiTileTracker == null || aiPopulationManager == null) return;

//         TurnSystem.SubscribeToEndOfTurn(OnTurnEnded);
//     }

//     private void OnDestroy()
//     {
//         TurnSystem.UnsubscribeFromEndOfTurn(OnTurnEnded);
//     }

//     public void SetStarterTile(GameObject tile)
//     {
//         aiStarterTile = tile;
//         StartCoroutine(DelayedBFS());
//     }

//     public void Initialize(GameObject starterTile)
//     {
//         undiscoveredTiles.Clear();
//         discoveredTiles.Clear();
//         activeDiscoveryProcesses.Clear();
//         UpdateInspectorLists(); // ✅ Update Inspector display
//     }

//     private IEnumerator DelayedBFS()
//     {
//         yield return new WaitForSeconds(bfsDelay);
//         if (aiStarterTile != null)
//         {
//             DiscoverTile(aiStarterTile);
//             UpdateUndiscoveredTiles();
//         }
//     }

//     private void OnTurnEnded()
//     {
//         ProcessDiscoveryTurns();
//     }

//     private void UpdateUndiscoveredTiles()
//     {
//         HashSet<GameObject> newTiles = new HashSet<GameObject>();

//         foreach (GameObject discoveredTile in discoveredTiles)
//         {
//             Collider[] hits = Physics.OverlapBox(
//                 discoveredTile.transform.position,
//                 discoveredTile.GetComponent<BoxCollider>().bounds.extents,
//                 discoveredTile.transform.rotation
//             );

//             foreach (Collider hit in hits)
//             {
//                 GameObject hitObj = hit.gameObject;

//                 if (!discoveredTiles.Contains(hitObj) &&
//                     !undiscoveredTiles.Contains(hitObj) &&
//                     hitObj.GetComponent<EnvironmentControl>() != null &&
//                     !hitObj.GetComponent<EnvironmentControl>().isDiscovered &&
//                     hitObj.GetComponent<EnvironmentControl>().isDiscoverable)
//                 {
//                     newTiles.Add(hitObj);
//                 }
//             }
//         }

//         foreach (var tile in newTiles)
//         {
//             undiscoveredTiles.Add(tile);
//         }

//         UpdateInspectorLists(); // ✅ Update Inspector display
//     }

//     public void ExecuteDiscovery(GameObject tile)
//     {
//         if (tile == null)
//         {
//             //Debug.LogWarning("[AITileDiscoveryManager] Attempted to discover a null tile.");
//             return;
//         }

//         EnvironmentControl envControl = tile.GetComponent<EnvironmentControl>();

//         if (envControl == null || envControl.isBeingDiscovered || !envControl.isDiscoverable || envControl.isBeingAIDiscovered || discoveredTiles.Contains(tile))
//         {
//             //Debug.Log($"[AITileDiscoveryManager] Skipping {tile.name} discovery. Already discovered or in process.");
//             return;
//         }

//         if (aiPopulationManager.GetAvailablePopulation() >= envControl.requiredPopulation)
//         {
//             StartDiscoveryProcess(tile, envControl);
//         }
//         else
//         {
//             //Debug.Log($"[AITileDiscoveryManager] Not enough population to discover {tile.name}.");
//         }
//     }

//     private void StartDiscoveryProcess(GameObject tile, EnvironmentControl envControl)
//     {
//         envControl.isBeingAIDiscovered = true;
//         envControl.discoveryTurnsLeft = envControl.discoveryTurnsRequired;
//         envControl.lastAIWorker = this.GetComponentInParent<AIPlayer>();
//         aiPopulationManager.UsePopulation(envControl.requiredPopulation);

//         activeDiscoveryProcesses.Add(new DiscoveryProcess()
//         {
//             tile = tile,
//             turnsRemaining = envControl.discoveryTurnsRequired
//         });

//         envControl.SetDiscoverIconActive(true);

//         undiscoveredTiles.Remove(tile);
//         //Debug.Log($"[AITileDiscoveryManager] Discovery started for {tile.name}, will take {envControl.discoveryTurnsRequired} turns.");

//         UpdateInspectorLists(); // ✅ Update Inspector display
//     }

//     private void ProcessDiscoveryTurns()
//     {
//         for (int i = activeDiscoveryProcesses.Count - 1; i >= 0; i--)
//         {
//             DiscoveryProcess process = activeDiscoveryProcesses[i];
//             EnvironmentControl envControl = process.tile.GetComponent<EnvironmentControl>();

//             if (envControl == null)
//             {
//                 activeDiscoveryProcesses.RemoveAt(i);
//                 continue;
//             }

//             float failureChance = envControl.initialDiscoveryFailureChance / (envControl.discoveryTurnsCompleted + 1);

//             bool failed = Random.value <= failureChance / 100f;

//             if (failed)
//             {
//                 FailDiscovery(process.tile, envControl);
//                 activeDiscoveryProcesses.RemoveAt(i);
//                 continue;
//             }

//             process.turnsRemaining--;
//             envControl.discoveryTurnsLeft--;

//             if (process.turnsRemaining <= 0)
//             {
//                 CompleteDiscovery(process.tile, envControl);
//                 activeDiscoveryProcesses.RemoveAt(i);
//             }
//         }
//     }

//     private void FailDiscovery(GameObject tile, EnvironmentControl envControl)
//     {
//         envControl.isBeingAIDiscovered = false;
//         envControl.SetDiscoverIconActive(false);
//         aiPopulationManager.ReleasePopulation(envControl.requiredPopulation);
//         aiPopulationManager.ApplyFailurePenalty(envControl.populationPenaltyOnFailure);

//         ApplyRandomAIDisease(envControl.discoveryDiseaseIDs);

//         //Debug.Log($"[AITileDiscoveryManager] Discovery failed for {tile.name}, population penalized.");

//         if (!undiscoveredTiles.Contains(tile))
//         {
//             undiscoveredTiles.Add(tile);
//         }

//         // Update the list of undiscovered tiles after expansion
//         UpdateUndiscoveredTiles();
//     }

//     private void CompleteDiscovery(GameObject tile, EnvironmentControl envControl)
//     {
//         AIPlayer aiPlayer = GetComponentInParent<AIPlayer>();

//         if (aiPlayer == null)
//         {
//             //Debug.LogError("[AITileDiscoveryManager] AIPlayer reference is NULL in CompleteDiscovery!");
//             return;
//         }

//         if (envControl == null)
//         {
//             //Debug.LogError("[AITileDiscoveryManager] EnvironmentControl reference is NULL for tile discovery.");
//             return;
//         }

//         if (!aiPlayer.isDiscovered)
//         {
//             envControl.SetAIDiscovered(true, aiPlayer);
//         }
//         else
//         {
//             envControl.SetDiscovered(true);
//         }

//         envControl.isBeingAIDiscovered = false;
//         aiPopulationManager.ReleasePopulation(envControl.requiredPopulation);

//         if (!discoveredTiles.Contains(tile))
//         {
//             discoveredTiles.Add(tile);
//         }

//         undiscoveredTiles.Remove(tile);
//         envControl.UpdateTileMaterialForAI(aiPlayer);
//         envControl.SetDiscoverIconActive(false);

//         aiTileTracker.PerformBreadthSearchFromTile(tile);
//         UpdateInspectorLists();

//         aiGatheringManager.AddGatherableTile(tile);
//         aiGatheringManager.UpdateGatherableTiles();
//         aiBuildingManager.UpdateAvailableBuildingTiles();

//         aiLevelManager.AddXP(envControl.discoveryEXPReward);

//         // ✅ Apply environment upgrades for all researched technologies to the new discovered tile
//         if (technologyManager != null)
//         {
//             foreach (Technology tech in technologyManager.GetResearchedTechnologies())
//             {
//                 //Debug.Log($"[AITileDiscoveryManager] Applying environment upgrades for {tech.technologyName} to newly discovered tile: {tile.name}");
//                 technologyManager.ApplyEnvironmentUpgrades(tech);
//             }
//         }

//         //Debug.Log($"[AITileDiscoveryManager] Successfully discovered {tile.name}. AI discovery status: {aiPlayer.isDiscovered}");
//     }

//     public void DiscoverTile(GameObject tile)
//     {
//         AIPlayer aiPlayer = GetComponentInParent<AIPlayer>();
        
//         if (tile == null) return;

//         if (!discoveredTiles.Contains(tile))
//         {
//             discoveredTiles.Add(tile);
//         }

//         // ✅ Get EnvironmentControl safely
//         EnvironmentControl envControl = tile.GetComponent<EnvironmentControl>();

//         if (envControl != null) // ✅ Only update if EnvironmentControl exists
//         {
//             // ✅ Update discovery status based on AI visibility
//             if (!aiPlayer.isDiscovered)
//             {
//                 envControl.SetAIDiscovered(true, aiPlayer);
//             }
//             else
//             {
//                 envControl.SetDiscovered(true);
//             }

//             envControl.UpdateTileMaterialForAI(aiPlayer);
            
//             aiGatheringManager.AddGatherableTile(tile);
//             aiGatheringManager.UpdateGatherableTiles();
//         }
//         else
//         {
//             //Debug.LogWarning($"[AITileDiscoveryManager] Skipping discovery update for {tile.name} (No EnvironmentControl found).");
//         }

//         // ✅ Update AI resource tracking
//         AIResourceTileTracker resourceTracker = aiTileTracker.GetComponent<AIResourceTileTracker>();
//         if (resourceTracker != null)
//         {
//             resourceTracker.UpdateResourceTracking();
//         }

//         UpdateInspectorLists(); // ✅ Update Inspector display
//     }

//     public void HandleAIDestroyedEnvironmentTile(GameObject environmentTile)
//     {
//         // Get the AIPlayer from the manager's parent
//         AIPlayer aiPlayer = GetComponentInParent<AIPlayer>();
//         if (aiPlayer == null)
//         {
//             //Debug.LogError("[AITileDiscoveryManager] AIPlayer reference is NULL in HandleAIDestroyedEnvironmentTile!");
//             return;
//         }

//         // Get the EnvironmentControl from the passed environment tile
//         EnvironmentControl envControl = environmentTile.GetComponent<EnvironmentControl>();
//         if (envControl != null)
//         {
//             // Mark the tile as discovered for AI and update its materials
//             envControl.SetAIDiscovered(true, aiPlayer);
//             envControl.UpdateTileMaterialForAI(aiPlayer);

//             // Add the environment tile to the AI's discovered list if not already added
//             if (!discoveredTiles.Contains(environmentTile))
//             {
//                 discoveredTiles.Add(environmentTile);
//             }

//             // Optionally, trigger a breadth-first search from this tile to update neighboring tiles
//             if (aiTileTracker != null)
//             {
//                 aiTileTracker.PerformBreadthSearchFromTile(environmentTile);
//             }

//             // ✅ Apply environment upgrades for all researched technologies to the new discovered tile
//             if (technologyManager != null)
//             {
//                 foreach (Technology tech in technologyManager.GetResearchedTechnologies())
//                 {
//                     //Debug.Log($"[AITileDiscoveryManager] Applying environment upgrades for {tech.technologyName} to newly discovered tile: {envControl.name}");
//                     technologyManager.ApplyEnvironmentUpgrades(tech);
//                 }
//             }

//             UpdateInspectorLists();
//             //Debug.Log($"[AITileDiscoveryManager] Handled AI destroyed environment tile: {environmentTile.name}.");
//         }
//     }

//     private void UpdateInspectorLists()
//     {
//         inspectorUndiscoveredTiles = new List<GameObject>(undiscoveredTiles);
//         inspectorDiscoveredTiles = new List<GameObject>(discoveredTiles);
//     }

//     public List<GameObject> GetDiscoveredTiles()
//     {
//         return new List<GameObject>(discoveredTiles);
//     }

//     public List<DiscoveryProcess> GetBeingDiscoveredTiles()
//     {
//         return new List<DiscoveryProcess>(activeDiscoveryProcesses);
//     }

//     public List<GameObject> GetUndiscoveredTiles()
//     {
//         return new List<GameObject>(undiscoveredTiles);
//     }

//     private void ApplyRandomAIDisease(List<string> diseaseIDs)
//     {
//         if (diseaseIDs == null || diseaseIDs.Count == 0)
//         {
//             //Debug.Log("[AIGatheringManager] No diseases available for AI gathering failure.");
//             return;
//         }

//         float diseaseApplyChance = 0.3f; // 30% chance to apply a disease

//         if (Random.value <= diseaseApplyChance)
//         {
//             string diseaseID = diseaseIDs[Random.Range(0, diseaseIDs.Count)];
//             Disease disease = DiseaseControl.Instance?.GetDiseaseByID(diseaseID);

//             if (disease != null)
//             {
//                 AIDiseaseManager aiDiseaseManager = aiPopulationManager.GetComponent<AIDiseaseManager>();

//                 if (aiDiseaseManager != null)
//                 {
//                     aiDiseaseManager.ApplyAIDisease(disease);
//                     //Debug.Log($"[AIGatheringManager] AI infected with {disease.diseaseName} after gathering failure.");
//                 }
//             }
//         }
//     }

//     public void RemoveDiscoveredTile(GameObject tile)
//     {
//         if (tile == null) return;

//         if (discoveredTiles.Contains(tile))
//         {
//             discoveredTiles.Remove(tile);
//             UpdateInspectorLists(); // ✅ Update debug view
//             //Debug.Log($"[AITileDiscoveryManager] Removed {tile.name} from discovered tiles.");
//         }
//     }

//     public AITileDiscoveryManagerSaveData SaveState()
//     {
//         AITileDiscoveryManagerSaveData data = new AITileDiscoveryManagerSaveData();

//         data.discoveredTiles = new List<AITileSaveData>();
//         foreach (GameObject tile in discoveredTiles)
//         {
//             AITileSaveData tsd = new AITileSaveData();
//             tsd.tileName = tile.name;
//             tsd.position = tile.transform.position;
//             data.discoveredTiles.Add(tsd);
//         }
        
//         data.undiscoveredTiles = new List<AITileSaveData>();
//         foreach (GameObject tile in undiscoveredTiles)
//         {
//             AITileSaveData tsd = new AITileSaveData();
//             tsd.tileName = tile.name;
//             tsd.position = tile.transform.position;
//             data.undiscoveredTiles.Add(tsd);
//         }
        
//         // Save active discovery processes (unchanged).
//         data.activeDiscoveryProcesses = new List<DiscoveryProcessSaveData>();
//         foreach (DiscoveryProcess process in activeDiscoveryProcesses)
//         {
//             DiscoveryProcessSaveData procData = new DiscoveryProcessSaveData();
//             procData.tileName = process.tile.name;
//             procData.turnsRemaining = process.turnsRemaining;
//             data.activeDiscoveryProcesses.Add(procData);
//         }
        
//         return data;
//     }

//     public void LoadState(AITileDiscoveryManagerSaveData data)
//     {
//         AIPlayer aiPlayer = GetComponentInParent<AIPlayer>(); // Get the correct AI context
//         if (data == null) return;
        
//         // Clear existing sets.
//         undiscoveredTiles.Clear();
//         discoveredTiles.Clear();
//         activeDiscoveryProcesses.Clear();
        
//         // Get the starter tile from the AIPlayer.
//         if (aiPlayer.StarterTileInstance == null)
//         {
//             //Debug.LogWarning("StarterTileInstance is null in LoadState.");
//             return;
//         }
//         aiStarterTile = aiPlayer.StarterTileInstance;
        
//         // Process saved discovered tiles.
//         foreach (AITileSaveData tsd in data.discoveredTiles)
//         {
//             GameObject foundTile = FindTileByNameAndPosition(tsd.tileName, tsd.position);
//             if (foundTile != null)
//             {
//                 discoveredTiles.Add(foundTile);
//                 // Reapply AI-discovered state.
//                 EnvironmentControl envControl = foundTile.GetComponent<EnvironmentControl>();
//                 if (envControl != null)
//                 {
//                     envControl.SetAIDiscovered(true, aiPlayer);
//                 }
//             }
//         }
        
//         // Process saved undiscovered tiles.
//         foreach (AITileSaveData tsd in data.undiscoveredTiles)
//         {
//             GameObject foundTile = FindTileByNameAndPosition(tsd.tileName, tsd.position);
//             if (foundTile != null)
//             {
//                 undiscoveredTiles.Add(foundTile);
//             }
//         }
        
//         // Optionally, you might call UpdateInspectorLists() here.
//         UpdateInspectorLists();
        
//         // Rebuild active discovery processes as before.
//         EnvironmentControl[] allTiles = FindObjectsOfType<EnvironmentControl>();
//         Dictionary<string, GameObject> tileLookup = new Dictionary<string, GameObject>();
//         foreach (EnvironmentControl tile in allTiles)
//         {
//             if (!tileLookup.ContainsKey(tile.gameObject.name))
//             {
//                 tileLookup.Add(tile.gameObject.name, tile.gameObject);
//             }
//         }
//         foreach (DiscoveryProcessSaveData procData in data.activeDiscoveryProcesses)
//         {
//             if (tileLookup.TryGetValue(procData.tileName, out GameObject tile))
//             {
//                 DiscoveryProcess process = new DiscoveryProcess();
//                 process.tile = tile;
//                 process.turnsRemaining = procData.turnsRemaining;
//                 activeDiscoveryProcesses.Add(process);
//             }
//         }
        
//         UpdateInspectorLists();
//     }

//     private GameObject FindTileByNameAndPosition(string tileName, Vector3 savedPosition, float tolerance = 0.1f)
//     {
//         // First, search among all EnvironmentControl objects in the scene.
//         EnvironmentControl[] allTiles = FindObjectsOfType<EnvironmentControl>();
//         foreach (EnvironmentControl tile in allTiles)
//         {
//             if (tile.gameObject.name == tileName &&
//                 Vector3.Distance(tile.transform.position, savedPosition) <= tolerance)
//             {
//                 return tile.gameObject;
//             }
//         }
        
//         // Optionally, if not found, search among children of objects with the matching name.
//         foreach (EnvironmentControl tile in allTiles)
//         {
//             if (tile.gameObject.name == tileName)
//             {
//                 EnvironmentControl[] childTiles = tile.GetComponentsInChildren<EnvironmentControl>();
//                 foreach (EnvironmentControl child in childTiles)
//                 {
//                     if (Vector3.Distance(child.transform.position, savedPosition) <= tolerance)
//                     {
//                         return child.gameObject;
//                     }
//                 }
//             }
//         }
        
//         return null;
//     }

//     [System.Serializable]
//     public class DiscoveryProcess
//     {
//         public GameObject tile;
//         public int turnsRemaining;
//     }
// }