using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Read-only detail panel for a Technology.
/// Does not start, complete, or affect research — display only.
///
/// Inspector setup:
///   [Root]             root
///   [Header]           techIconImage, techNameText, descriptionText
///   [Research Info]    turnsRequiredText, requiredKnowledgeText,
///                      requiredPlayerLevelText, requiredPopulationText (optional)
///   [Rewards]          rewardsSectionRoot, rewardsText
///   [Research Costs]   researchCostSectionRoot, researchCostContentRoot, valueRowPrefab
///   [Buildings]        researchBuildingsSectionRoot, researchBuildingsContentRoot
///   [Effects]          effectsSectionRoot, effectsContentRoot
///                      (reuses valueRowPrefab — title = effect category, value = description)
/// </summary>
public class TechnologyDetailPanelControl : MonoBehaviour
{
    [Header("Root")]
    public GameObject root;
    public Button     closeButton;

    [Header("Header")]
    public Image    techIconImage;
    public TMP_Text techNameText;
    public TMP_Text descriptionText;

    [Header("Research Info")]
    public TMP_Text turnsRequiredText;
    public TMP_Text requiredKnowledgeText;
    public TMP_Text requiredPlayerLevelText;
    public TMP_Text requiredPopulationText;

    [Header("Rewards")]
    public GameObject rewardsSectionRoot;
    public TMP_Text   rewardsText;

    [Header("Research Costs")]
    public GameObject        researchCostSectionRoot;
    public Transform         researchCostContentRoot;
    public BuildingCostEntry costEntryPrefab;

    [Header("Researchable Buildings")]
    public GameObject          researchBuildingsSectionRoot;
    public Transform           researchBuildingsContentRoot;
    public TechBuildingEntryUI buildingEntryPrefab;

    [Header("Effects")]
    public GameObject           effectsSectionRoot;
    public Transform            effectsContentRoot;
    public TechDetailValueRowUI valueRowPrefab;

    private readonly List<GameObject> _costRows     = new();
    private readonly List<GameObject> _buildingRows = new();
    private readonly List<GameObject> _effectRows   = new();

