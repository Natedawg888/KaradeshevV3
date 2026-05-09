public enum SpawnerSourceReason
{
    BaseEnvironment,
    BurntTile,
    AnimalDeath,
    WeatherCreated,
    PlayerAction,
    AnimalPresence
}

[System.Flags]
public enum TileStateFlags
{
    None               = 0,
    HasBeenIgnited     = 1 << 0,
    IsCurrentlyWet     = 1 << 1,
    WasRecentlyFlooded = 1 << 2,
    HasCarcass         = 1 << 3,
    HasVolcanicAsh     = 1 << 4,
    HasFreshDung       = 1 << 5,
    HasActiveAnimal    = 1 << 6,
    IsCurrentlyDry     = 1 << 7,
}

[System.Serializable]
public class ResourceSpawnerRuntime
{
    public ResourceSpawnerDefinition definition;
    public bool isActive = true;
    public SpawnerSourceReason sourceReason = SpawnerSourceReason.BaseEnvironment;

    public int turnsSinceLastSpawn = 0;

    // -1 means unlimited
    public int remainingUses = -1;
    public int remainingLifetimeTurns = -1;

    public bool IsExpired()
    {
        if (definition == null) return true;
        if (!definition.canExpire) return false;
        if (definition.maxUses > 0 && remainingUses <= 0) return true;
        // remainingLifetimeTurns == 0 means the countdown hit zero (was decremented from > 0).
        // -1 means unlimited. Checked here regardless of definition.lifetimeTurns so that
        // AddTemporarySpawner's runtime-only override is always respected.
        if (remainingLifetimeTurns == 0) return true;
        return false;
    }
}
