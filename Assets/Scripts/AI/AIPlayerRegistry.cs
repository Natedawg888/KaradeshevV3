// using System;
// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;

// [Serializable]
// public class AIPlayerRegistryData {
//     public List<string> aiPlayerIDs;
// }

// public class AIPlayerRegistry : MonoBehaviour
// {
//     private static AIPlayerRegistry instance;

//     public static AIPlayerRegistry Instance
//     {
//         get
//         {
//             if (instance == null)
//             {
//                 GameObject registryObject = new GameObject("AIPlayerRegistry");
//                 instance = registryObject.AddComponent<AIPlayerRegistry>();
//                 DontDestroyOnLoad(registryObject); // Ensure persistence across scenes
//             }
//             return instance;
//         }
//     }

//     private Dictionary<string, AIPlayer> aiPlayers = new Dictionary<string, AIPlayer>();

//     public void RegisterAIPlayer(AIPlayer aiPlayer)
//     {
//         if (aiPlayer == null || string.IsNullOrEmpty(aiPlayer.aiPlayerID))
//         {
//             Debug.LogWarning("[AIPlayerRegistry] Cannot register AI player: Invalid data.");
//             return;
//         }

//         if (aiPlayers.ContainsKey(aiPlayer.aiPlayerID))
//         {
//             Debug.LogWarning($"[AIPlayerRegistry] AI Player with ID {aiPlayer.aiPlayerID} is already registered.");
//             return;
//         }

//         aiPlayers.Add(aiPlayer.aiPlayerID, aiPlayer);
//         Debug.Log($"[AIPlayerRegistry] Registered AI Player {aiPlayer.aiName} (ID: {aiPlayer.aiPlayerID}).");
//     }

//     public void UnregisterAIPlayer(AIPlayer aiPlayer)
//     {
//         if (aiPlayer == null || string.IsNullOrEmpty(aiPlayer.aiPlayerID))
//         {
//             Debug.LogWarning("[AIPlayerRegistry] Cannot unregister AI player: Invalid data.");
//             return;
//         }

//         if (aiPlayers.Remove(aiPlayer.aiPlayerID))
//         {
//             Debug.Log($"[AIPlayerRegistry] Unregistered AI Player {aiPlayer.aiName} (ID: {aiPlayer.aiPlayerID}).");
//         }
//         else
//         {
//             Debug.LogWarning($"[AIPlayerRegistry] AI Player ID {aiPlayer.aiPlayerID} not found.");
//         }
//     }

//     public AIPlayer GetAIPlayerByID(string aiPlayerID)
//     {
//         if (aiPlayers.TryGetValue(aiPlayerID, out AIPlayer aiPlayer))
//         {
//             return aiPlayer;
//         }
//         Debug.LogWarning($"[AIPlayerRegistry] No AI Player found with ID: {aiPlayerID}.");
//         return null;
//     }

//     public List<AIPlayer> GetAllAIPlayers()
//     {
//         return new List<AIPlayer>(aiPlayers.Values);
//     }

//     public AIPlayerRegistryData SaveState()
//     {
//         AIPlayerRegistryData data = new AIPlayerRegistryData();
//         data.aiPlayerIDs = new List<string>(aiPlayers.Keys);
//         return data;
//     }

//     public void LoadState(AIPlayerRegistryData data)
//     {
//         if (data == null) return;
        
//         // Clear current registry.
//         aiPlayers.Clear();
        
//         // For each saved AI player ID, try to find the corresponding AIPlayer in the scene
//         AIPlayer[] allAIPlayers = FindObjectsOfType<AIPlayer>();
//         foreach (string id in data.aiPlayerIDs)
//         {
//             AIPlayer ai = System.Array.Find(allAIPlayers, player => player.aiPlayerID == id);
//             if (ai != null)
//             {
//                 RegisterAIPlayer(ai);
//             }
//         }
//         Debug.Log("[AIPlayerRegistry] Restored registry state.");
//     }
// }