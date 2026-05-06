using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UI.Extensions;

public class PopStatsLineGraphView : PopStatsSubviewBase
{
    [Header("Line Graph")]
    public UILineRenderer uiLineRenderer;
    public RectTransform graphBoundRect;

    [Tooltip("Max points to draw across the graph width.")]
    public int turnsToTrack = 40;

    [Tooltip("Sample every N turns (old panel used 4).")]
    public int turnStride = 4;

    [Header("Labels/Markers")]
    public TMP_Text populationText;
    public TMP_Text dayText;
    public RectTransform populationMarkerObject; // moves on Y only
    public RectTransform dayMarkerObject;        // moves on X only

    // <-- add this
    private bool _layoutRetryQueued = false;

    private void Start()
    {
        // Seed once if empty (usually already seeded by PlayerPopulationStatistic.OnEnable)
        if (stats != null && (stats.History == null || stats.History.Count == 0))
        {
            stats.ForceSnapshot();
        }

        RefreshNow();
    }

    public override void RefreshNow()
    {
        if (!stats || !uiLineRenderer || !graphBoundRect || !gameObject.activeInHierarchy) return;

        var rect = graphBoundRect.rect;
        if (rect.width <= 0f || rect.height <= 0f)
        {
            if (!_layoutRetryQueued)
            {
                _layoutRetryQueued = true;
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(graphBoundRect);
                StartCoroutine(RetryNextFrame());
            }
            return;
        }
        _layoutRetryQueued = false;

        var history = stats.History;
        if (history == null || history.Count == 0)
        {
            uiLineRenderer.enabled = false;
            uiLineRenderer.Points = new Vector2[] { Vector2.zero, Vector2.zero };
            UpdateLabelsAndMarkers(null, Vector2.zero, 0);
            return;
        }

        int stride = Mathf.Max(1, turnStride);

        // keep original indices so we know which "turn" each sampled point represents
        var sampled = history
            .Select((s, idx) => (snap: s, idx))
            .Where(t => (t.idx % stride) == 0)
            .ToList();

        if (sampled.Count == 0) sampled.Add((history[^1], history.Count - 1));

        int count = Mathf.Min(turnsToTrack, sampled.Count);
        var slice = sampled.Skip(sampled.Count - count).ToArray();

        float maxY = (populationManager && populationManager.maxPopulation > 0)
            ? populationManager.maxPopulation
            : Mathf.Max(1, slice.Max(s => s.snap.total));

        float w = Mathf.Max(1f, rect.width);
        float h = Mathf.Max(1f, rect.height);

        int n = Mathf.Max(2, count);
        var pts = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            int src = Mathf.Clamp(i + (count - n), 0, count - 1);
            float t = (n <= 1) ? 0f : i / (float)(n - 1);
            float x = t * w;
            float y = Mathf.Clamp01(slice[src].snap.total / maxY) * h;
            pts[i] = new Vector2(x, y);
        }

        // day = last sampled index / stride  (=> 1 day == stride turns)
        int lastSampledIndex = slice[^1].idx;
        int day = lastSampledIndex / stride;

        var lrRT = uiLineRenderer.GetComponent<RectTransform>();
        if (lrRT)
        {
            lrRT.anchorMin = lrRT.anchorMax = new Vector2(0f, 0f);
            lrRT.pivot = new Vector2(0f, 0f);
            lrRT.sizeDelta = new Vector2(w, h);
            lrRT.anchoredPosition = Vector2.zero;
        }

        uiLineRenderer.enabled = true;
        uiLineRenderer.Points = pts;
        uiLineRenderer.SetVerticesDirty();
        uiLineRenderer.SetLayoutDirty();

        UpdateLabelsAndMarkers(pts, pts[n - 1], day);
    }

    public void PrepareForHide()
    {
        if (uiLineRenderer)
        {
            uiLineRenderer.enabled = false;
            uiLineRenderer.Points = new Vector2[] { Vector2.zero, Vector2.zero };
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        PrepareForHide(); // guard against assertions when hidden
    }

    private System.Collections.IEnumerator RetryNextFrame()
    {
        yield return null;
        RefreshNow();
    }

    private void UpdateLabelsAndMarkers(Vector2[] pts, Vector2 lastLocalPoint, int dayValue)
    {
        if (populationText)
        {
            int lastTotal = (stats != null && stats.History != null && stats.History.Count > 0)
                ? stats.History[^1].total
                : 0;
            populationText.text = lastTotal.ToString();
        }

        if (dayText)
        {
            dayText.text = $"{dayValue}";
        }

        bool hasPts = pts != null && pts.Length > 0;

        if (populationMarkerObject)
        {
            var prt = populationMarkerObject;
            prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0f, 0f);
            prt.anchoredPosition = new Vector2(prt.anchoredPosition.x, lastLocalPoint.y);
            prt.gameObject.SetActive(hasPts);
        }

        if (dayMarkerObject)
        {
            var drt = dayMarkerObject;
            drt.anchorMin = drt.anchorMax = drt.pivot = new Vector2(0f, 0f);
            drt.anchoredPosition = new Vector2(lastLocalPoint.x, drt.anchoredPosition.y);
            drt.gameObject.SetActive(hasPts);
        }
    }
}