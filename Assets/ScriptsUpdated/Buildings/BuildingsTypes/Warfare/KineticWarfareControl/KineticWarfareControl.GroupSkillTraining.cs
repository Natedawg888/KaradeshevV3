using System;
using System.Collections.Generic;
using UnityEngine;

public partial class KineticWarfareControl
{
    public bool TryStartGroupSkillTraining(
        TileUnitGroupControl owner,
        TileUnitGroupData group,
        int trainingTurns,
        int bonusHealthDelta,
        float bonusMovementDelta,
        int bonusPowerDelta,
        int bonusDefenseDelta,
        int bonusAgilityDelta,
        int bonusAccuracyDelta,
        int bonusRangeDelta,
        int bonusStealthDelta,
        int newSkillLevel,
        out string failReason)
    {
        failReason = string.Empty;

        if (_buildingStatus != null && _buildingStatus.CurrentState != BuildingState.Normal)
        {
            failReason = "Building is not operational.";
            return false;
        }

        if (!HasFreeTrainingSlot())
        {
            failReason = "All training slots are currently in use.";
            return false;
        }

        if (owner == null || group == null || group.unitType == null)
        {
            failReason = "Invalid group to train.";
            return false;
        }

        var unit = group.unitType;

        if (!TryConsumeGroupSkillTrainingResources(group, newSkillLevel, out failReason))
        {
            return false;
        }

        var order = new GroupSkillTrainingOrder
        {
            orderId = Guid.NewGuid().ToString("N"),
            groupId = group.groupId,
            unit = unit,
            unitCount = group.unitCount,
            groupName = group.groupName,

            totalTurns = Mathf.Max(1, trainingTurns),
            remainingTurns = Mathf.Max(1, trainingTurns),

            bonusHealthDelta = bonusHealthDelta,
            bonusMovementDelta = bonusMovementDelta,
            bonusPowerDelta = bonusPowerDelta,
            bonusDefenseDelta = bonusDefenseDelta,
            bonusAgilityDelta = bonusAgilityDelta,
            bonusAccuracyDelta = bonusAccuracyDelta,
            bonusRangeDelta = bonusRangeDelta,
            bonusStealthDelta = bonusStealthDelta,

            newSkillLevel = newSkillLevel,

            populationReservationId = group.populationReservationId,
            reservedPopulation = group.reservedPopulation,
            expiryTurn = group.expiryTurn,

            isAdvancementOrder = false,
            advancementTargetUnit = null,

            owner = owner,
            groupData = group
        };

        _groupTrainingOrders.Add(order);

        // While the group is in training, this reservation belongs to the training building,
        // not to the live unit group on the tile.
        if (!string.IsNullOrWhiteSpace(order.populationReservationId))
            TagTrainingReservation(order.populationReservationId);

        // Remove the group from the tile while it is training.
        // IMPORTANT: this does NOT release the population reservation.
        owner.RemoveGroupForTraining(group);

        SpawnGroupTrainingWidget(order);

        //Debug.Log(
            //$"[KineticWarfare] Started skill training order {order.orderId} " +
            //$"for group {group.groupId} → skill {newSkillLevel} in {trainingTurns} turns."
        //);

        return true;
    }

