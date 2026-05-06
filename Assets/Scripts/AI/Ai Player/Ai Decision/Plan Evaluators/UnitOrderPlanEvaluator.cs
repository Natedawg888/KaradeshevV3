// using System.Collections;
// using System.Collections.Generic;
// using System.Linq;
// using UnityEngine;

// public class UnitOrderPlanEvaluator : MonoBehaviour
// {
//     public AIInventoryManager    inventoryManager;
//     public AIPopulationManager   populationManager;
//     public AIUnitManager         unitManager;
//     public AIPlanner             planner;        // your existing high‐level planner, to ask for reserved pop
//     public AIPlayer              aiPlayer;

//     void Awake()
//     {
//         aiPlayer          = GetComponentInParent<AIPlayer>();
//         inventoryManager  = aiPlayer.GetComponentInChildren<AIInventoryManager>();
//         populationManager = aiPlayer.GetComponentInChildren<AIPopulationManager>();
//         unitManager       = aiPlayer.GetComponentInChildren<AIUnitManager>();
//         planner           = aiPlayer.GetComponentInChildren<AIPlanner>();
//     }

//     /// Returns a priority score for ordering `unit` × `multiplier`.
//     public int EvaluateUnitOrderPlan(MilitiaUnit unit, int multiplier)
//     {
//         if (unit == null) return 0;

//         // 1) Calculate current and used population
//         int totalPop        = populationManager.GetAvailablePopulation();
//         int usedByUnits     = unitManager.GetAIUnitGroups()
//                                 .Sum(g => g.totalUnits * g.unitType.requiredPopulation);

//         // 2) Count population in training across all AI KineticWarfare buildings
//         int usedByTraining  = aiPlayer.GetComponentsInChildren<KineticWarfareControl>()
//                                 .SelectMany(k => k.GetActiveTrainingSlots())
//                                 .Where(s => s.unitBeingTrained != null)
//                                 .Sum(s => s.unitBeingTrained.requiredPopulation
//                                             * s.unitBeingTrained.outputUnits
//                                             * s.multiplier);

//         // 3) Reserve pop for any higher‐priority planner tasks
//         int reservedForTasks = planner.GetReservedPopulation(); // implement in your AIPlanner

//         int availablePop    = totalPop - usedByUnits - usedByTraining - reservedForTasks;
//         if (availablePop < unit.requiredPopulation * unit.outputUnits * multiplier)
//             return 0; // can’t afford population

//         // 4) Check food/water surplus (need 5 turns’ worth)
//         float survivalFactor = GetSurvivalFactor();
//         if (survivalFactor < 1f)
//             return 0; // don’t train if under survival pressure

//         // 5) Base priority & aggression boost
//         int basePriority  = 50; 
//         int aggroBoost    = aiPlayer.aggressionLevel * 5;
//         int priority      = basePriority + aggroBoost;

//         // 6) Penalize resource deficits
//         int costPenalty   = 0;
//         foreach (var req in unit.resourceCost)
//         {
//             int want = req.amount * multiplier * 3;
//             int have = inventoryManager.GetResourceAmount(req.resourceID);
//             if (have < want)
//                 costPenalty += (want - have) * 10;
//         }

//         priority -= costPenalty;
//         return Mathf.Max(priority, 0);
//     }

//     /// Returns a [0..1] factor: 1 = full surplus, 0 = in crisis.
//     private float GetSurvivalFactor()
//     {
//         int pop = populationManager.GetCurrentPopulation();
//         int foodNeed  = pop * populationManager.foodConsumptionPerPerson;
//         int waterNeed = pop * populationManager.waterConsumptionPerPerson;
//         if (foodNeed == 0 || waterNeed == 0) 
//             return 1f;

//         int foodStock  = inventoryManager.GetTotalNonWaterFoodAmount();
//         int waterStock = inventoryManager.GetResourceAmount("WFR");

//         // require 3 turns’ worth before building units
//         bool okFood  = foodStock  >= foodNeed  * 3;
//         bool okWater = waterStock >= waterNeed * 3;
//         return (okFood && okWater) ? 1f : 0f;
//     }
// }