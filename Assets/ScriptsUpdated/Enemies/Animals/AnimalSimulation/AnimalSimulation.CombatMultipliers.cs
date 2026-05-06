using UnityEngine;

public partial class AnimalSimulation
{
    private static float SizeCombatMult(AnimalSizeCategory size)
    {
        return size switch
        {
            AnimalSizeCategory.Small => 0.6f,
            AnimalSizeCategory.Medium => 1.0f,
            AnimalSizeCategory.Large => 1.6f,
            AnimalSizeCategory.Giant => 2.4f,
            _ => 1.0f
        };
    }

    private static float StrengthCombatMult(float strength01)
    {
        strength01 = Mathf.Clamp01(strength01);
        return Mathf.Lerp(0.6f, 2.0f, strength01);
    }

    private static float SizeSpeedMult(AnimalSizeCategory size)
    {
        return size switch
        {
            AnimalSizeCategory.Small => 1.25f,
            AnimalSizeCategory.Medium => 1.00f,
            AnimalSizeCategory.Large => 0.85f,
            AnimalSizeCategory.Giant => 0.70f,
            _ => 1.00f
        };
    }

    private static float GroupMobility01(int size)
    {
        size = Mathf.Max(1, size);
        return Mathf.Clamp01(1f / (1f + Mathf.Log(size + 1f) * 0.25f));
    }

    private float EscapeScore01(AnimalGroupState group)
    {
        var def = group.species;
        if (def == null) return 0.5f;

        float sp = Mathf.Clamp01(group.Speed) * SizeSpeedMult(def.sizeCategory);
        float st = Mathf.Clamp01(group.Strength);

        float baseScore = Mathf.Clamp01(sp * 0.7f + st * 0.3f);

        float agePenalty = GetAgeEscapePenalty01(group);

        return Mathf.Clamp01(baseScore - agePenalty);
    }

    private float EscapeSuccessChance01(AnimalGroupState runner, AnimalGroupState chaser)
    {
        float r = EscapeScore01(runner);
        float c = EscapeScore01(chaser);

        float diff = r - c;
        return Mathf.Clamp01(0.5f + diff * 0.6f);
    }

    private bool RollEscape(
        AnimalGroupState runner,
        AnimalGroupState chaser,
        float baseAttemptChance01)
    {
        if (_rng.NextDouble() >= Mathf.Clamp01(baseAttemptChance01))
            return false;

        float successChance = EscapeSuccessChance01(runner, chaser);

        float runnerMob = GroupMobility01(runner.size);
        float chaserMob = GroupMobility01(chaser.size);

        successChance *= runnerMob;
        successChance = Mathf.Lerp(0f, successChance, chaserMob);

        return _rng.NextDouble() < Mathf.Clamp01(successChance);
    }

    private float GetPredatorAmbushBonus01(AnimalGroupState attacker, AnimalGroupState defender)
    {
        var attackerDef = attacker.species;
        var defenderDef = defender.species;

        if (attackerDef == null || defenderDef == null)
            return 0f;

        float attackerStealth = Mathf.Clamp01(attacker.Stealth);
        float defenderSense = Mathf.Clamp01(defender.Sense);

        float attackerWeakness = GetGroupWeakness01(attacker);
        float defenderWeakness = GetGroupWeakness01(defender);

        attackerStealth *= Mathf.Lerp(1f, 0.65f, attackerWeakness);
        defenderSense *= Mathf.Lerp(1f, 0.75f, defenderWeakness);

        float diff = attackerStealth - defenderSense;

        return Mathf.Clamp(diff * 0.35f, -0.20f, 0.20f);
    }

    private float GetPredatorConflictInitiativeScore01(AnimalGroupState group)
    {
        var def = group.species;
        if (def == null)
            return 0.5f;

        float speedScore = Mathf.Clamp01(group.Speed) * SizeSpeedMult(def.sizeCategory);
        float aggressionScore = Mathf.Clamp01(group.Aggression);
        float strengthScore = Mathf.Clamp01(group.Strength);
        float senseScore = Mathf.Clamp01(group.Sense);

        float weaknessPenalty = GetGroupWeakness01(group);

        float score =
            speedScore * 0.40f +
            aggressionScore * 0.20f +
            senseScore * 0.25f +
            strengthScore * 0.15f;

        score -= weaknessPenalty * 0.30f;

        return Mathf.Clamp01(score);
    }

    private bool RollPredatorConflictInitiative(AnimalGroupState attacker, AnimalGroupState defender)
    {
        float attackerScore = GetPredatorConflictInitiativeScore01(attacker);
        float defenderScore = GetPredatorConflictInitiativeScore01(defender);

        float ambushBonus = GetPredatorAmbushBonus01(attacker, defender);

        float diff = (attackerScore + ambushBonus) - defenderScore;

        float attackerChance = Mathf.Clamp01(0.5f + diff * 0.5f);

        return _rng.NextDouble() < attackerChance;
    }

