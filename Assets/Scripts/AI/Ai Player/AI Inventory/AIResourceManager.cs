// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;

// public class AIResourceManager : MonoBehaviour
// {
//     public List<Resource> aiFoodResources = new List<Resource>();
//     public List<Resource> aiMaterialResources = new List<Resource>();
//     public List<Resource> aiCraftedResources = new List<Resource>();

//     private ResourceManager resourceManager;
//     private AILevelManager levelManager;

//     public bool hasResourceList = false;

//     private void Awake()
//     {
//         // Use Awake to cache references early
//         Transform aiPlayer = transform.parent;
//         if (aiPlayer == null)
//         {
//             //Debug.LogError("[AIResourceManager] AIPlayer missing! Disabling AIResourceManager.");
//             enabled = false;
//             return;
//         }

//         levelManager = aiPlayer.GetComponentInChildren<AILevelManager>();
//         if (levelManager == null)
//         {
//             //Debug.LogError("[AIResourceManager] AILevelManager missing! AI resource unlocking disabled.");
//             enabled = false;
//         }

//         resourceManager = FindObjectOfType<ResourceManager>();
//         if (resourceManager == null)
//         {
//             //Debug.LogError("[AIResourceManager] No ResourceManager found in the scene!");
//             enabled = false;
//         }

//         if (!hasResourceList)
//         {
//             InitializeAIResources();
//         }
//     }

//     /// **🔹 Initializes AI's resource list based on unlocked resources**
//     private void InitializeAIResources()
//     {
//         aiFoodResources.Clear();
//         aiMaterialResources.Clear();
//         aiCraftedResources.Clear();

//         if (resourceManager == null || levelManager == null)
//         {
//             //Debug.LogWarning("[AIResourceManager] Initialization failed due to missing dependencies.");
//             return;
//         }

//         foreach (Resource resource in resourceManager.GetAllResources())
//         {
//             if (IsResourceUnlocked(resource))
//             {
//                 AddAIResource(resource);
//             }
//         }

//         hasResourceList = true;

//         //Debug.Log($"[AIResourceManager] Initialized AI with {aiFoodResources.Count} food, {aiMaterialResources.Count} materials, and {aiCraftedResources.Count} crafted resources.");
//     }

//     /// **🔹 Check if the AI has unlocked a resource**
//     public bool IsResourceUnlocked(Resource resource)
//     {
//         return resource != null &&
//                resource.unlockLevel <= levelManager.GetAILevel() &&
//                (!resource.requiresResearch || resource.isResearched);
//     }

//     /// **🔹 Add resource to AI's available resource list**
//     public void AddAIResource(Resource resource)
//     {
//         if (resource == null || GetAIResourceByID(resource.resourceID) != null)
//             return; // Ignore duplicate or invalid resources

//         switch (resource.resourceType)
//         {
//             case ResourceType.Food:
//                 aiFoodResources.Add(resource);
//                 break;
//             case ResourceType.Material:
//                 aiMaterialResources.Add(resource);
//                 break;
//             default:
//                 aiCraftedResources.Add(resource);
//                 break;
//         }
//     }

//     /// **🔹 Get a resource by its ID**
//     public Resource GetAIResourceByID(string id)
//     {
//         if (string.IsNullOrEmpty(id)) return null;

//         return aiFoodResources.Find(r => r.resourceID == id) ??
//                aiMaterialResources.Find(r => r.resourceID == id) ??
//                aiCraftedResources.Find(r => r.resourceID == id);
//     }

//     /// **🔹 Get resources by type**
//     public List<Resource> GetResourcesByType(ResourceType resourceType)
//     {
//         List<Resource> resources = new List<Resource>();

//         if(resourceType == ResourceType.Food)
//         {
//             // Add native Food resources...
//             resources.AddRange(aiFoodResources);
//             // ...plus any crafted resources that are of type Food.
//             resources.AddRange(aiCraftedResources.FindAll(r => r.resourceType == ResourceType.Food));
//         }
//         else if(resourceType == ResourceType.Material)
//         {
//             // Add native Material resources...
//             resources.AddRange(aiMaterialResources);
//             // ...plus any crafted resources that are of type Material.
//             resources.AddRange(aiCraftedResources.FindAll(r => r.resourceType == ResourceType.Material));
//         }
//         else
//         {
//             // For any other type, default to just the crafted resources.
//             resources.AddRange(aiCraftedResources);
//         }

//         return resources;
//     }

//     /// **🔹 Unlocks a resource for AI when it levels up**
//     public void UnlockResourcesForAILevel(int newLevel)
//     {
//         if (resourceManager == null) return;

//         int unlockedCount = 0;
//         foreach (var resource in resourceManager.GetAllResources())
//         {
//             if (resource.unlockLevel == newLevel && GetAIResourceByID(resource.resourceID) == null)
//             {
//                 AddAIResource(resource);
//                 unlockedCount++;
//             }
//         }

