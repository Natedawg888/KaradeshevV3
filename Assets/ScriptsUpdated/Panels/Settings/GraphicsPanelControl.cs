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
    public Image brightnessOverlay; // full-screen Image on a top-most Canvas layer

    [Header("Quality Buttons")]
    public Button lowQualityButton;
    public Button mediumQualityButton;
    public Button highQualityButton;

    [Header("Button Colors")]
    public Color selectedColor = new Color(0.35f, 0.85f, 0.35f, 1f);
    public Color normalColor = Color.white;

    private const string BrightnessKey = "Brightness";
    private const string QualityKey = "GraphicsQuality";
    private const float DefaultBrightness = 0.5f;
    private const int DefaultQuality = 2; // 0=Low, 1=Med, 2=High

    // Mipmap limits: Low = quarter-res, Med = half-res, High = full-res
    private static readonly int[] MipmapLimits = { 2, 1, 0 };

    private int _currentQuality;
    private float _currentBrightness = DefaultBrightness;
    private bool _isUpdatingSlider;

    public float CurrentBrightness => _currentBrightness;
    public int CurrentQuality => _currentQuality;

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

        if (lowQualityButton != null)
        {
            lowQualityButton.onClick.RemoveAllListeners();
            lowQualityButton.onClick.AddListener(() => SetQuality(0));
        }

        if (mediumQualityButton != null)
        {
            mediumQualityButton.onClick.RemoveAllListeners();
            mediumQualityButton.onClick.AddListener(() => SetQuality(1));
        }

        if (highQualityButton != null)
        {
            highQualityButton.onClick.RemoveAllListeners();
            highQualityButton.onClick.AddListener(() => SetQuality(2));
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

    public void SetQuality(int level)
    {
        _currentQuality = Mathf.Clamp(level, 0, 2);
        ApplyQuality(_currentQuality);
        SaveSettings();
        RefreshQualityButtons();
    }

    private void OnBrightnessChanged(float value)
    {
        if (_isUpdatingSlider) return;
        _currentBrightness = value;
        ApplyBrightness(value);
        SaveSettings();
        RefreshBrightnessText();
    }

    public void LoadSavedState(float brightness, int quality)
    {
        _currentBrightness = Mathf.Clamp01(brightness);
        _currentQuality = Mathf.Clamp(quality, 0, 2);
        ApplyBrightness(_currentBrightness);
        ApplyQuality(_currentQuality);
        SaveSettings();
        RefreshUI();
    }

    private void ApplyBrightness(float value)
    {
        _currentBrightness = value;
        if (brightnessOverlay == null) return;

        // 0.5 = neutral (invisible overlay)
        // < 0.5 = darker (black overlay with increasing alpha)
        // > 0.5 = brighter (white overlay with increasing alpha)
        if (value < 0.5f)
        {
            float alpha = (0.5f - value) * 2f;
            brightnessOverlay.color = new Color(0f, 0f, 0f, alpha);
        }
        else
        {
            float alpha = (value - 0.5f) * 2f;
            brightnessOverlay.color = new Color(1f, 1f, 1f, alpha);
        }
    }

    private void ApplyQuality(int level)
    {
        QualitySettings.masterTextureLimit = MipmapLimits[level];
    }

    private void LoadSettings()
    {
        float brightness = PlayerPrefs.GetFloat(BrightnessKey, DefaultBrightness);
        _currentQuality = PlayerPrefs.GetInt(QualityKey, DefaultQuality);
        ApplyBrightness(brightness);
        ApplyQuality(_currentQuality);
    }

    private void SaveSettings()
    {
        PlayerPrefs.SetFloat(BrightnessKey, _currentBrightness);
        PlayerPrefs.SetInt(QualityKey, _currentQuality);
        PlayerPrefs.Save();
    }

    private void RefreshUI()
    {
        RefreshSliderSilently();
        RefreshBrightnessText();
        RefreshQualityButtons();
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

    private void RefreshQualityButtons()
    {
        SetButtonColor(lowQualityButton, _currentQuality == 0);
        SetButtonColor(mediumQualityButton, _currentQuality == 1);
        SetButtonColor(highQualityButton, _currentQuality == 2);
    }

    private void SetButtonColor(Button btn, bool selected)
    {
        if (btn == null) return;
        var colors = btn.colors;
        colors.normalColor = selected ? selectedColor : normalColor;
        btn.colors = colors;
    }
}
