using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays player-facing details for a TrackAreaActionSO.
/// Static info only — no selected unit, group, or target required.
///
/// Inspector setup:
///   [Header]       icon, nameText, typeText, descriptionText
///   [Requirements] requirementsGroup + requirementsText
///   [Scan Area]    scanRangeText
///   [Tracks]       tracksText
///   [Purpose]      purposeText
///   [Summary]      summaryGroup + summaryText
/// </summary>
public class TrackingActionDetailPanel : MonoBehaviour
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

    [Header("Timing")]
    public TMP_Text durationText;

    [Header("Scan Area")]
    public TMP_Text scanRangeText;

    [Header("What It Tracks")]
    public TMP_Text tracksText;

    [Header("Purpose")]
    public TMP_Text purposeText;

    // ── Public API ────────────────────────────────────────────────────────────

    public void ShowFor(TrackAreaActionSO action)
    {
        if (action == null) { Hide(); return; }

        if (root) root.SetActive(true);
        gameObject.SetActive(true);

        RefreshHeader(action);
        RefreshRequirements(action);
        RefreshTiming();
        RefreshScanArea(action);
        RefreshTracks(action);
        RefreshPurpose(action);
    }

    public void Hide()
    {
        if (root) root.SetActive(false);
    }

    // ── Sections ──────────────────────────────────────────────────────────────

    private void RefreshHeader(TrackAreaActionSO action)
    {
        if (nameText) nameText.text = action.displayName ?? action.actionID;
        if (typeText) typeText.text = "Tracking Action";

        if (icon)
        {
            icon.sprite  = action.icon;
            icon.enabled = action.icon != null;
        }
    }

    private void RefreshRequirements(TrackAreaActionSO action)
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

    private void RefreshTiming()
    {
        if (durationText) durationText.text = "Instant";
    }

    private void RefreshScanArea(TrackAreaActionSO action)
    {
        if (scanRangeText == null) return;
        int r = Mathf.Max(1, action.maxRangeInTiles);
        scanRangeText.text = $"Checks tiles within {r} tile{(r == 1 ? "" : "s")} of the action location.";
    }

    private void RefreshTracks(TrackAreaActionSO action)
    {
        if (tracksText == null) return;

        if (action.trackAnimals && action.trackUnits)
            tracksText.text = "Animals and Unit Groups";
        else if (action.trackAnimals)
            tracksText.text = "Animals";
        else if (action.trackUnits)
            tracksText.text = "Unit Groups";
        else
            tracksText.text = "Nothing configured";
    }

    private void RefreshPurpose(TrackAreaActionSO action)
    {
        if (purposeText == null) return;

        if (action.trackAnimals && action.trackUnits)
            purposeText.text = "Useful for scouting nearby animals and enemy unit movement.";
        else if (action.trackAnimals)
            purposeText.text = "Useful for finding nearby animals before sending hunters.";
        else if (action.trackUnits)
            purposeText.text = "Useful for spotting nearby enemy movement.";
        else
            purposeText.text = string.Empty;
    }
}
