using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InCombatTargetDisplayUI : MonoBehaviour
{
    [Header("Main")]
    public Image iconImage;
    public TMP_Text nameLabel;
    public TMP_Text countLabel;

    [Header("Animal")]
    public GameObject animalRoot;
    public TMP_Text aggressionText;
    public TMP_Text flightinessText;
    public TMP_Text strengthText;

    [Header("Unit")]
    public GameObject unitRoot;
    public TMP_Text movementSpeedText;
    public TMP_Text powerText;
    public TMP_Text defenseText;
    public TMP_Text agilityText;
    public TMP_Text accuracyText;
    public TMP_Text rangeText;
    public TMP_Text stealthText;

    [Header("Unit Health")]
    public GameObject unitHealthRoot;
    public Slider unitHealthSlider;

    public void Setup(MeleeTargetEntry entry)
    {
        if (entry == null) { gameObject.SetActive(false); return; }
        gameObject.SetActive(true);

        if (nameLabel)  nameLabel.text  = entry.displayName;
        if (countLabel) countLabel.text = entry.count.ToString();

        if (iconImage)
        {
            iconImage.sprite = entry.icon;
            iconImage.gameObject.SetActive(entry.icon != null);
        }

        bool isAnimal = entry.type == MeleeTargetType.Animal;
        bool isUnit   = entry.type == MeleeTargetType.Unit;

        if (animalRoot) animalRoot.SetActive(isAnimal);
        if (unitRoot)   unitRoot.SetActive(isUnit);

        if (isAnimal)
        {
            if (aggressionText)  aggressionText.text  = entry.aggression.ToString("0.00");
            if (flightinessText) flightinessText.text = entry.flightiness.ToString("0.00");
            if (strengthText) strengthText.text = entry.strength.ToString("0.00");

            if (unitHealthRoot) unitHealthRoot.SetActive(false);
        }
        else if (isUnit)
        {
            if (movementSpeedText) movementSpeedText.text = entry.movementSpeed.ToString("0.0");
            if (powerText)         powerText.text         = entry.power.ToString();
            if (defenseText)       defenseText.text       = entry.defense.ToString();
            if (agilityText)       agilityText.text       = entry.agility.ToString();
            if (accuracyText)      accuracyText.text      = entry.accuracy.ToString();
            if (rangeText)         rangeText.text         = entry.range.ToString();
            if (stealthText)       stealthText.text       = entry.stealth.ToString();

            if (unitHealthRoot) unitHealthRoot.SetActive(true);

            int maxH = Mathf.Max(1, entry.maxHealth);
            int curH = Mathf.Clamp(entry.currentHealth, 0, maxH);

            if (unitHealthSlider)
            {
                unitHealthSlider.minValue = 0;
                unitHealthSlider.maxValue = maxH;
                unitHealthSlider.value = curH;
                unitHealthSlider.wholeNumbers = true;
                unitHealthSlider.interactable = false;
            }
        }
    }
}
