using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BuildingTutorial : MonoBehaviour
{
    private enum TutorialStep
    {
        SelectStarterBuilding,
        ClickDestroy,
        SelectDestroyedBuilding,
        ClickClear,
        SelectClearedTile,
        ClickTileBuildButton,
        CatalogExplainCosts,
        CatalogExplainTurnsAndPopulation,
        CatalogClickBuild,
        GhostBuildToComplete
    }

    [Header("UI Root")]
    [SerializeField] private GameObject root;
    [SerializeField] private CanvasGroup rootCanvasGroup;

    [Header("Overlays")]
    [SerializeField] private GameObject darkOverlayWithHole;
    [SerializeField] private GameObject darkOverlayWithHole2;
    [SerializeField] private GameObject darkOverlayWithHole3;
    [SerializeField] private GameObject darkOverlayWithHole4;
    [SerializeField] private GameObject darkOverlayWithHole5;
    [SerializeField] private GameObject darkOverlayWithHole6;

    [Header("Message UI")]
    [SerializeField] private GameObject messagePanel;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button skipButton;

    [Header("References")]
    [SerializeField] private BuildingPanelControl buildingPanel;
    [SerializeField] private BuildingDestroyedPanelControl buildingDestroyedPanel;
    [SerializeField] private DiscoveredTilePanelControl discoveredTilePanel;
    [SerializeField] private BuildingCatalogPanelControl buildingCatalogPanel;
    [SerializeField] private CameraControl cameraControl;
    [SerializeField] private TileInteraction tileInteraction;

    [Header("Messages")]
    [SerializeField] private string selectStarterMessage = "Select your starter building tile.";
    [SerializeField] private string destroyMessage = "Click destroy.";
    [SerializeField] private string selectDestroyedMessage = "Now click the destroyed building.";
    [SerializeField] private string clearMessage = "Click clear to instantly clear it.";
    [SerializeField] private string selectClearedTileMessage = "Now click that tile.";
    [SerializeField] private string openBuildMessage = "Click the build button.";
    [SerializeField] private string costsMessage = "These are the building costs.";
    [SerializeField] private string turnsAndPopulationMessage = "Here you can see how many people and turns it takes.";
    [SerializeField] private string clickBuildCatalogMessage = "Now click build.";
    [SerializeField] private string ghostBuildMessage = "The building will now tick over until construction completes.";

    [Header("Settings")]
    [SerializeField] private bool resumeTurnTimerWhenFinished = true;

    private bool _running;
    private bool _completedThisGame;
    private bool _cameraLockedByTutorial;
    private TutorialStep _step = TutorialStep.SelectStarterBuilding;

    private BuildingControl _starterBuilding;
    private BuildingStatus _starterStatus;
    private BuildingInstance _starterInstance;
    private EnvironmentControl _restoredEnvironment;
    private BuildingCatalogItem _activeCatalogItem;

    public bool IsRunning => _running;
    public bool HasCompletedTutorial => _completedThisGame;

    [SerializeField] private BuildingTutorialPart2 buildingTutorialPart2;

    private Vector3 _buildTargetPosition;
    private Transform _buildTargetParent;

    public bool ShouldRunTutorial()
    {
        return !_running && !_completedThisGame;
    }

    private void Awake()
    {
        if (buildingPanel == null)
            buildingPanel = FindObjectOfType<BuildingPanelControl>(true);

        if (buildingDestroyedPanel == null)
            buildingDestroyedPanel = FindObjectOfType<BuildingDestroyedPanelControl>(true);

        if (discoveredTilePanel == null)
            discoveredTilePanel = FindObjectOfType<DiscoveredTilePanelControl>(true);

        if (buildingCatalogPanel == null)
            buildingCatalogPanel = FindObjectOfType<BuildingCatalogPanelControl>(true);

        if (cameraControl == null)
            cameraControl = FindObjectOfType<CameraControl>(true);

        if (tileInteraction == null)
            tileInteraction = FindObjectOfType<TileInteraction>(true);

        if (buildingTutorialPart2 == null)
            buildingTutorialPart2 = FindObjectOfType<BuildingTutorialPart2>(true);

        BindButtons();
        SetRootVisible(false);
        SetBlockingMode(false);
        SetAllOverlaysOff();
        ShowMessagePanel(false);
        SetContinueButtonVisible(false);
        SetSkipButtonVisible(false);
    }

    public void InstallRuntimeRefs(
        BuildingPanelControl newBuildingPanel = null,
        BuildingDestroyedPanelControl newBuildingDestroyedPanel = null,
        DiscoveredTilePanelControl newDiscoveredTilePanel = null,
        BuildingCatalogPanelControl newBuildingCatalogPanel = null,
        CameraControl newCameraControl = null,
        TileInteraction newTileInteraction = null,
        BuildingTutorialPart2 newBuildingTutorialPart2 = null)
    {
        if (newBuildingPanel != null)
            buildingPanel = newBuildingPanel;
        else if (buildingPanel == null)
            buildingPanel = FindObjectOfType<BuildingPanelControl>(true);

        if (newBuildingDestroyedPanel != null)
            buildingDestroyedPanel = newBuildingDestroyedPanel;
        else if (buildingDestroyedPanel == null)
            buildingDestroyedPanel = FindObjectOfType<BuildingDestroyedPanelControl>(true);

        if (newDiscoveredTilePanel != null)
            discoveredTilePanel = newDiscoveredTilePanel;
        else if (discoveredTilePanel == null)
            discoveredTilePanel = FindObjectOfType<DiscoveredTilePanelControl>(true);

        if (newBuildingCatalogPanel != null)
            buildingCatalogPanel = newBuildingCatalogPanel;
        else if (buildingCatalogPanel == null)
            buildingCatalogPanel = FindObjectOfType<BuildingCatalogPanelControl>(true);

        if (newCameraControl != null)
            cameraControl = newCameraControl;
        else if (cameraControl == null)
            cameraControl = FindObjectOfType<CameraControl>(true);

        if (newTileInteraction != null)
            tileInteraction = newTileInteraction;
        else if (tileInteraction == null)
            tileInteraction = FindObjectOfType<TileInteraction>(true);

        if (newBuildingTutorialPart2 != null)
            buildingTutorialPart2 = newBuildingTutorialPart2;
        else if (buildingTutorialPart2 == null)
            buildingTutorialPart2 = FindObjectOfType<BuildingTutorialPart2>(true);

        BindButtons();
    }

    public void BeginTutorial()
    {
        if (!ShouldRunTutorial())
        {
            if (resumeTurnTimerWhenFinished)
                TurnSystem.Instance?.ResumeTurnTimer();
            return;
        }

        ResolveStarterBuilding();

        if (_starterBuilding == null)
        {
            Debug.LogWarning("[BuildingTutorial] Could not resolve starter building.");
            if (resumeTurnTimerWhenFinished)
                TurnSystem.Instance?.ResumeTurnTimer();
            return;
        }

        _running = true;
        _step = TutorialStep.SelectStarterBuilding;
        _restoredEnvironment = null;
        _activeCatalogItem = null;

        TurnSystem.Instance?.PauseTurnTimer();

        if (cameraControl != null && !_cameraLockedByTutorial)
        {
            cameraControl.PushInputLock();
            _cameraLockedByTutorial = true;
        }

        TileInteraction.SetSelectionEnabled(false);
        tileInteraction?.EnableSelectionAfter(0.01f);

        BindTutorialHooks();

        SetRootVisible(true);
        SetBlockingMode(false);
        SetAllOverlaysOff();
        SetContinueButtonVisible(false);
        SetSkipButtonVisible(false);
        SetMessage(selectStarterMessage);
    }

    private void Update()
    {
        if (!_running)
            return;

        switch (_step)
        {
            case TutorialStep.SelectStarterBuilding:
                {
                    if (IsStarterBuildingPanelShowing())
                    {
                        _step = TutorialStep.ClickDestroy;
                        SetAllOverlaysOff();
                        SetOverlayVisible(darkOverlayWithHole, true);
                        SetMessage(destroyMessage);
                    }
                    break;
                }

            case TutorialStep.SelectDestroyedBuilding:
                {
                    if (IsStarterDestroyedPanelShowing())
                    {
                        _step = TutorialStep.ClickClear;
                        SetAllOverlaysOff();
                        SetOverlayVisible(darkOverlayWithHole2, true);
                        SetMessage(clearMessage);
                    }
                    break;
                }

            case TutorialStep.SelectClearedTile:
                {
                    if (IsRestoredTilePanelShowing())
                    {
                        _step = TutorialStep.ClickTileBuildButton;
                        SetAllOverlaysOff();
                        SetOverlayVisible(darkOverlayWithHole3, true);
                        SetMessage(openBuildMessage);
                    }
                    break;
                }
        }
    }

    private void OnContinuePressed()
    {
        if (!_running)
            return;

        switch (_step)
        {
            case TutorialStep.CatalogExplainCosts:
                {
                    _step = TutorialStep.CatalogExplainTurnsAndPopulation;
                    SetAllOverlaysOff();
                    SetOverlayVisible(darkOverlayWithHole5, true);
                    SetContinueButtonVisible(true);
                    SetMessage(turnsAndPopulationMessage);
                    break;
                }

            case TutorialStep.CatalogExplainTurnsAndPopulation:
                {
                    _step = TutorialStep.CatalogClickBuild;
                    SetAllOverlaysOff();
                    SetOverlayVisible(darkOverlayWithHole6, true);
                    SetContinueButtonVisible(false);
                    SetMessage(clickBuildCatalogMessage);
                    break;
                }
        }
    }

    private void OnSkipPressed()
    {
        SkipTutorial();
    }

    private bool HandleTutorialDestroyRequested(BuildingControl building)
    {
        if (!_running || _step != TutorialStep.ClickDestroy || building == null)
            return false;

        if (building != _starterBuilding)
            return false;

        BuildingStatus status = building.GetComponent<BuildingStatus>();
        BuildingHealth health = building.GetComponent<BuildingHealth>();

        if (status == null)
            return false;

        if (health != null)
            health.ApplyDamage(health.CurrentHealth);
        else
            status.SetState(BuildingState.Destroyed);

        _step = TutorialStep.SelectDestroyedBuilding;
        SetAllOverlaysOff();
        SetMessage(selectDestroyedMessage);

        if (buildingPanel != null)
            buildingPanel.Hide();

        return true;
    }

    private bool HandleTutorialClearRequested(BuildingControl building)
    {
        if (!_running || _step != TutorialStep.ClickClear || building == null)
            return false;

        if (_starterBuilding != null && building != _starterBuilding)
            return false;

        BuildingStatus status = building.GetComponent<BuildingStatus>();
        if (status == null)
            return false;

        Vector3 clearPos = building.transform.position;
        Transform clearParent = building.transform.parent;

        status.TryClearToBaseTile();

        _starterBuilding = null;
        _starterStatus = null;
        _starterInstance = null;

        _restoredEnvironment = FindRestoredEnvironmentNear(clearPos, clearParent);

        if (_restoredEnvironment != null)
        {
            EnvironmentStatus envStatus = _restoredEnvironment.GetComponent<EnvironmentStatus>();
            if (envStatus != null)
                envStatus.SetDiscovered(true);
        }

        _step = TutorialStep.SelectClearedTile;
        SetAllOverlaysOff();
        SetMessage(selectClearedTileMessage);

        return true;
    }

    private bool HandleTutorialTileBuildRequested(EnvironmentControl env)
    {
        if (!_running || _step != TutorialStep.ClickTileBuildButton || env == null)
            return false;

        if (_restoredEnvironment != null && env != _restoredEnvironment)
            return false;

        if (buildingCatalogPanel == null)
            return false;

        _buildTargetPosition = env.transform.position;
        _buildTargetParent = env.transform.parent;

        buildingCatalogPanel.ShowFor(env, discoveredTilePanel);
        _activeCatalogItem = buildingCatalogPanel.PrimaryItem;

        if (_activeCatalogItem == null)
        {
            Debug.LogWarning("[BuildingTutorial] No building catalog item available for tutorial.");
            return true;
        }

        _activeCatalogItem.TutorialBuildOverride = HandleTutorialCatalogBuildRequested;

        _step = TutorialStep.CatalogExplainCosts;
        SetAllOverlaysOff();
        SetOverlayVisible(darkOverlayWithHole4, true);
        SetContinueButtonVisible(true);
        SetMessage(costsMessage);
        return true;
    }

    private BuildingControl FindBuiltBuildingNear(Vector3 pos, Transform parent)
    {
        BuildingControl[] all = FindObjectsOfType<BuildingControl>(true);

        BuildingControl best = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < all.Length; i++)
        {
            BuildingControl b = all[i];
            if (b == null)
                continue;

            BuildingStatus status = b.GetComponent<BuildingStatus>();
            if (status != null && status.CurrentState == BuildingState.Destroyed)
                continue;

            if (parent != null && b.transform.parent != parent)
                continue;

            float dist = Vector3.Distance(b.transform.position, pos);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = b;
            }
        }

        return best;
    }

    private bool HandleTutorialCatalogBuildRequested(BuildingCatalogItem item)
    {
        if (!_running || _step != TutorialStep.CatalogClickBuild || item == null)
            return false;

        if (_activeCatalogItem != null && item != _activeCatalogItem)
            return false;

        Building def = item.Definition;
        EnvironmentControl env = item.TargetEnvironment;

        _step = TutorialStep.GhostBuildToComplete;
        SetAllOverlaysOff();
        SetContinueButtonVisible(false);
        SetMessage(ghostBuildMessage);

        bool started = item.TryStartRealBuild();
        if (!started)
        {
            Debug.LogWarning("[BuildingTutorial] Real build placement did not start.");
            return true;
        }

        StartCoroutine(RunTutorialFinalizePlacementAndGhostBuild(def, env));
        return true;
    }

    private IEnumerator RunTutorialFinalizePlacementAndGhostBuild(Building def, EnvironmentControl env)
    {
        // Let the real placement flow start first.
        yield return null;

        // Skip the placement confirmation step by finalizing it immediately.
        BuildingPlacementManager.Instance?.FinalizePlacement();

        // Let the placed construction object spawn.
        yield return null;
        yield return null;

        BuildingConstruction construction = FindConstructionFor(def, env);
        if (construction == null)
        {
            Debug.LogWarning("[BuildingTutorial] Could not find spawned BuildingConstruction after finalized placement.");
            CompleteTutorial();
            yield break;
        }

        if (PlayerConstructionManager.Instance != null)
        {
            yield return PlayerConstructionManager.Instance.StartCoroutine(
                PlayerConstructionManager.Instance.TutorialGhostCompleteConstruction(construction)
            );
        }
        else
        {
            while (construction != null && construction.IsActive && construction.TurnsLeft > 0)
            {
                if (TurnSystem.Instance != null)
                {
                    yield return TurnSystem.Instance.StartCoroutine(
                        TurnSystem.Instance.RunGhostPhaseAdvance(() =>
                        {
                            if (construction != null)
                                construction.AdvanceOneTurn();
                        })
                    );
                }
                else
                {
                    if (construction != null)
                        construction.AdvanceOneTurn();

                    yield return null;
                }
            }

            if (construction != null)
            {
                GameObject finalGO = construction.CompleteAndSpawnFinal();
                if (finalGO != null)
                {
                    BuildingInstance tag = finalGO.GetComponent<BuildingInstance>()
                        ?? finalGO.GetComponentInChildren<BuildingInstance>(true);

                    if (tag != null)
                        PlayerBuildingManager.Instance?.Register(tag);
                }

                Destroy(construction.gameObject);
            }
        }

        CompleteTutorial();
    }

    private BuildingConstruction FindConstructionFor(Building def, EnvironmentControl env)
    {
        BuildingConstruction[] all = FindObjectsOfType<BuildingConstruction>(true);

        BuildingConstruction best = null;
        float bestScore = float.MaxValue;

        Vector3 targetPos = env != null ? env.transform.position : Vector3.zero;
        Transform targetParent = env != null ? env.transform.parent : null;

        for (int i = 0; i < all.Length; i++)
        {
            BuildingConstruction bc = all[i];
            if (bc == null || !bc.IsActive)
                continue;

            if (def != null)
            {
                if (bc.Definition != null)
                {
                    bool sameDef = bc.Definition == def || bc.Definition.buildingID == def.buildingID;
                    if (!sameDef)
                        continue;
                }
            }

            float score = 0f;

            if (env != null)
            {
                score += Vector3.Distance(bc.transform.position, targetPos);

                if (targetParent != null && bc.transform.parent != targetParent)
                    score += 1000f;
            }

            if (score < bestScore)
            {
                bestScore = score;
                best = bc;
            }
        }

        return best;
    }

    public void SkipTutorial()
    {
        FinishTutorial(markComplete: true);
    }

    private void CompleteTutorial()
    {
        FinishTutorial(markComplete: true);
    }

    private void FinishTutorial(bool markComplete)
    {
        _running = false;

        if (markComplete)
            _completedThisGame = true;

        if (_activeCatalogItem != null &&
            _activeCatalogItem.TutorialBuildOverride == (System.Func<BuildingCatalogItem, bool>)HandleTutorialCatalogBuildRequested)
        {
            _activeCatalogItem.TutorialBuildOverride = null;
        }

        _activeCatalogItem = null;

        UnbindTutorialHooks();

        SetAllOverlaysOff();
        SetContinueButtonVisible(false);
        SetBlockingMode(false);
        SetRootVisible(false);
        ShowMessagePanel(false);

        if (_cameraLockedByTutorial && cameraControl != null)
        {
            cameraControl.PopInputLock();
            _cameraLockedByTutorial = false;
        }

        TileInteraction.SetSelectionEnabled(false);
        tileInteraction?.EnableSelectionAfter(0.01f);

        BeginNextTutorialOrResume();
    }

    private void BeginNextTutorialOrResume()
    {
        if (buildingTutorialPart2 != null)
        {
            BuildingControl built = FindBuiltBuildingNear(_buildTargetPosition, _buildTargetParent);
            buildingTutorialPart2.SetTargetBuilding(built);

            if (buildingTutorialPart2.ShouldRunTutorial())
            {
                buildingTutorialPart2.BeginTutorial();
                return;
            }
        }

        if (resumeTurnTimerWhenFinished)
            TurnSystem.Instance?.ResumeTurnTimer();
    }

    public void ResetTutorialForNewGame()
    {
        _running = false;
        _completedThisGame = false;
        _step = TutorialStep.SelectStarterBuilding;

        if (_activeCatalogItem != null &&
            _activeCatalogItem.TutorialBuildOverride == (System.Func<BuildingCatalogItem, bool>)HandleTutorialCatalogBuildRequested)
        {
            _activeCatalogItem.TutorialBuildOverride = null;
        }

        _activeCatalogItem = null;

        UnbindTutorialHooks();

        SetAllOverlaysOff();
        SetContinueButtonVisible(false);
        SetBlockingMode(false);
        SetRootVisible(false);
        ShowMessagePanel(false);

        if (_cameraLockedByTutorial && cameraControl != null)
        {
            cameraControl.PopInputLock();
            _cameraLockedByTutorial = false;
        }

        _starterBuilding = null;
        _starterStatus = null;
        _starterInstance = null;
        _restoredEnvironment = null;
    }

    private void ResolveStarterBuilding()
    {
        BuildingInstance[] allInstances = FindObjectsOfType<BuildingInstance>(true);
        _starterInstance = allInstances.FirstOrDefault(x => x != null && x.isStarter);
        _starterBuilding = _starterInstance != null
            ? _starterInstance.GetComponent<BuildingControl>()
            : null;
        _starterStatus = _starterBuilding != null
            ? _starterBuilding.GetComponent<BuildingStatus>()
            : null;
    }

    private EnvironmentControl FindRestoredEnvironmentNear(Vector3 pos, Transform parent)
    {
        EnvironmentControl[] envs = FindObjectsOfType<EnvironmentControl>(true);

        EnvironmentControl best = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < envs.Length; i++)
        {
            EnvironmentControl env = envs[i];
            if (env == null)
                continue;

            if (parent != null && env.transform.parent != parent)
                continue;

            float dist = Vector3.Distance(env.transform.position, pos);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = env;
            }
        }

        return best;
    }

    private bool IsStarterBuildingPanelShowing()
    {
        return buildingPanel != null &&
               buildingPanel.IsShowing &&
               buildingPanel.CurrentBuilding == _starterBuilding;
    }

    private bool IsStarterDestroyedPanelShowing()
    {
        return buildingDestroyedPanel != null &&
               buildingDestroyedPanel.IsShowing &&
               buildingDestroyedPanel.CurrentBuilding == _starterBuilding;
    }

    private bool IsRestoredTilePanelShowing()
    {
        return discoveredTilePanel != null &&
               discoveredTilePanel.IsShowing &&
               discoveredTilePanel.CurrentEnvironment == _restoredEnvironment;
    }

    private void BindTutorialHooks()
    {
        if (buildingPanel != null)
            buildingPanel.TutorialDestroyOverride = HandleTutorialDestroyRequested;

        if (buildingDestroyedPanel != null)
            buildingDestroyedPanel.TutorialClearOverride = HandleTutorialClearRequested;

        if (discoveredTilePanel != null)
            discoveredTilePanel.TutorialBuildOverride = HandleTutorialTileBuildRequested;
    }

    private void UnbindTutorialHooks()
    {
        if (buildingPanel != null &&
            buildingPanel.TutorialDestroyOverride == (System.Func<BuildingControl, bool>)HandleTutorialDestroyRequested)
        {
            buildingPanel.TutorialDestroyOverride = null;
        }

        if (buildingDestroyedPanel != null &&
            buildingDestroyedPanel.TutorialClearOverride == (System.Func<BuildingControl, bool>)HandleTutorialClearRequested)
        {
            buildingDestroyedPanel.TutorialClearOverride = null;
        }

        if (discoveredTilePanel != null &&
            discoveredTilePanel.TutorialBuildOverride == (System.Func<EnvironmentControl, bool>)HandleTutorialTileBuildRequested)
        {
            discoveredTilePanel.TutorialBuildOverride = null;
        }
    }

    private void BindButtons()
    {
        if (skipButton != null)
        {
            skipButton.onClick.RemoveListener(OnSkipPressed);
            skipButton.onClick.AddListener(OnSkipPressed);
        }

        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(OnContinuePressed);
            continueButton.onClick.AddListener(OnContinuePressed);
        }
    }

    private void SetMessage(string value)
    {
        if (messageText != null)
            messageText.text = value;

        ShowMessagePanel(true);
    }

    private void ShowMessagePanel(bool visible)
    {
        if (messagePanel != null)
            messagePanel.SetActive(visible);
    }

    private void SetRootVisible(bool visible)
    {
        if (root != null)
            root.SetActive(visible);
    }

    private void SetBlockingMode(bool blocking)
    {
        if (rootCanvasGroup == null)
            return;

        rootCanvasGroup.blocksRaycasts = blocking;
        rootCanvasGroup.interactable = blocking;
    }

    private void SetAllOverlaysOff()
    {
        SetOverlayVisible(darkOverlayWithHole, false);
        SetOverlayVisible(darkOverlayWithHole2, false);
        SetOverlayVisible(darkOverlayWithHole3, false);
        SetOverlayVisible(darkOverlayWithHole4, false);
        SetOverlayVisible(darkOverlayWithHole5, false);
        SetOverlayVisible(darkOverlayWithHole6, false);
    }

    private void SetOverlayVisible(GameObject overlay, bool visible)
    {
        if (overlay != null)
            overlay.SetActive(visible);
    }

    private void SetContinueButtonVisible(bool visible)
    {
        if (continueButton != null)
            continueButton.gameObject.SetActive(visible);

        if (visible)
            SetBlockingMode(true);
        else
            SetBlockingMode(false);
    }

    private void SetSkipButtonVisible(bool visible)
    {
        if (skipButton != null)
            skipButton.gameObject.SetActive(visible);
    }
}