    private bool TryConsumeGroupSkillTrainingResources(
        TileUnitGroupData group,
        int targetSkillLevel,
        out string failReason)
    {
        failReason = string.Empty;

        if (group == null || group.unitType == null)
        {
            failReason = "Invalid group for training cost.";
            return false;
        }

        var unit = group.unitType;
        var costs = unit.trainingCosts;
        if (costs == null || costs.Count == 0)
            return true;

        var inv = PlayerInventoryManager.Instance;
        if (inv == null)
        {
            failReason = "Inventory system not found.";
            return false;
        }

        int unitsInGroup = Mathf.Max(1, group.unitCount);
        int targetSkill = Mathf.Max(1, targetSkillLevel);

        // IMPORTANT: keep this in sync with UnitGroupPanelControl.GetSkillTrainingCostMultiplier()
        float factor = 1f + 0.1f * targetSkill;

        var required = new List<(ResourceDefinition res, int amount)>();

        foreach (var baseCost in costs)
        {
            if (baseCost == null || baseCost.resource == null)
                continue;

            int baseAmount = baseCost.amount * unitsInGroup;
            int finalNeeded = Mathf.CeilToInt(baseAmount * factor);
            if (finalNeeded <= 0)
                continue;

            int owned = InventoryQuery.GetOwned(baseCost.resource);
            if (owned < finalNeeded)
            {
                string resName = string.IsNullOrEmpty(baseCost.resource.resourceName)
                    ? baseCost.resource.resourceID
                    : baseCost.resource.resourceName;

                failReason = $"Not enough {resName}.";
                return false;
            }

            required.Add((baseCost.resource, finalNeeded));
        }

        for (int i = 0; i < required.Count; i++)
        {
            var (res, amt) = required[i];
            bool removed;

            if (!res.isGroup)
                removed = inv.TryRemove(res, amt);
            else
                removed = inv.TryRemoveGroup(res, amt);

            if (!removed)
            {
                //Debug.LogWarning(
                    //$"[KineticWarfare] Failed to remove {amt} x {res.resourceID} " +
                    //$"even though affordability check passed."
                //);

                failReason = "Unexpected inventory error when paying training cost.";
                return false;
            }
        }

        if (inv.inventoryPanel != null)
            inv.inventoryPanel.Refresh();

        return true;
    }

    private void SpawnGroupTrainingWidget(GroupSkillTrainingOrder order)
    {
        if (!ordersUIRoot || !orderWidgetPrefab) return;

        var w = Instantiate(orderWidgetPrefab, ordersUIRoot);

        var icon = order.unit != null ? order.unit.unitIcon : null;
        w.Bind(order.orderId, order.totalTurns, icon);
        w.UpdateTurns(order.remainingTurns);

        _groupTrainingWidgets[order.orderId] = w;
    }

    private void UpdateGroupTrainingWidget(GroupSkillTrainingOrder order)
    {
        if (_groupTrainingWidgets.TryGetValue(order.orderId, out var w) && w != null)
            w.UpdateTurns(order.remainingTurns);
    }

    private void RemoveGroupTrainingWidget(string orderId)
    {
        if (string.IsNullOrEmpty(orderId)) return;

        if (_groupTrainingWidgets.TryGetValue(orderId, out var w) && w != null)
            Destroy(w.gameObject);

        _groupTrainingWidgets.Remove(orderId);
    }

    private void AdvanceGroupSkillTrainingOrders()
    {
        if (_groupTrainingOrders.Count == 0)
            return;

        for (int i = _groupTrainingOrders.Count - 1; i >= 0; i--)
        {
            var order = _groupTrainingOrders[i];
            if (order == null)
            {
                _groupTrainingOrders.RemoveAt(i);
                continue;
            }

            if (!string.IsNullOrWhiteSpace(order.populationReservationId))
                TagTrainingReservation(order.populationReservationId);

            order.remainingTurns = Mathf.Max(0, order.remainingTurns - 1);
            UpdateGroupTrainingWidget(order);

            if (order.remainingTurns <= 0)
            {
                CompleteGroupSkillTraining(order);
                _groupTrainingOrders.RemoveAt(i);
            }
        }
    }

