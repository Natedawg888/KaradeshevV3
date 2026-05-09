using System;
using System.Collections.Generic;
using UnityEngine;

// Attach to the same GameObject as EnvironmentResourceNode.
// Wire animal-death logic to fire OnAnimalDied with an AnimalDeathSpawnRequest.
//
// Example call site (inside AnimalSimulation or a controller):
//   AnimalDeathResourceSpawnerHandler.OnAnimalDied?.Invoke(new AnimalDeathSpawnRequest
//   {
//       targetNode = node,
//       speciesID  = group.species?.speciesID ?? "",
//       groupSize  = deaths
//   });
public class AnimalDeathResourceSpawnerHandler : MonoBehaviour
{
    [Header("Default Remains Spawner")]
    [Tooltip("Used when no species-specific spawner matches.")]
    public ResourceSpawnerDefinition defaultRemainsSpawner;

    [Header("Species-Specific Spawners")]
    [Tooltip("Map species IDs to custom carcass spawner definitions.")]
    public List<AnimalRemainsEntry> speciesSpawners = new();

    [Header("Lifetime")]
    [Tooltip("How many turns the carcass spawner stays active.")]
    [Min(1)] public int carcassLifetimeTurns = 5;

    [Header("Debug")]
    [SerializeField] private bool debugLogging;

    // Static event — fire from anywhere in the animal system
    public static Action<AnimalDeathSpawnRequest> OnAnimalDied;

    private EnvironmentResourceNode node;

    // ── Setup ──────────────────────────────────────────────────────────────

    private void Awake()
    {
        node = GetComponent<EnvironmentResourceNode>();
        if (node == null)
            Debug.LogWarning($"[AnimalDeath] [{name}] No EnvironmentResourceNode found.");
    }

    private void OnEnable()  => OnAnimalDied += HandleAnimalDied;
    private void OnDisable() => OnAnimalDied -= HandleAnimalDied;

    // ── Handler ────────────────────────────────────────────────────────────

    private void HandleAnimalDied(AnimalDeathSpawnRequest request)
    {
        if (node == null || request == null) return;
        if (request.targetNode != node) return;

        var spawnerDef = GetSpawnerForSpecies(request.speciesID) ?? defaultRemainsSpawner;
        if (spawnerDef == null)
        {
            Debug.LogWarning($"[AnimalDeath] [{name}] No remains spawner for species '{request.speciesID}' " +
                             "and no defaultRemainsSpawner assigned.");
            return;
        }

        node.SetTileState(TileStateFlags.HasCarcass, true);
        node.AddTemporarySpawner(spawnerDef, carcassLifetimeTurns,
                                 SpawnerSourceReason.AnimalDeath, runImmediately: true);

        if (debugLogging)
            Debug.Log($"[AnimalDeath] [{name}] Event-based spawner CREATED: " +
                      $"species='{request.speciesID}' size={request.groupSize} " +
                  $"spawner='{spawnerDef.displayName}' lifetime={carcassLifetimeTurns}t");
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private ResourceSpawnerDefinition GetSpawnerForSpecies(string speciesID)
    {
        if (string.IsNullOrEmpty(speciesID) || speciesSpawners == null) return null;
        foreach (var entry in speciesSpawners)
        {
            if (entry != null && string.Equals(entry.speciesID, speciesID,
                StringComparison.OrdinalIgnoreCase))
                return entry.remainsSpawner;
        }
        return null;
    }

    // ── Entry type ─────────────────────────────────────────────────────────

    [Serializable]
    public class AnimalRemainsEntry
    {
        [Tooltip("Match against AnimalDefinition.speciesID (case-insensitive).")]
        public string speciesID;
        public ResourceSpawnerDefinition remainsSpawner;
    }
}

// ── Request object ─────────────────────────────────────────────────────────
public class AnimalDeathSpawnRequest
{
    public EnvironmentResourceNode targetNode;
    public string speciesID;
    public int    groupSize;
}
