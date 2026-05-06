using UnityEngine;
using UnityEngine.UI;
using TMPro;

public partial class StageThemeApplier
{
    private void ApplyThemeLayouts(StageTheme theme)
    {
        ApplyAvatarScrollViewLayout(theme);
        ApplyInventoryScrollViewLayout(theme);
        ApplyInventoryGridSpacing(theme);
        ApplyInventoryTextLayout(theme);
        ApplyPopulationTextLayout(theme);
        ApplyUndiscoveryTextLayout(theme);
        ApplyUndiscoveryText2Layout(theme);
        ApplyUndiscoveryPanelLayout(theme);
        ApplyInfoObjectLayout(theme);
        ApplyDiscoveredBorderLayout(theme);
        ApplyDiscoveredPanelChildLayout(theme);
        ApplyDiscoveredPanelChild2Layout(theme);
        ApplyDiscoveredText1Layout(theme);
        ApplySlidersLayout(theme);
        ApplySurveyInfoPanelLayout(theme);
        ApplySurveyInfoObjectLayout(theme);
        ApplyGatheringInfoPanelLayout(theme);
        ApplyGatheringInfoObjectLayout(theme);
        ApplySurveyPanelLayout(theme);
        ApplySurveyObjectLayout(theme);
        ApplyCollectedGoodsPanelLayout(theme);
        ApplyCollectedGoodsObjectLayout(theme);
        ApplyCollectedGoods2ObjectLayout(theme);
        ApplyCollectedGoodsText1Layout(theme);
    }

