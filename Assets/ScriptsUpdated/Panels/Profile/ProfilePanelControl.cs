using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class ProfilePanelControl : MonoBehaviour
{
    public static ProfilePanelControl Instance { get; private set; }

    [Header("Profile Button")]
    public Button openProfileButton;
    public Button closeProfileButton;

    [Header("Profile Panel")]
    public GameObject profilePanel;

    [Header("Profile Picture Panel")]
    public GameObject profilePictureMenuPanel;
    public Button openProfilePictureMenuButton;
    public Button closeProfilePictureMenuButton;
    public Button randomizeAvatarButton;

    [Header("Civilization Name")]
    public TMP_InputField civilizationNameInput;
    public TMP_Text titleText;
    public TMP_Text mainCanvasCivilizationText;

    [Header("Player Name")]
    public TMP_InputField playerNameInput;
    public TMP_Text mainCanvasPlayerText;

    [Header("Map Info")]
    [SerializeField] private TMP_Text environmentPresetText;

    [Header("Navigation")]
    public Button returnToTitleButton;
    [SerializeField] private string titleSceneName = "TitleScene";

    [Header("Support")]
    public Button patreonButton;
    [SerializeField] private string patreonUrl = "https://www.patreon.com/c/celtstudio/";
    public Button facebookButton;
    [SerializeField] private string facebookUrl = "https://www.facebook.com/celtstudio/";

    [Header("Profile Image")]
    public Image profileImage;

    [Header("Avatar Selection")]
    public List<Sprite> availableAvatars;

    public string CurrentAvatarName { get; private set; }

    public GameObject avatarItemPrefab;
    public Transform avatarGridParent;

    [Header("Limits")]
    [Tooltip("Max characters allowed for civilization name.")]
    public int maxCivilizationNameLength = 20;
    [Tooltip("Max characters allowed for player name.")]
    public int maxPlayerNameLength = 16;

    [Header("External")]
    public ProfilePicSelector profilePicSelector;

    public CameraControl cameraControl;

    private string _pendingAvatarName;

    private EnvironmentPresetManager environmentPresetManager;

    public bool IsShowing => profilePanel != null && profilePanel.activeInHierarchy;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (civilizationNameInput != null)
        {
            civilizationNameInput.characterLimit = maxCivilizationNameLength;
            civilizationNameInput.onValueChanged.AddListener(UpdateCivilizationName);
        }

        if (playerNameInput != null)
        {
            playerNameInput.characterLimit = maxPlayerNameLength;
            playerNameInput.onValueChanged.AddListener(UpdatePlayerName);
        }

        if (openProfileButton != null)
        {
            openProfileButton.onClick.RemoveAllListeners();
            openProfileButton.onClick.AddListener(ShowProfilePanel);
        }

        if (closeProfileButton != null)
        {
            closeProfileButton.onClick.RemoveAllListeners();
            closeProfileButton.onClick.AddListener(HideProfilePanel);
        }
        else
        {
            //Debug.LogWarning("closeProfileButton is not assigned!");
        }

        if (openProfilePictureMenuButton != null)
        {
            openProfilePictureMenuButton.onClick.RemoveAllListeners();
            openProfilePictureMenuButton.onClick.AddListener(ShowProfilePictureMenu);
        }

        if (closeProfilePictureMenuButton != null)
        {
            closeProfilePictureMenuButton.onClick.RemoveAllListeners();
            closeProfilePictureMenuButton.onClick.AddListener(HideProfilePictureMenu);
        }

        if (randomizeAvatarButton != null)
        {
            randomizeAvatarButton.onClick.RemoveAllListeners();
            randomizeAvatarButton.onClick.AddListener(RandomizeCurrentAvatar);
        }

        if (returnToTitleButton != null)
        {
            returnToTitleButton.onClick.RemoveAllListeners();
            returnToTitleButton.onClick.AddListener(ReturnToTitleScreen);
        }

        if (patreonButton != null)
        {
            patreonButton.onClick.RemoveAllListeners();
            patreonButton.onClick.AddListener(OpenPatreonPage);
        }

        if (facebookButton != null)
        {
            facebookButton.onClick.RemoveAllListeners();
            facebookButton.onClick.AddListener(OpenFacebookPage);
        }

        if (cameraControl == null)
            cameraControl = FindObjectOfType<CameraControl>();
    }

    public string CivilizationName =>
        civilizationNameInput != null ? civilizationNameInput.text : string.Empty;

    public string PlayerName =>
        playerNameInput != null ? playerNameInput.text : string.Empty;

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void ShowProfilePanel()
    {
        RefreshEnvironmentPresetText();

        if (profilePanel != null)
            profilePanel.SetActive(true);

        if (profilePictureMenuPanel != null)
            profilePictureMenuPanel.SetActive(false);

        if (cameraControl != null)
            cameraControl.PushInputLock();

        TileInteraction.SetSelectionEnabled(false);

        if (closeProfileButton != null)
            closeProfileButton.gameObject.SetActive(true);
    }

    public void HideProfilePanel()
    {
        TileInteraction.SetSelectionEnabled(false);
        TileInteraction.GetInstance()?.EnableSelectionAfter(0.01f);

        if (cameraControl != null)
            cameraControl.PopInputLock();

        if (profilePanel != null)
            profilePanel.SetActive(false);
    }

    private void ShowProfilePictureMenu()
    {
        PopulateAvatarMenu();

        if (profilePictureMenuPanel != null)
            profilePictureMenuPanel.SetActive(true);

        if (closeProfilePictureMenuButton != null)
            closeProfilePictureMenuButton.gameObject.SetActive(true);
    }

    public void HideProfilePictureMenu()
    {
        if (profilePictureMenuPanel != null)
            profilePictureMenuPanel.SetActive(false);

        if (closeProfilePictureMenuButton != null)
            closeProfilePictureMenuButton.gameObject.SetActive(false);
    }

    public void SetAvailableAvatars(List<Sprite> newList, bool repopulate = true, bool preserveSelectionByName = true, string fallbackName = null)
    {
        if (newList == null)
            return;

        string targetName = preserveSelectionByName
            ? (!string.IsNullOrEmpty(_pendingAvatarName) ? _pendingAvatarName : CurrentAvatarName)
            : null;

        availableAvatars = newList;

        if (repopulate)
            PopulateAvatarMenu(shuffle: true);

        if (!string.IsNullOrEmpty(targetName))
        {
            Sprite match = availableAvatars?.Find(s => s != null && s.name == targetName);
            if (match != null)
            {
                OnAvatarSelected(match);
                return;
            }
        }

        if (!string.IsNullOrEmpty(fallbackName))
        {
            Sprite fallback = availableAvatars?.Find(s => s != null && s.name == fallbackName);
            if (fallback != null)
            {
                OnAvatarSelected(fallback);
                return;
            }
        }

        if (availableAvatars != null && availableAvatars.Count > 0)
        {
            Sprite chosen = availableAvatars[0];
            if (chosen != null)
                OnAvatarSelected(chosen);
        }
    }

    public void PopulateAvatarMenu(bool shuffle = true, GameObject overridePrefab = null)
    {
        if (avatarGridParent == null || (avatarItemPrefab == null && overridePrefab == null) || availableAvatars == null)
            return;

        for (int i = avatarGridParent.childCount - 1; i >= 0; i--)
        {
            GameObject child = avatarGridParent.GetChild(i).gameObject;
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }

        GameObject prefabToUse = overridePrefab != null ? overridePrefab : avatarItemPrefab;

        List<Sprite> source = availableAvatars;
        if (shuffle)
        {
            source = new List<Sprite>(availableAvatars);
            for (int i = source.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (source[i], source[j]) = (source[j], source[i]);
            }
        }

        foreach (Sprite avatar in source)
        {
            GameObject go = Instantiate(prefabToUse, avatarGridParent);
            ProfilePicItem item = go.GetComponent<ProfilePicItem>();
            if (item == null)
            {
                //Debug.LogWarning("Avatar item prefab missing ProfilePicItem component.");
                continue;
            }

            item.Setup(avatar, OnAvatarSelected);
        }
    }

    public void ApplyAvatarItemPrefab(GameObject newPrefab, bool repopulate = true, bool shuffle = false)
    {
        if (newPrefab != null)
            avatarItemPrefab = newPrefab;

        if (repopulate)
            PopulateAvatarMenu(shuffle, avatarItemPrefab);
    }

    private void OnAvatarSelected(Sprite avatarSprite)
    {
        if (avatarSprite == null)
            return;

        if (profileImage != null)
            profileImage.sprite = avatarSprite;

        profilePicSelector?.SetProfilePicture(avatarSprite);

        CurrentAvatarName = avatarSprite.name;
        _pendingAvatarName = null;

        HideProfilePictureMenu();

        MarkCoreSystemsDirty();
    }

    private void RandomizeCurrentAvatar()
    {
        if (availableAvatars == null || availableAvatars.Count == 0)
            return;

        int idx = Random.Range(0, availableAvatars.Count);
        OnAvatarSelected(availableAvatars[idx]);

        MarkCoreSystemsDirty();
    }

    private void UpdateCivilizationName(string newName)
    {
        if (newName.Length > maxCivilizationNameLength)
            newName = newName.Substring(0, maxCivilizationNameLength);

        if (mainCanvasCivilizationText != null)
            mainCanvasCivilizationText.text = newName;

        if (titleText != null)
            titleText.text = newName;

        MarkCoreSystemsDirty();
    }

    private void UpdatePlayerName(string newName)
    {
        if (newName.Length > maxPlayerNameLength)
            newName = newName.Substring(0, maxPlayerNameLength);

        if (mainCanvasPlayerText != null)
            mainCanvasPlayerText.text = newName;

        MarkCoreSystemsDirty();
    }

    public PlayerProfileSaveData SaveState()
    {
        return new PlayerProfileSaveData
        {
            civilizationName = civilizationNameInput != null ? civilizationNameInput.text : string.Empty,
            playerName = playerNameInput != null ? playerNameInput.text : string.Empty,
            avatarName = CurrentAvatarName
        };
    }

    public void LoadState(PlayerProfileSaveData data)
    {
        if (data == null)
            return;

        string civName = data.civilizationName ?? string.Empty;
        string pName = data.playerName ?? string.Empty;

        if (civilizationNameInput != null)
            civilizationNameInput.SetTextWithoutNotify(civName);
        UpdateCivilizationName(civName);

        if (playerNameInput != null)
            playerNameInput.SetTextWithoutNotify(pName);
        UpdatePlayerName(pName);

        CurrentAvatarName = data.avatarName;
        _pendingAvatarName = data.avatarName;

        ApplySavedAvatarByName(data.avatarName);
    }

    private void ApplySavedAvatarByName(string avatarName)
    {
        if (string.IsNullOrWhiteSpace(avatarName))
            return;

        CurrentAvatarName = avatarName;
        _pendingAvatarName = avatarName;

        if (availableAvatars == null || availableAvatars.Count == 0)
            return;

        Sprite match = availableAvatars.Find(s => s != null && s.name == avatarName);
        if (match == null)
        {
            //Debug.LogWarning($"[PROFILE SAVE] Could not find avatar '{avatarName}' in availableAvatars.");
            return;
        }

        if (profileImage != null)
            profileImage.sprite = match;

        profilePicSelector?.SetProfilePicture(match);
        CurrentAvatarName = match.name;
        _pendingAvatarName = null;
    }

    private void MarkCoreSystemsDirty()
    {
        SaveSystem.MarkSectionDirty(SaveSectionKeys.CoreSystems);
    }

    public void OpenPatreonPage()
    {
        if (!string.IsNullOrWhiteSpace(patreonUrl))
            Application.OpenURL(patreonUrl);
    }

    public void OpenFacebookPage()
    {
        if (!string.IsNullOrWhiteSpace(facebookUrl))
            Application.OpenURL(facebookUrl);
    }

    public void InstallRuntimeRefs(CameraControl newCameraControl = null, EnvironmentPresetManager newEnvironmentPresetManager = null)
    {
        if (newCameraControl != null)
            cameraControl = newCameraControl;

        if (newEnvironmentPresetManager != null)
            environmentPresetManager = newEnvironmentPresetManager;

        RefreshEnvironmentPresetText();
    }

    public void RefreshEnvironmentPresetText()
    {
        if (environmentPresetText == null)
            return;

        if (environmentPresetManager == null)
        {
            environmentPresetText.text = "Environment: Unknown";
            return;
        }

        environmentPresetText.text = environmentPresetManager.GetCurrentPresetName();
    }

    public void ReturnToTitleScreen()
    {
        if (string.IsNullOrWhiteSpace(titleSceneName))
        {
            //Debug.LogError("[ProfilePanelControl] Title scene name is empty.");
            return;
        }

        if (profilePictureMenuPanel != null)
            profilePictureMenuPanel.SetActive(false);

        if (profilePanel != null)
            profilePanel.SetActive(false);

        TileInteraction.SetSelectionEnabled(false);

        if (cameraControl != null)
            cameraControl.PopInputLock();

        SceneManager.LoadScene(titleSceneName, LoadSceneMode.Single);
    }

    public void ApplyNewGameSetup(string civilizationName, string playerName, string avatarName)
    {
        if (civilizationNameInput != null)
            civilizationNameInput.SetTextWithoutNotify(civilizationName ?? string.Empty);
        UpdateCivilizationName(civilizationName ?? string.Empty);

        if (playerNameInput != null)
            playerNameInput.SetTextWithoutNotify(playerName ?? string.Empty);
        UpdatePlayerName(playerName ?? string.Empty);

        if (!string.IsNullOrWhiteSpace(avatarName))
        {
            CurrentAvatarName = avatarName;
            _pendingAvatarName = avatarName;
            ApplySavedAvatarByName(avatarName);
        }
    }
}
