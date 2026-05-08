using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TsunamiOverlayManager : MonoBehaviour
{
    public static TsunamiOverlayManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private TsunamiSimulationSystem tsunamiSimulationSystem;

    [Header("Overlay Root")]
    [SerializeField] private Transform tsunamiOverlayRoot;

    [Tooltip("Y height for tsunami visuals. Set this above environment tiles.")]
    [SerializeField] private float tsunamiOverlayHeight = 0.24f;

    [SerializeField] private Vector3 prefabScale = Vector3.one;

    [Header("Prefab")]
    [Tooltip("One wave overlay prefab placed on each active tsunami cell.")]
    [SerializeField] private GameObject wavePrefab;

    [Header("Rotation")]
    [Tooltip("Extra Y rotation offset if your wave prefab faces the wrong default direction. Example: 90, -90, or 180.")]
    [SerializeField] private float prefabRotationYOffset = 0f;

    [Header("Visual Refresh")]
    public bool processVisualRefreshOverFrames = true;

    [Min(1)]
    public int overlaysUpdatedPerFrame = 24;

    [Header("Pool")]
    public bool prewarmOnStart = true;

    [Min(0)]
    public int prewarmCount = 64;

    [Header("Energy Height Scaling")]
    [Tooltip("If true, wave prefab Y scale grows/shrinks based on tsunami energy.")]
    [SerializeField] private bool scaleWaveYByEnergy = true;

    [Tooltip("Y scale multiplier when tsunami energy is almost gone.")]
    [SerializeField, Min(0.01f)] private float minEnergyYScaleMultiplier = 0.25f;

    [Tooltip("Y scale multiplier when tsunami is at full starting energy.")]
    [SerializeField, Min(0.01f)] private float maxEnergyYScaleMultiplier = 2.5f;

    [Tooltip("Optional curve for energy-to-height. X = energy01, Y = height multiplier blend.")]
    [SerializeField] private AnimationCurve energyToHeightCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Debug")]
    public bool debugLogging = false;

    private TsunamiOverlayPool pool;

    private readonly Dictionary<TileCoord, int> activeCellRefCounts =
        new Dictionary<TileCoord, int>();

    private readonly Dictionary<TileCoord, GameObject> activeVisuals =
        new Dictionary<TileCoord, GameObject>();

    private readonly Dictionary<TileCoord, float> activeCellRotationY =
        new Dictionary<TileCoord, float>();

    private readonly Dictionary<int, float> tsunamiRotationById =
        new Dictionary<int, float>();

    private readonly Dictionary<TileCoord, float> activeCellEnergy01 =
    new Dictionary<TileCoord, float>();

    private readonly Dictionary<int, float> tsunamiEnergy01ById =
        new Dictionary<int, float>();

    private readonly Queue<TileCoord> pendingVisualRefreshes = new Queue<TileCoord>();
    private readonly HashSet<TileCoord> pendingVisualRefreshSet = new HashSet<TileCoord>();

    private Coroutine visualRefreshRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        EnsureLinks();
        EnsureRootAndPool();
    }

    private void OnEnable()
    {
        EnsureLinks();

        if (tsunamiSimulationSystem != null)
        {
            tsunamiSimulationSystem.OnTsunamiStarted += HandleTsunamiStarted;
            tsunamiSimulationSystem.OnTsunamiAdvanced += HandleTsunamiAdvanced;
            tsunamiSimulationSystem.OnTsunamiCellsChanged += HandleTsunamiCellsChanged;
            tsunamiSimulationSystem.OnTsunamiEnded += HandleTsunamiEnded;
        }
    }

    private void OnDisable()
    {
        if (tsunamiSimulationSystem != null)
        {
            tsunamiSimulationSystem.OnTsunamiStarted -= HandleTsunamiStarted;
            tsunamiSimulationSystem.OnTsunamiAdvanced -= HandleTsunamiAdvanced;
            tsunamiSimulationSystem.OnTsunamiCellsChanged -= HandleTsunamiCellsChanged;
            tsunamiSimulationSystem.OnTsunamiEnded -= HandleTsunamiEnded;
        }

        if (visualRefreshRoutine != null)
        {
            StopCoroutine(visualRefreshRoutine);
            visualRefreshRoutine = null;
        }

        pendingVisualRefreshes.Clear();
        pendingVisualRefreshSet.Clear();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        EnsureLinks();
        EnsureRootAndPool();

        if (prewarmOnStart)
            Prewarm();
    }

    private void EnsureLinks()
    {
        if (gridManager == null)
            gridManager = GridManager.Instance;

        if (tsunamiSimulationSystem == null)
            tsunamiSimulationSystem = TsunamiSimulationSystem.Instance;

        if (tsunamiSimulationSystem == null)
            tsunamiSimulationSystem = FindObjectOfType<TsunamiSimulationSystem>();
    }

    private void EnsureRootAndPool()
    {
        if (tsunamiOverlayRoot == null)
        {
            GameObject root = new GameObject("Tsunami Overlay Root");
            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            tsunamiOverlayRoot = root.transform;
        }

        if (pool == null)
            pool = new TsunamiOverlayPool(tsunamiOverlayRoot);
    }

    private void Prewarm()
    {
        EnsureRootAndPool();
        pool.Prewarm(wavePrefab, prewarmCount);
    }

    private void HandleTsunamiStarted(TsunamiStartedEventData data)
    {
        if (data == null)
            return;

        float rotationY = DirectionToRotationY(data.direction);
        tsunamiRotationById[data.tsunamiId] = rotationY;

        tsunamiEnergy01ById[data.tsunamiId] = 1f;
    }

    private void HandleTsunamiAdvanced(TsunamiAdvancedEventData data)
    {
        if (data == null)
            return;

        float rotationY = DirectionToRotationY(data.direction);
        tsunamiRotationById[data.tsunamiId] = rotationY;

        tsunamiEnergy01ById[data.tsunamiId] = Mathf.Clamp01(data.energy01);

        if (data.activeCells != null)
        {
            for (int i = 0; i < data.activeCells.Count; i++)
            {
                TileCoord coord = data.activeCells[i];
                activeCellEnergy01[coord] = Mathf.Clamp01(data.energy01);
                QueueVisualRefresh(coord);
            }
        }
    }

    private void HandleTsunamiEnded(TsunamiEndedEventData data)
    {
        if (data == null)
            return;

        tsunamiRotationById.Remove(data.tsunamiId);
        tsunamiEnergy01ById.Remove(data.tsunamiId);
    }

    private void HandleTsunamiCellsChanged(TsunamiCellsChangedEventData data)
    {
        if (data == null)
            return;

        float rotationY = 0f;

        if (tsunamiRotationById.TryGetValue(data.tsunamiId, out float foundRotation))
            rotationY = foundRotation;

        float energy01 = Mathf.Clamp01(data.energy01);

        if (tsunamiEnergy01ById.TryGetValue(data.tsunamiId, out float foundEnergy01))
            energy01 = Mathf.Clamp01(foundEnergy01);

        if (data.removedCells != null)
        {
            for (int i = 0; i < data.removedCells.Count; i++)
            {
                TileCoord coord = data.removedCells[i];

                DecrementActiveCell(coord);

                if (!activeCellRefCounts.ContainsKey(coord))
                {
                    activeCellRotationY.Remove(coord);
                    activeCellEnergy01.Remove(coord);
                }

                QueueVisualRefresh(coord);
            }
        }

        if (data.addedCells != null)
        {
            for (int i = 0; i < data.addedCells.Count; i++)
            {
                TileCoord coord = data.addedCells[i];

                IncrementActiveCell(coord);
                activeCellRotationY[coord] = rotationY;
                activeCellEnergy01[coord] = energy01;

                QueueVisualRefresh(coord);
            }
        }
    }

    public bool HasTsunamiOverlayAt(TileCoord coord)
    {
        return activeCellRefCounts.ContainsKey(coord);
    }

    public void ClearAllOverlays()
    {
        foreach (KeyValuePair<TileCoord, GameObject> pair in activeVisuals)
        {
            GameObject visual = pair.Value;

            if (visual != null && pool != null && wavePrefab != null)
                pool.Return(wavePrefab, visual);
        }

        activeVisuals.Clear();
        activeCellRefCounts.Clear();
        activeCellRotationY.Clear();

        tsunamiRotationById.Clear();
        tsunamiEnergy01ById.Clear();

        pendingVisualRefreshes.Clear();
        pendingVisualRefreshSet.Clear();

        if (debugLogging) {}
            //Debug.Log("[TsunamiOverlayManager] Cleared all overlays.");
    }

    private void IncrementActiveCell(TileCoord coord)
    {
        if (activeCellRefCounts.TryGetValue(coord, out int count))
            activeCellRefCounts[coord] = count + 1;
        else
            activeCellRefCounts.Add(coord, 1);
    }

    private void DecrementActiveCell(TileCoord coord)
    {
        if (!activeCellRefCounts.TryGetValue(coord, out int count))
            return;

        count--;

        if (count <= 0)
            activeCellRefCounts.Remove(coord);
        else
            activeCellRefCounts[coord] = count;
    }

    private void QueueVisualRefresh(TileCoord coord)
    {
        if (IsOutsideGrid(coord))
            return;

        if (!pendingVisualRefreshSet.Add(coord))
            return;

        pendingVisualRefreshes.Enqueue(coord);

        if (!processVisualRefreshOverFrames)
        {
            ProcessVisualRefreshImmediate();
            return;
        }

        if (visualRefreshRoutine == null && isActiveAndEnabled)
            visualRefreshRoutine = StartCoroutine(VisualRefreshRoutine());
    }

    private IEnumerator VisualRefreshRoutine()
    {
        while (pendingVisualRefreshes.Count > 0)
        {
            int processed = 0;
            int max = Mathf.Max(1, overlaysUpdatedPerFrame);

            while (pendingVisualRefreshes.Count > 0 && processed < max)
            {
                TileCoord coord = pendingVisualRefreshes.Dequeue();
                pendingVisualRefreshSet.Remove(coord);

                RefreshVisualAtCell(coord);
                processed++;
            }

            if (pendingVisualRefreshes.Count > 0)
                yield return null;
        }

        visualRefreshRoutine = null;
    }

    private void ProcessVisualRefreshImmediate()
    {
        while (pendingVisualRefreshes.Count > 0)
        {
            TileCoord coord = pendingVisualRefreshes.Dequeue();
            pendingVisualRefreshSet.Remove(coord);

            RefreshVisualAtCell(coord);
        }
    }

    private void RefreshVisualAtCell(TileCoord coord)
    {
        bool shouldHaveVisual = activeCellRefCounts.ContainsKey(coord);

        if (!shouldHaveVisual)
        {
            RemoveVisual(coord);
            return;
        }

        if (wavePrefab == null)
        {
            RemoveVisual(coord);

            if (debugLogging) {}
                //Debug.LogWarning("[TsunamiOverlayManager] Missing wavePrefab.");

            return;
        }

        float rotationY = 0f;

        if (activeCellRotationY.TryGetValue(coord, out float foundRotation))
            rotationY = foundRotation;

        rotationY += prefabRotationYOffset;

        Vector3 position = GetCellCenterWorld(coord) + Vector3.up * tsunamiOverlayHeight;
        Quaternion rotation = Quaternion.Euler(0f, rotationY, 0f);

        if (activeVisuals.TryGetValue(coord, out GameObject existing) && existing != null)
        {
            existing.transform.SetPositionAndRotation(position, rotation);
            existing.transform.localScale = GetEnergyScaledPrefabScale(coord);
            return;
        }

        GameObject visual = pool.Get(wavePrefab, position, rotation);

        if (visual == null)
            return;

        visual.transform.localScale = GetEnergyScaledPrefabScale(coord);
        activeVisuals[coord] = visual;
    }

    private Vector3 GetEnergyScaledPrefabScale(TileCoord coord)
    {
        Vector3 scale = prefabScale;

        if (!scaleWaveYByEnergy)
            return scale;

        float energy01 = 1f;

        if (activeCellEnergy01.TryGetValue(coord, out float foundEnergy01))
            energy01 = Mathf.Clamp01(foundEnergy01);

        float t = energy01;

        if (energyToHeightCurve != null)
            t = Mathf.Clamp01(energyToHeightCurve.Evaluate(energy01));

        float yMultiplier = Mathf.Lerp(
            minEnergyYScaleMultiplier,
            maxEnergyYScaleMultiplier,
            t);

        scale.y *= yMultiplier;

        return scale;
    }

    private void RemoveVisual(TileCoord coord)
    {
        if (!activeVisuals.TryGetValue(coord, out GameObject visual))
            return;

        if (visual != null && wavePrefab != null && pool != null)
            pool.Return(wavePrefab, visual);

        activeVisuals.Remove(coord);
    }

    private float DirectionToRotationY(Vector2Int direction)
    {
        if (direction == Vector2Int.zero)
            return 0f;

        Vector3 worldDirection = new Vector3(direction.x, 0f, direction.y);

        return Quaternion.LookRotation(worldDirection, Vector3.up).eulerAngles.y;
    }

    private bool IsOutsideGrid(TileCoord coord)
    {
        if (gridManager == null)
            gridManager = GridManager.Instance;

        if (gridManager == null)
            return false;

        return coord.x < 0 ||
               coord.y < 0 ||
               coord.x >= gridManager.columns ||
               coord.y >= gridManager.rows;
    }

    private Vector3 GetCellCenterWorld(TileCoord coord)
    {
        if (gridManager == null)
            gridManager = GridManager.Instance;

        if (gridManager == null)
            return new Vector3(coord.x, 0f, coord.y);

        Vector3 corner = gridManager.GetWorldPosition(coord.x, coord.y);

        return corner + new Vector3(
            gridManager.cellSize * 0.5f,
            0f,
            gridManager.cellSize * 0.5f);
    }

    public void RebuildAllOverlaysFromSimulation()
    {
        EnsureLinks();
        EnsureRootAndPool();

        ClearAllOverlays();

        if (tsunamiSimulationSystem == null)
            return;

        List<TsunamiVisualSnapshot> snapshots = new List<TsunamiVisualSnapshot>();

        if (!tsunamiSimulationSystem.CopyActiveTsunamiVisualSnapshots(snapshots))
            return;

        for (int i = 0; i < snapshots.Count; i++)
        {
            TsunamiVisualSnapshot snapshot = snapshots[i];

            if (snapshot == null)
                continue;

            float rotationY = DirectionToRotationY(snapshot.direction);
            float energy01 = Mathf.Clamp01(snapshot.energy01);

            tsunamiRotationById[snapshot.tsunamiId] = rotationY;
            tsunamiEnergy01ById[snapshot.tsunamiId] = energy01;

            if (snapshot.activeCells == null)
                continue;

            for (int c = 0; c < snapshot.activeCells.Count; c++)
            {
                TileCoord coord = snapshot.activeCells[c];

                IncrementActiveCell(coord);
                activeCellRotationY[coord] = rotationY;
                activeCellEnergy01[coord] = energy01;

                QueueVisualRefresh(coord);
            }
        }

        if (debugLogging) {}
            //Debug.Log($"[TsunamiOverlayManager] Rebuilt tsunami overlays from simulation. Waves={snapshots.Count}");
    }
}