    private void CompleteGroupSkillTraining(GroupSkillTrainingOrder order)
    {
        RemoveGroupTrainingWidget(order.orderId);

        if (order.owner == null)
        {
            if (!string.IsNullOrEmpty(order.populationReservationId))
                PlayersPopulationManager.Instance?.ReleaseReservation(order.populationReservationId);

            //Debug.LogWarning("[KineticWarfare] Training owner missing on group-training completion; released population.");
            return;
        }

        var group = order.groupData;
        if (group == null)
        {
            if (!string.IsNullOrEmpty(order.populationReservationId))
                PlayersPopulationManager.Instance?.ReleaseReservation(order.populationReservationId);

            //Debug.LogWarning("[KineticWarfare] Group data missing on skill-training completion; released population.");
            return;
        }

        // While finishing but before being re-added, it is still a training reservation.
        if (!string.IsNullOrWhiteSpace(order.populationReservationId))
            TagTrainingReservation(order.populationReservationId);

        group.populationReservationId = order.populationReservationId;
        group.reservedPopulation = order.reservedPopulation;
        group.expiryTurn = order.expiryTurn;

        if (order.isAdvancementOrder && order.advancementTargetUnit != null)
        {
            ApplyAdvancementToGroup(order, group);
        }
        else
        {
            group.bonusHealth += order.bonusHealthDelta;
            group.bonusMovementSpeed += order.bonusMovementDelta;
            group.bonusPower += order.bonusPowerDelta;
            group.bonusDefense += order.bonusDefenseDelta;
            group.bonusAgility += order.bonusAgilityDelta;
            group.bonusAccuracy += order.bonusAccuracyDelta;
            group.bonusRange += order.bonusRangeDelta;
            group.bonusStealth += order.bonusStealthDelta;

            group.skillLevel = order.newSkillLevel;
        }

        group.RecalculateMaxHealth(keepCurrentFraction: true);

        if (TurnSystem.Instance != null)
        {
            int currentTurn = TurnSystem.GetCurrentTurn();
            int interval = 4;
            if (PlayerUnitManager.Instance != null)
                interval = Mathf.Max(1, PlayerUnitManager.Instance.upkeepIntervalTurns);

            group.upkeepStartTurn = currentTurn + interval;
        }

        // Re-adding the group will call TileUnitGroupControl.AddExistingGroup(),
        // which should flow into PlayerUnitManager.RegisterGroup() and re-tag
        // the reservation as UnitGroup.
        order.owner.AddExistingGroup(group);

        PostSkillTrainingNotification(order, group);

        //Debug.Log(
            //$"[KineticWarfare] Completed {(order.isAdvancementOrder ? "advancement" : "skill training")} " +
            //$"order {order.orderId} for group {group.groupId}. " +
            //$"New unit = {group.unitType.unitName}, skill = {group.skillLevel}."
        //);
    }

    private static void PostSkillTrainingNotification(GroupSkillTrainingOrder order, TileUnitGroupData group)
    {
        if (NotificationManager.Instance == null) return;
        string groupName = !string.IsNullOrWhiteSpace(group.groupName) ? group.groupName : "Unit Group";
        string unitName  = group.unitType != null && !string.IsNullOrWhiteSpace(group.unitType.unitName)
            ? group.unitType.unitName : "Unit";
        Vector3 pos = order.owner != null ? order.owner.transform.position : default;
        string title, message;
        if (NotificationMessageCrafterManager.Instance != null)
            (title, message) = NotificationMessageCrafterManager.Instance.CraftUnitSkillTrainingCompleted(groupName, unitName, group.skillLevel);
        else
            (title, message) = ("Training Complete", $"{groupName} has completed training and reached skill level {group.skillLevel}.");
        NotificationManager.Instance.AddNotification(NotificationType.UnitSkillTrainingCompleted, title, message, pos);
    }

    private void ApplyAdvancementToGroup(GroupSkillTrainingOrder order, TileUnitGroupData group)
    {
        var sourceUnit = group.unitType;
        var targetUnit = order.advancementTargetUnit;
        if (sourceUnit == null || targetUnit == null)
            return;

        int oldHealth = sourceUnit.maxHealth + group.bonusHealth;
        float oldMove = sourceUnit.movementSpeed + group.bonusMovementSpeed;
        int oldPower = sourceUnit.power + group.bonusPower;
        int oldDefense = sourceUnit.defense + group.bonusDefense;
        int oldAgility = sourceUnit.agility + group.bonusAgility;
        int oldAccuracy = sourceUnit.accuracy + group.bonusAccuracy;
        int oldRange = sourceUnit.range + group.bonusRange;
        int oldStealth = sourceUnit.stealth + group.bonusStealth;

        group.unitType = targetUnit;

        group.bonusHealth = Mathf.Max(0, oldHealth - targetUnit.maxHealth);
        group.bonusMovementSpeed = Mathf.Max(0f, oldMove - targetUnit.movementSpeed);
        group.bonusPower = Mathf.Max(0, oldPower - targetUnit.power);
        group.bonusDefense = Mathf.Max(0, oldDefense - targetUnit.defense);
        group.bonusAgility = Mathf.Max(0, oldAgility - targetUnit.agility);
        group.bonusAccuracy = Mathf.Max(0, oldAccuracy - targetUnit.accuracy);
        group.bonusRange = Mathf.Max(0, oldRange - targetUnit.range);
        group.bonusStealth = Mathf.Max(0, oldStealth - targetUnit.stealth);

        group.skillLevel = Mathf.Clamp(order.newSkillLevel, 0, targetUnit.maxSkillLevel);
    }
}
