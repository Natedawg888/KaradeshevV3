using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "Kardashev/Unit Actions/Ranged Attack", fileName = "RangedAttackAction")]
public class RangedAttackActionSO : UnitActionDefinitionSO, IPerTurnUnitAction
{
    [Header("Targets")]
    public bool canTargetAnimals = true;

    [FormerlySerializedAs("canTargetUnits")]
    public bool canTargetUnitGroups = true;

    [Header("Timing")]
    [Min(1)] public int durationTurns = 2;

    [Header("Damage (per turn)")]
    [Min(0)] public int baseDamagePerTurn = 2;

    [Header("Range (tiles)")]
    [Tooltip("0 = use unit's (range + bonusRange). If > 0, this caps the range.")]
    [Min(0)] public int maxRangeInTiles = 0;

    // ✅ Hit chance (per turn) based on Accuracy + Range (+ distance penalty)
    [Header("Hit Chance (per turn)")]
    public bool useHitChance = true;

    [Range(0f, 1f)] public float baseHitChance = 0.55f;
    [Tooltip("Adds to hit chance per effective Accuracy point.")]
    public float accuracyToHitChance = 0.03f;

    [Tooltip("Adds to hit chance per effective Range point.")]
    public float rangeToHitChance = 0.02f;

    [Tooltip("Subtracts hit chance per tile of distance (Manhattan distance).")]
    public float distancePenaltyPerTile = 0.05f;

    [Range(0f, 1f)] public float minHitChance = 0.05f;
    [Range(0f, 1f)] public float maxHitChance = 0.95f;

    private static readonly List<TileControl> _rangeTiles = new(256);

    // Robust caches for “sibling canvas” setups
    private static readonly Dictionary<Vector2Int, TileControl> _tileByGrid = new(4096);
    private static readonly Dictionary<Vector2Int, TileUnitGroupControl> _unitCtrlByGrid = new(2048);

    public override bool CanUnitUseAction(MilitiaUnit unit) => unit != null;

    public override bool IsValidTarget(TileUnitGroupData group, TileControl originTile, TileControl targetTile)
    {
        if (group == null || group.unitType == null) return false;
        if (originTile == null || targetTile == null) return false;

        int r = GetEffectiveTileRange(group);
        return IsTileInRangeBfs(originTile, targetTile, r);
    }

    public override int GetTurnCost(TileUnitGroupData group, TileControl originTile, TileControl targetTile)
        => Mathf.Max(1, durationTurns);

    public override void Resolve(TileUnitGroupData group, TileUnitGroupControl owner, TileControl targetTile) { }

    public int GetEffectiveTileRange(TileUnitGroupData group)
    {
        if (group == null || group.unitType == null) return 0;

        int unitRange = Mathf.Max(0, group.unitType.range + group.bonusRange);

        if (maxRangeInTiles <= 0) return unitRange;
        if (unitRange <= 0) return 0;

        return Mathf.Min(unitRange, maxRangeInTiles);
    }

