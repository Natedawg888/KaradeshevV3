using System.Linq;
using TMPro;
using UnityEngine;

public class PopCivilisationNeedsView : PopStatsSubviewBase
{
    [Header("Bars Root")]
    public RectTransform barsRoot;     // parent rect that defines max height

    [Header("Bars (Needs)")]
    public RectTransform barHunger;
    public RectTransform barThirst;
    public RectTransform barHousing;

    [Header("Marker Containers (will ride the bars)")]
    public RectTransform markerHungerContainer;
    public RectTransform markerThirstContainer;
    public RectTransform markerHousingContainer;

        [Header("Marker Labels (optional)")]
    public TMP_Text markerHungerText;   // e.g. “72%”
    public TMP_Text markerThirstText;
    public TMP_Text markerHousingText;

    [Header("Marker Layout")]
    [Tooltip("Offset above the bar top (px).")]
    public float markerYOffset = 8f;
    [Tooltip("Padding inside the root so markers never overflow.")]
    public float clampPadding = 4f;

    [Header("Next-Cycle Needs (optional labels)")]
    public TMP_Text foodNextCycleText;    // total nutrition points next cycle
    public TMP_Text waterNextCycleText;   // total hydration points next cycle

    [Header("Housing (optional labels)")]
    public TMP_Text individualsHousingText;

