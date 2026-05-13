using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays player-facing details for a RangedAttackActionSO.
/// Static info only — no selected unit, group, or target required.
///
/// Inspector setup:
///   [Header]           icon, nameText, typeText
///   [Requirements]     requirementsGroup + requirementsText
///   [Targeting]        targetingGroup + targetsText + rangeText
///   [Timing & Damage]  timingGroup + durationText + damageText
///   [Hit Chance]       hitChanceGroup + hitChanceText + hitChanceModText
/// </summary>
public class RangedActionDetailPanel : MonoBehaviour
{
    [Header("Root")]
    public GameObject root;

    [Header("Header")]
    public Image    icon;
    public TMP_Text nameText;
    public TMP_Text typeText;

    [Header("Requirements")]
    public GameObject requirementsGroup;
    public TMP_Text   requirementsText;

    [Header("Targeting & Range")]
    public GameObject targetingGroup;
    public TMP_Text   targetsText;
    public TMP_Text   rangeText;

    [Header("Timing & Damage")]
    public GameObject timingGroup;
    public TMP_Text   durationText;
    public TMP_Text   damageText;

    [Header("Hit Chance")]
    public GameObject hitChanceGroup;
    public TMP_Text   hitChanceText;
    public TMP_Text   hitChanceModText;

    // ── Public API ────────────────────────────────────────────────────────────

    public void ShowFor(RangedAttackActionSO action)
    {
        if (action == null) { Hide(); return; }

        if (root) root.SetActive(true);
        gameObject.SetActive(true);

        RefreshHeader(action);
        RefreshRequirements(action);
        RefreshTargeting(action);
        RefreshTimingDamage(action);
        RefreshHitChance(action);
    }

    public void Hide()
    {
        if (root) root.SetActive(false);
    }

    // ── Sections ──────────────────────────────────────────────────────────────

    private void RefreshHeader(RangedAttackActionSO action)
    {
        string display = string.IsNullOrWhiteSpace(action.displayName) ? "Ranged Attack" : action.displayName;
        if (nameText) nameText.text = display;
        if (typeText) typeText.text = "Ranged Attack";

        if (icon)
        {
            icon.sprite  = action.icon;
            icon.enabled = action.icon != null;
        }
    }

    private void RefreshRequirements(RangedAttackActionSO action)
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
        if (requirementsText)  requirementsText.text = string.IsNullOrEmpty(text) ? "No Requirements" : text;
    }

    private void RefreshTargeting(RangedAttackActionSO action)
    {
        if (targetingGroup) targetingGroup.SetActive(true);

        if (targetsText)
        {
            if (action.canTargetAnimals && action.canTargetUnitGroups)
                targetsText.text = "Animals and Unit Groups";
            else if (action.canTargetAnimals)
                targetsText.text = "Animals";
            else if (action.canTargetUnitGroups)
                targetsText.text = "Unit Groups";
            else
                targetsText.text = "Nothing configured";
        }

        if (rangeText)
        {
            int r = action.maxRangeInTiles;
            rangeText.text = r > 0
                ? $"{r} tile{(r == 1 ? "" : "s")}"
                : "Not configured";
        }
    }

    private void RefreshTimingDamage(RangedAttackActionSO action)
    {
        if (timingGroup) timingGroup.SetActive(true);

        int d   = Mathf.Max(1, action.durationTurns);
        int dmg = Mathf.Max(0, action.baseDamagePerTurn);

        if (durationText)
            durationText.text = $"Takes {d} turn{(d == 1 ? "" : "s")} to complete.";

        if (damageText)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{dmg} per turn");
            if (d > 1)
                sb.Append($"Total base damage: {dmg * d} over {d} turns");
            damageText.text = sb.ToString().TrimEnd();
        }
    }

    private void RefreshHitChance(RangedAttackActionSO action)
    {
        if (!action.useHitChance)
        {
            if (hitChanceGroup) hitChanceGroup.SetActive(false);
            return;
        }

        if (hitChanceGroup) hitChanceGroup.SetActive(true);

        if (hitChanceText)
        {
            hitChanceText.text =
                $"Base: {Pct(action.baseHitChance)}%\n" +
                $"Min:  {Pct(action.minHitChance)}%\n" +
                $"Max:  {Pct(action.maxHitChance)}%";
        }

        if (hitChanceModText)
        {
            var sb = new StringBuilder();
            if (action.accuracyToHitChance > 0f)
                sb.AppendLine("• Higher Accuracy improves hit chance.");
            if (action.rangeToHitChance > 0f)
                sb.AppendLine("• Higher Range improves hit chance.");
            if (action.distancePenaltyPerTile > 0f)
                sb.Append("• Each tile of distance makes the shot harder.");
            hitChanceModText.text = sb.ToString().TrimEnd();
        }
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static int Pct(float v) => Mathf.RoundToInt(v * 100f);
}
