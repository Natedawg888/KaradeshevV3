// using System;
// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;

// public class AIDiseaseManager : MonoBehaviour
// {
//     private AIPopulationManager aiPopulationManager;
//     private AIPlayer aiPlayer;
    
//     [Header("Active AI Diseases")]
//     [SerializeField] private List<Disease> activeAIDiseases = new List<Disease>(); // List of diseases affecting AI
    
//     private int diseaseDamageTurnCounter = 0; // Counter to track turns for applying damage
//     public event Action<Disease> OnAIDiseaseRemoved;

//     private void Start()
//     {
//         aiPlayer = GetComponentInParent<AIPlayer>();
//         aiPopulationManager = aiPlayer?.GetComponentInChildren<AIPopulationManager>();

//         if (aiPopulationManager == null)
//         {
//             //Debug.LogWarning("[AIDiseaseManager] AI Population Manager is missing!");
//             return;
//         }

//         TurnSystem.SubscribeToEndOfTurn(HandleEndOfTurn);
//     }

//     private void OnDestroy()
//     {
//         TurnSystem.UnsubscribeFromEndOfTurn(HandleEndOfTurn);
//     }

//     /// **🔹 Applies a disease to the AI**
//     public void ApplyAIDisease(Disease newDisease)
//     {
//         if (activeAIDiseases.Exists(disease => disease.diseaseID == newDisease.diseaseID))
//         {
//             //Debug.Log($"[AIDiseaseManager] AI already has {newDisease.diseaseName}, skipping.");
//             return;
//         }

//         int maxAIDiseasesAllowed = Mathf.Max(1, Mathf.RoundToInt(aiPopulationManager.GetCurrentPopulation() * 0.1f));
//         if (activeAIDiseases.Count >= maxAIDiseasesAllowed)
//         {
//             //Debug.Log($"[AIDiseaseManager] AI cannot receive more diseases. Limit reached.");
//             return;
//         }

//         newDisease.turnsRemaining = newDisease.durationInTurns;
//         newDisease.ResetCurrentDamage();
//         activeAIDiseases.Add(newDisease);
        
//         //Debug.Log($"[AIDiseaseManager] AI infected with {newDisease.diseaseName}.");
//     }

//     public List<Disease> GetActiveAIDiseases() => activeAIDiseases;

//     private void HandleEndOfTurn()
//     {
//         ProcessAIDiseases();
//     }

//     /// **🔹 Process AI diseases each turn**
//     private void ProcessAIDiseases()
//     {
//         if (activeAIDiseases.Count == 0) return;

//         diseaseDamageTurnCounter++;

//         // **Apply disease damage every 4 turns**
//         if (diseaseDamageTurnCounter % 4 == 0)
//         {
//             foreach (var disease in activeAIDiseases)
//             {
//                 ApplyDiseaseDamageToAI(disease);
//                 disease.ModifyCurrentDamage();
//             }
//         }

//         List<Disease> diseasesToRemove = new List<Disease>();

//         foreach (var disease in activeAIDiseases)
//         {
//             disease.turnsRemaining--;

//             if (disease.turnsRemaining <= 0)
//             {
//                 // **Check if the disease should continue**
//                 if (UnityEngine.Random.value > disease.continuationChance / 100f)
//                 {
//                     diseasesToRemove.Add(disease);
//                 }
//                 else
//                 {
//                     disease.turnsRemaining = disease.durationInTurns;
//                 }
//             }
//         }

//         foreach (var disease in diseasesToRemove)
//         {
//             activeAIDiseases.Remove(disease);
//             OnAIDiseaseRemoved?.Invoke(disease);
//             //Debug.Log($"[AIDiseaseManager] Disease {disease.diseaseName} has ended for AI.");
//         }

//         if (diseaseDamageTurnCounter >= 4) diseaseDamageTurnCounter = 0;
//     }

//     /// **🔹 Applies disease damage to AI population groups**
//     private void ApplyDiseaseDamageToAI(Disease disease)
//     {
//         //Debug.Log($"[AIDiseaseManager] Applying {disease.diseaseName} damage to AI.");

