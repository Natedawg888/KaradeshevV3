using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VolcanoManager : MonoBehaviour
{
    public static VolcanoManager Instance { get; private set; }

    [Header("Lifecycle")]
    public bool findVolcanoesOnStart = true;
    public bool advanceWithTurnSystem = true;

    [Header("Over-Frame Processing")]
    public bool processVolcanoTurnsOverFrames = true;

    [Tooltip("How many registered volcano states to process per frame when batching.")]
    [Min(1)] public int volcanoesProcessedPerFrame = 8;

    [Header("Per-Turn Limits")]
    [Tooltip("0 = unlimited.")]
    [Min(0)] public int maxMountainAwakeningsPerTurn = 1;

    [Tooltip("0 = unlimited.")]
    [Min(0)] public int maxNewEruptionsPerTurn = 1;

    [Header("Debug")]
    public bool debugLogging = false;

    private readonly HashSet<VolcanoTileState> registeredVolcanoes = new HashSet<VolcanoTileState>();
    private readonly List<VolcanoTileState> advanceBuffer = new List<VolcanoTileState>(128);
    private readonly List<VolcanoTileState> eruptingVolcanoes = new List<VolcanoTileState>(32);

    private Coroutine advanceRoutine;
    private int queuedAdvanceTurns;
    private int mountainAwakeningsUsedThisTurn;
    private int newEruptionsUsedThisTurn;

    public IReadOnlyCollection<VolcanoTileState> RegisteredVolcanoes => registeredVolcanoes;
    public IReadOnlyList<VolcanoTileState> EruptingVolcanoes => eruptingVolcanoes;

    public event Action OnVolcanoTurnAdvanceStarted;
    public event Action OnVolcanoTurnAdvanceFinished;

    public event Action<VolcanoTileState> OnVolcanoCreated;
    public event Action<VolcanoTileState> OnVolcanoBecameDormant;
    public event Action<VolcanoTileState> OnEruptionStarted;
    public event Action<VolcanoTileState> OnEruptingVolcanoAdvanced;
    public event Action<VolcanoTileState> OnEruptionEnded;
    public event Action<VolcanoTileState> OnVolcanoRevertedToMountain;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnEnable()
    {
        if (advanceWithTurnSystem)
            TurnSystem.SubscribeToEndOfTurn(HandleEndOfTurn);
    }

    private void Start()
    {
        if (findVolcanoesOnStart)
            RegisterAllSceneVolcanoes();
    }

    private void OnDisable()
    {
        TurnSystem.UnsubscribeFromEndOfTurn(HandleEndOfTurn);

        if (advanceRoutine != null)
        {
            StopCoroutine(advanceRoutine);
            advanceRoutine = null;
        }

        queuedAdvanceTurns = 0;
        mountainAwakeningsUsedThisTurn = 0;
        newEruptionsUsedThisTurn = 0;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void RegisterAllSceneVolcanoes()
    {
#if UNITY_2023_1_OR_NEWER
        VolcanoTileState[] found = FindObjectsByType<VolcanoTileState>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
#else
        VolcanoTileState[] found = FindObjectsOfType<VolcanoTileState>(true);
#endif

        int added = 0;

        for (int i = 0; i < found.Length; i++)
        {
            if (RegisterVolcano(found[i]))
                added++;
        }

        RefreshEruptingList();

        if (debugLogging)
            //Debug.Log($"[VolcanoManager] Registered scene volcano states. Found={found.Length} Added={added}");
    }

    public bool RegisterVolcano(VolcanoTileState volcano)
    {
        if (volcano == null)
            return false;

        bool added = registeredVolcanoes.Add(volcano);

        if (volcano.IsErupting && !eruptingVolcanoes.Contains(volcano))
            eruptingVolcanoes.Add(volcano);

        if (debugLogging && added)
            //Debug.Log($"[VolcanoManager] Registered {volcano.name}");

        return added;
    }

    public bool UnregisterVolcano(VolcanoTileState volcano)
    {
        if (volcano == null)
            return false;

        bool removed = registeredVolcanoes.Remove(volcano);
        eruptingVolcanoes.Remove(volcano);

        if (debugLogging && removed)
            //Debug.Log($"[VolcanoManager] Unregistered {volcano.name}");

        return removed;
    }

    public void AdvanceVolcanoesOneTurn()
    {
        queuedAdvanceTurns++;

        if (advanceRoutine != null)
            return;

        if (processVolcanoTurnsOverFrames)
            advanceRoutine = StartCoroutine(AdvanceVolcanoesRoutine());
        else
        {
            while (queuedAdvanceTurns > 0)
            {
                queuedAdvanceTurns--;
                AdvanceVolcanoesImmediateSingleTurn();
            }
        }
    }

    private void HandleEndOfTurn()
    {
        if (!advanceWithTurnSystem)
            return;

        AdvanceVolcanoesOneTurn();
    }

    private IEnumerator AdvanceVolcanoesRoutine()
    {
        while (queuedAdvanceTurns > 0)
        {
            queuedAdvanceTurns--;

            PrepareSingleTurn();

            int processedThisFrame = 0;
            int maxPerFrame = Mathf.Max(1, volcanoesProcessedPerFrame);

            for (int i = 0; i < advanceBuffer.Count; i++)
            {
                VolcanoTileState volcano = advanceBuffer[i];
                if (volcano == null)
                    continue;

                volcano.AdvanceOneTurn(this);

                processedThisFrame++;

                if (processedThisFrame >= maxPerFrame)
                {
                    processedThisFrame = 0;
                    yield return null;
                }
            }

            FinishSingleTurn();
        }

        advanceRoutine = null;
    }

    private void AdvanceVolcanoesImmediateSingleTurn()
    {
        PrepareSingleTurn();

        for (int i = 0; i < advanceBuffer.Count; i++)
        {
            VolcanoTileState volcano = advanceBuffer[i];
            if (volcano == null)
                continue;

            volcano.AdvanceOneTurn(this);
        }

        FinishSingleTurn();
    }

    private void PrepareSingleTurn()
    {
        ResetPerTurnBudgets();

        BuildAdvanceBuffer();
        ShuffleAdvanceBuffer();

        OnVolcanoTurnAdvanceStarted?.Invoke();

        if (debugLogging)
            //Debug.Log($"[VolcanoManager] Volcano turn started. Count={advanceBuffer.Count}");
    }

    private void FinishSingleTurn()
    {
        CleanupDeadReferences();
        RefreshEruptingList();

        OnVolcanoTurnAdvanceFinished?.Invoke();

        if (debugLogging)
            //Debug.Log($"[VolcanoManager] Volcano turn finished. Erupting={eruptingVolcanoes.Count}");

        advanceBuffer.Clear();
    }

    private void BuildAdvanceBuffer()
    {
        advanceBuffer.Clear();

        foreach (VolcanoTileState volcano in registeredVolcanoes)
        {
            if (volcano == null)
                continue;

            advanceBuffer.Add(volcano);
        }
    }

    private void ShuffleAdvanceBuffer()
    {
        for (int i = advanceBuffer.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            VolcanoTileState temp = advanceBuffer[i];
            advanceBuffer[i] = advanceBuffer[j];
            advanceBuffer[j] = temp;
        }
    }

    private void CleanupDeadReferences()
    {
        registeredVolcanoes.RemoveWhere(v => v == null);
    }

    private void RefreshEruptingList()
    {
        eruptingVolcanoes.Clear();

        foreach (VolcanoTileState volcano in registeredVolcanoes)
        {
            if (volcano == null)
                continue;

            if (volcano.IsErupting)
                eruptingVolcanoes.Add(volcano);
        }
    }

    public bool TryConsumeMountainAwakeningBudget()
    {
        if (maxMountainAwakeningsPerTurn <= 0)
            return true;

        if (mountainAwakeningsUsedThisTurn >= maxMountainAwakeningsPerTurn)
            return false;

        mountainAwakeningsUsedThisTurn++;
        return true;
    }

    public bool TryConsumeEruptionBudget()
    {
        if (maxNewEruptionsPerTurn <= 0)
            return true;

        if (newEruptionsUsedThisTurn >= maxNewEruptionsPerTurn)
            return false;

        newEruptionsUsedThisTurn++;
        return true;
    }

    private void ResetPerTurnBudgets()
    {
        mountainAwakeningsUsedThisTurn = 0;
        newEruptionsUsedThisTurn = 0;
    }

    public void GetEruptingVolcanoesNonAlloc(List<VolcanoTileState> results)
    {
        if (results == null)
            return;

        results.Clear();

        for (int i = 0; i < eruptingVolcanoes.Count; i++)
        {
            if (eruptingVolcanoes[i] != null)
                results.Add(eruptingVolcanoes[i]);
        }
    }

    public void NotifyVolcanoCreated(VolcanoTileState volcano)
    {
        RegisterVolcano(volcano);
        OnVolcanoCreated?.Invoke(volcano);

        if (debugLogging && volcano != null)
            //Debug.Log($"[VolcanoManager] Volcano created: {volcano.name}");
    }

    public void NotifyVolcanoBecameDormant(VolcanoTileState volcano)
    {
        RegisterVolcano(volcano);
        eruptingVolcanoes.Remove(volcano);

        OnVolcanoBecameDormant?.Invoke(volcano);

        if (debugLogging && volcano != null)
            //Debug.Log($"[VolcanoManager] Volcano became Dormant: {volcano.name}");
    }

    public void NotifyEruptionStarted(VolcanoTileState volcano)
    {
        RegisterVolcano(volcano);

        if (volcano != null && !eruptingVolcanoes.Contains(volcano))
            eruptingVolcanoes.Add(volcano);

        OnEruptionStarted?.Invoke(volcano);

        if (debugLogging && volcano != null)
            //Debug.Log($"[VolcanoManager] Eruption started: {volcano.name}");
    }

    public void NotifyEruptingVolcanoAdvanced(VolcanoTileState volcano)
    {
        if (volcano == null)
            return;

        OnEruptingVolcanoAdvanced?.Invoke(volcano);
    }

    public void NotifyEruptionEnded(VolcanoTileState volcano)
    {
        eruptingVolcanoes.Remove(volcano);
        OnEruptionEnded?.Invoke(volcano);

        if (debugLogging && volcano != null)
            //Debug.Log($"[VolcanoManager] Eruption ended: {volcano.name}");
    }

    public void NotifyVolcanoRevertedToMountain(VolcanoTileState volcano)
    {
        eruptingVolcanoes.Remove(volcano);
        OnVolcanoRevertedToMountain?.Invoke(volcano);

        if (debugLogging && volcano != null)
            //Debug.Log($"[VolcanoManager] Volcano reverted to Mountain: {volcano.name}");
    }

    public VolcanoManagerSaveData SaveState()
    {
        CleanupDeadReferences();
        RefreshEruptingList();

        VolcanoManagerSaveData data = new VolcanoManagerSaveData
        {
            queuedAdvanceTurns = Mathf.Max(0, queuedAdvanceTurns),
            mountainAwakeningsUsedThisTurn = Mathf.Max(0, mountainAwakeningsUsedThisTurn),
            newEruptionsUsedThisTurn = Mathf.Max(0, newEruptionsUsedThisTurn),

            registeredVolcanoCount = registeredVolcanoes.Count,
            eruptingVolcanoCount = eruptingVolcanoes.Count
        };

        foreach (VolcanoTileState volcano in registeredVolcanoes)
        {
            if (volcano == null)
                continue;

            VolcanoTileRuntimeSaveData volcanoData = volcano.CaptureRuntimeSaveData();

            if (volcanoData == null)
                continue;

            data.volcanoStates.Add(volcanoData);
        }

        return data;
    }

    public void LoadState(VolcanoManagerSaveData data)
    {
        if (advanceRoutine != null)
        {
            StopCoroutine(advanceRoutine);
            advanceRoutine = null;
        }

        registeredVolcanoes.Clear();
        eruptingVolcanoes.Clear();
        advanceBuffer.Clear();

        RegisterAllSceneVolcanoes();

        if (data != null)
        {
            queuedAdvanceTurns = Mathf.Max(0, data.queuedAdvanceTurns);
            mountainAwakeningsUsedThisTurn = Mathf.Max(0, data.mountainAwakeningsUsedThisTurn);
            newEruptionsUsedThisTurn = Mathf.Max(0, data.newEruptionsUsedThisTurn);

            ApplySavedVolcanoStates(data);
        }
        else
        {
            queuedAdvanceTurns = 0;
            mountainAwakeningsUsedThisTurn = 0;
            newEruptionsUsedThisTurn = 0;
        }

        CleanupDeadReferences();
        RefreshEruptingList();

        NotifyLoadedEruptions();

        if (debugLogging)
        {
            //Debug.Log(
                //$"[VolcanoManager] Loaded manager state. " +
                //$"Registered={registeredVolcanoes.Count}, Erupting={eruptingVolcanoes.Count}, " +
                //$"QueuedTurns={queuedAdvanceTurns}");
        }
    }

    private void ApplySavedVolcanoStates(VolcanoManagerSaveData data)
    {
        if (data == null || data.volcanoStates == null || data.volcanoStates.Count == 0)
            return;

        List<VolcanoTileState> liveVolcanoes = new List<VolcanoTileState>();

        foreach (VolcanoTileState volcano in registeredVolcanoes)
        {
            if (volcano != null)
                liveVolcanoes.Add(volcano);
        }

        HashSet<VolcanoTileState> usedVolcanoes = new HashSet<VolcanoTileState>();

        for (int i = 0; i < data.volcanoStates.Count; i++)
        {
            VolcanoTileRuntimeSaveData saved = data.volcanoStates[i];

            if (saved == null)
                continue;

            VolcanoTileState target = FindMatchingVolcanoForSave(saved, liveVolcanoes, usedVolcanoes);

            if (target == null)
            {
                if (debugLogging)
                {
                    //Debug.LogWarning(
                        //$"[VolcanoManager] Could not find live volcano for saved state. " +
                        //$"HasCell={saved.hasPrimaryCell}, Cell=({saved.primaryCellX},{saved.primaryCellY}), " +
                        //$"StateValue={saved.activityStateValue}");
                }

                continue;
            }

            target.ApplyRuntimeSaveData(saved);
            usedVolcanoes.Add(target);
        }
    }

    private VolcanoTileState FindMatchingVolcanoForSave(
        VolcanoTileRuntimeSaveData saved,
        List<VolcanoTileState> liveVolcanoes,
        HashSet<VolcanoTileState> usedVolcanoes)
    {
        if (saved == null || liveVolcanoes == null)
            return null;

        if (saved.hasPrimaryCell)
        {
            TileCoord wantedCell = new TileCoord(saved.primaryCellX, saved.primaryCellY);

            for (int i = 0; i < liveVolcanoes.Count; i++)
            {
                VolcanoTileState volcano = liveVolcanoes[i];

                if (volcano == null || usedVolcanoes.Contains(volcano))
                    continue;

                if (!volcano.TryGetPrimaryCell(out TileCoord cell))
                    continue;

                if (cell.x == wantedCell.x && cell.y == wantedCell.y)
                    return volcano;
            }
        }

        // Fallback only if cell matching failed.
        for (int i = 0; i < liveVolcanoes.Count; i++)
        {
            VolcanoTileState volcano = liveVolcanoes[i];

            if (volcano == null || usedVolcanoes.Contains(volcano))
                continue;

            return volcano;
        }

        return null;
    }

    private void NotifyLoadedEruptions()
    {
        RefreshEruptingList();

        for (int i = 0; i < eruptingVolcanoes.Count; i++)
        {
            VolcanoTileState volcano = eruptingVolcanoes[i];

            if (volcano == null)
                continue;

            // Re-fire the eruption-start event after load so lava/soot/bridge systems
            // know this volcano is actively erupting.
            OnEruptionStarted?.Invoke(volcano);
        }
    }
}
