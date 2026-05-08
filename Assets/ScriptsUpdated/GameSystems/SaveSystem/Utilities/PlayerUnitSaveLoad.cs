using System;
using System.Collections.Generic;
using UnityEngine;

public static class PlayerUnitSaveLoad
{
    private static Dictionary<string, UnitActionDefinitionSO> _unitActionLookup;
    private static Dictionary<string, MilitiaUnit> _unitTypeLookup;
    private static Dictionary<string, ResourceDefinition> _resourceLookup;

    public static PlayerUnitsSaveData SaveState()
    {
        PlayerUnitsSaveData data = new PlayerUnitsSaveData();

        if (TileUnitGroupControl.NonEmptyControls.Count == 0)
            return data;

        //Debug.Log($"[PlayerUnitSaveLoad] NonEmptyControls={TileUnitGroupControl.NonEmptyControls.Count}");

        foreach (TileUnitGroupControl control in TileUnitGroupControl.NonEmptyControls)
        {
            if (control == null || !control.HasAnyGroups)
                continue;

            Saveable tileSaveable = control.GetComponent<Saveable>();
            if (tileSaveable == null)
                tileSaveable = control.GetComponentInParent<Saveable>();

            string tileSaveableId = tileSaveable != null ? tileSaveable.uniqueID : null;
            Vector2Int tileGrid = Vector2Int.zero;

            IReadOnlyList<TileUnitGroupData> groups = control.Groups;
            for (int i = 0; i < groups.Count; i++)
            {
                TileUnitGroupData group = groups[i];
                if (group == null || group.unitType == null)
                    continue;

                TileUnitGroupSaveData saved = new TileUnitGroupSaveData
                {
                    tileSaveableID = tileSaveableId,
                    tileGridPosition = tileGrid,

                    groupId = group.groupId,
                    unitTypeID = group.unitType.unitID,
                    groupName = group.groupName,

                    unitCount = group.unitCount,
                    maxHealth = group.maxHealth,
                    currentHealth = group.currentHealth,

                    skillLevel = group.skillLevel,

                    bonusHealth = group.bonusHealth,
                    bonusMovementSpeed = group.bonusMovementSpeed,
                    bonusPower = group.bonusPower,
                    bonusDefense = group.bonusDefense,
                    bonusAgility = group.bonusAgility,
                    bonusAccuracy = group.bonusAccuracy,
                    bonusRange = group.bonusRange,
                    bonusStealth = group.bonusStealth,

                    populationReservationId = group.populationReservationId,
                    reservedPopulation = group.reservedPopulation,

                    expiryTurn = group.expiryTurn,
                    missedUpkeepTurns = group.missedUpkeepTurns,
                    upkeepStartTurn = group.upkeepStartTurn,

                    currentPathIndex = group.currentPathIndex,
                    remainingTurnCostOnCurrentStep = group.remainingTurnCostOnCurrentStep,

                    isPatrolling = group.isPatrolling,

                    activeActionAssetName = group.activeAction != null ? group.activeAction.name : null,
                    activeActionTargetGrid = group.activeActionTargetGrid,
                    remainingActionTurns = group.remainingActionTurns,

                    hasPendingScoutResults = group.hasPendingScoutResults,
                    hasPendingTrackingResults = group.hasPendingTrackingResults,
                    lastTrackingMarkerTurns = group.lastTrackingMarkerTurns,

                    activeMeleeTargetType = group.activeMeleeTargetType,
                    activeMeleeTargetAnimalId = group.activeMeleeTargetAnimalId,
                    activeMeleeTargetUnitGroupId = group.activeMeleeTargetUnitGroupId,

                    meleeRetaliatedLastTick = group.meleeRetaliatedLastTick,
                    meleeTargetFledLastTick = group.meleeTargetFledLastTick
                };

                if (group.plannedPathGridPositions != null)
                    saved.plannedPathGridPositions.AddRange(group.plannedPathGridPositions);

                if (group.plannedStepTurnCosts != null)
                    saved.plannedStepTurnCosts.AddRange(group.plannedStepTurnCosts);

                if (group.patrolLoopGridPositions != null)
                    saved.patrolLoopGridPositions.AddRange(group.patrolLoopGridPositions);

                if (group.patrolLoopStepTurnCosts != null)
                    saved.patrolLoopStepTurnCosts.AddRange(group.patrolLoopStepTurnCosts);

                if (group.pendingLoot != null)
                {
                    for (int p = 0; p < group.pendingLoot.Count; p++)
                    {
                        PendingLootStack loot = group.pendingLoot[p];
                        if (loot.resource == null || loot.amount <= 0)
                            continue;

                        saved.pendingLoot.Add(new PendingLootStackSaveData
                        {
                            resourceID = loot.resource.resourceName,
                            amount = loot.amount
                        });
                    }
                }

                data.groups.Add(saved);
            }
        }

        return data;
    }