    private void Awake()
    {
        if (closeButton) closeButton.onClick.AddListener(Hide);
        if (root) root.SetActive(false);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Show(Technology tech)
    {
        if (tech == null) { Hide(); return; }

        if (root) root.SetActive(true);
        gameObject.SetActive(true);

        RefreshHeader(tech);
        RefreshResearchInfo(tech);
        RefreshRewards(tech);
        PopulateResearchCosts(tech);
        PopulateResearchBuildings(tech);
        PopulateEffects(tech);
    }

    public void Hide()
    {
        ClearContent(researchCostContentRoot, _costRows);
        ClearContent(researchBuildingsContentRoot, _buildingRows);
        ClearContent(effectsContentRoot, _effectRows);
        if (root) root.SetActive(false);
    }

    // ── Sections ──────────────────────────────────────────────────────────────

    private void RefreshHeader(Technology tech)
    {
        if (techNameText)
            techNameText.text = string.IsNullOrWhiteSpace(tech.techName) ? tech.techID : tech.techName;

        if (techIconImage)
        {
            techIconImage.sprite  = tech.icon;
            techIconImage.enabled = tech.icon != null;
        }

        if (descriptionText)
            descriptionText.text = string.IsNullOrWhiteSpace(tech.description)
                ? "No description available."
                : tech.description;
    }

    private void RefreshResearchInfo(Technology tech)
    {
        int turns = Mathf.Max(1, tech.turnsRequired);
        if (turnsRequiredText)
            turnsRequiredText.text = $"{turns} turn{(turns == 1 ? "" : "s")}";

        if (requiredKnowledgeText)
            requiredKnowledgeText.text = tech.requiredKnowledge.ToString();

        if (requiredPlayerLevelText)
            requiredPlayerLevelText.text = $"Level {tech.requiredPlayerLevel}";

        if (requiredPopulationText != null)
        {
            bool show = tech.requiredPopulation > 0;
            requiredPopulationText.gameObject.SetActive(show);
            if (show) requiredPopulationText.text = tech.requiredPopulation.ToString();
        }
    }

    private void RefreshRewards(Technology tech)
    {
        bool hasRewards = tech.knowledgeReward > 0 || tech.xpReward > 0;
        if (rewardsSectionRoot) rewardsSectionRoot.SetActive(hasRewards);
        if (!hasRewards || rewardsText == null) return;

        var sb = new StringBuilder();
        if (tech.knowledgeReward > 0) sb.AppendLine($"Knowledge: +{tech.knowledgeReward}");
        if (tech.xpReward        > 0) sb.Append($"XP: +{tech.xpReward}");
        rewardsText.text = sb.ToString().TrimEnd();
    }

    private void PopulateResearchCosts(Technology tech)
    {
        ClearContent(researchCostContentRoot, _costRows);

        bool hasCosts = tech.researchCosts != null && tech.researchCosts.Count > 0;
        if (researchCostSectionRoot) researchCostSectionRoot.SetActive(hasCosts);
        if (!hasCosts || researchCostContentRoot == null || costEntryPrefab == null) return;

        foreach (var cost in tech.researchCosts)
        {
            if (cost?.resource == null) continue;
            int owned = InventoryQuery.GetOwned(cost.resource);
            var entry = Instantiate(costEntryPrefab, researchCostContentRoot);
            entry.Bind(cost.resource, cost.amount, owned);
            _costRows.Add(entry.gameObject);
        }
    }

    private void PopulateResearchBuildings(Technology tech)
    {
        ClearContent(researchBuildingsContentRoot, _buildingRows);
        if (researchBuildingsSectionRoot) researchBuildingsSectionRoot.SetActive(true);

        bool anyBuilding = tech.researchableByBuildingIDs == null || tech.researchableByBuildingIDs.Count == 0;

        if (researchBuildingsContentRoot == null || buildingEntryPrefab == null) return;

        foreach (var id in tech.researchableByBuildingIDs)
        {
            if (string.IsNullOrEmpty(id)) continue;
            var building = BuildingManager.Instance?.GetBuildingByID(id);
            if (building == null) continue;
            var entry = Instantiate(buildingEntryPrefab, researchBuildingsContentRoot);
            entry.Bind(building, null);
            _buildingRows.Add(entry.gameObject);
        }
    }

    private void PopulateEffects(Technology tech)
    {
        ClearContent(effectsContentRoot, _effectRows);

        bool hasEffects = tech.effectSOs != null && tech.effectSOs.Count > 0;
        if (effectsSectionRoot) effectsSectionRoot.SetActive(hasEffects);
        if (!hasEffects || effectsContentRoot == null || valueRowPrefab == null) return;

        foreach (var effect in tech.effectSOs)
        {
            if (effect == null) continue;
            var (category, description) = DescribeEffect(effect);
            var row = Instantiate(valueRowPrefab, effectsContentRoot);
            row.Setup(category, description);
            _effectRows.Add(row.gameObject);
        }
    }

    // ── Effect description ────────────────────────────────────────────────────

    private static (string category, string description) DescribeEffect(TechnologyEffectSO effect)
    {
        if (effect is WorldTechEffectSO world)         return ("World",       DescribeWorld(world));
        if (effect is HealthTechEffectSO health)       return ("Health",      DescribeHealth(health));
        if (effect is BuildingTechEffectSO building)   return ("Buildings",   DescribeBuilding(building));
        if (effect is EnvironmentTechEffectSO env)     return ("Environment", DescribeEnvironment(env));
        return (effect.GetType().Name, "Effect applied on research");
    }

    private static string DescribeWorld(WorldTechEffectSO w)
    {
        var sb = new StringBuilder();

        if (w.addKnownResources?.Count > 0)
            sb.AppendLine($"Unlocks: {JoinResourceNames(w.addKnownResources)}");

        if (w.addKnownBuildingIDs?.Count > 0)
            sb.AppendLine($"Unlocks buildings: {JoinBuildingNames(w.addKnownBuildingIDs)}");

        if (w.addKnownTechnologyIDs?.Count > 0)
            sb.AppendLine($"Unlocks technologies: {JoinTechNames(w.addKnownTechnologyIDs)}");

        if (w.addKnownCraftingIDs?.Count > 0)
            sb.AppendLine($"Unlocks crafting: {JoinCraftingNames(w.addKnownCraftingIDs)}");

        if (w.addKnownProductionIDs?.Count > 0)
            sb.AppendLine($"Unlocks production: {JoinProductionNames(w.addKnownProductionIDs)}");

        if (w.addKnownUnits?.Count > 0)
            sb.AppendLine($"Unlocks units: {JoinUnitNames(w.addKnownUnits)}");

        if (w.addKnownSpirits?.Count > 0)
            sb.AppendLine($"Unlocks {w.addKnownSpirits.Count} spirit{(w.addKnownSpirits.Count == 1 ? "" : "s")}");

        if (w.addKnownRituals?.Count > 0)
            sb.AppendLine($"Unlocks {w.addKnownRituals.Count} ritual{(w.addKnownRituals.Count == 1 ? "" : "s")}");

        if (w.removeKnownResources?.Count > 0)
            sb.AppendLine($"Removes: {JoinResourceNames(w.removeKnownResources)}");

        return sb.Length > 0 ? sb.ToString().TrimEnd() : "No changes";
    }

    private static string DescribeHealth(HealthTechEffectSO h)
    {
        var sb = new StringBuilder();

        AppendDelta(sb, "Child health",  h.baseChildHealthDelta);
        AppendDelta(sb, "Teen health",   h.baseTeenHealthDelta);
        AppendDelta(sb, "Adult health",  h.baseAdultHealthDelta);
        AppendDelta(sb, "Elder health",  h.baseElderHealthDelta);
        AppendDelta(sb, "Lifespan",      h.lifespanDelta, "turns");
        AppendDelta(sb, "Child→Teen age",  h.childToTeenAgeDelta,  "turns");
        AppendDelta(sb, "Teen→Adult age",  h.teenToAdultAgeDelta,  "turns");
        AppendDelta(sb, "Adult→Elder age", h.adultToElderAgeDelta, "turns");

        AppendDeltaF(sb, "Child recovery",  h.childRecoveryDelta);
        AppendDeltaF(sb, "Teen recovery",   h.teenRecoveryDelta);
        AppendDeltaF(sb, "Adult recovery",  h.adultRecoveryDelta);
        AppendDeltaF(sb, "Elder recovery",  h.elderRecoveryDelta);

        AppendDeltaF(sb, "Child resistance",  h.childResistanceDelta);
        AppendDeltaF(sb, "Teen resistance",   h.teenResistanceDelta);
        AppendDeltaF(sb, "Adult resistance",  h.adultResistanceDelta);
        AppendDeltaF(sb, "Elder resistance",  h.elderResistanceDelta);

        return sb.Length > 0 ? sb.ToString().TrimEnd() : "No changes";
    }

    private static string DescribeBuilding(BuildingTechEffectSO b)
    {
        var sb = new StringBuilder();

        if (b.targetBuildingIDs?.Count > 0)
            sb.AppendLine($"Affects: {JoinBuildingNames(b.targetBuildingIDs)}");

        AppendDelta(sb, "Building health",       b.maxHealthDelta);
        AppendDelta(sb, "Degen amount",          b.degenerationAmountDelta);
        AppendDelta(sb, "Degen interval",        b.degenerationIntervalDelta, "turns");

        return sb.Length > 0 ? sb.ToString().TrimEnd() : "No changes";
    }

    private static string DescribeEnvironment(EnvironmentTechEffectSO e)
    {
        if (e.environmentEffects == null || e.environmentEffects.Count == 0)
            return "No environment effects";

        var sb = new StringBuilder();
        foreach (var fx in e.environmentEffects)
        {
            bool allEnvs   = fx.environmentTypes == null || fx.environmentTypes.Count == 0;
            bool allTiles  = fx.tileTypes         == null || fx.tileTypes.Count == 0;
            bool allSizes  = fx.tileSizes          == null || fx.tileSizes.Count == 0;

            if (allEnvs && allTiles && allSizes)
            {
                sb.AppendLine("Applies to: All tiles");
            }
            else
            {
                if (!allEnvs)
                    sb.AppendLine($"Environments: {string.Join(", ", fx.environmentTypes)}");
                if (!allTiles)
                    sb.AppendLine($"Tile types: {string.Join(", ", fx.tileTypes)}");
                if (!allSizes)
                    sb.AppendLine($"Sizes: {string.Join(", ", fx.tileSizes)}");
            }

            if (fx.unlockExplore)
                sb.AppendLine("Unlocks exploration");

            if (fx.discoveryFailureDeltaPct != 0f)
                sb.AppendLine($"Discovery failure: {SignedPct(fx.discoveryFailureDeltaPct)}");
            if (fx.discoveryFailureMult != 0f)
                sb.AppendLine($"Discovery failure: ×{fx.discoveryFailureMult:0.##}");
            if (fx.discoveryTurnsDelta != 0)
                sb.AppendLine($"Discovery turns: {SignedInt(fx.discoveryTurnsDelta)}");
            if (fx.discoveryTurnsMult != 0f)
                sb.AppendLine($"Discovery turns: ×{fx.discoveryTurnsMult:0.##}");
            if (fx.discoveryRequiredPopDelta != 0)
                sb.AppendLine($"Discovery pop: {SignedInt(fx.discoveryRequiredPopDelta)}");
            if (fx.discoveryPenaltyDelta != 0)
                sb.AppendLine($"Discovery penalty: {SignedInt(fx.discoveryPenaltyDelta)}");

            if (fx.gatheringFailureDeltaPct != 0f)
                sb.AppendLine($"Gathering failure: {SignedPct(fx.gatheringFailureDeltaPct)}");
            if (fx.gatheringFailureMult != 0f)
                sb.AppendLine($"Gathering failure: ×{fx.gatheringFailureMult:0.##}");
            if (fx.gatheringTurnsDelta != 0)
                sb.AppendLine($"Gathering turns: {SignedInt(fx.gatheringTurnsDelta)}");
            if (fx.gatheringTurnsMult != 0f)
                sb.AppendLine($"Gathering turns: ×{fx.gatheringTurnsMult:0.##}");
            if (fx.gatheringRequiredPopDelta != 0)
                sb.AppendLine($"Gathering pop: {SignedInt(fx.gatheringRequiredPopDelta)}");
            if (fx.gatheringPenaltyDelta != 0)
                sb.AppendLine($"Gathering penalty: {SignedInt(fx.gatheringPenaltyDelta)}");
        }

        return sb.Length > 0 ? sb.ToString().TrimEnd() : "No changes";
    }

    // ── Static helpers ────────────────────────────────────────────────────────

    private static void AppendDelta(StringBuilder sb, string label, int delta, string unit = "")
    {
        if (delta == 0) return;
        string suffix = string.IsNullOrEmpty(unit) ? "" : $" {unit}";
        sb.AppendLine(delta > 0 ? $"{label}: +{delta}{suffix}" : $"{label}: {delta}{suffix}");
    }

    private static void AppendDeltaF(StringBuilder sb, string label, float delta)
    {
        if (Mathf.Approximately(delta, 0f)) return;
        sb.AppendLine(delta > 0f ? $"{label}: +{delta:0.##}" : $"{label}: {delta:0.##}");
    }

    private static string JoinResourceNames(IList<ResourceDefinition> list)
    {
        var names = new List<string>(list.Count);
        foreach (var r in list)
            if (r != null) names.Add(r.resourceName ?? r.resourceID);
        return string.Join(", ", names);
    }

    private static string JoinBuildingNames(IList<string> ids)
    {
        var names = new List<string>(ids.Count);
        foreach (var id in ids)
            if (!string.IsNullOrEmpty(id)) names.Add(ResolveBuildingName(id));
        return string.Join(", ", names);
    }

    private static string JoinTechNames(IList<string> ids)
    {
        var names = new List<string>(ids.Count);
        foreach (var id in ids)
        {
            if (string.IsNullOrEmpty(id)) continue;
            var tech = TechnologyManager.Instance?.GetByID(id);
            names.Add(tech != null ? (tech.techName ?? tech.techID) : id);
        }
        return string.Join(", ", names);
    }

    private static string JoinCraftingNames(IList<string> ids)
    {
        var names = new List<string>(ids.Count);
        foreach (var id in ids)
        {
            if (string.IsNullOrEmpty(id)) continue;
            var recipe = CraftingRecipeManager.Instance?.GetByID(id);
            names.Add(recipe != null ? (recipe.craftingName ?? recipe.craftingID) : id);
        }
        return string.Join(", ", names);
    }

    private static string JoinProductionNames(IList<string> ids)
    {
        var names = new List<string>(ids.Count);
        foreach (var id in ids)
        {
            if (string.IsNullOrEmpty(id)) continue;
            var plan = ProductionPlanManager.Instance?.GetByID(id);
            names.Add(plan != null ? (plan.planName ?? plan.productionID) : id);
        }
        return string.Join(", ", names);
    }

    private static string JoinUnitNames(IList<MilitiaUnit> list)
    {
        var names = new List<string>(list.Count);
        foreach (var u in list)
            if (u != null) names.Add(u.unitName ?? u.unitID);
        return string.Join(", ", names);
    }

    private static string ResolveBuildingName(string buildingId)
    {
        var building = BuildingManager.Instance?.GetBuildingByID(buildingId);
        if (building != null && !string.IsNullOrWhiteSpace(building.buildingName))
            return building.buildingName;
        return buildingId;
    }

    private static void ClearContent(Transform contentRoot, List<GameObject> list)
    {
        for (int i = list.Count - 1; i >= 0; i--)
            if (list[i] != null) Destroy(list[i]);
        list.Clear();
    }

    private static string SignedInt(int v) => v > 0 ? $"+{v}" : v.ToString();
    private static string SignedPct(float v) => v > 0f ? $"+{v:0.#}%" : $"{v:0.#}%";
}
