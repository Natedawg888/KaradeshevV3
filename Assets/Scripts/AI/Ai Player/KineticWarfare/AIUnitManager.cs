// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
// using System.Linq;

// public class AIUnitManager : MonoBehaviour
// {
//     public static AIUnitManager Instance { get; private set; }
    
//     // List to hold all AI-controlled MilitiaUnitGroup instances.
//     public List<MilitiaUnitGroup> aiUnitGroups = new List<MilitiaUnitGroup>();

//     private void Awake()
//     {
//         if (Instance == null)
//             Instance = this;
//         else
//             Destroy(gameObject);
//     }

//     public void AddAIUnitGroup(MilitiaUnitGroup group)
//     {
//         if (group != null && group.isAiOwned && !aiUnitGroups.Contains(group))
//         {
//             aiUnitGroups.Add(group);
//             Debug.Log($"Added AI unit group: {group.groupID}");
//         }
//     }

//     public void RemoveAIUnitGroup(MilitiaUnitGroup group)
//     {
//         if (group != null && group.isAiOwned && aiUnitGroups.Contains(group))
//         {
//             aiUnitGroups.Remove(group);
//             Debug.Log($"Removed AI unit group: {group.groupID}");
//         }
//     }

//     public List<MilitiaUnitGroup> GetAIUnitGroups()
//     {
//         return new List<MilitiaUnitGroup>(aiUnitGroups);
//     }

//     public List<MilitiaUnit> GetAvailableTrainableUnitsForAI(AIPlayer aiPlayer)
//     {
//         List<MilitiaUnit> availableUnits = new List<MilitiaUnit>();

//         AIBuildingManager buildingManager = aiPlayer.GetComponentInChildren<AIBuildingManager>();
//         if (buildingManager == null) return availableUnits;

//         foreach (GameObject building in buildingManager.GetOwnedBuildings())
//         {
//             KineticWarfareControl warfareControl = building.GetComponent<KineticWarfareControl>();
//             if (warfareControl != null)
//             {
//                 foreach (var unit in warfareControl.trainableUnits)
//                 {
//                     if (unit.isResearched && !availableUnits.Any(u => u.unitID == unit.unitID))
//                     {
//                         availableUnits.Add(unit);
//                     }
//                 }
//             }
//         }

//         return availableUnits;
//     }
// }