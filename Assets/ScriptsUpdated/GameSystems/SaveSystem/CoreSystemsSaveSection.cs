using System.Collections;
using UnityEngine;

public sealed class CoreSystemsSaveSection : SaveSectionBase
{
    public override string Key => SaveSectionKeys.CoreSystems;

    public override IEnumerator CaptureInto(
        SaveSnapshot snapshot,
        SaveCaptureContext context,
        int objectsPerFrame)
    {
        snapshot.coreSystems = new CoreSystemsSectionSaveData
        {
            cameraPoseData = context.CameraControl != null ? context.CameraControl.SaveState() : null,
            turnData = TurnSystem.Instance != null ? TurnSystem.Instance.SaveState() : null,
            seasonData = SeasonManager.Instance != null ? SeasonManager.Instance.SaveState() : null,
            climateData = ClimateManager.Instance != null ? ClimateManager.Instance.SaveState() : null,

            // NEW
            weatherData = WeatherSystemsSaveLoad.SaveState(),

            playerLevelData = PlayerLevel.Instance != null ? PlayerLevel.Instance.SaveState() : null,
            playerProfileData = ProfilePanelControl.Instance != null ? ProfilePanelControl.Instance.SaveState() : null,
            civilizationStateData = CivilizationStateManager.Instance != null ? CivilizationStateManager.Instance.SaveState() : null,
            currentScore = ScoreManager.Instance != null ? ScoreManager.Instance.SaveState() : 0,
            gameId       = ScoreManager.Instance != null ? ScoreManager.Instance.GetGameId()  : string.Empty,

            musicMuted   = SoundPanelControl.Instance != null && SoundPanelControl.Instance.MusicMuted,
            masterVolume = AudioListener.volume,

            brightness = GraphicsPanelControl.Instance != null ? GraphicsPanelControl.Instance.CurrentBrightness : 0.5f
        };

        ClearDirty();
        yield break;
    }
}