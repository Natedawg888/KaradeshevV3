// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;

// public class AIGatheringManager : MonoBehaviour
// {
//     private AIPopulationManager aiPopulationManager;
//     private AIInventoryManager aiInventoryManager;
//     private AITileDiscoveryManager aiTileDiscoveryManager;
//     private AIResourceManager aiResourceManager;
//     private AILevelManager aiLevelManager;

//     [Header("Tracked Gathering Tiles")]
//     [SerializeField] private List<GameObject> gatherableTiles = new List<GameObject>();
//     [SerializeField] private List<GatheringProcess> activeGatheringProcesses = new List<GatheringProcess>();

//     private void Start()
//     {
//         Transform aiPlayer = transform.parent;

//         if (aiPlayer == null) return;

//         aiPopulationManager = aiPlayer.GetComponentInChildren<AIPopulationManager>();
//         aiInventoryManager = aiPlayer.GetComponentInChildren<AIInventoryManager>();
//         aiTileDiscoveryManager = aiPlayer.GetComponentInChildren<AITileDiscoveryManager>();
//         aiResourceManager = aiPlayer.GetComponentInChildren<AIResourceManager>();
//         aiLevelManager = aiPlayer.GetComponentInChildren<AILevelManager>();

//         if (aiTileDiscoveryManager == null || aiResourceManager == null)
//         {
//             //Debug.LogError("[AIGatheringManager] Missing required components.");
//             return;
//         }

//         TurnSystem.SubscribeToEndOfTurn(OnTurnEnded);
//     }

//     public void RemoveGatherableTile(GameObject tile)
//     {
//         if (tile == null) return;

//         if (gatherableTiles.Contains(tile))
//         {
//             gatherableTiles.Remove(tile);
//             UpdateGatherableTiles();
//             //Debug.Log($"[AIGatheringManager] Removed {tile.name} from gatherable tiles.");
//         }
//     }

//     private void OnDestroy()
//     {
//         TurnSystem.UnsubscribeFromEndOfTurn(OnTurnEnded);
//     }

//     private void OnTurnEnded()
//     {
//         ProcessGatheringTurns();
//     }

//     public void UpdateGatherableTiles()
//     {
//         List<GameObject> discoveredTiles = aiTileDiscoveryManager.GetDiscoveredTiles();

//         foreach (GameObject tile in discoveredTiles)
//         {
//             AddGatherableTile(tile);
//         }
//     }

//     public void AttemptGathering(AIPlan gatheringPlan)
//     {
//         if (gatheringPlan == null || gatheringPlan.target == null)
//         {
//             //Debug.LogWarning("[AIGatheringManager] Invalid gathering plan.");
//             return;
//         }

//         EnvironmentControl envControl = gatheringPlan.target.GetComponent<EnvironmentControl>();
//         if (envControl == null)
//         {
//             //Debug.LogWarning("[AIGatheringManager] Target does not have EnvironmentControl.");
//             return;
//         }

//         // ✅ Ensure AI is gathering based on planned action, not random tile selection
//         if (IsTileBeingGathered(envControl.gameObject))
//         {
//             //Debug.Log($"[AIGatheringManager] AI is already gathering from {envControl.name}. Skipping.");
//             return;
//         }

//         ExecuteGathering(envControl);
//     }

//     private bool IsTileBeingGathered(GameObject tile)
//     {
//         return activeGatheringProcesses.Exists(p => p.tile == tile);
//     }

//     private void ExecuteGathering(EnvironmentControl envControl)
//     {
//         int requiredPopulation = envControl.requiredGatheringPopulation;
//         if (aiPopulationManager.GetAvailablePopulation() < requiredPopulation)
//         {
//             //Debug.Log($"[AIGatheringManager] Not enough AI population to gather from {envControl.name}.");
//             return;
//         }

//         //Debug.Log($"[AIGatheringManager] AI is gathering from {envControl.name}");

