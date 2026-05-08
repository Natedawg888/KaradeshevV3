using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class FinalSetupInstaller : MonoBehaviour
{
    [Header("Resolved UI References")]
    [SerializeField] private Button endMovementButton;
    [SerializeField] private Button battleModeButton;
    [SerializeField] private Button repellerZoneButton;
    [SerializeField] private GameObject startingPointPickerPanel;
    [SerializeField] private Button startingPointPrevButton;
    [SerializeField] private Button startingPointNextButton;
    [SerializeField] private Button startingPointConfirmButton;

    [Header("Target Components In FinalSetup Scene")]
    [SerializeField] private UnitGroupMovementManager unitGroupMovementManager;
    [SerializeField] private TileWorldCanvasToggleButton tileWorldCanvasToggleButton;
    [SerializeField] private AnimalSimulationController animalSimulationController;
    [SerializeField] private RepellerZoneVisualizer repellerZoneVisualizer;
    [SerializeField] private Camera minimapCamera;
    [SerializeField] private StartingPointPicker startingPointPicker;

    [Header("UI Object Names")]
    [SerializeField] private string endMovementButtonObjectName = "EndMovementButton";
    [SerializeField] private string battleModeButtonObjectName = "BattleModeButton";
    [SerializeField] private string repellerZoneButtonObjectName = "RepellerZoneButton";
    [SerializeField] private string startingPointPickerPanelObjectName = "StartingPointPicker";
    [SerializeField] private string startingPointPrevButtonObjectName = "StartLeftButton";
    [SerializeField] private string startingPointNextButtonObjectName = "StartRightButton";
    [SerializeField] private string startingPointConfirmButtonObjectName = "StartingConfirmButton";

    public Scene LoadedScene => gameObject.scene;

    public Camera MinimapCamera => minimapCamera;
    public AnimalSimulationController AnimalSimulationController => animalSimulationController;
    public StartingPointPicker StartingPointPicker => startingPointPicker;

    public void ResolveFromUIScene(Scene uiScene)
    {
        if (!uiScene.IsValid() || !uiScene.isLoaded)
        {
            //Debug.LogError("[FinalSetupInstaller] UI scene is not valid or not loaded.");
            return;
        }

        ResolveFinalSceneTargets();

        endMovementButton = FindComponentInSceneByName<Button>(uiScene, endMovementButtonObjectName);
        battleModeButton = FindComponentInSceneByName<Button>(uiScene, battleModeButtonObjectName);
        repellerZoneButton = FindComponentInSceneByName<Button>(uiScene, repellerZoneButtonObjectName);

        GameObject panelGO = FindGameObjectInSceneByName(uiScene, startingPointPickerPanelObjectName);
        startingPointPickerPanel = panelGO;

        startingPointPrevButton = FindComponentInSceneByName<Button>(uiScene, startingPointPrevButtonObjectName);
        startingPointNextButton = FindComponentInSceneByName<Button>(uiScene, startingPointNextButtonObjectName);
        startingPointConfirmButton = FindComponentInSceneByName<Button>(uiScene, startingPointConfirmButtonObjectName);

        LogMissing("End Movement Button", endMovementButton, endMovementButtonObjectName);
        LogMissing("Battle Mode Button", battleModeButton, battleModeButtonObjectName);
        LogMissing("Repeller Zone Button", repellerZoneButton, repellerZoneButtonObjectName);
        LogMissing("Starting Point Picker Panel", startingPointPickerPanel, startingPointPickerPanelObjectName);
        LogMissing("Starting Point Prev Button", startingPointPrevButton, startingPointPrevButtonObjectName);
        LogMissing("Starting Point Next Button", startingPointNextButton, startingPointNextButtonObjectName);
        LogMissing("Starting Point Confirm Button", startingPointConfirmButton, startingPointConfirmButtonObjectName);

        InstallResolvedRefs();
    }

    private void ResolveFinalSceneTargets()
    {
        if (unitGroupMovementManager == null)
            unitGroupMovementManager = FindComponentInScene<UnitGroupMovementManager>(LoadedScene);

        if (tileWorldCanvasToggleButton == null)
            tileWorldCanvasToggleButton = FindComponentInScene<TileWorldCanvasToggleButton>(LoadedScene);

        if (repellerZoneVisualizer == null)
            repellerZoneVisualizer = FindComponentInScene<RepellerZoneVisualizer>(LoadedScene);

        if (animalSimulationController == null)
            animalSimulationController = FindComponentInScene<AnimalSimulationController>(LoadedScene);

        if (startingPointPicker == null)
            startingPointPicker = FindComponentInScene<StartingPointPicker>(LoadedScene);

        if (unitGroupMovementManager == null)
            //Debug.LogWarning("[FinalSetupInstaller] UnitGroupMovementManager not found in FinalSetup scene.");

        if (tileWorldCanvasToggleButton == null)
            //Debug.LogWarning("[FinalSetupInstaller] TileWorldCanvasToggleButton not found in FinalSetup scene.");

        if (animalSimulationController == null)
            //Debug.LogWarning("[FinalSetupInstaller] AnimalSimulationController not found in FinalSetup scene.");

        if (startingPointPicker == null)
            //Debug.LogWarning("[FinalSetupInstaller] StartingPointPicker not found in FinalSetup scene.");

        if (minimapCamera == null)
            //Debug.LogWarning("[FinalSetupInstaller] MinimapCamera is not assigned in FinalSetupInstaller.");
    }

    private void InstallResolvedRefs()
    {
        if (unitGroupMovementManager != null)
            unitGroupMovementManager.SetEndMovementButton(endMovementButton);

        if (tileWorldCanvasToggleButton != null)
            tileWorldCanvasToggleButton.SetToggleButton(battleModeButton);

        if (repellerZoneVisualizer != null)
            repellerZoneVisualizer.SetToggleButton(repellerZoneButton);
    }

    public void InstallBootstrapReferences(
        CameraControl cameraControl,
        TileActivator tileActivator,
        CameraIntroTutorial cameraIntroTutorial,
        EnvironmentTileTutorial environmentTileTutorial)
    {
        if (startingPointPicker != null)
        {
            startingPointPicker.InstallRuntimeRefs(
                newPanel: startingPointPickerPanel,
                newPrevButton: startingPointPrevButton,
                newNextButton: startingPointNextButton,
                newConfirmButton: startingPointConfirmButton,
                newCameraControl: cameraControl,
                newTileActivator: tileActivator,
                newCameraIntroTutorial: cameraIntroTutorial,
                newEnvironmentTileTutorial: environmentTileTutorial
            );
        }
    }

    public void BeginFinalSetupPhase()
    {
        if (startingPointPicker != null)
            startingPointPicker.BeginSelection();
    }

    private static void LogMissing(string label, Object value, string objectName)
    {
        if (value == null)
            //Debug.LogWarning($"[FinalSetupInstaller] Could not resolve {label} from UI scene using object name '{objectName}'.");
    }

    private static T FindComponentInSceneByName<T>(Scene scene, string targetName) where T : Component
    {
        if (string.IsNullOrWhiteSpace(targetName))
            return null;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T found = FindComponentRecursiveByName<T>(root.transform, targetName);
            if (found != null)
                return found;
        }

        return null;
    }

    private static GameObject FindGameObjectInSceneByName(Scene scene, string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName))
            return null;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform found = FindTransformRecursiveByName(root.transform, targetName);
            if (found != null)
                return found.gameObject;
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

    private static Transform FindTransformRecursiveByName(Transform current, string targetName)
    {
        if (current.name == targetName)
            return current;

        for (int i = 0; i < current.childCount; i++)
        {
            Transform found = FindTransformRecursiveByName(current.GetChild(i), targetName);
            if (found != null)
                return found;
        }

        return null;
    }

    private static T FindComponentInScene<T>(Scene scene) where T : Component
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return null;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T found = root.GetComponentInChildren<T>(true);
            if (found != null)
                return found;
        }

        return null;
    }

    public void RegisterAnimalSimulationController(AnimalSimulationController controller)
    {
        if (controller == null)
            return;

        animalSimulationController = controller;
    }
}
