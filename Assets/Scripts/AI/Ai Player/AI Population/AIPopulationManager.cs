using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Linq;

public class AIPopulationManager : MonoBehaviour
{
    // [Header("Population Settings")]
    // private int startingPopulation;
    // public int maxPopulation;
    // private int currentPopulation;
    // private int usedPopulation = 0;

    // [Header("UI Elements")]
    // private TMP_Text populationDisplayText;
    // private TMP_Text availablePopulationText;

    // [Header("Health Settings")]
    // public int baseChildHealth = 50;
    // public int baseTeenHealth = 100;
    // public int baseAdultHealth = 100;
    // public int baseElderHealth = 75;

    // [Header("Lifespan & Age Transition Settings")]
    // public int childToTeenTurn = 10;
    // public int teenToAdultTurn = 20;
    // public int adultToElderTurn = 30;
    // public int lifespan = 40;

    // [Header("Recovery Rate Settings")]
    // public float childRecoveryRate = 1f;
    // public float teenRecoveryRate = 2f;
    // public float adultRecoveryRate = 3f;
    // public float elderRecoveryRate = 1.5f;

    // [Header("Consumption Settings")]
    // public int foodConsumptionPerPerson = 1;
    // public int waterConsumptionPerPerson = 1;

    // [Header("Hunger and Thirst Settings")]
    // public float hungerIncreasePerCycle = 10f;
    // public float thirstIncreasePerCycle = 10f;
    // public float maxHungerLevel = 100f;
    // public float maxThirstLevel = 100f;

    // [Header("Damage Settings")]
    // public float baseDamagePerCycle = 5f;
    // public float childDamageMultiplier = 3f;
    // public float teenDamageMultiplier = 2f;
    // public float adultDamageMultiplier = 1f;
    // public float elderDamageMultiplier = 1.5f;

    // private float currentHungerLevel = 0f;
    // private float currentThirstLevel = 0f;

    // public float GetCurrentHungerLevel()
    // {
    //     return currentHungerLevel;
    // }

    // public float GetCurrentThirstLevel()
    // {
    //     return currentThirstLevel;
    // }

    // // Dictionary that holds population groups categorized by age
    // private Dictionary<AgeGroup, List<PopulationGroup>> populationGroups = new Dictionary<AgeGroup, List<PopulationGroup>>();

    // private int turnCounter = 0;

    // // **Serialized** List to display in the Unity Inspector
    // [SerializeField]
    // private List<PopulationGroup> serializedPopulationGroups = new List<PopulationGroup>();

    // private AIInventoryManager inventoryManager;

    // private void Start()
    // {
    //     Transform aiPlayer = transform.parent;

    //     if (aiPlayer == null)
    //     {
    //         //Debug.LogWarning("[AIDecisionMaker] AI Player (parent) is missing.");
    //         return;
    //     }

    //     inventoryManager = aiPlayer.GetComponentInChildren<AIInventoryManager>();

    //     UpdateUI();
    //     TurnSystem.SubscribeToEndOfTurn(OnTurnEnd);
    //     TurnSystem.SubscribeToStartOfTurn(OnTurnStart);
    // }

    // private void OnDestroy()
    // {
    //     TurnSystem.UnsubscribeFromEndOfTurn(OnTurnEnd);
    //     TurnSystem.UnsubscribeFromStartOfTurn(OnTurnStart);
    // }

    // public void InitializeUI(GameObject starterTile)
    // {
    //     if (starterTile == null)
    //     {
    //         //Debug.LogWarning("Starter tile is null; cannot initialize UI.");
    //         return;
    //     }

    //     Canvas worldCanvas = starterTile.GetComponentInChildren<Canvas>();
    //     if (worldCanvas == null)
    //     {
    //         //Debug.LogWarning("No Canvas found on the starter tile.");
    //         return;
    //     }

    //     TMP_Text[] texts = worldCanvas.GetComponentsInChildren<TMP_Text>(true);
    //     foreach (TMP_Text text in texts)
    //     {
    //         if (text.gameObject.name == "PopulationDisplayText")
    //             populationDisplayText = text;
    //         else if (text.gameObject.name == "AvailablePopulationText")
    //             availablePopulationText = text;
    //     }

    //     UpdateUI();
    // }

    // public void AddPopulationGroups(IEnumerable<PopulationGroup> groups)
    // {
    //     foreach (var g in groups)
    //     {
    //         // reuse existing routine so all bookkeeping/UI stays intact
    //         AddPopulationGroup(g.ageGroup, g.count, g.additionTurn);

    //         // restore health proportionally (optional – keeps wounds)
    //         var added = populationGroups[g.ageGroup]
    //                     .First(pg => pg.additionTurn == g.additionTurn);

    //         added.currentHealth = Mathf.Min(
    //             added.currentHealth + g.currentHealth,
    //             added.maxHealth + g.maxHealth);  // cap at max
    //     }

    //     UpdateSerializedPopulationGroups();   // maintain inspector list
    //     UpdateUI();                            // refresh HUD if visible
    // }

    // public void SetPopulationValues(int starting, int max)
    // {
    //     startingPopulation = starting;
    //     maxPopulation = max;
    //     currentPopulation = startingPopulation;

    //     if (startingPopulation < 1)
    //     {
    //         //Debug.LogWarning("[AIPopulationManager] Starting population cannot be less than 1.");
    //         startingPopulation = 1; // Ensure at least one unit exists
    //     }

    //     int minAdults = Mathf.Max(1, Mathf.CeilToInt(startingPopulation / 2f)); // Ensure at least 1 adult if possible
    //     int adultCount = Mathf.Max(0, Random.Range(minAdults, startingPopulation)); // Ensure valid range
    //     int childCount = Mathf.Max(1, startingPopulation - adultCount); // Ensure at least 1 child exists

    //     int currentTurn = TurnSystem.GetCurrentTurn();

