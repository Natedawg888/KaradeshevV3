using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Detail panel for a militia unit in the tech panel.
/// Shows all stats, training costs, compatible training buildings, and unit actions.
/// </summary>
public class TechUnitDetailPanel : MonoBehaviour
{
    [Header("Root")]
    public GameObject root;
    public Button closeButton;

    [Header("Header")]
    public Image icon;
    public Image categoryIcon;
    public TMP_Text nameText;

    [Header("Stats")]
    public TMP_Text maxGroupSizeText;
    [Tooltip("0 on the SO means unlimited — displayed as ∞")]
    public TMP_Text maxSkillText;
    public TMP_Text trainingTurnsText;
    public TMP_Text trainingPopulationText;
    public TMP_Text healthText;

    [Header("Combat Stats")]
    public TMP_Text powerText;
    public TMP_Text defenseText;
    public TMP_Text agilityText;
    public TMP_Text accuracyText;
    public TMP_Text rangeText;
    public TMP_Text stealthText;
    public TMP_Text movementText;

    [Header("Training Costs")]
    public Transform costContentRoot;
    public BuildingCostEntry costEntryPrefab;

    [Header("Compatible Buildings")]
    public Transform buildingsContentRoot;
    public TechBuildingEntryUI buildingEntryPrefab;

    [Header("Unit Actions")]
    public Transform actionsContentRoot;
    public TechUnitActionEntryUI actionEntryPrefab;
    [Tooltip("Router sub-panel that opens when an action row is clicked. Place as a child of this panel.")]
    public ActionDetailPanelRouter actionDetailPanel;

    private MilitiaUnit _unit;
    private readonly List<GameObject> _costEntries     = new();
    private readonly List<GameObject> _buildingEntries = new();
    private readonly List<GameObject> _actionEntries   = new();

    private void Awake()
    {
        if (closeButton) closeButton.onClick.AddListener(Hide);
        if (root) root.SetActive(false);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void ShowFor(MilitiaUnit unit)
    {
        if (unit == null) { Hide(); return; }
        _unit = unit;

        ClearAll();
        if (actionDetailPanel != null) actionDetailPanel.Hide();

        if (root) root.SetActive(true);
        gameObject.SetActive(true);

        if (nameText) nameText.text = unit.unitName ?? unit.unitID;
        if (icon)
        {
            icon.sprite  = unit.unitIcon;
            icon.enabled = unit.unitIcon != null;
        }
        if (categoryIcon)
        {
            Sprite catSprite = UnitCategoryIconManager.Instance != null
                ? UnitCategoryIconManager.Instance.GetIconForCategory(unit.category)
                : null;
            categoryIcon.sprite  = catSprite;
            categoryIcon.enabled = catSprite != null;
        }

        if (maxGroupSizeText)  maxGroupSizeText.text  = unit.maxGroupSize > 0 ? unit.maxGroupSize.ToString() : "∞";
        if (maxSkillText)      maxSkillText.text       = unit.maxSkillLevel.ToString();
        if (trainingTurnsText)      trainingTurnsText.text      = unit.trainingTurns.ToString();
        if (trainingPopulationText) trainingPopulationText.text = unit.populationToTrain.ToString();
        if (healthText)        healthText.text         = unit.maxHealth.ToString();

        if (powerText)    powerText.text    = unit.power.ToString();
        if (defenseText)  defenseText.text  = unit.defense.ToString();
        if (agilityText)  agilityText.text  = unit.agility.ToString();
        if (accuracyText) accuracyText.text = unit.accuracy.ToString();
        if (rangeText)    rangeText.text    = unit.range.ToString();
        if (stealthText)  stealthText.text  = unit.stealth.ToString();
        if (movementText) movementText.text = unit.movementSpeed.ToString("F1");

        PopulateCosts();
        PopulateCompatibleBuildings();
        PopulateActions();
    }

    public void Hide()
    {
        ClearAll();
        if (actionDetailPanel != null) actionDetailPanel.Hide();
        _unit = null;
        if (root) root.SetActive(false);
    }

    // ── Training costs ────────────────────────────────────────────────────────

    private void PopulateCosts()
    {
        if (costEntryPrefab == null || costContentRoot == null) return;

        foreach (var cost in _unit.trainingCosts)
        {
            if (cost?.resource == null) continue;
            int owned = InventoryQuery.GetOwned(cost.resource);
            var entry = Instantiate(costEntryPrefab, costContentRoot);
            entry.Bind(cost.resource, cost.amount, owned);
            _costEntries.Add(entry.gameObject);
        }
    }

    // ── Compatible buildings ──────────────────────────────────────────────────

    private void PopulateCompatibleBuildings()
    {
        if (buildingEntryPrefab == null || buildingsContentRoot == null) return;

        var knownMgr = PlayerKnownBuildingsManager.Instance;
        if (knownMgr == null) return;

        foreach (var building in knownMgr.GetKnownBuildings())
        {
            if (building?.finalBuildingPrefab == null) continue;

            var ctrl = building.finalBuildingPrefab.GetComponent<KineticWarfareControl>();
            if (ctrl == null) continue;

            bool canTrain = false;
            foreach (var trainable in ctrl.GetAvailableTrainableUnits())
            {
                if (trainable == _unit) { canTrain = true; break; }
            }
            if (!canTrain) continue;

            var entry = Instantiate(buildingEntryPrefab, buildingsContentRoot);
            entry.Bind(building, null);
            _buildingEntries.Add(entry.gameObject);
        }
    }

    // ── Actions ───────────────────────────────────────────────────────────────

    private void PopulateActions()
    {
        if (actionEntryPrefab == null || actionsContentRoot == null) return;
        if (_unit.actions == null) return;

        foreach (var action in _unit.actions)
        {
            if (action == null) continue;
            var entry = Instantiate(actionEntryPrefab, actionsContentRoot);
            entry.Bind(action, a => actionDetailPanel?.ShowFor(a));
            _actionEntries.Add(entry.gameObject);
        }
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────

    private void ClearAll()
    {
        ClearList(_costEntries);
        ClearList(_buildingEntries);
        ClearList(_actionEntries);
    }

    private static void ClearList(List<GameObject> list)
    {
        for (int i = list.Count - 1; i >= 0; i--)
            if (list[i] != null) Destroy(list[i]);
        list.Clear();
    }
}
