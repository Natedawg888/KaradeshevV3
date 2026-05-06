using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class EnvironmentPresetMenuOption
{
    public string presetName;
    public int presetID;
}

public class MainMenuNewGameSetupPanel : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject panelRoot;

    [Header("Inputs")]
    [SerializeField] private TMP_InputField playerNameInput;
    [SerializeField] private TMP_InputField civilizationNameInput;
    [SerializeField] private TMP_Dropdown environmentPresetDropdown;

    [Header("Name Limits")]
    [SerializeField] private int playerNameCharacterLimit = 16;
    [SerializeField] private int civilizationNameCharacterLimit = 20;

    [Header("Preset Dropdown")]
    [SerializeField] private bool includeRandomPresetOption = true;
    [SerializeField] private string randomPresetLabel = "Random Preset";

    [Header("Avatar Preview")]
    [SerializeField] private Image profileImage;
    [SerializeField] private List<Sprite> availableAvatars = new();

    [Header("Avatar Selection UI")]
    [SerializeField] private Button openAvatarSelectionButton;
    [SerializeField] private Button closeAvatarSelectionButton;
    [SerializeField] private GameObject avatarSelectionPanel;
    [SerializeField] private Transform avatarOptionsContent;
    [SerializeField] private GameObject avatarOptionButtonPrefab;
    [SerializeField] private Button randomizeAvatarButton;

    [Header("Tutorial Toggle")]
    [SerializeField] private Toggle tutorialToggle;
    [SerializeField] private Image tutorialToggleBackground;
    [SerializeField] private TMP_Text tutorialToggleLabel;
    [SerializeField] private bool defaultTutorialEnabled = true;
    [SerializeField] private string tutorialEnabledText = "Tutorial: ON";
    [SerializeField] private string tutorialDisabledText = "Tutorial: OFF";
    [SerializeField] private Color tutorialEnabledColor = new Color(0.20f, 0.70f, 0.35f, 1f);
    [SerializeField] private Color tutorialDisabledColor = new Color(0.30f, 0.30f, 0.30f, 1f);

    [Header("Preset Options")]
    [SerializeField] private List<EnvironmentPresetMenuOption> presetOptions = new();

    [Header("Buttons")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    private System.Action<NewGameSetupData> _onConfirm;
    private string _currentAvatarName;
    private bool _avatarOptionsBuilt;
    private bool _initialized;

    public void Initialize(System.Action<NewGameSetupData> onConfirm)
    {
        _onConfirm = onConfirm;

        if (playerNameInput != null)
        {
            playerNameInput.characterLimit = Mathf.Max(0, playerNameCharacterLimit);
            playerNameInput.onValueChanged.RemoveListener(OnAnyNameChanged);
            playerNameInput.onValueChanged.AddListener(OnAnyNameChanged);
        }

        if (civilizationNameInput != null)
        {
            civilizationNameInput.characterLimit = Mathf.Max(0, civilizationNameCharacterLimit);
            civilizationNameInput.onValueChanged.RemoveListener(OnAnyNameChanged);
            civilizationNameInput.onValueChanged.AddListener(OnAnyNameChanged);
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(Confirm);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(Hide);
        }

        if (openAvatarSelectionButton != null)
        {
            openAvatarSelectionButton.onClick.RemoveAllListeners();
            openAvatarSelectionButton.onClick.AddListener(ShowAvatarSelectionPanel);
        }

        if (closeAvatarSelectionButton != null)
        {
            closeAvatarSelectionButton.onClick.RemoveAllListeners();
            closeAvatarSelectionButton.onClick.AddListener(HideAvatarSelectionPanel);
        }

        if (randomizeAvatarButton != null)
        {
            randomizeAvatarButton.onClick.RemoveAllListeners();
            randomizeAvatarButton.onClick.AddListener(RandomizeAvatar);
        }

        if (tutorialToggle != null)
        {
            tutorialToggle.onValueChanged.RemoveListener(OnTutorialToggleChanged);
            tutorialToggle.onValueChanged.AddListener(OnTutorialToggleChanged);

            if (!_initialized)
                tutorialToggle.isOn = defaultTutorialEnabled;
        }

        BuildPresetDropdown();
        BuildAvatarOptionsIfNeeded();

        if (avatarSelectionPanel != null)
            avatarSelectionPanel.SetActive(false);

        if (availableAvatars != null && availableAvatars.Count > 0)
        {
            if (profileImage == null || profileImage.sprite == null)
                SetAvatar(availableAvatars[0]);
        }

        RefreshTutorialToggleVisuals(IsTutorialEnabled());
        UpdateConfirmButtonState();

        _initialized = true;
    }

    public void Show()
    {
        if (panelRoot != null)
            panelRoot.SetActive(true);

        HideAvatarSelectionPanel();
        RefreshTutorialToggleVisuals(IsTutorialEnabled());
        UpdateConfirmButtonState();
    }

    public void Hide()
    {
        HideAvatarSelectionPanel();

        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (playerNameInput != null)
            playerNameInput.onValueChanged.RemoveListener(OnAnyNameChanged);

        if (civilizationNameInput != null)
            civilizationNameInput.onValueChanged.RemoveListener(OnAnyNameChanged);

        if (tutorialToggle != null)
            tutorialToggle.onValueChanged.RemoveListener(OnTutorialToggleChanged);
    }

    private void OnAnyNameChanged(string _)
    {
        UpdateConfirmButtonState();
    }

    private void OnTutorialToggleChanged(bool isOn)
    {
        RefreshTutorialToggleVisuals(isOn);
    }

    private void RefreshTutorialToggleVisuals(bool isOn)
    {
        if (tutorialToggleBackground != null)
            tutorialToggleBackground.color = isOn ? tutorialEnabledColor : tutorialDisabledColor;

        if (tutorialToggleLabel != null)
            tutorialToggleLabel.text = isOn ? tutorialEnabledText : tutorialDisabledText;
    }

    private bool IsTutorialEnabled()
    {
        return tutorialToggle == null ? defaultTutorialEnabled : tutorialToggle.isOn;
    }

    private void UpdateConfirmButtonState()
    {
        if (confirmButton == null)
            return;

        bool hasPlayerName = !string.IsNullOrWhiteSpace(playerNameInput != null ? playerNameInput.text : string.Empty);
        bool hasCivilizationName = !string.IsNullOrWhiteSpace(civilizationNameInput != null ? civilizationNameInput.text : string.Empty);

        confirmButton.interactable = hasPlayerName && hasCivilizationName;
    }

    private void BuildPresetDropdown()
    {
        if (environmentPresetDropdown == null)
            return;

        environmentPresetDropdown.ClearOptions();

        List<string> names = new();

        if (includeRandomPresetOption)
            names.Add(randomPresetLabel);

        for (int i = 0; i < presetOptions.Count; i++)
        {
            if (presetOptions[i] != null)
            {
                names.Add(
                    string.IsNullOrWhiteSpace(presetOptions[i].presetName)
                        ? $"Preset {presetOptions[i].presetID}"
                        : presetOptions[i].presetName
                );
            }
        }

        environmentPresetDropdown.AddOptions(names);
        environmentPresetDropdown.value = 0;
        environmentPresetDropdown.RefreshShownValue();
    }

    private void BuildAvatarOptionsIfNeeded()
    {
        if (_avatarOptionsBuilt)
            return;

        PopulateAvatarOptions();
        _avatarOptionsBuilt = true;
    }

    private void PopulateAvatarOptions()
    {
        if (avatarOptionsContent == null || avatarOptionButtonPrefab == null)
            return;

        for (int i = avatarOptionsContent.childCount - 1; i >= 0; i--)
        {
            Destroy(avatarOptionsContent.GetChild(i).gameObject);
        }

        if (availableAvatars == null)
            return;

        for (int i = 0; i < availableAvatars.Count; i++)
        {
            Sprite avatarSprite = availableAvatars[i];
            if (avatarSprite == null)
                continue;

            GameObject optionGO = Instantiate(avatarOptionButtonPrefab, avatarOptionsContent);

            Button optionButton = optionGO.GetComponent<Button>();
            if (optionButton == null)
                optionButton = optionGO.GetComponentInChildren<Button>(true);

            Image optionImage = FindBestImageForAvatarOption(optionGO);

            if (optionImage != null)
                optionImage.sprite = avatarSprite;

            Sprite capturedSprite = avatarSprite;

            if (optionButton != null)
            {
                optionButton.onClick.RemoveAllListeners();
                optionButton.onClick.AddListener(() => OnAvatarOptionSelected(capturedSprite));
            }
            else
            {
                Debug.LogWarning("[MainMenuNewGameSetupPanel] Avatar option prefab is missing a Button component.");
            }
        }
    }

    private Image FindBestImageForAvatarOption(GameObject optionGO)
    {
        if (optionGO == null)
            return null;

        Image rootImage = optionGO.GetComponent<Image>();
        if (rootImage != null)
            return rootImage;

        Image childImage = optionGO.GetComponentInChildren<Image>(true);
        return childImage;
    }

    private void ShowAvatarSelectionPanel()
    {
        BuildAvatarOptionsIfNeeded();

        if (avatarSelectionPanel != null)
            avatarSelectionPanel.SetActive(true);
    }

    private void HideAvatarSelectionPanel()
    {
        if (avatarSelectionPanel != null)
            avatarSelectionPanel.SetActive(false);
    }

    private void OnAvatarOptionSelected(Sprite sprite)
    {
        SetAvatar(sprite);
        HideAvatarSelectionPanel();
    }

    private void RandomizeAvatar()
    {
        if (availableAvatars == null || availableAvatars.Count == 0)
            return;

        int idx = Random.Range(0, availableAvatars.Count);
        SetAvatar(availableAvatars[idx]);
        HideAvatarSelectionPanel();
    }

    private void SetAvatar(Sprite sprite)
    {
        if (sprite == null)
            return;

        if (profileImage != null)
            profileImage.sprite = sprite;

        _currentAvatarName = sprite.name;
    }

    private int GetSelectedPresetID()
    {
        if (presetOptions == null || presetOptions.Count == 0)
            return -1;

        int dropdownIndex = environmentPresetDropdown != null ? environmentPresetDropdown.value : 0;

        if (includeRandomPresetOption)
        {
            if (dropdownIndex <= 0)
            {
                int randomIndex = Random.Range(0, presetOptions.Count);
                return presetOptions[randomIndex] != null ? presetOptions[randomIndex].presetID : -1;
            }

            int actualIndex = dropdownIndex - 1;
            if (actualIndex >= 0 && actualIndex < presetOptions.Count && presetOptions[actualIndex] != null)
                return presetOptions[actualIndex].presetID;

            int fallbackRandomIndex = Random.Range(0, presetOptions.Count);
            return presetOptions[fallbackRandomIndex] != null ? presetOptions[fallbackRandomIndex].presetID : -1;
        }

        if (dropdownIndex >= 0 && dropdownIndex < presetOptions.Count && presetOptions[dropdownIndex] != null)
            return presetOptions[dropdownIndex].presetID;

        int fallbackIndex = Random.Range(0, presetOptions.Count);
        return presetOptions[fallbackIndex] != null ? presetOptions[fallbackIndex].presetID : -1;
    }

    private void Confirm()
    {
        if (_onConfirm == null)
            return;

        string playerName = playerNameInput != null ? playerNameInput.text.Trim() : string.Empty;
        string civilizationName = civilizationNameInput != null ? civilizationNameInput.text.Trim() : string.Empty;

        if (string.IsNullOrWhiteSpace(playerName) || string.IsNullOrWhiteSpace(civilizationName))
        {
            UpdateConfirmButtonState();
            return;
        }

        int selectedPresetID = GetSelectedPresetID();

        NewGameSetupData data = new NewGameSetupData
        {
            playerName = playerName,
            civilizationName = civilizationName,
            avatarName = _currentAvatarName,
            selectedPresetID = selectedPresetID,
            includeTutorial = IsTutorialEnabled()
        };

        _onConfirm.Invoke(data);
    }
}