    //     // **Only add groups if count is greater than 0**
    //     if (childCount > 0)
    //     {
    //         AddPopulationGroup(AgeGroup.Child, childCount, currentTurn);
    //     }
    //     if (adultCount > 0)
    //     {
    //         AddPopulationGroup(AgeGroup.Adult, adultCount, currentTurn - (lifespan / 2));
    //     }

    //     UpdateSerializedPopulationGroups();
    //     UpdateUI();

    //     //Debug.Log($"[AIPopulationManager] Population initialized - Children: {childCount}, Adults: {adultCount}");
    // }

    // private void OnTurnStart()
    // {
    //     ApplyRecovery();
    // }

    // private void OnTurnEnd()
    // {
    //     turnCounter++;

    //     RemoveAgedOutPopulation();
    //     AgePopulation();

    //     if (turnCounter % 4 == 0)
    //     {
    //         ConsumeResources();
    //         RemoveExcessPopulation();

    //         if (currentHungerLevel > 0 || currentThirstLevel > 0)
    //         {
    //             ApplyDamageFromHungerAndThirst();
    //         }
    //     }

    //     UpdateUI();
    // }

    // public void UsePopulation(int amount)
    // {
    //     if (GetAvailablePopulation() >= amount)
    //     {
    //         usedPopulation += amount;
    //         UpdateUI();
    //     }
    // }

    // public int GetTotalAvailablePopulation()
    // {
    //     // Sum up every group's count
    //     int totalPop = 0;
    //     foreach (var kvp in populationGroups)
    //         totalPop += kvp.Value.Sum(g => g.count);

    //     // Subtract those already assigned/used
    //     int avail = totalPop - usedPopulation;
    //     return Mathf.Max(0, avail);
    // }

    // public void ReleasePopulation(int amount)
    // {
    //     usedPopulation = Mathf.Max(usedPopulation - amount, 0);
    //     UpdateUI();
    // }

    // public void RemovePopulation(int amount)
    // {
    //     int removed = 0;
    //     List<PopulationGroup> toRemove = new List<PopulationGroup>();

    //     //Debug.Log($"[PopulationManager] Attempting to remove {amount} population.");

    //     foreach (var kvp in populationGroups)
    //     {
    //         string groupKey = kvp.Key.ToString(); // Assuming key represents an age group or similar.
    //         List<PopulationGroup> groups = kvp.Value;

    //         for (int i = groups.Count - 1; i >= 0; i--) // Iterate backwards to avoid modifying list while looping
    //         {
    //             PopulationGroup group = groups[i];
    //             int needed = amount - removed;
    //             //Debug.Log($"[PopulationManager] Checking group [{groupKey}] with current count {group.count}. Needed: {needed}.");

    //             if (group.count <= needed)
    //             {
    //                 removed += group.count;
    //                 //Debug.Log($"[PopulationManager] Removing entire group [{groupKey}] (count {group.count}). Total removed now: {removed}.");
    //                 toRemove.Add(group);
    //             }
    //             else
    //             {
    //                 //Debug.Log($"[PopulationManager] Reducing group [{groupKey}] by {needed}. Original count: {group.count}.");
    //                 group.count -= needed;
    //                 removed = amount;
    //                 //Debug.Log($"[PopulationManager] New count for group [{groupKey}]: {group.count}. Total removed now: {removed}.");
    //                 break;
    //             }

    //             if (removed >= amount)
    //             {
    //                 //Debug.Log($"[PopulationManager] Required population removal reached ({removed}/{amount}).");
    //                 break;
    //             }
    //         }

    //         if (removed >= amount)
    //             break;
    //     }

    //     // Remove fully depleted groups from all lists.
    //     foreach (PopulationGroup group in toRemove)
    //     {
    //         foreach (var list in populationGroups.Values)
    //         {
    //             if (list.Remove(group))
    //             {
    //                 //Debug.Log($"[PopulationManager] Group removed from list: {group}.");
    //             }
    //         }
    //     }

    //     //Debug.Log($"[PopulationManager] Final population removed: {removed} out of requested {amount}.");
    //     UpdateSerializedPopulationGroups();
    //     UpdateUI();
    // }

    // public int GetCurrentPopulation()
    // {
    //     int total = 0;
    //     foreach (var groupList in populationGroups.Values)
    //     {
    //         foreach (PopulationGroup group in groupList)
    //             total += group.count;
    //     }
    //     return total;
    // }

    // public int GetUsedPopulation()
    // {
    //     return usedPopulation;
    // }

    // public int GetAvailablePopulation()
    // {
    //     int childPopulation = GetPopulationCountByAgeGroup(AgeGroup.Child);
    //     int elderPopulation = GetPopulationCountByAgeGroup(AgeGroup.Elder);

    //     // **Ensure usedPopulation never exceeds total population**
    //     usedPopulation = Mathf.Min(usedPopulation, GetCurrentPopulation());

    //     return Mathf.Max(GetCurrentPopulation() - elderPopulation - childPopulation - usedPopulation, 0);
    // }

    // private void InitializePopulation()
    // {
    //     int currentTurn = TurnSystem.GetCurrentTurn();
    //     AddPopulationGroup(AgeGroup.Child, startingPopulation, currentTurn);
    // }

    // public void AddPopulationGroup(AgeGroup ageGroup, int count, int additionTurn)
    // {
    //     int baseHealth = GetBaseHealthForAgeGroup(ageGroup);

    //     if (!populationGroups.ContainsKey(ageGroup))
    //     {
    //         populationGroups[ageGroup] = new List<PopulationGroup>();
    //     }

    //     // **Check if a group with the same turn already exists**
    //     PopulationGroup existingGroup = populationGroups[ageGroup].FirstOrDefault(g => g.additionTurn == additionTurn);

