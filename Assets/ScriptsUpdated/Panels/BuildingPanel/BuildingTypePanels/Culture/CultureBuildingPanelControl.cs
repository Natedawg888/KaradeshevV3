using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CultureBuildingPanelControl : MonoBehaviour
{
    public event Action OnClose;

    [Header("Roots")]
    public GameObject root;
    public Button closeButton;

    [Header("Header")]
    public TMP_Text titleText;

    [Header("Content")]
    public Transform contentRoot;
    public CultureEffectEntry entryPrefab;

    [Header("Stat Icons (indexed by CivilizationStat enum order)")]
    [Tooltip("Order: Happiness, Health, Diversity, Integration, Order, Discovery, Knowledge, Faith")]
    public Sprite[] statIcons;

    private CanvasGroup _cg;
    private BuildingPanelControl _parentPanel;
    private BuildingControl _building;
    private TileControl _tile;
    private CultureBuildingControl _control;

    private readonly List<CultureEffectEntry> _entries = new List<CultureEffectEntry>();

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
    }

    public void OpenFor(BuildingControl building, BuildingPanelControl parent, TileControl tile)
    {
        if (building == null)
            return;

        _parentPanel = parent;
        _building = building;
        _tile = tile != null ? tile : building.GetComponentInParent<TileControl>();
        _control = building.GetComponent<CultureBuildingControl>();

        if (_control == null)
            return;

        if (titleText != null)
        {
            string name = !string.IsNullOrWhiteSpace(building.buildingName)
                ? building.buildingName
                : (BuildingManager.Instance?.GetBuildingByID(building.buildingID)?.buildingName ?? building.buildingID);

            titleText.text = name;
        }

        PopulateEntries();

        RootObject.SetActive(true);
        if (_cg != null)
        {
            _cg.alpha = 1f;
            _cg.interactable = true;
            _cg.blocksRaycasts = true;
        }
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

        ClearEntries();

        _control = null;
        _building = null;
        _tile = null;

        _parentPanel?.SoftShowFromChild();
        OnClose?.Invoke();
    }

    private void PopulateEntries()
    {
        ClearEntries();

        if (_control == null || _control.effects == null || contentRoot == null || entryPrefab == null)
            return;

        for (int i = 0; i < _control.effects.Count; i++)
        {
            CultureEffect effect = _control.effects[i];
            if (effect == null)
                continue;

            Sprite icon = GetIconForStat(effect.stat);
            CultureEffectEntry entry = Instantiate(entryPrefab, contentRoot);
            entry.Bind(effect, icon);
            _entries.Add(entry);
        }
    }

    private void ClearEntries()
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            if (_entries[i] != null)
                Destroy(_entries[i].gameObject);
        }
        _entries.Clear();
    }

    private Sprite GetIconForStat(CivilizationStat stat)
    {
        if (statIcons == null)
            return null;

        int index = (int)stat;
        if (index < 0 || index >= statIcons.Length)
            return null;

        return statIcons[index];
    }
}