    public override void RefreshNow()
    {
        if (!isActiveAndEnabled) return;
        if (!barsRoot || populationManager == null) return;

        var popMgr  = populationManager;
        var famMgr  = PlayerFamilySimulationManager.Instance;
        var bldMgr  = PlayerBuildingManager.Instance;
        var general = GeneralPopulationManager.Instance;
        if (famMgr == null || general == null) return;

        Canvas.ForceUpdateCanvases();

        // ---------- HUNGER / THIRST (weighted averages 0..1) ----------
        var groups = popMgr.AllPopulations;
        int totalPeople = groups.Sum(g => Mathf.Max(0, g?.count ?? 0));

        float avgHunger = 0f, avgThirst = 0f;
        if (totalPeople > 0)
        {
            float hungerSum = 0f, thirstSum = 0f;
            for (int i = 0; i < groups.Count; i++)
            {
                var g = groups[i];
                if (g == null || g.count <= 0) continue;
                hungerSum += g.hungerLevel * g.count;
                thirstSum += g.thirstLevel * g.count;
            }
            avgHunger = Mathf.Clamp01(hungerSum / Mathf.Max(1, totalPeople));
            avgThirst = Mathf.Clamp01(thirstSum / Mathf.Max(1, totalPeople));
        }

        // ---------- HOUSING FILL (capacity vs individuals) ----------
        int totalIndividuals = totalPeople;

        int shelterIndSlots = 0;
        if (bldMgr != null)
        {
            var all = bldMgr.GetAll();
            for (int i = 0; i < all.Count; i++)
            {
                var rec = all[i];
                if (rec == null || rec.instance == null) continue;

                var shelter = rec.instance.GetComponent<ShelterControl>();
                if (shelter == null || !shelter.isActiveAndEnabled) continue;

                // skip shelters that are destroyed
                var status = rec.instance.GetComponent<BuildingStatus>();
                if (status != null && status.CurrentState == BuildingState.Destroyed) continue;

                shelterIndSlots += Mathf.Max(0, shelter.individualCapacity);
            }
        }

        int individualsUnhoused = Mathf.Max(0, totalIndividuals - shelterIndSlots);

        // ratio grows with unmet housing (0 = everyone housed, 1 = none housed)
        float housingNeed = (totalIndividuals <= 0)
            ? 0f
            : Mathf.Clamp01(individualsUnhoused / (float)totalIndividuals);

        // ---------- Set bars + riding markers ----------
        SetBarAndMarker01(barHunger,  markerHungerContainer,  avgHunger);
        SetBarAndMarker01(barThirst,  markerThirstContainer,  avgThirst);
        SetBarAndMarker01(barHousing, markerHousingContainer, housingNeed);

        if (markerHungerText)  markerHungerText.text  = ToPct(avgHunger);
        if (markerThirstText)  markerThirstText.text  = ToPct(avgThirst);
        if (markerHousingText) markerHousingText.text = ToPct(housingNeed);

        // ---------- Next-cycle needs ----------
        var inv = PlayerInventoryManager.Instance;
        float foodNext = inv != null
            ? inv.CalcNextCycleNutritionIncreasePoints(popMgr, general)
            : Mathf.Max(0f, general.nutritionPointsPerPersonPerCycle) * totalPeople;

        float waterNext = inv != null
            ? inv.CalcNextCycleHydrationIncreasePoints(popMgr, general)
            : Mathf.Max(0f, general.hydrationPointsPerPersonPerCycle) * totalPeople;

        if (foodNextCycleText)  foodNextCycleText.text  = Mathf.CeilToInt(foodNext).ToString();
        if (waterNextCycleText) waterNextCycleText.text = Mathf.CeilToInt(waterNext).ToString();

        // ---------- Inventory totals (nutrition / hydration points available) ----------
        float invNutritionPts = 0f;
        float invHydrationPts = 0f;

        if (inv != null)
        {
            // FOOD stacks contribute nutrition points
            var foodStacks = inv.GetStacks(ResourceType.Food);
            for (int i = 0; i < foodStacks.Count; i++)
            {
                var s = foodStacks[i];
                if (s?.definition == null) continue;

                // Nutrition available from food
                invNutritionPts += s.definition.GetTotalNutrition(s.amount);

                // Hydration available from hydrating foods
                invHydrationPts += s.definition.GetTotalHydration(s.amount);
            }

            // WATER stacks contribute hydration points
            var waterStacks = inv.GetStacks(ResourceType.Water);
            for (int i = 0; i < waterStacks.Count; i++)
            {
                var s = waterStacks[i];
                if (s?.definition == null) continue;

                invHydrationPts += s.definition.GetTotalHydration(s.amount);
            }
        }

        // ---------- Label text: "Need X • Inv Y" ----------
        if (foodNextCycleText)
        {
            int needPts = Mathf.CeilToInt(foodNext);
            int invPts  = Mathf.CeilToInt(invNutritionPts);
            // Use 0 decimals to match your examples (1k, 10k, 100k, 1m)
            foodNextCycleText.text = $"{ShortNumberFormatter.Format(needPts, 2)} / {ShortNumberFormatter.Format(invPts, 0)}";
        }

        if (waterNextCycleText)
        {
            int needPts = Mathf.CeilToInt(waterNext);
            int invPts  = Mathf.CeilToInt(invHydrationPts);
            waterNextCycleText.text = $"{ShortNumberFormatter.Format(needPts, 2)} / {ShortNumberFormatter.Format(invPts, 0)}";
        }

        // ---------- Individuals housing label ----------
        if (individualsHousingText)
            individualsHousingText.text = $"{ShortNumberFormatter.Format(individualsUnhoused, 2)} / {ShortNumberFormatter.Format(shelterIndSlots, 0)}";
    }

    private void SetBarAndMarker01(RectTransform bar, RectTransform marker, float ratio01)
    {
        if (!bar || !barsRoot) return;

        float rootH = Mathf.Max(0f, barsRoot.rect.height);

        // Ensure marker’s rect height is current
        float markerH = 0f;
        if (marker)
        {
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(marker);
            markerH = marker.rect.height;
        }

        // Desired vs allowed (keep marker inside root)
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

    private static string ToPct(float v01) => Mathf.RoundToInt(Mathf.Clamp01(v01) * 100f) + "%";

    private void OnRectTransformDimensionsChange()
    {
        if (!isActiveAndEnabled) return;
        if (!barsRoot) return;
        RefreshNow();
    }
}