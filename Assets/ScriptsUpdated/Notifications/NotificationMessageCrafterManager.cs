using UnityEngine;

/// <summary>
/// Singleton MonoBehaviour that holds the NotificationMessageCrafter asset reference.
/// Place on a GameObject in your ManagerSetup scene.
/// </summary>
public class NotificationMessageCrafterManager : MonoBehaviour
{
    public static NotificationMessageCrafterManager Instance { get; private set; }

    [SerializeField] private NotificationMessageCrafter crafter;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public (string title, string message) Craft(NotificationType type, EnvironmentControl env, int populationLost = 0)
    {
        if (crafter == null) return (type.ToString(), env != null ? env.environmentName : "");
        return crafter.Craft(type, env, populationLost);
    }

    public (string title, string message) CraftResearch(NotificationType type, string techName)
    {
        if (crafter == null)
        {
            return type == NotificationType.ResearchFailed
                ? ("Research Failed",   $"Research on {techName} has failed.")
                : ("Research Complete", $"{techName} has been researched.");
        }
        return crafter.CraftResearch(type, techName);
    }

    public (string title, string message) CraftBirth(NotificationType type, string motherSurname, int bornAlive, bool motherDied)
    {
        if (crafter == null)
        {
            return type switch
            {
                NotificationType.BirthSucceeded => ("A Child is Born", $"The {motherSurname} family welcomes {bornAlive} newborn(s)."),
                _                               => ("Pregnancy Lost",   $"A pregnancy in the {motherSurname} family has failed."),
            };
        }
        return crafter.CraftBirth(type, motherSurname, bornAlive, motherDied);
    }

    public (string title, string message) CraftProduction(NotificationType type, string buildingName, string planName)
    {
        if (crafter == null)
        {
            return type switch
            {
                NotificationType.ProductionCompleted =>
                    ("Production Complete", $"{buildingName} has finished a cycle of {planName}."),
                NotificationType.ProductionPausedLackOfResources =>
                    ("Production Paused",  $"{buildingName} paused — not enough resources for {planName}."),
                NotificationType.ProductionPausedLackOfWorkers =>
                    ("Production Stopped", $"{buildingName} stopped — not enough workers for {planName}."),
                _ => ("Production Issue", buildingName),
            };
        }
        return crafter.CraftProduction(type, buildingName, planName);
    }

    public (string title, string message) CraftCrafting(NotificationType type, string recipeName, string buildingName)
    {
        if (crafter == null)
        {
            return type switch
            {
                NotificationType.CraftingCompleted     => ("Crafting Complete",     $"{buildingName} finished crafting {recipeName}."),
                NotificationType.CraftingFailedWeather => ("Crafting Interrupted",  $"Bad weather stopped {buildingName} from completing {recipeName}."),
                _                                      => ("Crafting", buildingName),
            };
        }
        return crafter.CraftCrafting(type, recipeName, buildingName);
    }

    public (string title, string message) CraftBuilding(NotificationType type, string buildingName)
    {
        if (crafter == null)
        {
            return type switch
            {
                NotificationType.BuildingDamaged   => ("Building Damaged",   $"{buildingName} has been damaged."),
                NotificationType.BuildingDestroyed => ("Building Destroyed", $"{buildingName} has been destroyed."),
                _                                  => ("Construction Complete", $"{buildingName} has been constructed."),
            };
        }
        return crafter.CraftBuilding(type, buildingName);
    }
}
