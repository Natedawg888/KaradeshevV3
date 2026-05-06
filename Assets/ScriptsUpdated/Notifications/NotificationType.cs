/// <summary>
/// Types of in-game notification. Add new values here as systems are expanded.
/// </summary>
public enum NotificationType
{
    GatheringCompleted,
    GatheringFailed,
    DiscoveryCompleted,
    DiscoveryFailed,
    BuildingCompleted,
    BuildingDamaged,
    BuildingDestroyed,

    // Future: CraftingCompleted, ProductionCompleted, ResearchCompleted, etc.
}