//         envControl.isBeingAIGathered = true;
//         envControl.SetGatherIconActive(true);
//         envControl.lastAIWorker = this.GetComponentInParent<AIPlayer>();
//         envControl.gatheringTurnsLeft = envControl.gatheringTurnsRequired;
//         envControl.gatheringTurnsCompleted = 0;
//         envControl.currentGatheringFailureChance = envControl.initialGatheringFailureChance;

//         aiPopulationManager.UsePopulation(requiredPopulation);

//         activeGatheringProcesses.Add(new GatheringProcess()
//         {
//             tile = envControl.gameObject,
//             turnsRemaining = envControl.gatheringTurnsRequired
//         });
//     }

//     private void ProcessGatheringTurns()
//     {
//         for (int i = activeGatheringProcesses.Count - 1; i >= 0; i--)
//         {
//             GatheringProcess process = activeGatheringProcesses[i];
            
//             // Check if the tile has been destroyed
//             if (process.tile == null)
//             {
//                 activeGatheringProcesses.RemoveAt(i);
//                 continue;
//             }

//             EnvironmentControl envControl = process.tile.GetComponent<EnvironmentControl>();
//             if (envControl == null)
//             {
//                 activeGatheringProcesses.RemoveAt(i);
//                 continue;
//             }

//             // Decrease failure chance relative to turns completed.
//             float failureChance = envControl.initialGatheringFailureChance / (envControl.gatheringTurnsCompleted + 1);
//             bool failed = Random.value <= failureChance / 100f;

//             if (failed)
//             {
//                 HandleGatheringFailure(envControl);
//                 activeGatheringProcesses.RemoveAt(i);
//                 continue;
//             }

//             // Reduce turns remaining.
//             process.turnsRemaining--;
//             envControl.gatheringTurnsLeft--;

//             if (process.turnsRemaining <= 0)
//             {
//                 CompleteGathering(envControl);
//                 activeGatheringProcesses.RemoveAt(i);
//             }
//         }
//     }

//     private void HandleGatheringFailure(EnvironmentControl envControl)
//     {
//         //Debug.Log($"[AIGatheringManager] AI gathering failed at {envControl.name}!");

//         aiPopulationManager.ApplyFailurePenalty(envControl.populationGatheringPenaltyOnFailure);
//         aiPopulationManager.ReleasePopulation(envControl.requiredGatheringPopulation);

//         envControl.isBeingAIGathered = false;
//         envControl.SetGatherIconActive(false); 
//         ApplyRandomAIDisease(envControl.gatheringDiseaseIDs);
//         UpdateGatherableTiles();
//     }

//     private void CompleteGathering(EnvironmentControl envControl)
//     {
//         //Debug.Log($"[AIGatheringManager] AI successfully gathered from {envControl.name}");

//         Dictionary<string, int> gatheredResources = GatherResources(envControl);

//         foreach (var resource in gatheredResources)
//         {
//             //Debug.Log($"[AIGatheringManager] Adding {resource.Value} of {resource.Key} to AI inventory.");
            
//             // **Check resource classification BEFORE adding**
//             Resource resData = ResourceManager.Instance.GetResourceByID(resource.Key);
//             if (resData == null)
//             {
//                 //Debug.LogError($"[AIGatheringManager] Resource {resource.Key} not found in ResourceManager!");
//                 continue;
//             }

//             //Debug.Log($"[AIGatheringManager] Resource {resource.Key} type: {resData.resourceType}");

//             aiInventoryManager.AddResource(resource.Key, resource.Value);
//         }

//         aiLevelManager.AddXP(envControl.gatheringEXPReward);

//         aiPopulationManager.ReleasePopulation(envControl.requiredGatheringPopulation);
//         envControl.isBeingAIGathered = false;
//         envControl.SetGatherIconActive(false);
//         UpdateGatherableTiles();
//     }

