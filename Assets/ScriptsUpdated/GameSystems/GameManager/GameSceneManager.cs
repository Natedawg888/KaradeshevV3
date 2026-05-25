using System.Collections;
using UnityEngine;

public class GameSceneManager : MonoBehaviour
{
    [Header("Core References")]
    [SerializeField] private SaveSystem saveSystem;
    [SerializeField] private MapGenerator mapGenerator;
    [SerializeField] private MapTilePlacer mapTilePlacer;
    [SerializeField] private TileActivator tileActivator;
    [SerializeField] private StageThemeApplier stageThemeApplier;
    [SerializeField] private PersistentLoadingUI persistentLoadingUI;

    [Header("Loading UI")]
    [SerializeField] private GameObject loadScreenRoot;
    [SerializeField] private TimerUI loadTimerUI;

    [Header("Startup")]
    [SerializeField] private GameStartMode defaultStartMode = GameStartMode.NewGame;
    [SerializeField, Min(0)] private int framesBeforeStartupWork = 2;
    [SerializeField] private bool logStartupTimings = true;

    [Header("Looping Load Timer")]
    [SerializeField, Min(2)] private int loopingTimerTurns = 12;
    [SerializeField, Min(0.2f)] private float loopingTimerSecondsPerCycle = 1.2f;

    [Header("Autosave")]
    [SerializeField, Min(1)] private int autosaveEveryTurns = 10;
    [SerializeField] private bool autoSaveOnQuit = true;
    [SerializeField] private bool autoSaveOnApplicationPause = true;

    [Header("Debug")]
    [SerializeField] private bool autoBeginStartup = false;

    private bool _startupComplete;
    private bool _quitSaveInProgress;
    private bool _quitRequestedAfterSave;
    private bool _startupRequested;

    private int _turnsSinceLastAutosave;
    private Coroutine _loopingLoadTimerCoroutine;
    private Coroutine _startupRoutine;

    private void Awake()
    {
        if (saveSystem == null)
            saveSystem = SaveSystem.Instance;

        //Debug.Log($"[GameSceneManager] Awake: {Time.realtimeSinceStartup:0.000}s");
    }

    private IEnumerator Start()
    {
        if (!autoBeginStartup)
            yield break;

        BeginBootstrapStartup();
    }

    private void OnEnable()
    {
        TurnSystem.SubscribeToEndOfTurn(HandleEndTurn);
        Application.wantsToQuit += HandleWantsToQuit;
    }

    private void OnDisable()
    {
        StopLoopingLoadTimer(false);
        TurnSystem.UnsubscribeFromEndOfTurn(HandleEndTurn);
        Application.wantsToQuit -= HandleWantsToQuit;
    }

    private IEnumerator RunStartupRoutine()
    {
        //Debug.Log($"[GameSceneManager] Start entered: {Time.realtimeSinceStartup:0.000}s");

        if (saveSystem == null)
            saveSystem = SaveSystem.Instance;

        _startupComplete = false;
        _turnsSinceLastAutosave = 0;
        _quitSaveInProgress = false;
        _quitRequestedAfterSave = false;

        ShowLoadScreen(1, 1);
        yield return null;

        float startupStartTime = Time.realtimeSinceStartup;
        LogTiming("Startup", "BEGIN", 0f);

        float sceneSettlingStart = Time.realtimeSinceStartup;

        for (int i = 0; i < framesBeforeStartupWork; i++)
            yield return null;

        LogTiming("Startup", "FramesBeforeStartupWork", Time.realtimeSinceStartup - sceneSettlingStart);

        // Treat None as NewGame so a stale defaultStartMode=LoadGame never auto-loads
        // when the scene is entered without going through the main menu.
        GameStartMode startMode = GameStartContext.ConsumeRequestedMode(GameStartMode.NewGame);

        if (startMode == GameStartMode.LoadGame && SaveSystem.HasSave())
        {
            float loadStart = Time.realtimeSinceStartup;

            if (saveSystem != null)
            {
                saveSystem.OnLoadProgressChanged += HandleLoadProgress;

                ShowLoadScreen(saveSystem.LoadPhaseCount, saveSystem.LoadPhaseCount);
                yield return saveSystem.LoadWorldStateCoroutine();

                saveSystem.OnLoadProgressChanged -= HandleLoadProgress;
            }

            LogTiming("Startup", "LoadSavedGame", Time.realtimeSinceStartup - loadStart);
        }
        else
        {
            if (startMode == GameStartMode.LoadGame && !SaveSystem.HasSave()) {}
                //Debug.LogWarning("[GameSceneManager] Load requested but no save exists. Falling back to New Game.");

            // Delete any old save so HasSave() is false during generation and stale
            // data cannot be re-loaded if something triggers a load attempt mid-startup.
            SaveSystem.DeleteSave();

            float newGameStart = Time.realtimeSinceStartup;
            yield return RunNewGameGeneration();
            LogTiming("Startup", "RunNewGameGeneration_TOTAL", Time.realtimeSinceStartup - newGameStart);
        }

        float themeStart = Time.realtimeSinceStartup;

        StopLoopingLoadTimer(true);
        UpdateLoadTimer(1, 0);
        yield return null;

        if (stageThemeApplier != null)
            stageThemeApplier.RefreshAfterLoad();

        LogTiming("Startup", "StageThemeRefresh", Time.realtimeSinceStartup - themeStart);

        HideLoadScreen();
        ScoreManager.Instance?.OnGameStarted();
        _startupComplete = true;

        LogTiming("Startup", "COMPLETE", Time.realtimeSinceStartup - startupStartTime);
    }

