using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays player-facing details for a MeleeAttackActionSO.
/// Receives an optional TileUnitGroupData for stat comparison (null = show base values).
///
/// Inspector setup — assign all fields; sections with a *Group field auto-hide when empty:
///
///   [Header]         icon, nameText, typeText
///   [Requirements]   requirementsGroup + requirementsText
///   [Targeting]      targetingGroup + targetsText + rangeText
///   [Hit Chance]     hitChanceText
///   [Damage]         damageText
///   [Initiative]     initiativeGroup + initiativeText
///   [Animal Combat]  animalCombatGroup + animalRetaliationText + animalFleeText
/// </summary>
public class MeleeActionDetailPanel : MonoBehaviour
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
    public GameObject targetingGroup;
    public TMP_Text targetsText;
    public TMP_Text rangeText;
    public TMP_Text durationText;

    [Header("Hit Chance")]
    public TMP_Text hitChanceText;

    [Header("Damage")]
    public TMP_Text damageText;

    [Header("Initiative")]
    public GameObject initiativeGroup;
    public TMP_Text initiativeText;

    [Header("Animal Combat")]
    public GameObject animalCombatGroup;
    public TMP_Text animalRetaliationText;
    public TMP_Text animalFleeText;

    // ── Public API ────────────────────────────────────────────────────────────

    public void ShowFor(MeleeAttackActionSO action, TileUnitGroupData group)
    {
        if (action == null) { Hide(); return; }

        if (root) root.SetActive(true);
        gameObject.SetActive(true);

        RefreshHeader(action);
        RefreshRequirements(action);
        RefreshTargeting(action);
        RefreshHitChance(action, group);
        RefreshDamage(action, group);
        RefreshInitiative(action, group);
        RefreshAnimalCombat(action, group);
    }

    public void Hide()
    {
        if (root) root.SetActive(false);
    }

    // ── Sections ──────────────────────────────────────────────────────────────

    private void RefreshHeader(MeleeAttackActionSO action)
    {
        if (nameText) nameText.text = action.displayName ?? action.actionID;
        if (typeText) typeText.text = "Melee Attack";

        if (icon)
        {
            icon.sprite  = action.icon;
            icon.enabled = action.icon != null;
        }
    }

    private void RefreshRequirements(MeleeAttackActionSO action)
    {
        var sb = new StringBuilder();

        if (action.requireMovement)  sb.AppendLine($"Movement:  {action.minMovement:F1}");
        if (action.requirePower)     sb.AppendLine($"Power:     {action.minPower}");
        if (action.requireDefense)   sb.AppendLine($"Defense:   {action.minDefense}");
        if (action.requireAgility)   sb.AppendLine($"Agility:   {action.minAgility}");
        if (action.requireAccuracy)  sb.AppendLine($"Accuracy:  {action.minAccuracy}");
        if (action.requireRange)     sb.AppendLine($"Range:     {action.minRange}");
        if (action.requireStealth)   sb.AppendLine($"Stealth:   {action.minStealth}");
        if (action.requireHealth)    sb.AppendLine($"Health:    {action.minHealth}");
        if (action.requireSkillLevel) sb.AppendLine($"Skill:     {action.minSkillLevel}");

        string text = sb.ToString().TrimEnd();

        if (requirementsGroup) requirementsGroup.SetActive(true);
        if (requirementsText)
            requirementsText.text = string.IsNullOrEmpty(text) ? "No Requirements" : text;
    }

    private void RefreshTargeting(MeleeAttackActionSO action)
    {
        if (targetingGroup) targetingGroup.SetActive(true);

        if (targetsText)
        {
            var targets = new StringBuilder();
            if (action.canTargetAnimals)    targets.Append("Animals");
            if (action.canTargetUnitGroups)
            {
                if (targets.Length > 0) targets.Append(", ");
                targets.Append("Unit Groups");
            }
            targetsText.text = targets.Length > 0 ? targets.ToString() : "None";
        }

        if (rangeText)
        {
            if (action.allowSameTileTarget && action.allowAdjacentTileTarget)
                rangeText.text = "Same Tile + Adjacent";
            else if (action.allowSameTileTarget)
                rangeText.text = "Same Tile Only";
            else if (action.allowAdjacentTileTarget)
                rangeText.text = "Adjacent Tile Only";
            else
                rangeText.text = "No Valid Range";
        }

        if (durationText)
            durationText.text = $"{Mathf.Max(1, action.durationTurns)}";
    }

    private void RefreshHitChance(MeleeAttackActionSO action, TileUnitGroupData group)
    {
        if (hitChanceText == null) return;

        if (!action.useAccuracyToHit)
        {
            hitChanceText.text = "Always";
            return;
        }

        if (group != null && group.unitType != null)
        {
            int acc = group.GetEffectiveAccuracy();
            float evasion = group.unitType.agility + group.bonusAgility
                          + (group.unitType.stealth + group.bonusStealth) * 0.5f;

            float chance = action.baseHitChance
                         + acc    * action.accuracyToHitChance
                         - evasion * action.evasionToMissChance;

            chance = Mathf.Clamp(chance, action.minHitChance, action.maxHitChance);
            hitChanceText.text = $"{chance * 100f:0}%";
        }
        else
        {
            hitChanceText.text = $"{action.baseHitChance * 100f:0}%";
        }
    }

    private void RefreshDamage(MeleeAttackActionSO action, TileUnitGroupData group)
    {
        if (damageText == null) return;

        if (group != null && group.unitType != null)
        {
            int power = group.GetEffectivePower();
            int dmg   = ScaledDamage(action, power);
            damageText.text = $"{dmg}";
        }
        else
        {
            string scaling =
                action.multiplyBaseDamageByUnitPower ? "Power strongly scales damage" :
                action.addUnitPowerToBaseDamage      ? "Power increases damage" :
                                                       $"Base damage: {action.baseDamagePerTurn}";
            damageText.text = scaling;
        }
    }

    private static int ScaledDamage(MeleeAttackActionSO a, int power)
    {
        float dmg = Mathf.Max(1f, a.baseDamagePerTurn);
        float p   = Mathf.Max(0f, power);

        if (a.addUnitPowerToBaseDamage)
            dmg += p * Mathf.Max(0f, a.unitPowerAdditionScale);

        if (a.multiplyBaseDamageByUnitPower)
            dmg *= Mathf.Max(1f, 1f + p * Mathf.Max(0f, a.unitPowerMultiplierScale));

        return Mathf.Max(1, Mathf.RoundToInt(dmg));
    }

    private void RefreshInitiative(MeleeAttackActionSO action, TileUnitGroupData group)
    {
        if (!action.useInitiativeRoll)
        {
            if (initiativeGroup) initiativeGroup.SetActive(false);
            return;
        }

        if (initiativeGroup) initiativeGroup.SetActive(true);
        if (initiativeText == null) return;

        if (group != null && group.unitType != null)
        {
            float score = InitiativeScore(action, group);
            string label = score >= 0.60f ? "High" : score >= 0.35f ? "Medium" : "Low";
            initiativeText.text = $"Initiative: {label}";
        }
        else
        {
            initiativeText.text = "Initiative: Roll-based";
        }
    }

    private static float InitiativeScore(MeleeAttackActionSO a, TileUnitGroupData g)
    {
        float move  = Mathf.Clamp01(g.GetEffectiveMovementSpeed() / Mathf.Max(0.01f, a.unitMoveForMaxInitiative));
        float agi   = Mathf.Clamp01((g.unitType.agility  + g.bonusAgility)  / (float)Mathf.Max(1, a.unitAgilityForMaxInitiative));
        float acc   = Mathf.Clamp01((g.unitType.accuracy + g.bonusAccuracy) / (float)Mathf.Max(1, a.unitAccuracyForMaxInitiative));
        float stl   = Mathf.Clamp01((g.unitType.stealth  + g.bonusStealth)  / (float)Mathf.Max(1, a.unitStealthForMaxInitiative));
        float pow   = Mathf.Clamp01(g.GetEffectivePower() / (float)Mathf.Max(1, a.unitPowerForMaxInitiative));
        float hp    = g.maxHealth > 0 ? Mathf.Clamp01(g.currentHealth / (float)g.maxHealth) : 1f;

        float score = move * 0.28f + agi * 0.28f + acc * 0.16f + stl * 0.08f + pow * 0.20f;
        score -= (1f - hp) * 0.20f;
        return Mathf.Clamp01(score);
    }

    private void RefreshAnimalCombat(MeleeAttackActionSO action, TileUnitGroupData group)
    {
        if (!action.canTargetAnimals)
        {
            if (animalCombatGroup) animalCombatGroup.SetActive(false);
            return;
        }

        if (animalCombatGroup) animalCombatGroup.SetActive(true);

        // Retaliation risk — based on size multipliers (no live target in encyclopedia context)
        if (animalRetaliationText)
            animalRetaliationText.text = "Animal retaliation possible";

        // Chase / flee info
        if (animalFleeText)
        {
            if (group != null && group.unitType != null)
            {
                float chase = ChaseScore(action, group);
                animalFleeText.text = $"Chase Chance: {chase * 100f:0}%";
            }
            else
            {
                animalFleeText.text = "Faster, stronger units are better at stopping animals from escaping";
            }
        }
    }

    private static float ChaseScore(MeleeAttackActionSO a, TileUnitGroupData g)
    {
        float move = Mathf.Clamp01(g.GetEffectiveMovementSpeed() / Mathf.Max(0.01f, a.unitMoveForMaxChase));
        float pow  = Mathf.Clamp01(g.GetEffectivePower() / (float)Mathf.Max(1, a.unitPowerForMaxChase));
        return Mathf.Clamp01(move * 0.7f + pow * 0.3f);
    }
}
