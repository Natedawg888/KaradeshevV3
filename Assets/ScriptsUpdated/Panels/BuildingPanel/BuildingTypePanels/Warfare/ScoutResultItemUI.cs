using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScoutResultItemUI : MonoBehaviour
{
    public Image iconImage;
    public TMP_Text nameLabel;
    public TMP_Text countLabel;

    [Header("Unit Moving Indicator")]
    public Image movingIcon;      // for UNIT groups only
    public Sprite movingSprite;   // optional
    public Sprite idleSprite;     // optional

    [Header("Animal Status Icons")]
    public GameObject animalEatingIcon;
    public GameObject animalDrinkingIcon;
    public GameObject animalMovingIcon;
    public GameObject animalHuntingIcon;
    public GameObject animalDefendingIcon;
    public GameObject animalTargetedIcon;

    [Header("Animal Combat Movement Icons")]
    public GameObject animalAttackingIcon;
    public GameObject animalFleeingIcon;

    public void Setup(ScoutResultEntry entry)
    {
        if (entry == null)
        {
            gameObject.SetActive(false);
            return;
        }

        if (nameLabel != null)
            nameLabel.text = entry.entityName;

        if (iconImage != null)
        {
            iconImage.sprite = entry.icon;
            iconImage.gameObject.SetActive(entry.icon != null);
        }

        if (countLabel != null)
            countLabel.text = entry.count.ToString();

        bool isAnimal = entry.entityType == ScoutEntityType.Animal;

        // ---------------- Unit UI ----------------
        if (movingIcon != null)
        {
            // Only show this for UNIT results
            movingIcon.gameObject.SetActive(!isAnimal);

            if (!isAnimal)
            {
                if (entry.wasMoving && movingSprite != null)
                    movingIcon.sprite = movingSprite;
                else if (!entry.wasMoving && idleSprite != null)
                    movingIcon.sprite = idleSprite;
            }
        }

        // ---------------- Animal UI ----------------
        if (animalEatingIcon != null)
            animalEatingIcon.SetActive(isAnimal && entry.wasEating);

        if (animalDrinkingIcon != null)
            animalDrinkingIcon.SetActive(isAnimal && entry.wasDrinking);

        if (animalMovingIcon != null)
            animalMovingIcon.SetActive(isAnimal && entry.wasMoving);

        if (animalHuntingIcon != null)
            animalHuntingIcon.SetActive(isAnimal && entry.wasHunting);

        if (animalDefendingIcon != null)
            animalDefendingIcon.SetActive(isAnimal && entry.wasDefending);

        if (animalTargetedIcon != null)
            animalTargetedIcon.SetActive(isAnimal && entry.wasTargeted);

        if (animalAttackingIcon != null)
            animalAttackingIcon.SetActive(isAnimal && entry.wasAttacking);

        if (animalFleeingIcon != null)
            animalFleeingIcon.SetActive(isAnimal && entry.wasFleeing);
    }
}