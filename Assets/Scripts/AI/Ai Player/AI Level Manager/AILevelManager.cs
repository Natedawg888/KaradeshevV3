// using System;
// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;

// public class AILevelManager : MonoBehaviour
// {
//     [Header("AI Level Settings")]
//     public int aiLevel = 1;
//     public int maxLevel = 10; // Set by AIPlayer
//     public int currentXP = 0;

//     private LevelManager levelManager;
//     private AIPlayer aiPlayer; 
//     private AIResourceManager aiResourceManager;

//     public event Action<int> OnLevelUp; // Event to notify other systems

//     private void Awake()
//     {
//         aiPlayer = GetComponentInParent<AIPlayer>(); // Use cached reference
//         if (aiPlayer == null)
//         {
//             //Debug.LogError("[AILevelManager] No AIPlayer found! AI level syncing disabled.");
//             enabled = false;
//             return;
//         }

//         aiResourceManager = aiPlayer.GetComponentInChildren<AIResourceManager>();
//     }

//     private void Start()
//     {
//         levelManager = FindObjectOfType<LevelManager>();
//         if (levelManager == null)
//         {
//             //Debug.LogError("[AILevelManager] No LevelManager found in the scene!");
//             return;
//         }

//         //Debug.Log($"[AILevelManager] Initialized at Level {aiLevel} with {currentXP} XP. Max Level: {maxLevel}");
//     }

//     /// **🔹 Add XP and check for level-up**
//     public void AddXP(int xpAmount)
//     {
//         if (aiLevel >= maxLevel || levelManager == null)
//             return;

//         currentXP += xpAmount;
//         //Debug.Log($"[AILevelManager] AI gained {xpAmount} XP (Total: {currentXP}/{GetXPToNextLevel()}).");

//         if (currentXP >= GetXPToNextLevel())
//         {
//             StartCoroutine(LevelUpRoutine()); // Delay to prevent frame lag
//         }
//     }

//     /// **🔹 Handles AI leveling up asynchronously**
//     private IEnumerator LevelUpRoutine()
//     {
//         yield return new WaitForEndOfFrame(); // Delay to avoid lag spikes

//         while (currentXP >= GetXPToNextLevel() && aiLevel < maxLevel)
//         {
//             aiLevel++;
//             currentXP -= GetXPToNextLevel();

//             //Debug.Log($"[AILevelManager] AI leveled up to {aiLevel}! Next level requires {GetXPToNextLevel()} XP.");

//             // ✅ Sync AIPlayer's Level
//             if (aiPlayer != null)
//             {
//                 aiPlayer.aiLevel = aiLevel;
//                 //Debug.Log($"[AILevelManager] Updated AIPlayer level to {aiPlayer.aiLevel}.");
//             }

//             // ✅ Unlock new resources for AI
//             aiResourceManager?.UnlockResourcesForAILevel(aiLevel);

//             OnLevelUp?.Invoke(aiLevel);
//         }
//     }

//     /// **🔹 Get XP Required for Next Level from `LevelManager`**
//     public int GetXPToNextLevel()
//     {
//         if (levelManager == null) return 0;

//         LevelData levelData = levelManager.GetLevelData(aiLevel);
//         return levelData != null ? levelData.xpRequired : int.MaxValue;
//     }

//     /// **🔹 Get AI Level**
//     public int GetAILevel() => aiLevel;

//     /// **🔹 Get Current XP**
//     public int GetCurrentXP() => currentXP;

//     public AILevelManagerSaveData SaveState()
//     {
//         AILevelManagerSaveData data = new AILevelManagerSaveData();
//         data.aiLevel = aiLevel;
//         data.maxLevel = maxLevel;
//         data.currentXP = currentXP;
//         return data;
//     }

//     public void LoadState(AILevelManagerSaveData data)
//     {
//         if (data == null) return;

//         aiLevel = data.aiLevel;
//         maxLevel = data.maxLevel;
//         currentXP = data.currentXP;

//         // Optionally: Sync the aiPlayer's aiLevel immediately.
//         if (aiPlayer != null)
//         {
//             aiPlayer.aiLevel = aiLevel;
//         }
//     }
// }