// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;

// public class AIManager : MonoBehaviour
// {
//     public static AIManager Instance;

//     // A list of AIPlayer prefabs (make sure each prefab has the AIPlayer script with its assigned aiStarterTilePrefab).
//     public List<GameObject> aiPlayerPrefabs = new List<GameObject>();

//     // Maximum number of AI players allowed.
//     public int maxAIPlayers = 3;

//     // List to keep track of active AI players.
//     private List<AIPlayer> activeAIPlayers = new List<AIPlayer>();

//     void Awake()
//     {
//         if (Instance == null)
//             Instance = this;
//         else
//             Destroy(gameObject);
//     }

//     void Start()
//     {
//         // // Wait until all tiles are activated before starting AI spawns.
//         // TileActivator tileActivator = FindObjectOfType<TileActivator>();
//         // if (tileActivator != null)
//         // {
//         //     tileActivator.OnTilesActivated += OnTilesActivated;
//         // }
//         // else
//         // {
//         //     Debug.LogWarning("TileActivator not found. Proceeding to subscribe to turn events immediately.");
//         //     OnTilesActivated();
//         // }
//     }

//     // Called once all tiles are activated.
//     private void OnTilesActivated()
//     {
//         // Subscribe to TurnSystem's start-of-turn event.
//         TurnSystem.SubscribeToStartOfTurn(HandleStartOfTurn);
//     }

//     // This method is called at the start of every turn.
//     private void HandleStartOfTurn()
//     {
//         // Get the current player level.
//         int playerLevel = PlayerLevel.Instance.GetCurrentLevel();

//         // Filter AIPlayer prefabs by allowed level:
//         List<GameObject> eligiblePrefabs = new List<GameObject>();
//         foreach (GameObject prefab in aiPlayerPrefabs)
//         {
//             AIPlayer aiPlayerComponent = prefab.GetComponent<AIPlayer>();
//             if (aiPlayerComponent != null)
//             {
//                 // Allowed if the prefab's required level is equal to, one less, or one greater than the player's level.
//                 if (Mathf.Abs(aiPlayerComponent.requiredPlayerLevel - playerLevel) <= 1)
//                 {
//                     eligiblePrefabs.Add(prefab);
//                 }
//             }
//         }

//         // If we haven't reached max AI players, spawn one new AI player.
//         if (activeAIPlayers.Count < maxAIPlayers)
//         {
//             CreateAIPlayerFromEligible(eligiblePrefabs);
//         }

//         // Additionally, every 5 turns, if there are still fewer than max AI players, spawn one extra.
//         if (TurnSystem.GetCurrentTurn() % 5 == 0 && activeAIPlayers.Count < maxAIPlayers)
//         {
//             CreateAIPlayerFromEligible(eligiblePrefabs);
//         }
//     }

//     // Creates a new AI player from the list of eligible prefabs.
//     private void CreateAIPlayerFromEligible(List<GameObject> eligiblePrefabs)
//     {
//         if (eligiblePrefabs == null || eligiblePrefabs.Count == 0)
//         {
//             Debug.LogWarning("No eligible AIPlayer prefabs available for the current player level.");
//             return;
//         }

//         int randomIndex = Random.Range(0, eligiblePrefabs.Count);
//         GameObject selectedPrefab = eligiblePrefabs[randomIndex];

//         // Instantiate the prefab.
//         GameObject aiPlayerGO = Instantiate(selectedPrefab);
//         AIPlayer aiPlayer = aiPlayerGO.GetComponent<AIPlayer>();
//         if (aiPlayer == null)
//         {
//             Debug.LogError("The instantiated prefab does not have an AIPlayer component!");
//             return;
//         }

//         aiPlayer.prefabIndex = randomIndex;

//         // Initialize the AI player (this will set its color, name, and place its starter tile).
//         aiPlayer.Initialize();

//         // Add to the list of active AI players.
//         activeAIPlayers.Add(aiPlayer);

//         Debug.Log($"Created AI Player '{aiPlayer.gameObject.name}' (Total AI: {activeAIPlayers.Count})");
//     }

//     public GameObject SpawnAIFromData(AIPlayerSaveData data)
//     {
//         // Check range
//         if (data.prefabIndex < 0 || data.prefabIndex >= aiPlayerPrefabs.Count)
//         {
//             Debug.LogWarning($"[AIManager] Prefab index {data.prefabIndex} out of range!");
//             return null;
//         }

//         // Grab the prefab from the list
//         GameObject selectedPrefab = aiPlayerPrefabs[data.prefabIndex];
//         if (selectedPrefab == null)
//         {
//             Debug.LogError($"[AIManager] Null prefab at index {data.prefabIndex}!");
//             return null;
//         }

//         // Instantiate it
//         GameObject aiPlayerGO = Instantiate(selectedPrefab);
//         AIPlayer aiPlayer = aiPlayerGO.GetComponent<AIPlayer>();
//         if (aiPlayer == null)
//         {
//             Debug.LogError("SpawnAIFromData: The instantiated prefab does not have an AIPlayer component!");
//             return null;
//         }

//         // Set the same index so it can be saved again
//         aiPlayer.prefabIndex = data.prefabIndex;

//         // Optionally call Initialize if needed
//         aiPlayer.Initialize();

//         // Add to active list
//         activeAIPlayers.Add(aiPlayer);

//         return aiPlayerGO;
//     }
// }