    //     if (existingGroup != null)
    //     {
    //         // **Merge population into existing group**
    //         existingGroup.count += count;
    //         existingGroup.maxHealth += count * baseHealth;
    //         existingGroup.currentHealth += count * baseHealth;
    //     }
    //     else
    //     {
    //         // **Create new group if no match found**
    //         PopulationGroup newGroup = new PopulationGroup()
    //         {
    //             ageGroup = ageGroup,
    //             count = count,
    //             additionTurn = additionTurn,
    //             maxHealth = count * baseHealth,
    //             currentHealth = count * baseHealth
    //         };
    //         populationGroups[ageGroup].Add(newGroup);
    //     }

    //     UpdateSerializedPopulationGroups(); // Update Unity Inspector View
    // }

    // private void RemoveExcessPopulation()
    // {
    //     currentPopulation = GetCurrentPopulation();
    //     if (currentPopulation <= maxPopulation) return; // ✅ No need to remove population if within limit

    //     int excessPopulation = currentPopulation - maxPopulation;
    //     //Debug.Log($"[AIPopulationManager] AI exceeds max population by {excessPopulation}. Removing excess...");

    //     // **Prioritize removing children first, then elders**
    //     excessPopulation -= RemovePopulationFromGroup(AgeGroup.Child, excessPopulation);
    //     if (excessPopulation > 0)
    //     {
    //         excessPopulation -= RemovePopulationFromGroup(AgeGroup.Elder, excessPopulation);
    //     }

    //     // **Ensure current population is correctly updated**
    //     currentPopulation = GetCurrentPopulation();
    //     UpdateSerializedPopulationGroups();
    //     UpdateUI();
    // }

    // private int RemovePopulationFromGroup(AgeGroup ageGroup, int amountToRemove)
    // {
    //     if (!populationGroups.ContainsKey(ageGroup) || amountToRemove <= 0)
    //         return 0;

    //     int removed = 0;
    //     List<PopulationGroup> groupsToRemove = new List<PopulationGroup>();

    //     foreach (PopulationGroup group in populationGroups[ageGroup])
    //     {
    //         if (removed >= amountToRemove) break;

    //         if (group.count <= (amountToRemove - removed))
    //         {
    //             removed += group.count;
    //             groupsToRemove.Add(group);
    //         }
    //         else
    //         {
    //             int toRemove = amountToRemove - removed;
    //             group.count -= toRemove;
    //             removed += toRemove;
    //         }
    //     }

    //     // **Remove groups that were completely depleted**
    //     foreach (PopulationGroup group in groupsToRemove)
    //     {
    //         populationGroups[ageGroup].Remove(group);
    //     }

    //     if (populationGroups[ageGroup].Count == 0)
    //         populationGroups.Remove(ageGroup);

    //     return removed;
    // }

    // private int GetBaseHealthForAgeGroup(AgeGroup ageGroup)
    // {
    //     switch (ageGroup)
    //     {
    //         case AgeGroup.Child: return baseChildHealth;
    //         case AgeGroup.Teen: return baseTeenHealth;
    //         case AgeGroup.Adult: return baseAdultHealth;
    //         case AgeGroup.Elder: return baseElderHealth;
    //         default: return 0;
    //     }
    // }

    // private void RemoveAgedOutPopulation()
    // {
    //     int currentTurn = TurnSystem.GetCurrentTurn();
    //     List<(AgeGroup, PopulationGroup)> groupsToRemove = new List<(AgeGroup, PopulationGroup)>();

    //     foreach (var kvp in populationGroups)
    //     {
    //         AgeGroup ageGroup = kvp.Key;
    //         List<PopulationGroup> groups = kvp.Value;

    //         foreach (PopulationGroup group in groups)
    //         {
    //             int age = currentTurn - group.additionTurn;
    //             if (age >= lifespan)
    //             {
    //                 groupsToRemove.Add((ageGroup, group));
    //             }
    //         }
    //     }

    //     foreach (var (ageGroup, group) in groupsToRemove)
    //     {
    //         populationGroups[ageGroup].Remove(group);
    //         if (populationGroups[ageGroup].Count == 0)
    //             populationGroups.Remove(ageGroup);
    //     }

    //     // **Ensure serialized population list updates**
    //     UpdateSerializedPopulationGroups();
    //     UpdateUI();
    // }

    // private void AgePopulation()
    // {
    //     int currentTurn = TurnSystem.GetCurrentTurn();
    //     Dictionary<AgeGroup, List<PopulationGroup>> newPopulationGroups = new Dictionary<AgeGroup, List<PopulationGroup>>();

    //     foreach (var kvp in populationGroups.ToList())
    //     {
    //         AgeGroup ageGroup = kvp.Key;
    //         List<PopulationGroup> groups = kvp.Value;

    //         foreach (PopulationGroup group in groups.ToList())
    //         {
    //             int age = currentTurn - group.additionTurn;
    //             AgeGroup newAgeGroup = ageGroup;

    //             if (age >= adultToElderTurn)
    //                 newAgeGroup = AgeGroup.Elder;
    //             else if (age >= teenToAdultTurn)
    //                 newAgeGroup = AgeGroup.Adult;
    //             else if (age >= childToTeenTurn)
    //                 newAgeGroup = AgeGroup.Teen;

    //             if (newAgeGroup != ageGroup)
    //             {
    //                 // **Move the population group to the new age category**
    //                 if (!newPopulationGroups.ContainsKey(newAgeGroup))
    //                 {
    //                     newPopulationGroups[newAgeGroup] = new List<PopulationGroup>();
    //                 }

    //                 int newBaseHealth = GetBaseHealthForAgeGroup(newAgeGroup);
    //                 group.maxHealth = group.count * newBaseHealth;
    //                 group.currentHealth = group.maxHealth;
    //                 group.ageGroup = newAgeGroup;
    //                 newPopulationGroups[newAgeGroup].Add(group);

    //                 // Remove from the old age group
    //                 groups.Remove(group);
    //             }
    //         }
    //     }

    //     // Merge the new groups into the main dictionary
    //     foreach (var kvp in newPopulationGroups)
    //     {
    //         if (!populationGroups.ContainsKey(kvp.Key))
    //             populationGroups[kvp.Key] = new List<PopulationGroup>();

