using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CollectedItemEntry : MonoBehaviour
{
    [Header("References (match ResourceEntryUI)")]
    public Image iconImage;
    public TMP_Text nameText;
    public TMP_Text amountText;
    public Slider spoilageSlider;          // optional; hidden if not provided

    [Header("Pickup Buttons")]
    public Button takeOneButton;
    public Button takeHalfButton;
    public Button takeAllButton;

    // Backing state
    private EnvironmentControl env;
    private ResourceDefinition def;
    private int amount;
    private CollectedGoodsPanelControl owner;

    private Func<int, int> _takeLootFunc;
    private Action _onChanged;

    public void BindLoot(ResourceDefinition def,
                        int amount,
                        Func<int, int> takeLootFunc,
                        Action onChanged)
    {
        this.def = def;
        this.amount = amount;
        _takeLootFunc = takeLootFunc;
        _onChanged = onChanged;

        if (iconImage != null)
            iconImage.sprite = def != null ? def.resourceIcon : null;

        if (nameText != null)
            nameText.text = def != null ? def.resourceName : "(Unknown)";

        if (amountText != null)
            amountText.text = Mathf.Max(0, amount).ToString();

        if (spoilageSlider != null)
            spoilageSlider.gameObject.SetActive(false);

        WireButtons_Loot();
        RefreshButtons();
    }

    private void WireButtons_Loot()
    {
        if (takeOneButton != null)
        {
            takeOneButton.onClick.RemoveAllListeners();
            takeOneButton.onClick.AddListener(() => TryTake_Loot(1));
        }

        if (takeHalfButton != null)
        {
            takeHalfButton.onClick.RemoveAllListeners();
            takeHalfButton.onClick.AddListener(() => TryTake_Loot(Mathf.Max(1, amount / 2)));
        }

        if (takeAllButton != null)
        {
            takeAllButton.onClick.RemoveAllListeners();
            takeAllButton.onClick.AddListener(() => TryTake_Loot(amount));
        }
    }

    public void Bind(EnvironmentControl env,
                     ResourceDefinition def,
                     int amount,
                     CollectedGoodsPanelControl owner,
                     int spoilageRemaining = -1,
                     int spoilageTotal = -1)
    {
        this.env   = env;
        this.def   = def;
        this.amount= amount;
        this.owner = owner;

        // --- Icon / Name / Amount (same pattern as ResourceEntryUI) ---
        if (iconImage != null)
            iconImage.sprite = def != null ? def.resourceIcon : null;

        if (nameText != null)
            nameText.text = def != null ? def.resourceName : "(Unknown)";

        if (amountText != null)
            amountText.text = Mathf.Max(0, amount).ToString();

        // --- Spoilage (optional) ---
        if (spoilageSlider != null)
        {
            if (spoilageRemaining >= 0 && spoilageTotal > 0)
            {
                spoilageSlider.gameObject.SetActive(true);
                spoilageSlider.minValue     = 0;
                spoilageSlider.maxValue     = spoilageTotal;
                spoilageSlider.wholeNumbers = true;
                spoilageSlider.value        = Mathf.Clamp(spoilageRemaining, 0, spoilageTotal);
                spoilageSlider.interactable = false;
                spoilageSlider.direction    = Slider.Direction.LeftToRight;
            }
            else
            {
                // No spoilage context for pending loot → hide to match ResourceEntryUI’s style gracefully
                spoilageSlider.gameObject.SetActive(false);
            }
        }

        WireButtons();
        RefreshButtons();
    }

    private void WireButtons()
    {
        if (takeOneButton != null)
        {
            takeOneButton.onClick.RemoveAllListeners();
            takeOneButton.onClick.AddListener(() => TryTake(1));
        }

        if (takeHalfButton != null)
        {
            takeHalfButton.onClick.RemoveAllListeners();
            takeHalfButton.onClick.AddListener(() => TryTake(Mathf.Max(1, amount / 2)));
        }

        if (takeAllButton != null)
        {
            takeAllButton.onClick.RemoveAllListeners();
            takeAllButton.onClick.AddListener(() => TryTake(amount));
        }
    }

    private void RefreshButtons()
    {
        bool has = amount > 0;
        if (takeOneButton)  takeOneButton.interactable  = has;
        if (takeHalfButton) takeHalfButton.interactable = has && amount > 1;
        if (takeAllButton)  takeAllButton.interactable  = has;
    }

    private void TryTake(int desired)
    {
        if (env == null || def == null || desired <= 0) return;

        // EnvironmentControl handles capacity and icon/canvas updates internally
        int actuallyTaken = env.TryTakePending(def, desired);
        if (actuallyTaken <= 0) return;

        amount = Mathf.Max(0, amount - actuallyTaken);
        if (amountText) amountText.text = amount.ToString();

        RefreshButtons();

        // Notify the panel to refresh/close when empty
        owner?.OnEntryChanged();
    }

    private void TryTake_Loot(int desired)
    {
        if (def == null || desired <= 0 || _takeLootFunc == null) return;

        int taken = _takeLootFunc.Invoke(desired);
        if (taken <= 0) return;

        amount = Mathf.Max(0, amount - taken);
        if (amountText) amountText.text = amount.ToString();

        RefreshButtons();
        _onChanged?.Invoke();
    }
}