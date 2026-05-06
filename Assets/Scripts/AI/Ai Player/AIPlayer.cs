// using System;
// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;

// public class AIPlayer : MonoBehaviour
// {
//     [Header("AI Player ID")]
//     public string aiPlayerID;

//     [Header("AI Discovery Status")]
//     public bool isDiscovered = false; // ✅ AI starts undiscovered

//     [Header("Starter Tile Prefab")]
//     public GameObject aiStarterTilePrefab;

//     [Header("Required Settings")]
//     public EnvironmentType requiredEnvironment;
//     public TileSize requiredTileSize;
//     public int requiredPlayerLevel = 1;

//     [Header("AI Player Properties")]
//     public Color aiColor = Color.red;
//     public string aiName = "AI Player";

//     [Header("Random Name Settings")]
//     public List<string> possibleNames = new List<string>() { "Alpha", "Bravo", "Charlie", "Delta", "Echo" };

//     [Header("Level Range Settings")]
//     public int minLevel = 1;
//     public int maxLevel = 5;
//     public int levelCap = 5;
//     public int aiLevel;

//     [Header("Priority Settings")]
//     public List<EnvironmentType> priorityNeighborEnvironments = new List<EnvironmentType>();

//     [Header("Population Range Settings")]
//     public int minAIPopulation = 5;
//     public int maxAIPopulation = 15;
//     public int minAIMaxPopulation = 30;
//     public int maxAIMaxPopulation = 50;

//     [Header("AI Inventory Settings")]
//     public int startingFoodInventoryCapacity = 100;
//     public int startingMaterialInventoryCapacity = 200;

//     [Header("Starting Resources (Set Resources, Random Amounts)")]
//     public List<AIStartingResource> startingResources = new List<AIStartingResource>();

//     [Header("AI Aggression Settings")]
//     public int aggressionLevel; // Random aggression level from 1 (least aggressive) to 20 (most aggressive)

//     public int prefabIndex;

//     private static int aiCounter = 0;
//     private GameObject starterTileInstance;
//     private AILevelManager levelManager;
//     private AITileTracker aiTileTracker;
//     private AIBuildingManager aiBuildingManager;
//     public GlobalMovementTracker movementTracker;

//     public void Initialize()
//     {
//         aiPlayerID = Guid.NewGuid().ToString();

//         levelManager = GetComponentInChildren<AILevelManager>();
//         aiTileTracker = GetComponentInChildren<AITileTracker>();
//         aiBuildingManager = GetComponentInChildren<AIBuildingManager>();
//         movementTracker = GetComponentInChildren<GlobalMovementTracker>();
//         if(movementTracker != null)
//         {
//             // Assign this AI player's ID to the movement tracker.
//             movementTracker.aiPlayerID = this.aiPlayerID;
//         }

//         if (levelManager == null)
//         {
//             //Debug.LogError("[AIPlayer] Missing AILevelManager component!");
//             return;
//         }

//         // ✅ Generate a light AI color (high brightness, moderate saturation)
//         aiColor = GenerateLightColor();

//         aiLevel = UnityEngine.Random.Range(minLevel, maxLevel + 1);
//         levelManager.aiLevel = aiLevel;
//         levelManager.maxLevel = levelCap;

//         // Randomly set the aggression level with a maximum of 20.
//         aggressionLevel = UnityEngine.Random.Range(1, 21);

//         // ✅ Register AI Player in Registry
//         AIPlayerRegistry.Instance.RegisterAIPlayer(this);

//         // **Use a cached TileManager reference**
//         TileManager tileManager = TileManager.Instance;
//         if (tileManager == null)
//         {
//             //Debug.LogError("[AIPlayer] TileManager instance is missing!");
//             return;
//         }

//         List<EnvironmentControl> matchingTiles = tileManager.GetTilesByTypeAndSize(requiredEnvironment, requiredTileSize);
//         if (matchingTiles.Count == 0)
//         {
//             //Debug.LogWarning("[AIPlayer] No matching tile found.");
//             return;
//         }