    //         populationGroups[kvp.Key].AddRange(kvp.Value);
    //     }

    //     // **Ensure available population updates correctly**
    //     currentPopulation = GetCurrentPopulation();
    //     usedPopulation = Mathf.Min(usedPopulation, currentPopulation);
    //     UpdateAvailablePopulation();

    //     UpdateSerializedPopulationGroups();
    //     UpdateUI();
    // }

    // private void UpdateAvailablePopulation()
    // {
    //     int childPopulation = 0;
    //     if (populationGroups.ContainsKey(AgeGroup.Child))
    //     {
    //         foreach (PopulationGroup group in populationGroups[AgeGroup.Child])
    //         {
    //             childPopulation += group.count;
    //         }
    //     }

    //     int elderPopulation = 0;
    //     if (populationGroups.ContainsKey(AgeGroup.Elder))
    //     {
    //         foreach (PopulationGroup group in populationGroups[AgeGroup.Elder])
    //         {
    //             elderPopulation += group.count;
    //         }
    //     }

    //     // Ensure available population never goes below 0
    //     int availablePopulation = Mathf.Max(GetCurrentPopulation() - elderPopulation - childPopulation - usedPopulation, 0);

    //     availablePopulationText.text = $"{availablePopulation}";
    // }

    // private void UpdateSerializedPopulationGroups()
    // {
    //     serializedPopulationGroups.Clear();
    //     foreach (var kvp in populationGroups)
    //     {
    //         foreach (var group in kvp.Value)
    //         {
    //             // Get the base health for one person of this age group.
    //             int baseHealthPerMember = GetBaseHealthForAgeGroup(group.ageGroup);
    //             // New maximum health is now based on the updated group count.
    //             int newMaxHealth = group.count * baseHealthPerMember;
    //             // Compute the health ratio (i.e. remaining health percentage) from the existing values.
    //             float healthRatio = (group.maxHealth > 0) ? (float)group.currentHealth / group.maxHealth : 1f;
    //             // Calculate new current health based on the new max health and the ratio.
    //             int newCurrentHealth = Mathf.RoundToInt(newMaxHealth * healthRatio);

    //             serializedPopulationGroups.Add(new PopulationGroup
    //             {
    //                 ageGroup = group.ageGroup,
    //                 count = group.count,
    //                 additionTurn = group.additionTurn,
    //                 currentHealth = newCurrentHealth,
    //                 maxHealth = newMaxHealth
    //             });
    //         }
    //     }
    // }

    // private void ApplyRecovery()
    // {
    //     foreach (var kvp in populationGroups)
    //     {
    //         AgeGroup ageGroup = kvp.Key;
    //         foreach (PopulationGroup group in kvp.Value)
    //         {
    //             float recoveryRate = ageGroup switch
    //             {
    //                 AgeGroup.Child => childRecoveryRate,
    //                 AgeGroup.Teen => teenRecoveryRate,
    //                 AgeGroup.Adult => adultRecoveryRate,
    //                 AgeGroup.Elder => elderRecoveryRate,
    //                 _ => 0f
    //             };
    //             int recoveryAmount = Mathf.RoundToInt(recoveryRate * group.count);
    //             group.currentHealth = Mathf.Min(group.currentHealth + recoveryAmount, group.maxHealth);
    //         }
    //     }
    // }

    // private void UpdateUI()
    // {
    //     if (populationDisplayText != null)
    //         populationDisplayText.text = $"{GetCurrentPopulation()} / {maxPopulation}";
    //     if (availablePopulationText != null)
    //         availablePopulationText.text = $"{GetAvailablePopulation()}";
    // }

    // private void ConsumeResources()
    // {
    //     // Get total child and non-child population
    //     int childPopulation = 0;
    //     if (populationGroups.ContainsKey(AgeGroup.Child))
    //     {
    //         foreach (var group in populationGroups[AgeGroup.Child])
    //         {
    //             childPopulation += group.count;
    //         }
    //     }

    //     int nonChildPopulation = GetCurrentPopulation() - childPopulation;

    //     int foodNeeded = (nonChildPopulation * foodConsumptionPerPerson) + Mathf.CeilToInt(childPopulation * foodConsumptionPerPerson / 2f);
    //     int waterNeeded = (nonChildPopulation * waterConsumptionPerPerson) + Mathf.CeilToInt(childPopulation * waterConsumptionPerPerson / 2f);

    //     int totalFoodConsumed = 0;
    //     int totalWaterConsumed = 0;

    //     //Debug.Log($"[AIPopulationManager] Turn {TurnSystem.GetCurrentTurn()} - Food Needed: {foodNeeded}, Water Needed: {waterNeeded}");

    //     // Water consumption (prefer WFR, then WCT)
    //     int availableWFR = inventoryManager.GetResourceAmount("WFR");
    //     int availableWCT = inventoryManager.GetResourceAmount("WCT");

    //     if (availableWFR >= waterNeeded)
    //     {
    //         inventoryManager.RemoveResource("WFR", waterNeeded);
    //         totalWaterConsumed = waterNeeded;
    //     }
    //     else
    //     {
    //         totalWaterConsumed += availableWFR;
    //         inventoryManager.RemoveResource("WFR", availableWFR);
    //         if (availableWCT >= waterNeeded - totalWaterConsumed)
    //         {
    //             inventoryManager.RemoveResource("WCT", waterNeeded - totalWaterConsumed);
    //             totalWaterConsumed = waterNeeded;
    //         }
    //         else
    //         {
    //             totalWaterConsumed += availableWCT;
    //             inventoryManager.RemoveResource("WCT", availableWCT);
    //         }
    //     }

