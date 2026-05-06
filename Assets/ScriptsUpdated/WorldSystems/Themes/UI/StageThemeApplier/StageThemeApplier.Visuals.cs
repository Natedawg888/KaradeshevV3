using UnityEngine;
using UnityEngine.UI;
using TMPro;

public partial class StageThemeApplier
{
    // -------- helpers (shared) --------
    private static void SetSimpleSprite(Image img, Sprite sprite)
    { if (!img) return; img.type = Image.Type.Simple; img.sprite = sprite; }

    private static void SetSpriteKeepType(Image img, Sprite sprite)
    { if (!img) return; img.sprite = sprite; }

    private static void ApplyTmpTextTheme(TMP_Text text, StageTheme theme)
    { if (!text || theme == null) return; text.font = theme.tmpFont; text.color = theme.fontColor; }

    // -------- main visuals driver --------
    private void ApplyThemeVisuals(StageTheme theme)
    {
        // banners
        if (topBanner)    SetSimpleSprite(topBanner, theme.topBannerSprite);
        if (bottomBanner) SetSimpleSprite(bottomBanner, theme.bottomBannerSprite);

        // borders & title cards
        if (theme.horizontalBorderSprite != null)
            foreach (var img in horizontalBorders) if (img) SetSimpleSprite(img, theme.horizontalBorderSprite);

        if (theme.squareBorderSprite != null)
            foreach (var img in squareBorders) if (img) SetSimpleSprite(img, theme.squareBorderSprite);

        if (theme.titleCardSprite != null)
            foreach (var img in titleCards) if (img) SetSimpleSprite(img, theme.titleCardSprite);

        // TMP styling
        if (tmpTexts != null)
            foreach (var t in tmpTexts) if (t) ApplyTmpTextTheme(t, theme);

        // population icon
        if (populationIconTarget && theme.populationIcon)
            SetSimpleSprite(populationIconTarget, theme.populationIcon);

        // panel sprites
        if (theme.panelSprite != null)
            foreach (var img in panelImages) if (img) SetSimpleSprite(img, theme.panelSprite);

        // randomise / inventory buttons
        if (randomiseButtonImage && theme.randomiseBtnSprite)
            SetSpriteKeepType(randomiseButtonImage, theme.randomiseBtnSprite);

        if (inventoryButtonImage && theme.inventoryBtnSprite)
            SetSpriteKeepType(inventoryButtonImage, theme.inventoryBtnSprite);

        // misc buttons/text
        if (theme.miscButtonSprite != null)
            foreach (var img in miscButtonImages) if (img) SetSpriteKeepType(img, theme.miscButtonSprite);

        if (theme.overrideMiscButtonTextColor && miscButtonTexts != null)
            foreach (var t in miscButtonTexts) if (t) t.color = theme.miscButtonTextColor;

        // info buttons
        if (theme.infoButtonSprite != null && infoButtonImages != null)
            foreach (var img in infoButtonImages) if (img) SetSpriteKeepType(img, theme.infoButtonSprite);

        // inventory grid columns
        if (theme.overrideInventoryGridColumns && inventoryGrid != null)
        {
            inventoryGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            inventoryGrid.constraintCount = Mathf.Max(1, theme.inventoryGridColumns);
        }

        // inventory item prefab swap
        if (Application.isPlaying && inventoryPanelControl && theme.inventoryItemPrefab)
            inventoryPanelControl.ApplyInventoryItemPrefab(theme.inventoryItemPrefab, repopulate: true);

        // population pies
        if (malePieImage && theme.malePieSprite)     SetSpriteKeepType(malePieImage, theme.malePieSprite);
        if (femalePieImage && theme.femalePieSprite) SetSpriteKeepType(femalePieImage, theme.femalePieSprite);

        // bars
        if (theme.barSprite != null && barImages != null)
            foreach (var img in barImages) if (img) SetSpriteKeepType(img, theme.barSprite);

        // age icons
        if (theme.childrenIcon && childrenImages != null)
            foreach (var img in childrenImages) if (img) SetSpriteKeepType(img, theme.childrenIcon);

        if (theme.teensIcon && teensImages != null)
            foreach (var img in teensImages) if (img) SetSpriteKeepType(img, theme.teensIcon);

        if (theme.adultsIcon && adultsImages != null)
            foreach (var img in adultsImages) if (img) SetSpriteKeepType(img, theme.adultsIcon);

        if (theme.eldersIcon && eldersImages != null)
            foreach (var img in eldersImages) if (img) SetSpriteKeepType(img, theme.eldersIcon);

        // name edit/cancel sprites
        if (theme.nameEditButtonSprite && nameEditButtonImages != null)
            foreach (var img in nameEditButtonImages) if (img) SetSpriteKeepType(img, theme.nameEditButtonSprite);

        if (theme.nameCancelButtonSprite && nameCancelButtonImages != null)
            foreach (var img in nameCancelButtonImages) if (img) SetSpriteKeepType(img, theme.nameCancelButtonSprite);

        // survey/collected prefabs
        if (Application.isPlaying && surveyPanelControl && theme.resourceEntryPrefab)
            surveyPanelControl.ApplyResourceEntryPrefab(theme.resourceEntryPrefab, repopulateIfOpen: true);

        if (Application.isPlaying && collectedGoodsPanelControl && theme.collectedItemEntryPrefab)
            collectedGoodsPanelControl.ApplyCollectedItemPrefab(theme.collectedItemEntryPrefab, repopulateIfOpen: true);

        // UI lines (no material instancing)
        if (uiLineRenderers != null)
            foreach (var lr in uiLineRenderers) if (lr) lr.color = theme.lineColor;

        // discovered panel border
        if (discoveredPanelBorderImage && theme.discoveredPanelBorderSprite)
            SetSpriteKeepType(discoveredPanelBorderImage, theme.discoveredPanelBorderSprite);

        // world line renderers colors only
        if (worldLineRenderers != null)
            foreach (var lr in worldLineRenderers) if (lr) { lr.startColor = theme.lineColor; lr.endColor = theme.lineColor; }

        // avatar list / avatar item prefab
        if (Application.isPlaying && profilePanelControl && theme.stageAvatars != null && theme.stageAvatars.Count > 0)
        {
            profilePanelControl.SetAvailableAvatars(
                newList: theme.stageAvatars,
                repopulate: true,
                preserveSelectionByName: true,
                fallbackName: theme.fallbackAvatarName
            );
        }
        if (Application.isPlaying && profilePanelControl && theme.avatarItemPrefab)
            profilePanelControl.ApplyAvatarItemPrefab(theme.avatarItemPrefab, repopulate: true, shuffle: false);

        // phases (icons/fill) get wired in ApplyTurnPhaseSprites(theme)
    }