//         // **Select the best available tile**
//         EnvironmentControl chosenTile = SelectPrioritizedTile(matchingTiles);
//         if (chosenTile == null)
//         {
//             chosenTile = matchingTiles[UnityEngine.Random.Range(0, matchingTiles.Count)];
//         }

//         Vector3 tilePosition = chosenTile.transform.position;

//         // **✅ Remove the tile from TileManager efficiently**
//         tileManager.tileList.Remove(chosenTile);
//         Vector2Int tileGridPosition = new Vector2Int(Mathf.RoundToInt(tilePosition.x), Mathf.RoundToInt(tilePosition.z));
//         tileManager.tileGrid.Remove(tileGridPosition);

//         // **Instantiate AI Starter Tile**
//         if (aiStarterTilePrefab != null)
//         {
//             starterTileInstance = Instantiate(aiStarterTilePrefab, tilePosition, Quaternion.identity);
//         }
//         else
//         {
//             starterTileInstance = new GameObject("AIStarterTile");
//             starterTileInstance.transform.position = tilePosition;
//         }

//         // **Destroy the old tile asynchronously to reduce lag**
//         StartCoroutine(DestroyTileAsync(chosenTile));

//         // ✅ **Apply AI Color to the Starter Tile's `AIBuildingState`**
//         AIBuildingState buildingState = starterTileInstance.GetComponent<AIBuildingState>();
//         if (buildingState != null)
//         {
//             buildingState.isDiscovered = isDiscovered;
//             buildingState.UpdateBuildingMaterialForAI(this);
//             //Debug.Log($"[AIPlayer] Applied AI color to starter tile {starterTileInstance.name}.");
//         }
//         else
//         {
//             //Debug.LogWarning($"[AIPlayer] Starter tile {starterTileInstance.name} does not have an AIBuildingState component!");
//         }

//         // **Initialize AI Components in one loop instead of multiple GetComponent calls**
//         foreach (MonoBehaviour component in GetComponentsInChildren<MonoBehaviour>())
//         {
//             if (component is AIPopulationManager popManager)
//             {
//                 int startingPop = UnityEngine.Random.Range(minAIPopulation, maxAIPopulation + 1);
//                 int maxPop = UnityEngine.Random.Range(minAIMaxPopulation, maxAIMaxPopulation + 1);
//                 popManager.SetPopulationValues(startingPop, maxPop);
//                 popManager.InitializeUI(starterTileInstance);
//             }
//             else if (component is AITileTracker tileTracker)
//             {
//                 tileTracker.SetStarterTile(starterTileInstance);
//             }
//             else if (component is AITileDiscoveryManager discoveryManager)
//             {
//                 discoveryManager.SetStarterTile(starterTileInstance);
//             }
//             else if (component is AIInventoryManager inventoryManager)
//             {
//                 inventoryManager.SetInventoryCapacity(startingFoodInventoryCapacity, startingMaterialInventoryCapacity);
//                 InitializeStartingResources(inventoryManager);
//             }
//             else if (component is AIBuildingManager aiBuildingManager)
//             {
//                 aiBuildingManager.SetStarterTile(starterTileInstance);
//             }
//         }

//         aiName = possibleNames.Count > 0 ? possibleNames[UnityEngine.Random.Range(0, possibleNames.Count)] : "AI Player";

//         aiCounter++;
//         this.gameObject.name = $"{aiName} (Level {aiLevel}) AI Player (AI #{aiCounter})";
//     }

//     public GameObject StarterTileInstance
//     {
//         get { return starterTileInstance; }
//     }

//     /// ✅ Generates a **light AI color** by ensuring high brightness and moderate saturation.
//     private Color GenerateLightColor()
//     {
//         if (AIColorRegistry.Instance != null)
//         {
//             return AIColorRegistry.Instance.GetUniqueAIColor();
//         }
//         else
//         {
//             //Debug.LogWarning("[AIPlayer] AIColorRegistry is missing! Generating random AI color.");
//             return new Color(UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0.4f, 0.6f), UnityEngine.Random.Range(0.7f, 1f));
//         }
//     }

