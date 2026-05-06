using UnityEngine;

public partial class StageThemeApplier : MonoBehaviour
{
    [ContextMenu("Apply Current Stage (Play Mode)")]
    public void ApplyCurrentStage()
    {
        if (!Application.isPlaying) return;
        if (!themeLibrary || !levelManager || !playerLevel) return;

        var stage = levelManager.GetStageForLevel(playerLevel.currentLevel);
        var theme = themeLibrary.Get(stage);

        ApplyTheme(theme);
        ApplySeasonSpritesForCurrentStage();
        ApplyTurnPhaseSprites(theme);
    }

    public void ApplyTheme(StageTheme theme)
    {
        if (!theme) return;

        ApplyThemeVisuals(theme);
        ApplyThemeLayouts(theme);
        ReplaceXPTracker(theme);

        // Rebind immediately after the tracker is replaced
        BindXPTrackerPlayer();
    }
}