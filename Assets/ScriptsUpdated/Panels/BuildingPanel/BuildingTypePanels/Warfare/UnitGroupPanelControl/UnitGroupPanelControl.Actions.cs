using UnityEngine;

public partial class UnitGroupPanelControl : MonoBehaviour
{
    private void SetupActionsUI()
    {
        if (actionOpenButton != null)
        {
            actionOpenButton.onClick.RemoveAllListeners();
            actionOpenButton.onClick.AddListener(OnActionOpenClicked);
        }

        if (actionPanelRoot != null)
            actionPanelRoot.SetActive(false);
    }

    private void OnActionOpenClicked()
    {
        if (actionPanelRoot == null)
            return;

        if (_group == null || _group.unitType == null)
        {
            actionPanelRoot.SetActive(false);

            if (scoutResultsPanelRoot != null)
                scoutResultsPanelRoot.SetActive(false);

            if (trackingResultsPanelRoot != null)
                trackingResultsPanelRoot.SetActive(false);

            if (lootPanelRoot != null)
                lootPanelRoot.SetActive(false);

            return;
        }

        if (GroupIsInCombat())
        {
            ToggleInCombatPanel();
            return;
        }

        if (_group.HasPendingLoot)
        {
            ToggleLootPanel();
            return;
        }

        if (_group.hasPendingScoutResults || _group.hasPendingTrackingResults)
        {
            if (_group.hasPendingScoutResults)
            {
                if (scoutResultsPanelRoot != null && scoutResultsPanelRoot.activeSelf)
                    scoutResultsPanelRoot.SetActive(false);
                else
                    OpenScoutResultsPanel();

                return;
            }

            if (_group.hasPendingTrackingResults)
            {
                if (trackingResultsPanelRoot != null && trackingResultsPanelRoot.activeSelf)
                    trackingResultsPanelRoot.SetActive(false);
                else
                    OpenTrackingResultsPanel();

                return;
            }
        }

        if (actionPanelRoot.activeSelf)
        {
            actionPanelRoot.SetActive(false);
            return;
        }

        if (actionItemPrefab == null || actionListContentRoot == null)
        {
            //Debug.LogWarning("[UnitGroupPanel] Action list UI not wired in inspector.");
            return;
        }

        RefreshActionList();
        actionPanelRoot.SetActive(true);
    }

    private void RefreshActionList()
    {
        if (actionListContentRoot == null || actionItemPrefab == null)
            return;

        foreach (Transform child in actionListContentRoot)
            Destroy(child.gameObject);

        var unit = _group != null ? _group.unitType : null;
        if (unit == null || unit.actions == null || unit.actions.Count == 0)
            return;

        for (int i = 0; i < unit.actions.Count; i++)
        {
            var action = unit.actions[i];
            if (action == null)
                continue;

            if (!action.CanUnitUseAction(unit))
                continue;

            if (!action.MeetsRequirements(_group))
                continue;

            var item = Instantiate(actionItemPrefab, actionListContentRoot);

            var capturedAction = action;
            item.Setup(
                capturedAction,
                _group,
                () => StartActionMode(capturedAction)
            );
        }
    }

