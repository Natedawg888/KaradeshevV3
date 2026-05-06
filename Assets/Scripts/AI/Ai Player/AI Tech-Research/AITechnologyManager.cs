// using System.Collections;
// using System.Collections.Generic;
// using System.Linq;
// using UnityEngine;

// public class AITechnologyManager : MonoBehaviour
// {
//     public AIPlayer aiPlayer;
//     public AIBuildingManager aiBuildingManager;
//     private AITileDiscoveryManager aiTileDiscoveryManager;
//     private AIResourceManager aiResourceManager;
//     private AIInventoryManager inventoryManager;
//     private AIPopulationManager populationManager;
//     private AILevelManager aiLevelManager;

//     [Header("AI Research Settings")]
//     [SerializeField] private int maxConcurrentResearch = 2;
//     public List<ResearchPlan> activeResearchPlans = new List<ResearchPlan>();

//     [Header("Available Technologies")]
//     [SerializeField] private List<Technology> availableTech = new List<Technology>();

//     [Header("Researched Technologies")]
//     [SerializeField] private List<Technology> researchedTechnologies = new List<Technology>();

//     private void Start()
//     {
//         aiTileDiscoveryManager = aiPlayer.GetComponentInChildren<AITileDiscoveryManager>();
//         aiResourceManager = aiPlayer.GetComponentInChildren<AIResourceManager>();
//         inventoryManager = aiPlayer.GetComponentInChildren<AIInventoryManager>();
//         populationManager = aiPlayer.GetComponentInChildren<AIPopulationManager>();
//         aiLevelManager = aiPlayer.GetComponentInChildren<AILevelManager>();

//         if (aiPlayer == null || aiBuildingManager == null || aiTileDiscoveryManager == null ||
//             aiResourceManager == null || inventoryManager == null || populationManager == null || aiLevelManager == null)
//         {
//             //Debug.LogError("[AITechnologyManager] Missing required AI components!");
//             enabled = false;
//             return;
//         }

//         TurnSystem.SubscribeToEndOfTurn(OnTurnEnd);
//         StartCoroutine(DelayedRT());
//         UpdateAvailableTech();
//     }

//     private IEnumerator DelayedRT()
//     {
//         yield return new WaitForSeconds(0.15f);
//         ResearchTechnologiesBelowCurrentLevel();
//     }

//     /// **🔹 AI automatically researches all technologies below its current level**
//     private void ResearchTechnologiesBelowCurrentLevel()
//     {
//         int aiLevel = aiPlayer.aiLevel;

//         // ✅ Get all technologies that are below the AI's level
//         List<Technology> technologiesToResearch = ResearchManager.Instance.GetAllTechnologies()
//             .Where(tech => tech.levelRequired < aiLevel)
//             .ToList();

//         if (technologiesToResearch.Count == 0)
//         {
//             //Debug.Log("[AITechnologyManager] No lower-level technologies to research.");
//             return;
//         }

//         int researchedCount = 0;

//         foreach (Technology tech in technologiesToResearch)
//         {
//             if (!researchedTechnologies.Contains(tech))
//             {
//                 researchedTechnologies.Add(tech);
//                 ApplyWorldUpgrades(tech);
//                 ApplyEnvironmentUpgrades(tech);
//                 ApplyBuildingUpgrades(tech);
//                 researchedCount++;
//             }
//         }

//         //Debug.Log($"[AITechnologyManager] AI automatically researched {researchedCount} technologies below level {aiLevel}.");
//     }

//     /// **🔹 Updates the available technology cache for the Inspector**
//     public void UpdateAvailableTech()
//     {
//         availableTech = GetAvailableTechnologies();
//     }

//     public List<Technology> GetAvailableTechnologies()
//     {
//         if (aiPlayer == null)
//         {
//             //Debug.LogError("[AITechnologyManager] aiPlayer is NULL!");
//             return new List<Technology>();
//         }

//         if (ResearchManager.Instance == null)
//         {
//             //Debug.LogError("[AITechnologyManager] ResearchManager.Instance is NULL! Ensure ResearchManager is initialized.");
//             return new List<Technology>();
//         }

