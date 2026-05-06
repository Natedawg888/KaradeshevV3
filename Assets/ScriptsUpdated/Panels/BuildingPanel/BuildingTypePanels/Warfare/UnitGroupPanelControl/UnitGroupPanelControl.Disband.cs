using System;
using System.Collections.Generic;
using UnityEngine;

public partial class UnitGroupPanelControl : MonoBehaviour
{
    private void SetupDisbandUI()
    {
        // Small panel starts hidden – player opens it with the Disband button.
        if (disbandPanelRoot != null)
            disbandPanelRoot.SetActive(false);

        if (temporaryDisbandButton != null)
        {
            temporaryDisbandButton.onClick.RemoveAllListeners();
            temporaryDisbandButton.onClick.AddListener(OnTemporaryDisbandClicked);
        }

        if (fullDisbandButton != null)
        {
            fullDisbandButton.onClick.RemoveAllListeners();
            fullDisbandButton.onClick.AddListener(OnFullDisbandClicked);
        }

        if (disbandCancelButton != null)
        {
            disbandCancelButton.onClick.RemoveAllListeners();
            disbandCancelButton.onClick.AddListener(OnDisbandCancelClicked);
        }

        UpdateDisbandButtonsState();
    }

    /// <summary>
    /// Called when the main "Disband..." button on the panel is clicked.
    /// Just opens the small disband panel if the group can be disbanded here.
    /// </summary>
    private void OnDisbandOpenClicked()
    {
        if (_group == null || _owner == null || _trainerContext == null)
            return;

        // Re-check that the group is on this building's tile.
        var buildingTile = _trainerContext.GetComponentInParent<TileControl>();
        var groupTile    = _owner.GetComponentInParent<TileControl>();

        bool canDisband = (buildingTile != null && groupTile != null && buildingTile == groupTile);
        if (!canDisband)
        {
            Debug.LogWarning("[UnitGroupPanel] Cannot disband: group is not on this building's tile.");
            if (disbandPanelRoot != null)
                disbandPanelRoot.SetActive(false);

            UpdateDisbandButtonsState();
            return;
        }

        if (disbandPanelRoot != null)
            disbandPanelRoot.SetActive(true);

        UpdateDisbandButtonsState();
    }

    /// <summary>
    /// Enables/disables the Disband button and the small panel’s buttons
    /// based on whether this group is actually at the Kinetic Warfare building.
    /// </summary>
    private void UpdateDisbandButtonsState()
    {
        bool canDisband = false;

        if (_group != null && _owner != null && _trainerContext != null)
        {
            var buildingTile = _trainerContext.GetComponentInParent<TileControl>();
            var groupTile    = _owner.GetComponentInParent<TileControl>();

            // Only disband if the group is literally standing on this building's tile.
            if (buildingTile != null && groupTile != null && buildingTile == groupTile)
            {
                canDisband = true;
            }
        }

        // Main "Disband..." button state
        if (disbandOpenButton != null)
            disbandOpenButton.interactable = canDisband;

        // If we *cannot* disband, force the small panel closed.
        if (disbandPanelRoot != null && !canDisband)
            disbandPanelRoot.SetActive(false);

        bool panelVisible   = (disbandPanelRoot != null && disbandPanelRoot.activeSelf);
        bool buttonsEnabled = canDisband && panelVisible;

        if (temporaryDisbandButton != null)
            temporaryDisbandButton.interactable = buttonsEnabled;

        if (fullDisbandButton != null)
            fullDisbandButton.interactable = buttonsEnabled;

        if (disbandCancelButton != null)
            disbandCancelButton.interactable = panelVisible;
    }

    private void OnDisbandCancelClicked()
    {
        if (disbandPanelRoot != null)
            disbandPanelRoot.SetActive(false);

        // Re-evaluate button states now that the panel is closed.
        UpdateDisbandButtonsState();
    }

    private void OnTemporaryDisbandClicked()
    {
        if (_group == null || _owner == null || _trainerContext == null)
            return;

        if (!_trainerContext.TryTemporarilyDisbandGroup(_owner, _group, out string failReason))
        {
            Debug.LogWarning($"[UnitGroupPanel] Failed to temporarily disband group {_group.groupId}: {failReason}");
            return;
        }

        Debug.Log($"[UnitGroupPanel] Temporarily disbanded group {_group.groupId} via {_trainerContext.name}.");

        Hide();
    }

    private void OnFullDisbandClicked()
    {
        if (_group == null || _owner == null)
            return;

        string groupId = _group.groupId;
        _owner.RemoveGroup(groupId);

        Debug.Log($"[UnitGroupPanel] Fully disbanded group {groupId}.");

        Hide();
    }
}