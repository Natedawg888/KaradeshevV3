using UnityEngine;
using TMPro;

public partial class UnitGroupPanelControl : MonoBehaviour
{
    private void SetupMovementUI()
    {
        if (movePanelRoot != null)
            movePanelRoot.SetActive(false);

        if (moveOpenButton != null)
        {
            moveOpenButton.onClick.RemoveAllListeners();
            moveOpenButton.onClick.AddListener(OnMoveOpenClicked);
        }

        if (moveModeButton != null)
        {
            moveModeButton.onClick.RemoveAllListeners();
            moveModeButton.onClick.AddListener(OnMoveModeClicked);
        }

        if (patrolModeButton != null)
        {
            patrolModeButton.onClick.RemoveAllListeners();
            patrolModeButton.onClick.AddListener(OnPatrolModeClicked);
        }

        if (moveCancelButton != null)
        {
            moveCancelButton.onClick.RemoveAllListeners();
            moveCancelButton.onClick.AddListener(OnMoveCancelClicked);
        }

        // Initial state: until a group is bound, patrol is disabled.
        UpdatePatrolButtonInteractable();
    }

    public static event System.Action OnMovePanelOpened;

    private void OnMoveOpenClicked()
    {
        if (_group == null || _owner == null)
        {
            //Debug.LogWarning("[UnitGroupPanel] Cannot open movement panel: missing group/owner.");
            return;
        }

        if (movePanelRoot != null)
            movePanelRoot.SetActive(true);

        // Whenever we show the Move/Patrol panel, update patrol availability.
        UpdatePatrolButtonInteractable();
        OnMovePanelOpened?.Invoke();
    }

    private void OnMoveCancelClicked()
    {
        if (movePanelRoot != null)
            movePanelRoot.SetActive(false);
    }

    private void OnMoveModeClicked()
    {
        if (_group == null || _owner == null)
        {
            //Debug.LogWarning("[UnitGroupPanel] Cannot start movement: missing group/owner.");
            return;
        }

        if (UnitGroupMovementManager.Instance == null)
        {
            //Debug.LogWarning("[UnitGroupPanel] Cannot start movement: no UnitGroupMovementManager in scene.");
            return;
        }

        // Close the small "Move / Patrol" sub-panel.
        if (movePanelRoot != null)
            movePanelRoot.SetActive(false);

        // 1) Start the step-by-step movement planner from this group's tile.
        UnitGroupMovementManager.Instance.BeginMovementForGroup(_group, _owner);

        TileInteraction.SetSelectionEnabled(false);  // <-- disable tile selection

        // 3) Hide THIS panel and any parent panels (kinetic + building),
        //    so the player is just looking at the map + movement UI.
        CloseAllPanelsStayHere();
    }

    private void OnPatrolModeClicked()
    {
        if (_group == null || _owner == null)
        {
            //Debug.LogWarning("[UnitGroupPanel] Cannot start patrol: missing group/owner.");
            return;
        }

        // If we're standing on an undiscovered environment tile, do not allow patrol.
        if (IsOnUndiscoveredEnvironmentTile())
        {
            //Debug.Log("[UnitGroupPanel] Cannot start patrol: group is on an undiscovered environment tile.");
            return;
        }

        if (UnitGroupMovementManager.Instance == null)
        {
            //Debug.LogWarning("[UnitGroupPanel] Cannot start patrol: no UnitGroupMovementManager in scene.");
            return;
        }

        // Close the small "Move / Patrol" sub-panel.
        if (movePanelRoot != null)
            movePanelRoot.SetActive(false);

        // 1) Start the patrol planner from this group's tile.
        UnitGroupMovementManager.Instance.BeginPatrolForGroup(_group, _owner);

        // 2) Zoom camera out to max height & disable tile selection while planning.
        var cam = FindObjectOfType<CameraControl>();
        if (cam != null)
        {
            cam.ZoomToMaxHeight();
        }

        TileInteraction.SetSelectionEnabled(false);

        // 3) Hide THIS panel and any parent panels.
        CloseAllPanelsStayHere();
    }