//         int aiLevel = aiPlayer.aiLevel;
//         List<Technology> allTechnologies = ResearchManager.Instance.GetAvailableTechnologiesForLevel(aiLevel);

//         if (allTechnologies == null)
//         {
//             //Debug.LogError("[AITechnologyManager] allTechnologies is NULL! Ensure technologies are loaded.");
//             return new List<Technology>();
//         }

//         List<Technology> availableTechs = new List<Technology>();

//         foreach (Technology tech in allTechnologies)
//         {
//             if (!researchedTechnologies.Contains(tech) && HasRequiredBuildingForTechnology(tech))
//             {
//                 availableTechs.Add(tech);
//             }
//         }

//         //Debug.Log($"[AITechnologyManager] Found {availableTechs.Count} available technologies.");
//         return availableTechs;
//     }


//     /// **🔹 Checks if the AI has a required building to research a technology**
//     private bool HasRequiredBuildingForTechnology(Technology tech)
//     {
//         if (aiBuildingManager == null)
//         {
//             //Debug.LogError("[AITechnologyManager] ERROR: aiBuildingManager is NULL! AI cannot check for required buildings.");
//             return false;
//         }

//         List<GameObject> ownedBuildings = aiBuildingManager.GetOwnedBuildings();

//         if (ownedBuildings == null || ownedBuildings.Count == 0)
//         {
//             //Debug.Log("[AITechnologyManager] AI has no buildings, assuming no researchable technologies.");
//             return false;
//         }

//         foreach (GameObject building in ownedBuildings)
//         {
//             if (building == null)
//                 continue;

//             // Check for BuildingControl or AIBuildingControl
//             BuildingControl buildingControl = building.GetComponent<BuildingControl>();
//             AIBuildingControl aiBuildingControl = building.GetComponent<AIBuildingControl>();

//             if (buildingControl == null && aiBuildingControl == null)
//             {
//                 //Debug.Log($"[AITechnologyManager] {building.name} has no BuildingControl or AIBuildingControl. Skipping.");
//                 continue; // ✅ Skip this building if it lacks required components
//             }

//             // If either component exists, check if it matches the required building ID
//             string buildingID = buildingControl?.buildingID ?? aiBuildingControl?.buildingID;

//             if (!string.IsNullOrEmpty(buildingID) && tech.researchableOnBuildings.Contains(buildingID))
//             {
//                 return true;
//             }
//         }

//         //Debug.Log("[AITechnologyManager] No valid research buildings found for this technology.");
//         return false;
//     }

//     /// **🔹 Returns researched technologies**
//     public List<Technology> GetResearchedTechnologies()
//     {
//         return new List<Technology>(researchedTechnologies);
//     }

//     /// **🔹 Returns a list of technology IDs currently being researched**
//     public List<string> GetActiveResearchTechnologies()
//     {
//         return activeResearchPlans.Select(plan => plan.technology.technologyID).ToList();
//     }

//     public int GetActiveResearchCount()
//     {
//         return activeResearchPlans.Count;
//     }

//     public int GetMaxConcurrentResearch()
//     {
//         return maxConcurrentResearch;
//     }

//     public bool StartResearch(Technology tech)
//     {
//         // Prevent researching technologies that require a level higher than the AI player's current level.
//         if (tech.levelRequired > aiPlayer.aiLevel)
//         {
//             Debug.Log($"[AITechnologyManager] Cannot research {tech.technologyName} because its required level ({tech.levelRequired}) is above the AI player's level ({aiPlayer.aiLevel}).");
//             return false;
//         }

//         if (researchedTechnologies.Contains(tech))
//         {
//             return false;
//         }

//         if (activeResearchPlans.Any(plan => plan.technology == tech))
//         {
//             return false;
//         }

//         if (activeResearchPlans.Count >= maxConcurrentResearch)
//         {
//             return false;
//         }

//         if (!HasRequiredResources(tech) || populationManager.GetAvailablePopulation() < tech.populationRequired)
//         {
//             return false;
//         }