    private void StartActionMode(UnitActionDefinitionSO action)
    {
        if (_group == null || _owner == null || action == null)
            return;

        if (GroupHasActiveRoute(_group) || GroupHasActiveAction(_group))
        {
            //Debug.LogWarning("[UnitGroupPanel] Cannot start new action: group already busy.");
            return;
        }

        if (!action.MeetsRequirements(_group))
        {
            //Debug.LogWarning($"[UnitGroupPanel] Requirements not met for action {action.displayName}.");
            return;
        }

        if (actionPanelRoot != null)
            actionPanelRoot.SetActive(false);

        if (action is ScoutTileActionSO scoutAction)
        {
            if (UnitGroupActionManager.Instance == null)
            {
                //Debug.LogWarning("[UnitGroupPanel] Cannot start Scout action: no UnitGroupActionManager in scene.");
                return;
            }

            UnitGroupActionManager.Instance.BeginScoutForGroup(_group, _owner, scoutAction);

            TileInteraction.SetSelectionEnabled(false);
            CloseAllPanelsStayHere();
        }
        else if (action is TrackAreaActionSO trackAction)
        {
            var originTile = ResolveOwnerTileForCombat();
            if (originTile == null)
            {
                //Debug.LogWarning("[UnitGroupPanel] Cannot run Track scan: owner has no TileControl parent.");
                return;
            }

            trackAction.Resolve(_group, _owner, originTile);
            OpenTrackingResultsPanel();
            UpdateActionButtonState();
        }
        else if (action is MeleeAttackActionSO meleeAction)
        {
            OpenMeleeTargetsPanel(meleeAction);
        }
        else if (action is RangedAttackActionSO rangedAction)
        {
            OpenRangedTargetsPanel(rangedAction);
        }
        else if (action is SurroundActionSO surroundAction)
        {
            OpenSurroundTargetsPanel(surroundAction);
        }
        else
        {
            //Debug.LogWarning(
                //$"[UnitGroupPanel] StartActionMode does not handle action type {action.GetType().Name} yet. " +
                //"Add handling here when you create new action types.");
        }
    }

    private void UpdateActionButtonState()
    {
        if (actionOpenButton == null)
            return;

        if (_group == null || _group.unitType == null)
        {
            actionOpenButton.interactable = false;
            SetActionButtonLabel("Actions");
            return;
        }

        var unit = _group.unitType;
        bool hasAnyAvailableActions = false;

        if (unit.actions != null)
        {
            for (int i = 0; i < unit.actions.Count; i++)
            {
                var a = unit.actions[i];
                if (a == null) continue;
                if (!a.CanUnitUseAction(unit)) continue;
                if (!a.MeetsRequirements(_group)) continue;

                hasAnyAvailableActions = true;
                break;
            }
        }

        if ((_group.activeAction is MeleeAttackActionSO ||
             _group.activeAction is RangedAttackActionSO ||
             _group.activeAction is SurroundActionSO) &&
            _group.remainingActionTurns > 0)
        {
            actionOpenButton.interactable = true;
            SetActionButtonLabel("Combat");
            return;
        }

        if (GroupHasActiveRoute(_group) || GroupHasActiveAction(_group))
        {
            actionOpenButton.interactable = false;

            if (_group.activeAction is ScoutTileActionSO)
                SetActionButtonLabel("Scouting");
            else if (_group.activeAction is TrackAreaActionSO)
                SetActionButtonLabel("Tracking");
            else if (_group.activeAction is SurroundActionSO)
                SetActionButtonLabel("Surrounding");
            else
                SetActionButtonLabel("Busy");

            return;
        }

        if (_group.HasPendingLoot)
        {
            actionOpenButton.interactable = true;
            SetActionButtonLabel("Loot");
            return;
        }

        if (_group.hasPendingScoutResults || _group.hasPendingTrackingResults)
        {
            actionOpenButton.interactable = true;
            SetActionButtonLabel("Results");
            return;
        }

        actionOpenButton.interactable = hasAnyAvailableActions;
        SetActionButtonLabel("Actions");
    }

    private bool GroupHasActiveAction(TileUnitGroupData group)
    {
        if (group == null)
            return false;

        return group.activeAction != null && group.remainingActionTurns > 0;
    }

    private void OnExternalGroupActionStateChanged(TileUnitGroupData changedGroup)
    {
        if (changedGroup == null || _group == null)
            return;

        if (changedGroup != _group && changedGroup.groupId != _group.groupId)
            return;

        if (!GroupIsInCombat() && inCombatPanelRoot != null && inCombatPanelRoot.activeSelf)
            inCombatPanelRoot.SetActive(false);

        UpdateActionButtonState();

        if (lootPanelRoot != null && lootPanelRoot.activeSelf)
        {
            if (_group.HasPendingLoot)
                RebuildLootList();
            else
                CloseLootPanel(discardLeftovers: false);
        }
    }
}
