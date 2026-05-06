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
    ResearchCompleted,
    ResearchFailed,

    // Future: CraftingCompleted, ProductionCompleted, etc.
}