    private IEnumerator RunNewGameGeneration()
    {
        float totalStart = Time.realtimeSinceStartup;

        TurnSystem.SetCurrentTurn(0);

        ShowLoadScreen(loopingTimerTurns, loopingTimerTurns);
        yield return null;

        if (mapGenerator != null)
        {
            float mapGenStart = Time.realtimeSinceStartup;

            StartLoopingLoadTimer();
            yield return StartCoroutine(mapGenerator.RegenerateCoroutine());
            StopLoopingLoadTimer(false);

            LogTiming("NewGame", "MapGeneration", Time.realtimeSinceStartup - mapGenStart);
        }
        else
        {
            //Debug.LogWarning("[GameSceneManager] MapGenerator reference is missing.");
        }

        yield return null;

        if (mapTilePlacer != null)
        {
            float tilePlacementStart = Time.realtimeSinceStartup;

            StartLoopingLoadTimer();
            mapTilePlacer.BeginPlacement();

            while (!MapTilePlacer.WorldReady)
                yield return null;

            StopLoopingLoadTimer(false);

            LogTiming("NewGame", "TilePlacement", Time.realtimeSinceStartup - tilePlacementStart);
        }
        else
        {
            //Debug.LogWarning("[GameSceneManager] MapTilePlacer reference is missing.");
        }

        yield return null;

        if (tileActivator != null)
        {
            float activationStart = Time.realtimeSinceStartup;
            bool finished = false;

            void HandleTilesActivated()
            {
                finished = true;
            }

            tileActivator.OnTilesActivated += HandleTilesActivated;

            // TileActivator already owns the timer UI during this phase.
            tileActivator.BeginActivation(loadTimerUI, false);

            while (!finished)
                yield return null;

            tileActivator.OnTilesActivated -= HandleTilesActivated;

            LogTiming("NewGame", "TileActivation", Time.realtimeSinceStartup - activationStart);
        }
        else
        {
            //Debug.LogWarning("[GameSceneManager] TileActivator reference is missing.");
        }

        StopLoopingLoadTimer(true);
        LogTiming("NewGame", "TOTAL", Time.realtimeSinceStartup - totalStart);
    }

    public void BeginExternalLoopingLoad()
    {
        ShowLoadScreen(loopingTimerTurns, loopingTimerTurns);
        StartLoopingLoadTimer();
    }

    public void EndExternalLoopingLoad(bool showEmpty, bool hideScreen)
    {
        StopLoopingLoadTimer(showEmpty);

        if (hideScreen)
            HideLoadScreen();
    }

    private void StartLoopingLoadTimer()
    {
        StopLoopingLoadTimer(false);

        if (loadTimerUI == null)
            return;

        loadTimerUI.gameObject.SetActive(true);
        _loopingLoadTimerCoroutine = StartCoroutine(AnimateLoopingLoadTimer());
    }

    private void StopLoopingLoadTimer(bool showEmpty)
    {
        if (_loopingLoadTimerCoroutine != null)
        {
            StopCoroutine(_loopingLoadTimerCoroutine);
            _loopingLoadTimerCoroutine = null;
        }

        if (loadTimerUI != null)
            loadTimerUI.SetState(loopingTimerTurns, showEmpty ? 0 : loopingTimerTurns);
    }