    private void ApplyAvatarScrollViewLayout(StageTheme theme)
    {
        if (!avatarScrollViewRect || theme == null) return;
        if (theme.overrideAvatarScrollPos)
            avatarScrollViewRect.anchoredPosition = theme.avatarScrollAnchoredPos;
        if (theme.overrideAvatarScrollHeight)
        {
            var sz = avatarScrollViewRect.sizeDelta;
            sz.y = theme.avatarScrollHeight;
            avatarScrollViewRect.sizeDelta = sz;
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(avatarScrollViewRect);
    }

    private void ApplyInventoryTextLayout(StageTheme theme)
    {
        foreach (var txt in inventoryText1)
        {
            if (!txt) continue;
            if (theme.overrideInvText1Position) txt.rectTransform.anchoredPosition = theme.invText1AnchoredPos;
            if (theme.overrideInvText1RectSize) txt.rectTransform.sizeDelta = theme.invText1RectSize;
            if (theme.overrideInvText1FontSize) txt.fontSize = theme.invText1FontSize;
        }

        foreach (var txt in inventoryText2)
        {
            if (!txt) continue;
            if (theme.overrideInvText2Position) txt.rectTransform.anchoredPosition = theme.invText2AnchoredPos;
            if (theme.overrideInvText2RectSize) txt.rectTransform.sizeDelta = theme.invText2RectSize;
            if (theme.overrideInvText2FontSize) txt.fontSize = theme.invText2FontSize;
        }
    }

    private void ApplyInventoryScrollViewLayout(StageTheme theme)
    {
        if (!inventoryScrollViewRect || theme == null) return;
        if (theme.overrideInventoryScrollPos)  inventoryScrollViewRect.anchoredPosition = theme.inventoryScrollAnchoredPos;
        if (theme.overrideInventoryScrollSize) inventoryScrollViewRect.sizeDelta = theme.inventoryScrollSize;
        LayoutRebuilder.ForceRebuildLayoutImmediate(inventoryScrollViewRect);
    }

    private void ApplyInventoryGridSpacing(StageTheme theme)
    {
        if (!inventoryGrid || theme == null || !theme.overrideInventoryGridSpacing) return;
        inventoryGrid.spacing = theme.inventoryGridSpacing;
        var rt = inventoryGrid.transform as RectTransform;
        if (rt) LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
    }

    private void ApplyUndiscoveryTextLayout(StageTheme theme)
    {
        if (undiscoveryText1 == null || theme == null) return;
        foreach (var txt in undiscoveryText1)
        {
            if (!txt) continue;
            if (theme.overrideUndiscText1Position) txt.rectTransform.anchoredPosition = theme.undiscText1AnchoredPos;
            if (theme.overrideUndiscText1RectSize) txt.rectTransform.sizeDelta = theme.undiscText1RectSize;
            if (theme.overrideUndiscText1FontSize) txt.fontSize = theme.undiscText1FontSize;
            if (theme.overrideUndiscText1Color)    txt.color = theme.undiscText1Color;
        }
    }

    private void ApplyUndiscoveryText2Layout(StageTheme theme)
    {
        if (undiscoveryText2 == null || theme == null) return;
        foreach (var txt in undiscoveryText2)
        {
            if (!txt) continue;
            if (theme.overrideUndiscText2Position) txt.rectTransform.anchoredPosition = theme.undiscText2AnchoredPos;
            if (theme.overrideUndiscText2RectSize) txt.rectTransform.sizeDelta = theme.undiscText2RectSize;
            if (theme.overrideUndiscText2FontSize) txt.fontSize = theme.undiscText2FontSize;
            if (theme.overrideUndiscText2Color)    txt.color = theme.undiscText2Color;
        }
    }

    private void ApplyUndiscoveryPanelLayout(StageTheme theme)
    {
        if (!undiscoveryPanelRect || theme == null) return;
        if (theme.overrideUndiscPanelSize) undiscoveryPanelRect.sizeDelta = theme.undiscPanelSize;
        LayoutRebuilder.ForceRebuildLayoutImmediate(undiscoveryPanelRect);
    }

    private void ApplyInfoObjectLayout(StageTheme theme)
    {
        if (!infoObjectRect || theme == null) return;
        if (theme.overrideInfoObjectPosition) infoObjectRect.anchoredPosition = theme.infoObjectAnchoredPos;
        LayoutRebuilder.ForceRebuildLayoutImmediate(infoObjectRect);
    }

    private void ApplyDiscoveredBorderLayout(StageTheme theme)
    {
        if (discoveredPanelBorderImage)
            ApplyBorderRT(discoveredPanelBorderImage.rectTransform, theme);
    }

    private void ApplyBorderRT(RectTransform rt, StageTheme theme)
    {
        if (!rt || theme == null) return;
        if (theme.overrideDiscoveredBorderSize)
            rt.sizeDelta = theme.discoveredBorderSize;
        LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
    }

    private void ApplyDiscoveredPanelLayout(StageTheme theme)
    {
        if (!discoveredPanelRect || theme == null) return;
        if (theme.overrideDiscoveredPanelPosition)
            discoveredPanelRect.anchoredPosition = theme.discoveredPanelAnchoredPos;
        LayoutRebuilder.ForceRebuildLayoutImmediate(discoveredPanelRect);
    }

    private void ApplyDiscoveredPanelChildLayout(StageTheme theme)
    {
        if (!theme || !discoveredPanelChildRect) return;
        if (theme.overrideDiscoveredChildPosition)
            discoveredPanelChildRect.anchoredPosition = theme.discoveredChildAnchoredPos;
        LayoutRebuilder.ForceRebuildLayoutImmediate(discoveredPanelChildRect);
    }

    private void ApplyDiscoveredPanelChild2Layout(StageTheme theme)
    {
        if (!theme || !discoveredPanelChild2Rect) return;
        if (theme.overrideDiscoveredChild2Position)
            discoveredPanelChild2Rect.anchoredPosition = theme.discoveredChild2AnchoredPos;
        LayoutRebuilder.ForceRebuildLayoutImmediate(discoveredPanelChild2Rect);
    }

    private void ApplyDiscoveredText1Layout(StageTheme theme)
    {
        if (discoveredText1 == null || theme == null) return;
        foreach (var txt in discoveredText1)
        {
            if (!txt) continue;
            if (theme.overrideDiscText1Position)  txt.rectTransform.anchoredPosition = theme.discText1AnchoredPos;
            if (theme.overrideDiscText1RectSize)  txt.rectTransform.sizeDelta = theme.discText1RectSize;
            if (theme.overrideDiscText1FontSize)  txt.fontSize = theme.discText1FontSize;
            if (theme.overrideDiscText1Color)     txt.color = theme.discText1Color;
        }
    }

    private void ApplySlidersLayout(StageTheme theme)
    {
        if (theme == null || sliderRects == null) return;
        foreach (var rt in sliderRects)
        {
            if (!rt) continue;
            if (theme.overrideSlidersPosition) rt.anchoredPosition = theme.slidersAnchoredPos;
            if (theme.overrideSlidersSize)     rt.sizeDelta       = theme.slidersSize;
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }
    }

    private void ApplySurveyInfoPanelLayout(StageTheme theme)
    {
        if (!surveyInfoPanelRect || !theme) return;
        if (theme.overrideSurveyInfoPanelSize)
            surveyInfoPanelRect.sizeDelta = theme.surveyInfoPanelSize;
        LayoutRebuilder.ForceRebuildLayoutImmediate(surveyInfoPanelRect);
    }

    private void ApplySurveyInfoObjectLayout(StageTheme theme)
    {
        if (!surveyInfoObjectRect || !theme) return;
        if (theme.overrideSurveyInfoObjectPosition)
            surveyInfoObjectRect.anchoredPosition = theme.surveyInfoObjectAnchoredPos;
        LayoutRebuilder.ForceRebuildLayoutImmediate(surveyInfoObjectRect);
    }

    private void ApplyGatheringInfoPanelLayout(StageTheme theme)
    {
        if (!gatheringInfoPanelRect || !theme) return;
        if (theme.overrideGatheringInfoPanelSize)
            gatheringInfoPanelRect.sizeDelta = theme.gatheringInfoPanelSize;
        LayoutRebuilder.ForceRebuildLayoutImmediate(gatheringInfoPanelRect);
    }

    private void ApplyGatheringInfoObjectLayout(StageTheme theme)
    {
        if (!gatheringInfoObjectRect || !theme) return;
        if (theme.overrideGatheringInfoObjectPosition)
            gatheringInfoObjectRect.anchoredPosition = theme.gatheringInfoObjectAnchoredPos;
        LayoutRebuilder.ForceRebuildLayoutImmediate(gatheringInfoObjectRect);
    }

    private void ApplySurveyPanelLayout(StageTheme theme)
    {
        if (!surveyPanelRect || !theme) return;
        if (theme.overrideSurveyPanelSize)
            surveyPanelRect.sizeDelta = theme.surveyPanelSize;
        LayoutRebuilder.ForceRebuildLayoutImmediate(surveyPanelRect);
    }

    private void ApplySurveyObjectLayout(StageTheme theme)
    {
        if (!surveyObjectRect || !theme) return;
        if (theme.overrideSurveyObjectPosition)
            surveyObjectRect.anchoredPosition = theme.surveyObjectAnchoredPos;
        LayoutRebuilder.ForceRebuildLayoutImmediate(surveyObjectRect);
    }

    private void ApplyCollectedGoodsPanelLayout(StageTheme theme)
    {
        if (!collectedGoodsPanelRect || !theme) return;
        if (theme.overrideCollectGoodsSize)
            collectedGoodsPanelRect.sizeDelta = theme.collectGoodsPanelSize;
        LayoutRebuilder.ForceRebuildLayoutImmediate(collectedGoodsPanelRect);
    }

    private void ApplyCollectedGoodsObjectLayout(StageTheme theme)
    {
        if (!collectedGoodsObjectRect || !theme) return;
        if (theme.overrideCollectGoodsObjectPosition)
            collectedGoodsObjectRect.anchoredPosition = theme.collectGoodsObjectAnchoredPos;
        LayoutRebuilder.ForceRebuildLayoutImmediate(collectedGoodsObjectRect);
    }

    private void ApplyCollectedGoods2ObjectLayout(StageTheme theme)
    {
        if (!collectedGoods2ObjectRect || !theme) return;
        if (theme.overrideCollectGoods2ObjectPosition)
            collectedGoods2ObjectRect.anchoredPosition = theme.collectGoods2ObjectAnchoredPos;
        LayoutRebuilder.ForceRebuildLayoutImmediate(collectedGoods2ObjectRect);
    }

    private void ApplyCollectedGoodsText1Layout(StageTheme theme)
    {
        if (collectedGoodsText1 == null || !theme) return;
        foreach (var txt in collectedGoodsText1)
        {
            if (!txt) continue;
            if (theme.overrideCollectGoodsText1Position)
                txt.rectTransform.anchoredPosition = theme.collectGoodsText1AnchoredPos;
            if (theme.overrideCollectGoodsText1RectSize)
                txt.rectTransform.sizeDelta = theme.collectGoodsText1RectSize;
            if (theme.overrideCollectGoodsText1FontSize)
                txt.fontSize = theme.collectGoodsText1FontSize;
            if (theme.overrideCollectGoodsText1Color)
                txt.color = theme.collectGoodsText1Color;
        }
    }

    private void ApplyPopulationTextLayout(StageTheme theme)
    {
        if (populationText1 == null || theme == null) return;
        foreach (var txt in populationText1)
        {
            if (!txt) continue;
            if (theme.overridePopText1Position)  txt.rectTransform.anchoredPosition = theme.popText1AnchoredPos;
            if (theme.overridePopText1RectSize)  txt.rectTransform.sizeDelta = theme.popText1RectSize;
            if (theme.overridePopText1FontSize)  txt.fontSize = theme.popText1FontSize;
        }
    }
}
