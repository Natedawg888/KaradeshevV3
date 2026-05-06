using UnityEngine;

public enum ScoutEntityType
{
    Unit,
    Animal
}

[System.Serializable]
public class ScoutResultEntry
{
    public string entityName;
    public Sprite icon;
    public int count;

    public ScoutEntityType entityType;

    // Shared flag (used by both units & animals)
    public bool wasMoving;

    // Animal-specific flags
    public bool wasEating;
    public bool wasDrinking;
    public bool wasHunting;
    public bool wasDefending;
    public bool wasTargeted;
    public bool wasAttacking;
    public bool wasFleeing;
}

public interface IScoutResultSource
{
    string GetScoutDisplayName();
    Sprite GetScoutIcon();
    int GetScoutCount();

    // Movement & behaviour flags
    bool GetIsMoving();
    bool GetIsEating();
    bool GetIsDrinking();
    bool GetIsHunting();
    bool GetIsDefending();
    bool GetIsTargeted();

    // NEW: for scout UI
    bool GetIsAttacking();
    bool GetIsFleeing();
}