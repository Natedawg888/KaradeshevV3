using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Kardashev/Unit Actions/Melee Attack", fileName = "MeleeAttackAction")]
public class MeleeAttackActionSO : UnitActionDefinitionSO, IPerTurnUnitAction
{
    [Header("Targets")]
    public bool canTargetAnimals = true;
    public bool canTargetUnitGroups = true;

    [Header("Targeting")]
    [Tooltip("Allow targeting the same tile the attacker is standing on.")]
    public bool allowSameTileTarget = true;

    [Tooltip("Allow targeting adjacent tiles.")]
    public bool allowAdjacentTileTarget = true;

    [Header("Timing")]
    public int durationTurns = 2;

    [Header("Damage (per turn)")]
    public int baseDamagePerTurn = 3;

    [Header("Unit power -> base damage scaling")]
    [Tooltip("If enabled, the acting unit group's power is added into base damage.")]
    public bool addUnitPowerToBaseDamage = true;

    [Tooltip("If enabled, the acting unit group's power also multiplies base damage.")]
    public bool multiplyBaseDamageByUnitPower = false;

    [Tooltip("When additive scaling is enabled, base damage gains (power * this value).")]
    public float unitPowerAdditionScale = 1f;

    [Tooltip("When multiplicative scaling is enabled, base damage is multiplied by (1 + power * this value).")]
    public float unitPowerMultiplierScale = 0.10f;

    [Header("Initiative")]
    public bool useInitiativeRoll = true;

    [Tooltip("Movement used to normalize initiative move score.")]
    public float unitMoveForMaxInitiative = 2.0f;

    [Tooltip("Power used to normalize initiative power score.")]
    public int unitPowerForMaxInitiative = 12;

    [Tooltip("Agility used to normalize initiative agility score.")]
    public int unitAgilityForMaxInitiative = 10;

    [Tooltip("Accuracy used to normalize initiative accuracy score.")]
    public int unitAccuracyForMaxInitiative = 10;

    [Tooltip("Stealth used to normalize initiative stealth score.")]
    public int unitStealthForMaxInitiative = 10;

    [Header("Hit Chance (per turn)")]
    public bool useAccuracyToHit = true;

    [Range(0f, 1f)] public float baseHitChance = 0.55f;
    public float accuracyToHitChance = 0.03f;
    public float evasionToMissChance = 0.02f;

    [Range(0f, 1f)] public float minHitChance = 0.05f;
    [Range(0f, 1f)] public float maxHitChance = 0.95f;

    [Header("Animal retaliation defense mitigation")]
    public float animalRetaliationDefenseMitigationPerPoint = 0.08f;
    [Range(0f, 1f)] public float minAnimalRetaliationMultAfterDefense = 0.0f;

    [Header("Animal size retaliation multipliers")]
    public float smallAnimalRetaliationMult = 0.6f;
    public float mediumAnimalRetaliationMult = 1.0f;
    public float largeAnimalRetaliationMult = 1.6f;
    public float giantAnimalRetaliationMult = 2.4f;

    [Header("Unit vs Unit tuning")]
    [Range(0f, 2f)] public float unitRetaliationMult = 1.0f;

    [Header("Animal flee contest (unit vs animal)")]
    public float unitMoveForMaxChase = 2.0f;
    public int unitPowerForMaxChase = 12;

    public override bool CanUnitUseAction(MilitiaUnit unit) => unit != null;

    public override bool IsValidTarget(TileUnitGroupData group, TileControl originTile, TileControl targetTile)
    {
        if (group == null || originTile == null || targetTile == null)
            return false;

        Vector2Int a = originTile.GetGridPosition();
        Vector2Int b = targetTile.GetGridPosition();

        bool sameTile = a == b;
        if (sameTile)
            return allowSameTileTarget;

        if (!allowAdjacentTileTarget)
            return false;

        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);

