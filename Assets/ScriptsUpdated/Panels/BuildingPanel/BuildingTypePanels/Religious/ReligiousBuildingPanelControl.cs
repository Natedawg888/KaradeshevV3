using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ReligiousBuildingPanelControl : MonoBehaviour
{
    public event Action OnOpen;
    public event Action OnClose;

    public bool IsShowing => RootObject != null && RootObject.activeSelf;

    [Header("Roots")]
    public GameObject root;
    public Button closeButton;

    [Header("Header")]
    public TMP_Text titleText;
    public TMP_Text selectedSpiritText;

    [Header("Top Buttons")]
    public Button ritualsButton;
    public Button spiritsButton;

    [Header("Child Panels")]
    public ReligiousRitualPanelControl ritualPanel;
    public ReligiousSpiritPanelControl spiritPanel;

    private CanvasGroup _cg;

    private BuildingPanelControl _parentPanel;
    private BuildingControl _building;
    private TileControl _tile;
    private ReligiousBuildingControl _control;
    private SpiritDefinitionSO _selectedSpirit;

    private GameObject RootObject => root != null ? root : gameObject;

    public BuildingControl CurrentBuilding => _building;
    public TileControl CurrentTile => _tile;
    public ReligiousBuildingControl CurrentControl => _control;
    public SpiritDefinitionSO SelectedSpirit => _selectedSpirit;

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

        if (ritualsButton != null)
        {
            ritualsButton.onClick.RemoveAllListeners();
            ritualsButton.onClick.AddListener(OnClickOpenRituals);
        }

        if (spiritsButton != null)
        {
            spiritsButton.onClick.RemoveAllListeners();
            spiritsButton.onClick.AddListener(OnClickOpenSpirits);
        }
    }

    public void OpenFor(BuildingControl building, BuildingPanelControl parent, TileControl tile)
    {
        if (building == null)
        {
            //Debug.LogError("[ReligiousBuildingPanel] OpenFor called with null building.");
            return;
        }

        _parentPanel = parent;
        _building = building;
        _tile = tile != null ? tile : building.GetComponentInParent<TileControl>();
        _control = building.GetComponent<ReligiousBuildingControl>();

        if (_control == null)
        {
            //Debug.LogError("[ReligiousBuildingPanel] Building has no ReligiousBuildingControl.");
            return;
        }

        _selectedSpirit = _control.GetDefaultSelectedSpirit();

        if (titleText != null)
        {
            string name = !string.IsNullOrWhiteSpace(building.buildingName)
                ? building.buildingName
                : (BuildingManager.Instance?.GetBuildingByID(building.buildingID)?.buildingName ?? building.buildingID);

            titleText.text = name;
        }

        RefreshSelectedSpiritText();

        RootObject.SetActive(true);
        if (_cg != null)
        {
            _cg.alpha = 1f;
            _cg.interactable = true;
            _cg.blocksRaycasts = true;
        }

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

        _control = null;
        _building = null;
        _tile = null;
        _selectedSpirit = null;

        _parentPanel?.SoftShowFromChild();
        OnClose?.Invoke();
    }

    public void SetSelectedSpirit(SpiritDefinitionSO spirit)
    {
        _selectedSpirit = spirit;
        RefreshSelectedSpiritText();
    }

    private void RefreshSelectedSpiritText()
    {
        if (selectedSpiritText == null)
            return;

        if (_selectedSpirit != null)
        {
            selectedSpiritText.text = $"Selected Spirit: {_selectedSpirit.displayName}";
            return;
        }

        selectedSpiritText.text = "Selected Spirit: None";
    }

    private void OnClickOpenRituals()
    {
        if (_building == null || ritualPanel == null)
            return;

        HideSelfForChild();
        ritualPanel.OpenFor(_building, this, _tile);
    }

    private void OnClickOpenSpirits()
    {
        if (_building == null || spiritPanel == null)
            return;

        HideSelfForChild();
        spiritPanel.OpenFor(_building, this, _tile);
    }

    public void SoftShowFromChild()
    {
        RootObject.SetActive(true);

        if (_cg != null)
        {
            _cg.alpha = 1f;
            _cg.interactable = true;
            _cg.blocksRaycasts = true;
        }

        RefreshSelectedSpiritText();
    }

    private void HideSelfForChild()
    {
        if (_cg != null)
        {
            _cg.alpha = 0f;
            _cg.interactable = false;
            _cg.blocksRaycasts = false;
        }
    }
}