//         // Deduct population and resources
//         populationManager.UsePopulation(tech.populationRequired);
//         foreach (var requirement in tech.resourceRequirements)
//         {
//             inventoryManager.RemoveResource(requirement.resourceID, requirement.amount);
//         }

//         availableTech.Remove(tech);
//         activeResearchPlans.Add(new ResearchPlan(tech, tech.turnsToComplete));
//         return true;
//     }

//     /// **🔹 Reduces research time at end of each turn**
//     private void OnTurnEnd()
//     {
//         List<ResearchPlan> completedResearch = new List<ResearchPlan>();

//         foreach (ResearchPlan plan in activeResearchPlans)
//         {
//             plan.remainingTurns--;

//             if (plan.remainingTurns <= 0)
//             {
//                 CompleteResearch(plan);
//                 completedResearch.Add(plan);
//             }
//         }

//         activeResearchPlans.RemoveAll(plan => completedResearch.Contains(plan));
//     }

//     /// **🔹 Completes research, releasing population & rewarding XP**
//     private void CompleteResearch(ResearchPlan plan)
//     {
//         researchedTechnologies.Add(plan.technology);
//         availableTech.Remove(plan.technology);
//         populationManager.ReleasePopulation(plan.technology.populationRequired);
//         aiLevelManager.AddXP(plan.technology.expReward);

//         ApplyWorldUpgrades(plan.technology);
//         ApplyEnvironmentUpgrades(plan.technology);

//         ApplyBuildingUpgrades(plan.technology);

//         //Debug.Log($"[AITechnologyManager] AI completed research on {plan.technology.technologyName}! Gained {plan.technology.expReward} XP.");
//     }

//     /// **🔹 Checks if AI has required resources for research**
//     private bool HasRequiredResources(Technology tech)
//     {
//         foreach (var requirement in tech.resourceRequirements)
//         {
//             if (!inventoryManager.HasEnoughResource(requirement.resourceID, requirement.amount))
//             {
//                 return false;
//             }
//         }
//         return true;
//     }

//     /// **🔹 Applies environment upgrades from a researched technology**
//     public void ApplyEnvironmentUpgrades(Technology tech)
//     {
//         if (tech.environmentUpgrades.Count == 0) 
//         {
//             //Debug.Log($"[AITechnologyManager] {tech.technologyName} has no environment upgrades. Skipping.");
//             return;
//         }

//         //Debug.Log($"[AITechnologyManager] Applying environment upgrades from {tech.technologyName}.");

//         // ✅ Ensure discovered and undiscovered tiles are retrieved, even if one is empty
//         List<GameObject> discoveredTiles = aiTileDiscoveryManager.GetDiscoveredTiles() ?? new List<GameObject>();
//         List<GameObject> undiscoveredTiles = aiTileDiscoveryManager.GetUndiscoveredTiles() ?? new List<GameObject>();

//         int totalTiles = discoveredTiles.Count + undiscoveredTiles.Count;
//         //Debug.Log($"[AITechnologyManager] Found {discoveredTiles.Count} discovered tiles and {undiscoveredTiles.Count} undiscovered tiles ({totalTiles} total).");

//         if (totalTiles == 0)
//         {
//             //Debug.LogWarning($"[AITechnologyManager] No valid tiles found for {tech.technologyName}. Environment upgrades will not be applied.");
//             return;
//         }

//         // ✅ Apply upgrades to discovered tiles
//         foreach (GameObject tile in discoveredTiles)
//         {
//             ApplyEnvironmentUpgradeToTile(tile, tech);
//         }

//         // ✅ Apply upgrades to undiscovered tiles
//         foreach (GameObject tile in undiscoveredTiles)
//         {
//             ApplyEnvironmentUpgradeToTile(tile, tech);
//         }
//     }

//     /// **🔹 Applies environment upgrades from a specific technology to a single tile**
//     public void ApplyEnvironmentUpgradeToTile(GameObject tile, Technology tech)
//     {
//         if (tile == null)
//         {
//             //Debug.LogWarning("[AITechnologyManager] Skipping null tile!");
//             return;
//         }

