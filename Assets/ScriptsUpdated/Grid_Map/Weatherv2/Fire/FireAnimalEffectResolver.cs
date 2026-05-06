using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FireAnimalEffectResolver : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WeatherFireSystem weatherFireSystem;
    [SerializeField] private AnimalSimulation animalSimulation;
    [SerializeField] private GridManager gridManager;

    [Header("Timing")]
    [Tooltip("Animals react when a fire cell first ignites.")]
    [SerializeField] private bool applyWhenFireIgnites = true;

    [Tooltip("Safety pass for animals still standing on burning cells.")]
    [SerializeField] private bool applyOngoingEachTurn = true;

    [Header("Animal Flee")]
    [Range(0f, 1f)]
    [SerializeField] private float fleeChanceWhenFireStarts = 0.70f;

    [Range(0f, 1f)]
    [SerializeField] private float fleeChanceOnOngoingFire = 0.45f;

    [Min(1)]
    [SerializeField] private int fleeSearchDistance = 1;

    [Header("If Flee Fails")]
    [SerializeField] private bool instantKillIfFleeFails = false;

    [Tooltip("Used if Instant Kill If Flee Fails is false.")]
    [Min(0)]
    [SerializeField] private int damageIfFleeFails = 8;

    [Header("Over-Frame Processing")]
    [SerializeField] private bool processOngoingOverFrames = true;

    [Min(1)]
    [SerializeField] private int fireCellsProcessedPerFrame = 8;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = false;

    private readonly List<int> groupIdsScratch = new List<int>(16);
    private readonly List<TileCoord> activeFireCellsScratch = new List<TileCoord>(128);
    private readonly HashSet<int> processedGroupsThisPass = new HashSet<int>();

    private WeatherFireSystem subscribedWeatherFireSystem;
    private Coroutine ongoingRoutine;

    private TileCoord currentIncomingFireCell;
    private bool hasCurrentIncomingFireCell;

    private void Awake()
    {
        EnsureLinks();
    }

    private void OnEnable()
    {
        EnsureLinks();
        RebindFireEvents();

        if (applyOngoingEachTurn)
            TurnSystem.SubscribeToEndOfTurn(HandleEndOfTurn);
    }

    private void Start()
    {
        EnsureLinks();
        RebindFireEvents();
    }

    private void OnDisable()
    {
        UnbindFireEvents();
        TurnSystem.UnsubscribeFromEndOfTurn(HandleEndOfTurn);

        if (ongoingRoutine != null)
        {
            StopCoroutine(ongoingRoutine);
            ongoingRoutine = null;
        }

        groupIdsScratch.Clear();
        activeFireCellsScratch.Clear();
        processedGroupsThisPass.Clear();

        hasCurrentIncomingFireCell = false;
    }

    private void EnsureLinks()
    {
        if (weatherFireSystem == null)
            weatherFireSystem = WeatherFireSystem.Instance;

        if (gridManager == null)
            gridManager = GridManager.Instance;

        if (animalSimulation == null)
            animalSimulation = AnimalSimulationAccess.Current;
    }

    private void RebindFireEvents()
    {
        if (subscribedWeatherFireSystem == weatherFireSystem)
            return;

        UnbindFireEvents();

        subscribedWeatherFireSystem = weatherFireSystem;

        if (subscribedWeatherFireSystem == null)
            return;

        if (applyWhenFireIgnites)
            subscribedWeatherFireSystem.OnFireCellIgnited += HandleFireCellIgnited;
    }

    private void UnbindFireEvents()
    {
        if (subscribedWeatherFireSystem == null)
            return;

        subscribedWeatherFireSystem.OnFireCellIgnited -= HandleFireCellIgnited;
        subscribedWeatherFireSystem = null;
    }

    private void HandleFireCellIgnited(TileCoord coord)
    {
        processedGroupsThisPass.Clear();

        ApplyAnimalFireThreatAtCell(
            coord,
            incomingFireCell: true,
            fleeChance01: fleeChanceWhenFireStarts);

        processedGroupsThisPass.Clear();
    }

    private void HandleEndOfTurn()
    {
        if (!applyOngoingEachTurn)
            return;

        EnsureLinks();

        if (weatherFireSystem == null || animalSimulation == null)
            return;

        if (!weatherFireSystem.CopyActiveFireCells(activeFireCellsScratch))
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
        int maxPerFrame = Mathf.Max(1, fireCellsProcessedPerFrame);

        for (int i = 0; i < activeFireCellsScratch.Count; i++)
        {
            ApplyAnimalFireThreatAtCell(
                activeFireCellsScratch[i],
                incomingFireCell: false,
                fleeChance01: fleeChanceOnOngoingFire);

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

        for (int i = 0; i < activeFireCellsScratch.Count; i++)
        {
            ApplyAnimalFireThreatAtCell(
                activeFireCellsScratch[i],
                incomingFireCell: false,
                fleeChance01: fleeChanceOnOngoingFire);
        }

        processedGroupsThisPass.Clear();
    }

    private void ApplyAnimalFireThreatAtCell(
        TileCoord coord,
        bool incomingFireCell,
        float fleeChance01)
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

        hasCurrentIncomingFireCell = incomingFireCell;
        currentIncomingFireCell = coord;

        for (int i = 0; i < groupIdsScratch.Count; i++)
        {
            int groupId = groupIdsScratch[i];

            if (processedGroupsThisPass.Contains(groupId))
                continue;

            processedGroupsThisPass.Add(groupId);

            animalSimulation.TryApplyFireThreatToGroup(
                groupId,
                fleeChance01,
                instantKillIfFleeFails,
                damageIfFleeFails,
                fleeSearchDistance,
                IsFireOrIncomingFireAtTile,
                IsValidFleeTile,
                debugLogging);
        }

        hasCurrentIncomingFireCell = false;
    }

    private bool IsFireOrIncomingFireAtTile(TileCoord coord)
    {
        if (hasCurrentIncomingFireCell && coord.Equals(currentIncomingFireCell))
            return true;

        if (weatherFireSystem != null && weatherFireSystem.IsAnythingOnFireAtCell(coord.x, coord.y))
            return true;

        return false;
    }

    private bool IsValidFleeTile(TileCoord coord)
    {
        if (IsOutsideGrid(coord))
            return false;

        if (IsFireOrIncomingFireAtTile(coord))
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