    //     // Food consumption (all non-water food considered as GFD)
    //     if (inventoryManager.HasEnoughResource("GFD", foodNeeded))
    //     {
    //         inventoryManager.RemoveGenericFood(foodNeeded);
    //         totalFoodConsumed = foodNeeded;
    //     }
    //     else
    //     {
    //         totalFoodConsumed = inventoryManager.GetTotalNonWaterFoodAmount();
    //         inventoryManager.RemoveGenericFood(totalFoodConsumed);
    //     }

    //     // Calculate the percentage of needs met for food and water
    //     float foodPercentage = (float)totalFoodConsumed / foodNeeded;
    //     float waterPercentage = (float)totalWaterConsumed / waterNeeded;

    //     //Debug.Log($"[AIPopulationManager] Hunger Before: {currentHungerLevel}, Thirst Before: {currentThirstLevel}");

    //     // Adjust hunger and thirst levels based on the percentage of resources consumed
    //     if (foodPercentage < 1f)
    //     {
    //         float foodShortage = 1f - foodPercentage;
    //         currentHungerLevel = Mathf.Min(currentHungerLevel + hungerIncreasePerCycle * foodShortage, maxHungerLevel);
    //     }
    //     else
    //     {
    //         currentHungerLevel = 0; // Reset hunger level if fully fed
    //     }

    //     if (waterPercentage < 1f)
    //     {
    //         float waterShortage = 1f - waterPercentage;
    //         currentThirstLevel = Mathf.Min(currentThirstLevel + thirstIncreasePerCycle * waterShortage, maxThirstLevel);
    //     }
    //     else
    //     {
    //         currentThirstLevel = 0; // Reset thirst level if fully hydrated
    //     }

    //     //Debug.Log($"[AIPopulationManager] Hunger After: {currentHungerLevel}, Thirst After: {currentThirstLevel}");

    //     // Apply damage if hunger or thirst levels are high
    //     if (currentHungerLevel > 0 || currentThirstLevel > 0)
    //     {
    //         ApplyDamageFromHungerAndThirst();
    //     }
    // }

    // private void ApplyRandomContaminatedFoodDisease()
    // {
    //     // This assumes you have a disease system like in PopulationManager
    //     Debug.Log("[AIPopulationManager] AI consumed contaminated food and may get a disease!");
    // }

    // private void ApplyRandomContaminatedWaterDisease()
    // {
    //     Debug.Log("[AIPopulationManager] AI consumed contaminated water and may get a disease!");
    // }

    // private void ApplyDamageFromHungerAndThirst()
    // {
    //     float hungerMultiplier = currentHungerLevel / maxHungerLevel;
    //     float thirstMultiplier = currentThirstLevel / maxThirstLevel;

    //     List<PopulationGroup> groupsToRemove = new List<PopulationGroup>();

    //     foreach (var pair in populationGroups)
    //     {
    //         var ageGroup = pair.Key;
    //         var groupList = pair.Value;

    //         // ✅ Iterate **backwards** to safely remove elements
    //         for (int i = groupList.Count - 1; i >= 0; i--)
    //         {
    //             PopulationGroup group = groupList[i];

    //             float groupDamageMultiplier = 1f;

    //             // ✅ **Apply correct damage multipliers based on age group**
    //             switch (ageGroup)
    //             {
    //                 case AgeGroup.Child:
    //                     groupDamageMultiplier = childDamageMultiplier;
    //                     break;
    //                 case AgeGroup.Teen:
    //                     groupDamageMultiplier = teenDamageMultiplier;
    //                     break;
    //                 case AgeGroup.Adult:
    //                     groupDamageMultiplier = adultDamageMultiplier;
    //                     break;
    //                 case AgeGroup.Elder:
    //                     groupDamageMultiplier = elderDamageMultiplier;
    //                     break;
    //             }

    //             float baseDamage = baseDamagePerCycle * groupDamageMultiplier;
    //             float totalDamage = 0;

    //             // ✅ **Apply damage based on hunger level**
    //             if (hungerMultiplier > 0)
    //             {
    //                 totalDamage += baseDamage * hungerMultiplier * group.count;
    //             }

    //             // ✅ **Apply damage based on thirst level**
    //             if (thirstMultiplier > 0)
    //             {
    //                 totalDamage += baseDamage * thirstMultiplier * group.count;
    //             }

    //             // ✅ **Apply health damage**
    //             ApplyHealthDamage(group, totalDamage);

    //             // ✅ **Check if the group's health has dropped to zero**
    //             if (group.currentHealth <= 0)
    //             {
    //                 groupsToRemove.Add(group);
    //             }
    //         }
    //     }

    //     // ✅ **Remove population groups that reached 0 health**
    //     foreach (var group in groupsToRemove)
    //     {
    //         foreach (var groupList in populationGroups.Values)
    //         {
    //             groupList.Remove(group);
    //         }
    //     }

    //     //Debug.Log($"[AIPopulationManager] Applied Hunger/Thirst Damage. Removed {groupsToRemove.Count} groups.");
    // }

    // /// **🔹 Applies health damage to a group**
    // private void ApplyHealthDamage(PopulationGroup group, float damage)
    // {
    //     // ✅ Multiply base damage by population count
    //     float populationBasedDamage = damage * group.count;

    //     group.currentHealth -= Mathf.RoundToInt(populationBasedDamage);

    //     // ✅ Ensure health doesn't drop below 0
    //     if (group.currentHealth < 0)
    //     {
    //         group.currentHealth = 0;
    //     }

    //     // ✅ Remove population if health reaches 0
    //     if (group.currentHealth <= 0)
    //     {
    //         RemovePopulation(group.count);
    //     }
    // }

    // /// **🔹 Apply disease damage to AI population**
    // public void ApplyDiseaseDamage(int damageToChildren, int damageToTeens, int damageToAdults, int damageToElders)
    // {
    //     List<PopulationGroup> groupsToRemove = new List<PopulationGroup>();

    //     foreach (var pair in populationGroups)
    //     {
    //         var ageGroup = pair.Key;
    //         var groupList = pair.Value;

