using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PlayerSetupInstaller : MonoBehaviour
{
    [Header("Resolved UI References")]
    [SerializeField] private Image phaseImage;
    [SerializeField] private Image phaseFillImage;
    [SerializeField] private TMP_Text turnText;
    [SerializeField] private Image pauseButtonImage;
    [SerializeField] private Image speedButtonImage;
    [SerializeField] private TMP_Text populationDisplayText;
    [SerializeField] private TMP_Text availableText;

    [Header("Target Components In PlayerSetup Scene")]
    [SerializeField] private TurnSystem turnSystem;
    [SerializeField] private PlayersPopulationManager playersPopulationManager;
    [SerializeField] private PlayerKnownResourcesManager knownResourcesManager;
    [SerializeField] private PlayerPopulationStatistic playerPopulationStatistic;

    [Header("UI Object Names")]
    [SerializeField] private string phaseImageObjectName = "PhaseImage";
    [SerializeField] private string phaseFillImageObjectName = "PhaseTimerBGImage";
    [SerializeField] private string turnTextObjectName = "TurnInfoText";
    [SerializeField] private string pauseButtonObjectName = "PauseButton";
    [SerializeField] private string speedButtonObjectName = "SpeedUpButton";
    [SerializeField] private string populationDisplayTextObjectName = "PopulationText";
    [SerializeField] private string availableTextObjectName = "AvailableUsedText";

    [SerializeField] private PlayerKnownBuildingsManager knownBuildingsManager;
    [SerializeField] private PlayerLevel playerLevel;
    [SerializeField] private ImmigrantOfferManager immigrantOfferManager;
    [SerializeField] private PlayerFamilySimulationManager playerFamilySimulationManager;
    [SerializeField] private PlayerBuildingManager playerBuildingManager;
    [SerializeField] private BuildingPlacementManager buildingPlacementManager;
    [SerializeField] private PlayerInventoryManager playerInventoryManager;
    [SerializeField] private TileInteraction tileInteraction;
    [SerializeField] private PlayerKnownCraftingManager knownCraftingManager;
    [SerializeField] private PlayerKnownProductionManager knownProductionManager;
    [SerializeField] private PlayerRitualManager playerRitualManager;

    [SerializeField] private PlayerResearchManager playerResearchManager;
    [SerializeField] private Transform researchTasksContentRoot;
    [SerializeField] private string researchTasksContentRootObjectName = "OrderContent";

    public Scene LoadedScene => gameObject.scene;

    public Image PhaseImage => phaseImage;
    public Image PhaseFillImage => phaseFillImage;
    public TMP_Text TurnText => turnText;
    public Image PauseButtonImage => pauseButtonImage;
    public Image SpeedButtonImage => speedButtonImage;
    public TMP_Text PopulationDisplayText => populationDisplayText;
    public TMP_Text AvailableText => availableText;

    public TurnSystem TurnSystem => turnSystem;
    public PlayersPopulationManager PlayersPopulationManager => playersPopulationManager;
    public PlayerKnownResourcesManager KnownResourcesManager => knownResourcesManager;
    public PlayerPopulationStatistic PlayerPopulationStatistic => playerPopulationStatistic;
    public PlayerKnownBuildingsManager KnownBuildingsManager => knownBuildingsManager;
    public PlayerLevel PlayerLevel => playerLevel;
    public ImmigrantOfferManager ImmigrantOfferManager => immigrantOfferManager;
    public PlayerFamilySimulationManager PlayerFamilySimulationManager => playerFamilySimulationManager;
    public PlayerBuildingManager PlayerBuildingManager => playerBuildingManager;
    public BuildingPlacementManager BuildingPlacementManager => buildingPlacementManager;
    public PlayerInventoryManager PlayerInventoryManager => playerInventoryManager;
    public PlayerResearchManager PlayerResearchManager => playerResearchManager;
    public Transform ResearchTasksContentRoot => researchTasksContentRoot;
    public TileInteraction TileInteraction => tileInteraction;
    public PlayerKnownCraftingManager KnownCraftingManager => knownCraftingManager;
    public PlayerKnownProductionManager KnownProductionManager => knownProductionManager;
    public PlayerRitualManager PlayerRitualManager => playerRitualManager;

    public void ResolveFromUIScene(Scene uiScene)
    {
        if (!uiScene.IsValid() || !uiScene.isLoaded)
        {
            Debug.LogError("[PlayerSetupInstaller] UI scene is not valid or not loaded.");
            return;
        }

        ResolvePlayerSceneTargets();

        phaseImage = FindComponentInSceneByName<Image>(uiScene, phaseImageObjectName);
        phaseFillImage = FindComponentInSceneByName<Image>(uiScene, phaseFillImageObjectName);
        turnText = FindComponentInSceneByName<TMP_Text>(uiScene, turnTextObjectName);
        pauseButtonImage = FindComponentInSceneByName<Image>(uiScene, pauseButtonObjectName);
        speedButtonImage = FindComponentInSceneByName<Image>(uiScene, speedButtonObjectName);
        populationDisplayText = FindComponentInSceneByName<TMP_Text>(uiScene, populationDisplayTextObjectName);
        availableText = FindComponentInSceneByName<TMP_Text>(uiScene, availableTextObjectName);
        researchTasksContentRoot = FindComponentInSceneByName<Transform>(uiScene, researchTasksContentRootObjectName);
        LogMissing("Research Tasks Content Root", researchTasksContentRoot, researchTasksContentRootObjectName);

        LogMissing("Phase Image", phaseImage, phaseImageObjectName);
        LogMissing("Phase Fill Image", phaseFillImage, phaseFillImageObjectName);
        LogMissing("Turn Text", turnText, turnTextObjectName);
        LogMissing("Pause Button Image", pauseButtonImage, pauseButtonObjectName);
        LogMissing("Speed Button Image", speedButtonImage, speedButtonObjectName);
        LogMissing("Population Display Text", populationDisplayText, populationDisplayTextObjectName);
        LogMissing("Available Text", availableText, availableTextObjectName);

        InstallResolvedRefs();
    }

    private void ResolvePlayerSceneTargets()
    {
        if (turnSystem == null)
            turnSystem = FindComponentInScene<TurnSystem>(LoadedScene);

        if (playersPopulationManager == null)
            playersPopulationManager = FindComponentInScene<PlayersPopulationManager>(LoadedScene);

        if (knownResourcesManager == null)
            knownResourcesManager = FindComponentInScene<PlayerKnownResourcesManager>(LoadedScene);

        if (playerPopulationStatistic == null)
            playerPopulationStatistic = FindComponentInScene<PlayerPopulationStatistic>(LoadedScene);

        if (immigrantOfferManager == null)
            immigrantOfferManager = FindComponentInScene<ImmigrantOfferManager>(LoadedScene);

        if (knownCraftingManager == null)
            knownCraftingManager = FindComponentInScene<PlayerKnownCraftingManager>(LoadedScene);

        if (immigrantOfferManager == null)
            Debug.LogWarning("[PlayerSetupInstaller] ImmigrantOfferManager not found in PlayerSetup scene.");

        if (turnSystem == null)
            Debug.LogWarning("[PlayerSetupInstaller] TurnSystem not found in PlayerSetup scene.");

        if (playersPopulationManager == null)
            Debug.LogWarning("[PlayerSetupInstaller] PlayersPopulationManager not found in PlayerSetup scene.");

        if (knownResourcesManager == null)
            Debug.LogWarning("[PlayerSetupInstaller] PlayerKnownResourcesManager not found in PlayerSetup scene.");

        if (knownBuildingsManager == null)
            knownBuildingsManager = FindComponentInScene<PlayerKnownBuildingsManager>(LoadedScene);

        if (playerLevel == null)
            playerLevel = FindComponentInScene<PlayerLevel>(LoadedScene);

        if (playerFamilySimulationManager == null)
            playerFamilySimulationManager = FindComponentInScene<PlayerFamilySimulationManager>(LoadedScene);

        if (playerBuildingManager == null)
            playerBuildingManager = FindComponentInScene<PlayerBuildingManager>(LoadedScene);

        if (buildingPlacementManager == null)
            buildingPlacementManager = FindComponentInScene<BuildingPlacementManager>(LoadedScene);

        if (playerInventoryManager == null)
            playerInventoryManager = FindComponentInScene<PlayerInventoryManager>(LoadedScene);

        if (playerResearchManager == null)
            playerResearchManager = FindComponentInScene<PlayerResearchManager>(LoadedScene);

        if (tileInteraction == null)
            tileInteraction = FindComponentInScene<TileInteraction>(LoadedScene);

        if (knownProductionManager == null)
            knownProductionManager = FindComponentInScene<PlayerKnownProductionManager>(LoadedScene);

        if (playerRitualManager == null)
            playerRitualManager = FindComponentInScene<PlayerRitualManager>(LoadedScene);

        if (knownBuildingsManager == null)
            Debug.LogWarning("[PlayerSetupInstaller] PlayerKnownBuildingsManager not found in PlayerSetup scene.");

        if (playerLevel == null)
            Debug.LogWarning("[PlayerSetupInstaller] PlayerLevel not found in PlayerSetup scene.");

        if (playerPopulationStatistic == null)
            Debug.LogWarning("[PlayerSetupInstaller] PlayerPopulationStatistic not found in PlayerSetup scene.");
        else if (playersPopulationManager != null)
            playerPopulationStatistic.populationManager = playersPopulationManager;
    }

    public void InstallResolvedRefs()
    {
        if (turnSystem != null)
        {
            turnSystem.phaseImage = phaseImage;
            turnSystem.phaseFillImage = phaseFillImage;
            turnSystem.turnText = turnText;
            turnSystem.pauseButtonImage = pauseButtonImage;
            turnSystem.speedButtonImage = speedButtonImage;
            turnSystem.RefreshResolvedUI();
        }

        if (playersPopulationManager != null)
        {
            playersPopulationManager.populationDisplayText = populationDisplayText;
            playersPopulationManager.availableText = availableText;
            playersPopulationManager.ForceSyncUI();
        }

        if (playerPopulationStatistic != null && playersPopulationManager != null)
            playerPopulationStatistic.populationManager = playersPopulationManager;
    }

    private static void LogMissing(string label, Object value, string objectName)
    {
        if (value == null)
            Debug.LogWarning($"[PlayerSetupInstaller] Could not resolve {label} from UI scene using object name '{objectName}'.");
    }

    private static T FindComponentInSceneByName<T>(Scene scene, string targetName) where T : Component
    {
        if (string.IsNullOrWhiteSpace(targetName))
            return null;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            T found = FindComponentRecursiveByName<T>(roots[i].transform, targetName);
            if (found != null)
                return found;
        }

        return null;
    }

    private static T FindComponentRecursiveByName<T>(Transform current, string targetName) where T : Component
    {
        if (current.name == targetName)
        {
            T component = current.GetComponent<T>();
            if (component != null)
                return component;
        }

        for (int i = 0; i < current.childCount; i++)
        {
            T found = FindComponentRecursiveByName<T>(current.GetChild(i), targetName);
            if (found != null)
                return found;
        }

        return null;
    }

    private static T FindComponentInScene<T>(Scene scene) where T : Component
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return null;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            T found = roots[i].GetComponentInChildren<T>(true);
            if (found != null)
                return found;
        }

        return null;
    }

    public void RegisterPopulationStatistic(PlayerPopulationStatistic statistic)
    {
        if (statistic == null)
            return;

        playerPopulationStatistic = statistic;

        if (playersPopulationManager != null)
            playerPopulationStatistic.populationManager = playersPopulationManager;
    }
}