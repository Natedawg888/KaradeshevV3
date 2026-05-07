using System;
using System.Text;
using UnityEngine;

/// <summary>
/// ScriptableObject that crafts notification titles and messages.
/// Success types use randomised template arrays.
/// Failure types delegate to TaskFailureStoryManager so they reuse the same flavour text.
/// Create via Assets → Create → Game → Notification Message Crafter.
/// </summary>
[CreateAssetMenu(fileName = "NotificationMessageCrafter", menuName = "Game/Notification Message Crafter")]
public class NotificationMessageCrafter : ScriptableObject
{
    [Serializable]
    public class SuccessTemplateSet
    {
        public NotificationType type;
        [TextArea] public string[] titles;
        [TextArea] public string[] messages;
    }

    [SerializeField] private SuccessTemplateSet[] successSets;

    // Supports {ENV_NAME}, {ENV}, {TILE}, {SIZE}
    public (string title, string message) Craft(NotificationType type, EnvironmentControl env, int populationLost = 0)
    {
        switch (type)
        {
            case NotificationType.GatheringFailed:
                return CraftFailure(TaskFailureType.Gathering, env, populationLost);
            case NotificationType.DiscoveryFailed:
                return CraftFailure(TaskFailureType.Discovery, env, populationLost);
            default:
                return CraftSuccess(type, env);
        }
    }

    public (string title, string message) CraftResearch(NotificationType type, string techName)
    {
        var set = GetSuccessSet(type);
        if (set == null)
        {
            return type == NotificationType.ResearchFailed
                ? ("Research Failed",   $"Research on {techName} has failed.")
                : ("Research Complete", $"{techName} has been researched.");
        }

        string title   = Pick(set.titles);
        string message = Pick(set.messages).Replace("{TECH}", techName);
        return (title, message);
    }

    public (string title, string message) CraftBirth(NotificationType type, string motherSurname, int bornAlive, bool motherDied)
    {
        var set = GetSuccessSet(type);
        if (set == null)
        {
            return type switch
            {
                NotificationType.BirthSucceeded => ("A Child is Born", $"The {motherSurname} family welcomes {bornAlive} newborn(s)."),
                _                               => ("Pregnancy Lost",   $"A pregnancy in the {motherSurname} family has failed."),
            };
        }

        string title   = Pick(set.titles);
        string message = Pick(set.messages)
            .Replace("{MOTHER}", motherSurname)
            .Replace("{COUNT}",  bornAlive.ToString());
        return (title, message);
    }

    public (string title, string message) CraftProduction(NotificationType type, string buildingName, string planName)
    {
        var set = GetSuccessSet(type);
        if (set == null)
        {
            return type switch
            {
                NotificationType.ProductionCompleted =>
                    ("Production Complete", $"{buildingName} has finished a cycle of {planName}."),
                NotificationType.ProductionPausedLackOfResources =>
                    ("Production Paused",  $"{buildingName} paused — not enough resources to run {planName}."),
                NotificationType.ProductionPausedLackOfWorkers =>
                    ("Production Stopped", $"{buildingName} stopped — not enough workers available for {planName}."),
                _ => ("Production Issue", buildingName),
            };
        }
        string title   = Pick(set.titles);
        string message = Pick(set.messages)
            .Replace("{BUILDING}", buildingName)
            .Replace("{PLAN}",     planName);
        return (title, message);
    }

    public (string title, string message) CraftFireFight(NotificationType type, string targetName, int casualties)
    {
        var set = GetSuccessSet(type);
        if (set == null)
        {
            return type switch
            {
                NotificationType.FireFightSucceeded => ("Fire Extinguished!",  casualties > 0
                    ? $"Your people put out the fire at {targetName}. {casualties} worker(s) were lost."
                    : $"Your people put out the fire at {targetName} without casualties."),
                NotificationType.FireFightFailed    => ("Fire Fight Failed",   $"All workers at {targetName} were lost to the flames."),
                _                                   => ("Fire", targetName),
            };
        }

        string title   = Pick(set.titles);
        string message = Pick(set.messages)
            .Replace("{NAME}",       targetName)
            .Replace("{CASUALTIES}", casualties.ToString());
        return (title, message);
    }

