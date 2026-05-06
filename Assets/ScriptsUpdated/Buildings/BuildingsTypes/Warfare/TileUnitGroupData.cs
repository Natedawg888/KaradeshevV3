using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct TrackingTargetRuntime
{
    public TileControl tile;
    public Sprite icon;
}

[Serializable]
public struct PendingLootStack
{
    public ResourceDefinition resource;
    public int amount;
}

[Serializable]
public class TileUnitGroupData
{
    public string groupId;
    public MilitiaUnit unitType;

    public string groupName;

    public int unitCount;
    public int maxHealth;
    public int currentHealth;

    public int skillLevel;

    public int bonusHealth;
    public float bonusMovementSpeed;
    public int bonusPower;
    public int bonusDefense;
    public int bonusAgility;
    public int bonusAccuracy;
    public int bonusRange;
    public int bonusStealth;

    public string populationReservationId;
    public int reservedPopulation;

    public int expiryTurn = -1;
    public bool HasExpiry => expiryTurn >= 0;

    public int missedUpkeepTurns;
    public int upkeepStartTurn = -1;

    [NonSerialized] public List<Vector2Int> plannedPathGridPositions = new();
    [NonSerialized] public List<float> plannedStepTurnCosts = new();
    [NonSerialized] public int currentPathIndex = 0;
    [NonSerialized] public float remainingTurnCostOnCurrentStep = 0f;

    [NonSerialized] public bool isPatrolling = false;
    [NonSerialized] public List<Vector2Int> patrolLoopGridPositions = new();
    [NonSerialized] public List<float> patrolLoopStepTurnCosts = new();

    [NonSerialized] public UnitActionDefinitionSO activeAction = null;
    [NonSerialized] public Vector2Int activeActionTargetGrid;
    [NonSerialized] public TileControl activeActionTargetTile = null;
    [NonSerialized] public int remainingActionTurns = 0;

    [HideInInspector] public List<ScoutResultEntry> lastScoutResults;
    [HideInInspector] public bool hasPendingScoutResults;

    // -------------------- TRACKING --------------------
    [HideInInspector] public List<TrackingResultEntry> lastTrackingAnimalResults;
    [HideInInspector] public List<TrackingResultEntry> lastTrackingUnitResults;
    [HideInInspector] public bool hasPendingTrackingResults;
    [HideInInspector] public int lastTrackingMarkerTurns = 1;

    // ✅ runtime-only: what THIS group is currently tracking (so it can be cleared on expiry)
    [NonSerialized] public List<TrackingTargetRuntime> activeTrackingTargets = new();

    [NonSerialized] public MeleeTargetType activeMeleeTargetType = MeleeTargetType.None;
    [NonSerialized] public int activeMeleeTargetAnimalId = -1;          // AnimalGroupState.id
    [NonSerialized] public string activeMeleeTargetUnitGroupId = null;  // TileUnitGroupData.groupId

    [NonSerialized] public MeleeTargetType activeSurroundTargetType = MeleeTargetType.None;
    [NonSerialized] public int activeSurroundTargetAnimalId = -1;
    [NonSerialized] public string activeSurroundTargetUnitGroupId = null;

    // ---- MELEE (runtime) ----
    [NonSerialized] public bool meleeRetaliatedLastTick = false;
    [NonSerialized] public bool meleeTargetFledLastTick = false;

    public bool IsTrackingTargetActive(TileControl tile, Sprite icon)
    {
        if (tile == null || icon == null) return false;
        if (activeTrackingTargets == null) return false;

        for (int i = 0; i < activeTrackingTargets.Count; i++)
        {
            var t = activeTrackingTargets[i];
            if (t.tile == tile && t.icon == icon)
                return true;
        }

        return false;
    }

    public void ClearMeleeState()
    {
        activeMeleeTargetType = MeleeTargetType.None;
        activeMeleeTargetAnimalId = -1;
        activeMeleeTargetUnitGroupId = null;

        meleeRetaliatedLastTick = false;
        meleeTargetFledLastTick = false;
    }

