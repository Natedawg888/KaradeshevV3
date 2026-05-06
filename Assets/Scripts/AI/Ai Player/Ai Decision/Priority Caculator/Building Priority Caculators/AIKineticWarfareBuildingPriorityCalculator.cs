// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
// using System.Linq;

// public class AIKineticWarfareBuildingPriorityCalculator : MonoBehaviour
// {
//     private AIPlayer aiPlayer;

//     private void Start()
//     {
//         aiPlayer = GetComponentInParent<AIPlayer>();
//         if (aiPlayer == null)
//         {
//             Debug.LogWarning("[AIKineticWarfareBuildingPriorityCalculator] AIPlayer not found in parent.");
//             enabled = false;
//         }
//     }

//     /// Calculates priority for KineticWarfare buildings by looking at the prefab's
//     /// KineticWarfareControl component and comparing its unit list to other AIs'.
//     public int CalculateKineticWarfareBuildingPriority(Building building, int basePriority)
//     {
//         if (building == null || building.buildingPrefab == null)
//             return basePriority;

//         // 1) grab the KineticWarfareControl off the prefab
//         var kwControl = building.buildingPrefab.GetComponent<KineticWarfareControl>();
//         if (kwControl == null)
//             return basePriority;

//         // 2) core priority: base + aggression + no‐units bonus
//         var aiUnitMgr = aiPlayer.GetComponentInChildren<AIUnitManager>();
//         int unitCount = aiUnitMgr?.aiUnitGroups.Count ?? 0;

//         int priority = basePriority
//                     + 60
//                     + aiPlayer.aggressionLevel * 10
//                     + (unitCount == 0 ? 200 : 0);

//         // 3) check how many KW buildings we already have
//         var myBM = aiPlayer.GetComponentInChildren<AIBuildingManager>();
//         var ownedKW = myBM.GetOwnedBuildings()
//                         .Select(go => go.GetComponent<KineticWarfareControl>())
//                         .Where(k => k != null)
//                         .ToList();

//         // 4) if we have *none*, give a big “first KW” boost and skip sibling logic
//         if (ownedKW.Count == 0)
//         {
//             priority += 300;  // tweak to taste
//             return Mathf.Max(priority, 0);
//         }

//         // 5) otherwise collect all unitIDs from our *other* KW buildings
//         var siblingTrainable = new HashSet<string>();
//         foreach (var siblingKW in ownedKW)
//         {
//             if (siblingKW == kwControl) continue;
//             foreach (var u in siblingKW.trainableUnits)
//                 siblingTrainable.Add(u.unitID);
//         }

//         // 6) count how many of *this* building’s units are unique among siblings
//         int exclusives = kwControl.trainableUnits
//                         .Select(u => u.unitID)
//                         .Distinct()
//                         .Count(id => !siblingTrainable.Contains(id));

//         const int EXCLUSIVE_BONUS = 50;
//         priority += exclusives * EXCLUSIVE_BONUS;

//         return Mathf.Max(priority, 0);
//     }
// }