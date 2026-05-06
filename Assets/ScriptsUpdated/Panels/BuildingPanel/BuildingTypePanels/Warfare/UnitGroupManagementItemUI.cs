using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UnitGroupManagementItemUI : MonoBehaviour
{
    [Header("Basic UI")]
    public Image iconImage;
    public TMP_Text nameText;
    public TMP_Text countText;

    [Header("Stats")]
    public TMP_Text healthText;
    public TMP_Text movementSpeedText;
    public TMP_Text powerText;
    public TMP_Text defenseText;
    public TMP_Text agilityText;
    public TMP_Text accuracyText;
    public TMP_Text stealthText;
    public TMP_Text rangeText;
    public TMP_Text skillText;

    [Header("Health Bar")]
    public Slider healthSlider;

    [Header("Category UI")]
    public Image categoryIconImage;

    [Header("Actions")]
    public Button manageButton;

    [Header("Temporary Disband UI")]
    public GameObject disbandedOverlayRoot;
    public Button rebandButton;
    public Button rebandWithoutPregnantButton;

    private TileUnitGroupData _group;
    private TileUnitGroupControl _owner;
    private KineticWarfarePanelControl _panel;
    private bool _isTemporarilyDisbanded;

    public void Setup(TileUnitGroupData group, TileUnitGroupControl owner, KineticWarfarePanelControl panel)
    {
        // Active group by default
        Setup(group, owner, panel, false, false, false);
    }

    public void Setup(
        TileUnitGroupData group,
        TileUnitGroupControl owner,
        KineticWarfarePanelControl panel,
        bool isTemporarilyDisbanded,
        bool canReband,
        bool canRebandWithoutPregnant)
    {
        _group = group;
        _owner = owner;
        _panel = panel;
        _isTemporarilyDisbanded = isTemporarilyDisbanded;

        Refresh();

        // Normal "Manage" behaviour only if the group is active.
        if (manageButton != null)
        {
            manageButton.onClick.RemoveAllListeners();

            if (!_isTemporarilyDisbanded)
            {
                manageButton.interactable = true;
                manageButton.onClick.AddListener(OnClickManage);
            }
            else
            {
                manageButton.interactable = false;
            }
        }

        if (disbandedOverlayRoot != null)
            disbandedOverlayRoot.SetActive(_isTemporarilyDisbanded);

        if (rebandButton != null)
        {
            rebandButton.onClick.RemoveAllListeners();
            rebandButton.gameObject.SetActive(_isTemporarilyDisbanded);
            rebandButton.interactable = _isTemporarilyDisbanded && canReband;

            if (_isTemporarilyDisbanded && canReband)
                rebandButton.onClick.AddListener(OnClickReband);
        }

        if (rebandWithoutPregnantButton != null)
        {
            rebandWithoutPregnantButton.onClick.RemoveAllListeners();

            bool showReducedReband = _isTemporarilyDisbanded && canRebandWithoutPregnant;
            rebandWithoutPregnantButton.gameObject.SetActive(showReducedReband);
            rebandWithoutPregnantButton.interactable = showReducedReband;

            if (showReducedReband)
                rebandWithoutPregnantButton.onClick.AddListener(OnClickRebandWithoutPregnant);
        }
    }

    public void Refresh()
    {
        if (_group == null) return;

        var unit = _group.unitType;

        // Icon + name + count
        if (iconImage != null)
            iconImage.sprite = unit != null ? unit.unitIcon : null;

        if (nameText != null)
        {
            string displayName =
                !string.IsNullOrEmpty(_group.groupName)
                    ? _group.groupName
                    : (unit != null ? unit.unitName : "Unit Group");

            nameText.text = displayName;
        }

        if (countText != null)
            countText.text = _group.unitCount.ToString();

        // ---------- Stats (per-unit) INCLUDING training bonuses ----------
        if (unit != null)
        {
            int perUnitHealth = unit.maxHealth + _group.bonusHealth;
            if (healthText) healthText.text = perUnitHealth.ToString();

            float moveTotal = unit.movementSpeed + _group.bonusMovementSpeed;
            if (movementSpeedText) movementSpeedText.text = moveTotal.ToString("0.0");

            int powerTotal = unit.power + _group.bonusPower;
            if (powerText) powerText.text = powerTotal.ToString();

            int defenseTotal = unit.defense + _group.bonusDefense;
            if (defenseText) defenseText.text = defenseTotal.ToString();

            int agilityTotal = unit.agility + _group.bonusAgility;
            if (agilityText) agilityText.text = agilityTotal.ToString();

            int accuracyTotal = unit.accuracy + _group.bonusAccuracy;
            if (accuracyText) accuracyText.text = accuracyTotal.ToString();

            int stealthTotal = unit.stealth + _group.bonusStealth;
            if (stealthText) stealthText.text = stealthTotal.ToString();

            int rangeTotal = unit.range + _group.bonusRange;
            if (rangeText) rangeText.text = rangeTotal.ToString();
        }
        else
        {
            if (healthText) healthText.text = "";
            if (movementSpeedText) movementSpeedText.text = "";
            if (powerText) powerText.text = "";
            if (defenseText) defenseText.text = "";
            if (agilityText) agilityText.text = "";
            if (accuracyText) accuracyText.text = "";
            if (stealthText) stealthText.text = "";
            if (rangeText) rangeText.text = "";
        }

        // Skill (uses group’s actual skill level)
        if (skillText != null)
        {
            int currentSkill = Mathf.Max(0, _group.skillLevel);
            int maxSkill = unit != null ? Mathf.Max(1, unit.maxSkillLevel) : currentSkill;
            skillText.text = $"{currentSkill}/{maxSkill}";
        }

        // Group health bar (actual group HP)
        if (healthSlider != null)
        {
            healthSlider.minValue = 0f;
            healthSlider.maxValue = Mathf.Max(1, _group.maxHealth);
            healthSlider.value = Mathf.Clamp(_group.currentHealth, 0, _group.maxHealth);
        }

        // Category icon
        if (categoryIconImage != null)
        {
            if (unit != null && UnitCategoryIconManager.Instance != null)
            {
                var icon = UnitCategoryIconManager.Instance.GetIconForCategory(unit.category);
                categoryIconImage.sprite = icon;
                categoryIconImage.gameObject.SetActive(icon != null);
            }
            else
            {
                categoryIconImage.gameObject.SetActive(false);
            }
        }
    }

    private void OnClickManage()
    {
        if (_panel != null && _group != null && _owner != null)
            _panel.OpenUnitGroupPanel(_group, _owner);
    }

    private void OnClickReband()
    {
        if (_panel != null && _group != null)
            _panel.OnRebandGroupClicked(_group, _owner);
    }

    private void OnClickRebandWithoutPregnant()
    {
        if (_panel != null && _group != null)
            _panel.OnRebandGroupWithoutPregnantClicked(_group, _owner);
    }
}