using UnityEngine;
using UnityEngine.SceneManagement;

public class TutorialSetupInstaller : MonoBehaviour
{
    [Header("Tutorial Scene References")]
    [SerializeField] private CameraIntroTutorial cameraIntroTutorial;
    [SerializeField] private EnvironmentTileTutorial environmentTileTutorial;
    [SerializeField] private PopulationTutorial populationTutorial;
    [SerializeField] private DiscoveryTutorial discoveryTutorial;
    [SerializeField] private GatheringTutorial gatheringTutorial;
    [SerializeField] private InventoryTutorial inventoryTutorial;
    [SerializeField] private PopulationStatisticsTutorial populationStatisticsTutorial;
    [SerializeField] private ExtraUITutorial extraUITutorial;
    [SerializeField] private ProfileTutorial profileTutorial;
    [SerializeField] private BuildingTutorial buildingTutorial;
    [SerializeField] private BuildingTutorialPart2 buildingTutorialPart2;
    [SerializeField] private CraftingTutorial craftingTutorial;
    [SerializeField] private ProductionTutorial productionTutorial;
    [SerializeField] private ProductionRunningTutorial productionRunningTutorial;

    public Scene LoadedScene => gameObject.scene;
    public CameraIntroTutorial CameraIntroTutorial => cameraIntroTutorial;
    public EnvironmentTileTutorial EnvironmentTileTutorial => environmentTileTutorial;
    public PopulationTutorial PopulationTutorial => populationTutorial;
    public DiscoveryTutorial DiscoveryTutorial => discoveryTutorial;
    public GatheringTutorial GatheringTutorial => gatheringTutorial;
    public InventoryTutorial InventoryTutorial => inventoryTutorial;
    public PopulationStatisticsTutorial PopulationStatisticsTutorial => populationStatisticsTutorial;
    public ExtraUITutorial ExtraUITutorial => extraUITutorial;
    public ProfileTutorial ProfileTutorial => profileTutorial;
    public BuildingTutorial BuildingTutorial => buildingTutorial;
    public BuildingTutorialPart2 BuildingTutorialPart2 => buildingTutorialPart2;
    public CraftingTutorial CraftingTutorial => craftingTutorial;
    public ProductionTutorial ProductionTutorial => productionTutorial;
    public ProductionRunningTutorial ProductionRunningTutorial => productionRunningTutorial;

    private void Awake()
    {
        if (cameraIntroTutorial == null)
            cameraIntroTutorial = GetComponentInChildren<CameraIntroTutorial>(true);

        if (environmentTileTutorial == null)
            environmentTileTutorial = GetComponentInChildren<EnvironmentTileTutorial>(true);

        if (populationTutorial == null)
            populationTutorial = GetComponentInChildren<PopulationTutorial>(true);

        if (discoveryTutorial == null)
            discoveryTutorial = GetComponentInChildren<DiscoveryTutorial>(true);

        if (gatheringTutorial == null)
            gatheringTutorial = GetComponentInChildren<GatheringTutorial>(true);

        if (inventoryTutorial == null)
            inventoryTutorial = GetComponentInChildren<InventoryTutorial>(true);

        if (populationStatisticsTutorial == null)
            populationStatisticsTutorial = GetComponentInChildren<PopulationStatisticsTutorial>(true);

        if (extraUITutorial == null)
            extraUITutorial = GetComponentInChildren<ExtraUITutorial>(true);

        if (profileTutorial == null)
            profileTutorial = GetComponentInChildren<ProfileTutorial>(true);

        if (buildingTutorial == null)
            buildingTutorial = GetComponentInChildren<BuildingTutorial>(true);

        if (buildingTutorialPart2 == null)
            buildingTutorialPart2 = GetComponentInChildren<BuildingTutorialPart2>(true);

        if (craftingTutorial == null)
            craftingTutorial = GetComponentInChildren<CraftingTutorial>(true);

        if (productionTutorial == null)
            productionTutorial = GetComponentInChildren<ProductionTutorial>(true);

        if (productionRunningTutorial == null)
            productionRunningTutorial = GetComponentInChildren<ProductionRunningTutorial>(true);

        if (cameraIntroTutorial == null) {}
            //Debug.LogWarning("[TutorialSetupInstaller] CameraIntroTutorial not found in TutorialSetup scene.");

        if (environmentTileTutorial == null) {}
            //Debug.LogWarning("[TutorialSetupInstaller] EnvironmentTileTutorial not found in TutorialSetup scene.");

        if (populationTutorial == null) {}
            //Debug.LogWarning("[TutorialSetupInstaller] PopulationTutorial not found in TutorialSetup scene.");

        if (discoveryTutorial == null) {}
            //Debug.LogWarning("[TutorialSetupInstaller] DiscoveryTutorial not found in TutorialSetup scene.");

        if (gatheringTutorial == null) {}
            //Debug.LogWarning("[TutorialSetupInstaller] GatheringTutorial not found in TutorialSetup scene.");

        if (inventoryTutorial == null) {}
            //Debug.LogWarning("[TutorialSetupInstaller] InventoryTutorial not found in TutorialSetup scene.");

        if (populationStatisticsTutorial == null) {}
            //Debug.LogWarning("[TutorialSetupInstaller] PopulationStatisticsTutorial not found in TutorialSetup scene.");

        if (extraUITutorial == null) {}
            //Debug.LogWarning("[TutorialSetupInstaller] ExtraUITutorial not found in TutorialSetup scene.");

        if (profileTutorial == null) {}
            //Debug.LogWarning("[TutorialSetupInstaller] ProfileTutorial not found in TutorialSetup scene.");

        if (buildingTutorial == null) {}
            //Debug.LogWarning("[TutorialSetupInstaller] BuildingTutorial not found in TutorialSetup scene.");

        if (buildingTutorialPart2 == null) {}
            //Debug.LogWarning("[TutorialSetupInstaller] BuildingTutorialPart2 not found in TutorialSetup scene.");
    }