    private void OnCancelMovementPlanClicked()
    {
        if (_group == null)
            return;

        var moveMgr = UnitGroupMovementManager.Instance;
        if (moveMgr != null)
        {
            // Use central movement manager to clear route + refresh markers.
            moveMgr.CancelMovementForGroup(_group);
        }
        else
        {
            // Fallback: clear route directly if manager missing.
            if (_group.plannedPathGridPositions != null)
                _group.plannedPathGridPositions.Clear();
            if (_group.plannedStepTurnCosts != null)
                _group.plannedStepTurnCosts.Clear();
            if (_group.patrolLoopGridPositions != null)
                _group.patrolLoopGridPositions.Clear();
            if (_group.patrolLoopStepTurnCosts != null)
                _group.patrolLoopStepTurnCosts.Clear();

            _group.currentPathIndex               = 0;
            _group.remainingTurnCostOnCurrentStep = 0f;
            _group.isPatrolling                   = false;
        }

        // Rebuild panel state (move button + training, etc.)
        Refresh();

        // Update patrol button state after cancelling any route.
        UpdatePatrolButtonInteractable();
    }

    private void UpdateMoveButtonState()
    {
        if (moveOpenButton == null)
            return;

        bool hasRoute = GroupHasActiveRoute(_group);

        // If a route is active, make sure the small Move/Patrol panel is closed.
        if (movePanelRoot != null && hasRoute)
            movePanelRoot.SetActive(false);

        // Rewire the button each refresh so we can swap behaviour.
        moveOpenButton.onClick.RemoveAllListeners();

        var label = moveOpenButton.GetComponentInChildren<TMP_Text>();

        if (hasRoute)
        {
            // When moving/patrolling: button becomes "Cancel Movement"
            if (label != null)
                label.text = "Cancel";

            moveOpenButton.interactable = true;
            moveOpenButton.onClick.AddListener(OnCancelMovementPlanClicked);
        }
        else
        {
            // Normal "Move..." behaviour, only if unit has movement
            bool canMove = false;

            if (_group != null && _group.unitType != null && _owner != null)
            {
                float totalMove = _group.unitType.movementSpeed + _group.bonusMovementSpeed;
                canMove = totalMove > 0.1f;
            }

            if (label != null)
                label.text = "Move";

            moveOpenButton.interactable = canMove;
            moveOpenButton.onClick.AddListener(OnMoveOpenClicked);
        }

        // Also keep patrol button's state in sync whenever we refresh move state.
        UpdatePatrolButtonInteractable();
    }

    private bool GroupHasActiveRoute(TileUnitGroupData g)
    {
        return g != null &&
               g.plannedPathGridPositions != null &&
               g.plannedPathGridPositions.Count > 0 &&
               g.currentPathIndex < g.plannedPathGridPositions.Count;
    }

    // -------------------------------------------------------------------------
    //  PATROL BUTTON STATE HELPERS
    // -------------------------------------------------------------------------

    private bool IsOnUndiscoveredEnvironmentTile()
    {
        if (_owner == null)
            return false;

        var tile = _owner.GetComponentInParent<TileControl>();
        if (tile == null)
            return false;

        if (tile.tileContentType != TileContentType.Environment)
            return false;

        var status = tile.GetComponentInChildren<EnvironmentStatus>();
        if (status != null)
            return !status.IsDiscovered;

        var envCtrl = tile.GetComponentInChildren<EnvironmentControl>();
        if (envCtrl != null)
            return !envCtrl.IsDiscovered;

        // No discovery info – safest to treat as undiscovered.
        return true;
    }

    private void UpdatePatrolButtonInteractable()
    {
        if (patrolModeButton == null)
            return;

        bool canPatrol = true;

        if (_group == null || _owner == null)
        {
            canPatrol = false;
        }
        else if (IsOnUndiscoveredEnvironmentTile())
        {
            canPatrol = false;
        }

        patrolModeButton.interactable = canPatrol;
    }
}
