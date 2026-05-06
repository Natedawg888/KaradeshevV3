using UnityEngine;

public partial class UnitGroupActionManager
{
    private AnimalSimulation GetAnimalSimulation()
    {
        return AnimalSimulationAccess.Current;
    }

    public void SetAnimalPlayerTargeted(int animalGroupId, bool targeted)
    {
        if (animalGroupId < 0)
            return;

        var sim = GetAnimalSimulation();
        if (sim == null)
            return;

        sim.SetTargetedByHumanUnit(animalGroupId, targeted);
    }

    private bool IsAnyActionStillTargetingAnimal(int animalGroupId, TileUnitGroupData ignoreGroup = null)
    {
        if (animalGroupId < 0)
            return false;

        var pum = PlayerUnitManager.Instance;
        if (pum == null)
            return false;

        _groupInfoBuffer.Clear();
        pum.GetAllGroups(_groupInfoBuffer);

        for (int i = 0; i < _groupInfoBuffer.Count; i++)
        {
            var g = _groupInfoBuffer[i].data;
            if (g == null || g == ignoreGroup)
                continue;

            if (g.remainingActionTurns <= 0 || g.activeAction == null)
                continue;

            if (g.activeAction is MeleeAttackActionSO &&
                g.activeMeleeTargetType == MeleeTargetType.Animal &&
                g.activeMeleeTargetAnimalId == animalGroupId)
            {
                return true;
            }

            if (g.activeAction is SurroundActionSO &&
                g.activeSurroundTargetType == MeleeTargetType.Animal &&
                g.activeSurroundTargetAnimalId == animalGroupId)
            {
                return true;
            }
        }

        return false;
    }

    private void RefreshAnimalPlayerTargetedFromActions(int animalGroupId, TileUnitGroupData ignoreGroup = null)
    {
        SetAnimalPlayerTargeted(animalGroupId, IsAnyActionStillTargetingAnimal(animalGroupId, ignoreGroup));
    }

    public void ClearAnimalPlayerTargeted(int animalGroupId)
    {
        RefreshAnimalPlayerTargetedFromActions(animalGroupId);
    }

    public void ClearTrackedMeleeTargetMarker(TileUnitGroupData group)
    {
        if (group == null)
            return;

        if (group.activeMeleeTargetType != MeleeTargetType.Animal)
            return;

        if (group.activeMeleeTargetAnimalId < 0)
            return;

        RefreshAnimalPlayerTargetedFromActions(group.activeMeleeTargetAnimalId, group);
    }

    public void SetTrackedMeleeTargetMarker(TileUnitGroupData group)
    {
        if (group == null)
            return;

        if (group.activeMeleeTargetType != MeleeTargetType.Animal)
            return;

        if (group.activeMeleeTargetAnimalId < 0)
            return;

        SetAnimalPlayerTargeted(group.activeMeleeTargetAnimalId, true);
    }

    public void SwapTrackedMeleeAnimalTargetMarker(int oldAnimalGroupId, int newAnimalGroupId)
    {
        if (oldAnimalGroupId >= 0 && oldAnimalGroupId != newAnimalGroupId)
            RefreshAnimalPlayerTargetedFromActions(oldAnimalGroupId);

        if (newAnimalGroupId >= 0)
            SetAnimalPlayerTargeted(newAnimalGroupId, true);
    }

    public void CancelAnimalMeleeTarget(TileUnitGroupData group)
    {
        if (group == null)
            return;

        ClearActiveAction(group);
    }

    public void ClearTrackedSurroundTargetMarker(TileUnitGroupData group)
    {
        if (group == null)
            return;

        if (group.activeSurroundTargetType != MeleeTargetType.Animal)
            return;

        if (group.activeSurroundTargetAnimalId < 0)
            return;

        RefreshAnimalPlayerTargetedFromActions(group.activeSurroundTargetAnimalId, group);
    }

    public void SetTrackedSurroundTargetMarker(TileUnitGroupData group)
    {
        if (group == null)
            return;

        if (group.activeSurroundTargetType != MeleeTargetType.Animal)
            return;

        if (group.activeSurroundTargetAnimalId < 0)
            return;

        SetAnimalPlayerTargeted(group.activeSurroundTargetAnimalId, true);
    }

    public void CancelSurroundTarget(TileUnitGroupData group)
    {
        if (group == null)
            return;

        ClearActiveAction(group);
    }

