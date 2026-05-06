// using UnityEngine;

// public static class AIDisbandPlanEvaluator
// {
//     public static int CalculateDisbandPriority(
//         MilitiaUnitGroup    group,
//         AIPopulationManager popMgr,
//         AIInventoryManager  invMgr,
//         int                  survivalTurns = 3)
//     {
//         if (group == null || !group.isAiOwned)
//             return 0;

//         // 1) pull in both free and reserved pop
//         int freePop = popMgr.GetAvailablePopulation();
//         int usedPop = popMgr.GetUsedPopulation();               // NEW: expose usedPopulation
//         int totalPool = freePop + usedPop;

//         // 2) compute how much pop this group itself requires
//         int thisGroupCost = group.totalUnits * group.unitType.requiredPopulation;

//         // 3) if you'd exceed your actual pool, disband immediately
//         if (totalPool - thisGroupCost <= (totalPool * 0.3f))
//             return 1000;

//         // 4) fallback: survival over next N turns
//         const int WINDOW = 3;
//         int currentPop   = popMgr.GetCurrentPopulation();
//         int foodPerTurn  = currentPop * popMgr.foodConsumptionPerPerson;
//         int waterPerTurn = currentPop * popMgr.waterConsumptionPerPerson;
//         int haveFood     = invMgr.GetTotalNonWaterFoodAmount();
//         int haveWater    = invMgr.GetResourceAmount("WFR");

//         float foodFrac  = (foodPerTurn  > 0)
//             ? Mathf.Min(1f, (float)haveFood  / (foodPerTurn  * WINDOW))
//             : 1f;
//         float waterFrac = (waterPerTurn > 0)
//             ? Mathf.Min(1f, (float)haveWater / (waterPerTurn * WINDOW))
//             : 1f;
//         float survivalFactor = Mathf.Min(foodFrac, waterFrac);

//         // 5) upkeep shortfall
//         float upkeepShortfall = 0f;
//         foreach (var req in group.unitType.upkeepCost)
//         {
//             int needed = req.amount * group.totalUnits;
//             int onHand = invMgr.GetResourceAmount(req.resourceID);
//             if (onHand < needed)
//                 upkeepShortfall += (needed - onHand);
//         }

//         // 6) score: up to +300 for starvation, +10 per missing upkeep point
//         float score = 0f;
//         score += (1f - survivalFactor) * 300f;
//         score += upkeepShortfall * 10f;

//         return Mathf.Clamp(Mathf.RoundToInt(score), 0, 1000);
//     }
// }