//         EnvironmentControl envControl = tile.GetComponent<EnvironmentControl>();

//         if (envControl == null)
//         {
//             //Debug.LogWarning($"[AITechnologyManager] Tile {tile.name} is missing EnvironmentControl. Skipping.");
//             return;
//         }

//         // ✅ Skip if technology was already applied
//         if (envControl.HasTechnologyApplied(tech.technologyID))
//         {
//             //Debug.Log($"[AITechnologyManager] {tile.name} already has technology {tech.technologyName}. Skipping.");
//             return;
//         }

//         //Debug.Log($"[AITechnologyManager] Checking tile {tile.name} ({envControl.environmentType}, {envControl.tileSize}) for environment upgrades from {tech.technologyName}.");

//         bool upgradeApplied = false; // Track if an upgrade was actually applied

//         foreach (EnvironmentUpgrade upgrade in tech.environmentUpgrades)
//         {
//             //Debug.Log($"[AITechnologyManager] Checking if {upgrade.upgradeType} applies to {tile.name}.");

//             // ✅ Check if upgrade applies to this tile's environment and size
//             if (upgrade.affectedEnvironmentTypes.Contains(envControl.environmentType) &&
//                 upgrade.affectedTileSizes.Contains(envControl.tileSize))
//             {
//                 //Debug.Log($"[AITechnologyManager] {upgrade.upgradeType} applies to {tile.name}. Checking if already applied...");

//                 // ✅ Apply the upgrade
//                 upgrade.ApplyUpgrade(envControl);
//                 envControl.ApplyTechnology(tech.technologyID);
//                 //Debug.Log($"[AITechnologyManager] Applied {upgrade.upgradeType} from {tech.technologyName} to {tile.name}.");
//                 upgradeApplied = true;
//             }
//             else
//             {
//                 //Debug.Log($"[AITechnologyManager] {upgrade.upgradeType} does NOT apply to {tile.name}.");
//             }
//         }

//         if (!upgradeApplied)
//         {
//             //Debug.Log($"[AITechnologyManager] No matching environment upgrades applied to {tile.name} from {tech.technologyName}.");
//         }
//     }

//     /// **🔹 Applies world upgrades from a researched technology**
//     private void ApplyWorldUpgrades(Technology tech)
//     {
//         if (tech.worldUpgrades.Count == 0) return;

//         //Debug.Log($"[AITechnologyManager] Applying world upgrades from {tech.technologyName}.");

//         foreach (WorldUpgrade upgrade in tech.worldUpgrades)
//         {
//             ApplyWorldUpgrade(upgrade, tech);
//         }
//     }

//     /// **🔹 Applies a single world upgrade from a specific technology**
//     private void ApplyWorldUpgrade(WorldUpgrade upgrade, Technology tech)
//     {
//         switch (upgrade.upgradeType)
//         {
//             case WorldUpgradeType.DecreaseSpoilageRate:
//                 aiResourceManager.ApplySpoilageRateDecrease(upgrade);
//                 break;

//             case WorldUpgradeType.IncreaseSpoilageInterval:
//                 aiResourceManager.ApplySpoilageIntervalIncrease(upgrade);
//                 break;

//             case WorldUpgradeType.ResourceUnlock:
//                 aiResourceManager.ApplyResourceUnlock(upgrade);
//                 break;

//             case WorldUpgradeType.IncreaseAgeGroupHealth:
//                 aiPlayer.GetComponentInChildren<AIPopulationManager>().ApplyAgeGroupHealthIncrease(upgrade);
//                 break;

//             case WorldUpgradeType.IncreaseRegenerationRate:
//                 aiPlayer.GetComponentInChildren<AIPopulationManager>().ApplyRegenerationRateIncrease(upgrade);
//                 break;

//             case WorldUpgradeType.BuildingUnlock:
//                 aiPlayer.GetComponentInChildren<AIBuildingManager>().ApplyBuildingUnlock(upgrade);
//                 break;

//             case WorldUpgradeType.IncreaseMaxConcurrentResearch:
//                 ApplyMaxConcurrentResearchIncrease(upgrade);
//                 break;