    public static void LoadState(PlayerUnitsSaveData data)
    {
        TileUnitGroupControl[] controls = UnityEngine.Object.FindObjectsOfType<TileUnitGroupControl>(true);

        for (int i = 0; i < controls.Length; i++)
        {
            if (controls[i] != null)
                controls[i].ClearGroupsForLoad();
        }

        if (data == null || data.groups == null || data.groups.Count == 0)
            return;

        Dictionary<string, TileUnitGroupControl> controlLookup = BuildControlLookup(controls);

        for (int i = 0; i < data.groups.Count; i++)
        {
            TileUnitGroupSaveData saved = data.groups[i];
            if (saved == null)
                continue;

            MilitiaUnit resolvedUnitType = ResolveUnitTypeById(saved.unitTypeID);
            if (resolvedUnitType == null)
            {
                //Debug.LogWarning($"[PlayerUnitSaveLoad] Could not resolve unit type '{saved.unitTypeID}' while loading group '{saved.groupId}'.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(saved.tileSaveableID) ||
                !controlLookup.TryGetValue(saved.tileSaveableID, out TileUnitGroupControl targetControl) ||
                targetControl == null)
            {
                //Debug.LogWarning($"[PlayerUnitSaveLoad] Could not resolve tile control '{saved.tileSaveableID}' while loading unit group '{saved.groupId}'.");
                continue;
            }

            TileUnitGroupData group = new TileUnitGroupData(
                saved.groupId,
                resolvedUnitType,
                saved.unitCount,
                saved.populationReservationId,
                saved.reservedPopulation,
                saved.expiryTurn
            );

            group.groupName = saved.groupName;
            group.maxHealth = saved.maxHealth;
            group.currentHealth = Mathf.Clamp(saved.currentHealth, 0, Mathf.Max(1, saved.maxHealth));

            group.skillLevel = saved.skillLevel;

            group.bonusHealth = saved.bonusHealth;
            group.bonusMovementSpeed = saved.bonusMovementSpeed;
            group.bonusPower = saved.bonusPower;
            group.bonusDefense = saved.bonusDefense;
            group.bonusAgility = saved.bonusAgility;
            group.bonusAccuracy = saved.bonusAccuracy;
            group.bonusRange = saved.bonusRange;
            group.bonusStealth = saved.bonusStealth;

            group.missedUpkeepTurns = saved.missedUpkeepTurns;
            group.upkeepStartTurn = saved.upkeepStartTurn;

            if (saved.plannedPathGridPositions != null)
                group.plannedPathGridPositions.AddRange(saved.plannedPathGridPositions);

            if (saved.plannedStepTurnCosts != null)
                group.plannedStepTurnCosts.AddRange(saved.plannedStepTurnCosts);

            group.currentPathIndex = saved.currentPathIndex;
            group.remainingTurnCostOnCurrentStep = saved.remainingTurnCostOnCurrentStep;

            group.isPatrolling = saved.isPatrolling;

            if (saved.patrolLoopGridPositions != null)
                group.patrolLoopGridPositions.AddRange(saved.patrolLoopGridPositions);

            if (saved.patrolLoopStepTurnCosts != null)
                group.patrolLoopStepTurnCosts.AddRange(saved.patrolLoopStepTurnCosts);

            group.activeActionTargetGrid = saved.activeActionTargetGrid;
            group.remainingActionTurns = saved.remainingActionTurns;
            group.activeAction = ResolveUnitActionByName(saved.activeActionAssetName);
            group.activeActionTargetTile = null;

            group.hasPendingScoutResults = saved.hasPendingScoutResults;
            group.hasPendingTrackingResults = saved.hasPendingTrackingResults;
            group.lastTrackingMarkerTurns = saved.lastTrackingMarkerTurns;

            group.activeMeleeTargetType = saved.activeMeleeTargetType;
            group.activeMeleeTargetAnimalId = saved.activeMeleeTargetAnimalId;
            group.activeMeleeTargetUnitGroupId = saved.activeMeleeTargetUnitGroupId;

            group.meleeRetaliatedLastTick = saved.meleeRetaliatedLastTick;
            group.meleeTargetFledLastTick = saved.meleeTargetFledLastTick;

            if (saved.pendingLoot != null)
            {
                for (int p = 0; p < saved.pendingLoot.Count; p++)
                {
                    PendingLootStackSaveData loot = saved.pendingLoot[p];
                    if (loot == null || loot.amount <= 0)
                        continue;

                    ResourceDefinition resolvedResource = ResolveResourceDefinitionById(loot.resourceID);
                    if (resolvedResource == null)
                        continue;

                    group.AddPendingLoot(resolvedResource, loot.amount);
                }
            }

            targetControl.AddLoadedGroup(group);
        }
    }