//     private Dictionary<string, int> GatherResources(EnvironmentControl envControl)
//     {
//         int populationUsed = envControl.requiredGatheringPopulation;
//         Dictionary<string, int> gatheredResources = new Dictionary<string, int>();

//         int totalFoodGathered = 0;
//         int totalMaterialGathered = 0;

//         HashSet<string> gatheredResourceIDs = new HashSet<string>();

//         //Debug.Log($"[AIGatheringManager] Gathering from {envControl.name} - Population Used: {populationUsed}");

//         // ✅ **Gather GUARANTEED resources first**
//         foreach (string guaranteedResourceID in envControl.guaranteedResourceIDs)
//         {
//             ResourceAmount resource = envControl.resources.Find(r => r.resourceID == guaranteedResourceID);
//             if (resource != null && resource.currentAmount > 0)
//             {
//                 // **Ensure AI has unlocked this resource**
//                 Resource resData = aiResourceManager.GetAIResourceByID(resource.resourceID);
//                 if (resData == null || !aiResourceManager.IsResourceUnlocked(resData))
//                 {
//                     //Debug.Log($"[AIGatheringManager] Skipping {resource.resourceID} - Not unlocked by AI.");
//                     continue;
//                 }

//                 int amountToGather = Random.Range(resource.minAmount, resource.maxAmount) * populationUsed;
//                 amountToGather = Mathf.Min(amountToGather, resource.currentAmount);

//                 if (gatheredResources.ContainsKey(resource.resourceID))
//                     gatheredResources[resource.resourceID] += amountToGather;
//                 else
//                     gatheredResources[resource.resourceID] = amountToGather;

//                 resource.currentAmount -= amountToGather;

//                 if (resData.resourceType == ResourceType.Food)
//                     totalFoodGathered += amountToGather;
//                 else if (resData.resourceType == ResourceType.Material)
//                     totalMaterialGathered += amountToGather;

//                 gatheredResourceIDs.Add(resource.resourceID);

//                 //Debug.Log($"[AIGatheringManager] Gathered GUARANTEED {amountToGather} {resource.resourceID} from {envControl.name}");
//             }
//         }

//         // ✅ **Prepare list of remaining resources to gather based on chance**
//         List<ResourceAmount> weightedResources = new List<ResourceAmount>();
//         foreach (var resource in envControl.resources)
//         {
//             if (gatheredResourceIDs.Contains(resource.resourceID) || resource.currentAmount <= 0)
//                 continue;

//             // **Ensure AI has unlocked this resource**
//             Resource resData = aiResourceManager.GetAIResourceByID(resource.resourceID);
//             if (resData == null || !aiResourceManager.IsResourceUnlocked(resData))
//             {
//                 //Debug.Log($"[AIGatheringManager] Skipping {resource.resourceID} - Not unlocked by AI.");
//                 continue;
//             }

//             for (int i = 0; i < resource.gatheringChance; i++)
//             {
//                 weightedResources.Add(resource);
//             }
//         }

//         // ✅ **Randomly gather resources based on weighted chance**
//         int resourcesToGather = Random.Range(1, weightedResources.Count + 1);
//         for (int i = 0; i < resourcesToGather; i++)
//         {
//             if (weightedResources.Count > 0)
//             {
//                 ResourceAmount resource = weightedResources[Random.Range(0, weightedResources.Count)];
//                 if (resource != null && resource.currentAmount > 0)
//                 {
//                     int amountToGather = Random.Range(resource.minAmount, resource.maxAmount) * populationUsed;
//                     amountToGather = Mathf.Min(amountToGather, resource.currentAmount);

//                     if (gatheredResources.ContainsKey(resource.resourceID))
//                         gatheredResources[resource.resourceID] += amountToGather;
//                     else
//                         gatheredResources[resource.resourceID] = amountToGather;

//                     resource.currentAmount -= amountToGather;

