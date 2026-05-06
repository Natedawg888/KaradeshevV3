// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;

// public class CancelProductionPlanEvaluator : MonoBehaviour
// {
//     public AIResourcePriorityCalculator resourcePriorityCalculator;
//     public AIInventoryManager inventoryManager;
//     public AIPopulationManager populationManager;

//     public int EvaluateCancelProductionPlan(ProductionItem item, ProductionBuildingControl productionControl, int reservedPopulation)
//     {
//         // If item or its outputs are null, cancellation is urgent.
//         if (item == null || item.outputResources == null)
//         {
//             return int.MaxValue;
//         }
        
//         int cancelPriority = 0;
        
//         // 1. Evaluate the average output resource priority.
//         float sumOutputPriority = 0f;
//         int count = 0;
//         foreach (var output in item.outputResources)
//         {
//             if (output != null)
//             {
//                 float rp = resourcePriorityCalculator.GetResourcePriority(output.resourceID);
//                 sumOutputPriority += rp;
//                 count++;
//             }
//         }
//         float avgOutputPriority = (count > 0) ? sumOutputPriority / count : 0f;
        
//         // If the average output priority is low, add a penalty.
//         // (For example, if avgOutputPriority is 80, then (100 - 80) = 20 cancellation points are added.)
//         if (avgOutputPriority < 100f)
//         {
//             cancelPriority += Mathf.RoundToInt(100f - avgOutputPriority);
//         }
        
//         // 2. Check if the production has been paused for too long with no output.
//         if (productionControl != null && productionControl.IsPaused() && productionControl.TurnsPaused >= 4 && productionControl.totalStoredAmount == 0)
//         {
//             cancelPriority += 100;
//             Debug.Log($"[CancelEvaluator] Adding 100 for being paused too long on {item.itemName}.");
//         }
        
//         // 3. Check if population is urgently needed elsewhere.
//         int availablePop = populationManager.GetAvailablePopulation();
//         if (reservedPopulation > availablePop)
//         {
//             cancelPriority += 50;
//             Debug.Log($"[CancelEvaluator] Adding 50 for population shortage on {item.itemName}.");
//         }
        
//         // 4. Check resource deficiencies for production costs.
//         if (item.resourceCost != null)
//         {
//             foreach (var req in item.resourceCost)
//             {
//                 if (req == null)
//                     continue;
                
//                 if (!inventoryManager.HasEnoughResource(req.resourceID, req.amount))
//                 {
//                     cancelPriority += 100;
//                     Debug.Log($"[CancelEvaluator] Adding 100 for insufficient resource {req.resourceID} on {item.itemName}.");
//                 }
//             }
//         }
        
//         Debug.Log($"[CancelEvaluator] Final cancel priority for {item.itemName}: {cancelPriority}");
//         return cancelPriority;
//     }
// }