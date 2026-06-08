using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Popup shown on every 4th-turn population consumption cycle.
// Wire all fields in the Inspector, then attach to a panel prefab.
public class PopulationConsumptionPanel : MonoBehaviour
{
    [Header("Panel Root")]
    [SerializeField] private GameObject panelRoot;

    [Header("Hunger Row")]
    [SerializeField] private Image    hungerIcon;
    [SerializeField] private TMP_Text hungerNeededText;
    [SerializeField] private TMP_Text hungerTakenText;
    [SerializeField] private GameObject hungerSatisfiedBadge;

    [Header("Thirst Row")]
    [SerializeField] private Image    thirstIcon;
    [SerializeField] private TMP_Text thirstNeededText;
    [SerializeField] private TMP_Text thirstTakenText;
    [SerializeField] private GameObject thirstSatisfiedBadge;

    [Header("Consumed Resources List")]
    [SerializeField] private Transform        contentRoot;
    [SerializeField] private BuildingCostEntry costEntryPrefab;

    [Header("Dismiss")]
    [SerializeField] private Button dismissButton;

    private readonly List<BuildingCostEntry> _rows = new();

    private void Awake()
    {
        if (dismissButton) dismissButton.onClick.AddListener(Hide);
        Hide();
    }

    private void OnEnable()
    {
        PlayerAggregatedPopulationSimulationManager.OnConsumptionCycle += HandleConsumptionCycle;
    }

    private void OnDisable()
    {
        PlayerAggregatedPopulationSimulationManager.OnConsumptionCycle -= HandleConsumptionCycle;
    }

    private void HandleConsumptionCycle(ConsumptionCycleResult result)
    {
        Populate(result);
        Show();
    }

    private void Populate(ConsumptionCycleResult r)
    {
        // Compute remaining inventory totals (nutrition / hydration points after consumption)
        float invNutrition = 0f, invHydration = 0f;
        var inv = PlayerInventoryManager.Instance;
        if (inv != null)
        {
            var food = inv.GetStacks(ResourceType.Food);
            for (int i = 0; i < food.Count; i++)
            {
                var s = food[i];
                if (s?.definition == null) continue;
                invNutrition += s.definition.GetNutritionPerUnit() * s.amount;
                invHydration += s.definition.GetHydrationPerUnit() * s.amount;
            }
            var water = inv.GetStacks(ResourceType.Water);
            for (int i = 0; i < water.Count; i++)
            {
                var s = water[i];
                if (s?.definition == null) continue;
                invHydration += s.definition.GetHydrationPerUnit() * s.amount;
            }
        }

        // Hunger texts: "Needed: X"  |  "Taken / Inv: Y / Z"
        if (hungerNeededText)
            hungerNeededText.text = ShortNumberFormatter.Format(Mathf.CeilToInt(r.FoodNeededPts));
        if (hungerTakenText)
            hungerTakenText.text  = $"{ShortNumberFormatter.Format(Mathf.CeilToInt(r.FoodProvidedPts))} / {ShortNumberFormatter.Format(Mathf.CeilToInt(invNutrition))}";

        if (hungerSatisfiedBadge)
            hungerSatisfiedBadge.SetActive(r.HungerSatisfied);

        // Thirst texts
        if (thirstNeededText)
            thirstNeededText.text = ShortNumberFormatter.Format(Mathf.CeilToInt(r.WaterNeededPts));
        if (thirstTakenText)
            thirstTakenText.text  = $"{ShortNumberFormatter.Format(Mathf.CeilToInt(r.WaterProvidedPts))} / {ShortNumberFormatter.Format(Mathf.CeilToInt(invHydration))}";

        if (thirstSatisfiedBadge)
            thirstSatisfiedBadge.SetActive(r.ThirstSatisfied);

        // Consumed resource rows
        ClearRows();
        if (contentRoot == null || costEntryPrefab == null || r.Consumed == null) return;

        for (int i = 0; i < r.Consumed.Count; i++)
        {
            var entry = r.Consumed[i];
            if (entry.Definition == null) continue;

            var row = Instantiate(costEntryPrefab, contentRoot);
            int remaining = GetInventoryAmount(entry.Definition);
            // Bind: needText = amount consumed this cycle, haveText = remaining in inv
            row.Bind(entry.Definition, entry.AmountConsumed, remaining);
            _rows.Add(row);
        }
    }

    private static int GetInventoryAmount(ResourceDefinition def)
    {
        var inv = PlayerInventoryManager.Instance;
        if (inv == null || def == null) return 0;
        var stacks = inv.GetStacks(def.resourceType);
        int total = 0;
        for (int i = 0; i < stacks.Count; i++)
        {
            var s = stacks[i];
            if (s?.definition == null) continue;
            if (string.Equals(s.definition.resourceID, def.resourceID, System.StringComparison.OrdinalIgnoreCase))
                total += s.amount;
        }
        return total;
    }

    private void ClearRows()
    {
        for (int i = 0; i < _rows.Count; i++)
            if (_rows[i] != null) Destroy(_rows[i].gameObject);
        _rows.Clear();
    }

    private void Show() { if (panelRoot) panelRoot.SetActive(true); }
    private void Hide() { if (panelRoot) panelRoot.SetActive(false); }
}
