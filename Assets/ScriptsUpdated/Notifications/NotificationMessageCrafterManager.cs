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

    public (string title, string message) CraftBuilding(string buildingName)
    {
        if (crafter == null) return ("Construction Complete", $"{buildingName} has been constructed.");
        return crafter.CraftBuilding(buildingName);
    }
}
