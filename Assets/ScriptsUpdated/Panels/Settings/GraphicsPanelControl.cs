using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GraphicsPanelControl : MonoBehaviour
{
    public static GraphicsPanelControl Instance { get; private set; }

    [Header("Panel")]
    public Button openButton;
    public Button closeButton;
    public GameObject graphicsPanel;

    [Header("Brightness & Gamma")]
    public Slider brightnessSlider;
    public TMP_Text brightnessText;
    public Image brightnessOverlay;

    private const string BrightnessKey = "Brightness";
    private const float DefaultBrightness = 0.0f;
    private const float MaxOverlayAlpha = 0.05f;

    private float _currentBrightness = DefaultBrightness;
    private bool _isUpdatingSlider;

    public float CurrentBrightness => _currentBrightness;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (openButton != null)
        {
            openButton.onClick.RemoveAllListeners();
            openButton.onClick.AddListener(Show);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }

        if (brightnessSlider != null)
        {
            brightnessSlider.onValueChanged.RemoveAllListeners();
            brightnessSlider.onValueChanged.AddListener(OnBrightnessChanged);
        }
    }

    private void Start()
    {
        LoadSettings();
        RefreshUI();

        if (graphicsPanel != null)
            graphicsPanel.SetActive(false);
    }

    public void Show()
    {
        if (graphicsPanel != null)
            graphicsPanel.SetActive(true);

        TileInteraction.SetSelectionEnabled(false);
    }

    public void Hide()
    {
        TileInteraction.SetSelectionEnabled(false);
        TileInteraction.GetInstance()?.EnableSelectionAfter(0.01f);

        if (graphicsPanel != null)
            graphicsPanel.SetActive(false);
    }

    private void OnBrightnessChanged(float value)
    {
        if (_isUpdatingSlider) return;
        _currentBrightness = value;
        ApplyBrightness(value);
        SaveSettings();
        RefreshBrightnessText();
    }

    public void LoadSavedState(float brightness)
    {
        _currentBrightness = Mathf.Clamp01(brightness);
        ApplyBrightness(_currentBrightness);
        SaveSettings();
        RefreshUI();
    }

    private void ApplyBrightness(float value)
    {
        _currentBrightness = value;

        if (brightnessOverlay != null)
        {
            if (value < 0.5f)
            {
                float alpha = Mathf.Min((0.5f - value) * 2f, MaxOverlayAlpha);
                brightnessOverlay.color = new Color(0f, 0f, 0f, alpha);
            }
            else
            {
                float alpha = Mathf.Min((value - 0.5f) * 2f, MaxOverlayAlpha);
                brightnessOverlay.color = new Color(1f, 1f, 1f, alpha);
            }
        }

        // value 0→0x, 0.5→1x, 1→2x intensity
        TurnSystem.Instance?.SetBrightnessMultiplier(value * 2f);
    }

    private void LoadSettings()
    {
        float brightness = PlayerPrefs.GetFloat(BrightnessKey, DefaultBrightness);
        ApplyBrightness(brightness);
    }

    private void SaveSettings()
    {
        PlayerPrefs.SetFloat(BrightnessKey, _currentBrightness);
        PlayerPrefs.Save();
    }

    private void RefreshUI()
    {
        RefreshSliderSilently();
        RefreshBrightnessText();
    }

    private void RefreshSliderSilently()
    {
        if (brightnessSlider == null) return;
        _isUpdatingSlider = true;
        brightnessSlider.minValue = 0f;
        brightnessSlider.maxValue = 1f;
        brightnessSlider.value = PlayerPrefs.GetFloat(BrightnessKey, DefaultBrightness);
        _isUpdatingSlider = false;
    }

    private void RefreshBrightnessText()
    {
        if (brightnessText == null) return;
        float pct = brightnessSlider != null ? brightnessSlider.value : DefaultBrightness;
        int display = Mathf.RoundToInt((pct - 0.5f) * 200f);
        brightnessText.text = display >= 0 ? $"Brightness: +{display}%" : $"Brightness: {display}%";
    }
}
