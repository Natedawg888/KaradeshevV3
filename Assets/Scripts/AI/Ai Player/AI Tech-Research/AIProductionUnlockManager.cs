// using System;
// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;

// [Serializable]
// public class AIProductionUnlockSaveData
// {
//     public List<string> unlockedProductionItems;
// }

// public class AIProductionUnlockManager : MonoBehaviour
// {
//     // Tracks unlocked production item IDs for the AI.
//     private HashSet<string> unlockedProductionItems = new HashSet<string>();

//     // Unlock a production item by its ID.
//     public void UnlockProductionItem(string itemID)
//     {
//         if (!unlockedProductionItems.Contains(itemID))
//         {
//             unlockedProductionItems.Add(itemID);
//             Debug.Log($"[AIProductionUnlockManager] Unlocked production item: {itemID}");
//         }
//     }

//     // Check if a production item is unlocked.
//     public bool IsProductionItemUnlocked(string itemID)
//     {
//         return unlockedProductionItems.Contains(itemID);
//     }

//     // Returns all unlocked production item IDs.
//     public IEnumerable<string> GetAllUnlockedProductionItems()
//     {
//         return unlockedProductionItems;
//     }

//     // Save the state by converting the HashSet to a List.
//     public AIProductionUnlockSaveData SaveState()
//     {
//         AIProductionUnlockSaveData data = new AIProductionUnlockSaveData();
//         data.unlockedProductionItems = new List<string>(unlockedProductionItems);
//         return data;
//     }

//     // Load the state by rebuilding the HashSet from the saved list.
//     public void LoadState(AIProductionUnlockSaveData data)
//     {
//         if (data == null) return;
//         unlockedProductionItems = new HashSet<string>(data.unlockedProductionItems);
//     }
// }