    public (string title, string message) CraftCrafting(NotificationType type, string recipeName, string buildingName)
    {
        var set = GetSuccessSet(type);
        if (set == null)
        {
            return type switch
            {
                NotificationType.CraftingCompleted    => ("Crafting Complete",      $"{buildingName} finished crafting {recipeName}."),
                NotificationType.CraftingFailedWeather => ("Crafting Interrupted",  $"Bad weather stopped {buildingName} from completing {recipeName}."),
                _                                     => ("Crafting", buildingName),
            };
        }

        string title   = Pick(set.titles);
        string message = Pick(set.messages)
            .Replace("{RECIPE}",   recipeName)
            .Replace("{BUILDING}", buildingName);
        return (title, message);
    }

    public (string title, string message) CraftBuilding(NotificationType type, string buildingName)
    {
        var set = GetSuccessSet(type);
        if (set == null)
        {
            return type switch
            {
                NotificationType.BuildingOnFire    => ("Building on Fire",   $"{buildingName} is on fire!"),
                NotificationType.BuildingDamaged   => ("Building Damaged",   $"{buildingName} has been damaged."),
                NotificationType.BuildingDestroyed => ("Building Destroyed", $"{buildingName} has been destroyed."),
                _                                  => ("Construction Complete", $"{buildingName} has been constructed."),
            };
        }

        string title   = Pick(set.titles);
        string message = Pick(set.messages).Replace("{BUILDING}", buildingName);
        return (title, message);
    }

    // ------------------------------------------------------------------

    private (string title, string message) CraftSuccess(NotificationType type, EnvironmentControl env)
    {
        var set = GetSuccessSet(type);
        if (set == null) return (Fallback(type), env != null ? env.environmentName : "");

        string title   = Replace(Pick(set.titles),   env);
        string message = Replace(Pick(set.messages), env);
        return (title, message);
    }

    private static (string title, string message) CraftFailure(TaskFailureType failType, EnvironmentControl env, int populationLost)
    {
        string title = failType == TaskFailureType.Gathering ? "Gathering Failed" : "Discovery Failed";

        string message = "";
        if (TaskFailureStoryManager.Instance != null && env != null)
            message = TaskFailureStoryManager.Instance.BuildStory(env, failType, populationLost);

        if (string.IsNullOrWhiteSpace(message) && env != null)
        {
            string kind = failType == TaskFailureType.Gathering ? "gathering" : "discovery";
            message = $"The {kind} at {env.environmentName} failed.";
            if (populationLost > 0) message += $" {populationLost} lost.";
        }

        return (title, message);
    }

    private SuccessTemplateSet GetSuccessSet(NotificationType type)
    {
        if (successSets == null) return null;
        for (int i = 0; i < successSets.Length; i++)
            if (successSets[i] != null && successSets[i].type == type) return successSets[i];
        return null;
    }

    private static string Pick(string[] arr)
    {
        if (arr == null || arr.Length == 0) return "";
        return arr[UnityEngine.Random.Range(0, arr.Length)] ?? "";
    }

    private static string Replace(string s, EnvironmentControl env)
    {
        if (string.IsNullOrEmpty(s) || env == null) return s ?? "";
        return s
            .Replace("{ENV_NAME}", string.IsNullOrEmpty(env.environmentName) ? "the area" : env.environmentName)
            .Replace("{ENV}",  Nicify(env.environmentType.ToString()))
            .Replace("{TILE}", Nicify(env.environmentTileType.ToString()))
            .Replace("{SIZE}", Nicify(env.tileSize.ToString()));
    }

    private static string Nicify(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        var sb = new StringBuilder(raw.Length + 8);
        sb.Append(raw[0]);
        for (int i = 1; i < raw.Length; i++)
        {
            if (char.IsUpper(raw[i]) && !char.IsUpper(raw[i - 1])) sb.Append(' ');
            sb.Append(raw[i]);
        }
        return sb.ToString();
    }

    private static string Fallback(NotificationType type)
    {
        switch (type)
        {
            case NotificationType.GatheringCompleted: return "Gathering Complete";
            case NotificationType.DiscoveryCompleted: return "Discovery Complete";
            default:                                  return type.ToString();
        }
    }

