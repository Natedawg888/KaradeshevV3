#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Lives only in the Editor. Safe to use OnValidate for preview.
[ExecuteAlways]
public class StageThemeApplierPreview : MonoBehaviour
{
    [Tooltip("Runtime applier this preview will drive")]
    public StageThemeApplier applier;

    [Header("Editor Preview")]
    public StageTheme previewTheme;
    public SeasonVisualType previewSeason = SeasonVisualType.Spring;
    public DayPhase previewPhase = DayPhase.Day;
    public bool livePreview = false;

    private StageTheme _lastTheme;
    private SeasonVisualType _lastSeason;
    private DayPhase _lastPhase;

    private void Reset()
    {
        if (!applier) applier = GetComponent<StageThemeApplier>();
    }

    private void OnValidate()
    {
        if (!livePreview) return;
        if (!applier) applier = GetComponent<StageThemeApplier>();
        if (!applier || !previewTheme) return;
        if (_lastTheme == previewTheme && _lastSeason == previewSeason && _lastPhase == previewPhase) return;

        applier.ApplyTheme(previewTheme);
        ApplySeasonSprites_Editor(previewTheme, previewSeason);
        ApplyTurnPhaseSprites_Editor(previewTheme, previewPhase);
        ApplyPopulationPieSprites_Editor(previewTheme);
        ApplyAvatarGrid_Editor(previewTheme);

        _lastTheme = previewTheme;
        _lastSeason = previewSeason;
        _lastPhase = previewPhase;

        EditorUtility.SetDirty(applier);
    }

    private void ApplyAvatarGrid_Editor(StageTheme theme)
    {
        if (!applier || !applier.profilePanelControl) return;

        if (theme.stageAvatars != null && theme.stageAvatars.Count > 0)
        {
            applier.profilePanelControl.SetAvailableAvatars(
                newList: theme.stageAvatars,
                repopulate: true,
                preserveSelectionByName: true,
                fallbackName: theme.fallbackAvatarName
            );
        }

        if (theme.avatarItemPrefab)
        {
            applier.profilePanelControl.ApplyAvatarItemPrefab(
                newPrefab: theme.avatarItemPrefab,
                repopulate: true,
                shuffle: false
            );
        }

        if (applier.profilePanelControl.avatarGridParent)
            EditorUtility.SetDirty(applier.profilePanelControl.avatarGridParent);
        if (applier.profilePanelControl)
            EditorUtility.SetDirty(applier.profilePanelControl);
    }

    public void ApplyPreviewNow()
    {
        bool prev = livePreview;
        livePreview = true;
        OnValidate();
        livePreview = prev;
    }

    public void CycleTheme(int delta)
    {
        if (!applier) applier = GetComponent<StageThemeApplier>();
        if (!applier || !applier.themeLibrary || applier.themeLibrary.themes == null || applier.themeLibrary.themes.Count == 0)
            return;

        var list = applier.themeLibrary.themes;
        int idx = Mathf.Max(0, list.IndexOf(previewTheme));
        idx = (idx + delta) % list.Count;
        if (idx < 0) idx += list.Count;

        previewTheme = list[idx];
        ApplyPreviewNow();
    }

    private void ApplySeasonSprites_Editor(StageTheme theme, SeasonVisualType season)
    {
        if (!applier) return;

        Sprite icon = null;
        Sprite fill = null;

        switch (season)
        {
            case SeasonVisualType.Spring:
                icon = theme.springIcon;
                fill = theme.springFill;
                break;

            case SeasonVisualType.Summer:
                icon = theme.summerIcon;
                fill = theme.summerFill;
                break;

            case SeasonVisualType.Autumn:
                icon = theme.autumnIcon;
                fill = theme.autumnFill;
                break;

            case SeasonVisualType.Winter:
            case SeasonVisualType.Frozen:
                icon = theme.winterIcon;
                fill = theme.winterFill;
                break;

            case SeasonVisualType.Wet:
            case SeasonVisualType.Rainy:
            case SeasonVisualType.Monsoon:
            case SeasonVisualType.Storm:
                icon = theme.autumnIcon != null ? theme.autumnIcon : theme.springIcon;
                fill = theme.autumnFill != null ? theme.autumnFill : theme.springFill;
                break;

            case SeasonVisualType.Dry:
            case SeasonVisualType.Heatwave:
                icon = theme.summerIcon;
                fill = theme.summerFill;
                break;

            case SeasonVisualType.ColdSnap:
                icon = theme.winterIcon;
                fill = theme.winterFill;
                break;

            default:
                icon = theme.springIcon;
                fill = theme.springFill;
                break;
        }

        if (applier.seasonIconTarget && icon) applier.seasonIconTarget.sprite = icon;
        if (applier.seasonFillTarget && fill) applier.seasonFillTarget.sprite = fill;

        if (applier.seasonIconTarget) EditorUtility.SetDirty(applier.seasonIconTarget);
        if (applier.seasonFillTarget) EditorUtility.SetDirty(applier.seasonFillTarget);
    }

    private void ApplyTurnPhaseSprites_Editor(StageTheme theme, DayPhase phase)
    {
        if (!applier) return;

        Sprite icon = phase switch
        {
            DayPhase.Day => theme.phaseDayIcon,
            DayPhase.Dusk => theme.phaseDuskIcon,
            DayPhase.Night => theme.phaseNightIcon,
            DayPhase.Dawn => theme.phaseDawnIcon,
            _ => null
        };

        if (applier.phaseIconTarget && icon) applier.phaseIconTarget.sprite = icon;
        if (applier.phaseFillTarget && theme.phaseFill) applier.phaseFillTarget.sprite = theme.phaseFill;

        if (applier.phaseIconTarget) EditorUtility.SetDirty(applier.phaseIconTarget);
        if (applier.phaseFillTarget) EditorUtility.SetDirty(applier.phaseFillTarget);
    }

    private void ApplyPopulationPieSprites_Editor(StageTheme theme)
    {
        if (!applier) return;

        if (applier.malePieImage && theme.malePieSprite)
            applier.malePieImage.sprite = theme.malePieSprite;

        if (applier.femalePieImage && theme.femalePieSprite)
            applier.femalePieImage.sprite = theme.femalePieSprite;

        if (applier.malePieImage) EditorUtility.SetDirty(applier.malePieImage);
        if (applier.femalePieImage) EditorUtility.SetDirty(applier.femalePieImage);
    }
}
#endif