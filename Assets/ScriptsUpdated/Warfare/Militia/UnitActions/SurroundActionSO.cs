using UnityEngine;

[CreateAssetMenu(menuName = "Kardashev/Unit Actions/Surround", fileName = "SurroundAction")]
public class SurroundActionSO : UnitActionDefinitionSO, IPerTurnUnitAction
{
    [Header("Timing")]
    public int durationTurns = 2;

    [Header("Support Score Weights")]
    public float movementWeight = 0.18f;
    public float agilityWeight = 0.20f;
    public float stealthWeight = 0.10f;
    public float powerWeight = 0.12f;
    public float defenseWeight = 0.10f;
    public float accuracyWeight = 0.08f;
    public float skillWeight = 0.07f;
    public float unitCountWeight = 0.15f;

    [Header("Normalization")]
    public float movementForMaxScore = 2.0f;
    public int agilityForMaxScore = 10;
    public int stealthForMaxScore = 10;
    public int powerForMaxScore = 12;
    public int defenseForMaxScore = 12;
    public int accuracyForMaxScore = 10;
    public int skillForMaxScore = 5;
    public int unitCountForMaxScore = 12;

    [Header("Effects")]
    [Range(0f, 1f)] public float maxEscapeAttemptReduction = 0.35f;
    [Range(0f, 1f)] public float maxEscapeSuccessReduction = 0.45f;
    [Range(0f, 1f)] public float maxAnimalRetaliationBonus = 0.25f;
    [Range(0f, 1f)] public float maxUnitRetaliationHitBonus = 0.20f;
    public float maxUnitRetaliationDamageBonus = 0.35f;
    [Range(0f, 1f)] public float baseAnimalStragglerChance = 0.05f;
    [Range(0f, 1f)] public float maxAnimalStragglerBonus = 0.40f;

    public override bool CanUnitUseAction(MilitiaUnit unit) => unit != null;

    public override bool IsValidTarget(TileUnitGroupData group, TileControl originTile, TileControl targetTile)
    {
        if (group == null || originTile == null || targetTile == null)
            return false;

        // Surround must be performed on the same tile.
        return originTile.GetGridPosition() == targetTile.GetGridPosition();
    }

    public override int GetTurnCost(TileUnitGroupData group, TileControl originTile, TileControl targetTile)
    {
        return Mathf.Max(1, durationTurns);
    }

    public override void Resolve(TileUnitGroupData group, TileUnitGroupControl owner, TileControl targetTile) { }

    public bool Tick(TileUnitGroupData supporter, TileUnitGroupControl owner, TileControl tile)
    {
        if (supporter == null || supporter.unitType == null || owner == null || tile == null)
            return true;

        if (supporter.activeSurroundTargetType == MeleeTargetType.None)
            return true;

        if (UnitGroupActionManager.Instance == null)
            return true;

        TileControl ownerTile = UnitGroupActionManager.Instance.ResolveTileForUnitControl(owner);
        if (ownerTile == null)
            return true;

        // Surrounder must remain on the same tile it is locking down.
        if (ownerTile.GetGridPosition() != tile.GetGridPosition())
            return true;

        if (!IsStoredTargetStillValid(supporter, tile))
            return true;

        if (!UnitGroupActionManager.Instance.HasMatchingPrimaryEngagerForSurround(supporter, tile))
            return true;

        // Keep the animal pinned before it takes its turn.
        UnitGroupActionManager.Instance.SetTrackedSurroundTargetMarker(supporter);
        return false;
    }

    public bool IsMatchingStoredTarget(
        TileUnitGroupData group,
        MeleeTargetType targetType,
        int animalId,
        string unitGroupId)
    {
        if (group == null)
            return false;

        if (group.activeSurroundTargetType != targetType)
            return false;

        return targetType switch
        {
            MeleeTargetType.Animal => group.activeSurroundTargetAnimalId == animalId,
            MeleeTargetType.Unit => group.activeSurroundTargetUnitGroupId == unitGroupId,
            _ => false
        };
    }

