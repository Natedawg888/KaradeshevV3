using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class TileInteraction : MonoBehaviour
{
    public static TileInteraction Instance { get; private set; }
    public static TileControl SelectedTile => Instance != null ? Instance._currentSelected : null;

    [Header("Raycast Settings")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private LayerMask tileLayerMask = ~0;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

    [Tooltip("Max colliders to consider under cursor. Increase if you have lots of stacked colliders.")]
    [SerializeField] private int maxRayHits = 64;

    [Header("Panels")]
    [SerializeField] private UndiscoveredTilePanelControl undiscoveredPanel;
    [SerializeField] private DiscoveredTilePanelControl discoveredPanel;
    [SerializeField] private BuildingPanelControl buildingPanel;
    [SerializeField] private BuildingDamagedPanelControl buildingDamagedPanel;
    [SerializeField] private BuildingDestroyedPanelControl buildingDestroyedPanel;
    [SerializeField] private TaskFailedPanelControl taskFailedPanel;

    [Header("Collected Goods Panel")]
    [SerializeField] private CollectedGoodsPanelControl collectedGoodsPanel;

    [Header("Tap Filtering")]
    [SerializeField, Min(1f)] private float tapDragThresholdPixels = 12f;

    private bool _pointerTrackingActive;
    private bool _pointerStartedOverUI;
    private bool _pointerMovedTooFar;
    private int _activePointerId = int.MinValue;
    private Vector2 _pointerPressScreenPos;

    public event Action<TileControl> OnTileSelected;
    public event Action<TileControl> OnTileDeselected;

    private TileControl _currentSelected;
    private CameraControl _cameraControl;

    private bool _selectionEnabled = true;

    // Panel open flags (used to only deselect when everything is closed)
    private bool _undiscoveredOpen;
    private bool _discoveredOpen;
    private bool _collectedOpen;
    private bool _buildingOpen;
    private bool _buildingDamagedOpen;
    private bool _buildingDestroyedOpen;
    private bool _taskFailedOpen;

    // Unit-only mode state
    private bool _selectionEnabledBeforeUnitMode = true;
    private bool _unitModeActive = false;

    // Building status subscription (live-route building panels)
    private BuildingStatus _subscribedStatus;

    private bool _suppressAutoDeselect = false;

    // NonAlloc buffers
    private RaycastHit[] _rayHits;

    // Cached UI raycast (fallback only)
    private PointerEventData _uiEventData;
    private readonly List<RaycastResult> _uiResults = new List<RaycastResult>(16);

    [Header("Selection Restrictions")]
    [SerializeField] private bool restrictToFrontierSelection = true;

    [Tooltip("Expands the tile bounds when checking neighbors. 1.05–1.2 is typical.")]
    [SerializeField, Range(1f, 2f)] private float neighborBoundsExpand = 1.15f;

    [Tooltip("Max colliders to consider when checking adjacent tiles.")]
    [SerializeField] private int maxNeighborHits = 64;

    // NonAlloc neighbor buffer
    private Collider[] _neighborHits;

    public static TileInteraction GetInstance() => Instance;
    public TileControl GetCurrentSelected() => _currentSelected;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (maxRayHits < 8) maxRayHits = 8;
        _rayHits = new RaycastHit[maxRayHits];

        if (maxNeighborHits < 8) maxNeighborHits = 8;
        _neighborHits = new Collider[maxNeighborHits];

        SubscribePanelEvents();
    }

    private void OnDestroy()
    {
        UnsubscribePanelEvents();

        if (Instance == this)
            Instance = null;
    }

    private void OnEnable()
    {
        WorldCanvasMode.OnChanged += HandleWorldCanvasModeChanged;
        HandleWorldCanvasModeChanged(WorldCanvasMode.UnitsOnly);
    }

    private void OnDisable()
    {
        WorldCanvasMode.OnChanged -= HandleWorldCanvasModeChanged;
    }

    private void Start()
    {
        if (targetCamera == null) targetCamera = Camera.main;

        _cameraControl = FindObjectOfType<CameraControl>();
        if (_cameraControl == null)
            //Debug.LogWarning($"{nameof(TileInteraction)}: {nameof(CameraControl)} not found in scene.");
    }

    private void OnValidate()
    {
        if (maxRayHits < 8) maxRayHits = 8;
        if (_rayHits == null || _rayHits.Length != maxRayHits)
            _rayHits = new RaycastHit[maxRayHits];

        if (maxNeighborHits < 8) maxNeighborHits = 8;
        if (_neighborHits == null || _neighborHits.Length != maxNeighborHits)
            _neighborHits = new Collider[maxNeighborHits];
    }

    private void Update()
    {
        if (!_selectionEnabled) return;

        UpdatePointerTapFilter();

        if (TryGetPointerReleased(out Vector2 screenPos))
        {
            bool shouldIgnoreRelease =
                _pointerStartedOverUI ||
                _pointerMovedTooFar ||
                (_cameraControl != null && _cameraControl.IsDragging());

            ResetPointerTapFilter();

            if (shouldIgnoreRelease) return;
            if (IsScreenPosOverUI(screenPos)) return;

            TrySelectTileAtScreenPos(screenPos);
        }

        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            DeselectCurrent();
    }

    private void UpdatePointerTapFilter()
    {
        if (TryGetPointerPressed(out Vector2 pressedPos, out int pressedPointerId))
        {
            _pointerTrackingActive = true;
            _activePointerId = pressedPointerId;
            _pointerPressScreenPos = pressedPos;
            _pointerMovedTooFar = false;
            _pointerStartedOverUI = IsPointerOverUI(pressedPointerId, pressedPos);
            return;
        }

        if (!_pointerTrackingActive) return;

        if (TryGetPointerHeld(out Vector2 currentPos, out int heldPointerId))
        {
            if (heldPointerId == _activePointerId)
            {
                float thresholdSq = tapDragThresholdPixels * tapDragThresholdPixels;
                if ((currentPos - _pointerPressScreenPos).sqrMagnitude >= thresholdSq)
                    _pointerMovedTooFar = true;
            }
        }
    }

    private void ResetPointerTapFilter()
    {
        _pointerTrackingActive = false;
        _pointerStartedOverUI = false;
        _pointerMovedTooFar = false;
        _activePointerId = int.MinValue;
        _pointerPressScreenPos = default;
    }

    private bool TryGetPointerPressed(out Vector2 screenPos, out int pointerId)
    {
        if (Touchscreen.current != null &&
            Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            screenPos = Touchscreen.current.primaryTouch.position.ReadValue();
            pointerId = Touchscreen.current.primaryTouch.touchId.ReadValue();
            return true;
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            screenPos = Mouse.current.position.ReadValue();
            pointerId = -1;
            return true;
        }

        screenPos = default;
        pointerId = int.MinValue;
        return false;
    }

    private bool TryGetPointerHeld(out Vector2 screenPos, out int pointerId)
    {
        if (Touchscreen.current != null &&
            Touchscreen.current.primaryTouch.press.isPressed)
        {
            screenPos = Touchscreen.current.primaryTouch.position.ReadValue();
            pointerId = Touchscreen.current.primaryTouch.touchId.ReadValue();
            return true;
        }

        if (Mouse.current != null && Mouse.current.leftButton.isPressed)
        {
            screenPos = Mouse.current.position.ReadValue();
            pointerId = -1;
            return true;
        }

        screenPos = default;
        pointerId = int.MinValue;
        return false;
    }

    private bool TryGetPointerReleased(out Vector2 screenPos)
    {
        // Touch (new input system)
        if (Touchscreen.current != null &&
            Touchscreen.current.primaryTouch.press.wasReleasedThisFrame)
        {
            screenPos = Touchscreen.current.primaryTouch.position.ReadValue();
            return true;
        }

        // Mouse
        if (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame)
        {
            screenPos = Mouse.current.position.ReadValue();
            return true;
        }

        screenPos = default;
        return false;
    }

    // -------------------------
    // Public controls
    // -------------------------

    private int _selectionVersion = 0;

    private bool _baseSelectionEnabled = true;              // “normal” desired state
    private readonly HashSet<int> _selectionLocks = new(); // any active lock keeps selection off

    private void RecomputeSelectionEnabled()
    {
        // Selection is allowed only if:
        // - base wants it enabled
        // - not in UnitsOnly
        // - nobody is holding a lock
        _selectionEnabled = _baseSelectionEnabled && !_unitModeActive && _selectionLocks.Count == 0;
    }

    // Replace your SetSelectionEnabled with this:
    public static void SetSelectionEnabled(bool enabled)
    {
        if (Instance == null) return;
        Instance._baseSelectionEnabled = enabled;
        Instance.RecomputeSelectionEnabled();
    }

    // NEW: lock API (works for any system: building panels, move mode, etc.)
    public static void AcquireSelectionLock(UnityEngine.Object owner)
    {
        if (Instance == null || owner == null) return;
        Instance._selectionLocks.Add(owner.GetInstanceID());
        Instance.RecomputeSelectionEnabled();
    }

    public static void ReleaseSelectionLock(UnityEngine.Object owner)
    {
        if (Instance == null || owner == null) return;
        Instance._selectionLocks.Remove(owner.GetInstanceID());
        Instance.RecomputeSelectionEnabled();
    }

    public void TemporarilyDisableSelection(float delaySeconds)
    {
        if (delaySeconds <= 0f) return;
        StartCoroutine(DisableSelectionCoroutine(delaySeconds));
    }

    private IEnumerator DisableSelectionCoroutine(float delay)
    {
        // just force off briefly, then recompute (don’t force ON)
        _selectionEnabled = false;
        yield return new WaitForSeconds(delay);
        RecomputeSelectionEnabled();
    }

    public void EnableSelectionAfter(float delaySeconds)
    {
        StartCoroutine(EnableSelectionAfterCoroutine(delaySeconds, _selectionVersion));
    }

    private IEnumerator EnableSelectionAfterCoroutine(float delay, int versionAtStart)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (versionAtStart != _selectionVersion) yield break;

        // Respect UnitsOnly mode (it should stay disabled)
        if (!_unitModeActive)
        {
            _selectionEnabled = true;
            _selectionVersion++;
        }
    }

    public static void ReleaseSelectionLockNextFrame(UnityEngine.Object owner)
    {
        if (Instance == null || owner == null) return;
        Instance.StartCoroutine(Instance.ReleaseLockNextFrameRoutine(owner.GetInstanceID()));
    }

    private IEnumerator ReleaseLockNextFrameRoutine(int ownerId)
    {
        yield return null; // next frame
        _selectionLocks.Remove(ownerId);
        RecomputeSelectionEnabled();
    }

    // -------------------------
    // Mode handling
    // -------------------------


    // Update UnitsOnly handler to use recompute:
    private void HandleWorldCanvasModeChanged(bool unitsOnly)
    {
        if (unitsOnly == _unitModeActive) return;

        _unitModeActive = unitsOnly;

        if (unitsOnly)
            DeselectCurrent();

        RecomputeSelectionEnabled();
    }

    // -------------------------
    // Selection flow (NonAlloc)
    // -------------------------

    private void TrySelectTileAtScreenPos(Vector2 screenPos)
    {
        if (targetCamera == null) return;

        Ray ray = targetCamera.ScreenPointToRay(screenPos);

        int hitCount = Physics.RaycastNonAlloc(
            ray, _rayHits, Mathf.Infinity, tileLayerMask, triggerInteraction
        );

        if (hitCount <= 0)
        {
            DeselectCurrent();
            return;
        }

        TileControl bestTile = null;
        float bestDist = float.PositiveInfinity;

        for (int i = 0; i < hitCount; i++)
        {
            var hit = _rayHits[i];
            var col = hit.collider;
            if (col == null) continue;

            var tile = TryGetTileFromCollider(col);
            if (tile == null) continue;

            if (hit.distance < bestDist)
            {
                bestDist = hit.distance;
                bestTile = tile;
            }
        }

        if (bestTile == null || !bestTile.IsInteractable())
        {
            DeselectCurrent();
            return;
        }

        // ✅ Tap same tile again -> deselect
        if (_currentSelected == bestTile)
        {
            DeselectCurrent();
            return;
        }

        if (restrictToFrontierSelection && !CanSelectTile(bestTile))
            return;

        SelectTile(bestTile);
    }

    private static TileControl TryGetTileFromCollider(Collider col)
    {
        if (col.TryGetComponent<TileControl>(out var tc))
            return tc;

        Transform t = col.transform;
        while (t != null)
        {
            if (t.TryGetComponent<TileControl>(out tc))
                return tc;

            t = t.parent;
        }

        return null;
    }

    private void SelectTile(TileControl tile)
    {
        if (tile == null) return;

        if (_currentSelected != null && _currentSelected != tile)
        {
            _currentSelected.DeselectTile();
            OnTileDeselected?.Invoke(_currentSelected);
        }

        _currentSelected = tile;
        _currentSelected.SelectTile();
        OnTileSelected?.Invoke(_currentSelected);

        ResetOpenFlags();

        var building = tile.GetComponentInChildren<BuildingControl>(true);
        if (building != null)
        {
            SubscribeToBuildingStatus(building);
            return;
        }

        if (tile.tileContentType != TileContentType.Environment)
        {
            HideAllPanels();
            return;
        }

        var envCtrl = tile.EnvironmentControl;
        if (envCtrl == null)
        {
            //Debug.LogWarning($"{nameof(TileInteraction)}: selected tile claims Environment but is missing {nameof(EnvironmentControl)}.");
            HideAllPanels();
            return;
        }

        TryOpenNextTaskFailed(envCtrl);

        if (envCtrl.HasLootReady) OpenCollectedGoods(envCtrl);

        if (envCtrl.IsDiscovered) OpenDiscovered(envCtrl);
        else OpenUndiscovered(envCtrl);
    }

    public void DeselectCurrent()
    {
        if (_currentSelected == null) return;

        _currentSelected.DeselectTile();
        OnTileDeselected?.Invoke(_currentSelected);
        _currentSelected = null;

        UnsubscribeFromBuildingStatus();

        HideAllPanels();
        ResetOpenFlags();
    }

    private void ResetOpenFlags()
    {
        _undiscoveredOpen = false;
        _discoveredOpen = false;
        _collectedOpen = false;
        _buildingOpen = false;
        _buildingDamagedOpen = false;
        _buildingDestroyedOpen = false;
        _taskFailedOpen = false;
    }

    // -------------------------
    // Panel open helpers
    // -------------------------

    private void OpenCollectedGoods(EnvironmentControl envCtrl)
    {
        if (collectedGoodsPanel == null)
        {
            //Debug.LogWarning($"{nameof(TileInteraction)}: {nameof(collectedGoodsPanel)} is not assigned in the inspector.");
            return;
        }

        collectedGoodsPanel.Show(envCtrl);
        _collectedOpen = true;
    }

    private void OpenDiscovered(EnvironmentControl envCtrl)
    {
        if (discoveredPanel == null) return;
        discoveredPanel.Show(envCtrl);
        _discoveredOpen = true;
    }

    private void OpenUndiscovered(EnvironmentControl envCtrl)
    {
        if (undiscoveredPanel == null) return;
        undiscoveredPanel.Show(envCtrl);
        _undiscoveredOpen = true;
    }

    private void OpenBuilding(BuildingControl bc, TileControl tile)
    {
        if (buildingPanel == null) return;
        buildingPanel.Show(bc, tile);
        _buildingOpen = true;
    }

    private void OpenBuildingDamaged(BuildingControl bc, TileControl tile)
    {
        if (buildingDamagedPanel == null) { OpenBuilding(bc, tile); return; }
        buildingDamagedPanel.Show(bc, tile);
        _buildingDamagedOpen = true;
    }

    private void OpenBuildingDestroyed(BuildingControl bc, TileControl tile)
    {
        if (buildingDestroyedPanel == null) { OpenBuilding(bc, tile); return; }
        buildingDestroyedPanel.Show(bc, tile);
        _buildingDestroyedOpen = true;
    }

    private void HideAllPanels()
    {
        _suppressAutoDeselect = true;
        try
        {
            undiscoveredPanel?.Hide();
            discoveredPanel?.Hide();
            collectedGoodsPanel?.Hide();
            buildingPanel?.Hide();
            buildingDamagedPanel?.Hide();
            buildingDestroyedPanel?.Hide();
            taskFailedPanel?.Hide();
            _taskFailedOpen = false;
        }
        finally
        {
            _suppressAutoDeselect = false;
        }
    }

    private void TryFinishSelectionWhenAllClosed()
    {
        if (_suppressAutoDeselect) return;

        if (!_undiscoveredOpen && !_discoveredOpen && !_collectedOpen &&
            !_buildingOpen && !_buildingDamagedOpen && !_buildingDestroyedOpen &&
            !_taskFailedOpen)
        {
            DeselectCurrent();
        }
    }

    // -------------------------
    // Panel close events (subscribed once)
    // -------------------------

    private void SubscribePanelEvents()
    {
        if (undiscoveredPanel != null) undiscoveredPanel.OnClose += HandleUndiscoveredClosed;
        if (discoveredPanel != null) discoveredPanel.OnClose += HandleDiscoveredClosed;
        if (collectedGoodsPanel != null) collectedGoodsPanel.OnClose += HandleCollectedClosed;
        if (buildingPanel != null) buildingPanel.OnClose += HandleBuildingClosed;
        if (buildingDamagedPanel != null) buildingDamagedPanel.OnClose += HandleBuildingDamagedClosed;
        if (buildingDestroyedPanel != null) buildingDestroyedPanel.OnClose += HandleBuildingDestroyedClosed;
        if (taskFailedPanel != null) taskFailedPanel.OnClose += HandleTaskFailedClosed;
    }

    private void UnsubscribePanelEvents()
    {
        if (undiscoveredPanel != null) undiscoveredPanel.OnClose -= HandleUndiscoveredClosed;
        if (discoveredPanel != null) discoveredPanel.OnClose -= HandleDiscoveredClosed;
        if (collectedGoodsPanel != null) collectedGoodsPanel.OnClose -= HandleCollectedClosed;
        if (buildingPanel != null) buildingPanel.OnClose -= HandleBuildingClosed;
        if (buildingDamagedPanel != null) buildingDamagedPanel.OnClose -= HandleBuildingDamagedClosed;
        if (buildingDestroyedPanel != null) buildingDestroyedPanel.OnClose -= HandleBuildingDestroyedClosed;
        if (taskFailedPanel != null) taskFailedPanel.OnClose -= HandleTaskFailedClosed;
    }

    private void HandleCollectedClosed() { _collectedOpen = false; TryFinishSelectionWhenAllClosed(); }
    private void HandleDiscoveredClosed() { _discoveredOpen = false; TryFinishSelectionWhenAllClosed(); }
    private void HandleUndiscoveredClosed() { _undiscoveredOpen = false; TryFinishSelectionWhenAllClosed(); }
    private void HandleBuildingClosed() { _buildingOpen = false; TryFinishSelectionWhenAllClosed(); }
    private void HandleBuildingDamagedClosed() { _buildingDamagedOpen = false; TryFinishSelectionWhenAllClosed(); }
    private void HandleBuildingDestroyedClosed() { _buildingDestroyedOpen = false; TryFinishSelectionWhenAllClosed(); }

    // -------------------------
    // Building state routing
    // -------------------------

    private void SubscribeToBuildingStatus(BuildingControl building)
    {
        var status = building.GetComponent<BuildingStatus>();
        if (status == null)
        {
            OpenBuilding(building, _currentSelected);
            return;
        }

        UnsubscribeFromBuildingStatus();

        _subscribedStatus = status;
        _subscribedStatus.OnStateChanged += HandleBuildingStateChanged;

        RouteBuildingPanelForState(building, _currentSelected, _subscribedStatus.CurrentState);
    }

    private void UnsubscribeFromBuildingStatus()
    {
        if (_subscribedStatus == null) return;
        _subscribedStatus.OnStateChanged -= HandleBuildingStateChanged;
        _subscribedStatus = null;
    }

    private void HandleBuildingStateChanged(BuildingState state)
    {
        if (_currentSelected == null) return;

        var bc = _currentSelected.GetComponentInChildren<BuildingControl>(true);
        if (bc == null) return;

        RouteBuildingPanelForState(bc, _currentSelected, state);
    }

    private void RouteBuildingPanelForState(BuildingControl bc, TileControl tile, BuildingState state)
    {
        _suppressAutoDeselect = true;
        try
        {
            // These Hide() calls may trigger OnClose events
            buildingPanel?.Hide();
            buildingDamagedPanel?.Hide();
            buildingDestroyedPanel?.Hide();

            _buildingOpen = _buildingDamagedOpen = _buildingDestroyedOpen = false;
        }
        finally
        {
            _suppressAutoDeselect = false;
        }

        switch (state)
        {
            case BuildingState.Normal: OpenBuilding(bc, tile); break;
            case BuildingState.Damaged: OpenBuildingDamaged(bc, tile); break;
            case BuildingState.Destroyed: OpenBuildingDestroyed(bc, tile); break;
        }
    }

    private void HandleTaskFailedClosed()
    {
        _taskFailedOpen = false;

        // If more failures are queued on this selected tile, show the next one
        if (_currentSelected != null && _currentSelected.tileContentType == TileContentType.Environment)
        {
            var env = _currentSelected.EnvironmentControl;
            if (env != null)
                TryOpenNextTaskFailed(env);
        }

        TryFinishSelectionWhenAllClosed();
    }

    // -------------------------
    // UI hit test (finger-safe)
    // -------------------------

    private bool IsPointerOverUI(int pointerId, Vector2 screenPos)
    {
        if (EventSystem.current == null) return false;

        // Touch: MUST use fingerId
        if (pointerId >= 0)
        {
            if (EventSystem.current.IsPointerOverGameObject(pointerId))
                return true;
        }
        else
        {
            if (EventSystem.current.IsPointerOverGameObject())
                return true;
        }

        // Fallback raycast (cached objects, no allocations)
        if (_uiEventData == null)
            _uiEventData = new PointerEventData(EventSystem.current);

        _uiEventData.position = screenPos;

        _uiResults.Clear();
        EventSystem.current.RaycastAll(_uiEventData, _uiResults);
        return _uiResults.Count > 0;
    }

    private bool CanSelectTile(TileControl tile)
    {
        if (tile == null) return false;
        if (!tile.IsInteractable()) return false;

        // Building tiles are always selectable
        if (tile.tileContentType == TileContentType.Building)
            return true;

        // Only Environment tiles can be selected (in this rule set)
        if (tile.tileContentType != TileContentType.Environment)
            return false;

        // Discovered environments are selectable
        if (IsTileDiscovered(tile))
            return true;

        // Undiscovered environments: only selectable if adjacent to a building OR discovered environment
        return HasAdjacentSelectableNeighbor(tile);
    }

    private bool IsTileDiscovered(TileControl tile)
    {
        // Prefer EnvironmentStatus if present (your new discovery script)
        var status = tile.GetComponentInChildren<EnvironmentStatus>(true);
        if (status != null) return status.IsDiscovered;

        // Fallback to EnvironmentControl if that’s what your panels use
        var env = tile.EnvironmentControl;
        if (env != null) return env.IsDiscovered; // assumes EnvironmentControl has IsDiscovered

        return false;
    }

    private bool HasAdjacentSelectableNeighbor(TileControl tile)
    {
        var col = GetTileNeighborCollider(tile);
        if (col == null) return false;

        Bounds b = col.bounds;
        Vector3 halfExtents = b.extents * neighborBoundsExpand;

        int count = Physics.OverlapBoxNonAlloc(
            b.center,
            halfExtents,
            _neighborHits,
            Quaternion.identity,
            tileLayerMask,
            triggerInteraction
        );

        for (int i = 0; i < count; i++)
        {
            var c = _neighborHits[i];
            if (c == null) continue;

            // Ignore self / own children colliders
            if (c.transform.IsChildOf(tile.transform)) continue;

            var neighborTile = TryGetTileFromCollider(c);
            if (neighborTile == null) continue;
            if (neighborTile == tile) continue;

            // Neighbor building => OK
            if (neighborTile.tileContentType == TileContentType.Building)
                return true;

            // Neighbor discovered environment => OK
            if (neighborTile.tileContentType == TileContentType.Environment && IsTileDiscovered(neighborTile))
                return true;
        }

        return false;
    }

    private Collider GetTileNeighborCollider(TileControl tile)
    {
        // Prefer the collider on the tile root (usually your BoxCollider)
        if (tile.TryGetComponent<Collider>(out var col) && col != null)
            return col;

        // Fallback: any collider in children (still uses bounds as a box)
        return tile.GetComponentInChildren<Collider>(true);
    }

    private bool IsScreenPosOverUI(Vector2 screenPos)
    {
        if (EventSystem.current == null) return false;

        if (_uiEventData == null)
            _uiEventData = new PointerEventData(EventSystem.current);

        _uiEventData.position = screenPos;
        _uiResults.Clear();
        EventSystem.current.RaycastAll(_uiEventData, _uiResults);
        return _uiResults.Count > 0;
    }

    public static void NotifyTaskFailed(EnvironmentControl env)
    {
        if (Instance == null || env == null) return;
        if (SelectedTile == null) return;
        if (SelectedTile.EnvironmentControl != env) return;

        Instance.TryOpenNextTaskFailed(env);
    }

    private bool TryOpenNextTaskFailed(EnvironmentControl env)
    {
        if (env == null) return false;
        if (taskFailedPanel == null) return false;
        if (_taskFailedOpen) return false;

        if (env.TryDequeuePendingTaskFailure(out var data))
        {
            // selecting acknowledges/hides the icons
            if (env.HasPendingFailureIndicators)
                env.AcknowledgeFailureIndicators();

            taskFailedPanel.Show(env, data);
            _taskFailedOpen = true;
            return true;
        }

        // If no queued story but icons were pending, still clear them on select
        if (env.HasPendingFailureIndicators)
            env.AcknowledgeFailureIndicators();

        return false;
    }

    public void InstallRuntimeRefs(Camera newTargetCamera = null, CameraControl newCameraControl = null)
    {
        if (newTargetCamera != null)
            targetCamera = newTargetCamera;

        if (newCameraControl != null)
            _cameraControl = newCameraControl;
    }
}