    private static Dictionary<string, TileUnitGroupControl> BuildControlLookup(TileUnitGroupControl[] controls)
    {
        Dictionary<string, TileUnitGroupControl> map = new Dictionary<string, TileUnitGroupControl>(StringComparer.Ordinal);

        for (int i = 0; i < controls.Length; i++)
        {
            TileUnitGroupControl control = controls[i];
            if (control == null)
                continue;

            Saveable saveable = control.GetComponent<Saveable>();
            if (saveable == null)
                saveable = control.GetComponentInParent<Saveable>();

            if (saveable == null || string.IsNullOrWhiteSpace(saveable.uniqueID))
                continue;

            if (!map.ContainsKey(saveable.uniqueID))
                map.Add(saveable.uniqueID, control);
        }

        return map;
    }

    private static MilitiaUnit ResolveUnitTypeById(string unitTypeId)
    {
        if (string.IsNullOrWhiteSpace(unitTypeId))
            return null;

        if (_unitTypeLookup == null)
        {
            _unitTypeLookup = new Dictionary<string, MilitiaUnit>(StringComparer.Ordinal);
            MilitiaUnit[] allUnits = Resources.LoadAll<MilitiaUnit>(string.Empty);

            for (int i = 0; i < allUnits.Length; i++)
            {
                MilitiaUnit unit = allUnits[i];
                if (unit == null || string.IsNullOrWhiteSpace(unit.unitID))
                    continue;

                if (!_unitTypeLookup.ContainsKey(unit.unitID))
                    _unitTypeLookup.Add(unit.unitID, unit);
            }
        }

        _unitTypeLookup.TryGetValue(unitTypeId.Trim(), out MilitiaUnit result);
        return result;
    }

    private static ResourceDefinition ResolveResourceDefinitionById(string resourceId)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
            return null;

        if (_resourceLookup == null)
        {
            _resourceLookup = new Dictionary<string, ResourceDefinition>(StringComparer.Ordinal);
            ResourceDefinition[] allResources = Resources.LoadAll<ResourceDefinition>(string.Empty);

            for (int i = 0; i < allResources.Length; i++)
            {
                ResourceDefinition resource = allResources[i];
                if (resource == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(resource.resourceName) &&
                    !_resourceLookup.ContainsKey(resource.resourceName))
                {
                    _resourceLookup.Add(resource.resourceName, resource);
                }

                if (!string.IsNullOrWhiteSpace(resource.name) &&
                    !_resourceLookup.ContainsKey(resource.name))
                {
                    _resourceLookup.Add(resource.name, resource);
                }
            }
        }

        _resourceLookup.TryGetValue(resourceId.Trim(), out ResourceDefinition result);
        return result;
    }

    private static UnitActionDefinitionSO ResolveUnitActionByName(string actionName)
    {
        if (string.IsNullOrWhiteSpace(actionName))
            return null;

        if (_unitActionLookup == null)
        {
            _unitActionLookup = new Dictionary<string, UnitActionDefinitionSO>(StringComparer.Ordinal);
            UnitActionDefinitionSO[] allActions = Resources.LoadAll<UnitActionDefinitionSO>(string.Empty);

            for (int i = 0; i < allActions.Length; i++)
            {
                UnitActionDefinitionSO action = allActions[i];
                if (action == null || string.IsNullOrWhiteSpace(action.name))
                    continue;

                if (!_unitActionLookup.ContainsKey(action.name))
                    _unitActionLookup.Add(action.name, action);
            }
        }

        _unitActionLookup.TryGetValue(actionName.Trim(), out UnitActionDefinitionSO result);
        return result;
    }
}
