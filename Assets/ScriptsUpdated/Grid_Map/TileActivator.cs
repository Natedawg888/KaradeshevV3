using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public class TileActivator : MonoBehaviour
{
    [Header("References")]
    public MapGenerator mapGenerator;
    public GridManager gridManager;

    [Header("Wave Settings")]
    public int numberOfWaveStartPoints = 3;
    public float activationDelay = 0.05f;

    [Header("Startup Control")]
    [SerializeField] private bool autoStartActivation = false;

    [Header("UI (optional)")]
    public GameObject loadingScreen;
    public TimerUI timerUI;

    private List<TileScript> allTileScripts = new List<TileScript>();
    private int totalTiles;
    private int activatedCount;

    private Coroutine _activationCoroutine;
    private bool _isRunning;

    private TimerUI _runtimeTimerOverride;
    private bool _manageLoadingScreenInternally = true;
    private bool _ensureCaveOnComplete = true;

    public bool IsRunning => _isRunning;

    public event Action OnTilesActivated;

    private void Start()
    {
        if (autoStartActivation)
            BeginActivation();
    }

    public void BeginActivation()
    {
        BeginActivation(null, true, true);
    }

    public void BeginActivation(TimerUI timerOverride, bool manageLoadingScreenInternally)
    {
        BeginActivation(timerOverride, manageLoadingScreenInternally, true);
    }

    public void DisableCaveGuarantee() => _ensureCaveOnComplete = false;

    public void BeginActivation(TimerUI timerOverride, bool manageLoadingScreenInternally, bool ensureCaveOnComplete)
    {
        _runtimeTimerOverride = timerOverride;
        _manageLoadingScreenInternally = manageLoadingScreenInternally;
        _ensureCaveOnComplete = ensureCaveOnComplete;

        if (_activationCoroutine != null)
            StopCoroutine(_activationCoroutine);

        _activationCoroutine = StartCoroutine(DelayedActivate());
    }

    private IEnumerator DelayedActivate()
    {
        _isRunning = true;

        yield return new WaitUntil(() => MapTilePlacer.WorldReady);

        BuildTileList();
        InitializeUI();
        yield return StartCoroutine(ActivateTilesWave());

        _isRunning = false;
        _activationCoroutine = null;
        _runtimeTimerOverride = null;
        _manageLoadingScreenInternally = true;
        _ensureCaveOnComplete = true;
    }

    private void BuildTileList()
    {
        allTileScripts = FindObjectsOfType<TileScript>()
            .Where(ts => ts != null)
            .ToList();

        totalTiles = allTileScripts.Count;
    }

    private void InitializeUI()
    {
        activatedCount = 0;

        for (int i = 0; i < allTileScripts.Count; i++)
        {
            TileScript ts = allTileScripts[i];
            if (ts != null && ts.HasSpawned)
                activatedCount++;
        }

        if (_manageLoadingScreenInternally && loadingScreen != null)
            loadingScreen.SetActive(true);

        TimerUI activeTimer = GetActiveTimerUI();
        if (activeTimer != null)
        {
            activeTimer.gameObject.SetActive(true);
            activeTimer.SetState(
                Mathf.Max(1, totalTiles),
                Mathf.Max(0, totalTiles - activatedCount)
            );
        }
    }

    private IEnumerator ActivateTilesWave()
    {
        var candidates = new List<TileScript>();

        for (int i = 0; i < allTileScripts.Count; i++)
        {
            TileScript ts = allTileScripts[i];
            if (ts != null && !ts.HasSpawned)
                candidates.Add(ts);
        }

        if (candidates.Count == 0)
        {
            if (_ensureCaveOnComplete)
                EnsureAtLeastOneCaveOnFirstMap();

            yield return null;

            if (_manageLoadingScreenInternally && loadingScreen != null)
                loadingScreen.SetActive(false);

            OnTilesActivated?.Invoke();
            yield break;
        }

        int n = Mathf.Min(numberOfWaveStartPoints, candidates.Count);
        var starts = new List<TileScript>();

        while (starts.Count < n)
        {
            var pick = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            if (!starts.Contains(pick))
                starts.Add(pick);
        }

        foreach (var ts in starts)
        {
            StartCoroutine(ActivateWaveFrom(ts));
            yield return new WaitForSeconds(activationDelay);
        }

        while (activatedCount < totalTiles)
            yield return null;

        if (_ensureCaveOnComplete)
            EnsureAtLeastOneCaveOnFirstMap();

        yield return null;

        if (_manageLoadingScreenInternally && loadingScreen != null)
            loadingScreen.SetActive(false);

        OnTilesActivated?.Invoke();
    }

    private IEnumerator ActivateWaveFrom(TileScript origin)
    {
        var queue = new Queue<TileScript>();
        var visited = new HashSet<TileScript>();

        if (origin != null)
            queue.Enqueue(origin);

        while (queue.Count > 0)
        {
            TileScript ts = queue.Dequeue();
            if (ts == null || visited.Contains(ts))
                continue;

            visited.Add(ts);

            if (!ts.HasSpawned)
            {
                ts.SpawnEnvironmentTile();
                activatedCount++;

                TimerUI activeTimer = GetActiveTimerUI();
                if (activeTimer != null)
                    activeTimer.UpdateTimer(Mathf.Max(0, totalTiles - activatedCount));

                yield return new WaitForSeconds(activationDelay);
            }

            Collider col = ts.GetComponent<Collider>();
            if (col != null)
            {
                Vector3 ext = col.bounds.extents * 1.2f;
                Collider[] hits = Physics.OverlapBox(
                    col.bounds.center,
                    ext,
                    ts.transform.rotation,
                    ~0,
                    QueryTriggerInteraction.Collide
                );

                foreach (Collider h in hits)
                {
                    if (h == null) continue;

                    TileScript neigh = h.GetComponentInParent<TileScript>();
                    if (neigh != null && neigh != ts && !visited.Contains(neigh))
                        queue.Enqueue(neigh);
                }
            }
        }
    }

    private TimerUI GetActiveTimerUI()
    {
        return _runtimeTimerOverride != null ? _runtimeTimerOverride : timerUI;
    }

    private void EnsureAtLeastOneCaveOnFirstMap()
    {
        bool hasCave = allTileScripts.Any(ts =>
            ts != null &&
            ts.HasSpawned &&
            ts.GetChosenTileType() == EnvironmentTileType.Cave);

        if (hasCave)
            return;

        var caveCapable = allTileScripts
            .Where(ts => ts != null &&
                         ts.options != null &&
                         ts.options.Any(o => o != null && o.tileType == EnvironmentTileType.Cave))
            .OrderBy(_ => UnityEngine.Random.value)
            .ToList();

        if (caveCapable.Count == 0)
        {
            //Debug.LogWarning("[TileActivator] No tiles have a Cave option—cannot guarantee a Cave on first map.");
            return;
        }

        foreach (TileScript chosen in caveCapable)
        {
            if (chosen == null)
                continue;

            if (chosen.ForceSpawnSpecificTileTypeFiltered(EnvironmentTileType.Cave, markDiscovered: true))
                return;
        }

        //Debug.LogWarning("[TileActivator] Could not place a Cave that satisfies filters. Falling back to unfiltered force.");

        TileScript fallback = caveCapable[UnityEngine.Random.Range(0, caveCapable.Count)];
        bool ok = fallback.ForceSpawnSpecificTileType(EnvironmentTileType.Cave);

        if (!ok) {}
            //Debug.LogWarning("[TileActivator] Fallback cave force failed.");
    }
}
