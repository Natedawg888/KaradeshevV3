using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SummoningSpiritOfferItemUI : MonoBehaviour
{
    public Image icon;
    public TMP_Text nameText;
    public TMP_Text descriptionText;
    public Button chooseButton;

    private SpiritDefinitionSO _spirit;
    private SummoningSpiritOfferPanelControl _owner;

    public void Setup(SpiritDefinitionSO spirit, SummoningSpiritOfferPanelControl owner)
    {
        _spirit = spirit;
        _owner = owner;

        if (icon != null)
            icon.sprite = spirit != null ? spirit.icon : null;

        if (nameText != null)
            nameText.text = spirit != null ? spirit.displayName : "Unknown Spirit";

        if (descriptionText != null)
            descriptionText.text = spirit != null ? spirit.description : string.Empty;

        if (chooseButton != null)
        {
            chooseButton.onClick.RemoveAllListeners();
            chooseButton.onClick.AddListener(OnClickChoose);
        }
    }

    private void OnClickChoose()
    {
        if (_spirit == null || _owner == null)
            return;

        _owner.OnClickChooseSpirit(_spirit);
    }
}