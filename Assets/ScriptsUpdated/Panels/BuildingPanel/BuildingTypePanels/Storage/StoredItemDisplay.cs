using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StoredItemDisplay : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text resourceNameText;    // <-- ADD THIS
    public TMP_Text resourceAmountText;
    public Image resourceIconImage;

    [Header("Buttons")]
    public Button takeAllButton;
    public Button takeHalfButton;
    public Button takeOneButton;

    [Header("Spoilage")]
    public Slider spoilageSlider;

    private StorageItem _item;
    private StorageBuildingControl _storage;
    private StoragePanelControl _panel;

    public void SetDisplay(StorageItem item, StorageBuildingControl storage, StoragePanelControl panel)
    {
        _item = item;
        _storage = storage;
        _panel = panel;

        if (_item == null || _item.definition == null) return;

        // Name + Icon + Amount
        if (resourceNameText != null)
            resourceNameText.text = _item.definition.resourceName;

        if (resourceIconImage != null)
            resourceIconImage.sprite = _item.definition.resourceIcon;

        if (resourceAmountText != null)
            resourceAmountText.text = Mathf.Max(0, _item.amount).ToString();

        // Spoilage
        if (spoilageSlider != null)
        {
            int max = Mathf.Max(1, _item.spoilageInterval <= 0 ? 1 : _item.spoilageInterval);
            spoilageSlider.maxValue = max;
            spoilageSlider.value = Mathf.Clamp(_item.currentInterval, 0, max);
        }

        // Buttons
        if (takeAllButton != null) takeAllButton.onClick.RemoveAllListeners();
        if (takeHalfButton != null) takeHalfButton.onClick.RemoveAllListeners();
        if (takeOneButton != null) takeOneButton.onClick.RemoveAllListeners();

        int amount = Mathf.Max(0, _item.amount);

        if (takeAllButton != null) takeAllButton.onClick.AddListener(() => TakeResources(amount));
        if (takeHalfButton != null) takeHalfButton.onClick.AddListener(() => TakeResources(Mathf.CeilToInt(amount / 2f)));
        if (takeOneButton != null) takeOneButton.onClick.AddListener(() => TakeResources(1));
    }

    private void TakeResources(int amountToTake)
    {
        if (_storage == null || _item == null || _item.definition == null) return;
        if (amountToTake <= 0) return;

        if (_storage.TryWithdraw(_item.definition, amountToTake, out int taken) && taken > 0)
        {
            if (_panel != null)
                _panel.Refresh();
        }
        else
        {
            //Debug.LogWarning("Could not take resources (likely no player inventory space).");
        }
    }
}
