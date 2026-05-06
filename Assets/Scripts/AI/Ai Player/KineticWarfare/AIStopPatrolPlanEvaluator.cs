// using UnityEngine;
// using System.Linq;

// public static class AIStopPatrolPlanEvaluator
// {
//     public static int CalculateStopPatrolPriority(
//         MilitiaUnitGroup  group,
//         AIBuildingManager buildingManager,
//         AIUnitManager     unitManager)
//     {
//         if (group == null)
//             return 0;

//         // 1) If *any other* group in your AIUnitManager is under attack → absolute top priority
//         if (unitManager.GetAIUnitGroups()
//                        .Where(g => g != group)
//                        .Any(g => g.isUnderAttack))
//         {
//             return 500;
//         }

//         // 2) Otherwise, if any of *your* buildings are under attack → very high priority
//         if (buildingManager.GetOwnedBuildings()
//                            .Select(go => go.GetComponent<AIBuildingControl>())
//                            .Where(bc => bc != null)
//                            .Any(bc => bc.isUnderAttack))
//         {
//             return 500;
//         }

//         // 3) Otherwise, no StopPatrol needed
//         return 0;
//     }
// }