    private int CalculatePredatorConflictKills(
        AnimalGroupState source,
        AnimalGroupState target,
        float minWeaknessMult)
    {
        var def = source.species;
        if (def == null || source.size <= 0 || target.size <= 0)
            return 0;

        float sourcePower = GetGroupCombatPower(source, includeWeakness: true);
        float targetPower = GetGroupCombatPower(target, includeWeakness: true);

        if (sourcePower <= 0f)
            return 0;

        float aggressionFactor = 0.5f + source.Aggression * 0.5f;
        float weakness = GetGroupWeakness01(source);
        float weaknessMult = Mathf.Lerp(1f, minWeaknessMult, weakness);

        float powerRatio = sourcePower / Mathf.Max(1f, targetPower);
        float ratioMult = Mathf.Clamp(powerRatio, 0.35f, 2.5f);

        int baseKills = Mathf.Max(1, Mathf.FloorToInt(source.size * 0.15f));

        int kills = Mathf.Max(
            1,
            Mathf.FloorToInt(baseKills * aggressionFactor * ratioMult * weaknessMult));

        kills = Mathf.Min(kills, Mathf.Max(1, Mathf.CeilToInt(target.size * 0.35f)));

        return Mathf.Min(kills, target.size);
    }

    private int ConvertEquivalentKillsToDamage(AnimalGroupState target, int equivalentKills)
    {
        if (equivalentKills <= 0)
            return 0;

        int hpPerAnimal = Mathf.Max(1, target.HealthPerAnimal);

        const float damagePerEquivalentKill = 1.10f;

        return Mathf.Max(1,
            Mathf.RoundToInt(equivalentKills * hpPerAnimal * damagePerEquivalentKill));
    }

    private int ApplyDamageToGroup(ref AnimalGroupState target, int damage)
    {
        if (damage <= 0 || !target.isAlive)
            return 0;

        target.EnsureHealthValid();

        int oldSize = target.size;
        int oldHealth = Mathf.Max(0, target.currentHealth);

        int appliedDamage = Mathf.Min(damage, oldHealth);
        target.currentHealth = oldHealth - appliedDamage;

        if (target.currentHealth <= 0)
        {
            target.currentHealth = 0;
            target.size = 0;
            return oldSize;
        }

        target.size = Mathf.CeilToInt(target.currentHealth / (float)target.HealthPerAnimal);
        return Mathf.Max(0, oldSize - target.size);
    }

    private void DealPredatorCombatDamage(
        ref AnimalGroupState attacker,
        ref AnimalGroupState target,
        float minWeaknessMult)
    {
        int kills = CalculatePredatorConflictKills(attacker, target, minWeaknessMult);
        if (kills <= 0)
            return;

        target.size = Mathf.Max(0, target.size - kills);

        target.EnsureHealthValid();

        target.currentHealth = Mathf.Clamp(
            target.currentHealth - kills * Mathf.Max(1, target.HealthPerAnimal),
            0,
            target.MaxHealth);

        if (target.size <= 0)
        {
            target.currentHealth = 0;
        }
        else
        {
            target.EnsureHealthValid();
        }
    }

    private bool TryLeaveEscapeStragglers(ref AnimalGroupState runner, TileCoord stragglerTile)
    {
        var def = runner.species;
        if (def == null)
            return false;

        if (!def.canLeaveStragglersOnEscape)
            return false;

        if (runner.size <= 1)
            return false;

        if (HasReachedGroupCap())
            return false;

        float weakness = GetGroupWeakness01(runner);
        float chance = Mathf.Clamp01(
            def.baseEscapeSplitChance +
            weakness * def.maxExtraEscapeSplitChanceFromWeakness);

        if (_rng.NextDouble() >= chance)
            return false;

        int maxAllowed = Mathf.Min(Mathf.Max(1, def.maxEscapeStragglers), runner.size - 1);
        int minAllowed = Mathf.Clamp(def.minEscapeStragglers, 1, maxAllowed);

        if (maxAllowed < 1 || minAllowed > maxAllowed)
            return false;

        int stragglerCount = _rng.Next(minAllowed, maxAllowed + 1);

        // IMPORTANT:
        // AnimalGroupState is a class, so do not do: AnimalGroupState stragglers = runner;
        // That would copy the reference, not clone the state.
        AnimalGroupState stragglers = CreateCopiedGroupState(runner);
        stragglers.id = _nextGroupId++;
        stragglers.tile = stragglerTile;
        stragglers.lastAction = AnimalActionType.Idle;

        stragglers.isHunting = false;
        stragglers.huntingTargetGroupId = -1;
        stragglers.isTargetedByPredator = false;
        stragglers.huntingEscapeCount = 0;

        stragglers.isInPredatorConflict = false;
        stragglers.predatorConflictTargetGroupId = -1;

        stragglers.targetedByPredatorGroupId = -1;

        stragglers.isFleeingFromThreat = false;
        stragglers.fleeFromPredatorGroupId = -1;
        stragglers.fleeUntilDistanceTiles = 0;
        stragglers.fleeThreatLastKnownTile = stragglerTile;

        stragglers.isRaidingPlayerTile = false;
        stragglers.isHuntingHumanUnits = false;
        stragglers.huntingHumanUnitGroupId = null;

        stragglers.fleeStepsRemaining = 0;

        stragglers.hasWaterSearchMemory = false;
        stragglers.lastWaterSearchPreviousTile = stragglerTile;
        stragglers.secondLastWaterSearchPreviousTile = stragglerTile;
        stragglers.waterSearchBacktrackAvoidanceTurns = 0;

        stragglers.raidTargetTile = stragglerTile;

        SplitGroupHealthBySizeNonCombat(ref runner, ref stragglers, stragglerCount);

        runner.EnsureHealthValid();
        stragglers.EnsureHealthValid();

        _groups[stragglers.id] = stragglers;
        AddToTileIndex(stragglers.id, stragglers.tile);
        OnGroupCreated?.Invoke(stragglers);

        return true;
    }

