using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MeleeTargetItemUI : MonoBehaviour
{
    [Header("Main Info")]
    public Image iconImage;
    public TMP_Text nameLabel;
    public TMP_Text countLabel;
    public Button attackButton;
    public TMP_Text attackButtonLabel;

    [Header("Animal Details")]
    public GameObject animalDetailsRoot;
    public TMP_Text aggressionText;
    public TMP_Text flightinessText;

    [Header("Unit Details")]
    public GameObject unitDetailsRoot;
    public TMP_Text movementSpeedText;
    public TMP_Text powerText;
    public TMP_Text defenseText;
    public TMP_Text agilityText;
    public TMP_Text accuracyText;
    public TMP_Text rangeText;
    public TMP_Text stealthText;

    [Header("Unit Health")]
    public GameObject unitHealthRoot;
    public Slider healthSlider;

    // Backwards-compatible old call path
    public void Setup(MeleeTargetEntry entry, Action onClick)
    {
        Setup(entry, onClick, false);
    }

    // New overload for melee vs surround mode
    public void Setup(MeleeTargetEntry entry, Action onClick, bool isSurroundMode)
    {
        if (entry == null)
        {
            gameObject.SetActive(false);
            return;
        }

        if (nameLabel) nameLabel.text = entry.displayName;
        if (countLabel) countLabel.text = entry.count.ToString();

        if (iconImage)
        {
            iconImage.sprite = entry.icon;
            iconImage.gameObject.SetActive(entry.icon != null);
        }

        if (attackButtonLabel != null)
            attackButtonLabel.text = isSurroundMode ? "Support" : "Attack";

        bool isAnimal = entry.type == MeleeTargetType.Animal;
        bool isUnit = entry.type == MeleeTargetType.Unit;

        if (animalDetailsRoot) animalDetailsRoot.SetActive(isAnimal);
        if (unitDetailsRoot) unitDetailsRoot.SetActive(isUnit);

        if (isAnimal)
        {
            if (aggressionText) aggressionText.text = entry.aggression.ToString("0.00");
            if (flightinessText) flightinessText.text = entry.flightiness.ToString("0.00");
            if (unitHealthRoot) unitHealthRoot.SetActive(false);
        }
        else if (isUnit)
        {
            if (unitHealthRoot) unitHealthRoot.SetActive(true);

            int maxH = Mathf.Max(1, entry.maxHealth);
            int curH = Mathf.Clamp(entry.currentHealth, 0, maxH);

            if (healthSlider)
            {
                healthSlider.minValue = 0f;
                healthSlider.maxValue = maxH;
                healthSlider.value = curH;
                healthSlider.wholeNumbers = true;
                healthSlider.interactable = false;
            }

            if (movementSpeedText) movementSpeedText.text = entry.movementSpeed.ToString("0.0");
            if (powerText) powerText.text = entry.power.ToString();
            if (defenseText) defenseText.text = entry.defense.ToString();
            if (agilityText) agilityText.text = entry.agility.ToString();
            if (accuracyText) accuracyText.text = entry.accuracy.ToString();
            if (rangeText) rangeText.text = entry.range.ToString();
            if (stealthText) stealthText.text = entry.stealth.ToString();
        }

        if (attackButton)
        {
            attackButton.onClick.RemoveAllListeners();
            if (onClick != null)
                attackButton.onClick.AddListener(() => onClick());
        }

        gameObject.SetActive(true);
    }
}