    public void AddTrackingTarget(TileControl tile, Sprite icon)
    {
        if (tile == null || icon == null) return;

        if (activeTrackingTargets == null)
            activeTrackingTargets = new List<TrackingTargetRuntime>(8);

        if (IsTrackingTargetActive(tile, icon))
            return;

        activeTrackingTargets.Add(new TrackingTargetRuntime { tile = tile, icon = icon });
    }

    public void RemoveTrackingTarget(TileControl tile, Sprite icon)
    {
        if (tile == null || icon == null) return;
        if (activeTrackingTargets == null || activeTrackingTargets.Count == 0) return;

        for (int i = activeTrackingTargets.Count - 1; i >= 0; i--)
        {
            var t = activeTrackingTargets[i];
            if (t.tile == tile && t.icon == icon)
                activeTrackingTargets.RemoveAt(i);
        }
    }

    public TileUnitGroupData(string groupId, MilitiaUnit unitType, int unitCount)
    {
        this.groupId = groupId;
        this.unitType = unitType;
        this.unitCount = Mathf.Max(1, unitCount);

        missedUpkeepTurns = 0;
        upkeepStartTurn = -1;

        isPatrolling = false;

        activeAction = null;
        activeActionTargetGrid = Vector2Int.zero;
        activeActionTargetTile = null;
        remainingActionTurns = 0;

        if (unitType != null)
            skillLevel = Mathf.Clamp(unitType.startingSkillLevel, 0, unitType.maxSkillLevel);
        else
            skillLevel = 0;

        RecalculateMaxHealth(keepCurrentFraction: false);
    }

    public TileUnitGroupData(
        string groupId,
        MilitiaUnit unitType,
        int unitCount,
        string populationReservationId,
        int reservedPopulation,
        int expiryTurn = -1)
        : this(groupId, unitType, unitCount)
    {
        this.populationReservationId = populationReservationId;
        this.reservedPopulation = reservedPopulation;
        this.expiryTurn = expiryTurn;
    }

    public bool UsesPopulation =>
        !string.IsNullOrEmpty(populationReservationId) && reservedPopulation > 0;

    public float HealthFraction => maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;

    public int RemainingUpkeepTolerance
    {
        get
        {
            if (unitType == null || unitType.maxMissedUpkeepTurns <= 0) return 0;
            return Mathf.Max(0, unitType.maxMissedUpkeepTurns - missedUpkeepTurns);
        }
    }

    public void RecalculateMaxHealth(bool keepCurrentFraction)
    {
        int basePerUnit = (unitType != null ? unitType.maxHealth : 1);
        int perUnitTotal = Mathf.Max(1, basePerUnit + bonusHealth);

        int oldMax = maxHealth;
        float fraction = HealthFraction;

        maxHealth = perUnitTotal * Mathf.Max(1, unitCount);

        if (!keepCurrentFraction || oldMax <= 0)
        {
            currentHealth = maxHealth;
        }
        else
        {
            currentHealth = Mathf.Clamp(
                Mathf.RoundToInt(fraction * maxHealth),
                1,
                maxHealth
            );
        }
    }

    [NonSerialized] public List<PendingLootStack> pendingLoot = new();

    public bool HasPendingLoot => pendingLoot != null && pendingLoot.Count > 0;

    public void AddPendingLoot(ResourceDefinition def, int amount)
    {
        if (def == null || amount <= 0) return;

        if (pendingLoot == null)
            pendingLoot = new List<PendingLootStack>(8);

        for (int i = 0; i < pendingLoot.Count; i++)
        {
            if (pendingLoot[i].resource == def)
            {
                var s = pendingLoot[i];
                s.amount += amount;
                pendingLoot[i] = s;
                return;
            }
        }

        pendingLoot.Add(new PendingLootStack { resource = def, amount = amount });
    }

    public int GetPendingLootAmount(ResourceDefinition def)
    {
        if (def == null || pendingLoot == null) return 0;
        for (int i = 0; i < pendingLoot.Count; i++)
            if (pendingLoot[i].resource == def)
                return pendingLoot[i].amount;
        return 0;
    }

