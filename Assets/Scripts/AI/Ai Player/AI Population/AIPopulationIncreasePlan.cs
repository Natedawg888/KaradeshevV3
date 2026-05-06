// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;

// public class AIPopulationIncreasePlan : MonoBehaviour
// {
//     public AIPopulationManager populationManager;
//     public AIInventoryManager inventoryManager;
//     private AILevelManager aiLevelManager;
//     public AIPlayer aiPlayer; // Reference to AIPlayer
//     private GameObject aiStarterTile; // Correct reference to the starter tile
    
//     [Header("AI Population Growth Settings")]
//     public int foodForPopulationIncrease = 10;
//     public int turnsForIncrease = 4;
//     public float populationIncreaseFailureChance = 20f; // 20% failure chance
//     public int populationIncreaseExp = 10;

//     [Header("Active Population Increase Orders")]
//     [SerializeField] private List<PopulationIncreaseOrder> activePopulationOrders = new List<PopulationIncreaseOrder>();

//     private void Start()
//     {
//         aiPlayer = GetComponentInParent<AIPlayer>(); // Ensure reference to AIPlayer

//         if (aiPlayer == null)
//         {
//             //Debug.LogWarning("[AIPopulationIncreasePlan] AIPlayer is missing.");
//             return;
//         }

//         populationManager = aiPlayer.GetComponentInChildren<AIPopulationManager>();
//         inventoryManager = aiPlayer.GetComponentInChildren<AIInventoryManager>();
//         aiLevelManager = aiPlayer.GetComponentInChildren<AILevelManager>();
//         aiStarterTile = aiPlayer.GetStarterTile(); // Get the correct starter tile

//         if (populationManager == null || inventoryManager == null || aiStarterTile == null)
//         {
//             //Debug.LogWarning("[AIPopulationIncreasePlan] Missing required components.");
//         }

//         TurnSystem.SubscribeToEndOfTurn(ProcessPopulationIncrease);
//     }

//     private void OnDestroy()
//     {
//         TurnSystem.UnsubscribeFromEndOfTurn(ProcessPopulationIncrease);
//     }

//     public void AttemptPopulationIncrease()
//     {
//         if (inventoryManager == null || populationManager == null)
//         {
//             //DebugError("[AIPopulationIncreasePlan] InventoryManager or PopulationManager is missing!");
//             return;
//         }

//         // **Check if AI has enough food ("GFD")**
//         if (!inventoryManager.HasEnoughResource("GFD", foodForPopulationIncrease))
//         {
//             //DebugLog("[AIPopulationIncreasePlan] Not enough food (GFD) to increase population.");
//             return;
//         }

//         // **Check if there is available population to use**
//         if (populationManager.GetAvailablePopulation() < 2)
//         {
//             //DebugLog("[AIPopulationIncreasePlan] Not enough available AI population to start increase.");
//             return;
//         }

//         // **Consume the required food**
//         inventoryManager.RemoveResource("GFD", foodForPopulationIncrease);

//         // **Use 1 population**
//         populationManager.UsePopulation(1);

//         // **Check if an existing order can be stacked**
//         PopulationIncreaseOrder existingOrder = activePopulationOrders.Find(order => order.remainingTurns == turnsForIncrease);
//         if (existingOrder != null)
//         {
//             existingOrder.StackOrder();
//         }
//         else
//         {
//             PopulationIncreaseOrder newOrder = new PopulationIncreaseOrder(turnsForIncrease);
//             activePopulationOrders.Add(newOrder);
//         }

//         //DebugLog("[AIPopulationIncreasePlan] AI started a population increase process.");
//     }

//     private void ProcessPopulationIncrease()
//     {
//         List<PopulationIncreaseOrder> completedOrders = new List<PopulationIncreaseOrder>();
//         int totalFailedOrders = 0;
//         int totalPopulationLost = 0;
//         int totalSuccessfulIncreases = 0;

//         foreach (var order in activePopulationOrders)
//         {
//             int successfulIncreases = 0;
//             int failedIncreases = 0;

//             // **Calculate the reduced failure chance as the order progresses**
//             float failureChance = populationIncreaseFailureChance * ((float)order.remainingTurns / turnsForIncrease);

//             // **Process failures at any turn**
//             for (int i = 0; i < order.orderCount; i++)
//             {
//                 bool processFails = Random.value <= (failureChance / 100f); // Use updated failure chance

//                 if (processFails)
//                 {
//                     failedIncreases++;

//                     // **50% chance to lose the used population**
//                     bool losePopulation = Random.value <= 0.5f;
//                     if (losePopulation)
//                     {
//                         populationManager.ApplyFailurePenalty(1); // AI loses population
//                         totalPopulationLost++;
//                     }