    //         foreach (var group in groupList)
    //         {
    //             int damage = 0;

    //             switch (ageGroup)
    //             {
    //                 case AgeGroup.Child:
    //                     damage = damageToChildren;
    //                     break;
    //                 case AgeGroup.Teen:
    //                     damage = damageToTeens;
    //                     break;
    //                 case AgeGroup.Adult:
    //                     damage = damageToAdults;
    //                     break;
    //                 case AgeGroup.Elder:
    //                     damage = damageToElders;
    //                     break;
    //             }

    //             if (damage > 0)
    //             {
    //                 // ✅ Apply damage to the group's current health
    //                 group.currentHealth -= damage;

    //                 //Debug.Log($"[AIPopulationManager] {group.count} {ageGroup} affected. Damage: {damage}. New Health: {group.currentHealth}");

    //                 // ✅ If health reaches zero, mark for removal
    //                 if (group.currentHealth <= 0)
    //                 {
    //                     groupsToRemove.Add(group);
    //                 }
    //             }
    //         }
    //     }

    //     // ✅ Remove all groups whose health reached zero
    //     foreach (var group in groupsToRemove)
    //     {
    //         foreach (var groupList in populationGroups.Values)
    //         {
    //             groupList.Remove(group);
    //         }
    //     }

    //     //Debug.Log($"[AIPopulationManager] Removed {groupsToRemove.Count} population groups due to disease.");
    // }

    // public int GetPopulationCountByAgeGroup(AgeGroup ageGroup)
    // {
    //     if (!populationGroups.ContainsKey(ageGroup))
    //         return 0;

    //     int count = 0;
    //     foreach (var group in populationGroups[ageGroup])
    //     {
    //         count += group.count;
    //     }
    //     return count;
    // }

    // public void ApplyFailurePenalty(int penaltyAmount)
    // {
    //     //Debug.Log($"[AIPopulationManager] Starting failure penalty removal. Requested penalty: {penaltyAmount}");
        
    //     // List of age groups eligible for penalty
    //     List<AgeGroup> eligibleGroups = new List<AgeGroup> { AgeGroup.Teen, AgeGroup.Adult };

    //     // Shuffle the list to randomize the selection order
    //     eligibleGroups = eligibleGroups.OrderBy(x => Random.value).ToList();

    //     int remainingPenalty = penaltyAmount;

    //     foreach (AgeGroup ageGroup in eligibleGroups)
    //     {
    //         if (remainingPenalty <= 0)
    //             break;

    //         if (populationGroups.ContainsKey(ageGroup))
    //         {
    //             List<PopulationGroup> groups = populationGroups[ageGroup];

    //             for (int i = groups.Count - 1; i >= 0; i--) // Iterate backwards to avoid modifying list while looping
    //             {
    //                 PopulationGroup group = groups[i];
    //                 //Debug.Log($"[AIPopulationManager] AgeGroup {ageGroup}: Processing group with count {group.count}. Remaining penalty: {remainingPenalty}");

    //                 if (group.count <= remainingPenalty)
    //                 {
    //                     remainingPenalty -= group.count;
    //                     //Debug.Log($"[AIPopulationManager] Removing entire group (count: {group.count}) from AgeGroup {ageGroup}. New remaining penalty: {remainingPenalty}");
    //                     groups.RemoveAt(i);
    //                 }
    //                 else
    //                 {
    //                     int reduction = remainingPenalty;
    //                     //Debug.Log($"[AIPopulationManager] Reducing group in AgeGroup {ageGroup} by {reduction}. Original count: {group.count}");
    //                     group.count -= reduction;
    //                     remainingPenalty = 0;
    //                     //Debug.Log($"[AIPopulationManager] New group count for AgeGroup {ageGroup}: {group.count}. Penalty fulfilled.");
    //                     break;
    //                 }
    //             }
    //         }
    //     }

    //     // Update serialized population groups and UI.
    //     UpdateSerializedPopulationGroups();
    //     UpdateUI();
    // }

    // /// **🔹 Applies health increase to AI population age groups (including base health)**
    // public void ApplyAgeGroupHealthIncrease(WorldUpgrade upgrade)
    // {
    //     if (upgrade.affectedAgeGroups == null || upgrade.affectedAgeGroups.Count == 0)
    //     {
    //         //Debug.LogWarning("[AIPopulationManager] No age groups specified for health increase. Skipping.");
    //         return;
    //     }

    //     foreach (AgeGroup ageGroup in upgrade.affectedAgeGroups)
    //     {
    //         int healthIncrease = Mathf.RoundToInt(upgrade.value);

    //         // ✅ Update base health for new population groups
    //         switch (ageGroup)
    //         {
    //             case AgeGroup.Child:
    //                 baseChildHealth += healthIncrease;
    //                 //Debug.Log($"[AIPopulationManager] Increased base Child health to {baseChildHealth}");
    //                 break;
    //             case AgeGroup.Teen:
    //                 baseTeenHealth += healthIncrease;
    //                 //Debug.Log($"[AIPopulationManager] Increased base Teen health to {baseTeenHealth}");
    //                 break;
    //             case AgeGroup.Adult:
    //                 baseAdultHealth += healthIncrease;
    //                 //Debug.Log($"[AIPopulationManager] Increased base Adult health to {baseAdultHealth}");
    //                 break;
    //             case AgeGroup.Elder:
    //                 baseElderHealth += healthIncrease;
    //                 //Debug.Log($"[AIPopulationManager] Increased base Elder health to {baseElderHealth}");
    //                 break;
    //         }

    //         // ✅ Update health for existing population groups
    //         if (!populationGroups.ContainsKey(ageGroup))
    //         {
    //             //Debug.LogWarning($"[AIPopulationManager] Age group '{ageGroup}' not found in AI population dictionary. Skipping.");
    //             continue;
    //         }