    public bool TryFindSurroundLockTarget(
        TileUnitGroupData supporter,
        TileControl targetTile,
        out MeleeTargetType targetType,
        out int animalId,
        out string unitGroupId)
    {
        targetType = MeleeTargetType.None;
        animalId = -1;
        unitGroupId = null;

        if (supporter == null || targetTile == null)
            return false;

        var pum = PlayerUnitManager.Instance;
        if (pum == null)
            return false;

        _groupInfoBuffer.Clear();
        pum.GetAllGroups(_groupInfoBuffer);

        Vector2Int targetGrid = targetTile.GetGridPosition();

        for (int i = 0; i < _groupInfoBuffer.Count; i++)
        {
            var other = _groupInfoBuffer[i].data;
            if (other == null || other == supporter)
                continue;

            if (!(other.activeAction is MeleeAttackActionSO))
                continue;

            if (other.remainingActionTurns <= 0)
                continue;

            TileControl otherTargetTile = other.activeActionTargetTile;
            if (otherTargetTile == null)
                continue;

            if (otherTargetTile.GetGridPosition() != targetGrid)
                continue;

            if (other.activeMeleeTargetType == MeleeTargetType.Animal &&
                other.activeMeleeTargetAnimalId >= 0)
            {
                targetType = MeleeTargetType.Animal;
                animalId = other.activeMeleeTargetAnimalId;
                return true;
            }

            if (other.activeMeleeTargetType == MeleeTargetType.Unit &&
                !string.IsNullOrEmpty(other.activeMeleeTargetUnitGroupId))
            {
                targetType = MeleeTargetType.Unit;
                unitGroupId = other.activeMeleeTargetUnitGroupId;
                return true;
            }
        }

        return false;
    }

    public bool HasMatchingPrimaryEngagerForSurround(TileUnitGroupData supporter, TileControl targetTile)
    {
        if (supporter == null || targetTile == null)
            return false;

        if (supporter.activeSurroundTargetType == MeleeTargetType.None)
            return false;

        var pum = PlayerUnitManager.Instance;
        if (pum == null)
            return false;

        _groupInfoBuffer.Clear();
        pum.GetAllGroups(_groupInfoBuffer);

        Vector2Int targetGrid = targetTile.GetGridPosition();

        for (int i = 0; i < _groupInfoBuffer.Count; i++)
        {
            var other = _groupInfoBuffer[i].data;
            if (other == null || other == supporter)
                continue;

            if (!(other.activeAction is MeleeAttackActionSO))
                continue;

            if (other.remainingActionTurns <= 0)
                continue;

            TileControl otherTargetTile = other.activeActionTargetTile;
            if (otherTargetTile == null || otherTargetTile.GetGridPosition() != targetGrid)
                continue;

            if (supporter.activeSurroundTargetType == MeleeTargetType.Animal &&
                other.activeMeleeTargetType == MeleeTargetType.Animal &&
                other.activeMeleeTargetAnimalId == supporter.activeSurroundTargetAnimalId)
            {
                return true;
            }

            if (supporter.activeSurroundTargetType == MeleeTargetType.Unit &&
                other.activeMeleeTargetType == MeleeTargetType.Unit &&
                other.activeMeleeTargetUnitGroupId == supporter.activeSurroundTargetUnitGroupId)
            {
                return true;
            }
        }

        return false;
    }

    public bool BeginSurroundAction(
        TileUnitGroupData supporter,
        TileUnitGroupControl owner,
        SurroundActionSO action,
        TileControl targetTile)
    {
        if (supporter == null || owner == null || action == null || targetTile == null)
            return false;

        TileControl ownerTile = ResolveTileForUnitControl(owner);
        if (ownerTile == null)
            return false;

        if (!action.MeetsRequirements(supporter))
            return false;

        if (!action.IsValidTarget(supporter, ownerTile, targetTile))
            return false;

        if (!TryFindSurroundLockTarget(
            supporter,
            targetTile,
            out var targetType,
            out int animalId,
            out string unitGroupId))
        {
            return false;
        }

        ClearActiveAction(supporter);

        supporter.activeAction = action;
        supporter.activeActionTargetTile = targetTile;
        supporter.activeActionTargetGrid = targetTile.GetGridPosition();
        supporter.remainingActionTurns = Mathf.Max(1, action.durationTurns);

        supporter.activeSurroundTargetType = targetType;
        supporter.activeSurroundTargetAnimalId = targetType == MeleeTargetType.Animal ? animalId : -1;
        supporter.activeSurroundTargetUnitGroupId = targetType == MeleeTargetType.Unit ? unitGroupId : null;

        SetTrackedSurroundTargetMarker(supporter);

        owner.RefreshMarker(supporter);
        RaiseGroupActionStateChanged(supporter);
        return true;
    }
}