//         if (unlockedCount > 0)
//         {
//             //Debug.Log($"[AIResourceManager] AI unlocked {unlockedCount} new resources at Level {newLevel}.");
//         }
//     }

//     /// **🔹 Applies spoilage rate decrease to AI's resources**
//     public void ApplySpoilageRateDecrease(WorldUpgrade upgrade)
//     {
//         foreach (string resourceID in upgrade.resourceIDs)
//         {
//             Resource resource = GetAIResourceByID(resourceID);
//             if (resource != null)
//             {
//                 resource.spoilageRate = Mathf.Max(0, resource.spoilageRate - upgrade.value);
//                 //Debug.Log($"[AIResourceManager] AI Spoilage rate for {resource.resourceName} decreased to {resource.spoilageRate}%.");
//             }
//         }
//     }

//     /// **🔹 Applies spoilage interval increase to AI's resources**
//     public void ApplySpoilageIntervalIncrease(WorldUpgrade upgrade)
//     {
//         foreach (string resourceID in upgrade.resourceIDs)
//         {
//             Resource resource = GetAIResourceByID(resourceID);
//             if (resource != null)
//             {
//                 resource.spoilageInterval += Mathf.RoundToInt(upgrade.value);
//                 //Debug.Log($"[AIResourceManager] AI Spoilage interval for {resource.resourceName} increased to {resource.spoilageInterval} turns.");
//             }
//         }
//     }

//     /// **🔹 Applies resource unlocks from a world upgrade**
//     public void ApplyResourceUnlock(WorldUpgrade upgrade)
//     {
//         if (upgrade.resourceIDs == null || upgrade.resourceIDs.Count == 0) return;

//         int unlockedCount = 0;

//         foreach (string resourceID in upgrade.resourceIDs)
//         {
//             Resource resource = resourceManager.GetResourceByID(resourceID);
//             if (resource != null && GetAIResourceByID(resourceID) == null)
//             {
//                 AddAIResource(resource);
//                 // If the resource requires research, mark it as researched.
//                 if (resource.requiresResearch)
//                 {
//                     resource.isResearched = true;
//                 }
//                 unlockedCount++;
//                 //Debug.Log($"[AIResourceManager] AI unlocked new resource: {resource.resourceName}. Marked as researched.");
//             }
//         }
//     }

//     public AIResourceManagerSaveData SaveState()
//     {
//         AIResourceManagerSaveData data = new AIResourceManagerSaveData();

//         // Save food resources.
//         foreach (Resource resource in aiFoodResources)
//         {
//             data.savedFoodResources.Add(SaveResource(resource));
//         }

//         // Save material resources.
//         foreach (Resource resource in aiMaterialResources)
//         {
//             data.savedMaterialResources.Add(SaveResource(resource));
//         }

//         // Save crafted resources.
//         foreach (Resource resource in aiCraftedResources)
//         {
//             data.savedCraftedResources.Add(SaveResource(resource));
//         }

//         return data;
//     }

//     private FullResourceSaveData SaveResource(Resource resource)
//     {
//         return new FullResourceSaveData
//         {
//             resourceID = resource.resourceID,
//             resourceName = resource.resourceName,
//             description = resource.description,
//             resourceType = resource.resourceType,
//             unlockLevel = resource.unlockLevel,
//             requiresResearch = resource.requiresResearch,
//             isResearched = resource.isResearched,
//             spoilageRate = resource.spoilageRate,
//             spoilageInterval = resource.spoilageInterval,
//             resourceIconName = resource.resourceIcon != null ? resource.resourceIcon.name : null
//         };
//     }

//     public void LoadState(AIResourceManagerSaveData data)
//     {
//         if (data == null) return;

//         // Option 1: If you want to reinitialize from scratch:
//         InitializeAIResources();

//         // Option 2: Or update existing resources.
//         // Here we update fields of already added resources.
//         LoadResourcesFromList(data.savedFoodResources, aiFoodResources);
//         LoadResourcesFromList(data.savedMaterialResources, aiMaterialResources);
//         LoadResourcesFromList(data.savedCraftedResources, aiCraftedResources);

//         Debug.Log("[AIResourceManager] AI resources restored from save data.");
//     }

//     private void LoadResourcesFromList(List<FullResourceSaveData> savedList, List<Resource> targetList)
//     {
//         foreach (var savedRes in savedList)
//         {
//             Resource resource = targetList.Find(r => r.resourceID == savedRes.resourceID);
//             if (resource != null)
//             {
//                 resource.resourceName = savedRes.resourceName;
//                 resource.description = savedRes.description;
//                 resource.resourceType = savedRes.resourceType;
//                 resource.unlockLevel = savedRes.unlockLevel;
//                 resource.requiresResearch = savedRes.requiresResearch;
//                 resource.isResearched = savedRes.isResearched;
//                 resource.spoilageRate = savedRes.spoilageRate;
//                 resource.spoilageInterval = savedRes.spoilageInterval;
//                 // Optionally, update resourceIcon by searching Resources if needed.
//             }
//         }
//     }
// }