        return dx <= 1 && dy <= 1;
    }

    public override int GetTurnCost(TileUnitGroupData group, TileControl originTile, TileControl targetTile)
    {
        return Mathf.Max(1, durationTurns);
    }

    public override void Resolve(TileUnitGroupData group, TileUnitGroupControl owner, TileControl targetTile) { }

    public bool Tick(TileUnitGroupData attacker, TileUnitGroupControl attackerOwner, TileControl tile)
    {
        if (attacker == null || attacker.unitType == null || attackerOwner == null || tile == null)
            return true;

        attacker.meleeRetaliatedLastTick = false;
        attacker.meleeTargetFledLastTick = false;

        int atkCount = Mathf.Max(1, attacker.unitCount);
        int atkPower = attacker.GetEffectivePower();
        int atkDefense = attacker.GetEffectiveDefense();
        int atkAcc = attacker.GetEffectiveAccuracy();

        // --------- ANIMAL TARGET ----------
        if (attacker.activeMeleeTargetType == MeleeTargetType.Animal)
        {
            if (!canTargetAnimals) return true;
            if (attacker.activeMeleeTargetAnimalId < 0) return true;

            var sim = AnimalSimulationAccess.Current;
            if (sim == null) return true;

            var surround = GetSurroundEffectsAgainstAnimal(
                tile,
                attacker.activeMeleeTargetAnimalId,
                attacker.groupId);

            if (!sim.TryGetGroup(attacker.activeMeleeTargetAnimalId, out var animal) || animal == null || !animal.isAlive)
            {
                UnitGroupActionManager.Instance?.CancelAnimalMeleeTarget(attacker);
                return true;
            }

            PlayerReligionManager.Instance?.NotifySacredAnimalAttacked(animal.id, animal.species);

            Vector2Int grid = tile.GetGridPosition();
            TileCoord tileCoord = new TileCoord { x = grid.x, y = grid.y };

            bool targetStillInRange = IsCoordInMeleeRange(tileCoord, animal.tile);
            if (!targetStillInRange)
            {
                attacker.meleeTargetFledLastTick = true;
                UnitGroupActionManager.Instance?.CancelAnimalMeleeTarget(attacker);
                return true;
            }

            bool unitActsFirst = !useInitiativeRoll || RollUnitVsAnimalInitiative(attacker, animal, atkPower);

            if (!unitActsFirst)
            {
                if (TryResolveAnimalReaction(
                    attacker,
                    attackerOwner,
                    sim,
                    ref animal,
                    tileCoord,
                    atkCount,
                    atkPower,
                    atkDefense,
                    surround))
                {
                    return true;
                }
            }

            bool hitThisTurn = RollUnitHitAgainstAnimal(attacker, animal, atkAcc);

            animal.EnsureHealthValid();

            int hpPer = animal.HealthPerAnimal;
            int oldSize = animal.size;

            if (hitThisTurn)
            {
                int damage = GetUnitDamageAgainstAnimal(atkPower);

                int oldHealth = animal.currentHealth;
                animal.currentHealth = Mathf.Max(0, oldHealth - damage);

                int newSize = (animal.currentHealth <= 0)
                    ? 0
                    : Mathf.CeilToInt(animal.currentHealth / (float)hpPer);

                int killed = Mathf.Clamp(oldSize - newSize, 0, oldSize);

                //Debug.Log(
                    //$"[MeleeLootCheck] attacker={attacker.groupId}, oldHealth={oldHealth}, newHealth={animal.currentHealth}, oldSize={oldSize}, newSize={newSize}, killed={killed}");

                if (killed > 0 && animal.species != null && animal.species.lootPerKill != null)
                {
                    AddLootForKills(attacker, animal.species.lootPerKill, killed);
                    PlayerReligionManager.Instance?.NotifySacredAnimalKilled(animal.id, animal.species);
                }

                    animal.size = newSize;
            }

            if (animal.size <= 0)
            {
                UnitGroupActionManager.Instance?.CancelAnimalMeleeTarget(attacker);
                sim.RemoveGroup(animal.id, animal.tile);
                return true;
            }

            if (unitActsFirst)
            {
                if (TryResolveAnimalReaction(
                    attacker,
                    attackerOwner,
                    sim,
                    ref animal,
                    tileCoord,
                    atkCount,
                    atkPower,
                    atkDefense,
                    surround))
                {
                    return true;
                }
            }

            sim.SetGroup(animal);
            return false;
        }

        // --------- UNIT TARGET ----------
        if (attacker.activeMeleeTargetType == MeleeTargetType.Unit)
        {
            if (!canTargetUnitGroups) return true;
            if (string.IsNullOrEmpty(attacker.activeMeleeTargetUnitGroupId)) return true;

            TileControl targetTile = attacker.activeActionTargetTile;
            if (targetTile == null)
            {
                Vector2Int targetGrid = attacker.activeActionTargetGrid;

                if (targetGrid != Vector2Int.zero)
                {
                    if (UnitGroupActionManager.Instance != null)
                        targetTile = UnitGroupActionManager.Instance.FindTileByGridPosition_SLOW(targetGrid);

                    if (targetTile == null)
                    {
                        var allTiles = GameObject.FindObjectsOfType<TileControl>();
                        for (int i = 0; i < allTiles.Length; i++)
                        {
                            var t = allTiles[i];
                            if (t != null && t.GetGridPosition() == targetGrid)
                            {
                                targetTile = t;
                                break;
                            }
                        }
                    }
                }
            }

            if (targetTile == null)
            {
                attacker.meleeTargetFledLastTick = true;
                return true;
            }

            Vector2Int attackerGrid = tile.GetGridPosition();
            Vector2Int defenderGrid = targetTile.GetGridPosition();

            TileCoord attackerCoord = new TileCoord { x = attackerGrid.x, y = attackerGrid.y };
            TileCoord defenderCoord = new TileCoord { x = defenderGrid.x, y = defenderGrid.y };

            if (!IsCoordInMeleeRange(attackerCoord, defenderCoord))
            {
                attacker.meleeTargetFledLastTick = true;
                return true;
            }

            TileUnitGroupControl tileUnitCtrl = null;
            UnitGroupActionManager.Instance?.TryGetUnitControlForTile(targetTile, out tileUnitCtrl);

            if (tileUnitCtrl == null || tileUnitCtrl.Groups == null)
                return true;

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

            var surround = GetSurroundEffectsAgainstUnit(targetTile, target.groupId, attacker.groupId);

            int defCount = Mathf.Max(1, target.unitCount);
            int defPower = target.GetEffectivePower();
            int defDefense = target.GetEffectiveDefense();
            int defAcc = target.GetEffectiveAccuracy();

            bool attackerActsFirst = !useInitiativeRoll || RollUnitVsUnitInitiative(attacker, target, atkPower, defPower);

            if (!attackerActsFirst)
            {
                if (TryResolveUnitStrikeAgainstAttacker(
                    source: target,
                    sourceCount: defCount,
                    sourcePower: defPower,
                    sourceAcc: defAcc,
                    attacker: attacker,
                    attackerOwner: attackerOwner,
                    attackerDefense: atkDefense,
                    retaliationHitBonus: surround.unitRetaliationHitBonus,
                    retaliationDamageBonus: surround.unitRetaliationDamageBonus))
                {
                    return true;
                }
            }

            if (TryResolveUnitStrikeAgainstTarget(
                source: attacker,
                sourceCount: atkCount,
                sourcePower: atkPower,
                sourceAcc: atkAcc,
                target: target,
                targetOwner: tileUnitCtrl,
                targetDefense: defDefense))
            {
                return true;
            }

            if (attackerActsFirst)
            {
                if (TryResolveUnitStrikeAgainstAttacker(
                    source: target,
                    sourceCount: defCount,
                    sourcePower: defPower,
                    sourceAcc: defAcc,
                    attacker: attacker,
                    attackerOwner: attackerOwner,
                    attackerDefense: atkDefense,
                    retaliationHitBonus: surround.unitRetaliationHitBonus,
                    retaliationDamageBonus: surround.unitRetaliationDamageBonus))
                {
                    return true;
                }
            }

            return false;
        }

        return true;
    }

    private bool TryResolveAnimalReaction(
        TileUnitGroupData attacker,
        TileUnitGroupControl attackerOwner,
        AnimalSimulation sim,
        ref AnimalGroupState animal,
        TileCoord attackerTile,
        int atkCount,
        int atkPower,
        int atkDefense,
        SurroundEffectTotals surround)
    {
        var species = animal.species;
        if (species == null)
            return false;

        float aggression = animal.Aggression;
        float flightiness = animal.Flightiness;
        float strength = animal.Strength;
        float defense = animal.Defense;
        float speed = animal.Speed;
        float sense = animal.Sense;
        float stealth = animal.Stealth;

        float outnumberedPressure = Mathf.Clamp01(
            (atkCount - animal.size) / (float)Mathf.Max(1, atkCount));

        float fleeAttemptChance = GetAnimalFleeAttemptChance01(animal, outnumberedPressure);
        fleeAttemptChance = Mathf.Clamp01(fleeAttemptChance * (1f - surround.escapeAttemptReduction));

        float animalEscapeScore = AnimalEscapeScore01(animal);
        float unitChaseScore = UnitChaseScore01(attacker, atkPower);

        float fleeSuccessChance = Mathf.Clamp01(0.5f + (animalEscapeScore - unitChaseScore) * 0.6f);
        fleeSuccessChance = Mathf.Clamp01(fleeSuccessChance * (1f - surround.escapeSuccessReduction));

        bool flees =
            Random.value < fleeAttemptChance &&
            Random.value < fleeSuccessChance;

        if (flees)
        {
            TryApplyAnimalStragglerFromSurround(attacker, ref animal, surround.animalStragglerChance);

            if (animal.size <= 0 || animal.currentHealth <= 0)
            {
                UnitGroupActionManager.Instance?.CancelAnimalMeleeTarget(attacker);
                sim.RemoveGroup(animal.id, animal.tile);
                return true;
            }

            int targetAnimalId = attacker.activeMeleeTargetAnimalId;

            animal.lastAction = AnimalActionType.Flee;
            sim.SetGroup(animal);

            var before = animal.tile;
            sim.ForceStepAwayFromThreat(animal.id, attackerTile);

            if (sim.TryGetGroup(targetAnimalId, out var after) && after != null)
                attacker.meleeTargetFledLastTick = !after.tile.Equals(before);
            else
                attacker.meleeTargetFledLastTick = true;

            UnitGroupActionManager.Instance?.CancelAnimalMeleeTarget(attacker);
            return true;
        }

        float retaliationChance = Mathf.Clamp01(GetAnimalRetaliationChance01(animal) + surround.animalRetaliationBonus);

        bool retaliationHit = true;
        if (useAccuracyToHit)
        {
            int animalAcc = GetAnimalAccuracyPoints(animal);
            float attackerEvasionPts = GetUnitEvasionPoints(attacker);
            float retaliationHitChance = ComputeHitChance01(animalAcc, attackerEvasionPts);
            retaliationHit = Random.value < retaliationHitChance;
        }

        if (Random.value < retaliationChance && retaliationHit)
        {
            animal.lastAction = AnimalActionType.DefendAnimal;

            float sizeMult = GetSizeRetaliationMult(species.sizeCategory);

            int rawRetaliation = GetAnimalRetaliationDamage(
                animal,
                sizeMult,
                aggression,
                strength,
                defense,
                speed,
                sense,
                stealth,
                flightiness);

            float defenseMult = 1f / (1f + atkDefense * animalRetaliationDefenseMitigationPerPoint);
            defenseMult = Mathf.Max(minAnimalRetaliationMultAfterDefense, defenseMult);

            int retaliation = Mathf.RoundToInt(rawRetaliation * defenseMult);
            retaliation = Mathf.Max(0, retaliation);

            if (retaliation > 0)
                attacker.meleeRetaliatedLastTick = true;

            attacker.currentHealth = Mathf.Max(0, attacker.currentHealth - retaliation);
            if (attacker.currentHealth <= 0)
            {
                attackerOwner.RemoveGroupDueToFatalities(attacker);
                return true;
            }
        }

        return false;
    }

    private bool TryResolveUnitStrikeAgainstTarget(
        TileUnitGroupData source,
        int sourceCount,
        int sourcePower,
        int sourceAcc,
        TileUnitGroupData target,
        TileUnitGroupControl targetOwner,
        int targetDefense)
    {
        if (source == null || source.unitType == null || target == null || target.unitType == null || targetOwner == null)
            return true;

        float targetEvasionPts = GetUnitEvasionPoints(target);
        bool hitThisTurn = true;

        if (useAccuracyToHit)
        {
            float hitChance = ComputeHitChance01(sourceAcc, targetEvasionPts);
            hitThisTurn = Random.value < hitChance;
        }

        int damageToTarget = 0;
        if (hitThisTurn)
        {
            damageToTarget = ComputeUnitDamageAgainstUnit(sourceCount, sourcePower, targetDefense);
        }

        ApplyDamageToUnitGroup(target, damageToTarget, out int unitsKilled);

        if (unitsKilled > 0 && target.unitType.lootPerUnitKilled != null)
            AddLootForKills(source, target.unitType.lootPerUnitKilled, unitsKilled);

        if (target.currentHealth <= 0)
        {
            targetOwner.RemoveGroupDueToFatalities(target);
            return true;
        }

        return false;
    }

    private bool TryResolveUnitStrikeAgainstAttacker(
        TileUnitGroupData source,
        int sourceCount,
        int sourcePower,
        int sourceAcc,
        TileUnitGroupData attacker,
        TileUnitGroupControl attackerOwner,
        int attackerDefense,
        float retaliationHitBonus,
        float retaliationDamageBonus)
    {
        if (source == null || source.unitType == null || attacker == null || attacker.unitType == null || attackerOwner == null)
            return true;

        float attackerEvasionPts = GetUnitEvasionPoints(attacker);
        bool retaliationHit = true;

        if (useAccuracyToHit)
        {
            float hitChance = ComputeHitChance01(sourceAcc, attackerEvasionPts);
            hitChance = Mathf.Clamp01(hitChance + retaliationHitBonus);
            retaliationHit = Random.value < hitChance;
        }

        int damageBack = 0;
        if (retaliationHit)
        {
            damageBack = Mathf.RoundToInt(
                ComputeUnitDamageAgainstUnit(sourceCount, sourcePower, attackerDefense) *
                unitRetaliationMult *
                (1f + Mathf.Max(0f, retaliationDamageBonus)));

            damageBack = Mathf.Max(0, damageBack);
        }

        if (retaliationHit && damageBack > 0)
        {
            attacker.meleeRetaliatedLastTick = true;

            ApplyDamageToUnitGroup(attacker, damageBack, out int unitsKilled);

            if (unitsKilled > 0 && attacker.unitType.lootPerUnitKilled != null)
                AddLootForKills(source, attacker.unitType.lootPerUnitKilled, unitsKilled);

            if (attacker.currentHealth <= 0)
            {
                attackerOwner.RemoveGroupDueToFatalities(attacker);
                return true;
            }
        }
        else
        {
            attacker.meleeRetaliatedLastTick = false;
        }

        return false;
    }

    private void ApplyDamageToUnitGroup(TileUnitGroupData target, int damage, out int unitsKilled)
    {
        unitsKilled = 0;

        if (target == null || target.unitType == null)
            return;

        int perUnitHp = Mathf.Max(1, target.unitType.maxHealth + target.bonusHealth);
        int oldHealth = Mathf.Max(0, target.currentHealth);

        int newHealth = Mathf.Max(0, oldHealth - Mathf.Max(0, damage));

        int beforeUnitsAlive = oldHealth <= 0 ? 0 : Mathf.CeilToInt(oldHealth / (float)perUnitHp);
        int afterUnitsAlive = newHealth <= 0 ? 0 : Mathf.CeilToInt(newHealth / (float)perUnitHp);
        unitsKilled = Mathf.Max(0, beforeUnitsAlive - afterUnitsAlive);

        target.currentHealth = newHealth;

        if (unitsKilled > 0)
        {
            target.unitCount = Mathf.Max(0, target.unitCount - unitsKilled);
            target.maxHealth = perUnitHp * Mathf.Max(0, target.unitCount);
            target.currentHealth = Mathf.Clamp(target.currentHealth, 0, Mathf.Max(0, target.maxHealth));
        }
    }

    private bool RollUnitHitAgainstAnimal(TileUnitGroupData attacker, AnimalGroupState animal, int atkAcc)
    {
        if (!useAccuracyToHit)
            return true;

        float animalEvasionPts = GetAnimalEvasionPoints(animal);
        float hitChance = ComputeHitChance01(atkAcc, animalEvasionPts);
        return Random.value < hitChance;
    }

    private int GetUnitDamageAgainstAnimal(int unitPower)
    {
        return Mathf.Max(1, GetScaledBaseDamageForUnitPower(unitPower));
    }

    private int ComputeUnitDamageAgainstUnit(int sourceCount, int sourcePower, int targetDefense)
    {
        int scaledBaseDamage = GetScaledBaseDamageForUnitPower(sourcePower);

        return Mathf.Max(
            1,
            Mathf.RoundToInt(
                scaledBaseDamage *
                Mathf.Max(1, sourceCount) *
                Mathf.Max(0.25f, sourcePower / Mathf.Max(1f, targetDefense))));
    }

    private int GetScaledBaseDamageForUnitPower(int unitPower)
    {
        float damage = Mathf.Max(1f, baseDamagePerTurn);
        float power = Mathf.Max(0f, unitPower);

        if (addUnitPowerToBaseDamage)
            damage += power * Mathf.Max(0f, unitPowerAdditionScale);

        if (multiplyBaseDamageByUnitPower)
            damage *= Mathf.Max(1f, 1f + power * Mathf.Max(0f, unitPowerMultiplierScale));

        return Mathf.Max(1, Mathf.RoundToInt(damage));
    }

    private bool RollUnitVsAnimalInitiative(TileUnitGroupData attacker, AnimalGroupState animal, int atkPower)
    {
        float unitScore = GetUnitInitiativeScore01(attacker, atkPower);
        float animalScore = GetAnimalInitiativeScore01(animal);

        float diff = unitScore - animalScore;
        float unitChance = Mathf.Clamp01(0.5f + diff * 0.5f);

        return Random.value < unitChance;
    }

    private bool RollUnitVsUnitInitiative(TileUnitGroupData attacker, TileUnitGroupData target, int atkPower, int defPower)
    {
        float attackerScore = GetUnitInitiativeScore01(attacker, atkPower);
        float targetScore = GetUnitInitiativeScore01(target, defPower);

        float diff = attackerScore - targetScore;
        float attackerChance = Mathf.Clamp01(0.5f + diff * 0.5f);

        return Random.value < attackerChance;
    }

    private float GetUnitInitiativeScore01(TileUnitGroupData group, int power)
    {
        if (group == null || group.unitType == null)
            return 0.5f;

        float move = group.GetEffectiveMovementSpeed();
        float moveScore = Mathf.Clamp01(move / Mathf.Max(0.01f, unitMoveForMaxInitiative));

        int agi = group.unitType.agility + group.bonusAgility;
        float agilityScore = Mathf.Clamp01(agi / (float)Mathf.Max(1, unitAgilityForMaxInitiative));

        int acc = group.unitType.accuracy + group.bonusAccuracy;
        float accuracyScore = Mathf.Clamp01(acc / (float)Mathf.Max(1, unitAccuracyForMaxInitiative));

        int stl = group.unitType.stealth + group.bonusStealth;
        float stealthScore = Mathf.Clamp01(stl / (float)Mathf.Max(1, unitStealthForMaxInitiative));

        float powerScore = Mathf.Clamp01(power / (float)Mathf.Max(1, unitPowerForMaxInitiative));
        float healthScore = GetUnitHealthFraction01(group);

        float score =
            moveScore * 0.28f +
            agilityScore * 0.28f +
            accuracyScore * 0.16f +
            stealthScore * 0.08f +
            powerScore * 0.20f;

        score -= (1f - healthScore) * 0.20f;

        return Mathf.Clamp01(score);
    }

    private float GetAnimalInitiativeScore01(AnimalGroupState group)
    {
        if (group == null || group.species == null)
            return 0.5f;

        float speedScore = Mathf.Clamp01(group.Speed) * GetSizeSpeedMult(group.species.sizeCategory);
        float senseScore = Mathf.Clamp01(group.Sense);
        float strengthScore = Mathf.Clamp01(group.Strength);
        float aggressionScore = Mathf.Clamp01(group.Aggression);
        float stealthScore = Mathf.Clamp01(group.Stealth);
        float healthScore = GetAnimalHealthFraction01(group);

        float score =
            speedScore * 0.45f +
            senseScore * 0.20f +
            strengthScore * 0.15f +
            aggressionScore * 0.10f +
            stealthScore * 0.10f;

        score -= (1f - healthScore) * 0.20f;

        return Mathf.Clamp01(score);
    }

    private float GetAnimalHealthFraction01(AnimalGroupState group)
    {
        if (group == null)
            return 1f;

        group.EnsureHealthValid();

        int max = group.MaxHealth;
        if (max <= 0)
            return 1f;

        return Mathf.Clamp01(group.currentHealth / (float)max);
    }

    private float GetUnitHealthFraction01(TileUnitGroupData group)
    {
        if (group == null || group.unitType == null)
            return 1f;

        int max = Mathf.Max(1, group.maxHealth);
        return Mathf.Clamp01(group.currentHealth / (float)max);
    }

    private bool IsCoordInMeleeRange(TileCoord a, TileCoord b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);

        if (dx == 0 && dy == 0)
            return allowSameTileTarget;

        if (!allowAdjacentTileTarget)
            return false;

        return dx <= 1 && dy <= 1;
    }

    private float ComputeHitChance01(int attackerAccuracy, float targetEvasionPoints)
    {
        float chance = baseHitChance
                       + (attackerAccuracy * accuracyToHitChance)
                       - (targetEvasionPoints * evasionToMissChance);

        return Mathf.Clamp(chance, minHitChance, maxHitChance);
    }

    private float GetAnimalEvasionPoints(AnimalGroupState group)
    {
        if (group == null || group.species == null)
            return 0f;

        float speed = Mathf.Clamp01(group.Speed);
        float flightiness = Mathf.Clamp01(group.Flightiness);
        float stealth = Mathf.Clamp01(group.Stealth);
        float sense = Mathf.Clamp01(group.Sense);

        float score =
            speed * 0.40f +
            flightiness * 0.30f +
            stealth * 0.20f +
            sense * 0.10f;

        return score * 10f;
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
            {
                //Debug.Log($"[MeleeLoot] Skipped unknown resource '{d.resource.name}' for attacker {attacker.groupId}.");
                continue;
            }

            int amount = d.amountPerKill * kills;
            attacker.AddPendingLoot(d.resource, amount);

            //Debug.Log(
                //$"[MeleeLoot] Attacker={attacker.groupId} gained {amount}x {d.resource.name} " +
                //$"for {kills} kills. Pending now={attacker.GetPendingLootAmount(d.resource)}");
        }
    }

    private float GetSizeRetaliationMult(AnimalSizeCategory size)
    {
        return size switch
        {
            AnimalSizeCategory.Small => smallAnimalRetaliationMult,
            AnimalSizeCategory.Medium => mediumAnimalRetaliationMult,
            AnimalSizeCategory.Large => largeAnimalRetaliationMult,
            AnimalSizeCategory.Giant => giantAnimalRetaliationMult,
            _ => 1.0f
        };
    }

    private float GetSizeSpeedMult(AnimalSizeCategory size)
    {
        return size switch
        {
            AnimalSizeCategory.Small => 1.25f,
            AnimalSizeCategory.Medium => 1.00f,
            AnimalSizeCategory.Large => 0.85f,
            AnimalSizeCategory.Giant => 0.70f,
            _ => 1.0f
        };
    }

    private float AnimalEscapeScore01(AnimalGroupState group)
    {
        if (group == null || group.species == null)
            return 0.5f;

        float sp = Mathf.Clamp01(group.Speed) * GetSizeSpeedMult(group.species.sizeCategory);
        float fl = Mathf.Clamp01(group.Flightiness);
        float st = Mathf.Clamp01(group.Strength);
        float se = Mathf.Clamp01(group.Sense);
        float th = Mathf.Clamp01(group.Stealth);

        return Mathf.Clamp01(
            sp * 0.35f +
            fl * 0.30f +
            th * 0.15f +
            se * 0.10f +
            st * 0.10f
        );
    }

    private float GetAnimalFleeAttemptChance01(AnimalGroupState group, float outnumberedPressure)
    {
        if (group == null || group.species == null)
            return 0.5f;

        float flightiness = Mathf.Clamp01(group.Flightiness);
        float speed = Mathf.Clamp01(group.Speed);
        float sense = Mathf.Clamp01(group.Sense);
        float aggression = Mathf.Clamp01(group.Aggression);
        float strength = Mathf.Clamp01(group.Strength);

        float chance =
            flightiness * 0.55f +
            speed * 0.15f +
            sense * 0.10f +
            outnumberedPressure * 0.30f -
            aggression * 0.20f -
            strength * 0.10f;

        return Mathf.Clamp01(chance);
    }

    private float UnitChaseScore01(TileUnitGroupData attacker, int atkPower)
    {
        float move = attacker.GetEffectiveMovementSpeed();
        float move01 = Mathf.Clamp01(move / Mathf.Max(0.01f, unitMoveForMaxChase));

        float pow01 = Mathf.Clamp01(atkPower / (float)Mathf.Max(1, unitPowerForMaxChase));

        return Mathf.Clamp01(move01 * 0.7f + pow01 * 0.3f);
    }

    private float GetUnitEvasionPoints(TileUnitGroupData g)
    {
        if (g == null || g.unitType == null) return 0f;

        int agi = g.unitType.agility + g.bonusAgility;
        int stl = g.unitType.stealth + g.bonusStealth;

        return agi + (stl * 0.5f);
    }

    private int GetAnimalAccuracyPoints(AnimalGroupState group)
    {
        if (group == null || group.species == null)
            return 0;

        float score =
            Mathf.Clamp01(group.Speed) * 0.35f +
            Mathf.Clamp01(group.Sense) * 0.35f +
            Mathf.Clamp01(group.Strength) * 0.15f +
            Mathf.Clamp01(group.Aggression) * 0.10f +
            Mathf.Clamp01(group.Stealth) * 0.05f;

        return Mathf.RoundToInt(score * 10f);
    }

    private float GetAnimalRetaliationChance01(AnimalGroupState group)
    {
        if (group == null || group.species == null)
            return 0f;

        float aggression = Mathf.Clamp01(group.Aggression);
        float strength = Mathf.Clamp01(group.Strength);
        float defense = Mathf.Clamp01(group.Defense);
        float speed = Mathf.Clamp01(group.Speed);
        float flightiness = Mathf.Clamp01(group.Flightiness);

        float chance =
            aggression * 0.50f +
            strength * 0.20f +
            defense * 0.20f +
            speed * 0.15f -
            flightiness * 0.15f;

        return Mathf.Clamp01(chance);
    }

    private int GetAnimalRetaliationDamage(
        AnimalGroupState group,
        float sizeMult,
        float aggression,
        float strength,
        float defense,
        float speed,
        float sense,
        float stealth,
        float flightiness)
    {
        if (group == null || group.species == null)
            return 0;

        float profile =
            strength * 0.40f +
            defense * 0.25f +
            aggression * 0.20f +
            speed * 0.10f +
            sense * 0.05f;

        profile = Mathf.Clamp01(profile);

        float behaviorScale = 0.5f + (profile * 1.5f);

        if (flightiness > aggression)
            behaviorScale *= Mathf.Lerp(1f, 0.8f, Mathf.Clamp01(flightiness - aggression));

        int damage = Mathf.RoundToInt(baseDamagePerTurn * behaviorScale * Mathf.Max(0.1f, sizeMult));
        return Mathf.Max(1, damage);
    }

    private struct SurroundEffectTotals
    {
        public int supporterCount;
        public float escapeAttemptReduction;
        public float escapeSuccessReduction;
        public float animalRetaliationBonus;
        public float unitRetaliationHitBonus;
        public float unitRetaliationDamageBonus;
        public float animalStragglerChance;
    }

    private SurroundEffectTotals GetSurroundEffectsAgainstAnimal(
        TileControl targetTile,
        int animalGroupId,
        string ignoreGroupId)
    {
        SurroundEffectTotals fx = default;

        if (targetTile == null || animalGroupId < 0)
            return fx;

        TileUnitGroupControl tileUnitCtrl = null;
        UnitGroupActionManager.Instance?.TryGetUnitControlForTile(targetTile, out tileUnitCtrl);

        if (tileUnitCtrl == null || tileUnitCtrl.Groups == null)
            return fx;

        for (int i = 0; i < tileUnitCtrl.Groups.Count; i++)
        {
            var g = tileUnitCtrl.Groups[i];
            if (g == null || g.groupId == ignoreGroupId)
                continue;

            if (g.remainingActionTurns <= 0)
                continue;

            if (!(g.activeAction is SurroundActionSO surround))
                continue;

            if (!surround.IsMatchingStoredTarget(g, MeleeTargetType.Animal, animalGroupId, null))
                continue;

            fx.supporterCount++;
            fx.escapeAttemptReduction = Mathf.Clamp01(fx.escapeAttemptReduction + surround.GetEscapeAttemptReduction01(g));
            fx.escapeSuccessReduction = Mathf.Clamp01(fx.escapeSuccessReduction + surround.GetEscapeSuccessReduction01(g));
            fx.animalRetaliationBonus = Mathf.Clamp01(fx.animalRetaliationBonus + surround.GetAnimalRetaliationBonus01(g));
            fx.unitRetaliationHitBonus = Mathf.Clamp01(fx.unitRetaliationHitBonus + surround.GetUnitRetaliationHitBonus01(g));
            fx.unitRetaliationDamageBonus += surround.GetUnitRetaliationDamageBonus01(g);
            fx.animalStragglerChance = Mathf.Clamp01(fx.animalStragglerChance + surround.GetAnimalStragglerChance01(g));
        }

        fx.unitRetaliationDamageBonus = Mathf.Clamp(fx.unitRetaliationDamageBonus, 0f, 1.5f);
        return fx;
    }

    private SurroundEffectTotals GetSurroundEffectsAgainstUnit(
        TileControl targetTile,
        string targetUnitGroupId,
        string ignoreGroupId)
    {
        SurroundEffectTotals fx = default;

        if (targetTile == null || string.IsNullOrEmpty(targetUnitGroupId))
            return fx;

        TileUnitGroupControl tileUnitCtrl = null;
        UnitGroupActionManager.Instance?.TryGetUnitControlForTile(targetTile, out tileUnitCtrl);

        if (tileUnitCtrl == null || tileUnitCtrl.Groups == null)
            return fx;

        for (int i = 0; i < tileUnitCtrl.Groups.Count; i++)
        {
            var g = tileUnitCtrl.Groups[i];
            if (g == null || g.groupId == ignoreGroupId)
                continue;

            if (g.remainingActionTurns <= 0)
                continue;

            if (!(g.activeAction is SurroundActionSO surround))
                continue;

            if (!surround.IsMatchingStoredTarget(g, MeleeTargetType.Unit, -1, targetUnitGroupId))
                continue;

            fx.supporterCount++;
            fx.escapeAttemptReduction = Mathf.Clamp01(fx.escapeAttemptReduction + surround.GetEscapeAttemptReduction01(g));
            fx.escapeSuccessReduction = Mathf.Clamp01(fx.escapeSuccessReduction + surround.GetEscapeSuccessReduction01(g));
            fx.animalRetaliationBonus = Mathf.Clamp01(fx.animalRetaliationBonus + surround.GetAnimalRetaliationBonus01(g));
            fx.unitRetaliationHitBonus = Mathf.Clamp01(fx.unitRetaliationHitBonus + surround.GetUnitRetaliationHitBonus01(g));
            fx.unitRetaliationDamageBonus += surround.GetUnitRetaliationDamageBonus01(g);
        }

        fx.unitRetaliationDamageBonus = Mathf.Clamp(fx.unitRetaliationDamageBonus, 0f, 1.5f);
        return fx;
    }

    private void TryApplyAnimalStragglerFromSurround(
        TileUnitGroupData attacker,
        ref AnimalGroupState animal,
        float stragglerChance)
    {
        if (attacker == null || animal == null)
            return;

        if (animal.size <= 1)
            return;

        if (stragglerChance <= 0f)
            return;

        if (Random.value >= stragglerChance)
            return;

        animal.EnsureHealthValid();

        int hpPer = Mathf.Max(1, animal.HealthPerAnimal);
        int oldSize = animal.size;

        animal.currentHealth = Mathf.Max(0, animal.currentHealth - hpPer);
        animal.size = animal.currentHealth <= 0
            ? 0
            : Mathf.CeilToInt(animal.currentHealth / (float)hpPer);

        int killed = Mathf.Clamp(oldSize - animal.size, 0, oldSize);
        if (killed > 0 && animal.species != null && animal.species.lootPerKill != null)
            AddLootForKills(attacker, animal.species.lootPerKill, killed);
    }
}