    public void InstallBootstrapReferences(
        CameraControl cameraControl,
        GridManager gridManager,
        MonoEnvironmentDataSource environmentDataSource,
        UndiscoveredTilePanelControl undiscoveredTilePanelControl,
        DiscoveredTilePanelControl discoveredTilePanelControl,
        CollectedGoodsPanelControl collectedGoodsPanelControl,
        InventoryPanelControl inventoryPanelControl,
        PlayerPopulationStatisticsPanelRoot playerPopulationStatisticsPanelRoot,
        ProfilePanelControl profilePanelControl,
        BuildingPanelControl buildingPanelControl,
        BuildingDestroyedPanelControl buildingDestroyedPanelControl,
        BuildingCatalogPanelControl buildingCatalogPanelControl,
        ProductionRunningPanelControl productionRunningPanelControl,
        TileInteraction tileInteraction)
    {
        if (cameraIntroTutorial != null)
            cameraIntroTutorial.InstallRuntimeRefs(cameraControl, environmentTileTutorial);

        if (environmentTileTutorial != null)
            environmentTileTutorial.InstallRuntimeRefs(
                cameraControl,
                gridManager,
                environmentDataSource
            );

        if (populationTutorial != null)
            populationTutorial.InstallRuntimeRefs(discoveryTutorial);

        if (discoveryTutorial != null)
        {
            discoveryTutorial.InstallRuntimeRefs(
                undiscoveredTilePanelControl,
                cameraControl,
                tileInteraction,
                gatheringTutorial
            );
        }

        if (gatheringTutorial != null)
        {
            gatheringTutorial.InstallRuntimeRefs(
                discoveredTilePanelControl,
                discoveredTilePanelControl != null ? discoveredTilePanelControl.surveyPanel : null,
                collectedGoodsPanelControl,
                inventoryTutorial
            );
        }

        if (inventoryTutorial != null)
        {
            inventoryTutorial.InstallRuntimeRefs(
                inventoryPanelControl,
                cameraControl,
                tileInteraction,
                populationStatisticsTutorial
            );
        }

        if (populationStatisticsTutorial != null)
        {
            populationStatisticsTutorial.InstallRuntimeRefs(
                playerPopulationStatisticsPanelRoot,
                cameraControl,
                tileInteraction,
                extraUITutorial
            );
        }

        if (extraUITutorial != null)
        {
            extraUITutorial.InstallRuntimeRefs(
                profilePanelControl,
                cameraControl,
                tileInteraction,
                profileTutorial
            );
        }

        if (profileTutorial != null)
        {
            profileTutorial.InstallRuntimeRefs(profilePanelControl);
        }

        if (buildingTutorialPart2 != null)
        {
            buildingTutorialPart2.InstallRuntimeRefs(
                buildingPanelControl,
                buildingPanelControl != null ? buildingPanelControl.shelterPanel : null,
                buildingPanelControl != null ? buildingPanelControl.storagePanel : null,
                cameraControl,
                tileInteraction
            );
        }

        if (buildingTutorial != null)
        {
            buildingTutorial.InstallRuntimeRefs(
                buildingPanelControl,
                buildingDestroyedPanelControl,
                discoveredTilePanelControl,
                buildingCatalogPanelControl,
                cameraControl,
                tileInteraction,
                buildingTutorialPart2
            );
        }

        if (buildingPanelControl != null && buildingPanelControl.craftingPanel != null)
        {
            buildingPanelControl.craftingPanel.InstallRuntimeRefs(
                newCraftingTutorial: craftingTutorial,
                refreshIfOpen: false
            );
        }

        if (buildingPanelControl != null && buildingPanelControl.productionPanel != null)
        {
            buildingPanelControl.productionPanel.InstallRuntimeRefs(
                newProductionTutorial: productionTutorial,
                refreshIfOpen: false
            );
        }

        if (productionRunningPanelControl != null)
        {
            productionRunningPanelControl.InstallRuntimeRefs(
                productionRunningTutorial
            );
        }
    }
}
