// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;

// [System.Serializable]
// public class EnvironmentPriority
// {
//     public EnvironmentType environmentType;
//     public int priority; // Higher value = higher priority
// }

// public class AIEnvironmentPriorityCalculator : MonoBehaviour
// {
//     [SerializeField] private List<EnvironmentPriority> environmentPriorities = new List<EnvironmentPriority>();
//     private Dictionary<EnvironmentType, int> priorityDictionary = new Dictionary<EnvironmentType, int>();

//     private void Awake()
//     {
//         InitializePriorityDictionary();
//     }

//     private void InitializePriorityDictionary()
//     {
//         priorityDictionary.Clear();
//         foreach (var envPriority in environmentPriorities)
//             priorityDictionary[envPriority.environmentType] = envPriority.priority;

//         //Debug.Log("[AIEnvironmentPriority] Environment priorities initialized.");
//     }

//     public int GetEnvironmentPriority(EnvironmentType envType)
//     {
//         return priorityDictionary.TryGetValue(envType, out int priority) ? priority : 0;
//     }

//     public void SetEnvironmentPriority(EnvironmentType envType, int newPriority)
//     {
//         priorityDictionary[envType] = newPriority;

//         for (int i = 0; i < environmentPriorities.Count; i++)
//         {
//             if (environmentPriorities[i].environmentType == envType)
//             {
//                 environmentPriorities[i].priority = newPriority;
//                 return;
//             }
//         }

//         environmentPriorities.Add(new EnvironmentPriority { environmentType = envType, priority = newPriority });
//     }

//     public List<EnvironmentType> GetHighPriorityEnvironments() => GetEnvironmentsByPriority(80);
//     public List<EnvironmentType> GetMediumPriorityEnvironments() => GetEnvironmentsByPriority(40, 79);
//     public List<EnvironmentType> GetLowPriorityEnvironments() => GetEnvironmentsByPriority(1, 39);

//     private List<EnvironmentType> GetEnvironmentsByPriority(int minPriority, int maxPriority = int.MaxValue)
//     {
//         List<EnvironmentType> filteredList = new List<EnvironmentType>();
//         foreach (var kvp in priorityDictionary)
//         {
//             if (kvp.Value >= minPriority && kvp.Value <= maxPriority)
//                 filteredList.Add(kvp.Key);
//         }
//         return filteredList;
//     }
// }