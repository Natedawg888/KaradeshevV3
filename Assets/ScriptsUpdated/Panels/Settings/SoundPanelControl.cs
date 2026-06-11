using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SoundPanelControl : MonoBehaviour
{
    public static SoundPanelControl Instance { get; private set; }

    [Header("Sound Button")]
    public Button openSoundButton;
    public Button closeSoundButton;

    [Header("Sound Panel")]
    public GameObject soundPanel;

    [Header("Music")]
    public Button toggleMusicButton;
    public Image toggleMusicButtonImage; // optional, falls back to button.image
    public Sprite musicOnSprite;
    public Sprite musicOffSprite;

    [Header("Volume")]
    public Slider volumeSlider;
    public TMP_Text volumeText; // optional

    [Header("Settings")]
    [Range(0.01f, 1f)] public float volumeStep = 0.1f;
    [Range(0f, 1f)] public float defaultVolume = 1f;
    [Range(0f, 1f)] public float toggleRestoreVolume = 0.5f;

    [Header("Refs")]
    public MusicDirector musicDirector;

    private const string MasterVolumeKey = "MasterVolume";
    private const string MusicMutedKey = "MusicMuted";
    private const float ZeroVolumeThreshold = 0.0001f;

    private AudioSource[] _musicSources = Array.Empty<AudioSource>();
    private bool _musicMuted;
    private bool _isUpdatingSlider;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (openSoundButton != null)
        {
            openSoundButton.onClick.RemoveAllListeners();
            openSoundButton.onClick.AddListener(ShowSoundPanel);
        }

        if (closeSoundButton != null)
        {
            closeSoundButton.onClick.RemoveAllListeners();
            closeSoundButton.onClick.AddListener(HideSoundPanel);
        }

        if (toggleMusicButton != null)
        {
            toggleMusicButton.onClick.RemoveAllListeners();
            toggleMusicButton.onClick.AddListener(ToggleMusic);
        }

        if (volumeSlider != null)
        {
            volumeSlider.onValueChanged.RemoveAllListeners();
            volumeSlider.onValueChanged.AddListener(OnSliderVolumeChanged);
        }
    }

    private void Start()
    {
        CacheMusicSources();
        LoadSettings();
        ApplyMusicMute();
        RefreshUI();

        if (soundPanel != null)
            soundPanel.SetActive(false);

        if (closeSoundButton != null)
            closeSoundButton.gameObject.SetActive(false);
    }

    private void CacheMusicSources()
    {
        if (musicDirector == null)
        {
            _musicSources = Array.Empty<AudioSource>();
            return;
        }

        _musicSources = musicDirector.GetComponents<AudioSource>();
    }

    private void ShowSoundPanel()
    {
        if (soundPanel == null)
            return;

        soundPanel.SetActive(true);
        TileInteraction.SetSelectionEnabled(false);

        if (closeSoundButton != null)
            closeSoundButton.gameObject.SetActive(true);
    }

    public void HideSoundPanel()
    {
        TileInteraction.SetSelectionEnabled(false);
        TileInteraction.GetInstance()?.EnableSelectionAfter(0.01f);

        if (soundPanel != null)
            soundPanel.SetActive(false);

        if (closeSoundButton != null)
            closeSoundButton.gameObject.SetActive(false);
    }

    public bool MusicMuted => _musicMuted;

    public void LoadSavedState(bool muted, float volume)
    {
        _musicMuted = muted;
        AudioListener.volume = Mathf.Clamp01(volume);
        ApplyMusicMute();
        SaveSettings();
        RefreshUI();
    }

    public void ToggleMusic()
    {
        // If the slider is at 0 and the player taps the toggle,
        // restore volume to 50% and turn music back on.
        if (IsVolumeZero())
        {
            _musicMuted = false;
            SetMasterVolume(toggleRestoreVolume);
            return;
        }

        _musicMuted = !_musicMuted;
        ApplyMusicMute();
        SaveSettings();
        RefreshUI();
    }

    public void LowerVolume()
    {
        SetMasterVolume(AudioListener.volume - volumeStep);
    }

    public void IncreaseVolume()
    {
        SetMasterVolume(AudioListener.volume + volumeStep);
    }

    public void OnSliderVolumeChanged(float value)
    {
        if (_isUpdatingSlider)
            return;

        SetMasterVolume(value);
    }

    public void SetMasterVolume(float value)
    {
        AudioListener.volume = Mathf.Clamp01(value);

        // Slider at 0 should visually/audio behave like off,
        // but we do not force the player's toggle preference unless needed.
        ApplyMusicMute();
        SaveSettings();
        RefreshUI();
    }

    private bool IsVolumeZero()
    {
        return AudioListener.volume <= ZeroVolumeThreshold;
    }

    private bool IsEffectivelyMuted()
    {
        return _musicMuted || IsVolumeZero();
    }

    private void ApplyMusicMute()
    {
        if (_musicSources == null || _musicSources.Length == 0)
            CacheMusicSources();

        bool shouldMute = IsEffectivelyMuted();

        for (int i = 0; i < _musicSources.Length; i++)
        {
            if (_musicSources[i] != null)
                _musicSources[i].mute = shouldMute;
        }
    }

    private void LoadSettings()
    {
        float savedVolume = PlayerPrefs.GetFloat(MasterVolumeKey, defaultVolume);
        AudioListener.volume = Mathf.Clamp01(savedVolume);

        _musicMuted = PlayerPrefs.GetInt(MusicMutedKey, 0) == 1;
    }

    private void SaveSettings()
    {
        PlayerPrefs.SetFloat(MasterVolumeKey, AudioListener.volume);
        PlayerPrefs.SetInt(MusicMutedKey, _musicMuted ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void RefreshUI()
    {
        if (volumeText != null)
            volumeText.text = $"Volume: {Mathf.RoundToInt(AudioListener.volume * 100f)}%";

        RefreshMusicButtonSprite();
        RefreshSliderSilently();
    }

    private void RefreshMusicButtonSprite()
    {
        Image targetImage = toggleMusicButtonImage;

        if (targetImage == null && toggleMusicButton != null)
            targetImage = toggleMusicButton.image;

        if (targetImage == null)
            return;

        if (IsEffectivelyMuted())
        {
            if (musicOffSprite != null)
                targetImage.sprite = musicOffSprite;
        }
        else
        {
            if (musicOnSprite != null)
                targetImage.sprite = musicOnSprite;
        }
    }

    private void RefreshSliderSilently()
    {
        if (volumeSlider == null)
            return;

        _isUpdatingSlider = true;
        volumeSlider.minValue = 0f;
        volumeSlider.maxValue = 1f;
        volumeSlider.value = AudioListener.volume;
        _isUpdatingSlider = false;
    }
}