using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ResourceNodeManager : MonoBehaviour
{
    public static ResourceNodeManager Instance { get; private set; }

    [Header("Initial Generation Batching")]
    [Min(1)]
    [Tooltip("How many nodes to call GenerateResources() on per frame when doing a world-wide initial spawn.")]
    public int nodesPerFrameGeneration = 64;

    [Range(0f, 0.05f)]
    [Tooltip("Optional wait between generation batches.")]
    public float generationBatchWaitSeconds = 0f;

    [Header("Debug")]
    [SerializeField, Tooltip("All tracked resource nodes in the scene (auto-managed).")]
    private List<EnvironmentResourceNode> _nodes = new();

    private Coroutine _generationRoutine;

    // ---------- LIFECYCLE ----------

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDisable()
    {
        if (_generationRoutine != null)
        {
            StopCoroutine(_generationRoutine);
            _generationRoutine = null;
        }
    }

    // ---------- PUBLIC REGISTRATION API ----------

    public void RegisterNode(EnvironmentResourceNode node)
    {
        if (node == null) return;

        if (_nodes == null)
            _nodes = new List<EnvironmentResourceNode>();

        if (!_nodes.Contains(node))
            _nodes.Add(node);
    }

    public void UnregisterNode(EnvironmentResourceNode node)
    {
        if (node == null || _nodes == null) return;
        _nodes.Remove(node);
    }

    public IReadOnlyList<EnvironmentResourceNode> GetAllNodes()
        => _nodes ?? (IReadOnlyList<EnvironmentResourceNode>)System.Array.Empty<EnvironmentResourceNode>();

    // ---------- INITIAL GENERATION (one-off, batched) ----------

    /// <summary>
    /// Call this once when your map is fully generated
    /// to run GenerateResources() on all nodes, batched across frames.
    /// </summary>
    public void GenerateAllNodesInitialBatched()
    {
        if (_generationRoutine != null)
            StopCoroutine(_generationRoutine);

        _generationRoutine = StartCoroutine(GenerateAllNodesCoroutine());
    }

    private IEnumerator GenerateAllNodesCoroutine()
    {
        if (_nodes == null || _nodes.Count == 0)
        {
            _generationRoutine = null;
            yield break;
        }

        int processed    = 0;
        int perFrame     = Mathf.Max(1, nodesPerFrameGeneration);
        float wait       = generationBatchWaitSeconds;

        for (int i = 0; i < _nodes.Count; i++)
        {
            var node = _nodes[i];
            if (node == null) continue;

            node.GenerateResources();

            processed++;
            if (processed % perFrame == 0)
            {
                if (wait > 0f)
                    yield return new WaitForSeconds(wait);
                else
                    yield return null;
            }
        }

        _generationRoutine = null;
    }

}