    // -------- season & phase visuals --------
    private void ApplySeasonSpritesForCurrentStage()
    {
        if (!themeLibrary || !levelManager || !playerLevel || seasonManager == null) return;

        var theme = themeLibrary.Get(levelManager.GetStageForLevel(playerLevel.currentLevel));
        if (!theme) return;

        SeasonDefinition season = seasonManager.CurrentSeason;

        Sprite icon = GetSeasonIconForTheme(theme, season);
        Sprite fill = GetSeasonFillForTheme(theme, season);

        if (seasonIconTarget && icon) SetSimpleSprite(seasonIconTarget, icon);
        if (seasonFillTarget && fill) SetSpriteKeepType(seasonFillTarget, fill);
    }

    private Sprite GetSeasonIconForTheme(StageTheme theme, SeasonDefinition season)
    {
        if (!theme) return null;
        if (season == null) return theme.springIcon;

        switch (season.visualType)
        {
            case SeasonVisualType.Spring:
                return theme.springIcon;

            case SeasonVisualType.Summer:
            case SeasonVisualType.Dry:
            case SeasonVisualType.Heatwave:
                return theme.summerIcon;

            case SeasonVisualType.Autumn:
            case SeasonVisualType.Wet:
            case SeasonVisualType.Rainy:
            case SeasonVisualType.Monsoon:
            case SeasonVisualType.Storm:
                return theme.autumnIcon ? theme.autumnIcon : theme.springIcon;

            case SeasonVisualType.Winter:
            case SeasonVisualType.Frozen:
            case SeasonVisualType.ColdSnap:
                return theme.winterIcon;

            default:
                return theme.springIcon;
        }
    }

    private Sprite GetSeasonFillForTheme(StageTheme theme, SeasonDefinition season)
    {
        if (!theme) return null;
        if (season == null) return theme.springFill;

        switch (season.visualType)
        {
            case SeasonVisualType.Spring:
                return theme.springFill;

            case SeasonVisualType.Summer:
            case SeasonVisualType.Dry:
            case SeasonVisualType.Heatwave:
                return theme.summerFill;

            case SeasonVisualType.Autumn:
            case SeasonVisualType.Wet:
            case SeasonVisualType.Rainy:
            case SeasonVisualType.Monsoon:
            case SeasonVisualType.Storm:
                return theme.autumnFill ? theme.autumnFill : theme.springFill;

            case SeasonVisualType.Winter:
            case SeasonVisualType.Frozen:
            case SeasonVisualType.ColdSnap:
                return theme.winterFill;

            default:
                return theme.springFill;
        }
    }

    private void ApplyTurnPhaseSprites(StageTheme theme)
    {
        if (!theme) return;

        if (turnSystem)
        {
            turnSystem.daySprite   = theme.phaseDayIcon;
            turnSystem.duskSprite  = theme.phaseDuskIcon;
            turnSystem.nightSprite = theme.phaseNightIcon;
            turnSystem.dawnSprite  = theme.phaseDawnIcon;

            if (phaseIconTarget) turnSystem.phaseImage = phaseIconTarget;
            if (phaseFillTarget) turnSystem.phaseFillImage = phaseFillTarget;

            if (Application.isPlaying)
                turnSystem.UpdatePhaseImage(turnSystem.currentPhase);
        }

        var currentIcon = !turnSystem ? null : turnSystem.currentPhase switch
        {
            DayPhase.Day   => theme.phaseDayIcon,
            DayPhase.Dusk  => theme.phaseDuskIcon,
            DayPhase.Night => theme.phaseNightIcon,
            DayPhase.Dawn  => theme.phaseDawnIcon,
            _ => null
        };

        if (phaseIconTarget && currentIcon) SetSimpleSprite(phaseIconTarget, currentIcon);
        if (phaseFillTarget && theme.phaseFill) SetSpriteKeepType(phaseFillTarget, theme.phaseFill);
    }
}
