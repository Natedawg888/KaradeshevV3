// using System;
// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;

// [Serializable]
// public class AICraftingUnlockSaveData
// {
//     public List<string> unlockedCraftingItems;
// }

// public class AICraftingUnlockManager : MonoBehaviour
// {
//     // Tracks unlocked crafting item IDs for the AI.
//     private HashSet<string> unlockedCraftingItems = new HashSet<string>();

//     // Unlock a crafting item by its ID.
//     public void UnlockCraftingItem(string itemID)
//     {
//         if (!unlockedCraftingItems.Contains(itemID))
//         {
//             unlockedCraftingItems.Add(itemID);
//             Debug.Log($"[AICraftingUnlockManager] Unlocked crafting item: {itemID}");
//         }
//     }

//     // Check if a crafting item is unlocked.
//     public bool IsCraftingItemUnlocked(string itemID)
//     {
//         return unlockedCraftingItems.Contains(itemID);
//     }

//     // Returns all unlocked crafting item IDs.
//     public IEnumerable<string> GetAllUnlockedCraftingItems()
//     {
//         return unlockedCraftingItems;
//     }

//         // Save the state by converting the HashSet to a List for serialization.
//     public AICraftingUnlockSaveData SaveState()
//     {
//         AICraftingUnlockSaveData data = new AICraftingUnlockSaveData();
//         data.unlockedCraftingItems = new List<string>(unlockedCraftingItems);
//         return data;
//     }

//     // Load the state by rebuilding the HashSet from the saved list.
//     public void LoadState(AICraftingUnlockSaveData data)
//     {
//         if (data == null) return;
//         unlockedCraftingItems = new HashSet<string>(data.unlockedCraftingItems);
//     }
// }