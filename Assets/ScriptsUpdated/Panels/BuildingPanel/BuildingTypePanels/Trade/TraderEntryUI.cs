using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TraderEntryUI : MonoBehaviour
{
    [Header("Display")]
    [SerializeField] private Image profileIcon;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descriptionText;

    [Header("Actions")]
    [SerializeField] private Button openButton;

    public void Bind(TravelingTraderOffer offer, Sprite portrait, Action onOpen)
    {
        if (nameText != null)
            nameText.text = offer.traderName;

        if (descriptionText != null)
            descriptionText.text = offer.flavorDescription;

        if (profileIcon != null)
        {
            profileIcon.sprite = portrait;
            profileIcon.gameObject.SetActive(portrait != null);
        }

        if (openButton != null)
        {
            openButton.onClick.RemoveAllListeners();
            openButton.onClick.AddListener(() => onOpen?.Invoke());
        }
    }
}