//         foreach (AgeGroup ageGroup in disease.affectedAgeGroups)
//         {
//             int damageToChildren = (ageGroup == AgeGroup.Child) ? disease.currentDamageToChildren : 0;
//             int damageToTeens = (ageGroup == AgeGroup.Teen) ? disease.currentDamageToTeens : 0;
//             int damageToAdults = (ageGroup == AgeGroup.Adult) ? disease.currentDamageToAdults : 0;
//             int damageToElders = (ageGroup == AgeGroup.Elder) ? disease.currentDamageToElders : 0;

//             //Debug.Log($"[AIDiseaseManager] {disease.diseaseName} -> Child: {damageToChildren}, Teen: {damageToTeens}, Adult: {damageToAdults}, Elder: {damageToElders}");

//             aiPopulationManager.ApplyDiseaseDamage(damageToChildren, damageToTeens, damageToAdults, damageToElders);
//         }
//     }

//     /// **🔹 Retrieves a disease by ID from DiseaseControl**
//     public Disease GetAIDiseaseByID(string diseaseID)
//     {
//         return DiseaseControl.Instance?.GetDiseaseByID(diseaseID);
//     }

//     public AIDiseaseManagerSaveData SaveState()
//     {
//         AIDiseaseManagerSaveData data = new AIDiseaseManagerSaveData();
//         data.diseaseDamageTurnCounter = diseaseDamageTurnCounter;
//         data.activeAIDiseases = new List<DiseaseSaveData>();
    
//         foreach (Disease disease in activeAIDiseases)
//         {
//             DiseaseSaveData ds = new DiseaseSaveData();
//             ds.diseaseID = disease.diseaseID;
//             ds.diseaseName = disease.diseaseName;
//             ds.diseaseIconName = disease.diseaseIcon != null ? disease.diseaseIcon.name : "";
//             ds.damageToChildren = disease.damageToChildren;
//             ds.damageToTeens = disease.damageToTeens;
//             ds.damageToAdults = disease.damageToAdults;
//             ds.damageToElders = disease.damageToElders;
//             ds.durationInTurns = disease.durationInTurns;
//             ds.continuationChance = disease.continuationChance;
//             ds.applyChance = disease.applyChance;
//             ds.damageChangePercentagePerTurn = disease.damageChangePercentagePerTurn;
//             ds.turnsRemaining = disease.turnsRemaining;
//             ds.affectedAgeGroups = new List<AgeGroup>(disease.affectedAgeGroups);
    
//             ds.currentDamageToChildren = disease.currentDamageToChildren;
//             ds.currentDamageToTeens = disease.currentDamageToTeens;
//             ds.currentDamageToAdults = disease.currentDamageToAdults;
//             ds.currentDamageToElders = disease.currentDamageToElders;
    
//             data.activeAIDiseases.Add(ds);
//         }
    
//         return data;
//     }

//     public void LoadState(AIDiseaseManagerSaveData data)
//     {
//         if (data == null) return;
    
//         diseaseDamageTurnCounter = data.diseaseDamageTurnCounter;
//         activeAIDiseases.Clear();
    
//         foreach (DiseaseSaveData ds in data.activeAIDiseases)
//         {
//             Sprite diseaseIcon = Resources.Load<Sprite>("UI_Assets/" + ds.diseaseIconName);

//             // Create a new Disease instance and restore its values.
//                 Disease disease = new Disease(
//                 ds.diseaseID,
//                 ds.diseaseName,
//                 diseaseIcon,
//                 ds.damageToChildren,
//                 ds.damageToTeens,
//                 ds.damageToAdults,
//                 ds.damageToElders,
//                 ds.durationInTurns,
//                 ds.continuationChance,
//                 ds.applyChance,
//                 ds.affectedAgeGroups,
//                 ds.damageChangePercentagePerTurn
//             );
    
//             // Restore additional values
//             disease.turnsRemaining = ds.turnsRemaining;
//             disease.currentDamageToChildren = ds.currentDamageToChildren;
//             disease.currentDamageToTeens = ds.currentDamageToTeens;
//             disease.currentDamageToAdults = ds.currentDamageToAdults;
//             disease.currentDamageToElders = ds.currentDamageToElders;
    
//             activeAIDiseases.Add(disease);
//         }
//     }
// }