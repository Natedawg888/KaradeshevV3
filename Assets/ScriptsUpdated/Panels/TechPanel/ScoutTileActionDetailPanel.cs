using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays player-facing details for a ScoutTileActionSO.
/// Static info only — no selected unit, group, or target required.
///
/// Inspector setup:
///   [Header]       icon, nameText, typeText
///   [Requirements] requirementsGroup + requirementsText
///   [Targeting]    rangeText
///   [Valid Tiles]  tilesGroup + tilesText
///   [Timing]       timingText
///   [Improved By]  improvedByGroup + improvedByText
///   [Speed]        speedText
///   [Purpose]      purposeText
///   [Summary]      summaryGroup + summaryText
/// </summary>
public class ScoutTileActionDetailPanel : MonoBehaviour
{
    [Header("Root")]
    public GameObject root;

    [Header("Header")]
    public Image icon;
    public TMP_Text nameText;
    public TMP_Text typeText;

    [Header("Requirements")]
    public GameObject requirementsGroup;
    public TMP_Text requirementsText;

    [Header("Targeting")]
    public TMP_Text rangeText;

    [Header("Valid Tiles")]
    public GameObject tilesGroup;
    public TMP_Text tilesText;

    [Header("Timing")]
    public TMP_Text timingText;

    [Header("Speed Scaling")]
    public TMP_Text speedText;

    [Header("Purpose")]
    public TMP_Text purposeText;

    // ── Public API ────────────────────────────────────────────────────────────

    public void ShowFor(ScoutTileActionSO action)
    {
        if (action == null) { Hide(); return; }

        if (root) root.SetActive(true);
        gameObject.SetActive(true);

        RefreshHeader(action);
        RefreshRequirements(action);
        RefreshTargeting(action);
        RefreshValidTiles(action);
        RefreshTiming(action);
        RefreshSpeedScaling(action);
        RefreshPurpose(action);
    }

    public void Hide()
    {
        if (root) root.SetActive(false);
    }

    // ── Sections ──────────────────────────────────────────────────────────────

    private void RefreshHeader(ScoutTileActionSO action)
    {
        if (nameText) nameText.text = !string.IsNullOrEmpty(action.displayName)
            ? action.displayName : "Scout Tile";
        if (typeText) typeText.text = "Scout Action";

        if (icon)
        {
            icon.sprite  = action.icon;
            icon.enabled = action.icon != null;
        }
    }

    private void RefreshRequirements(ScoutTileActionSO action)
    {
        var sb = new StringBuilder();

        if (action.requireMovement)   sb.AppendLine($"Movement:  {action.minMovement:F1}");
        if (action.requirePower)      sb.AppendLine($"Power:     {action.minPower}");
        if (action.requireDefense)    sb.AppendLine($"Defense:   {action.minDefense}");
        if (action.requireAgility)    sb.AppendLine($"Agility:   {action.minAgility}");
        if (action.requireAccuracy)   sb.AppendLine($"Accuracy:  {action.minAccuracy}");
        if (action.requireRange)      sb.AppendLine($"Range:     {action.minRange}");
        if (action.requireStealth)    sb.AppendLine($"Stealth:   {action.minStealth}");
        if (action.requireHealth)     sb.AppendLine($"Health:    {action.minHealth}");
        if (action.requireSkillLevel) sb.AppendLine($"Skill:     {action.minSkillLevel}");

        string text = sb.ToString().TrimEnd();
        if (requirementsGroup) requirementsGroup.SetActive(true);
        if (requirementsText)
            requirementsText.text = string.IsNullOrEmpty(text) ? "No Requirements" : text;
    }

    private void RefreshTargeting(ScoutTileActionSO action)
    {
        if (rangeText == null) return;
        int r = Mathf.Max(1, action.maxRangeInTiles);
        rangeText.text = $"{r}";
    }

    private void RefreshValidTiles(ScoutTileActionSO action)
    {
        var sb = new StringBuilder();

        if (action.allowEnvironmentTiles)  sb.AppendLine("• Environment tiles");
        if (action.allowBuildingTiles)     sb.AppendLine("• Building tiles");
        if (action.allowDiscoveredTiles)   sb.AppendLine("• Discovered tiles");
        if (action.allowUndiscoveredTiles) sb.AppendLine("• Undiscovered tiles");

        string text = sb.ToString().TrimEnd();
        bool hasAny = !string.IsNullOrEmpty(text);
        if (tilesGroup) tilesGroup.SetActive(hasAny);
        if (tilesText)  tilesText.text = hasAny ? text : string.Empty;
    }

    private void RefreshTiming(ScoutTileActionSO action)
    {
        if (timingText == null) return;

        int baseTurns = Mathf.Max(1, action.baseTurns);
        var sb = new StringBuilder();
        sb.AppendLine($"{baseTurns}");

        float discMult  = action.discoveredTileTurnMult;
        float undisMult = action.undiscoveredTileTurnMult;

        if (!Mathf.Approximately(discMult, 1f))
        {
            int pct = Mathf.RoundToInt(Mathf.Abs(1f - discMult) * 100f);
            sb.AppendLine(discMult < 1f
                ? $"Discovered tiles: ~{pct}% faster"
                : $"Discovered tiles: ~{pct}% slower");
        }

        if (!Mathf.Approximately(undisMult, 1f))
        {
            int pct = Mathf.RoundToInt(Mathf.Abs(undisMult - 1f) * 100f);
            sb.AppendLine(undisMult > 1f
                ? $"Undiscovered tiles: ~{pct}% slower"
                : $"Undiscovered tiles: ~{pct}% faster");
        }

        timingText.text = sb.ToString().TrimEnd();
    }

    private void RefreshSpeedScaling(ScoutTileActionSO action)
    {
        if (speedText == null) return;

        var sb = new StringBuilder();

        int fasterPct = Mathf.RoundToInt((1f - Mathf.Min(1f, action.minFastMult)) * 100f);
        float slowerMult = action.maxSlowMult;

        if (fasterPct > 0)
            sb.AppendLine($"Fast scouts can complete this up to {fasterPct}% faster.");
        if (slowerMult > 1f)
            sb.AppendLine($"Poor scouts may take up to {slowerMult:0.#}x longer.");

        speedText.text = sb.ToString().TrimEnd();
    }

    private void RefreshPurpose(ScoutTileActionSO action)
    {
        if (purposeText == null) return;

        var sb = new StringBuilder();
        sb.AppendLine("Scouting helps inspect nearby tiles before committing units to movement, gathering, hunting, or combat.");

        if (action.allowUndiscoveredTiles)
            sb.AppendLine("Useful for revealing or checking unknown nearby tiles.");
        if (action.allowBuildingTiles)
            sb.AppendLine("Can also inspect buildings within range.");

        purposeText.text = sb.ToString().TrimEnd();
    }
}
