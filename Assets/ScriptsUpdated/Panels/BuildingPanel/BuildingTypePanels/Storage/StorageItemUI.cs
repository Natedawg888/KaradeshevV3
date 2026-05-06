using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public class StorageItemUI : MonoBehaviour
{
    public TMP_Text resourceNameText;
    public Image resourceIconImage;
    public TMP_InputField manualAmountInput;
    public Button storeAllButton;
    public Button storeHalfButton;
    public Button storeOneButton;
    public Button confirmButton;
    public TMP_Text storageCapacityText;

    private ResourceDefinition _def;
    private int _maxAmount;
    private int _storeAmount;
    private int _availableCapacity;

    private StorageBuildingControl _storage;
    private StoragePanelControl _panel;

    private bool _suppressInput;

    public void SetResource(ResourceDefinition def, int availableAmount, StorageBuildingControl storage, StoragePanelControl panel)
    {
        _def = def;
        _maxAmount = Mathf.Max(0, availableAmount);
        _storage = storage;
        _panel = panel;

        if (_def == null || _storage == null)
        {
            Debug.LogWarning("[StorageItemUI] Missing def or storage.");
            return;
        }

        resourceNameText.text = _def.resourceName;
        resourceIconImage.sprite = _def.resourceIcon;

        _availableCapacity = _storage.GetAvailableSpaceForResource(_def);
        _storeAmount = Mathf.Min(_maxAmount, _availableCapacity);

        // Clear listeners
        if (manualAmountInput != null) manualAmountInput.onValueChanged.RemoveAllListeners();
        if (storeAllButton != null) storeAllButton.onClick.RemoveAllListeners();
        if (storeHalfButton != null) storeHalfButton.onClick.RemoveAllListeners();
        if (storeOneButton != null) storeOneButton.onClick.RemoveAllListeners();
        if (confirmButton != null) confirmButton.onClick.RemoveAllListeners();

        SetAmountToStore(_storeAmount);

        if (manualAmountInput != null)
            manualAmountInput.onValueChanged.AddListener(OnManualAmountChanged);

        if (storeAllButton != null)
            storeAllButton.onClick.AddListener(() => SetAmountToStore(Mathf.Min(_maxAmount, _availableCapacity)));

        if (storeHalfButton != null)
            storeHalfButton.onClick.AddListener(() =>
            {
                int halfAmount = Mathf.CeilToInt(_maxAmount / 2f);
                int halfCap = Mathf.CeilToInt(_availableCapacity / 2f);
                SetAmountToStore(Mathf.Min(halfAmount, halfCap));
            });

        if (storeOneButton != null)
            storeOneButton.onClick.AddListener(() => SetAmountToStore(Mathf.Min(1, _availableCapacity)));

        if (confirmButton != null)
            confirmButton.onClick.AddListener(StoreResource);

        UpdateStorageCapacityText();
        UpdateConfirmButtonState();
    }

    private void OnManualAmountChanged(string newValue)
    {
        if (_suppressInput) return;

        if (!int.TryParse(newValue, out int newAmount))
            newAmount = 0;

        int clamped = Mathf.Clamp(newAmount, 0, _maxAmount);
        clamped = Mathf.Min(clamped, _availableCapacity);

        _storeAmount = clamped;

        string target = _storeAmount.ToString();
        if (manualAmountInput != null && manualAmountInput.text != target)
        {
            _suppressInput = true;
            manualAmountInput.text = target;
            _suppressInput = false;
        }

        UpdateConfirmButtonState();
    }

    private void SetAmountToStore(int amount)
    {
        amount = Mathf.Max(0, amount);
        amount = Mathf.Min(amount, _maxAmount);
        amount = Mathf.Min(amount, _availableCapacity);

        _storeAmount = amount;

        if (manualAmountInput != null)
        {
            _suppressInput = true;
            manualAmountInput.text = _storeAmount.ToString();
            _suppressInput = false;
        }

        UpdateConfirmButtonState();
    }

    private void UpdateStorageCapacityText()
    {
        if (storageCapacityText != null)
            storageCapacityText.text = $"{_availableCapacity}";
    }

    private void UpdateConfirmButtonState()
    {
        if (confirmButton == null) return;

        int inInv = PlayerInventoryManager.Instance != null ? PlayerInventoryManager.Instance.GetAmount(_def) : 0;

        confirmButton.interactable =
            _storeAmount > 0 &&
            _storeAmount <= _availableCapacity &&
            _storeAmount <= inInv &&
            _storage != null &&
            _storage.CanStoreResource(_def);
    }

    private void StoreResource()
    {
        if (_storage == null || _def == null) return;
        if (PlayerInventoryManager.Instance == null) return;
        if (_storeAmount <= 0) return;

        _availableCapacity = _storage.GetAvailableSpaceForResource(_def);
        if (_storeAmount > _availableCapacity) return;

        int have = PlayerInventoryManager.Instance.GetAmount(_def);
        if (have < _storeAmount) return;

        // Snapshot remaining turns BEFORE remove (for spoilage preservation)
        int baseInterval = _def.spoilageInterval <= 0 ? 0 : Mathf.Max(1, _def.spoilageInterval);
        int baseCurrent = baseInterval;

        if (!_def.nonPerishable)
        {
            var stack = PlayerInventoryManager.Instance.GetStacks(_def.resourceType)
                .FirstOrDefault(s => s != null && s.definition != null && s.definition.resourceID == _def.resourceID);

            if (stack != null)
            {
                if (baseInterval <= 0) baseCurrent = 0;
                else
                {
                    int r = stack.remainingSpoilageTurns;
                    if (r < 0) r = baseInterval;
                    baseCurrent = Mathf.Clamp(r, 0, baseInterval);
                }
            }
        }

        // Remove from inventory
        if (!PlayerInventoryManager.Instance.TryRemove(_def, _storeAmount))
            return;

        // Add to storage (rollback if storage refuses)
        bool stored = _storage.AddResourceWithSpoilage(_def, _storeAmount, baseInterval, baseCurrent);
        if (!stored)
        {
            PlayerInventoryManager.Instance.TryAdd(_def, _storeAmount);
            Debug.LogWarning("Storage refused deposit; rolled back to player inventory.");
            return;
        }

        _availableCapacity = _storage.GetAvailableSpaceForResource(_def);
        UpdateStorageCapacityText();
        UpdateConfirmButtonState();

        if (_panel != null)
            _panel.Refresh();
    }
}