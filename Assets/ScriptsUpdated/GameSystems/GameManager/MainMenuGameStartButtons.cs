using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class MainMenuGameStartButtons : MonoBehaviour
{
#if UNITY_EDITOR
    [Header("Scene Reference")]
    [SerializeField] private SceneAsset gameplayScene;
#endif

    [SerializeField, HideInInspector] private string gameplayScenePath = "";

    [Header("Persistent Loading UI")]
    [SerializeField] private PersistentLoadingUI persistentLoadingUI;

    [Header("Buttons")]
    [SerializeField] private Button[] buttonsToDisable;

    [Header("New Game Setup")]
    [SerializeField] private MainMenuNewGameSetupPanel newGameSetupPanel;

    [Header("Timing")]
    [SerializeField, Min(0)] private int framesBeforeStartingLoad = 1;
    [SerializeField] private bool logSceneLoadTimings = true;

    private float _buttonPressTime;
    private bool _sceneLoadedCallbackFired;
    private bool _isLoading;

    private void Awake()
    {
        if (newGameSetupPanel != null)
            newGameSetupPanel.Initialize(HandleNewGameSetupConfirmed);
    }

    public void StartNewGame()
    {
        if (_isLoading)
            return;

        if (newGameSetupPanel != null)
        {
            newGameSetupPanel.Show();
            return;
        }

        // fallback if panel missing
        HandleNewGameSetupConfirmed(new NewGameSetupData());
    }

    private void HandleNewGameSetupConfirmed(NewGameSetupData data)
    {
        if (_isLoading)
            return;

        if (newGameSetupPanel != null)
            newGameSetupPanel.Hide();

        _buttonPressTime = Time.realtimeSinceStartup;
        LogTiming("ButtonPressed_NewGame", 0f);

        GameStartContext.SetRequestedMode(GameStartMode.NewGame);
        GameStartContext.SetPendingNewGameSetup(data);

        StartCoroutine(LoadGameplaySceneAsync());
    }

    public void LoadSavedGame()
    {
        if (_isLoading)
            return;

        _buttonPressTime = Time.realtimeSinceStartup;
        LogTiming("ButtonPressed_LoadGame", 0f);

        GameStartContext.SetRequestedMode(GameStartMode.LoadGame);
        GameStartContext.ClearPendingNewGameSetup();

        StartCoroutine(LoadGameplaySceneAsync());
    }

    private IEnumerator LoadGameplaySceneAsync()
    {
        _isLoading = true;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        _sceneLoadedCallbackFired = false;

        SetButtonsInteractable(false);

        if (persistentLoadingUI != null)
            persistentLoadingUI.StartLoop();

        LogTiming("LoadingUIShown", Time.realtimeSinceStartup - _buttonPressTime);

        if (string.IsNullOrEmpty(gameplayScenePath))
        {
            Debug.LogError("MainMenuGameStartButtons: No gameplay scene assigned.");
            if (persistentLoadingUI != null)
                persistentLoadingUI.HideImmediate();

            SetButtonsInteractable(true);
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            _isLoading = false;
            yield break;
        }

        for (int i = 0; i < framesBeforeStartingLoad; i++)
            yield return null;

        LogTiming("BeforeLoadSceneAsync", Time.realtimeSinceStartup - _buttonPressTime);

        AsyncOperation op = SceneManager.LoadSceneAsync(gameplayScenePath);
        if (op == null)
        {
            Debug.LogError($"Failed to load scene: {gameplayScenePath}");

            if (persistentLoadingUI != null)
                persistentLoadingUI.HideImmediate();

            SetButtonsInteractable(true);
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            _isLoading = false;
            yield break;
        }

        LogTiming("LoadSceneAsyncStarted", Time.realtimeSinceStartup - _buttonPressTime);

        op.allowSceneActivation = false;

        while (op.progress < 0.9f)
            yield return null;

        LogTiming("AsyncLoadReached90Percent", Time.realtimeSinceStartup - _buttonPressTime);

        op.allowSceneActivation = true;
        LogTiming("SceneActivationAllowed", Time.realtimeSinceStartup - _buttonPressTime);

        while (!_sceneLoadedCallbackFired)
            yield return null;

        LogTiming("SceneLoadedCallbackObserved", Time.realtimeSinceStartup - _buttonPressTime);

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        _isLoading = false;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _sceneLoadedCallbackFired = true;
        LogTiming($"sceneLoaded: {scene.name}", Time.realtimeSinceStartup - _buttonPressTime);
    }

    private void SetButtonsInteractable(bool value)
    {
        if (buttonsToDisable == null)
            return;

        foreach (var button in buttonsToDisable)
        {
            if (button != null)
                button.interactable = value;
        }
    }

    private void LogTiming(string label, float seconds)
    {
        if (!logSceneLoadTimings)
            return;

        Debug.Log($"[SceneLoadTiming] {label}: {seconds:0.000}s");
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (gameplayScene != null)
            gameplayScenePath = AssetDatabase.GetAssetPath(gameplayScene);
        else
            gameplayScenePath = "";
    }
#endif
}