    private IEnumerator AnimateLoopingLoadTimer()
    {
        if (loadTimerUI == null)
            yield break;

        int maxTurns = Mathf.Max(2, loopingTimerTurns);
        float cycle = Mathf.Max(0.2f, loopingTimerSecondsPerCycle);
        float elapsed = 0f;

        while (true)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Repeat(elapsed, cycle) / cycle;
            int turnsLeft = Mathf.Clamp(Mathf.CeilToInt((1f - t) * maxTurns), 0, maxTurns);

            loadTimerUI.SetState(maxTurns, turnsLeft);
            yield return null;
        }
    }

    private void HandleLoadProgress(int totalPhases, int phasesRemaining)
    {
        ShowLoadScreen(totalPhases, phasesRemaining);
    }

    private void HandleEndTurn()
    {
        if (!_startupComplete)
            return;

        _turnsSinceLastAutosave++;

        if (_turnsSinceLastAutosave < Mathf.Max(1, autosaveEveryTurns))
            return;

        _turnsSinceLastAutosave = 0;
        SaveSystem.RequestTurnAutoSave();
    }

    private bool HandleWantsToQuit()
    {
        if (!_startupComplete || !autoSaveOnQuit)
            return true;

        if (_quitRequestedAfterSave)
            return true;

        if (_quitSaveInProgress)
            return false;

        if (saveSystem == null)
            saveSystem = SaveSystem.Instance;

        if (saveSystem == null)
            return true;

        _quitRequestedAfterSave = true;
        StartCoroutine(SaveThenQuitCoroutine());
        return false;
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus)
            return;

        if (!_startupComplete || !autoSaveOnApplicationPause)
            return;

        if (saveSystem == null)
            saveSystem = SaveSystem.Instance;

        CommitScoreToLeaderboard();
        SaveSystem.SaveCloseGameNow();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
#if UNITY_ANDROID || UNITY_IOS
        if (hasFocus)
            return;

        if (!_startupComplete || !autoSaveOnApplicationPause)
            return;

        if (saveSystem == null)
            saveSystem = SaveSystem.Instance;

        CommitScoreToLeaderboard();
        SaveSystem.SaveCloseGameNow();
#endif
    }

    private static void CommitScoreToLeaderboard()
    {
        if (ScoreManager.Instance == null)
            return;

        string playerName  = ProfilePanelControl.Instance != null ? ProfilePanelControl.Instance.PlayerName        : string.Empty;
        string civName     = ProfilePanelControl.Instance != null ? ProfilePanelControl.Instance.CivilizationName  : string.Empty;
        string avatarName  = ProfilePanelControl.Instance != null ? ProfilePanelControl.Instance.CurrentAvatarName : string.Empty;

        ScoreManager.Instance.CommitScoreToLeaderboard(playerName, civName, avatarName);
    }

    private void ShowLoadScreen(int maxTurns, int turnsLeft)
    {
        if (loadScreenRoot != null)
            loadScreenRoot.SetActive(true);

        UpdateLoadTimer(maxTurns, turnsLeft);
    }

    private void UpdateLoadTimer(int maxTurns, int turnsLeft)
    {
        if (loadTimerUI == null)
            return;

        loadTimerUI.gameObject.SetActive(true);
        loadTimerUI.SetState(Mathf.Max(1, maxTurns), Mathf.Clamp(turnsLeft, 0, Mathf.Max(1, maxTurns)));
    }

    private void HideLoadScreen()
    {
        if (loadScreenRoot != null)
            loadScreenRoot.SetActive(false);
    }

    private void LogTiming(string group, string label, float seconds)
    {
        if (!logStartupTimings)
            return;

        //Debug.Log($"[Timing][{group}] {label}: {seconds:0.000}s");
    }

    public void ConfigureWorldSetup(WorldSetupInstaller worldSetup)
    {
        if (worldSetup == null)
        {
            //Debug.LogError("[GameSceneManager] ConfigureWorldSetup received null installer.");
            return;
        }

        mapGenerator = worldSetup.MapGenerator;
        mapTilePlacer = worldSetup.MapTilePlacer;
        tileActivator = worldSetup.TileActivator;
    }

    public void ConfigureUISetup(UISetupInstaller uiSetup)
    {
        if (uiSetup == null)
        {
            //Debug.LogError("[GameSceneManager] ConfigureUISetup received null installer.");
            return;
        }

        stageThemeApplier = uiSetup.StageThemeApplier;
    }

    public void BeginBootstrapStartup()
    {
        if (_startupRequested)
            return;

        _startupRequested = true;
        _startupRoutine = StartCoroutine(RunStartupRoutine());
    }

    private IEnumerator SaveThenQuitCoroutine()
    {
        _quitSaveInProgress = true;

        if (saveSystem == null)
            saveSystem = SaveSystem.Instance;

        ShowLoadScreen(1, 1);
        SaveSystem.SaveCloseGameNow();

        while (saveSystem != null && saveSystem.IsSaving)
            yield return null;

        UpdateLoadTimer(1, 0);
        yield return null;

        _quitSaveInProgress = false;
        CommitScoreToLeaderboard();
        Application.Quit();
    }
}
