using System.Collections.Generic;
using UnityEngine;

// Place one instance in the FinalSetup scene.
// Each turn this rolls a per-visitor-type chance on every registered node.
// When the roll succeeds, a temporary spawner is added to that node, producing
// the animal as a catchable resource. The spawner expires after a short lifetime
// (the animal "leaves"), or immediately after one use (it was caught).
//
// Setup:
//   1. Run Tools → Kardeshev → Create Small Animal Visitor Spawners to generate the SOs.
//   2. Add this component to a scene GameObject.
//   3. Populate the Visitors list with the generated SOs and configure each entry's
//      allowed environments, tile types, chance, and lifetime.
public class SmallAnimalVisitorSystem : MonoBehaviour
{
    [System.Serializable]
    public class VisitorEntry
    {
        public ResourceSpawnerDefinition spawner;

        [Tooltip("Which environment types allow this visitor. Empty = any.")]
        public List<EnvironmentType> allowedEnvironments = new();

        [Tooltip("Which tile types allow this visitor. Empty = any.")]
        public List<EnvironmentTileType> allowedTileTypes = new();

        [Range(0f, 1f)]
        [Tooltip("Probability per turn that this visitor appears on any qualifying node.")]
        public float visitChancePerTurn = 0.05f;

        [Tooltip("How many turns the visitor stays before leaving if uncaught.")]
        [Min(1)] public int lifetimeTurns = 4;
    }

    [Header("Visitors")]
    public List<VisitorEntry> visitors = new();

    public static SmallAnimalVisitorSystem Instance { get; private set; }

    [Header("Debug")]
    [SerializeField] private bool debugLogging;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    internal void Tick()
    {
        var nodes = EnvironmentResourceUpdater.AllNodes;
        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            if (node == null || node.IsBarren) continue;
            TryAddVisitor(node);
        }
    }

    private void TryAddVisitor(EnvironmentResourceNode node)
    {
        var ec = node.environmentControl;
        if (ec == null) return;

        for (int v = 0; v < visitors.Count; v++)
        {
            var entry = visitors[v];
            if (entry?.spawner == null) continue;
            if (node.HasSpawner(entry.spawner.spawnerID)) continue; // already visiting

            if (entry.allowedEnvironments.Count > 0
                && !entry.allowedEnvironments.Contains(ec.environmentType))
                continue;

            if (entry.allowedTileTypes.Count > 0
                && !entry.allowedTileTypes.Contains(ec.environmentTileType))
                continue;

            if (Random.value > entry.visitChancePerTurn) continue;

            node.AddTemporarySpawner(entry.spawner, entry.lifetimeTurns,
                SpawnerSourceReason.AnimalPresence, runImmediately: true);

            if (debugLogging)
                Debug.Log($"[AnimalVisitor] '{entry.spawner.displayName}' visiting " +
                          $"'{node.name}' ({ec.environmentType}/{ec.environmentTileType}) " +
                          $"for {entry.lifetimeTurns} turns.");
            return; // one new visitor per node per turn
        }
    }
}
