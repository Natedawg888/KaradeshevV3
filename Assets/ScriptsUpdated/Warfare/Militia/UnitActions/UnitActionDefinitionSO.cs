using UnityEngine;

public abstract class UnitActionDefinitionSO : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Unique ID for this action, e.g. 'SCOUT_TILE'")]
    public string actionID;

    public string displayName;
    public Sprite icon;

    // ---------------------------------------------------------
    // ✅ Optional requirements (set per action in inspector)
    // ---------------------------------------------------------
    [Header("Requirements (Optional)")]
    public bool requireSkillLevel;
    [Min(0)] public int minSkillLevel;

    public bool requireHealth;
    [Min(0)] public int minHealth; // per-unit health (unitType.maxHealth + bonusHealth)

    public bool requireMovement;
    [Min(0f)] public float minMovement; // movementSpeed + bonusMovementSpeed

    public bool requirePower;
    [Min(0)] public int minPower;

    public bool requireDefense;
    [Min(0)] public int minDefense;

    public bool requireAgility;
    [Min(0)] public int minAgility;

    public bool requireAccuracy;
    [Min(0)] public int minAccuracy;

    public bool requireRange;
    [Min(0)] public int minRange;

    public bool requireStealth;
    [Min(0)] public int minStealth;

    public virtual bool MeetsRequirements(TileUnitGroupData group)
    {
        if (group == null || group.unitType == null)
            return false;

        var u = group.unitType;

        int effHealth    = Mathf.Max(0, u.maxHealth + group.bonusHealth);
        float effMove    = u.movementSpeed + group.bonusMovementSpeed;
        int effPower     = Mathf.Max(0, u.power + group.bonusPower);
        int effDefense   = Mathf.Max(0, u.defense + group.bonusDefense);
        int effAgility   = Mathf.Max(0, u.agility + group.bonusAgility);
        int effAccuracy  = Mathf.Max(0, u.accuracy + group.bonusAccuracy);
        int effRange     = Mathf.Max(0, u.range + group.bonusRange);
        int effStealth   = Mathf.Max(0, u.stealth + group.bonusStealth);
        int effSkill     = Mathf.Max(0, group.skillLevel);

        if (requireSkillLevel && effSkill < minSkillLevel) return false;
        if (requireHealth     && effHealth < minHealth)    return false;
        if (requireMovement   && effMove < minMovement)    return false;
        if (requirePower      && effPower < minPower)      return false;
        if (requireDefense    && effDefense < minDefense)  return false;
        if (requireAgility    && effAgility < minAgility)  return false;
        if (requireAccuracy   && effAccuracy < minAccuracy)return false;
        if (requireRange      && effRange < minRange)      return false;
        if (requireStealth    && effStealth < minStealth)  return false;

        return true;
    }

    public abstract bool CanUnitUseAction(MilitiaUnit unit);

    public abstract bool IsValidTarget(
        TileUnitGroupData group,
        TileControl originTile,
        TileControl targetTile
    );

    public abstract int GetTurnCost(
        TileUnitGroupData group,
        TileControl originTile,
        TileControl targetTile
    );

    public abstract void Resolve(
        TileUnitGroupData group,
        TileUnitGroupControl owner,
        TileControl targetTile
    );
}