    public bool Tick(TileUnitGroupData attacker, TileUnitGroupControl attackerOwner, TileControl targetTile)
    {
        if (attacker == null || attacker.unitType == null || attackerOwner == null) return true;
        if (targetTile == null) return true;

        attacker.meleeRetaliatedLastTick = false;
        attacker.meleeTargetFledLastTick = false;

        var originTile = ResolveTileFromOwner(attackerOwner);
        if (originTile == null)
        {
            attacker.meleeTargetFledLastTick = true;
            return true;
        }

        int r = GetEffectiveTileRange(attacker);
        if (!IsTileInRangeBfs(originTile, targetTile, r))
        {
            attacker.meleeTargetFledLastTick = true;
            return true;
        }

        int atkPower = attacker.unitType.power + attacker.bonusPower;
        int atkRange = attacker.unitType.range + attacker.bonusRange;

        int atkAcc = attacker.unitType.accuracy + attacker.bonusAccuracy;
        float distTiles = ComputeManhattanDistanceTiles(originTile, targetTile);

        bool hitThisTurn = true;
        if (useHitChance)
        {
            float hitChance = ComputeHitChance01(atkAcc, atkRange, distTiles);
            hitThisTurn = UnityEngine.Random.value < hitChance;
        }

        // ------------------- ANIMAL TARGET -------------------
        if (attacker.activeMeleeTargetType == MeleeTargetType.Animal)
        {
            if (!canTargetAnimals) return true;
            if (attacker.activeMeleeTargetAnimalId < 0) return true;

            var sim = AnimalSimulationAccess.Current;
            if (sim == null) return true;

            if (!sim.TryGetGroup(attacker.activeMeleeTargetAnimalId, out var animal) ||
                animal == null || !animal.isAlive || animal.species == null)
                return true;

            Vector2Int grid = targetTile.GetGridPosition();
            TileCoord coord = new TileCoord { x = grid.x, y = grid.y };

            if (animal.tile.x != coord.x || animal.tile.y != coord.y)
            {
                attacker.meleeTargetFledLastTick = true;
                return true;
            }

            if (hitThisTurn)
            {
                int damage = Mathf.Max(1, Mathf.RoundToInt(baseDamagePerTurn * Mathf.Max(1f, atkPower * 0.25f)));

                int killed = ApplyDamageToAnimalGroup(ref animal, damage);

                if (killed > 0 && animal.species != null && animal.species.lootPerKill != null)
                    AddLootForKills(attacker, animal.species.lootPerKill, killed);

                if (animal.size <= 0)
                {
                    sim.RemoveGroup(animal.id, animal.tile);
                    return true;
                }
            }

            // ✅ IMPORTANT: SetGroup so marker HP bar updates immediately after damage
            sim.SetGroup(animal);
            return false;
        }

        // ------------------- UNIT TARGET -------------------
        if (attacker.activeMeleeTargetType == MeleeTargetType.Unit)
        {
            if (!canTargetUnitGroups) return true;
            if (string.IsNullOrEmpty(attacker.activeMeleeTargetUnitGroupId)) return true;

            var tileUnitCtrl = ResolveUnitGroupControlOnTile(targetTile);
            if (tileUnitCtrl == null || tileUnitCtrl.Groups == null) return true;

            TileUnitGroupData target = null;
            for (int i = 0; i < tileUnitCtrl.Groups.Count; i++)
            {
                var g = tileUnitCtrl.Groups[i];
                if (g != null && g.groupId == attacker.activeMeleeTargetUnitGroupId)
                {
                    target = g;
                    break;
                }
            }

            if (target == null || target.unitType == null)
            {
                attacker.meleeTargetFledLastTick = true;
                return true;
            }

            if (!hitThisTurn)
                return false; // miss -> keep going next tick

            int atkCount = Mathf.Max(1, attacker.unitCount);
            int defDefense = target.unitType.defense + target.bonusDefense;

            int perUnitHp = Mathf.Max(1, target.unitType.maxHealth + target.bonusHealth);
            int oldHealth = Mathf.Max(0, target.currentHealth);

            float offense = atkPower + (atkRange * 0.5f);
            int damageToTarget = Mathf.Max(
                1,
                Mathf.RoundToInt(baseDamagePerTurn * atkCount * Mathf.Max(0.25f, offense / Mathf.Max(1f, defDefense)))
            );

            int newHealth = Mathf.Max(0, oldHealth - damageToTarget);

            int beforeUnitsAlive = oldHealth <= 0 ? 0 : Mathf.CeilToInt(oldHealth / (float)perUnitHp);
            int afterUnitsAlive = newHealth <= 0 ? 0 : Mathf.CeilToInt(newHealth / (float)perUnitHp);
            int unitsKilled = Mathf.Max(0, beforeUnitsAlive - afterUnitsAlive);

            target.currentHealth = newHealth;

            if (unitsKilled > 0 && target.unitType.lootPerUnitKilled != null)
                AddLootForKills(attacker, target.unitType.lootPerUnitKilled, unitsKilled);

            if (unitsKilled > 0)
            {
                target.unitCount = Mathf.Max(0, target.unitCount - unitsKilled);
                target.maxHealth = perUnitHp * Mathf.Max(0, target.unitCount);
                target.currentHealth = Mathf.Clamp(target.currentHealth, 0, Mathf.Max(0, target.maxHealth));
            }

            if (target.currentHealth <= 0)
            {
                tileUnitCtrl.RemoveGroupDueToFatalities(target);
                return true;
            }

            return false;
        }

        return true;
    }

    private float ComputeHitChance01(int attackerAccuracy, int attackerRange, float distTiles)
    {
        float chance =
            baseHitChance +
            (attackerAccuracy * accuracyToHitChance) +
            (attackerRange * rangeToHitChance) -
            (distTiles * distancePenaltyPerTile);

        return Mathf.Clamp(chance, minHitChance, maxHitChance);
    }

