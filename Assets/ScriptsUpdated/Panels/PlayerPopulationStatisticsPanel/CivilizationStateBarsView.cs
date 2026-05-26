using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class CivilizationStateBarsView : MonoBehaviour
{
    [Header("Bars Root (max height)")]
    public RectTransform barsRoot;

    [Header("Bars")]
    public RectTransform barHappiness;
    public RectTransform barOverallHealth;
    public RectTransform barDiversity;
    public RectTransform barIntegration;
    public RectTransform barOrder;
    public RectTransform barDiscovery;
    public RectTransform barKnowledge;
    public RectTransform barFaith;

    [Header("Marker Containers (ride the bars)")]
    public RectTransform markerHappinessContainer;
    public RectTransform markerOverallHealthContainer;
    public RectTransform markerDiversityContainer;
    public RectTransform markerIntegrationContainer;
    public RectTransform markerOrderContainer;
    public RectTransform markerDiscoveryContainer;
    public RectTransform markerKnowledgeContainer;
    public RectTransform markerFaithContainer;

    [Header("Marker Labels (optional)")]
    public TMP_Text markerHappinessText;
    public TMP_Text markerOverallHealthText;
    public TMP_Text markerDiversityText;
    public TMP_Text markerIntegrationText;
    public TMP_Text markerOrderText;
    public TMP_Text markerDiscoveryText;
    public TMP_Text markerKnowledgeText;
    public TMP_Text markerFaithText;

    [Header("Marker Layout")]
    [Tooltip("Offset above the bar top (px).")]
    public float markerYOffset = 8f;
    [Tooltip("Padding so markers never overflow the root.")]
    public float clampPadding = 4f;

    [Header("Genetic Diversity Sampling")]
    [Tooltip("Max individuals sampled per refresh. Higher = more accurate, slightly more CPU.")]
    public int diversitySampleSize = 20;

    private CivilizationStateManager civ;
    private PlayersPopulationManager pop;
    private readonly List<string> _geneBuffer = new();

    private void Awake()
    {
        civ = CivilizationStateManager.Instance;
        pop = PlayersPopulationManager.Instance;
    }

    public void RefreshNow()
    {
        if (!isActiveAndEnabled || !barsRoot) return;

        if (!civ) civ = CivilizationStateManager.Instance;
        if (!pop) pop = PlayersPopulationManager.Instance;

        float happiness     = civ ? Mathf.Clamp01(civ.happiness01)   : 0f;
        float diversity     = ComputeGeneticDiversity01();
        float integration   = civ ? Mathf.Clamp01(civ.integration01) : 0f;
        float overallHealth = ComputeOverallHealth01();
        float order         = civ ? Mathf.Clamp01(civ.order01)       : 0f;
        float discovery     = civ ? Mathf.Clamp01(civ.discovery01)   : 0f;
        float knowledge     = civ ? Mathf.Clamp01(civ.knowledge01)   : 0f;
        float faith         = civ ? Mathf.Clamp01(civ.faith01)       : 0f;

        if (civ) civ.SetDiversity01(diversity);

        Canvas.ForceUpdateCanvases();

        SetBarAndMarker01(barHappiness,     markerHappinessContainer,     happiness,     markerHappinessText);
        SetBarAndMarker01(barOverallHealth, markerOverallHealthContainer, overallHealth, markerOverallHealthText);
        SetBarAndMarker01(barDiversity,     markerDiversityContainer,     diversity,     markerDiversityText);
        SetBarAndMarker01(barIntegration,   markerIntegrationContainer,   integration,   markerIntegrationText);
        SetBarAndMarker01(barOrder,         markerOrderContainer,         order,         markerOrderText);
        SetBarAndMarker01(barDiscovery,     markerDiscoveryContainer,     discovery,     markerDiscoveryText);
        SetBarAndMarker01(barKnowledge,     markerKnowledgeContainer,     knowledge,     markerKnowledgeText);
        SetBarAndMarker01(barFaith,         markerFaithContainer,         faith,         markerFaithText);
    }

    private float ComputeOverallHealth01()
    {
        if (!pop) return 0f;

        var list = pop.AllPopulations;
        int sumCount = 0;
        float sumHealth = 0f;

        for (int i = 0; i < list.Count; i++)
        {
            var g = list[i];
            if (g == null || g.count <= 0) continue;
            sumCount  += g.count;
            sumHealth += g.averageHealth * g.count;
        }
        if (sumCount <= 0) return 0f;
        return Mathf.Clamp01(sumHealth / sumCount);
    }

    // Genetic diversity: average pairwise Hamming dissimilarity across a random sample.
    // 0 = completely inbred (all genes identical), 1 = maximum diversity.
    private float ComputeGeneticDiversity01()
    {
        var famSim = PlayerFamilySimulationManager.Instance;
        if (famSim == null) return civ ? Mathf.Clamp01(civ.diversity01) : 0f;

        var individuals = famSim.GetIndividuals();

        _geneBuffer.Clear();
        for (int i = 0; i < individuals.Count; i++)
        {
            var ind = individuals[i];
            if (ind == null || !ind.IsAlive || string.IsNullOrEmpty(ind.LineageId))
                continue;
            _geneBuffer.Add(ind.LineageId);
        }

        int n = _geneBuffer.Count;
        if (n < 2) return 0f;

        // Sample up to diversitySampleSize genes; compare all pairs within the sample.
        int sampleN = Mathf.Min(Mathf.Max(2, diversitySampleSize), n);
        // Reservoir sample: pick sampleN from the buffer without allocating a new list.
        // Swap chosen indices to the front so we only look at [0..sampleN).
        var rng = new System.Random();
        for (int i = 0; i < sampleN; i++)
        {
            int j = i + rng.Next(n - i);
            (_geneBuffer[i], _geneBuffer[j]) = (_geneBuffer[j], _geneBuffer[i]);
        }

        double sumDissimilarity = 0.0;
        int pairCount = 0;
        for (int a = 0; a < sampleN - 1; a++)
        {
            for (int b = a + 1; b < sampleN; b++)
            {
                sumDissimilarity += 1.0 - LineageUtils.HammingSimilarity(_geneBuffer[a], _geneBuffer[b]);
                pairCount++;
            }
        }

        if (pairCount == 0) return 0f;
        return Mathf.Clamp01((float)(sumDissimilarity / pairCount));
    }

    private void SetBarAndMarker01(RectTransform bar, RectTransform marker, float ratio01, TMP_Text label)
    {
        if (!bar || !barsRoot) return;

        float rootH = Mathf.Max(0f, barsRoot.rect.height);

        // Ensure marker rect is current
        float markerH = 0f;
        if (marker)
        {
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(marker);
            markerH = marker.rect.height;
        }

        // Desired vs allowed (so marker stays inside)
        float desiredH   = Mathf.Clamp01(ratio01) * rootH;
        float allowedMax = Mathf.Max(0f, rootH - (markerH + markerYOffset + clampPadding));
        float finalH     = Mathf.Min(desiredH, allowedMax);

        ApplyBarHeight(bar, finalH);

        if (marker)
        {
            if (marker.parent != bar) marker.SetParent(bar, false);

            marker.localScale = Vector3.one;
            marker.anchorMin  = marker.anchorMax = new Vector2(0.5f, 1f);
            marker.pivot      = new Vector2(0.5f, 0f);

            float spaceAboveBar = Mathf.Max(0f, rootH - bar.rect.height - clampPadding);
            float maxOffset     = Mathf.Max(0f, spaceAboveBar - markerH);
            float offY          = Mathf.Clamp(markerYOffset, 0f, maxOffset);

            marker.anchoredPosition = new Vector2(0f, offY);
            if (!marker.gameObject.activeSelf) marker.gameObject.SetActive(true);
        }

        if (label) label.text = $"{Mathf.RoundToInt(Mathf.Clamp01(ratio01) * 100f)}%";
    }

    private void ApplyBarHeight(RectTransform bar, float height)
    {
        bar.anchorMin = new Vector2(bar.anchorMin.x, 0f);
        bar.anchorMax = new Vector2(bar.anchorMax.x, 0f);
        bar.pivot     = new Vector2(bar.pivot.x,     0f);

        var size = bar.sizeDelta;
        size.y = height;
        bar.sizeDelta = size;

        bar.anchoredPosition = new Vector2(bar.anchoredPosition.x, 0f);
    }

    private void OnRectTransformDimensionsChange()
    {
        if (!isActiveAndEnabled) return;
        if (!barsRoot) return;
        RefreshNow();
    }
}
