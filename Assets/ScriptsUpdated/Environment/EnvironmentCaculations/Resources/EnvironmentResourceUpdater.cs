using System.Collections;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

// Processes EnvironmentResourceNode.TickResourceLifecycle across all registered nodes
// each turn, spread across frames via nodesPerFrame batching.
//
// NOTE: In the Unity Editor, stack trace generation on Debug.Log is very expensive.
// The main fix is guarding per-turn logs (debugNodeProcessing / debugSpawnerLogging on each node).
// As a secondary measure you can also disable stack traces for Log messages in
// Edit → Project Settings → Player → Stack Trace → Log = None.
public class EnvironmentResourceUpdater : MonoBehaviour
{
    [Header("Processing")]
    [Tooltip("How many nodes to process per frame to spread turn cost.")]
    public int nodesPerFrame = 10;

    [Header("Debug (default off — enable only for profiling)")]
    [Tooltip("Log once-per-turn summary of processed nodes/resources.")]
    [SerializeField] private bool debugSpawnerSummary;
    [Tooltip("Log a line for each individual node processed.")]
    [SerializeField] private bool debugNodeProcessing;
    [Tooltip("Log general updater events (season change, list refresh).")]
    [SerializeField] private bool debugLogging;

    // ── Profiler markers ──────────────────────────────────────────────────────
    private static readonly ProfilerMarker s_processNodeMarker =
        new ProfilerMarker("EnvironmentResourceUpdater.ProcessNode");
    private static readonly ProfilerMarker s_batchMarker =
        new ProfilerMarker("EnvironmentResourceUpdater.ProcessBatch");

    // ── Node registry ─────────────────────────────────────────────────────────
    private readonly List<EnvironmentResourceNode> _allNodes = new();
    private static EnvironmentResourceUpdater s_instance;
    private static readonly IReadOnlyList<EnvironmentResourceNode> _emptyNodes = new List<EnvironmentResourceNode>();

    public static IReadOnlyList<EnvironmentResourceNode> AllNodes =>
        s_instance != null ? s_instance._allNodes : _emptyNodes;

    public static void RegisterNode(EnvironmentResourceNode node)
    {
        if (s_instance == null || node == null) return;
        if (!s_instance._allNodes.Contains(node))
            s_instance._allNodes.Add(node);
    }

    public static void UnregisterNode(EnvironmentResourceNode node)
    {
        if (s_instance == null || node == null) return;
        s_instance._allNodes.Remove(node);
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        s_instance = this;
    }

    private void OnEnable()
    {
        TurnSystem.SubscribeToEndOfTurn(OnTurnEnded);

        if (SeasonManager.Instance != null)
            SeasonManager.Instance.OnSeasonChanged += OnSeasonChanged;
    }

    private void OnDisable()
    {
        TurnSystem.UnsubscribeFromEndOfTurn(OnTurnEnded);

        if (SeasonManager.Instance != null)
            SeasonManager.Instance.OnSeasonChanged -= OnSeasonChanged;
    }

    private void Start()
    {
        // Seed the list once. After this, nodes self-register via OnEnable/OnDisable.
        _allNodes.Clear();
        _allNodes.AddRange(FindObjectsOfType<EnvironmentResourceNode>());
        if (debugLogging)
            Debug.Log($"[ResourceUpdater] Seeded node list with {_allNodes.Count} nodes.");
    }

    // ── Turn handling ─────────────────────────────────────────────────────────

    private void OnTurnEnded()
    {
        StartCoroutine(ProcessNodeLifecycle());
    }

    private void OnSeasonChanged(SeasonDefinition newSeason)
    {
        if (debugLogging)
            Debug.Log($"[ResourceUpdater] Season changed — regenerating resources for {_allNodes.Count} nodes.");
        StartCoroutine(GenerateSeasonResources());
    }

    private IEnumerator ProcessNodeLifecycle()
    {
        using (s_batchMarker.Auto())
        {
            int processed  = 0;
            int frameCount = 0;
            int nullRemoved = 0;

            for (int i = _allNodes.Count - 1; i >= 0; i--)
            {
                var node = _allNodes[i];
                if (node == null)
                {
                    _allNodes.RemoveAt(i);
                    nullRemoved++;
                    continue;
                }

                using (s_processNodeMarker.Auto())
                {
                    if (debugNodeProcessing)
                        Debug.Log($"[ResourceUpdater] Processing node '{node.name}'");

                    node.TickResourceLifecycle();
                }

                processed++;
                frameCount++;
                if (frameCount >= nodesPerFrame)
                {
                    frameCount = 0;
                    yield return null;
                }
            }

            if (debugSpawnerSummary)
                Debug.Log($"[ResourceUpdater] Turn lifecycle complete — " +
                          $"processed={processed} nodes, stale removed={nullRemoved}");
        }
    }

    private IEnumerator GenerateSeasonResources()
    {
        int processed  = 0;
        int frameCount = 0;

        for (int i = _allNodes.Count - 1; i >= 0; i--)
        {
            var node = _allNodes[i];
            if (node == null)
            {
                _allNodes.RemoveAt(i);
                continue;
            }

            node.GenerateResources();
            processed++;
            frameCount++;

            if (frameCount >= nodesPerFrame)
            {
                frameCount = 0;
                yield return null;
            }
        }

        if (debugLogging)
            Debug.Log($"[ResourceUpdater] Season resource generation complete — {processed} nodes.");
    }
}