//                     // ✅ Track total gathered food & materials
//                     Resource resData = aiResourceManager.GetAIResourceByID(resource.resourceID);
//                     if (resData != null)
//                     {
//                         if (resData.resourceType == ResourceType.Food)
//                             totalFoodGathered += amountToGather;
//                         else if (resData.resourceType == ResourceType.Material)
//                             totalMaterialGathered += amountToGather;
//                     }

//                     gatheredResourceIDs.Add(resource.resourceID);
//                     //Debug.Log($"[AIGatheringManager] Gathered {amountToGather} {resource.resourceID} from {envControl.name}");
//                 }
//             }
//         }

//         // ✅ **Check AI Inventory Capacity**
//         int availableFoodSpace = aiInventoryManager.maxFoodInventory - aiInventoryManager.GetTotalNonWaterFoodAmount();
//         int availableMaterialSpace = aiInventoryManager.maxMaterialInventory - aiInventoryManager.GetTotalMaterialAmount();

//         // ✅ **Reduce gathered resources if exceeding inventory capacity**
//         if (totalFoodGathered > availableFoodSpace)
//         {
//             float reductionFactor = (float)availableFoodSpace / totalFoodGathered;
//             AdjustResourceAmounts(gatheredResources, ResourceType.Food, reductionFactor);
//         }

//         if (totalMaterialGathered > availableMaterialSpace)
//         {
//             float reductionFactor = (float)availableMaterialSpace / totalMaterialGathered;
//             AdjustResourceAmounts(gatheredResources, ResourceType.Material, reductionFactor);
//         }

//         return gatheredResources;
//     }

//     private void AdjustResourceAmounts(Dictionary<string, int> gatheredResources, ResourceType resourceType, float reductionFactor)
//     {
//         var updatedResources = new List<KeyValuePair<string, int>>();

//         foreach (var resource in gatheredResources)
//         {
//             Resource resourceDetails = ResourceManager.Instance.GetResourceByID(resource.Key);
//             if (resourceDetails != null && resourceDetails.resourceType == resourceType)
//             {
//                 int adjustedAmount = Mathf.FloorToInt(resource.Value * reductionFactor);
//                 adjustedAmount = Mathf.Max(0, adjustedAmount);
//                 updatedResources.Add(new KeyValuePair<string, int>(resource.Key, adjustedAmount));
//             }
//         }

//         foreach (var updatedResource in updatedResources)
//         {
//             gatheredResources[updatedResource.Key] = updatedResource.Value;
//         }
//     }

//     public List<GameObject> GetGatherableTiles()
//     {
//         return gatherableTiles;
//     }

//     public List<GatheringProcess> GetActiveGatheringProcesses()
//     {
//         return activeGatheringProcesses;
//     }

//     public void AddGatherableTile(GameObject tile)
//     {
//         if (tile == null) return;

//         EnvironmentControl envControl = tile.GetComponent<EnvironmentControl>();
//         if (envControl != null && !gatherableTiles.Contains(tile))
//         {
//             gatherableTiles.Add(tile);
//             //Debug.Log($"[AIGatheringManager] {tile.name} added to gatherable tiles.");
//         }
//     }

//     private readonly HashSet<string> excludedFoodIDs = new HashSet<string> { "WFR", "WCT", "SPF" };

//     public int CalculatePotentialGathering(ResourceType type, bool isWater = false)
//     {
//         int totalPotential = 0;

//         foreach (GameObject tile in gatherableTiles)
//         {
//             EnvironmentControl envControl = tile.GetComponent<EnvironmentControl>();
//             if (envControl == null) continue;

//             foreach (ResourceAmount resource in envControl.resources)
//             {
//                 Resource aiResource = aiResourceManager.GetAIResourceByID(resource.resourceID);
//                 if (aiResource == null || aiResource.resourceType != type)
//                     continue;

//                 // ✅ If checking for water, only count WFR
//                 if (isWater && resource.resourceID != "WFR")
//                     continue;

