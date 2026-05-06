using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LavaAnimalEffectResolver : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LavaOverlayManager lavaOverlayManager;
    [SerializeField] private AnimalSimulation animalSimulation;
    [SerializeField] private GridManager gridManager;

    [Header("Timing")]
    [Tooltip("Best option. Animals react before the lava cell is officially added.")]
    [SerializeField] private bool applyBeforeLavaCellActivates = true;

    [Tooltip("Fallback option. Usually keep this false if using before activation.")]
    [SerializeField] private bool applyWhenLavaCellActivates = false;

    [Tooltip("Optional safety check. Applies lava effects to animals still standing on active lava each turn.")]
    [SerializeField] private bool applyOngoingEachTurn = true;

    [Header("Animal Flee")]
    [Range(0f, 1f)]
    [SerializeField] private float fleeChanceBeforeLavaHits = 0.65f;

    [Min(1)]
    [SerializeField] private int fleeSearchDistance = 1;

    [Header("If Flee Fails")]
    [SerializeField] private bool instantKillIfFleeFails = true;

    [Tooltip("Only used if Instant Kill If Flee Fails is false.")]
    [Min(0)]
    [SerializeField] private int damageIfFleeFails = 9999;

    [Header("Over-Frame Processing")]
    [SerializeField] private bool processOngoingOverFrames = true;

    [Min(1)]
    [SerializeField] private int lavaCellsProcessedPerFrame = 8;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = false;

    private readonly List<int> groupIdsScratch = new List<int>(16);
    private readonly List<TileCoord> activeLavaCellsScratch = new List<TileCoord>(128);
    private readonly HashSet<int> processedGroupsThisPass = new HashSet<int>();

    private LavaOverlayManager subscribedLavaOverlayManager;
    private Coroutine ongoingRoutine;

    private TileCoord currentIncomingLavaCell;
    private bool hasCurrentIncomingLavaCell;

    private void Awake()
    {
        EnsureLinks();
    }

    private void OnEnable()
    {
        EnsureLinks();
        RebindLavaEvents();

        if (applyOngoingEachTurn)
            TurnSystem.SubscribeToEndOfTurn(HandleEndOfTurn);
    }

    private void Start()
    {
        EnsureLinks();
        RebindLavaEvents();
    }

    private void OnDisable()
    {
        UnbindLavaEvents();
        TurnSystem.UnsubscribeFromEndOfTurn(HandleEndOfTurn);

        if (ongoingRoutine != null)
        {
            StopCoroutine(ongoingRoutine);
            ongoingRoutine = null;
        }

        groupIdsScratch.Clear();
        activeLavaCellsScratch.Clear();
        processedGroupsThisPass.Clear();

        hasCurrentIncomingLavaCell = false;
    }

    private void EnsureLinks()
    {
        if (lavaOverlayManager == null)
            lavaOverlayManager = LavaOverlayManager.Instance;

        if (gridManager == null)
            gridManager = GridManager.Instance;

        // Assign this manually if you have more than one AnimalSimulation.
        // This is only a startup fallback, not a per-turn scan.
        if (animalSimulation == null)
            animalSimulation = AnimalSimulationAccess.Current;
    }

    private void RebindLavaEvents()
    {
        if (subscribedLavaOverlayManager == lavaOverlayManager)
            return;

        UnbindLavaEvents();

        subscribedLavaOverlayManager = lavaOverlayManager;

        if (subscribedLavaOverlayManager == null)
            return;

        if (applyBeforeLavaCellActivates)
            subscribedLavaOverlayManager.OnBeforeLavaCellActivated += HandleBeforeLavaCellActivated;

        if (applyWhenLavaCellActivates)
            subscribedLavaOverlayManager.OnLavaCellActivated += HandleLavaCellActivated;
    }

    private void UnbindLavaEvents()
    {
        if (subscribedLavaOverlayManager == null)
            return;

        subscribedLavaOverlayManager.OnBeforeLavaCellActivated -= HandleBeforeLavaCellActivated;
        subscribedLavaOverlayManager.OnLavaCellActivated -= HandleLavaCellActivated;

        subscribedLavaOverlayManager = null;
    }

    private void HandleBeforeLavaCellActivated(TileCoord coord)
    {
        ApplyAnimalLavaThreatAtCell(coord, incomingLavaCell: true);
    }

    private void HandleLavaCellActivated(TileCoord coord)
    {
        ApplyAnimalLavaThreatAtCell(coord, incomingLavaCell: false);
    }

    private void HandleEndOfTurn()
    {
        if (!applyOngoingEachTurn)
            return;

        EnsureLinks();

        if (lavaOverlayManager == null || animalSimulation == null)
            return;

        if (!lavaOverlayManager.CopyActiveLavaCells(activeLavaCellsScratch))
            return;

        if (processOngoingOverFrames)
        {
            if (ongoingRoutine == null)
                ongoingRoutine = StartCoroutine(ProcessOngoingRoutine());
        }
        else
        {
            ProcessOngoingImmediate();
        }
    }

    private IEnumerator ProcessOngoingRoutine()
    {
        processedGroupsThisPass.Clear();

        int processed = 0;
        int maxPerFrame = Mathf.Max(1, lavaCellsProcessedPerFrame);

        for (int i = 0; i < activeLavaCellsScratch.Count; i++)
        {
            ApplyAnimalLavaThreatAtCell(activeLavaCellsScratch[i], incomingLavaCell: false);

            processed++;
            if (processed >= maxPerFrame)
            {
                processed = 0;
                yield return null;
            }
        }

        processedGroupsThisPass.Clear();
        ongoingRoutine = null;
    }

    private void ProcessOngoingImmediate()
    {
        processedGroupsThisPass.Clear();

        for (int i = 0; i < activeLavaCellsScratch.Count; i++)
            ApplyAnimalLavaThreatAtCell(activeLavaCellsScratch[i], incomingLavaCell: false);

        processedGroupsThisPass.Clear();
    }

    private void ApplyAnimalLavaThreatAtCell(TileCoord coord, bool incomingLavaCell)
    {
        EnsureLinks();

        if (animalSimulation == null)
            return;

        if (!animalSimulation.HasGroupsAtTile(coord))
            return;

        groupIdsScratch.Clear();
        int count = animalSimulation.GetGroupIdsAtTileNonAlloc(coord, groupIdsScratch);

        if (count <= 0)
            return;

        hasCurrentIncomingLavaCell = incomingLavaCell;
        currentIncomingLavaCell = coord;

        for (int i = 0; i < groupIdsScratch.Count; i++)
        {
            int groupId = groupIdsScratch[i];

            if (processedGroupsThisPass.Contains(groupId))
                continue;

            processedGroupsThisPass.Add(groupId);

            animalSimulation.TryApplyLavaThreatToGroup(
                groupId,
                fleeChanceBeforeLavaHits,
                instantKillIfFleeFails,
                damageIfFleeFails,
                fleeSearchDistance,
                IsLavaOrIncomingLavaAtTile,
                IsValidFleeTile,
                debugLogging);
        }

        hasCurrentIncomingLavaCell = false;
    }

    private bool IsLavaOrIncomingLavaAtTile(TileCoord coord)
    {
        if (hasCurrentIncomingLavaCell && coord.Equals(currentIncomingLavaCell))
            return true;

        if (lavaOverlayManager != null && lavaOverlayManager.HasLavaAt(coord))
            return true;

        return false;
    }

    private bool IsValidFleeTile(TileCoord coord)
    {
        if (IsOutsideGrid(coord))
            return false;

        if (IsLavaOrIncomingLavaAtTile(coord))
            return false;

        return true;
    }

    private bool IsOutsideGrid(TileCoord coord)
    {
        EnsureLinks();

        if (gridManager == null)
            return false;

        return coord.x < 0 ||
               coord.y < 0 ||
               coord.x >= gridManager.columns ||
               coord.y >= gridManager.rows;
    }
}