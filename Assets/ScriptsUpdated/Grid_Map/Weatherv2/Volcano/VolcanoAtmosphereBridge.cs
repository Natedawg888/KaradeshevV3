using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VolcanoAtmosphereBridge : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private VolcanoManager volcanoManager;
    [SerializeField] private CloudSimulationSystem cloudSimulationSystem;

    [Tooltip("Preferred source for volcano-owned weather cells.")]
    [SerializeField] private WeatherGridManager weatherGridManager;

    [Tooltip("Fallback only if WeatherGridManager ownership lookup misses.")]
    [SerializeField] private MonoEnvironmentDataSource environmentDataSource;

    [Header("Ownership Lookup")]
    public bool preferWeatherGridOwnership = true;
    public bool includePrimaryCellAlways = true;

    [Header("Over-Frame Processing")]
    public bool processEmissionsOverFrames = true;

    [Tooltip("How many soot stamp jobs are submitted to CloudSimulationSystem per frame.")]
    [Min(1)] public int sootStampJobsPerFrame = 8;

    [Header("Debug")]
    public bool debugLogging = false;

    private readonly List<TileCoord> footprintScratch = new List<TileCoord>(16);
    private readonly Queue<SootStampEmission> pendingStampEmissions = new Queue<SootStampEmission>(64);

    private Coroutine flushRoutine;
    private VolcanoManager subscribedManager;

    private struct SootStampEmission
    {
        public VolcanoTileState volcano;
        public float amount;
        public int radius;
        public float maxAddPerCell;
        public float falloffPerCell;
        public List<TileCoord> originCells;
    }

    private void Awake()
    {
        EnsureLinks();
    }

    private void OnEnable()
    {
        EnsureLinks();
        RebindManagerEvents();
    }

    private void Start()
    {
        EnsureLinks();
        RebindManagerEvents();
    }

    private void OnDisable()
    {
        UnbindManagerEvents();

        if (flushRoutine != null)
        {
            StopCoroutine(flushRoutine);
            flushRoutine = null;
        }

        pendingStampEmissions.Clear();
    }

    private void EnsureLinks()
    {
        if (volcanoManager == null)
            volcanoManager = VolcanoManager.Instance;

        if (cloudSimulationSystem == null)
            cloudSimulationSystem = CloudSimulationSystem.Instance;

        if (weatherGridManager == null)
            weatherGridManager = WeatherGridManager.Instance;

        if (environmentDataSource == null)
            environmentDataSource = MonoEnvironmentDataSource.Instance;
    }

    private void RebindManagerEvents()
    {
        if (subscribedManager == volcanoManager)
            return;

        UnbindManagerEvents();

        subscribedManager = volcanoManager;

        if (subscribedManager == null)
            return;

        subscribedManager.OnVolcanoTurnAdvanceStarted += HandleVolcanoTurnStarted;
        subscribedManager.OnEruptingVolcanoAdvanced += HandleEruptingVolcanoAdvanced;
        subscribedManager.OnVolcanoTurnAdvanceFinished += HandleVolcanoTurnFinished;
    }

    private void UnbindManagerEvents()
    {
        if (subscribedManager == null)
            return;

        subscribedManager.OnVolcanoTurnAdvanceStarted -= HandleVolcanoTurnStarted;
        subscribedManager.OnEruptingVolcanoAdvanced -= HandleEruptingVolcanoAdvanced;
        subscribedManager.OnVolcanoTurnAdvanceFinished -= HandleVolcanoTurnFinished;

        subscribedManager = null;
    }

    private void HandleVolcanoTurnStarted()
    {
        pendingStampEmissions.Clear();
    }

    private void HandleEruptingVolcanoAdvanced(VolcanoTileState volcano)
    {
        if (volcano == null)
            return;

        EnsureLinks();

        float amount = volcano.GetSootEmissionThisTurn();
        if (amount <= 0f)
            return;

        if (!volcano.TryGetPrimaryCell(out TileCoord primaryCell))
        {
            if (debugLogging)
                //Debug.LogWarning($"[VolcanoAtmosphereBridge] Could not resolve primary cell for {volcano.name}");

            return;
        }

        footprintScratch.Clear();
        ResolveVolcanoOwnedWeatherCells(volcano, primaryCell, footprintScratch);

        if (footprintScratch.Count == 0)
            footprintScratch.Add(primaryCell);

        List<TileCoord> ownedCellsCopy = new List<TileCoord>(footprintScratch.Count);

        for (int i = 0; i < footprintScratch.Count; i++)
            ownedCellsCopy.Add(footprintScratch[i]);

        pendingStampEmissions.Enqueue(new SootStampEmission
        {
            volcano = volcano,
            amount = amount,
            radius = volcano.GetSootStampRadius(),
            maxAddPerCell = volcano.GetMaxSootAddedPerCellThisTurn(),
            falloffPerCell = volcano.GetSootStampFalloffPerCell(),
            originCells = ownedCellsCopy
        });

        if (debugLogging)
        {
            string cells = "";
            for (int i = 0; i < ownedCellsCopy.Count; i++)
                cells += $"({ownedCellsCopy[i].x},{ownedCellsCopy[i].y}) ";

            //Debug.Log(
                //$"[VolcanoAtmosphereBridge] Queued soot stamp volcano={volcano.name} " +
                //$"amount={amount:0.00} radius={volcano.GetSootStampRadius()} " +
                //$"ownedCells={ownedCellsCopy.Count} cells={cells}");
        }
    }

    private void HandleVolcanoTurnFinished()
    {
        StartFlushIfNeeded();
    }

    private void ResolveVolcanoOwnedWeatherCells(
        VolcanoTileState volcano,
        TileCoord primaryCell,
        List<TileCoord> results)
    {
        results.Clear();

        if (preferWeatherGridOwnership && weatherGridManager != null)
        {
            EnvironmentControl env = ResolveVolcanoEnvironment(volcano, primaryCell);

            if (env != null &&
                weatherGridManager.TryGetEnvironmentCoveredCells(env, results) &&
                results.Count > 0)
            {
                if (includePrimaryCellAlways)
                    AddUniqueCoord(results, primaryCell);

                return;
            }

            if (weatherGridManager.TryGetEnvironmentCoveredCells(primaryCell, results) &&
                results.Count > 0)
            {
                if (includePrimaryCellAlways)
                    AddUniqueCoord(results, primaryCell);

                return;
            }
        }

        if (environmentDataSource != null &&
            environmentDataSource.TryGetFootprintCoords(primaryCell, results) &&
            results.Count > 0)
        {
            if (includePrimaryCellAlways)
                AddUniqueCoord(results, primaryCell);

            return;
        }

        if (includePrimaryCellAlways)
            AddUniqueCoord(results, primaryCell);
    }

    private EnvironmentControl ResolveVolcanoEnvironment(VolcanoTileState volcano, TileCoord primaryCell)
    {
        if (volcano == null)
            return null;

        EnvironmentControl env = volcano.GetComponent<EnvironmentControl>();

        if (env == null)
            env = volcano.GetComponentInParent<EnvironmentControl>(true);

        if (env == null)
            env = volcano.GetComponentInChildren<EnvironmentControl>(true);

        if (env != null)
            return env;

        if (weatherGridManager != null &&
            weatherGridManager.TryGetEnvironmentAtCell(primaryCell.x, primaryCell.y, out env))
        {
            return env;
        }

        return null;
    }

    private void AddUniqueCoord(List<TileCoord> list, TileCoord coord)
    {
        if (list == null)
            return;

        for (int i = 0; i < list.Count; i++)
        {
            TileCoord existing = list[i];

            if (existing.x == coord.x && existing.y == coord.y)
                return;
        }

        list.Add(coord);
    }

    private void StartFlushIfNeeded()
    {
        if (flushRoutine != null)
            return;

        if (processEmissionsOverFrames)
            flushRoutine = StartCoroutine(FlushEmissionsRoutine());
        else
            FlushEmissionsImmediate();
    }

    private IEnumerator FlushEmissionsRoutine()
    {
        while (pendingStampEmissions.Count > 0)
        {
            EnsureLinks();

            int processed = 0;
            int maxPerFrame = Mathf.Max(1, sootStampJobsPerFrame);

            while (pendingStampEmissions.Count > 0 && processed < maxPerFrame)
            {
                SootStampEmission emission = pendingStampEmissions.Dequeue();
                SubmitStampEmission(emission);
                processed++;
            }

            if (pendingStampEmissions.Count > 0)
                yield return null;
        }

        flushRoutine = null;
    }

    private void FlushEmissionsImmediate()
    {
        EnsureLinks();

        while (pendingStampEmissions.Count > 0)
            SubmitStampEmission(pendingStampEmissions.Dequeue());
    }

    private void SubmitStampEmission(SootStampEmission emission)
    {
        if (cloudSimulationSystem == null)
            return;

        if (emission.amount <= 0f)
            return;

        if (emission.originCells == null || emission.originCells.Count == 0)
            return;

        cloudSimulationSystem.AddVolcanicSootStamp(
            emission.originCells,
            emission.amount,
            emission.radius,
            emission.maxAddPerCell,
            emission.falloffPerCell);

        if (debugLogging)
        {
            string volcanoName = emission.volcano != null ? emission.volcano.name : "null";

            //Debug.Log(
                //$"[VolcanoAtmosphereBridge] Submitted soot stamp volcano={volcanoName} " +
                //$"amount={emission.amount:0.00} radius={emission.radius} " +
                //$"maxAddPerCell={emission.maxAddPerCell:0.00} " +
                //$"falloff={emission.falloffPerCell:0.00} " +
                //$"originCells={emission.originCells.Count}");
        }
    }
}
