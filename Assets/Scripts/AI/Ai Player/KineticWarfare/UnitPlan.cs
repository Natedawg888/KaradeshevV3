// using System.Collections.Generic;
// using UnityEngine;

// /// Defines the types of plans that a unit can execute.
// public enum UnitPlanType
// {
//     Idle,
//     Move,
//     Patrol,
//     StopPatrol,
//     Disband,
//     Split,
//     Merge,
//     Attack,
//     Communicate
// }

// /// Represents a unit plan for a MilitiaUnitGroup, including the plan type and any related parameters.
// [System.Serializable]
// public class UnitPlan
// {
//     // The unit group associated with this plan.
//     public MilitiaUnitGroup unitGroup;

//     // The type of plan to execute.
//     public UnitPlanType planType;

//     // For movement plans, you can store the movement path positions.
//     public List<Vector3> movementPath = new List<Vector3>();

//     // Optional: For plans like merge or attack, you might want to store a target unit group.
//     public MilitiaUnitGroup targetUnitGroup;

//     // Optional: For plans that require a target tile (e.g., move or attack), store the target UnitControl.
//     public UnitControl targetUnitControl;
//     public int priority;

//     // Constructor to quickly create a new plan.
//     public UnitPlan(MilitiaUnitGroup group, UnitPlanType planType)
//     {
//         this.unitGroup = group;
//         this.planType = planType;
//         priority = 0;
//     }
// }