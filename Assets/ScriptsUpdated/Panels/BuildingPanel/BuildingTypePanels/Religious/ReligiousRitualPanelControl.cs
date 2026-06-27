using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ReligiousRitualPanelControl : MonoBehaviour
{
    public event Action OnOpen;
    public event Action OnClose;

    public bool IsShowing => RootObject != null && RootObject.activeSelf;
    public static bool TutorialShowOnlySummoningRitual = false;

    [Header("Roots")]
    public GameObject root;
    public Button closeButton;
    public Button backButton;

    [Header("Header")]
    public TMP_Text titleText;
    public TMP_Text selectedSpiritText;

    [Header("List")]
    public Transform ritualListContent;
    public ReligiousRitualItemUI ritualItemPrefab;

    private CanvasGroup _cg;

    private ReligiousBuildingPanelControl _parentPanel;
    private BuildingControl _building;
    private TileControl _tile;
    private ReligiousBuildingControl _control;

    private readonly List<ReligiousRitualItemUI> _spawned = new();
    private readonly List<ReligionRitualDefinitionSO> _ritualBuffer = new();

    private GameObject RootObject => root != null ? root : gameObject;

    private void Awake()
    {
        var go = RootObject;
        _cg = go.GetComponent<CanvasGroup>();
        if (_cg == null) _cg = go.AddComponent<CanvasGroup>();

        _cg.alpha = 0f;
        _cg.interactable = false;
        _cg.blocksRaycasts = false;
        go.SetActive(false);

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }

        if (backButton != null)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(Hide);
        }
    }

    private void OnEnable()
    {
        if (PlayerKnownRitualsManager.Instance != null)
            PlayerKnownRitualsManager.Instance.KnownRitualsChanged += HandleDataChanged;

        if (PlayerReligionManager.Instance != null)
            PlayerReligionManager.Instance.ReligionChanged += HandleDataChanged;
    }

    private void OnDisable()
    {
        if (PlayerKnownRitualsManager.Instance != null)
            PlayerKnownRitualsManager.Instance.KnownRitualsChanged -= HandleDataChanged;

        if (PlayerReligionManager.Instance != null)
            PlayerReligionManager.Instance.ReligionChanged -= HandleDataChanged;

        if (_control != null)
            _control.BuildingReligionChanged -= HandleDataChanged;
    }

    public void OpenFor(BuildingControl building, ReligiousBuildingPanelControl parent, TileControl tile)
    {
        if (building == null)
        {
            //Debug.LogError("[ReligiousRitualPanel] OpenFor called with null building.");
            return;
        }

        _parentPanel = parent;
        _building = building;
        _tile = tile != null ? tile : building.GetComponentInParent<TileControl>();
        _control = building.GetComponent<ReligiousBuildingControl>();

        if (_control == null)
        {
            //Debug.LogError("[ReligiousRitualPanel] Building has no ReligiousBuildingControl.");
            return;
        }

        _control.BuildingReligionChanged -= HandleDataChanged;
        _control.BuildingReligionChanged += HandleDataChanged;

        if (titleText != null)
            titleText.text = "Rituals";

        RootObject.SetActive(true);
        if (_cg != null)
        {
            _cg.alpha = 1f;
            _cg.interactable = true;
            _cg.blocksRaycasts = true;
        }

        RebuildList();
        OnOpen?.Invoke();
    }

    public void Hide()
    {
        if (_cg != null)
        {
            _cg.alpha = 0f;
            _cg.interactable = false;
            _cg.blocksRaycasts = false;
        }

        RootObject.SetActive(false);
        ClearList();

        if (_control != null)
            _control.BuildingReligionChanged -= HandleDataChanged;

        _control = null;
        _building = null;
        _tile = null;

        _parentPanel?.SoftShowFromChild();
        OnClose?.Invoke();
    }

    public void Refresh()
    {
        if (_control != null)
            RebuildList();
    }

    private void HandleDataChanged()
    {
        if (_control != null && RootObject.activeInHierarchy)
            RebuildList();
    }

    private void RebuildList()
    {
        ClearList();

        if (_control == null)
            return;

        SpiritDefinitionSO selectedSpirit = _parentPanel != null ? _parentPanel.SelectedSpirit : null;

        if (selectedSpiritText != null)
        {
            selectedSpiritText.text = selectedSpirit != null
                ? $"Spirit: {selectedSpirit.displayName}"
                : "Spirit: None selected";
        }

        if (TutorialShowOnlySummoningRitual)
        {
            for (int i = 0; i < _control.ritualOptions.Count; i++)
            {
                ReligionRitualDefinitionSO ritual = _control.ritualOptions[i];
                if (ritual == null || !ritual.IsSummoningRitual) continue;
                if (ritualItemPrefab == null || ritualListContent == null) continue;
                ReligiousRitualItemUI item = Instantiate(ritualItemPrefab, ritualListContent);
                item.Setup(ritual, _control, _parentPanel, this);
                _spawned.Add(item);
            }
            return;
        }

        _control.GetKnownRitualsForSelectedSpirit(selectedSpirit, _ritualBuffer);

        for (int i = 0; i < _ritualBuffer.Count; i++)
        {
            ReligionRitualDefinitionSO ritual = _ritualBuffer[i];
            if (ritual == null || ritualItemPrefab == null || ritualListContent == null)
                continue;

            ReligiousRitualItemUI item = Instantiate(ritualItemPrefab, ritualListContent);
            item.Setup(ritual, _control, _parentPanel, this);
            _spawned.Add(item);
        }
    }

    private void ClearList()
    {
        for (int i = 0; i < _spawned.Count; i++)
        {
            if (_spawned[i] != null)
                Destroy(_spawned[i].gameObject);
        }

        _spawned.Clear();
    }
}
