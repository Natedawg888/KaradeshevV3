using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.UI.Extensions;

public partial class StageThemeApplier
{
    [Header("Main Canvas")]

    [Header("Canvas Banner Images")]
    public Image topBanner;
    public Image bottomBanner;

    [Header("Canvas Horizontal Borders")]
    public List<Image> horizontalBorders = new();   // assign all border Images here

    [Header("Canvas Square Borders")]
    public List<Image> squareBorders = new();

    [Header("Title Cards")]
    public List<Image> titleCards = new();

    [Header("Population Target")]
    public Image populationIconTarget;

    [Header("Texts — TextMeshPro")]
    public List<TMP_Text> tmpTexts = new();

    [Header("Season Targets")]
    public Image seasonIconTarget;       // icon image to swap per season
    public Image seasonFillTarget;       // fill image to swap per season

    [Header("Phase Targets")]
    public Image phaseIconTarget;
    public Image phaseFillTarget;

    [Header("Panel Targets")]
    public List<Image> panelImages = new();

    [Header("Randomise Picture Button")]
    public Image randomiseButtonImage;

    [Header("Profile / Avatar Menu")]
    public ProfilePanelControl profilePanelControl;
    public RectTransform avatarScrollViewRect;

    [Header("Inventory Button")]
    public Image inventoryButtonImage;

    [Header("Miscellaneous Buttons")]
    public List<Image> miscButtonImages = new();
    public List<TMP_Text> miscButtonTexts = new();

    [Header("Info Buttons")]
    public List<Image> infoButtonImages = new();

    [Header("Inventory Texts")]
    public List<TMP_Text> inventoryText1 = new();   // list of 2+ texts
    public List<TMP_Text> inventoryText2 = new();   // list of 2+ texts
    public RectTransform inventoryScrollViewRect;

    public GridLayoutGroup inventoryGrid;

    [Header("Inventory Panel")]
    public InventoryPanelControl inventoryPanelControl;

    [Header("Undiscovery Panel Texts")]
    public List<TMP_Text> undiscoveryText1 = new();
    public List<TMP_Text> undiscoveryText2 = new();

    [Header("Undiscovery Information Panel")]
    public RectTransform undiscoveryPanelRect;

    [Header("Information Object")]
    public RectTransform infoObjectRect;

    [Header("Discovered Panel")]
    public Image discoveredPanelBorderImage;

    [Header("Discovered Panel")]
    public RectTransform discoveredPanelRect;

    [Header("Discovered Panel — Child Object")]
    public RectTransform discoveredPanelChildRect;

    [Header("Discovered Panel — Child Object")]
    public RectTransform discoveredPanelChild2Rect;

    [Header("Name Edit Buttons")]
    public List<Image> nameEditButtonImages = new();
    public List<Image> nameCancelButtonImages = new();

    [Header("Discovered Panel Texts")]
    public List<TMP_Text> discoveredText1 = new();

    [Header("Themed Sliders")]
    public List<RectTransform> sliderRects = new();

    [Header("Gathering Info Panel")]
    public RectTransform gatheringInfoPanelRect;

    [Header("Gathering Info Object")]
    public RectTransform gatheringInfoObjectRect;

    [Header("Survey Info Panel")]
    public RectTransform surveyInfoPanelRect;

    [Header("Survey Info Object")]
    public RectTransform surveyInfoObjectRect;

    [Header("Survey Panel")]
    public RectTransform surveyPanelRect;

    [Header("Survey Object")]
    public RectTransform surveyObjectRect;

    [Header("Survey Panel")]
    public SurveyPanelControl surveyPanelControl;

    [Header("Collected Goods Panel")]
    public RectTransform collectedGoodsPanelRect;

    [Header("Collected Goods Object")]
    public RectTransform collectedGoodsObjectRect;

    [Header("Collected Goods 2 Object")]
    public RectTransform collectedGoods2ObjectRect;

    [Header("Collected Goods Text 1")]
    public List<TMP_Text> collectedGoodsText1 = new();

    [Header("Collected Goods Panel — Control")]
    public CollectedGoodsPanelControl collectedGoodsPanelControl;

    [Header("Experience Tracker")]
    public Transform xpTrackerMount;     // empty GameObject under your canvas
    private GameObject currentXPTracker; // keep track of the spawned instance

    [Header("Population Pie Targets")]
    public Image malePieImage;     // should be an Image with Type = Filled (Radial)
    public Image femalePieImage;

    [Header("Population Texts")]
    public List<TMP_Text> populationText1 = new();

    [Header("Bar Targets")]
    public List<Image> barImages = new();

    [Header("Age Group Targets")]
    public List<Image> childrenImages = new();
    public List<Image> teensImages = new();
    public List<Image> adultsImages = new();
    public List<Image> eldersImages = new();

    [Header("Line Renderer Targets")]
    public List<UILineRenderer> uiLineRenderers = new();
    public List<LineRenderer> worldLineRenderers = new();

    [Header("Refs")]
    public StageThemeLibrary themeLibrary;
    public LevelManager levelManager;
    public PlayerLevel playerLevel;
    public SeasonManager seasonManager;
    public TurnSystem turnSystem;

    private void OnEnable()
    {
        if (playerLevel != null) playerLevel.OnLevelUp += HandleLevelUp;
        if (seasonManager != null) seasonManager.OnSeasonChanged += HandleSeasonChanged;

        // adopt pre-placed tracker
        if (xpTrackerMount && currentXPTracker == null && xpTrackerMount.childCount > 0)
            currentXPTracker = xpTrackerMount.GetChild(0).gameObject;

        if (Application.isPlaying) ApplyCurrentStage();
    }

    private void OnDisable()
    {
        if (playerLevel != null) playerLevel.OnLevelUp -= HandleLevelUp;
        if (seasonManager != null) seasonManager.OnSeasonChanged -= HandleSeasonChanged;
    }

    private void HandleLevelUp(int _) { if (Application.isPlaying) ApplyCurrentStage(); }
    private void HandleSeasonChanged(SeasonDefinition _) { if (Application.isPlaying) ApplySeasonSpritesForCurrentStage(); }

    public void RefreshAfterLoad()
    {
        if (!Application.isPlaying) return;
        ApplyCurrentStage();
    }
}