//             default:
//                 //Debug.Log($"[AITechnologyManager] Applying generic world upgrade: {upgrade.upgradeType} from {tech.technologyName}.");
//                 upgrade.ApplyUpgrade();
//                 break;
//         }

//         //Debug.Log($"[AITechnologyManager] Applied {upgrade.upgradeType} from {tech.technologyName}.");
//     }

//     /// **🔹 Increases the AI's max concurrent research limit**
//     private void ApplyMaxConcurrentResearchIncrease(WorldUpgrade upgrade)
//     {
//         int increaseAmount = Mathf.RoundToInt(upgrade.value);
//         maxConcurrentResearch += increaseAmount;
//         //Debug.Log($"[AITechnologyManager] Increased AI max concurrent research to {maxConcurrentResearch} (+{increaseAmount}).");
//     }

//     /// **🔹 Returns technologies that can be researched in a specific building**
//     public List<Technology> GetTechnologiesResearchableOnBuilding(string buildingID)
//     {
//         List<Technology> researchableTechs = new List<Technology>();

//         // Instead of availableTech, retrieve all technologies from the ResearchManager.
//         List<Technology> allTechs = ResearchManager.Instance.GetAllTechnologies();

//         foreach (Technology tech in allTechs)
//         {
//             // Check if this technology can be researched on the given building.
//             if (tech.researchableOnBuildings.Contains(buildingID))
//             {
//                 researchableTechs.Add(tech);
//             }
//         }

//         //Debug.Log($"[AITechnologyManager] Found {researchableTechs.Count} researchable technologies for building: {buildingID}");

//         return researchableTechs;
//     }

//     public void ApplyBuildingUpgrades(Technology tech)
//     {
//         if (tech.buildingUpgrades == null || tech.buildingUpgrades.Count == 0)
//         {
//             //Debug.Log($"[AITechnologyManager] {tech.technologyName} has no building upgrades to apply.");
//             return;
//         }

//         //Debug.Log($"[AITechnologyManager] Applying building upgrades from {tech.technologyName}.");

//         // Get the list of AI-owned buildings.
//         List<GameObject> ownedBuildings = aiBuildingManager.GetOwnedBuildings();

//         foreach (GameObject building in ownedBuildings)
//         {
//             if (building == null)
//                 continue;

//             // Use AIBuildingControl exclusively.
//             AIBuildingControl aiBuildingControl = building.GetComponent<AIBuildingControl>();

//             if (aiBuildingControl == null)
//             {
//                 //Debug.Log($"[AITechnologyManager] {building.name} does not have AIBuildingControl. Skipping.");
//                 continue;
//             }

//             // Loop through each building upgrade in the technology.
//             foreach (BuildingUpgrade upgrade in tech.buildingUpgrades)
//             {
//                 // Instead of calling upgrade.ApplyUpgrade(aiBuildingControl), call our helper:
//                 ApplyBuildingUpgradeToAIBuilding(upgrade, aiBuildingControl);
//                 //Debug.Log($"[AITechnologyManager] Processed {upgrade.upgradeType} from {tech.technologyName} on {building.name}.");
//             }
//         }
//     }

//     private void ApplyBuildingUpgradeToAIBuilding(BuildingUpgrade upgrade, AIBuildingControl aiBuildingControl)
//     {
//         // Only proceed if this upgrade is meant for this building.
//         if (!upgrade.affectedBuildingIDs.Contains(aiBuildingControl.buildingID))
//         {
//             return;
//         }
        
//         switch (upgrade.upgradeType)
//         {
//             case BuildingUpgradeType.MaxHealthIncrease:
//                 aiBuildingControl.healthSlider.maxValue += upgrade.modificationValue;
//                 aiBuildingControl.UpdateHealthSlider(aiBuildingControl.health);
//                 //Debug.Log($"Max health increased by {upgrade.modificationValue} for building ID: {aiBuildingControl.buildingID}");
//                 break;