//                     populationManager.ReleasePopulation(1);
//                 }
//             }

//             totalFailedOrders += failedIncreases;
//             order.orderCount -= failedIncreases; // Remove failed orders

//             // **Now decrease the turns remaining**
//             order.remainingTurns--;

//             // **Only process successful population increases on the last turn**
//             if (order.remainingTurns <= 0 && order.orderCount > 0)
//             {
//                 for (int i = 0; i < order.orderCount; i++)
//                 {
//                     int increaseAmount = CalculatePopulationIncrease();
//                     populationManager.AddPopulationGroup(AgeGroup.Child, increaseAmount, TurnSystem.GetCurrentTurn());
//                     aiLevelManager.AddXP(populationIncreaseExp);
//                     populationManager.ReleasePopulation(1);
//                     successfulIncreases += increaseAmount;
//                 }

//                 totalSuccessfulIncreases += successfulIncreases;
//                 completedOrders.Add(order); // Mark order for removal
//             }

//             // If all orders failed, mark for removal as well
//             if (order.orderCount <= 0)
//             {
//                 completedOrders.Add(order);
//             }
//         }

//         // **Remove all completed orders**
//         foreach (var order in completedOrders)
//         {
//             activePopulationOrders.Remove(order);
//         }

//         // **Log active orders after processing**
//         ShowActiveOrders();

//         // **Log the results**
//         //DebugLog($"[AIPopulationIncreasePlan] Success: {totalSuccessfulIncreases}, Failures: {totalFailedOrders}, Lost: {totalPopulationLost}");
//     }

//     private int CalculatePopulationIncrease()
//     {
//         float randomValue = Random.value;
//         if (randomValue <= 0.1f) return 3; // 10% chance for 3
//         if (randomValue <= 0.3f) return 2; // 20% chance for 2
//         return 1; // 70% chance for 1
//     }

//     // **🔹 Shows all active population increase orders**
//     public void ShowActiveOrders()
//     {
//         if (activePopulationOrders.Count == 0)
//         {
//             //DebugLog("[AIPopulationIncreasePlan] No active population increase orders.");
//             return;
//         }

//         //DebugLog("[AIPopulationIncreasePlan] Active Population Increase Orders:");
//         // foreach (var order in activePopulationOrders)
//         // {
//         //     //DebugLog($"  ➤ Order: {order.orderCount} stacks, {order.remainingTurns} turns remaining.");
//         // }
//     }

//     // **🔹 Returns active population increase orders for UI/debugging**
//     public List<PopulationIncreaseOrder> GetActivePopulationOrders()
//     {
//         return new List<PopulationIncreaseOrder>(activePopulationOrders);
//     }

//     private void DebugLog(string message)
//     {
//         Debug.Log(message);
//     }

//     private void DebugError(string message)
//     {
//         Debug.LogError(message);
//     }

//     [System.Serializable]
//     public class PopulationIncreaseOrder
//     {
//         [Tooltip("Turns remaining for this population increase to complete.")]
//         public int remainingTurns;

//         [Tooltip("Number of stacked population increase orders.")]
//         public int orderCount;

//         public PopulationIncreaseOrder(int turns)
//         {
//             remainingTurns = turns;
//             orderCount = 1;
//         }

//         public void StackOrder()
//         {
//             orderCount++;
//         }
//     }

//     public AIPopulationIncreasePlanSaveData SaveState()
//     {
//         AIPopulationIncreasePlanSaveData data = new AIPopulationIncreasePlanSaveData();
        
//         data.foodForPopulationIncrease = foodForPopulationIncrease;
//         data.turnsForIncrease = turnsForIncrease;
//         data.populationIncreaseFailureChance = populationIncreaseFailureChance;
//         data.populationIncreaseExp = populationIncreaseExp;
        
//         // Make a new list copy of active orders.
//         data.activePopulationOrders = new List<PopulationIncreaseOrder>(activePopulationOrders);
        
//         return data;
//     }

//     public void LoadState(AIPopulationIncreasePlanSaveData data)
//     {
//         if (data == null) return;
        
//         foodForPopulationIncrease = data.foodForPopulationIncrease;
//         turnsForIncrease = data.turnsForIncrease;
//         populationIncreaseFailureChance = data.populationIncreaseFailureChance;
//         populationIncreaseExp = data.populationIncreaseExp;
        
//         // Restore the active orders.
//         activePopulationOrders = new List<PopulationIncreaseOrder>(data.activePopulationOrders);
//     }
// }