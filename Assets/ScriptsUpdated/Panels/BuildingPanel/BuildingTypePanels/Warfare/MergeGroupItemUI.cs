using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MergeGroupItemUI : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text nameText;
    public TMP_Text countText;
    public Image iconImage;
    public Button selectButton;

    private TileUnitGroupData _group;
    private UnitGroupPanelControl _panel;
    private int _effectiveMergeAmount;

    public void Setup(TileUnitGroupData group, UnitGroupPanelControl panel, int effectiveMergeAmount)
    {
        _group = group;
        _panel = panel;
        _effectiveMergeAmount = effectiveMergeAmount;

        string displayName;

        if (!string.IsNullOrEmpty(group.groupName))
        {
            displayName = group.groupName;
        }
        else if (group.unitType != null)
        {
            displayName = group.unitType.unitName;
        }
        else
        {
            displayName = "Unit Group";
        }

        if (nameText != null)
            nameText.text = displayName;

        if (countText != null)
            countText.text = $"{group.unitCount}+{_effectiveMergeAmount}";

        // 🔹 Icon
        if (iconImage != null)
        {
            if (group.unitType != null && group.unitType.unitIcon != null)
            {
                iconImage.sprite = group.unitType.unitIcon;
                iconImage.gameObject.SetActive(true);
            }
            else
            {
                iconImage.gameObject.SetActive(false);
            }
        }

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(OnSelectClicked);
        }
    }

    private void OnSelectClicked()
    {
        if (_panel != null && _group != null)
        {
            _panel.ConfirmMergeInto(_group);
        }
    }
}