//                 // ✅ If checking for food, exclude water-related resources
//                 if (!isWater && excludedFoodIDs.Contains(resource.resourceID))
//                     continue;

//                 int averageYield = (resource.minAmount + resource.maxAmount) / 2;
//                 totalPotential += averageYield * envControl.requiredGatheringPopulation;
//             }
//         }

//         return totalPotential;
//     }

//     /// **🔹 Apply a random disease from the environment control's gathering diseases**
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

//     public AIGatheringManagerSaveData SaveState()
//     {
//         AIGatheringManagerSaveData data = new AIGatheringManagerSaveData();
        
//         // Save gatherable tiles (their name and position).
//         data.gatherableTiles = new List<AIGatheringTileSaveData>();
//         foreach (GameObject tile in gatherableTiles)
//         {
//             if (tile == null) continue;
//             AIGatheringTileSaveData tsd = new AIGatheringTileSaveData();
//             tsd.tileName = tile.name;
//             tsd.position = tile.transform.position;
//             data.gatherableTiles.Add(tsd);
//         }
        
//         // Save active gathering processes.
//         data.activeGatheringProcesses = new List<GatheringProcessSaveData>();
//         foreach (GatheringProcess process in activeGatheringProcesses)
//         {
//             if (process.tile == null) continue;
//             GatheringProcessSaveData procData = new GatheringProcessSaveData();
//             procData.tileName = process.tile.name;
//             procData.turnsRemaining = process.turnsRemaining;
//             data.activeGatheringProcesses.Add(procData);
//         }
        
//         return data;
//     }

//     public void LoadState(AIGatheringManagerSaveData data)
//     {
//         if (data == null) return;
        
//         // Clear current lists.
//         gatherableTiles.Clear();
//         activeGatheringProcesses.Clear();
        
//         // Process saved gatherable tiles.
//         foreach (AIGatheringTileSaveData tsd in data.gatherableTiles)
//         {
//             GameObject foundTile = FindTileByNameAndPosition(tsd.tileName, tsd.position);
//             if (foundTile != null)
//             {
//                 // Add the found tile if it’s not already in the list.
//                 if (!gatherableTiles.Contains(foundTile))
//                     gatherableTiles.Add(foundTile);
//             }
//         }
        
//         // Process saved gathering processes.
//         // First, build a lookup of all EnvironmentControl objects in the scene.
//         EnvironmentControl[] allTiles = FindObjectsOfType<EnvironmentControl>();
//         Dictionary<string, GameObject> tileLookup = new Dictionary<string, GameObject>();
//         foreach (EnvironmentControl tile in allTiles)
//         {
//             if (!tileLookup.ContainsKey(tile.gameObject.name))
//                 tileLookup.Add(tile.gameObject.name, tile.gameObject);
//         }
//         foreach (GatheringProcessSaveData procData in data.activeGatheringProcesses)
//         {
//             if (tileLookup.TryGetValue(procData.tileName, out GameObject tile))
//             {
//                 GatheringProcess process = new GatheringProcess();
//                 process.tile = tile;
//                 process.turnsRemaining = procData.turnsRemaining;
//                 activeGatheringProcesses.Add(process);
//             }
//         }
        
//         UpdateGatherableTiles();
//     }

//     // Helper method to find a tile by name and position (with tolerance).
//     private GameObject FindTileByNameAndPosition(string tileName, Vector3 savedPosition, float tolerance = 0.1f)
//     {
//         EnvironmentControl[] allTiles = FindObjectsOfType<EnvironmentControl>();
//         foreach (EnvironmentControl tile in allTiles)
//         {
//             if (tile.gameObject.name == tileName &&
//                 Vector3.Distance(tile.transform.position, savedPosition) <= tolerance)
//             {
//                 return tile.gameObject;
//             }
//         }
        
//         // Optionally, search children.
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
//     public class GatheringProcess
//     {
//         public GameObject tile;
//         public int turnsRemaining;
//     }
// }