    private float ComputeManhattanDistanceTiles(TileControl origin, TileControl target)
    {
        if (origin == null || target == null) return 0f;

        Vector2Int a = origin.GetGridPosition();
        Vector2Int b = target.GetGridPosition();

        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private bool IsTileInRangeBfs(TileControl origin, TileControl target, int maxRange)
    {
        if (origin == null || target == null) return false;
        if (origin == target) return true;

        maxRange = Mathf.Max(0, maxRange);
        if (maxRange <= 0) return false;

        var mgr = UnitGroupActionManager.Instance;
        if (mgr == null) return false;

        _rangeTiles.Clear();
        mgr.CollectTilesInRangeBFS(origin, maxRange, _rangeTiles, includeOrigin: true);

        for (int i = 0; i < _rangeTiles.Count; i++)
            if (_rangeTiles[i] == target)
                return true;

        return false;
    }

    // sibling canvas helpers
    private TileControl ResolveTileFromOwner(TileUnitGroupControl owner)
    {
        if (owner == null) return null;

        var parentTile = owner.GetComponentInParent<TileControl>();
        if (parentTile != null) return parentTile;

        var gridMgr = UnityEngine.Object.FindObjectOfType<GridManager>();
        if (gridMgr == null) return null;

        Vector2Int gp = gridMgr.GetGridPosition(owner.transform.position);
        if (TryGetTileAtGrid(gp, out var tile)) return tile;

        return null;
    }

    private bool TryGetTileAtGrid(Vector2Int gp, out TileControl tile)
    {
        tile = null;

        if (_tileByGrid.TryGetValue(gp, out tile) && tile != null) return true;

        RebuildTileCache();

        if (_tileByGrid.TryGetValue(gp, out tile) && tile != null) return true;

        tile = null;
        return false;
    }

    private void RebuildTileCache()
    {
        _tileByGrid.Clear();

        var tiles = UnityEngine.Object.FindObjectsOfType<TileControl>();
        for (int i = 0; i < tiles.Length; i++)
        {
            var tc = tiles[i];
            if (tc == null) continue;
            _tileByGrid[tc.GetGridPosition()] = tc;
        }
    }

    private TileUnitGroupControl ResolveUnitGroupControlOnTile(TileControl tile)
    {
        if (tile == null) return null;

        var direct = tile.GetComponentInChildren<TileUnitGroupControl>();
        if (direct != null) return direct;

        var gp = tile.GetGridPosition();

        if (_unitCtrlByGrid.TryGetValue(gp, out var ctrl) && ctrl != null) return ctrl;

        RebuildUnitCtrlCache();

        if (_unitCtrlByGrid.TryGetValue(gp, out ctrl) && ctrl != null) return ctrl;

        return null;
    }

    private void RebuildUnitCtrlCache()
    {
        _unitCtrlByGrid.Clear();

        var gridMgr = UnityEngine.Object.FindObjectOfType<GridManager>();
        var ctrls = UnityEngine.Object.FindObjectsOfType<TileUnitGroupControl>();

        for (int i = 0; i < ctrls.Length; i++)
        {
            var c = ctrls[i];
            if (c == null) continue;

            var pt = c.GetComponentInParent<TileControl>();
            if (pt != null)
            {
                _unitCtrlByGrid[pt.GetGridPosition()] = c;
                continue;
            }

            if (gridMgr != null)
            {
                var gp = gridMgr.GetGridPosition(c.transform.position);
                _unitCtrlByGrid[gp] = c;
            }
        }
    }

    private void AddLootForKills(TileUnitGroupData attacker, IList<ResourceLootEntry> drops, int kills)
    {
        if (attacker == null || drops == null || kills <= 0) return;

        var knownMgr = PlayerKnownResourcesManager.Instance;

        for (int i = 0; i < drops.Count; i++)
        {
            var d = drops[i];
            if (d.resource == null) continue;
            if (d.amountPerKill <= 0) continue;

            if (knownMgr != null && !knownMgr.IsKnown(d.resource))
                continue;

            attacker.AddPendingLoot(d.resource, d.amountPerKill * kills);
        }
    }

    private static int ApplyDamageToAnimalGroup(ref AnimalGroupState g, int damage)
    {
        if (g == null || damage <= 0) return 0;

        g.EnsureHealthValid();

        int hpPer = Mathf.Max(1, g.HealthPerAnimal);

        int oldHealth = Mathf.Max(0, g.currentHealth);
        int oldAlive = oldHealth <= 0 ? 0 : Mathf.CeilToInt(oldHealth / (float)hpPer);

        g.currentHealth = Mathf.Max(0, oldHealth - damage);

        int newAlive = g.currentHealth <= 0 ? 0 : Mathf.CeilToInt(g.currentHealth / (float)hpPer);

        int killed = Mathf.Clamp(oldAlive - newAlive, 0, Mathf.Max(0, g.size));

        g.size = newAlive;

        int newMax = Mathf.Max(0, g.MaxHealth);
        g.currentHealth = Mathf.Clamp(g.currentHealth, 0, newMax);

        return killed;
    }
}