    public float GetSupportScore01(TileUnitGroupData group)
    {
        if (group == null || group.unitType == null)
            return 0f;

        float moveScore = Mathf.Clamp01(
            (group.unitType.movementSpeed + group.bonusMovementSpeed) /
            Mathf.Max(0.01f, movementForMaxScore));

        float agilityScore = Mathf.Clamp01(
            (group.unitType.agility + group.bonusAgility) /
            Mathf.Max(1f, agilityForMaxScore));

        float stealthScore = Mathf.Clamp01(
            (group.unitType.stealth + group.bonusStealth) /
            Mathf.Max(1f, stealthForMaxScore));

        float powerScore = Mathf.Clamp01(
            (group.unitType.power + group.bonusPower) /
            Mathf.Max(1f, powerForMaxScore));

        float defenseScore = Mathf.Clamp01(
            (group.unitType.defense + group.bonusDefense) /
            Mathf.Max(1f, defenseForMaxScore));

        float accuracyScore = Mathf.Clamp01(
            (group.unitType.accuracy + group.bonusAccuracy) /
            Mathf.Max(1f, accuracyForMaxScore));

        float skillScore = Mathf.Clamp01(
            group.skillLevel / Mathf.Max(1f, skillForMaxScore));

        float countScore = Mathf.Clamp01(
            group.unitCount / Mathf.Max(1f, unitCountForMaxScore));

        float totalWeight =
            movementWeight + agilityWeight + stealthWeight + powerWeight +
            defenseWeight + accuracyWeight + skillWeight + unitCountWeight;

        if (totalWeight <= 0f)
            return 0f;

        float raw =
            moveScore * movementWeight +
            agilityScore * agilityWeight +
            stealthScore * stealthWeight +
            powerScore * powerWeight +
            defenseScore * defenseWeight +
            accuracyScore * accuracyWeight +
            skillScore * skillWeight +
            countScore * unitCountWeight;

        float healthFrac = group.maxHealth > 0
            ? Mathf.Clamp01(group.currentHealth / (float)group.maxHealth)
            : 1f;

        float woundedPenaltyMult = Mathf.Lerp(0.45f, 1f, healthFrac);

        return Mathf.Clamp01((raw / totalWeight) * woundedPenaltyMult);
    }

    public float GetEscapeAttemptReduction01(TileUnitGroupData group)
    {
        return Mathf.Clamp01(GetSupportScore01(group) * maxEscapeAttemptReduction);
    }

    public float GetEscapeSuccessReduction01(TileUnitGroupData group)
    {
        return Mathf.Clamp01(GetSupportScore01(group) * maxEscapeSuccessReduction);
    }

    public float GetAnimalRetaliationBonus01(TileUnitGroupData group)
    {
        return Mathf.Clamp01(GetSupportScore01(group) * maxAnimalRetaliationBonus);
    }

    public float GetUnitRetaliationHitBonus01(TileUnitGroupData group)
    {
        return Mathf.Clamp01(GetSupportScore01(group) * maxUnitRetaliationHitBonus);
    }

    public float GetUnitRetaliationDamageBonus01(TileUnitGroupData group)
    {
        return Mathf.Max(0f, GetSupportScore01(group) * maxUnitRetaliationDamageBonus);
    }

    public float GetAnimalStragglerChance01(TileUnitGroupData group)
    {
        float score = GetSupportScore01(group);
        return Mathf.Clamp01(baseAnimalStragglerChance + score * maxAnimalStragglerBonus);
    }

    private bool IsStoredTargetStillValid(TileUnitGroupData supporter, TileControl tile)
    {
        if (supporter == null || tile == null)
            return false;

        if (supporter.activeSurroundTargetType == MeleeTargetType.Animal)
        {
            var sim = AnimalSimulationAccess.Current;
            if (sim == null)
                return false;

            if (!sim.TryGetGroup(supporter.activeSurroundTargetAnimalId, out var animal) ||
                animal == null ||
                !animal.isAlive)
            {
                return false;
            }

            Vector2Int grid = tile.GetGridPosition();
            return animal.tile.x == grid.x && animal.tile.y == grid.y;
        }

        if (supporter.activeSurroundTargetType == MeleeTargetType.Unit)
        {
            TileUnitGroupControl tileUnitCtrl = null;
            UnitGroupActionManager.Instance?.TryGetUnitControlForTile(tile, out tileUnitCtrl);

            if (tileUnitCtrl == null || tileUnitCtrl.Groups == null)
                return false;

            for (int i = 0; i < tileUnitCtrl.Groups.Count; i++)
            {
                var g = tileUnitCtrl.Groups[i];
                if (g != null && g.groupId == supporter.activeSurroundTargetUnitGroupId)
                    return true;
            }
        }

        return false;
    }
}