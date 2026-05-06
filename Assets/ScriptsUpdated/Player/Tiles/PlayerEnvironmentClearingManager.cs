using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerEnvironmentClearingManager : MonoBehaviour
{
    public static PlayerEnvironmentClearingManager Instance { get; private set; }

    [Header("Prefabs & References")]
    [Tooltip("Prefab that has EnvironmentClearingTask + BoxCollider on it.")]
    public EnvironmentClearingTask clearingTaskPrefab;

    [Tooltip("Tiny base tile prefab used when EnvironmentClearingTask.spawnBaseEnvironmentTilesFirst is true.")]
    public GameObject baseTilePrefab;

    [Tooltip("Cleared tile prefab that replaces environment tiles.")]
    public GameObject clearedTilePrefab;

    [Tooltip("Grid manager used for placing tiny base tiles.")]
    public GridManager gridManager;

    [Tooltip("If true, clearing tasks will first fill the area with tiny base tiles.")]
    public bool spawnBaseEnvironmentTilesFirstByDefault = false;

    [Header("Performance")]
    [Tooltip("How many clearing tasks to advance per frame at end of turn.")]
    [Min(1)] public int maxClearingsPerFrame = 8;

    // Track active clearing per environment to prevent duplicates
    private readonly Dictionary<EnvironmentControl, EnvironmentClearingTask> _activeByEnv = new();
    // Flat list for batching
    private readonly List<EnvironmentClearingTask> _activeTasks = new();

    private Coroutine _processingCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (gridManager == null)
            gridManager = FindObjectOfType<GridManager>();
    }

    private void OnEnable()
    {
        TurnSystem.SubscribeToEndOfTurn(OnTurnEnded);
    }

    private void OnDisable()
    {
        TurnSystem.UnsubscribeFromEndOfTurn(OnTurnEnded);
    }

    public bool StartClearing(EnvironmentControl env)
    {
        if (env == null)
        {
            Debug.LogWarning("[PlayerEnvironmentClearingManager] StartClearing called with null EnvironmentControl.");
            return false;
        }

        if (!env.IsDiscovered)
        {
            Debug.LogWarning("[PlayerEnvironmentClearingManager] Cannot clear undiscovered environment.");
            return false;
        }

        // Already clearing this environment?
        if (_activeByEnv.ContainsKey(env))
        {
            Debug.Log("[PlayerEnvironmentClearingManager] Environment is already being cleared.");
            return false;
        }

        if (clearingTaskPrefab == null)
        {
            Debug.LogError("[PlayerEnvironmentClearingManager] clearingTaskPrefab is not assigned.");
            return false;
        }

        // Spawn the clearing task at the tile's position
        Vector3 pos = env.transform.position;
        Quaternion rot = env.transform.rotation;

        var task = Instantiate(clearingTaskPrefab, pos, rot);

        // Wire up what the prefab needs; it will:
        // - compute turns/pop if 0
        // - reserve population
        // - call RegisterTask(this) once it's actually running
        task.environmentControl             = env;
        task.gridManager                    = gridManager;
        task.baseTilePrefab                 = baseTilePrefab;
        task.clearedTilePrefab              = clearedTilePrefab;
        task.spawnBaseEnvironmentTilesFirst = spawnBaseEnvironmentTilesFirstByDefault;
        task.forcedEnvironmentType          = env.environmentType;

        // Do NOT touch clearingTimerUI – it’s already on the prefab.

        // We DON'T add to _active lists here; that only happens in RegisterTask
        // after population is successfully reserved, so failed tasks don't
        // leave stale entries.

        return true;
    }


    public void RegisterTask(EnvironmentClearingTask task)
    {
        if (task == null) return;
        if (_activeTasks.Contains(task)) return;

        _activeTasks.Add(task);

        var env = task.environmentControl;
        if (env != null && !_activeByEnv.ContainsKey(env))
            _activeByEnv[env] = task;

    }

    public void NotifyTaskCompleted(EnvironmentClearingTask task)
    {
        if (task == null) return;

        _activeTasks.Remove(task);

        var env = task.environmentControl;
        if (env != null && _activeByEnv.TryGetValue(env, out var current) && current == task)
        {
            _activeByEnv.Remove(env);
            // if (env != null) env.isClearing = false;
        }
    }

    // ------------------------------------------------------
    // Turn batching
    // ------------------------------------------------------

    private void OnTurnEnded()
    {
        if (_activeTasks.Count == 0) return;

        var snapshot = new List<EnvironmentClearingTask>(_activeTasks);
        if (_processingCoroutine != null)
            StopCoroutine(_processingCoroutine);

        _processingCoroutine = StartCoroutine(ProcessClearingUpdates(snapshot));
    }

    private IEnumerator ProcessClearingUpdates(List<EnvironmentClearingTask> pending)
    {
        int idx = 0;

        while (idx < pending.Count)
        {
            int end = Mathf.Min(idx + maxClearingsPerFrame, pending.Count);

            for (int i = idx; i < end; i++)
            {
                var task = pending[i];
                if (task == null) continue;

                // Each active task advances ONE turn per game-turn,
                // but we spread the work across frames.
                task.AdvanceOneTurn();
            }

            idx = end;
            yield return null; // spread workload over multiple frames
        }

        _processingCoroutine = null;
    }
}