//     /// **🔹 Mark AI as Discovered**
//     public void SetAIDiscovered()
//     {
//         if (!isDiscovered)
//         {
//             isDiscovered = true;
//             //Debug.Log($"[AIPlayer] AI {aiName} has been discovered!");
//         }
//     }

//     /// **🔹 Destroy tile asynchronously to prevent performance spikes**
//     private IEnumerator DestroyTileAsync(EnvironmentControl tile)
//     {
//         yield return new WaitForEndOfFrame(); // Let Unity finish its frame processing
//         if (tile != null)
//         { 
//                 Destroy(tile.transform.parent.gameObject);
//                 Destroy(tile.gameObject);
//         }
//     }

//     private EnvironmentControl SelectPrioritizedTile(List<EnvironmentControl> candidates)
//     {
//         EnvironmentControl bestCandidate = null;
//         int bestPriorityCount = -1;
//         int bfsMaxDepth = 3; // BFS max depth for priority tile search

//         // **1️⃣ First, check if a tile is **directly next to** a priority neighbor**
//         foreach (EnvironmentControl candidate in candidates)
//         {
//             List<GameObject> adjacentTiles = aiTileTracker.GetNeighboringTiles(candidate.gameObject);
//             int priorityCount = 0;

//             foreach (GameObject neighbor in adjacentTiles)
//             {
//                 if (neighbor == null) continue;
//                 EnvironmentControl neighborEnv = neighbor.GetComponent<EnvironmentControl>();

//                 if (neighborEnv != null && priorityNeighborEnvironments.Contains(neighborEnv.environmentType))
//                 {
//                     priorityCount++;
//                 }
//             }

//             // **Pick the tile with the highest priority neighbors**
//             if (priorityCount > bestPriorityCount)
//             {
//                 bestPriorityCount = priorityCount;
//                 bestCandidate = candidate;
//             }
//         }

//         if (bestCandidate != null)
//         {
//             //Debug.Log($"[AIPlayer] ✅ Selected {bestCandidate.name} (Direct priority neighbor count: {bestPriorityCount})");
//             return bestCandidate;
//         }

//         // **2️⃣ If no adjacent priority neighbor is found, perform BFS search**
//         Queue<(EnvironmentControl tile, int depth)> bfsQueue = new Queue<(EnvironmentControl, int)>();
//         HashSet<EnvironmentControl> visitedTiles = new HashSet<EnvironmentControl>();

//         foreach (EnvironmentControl candidate in candidates)
//         {
//             bfsQueue.Enqueue((candidate, 0));
//             visitedTiles.Add(candidate);
//         }

//         while (bfsQueue.Count > 0)
//         {
//             (EnvironmentControl currentTile, int depth) = bfsQueue.Dequeue();

//             if (depth >= bfsMaxDepth) continue; // Stop BFS at max depth

//             List<GameObject> neighboringTiles = aiTileTracker.GetNeighboringTiles(currentTile.gameObject);

//             foreach (GameObject neighbor in neighboringTiles)
//             {
//                 EnvironmentControl neighborEnv = neighbor.GetComponent<EnvironmentControl>();

//                 if (neighborEnv != null && priorityNeighborEnvironments.Contains(neighborEnv.environmentType))
//                 {
//                     //Debug.Log($"[AIPlayer] ✅ Selected {currentTile.name} (Found priority environment in BFS at depth {depth})");
//                     return currentTile;
//                 }

//                 if (neighborEnv != null && !visitedTiles.Contains(neighborEnv))
//                 {
//                     bfsQueue.Enqueue((neighborEnv, depth + 1));
//                     visitedTiles.Add(neighborEnv);
//                 }
//             }
//         }

//         // **3️⃣ Fallback to a random tile if no priority tile is found**
//         //Debug.LogWarning("[AIPlayer] ⚠ No priority tiles found. Selecting a random tile.");
//         return candidates[UnityEngine.Random.Range(0, candidates.Count)];
//     }