//             case BuildingUpgradeType.DecreaseDegenerationAmount:
//                 aiBuildingControl.degenerationAmount = Mathf.Max(0, aiBuildingControl.degenerationAmount - upgrade.modificationValue);
//                 //Debug.Log($"Degeneration amount decreased by {upgrade.modificationValue} for building ID: {aiBuildingControl.buildingID}. New value: {aiBuildingControl.degenerationAmount}");
//                 break;

//             // New: Increase Degeneration Interval upgrade type
//             case BuildingUpgradeType.IncreaseDegenerationInterval:
//                 aiBuildingControl.degenerationIntervalTurns += upgrade.modificationValue;
//                 Debug.Log($"[AITechnologyManager] Degeneration interval increased by {upgrade.modificationValue} turns for building {aiBuildingControl.buildingID}. New interval: {aiBuildingControl.degenerationIntervalTurns} turns.");
//                 break;
            

//             // New: Increase Crafting Output Modifier
//             case BuildingUpgradeType.IncreaseCraftingOutputModifier:
//                 {
//                     CraftingBuildingControl craftingControl = aiBuildingControl.GetComponent<CraftingBuildingControl>();
//                     if (craftingControl != null)
//                     {
//                         foreach (var item in craftingControl.craftableItems)
//                         {
//                             if (upgrade.affectedCraftingItemIDs.Contains(item.itemID))
//                             {
//                                 foreach (var output in item.outputResources)
//                                 {
//                                     output.amount = Mathf.CeilToInt(output.amount * (1 + upgrade.modificationValue));
//                                 }
//                                 //Debug.Log($"Crafting output for {item.itemName} increased by {upgrade.modificationValue * 100}% in building ID: {aiBuildingControl.buildingID}");
//                             }
//                         }
//                     }
//                     else
//                     {
//                         //Debug.LogWarning($"[AITechnologyManager] Building {aiBuildingControl.buildingID} is not a CraftingBuildingControl. Cannot apply IncreaseCraftingOutputModifier.");
//                     }
//                     break;
//                 }

//             // New: Decrease Crafting Cost Modifier
//             case BuildingUpgradeType.DecreaseCraftingCostModifier:
//                 {
//                     CraftingBuildingControl craftingControl = aiBuildingControl.GetComponent<CraftingBuildingControl>();
//                     if (craftingControl != null)
//                     {
//                         foreach (var item in craftingControl.craftableItems)
//                         {
//                             if (upgrade.affectedCraftingItemIDs.Contains(item.itemID))
//                             {
//                                 foreach (var cost in item.resourceCost)
//                                 {
//                                     cost.amount = Mathf.Max(0, Mathf.CeilToInt(cost.amount * (1 - upgrade.modificationValue)));
//                                 }
//                                 //Debug.Log($"Crafting cost for {item.itemName} decreased by {upgrade.modificationValue * 100}% in building ID: {aiBuildingControl.buildingID}");
//                             }
//                         }
//                     }
//                     else
//                     {
//                         //Debug.LogWarning($"[AITechnologyManager] Building {aiBuildingControl.buildingID} is not a CraftingBuildingControl. Cannot apply DecreaseCraftingCostModifier.");
//                     }
//                     break;
//                 }

//             // New: Unlock Crafting Items (local to the building)
//             case BuildingUpgradeType.UnlockCraftingItems:
//                 {
//                     CraftingBuildingControl craftingControl = aiBuildingControl.GetComponent<CraftingBuildingControl>();
//                     if (craftingControl != null)
//                     {
//                         // Get the AI-specific unlock manager from the AIPlayer.
//                         AICraftingUnlockManager aiUnlockManager = aiPlayer.GetComponentInChildren<AICraftingUnlockManager>();
//                         if (aiUnlockManager == null)
//                         {
//                             //Debug.LogWarning($"[AITechnologyManager] No AICraftingUnlockManager found on AIPlayer.");
//                         }
//                         foreach (string craftingItemID in upgrade.craftingItemsToUnlock)
//                         {
//                             // Find the item in the building's craftable items list.
//                             CraftedItem item = craftingControl.craftableItems.FirstOrDefault(x => x.itemID == craftingItemID);
//                             if (item != null)
//                             {
//                                 item.isUnlocked = true;
//                                 //Debug.Log($"[AITechnologyManager] Unlocked crafting item {craftingItemID} on building {aiBuildingControl.buildingID}");
//                                 // Add the unlocked item to the AI's crafting unlock manager.
//                                 aiUnlockManager?.UnlockCraftingItem(craftingItemID);
//                             }
//                         }
//                         // Refresh the building's unlocked items list.
//                         craftingControl.UpdateUnlockedCraftingItems();
//                     }
                
