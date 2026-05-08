using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ReligiousSpiritPanelControl : MonoBehaviour
{
    public event Action OnClose;

    [Header("Roots")]
    public GameObject root;
    public Button closeButton;
    public Button backButton;

    [Header("Header")]
    public TMP_Text titleText;

    [Header("List")]
    public Transform spiritListContent;
    public ReligiousSpiritItemUI spiritItemPrefab;

    private CanvasGroup _cg;

    private ReligiousBuildingPanelControl _parentPanel;
    private BuildingControl _building;
    private TileControl _tile;
    private ReligiousBuildingControl _control;

    private readonly List<ReligiousSpiritItemUI> _spawned = new();
    private readonly List<SpiritDefinitionSO> _knownSpiritsBuffer = new();

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
        if (PlayerReligionManager.Instance != null)
            PlayerReligionManager.Instance.ReligionChanged += HandleDataChanged;

        if (PlayerKnownSpiritsManager.Instance != null)
            PlayerKnownSpiritsManager.Instance.OnKnownChanged += HandleDataChanged;
    }

    private void OnDisable()
    {
        if (PlayerReligionManager.Instance != null)
            PlayerReligionManager.Instance.ReligionChanged -= HandleDataChanged;

        if (PlayerKnownSpiritsManager.Instance != null)
            PlayerKnownSpiritsManager.Instance.OnKnownChanged -= HandleDataChanged;

        if (_control != null)
            _control.BuildingReligionChanged -= HandleDataChanged;
    }

    public void OpenFor(BuildingControl building, ReligiousBuildingPanelControl parent, TileControl tile)
    {
        if (building == null)
        {
            //Debug.LogError("[ReligiousSpiritPanel] OpenFor called with null building.");
            return;
        }

        _parentPanel = parent;
        _building = building;
        _tile = tile != null ? tile : building.GetComponentInParent<TileControl>();
        _control = building.GetComponent<ReligiousBuildingControl>();

        if (_control == null)
        {
            //Debug.LogError("[ReligiousSpiritPanel] Building has no ReligiousBuildingControl.");
            return;
        }

        _control.BuildingReligionChanged -= HandleDataChanged;
        _control.BuildingReligionChanged += HandleDataChanged;

        if (titleText != null)
            titleText.text = "Spirits";

        RootObject.SetActive(true);
        if (_cg != null)
        {
            _cg.alpha = 1f;
            _cg.interactable = true;
            _cg.blocksRaycasts = true;
        }

        RebuildList();
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

    private void HandleDataChanged()
    {
        if (_control != null && RootObject.activeInHierarchy)
            RebuildList();
    }

    private void RebuildList()
    {
        ClearList();
        _knownSpiritsBuffer.Clear();

        if (_control == null || spiritListContent == null || spiritItemPrefab == null)
            return;

        IReadOnlyList<SpiritDefinitionSO> affiliated = _control.AffiliatedSpirits;
        if (affiliated == null || affiliated.Count == 0)
            return;

        for (int i = 0; i < affiliated.Count; i++)
        {
            SpiritDefinitionSO spirit = affiliated[i];
            if (spirit != null)
                _knownSpiritsBuffer.Add(spirit);
        }

        _knownSpiritsBuffer.Sort((a, b) =>
            string.Compare(a.displayName, b.displayName, StringComparison.OrdinalIgnoreCase));

        for (int i = 0; i < _knownSpiritsBuffer.Count; i++)
        {
            SpiritDefinitionSO spirit = _knownSpiritsBuffer[i];
            ReligiousSpiritItemUI item = Instantiate(spiritItemPrefab, spiritListContent);
            item.Setup(spirit, _parentPanel, _control, this);
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
