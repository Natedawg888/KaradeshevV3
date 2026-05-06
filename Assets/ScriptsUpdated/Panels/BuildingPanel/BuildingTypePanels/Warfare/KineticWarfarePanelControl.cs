using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class KineticWarfarePanelControl : MonoBehaviour
{
    public event Action OnClose;

    [Header("Roots")]
    [Tooltip("Root object for this panel.")]
    public GameObject root;
    public Button closeButton;

    [Header("Header")]
    public TMP_Text titleText;

    [Header("Top Buttons")]
    public Button managementButton;
    public Button orderButton;

    [Header("Order View")]
    public GameObject orderContentRoot;
    public Button orderBackButton;
    public Transform unitListContent;
    public UnitOrderItemUI unitOrderItemPrefab;

    [Header("Management View")]
    public GameObject managementContentRoot;
    public Button managementBackButton;
    public Transform managementListContent;
    public UnitGroupManagementItemUI managementItemPrefab;

    [Header("Unit Group Panel")]
    public UnitGroupPanelControl unitGroupPanel;

    // --- runtime ---
    private BuildingPanelControl _parentPanel;
    private BuildingControl _building;
    private TileControl _tile;
    private KineticWarfareControl _currentControl;

    private readonly List<UnitOrderItemUI> _spawnedOrderItems = new();
    private readonly List<UnitGroupManagementItemUI> _spawnedManagementItems = new();

    private readonly List<PlayerUnitManager.GroupInfo> _groupBuffer = new();
    private readonly HashSet<MilitiaUnit> _trainableUnitSet = new();

    private CanvasGroup _cg;

    private GameObject RootObject => root != null ? root : gameObject;

    private void Awake()
    {
        // CanvasGroup setup...
        var go = RootObject;
        _cg = go.GetComponent<CanvasGroup>();
        if (_cg == null) _cg = go.AddComponent<CanvasGroup>();

        _cg.alpha = 0f;
        _cg.interactable = false;
        _cg.blocksRaycasts = false;
        go.SetActive(false);

        // Close button
        if (closeButton)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }

        // Top buttons
        if (orderButton != null)
        {
            orderButton.onClick.RemoveAllListeners();
            orderButton.onClick.AddListener(ShowOrderView);
        }

        if (orderBackButton != null)
        {
            orderBackButton.onClick.RemoveAllListeners();
            orderBackButton.onClick.AddListener(HideOrderView);
        }

        if (managementButton != null)
        {
            managementButton.onClick.RemoveAllListeners();
            managementButton.onClick.AddListener(ShowManagementView);
        }

        if (managementBackButton != null)
        {
            managementBackButton.onClick.RemoveAllListeners();
            managementBackButton.onClick.AddListener(HideManagementView);
        }
    }

    public void OpenFor(BuildingControl building, BuildingPanelControl parent, TileControl tile)
    {
        if (building == null)
        {
            Debug.LogError("[KineticWarfarePanel] OpenFor called with null building.");
            return;
        }

        _parentPanel = parent;
        _building = building;
        _tile = tile != null ? tile : building.GetComponentInParent<TileControl>();
        _currentControl = building.GetComponent<KineticWarfareControl>();

        if (_currentControl == null)
        {
            Debug.LogError("[KineticWarfarePanel] Building has no KineticWarfareControl.");
            return;
        }

        if (titleText != null)
        {
            var name = !string.IsNullOrWhiteSpace(building.buildingName)
                ? building.buildingName
                : (BuildingManager.Instance?.GetBuildingByID(building.buildingID)?.buildingName ?? building.buildingID);

            titleText.text = name;
        }

        if (orderContentRoot != null) orderContentRoot.SetActive(false);
        if (managementContentRoot != null) managementContentRoot.SetActive(false);

        if (managementButton != null) managementButton.gameObject.SetActive(true);
        if (orderButton != null) orderButton.gameObject.SetActive(true);

        ClearOrderList();
        ClearManagementList();

        RootObject.SetActive(true);
        if (_cg != null)
        {
            _cg.alpha = 1f;
            _cg.interactable = true;
            _cg.blocksRaycasts = true;
        }
    }

    public void Show(KineticWarfareControl control)
    {
        if (control == null)
        {
            Debug.LogWarning("[KineticWarfarePanel] Show called with null control.");
            return;
        }

        var building = control.GetComponent<BuildingControl>();
        var tile = building ? building.GetComponentInParent<TileControl>() : null;

        OpenFor(building, null, tile);
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

        ClearOrderList();
        ClearManagementList();
        _currentControl = null;

        _parentPanel?.SoftShowFromChild();
        OnClose?.Invoke();
    }

    private void ShowOrderView()
    {
        if (managementButton != null) managementButton.gameObject.SetActive(false);
        if (orderButton != null) orderButton.gameObject.SetActive(false);

        if (managementContentRoot != null) managementContentRoot.SetActive(false);
        if (orderContentRoot != null) orderContentRoot.SetActive(true);

        ClearManagementList();
        RebuildOrderList();
    }

    private void HideOrderView()
    {
        if (orderContentRoot != null) orderContentRoot.SetActive(false);
        if (managementButton != null) managementButton.gameObject.SetActive(true);
        if (orderButton != null) orderButton.gameObject.SetActive(true);

        ClearOrderList();
    }

    private void ClearOrderList()
    {
        foreach (var item in _spawnedOrderItems)
        {
            if (item != null)
                Destroy(item.gameObject);
        }

        _spawnedOrderItems.Clear();
    }

    private void RebuildOrderList()
    {
        ClearOrderList();

        if (_currentControl == null || unitListContent == null || unitOrderItemPrefab == null)
            return;

        var units = _currentControl.GetAvailableTrainableUnits();
        foreach (var u in units)
        {
            var item = Instantiate(unitOrderItemPrefab, unitListContent);
            item.Setup(u, _currentControl);
            _spawnedOrderItems.Add(item);
        }
    }

    private void ShowManagementView()
    {
        if (managementButton != null) managementButton.gameObject.SetActive(false);
        if (orderButton != null) orderButton.gameObject.SetActive(false);

        if (orderContentRoot != null) orderContentRoot.SetActive(false);
        if (managementContentRoot != null) managementContentRoot.SetActive(true);

        ClearOrderList();
        RebuildManagementList();
    }

    private void HideManagementView()
    {
        if (managementContentRoot != null) managementContentRoot.SetActive(false);
        if (managementButton != null) managementButton.gameObject.SetActive(true);
        if (orderButton != null) orderButton.gameObject.SetActive(true);

        ClearManagementList();
    }

    private void ClearManagementList()
    {
        foreach (var item in _spawnedManagementItems)
        {
            if (item != null)
                Destroy(item.gameObject);
        }

        _spawnedManagementItems.Clear();
    }

    private void RebuildManagementList()
    {
        ClearManagementList();

        if (_currentControl == null) return;
        if (managementListContent == null || managementItemPrefab == null) return;

        var unitMgr = PlayerUnitManager.Instance;
        if (unitMgr == null)
        {
            Debug.LogWarning("[KineticWarfarePanel] No PlayerUnitManager; cannot build management list.");
            return;
        }

        _trainableUnitSet.Clear();
        var trainableUnits = _currentControl.GetAvailableTrainableUnits();
        if (trainableUnits == null || trainableUnits.Count == 0)
            return;

        for (int i = 0; i < trainableUnits.Count; i++)
        {
            var u = trainableUnits[i];
            if (u != null)
                _trainableUnitSet.Add(u);
        }

        // ---------- 1) ACTIVE GROUPS ----------
        _groupBuffer.Clear();
        unitMgr.GetAllGroups(_groupBuffer);

        for (int i = 0; i < _groupBuffer.Count; i++)
        {
            var info = _groupBuffer[i];
            if (info.data == null || info.owner == null) continue;
            if (info.data.unitType == null) continue;
            if (!_trainableUnitSet.Contains(info.data.unitType)) continue;

            var item = Instantiate(managementItemPrefab, managementListContent);
            item.Setup(info.data, info.owner, this);
            _spawnedManagementItems.Add(item);
        }

        // ---------- 2) TEMPORARILY DISBANDED GROUPS ----------
        var tempGroups = _currentControl.TemporarilyDisbandedGroups;
        if (tempGroups != null)
        {
            for (int i = 0; i < tempGroups.Count; i++)
            {
                var rec = tempGroups[i];
                if (rec == null || rec.group == null) continue;

                var g = rec.group;
                if (g.unitType == null) continue;
                if (!_trainableUnitSet.Contains(g.unitType)) continue;

                var owner = rec.originalOwner;

                bool canReband = _currentControl.CanRebandTemporarilyDisbandedGroup(g);

                bool canRebandWithoutPregnant = false;
                if (_currentControl.TryEvaluateTemporarilyDisbandedGroupRegroup(g, out var eval, out _))
                    canRebandWithoutPregnant = eval != null && eval.CanRegroupByRemovingPregnant;

                var item = Instantiate(managementItemPrefab, managementListContent);
                item.Setup(
                    g,
                    owner,
                    this,
                    true,
                    canReband,
                    canRebandWithoutPregnant);

                _spawnedManagementItems.Add(item);
            }
        }
    }

    public void OpenUnitGroupPanel(TileUnitGroupData group, TileUnitGroupControl owner)
    {
        if (group == null || owner == null) return;

        FocusCameraOnOwnerTile(owner);

        if (unitGroupPanel != null)
        {
            unitGroupPanel.ShowFor(group, owner, _currentControl, this, _parentPanel);
        }
        else
        {
            Debug.LogWarning("[KineticWarfarePanel] unitGroupPanel reference not set.");
        }
    }

    public void OnRebandGroupClicked(TileUnitGroupData group, TileUnitGroupControl owner)
    {
        if (_currentControl == null || group == null)
            return;

        if (!_currentControl.TryRebandTemporarilyDisbandedGroup(group, owner, out string failReason))
        {
            Debug.LogWarning($"[KineticWarfarePanel] Failed to reband group {group.groupId}: {failReason}");
            return;
        }

        RefreshForSameBuilding();
    }

    public void OnRebandGroupWithoutPregnantClicked(TileUnitGroupData group, TileUnitGroupControl owner)
    {
        if (_currentControl == null || group == null)
            return;

        if (!_currentControl.TryRebandTemporarilyDisbandedGroup(
                group,
                owner,
                KineticWarfareControl.TempRegroupMode.RemovePregnantReservedIdsAndRegroup,
                out string failReason))
        {
            Debug.LogWarning($"[KineticWarfarePanel] Failed to reband group without pregnant population {group.groupId}: {failReason}");
            return;
        }

        RefreshForSameBuilding();
    }

    private void FocusCameraOnOwnerTile(TileUnitGroupControl owner)
    {
        var tile = owner.GetComponentInParent<TileControl>();
        if (tile == null) return;

        var cam = GameObject.FindObjectOfType<CameraControl>();
        if (cam == null)
        {
            Debug.LogWarning("[KineticWarfarePanel] No CameraControl found for focusing on unit group.");
            return;
        }

        cam.SaveCameraPose();

        Vector3 point = tile.transform.position;

        if (cam.mainCamera != null)
            cam.FocusOnPoint(point, cam.mainCamera.transform.forward);
        else
            cam.FocusOnPoint(point, cam.transform.forward);
    }

    public void RefreshForSameBuilding()
    {
        if (_building == null)
            return;

        if (_currentControl == null)
            _currentControl = _building.GetComponent<KineticWarfareControl>();

        if (_currentControl == null)
            return;

        if (titleText != null)
        {
            var name = !string.IsNullOrWhiteSpace(_building.buildingName)
                ? _building.buildingName
                : (BuildingManager.Instance?.GetBuildingByID(_building.buildingID)?.buildingName ?? _building.buildingID);

            if (_tile != null)
            {
                var gp = _tile.GetGridPosition();
                titleText.text = $"{name}  ({gp.x},{gp.y})";
            }
            else
            {
                titleText.text = name;
            }
        }

        if (managementContentRoot != null && managementContentRoot.activeSelf)
            RebuildManagementList();

        if (orderContentRoot != null && orderContentRoot.activeSelf)
            RebuildOrderList();
    }
}