//     private void InitializeStartingResources(AIInventoryManager inventoryManager)
//     {
//         if (inventoryManager == null)
//         {
//             //Debug.LogError("[AIPlayer] AIInventoryManager is missing! Cannot initialize resources.");
//             return;
//         }

//         if (startingResources == null || startingResources.Count == 0)
//         {
//             //Debug.LogWarning("[AIPlayer] No starting resources defined.");
//             return;
//         }

//         //Debug.Log($"[AIPlayer] Initializing {startingResources.Count} starting resources.");

//         foreach (var resource in startingResources)
//         {
//             if (resource == null || string.IsNullOrEmpty(resource.resourceID))
//                 continue;

//             int amount = UnityEngine.Random.Range(resource.minAmount, resource.maxAmount + 1);

//             if (amount > 0)
//             {
//                 inventoryManager.AddResource(resource.resourceID, amount);
//                 //Debug.Log($"[AIPlayer] Added {amount} of {resource.resourceID} to AI inventory.");
//             }
//         }
//     }

//     public GameObject GetStarterTile()
//     {
//         return starterTileInstance;
//     }

//     public AIPlayerSaveData SaveState()
//     {
//         AIPlayerSaveData data = new AIPlayerSaveData();
//         data.aiPlayerID = aiPlayerID;
//         data.isDiscovered = isDiscovered;
//         data.aiName = aiName;
//         data.aiLevel = aiLevel;
//         data.aggressionLevel = aggressionLevel;

//         // Save which prefab in the AIManager list was used
//         data.prefabIndex = prefabIndex;

//         // If your aiStarterTilePrefab is a building loaded from "Buildings" folder, store its name:
//         if (aiStarterTilePrefab != null)
//         {
//             data.aiStarterBuildingName = aiStarterTilePrefab.name;
//         }

//         if (starterTileInstance != null)
//         {
//             Vector3 pos = starterTileInstance.transform.position;
//             data.aiStarterBuildingPosition = new float[] { pos.x, pos.y, pos.z };
//         }

//         var popManager = GetComponentInChildren<AIPopulationManager>();
//         if (popManager != null)
//         {
//             data.populationData = popManager.SaveState();
//         }
        
//         AIPopulationIncreasePlan popPlan = GetComponentInChildren<AIPopulationIncreasePlan>();
//         if (popPlan != null)
//         {
//             data.populationIncreasePlanData = popPlan.SaveState();
//         }
        
//         AIDiseaseManager aiDiseaseManager = GetComponentInChildren<AIDiseaseManager>();
//         if (aiDiseaseManager != null)
//         {
//             data.aiDiseaseManagerData = aiDiseaseManager.SaveState();
//         }

//         AITileDiscoveryManager tileDiscoveryManager = GetComponentInChildren<AITileDiscoveryManager>();
//         if (tileDiscoveryManager != null)
//         {
//             data.aiTileDiscoveryData = tileDiscoveryManager.SaveState();
//         }

//          // Save gathering manager data.
//         AIGatheringManager gatheringManager = GetComponentInChildren<AIGatheringManager>();
//         if (gatheringManager != null)
//         {
//             data.aiGatheringManagerData = gatheringManager.SaveState();
//         }

//         AIResourcePriorityCalculator resourcePriorityCalculator = GetComponentInChildren<AIResourcePriorityCalculator>();
//         if (resourcePriorityCalculator != null)
//         {
//             data.aiResourcePriorityData = resourcePriorityCalculator.SaveState();
//         }

//         AITechnologyManager techManager = GetComponentInChildren<AITechnologyManager>();
//         if (techManager != null)
//         {
//             data.aiTechnologyManagerData = techManager.SaveState();
//         }

//         AICraftingUnlockManager craftingUnlockManager = GetComponentInChildren<AICraftingUnlockManager>();
//         if (craftingUnlockManager != null)
//         {
//             data.aiCraftingUnlockData = craftingUnlockManager.SaveState();
//         }