    private float GetGroupHealthFraction01(AnimalGroupState group)
    {
        if (group.MaxHealth <= 0)
            return 1f;

        return Mathf.Clamp01(group.currentHealth / (float)group.MaxHealth);
    }

    private float GetCombatHitChance01(
        AnimalGroupState attacker,
        AnimalGroupState defender,
        bool isHuntingAttack)
    {
        var attackerDef = attacker.species;
        var defenderDef = defender.species;

        if (attackerDef == null || defenderDef == null)
            return 0.65f;

        float attackerSpeed = Mathf.Clamp01(attacker.Speed) * SizeSpeedMult(attackerDef.sizeCategory);
        float attackerSense = Mathf.Clamp01(attacker.Sense);
        float attackerStrength = Mathf.Clamp01(attacker.Strength);
        float attackerAggression = Mathf.Clamp01(attacker.Aggression);
        float attackerWeakness = GetGroupWeakness01(attacker);

        float defenderSpeed = Mathf.Clamp01(defender.Speed) * SizeSpeedMult(defenderDef.sizeCategory);
        float defenderStealth = Mathf.Clamp01(defender.Stealth);
        float defenderSense = Mathf.Clamp01(defender.Sense);
        float defenderDefense = Mathf.Clamp01(defender.Defense);
        float defenderFlightiness = Mathf.Clamp01(defender.Flightiness);
        float defenderWeakness = GetGroupWeakness01(defender);

        float sizeReachBonus = ((int)attackerDef.sizeCategory - (int)defenderDef.sizeCategory) * 0.04f;
        float ambushBonus = isHuntingAttack ? GetPredatorAmbushBonus01(attacker, defender) * 0.50f : 0f;

        float accuracy =
            attackerSpeed * 0.28f +
            attackerSense * 0.24f +
            attackerStrength * 0.22f +
            attackerAggression * 0.10f;

        float evasion =
            defenderSpeed * 0.24f +
            defenderStealth * 0.22f +
            defenderSense * 0.10f +
            defenderDefense * 0.18f +
            defenderFlightiness * 0.08f;

        accuracy -= attackerWeakness * 0.20f;
        evasion -= defenderWeakness * 0.10f;

        float chance = 0.65f + (accuracy - evasion) * 0.45f + sizeReachBonus + ambushBonus;

        return Mathf.Clamp(chance, 0.20f, 0.95f);
    }

    private static float DefenseCombatMult(float defense01)
    {
        defense01 = Mathf.Clamp01(defense01);
        return Mathf.Lerp(0.6f, 2.0f, defense01);
    }

    private static float SpeedCombatMult(float speed01, AnimalSizeCategory size)
    {
        speed01 = Mathf.Clamp01(speed01);

        float sizedSpeed = speed01 * SizeSpeedMult(size);
        sizedSpeed = Mathf.Clamp01(sizedSpeed);

        return Mathf.Lerp(0.75f, 1.50f, sizedSpeed);
    }

    private bool RollCombatHit(
        AnimalGroupState attacker,
        AnimalGroupState defender,
        bool isHuntingAttack)
    {
        float chance = GetCombatHitChance01(attacker, defender, isHuntingAttack);
        return _rng.NextDouble() < chance;
    }

    private static bool IsPredatorLike(AnimalDefinition species)
    {
        if (species == null)
            return false;

        return species.diet == AnimalDiet.Carnivore ||
               species.diet == AnimalDiet.Omnivore;
    }

    public void SetWorldSpeciesGroupCapMultiplier(float multiplier)
    {
        _worldSpeciesGroupCapMultiplier = Mathf.Max(0.1f, multiplier);

        // Re-apply immediately if groups already exist.
        EnforceAllSpeciesGroupCaps();
    }

    private int GetEffectiveSpeciesGroupCap(AnimalDefinition species)
    {
        if (species == null)
            return 0;

        int baseCap = Mathf.Max(0, species.maxLiveGroupsOnMap);

        // 0 still means "no species cap"
        if (baseCap <= 0)
            return 0;

        float scaled = baseCap * _worldSpeciesGroupCapMultiplier;

        // If a species has a cap, keep the effective result at least 1.
        return Mathf.Max(1, Mathf.RoundToInt(scaled));
    }
}