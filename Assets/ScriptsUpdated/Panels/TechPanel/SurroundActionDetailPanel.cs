using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays player-facing details for a SurroundActionSO.
/// Static info only — no selected unit, group, or target required.
///
/// Inspector setup:
///   [Header]          icon, nameText, typeText
///   [Requirements]    requirementsGroup + requirementsText
///   [Timing]          durationText
///   [Improved By]     improvedByGroup + improvedByText
///   [Escape Control]  escapeGroup + escapeText         (hidden if all values are 0)
///   [Retaliation]     retaliationGroup + retaliationText (hidden if all values are 0)
///   [Straggler]       stragglerGroup + stragglerText   (hidden if all values are 0)
///   [Summary]         summaryGroup + summaryText
/// </summary>
public class SurroundActionDetailPanel : MonoBehaviour
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

    [Header("Improved By")]
    public GameObject improvedByGroup;
    public TMP_Text improvedByText;

    [Header("Escape Control")]
    public GameObject escapeGroup;
    public TMP_Text escapeText;

    [Header("Retaliation Effects")]
    public GameObject retaliationGroup;
    public TMP_Text retaliationText;

    [Header("Straggler Effect")]
    public GameObject stragglerGroup;
    public TMP_Text stragglerText;

    // ── Public API ────────────────────────────────────────────────────────────

    public void ShowFor(SurroundActionSO action)
    {
        if (action == null) { Hide(); return; }

        if (root) root.SetActive(true);
        gameObject.SetActive(true);

        RefreshHeader(action);
        RefreshRequirements(action);
        RefreshTiming(action);
        RefreshImprovedBy(action);
        RefreshEscapeControl(action);
        RefreshRetaliation(action);
        RefreshStraggler(action);
    }

    public void Hide()
    {
        if (root) root.SetActive(false);
    }

    // ── Sections ──────────────────────────────────────────────────────────────

    private void RefreshHeader(SurroundActionSO action)
    {
        if (nameText) nameText.text = action.displayName ?? action.actionID;
        if (typeText) typeText.text = "Support Action";

        if (icon)
        {
            icon.sprite  = action.icon;
            icon.enabled = action.icon != null;
        }
    }

    private void RefreshRequirements(SurroundActionSO action)
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

    private void RefreshTiming(SurroundActionSO action)
    {
        if (durationText == null) return;
        int d = Mathf.Max(1, action.durationTurns);
        durationText.text = $"Takes {d} turn{(d == 1 ? "" : "s")} to complete.";
    }

    private void RefreshImprovedBy(SurroundActionSO action)
    {
        // Collect (statName, weight) pairs for all weights > 0
        var weights = new List<(string label, float weight)>
        {
            ("Unit Count", action.unitCountWeight),
            ("Movement",   action.movementWeight),
            ("Agility",    action.agilityWeight),
            ("Stealth",    action.stealthWeight),
            ("Power",      action.powerWeight),
            ("Defense",    action.defenseWeight),
            ("Accuracy",   action.accuracyWeight),
            ("Skill",      action.skillWeight),
        };

        // Remove zero weights and sort highest-first
        weights.RemoveAll(w => w.weight <= 0f);
        weights.Sort((a, b) => b.weight.CompareTo(a.weight));

        if (weights.Count == 0)
        {
            if (improvedByGroup) improvedByGroup.SetActive(false);
            return;
        }

        if (improvedByGroup) improvedByGroup.SetActive(true);

        var sb = new StringBuilder();
        foreach (var (label, _) in weights)
            sb.AppendLine($"• {label}");

        // Highlight the top stat if it's unit count
        if (weights[0].label == "Unit Count")
            sb.AppendLine("\nGroup size has the biggest impact.");

        if (improvedByText) improvedByText.text = sb.ToString().TrimEnd();
    }

    private void RefreshEscapeControl(SurroundActionSO action)
    {
        bool hasAttempt = action.maxEscapeAttemptReduction > 0f;
        bool hasSuccess = action.maxEscapeSuccessReduction > 0f;

        if (!hasAttempt && !hasSuccess)
        {
            if (escapeGroup) escapeGroup.SetActive(false);
            return;
        }

        if (escapeGroup) escapeGroup.SetActive(true);

        var sb = new StringBuilder();
        if (hasAttempt) sb.AppendLine($"Escape Attempts:  up to -{Pct(action.maxEscapeAttemptReduction)}%");
        if (hasSuccess) sb.AppendLine($"Escape Success:   up to -{Pct(action.maxEscapeSuccessReduction)}%");

        if (escapeText) escapeText.text = sb.ToString().TrimEnd();
    }

    private void RefreshRetaliation(SurroundActionSO action)
    {
        bool hasAnimal  = action.maxAnimalRetaliationBonus > 0f;
        bool hasUnitHit = action.maxUnitRetaliationHitBonus > 0f;
        bool hasUnitDmg = action.maxUnitRetaliationDamageBonus > 0f;

        if (!hasAnimal && !hasUnitHit && !hasUnitDmg)
        {
            if (retaliationGroup) retaliationGroup.SetActive(false);
            return;
        }

        if (retaliationGroup) retaliationGroup.SetActive(true);

        var sb = new StringBuilder();
        if (hasAnimal)  sb.AppendLine($"Animal Retaliation:  up to +{Pct(action.maxAnimalRetaliationBonus)}%");
        if (hasUnitHit) sb.AppendLine($"Unit Hit Bonus:      up to +{Pct(action.maxUnitRetaliationHitBonus)}%");
        if (hasUnitDmg) sb.AppendLine($"Unit Damage Bonus:   up to +{Pct(action.maxUnitRetaliationDamageBonus)}%");

        if (retaliationText) retaliationText.text = sb.ToString().TrimEnd();
    }

    private void RefreshStraggler(SurroundActionSO action)
    {
        bool hasBase  = action.baseAnimalStragglerChance > 0f;
        bool hasBonus = action.maxAnimalStragglerBonus > 0f;

        if (!hasBase && !hasBonus)
        {
            if (stragglerGroup) stragglerGroup.SetActive(false);
            return;
        }

        if (stragglerGroup) stragglerGroup.SetActive(true);

        var sb = new StringBuilder();
        if (hasBase)  sb.AppendLine($"Base Straggler Chance:  {Pct(action.baseAnimalStragglerChance)}%");
        if (hasBonus) sb.AppendLine($"Straggler Bonus:        up to +{Pct(action.maxAnimalStragglerBonus)}%");

        if (stragglerText) stragglerText.text = sb.ToString().TrimEnd();
    }
    // ── Helper ────────────────────────────────────────────────────────────────

    private static int Pct(float value01) => Mathf.RoundToInt(value01 * 100f);
}
