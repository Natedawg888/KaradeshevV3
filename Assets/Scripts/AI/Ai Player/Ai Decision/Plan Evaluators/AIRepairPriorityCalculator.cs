// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;

// public class AIRepairPriorityCalculator : MonoBehaviour
// {
//     private AIInventoryManager inventoryManager;

//     private void Start()
//     {
//         inventoryManager = GetComponentInParent<AIPlayer>()?.GetComponentInChildren<AIInventoryManager>();
//     }

//     public int CalculateRepairPriority(AIBuildingControl building)
//     {
//         if (building == null || building.healthSlider == null)
//             return 0;

//         float maxHealth = building.healthSlider.maxValue;
//         float currentHealth = building.health;
//         float healthPercentage = currentHealth / maxHealth;
//         //Debug.Log($"[AIRepairPriorityCalculator] {building.buildingID} health: {currentHealth}/{maxHealth} = {healthPercentage:F2}");

//         // Base priority scales linearly with damage.
//         int basePriority = Mathf.RoundToInt((1 - healthPercentage) * 200);
//         //Debug.Log($"[AIRepairPriorityCalculator] Base priority from damage: {basePriority}");

//         // Bonus adjustments based on health thresholds.
//         if (healthPercentage < 0.75f)
//         {
//             basePriority += 250;
//             //Debug.Log($"[AIRepairPriorityCalculator] Health below 75%: +50 bonus");
//         }
//         if (healthPercentage < 0.5f)
//         {
//             basePriority += 450;
//             //Debug.Log($"[AIRepairPriorityCalculator] Health below 50%: +100 bonus");
//         }
//         if (healthPercentage < 0.4f)
//         {
//             basePriority += 650;
//             //Debug.Log($"[AIRepairPriorityCalculator] Health below 40%: +300 bonus");
//         }

//         int finalPriority = Mathf.Clamp(basePriority, 0, 1000);
//         //Debug.Log($"[AIRepairPriorityCalculator] Final repair priority for {building.buildingID}: {finalPriority}");
//         return finalPriority;
//     }
// }