//         // NEW: Save production unlock manager data
//         AIProductionUnlockManager productionUnlockManager = GetComponentInChildren<AIProductionUnlockManager>();
//         if (productionUnlockManager != null)
//         {
//             data.aiProductionUnlockData = productionUnlockManager.SaveState();
//         }

//         AILevelManager levelManager = GetComponentInChildren<AILevelManager>();
//         if (levelManager != null)
//         {
//             data.aiLevelManagerData = levelManager.SaveState();
//         }
        
//         AIInventoryManager aiInventory = GetComponentInChildren<AIInventoryManager>();
//         if (aiInventory != null)
//         {
//             data.aiInventoryManagerData = aiInventory.SaveState();
//         }

//         AIBuildingManager bldgManager = GetComponentInChildren<AIBuildingManager>();
//         if (bldgManager != null)
//         {
//             data.aiBuildingManagerData = bldgManager.SaveState();
//         }

//         // If you want to store the color:
//         data.aiColorRGBA = new float[] { aiColor.r, aiColor.g, aiColor.b, aiColor.a };

//         return data;
//     }

//     public void LoadState(AIPlayerSaveData data)
//     {
//         if (data == null) return;

//         aiPlayerID = data.aiPlayerID;
//         isDiscovered = data.isDiscovered;
//         aiName = data.aiName;
//         aiLevel = data.aiLevel;
//         prefabIndex = data.prefabIndex;
//         aggressionLevel = data.aggressionLevel;

//         if (data.aiColorRGBA != null && data.aiColorRGBA.Length == 4)
//         {
//             aiColor = new Color(data.aiColorRGBA[0], data.aiColorRGBA[1],
//                                 data.aiColorRGBA[2], data.aiColorRGBA[3]);
//         }

//         if (!string.IsNullOrEmpty(data.aiStarterBuildingName))
//         {
//             GameObject[] buildingPrefabs = Resources.LoadAll<GameObject>("Buildings");
//             foreach (var prefab in buildingPrefabs)
//             {
//                 if (prefab.name == data.aiStarterBuildingName)
//                 {
//                     aiStarterTilePrefab = prefab;
//                     break;
//                 }
//             }
//         }

//         // Now get the building's position if we saved it
//         Vector3 buildingPos = Vector3.zero;
//         if (data.aiStarterBuildingPosition != null && data.aiStarterBuildingPosition.Length == 3)
//         {
//             buildingPos = new Vector3(
//                 data.aiStarterBuildingPosition[0],
//                 data.aiStarterBuildingPosition[1],
//                 data.aiStarterBuildingPosition[2]
//             );
//         }

//         // If we actually want to re-instantiate the building:
//         if (aiStarterTilePrefab != null)
//         {
//             starterTileInstance = Instantiate(aiStarterTilePrefab, buildingPos, Quaternion.identity);

//             // Then apply discovered state, etc.
//             var buildingState = starterTileInstance.GetComponent<AIBuildingState>();
//             if (buildingState != null)
//             {
//                 buildingState.isDiscovered = isDiscovered;
//                 buildingState.UpdateBuildingMaterialForAI(this);
//             }
//         }

//         var popManager = GetComponentInChildren<AIPopulationManager>();
//         if (popManager != null && data.populationData != null)
//         {
//             popManager.LoadState(data.populationData);
//         }

//         AIPopulationIncreasePlan popPlan = GetComponentInChildren<AIPopulationIncreasePlan>();
//         if (popPlan != null && data.populationIncreasePlanData != null)
//         {
//             popPlan.LoadState(data.populationIncreasePlanData);
//         }

//         if (data.aiDiseaseManagerData != null)
//         {
//             AIDiseaseManager aiDiseaseManager = GetComponentInChildren<AIDiseaseManager>();
//             if (aiDiseaseManager != null)
//             {
//                 aiDiseaseManager.LoadState(data.aiDiseaseManagerData);
//             }
//         }
        
