// using UnityEngine;
// using System.Collections.Generic;

// public enum AIPlanType
// {
//     Discovery,
//     Gathering,
//     PopIncrease,
//     Building,
//     Repair,
//     Crafting,
//     Research,
//     StorageIn,
//     StorageOut,
//     StartProduction,
//     ResumeProduction,
//     CancelProduction,
//     CollectProducedGoods,
//     OrderUnit,
//     SplitUnitGroup,
//     MergeUnitGroup,
//     DisbandUnitGroup,
//     MoveUnitGroup,
//     TrainUnitGroup
// }

// [System.Serializable]
// public class StorageResourceEntry
// {
//     public string resourceID;
//     public int amount;

//     public StorageResourceEntry(string id, int amt)
//     {
//         resourceID = id;
//         amount = amt;
//     }
// }

// [System.Serializable]
// public class AIPlan
// {
//     [Header("Main Settings")]
//     public AIPlanType planType;           // Type of AI action.
//     public GameObject target;             // The tile/resource being acted upon.
//     public int priority;                  // Higher value = higher priority.
    
//     [Header("For Building")]
//     public Building selectedBuilding;     // Stores the planned building.
    
//     [Header("For Research")]
//     public Technology selectedTechnology; // Stores the planned technology research.
    
//     [Header("For Crafting")]
//     public CraftedItem craftedItem;       // Stores the planned crafted item.
//     public int turnsWithoutResources = 0; // Counter for how many turns the plan lacked resources.

//     [Header("For Storage")]
//     // New field for storage tasks: can hold multiple resource entries.
//     public List<StorageResourceEntry> storageEntries;

//     [Header("For Production")]
//     public ProductionItem productionItem;

//     [Header("For Unit Orders")]
//     public MilitiaUnit unitToOrder;       // Stores the unit type to be ordered.
//     public int orderMultiplier;

//     [Header("For Unit Management")]
//     public MilitiaUnitGroup unitGroup;

//     public AIPlan(AIPlanType type, GameObject target, int priority)
//     {
//         this.planType = type;
//         this.target = target;
//         this.priority = priority;
//     }
// }