    //         foreach (PopulationGroup group in populationGroups[ageGroup])
    //         {
    //             group.maxHealth += healthIncrease * group.count;
    //             group.currentHealth += healthIncrease * group.count; // Heal population when health increases
    //         }

    //         //Debug.Log($"[AIPopulationManager] Increased max health for {ageGroup} by {healthIncrease} per person.");
    //     }
    // }

    // /// **🔹 Applies regeneration rate increase to AI population age groups (including base regeneration rate)**
    // public void ApplyRegenerationRateIncrease(WorldUpgrade upgrade)
    // {
    //     if (upgrade.affectedAgeGroups == null || upgrade.affectedAgeGroups.Count == 0)
    //     {
    //         //Debug.LogWarning("[AIPopulationManager] No age groups specified for regeneration rate increase. Skipping.");
    //         return;
    //     }

    //     foreach (AgeGroup ageGroup in upgrade.affectedAgeGroups)
    //     {
    //         float regenIncrease = upgrade.value;

    //         // ✅ Update base regeneration rates for future population groups
    //         switch (ageGroup)
    //         {
    //             case AgeGroup.Child:
    //                 childRecoveryRate += regenIncrease;
    //                 //Debug.Log($"[AIPopulationManager] Increased base Child recovery rate to {childRecoveryRate}");
    //                 break;
    //             case AgeGroup.Teen:
    //                 teenRecoveryRate += regenIncrease;
    //                 //Debug.Log($"[AIPopulationManager] Increased base Teen recovery rate to {teenRecoveryRate}");
    //                 break;
    //             case AgeGroup.Adult:
    //                 adultRecoveryRate += regenIncrease;
    //                 //Debug.Log($"[AIPopulationManager] Increased base Adult recovery rate to {adultRecoveryRate}");
    //                 break;
    //             case AgeGroup.Elder:
    //                 elderRecoveryRate += regenIncrease;
    //                 //Debug.Log($"[AIPopulationManager] Increased base Elder recovery rate to {elderRecoveryRate}");
    //                 break;
    //         }

    //         // ✅ No need to modify population groups directly, since `ApplyRecovery()` already uses base recovery rates
    //         //Debug.Log($"[AIPopulationManager] Increased regeneration rate for {ageGroup} by {regenIncrease}. Future population will heal faster.");
    //     }
    // }

    
    // public int GetHumanExpiryTurns()
    // {
    //     // Gather all PopulationGroup objects into a single list.
    //     List<PopulationGroup> allPopulations = new List<PopulationGroup>();
    //     foreach (var groupList in populationGroups.Values)
    //     {
    //         allPopulations.AddRange(groupList);
    //     }

    //     // Log the total number of populations.
    //     Debug.Log($"[GetHumanExpiryTurns] Total populations: {allPopulations.Count}");

    //     List<PopulationGroup> validGroups = new List<PopulationGroup>();
    //     foreach (PopulationGroup pg in allPopulations)
    //     {
    //         Debug.Log($"[GetHumanExpiryTurns] PopulationGroup: {pg.ageGroup}, additionTurn: {pg.additionTurn}");
    //         // Consider groups that are Teen or Adult as valid for expiry calculations.
    //         if (pg.ageGroup == AgeGroup.Teen || pg.ageGroup == AgeGroup.Adult)
    //         {
    //             validGroups.Add(pg);
    //         }
    //     }

    //     if (validGroups.Count == 0)
    //     {
    //         Debug.LogWarning("No valid human population groups found. Defaulting expiry turns to 0.");
    //         return 0;
    //     }

    //     // Randomly choose one of the valid groups.
    //     PopulationGroup chosen = validGroups[Random.Range(0, validGroups.Count)];
    //     Debug.Log($"[GetHumanExpiryTurns] Chosen group: {chosen.ageGroup} added at turn {chosen.additionTurn}");

    //     // Get the current turn from your TurnSystem.
    //     int currentTurn = TurnSystem.CurrentTurn;
    //     int age = currentTurn - chosen.additionTurn;
    //     Debug.Log($"[GetHumanExpiryTurns] CurrentTurn: {currentTurn}, Age: {age}");

    //     // For a Teen group, calculate the remaining turns based on teen to adult and adult to elder transitions.
    //     if (chosen.ageGroup == AgeGroup.Teen)
    //     {
    //         // Replace "teenToAdultAge" with the existing variable "teenToAdultTurn".
    //         int totalTurnsForTeen = teenToAdultTurn + adultToElderTurn;
    //         int remaining = Mathf.Max(totalTurnsForTeen - age, 0);
    //         Debug.Log($"[GetHumanExpiryTurns] Teen remaining turns (before division): {remaining}");
    //         return remaining;
    //     }
    //     else // Adult
    //     {
    //         int remaining = Mathf.Max(adultToElderTurn - age, 0);
    //         Debug.Log($"[GetHumanExpiryTurns] Adult remaining turns (before division): {remaining}");
    //         return remaining;
    //     }
    // }

    // public List<PopulationGroup> ExtractRandomPrisoners(int amount, bool includeAllAgeGroups = false)
    // {
    //     var taken = new List<PopulationGroup>();
    //     if (amount <= 0) return taken;

    //     var pools = populationGroups
    //         .Where(kvp =>
    //             includeAllAgeGroups
    //             || kvp.Key == AgeGroup.Teen
    //             || kvp.Key == AgeGroup.Adult)
    //         .SelectMany(kvp => kvp.Value)
    //         .OrderBy(_ => Random.value)
    //         .ToList();

    //     foreach (var grp in pools)
    //     {
    //         if (amount <= 0) break;
    //         int grab = Mathf.Min(grp.count, amount);
    //         if (grab <= 0) continue;

    //         taken.Add(new PopulationGroup {
    //             ageGroup      = grp.ageGroup,
    //             count         = grab,
    //             additionTurn  = grp.additionTurn,
    //             currentHealth = Mathf.RoundToInt(((float)grab / grp.count) * grp.currentHealth),
    //             maxHealth     = Mathf.RoundToInt(((float)grab / grp.count) * grp.maxHealth),
    //         });

