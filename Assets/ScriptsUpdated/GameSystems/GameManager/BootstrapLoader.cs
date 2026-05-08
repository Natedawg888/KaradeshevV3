using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class BootstrapLoader : MonoBehaviour
{
#if UNITY_EDITOR
    [Header("Scene References")]
    [SerializeField] private SceneAsset worldSetupScene;
    [SerializeField] private SceneAsset managerSetupScene;
    [SerializeField] private SceneAsset uiSetupScene;
    [SerializeField] private SceneAsset playerSetupScene;
    [SerializeField] private SceneAsset finalSetupScene;
    [SerializeField] private SceneAsset tutorialSetupScene;
#endif

    [SerializeField, HideInInspector] private string worldSetupScenePath = "";
    [SerializeField, HideInInspector] private string managerSetupScenePath = "";
    [SerializeField, HideInInspector] private string uiSetupScenePath = "";
    [SerializeField, HideInInspector] private string playerSetupScenePath = "";
    [SerializeField, HideInInspector] private string finalSetupScenePath = "";
    [SerializeField, HideInInspector] private string tutorialSetupScenePath = "";

    [Header("References")]
    [SerializeField] private GameSceneManager gameSceneManager;
    [SerializeField] private PersistentLoadingUI persistentLoadingUI;
    [SerializeField] private CameraControl bootstrapCameraControl;
    [SerializeField] private MusicDirector bootstrapMusicDirector;

    [Header("Debug")]
    [SerializeField] private bool logBootstrapTimings = true;

    private WorldSetupInstaller _worldInstaller;
    private ManagerSetupInstaller _managerInstaller;
    private PlayerSetupInstaller _playerInstaller;
    private FinalSetupInstaller _finalInstaller;
    private TutorialSetupInstaller _tutorialInstaller;

    private IEnumerator Start()
    {
        if (gameSceneManager == null)
            gameSceneManager = FindFirstObjectByType<GameSceneManager>(FindObjectsInactive.Include);

        if (gameSceneManager == null)
        {
            //Debug.LogError("[BootstrapLoader] No GameSceneManager found in BootstrapCore.");
            yield break;
        }

        if (string.IsNullOrEmpty(worldSetupScenePath))
        {
            //Debug.LogError("[BootstrapLoader] WorldSetup scene is not assigned.");
            yield break;
        }

        if (string.IsNullOrEmpty(managerSetupScenePath))
        {
            //Debug.LogError("[BootstrapLoader] ManagerSetup scene is not assigned.");
            yield break;
        }

        if (string.IsNullOrEmpty(uiSetupScenePath))
        {
            //Debug.LogError("[BootstrapLoader] UISetup scene is not assigned.");
            yield break;
        }

        if (string.IsNullOrEmpty(playerSetupScenePath))
        {
            //Debug.LogError("[BootstrapLoader] PlayerSetup scene is not assigned.");
            yield break;
        }

        if (string.IsNullOrEmpty(finalSetupScenePath))
        {
            //Debug.LogError("[BootstrapLoader] FinalSetup scene is not assigned.");
            yield break;
        }

        persistentLoadingUI?.StartLoop();

        // 1) Load WorldSetup
        float worldStart = Time.realtimeSinceStartup;
        Log("LoadWorldSetup_BEGIN", 0f);

        AsyncOperation worldOp = SceneManager.LoadSceneAsync(worldSetupScenePath, LoadSceneMode.Additive);
        if (worldOp == null)
        {
            //Debug.LogError($"[BootstrapLoader] Failed to start loading scene '{worldSetupScenePath}'.");
            yield break;
        }

        while (!worldOp.isDone)
            yield return null;

        Log("LoadWorldSetup_DONE", Time.realtimeSinceStartup - worldStart);

        _worldInstaller = FindFirstObjectByType<WorldSetupInstaller>(FindObjectsInactive.Include);
        if (_worldInstaller == null)
        {
            //Debug.LogError("[BootstrapLoader] WorldSetupInstaller not found after WorldSetup loaded.");
            yield break;
        }

        if (GameStartContext.GetRequestedMode() == GameStartMode.NewGame)
        {
            var setup = GameStartContext.GetPendingNewGameSetup();
            if (setup != null && _worldInstaller.EnvironmentPresetManager != null)
            {
                _worldInstaller.EnvironmentPresetManager.ApplyPreset(setup.selectedPresetID);
            }
        }

        InstallWorldReferencesIntoBootSystems();

        if (bootstrapCameraControl != null)
        {
            bootstrapCameraControl.InstallRuntimeRefs(
                newGridManager: _worldInstaller.GridManager
            );
        }

        // Keep world scene active so spawned map/world objects go there.
        SceneManager.SetActiveScene(_worldInstaller.LoadedScene);
        gameSceneManager.ConfigureWorldSetup(_worldInstaller);

        // 2) Load ManagerSetup
        float managerStart = Time.realtimeSinceStartup;
        Log("LoadManagerSetup_BEGIN", Time.realtimeSinceStartup - worldStart);

        AsyncOperation managerOp = SceneManager.LoadSceneAsync(managerSetupScenePath, LoadSceneMode.Additive);
        if (managerOp == null)
        {
            //Debug.LogError($"[BootstrapLoader] Failed to start loading scene '{managerSetupScenePath}'.");
            yield break;
        }

        while (!managerOp.isDone)
            yield return null;

        Log("LoadManagerSetup_DONE", Time.realtimeSinceStartup - managerStart);

        _managerInstaller = FindFirstObjectByType<ManagerSetupInstaller>(FindObjectsInactive.Include);
        if (_managerInstaller == null)
        {
            //Debug.LogError("[BootstrapLoader] ManagerSetupInstaller not found after ManagerSetup loaded.");
            yield break;
        }

        if (bootstrapMusicDirector != null)
            bootstrapMusicDirector.SetLevelManager(_managerInstaller.LevelManager, refreshNow: false);

        // 3) Load UISetup
        float uiStart = Time.realtimeSinceStartup;
        Log("LoadUISetup_BEGIN", Time.realtimeSinceStartup - worldStart);

        AsyncOperation uiOp = SceneManager.LoadSceneAsync(uiSetupScenePath, LoadSceneMode.Additive);
        if (uiOp == null)
        {
            //Debug.LogError($"[BootstrapLoader] Failed to start loading scene '{uiSetupScenePath}'.");
            yield break;
        }

        while (!uiOp.isDone)
            yield return null;

        Log("LoadUISetup_DONE", Time.realtimeSinceStartup - uiStart);

        UISetupInstaller uiInstaller = FindFirstObjectByType<UISetupInstaller>(FindObjectsInactive.Include);
        if (uiInstaller == null)
        {
            //Debug.LogError("[BootstrapLoader] UISetupInstaller not found after UISetup loaded.");
            yield break;
        }

        InstallUIReferences(uiInstaller);
        gameSceneManager.ConfigureUISetup(uiInstaller);

        // 4) Load PlayerSetup
        float playerStart = Time.realtimeSinceStartup;
        Log("LoadPlayerSetup_BEGIN", Time.realtimeSinceStartup - worldStart);

        AsyncOperation playerOp = SceneManager.LoadSceneAsync(playerSetupScenePath, LoadSceneMode.Additive);
        if (playerOp == null)
        {
            //Debug.LogError($"[BootstrapLoader] Failed to start loading scene '{playerSetupScenePath}'.");
            yield break;
        }

        while (!playerOp.isDone)
            yield return null;

        Log("LoadPlayerSetup_DONE", Time.realtimeSinceStartup - playerStart);

        _playerInstaller = FindFirstObjectByType<PlayerSetupInstaller>(FindObjectsInactive.Include);
        if (_playerInstaller == null)
        {
            //Debug.LogError("[BootstrapLoader] PlayerSetupInstaller not found after PlayerSetup loaded.");
            yield break;
        }

        _playerInstaller.ResolveFromUIScene(uiInstaller.LoadedScene);
        InstallPlayerReferences(uiInstaller, _playerInstaller);

        // 5) Load FinalSetup
        float finalStart = Time.realtimeSinceStartup;
        Log("LoadFinalSetup_BEGIN", Time.realtimeSinceStartup - worldStart);

        AsyncOperation finalOp = SceneManager.LoadSceneAsync(finalSetupScenePath, LoadSceneMode.Additive);
        if (finalOp == null)
        {
            //Debug.LogError($"[BootstrapLoader] Failed to start loading scene '{finalSetupScenePath}'.");
            yield break;
        }

        while (!finalOp.isDone)
            yield return null;

        Log("LoadFinalSetup_DONE", Time.realtimeSinceStartup - finalStart);

        _finalInstaller = FindFirstObjectByType<FinalSetupInstaller>(FindObjectsInactive.Include);
        if (_finalInstaller == null)
        {
            //Debug.LogError("[BootstrapLoader] FinalSetupInstaller not found after FinalSetup loaded.");
            yield break;
        }

        _finalInstaller.ResolveFromUIScene(uiInstaller.LoadedScene);

        // 6) Load TutorialSetup (optional)
        _tutorialInstaller = null;

        if (ShouldLoadTutorialSetup())
        {
            if (string.IsNullOrEmpty(tutorialSetupScenePath))
            {
                //Debug.LogWarning("[BootstrapLoader] Tutorial was enabled, but TutorialSetup scene is not assigned. Continuing without tutorials.");
            }
            else
            {
                float tutorialStart = Time.realtimeSinceStartup;
                Log("LoadTutorialSetup_BEGIN", Time.realtimeSinceStartup - worldStart);

                AsyncOperation tutorialOp = SceneManager.LoadSceneAsync(tutorialSetupScenePath, LoadSceneMode.Additive);
                if (tutorialOp == null)
                {
                    //Debug.LogWarning($"[BootstrapLoader] Failed to start loading scene '{tutorialSetupScenePath}'. Continuing without tutorials.");
                }
                else
                {
                    while (!tutorialOp.isDone)
                        yield return null;

                    Log("LoadTutorialSetup_DONE", Time.realtimeSinceStartup - tutorialStart);

                    _tutorialInstaller = FindFirstObjectByType<TutorialSetupInstaller>(FindObjectsInactive.Include);
                    if (_tutorialInstaller == null)
                    {
                        //Debug.LogWarning("[BootstrapLoader] TutorialSetupInstaller not found after TutorialSetup loaded. Continuing without tutorials.");
                    }
                    else
                    {
                        _tutorialInstaller.InstallBootstrapReferences(
                            bootstrapCameraControl,
                            _worldInstaller != null ? _worldInstaller.GridManager : null,
                            _worldInstaller != null ? _worldInstaller.EnvDataSource : null,
                            uiInstaller != null ? uiInstaller.UndiscoveredTilePanelControl : null,
                            uiInstaller != null ? uiInstaller.DiscoveredTilePanelControl : null,
                            uiInstaller != null ? uiInstaller.CollectedGoodsPanelControl : null,
                            uiInstaller != null ? uiInstaller.InventoryPanelControl : null,
                            uiInstaller != null ? uiInstaller.PlayerPopulationStatisticsPanelRoot : null,
                            uiInstaller != null ? uiInstaller.ProfilePanelControl : null,
                            uiInstaller != null ? uiInstaller.BuildingPanelControl : null,
                            uiInstaller != null ? uiInstaller.BuildingDestroyedPanelControl : null,
                            uiInstaller != null ? uiInstaller.BuildingCatalogPanelControl : null,
                            uiInstaller != null ? uiInstaller.ProductionRunningPanelControl : null,
                            _playerInstaller != null ? _playerInstaller.TileInteraction : null
                        );
                    }
                }
            }
        }
        else
        {
            Log("LoadTutorialSetup_SKIPPED", Time.realtimeSinceStartup - worldStart);
        }

        InstallFinalReferences(_finalInstaller, _tutorialInstaller);

        if (persistentLoadingUI != null)
            persistentLoadingUI.HideImmediate();

        gameSceneManager.BeginBootstrapStartup();
    }

    private bool ShouldLoadTutorialSetup()
    {
        if (GameStartContext.GetRequestedMode() != GameStartMode.NewGame)
            return false;

        var setup = GameStartContext.GetPendingNewGameSetup();
        if (setup == null)
            return false;

        return setup.includeTutorial;
    }

    private void InstallUIReferences(UISetupInstaller uiInstaller)
    {
        if (uiInstaller == null)
            return;

        if (bootstrapCameraControl == null)
            bootstrapCameraControl = FindFirstObjectByType<CameraControl>(FindObjectsInactive.Include);

        if (bootstrapCameraControl != null && uiInstaller != null)
        {
            bootstrapCameraControl.InstallRuntimeRefs(
                newMinimapImage: uiInstaller.MinimapImage,
                newMinimapRenderTexture: uiInstaller.MinimapRenderTexture,
                newCameraIcon: uiInstaller.CameraIcon
            );
        }

        if (bootstrapCameraControl != null)
        {
            bootstrapCameraControl.InstallCloudLayerToggleButton(
                uiInstaller.CloudLayerToggleButton
            );
        }

        SaveSystem saveSystem = SaveSystem.Instance != null
            ? SaveSystem.Instance
            : FindFirstObjectByType<SaveSystem>(FindObjectsInactive.Include);

        if (uiInstaller.SaveStatusUIControl != null)
        {
            uiInstaller.SaveStatusUIControl.InstallSaveSystem(saveSystem);
        }

        if (uiInstaller.ProfilePanelControl != null)
            uiInstaller.ProfilePanelControl.cameraControl = bootstrapCameraControl;

        if (uiInstaller.DiscoveredTilePanelControl != null)
            uiInstaller.DiscoveredTilePanelControl.cameraControl = bootstrapCameraControl;

        if (uiInstaller.UndiscoveredTilePanelControl != null)
            uiInstaller.UndiscoveredTilePanelControl.cameraControl = bootstrapCameraControl;

        if (uiInstaller.InventoryPanelControl != null)
            uiInstaller.InventoryPanelControl.cameraControl = bootstrapCameraControl;

        if (uiInstaller.PlayerPopulationStatisticsPanelRoot != null)
            uiInstaller.PlayerPopulationStatisticsPanelRoot.cameraControl = bootstrapCameraControl;

        if (uiInstaller.UnitGroupPanelControl != null)
            uiInstaller.UnitGroupPanelControl.cameraControl = bootstrapCameraControl;

        if (uiInstaller.ImmigrantOfferPanel != null)
            uiInstaller.ImmigrantOfferPanel.cameraControl = bootstrapCameraControl;

        if (uiInstaller.SoundPanelControl != null)
            uiInstaller.SoundPanelControl.musicDirector = bootstrapMusicDirector;

        if (uiInstaller.BuildingPanelControl != null)
        {
            uiInstaller.BuildingPanelControl.InstallRuntimeRefs(
                newCameraControl: bootstrapCameraControl,
                newBuildingManager: _managerInstaller != null ? _managerInstaller.BuildingManager : null,
                refreshIfShowing: false
            );
        }

        if (uiInstaller.BuildingPanelControl != null && uiInstaller.BuildingPanelControl.craftingPanel != null)
        {
            uiInstaller.BuildingPanelControl.craftingPanel.InstallRuntimeRefs(
                newBuildingManager: _managerInstaller != null ? _managerInstaller.BuildingManager : null,
                newCraftingRecipeManager: _managerInstaller != null ? _managerInstaller.CraftingRecipeManager : null,
                refreshIfOpen: false
            );
        }

        if (uiInstaller.BuildingPanelControl != null && uiInstaller.BuildingPanelControl.productionPanel != null)
        {
            uiInstaller.BuildingPanelControl.productionPanel.InstallRuntimeRefs(
                newBuildingManager: _managerInstaller != null ? _managerInstaller.BuildingManager : null,
                newProductionPlanManager: _managerInstaller != null ? _managerInstaller.ProductionPlanManager : null,
                refreshIfOpen: false
            );
        }

        if (uiInstaller.ProfilePanelControl != null)
        {
            uiInstaller.ProfilePanelControl.InstallRuntimeRefs(
                bootstrapCameraControl,
                _worldInstaller != null ? _worldInstaller.EnvironmentPresetManager : null
            );

            if (GameStartContext.GetRequestedMode() == GameStartMode.NewGame)
            {
                var setup = GameStartContext.GetPendingNewGameSetup();
                if (setup != null)
                {
                    uiInstaller.ProfilePanelControl.ApplyNewGameSetup(
                        setup.civilizationName,
                        setup.playerName,
                        setup.avatarName
                    );

                    uiInstaller.ProfilePanelControl.RefreshEnvironmentPresetText();
                }
            }
        }

    }

    private void InstallPlayerReferences(UISetupInstaller uiInstaller, PlayerSetupInstaller playerInstaller)
    {
        if (uiInstaller == null || playerInstaller == null)
            return;

        if (uiInstaller.SurveyPanelControl != null)
            uiInstaller.SurveyPanelControl.SetKnownManager(playerInstaller.KnownResourcesManager, true);

        if (uiInstaller.PlayerPopulationStatisticsPanelRoot != null)
        {
            uiInstaller.PlayerPopulationStatisticsPanelRoot.InstallPopulationRefs(
                playerInstaller.PlayerPopulationStatistic,
                playerInstaller.PlayersPopulationManager,
                true
            );
        }

        if (uiInstaller.BuildingPanelControl != null)
        {
            uiInstaller.BuildingPanelControl.InstallRuntimeRefs(
                newKnownBuildingsManager: playerInstaller.KnownBuildingsManager,
                newPlayerLevel: playerInstaller.PlayerLevel,
                refreshIfShowing: true
            );
        }

        if (uiInstaller.ImmigrantOfferPanel != null && playerInstaller.ImmigrantOfferManager != null)
        {
            playerInstaller.ImmigrantOfferManager.SetPanel(uiInstaller.ImmigrantOfferPanel, true);
        }

        if (playerInstaller.PlayerFamilySimulationManager != null)
        {
            playerInstaller.PlayerFamilySimulationManager.InstallExternalManagers(
                playerInstaller.PlayersPopulationManager,
                _managerInstaller != null ? _managerInstaller.GeneralPopulationManager : null
            );
        }

        BuildingManager buildingManager = _managerInstaller != null ? _managerInstaller.BuildingManager : null;

        if (playerInstaller.KnownBuildingsManager != null)
            playerInstaller.KnownBuildingsManager.SetBuildingManager(buildingManager);

        if (playerInstaller.PlayerBuildingManager != null)
            playerInstaller.PlayerBuildingManager.SetBuildingManager(buildingManager);

        if (playerInstaller.BuildingPlacementManager != null)
        {
            playerInstaller.BuildingPlacementManager.InstallRuntimeRefs(
                newPanel: uiInstaller.BuildingPlacementPanelControl,
                newCameraControl: bootstrapCameraControl,
                newGridManager: _worldInstaller != null ? _worldInstaller.GridManager : null
            );
        }

        if (uiInstaller.InventoryPanelControl != null && playerInstaller.PlayerInventoryManager != null)
        {
            playerInstaller.PlayerInventoryManager.SetInventoryPanel(
                uiInstaller.InventoryPanelControl,
                true
            );
        }

        if (playerInstaller.TileInteraction != null && bootstrapCameraControl != null)
        {
            playerInstaller.TileInteraction.InstallRuntimeRefs(
                newTargetCamera: bootstrapCameraControl.mainCamera,
                newCameraControl: bootstrapCameraControl
            );
        }

        if (WorldBuildingManager.Instance != null)
        {
            WorldBuildingManager.Instance.SetPlayerBuildingManager(
                playerInstaller.PlayerBuildingManager
            );
        }

        if (WeatherGridManager.Instance != null)
        {
            WeatherGridManager.Instance.SetWorldBuildingManager(
                WorldBuildingManager.Instance,
                rebuildCoverage: true
            );
        }

        InstallWorldBuildingManagerIntoEarthquakeSystems(WorldBuildingManager.Instance);
        InstallWorldBuildingManagerIntoTsunamiSystems(WorldBuildingManager.Instance);
        InstallWorldBuildingManagerIntoFloodSystems(WorldBuildingManager.Instance);

        LevelManager levelManager = _managerInstaller != null ? _managerInstaller.LevelManager : null;

        if (playerInstaller.PlayerLevel != null)
            playerInstaller.PlayerLevel.SetLevelManager(levelManager);

        if (bootstrapMusicDirector != null)
        {
            bootstrapMusicDirector.SetLevelManager(levelManager, refreshNow: false);
            bootstrapMusicDirector.SetPlayerLevel(playerInstaller.PlayerLevel, refreshNow: true);
        }

        TechnologyManager technologyManager = _managerInstaller != null ? _managerInstaller.TechnologyManager : null;

        if (playerInstaller.PlayerResearchManager != null)
        {
            playerInstaller.PlayerResearchManager.InstallRuntimeRefs(
                newTechnologyManager: technologyManager,
                newResearchTasksContentRoot: playerInstaller.ResearchTasksContentRoot
            );
        }

        if (uiInstaller.StageThemeApplier != null)
        {
            SeasonManager seasonManager = _worldInstaller != null ? _worldInstaller.SeasonManager : null;

            uiInstaller.StageThemeApplier.InstallStageRefs(
                levelManager,
                playerInstaller.PlayerLevel,
                seasonManager
            );
        }

        if (uiInstaller.BuildingPanelControl != null && uiInstaller.BuildingPanelControl.craftingPanel != null)
        {
            uiInstaller.BuildingPanelControl.craftingPanel.InstallRuntimeRefs(
                newKnownCraftingManager: playerInstaller.KnownCraftingManager,
                refreshIfOpen: true
            );
        }

        if (uiInstaller.BuildingPanelControl != null && uiInstaller.BuildingPanelControl.productionPanel != null)
        {
            uiInstaller.BuildingPanelControl.productionPanel.InstallRuntimeRefs(
                newKnownProductionManager: playerInstaller.KnownProductionManager,
                refreshIfOpen: true
            );
        }

        if (playerInstaller.PlayerRitualManager != null)
        {
            playerInstaller.PlayerRitualManager.InstallRuntimeRefs(
                uiInstaller.SummoningSpiritOfferPanelControl,
                true
            );
        }
    }

    private void InstallWorldReferencesIntoBootSystems()
    {
        if (_worldInstaller == null)
            return;

        SaveSystem saveSystem = SaveSystem.Instance != null
            ? SaveSystem.Instance
            : FindFirstObjectByType<SaveSystem>(FindObjectsInactive.Include);

        if (saveSystem != null)
        {
            saveSystem.InstallWorldLoadHelpers(
                _worldInstaller.SavedTilePlacer,
                _worldInstaller.TileActivator
            );
        }
    }

    private void InstallFinalReferences(FinalSetupInstaller finalInstaller, TutorialSetupInstaller tutorialInstaller)
    {
        if (finalInstaller == null)
            return;

        if (bootstrapCameraControl != null && finalInstaller.MinimapCamera != null)
        {
            bootstrapCameraControl.InstallRuntimeRefs(
                newMinimapCamera: finalInstaller.MinimapCamera
            );
        }

        CameraIntroTutorial cameraTutorial = tutorialInstaller != null
            ? tutorialInstaller.CameraIntroTutorial
            : null;

        EnvironmentTileTutorial environmentTutorial = tutorialInstaller != null
            ? tutorialInstaller.EnvironmentTileTutorial
            : null;

        finalInstaller.InstallBootstrapReferences(
            bootstrapCameraControl,
            _worldInstaller != null ? _worldInstaller.TileActivator : null,
            cameraTutorial,
            environmentTutorial
        );

        if (finalInstaller.AnimalSimulationController != null)
        {
            finalInstaller.AnimalSimulationController.InstallRuntimeRefs(
                newTurnSystem: _playerInstaller != null ? _playerInstaller.TurnSystem : null,
                newEnvDataSource: _worldInstaller != null ? _worldInstaller.EnvDataSource : null,
                newTileActivator: _worldInstaller != null ? _worldInstaller.TileActivator : null
            );
        }

        InstallAnimalSimulationIntoTsunamiSystems();
        InstallAnimalSimulationIntoFloodSystems();
    }

    private void InstallWorldBuildingManagerIntoFloodSystems(WorldBuildingManager worldBuildingManager)
    {
        if (worldBuildingManager == null)
        {
            //Debug.LogWarning("[BootstrapLoader] Cannot install WorldBuildingManager into flood systems because it is null.");
            return;
        }

#if UNITY_2023_1_OR_NEWER
    FloodBuildingEffectResolver[] resolvers = FindObjectsByType<FloodBuildingEffectResolver>(
        FindObjectsInactive.Include,
        FindObjectsSortMode.None
    );
#else
        FloodBuildingEffectResolver[] resolvers = FindObjectsOfType<FloodBuildingEffectResolver>(true);
#endif

        FloodSimulationSystem floodSimulationSystem =
#if UNITY_2023_1_OR_NEWER
        FindFirstObjectByType<FloodSimulationSystem>(FindObjectsInactive.Include);
#else
            FindObjectOfType<FloodSimulationSystem>(true);
#endif

        GridManager gridManager = _worldInstaller != null
            ? _worldInstaller.GridManager
            : GridManager.Instance;

        for (int i = 0; i < resolvers.Length; i++)
        {
            if (resolvers[i] == null)
                continue;

            resolvers[i].InstallRuntimeRefs(
                floodSimulationSystem,
                gridManager,
                worldBuildingManager
            );
        }

        //Debug.Log(
            //$"[BootstrapLoader] Installed WorldBuildingManager '{worldBuildingManager.name}' " +
            //$"into {resolvers.Length} flood building resolver(s)."
        //);
    }

    private void InstallAnimalSimulationIntoFloodSystems()
    {
        AnimalSimulation animalSimulation = AnimalSimulationAccess.Current;

        if (animalSimulation == null)
        {
            //Debug.LogWarning("[BootstrapLoader] Cannot install AnimalSimulation into flood systems because AnimalSimulationAccess.Current is null.");
            return;
        }

#if UNITY_2023_1_OR_NEWER
    FloodAnimalEffectResolver[] resolvers = FindObjectsByType<FloodAnimalEffectResolver>(
        FindObjectsInactive.Include,
        FindObjectsSortMode.None
    );
#else
        FloodAnimalEffectResolver[] resolvers = FindObjectsOfType<FloodAnimalEffectResolver>(true);
#endif

        FloodSimulationSystem floodSimulationSystem =
#if UNITY_2023_1_OR_NEWER
        FindFirstObjectByType<FloodSimulationSystem>(FindObjectsInactive.Include);
#else
            FindObjectOfType<FloodSimulationSystem>(true);
#endif

        GridManager gridManager = _worldInstaller != null
            ? _worldInstaller.GridManager
            : GridManager.Instance;

        for (int i = 0; i < resolvers.Length; i++)
        {
            if (resolvers[i] == null)
                continue;

            resolvers[i].InstallRuntimeRefs(
                floodSimulationSystem,
                gridManager,
                animalSimulation
            );
        }

        //Debug.Log(
            //$"[BootstrapLoader] Installed AnimalSimulation into {resolvers.Length} flood animal resolver(s)."
        //);
    }

    private void InstallAnimalSimulationIntoTsunamiSystems()
    {
        AnimalSimulation animalSimulation = AnimalSimulationAccess.Current;

        if (animalSimulation == null)
        {
            //Debug.LogWarning("[BootstrapLoader] Cannot install AnimalSimulation into tsunami systems because AnimalSimulationAccess.Current is null.");
            return;
        }

#if UNITY_2023_1_OR_NEWER
    TsunamiAnimalEffectResolver[] resolvers = FindObjectsByType<TsunamiAnimalEffectResolver>(
        FindObjectsInactive.Include,
        FindObjectsSortMode.None
    );
#else
        TsunamiAnimalEffectResolver[] resolvers = FindObjectsOfType<TsunamiAnimalEffectResolver>(true);
#endif

        TsunamiSimulationSystem simulationSystem =
#if UNITY_2023_1_OR_NEWER
        FindFirstObjectByType<TsunamiSimulationSystem>(FindObjectsInactive.Include);
#else
            FindObjectOfType<TsunamiSimulationSystem>(true);
#endif

        GridManager gridManager = _worldInstaller != null
            ? _worldInstaller.GridManager
            : GridManager.Instance;

        for (int i = 0; i < resolvers.Length; i++)
        {
            if (resolvers[i] == null)
                continue;

            resolvers[i].InstallRuntimeRefs(
                simulationSystem,
                gridManager,
                animalSimulation
            );
        }

        //Debug.Log(
            //$"[BootstrapLoader] Installed AnimalSimulation into {resolvers.Length} tsunami animal resolver(s)."
        //);
    }

    private void InstallWorldBuildingManagerIntoTsunamiSystems(WorldBuildingManager worldBuildingManager)
    {
        if (worldBuildingManager == null)
        {
            //Debug.LogWarning("[BootstrapLoader] Cannot install WorldBuildingManager into tsunami systems because it is null.");
            return;
        }

#if UNITY_2023_1_OR_NEWER
    TsunamiBuildingEffectResolver[] resolvers = FindObjectsByType<TsunamiBuildingEffectResolver>(
        FindObjectsInactive.Include,
        FindObjectsSortMode.None
    );
#else
        TsunamiBuildingEffectResolver[] resolvers = FindObjectsOfType<TsunamiBuildingEffectResolver>(true);
#endif

        TsunamiSimulationSystem simulationSystem =
#if UNITY_2023_1_OR_NEWER
        FindFirstObjectByType<TsunamiSimulationSystem>(FindObjectsInactive.Include);
#else
            FindObjectOfType<TsunamiSimulationSystem>(true);
#endif

        GridManager gridManager = _worldInstaller != null
            ? _worldInstaller.GridManager
            : GridManager.Instance;

        for (int i = 0; i < resolvers.Length; i++)
        {
            if (resolvers[i] == null)
                continue;

            resolvers[i].InstallRuntimeRefs(
                simulationSystem,
                gridManager,
                worldBuildingManager
            );
        }

        //Debug.Log(
            //$"[BootstrapLoader] Installed WorldBuildingManager '{worldBuildingManager.name}' " +
            //$"into {resolvers.Length} tsunami building resolver(s)."
        //);
    }

    private void InstallWorldBuildingManagerIntoEarthquakeSystems(WorldBuildingManager worldBuildingManager)
    {
        if (worldBuildingManager == null)
        {
            //Debug.LogWarning("[BootstrapLoader] Cannot install WorldBuildingManager into earthquake systems because it is null.");
            return;
        }

#if UNITY_2023_1_OR_NEWER
    EarthquakeBuildingEffectResolver[] resolvers = FindObjectsByType<EarthquakeBuildingEffectResolver>(
        FindObjectsInactive.Include,
        FindObjectsSortMode.None
    );
#else
        EarthquakeBuildingEffectResolver[] resolvers = FindObjectsOfType<EarthquakeBuildingEffectResolver>(true);
#endif

        EarthquakeSimulationSystem simulationSystem =
#if UNITY_2023_1_OR_NEWER
        FindFirstObjectByType<EarthquakeSimulationSystem>(FindObjectsInactive.Include);
#else
            FindObjectOfType<EarthquakeSimulationSystem>(true);
#endif

        MapGenerator mapGenerator = FindFirstObjectByType<MapGenerator>(FindObjectsInactive.Include);
        GridManager gridManager = _worldInstaller != null ? _worldInstaller.GridManager : null;

        for (int i = 0; i < resolvers.Length; i++)
        {
            if (resolvers[i] == null)
                continue;

            resolvers[i].InstallRuntimeRefs(
                simulationSystem,
                mapGenerator,
                gridManager,
                worldBuildingManager
            );
        }

        //Debug.Log(
            //$"[BootstrapLoader] Installed WorldBuildingManager '{worldBuildingManager.name}' " +
            //$"into {resolvers.Length} earthquake building resolver(s)."
        //);
    }

    private void Log(string label, float seconds)
    {
        if (!logBootstrapTimings)
            return;

        //Debug.Log($"[BootstrapLoader] {label}: {seconds:0.000}s");
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (worldSetupScene != null)
            worldSetupScenePath = AssetDatabase.GetAssetPath(worldSetupScene);
        else
            worldSetupScenePath = "";

        if (managerSetupScene != null)
            managerSetupScenePath = AssetDatabase.GetAssetPath(managerSetupScene);
        else
            managerSetupScenePath = "";

        if (uiSetupScene != null)
            uiSetupScenePath = AssetDatabase.GetAssetPath(uiSetupScene);
        else
            uiSetupScenePath = "";

        if (playerSetupScene != null)
            playerSetupScenePath = AssetDatabase.GetAssetPath(playerSetupScene);
        else
            playerSetupScenePath = "";

        if (finalSetupScene != null)
            finalSetupScenePath = AssetDatabase.GetAssetPath(finalSetupScene);
        else
            finalSetupScenePath = "";
        
        if (tutorialSetupScene != null)
            tutorialSetupScenePath = AssetDatabase.GetAssetPath(tutorialSetupScene);
        else
            tutorialSetupScenePath = "";
    }
#endif
}
