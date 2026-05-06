using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ReligiousSpiritItemUI : MonoBehaviour
{
    public Image icon;
    public TMP_Text nameText;
    public TMP_Text statusText;
    public Button selectButton;

    private SpiritDefinitionSO _spirit;
    private ReligiousBuildingPanelControl _ownerPanel;
    private ReligiousBuildingControl _control;
    private ReligiousSpiritPanelControl _parentPanel;

    public void Setup(
        SpiritDefinitionSO spirit,
        ReligiousBuildingPanelControl ownerPanel,
        ReligiousBuildingControl control,
        ReligiousSpiritPanelControl parentPanel)
    {
        _spirit = spirit;
        _ownerPanel = ownerPanel;
        _control = control;
        _parentPanel = parentPanel;

        if (icon != null)
            icon.sprite = spirit != null ? spirit.icon : null;

        if (nameText != null)
            nameText.text = spirit != null ? spirit.displayName : "Unknown Spirit";

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(OnClickSelect);
        }

        RefreshState();
    }

    public void RefreshState()
    {
        if (_spirit == null)
            return;

        bool isSelected = _ownerPanel != null && _ownerPanel.SelectedSpirit == _spirit;
        bool isAccepted = PlayerReligionManager.Instance != null && PlayerReligionManager.Instance.IsAccepted(_spirit);
        bool isAffiliated = _control != null && _control.IsSpiritAffiliated(_spirit);

        if (statusText != null)
        {
            if (isSelected)
                statusText.text = "Selected";
            else if (isAffiliated)
                statusText.text = "Affiliated";
            else if (isAccepted)
                statusText.text = "Followed";
            else
                statusText.text = "Known";
        }

        if (selectButton != null)
            selectButton.interactable = !isSelected;
    }

    private void OnClickSelect()
    {
        if (_ownerPanel == null || _spirit == null)
            return;

        _ownerPanel.SetSelectedSpirit(_spirit);
        _parentPanel.Hide();
    }
}