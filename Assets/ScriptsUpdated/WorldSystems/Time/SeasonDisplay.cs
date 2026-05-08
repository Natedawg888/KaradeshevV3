using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SeasonDisplay : MonoBehaviour
{
    [Header("UI")]
    public Image seasonIcon;
    public Image seasonProgressFill;

    private SeasonManager seasonManager;

    private void OnEnable()
    {
        seasonManager = SeasonManager.Instance;
        if (seasonManager == null)
        {
            seasonManager = FindObjectOfType<SeasonManager>();
            if (seasonManager == null)
            {
                //Debug.LogError("SeasonManager not found in scene. Please ensure one exists and is enabled.");
                enabled = false;
                return;
            }
        }

        EnsureProgressFillSetup();

        seasonManager.OnSeasonChanged += HandleSeasonChanged;
        TurnSystem.SubscribeToEndOfTurn(UpdateCountdown);

        HandleSeasonChanged(seasonManager.CurrentSeason);
        UpdateCountdown();
    }

    private void OnDisable()
    {
        if (seasonManager != null)
            seasonManager.OnSeasonChanged -= HandleSeasonChanged;

        TurnSystem.UnsubscribeFromEndOfTurn(UpdateCountdown);
    }

    private void HandleSeasonChanged(SeasonDefinition season)
    {
        if (seasonIcon != null)
            seasonIcon.sprite = season != null ? season.iconSprite : null;

        if (seasonProgressFill != null)
        {
            seasonProgressFill.sprite = season != null ? season.fillSprite : null;
            EnsureProgressFillSetup();
        }

        UpdateCountdown();
    }

    private void UpdateCountdown()
    {
        if (seasonManager == null || seasonProgressFill == null)
            return;

        seasonProgressFill.fillAmount = 1f - seasonManager.GetSeasonProgress();
    }

    private void EnsureProgressFillSetup()
    {
        if (seasonProgressFill == null)
            return;

        seasonProgressFill.type = Image.Type.Filled;
        seasonProgressFill.fillMethod = Image.FillMethod.Radial360;
        seasonProgressFill.fillOrigin = (int)Image.Origin360.Top;
        seasonProgressFill.fillClockwise = true;
        seasonProgressFill.preserveAspect = true;
    }
}