//         if (data.aiTileDiscoveryData != null)
//         {
//             StartCoroutine(DelayedDiscoveryManagerLoad(data.aiTileDiscoveryData));
//         }

//         if (data.aiGatheringManagerData != null)
//         {
//             StartCoroutine(DelayedGatheringManagerLoad(data.aiGatheringManagerData));
//         }

//         // NEW: Load resource priority data.
//         if (data.aiResourcePriorityData != null)
//         {
//             AIResourcePriorityCalculator resourcePriorityCalculator = GetComponentInChildren<AIResourcePriorityCalculator>();
//             if (resourcePriorityCalculator != null)
//             {
//                 resourcePriorityCalculator.LoadState(data.aiResourcePriorityData);
//             }
//         }

//         if (data.aiTechnologyManagerData != null)
//         {
//             AITechnologyManager techManager = GetComponentInChildren<AITechnologyManager>();
//             if (techManager != null)
//             {
//                 techManager.LoadState(data.aiTechnologyManagerData);
//             }
//         }

//         if (data.aiCraftingUnlockData != null)
//         {
//             AICraftingUnlockManager craftingUnlockManager = GetComponentInChildren<AICraftingUnlockManager>();
//             if (craftingUnlockManager != null)
//             {
//                 craftingUnlockManager.LoadState(data.aiCraftingUnlockData);
//             }
//         }

//         if (data.aiProductionUnlockData != null)
//         {
//             AIProductionUnlockManager productionUnlockManager = GetComponentInChildren<AIProductionUnlockManager>();
//             if (productionUnlockManager != null)
//             {
//                 productionUnlockManager.LoadState(data.aiProductionUnlockData);
//             }
//         }

//         if (data.aiLevelManagerData != null)
//         {
//             AILevelManager levelManager = GetComponentInChildren<AILevelManager>();
//             if (levelManager != null)
//             {
//                 levelManager.LoadState(data.aiLevelManagerData);
//             }
//         }

//         if (data.aiInventoryManagerData != null)
//         {
//             AIInventoryManager aiInventory = GetComponentInChildren<AIInventoryManager>();
//             if (aiInventory != null)
//             {
//                 aiInventory.LoadState(data.aiInventoryManagerData);
//             }
//         }

//         if (data.aiBuildingManagerData != null)
//         {
//             AIBuildingManager bldgManager = GetComponentInChildren<AIBuildingManager>();
//             if (bldgManager != null)
//             {
//                 StartCoroutine(DelayedBuildingManagerLoad(bldgManager, data.aiBuildingManagerData));
//             }
//         }
//     }

//     private IEnumerator DelayedDiscoveryManagerLoad(AITileDiscoveryManagerSaveData discoveryData)
//     {
//         // Wait for a short delay to ensure other components are ready.
//         yield return new WaitForSeconds(0.5f);
//         AITileDiscoveryManager tileDiscoveryManager = GetComponentInChildren<AITileDiscoveryManager>();
//         if (tileDiscoveryManager != null)
//         {
//             tileDiscoveryManager.LoadState(discoveryData);
//         }
//     }

//     private IEnumerator DelayedGatheringManagerLoad(AIGatheringManagerSaveData gatheringData)
//     {
//         // Wait for a short delay to ensure other components are ready.
//         yield return new WaitForSeconds(0.5f); // Adjust delay as needed

//         AIGatheringManager gatheringManager = GetComponentInChildren<AIGatheringManager>();
//         if (gatheringManager != null)
//         {
//             gatheringManager.LoadState(gatheringData);
//         }
//     }

//     private IEnumerator DelayedBuildingManagerLoad(AIBuildingManager bldgManager, AIBuildingManagerSaveData buildingData)
//     {
//         // Wait half a second (or however long you need)
//         yield return new WaitForSeconds(0.5f);

//         // Now load once other components have had time to initialize
//         bldgManager.LoadState(buildingData);
//     }
// }

// [Serializable]
// public class AIStartingResource
// {
//     public string resourceID;
//     public int minAmount;
//     public int maxAmount;
// }