using UnityEngine;

public enum GameStartMode
{
    None,
    NewGame,
    LoadGame
}

[System.Serializable]
public class NewGameSetupData
{
    public string playerName;
    public string civilizationName;
    public string avatarName;
    public int selectedPresetID = -1;

    // NEW
    public bool includeTutorial = true;
}

public static class GameStartContext
{
    private static GameStartMode _requestedMode = GameStartMode.None;
    private static NewGameSetupData _pendingNewGameSetup;

    public static void SetRequestedMode(GameStartMode mode)
    {
        _requestedMode = mode;
    }

    public static GameStartMode GetRequestedMode()
    {
        return _requestedMode;
    }

    public static GameStartMode ConsumeRequestedMode(GameStartMode fallback)
    {
        GameStartMode result = _requestedMode == GameStartMode.None
            ? fallback
            : _requestedMode;

        _requestedMode = GameStartMode.None;
        return result;
    }

    public static void SetPendingNewGameSetup(NewGameSetupData data)
    {
        _pendingNewGameSetup = data;
    }

    public static NewGameSetupData GetPendingNewGameSetup()
    {
        return _pendingNewGameSetup;
    }

    public static void ClearPendingNewGameSetup()
    {
        _pendingNewGameSetup = null;
    }

    public static void ClearAll()
    {
        _requestedMode = GameStartMode.None;
        _pendingNewGameSetup = null;
    }
}