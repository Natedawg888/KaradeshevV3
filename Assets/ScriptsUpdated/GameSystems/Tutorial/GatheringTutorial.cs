using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GatheringTutorial : MonoBehaviour
{
    private enum TutorialStep
    {
        OpenDiscoveredTilePanel,
        ClickSurveyButton,
        CloseSurveyPanel,
        ClickGatherButton,
        GhostGatherSequence,
        OpenCollectedGoodsPanel,
        ExplainCollectedGoodsPanel,
        CloseCollectedGoodsPanel,
        CloseDiscoveredTilePanel
    }

    [Header("UI")]
    [SerializeField] private GameObject root;
    [SerializeField] private CanvasGroup rootCanvasGroup;
    [SerializeField] private GameObject darkOverlayWithHole;
    [SerializeField] private GameObject darkOverlayWithHole2;
    [SerializeField] private GameObject darkOverlayWithHole3;
    [SerializeField] private GameObject messagePanel;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button skipButton;
    [SerializeField] private Button continueButton;

    [Header("References")]
    [SerializeField] private DiscoveredTilePanelControl discoveredTilePanel;
    [SerializeField] private SurveyPanelControl surveyPanel;
    [SerializeField] private CollectedGoodsPanelControl collectedGoodsPanel;

    [Header("Tutorial Survey Resources")]
    [SerializeField] private List<ResourceDefinition> tutorialSurveyResourcePool = new List<ResourceDefinition>();
    [SerializeField, Min(1)] private int minTutorialSurveyEntries = 2;
    [SerializeField, Min(1)] private int maxTutorialSurveyEntries = 4;
    [SerializeField, Min(1)] private int minTutorialSurveyAmount = 2;
    [SerializeField, Min(1)] private int maxTutorialSurveyAmount = 10;

    [Header("Tutorial Gather Resources")]
    [SerializeField] private List<ResourceDefinition> tutorialGatherResourcePool = new List<ResourceDefinition>();
    [SerializeField, Min(1)] private int minTutorialGatherEntries = 2;
    [SerializeField, Min(1)] private int maxTutorialGatherEntries = 4;
    [SerializeField, Min(1)] private int minTutorialGatherAmount = 2;
    [SerializeField, Min(1)] private int maxTutorialGatherAmount = 10;

    [Header("Settings")]
    [SerializeField] private bool resumeTurnTimerWhenFinished = true;

    private bool _running;
    private bool _completedThisGame;
    private TutorialStep _step = TutorialStep.OpenDiscoveredTilePanel;
    private EnvironmentControl _targetEnvironment;
    private bool _surveyWasOpenWhenStepStarted;
    private Coroutine _ghostGatherRoutine;

    public bool IsRunning => _running;
    public bool HasCompletedTutorial => _completedThisGame;

    [SerializeField] private InventoryTutorial inventoryTutorial;

    public bool ShouldRunTutorial()
    {
        return !_running && !_completedThisGame && _targetEnvironment != null;
    }

    private void Awake()
    {
        if (discoveredTilePanel == null)
            discoveredTilePanel = FindObjectOfType<DiscoveredTilePanelControl>(true);

        if (surveyPanel == null && discoveredTilePanel != null)
            surveyPanel = discoveredTilePanel.surveyPanel;

        if (collectedGoodsPanel == null)
            collectedGoodsPanel = FindObjectOfType<CollectedGoodsPanelControl>(true);

        BindButtons();
        SetRootVisible(false);
        SetBlockingMode(false);
        SetSurveyOverlayVisible(false);
        SetGatherOverlayVisible(false);
        SetCollectedOverlayVisible(false);
        SetContinueButtonVisible(false);
    }

    public void InstallRuntimeRefs(
    DiscoveredTilePanelControl newDiscoveredTilePanel = null,
    SurveyPanelControl newSurveyPanel = null,
    CollectedGoodsPanelControl newCollectedGoodsPanel = null,
    InventoryTutorial newInventoryTutorial = null)
    {
        UnbindTutorialHooks();

        if (newDiscoveredTilePanel != null)
            discoveredTilePanel = newDiscoveredTilePanel;
        else if (discoveredTilePanel == null)
            discoveredTilePanel = FindObjectOfType<DiscoveredTilePanelControl>(true);

        if (newSurveyPanel != null)
            surveyPanel = newSurveyPanel;
        else if (surveyPanel == null && discoveredTilePanel != null)
            surveyPanel = discoveredTilePanel.surveyPanel;

        if (newCollectedGoodsPanel != null)
            collectedGoodsPanel = newCollectedGoodsPanel;
        else if (collectedGoodsPanel == null)
            collectedGoodsPanel = FindObjectOfType<CollectedGoodsPanelControl>(true);

        if (newInventoryTutorial != null)
            inventoryTutorial = newInventoryTutorial;

        BindButtons();
    }

    public void SetTargetEnvironment(EnvironmentControl env)
    {
        _targetEnvironment = env;
    }

    public void BeginTutorial()
    {
        if (!ShouldRunTutorial())
        {
            if (resumeTurnTimerWhenFinished)
                TurnSystem.Instance?.ResumeTurnTimer();
            return;
        }

        if (discoveredTilePanel == null)
            discoveredTilePanel = FindObjectOfType<DiscoveredTilePanelControl>(true);

        if (surveyPanel == null && discoveredTilePanel != null)
            surveyPanel = discoveredTilePanel.surveyPanel;

        if (collectedGoodsPanel == null)
            collectedGoodsPanel = FindObjectOfType<CollectedGoodsPanelControl>(true);

        BindTutorialHooks();

        _running = true;
        _step = TutorialStep.OpenDiscoveredTilePanel;
        _surveyWasOpenWhenStepStarted = surveyPanel != null && surveyPanel.IsShowing;

        TurnSystem.Instance?.PauseTurnTimer();

        SetRootVisible(true);
        SetBlockingMode(false);
        SetSurveyOverlayVisible(false);
        SetGatherOverlayVisible(false);
        SetCollectedOverlayVisible(false);
        SetSkipButtonVisible(false);
        SetContinueButtonVisible(false);

        if (messagePanel != null)
            messagePanel.SetActive(true);

        SetMessage("Select the tile you discovered to open the discovered tile panel.");
    }

    private void SetBlockingMode(bool blocking)
    {
        if (rootCanvasGroup == null)
            return;

        rootCanvasGroup.blocksRaycasts = blocking;
        rootCanvasGroup.interactable = blocking;
    }

    private void Update()
    {
        if (!_running)
            return;

        switch (_step)
        {
            case TutorialStep.OpenDiscoveredTilePanel:
                {
                    if (IsCorrectDiscoveredTilePanelShowing())
                    {
                        _step = TutorialStep.ClickSurveyButton;
                        _surveyWasOpenWhenStepStarted = surveyPanel != null && surveyPanel.IsShowing;
                        SetSurveyOverlayVisible(true);
                        SetGatherOverlayVisible(false);
                        SetCollectedOverlayVisible(false);
                        SetContinueButtonVisible(false);
                        SetMessage("Click the survey button.");
                    }
                    break;
                }

            case TutorialStep.ClickSurveyButton:
                {
                    if (!_surveyWasOpenWhenStepStarted && surveyPanel != null && surveyPanel.IsShowing)
                    {
                        _step = TutorialStep.CloseSurveyPanel;
                        SetSurveyOverlayVisible(false);
                        SetGatherOverlayVisible(false);
                        SetCollectedOverlayVisible(false);
                        SetContinueButtonVisible(false);
                        SetMessage("This panel shows the resources on the tile. Close the survey panel.");
                    }
                    break;
                }

            case TutorialStep.CloseSurveyPanel:
                {
                    if (surveyPanel != null && !surveyPanel.IsShowing)
                    {
                        _step = TutorialStep.ClickGatherButton;
                        SetSurveyOverlayVisible(false);
                        SetGatherOverlayVisible(true);
                        SetCollectedOverlayVisible(false);
                        SetContinueButtonVisible(false);
                        SetMessage("Now click the gathering button.");
                    }
                    break;
                }

            case TutorialStep.OpenCollectedGoodsPanel:
                {
                    if (IsCorrectCollectedGoodsPanelShowing())
                    {
                        _step = TutorialStep.ExplainCollectedGoodsPanel;
                        SetSurveyOverlayVisible(false);
                        SetGatherOverlayVisible(false);
                        SetCollectedOverlayVisible(true);
                        SetContinueButtonVisible(true);
                        SetMessage("This panel shows the goods gathered from the tile. These buttons are used to collect Them. Press Continue");
                    }
                    break;
                }

            case TutorialStep.CloseCollectedGoodsPanel:
                {
                    if (collectedGoodsPanel != null && !collectedGoodsPanel.IsShowing)
                    {
                        _step = TutorialStep.CloseDiscoveredTilePanel;
                        SetSurveyOverlayVisible(false);
                        SetGatherOverlayVisible(false);
                        SetCollectedOverlayVisible(false);
                        SetContinueButtonVisible(false);

                        if (discoveredTilePanel != null && _targetEnvironment != null && !discoveredTilePanel.IsShowing)
                            discoveredTilePanel.Show(_targetEnvironment);

                        SetMessage("Now close the discovered tile panel.");
                    }
                    break;
                }

            case TutorialStep.CloseDiscoveredTilePanel:
                {
                    if (discoveredTilePanel != null && !discoveredTilePanel.IsShowing)
                        CompleteTutorial();
                    break;
                }
        }
    }

    private void OnSkipPressed()
    {
        SkipTutorial();
    }

    private void OnContinuePressed()
    {
        if (!_running)
            return;

        if (_step == TutorialStep.ExplainCollectedGoodsPanel)
        {
            _step = TutorialStep.CloseCollectedGoodsPanel;
            SetCollectedOverlayVisible(false);
            SetContinueButtonVisible(false);
            SetMessage("Close the collected goods panel.");
        }
    }

    private bool HandleTutorialSurveyRequested(EnvironmentControl env)
    {
        if (!_running || _step != TutorialStep.ClickSurveyButton || env == null)
            return false;

        if (_targetEnvironment != null && env != _targetEnvironment)
            return false;

        env.CompleteSurveyVisuals();

        if (surveyPanel != null)
            surveyPanel.ShowTutorialEntries(BuildRandomSurveyEntries());

        SetSkipButtonVisible(false);

        return true;
    }

    private bool HandleTutorialGatherRequested(EnvironmentControl env)
    {
        if (!_running || _step != TutorialStep.ClickGatherButton || env == null)
            return false;

        if (_targetEnvironment != null && env != _targetEnvironment)
            return false;

        _step = TutorialStep.GhostGatherSequence;
        SetSurveyOverlayVisible(false);
        SetGatherOverlayVisible(false);
        SetCollectedOverlayVisible(false);
        SetContinueButtonVisible(false);
        SetSkipButtonVisible(false);
        SetMessage("The tile will tick over until gathering completes.");

        env.BeginGatheringVisuals();

        if (discoveredTilePanel != null)
        {
            discoveredTilePanel.SuppressSelectionReenableOnHide = true;
            discoveredTilePanel.Hide();
        }

        TileInteraction.SetSelectionEnabled(false);

        if (_ghostGatherRoutine != null)
            StopCoroutine(_ghostGatherRoutine);

        _ghostGatherRoutine = StartCoroutine(RunTutorialGatherSequence(env));
        return true;
    }

    private IEnumerator RunTutorialGatherSequence(EnvironmentControl env)
    {
        if (env == null)
            yield break;

        while (env.gatheringTurnsLeft > 0)
        {
            bool finalTick = env.gatheringTurnsLeft <= 1;

            if (TurnSystem.Instance != null)
            {
                yield return TurnSystem.Instance.StartCoroutine(
                    TurnSystem.Instance.RunGhostPhaseAdvance(() =>
                    {
                        if (finalTick)
                            env.StorePendingLoot(BuildRandomGatherLoot());

                        env.AdvanceGatheringTurn();
                    })
                );
            }
            else
            {
                if (finalTick)
                    env.StorePendingLoot(BuildRandomGatherLoot());

                env.AdvanceGatheringTurn();
                yield return null;
            }
        }

        _step = TutorialStep.OpenCollectedGoodsPanel;
        TileInteraction.SetSelectionEnabled(false);
        TileInteraction.GetInstance()?.EnableSelectionAfter(0.01f);

        SetSurveyOverlayVisible(false);
        SetGatherOverlayVisible(false);
        SetCollectedOverlayVisible(false);
        SetContinueButtonVisible(false);
        SetMessage("Click the tile to open the collected goods panel.");
    }

    private List<SurveyPanelControl.TutorialSurveyEntry> BuildRandomSurveyEntries()
    {
        List<SurveyPanelControl.TutorialSurveyEntry> results = new List<SurveyPanelControl.TutorialSurveyEntry>();

        List<ResourceDefinition> defs = BuildUniqueRandomDefinitions(
            tutorialSurveyResourcePool,
            minTutorialSurveyEntries,
            maxTutorialSurveyEntries
        );

        for (int i = 0; i < defs.Count; i++)
        {
            results.Add(new SurveyPanelControl.TutorialSurveyEntry
            {
                definition = defs[i],
                amount = Random.Range(minTutorialSurveyAmount, maxTutorialSurveyAmount + 1)
            });
        }

        return results;
    }

    private List<(ResourceDefinition def, int amount)> BuildRandomGatherLoot()
    {
        List<(ResourceDefinition def, int amount)> results = new List<(ResourceDefinition def, int amount)>();

        List<ResourceDefinition> defs = BuildUniqueRandomDefinitions(
            tutorialGatherResourcePool,
            minTutorialGatherEntries,
            maxTutorialGatherEntries
        );

        for (int i = 0; i < defs.Count; i++)
        {
            results.Add((
                defs[i],
                Random.Range(minTutorialGatherAmount, maxTutorialGatherAmount + 1)
            ));
        }

        return results;
    }

    private List<ResourceDefinition> BuildUniqueRandomDefinitions(
        List<ResourceDefinition> pool,
        int minEntries,
        int maxEntries)
    {
        List<ResourceDefinition> candidates = new List<ResourceDefinition>();

        if (pool != null)
        {
            for (int i = 0; i < pool.Count; i++)
            {
                ResourceDefinition def = pool[i];
                if (def != null && !candidates.Contains(def))
                    candidates.Add(def);
            }
        }

        if (candidates.Count == 0)
        {
            Debug.LogWarning("[GatheringTutorial] Resource pool is empty.");
            return new List<ResourceDefinition>();
        }

        int targetCount = Random.Range(minEntries, maxEntries + 1);
        targetCount = Mathf.Clamp(targetCount, 1, candidates.Count);

        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            ResourceDefinition tmp = candidates[i];
            candidates[i] = candidates[swapIndex];
            candidates[swapIndex] = tmp;
        }

        List<ResourceDefinition> picked = new List<ResourceDefinition>();
        for (int i = 0; i < targetCount; i++)
            picked.Add(candidates[i]);

        return picked;
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

        if (_ghostGatherRoutine != null)
        {
            StopCoroutine(_ghostGatherRoutine);
            _ghostGatherRoutine = null;
        }

        UnbindTutorialHooks();

        if (collectedGoodsPanel != null && collectedGoodsPanel.IsShowing)
            collectedGoodsPanel.Hide();

        if (_targetEnvironment != null)
            _targetEnvironment.ResetTutorialEnvironmentState();

        SetSurveyOverlayVisible(false);
        SetGatherOverlayVisible(false);
        SetCollectedOverlayVisible(false);
        SetContinueButtonVisible(false);
        SetSkipButtonVisible(false);
        SetBlockingMode(false);
        SetRootVisible(false);

        BeginNextTutorialOrResume();
    }

    private void BeginNextTutorialOrResume()
{
    if (inventoryTutorial != null && inventoryTutorial.ShouldRunTutorial())
    {
        inventoryTutorial.BeginTutorial();
        return;
    }

    if (resumeTurnTimerWhenFinished)
        TurnSystem.Instance?.ResumeTurnTimer();
}

    public void ResetTutorialForNewGame()
    {
        _running = false;
        _completedThisGame = false;
        _step = TutorialStep.OpenDiscoveredTilePanel;
        _targetEnvironment = null;
        _surveyWasOpenWhenStepStarted = false;

        if (_ghostGatherRoutine != null)
        {
            StopCoroutine(_ghostGatherRoutine);
            _ghostGatherRoutine = null;
        }

        UnbindTutorialHooks();

        SetSurveyOverlayVisible(false);
        SetGatherOverlayVisible(false);
        SetCollectedOverlayVisible(false);
        SetContinueButtonVisible(false);
        SetSkipButtonVisible(false);
        SetBlockingMode(false);
        SetRootVisible(false);
    }

    private bool IsCorrectDiscoveredTilePanelShowing()
    {
        if (discoveredTilePanel == null || _targetEnvironment == null)
            return false;

        return discoveredTilePanel.IsShowing && discoveredTilePanel.CurrentEnvironment == _targetEnvironment;
    }

    private bool IsCorrectCollectedGoodsPanelShowing()
    {
        if (collectedGoodsPanel == null || _targetEnvironment == null)
            return false;

        return collectedGoodsPanel.IsShowing && collectedGoodsPanel.CurrentEnvironment == _targetEnvironment;
    }

    private void BindTutorialHooks()
    {
        if (discoveredTilePanel == null)
            return;

        discoveredTilePanel.TutorialSurveyOverride = HandleTutorialSurveyRequested;
        discoveredTilePanel.TutorialGatherOverride = HandleTutorialGatherRequested;
    }

    private void UnbindTutorialHooks()
    {
        if (discoveredTilePanel == null)
            return;

        if (discoveredTilePanel.TutorialSurveyOverride == (System.Func<EnvironmentControl, bool>)HandleTutorialSurveyRequested)
            discoveredTilePanel.TutorialSurveyOverride = null;

        if (discoveredTilePanel.TutorialGatherOverride == (System.Func<EnvironmentControl, bool>)HandleTutorialGatherRequested)
            discoveredTilePanel.TutorialGatherOverride = null;
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
    }

    private void SetRootVisible(bool visible)
    {
        if (root != null)
            root.SetActive(visible);
    }

    private void SetSurveyOverlayVisible(bool visible)
    {
        if (darkOverlayWithHole != null)
            darkOverlayWithHole.SetActive(visible);
    }

    private void SetGatherOverlayVisible(bool visible)
    {
        if (darkOverlayWithHole2 != null)
            darkOverlayWithHole2.SetActive(visible);
    }

    private void SetCollectedOverlayVisible(bool visible)
    {
        if (darkOverlayWithHole3 != null)
            darkOverlayWithHole3.SetActive(visible);
    }

    private void SetSkipButtonVisible(bool visible)
    {
        if (skipButton == null)
            return;

        skipButton.gameObject.SetActive(visible);
        skipButton.interactable = visible;

        if (visible)
            skipButton.transform.SetAsLastSibling();
        if (visible)
            SetBlockingMode(true);
        else
            SetBlockingMode(false);
    }

    private void SetContinueButtonVisible(bool visible)
    {
        if (continueButton == null)
            return;

        continueButton.gameObject.SetActive(visible);
        continueButton.interactable = visible;

        if (visible)
            continueButton.transform.SetAsLastSibling();

        // Match DiscoveryTutorial behaviour:
        // only make the tutorial root interactable when Continue is up.
        if (visible)
            SetBlockingMode(true);
        else
            SetBlockingMode(false);
    }
}