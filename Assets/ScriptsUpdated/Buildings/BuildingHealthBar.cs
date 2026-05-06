using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BuildingHealthBar : MonoBehaviour
{
    [Header("Bindings")]
    public BuildingHealth source;        // If null, auto-find in parents
    public Slider slider;                // World-space slider
    public TMP_Text valueLabel;          // Optional: “75%” or “150/200”

    public GameObject healthHolderObject;

    [Header("Display")]
    public bool hideWhenFull = true;
    public bool hideWhenDestroyed = true;

    void Awake()
    {

        if (source == null) source = GetComponentInParent<BuildingHealth>();
        if (!slider)
        {
            slider = GetComponentInChildren<Slider>(true);
            if (slider != null) slider.wholeNumbers = false;
        }
    }

    void OnEnable()
    {
        if (source != null)
            source.OnHealthChanged += HandleHealthChanged;

        // Initial draw if we can read current values
        if (source != null)
            HandleHealthChanged(source.CurrentHealth, source.maxHealth);
    }

    void OnDisable()
    {
        if (source != null)
            source.OnHealthChanged -= HandleHealthChanged;
    }

    private void HandleHealthChanged(int current, int max)
    {
        if (!slider || max <= 0) return;

        // Use 0..max slider range (keeps marker movement linear in px)
        slider.minValue = 0f;
        slider.maxValue = max;
        slider.value    = Mathf.Clamp(current, 0, max);

        float pct = max > 0 ? (current / (float)max) : 0f;

        if (valueLabel)
            valueLabel.text = $"{current} / {max}";

        // Hide/show rules
        bool destroyed = (GetComponentInParent<BuildingStatus>()?.CurrentState == BuildingState.Destroyed);
        bool shouldHide =
            (hideWhenFull && pct >= 0.999f) ||
            (hideWhenDestroyed && destroyed);

        healthHolderObject.SetActive(!shouldHide);
    }
}