//                     break;
//                 }

//                 // New: Increase Production Output Modifier
//                 case BuildingUpgradeType.IncreaseProductionOutputModifier:
//                 {
//                     ProductionBuildingControl productionControl = aiBuildingControl.GetComponent<ProductionBuildingControl>();
//                     if (productionControl != null)
//                     {
//                         foreach (var item in productionControl.availableProductionItems)
//                         {
//                             if (upgrade.affectedProductionItemIDs.Contains(item.itemID))
//                             {
//                                 foreach (var output in item.outputResources)
//                                 {
//                                     output.amount = Mathf.CeilToInt(output.amount * (1 + upgrade.modificationValue));
//                                 }
//                                 Debug.Log($"[AITechnologyManager] Increased production output for item {item.itemName} in building {aiBuildingControl.buildingID} by {upgrade.modificationValue * 100}%");
//                             }
//                         }
//                     }
//                     break;
//                 }

//                 // New: Decrease Production Cost Modifier
//                 case BuildingUpgradeType.DecreaseProductionCostModifier:
//                 {
//                     ProductionBuildingControl productionControl = aiBuildingControl.GetComponent<ProductionBuildingControl>();
//                     if (productionControl != null)
//                     {
//                         foreach (var item in productionControl.availableProductionItems)
//                         {
//                             if (upgrade.affectedProductionItemIDs.Contains(item.itemID))
//                             {
//                                 foreach (var cost in item.resourceCost)
//                                 {
//                                     cost.amount = Mathf.Max(0, Mathf.CeilToInt(cost.amount * (1 - upgrade.modificationValue)));
//                                 }
//                                 Debug.Log($"[AITechnologyManager] Decreased production cost for item {item.itemName} in building {aiBuildingControl.buildingID} by {upgrade.modificationValue * 100}%");
//                             }
//                         }
//                     }
//                     break;
//                 }

//                 // New: Unlock Production Items (similar to crafting unlocks)
//                 case BuildingUpgradeType.UnlockProductionItems:
//                 {
//                     // Get the ProductionBuildingControl from the building.
//                     ProductionBuildingControl productionControl = aiBuildingControl.GetComponent<ProductionBuildingControl>();
//                     if (productionControl != null)
//                     {
//                         // Get the AI-specific production unlock manager from the AIPlayer.
//                         AIProductionUnlockManager aiProdUnlockManager = aiPlayer.GetComponentInChildren<AIProductionUnlockManager>();
//                         if (aiProdUnlockManager == null)
//                         {
//                             Debug.LogWarning("[AITechnologyManager] No AIProductionUnlockManager found on AIPlayer.");
//                             break;
//                         }
//                         foreach (string productionItemID in upgrade.productionItemsToUnlock)
//                         {
//                             // Use availableProductionItems instead of productionItems.
//                             ProductionItem item = productionControl.availableProductionItems.FirstOrDefault(x => x.itemID == productionItemID);
//                             if (item != null)
//                             {
//                                 item.isUnlocked = true;
//                                 aiProdUnlockManager.UnlockProductionItem(productionItemID);
//                                 Debug.Log($"[AITechnologyManager] Unlocked production item {productionItemID} on building {aiBuildingControl.buildingID}");
//                             }
//                         }
//                         productionControl.UpdateUnlockedProductionItems();
//                     }
//                     break;
//                 }
//             default:
//                 //Debug.Log($"[AITechnologyManager] Applying generic upgrade {upgrade.upgradeType} to building ID: {aiBuildingControl.buildingID}.");
//                 break;
//         }
//     }

