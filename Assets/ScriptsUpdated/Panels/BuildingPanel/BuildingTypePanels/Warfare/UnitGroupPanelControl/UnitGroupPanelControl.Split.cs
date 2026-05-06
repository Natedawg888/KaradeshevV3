using UnityEngine;

public partial class UnitGroupPanelControl
{
    private void SetupSplitUI()
    {
        if (splitButton != null)
        {
            splitButton.onClick.RemoveAllListeners();
            splitButton.onClick.AddListener(BeginSplit);
        }

        if (splitPlusButton != null)
        {
            splitPlusButton.onClick.RemoveAllListeners();
            splitPlusButton.onClick.AddListener(() => AdjustSplitCount(+1));
        }

        if (splitMinusButton != null)
        {
            splitMinusButton.onClick.RemoveAllListeners();
            splitMinusButton.onClick.AddListener(() => AdjustSplitCount(-1));
        }

        if (splitConfirmButton != null)
        {
            splitConfirmButton.onClick.RemoveAllListeners();
            splitConfirmButton.onClick.AddListener(ConfirmSplit);
        }

        if (splitCancelButton != null)
        {
            splitCancelButton.onClick.RemoveAllListeners();
            splitCancelButton.onClick.AddListener(CancelSplit);
        }
    }

    private void UpdateSplitButtonState()
    {
        if (splitButton == null) return;

        bool canSplit = _group != null && _group.unitCount > 1;
        splitButton.interactable = canSplit;
    }

    private void BeginSplit()
    {
        if (_group == null || _owner == null) return;
        if (_group.unitCount <= 1) return;

        _pendingSplitCount = 1;

        if (splitPanelRoot != null)
            splitPanelRoot.SetActive(true);

        RefreshSplitPanelUI();
    }

    private void AdjustSplitCount(int delta)
    {
        if (_group == null) return;

        int maxSplit = Mathf.Max(1, _group.unitCount - 1); // must leave at least 1 in original
        _pendingSplitCount = Mathf.Clamp(_pendingSplitCount + delta, 1, maxSplit);

        RefreshSplitPanelUI();
    }

    private void RefreshSplitPanelUI()
    {
        if (_group == null) return;

        if (splitCountText != null)
            splitCountText.text = _pendingSplitCount.ToString();

        int maxSplit = Mathf.Max(1, _group.unitCount - 1);

        if (splitMinusButton != null)
            splitMinusButton.interactable = _pendingSplitCount > 1;

        if (splitPlusButton != null)
            splitPlusButton.interactable = _pendingSplitCount < maxSplit;

        if (splitConfirmButton != null)
            splitConfirmButton.interactable = _group.unitCount > 1;
    }

    private void CancelSplit()
    {
        if (splitPanelRoot != null)
            splitPanelRoot.SetActive(false);
    }

    private void ConfirmSplit()
    {
        if (_group == null || _owner == null)
        {
            CancelSplit();
            return;
        }

        int totalCount = _group.unitCount;
        if (totalCount <= 1)
        {
            CancelSplit();
            return;
        }

        int maxSplit   = Mathf.Max(1, totalCount - 1);
        int splitCount = Mathf.Clamp(_pendingSplitCount, 1, maxSplit);
        int remaining  = totalCount - splitCount;

        if (remaining <= 0)
        {
            CancelSplit();
            return;
        }

        var unit = _group.unitType;
        if (unit == null)
        {
            Debug.LogWarning("[UnitGroupPanel] Cannot split group with null unitType.");
            CancelSplit();
            return;
        }

        // Health fraction before splitting
        float healthFraction = _group.HealthFraction;
        if (!float.IsFinite(healthFraction))
            healthFraction = 1f;

        // New group with same expiry on same tile
        var newGroup = _owner.AddGroup(
            unit,
            splitCount,
            populationReservationId: null,
            reservedPopulation: 0,
            expiryTurn: _group.expiryTurn);

        if (newGroup == null)
        {
            Debug.LogWarning("[UnitGroupPanel] Failed to create split group.");
            CancelSplit();
            return;
        }

        // Copy "meta" state
        newGroup.skillLevel        = _group.skillLevel;
        newGroup.groupName         = _group.groupName;
        newGroup.missedUpkeepTurns = _group.missedUpkeepTurns;
        newGroup.upkeepStartTurn   = _group.upkeepStartTurn;

        CopyGroupBonuses(_group, newGroup);

        // Scale health for new + remaining group
        newGroup.RecalculateMaxHealth(keepCurrentFraction: false);
        newGroup.currentHealth = Mathf.Clamp(
            Mathf.RoundToInt(newGroup.maxHealth * healthFraction),
            1,
            newGroup.maxHealth);

        _group.unitCount = remaining;
        _group.RecalculateMaxHealth(keepCurrentFraction: false);
        _group.currentHealth = Mathf.Clamp(
            Mathf.RoundToInt(_group.maxHealth * healthFraction),
            1,
            _group.maxHealth);

        // Update markers and panel
        _owner.RefreshMarker(_group);
        _owner.RefreshMarker(newGroup);

        Refresh();
        _kineticPanel?.RefreshForSameBuilding();

        CancelSplit();
    }

    private void CopyGroupBonuses(TileUnitGroupData from, TileUnitGroupData to)
    {
        if (from == null || to == null) return;

        to.skillLevel = from.skillLevel;
        to.groupName = from.groupName;

        to.bonusHealth = from.bonusHealth;
        to.bonusMovementSpeed = from.bonusMovementSpeed;
        to.bonusPower = from.bonusPower;
        to.bonusDefense = from.bonusDefense;
        to.bonusAgility = from.bonusAgility;
        to.bonusAccuracy = from.bonusAccuracy;
        to.bonusRange = from.bonusRange;
        to.bonusStealth = from.bonusStealth;

        to.missedUpkeepTurns = from.missedUpkeepTurns;
        to.upkeepStartTurn = from.upkeepStartTurn;
    }
}
