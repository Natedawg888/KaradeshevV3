// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;

// public class GlobalMovementTracker : MonoBehaviour
// {
//     // Set this identifier in the Inspector or via code
//     // to indicate which AI player this tracker is for.
//     public string aiPlayerID;

//     // Maps a target tile (UnitControl) to the list of unit groups moving toward that tile.
//     private Dictionary<UnitControl, List<MilitiaUnitGroup>> groupsMovingTo = new Dictionary<UnitControl, List<MilitiaUnitGroup>>();

//     /// Registers a unit group as moving toward the given target tile.
//     /// Only registers if the group belongs to the AI with matching aiPlayerID.
//     public void RegisterMovement(MilitiaUnitGroup group, UnitControl targetTile)
//     {
//         if (group == null || targetTile == null)
//             return;

//         // Only track unit groups that belong to this AI.
//         if (group.aiPlayerID != aiPlayerID)
//             return;

//         if (!groupsMovingTo.ContainsKey(targetTile))
//         {
//             groupsMovingTo[targetTile] = new List<MilitiaUnitGroup>();
//         }
//         if (!groupsMovingTo[targetTile].Contains(group))
//         {
//             groupsMovingTo[targetTile].Add(group);
//         }
//     }

//     /// Unregisters a unit group from moving toward the given target tile.
//     public void UnregisterMovement(MilitiaUnitGroup group, UnitControl targetTile)
//     {
//         if (group == null || targetTile == null)
//             return;

//         if (groupsMovingTo.ContainsKey(targetTile))
//         {
//             groupsMovingTo[targetTile].Remove(group);
//         }
//     }

//     /// Returns a list of unit groups (belonging to this AI) that are currently moving to the given target tile.
//     public List<MilitiaUnitGroup> GetGroupsMovingTo(UnitControl targetTile)
//     {
//         if (targetTile == null || !groupsMovingTo.ContainsKey(targetTile))
//             return new List<MilitiaUnitGroup>();

//         // Filter the list to be extra sure—though all registered groups should already match.
//         return groupsMovingTo[targetTile].FindAll(g => g.aiPlayerID == aiPlayerID);
//     }

//     /// Clears the tracker—this might be used at the end of a turn.
//     public void Clear()
//     {
//         groupsMovingTo.Clear();
//     }
// }