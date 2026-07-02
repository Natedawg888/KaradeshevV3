using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UnitGroupMovementManager : MonoBehaviour
{
    public static UnitGroupMovementManager Instance { get; private set; }

    [Header("Global Movement UI")]
    public Button endMovementButton;

    [Header("Movement Processing")]
    [Tooltip("How many groups to process per frame when resolving multi-turn movement.")]
    public int groupsPerBatch = 10;

    [Header("Neighbour Detection (Colliders)")]
    [Tooltip("Slightly enlarge the box around the tile to pick up touching neighbour tiles.")]
    [SerializeField] private float neighbourDetectionMultiplier = 1.05f;

    [Tooltip("Physics layer mask for tile colliders (set this to your TileClickable layer).")]
    [SerializeField] private LayerMask tileLayerMask = 0;

    private class MovementContext
    {
        public TileUnitGroupData group;
        public TileUnitGroupControl owner;
        public TileControl currentTile;
        public TileControl originTile;
        public int stepsRemaining;

        public readonly List<TileControl> plannedTiles = new();
        public readonly List<float> stepTurnCosts = new();

        public bool isPatrol;
    }

    private MovementContext _activeContext;

    private float _turnCostMultiplier = 1f;
    private float _minTurnCost = 0.1f;

    // Lazy caches — rebuilt only when dirty, not every turn
    private static readonly List<TileUnitGroupControl> s_controlsCache = new();
    private static bool s_controlsCacheDirty = true;

    private static readonly List<UnitGroupMarker> s_markersCache = new();
    private static bool s_markersCacheDirty = true;

    public static event System.Action OnMovementPlanningBegan;
    public static event System.Action<TileUnitGroupData, TileUnitGroupControl> OnMovementRouteConfirmed;

    // Call when unit groups are added or removed from the scene
    public static void InvalidateUnitCaches()
    {
        s_controlsCacheDirty = true;
        s_markersCacheDirty  = true;
    }

    private static List<TileUnitGroupControl> GetAllControls()
    {
        if (s_controlsCacheDirty)
        {
            s_controlsCache.Clear();
            s_controlsCache.AddRange(FindObjectsOfType<TileUnitGroupControl>());
            s_controlsCacheDirty = false;
        }
        return s_controlsCache;
    }

    private static List<UnitGroupMarker> GetAllMarkers()
    {
        if (s_markersCacheDirty)
        {
            s_markersCache.Clear();
            s_markersCache.AddRange(FindObjectsOfType<UnitGroupMarker>());
            s_markersCacheDirty = false;
        }
        return s_markersCache;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (tileLayerMask == 0)
            tileLayerMask = LayerMask.GetMask("TileClickable");

        // Mark caches dirty on startup so first use does a fresh FindObjectsOfType
        s_controlsCacheDirty = true;
        s_markersCacheDirty  = true;

        if (endMovementButton != null)
        {
            endMovementButton.onClick.RemoveAllListeners();
            endMovementButton.onClick.AddListener(OnConfirmMovementClicked);
            endMovementButton.gameObject.SetActive(false);
        }
    }

    public void BeginMovementForGroup(TileUnitGroupData group, TileUnitGroupControl owner)
    {
        _turnCostMultiplier = 1f;
        _minTurnCost = 0.1f;

        BeginMovementForGroup_Internal(group, owner);
    }

    public void BeginMovementForGroup(
        TileUnitGroupData group,
        TileUnitGroupControl owner,
        float turnCostMultiplier,
        float minTurnCost)
    {
        _turnCostMultiplier = Mathf.Clamp(turnCostMultiplier, 0.01f, 10f);
        _minTurnCost = Mathf.Max(0.1f, minTurnCost);

        BeginMovementForGroup_Internal(group, owner);
    }

    private void BeginMovementForGroup_Internal(TileUnitGroupData group, TileUnitGroupControl owner)
    {
        if (group == null || owner == null)
        {
            //Debug.LogWarning("[UnitGroupMovementManager] BeginMovementForGroup: missing group or owner.");
            return;
        }

        var unit = group.unitType;
        if (unit == null)
        {
            //Debug.LogWarning("[UnitGroupMovementManager] BeginMovementForGroup: group has no unitType.");
            return;
        }

        var originTile = ResolveTileForOwner(owner);
        if (originTile == null)
        {
            //Debug.LogWarning("[UnitGroupMovementManager] BeginMovementForGroup: could not resolve TileControl for owner.");
            return;
        }

        float totalMove = unit.movementSpeed + group.bonusMovementSpeed;
        int maxSteps = Mathf.FloorToInt(totalMove);

        if (maxSteps <= 0)
        {
            //Debug.Log($"[UnitGroupMovementManager] Group {group.groupId} has no movement.");
            return;
        }

        if (group.plannedPathGridPositions != null)
            group.plannedPathGridPositions.Clear();
        if (group.plannedStepTurnCosts != null)
            group.plannedStepTurnCosts.Clear();
        if (group.patrolLoopGridPositions != null)
            group.patrolLoopGridPositions.Clear();
        if (group.patrolLoopStepTurnCosts != null)
            group.patrolLoopStepTurnCosts.Clear();

        group.currentPathIndex = 0;
        group.remainingTurnCostOnCurrentStep = 0f;
        group.isPatrolling = false;

        _activeContext = new MovementContext
        {
            group = group,
            owner = owner,
            currentTile = originTile,
            originTile = originTile,
            stepsRemaining = maxSteps,
            isPatrol = false
        };

        //Debug.Log($"[UnitGroupMovementManager] Starting movement planning for group {group.groupId} with {maxSteps} steps.");

        ShowStepOptions();
        OnMovementPlanningBegan?.Invoke();
    }

    public void BeginPatrolForGroup(TileUnitGroupData group, TileUnitGroupControl owner)
    {
        if (group == null || owner == null)
        {
            //Debug.LogWarning("[UnitGroupMovementManager] BeginPatrolForGroup: missing group or owner.");
            return;
        }

        var unit = group.unitType;
        if (unit == null)
        {
            //Debug.LogWarning("[UnitGroupMovementManager] BeginPatrolForGroup: group has no unitType.");
            return;
        }

        var originTile = ResolveTileForOwner(owner);
        if (originTile == null)
        {
            //Debug.LogWarning("[UnitGroupMovementManager] BeginPatrolForGroup: could not resolve TileControl for owner.");
            return;
        }

        if (!IsTileAllowedForPatrol(originTile))
        {
            //Debug.Log("[UnitGroupMovementManager] Cannot start patrol: origin tile must be a discovered environment or a building.");
            return;
        }

        float totalMove = unit.movementSpeed + group.bonusMovementSpeed;
        int maxSteps = Mathf.FloorToInt(totalMove);

        if (maxSteps <= 0)
        {
            //Debug.Log($"[UnitGroupMovementManager] Group {group.groupId} has no movement; cannot start patrol.");
            return;
        }

        if (group.plannedPathGridPositions != null)
            group.plannedPathGridPositions.Clear();
        if (group.plannedStepTurnCosts != null)
            group.plannedStepTurnCosts.Clear();
        if (group.patrolLoopGridPositions != null)
            group.patrolLoopGridPositions.Clear();
        if (group.patrolLoopStepTurnCosts != null)
            group.patrolLoopStepTurnCosts.Clear();

        group.currentPathIndex = 0;
        group.remainingTurnCostOnCurrentStep = 0f;
        group.isPatrolling = false;

        _activeContext = new MovementContext
        {
            group = group,
            owner = owner,
            currentTile = originTile,
            originTile = originTile,
            stepsRemaining = maxSteps,
            isPatrol = true
        };

        //Debug.Log($"[UnitGroupMovementManager] Starting PATROL planning for group {group.groupId} with {maxSteps} steps.");

        ShowStepOptions();
    }

    private void OnConfirmMovementClicked()
    {
        ConfirmMovement();
    }

    private void ShowStepOptions()
    {
        ClearAllMoveHereButtons();

        if (_activeContext == null || _activeContext.group == null || _activeContext.owner == null)
        {
            if (endMovementButton != null)
                endMovementButton.gameObject.SetActive(false);
            return;
        }

        if (_activeContext.stepsRemaining <= 0)
        {
            //Debug.Log($"[UnitGroupMovementManager] Group {_activeContext.group.groupId} has no steps remaining.");
            if (endMovementButton != null)
                endMovementButton.gameObject.SetActive(_activeContext.plannedTiles.Count > 0);
            return;
        }

        var currentTile = _activeContext.currentTile;
        if (currentTile == null)
        {
            //Debug.LogWarning("[UnitGroupMovementManager] ShowStepOptions: currentTile is null.");
            if (endMovementButton != null)
                endMovementButton.gameObject.SetActive(false);
            _activeContext = null;
            return;
        }

        int options = 0;

        foreach (var neigh in GetNeighbors(currentTile))
        {
            if (neigh == null) continue;

            var movementUI = neigh.GetComponentInChildren<TileMovementUI>(true);
            if (movementUI == null || movementUI.moveHereButton == null)
                continue;

            if (_activeContext.isPatrol && !IsTileAllowedForPatrol(neigh))
                continue;

            float stepTurnCost = TileMovementCostCalculator.GetTurnCostForStep(neigh, _activeContext.group);

            if (stepTurnCost <= 0f || float.IsInfinity(stepTurnCost) || float.IsNaN(stepTurnCost))
                continue;

            stepTurnCost = Mathf.Max(_minTurnCost, stepTurnCost * _turnCostMultiplier);

            if (stepTurnCost <= 0f || float.IsInfinity(stepTurnCost) || float.IsNaN(stepTurnCost))
                continue;

            float localCost = stepTurnCost;

            EnvironmentControl envCtrl;
            float hazardChance01;
            float dmgOutcome01;
            float fatalOutcome01;

            bool hasHazard = TryComputeHazardChances(
                neigh,
                _activeContext.group,
                out envCtrl,
                out hazardChance01,
                out dmgOutcome01,
                out fatalOutcome01
            );

            var targetTile = neigh;
            options++;

            movementUI.ShowMoveHereButton(
                () => OnStepMoveClicked(targetTile, localCost),
                localCost,
                dmgOutcome01,
                fatalOutcome01,
                hasHazard
            );
        }

        bool canConfirm =
            _activeContext.plannedTiles.Count > 0 ||
            options > 0;

        if (endMovementButton != null)
            endMovementButton.gameObject.SetActive(canConfirm);

        //Debug.Log(
            //$"[UnitGroupMovementManager] Showing {options} step options for group {_activeContext.group.groupId} " +
            //$"(steps remaining: {_activeContext.stepsRemaining}, planned tiles: {_activeContext.plannedTiles.Count}).");

        if (options == 0)
        {
            //Debug.LogWarning(
                //$"[UnitGroupMovementManager] No neighbour tiles found for {currentTile.name}. " +
                //"Check tile BoxColliders / layers / TileMovementUI.");
        }
    }

    private void OnStepMoveClicked(TileControl targetTile, float stepTurnCost)
    {
        if (_activeContext == null ||
            _activeContext.group == null ||
            _activeContext.owner == null)
        {
            //Debug.LogWarning("[UnitGroupMovementManager] OnStepMoveClicked with no active context.");
            return;
        }

        if (targetTile == null)
        {
            //Debug.LogWarning("[UnitGroupMovementManager] OnStepMoveClicked: invalid target (null tile).");
            return;
        }

        _activeContext.plannedTiles.Add(targetTile);
        _activeContext.stepTurnCosts.Add(Mathf.Max(0f, stepTurnCost));
        _activeContext.stepsRemaining = Mathf.Max(0, _activeContext.stepsRemaining - 1);

        _activeContext.currentTile = targetTile;

        string mode = _activeContext.isPatrol ? "PATROL" : "MOVE";

        //Debug.Log(
            //$"[UnitGroupMovementManager] Planned {mode} step #{_activeContext.plannedTiles.Count} for group {_activeContext.group.groupId} " +
            //$"to {targetTile.name} (step cost: {stepTurnCost:0.0} turns, steps remaining: {_activeContext.stepsRemaining}).");

        ClearAllMoveHereButtons();

        if (_activeContext.stepsRemaining > 0)
            ShowStepOptions();
        else
            ConfirmMovement();
    }

    private void ConfirmMovement()
    {
        if (_activeContext == null)
        {
            if (endMovementButton != null)
                endMovementButton.gameObject.SetActive(false);

            TileInteraction.SetSelectionEnabled(true);
            return;
        }

        var group = _activeContext.group;

        if (_activeContext.plannedTiles.Count == 0)
        {
            //Debug.Log($"[UnitGroupMovementManager] ConfirmMovement: no tiles selected for group {group.groupId}. Cancelling.");
            NotifyGroupMovementUpdated(group);
            ClearAllMoveHereButtons();
            if (endMovementButton != null)
                endMovementButton.gameObject.SetActive(false);

            TileInteraction.SetSelectionEnabled(true);

            _activeContext = null;
            return;
        }

        if (_activeContext.isPatrol)
            BuildPatrolRouteFromContext(_activeContext);
        else
            BuildStandardRouteFromContext(_activeContext);

        ClearAllMoveHereButtons();
        if (endMovementButton != null)
            endMovementButton.gameObject.SetActive(false);

        _activeContext = null;
        TileInteraction.SetSelectionEnabled(true);
    }

    private void BuildStandardRouteFromContext(MovementContext ctx)
    {
        var group = ctx.group;

        float totalTurnCost = 0f;
        for (int i = 0; i < ctx.stepTurnCosts.Count; i++)
            totalTurnCost += Mathf.Max(0f, ctx.stepTurnCosts[i]);

        int displayTotal = Mathf.CeilToInt(totalTurnCost);

        //Debug.Log(
            //$"[UnitGroupMovementManager] Confirmed movement for group {group.groupId}: " +
            //$"{ctx.plannedTiles.Count} steps, approx {displayTotal} turns total.");

        if (group.plannedPathGridPositions == null)
            group.plannedPathGridPositions = new List<Vector2Int>();
        if (group.plannedStepTurnCosts == null)
            group.plannedStepTurnCosts = new List<float>();
        if (group.patrolLoopGridPositions == null)
            group.patrolLoopGridPositions = new List<Vector2Int>();
        if (group.patrolLoopStepTurnCosts == null)
            group.patrolLoopStepTurnCosts = new List<float>();

        group.plannedPathGridPositions.Clear();
        group.plannedStepTurnCosts.Clear();
        group.patrolLoopGridPositions.Clear();
        group.patrolLoopStepTurnCosts.Clear();
        group.isPatrolling = false;

        for (int i = 0; i < ctx.plannedTiles.Count; i++)
        {
            TileControl tile = ctx.plannedTiles[i];
            Vector2Int gp = tile.GetGridPosition();

            group.plannedPathGridPositions.Add(gp);
            group.plannedStepTurnCosts.Add(Mathf.Max(0f, ctx.stepTurnCosts[i]));
        }

        group.currentPathIndex = 0;
        group.remainingTurnCostOnCurrentStep =
            group.plannedStepTurnCosts.Count > 0
            ? Mathf.Max(0.1f, group.plannedStepTurnCosts[0])
            : 0f;

        //Debug.Log(
            //$"[UnitGroupMovementManager] Stored route on group {group.groupId}: " +
            //$"{group.plannedPathGridPositions.Count} steps, first step cost = {group.remainingTurnCostOnCurrentStep:0.0} turns.");

        NotifyGroupMovementUpdated(group);
        OnMovementRouteConfirmed?.Invoke(group, ctx.owner);
    }

    private void BuildPatrolRouteFromContext(MovementContext ctx)
    {
        var group = ctx.group;
        var origin = ctx.originTile ?? ResolveTileForOwner(ctx.owner);

        if (origin == null)
        {
            //Debug.LogWarning("[UnitGroupMovementManager] BuildPatrolRouteFromContext: origin tile missing; falling back to normal movement.");
            BuildStandardRouteFromContext(ctx);
            return;
        }

        var forwardGrid = new List<Vector2Int>();
        var forwardCosts = new List<float>();

        for (int i = 0; i < ctx.plannedTiles.Count; i++)
        {
            TileControl tile = ctx.plannedTiles[i];
            if (tile == null) continue;

            if (!IsTileAllowedForPatrol(tile))
            {
                //Debug.LogWarning(
                    //$"[UnitGroupMovementManager] Patrol path for group {group.groupId} contains invalid tile '{tile.name}'. " +
                    //"Falling back to one-way movement.");
                BuildStandardRouteFromContext(ctx);
                return;
            }

            forwardGrid.Add(tile.GetGridPosition());
            forwardCosts.Add(Mathf.Max(0f, ctx.stepTurnCosts[i]));
        }

        if (forwardGrid.Count == 0)
        {
            //Debug.LogWarning("[UnitGroupMovementManager] BuildPatrolRouteFromContext: no tiles in patrol path.");
            BuildStandardRouteFromContext(ctx);
            return;
        }

        Vector2Int originGrid = origin.GetGridPosition();

        var loopGrid = new List<Vector2Int>(forwardGrid.Count * 2);
        var loopCosts = new List<float>(forwardCosts.Count * 2);

        loopGrid.AddRange(forwardGrid);
        loopCosts.AddRange(forwardCosts);

        for (int i = forwardGrid.Count - 2; i >= 0; i--)
        {
            loopGrid.Add(forwardGrid[i]);
            loopCosts.Add(forwardCosts[i]);
        }

        if (IsTileAllowedForPatrol(origin))
        {
            loopGrid.Add(originGrid);
            float originStepCost = TileMovementCostCalculator.GetTurnCostForStep(origin, group);
            originStepCost = Mathf.Max(0.1f, originStepCost);
            loopCosts.Add(originStepCost);
        }

        if (group.plannedPathGridPositions == null)
            group.plannedPathGridPositions = new List<Vector2Int>();
        if (group.plannedStepTurnCosts == null)
            group.plannedStepTurnCosts = new List<float>();
        if (group.patrolLoopGridPositions == null)
            group.patrolLoopGridPositions = new List<Vector2Int>();
        if (group.patrolLoopStepTurnCosts == null)
            group.patrolLoopStepTurnCosts = new List<float>();

        group.plannedPathGridPositions.Clear();
        group.plannedStepTurnCosts.Clear();
        group.patrolLoopGridPositions.Clear();
        group.patrolLoopStepTurnCosts.Clear();

        group.plannedPathGridPositions.AddRange(loopGrid);
        group.plannedStepTurnCosts.AddRange(loopCosts);

        group.patrolLoopGridPositions.AddRange(loopGrid);
        group.patrolLoopStepTurnCosts.AddRange(loopCosts);

        group.currentPathIndex = 0;
        group.remainingTurnCostOnCurrentStep =
            group.plannedStepTurnCosts.Count > 0
            ? Mathf.Max(0.1f, group.plannedStepTurnCosts[0])
            : 0f;

        group.isPatrolling = true;

        float totalTurnCost = 0f;
        for (int i = 0; i < loopCosts.Count; i++)
            totalTurnCost += loopCosts[i];
        int approxTurns = Mathf.CeilToInt(totalTurnCost);

        //Debug.Log(
            //$"[UnitGroupMovementManager] Confirmed PATROL route for group {group.groupId}: " +
            //$"{loopGrid.Count} steps in ping-pong loop (≈{approxTurns} turns per full cycle).");

        NotifyGroupMovementUpdated(group);
    }

    public void ProcessMovementForAllGroupsBatched()
    {
        StartCoroutine(ProcessMovementCoroutine());
    }

    private IEnumerator ProcessMovementCoroutine()
    {
        var toProcess = new List<(TileUnitGroupData group, TileUnitGroupControl owner)>();

        var allControls = GetAllControls();  // cached — no FindObjectsOfType per turn
        foreach (var control in allControls)
        {
            var groups = control.Groups;
            if (groups == null) continue;

            for (int i = 0; i < groups.Count; i++)
            {
                var g = groups[i];
                if (g == null) continue;

                if (g.plannedPathGridPositions != null &&
                    g.plannedPathGridPositions.Count > 0 &&
                    g.currentPathIndex < g.plannedPathGridPositions.Count)
                {
                    toProcess.Add((g, control));
                }
            }
        }

        if (toProcess.Count == 0)
            yield break;

        int processedThisFrame = 0;

        foreach (var entry in toProcess)
        {
            ProcessGroupMovementForTurn(entry.group, entry.owner);

            processedThisFrame++;
            if (processedThisFrame >= Mathf.Max(1, groupsPerBatch))
            {
                processedThisFrame = 0;
                yield return null;
            }
        }
    }

    private void ProcessGroupMovementForTurn(TileUnitGroupData group, TileUnitGroupControl owner)
    {
        if (group == null || owner == null)
            return;

        if (group.plannedPathGridPositions == null ||
            group.plannedPathGridPositions.Count == 0 ||
            group.currentPathIndex >= group.plannedPathGridPositions.Count)
        {
            return;
        }

        if (group.plannedStepTurnCosts == null ||
            group.plannedStepTurnCosts.Count != group.plannedPathGridPositions.Count)
        {
            //Debug.LogWarning(
                //$"[UnitGroupMovementManager] Route/cost mismatch on group {group.groupId}. Clearing route.");
            ClearRouteOnGroup(group);
            return;
        }

        if (group.remainingTurnCostOnCurrentStep <= 0f)
        {
            group.remainingTurnCostOnCurrentStep =
                Mathf.Max(0.1f, group.plannedStepTurnCosts[group.currentPathIndex]);
        }

        group.remainingTurnCostOnCurrentStep -= 1f;

        if (group.remainingTurnCostOnCurrentStep > 0f)
        {
            //Debug.Log(
                //$"[UnitGroupMovementManager] Group {group.groupId} has " +
                //$"{group.remainingTurnCostOnCurrentStep:0.0} turns left on step index {group.currentPathIndex}.");

            NotifyGroupMovementUpdated(group);
            return;
        }

        var currentTile = ResolveTileForOwner(owner);
        if (currentTile == null)
        {
            //Debug.LogWarning(
                //$"[UnitGroupMovementManager] Could not resolve current tile for owner {owner.name}. Clearing route for group {group.groupId}.");
            ClearRouteOnGroup(group);
            return;
        }

        Vector2Int nextGrid = group.plannedPathGridPositions[group.currentPathIndex];

        TileControl nextTile = null;
        foreach (var neigh in GetNeighbors(currentTile))
        {
            if (neigh == null) continue;

            if (neigh.GetGridPosition() == nextGrid)
            {
                nextTile = neigh;
                break;
            }
        }

        if (nextTile == null)
        {
            //Debug.LogWarning(
                //$"[UnitGroupMovementManager] Could not find neighbour tile at {nextGrid} " +
                //$"from {currentTile.name} for group {group.groupId}. Clearing route.");
            ClearRouteOnGroup(group);
            return;
        }

        TileUnitGroupControl nextOwner = null;
        if (UnitGroupActionManager.Instance != null)
            UnitGroupActionManager.Instance.TryGetUnitControlForTile(nextTile, out nextOwner);

        if (nextOwner == null)
        {
            nextOwner = nextTile.GetComponentInChildren<TileUnitGroupControl>(true);
            if (nextOwner == null && nextTile.transform.parent != null)
                nextOwner = nextTile.transform.parent.GetComponentInChildren<TileUnitGroupControl>(true);
        }

        if (nextOwner == null)
        {
            //Debug.LogWarning(
                //$"[UnitGroupMovementManager] Tile {nextTile.name} has no TileUnitGroupControl. Clearing route.");
            ClearRouteOnGroup(group);
            return;
        }

        owner.MoveGroupTo(group, nextOwner);
        //Debug.Log(
            //$"[UnitGroupMovementManager] Group {group.groupId} moved one step to {nextTile.name} " +
            //$"(step {group.currentPathIndex + 1}/{group.plannedPathGridPositions.Count}).");

        bool groupDestroyedByHazard = TryApplyUndiscoveredTileHazard(group, nextOwner, nextTile);
        if (groupDestroyedByHazard)
        {
            ClearRouteOnGroup(group);
            return;
        }

        group.currentPathIndex++;

        if (group.currentPathIndex >= group.plannedPathGridPositions.Count)
        {
            if (group.isPatrolling &&
                group.patrolLoopGridPositions != null &&
                group.patrolLoopGridPositions.Count > 0 &&
                group.patrolLoopStepTurnCosts != null &&
                group.patrolLoopStepTurnCosts.Count == group.patrolLoopGridPositions.Count)
            {
                group.currentPathIndex = 0;

                if (!ReferenceEquals(group.plannedPathGridPositions, group.patrolLoopGridPositions))
                {
                    group.plannedPathGridPositions.Clear();
                    group.plannedStepTurnCosts.Clear();
                    group.plannedPathGridPositions.AddRange(group.patrolLoopGridPositions);
                    group.plannedStepTurnCosts.AddRange(group.patrolLoopStepTurnCosts);
                }

                group.remainingTurnCostOnCurrentStep =
                    Mathf.Max(0.1f, group.patrolLoopStepTurnCosts[0]);

                //Debug.Log(
                    //$"[UnitGroupMovementManager] Group {group.groupId} completed a patrol loop; restarting from beginning.");

                NotifyGroupMovementUpdated(group);
            }
            else
            {
                PostMovementCompletedNotification(group, nextTile);
                ClearRouteOnGroup(group);
            }

            return;
        }
        else
        {
            group.remainingTurnCostOnCurrentStep =
                Mathf.Max(0.1f, group.plannedStepTurnCosts[group.currentPathIndex]);

            NotifyGroupMovementUpdated(group);
        }
    }

    private static void PostMovementCompletedNotification(TileUnitGroupData group, TileControl destinationTile)
    {
        if (NotificationManager.Instance == null) return;
        string groupName = !string.IsNullOrWhiteSpace(group.groupName) ? group.groupName : "Unit Group";
        string unitName  = group.unitType != null && !string.IsNullOrWhiteSpace(group.unitType.unitName)
            ? group.unitType.unitName : "Unit";
        Vector3 pos = destinationTile != null ? destinationTile.transform.position : default;
        string title, message;
        if (NotificationMessageCrafterManager.Instance != null)
            (title, message) = NotificationMessageCrafterManager.Instance.CraftUnitMovementCompleted(groupName, unitName);
        else
            (title, message) = ("Movement Complete", $"{groupName} has reached their destination.");
        NotificationManager.Instance.AddNotification(NotificationType.UnitMovementCompleted, title, message, pos);
    }

    private void ClearRouteOnGroup(TileUnitGroupData group)
    {
        if (group == null) return;

        if (group.plannedPathGridPositions != null)
            group.plannedPathGridPositions.Clear();
        if (group.plannedStepTurnCosts != null)
            group.plannedStepTurnCosts.Clear();

        if (group.patrolLoopGridPositions != null)
            group.patrolLoopGridPositions.Clear();
        if (group.patrolLoopStepTurnCosts != null)
            group.patrolLoopStepTurnCosts.Clear();

        group.currentPathIndex = 0;
        group.remainingTurnCostOnCurrentStep = 0f;
        group.isPatrolling = false;

        NotifyGroupMovementUpdated(group);
    }

    private TileControl ResolveTileForOwner(TileUnitGroupControl owner)
    {
        if (owner == null)
            return null;

        if (UnitGroupActionManager.Instance != null)
        {
            var resolved = UnitGroupActionManager.Instance.ResolveTileForUnitControl(owner);
            if (resolved != null)
                return resolved;
        }

        var tile = owner.GetComponent<TileControl>();
        if (tile != null)
            return tile;

        tile = owner.GetComponentInParent<TileControl>(true);
        if (tile != null)
            return tile;

        tile = owner.GetComponentInChildren<TileControl>(true);
        if (tile != null)
            return tile;

        if (owner.transform.parent != null)
        {
            tile = owner.transform.parent.GetComponentInChildren<TileControl>(true);
            if (tile != null)
                return tile;
        }

        return null;
    }

    private IEnumerable<TileControl> GetNeighbors(TileControl originTile)
    {
        if (originTile == null)
            yield break;

        var box = originTile.GetComponent<BoxCollider>();
        if (box == null)
            box = originTile.GetComponentInChildren<BoxCollider>(true);

        if (box == null)
        {
            //Debug.LogWarning(
                //$"[UnitGroupMovementManager] GetNeighbors: Tile {originTile.name} has no BoxCollider. " +
                //"Neighbour detection will be empty.");
            yield break;
        }

        Vector3 center = box.bounds.center;
        Vector3 halfExtents = box.bounds.extents * neighbourDetectionMultiplier;

        Collider[] hits = Physics.OverlapBox(
            center,
            halfExtents,
            Quaternion.identity,
            tileLayerMask
        );

        var seen = new HashSet<TileControl>();

        foreach (var hit in hits)
        {
            if (hit == null) continue;

            var tile = hit.GetComponentInParent<TileControl>();
            if (tile == null) continue;
            if (tile == originTile) continue;
            if (!seen.Add(tile)) continue;

            yield return tile;
        }
    }

    private void ClearAllMoveHereButtons()
    {
        var all = GameObject.FindObjectsOfType<TileMovementUI>(true);
        foreach (var ui in all)
            ui.Hide();
    }

    private void NotifyGroupMovementUpdated(TileUnitGroupData group)
    {
        if (group == null) return;

        var markers = GetAllMarkers();  // cached — no FindObjectsOfType per call
        for (int i = 0; i < markers.Count; i++)
        {
            var m = markers[i];
            if (m == null) continue;

            if (m.BoundGroup == group)
            {
                m.Refresh();
                break;
            }
        }
    }

    private bool IsTileAllowedForPatrol(TileControl tile)
    {
        if (tile == null) return false;

        if (tile.tileContentType == TileContentType.Building)
            return true;

        if (tile.tileContentType == TileContentType.Environment)
        {
            var status = tile.GetComponentInChildren<EnvironmentStatus>(true);
            if (status != null)
                return status.IsDiscovered;

            var envCtrl = tile.GetComponentInChildren<EnvironmentControl>(true);
            if (envCtrl != null)
                return envCtrl.IsDiscovered;

            return false;
        }

        return false;
    }

    private bool TryComputeHazardChances(
        TileControl destTile,
        TileUnitGroupData group,
        out EnvironmentControl envCtrl,
        out float hazardChance01,
        out float damageOutcomeChance01,
        out float fatalOutcomeChance01)
    {
        envCtrl = null;
        hazardChance01 = 0f;
        damageOutcomeChance01 = 0f;
        fatalOutcomeChance01 = 0f;

        if (destTile == null || group == null)
            return false;

        if (destTile.tileContentType != TileContentType.Environment)
            return false;

        envCtrl = destTile.GetComponentInChildren<EnvironmentControl>(true);
        if (envCtrl == null || envCtrl.IsDiscovered)
            return false;

        hazardChance01 = Mathf.Clamp01(envCtrl.DiscoveryFailureChance / 100f);
        if (hazardChance01 <= 0f)
            return false;

        float healthComponent = Mathf.Max(1f, group.maxHealth);
        float defenceComponent = 0f;

        float toughnessScore = (healthComponent + defenceComponent) * Mathf.Max(1, group.unitCount);

        float normalizedToughness = toughnessScore / (toughnessScore + 100f);
        float damageGivenHazard = Mathf.Clamp01(normalizedToughness);
        float fatalGivenHazard = 1f - damageGivenHazard;

        damageOutcomeChance01 = hazardChance01 * damageGivenHazard;
        fatalOutcomeChance01 = hazardChance01 * fatalGivenHazard;

        return true;
    }

    private bool TryApplyUndiscoveredTileHazard(
        TileUnitGroupData group,
        TileUnitGroupControl newOwner,
        TileControl destTile)
    {
        if (group == null || newOwner == null || destTile == null)
            return false;

        EnvironmentControl envCtrl;
        float hazardChance01;
        float dmgOutcome01;
        float fatalOutcome01;

        if (!TryComputeHazardChances(destTile, group,
                out envCtrl,
                out hazardChance01,
                out dmgOutcome01,
                out fatalOutcome01))
        {
            return false;
        }

        float roll = Random.value;
        if (roll > hazardChance01)
        {
            //Debug.Log(
                //$"[UnitGroupMovementManager] Group {group.groupId} crossed undiscovered '{envCtrl.environmentName}' safely " +
                //$"(roll={roll:0.00} vs hazard={hazardChance01:0.00}).");
            return false;
        }

        float fatalGivenHazard = Mathf.Approximately(hazardChance01, 0f)
            ? 0f
            : Mathf.Clamp01(fatalOutcome01 / hazardChance01);

        bool isFatalEvent = Random.value < fatalGivenHazard;

        if (isFatalEvent)
        {
            int maxCasualties = Mathf.Max(1, group.unitCount);
            int casualties = Random.Range(1, maxCasualties + 1);

            ApplyFatalCasualtiesToGroup(group, newOwner, casualties, envCtrl, hazardChance01);
            return group.unitCount <= 0;
        }
        else
        {
            ApplyNonFatalDamageToGroup(group, envCtrl, hazardChance01);
            newOwner.RefreshMarker(group);
            return false;
        }
    }

    private void ApplyNonFatalDamageToGroup(
        TileUnitGroupData group,
        EnvironmentControl envCtrl,
        float hazardStrength01)
    {
        if (group == null) return;

        float minFrac = 0.10f;
        float maxFrac = 0.40f;
        float frac = Mathf.Lerp(minFrac, maxFrac, Mathf.Clamp01(hazardStrength01));

        int damage = Mathf.Max(1, Mathf.RoundToInt(group.maxHealth * frac));

        int oldHealth = group.currentHealth;
        group.currentHealth = Mathf.Max(1, group.currentHealth - damage);

        //Debug.Log(
            //$"[UnitGroupMovementManager] Group {group.groupId} took {damage} damage " +
            //$"crossing undiscovered '{envCtrl.environmentName}' " +
            //$"(health {oldHealth} → {group.currentHealth}/{group.maxHealth}).");
    }

    private void ApplyFatalCasualtiesToGroup(
        TileUnitGroupData group,
        TileUnitGroupControl owner,
        int casualties,
        EnvironmentControl envCtrl,
        float hazardStrength01)
    {
        if (group == null || owner == null) return;

        casualties = Mathf.Clamp(casualties, 1, group.unitCount);

        var popMgr = PlayersPopulationManager.Instance;

        if (!string.IsNullOrEmpty(group.populationReservationId) && popMgr != null)
        {
            int killFromReservation = casualties;

            if (group.reservedPopulation > 0)
                killFromReservation = Mathf.Min(killFromReservation, group.reservedPopulation);

            if (killFromReservation > 0)
            {
                popMgr.ApplyPenaltyFromReservation(group.populationReservationId, killFromReservation);
                group.reservedPopulation = Mathf.Max(0, group.reservedPopulation - killFromReservation);
            }
        }

        group.unitCount -= casualties;

        //Debug.Log(
            //$"[UnitGroupMovementManager] Group {group.groupId} lost {casualties} " +
            //$"people crossing undiscovered '{envCtrl.environmentName}' " +
            //$"(units left: {group.unitCount}).");

        if (group.unitCount <= 0)
        {
            //Debug.Log(
                //$"[UnitGroupMovementManager] Group {group.groupId} was wiped out by hazards in '{envCtrl.environmentName}'.");

            if (!string.IsNullOrEmpty(group.populationReservationId) && popMgr != null && group.reservedPopulation > 0)
            {
                popMgr.ApplyPenaltyFromReservation(group.populationReservationId, group.reservedPopulation);
                group.reservedPopulation = 0;
            }

            if (!string.IsNullOrEmpty(group.populationReservationId) && popMgr != null)
            {
                popMgr.ReleaseReservation(group.populationReservationId);
                group.populationReservationId = null;
            }

            owner.RemoveGroupDueToFatalities(group);
        }
        else
        {
            owner.RefreshMarker(group);
        }
    }

    public void CancelMovementForGroup(TileUnitGroupData group)
    {
        ClearRouteOnGroup(group);
    }

    public void SetEndMovementButton(Button newEndMovementButton)
    {
        if (newEndMovementButton == null)
            return;

        endMovementButton = newEndMovementButton;

        endMovementButton.onClick.RemoveAllListeners();
        endMovementButton.onClick.AddListener(OnConfirmMovementClicked);
        endMovementButton.gameObject.SetActive(false);
    }

    public void ProcessMovementTickForGroup(TileUnitGroupData group, TileUnitGroupControl owner)
    {
        ProcessGroupMovementForTurn(group, owner);
    }

    public bool GroupHasRoute(TileUnitGroupData g)
    {
        return g != null &&
               g.plannedPathGridPositions != null &&
               g.plannedPathGridPositions.Count > 0 &&
               g.currentPathIndex < g.plannedPathGridPositions.Count;
    }
}
