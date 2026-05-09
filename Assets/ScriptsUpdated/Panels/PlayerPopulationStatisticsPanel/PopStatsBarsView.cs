using TMPro;
using UnityEngine;

public class PopStatsBarsView : PopStatsSubviewBase
{
    [Header("Bars (Age)")]
    public RectTransform ageBarsRoot;
    public RectTransform barChild;
    public RectTransform barTeen;
    public RectTransform barAdult;
    public RectTransform barElder;

    [Header("Marker Containers (will move with bars)")]
    public RectTransform markerChildContainer;
    public RectTransform markerTeenContainer;
    public RectTransform markerAdultContainer;
    public RectTransform markerElderContainer;

    [Header("Marker Labels (optional)")]
    public TMP_Text markerChildText;
    public TMP_Text markerTeenText;
    public TMP_Text markerAdultText;
    public TMP_Text markerElderText;

    [Header("Marker Layout")]
    [Tooltip("Offset above the bar top (in px).")]
    public float markerYOffset = 8f;
    [Tooltip("Padding inside the bounding rect so markers never overflow.")]
    public float clampPadding = 4f;

    public override void RefreshNow()
    {
        if (!ageBarsRoot || populationManager == null) return;

        Canvas.ForceUpdateCanvases(); // make sure sizes are current

        var groups = populationManager.AllPopulations;
        int cChild = 0, cTeen = 0, cAdult = 0, cElder = 0;
        for (int i = 0; i < groups.Count; i++)
        {
            var g = groups[i];
            if (g == null) continue;
            switch (g.ageGroup)
            {
                case AgeGroup.Child: cChild += g.count; break;
                case AgeGroup.Teen:  cTeen  += g.count; break;
                case AgeGroup.Adult: cAdult += g.count; break;
                case AgeGroup.Elder: cElder += g.count; break;
            }
        }
        int total = Mathf.Max(0, cChild + cTeen + cAdult + cElder);

        SetBarAndMarker(barChild, markerChildContainer, cChild, total);
        SetBarAndMarker(barTeen,  markerTeenContainer,  cTeen,  total);
        SetBarAndMarker(barAdult, markerAdultContainer, cAdult, total);
        SetBarAndMarker(barElder, markerElderContainer, cElder, total);

        if (markerChildText) markerChildText.text = cChild.ToString();
        if (markerTeenText)  markerTeenText.text  = cTeen.ToString();
        if (markerAdultText) markerAdultText.text = cAdult.ToString();
        if (markerElderText) markerElderText.text = cElder.ToString();
    }

    private void SetBarByCount(RectTransform bar, int count, int total)
    {
        if (!bar) return;

        float maxH = ageBarsRoot ? ageBarsRoot.rect.height : ((RectTransform)bar.parent).rect.height;
        float h = (total > 0) ? Mathf.Clamp01(count / (float)total) * maxH : 0f;

        ApplyBarHeight(bar, h);
    }

    private void SetBarAndMarker(RectTransform bar, RectTransform marker, int count, int total)
    {
        if (!bar) return;

        float rootH = ageBarsRoot.rect.height;

        // current marker height (force a layout pass so rect.height is valid)
        float markerH = 0f;
        if (marker)
        {
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(marker);
            markerH = marker.rect.height;
        }

        // Desired bar height by ratio
        float desiredH = (total > 0) ? Mathf.Clamp01(count / (float)total) * rootH : 0f;

        // CAP: ensure the marker can sit on top without leaving the root
        float allowedBarMax = Mathf.Max(0f, rootH - (markerH + markerYOffset + clampPadding));
        float finalBarH = Mathf.Min(desiredH, allowedBarMax);

        ApplyBarHeight(bar, finalBarH);

        // Place marker safely above the bar, still clamped
        if (marker)
        {
            // Make it ride the bar
            if (marker.parent != bar) marker.SetParent(bar, false);

            marker.localScale = Vector3.one;
            marker.anchorMin = marker.anchorMax = new Vector2(0.5f, 1f);
            marker.pivot     = new Vector2(0.5f, 0f);

            // Space between the bar top and the root top (in the same space as heights)
            float spaceAboveBar = Mathf.Max(0f, rootH - bar.rect.height - clampPadding);

            // Max vertical offset that still keeps marker inside root
            float maxOffset = Mathf.Max(0f, spaceAboveBar - markerH);

            float offY = Mathf.Clamp(markerYOffset, 0f, maxOffset);
            marker.anchoredPosition = new Vector2(0f, offY);

            if (!marker.gameObject.activeSelf) marker.gameObject.SetActive(true);
        }
    }

    private void ApplyBarHeight(RectTransform bar, float height)
    {
        if (!bar) return;

        bar.anchorMin = new Vector2(bar.anchorMin.x, 0f);
        bar.anchorMax = new Vector2(bar.anchorMax.x, 0f);
        bar.pivot     = new Vector2(bar.pivot.x,     0f);

        var size = bar.sizeDelta;
        size.y = height;
        bar.sizeDelta = size;

        bar.anchoredPosition = new Vector2(bar.anchoredPosition.x, 0f);
    }

    // Recompute if the layout/size changes (keeps everything inside bounds on resize)
    private void OnRectTransformDimensionsChange()
    {
        // Optional quick guards
        if (!isActiveAndEnabled) return;
        if (!ageBarsRoot) return;

        RefreshNow();
    }
}