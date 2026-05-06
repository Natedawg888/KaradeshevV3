using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UISetupInstaller : MonoBehaviour
{
    [Header("UI Core")]
    [SerializeField] private ProfilePanelControl profilePanelControl;
    [SerializeField] private DiscoveredTilePanelControl discoveredTilePanelControl;
    [SerializeField] private UndiscoveredTilePanelControl undiscoveredTilePanelControl;
    [SerializeField] private InventoryPanelControl inventoryPanelControl;
    [SerializeField] private PlayerPopulationStatisticsPanelRoot playerPopulationStatisticsPanelRoot;
    [SerializeField] private UnitGroupPanelControl unitGroupPanelControl;
    [SerializeField] private ImmigrantOfferPanel immigrantOfferPanel;
    [SerializeField] private SoundPanelControl soundPanelControl;
    [SerializeField] private StageThemeApplier stageThemeApplier;
    [SerializeField] private SurveyPanelControl surveyPanelControl;
    [SerializeField] private BuildingPanelControl buildingPanelControl;
    [SerializeField] private BuildingDestroyedPanelControl buildingDestroyedPanelControl;
    [SerializeField] private BuildingCatalogPanelControl buildingCatalogPanelControl;
    [SerializeField] private BuildingPlacementPanelControl buildingPlacementPanelControl;
    [SerializeField] private CollectedGoodsPanelControl collectedGoodsPanelControl;
    [SerializeField] private ProductionRunningPanelControl productionRunningPanelControl;
    [SerializeField] private SummoningSpiritOfferPanelControl summoningSpiritOfferPanelControl;

    [Header("Minimap UI")]
    [SerializeField] private RawImage minimapImage;
    [SerializeField] private RenderTexture minimapRenderTexture;
    [SerializeField] private RectTransform cameraIcon;

    [Header("Camera UI")]
    [SerializeField] private Button cloudLayerToggleButton;

    [Header("Save UI")]
    [SerializeField] private SaveStatusUIControl saveStatusUIControl;

    public Scene LoadedScene => gameObject.scene;

    public ProfilePanelControl ProfilePanelControl => profilePanelControl;
    public DiscoveredTilePanelControl DiscoveredTilePanelControl => discoveredTilePanelControl;
    public UndiscoveredTilePanelControl UndiscoveredTilePanelControl => undiscoveredTilePanelControl;
    public InventoryPanelControl InventoryPanelControl => inventoryPanelControl;
    public PlayerPopulationStatisticsPanelRoot PlayerPopulationStatisticsPanelRoot => playerPopulationStatisticsPanelRoot;
    public UnitGroupPanelControl UnitGroupPanelControl => unitGroupPanelControl;
    public ImmigrantOfferPanel ImmigrantOfferPanel => immigrantOfferPanel;
    public SoundPanelControl SoundPanelControl => soundPanelControl;
    public StageThemeApplier StageThemeApplier => stageThemeApplier;
    public SurveyPanelControl SurveyPanelControl => surveyPanelControl;
    public BuildingPanelControl BuildingPanelControl => buildingPanelControl;
    public BuildingDestroyedPanelControl BuildingDestroyedPanelControl => buildingDestroyedPanelControl;
    public BuildingCatalogPanelControl BuildingCatalogPanelControl => buildingCatalogPanelControl;
    public BuildingPlacementPanelControl BuildingPlacementPanelControl => buildingPlacementPanelControl;
    public CollectedGoodsPanelControl CollectedGoodsPanelControl => collectedGoodsPanelControl;
    public ProductionRunningPanelControl ProductionRunningPanelControl => productionRunningPanelControl;
    public SummoningSpiritOfferPanelControl SummoningSpiritOfferPanelControl => summoningSpiritOfferPanelControl;

    public RawImage MinimapImage => minimapImage;
    public RenderTexture MinimapRenderTexture => minimapRenderTexture;
    public RectTransform CameraIcon => cameraIcon;

    public Button CloudLayerToggleButton => cloudLayerToggleButton;
    public SaveStatusUIControl SaveStatusUIControl => saveStatusUIControl;
}