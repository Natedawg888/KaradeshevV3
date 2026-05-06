using UnityEngine;

public partial class StageThemeApplier : MonoBehaviour
{
    public void InstallStageRefs(LevelManager newLevelManager, PlayerLevel newPlayerLevel, SeasonManager newSeasonManager)
    {
        if (playerLevel != null)
            playerLevel.OnLevelUp -= HandleLevelUp;

        if (seasonManager != null)
            seasonManager.OnSeasonChanged -= HandleSeasonChanged;

        levelManager = newLevelManager;
        playerLevel = newPlayerLevel;
        seasonManager = newSeasonManager;

        if (isActiveAndEnabled)
        {
            if (playerLevel != null)
                playerLevel.OnLevelUp += HandleLevelUp;

            if (seasonManager != null)
                seasonManager.OnSeasonChanged += HandleSeasonChanged;
        }

        // Immediate bind to whatever XP tracker currently exists
        BindXPTrackerPlayer();

        if (Application.isPlaying)
            ApplyCurrentStage();
    }

    public void InstallTurnSystem(TurnSystem newTurnSystem)
    {
        turnSystem = newTurnSystem;
    }

    private void BindXPTrackerPlayer()
    {
        if (playerLevel == null)
            return;

        XPCircleUI xpCircle = GetComponentInChildren<XPCircleUI>(true);

        if (xpCircle == null)
            xpCircle = FindFirstObjectByType<XPCircleUI>(FindObjectsInactive.Include);

        if (xpCircle != null)
            xpCircle.SetPlayer(playerLevel, true);
    }
}