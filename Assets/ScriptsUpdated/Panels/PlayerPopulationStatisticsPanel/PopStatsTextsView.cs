using TMPro;
using UnityEngine;
public class PopStatsTextsView : PopStatsSubviewBase
{
    [Header("Texts")]
    public TMP_Text currentPopulationText;
    public TMP_Text maxPopulationText;
    public TMP_Text availableText;
    public TMP_Text usedText;

    public override void RefreshNow()
    {
        if (!populationManager) return;

        int total = populationManager.GetTotalPopulation();
        int max   = populationManager.maxPopulation;
        int avail = populationManager.GetAvailableTaskPopulation();
        int used  = populationManager.GetUsedTaskPopulation();

        if (currentPopulationText) currentPopulationText.text = $"Current: {total}";
        if (maxPopulationText)     maxPopulationText.text     = $"Max: {max}";
        if (availableText)         availableText.text         = $"Available: {avail}";
        if (usedText)              usedText.text              = $"Used: {used}";
    }
}