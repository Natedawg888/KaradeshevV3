using System;
using System.Collections.Generic;
using UnityEngine;

public partial class KineticWarfareControl
{
    private bool MeetsAdvancementStatRequirements(TileUnitGroupData group, MilitiaUnitAdvancementOption option)
    {
        if (group == null || group.unitType == null || option == null || option.targetUnit == null)
            return false;

        var source = group.unitType;
        var target = option.targetUnit;

        int   currentHealth   = source.maxHealth + group.bonusHealth;
        float currentMove     = source.movementSpeed + group.bonusMovementSpeed;
        int   currentPower    = source.power + group.bonusPower;
        int   currentDefense  = source.defense + group.bonusDefense;
        int   currentAgility  = source.agility + group.bonusAgility;
        int   currentAccuracy = source.accuracy + group.bonusAccuracy;
        int   currentRange    = source.range + group.bonusRange;
        int   currentStealth  = source.stealth + group.bonusStealth;

        bool hasCustomReq =
            option.requireHealth   ||
            option.requireMovement ||
            option.requirePower    ||
            option.requireDefense  ||
            option.requireAgility  ||
            option.requireAccuracy ||
            option.requireRange    ||
            option.requireStealth;

        if (hasCustomReq)
        {
            if (option.requireHealth   && currentHealth   < option.minHealth)    return false;
            if (option.requireMovement && currentMove     < option.minMovement)  return false;
            if (option.requirePower    && currentPower    < option.minPower)     return false;
            if (option.requireDefense  && currentDefense  < option.minDefense)   return false;
            if (option.requireAgility  && currentAgility  < option.minAgility)   return false;
            if (option.requireAccuracy && currentAccuracy < option.minAccuracy)  return false;
            if (option.requireRange    && currentRange    < option.minRange)     return false;
            if (option.requireStealth  && currentStealth  < option.minStealth)   return false;

            return true;
        }
        else
        {
            // Fallback: old behaviour – gate based on target unit's base combat stats
            if (currentPower    < target.power)          return false;
            if (currentDefense  < target.defense)        return false;
            if (currentAgility  < target.agility)        return false;
            if (currentAccuracy < target.accuracy)       return false;
            if (currentRange    < target.range)          return false;
            if (currentStealth  < target.stealth)        return false;

            return true;
        }
    }

    private bool TryConsumeGroupAdvancementResources(
        TileUnitGroupData group,
        MilitiaUnit targetUnit,
        out string failReason)
    {
        failReason = string.Empty;

        if (group == null || targetUnit == null)
        {
            failReason = "Invalid group or target unit for advancement.";
            return false;
        }

        var costs = targetUnit.trainingCosts;
        if (costs == null || costs.Count == 0)
            return true; // no cost => free advancement

        var inv = PlayerInventoryManager.Instance;
        if (inv == null)
        {
            failReason = "Inventory system not found.";
            return false;
        }

        int unitsInGroup = Mathf.Max(1, group.unitCount);

        var required = new List<(ResourceDefinition res, int amount)>();

        foreach (var c in costs)
        {
            if (c == null || c.resource == null) continue;

            int needed = c.amount * unitsInGroup;
            if (needed <= 0) continue;

            int owned = InventoryQuery.GetOwned(c.resource);
            if (owned < needed)
            {
                string resName = string.IsNullOrEmpty(c.resource.resourceName)
                    ? c.resource.resourceID
                    : c.resource.resourceName;

                failReason = $"Not enough {resName} to advance.";
                return false;
            }

            required.Add((c.resource, needed));
        }

        // Consume now
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
                Debug.LogWarning($"[KineticWarfare] Failed to remove {amt}x {res.resourceID} for advancement.");
                failReason = "Unexpected inventory error when paying advancement cost.";
                return false;
            }
        }

        inv.inventoryPanel?.Refresh();
        return true;
    }

    public bool TryStartGroupAdvancement(
        TileUnitGroupControl owner,
        TileUnitGroupData group,
        MilitiaUnit targetUnit,
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

        if (owner == null || group == null || group.unitType == null || targetUnit == null)
        {
            failReason = "Invalid group or target unit.";
            return false;
        }

        var sourceUnit = group.unitType;

        // Find matching advancement option for this target
        MilitiaUnitAdvancementOption option = null;
        if (sourceUnit.advancementOptions != null)
        {
            for (int i = 0; i < sourceUnit.advancementOptions.Count; i++)
            {
                var opt = sourceUnit.advancementOptions[i];
                if (opt != null && opt.targetUnit == targetUnit)
                {
                    option = opt;
                    break;
                }
            }
        }

        if (option == null)
        {
            failReason = "This unit cannot advance into that specialization.";
            return false;
        }

        // Stat gate: group must meet or exceed the option's requirements
        if (!MeetsAdvancementStatRequirements(group, option))
        {
            failReason = "Group does not meet the stat requirements to advance.";
            return false;
        }

        // Pay resources (target unit training cost × group size)
        if (!TryConsumeGroupAdvancementResources(group, targetUnit, out failReason))
        {
            return false;
        }

        // Training time for advancement – use the target unit's base trainingTurns.
        int turns = Mathf.Max(1, targetUnit.trainingTurns);

        // Skill after advancement – use the target's starting skill level (tweak if you want).
        int newSkillLevel = Mathf.Clamp(targetUnit.startingSkillLevel, 0, targetUnit.maxSkillLevel);

        var order = new GroupSkillTrainingOrder
        {
            orderId               = Guid.NewGuid().ToString("N"),
            groupId               = group.groupId,
            unit                  = sourceUnit,
            unitCount             = group.unitCount,
            groupName             = group.groupName,

            totalTurns            = turns,
            remainingTurns        = turns,

            bonusHealthDelta      = 0,
            bonusMovementDelta    = 0,
            bonusPowerDelta       = 0,
            bonusDefenseDelta     = 0,
            bonusAgilityDelta     = 0,
            bonusAccuracyDelta    = 0,
            bonusRangeDelta       = 0,
            bonusStealthDelta     = 0,

            newSkillLevel         = newSkillLevel,

            populationReservationId = group.populationReservationId,
            reservedPopulation      = group.reservedPopulation,
            expiryTurn              = group.expiryTurn,

            isAdvancementOrder    = true,
            advancementTargetUnit = targetUnit,

            owner                 = owner,
            groupData             = group
        };

        _groupTrainingOrders.Add(order);

        // Remove group from tile while advancing (population reservation stays).
        owner.RemoveGroupForTraining(group);

        SpawnGroupTrainingWidget(order);

        Debug.Log(
            $"[KineticWarfare] Started advancement order {order.orderId} " +
            $"for group {group.groupId} → {targetUnit.unitName} in {turns} turns."
        );

        return true;
    }
}