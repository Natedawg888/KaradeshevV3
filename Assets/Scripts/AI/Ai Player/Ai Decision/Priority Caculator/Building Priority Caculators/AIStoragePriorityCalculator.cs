// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;

// public class AIStoragePriorityCalculator : MonoBehaviour
// {
//     private AIPlayer aiPlayer;
//     private AIInventoryManager inventoryManager;

//     // You can adjust these thresholds and bonus values as needed.
//     private const float UsageThreshold = 0.75f;  // 75% usage before urgency kicks in
//     private const int MaxBonusPriority = 200;      // Maximum bonus to add when usage is 100%
//     private const float ExistingStorageUsageThreshold = 0.5f; // New threshold: storage building is half full or more
//     private const int ExtraBonusPerStorage = 50;  // Extra bonus for each storage building that is half full or more

//     private void Start()
//     {
//         aiPlayer = GetComponentInParent<AIPlayer>();
//         if (aiPlayer != null)
//         {
//             inventoryManager = aiPlayer.GetComponentInChildren<AIInventoryManager>();
//         }

//         if (inventoryManager == null)
//         {
//             //Debug.LogError("[AIStoragePriorityCalculator] Missing AIInventoryManager!");
//         }
//     }

//     // Now accepts a StorageBuildingControl parameter instead of Building.
//     public int CalculateStorageBuildingPriority(StorageBuildingControl storageControl, int basePriority)
//     {
//         if (storageControl == null || inventoryManager == null)
//         {
//             return basePriority;
//         }
        
//         float bonusFood = 0f;
//         float bonusMaterial = 0f;
        
//         // Calculate bonus for food storage if applicable.
//         if (storageControl.increasesFoodStorage)
//         {
//             int currentFood = inventoryManager.GetTotalFoodAmount();
//             int maxFood = inventoryManager.maxFoodInventory;
//             float foodUsageRatio = (float)currentFood / maxFood;
//             if (foodUsageRatio > UsageThreshold)
//             {
//                 // Scale the bonus so that if usage is 100%, bonusFood equals half of MaxBonusPriority.
//                 bonusFood = ((foodUsageRatio - UsageThreshold) / (1f - UsageThreshold)) * (MaxBonusPriority / 2f);
//             }
//         }
        
//         // Calculate bonus for material storage if applicable.
//         if (storageControl.increasesMaterialStorage)
//         {
//             int currentMaterial = inventoryManager.GetTotalMaterialAmount();
//             int maxMaterial = inventoryManager.maxMaterialInventory;
//             float materialUsageRatio = (float)currentMaterial / maxMaterial;
//             if (materialUsageRatio > UsageThreshold)
//             {
//                 // Scale similarly as food bonus.
//                 bonusMaterial = ((materialUsageRatio - UsageThreshold) / (1f - UsageThreshold)) * (MaxBonusPriority / 2f);
//             }
//         }
        
//         // Total bonus is the sum of the two.
//         int bonusPriority = Mathf.RoundToInt(bonusFood + bonusMaterial);
        
//         // Optionally, add an extra bonus if both food and material are over the threshold.
//         if (inventoryManager.GetTotalFoodAmount() > UsageThreshold * inventoryManager.maxFoodInventory &&
//             inventoryManager.GetTotalMaterialAmount() > UsageThreshold * inventoryManager.maxMaterialInventory)
//         {
//             int extraBonus = 50; // Adjust this extra bonus as needed.
//             bonusPriority += extraBonus;
//         }

//         int extraStorageBonus = 0;
//         AIBuildingManager buildingManager = aiPlayer.GetComponentInChildren<AIBuildingManager>();
//         if (buildingManager != null)
//         {
//             List<GameObject> ownedBuildings = buildingManager.GetOwnedBuildings();
//             foreach (GameObject b in ownedBuildings)
//             {
//                 if (b == null) continue;
//                 StorageBuildingControl existingStorage = b.GetComponent<StorageBuildingControl>();
//                 if (existingStorage != null)
//                 {
//                     float usageRatio = (float)existingStorage.GetTotalStoredAmount() / existingStorage.maxStorageCapacity;
//                     if (usageRatio >= ExistingStorageUsageThreshold)
//                     {
//                         extraStorageBonus += ExtraBonusPerStorage;
//                     }
//                 }
//             }
//         }

//         bonusPriority += extraStorageBonus;
        
//         int totalPriority = basePriority + bonusPriority;
        
//         return totalPriority;
//     }
// }