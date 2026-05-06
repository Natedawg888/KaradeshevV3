using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventoryItemUI : MonoBehaviour
{
    [Header("Refs")]
    public Image icon;
    public TMP_Text nameText;
    public TMP_Text amountText;
    public TMP_Text spaceText;
    public Slider spoilageSlider;

    [Header("Actions")]
    public Button removeOneButton;
    public Button removeHalfButton;
    public Button removeAllButton;

    [Header("Population Consumption")]
    public Button populationConsumeToggleButton;
    public Image populationConsumeToggleImage;
    public Color consumeAllowedColor = Color.green;
    public Color consumeBlockedColor = Color.red;

    private InventoryStack _stack;

    public void Bind(InventoryStack stack)
    {
        _stack = stack;

        var def = stack.definition;
        if (icon) icon.sprite = def.resourceIcon;
        if (nameText) nameText.text = def.resourceName;

        UpdateAmountText();
        UpdateSpaceText();
        SetupPopulationConsumeButton();

        if (def.nonPerishable)
        {
            SetupNoSpoilUI();
        }
        else if (def.spoilageInterval <= 0)
        {
            if (spoilageSlider != null)
                spoilageSlider.gameObject.SetActive(false);
        }
        else
        {
            if (spoilageSlider != null)
            {
                spoilageSlider.gameObject.SetActive(true);
                spoilageSlider.minValue = 0;
                spoilageSlider.maxValue = def.spoilageInterval;
                spoilageSlider.wholeNumbers = true;
                spoilageSlider.value = Mathf.Clamp(_stack.remainingSpoilageTurns, 0, def.spoilageInterval);
            }
        }

        if (removeOneButton != null)
        {
            removeOneButton.onClick.RemoveAllListeners();
            removeOneButton.onClick.AddListener(() =>
            {
                PlayerInventoryManager.Instance?.TryRemove(def, 1);
                UpdateAfterChange();
            });
        }

        if (removeHalfButton != null)
        {
            removeHalfButton.onClick.RemoveAllListeners();
            removeHalfButton.onClick.AddListener(() =>
            {
                PlayerInventoryManager.Instance?.TryRemoveHalf(def);
                UpdateAfterChange();
            });
        }

        if (removeAllButton != null)
        {
            removeAllButton.onClick.RemoveAllListeners();
            removeAllButton.onClick.AddListener(() =>
            {
                PlayerInventoryManager.Instance?.TryRemoveAll(def);
                UpdateAfterChange();
            });
        }
    }

    private void SetupPopulationConsumeButton()
    {
        if (populationConsumeToggleButton == null)
            return;

        bool showButton =
            _stack != null &&
            _stack.definition != null &&
            (_stack.definition.resourceType == ResourceType.Food ||
             _stack.definition.resourceType == ResourceType.Water);

        populationConsumeToggleButton.gameObject.SetActive(showButton);

        if (!showButton)
            return;

        populationConsumeToggleButton.onClick.RemoveAllListeners();
        populationConsumeToggleButton.onClick.AddListener(() =>
        {
            if (_stack == null)
                return;

            bool newAllowed = !_stack.allowPopulationConsumption;
            PlayerInventoryManager.Instance?.SetPopulationConsumptionAllowed(_stack, newAllowed);
            UpdatePopulationConsumeButtonVisual();
        });

        UpdatePopulationConsumeButtonVisual();
    }

    private void UpdatePopulationConsumeButtonVisual()
    {
        if (populationConsumeToggleImage == null || _stack == null)
            return;

        populationConsumeToggleImage.color =
            _stack.allowPopulationConsumption
                ? consumeAllowedColor
                : consumeBlockedColor;
    }

    private void SetupNoSpoilUI()
    {
        if (spoilageSlider)
            spoilageSlider.gameObject.SetActive(false);
    }

    private void UpdateAmountText()
    {
        if (amountText)
            amountText.text = ShortNumberFormatter.Format(_stack.amount);
    }

    private void UpdateSpaceText()
    {
        if (spaceText)
        {
            float space = _stack.amount * _stack.definition.weightPerUnit * _stack.definition.sizePerUnit;
            spaceText.text = ShortNumberFormatter.Format(space);
        }
    }

    private void UpdateAfterChange()
    {
        if (_stack == null || _stack.amount <= 0)
        {
            Destroy(gameObject);
            return;
        }

        UpdateAmountText();
        UpdateSpaceText();
        UpdatePopulationConsumeButtonVisual();

        var def = _stack.definition;
        if (!def.nonPerishable && def.spoilageInterval > 0 && spoilageSlider != null)
        {
            spoilageSlider.value = Mathf.Clamp(_stack.remainingSpoilageTurns, 0, def.spoilageInterval);
        }
    }
}