    //         grp.currentHealth -= taken.Last().currentHealth;
    //         grp.maxHealth     -= taken.Last().maxHealth;
    //         grp.count         -= grab;
    //         amount           -= grab;
    //     }

    //     // clean up any zeroed‐out groups
    //     foreach (var key in populationGroups.Keys.ToList())
    //         populationGroups[key].RemoveAll(g => g.count <= 0);

    //     return taken;
    // }

    // public AIPopulationManagerSaveData SaveState()
    // {
    //     var data = new AIPopulationManagerSaveData();

    //     data.maxPopulation = maxPopulation;
    //     data.currentPopulation = GetCurrentPopulation(); // or use currentPopulation if it’s maintained
    //     data.usedPopulation = usedPopulation;

    //     data.baseChildHealth = baseChildHealth;
    //     data.baseTeenHealth = baseTeenHealth;
    //     data.baseAdultHealth = baseAdultHealth;
    //     data.baseElderHealth = baseElderHealth;

    //     data.childToTeenTurn = childToTeenTurn;
    //     data.teenToAdultTurn = teenToAdultTurn;
    //     data.adultToElderTurn = adultToElderTurn;
    //     data.lifespan = lifespan;

    //     data.childRecoveryRate = childRecoveryRate;
    //     data.teenRecoveryRate = teenRecoveryRate;
    //     data.adultRecoveryRate = adultRecoveryRate;
    //     data.elderRecoveryRate = elderRecoveryRate;

    //     data.foodConsumptionPerPerson = foodConsumptionPerPerson;
    //     data.waterConsumptionPerPerson = waterConsumptionPerPerson;

    //     data.currentHungerLevel = currentHungerLevel;
    //     data.currentThirstLevel = currentThirstLevel;

    //     // Build a list of population groups for saving
    //     data.populationGroups = new List<PopulationGroupSaveData>();

    //     // Loop over your dictionary: Dictionary<AgeGroup, List<PopulationGroup>> populationGroups
    //     foreach (var kvp in populationGroups)
    //     {
    //         AgeGroup ageGroup = kvp.Key;
    //         List<PopulationGroup> groupList = kvp.Value;

    //         foreach (PopulationGroup group in groupList)
    //         {
    //             PopulationGroupSaveData groupData = new PopulationGroupSaveData
    //             {
    //                 ageGroup = ageGroup,
    //                 count = group.count,
    //                 additionTurn = group.additionTurn,
    //                 currentHealth = group.currentHealth,
    //                 maxHealth = group.maxHealth
    //             };
    //             data.populationGroups.Add(groupData);
    //         }
    //     }

    //     // NEW: Save the UI text values if available
    //     if (populationDisplayText != null)
    //         data.populationDisplayTextValue = populationDisplayText.text;
    //     if (availablePopulationText != null)
    //         data.availablePopulationTextValue = availablePopulationText.text;

    //     return data;
    // }

    // public void LoadState(AIPopulationManagerSaveData data)
    // {
    //     if (data == null) return;

    //     maxPopulation = data.maxPopulation;
    //     // currentPopulation might be updated after we rebuild population
    //     usedPopulation = data.usedPopulation;

    //     baseChildHealth = data.baseChildHealth;
    //     baseTeenHealth = data.baseTeenHealth;
    //     baseAdultHealth = data.baseAdultHealth;
    //     baseElderHealth = data.baseElderHealth;

    //     childToTeenTurn = data.childToTeenTurn;
    //     teenToAdultTurn = data.teenToAdultTurn;
    //     adultToElderTurn = data.adultToElderTurn;
    //     lifespan = data.lifespan;

    //     childRecoveryRate = data.childRecoveryRate;
    //     teenRecoveryRate = data.teenRecoveryRate;
    //     adultRecoveryRate = data.adultRecoveryRate;
    //     elderRecoveryRate = data.elderRecoveryRate;

    //     foodConsumptionPerPerson = data.foodConsumptionPerPerson;
    //     waterConsumptionPerPerson = data.waterConsumptionPerPerson;

    //     currentHungerLevel = data.currentHungerLevel;
    //     currentThirstLevel = data.currentThirstLevel;

    //     // Clear existing dictionary
    //     populationGroups.Clear();

    //     // Reconstruct population dictionary from data.populationGroups
    //     foreach (var groupData in data.populationGroups)
    //     {
    //         AgeGroup ageGroup = groupData.ageGroup;
    //         if (!populationGroups.ContainsKey(ageGroup))
    //         {
    //             populationGroups[ageGroup] = new List<PopulationGroup>();
    //         }

    //         PopulationGroup newGroup = new PopulationGroup
    //         {
    //             ageGroup = groupData.ageGroup,
    //             count = groupData.count,
    //             additionTurn = groupData.additionTurn,
    //             currentHealth = groupData.currentHealth,
    //             maxHealth = groupData.maxHealth
    //         };
    //         populationGroups[ageGroup].Add(newGroup);
    //     }

    //     // Update UI text from saved values, if the TMP_Text references exist
    //     if (populationDisplayText != null && !string.IsNullOrEmpty(data.populationDisplayTextValue))
    //     {
    //         populationDisplayText.text = data.populationDisplayTextValue;
    //     }
    //     if (availablePopulationText != null && !string.IsNullOrEmpty(data.availablePopulationTextValue))
    //     {
    //         availablePopulationText.text = data.availablePopulationTextValue;
    //     }

    //     // Now update currentPopulation by counting all groups
    //     currentPopulation = GetCurrentPopulation();

    //     // Rebuild the serializedPopulationGroups if needed
    //     UpdateSerializedPopulationGroups();

    //     // If you need to refresh the UI, call your UpdateUI() method
    //     GameObject starterTile = GetComponentInParent<AIPlayer>()?.GetStarterTile();
    //     InitializeUI(starterTile);
    // }
}