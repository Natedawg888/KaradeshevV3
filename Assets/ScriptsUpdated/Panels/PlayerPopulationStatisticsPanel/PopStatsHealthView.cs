using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;

public class PopStatsHealthView : PopStatsSubviewBase
{
    [Header("Health Sliders (0..1)")]
    public Slider childSlider;
    public Slider teenSlider;
    public Slider adultSlider;
    public Slider elderSlider;

    [Header("Optional Labels")]
    public TMP_Text childText;
    public TMP_Text teenText;
    public TMP_Text adultText;
    public TMP_Text elderText;

    private void Awake()
    {
        Setup(childSlider);
        Setup(teenSlider);
        Setup(adultSlider);
        Setup(elderSlider);
    }

    private static void Setup(Slider s)
    {
        if (!s) return;
        s.minValue = 0f;
        s.maxValue = 1f;
        s.wholeNumbers = false;
    }

    public override void RefreshNow()
    {
        if (!populationManager) return;

        SetGroup(childSlider, childText, AgeGroup.Child);
        SetGroup(teenSlider,  teenText,  AgeGroup.Teen);
        SetGroup(adultSlider, adultText, AgeGroup.Adult);
        SetGroup(elderSlider, elderText, AgeGroup.Elder);
    }

    private void SetGroup(Slider s, TMP_Text t, AgeGroup ag)
    {
        long current, max;
        ComputeHealthForGroup(ag, out current, out max);

        float ratio = (max > 0) ? Mathf.Clamp01((float)((double)current / (double)max)) : 0f;

        if (s) s.value = ratio;

        if (t)
        {
            string curStr = ShortNumberFormatter.Format(current, 1);
            string maxStr = ShortNumberFormatter.Format(max, 1);
            t.text = $"{curStr} / {maxStr}";
        }
    }

    private void ComputeHealthForGroup(AgeGroup ag, out long current, out long max)
    {
        current = 0L;
        max = 0L;

        var list = populationManager.AllPopulations;
        if (list == null || list.Count == 0) return;

        for (int i = 0; i < list.Count; i++)
        {
            var g = list[i];
            if (g == null || g.count <= 0) continue;
            if (g.ageGroup != ag) continue;

            long groupMax = (long)Mathf.Max(0, g.maxHealthPerIndividual) * g.count;
            max += groupMax;

            double groupCurrentD = Mathf.Clamp01(g.averageHealth) * g.maxHealthPerIndividual * (double)g.count;
            long groupCurrent = (long)System.Math.Round(System.Math.Min(groupCurrentD, groupMax));
            current += groupCurrent;
        }
    }
}