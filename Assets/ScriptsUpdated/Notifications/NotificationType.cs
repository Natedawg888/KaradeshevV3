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
    BuildingOnFire,
    BuildingDamaged,
    BuildingDestroyed,
    ResearchCompleted,
    ResearchFailed,
    BirthSucceeded,
    BirthFailed,

    ProductionCompleted,
    ProductionPausedLackOfResources,
    ProductionPausedLackOfWorkers,

    CraftingCompleted,
    CraftingFailedWeather,

    FireFightSucceeded,
    FireFightFailed,

    BuildingFlooded,

    PopulationAgedUp,
    ElderDiedOfOldAge,

    DiseaseOutbreak,
}