//     public void ApplyBuildingUpgradesToNewBuilding(GameObject building)
//     {
//         if (building == null)
//             return;

//         AIBuildingControl aiBuildingControl = building.GetComponent<AIBuildingControl>();

//         if (aiBuildingControl == null)
//         {
//             //Debug.Log($"[AITechnologyManager] {building.name} does not have AIBuildingControl. Cannot apply upgrades.");
//             return;
//         }

//         string buildingID = aiBuildingControl.buildingID;

//         foreach (Technology tech in researchedTechnologies)
//         {
//             if (tech.buildingUpgrades != null && tech.buildingUpgrades.Count > 0)
//             {
//                 foreach (BuildingUpgrade upgrade in tech.buildingUpgrades)
//                 {
//                     if (!string.IsNullOrEmpty(buildingID) && upgrade.affectedBuildingIDs.Contains(buildingID))
//                     {
//                         ApplyBuildingUpgradeToAIBuilding(upgrade, aiBuildingControl);
//                         //Debug.Log($"[AITechnologyManager] Applied {upgrade.upgradeType} from {tech.technologyName} to new building {building.name}.");
//                     }
//                 }
//             }
//         }
//     }

//     public AITechnologyManagerSaveData SaveState()
//     {
//         AITechnologyManagerSaveData data = new AITechnologyManagerSaveData();
//         data.maxConcurrentResearch = maxConcurrentResearch;
        
//         // Save active research plans.
//         data.activeResearchPlans = new List<ResearchPlanSaveData>();
//         foreach (ResearchPlan plan in activeResearchPlans)
//         {
//             ResearchPlanSaveData rpData = new ResearchPlanSaveData();
//             rpData.technologyID = plan.technology.technologyID;
//             rpData.remainingTurns = plan.remainingTurns;
//             data.activeResearchPlans.Add(rpData);
//         }
        
//         // Save researched technologies.
//         data.researchedTechnologies = new List<TechnologySaveData>();
//         foreach (Technology tech in researchedTechnologies)
//         {
//             TechnologySaveData tsData = new TechnologySaveData();
//             tsData.technologyID = tech.technologyID;
//             tsData.technologyName = tech.technologyName;
//             tsData.isResearched = tech.isResearched;
//             tsData.turnsToComplete = tech.turnsToComplete;
//             tsData.remainingTurns = tech.remainingTurns;
//             data.researchedTechnologies.Add(tsData);
//         }
        
//         return data;
//     }

//     public void LoadState(AITechnologyManagerSaveData data)
//     {
//         if (data == null) return;
        
//         maxConcurrentResearch = data.maxConcurrentResearch;
//         activeResearchPlans.Clear();
//         researchedTechnologies.Clear();
        
//         // Rebuild active research plans from the saved data.
//         foreach (ResearchPlanSaveData rpData in data.activeResearchPlans)
//         {
//             // Look up the technology from availableTech.
//             Technology tech = availableTech.Find(t => t.technologyID == rpData.technologyID);
//             if (tech != null)
//             {
//                 tech.remainingTurns = rpData.remainingTurns;
//                 activeResearchPlans.Add(new ResearchPlan(tech, rpData.remainingTurns));
//             }
//         }
        
//         // Rebuild researched technologies.
//         foreach (TechnologySaveData tsData in data.researchedTechnologies)
//         {
//             Technology tech = availableTech.Find(t => t.technologyID == tsData.technologyID);
//             if (tech != null)
//             {
//                 tech.isResearched = tsData.isResearched;
//                 tech.turnsToComplete = tsData.turnsToComplete;
//                 tech.remainingTurns = tsData.remainingTurns;
//                 ApplyWorldUpgrades(tech);
//                 ApplyEnvironmentUpgrades(tech);
//                 ApplyBuildingUpgrades(tech);
//                 researchedTechnologies.Add(tech);
//             }
//         }
//     }
// }

// [System.Serializable]
// public class ResearchPlan
// {
//     public Technology technology;
//     public int remainingTurns;

//     public ResearchPlan(Technology tech, int turns)
//     {
//         technology = tech;
//         remainingTurns = turns;
//     }
// }