    public int RemovePendingLoot(ResourceDefinition def, int amount)
    {
        if (def == null || amount <= 0 || pendingLoot == null) return 0;

        for (int i = 0; i < pendingLoot.Count; i++)
        {
            if (pendingLoot[i].resource != def) continue;

            var s = pendingLoot[i];
            int taken = Mathf.Clamp(amount, 0, s.amount);

            s.amount -= taken;

            if (s.amount <= 0)
                pendingLoot.RemoveAt(i);
            else
                pendingLoot[i] = s;

            return taken;
        }

        return 0;
    }

    public void ClearPendingLoot()
    {
        if (pendingLoot != null)
            pendingLoot.Clear();
    }

    public void ClearSurroundState()
    {
        activeSurroundTargetType = MeleeTargetType.None;
        activeSurroundTargetAnimalId = -1;
        activeSurroundTargetUnitGroupId = null;
    }

    public void ClearCombatActionState()
    {
        ClearMeleeState();
        ClearSurroundState();
    }

    private float GetReligionMultiplier(SpiritEffectType effectType)
    {
        PlayerReligionManager religion = PlayerReligionManager.Instance;
        if (religion == null)
            return 1f;

        return Mathf.Max(0f, religion.GetMultiplierProduct(effectType));
    }

    public int GetEffectivePower()
    {
        int raw = Mathf.Max(0, (unitType != null ? unitType.power : 0) + bonusPower);
        float mult = GetReligionMultiplier(SpiritEffectType.UnitAttackMultiplier);
        return Mathf.Max(0, Mathf.RoundToInt(raw * mult));
    }

    public int GetEffectiveDefense()
    {
        int raw = Mathf.Max(0, (unitType != null ? unitType.defense : 0) + bonusDefense);
        float mult = GetReligionMultiplier(SpiritEffectType.UnitDefenseMultiplier);
        return Mathf.Max(0, Mathf.RoundToInt(raw * mult));
    }

    public int GetEffectiveAccuracy()
    {
        int raw = Mathf.Max(0, (unitType != null ? unitType.accuracy : 0) + bonusAccuracy);
        float mult = GetReligionMultiplier(SpiritEffectType.UnitAccuracyMultiplier);
        return Mathf.Max(0, Mathf.RoundToInt(raw * mult));
    }

    public float GetEffectiveMovementSpeed()
    {
        float raw = Mathf.Max(0f, (unitType != null ? unitType.movementSpeed : 0f) + bonusMovementSpeed);
        float mult = GetReligionMultiplier(SpiritEffectType.UnitMovementMultiplier);
        return Mathf.Max(0f, raw * mult);
    }

    public void ClearMovementAndActionStateForTornado()
    {
        if (plannedPathGridPositions != null)
            plannedPathGridPositions.Clear();

        if (plannedStepTurnCosts != null)
            plannedStepTurnCosts.Clear();

        currentPathIndex = 0;
        remainingTurnCostOnCurrentStep = 0f;

        isPatrolling = false;

        if (patrolLoopGridPositions != null)
            patrolLoopGridPositions.Clear();

        if (patrolLoopStepTurnCosts != null)
            patrolLoopStepTurnCosts.Clear();

        activeAction = null;
        activeActionTargetGrid = Vector2Int.zero;
        activeActionTargetTile = null;
        remainingActionTurns = 0;

        ClearCombatActionState();
    }

    public int ApplyDamageAndReturnUnitsLost(int damage)
    {
        if (damage <= 0 || unitCount <= 0)
            return 0;

        int oldUnitCount = unitCount;

        currentHealth = Mathf.Max(0, currentHealth - damage);

        int perUnitHealth = Mathf.Max(1, (unitType != null ? unitType.maxHealth : 1) + bonusHealth);

        int survivingUnits = currentHealth > 0
            ? Mathf.CeilToInt(currentHealth / (float)perUnitHealth)
            : 0;

        survivingUnits = Mathf.Clamp(survivingUnits, 0, oldUnitCount);

        if (survivingUnits <= 0)
        {
            unitCount = 0;
            currentHealth = 0;
            maxHealth = 0;
            return oldUnitCount;
        }

        if (survivingUnits != oldUnitCount)
        {
            unitCount = survivingUnits;
            maxHealth = perUnitHealth * survivingUnits;
            currentHealth = Mathf.Clamp(currentHealth, 1, maxHealth);
        }

        return Mathf.Max(0, oldUnitCount - survivingUnits);
    }
}