    // ------------------------------------------------------------------
    // Inspector defaults
    // ------------------------------------------------------------------

    [ContextMenu("Populate Defaults")]
    private void PopulateDefaults()
    {
        successSets = new SuccessTemplateSet[]
        {
            new SuccessTemplateSet
            {
                type     = NotificationType.GatheringCompleted,
                titles   = new[] { "Gathering Complete", "Resources Secured", "Haul Successful" },
                messages = new[]
                {
                    "Your people gathered resources from {ENV_NAME}.",
                    "The {ENV} at {ENV_NAME} has been harvested.",
                    "A successful haul from the {TILE} region of {ENV_NAME}.",
                    "Resources from the {SIZE} {ENV} site are ready.",
                },
            },
            new SuccessTemplateSet
            {
                type     = NotificationType.DiscoveryCompleted,
                titles   = new[] { "Discovery Complete", "New Land Charted", "Area Revealed" },
                messages = new[]
                {
                    "{ENV_NAME} has been fully discovered.",
                    "Your scouts mapped the {ENV} at {ENV_NAME}.",
                    "The {TILE} region known as {ENV_NAME} is now charted.",
                    "A {SIZE} {ENV} site — {ENV_NAME} — has been revealed.",
                },
            },
            new SuccessTemplateSet
            {
                type     = NotificationType.BuildingCompleted,
                titles   = new[] { "Construction Complete", "Building Ready", "Structure Finished" },
                messages = new[]
                {
                    "{BUILDING} has been constructed.",
                    "Your workers finished building {BUILDING}.",
                    "Construction of {BUILDING} is complete.",
                    "{BUILDING} is now operational.",
                },
            },
            new SuccessTemplateSet
            {
                type     = NotificationType.BuildingOnFire,
                titles   = new[] { "Building on Fire!", "Fire Detected!", "Structure Ablaze" },
                messages = new[]
                {
                    "{BUILDING} is on fire — act quickly!",
                    "Fire has broken out at {BUILDING}.",
                    "{BUILDING} is burning. Send help before it's too late.",
                    "Flames have engulfed {BUILDING}.",
                },
            },
            new SuccessTemplateSet
            {
                type     = NotificationType.BuildingDamaged,
                titles   = new[] { "Building Damaged", "Structure Damaged", "Damage Reported" },
                messages = new[]
                {
                    "{BUILDING} has sustained damage.",
                    "Your {BUILDING} has been damaged and needs repair.",
                    "Damage reported at {BUILDING}.",
                    "{BUILDING} is in a damaged state.",
                },
            },
            new SuccessTemplateSet
            {
                type     = NotificationType.ResearchCompleted,
                titles   = new[] { "Research Complete", "Technology Unlocked", "Discovery Made" },
                messages = new[]
                {
                    "{TECH} has been researched.",
                    "Your researchers have completed {TECH}.",
                    "Knowledge of {TECH} is now yours.",
                    "{TECH} has been unlocked.",
                },
            },
            new SuccessTemplateSet
            {
                type     = NotificationType.ResearchFailed,
                titles   = new[] { "Research Failed", "Setback in Research", "Research Lost" },
                messages = new[]
                {
                    "Research on {TECH} has failed.",
                    "Your researchers failed to complete {TECH}.",
                    "The research into {TECH} came to nothing.",
                    "{TECH} could not be completed this time.",
                },
            },
            new SuccessTemplateSet
            {
                type     = NotificationType.BuildingDestroyed,
                titles   = new[] { "Building Destroyed", "Structure Lost", "Total Loss" },
                messages = new[]
                {
                    "{BUILDING} has been destroyed.",
                    "Your {BUILDING} has been completely destroyed.",
                    "{BUILDING} is lost — nothing remains.",
                    "The {BUILDING} has been reduced to rubble.",
                },
            },
            new SuccessTemplateSet
            {
                type     = NotificationType.BirthSucceeded,
                titles   = new[] { "A Child is Born", "New Life", "Birth Successful" },
                messages = new[]
                {
                    "The {MOTHER} family welcomes {COUNT} newborn(s).",
                    "A healthy child has joined the {MOTHER} family.",
                    "New life brightens the {MOTHER} household.",
                    "{COUNT} child(ren) born to the {MOTHER} family.",
                },
            },
            new SuccessTemplateSet
            {
                type     = NotificationType.BirthFailed,
                titles   = new[] { "Pregnancy Lost", "Birth Failed", "A Sad Loss" },
                messages = new[]
                {
                    "A pregnancy in the {MOTHER} family has failed.",
                    "The {MOTHER} family suffered a pregnancy loss.",
                    "Complications ended a pregnancy for the {MOTHER} family.",
                },
            },
            new SuccessTemplateSet
            {
                type     = NotificationType.ProductionCompleted,
                titles   = new[] { "Production Complete", "Cycle Finished", "Output Ready" },
                messages = new[]
                {
                    "{BUILDING} has completed a cycle of {PLAN}.",
                    "A {PLAN} cycle at {BUILDING} is done — output is ready.",
                    "{BUILDING} finished producing {PLAN}.",
                },
            },
            new SuccessTemplateSet
            {
                type     = NotificationType.ProductionPausedLackOfResources,
                titles   = new[] { "Production Paused", "Resources Depleted", "Work Halted" },
                messages = new[]
                {
                    "{BUILDING} has paused {PLAN} — resources ran out.",
                    "Not enough resources to continue {PLAN} at {BUILDING}.",
                    "{BUILDING} is idle — {PLAN} requires more supplies.",
                },
            },
            new SuccessTemplateSet
            {
                type     = NotificationType.ProductionPausedLackOfWorkers,
                titles   = new[] { "Production Stopped", "Workers Unavailable", "Workforce Shortage" },
                messages = new[]
                {
                    "{BUILDING} has stopped {PLAN} — not enough workers.",
                    "Insufficient workers to staff {PLAN} at {BUILDING}.",
                    "{BUILDING} is idle — {PLAN} needs more hands.",
                },
            },
            new SuccessTemplateSet
            {
                type     = NotificationType.CraftingCompleted,
                titles   = new[] { "Crafting Complete", "Item Ready", "Craft Successful" },
                messages = new[]
                {
                    "{BUILDING} finished crafting {RECIPE}.",
                    "Your crafters completed {RECIPE} at {BUILDING}.",
                    "A batch of {RECIPE} is ready at {BUILDING}.",
                    "{RECIPE} has been crafted — collect it from {BUILDING}.",
                },
            },
            new SuccessTemplateSet
            {
                type     = NotificationType.CraftingFailedWeather,
                titles   = new[] { "Crafting Interrupted", "Weather Halts Work", "Storm Disrupts Craft" },
                messages = new[]
                {
                    "Bad weather forced {BUILDING} to stop work on {RECIPE}.",
                    "Harsh conditions at {BUILDING} interrupted {RECIPE}.",
                    "The weather has disrupted crafting of {RECIPE} at {BUILDING}.",
                    "{BUILDING} could not complete {RECIPE} — weather conditions too severe.",
                },
            },
            new SuccessTemplateSet
            {
                type     = NotificationType.FireFightSucceeded,
                titles   = new[] { "Fire Extinguished!", "Flames Defeated", "Fire Under Control" },
                messages = new[]
                {
                    "Your people put out the fire at {NAME}. {CASUALTIES} worker(s) were lost.",
                    "The fire at {NAME} has been extinguished. {CASUALTIES} paid with their lives.",
                    "{NAME} is safe — the flames are out. Lost: {CASUALTIES}.",
                    "Victory against the fire at {NAME}. {CASUALTIES} casualty(ies) sustained.",
                },
            },
            new SuccessTemplateSet
            {
                type     = NotificationType.FireFightFailed,
                titles   = new[] { "Fire Fight Failed", "Workers Lost to Fire", "Overwhelmed by Flames" },
                messages = new[]
                {
                    "All workers at {NAME} were lost to the flames.",
                    "The fire at {NAME} proved too fierce — every fighter perished.",
                    "{NAME} claimed the lives of all {CASUALTIES} worker(s) sent to fight it.",
                    "Your firefighters at {NAME} were overcome. The fire rages on.",
                },
            },
        };
    }

    private void Reset() => PopulateDefaults();
}
