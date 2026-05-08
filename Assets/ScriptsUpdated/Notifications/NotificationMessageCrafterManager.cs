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

    public (string title, string message) CraftFlood(string buildingName, string depthLabel)
    {
        if (crafter == null)
            return ("Building Flooded", $"{buildingName} is being flooded ({depthLabel} water).");
        return crafter.CraftFlood(buildingName, depthLabel);
    }

    public (string title, string message) CraftFireFight(NotificationType type, string targetName, int casualties)
    {
        if (crafter == null)
        {
            return type switch
            {
                NotificationType.FireFightSucceeded => ("Fire Extinguished!", casualties > 0
                    ? $"Your people put out the fire at {targetName}. {casualties} worker(s) were lost."
                    : $"Your people put out the fire at {targetName} without casualties."),
                NotificationType.FireFightFailed    => ("Fire Fight Failed",  $"All workers at {targetName} were lost to the flames."),
                _                                   => ("Fire", targetName),
            };
        }
        return crafter.CraftFireFight(type, targetName, casualties);
    }

    public (string title, string message) CraftAging(AgeGroup newGroup, int count)
    {
        if (crafter == null)
        {
            string gn = newGroup switch
            {
                AgeGroup.Teen  => "teenagers",
                AgeGroup.Adult => "adults",
                AgeGroup.Elder => "elders",
                _              => newGroup.ToString().ToLower() + "s",
            };
            return ("People are Growing Up", $"{count} of your people have become {gn}.");
        }
        return crafter.CraftAging(newGroup, count);
    }

    public (string title, string message) CraftElderDeath(int count, int lifespanTurns)
    {
        if (crafter == null)
            return ("Elders Passed", $"{count} elder(s) have died of old age after {lifespanTurns} turns.");
        return crafter.CraftElderDeath(count, lifespanTurns);
    }

    public (string title, string message) CraftDiseaseOutbreak(string diseaseName, string causeType)
    {
        if (crafter == null)
            return ("Disease Outbreak!", $"{diseaseName} has appeared in your population.");
        return crafter.CraftDiseaseOutbreak(diseaseName, causeType);
    }

    public (string title, string message) CraftDiseaseKilled(string diseaseName, string surname)
    {
        if (crafter == null)
            return ("Death from Disease", $"{surname} has died from {diseaseName}.");
        return crafter.CraftDiseaseKilled(diseaseName, surname);
    }

    public (string title, string message) CraftUnitTrainingCompleted(string unitName, int count)
    {
        if (crafter == null)
            return ("Training Complete", $"{count} {unitName}(s) are ready for deployment.");
        return crafter.CraftUnitTrainingCompleted(unitName, count);
    }

    public (string title, string message) CraftUnitMovementCompleted(string groupName, string unitName)
    {
        if (crafter == null)
            return ("Movement Complete", $"{groupName} has reached their destination.");
        return crafter.CraftUnitMovementCompleted(groupName, unitName);
    }

    public (string title, string message) CraftSpiritSummoned(string spiritName)
    {
        if (crafter == null)
            return ("Spirit Summoned", $"{spiritName} has been summoned and accepted.");
        return crafter.CraftSpiritSummoned(spiritName);
    }

    public (string title, string message) CraftSpiritOfferingMade(string spiritName, int favorChange)
    {
        if (crafter == null)
        {
            string s = favorChange >= 0 ? $"+{favorChange}" : favorChange.ToString();
            return ("Offering Made", $"An offering was made to {spiritName} ({s} favor).");
        }
        return crafter.CraftSpiritOfferingMade(spiritName, favorChange);
    }

    public (string title, string message) CraftSpiritMoodChanged(string spiritName, string newMood, string previousMood)
    {
        if (crafter == null)
            return ("Spirit Mood Changed", $"{spiritName} is now {newMood}.");
        return crafter.CraftSpiritMoodChanged(spiritName, newMood, previousMood);
    }

    public (string title, string message) CraftUnitGroupDestroyed(string groupName, string unitName)
    {
        if (crafter == null)
            return ("Unit Lost", $"{groupName} has been destroyed.");
        return crafter.CraftUnitGroupDestroyed(groupName, unitName);
    }

    public (string title, string message) CraftUnitAttackActionCompleted(string groupName, string unitName, string actionName)
    {
        if (crafter == null)
            return ("Attack Complete", $"{groupName} has finished their {actionName}.");
        return crafter.CraftUnitAttackActionCompleted(groupName, unitName, actionName);
    }

    public (string title, string message) CraftUnitTargetedByAnimal(string groupName, string unitName, string speciesName)
    {
        if (crafter == null)
            return ("Under Attack!", $"{groupName} is being attacked by {speciesName}.");
        return crafter.CraftUnitTargetedByAnimal(groupName, unitName, speciesName);
    }

    public (string title, string message) CraftUnitTrainingFailedWeather(string unitName, int count, string cause)
    {
        if (crafter == null)
            return ("Training Disrupted", $"{count} {unitName}(s) lost their training due to {cause}.");
        return crafter.CraftUnitTrainingFailedWeather(unitName, count, cause);
    }

    public (string title, string message) CraftUnitSkillTrainingCompleted(string groupName, string unitName, int skillLevel)
    {
        if (crafter == null)
            return ("Training Complete", $"{groupName} has completed training and reached skill level {skillLevel}.");
        return crafter.CraftUnitSkillTrainingCompleted(groupName, unitName, skillLevel);
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
