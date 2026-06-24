using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BuildingDamagedPanelControl : MonoBehaviour
{
    [Header("UI Roots")]
    public GameObject root;
    public Button closeButton;

    [Header("Header")]
    public TMP_Text titleText;
    public TMP_Text coordinatesText;

    [Header("Repair Panel")]
    public Button openRepairButton;
    public RepairPanelControl repairPanel;

    [Header("Destroy")]
    public Button destroyButton;

    [Header("Repair (Progress UI)")]
    [Tooltip("Container that holds the repair progress visuals (e.g., slider)")]
    public GameObject repairOb;
    [Tooltip("Shows turns remaining; configured as a right-to-left countdown")]
    public Slider repairProgressSlider;

    [Header("Other Panels")]
    [Tooltip("Reference to the main/normal building panel so we can swap to it when building becomes Normal.")]
    public BuildingPanelControl buildingPanel;
    [Tooltip("Reference to the destroyed building panel so we can swap to it when building becomes Destroyed.")]
    public BuildingDestroyedPanelControl destroyedPanel;   // <<< NEW

    public CameraControl cameraControl;

    // --- runtime refs ---
    private BuildingControl  currentBuilding;
    private BuildingStatus   currentStatus;
    private BuildingHealth   currentHealth;
    private BuildingRepair   currentRepair;
    private TileControl      currentTile;

    public bool IsShowing => root != null && root.activeInHierarchy;
    public event Action OnShow;
    public event Action OnClose;

    private void Start()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }

        if (openRepairButton != null)
        {
            openRepairButton.onClick.RemoveAllListeners();
            openRepairButton.onClick.AddListener(OnClickOpenRepair);
        }

        if (destroyButton != null)
        {
            destroyButton.onClick.RemoveAllListeners();
            destroyButton.onClick.AddListener(OnClickDestroyBuilding);
        }

        if (cameraControl == null)
            cameraControl = FindObjectOfType<CameraControl>();

        if (repairOb) repairOb.SetActive(false);
    }

    private void OnDisable() { Unsubscribe(); }
    private void OnDestroy() { Unsubscribe(); }

    // -------- Public API --------
    public void Show(BuildingControl building, TileControl tile = null)
    {
        if (!building) return;

        Unsubscribe(); // clean previous

        currentBuilding = building;
        currentStatus   = building.GetComponent<BuildingStatus>();
        currentHealth   = building.GetComponent<BuildingHealth>();
        currentRepair   = building.GetComponent<BuildingRepair>();
        currentTile     = tile ? tile : building.GetComponentInParent<TileControl>();

        // Must be in Damaged state to show this panel
        if (currentStatus && currentStatus.CurrentState != BuildingState.Damaged)
        {
            Hide();
            return;
        }

        // Subscribe
        if (currentStatus != null)
            currentStatus.OnStateChanged += HandleStateChanged;

        if (currentRepair != null)
        {
            currentRepair.OnRepairStarted   += HandleRepairStarted;
            currentRepair.OnRepairProgress  += HandleRepairProgress;
            currentRepair.OnRepairCompleted += HandleRepairCompleted;
        }

        root.SetActive(true);

        TileInteraction.SetSelectionEnabled(false);

        cameraControl.PushInputLock();

        RefreshHeader();
        RefreshRepairEntryState();
        RefreshDestroyButton();
        RefreshRepairProgressUI();

        OnShow?.Invoke();
    }

    public void Hide()
    {
        TileInteraction.SetSelectionEnabled(false);
        TileInteraction.GetInstance()?.EnableSelectionAfter(0.01f);

        cameraControl.PopInputLock();

        root.SetActive(false);

        Unsubscribe();
        OnClose?.Invoke();

        currentBuilding = null;
        currentStatus   = null;
        currentHealth   = null;
        currentRepair   = null;
        currentTile     = null;

        if (repairOb) repairOb.SetActive(false);
    }

    // -------- Internals --------
    private void Unsubscribe()
    {
        if (currentStatus != null)
            currentStatus.OnStateChanged -= HandleStateChanged;

        if (currentRepair != null)
        {
            currentRepair.OnRepairStarted   -= HandleRepairStarted;
            currentRepair.OnRepairProgress  -= HandleRepairProgress;
            currentRepair.OnRepairCompleted -= HandleRepairCompleted;
        }
    }

    private void RefreshHeader()
    {
        if (!currentBuilding) return;

        string baseName = !string.IsNullOrWhiteSpace(currentBuilding.buildingName)
            ? currentBuilding.buildingName
            : (BuildingManager.Instance?.GetBuildingByID(currentBuilding.buildingID)?.buildingName
               ?? currentBuilding.buildingID);

        if (titleText) titleText.text = $"{baseName} (Damaged)";

        if (coordinatesText)
        {
            Vector2Int coords = Vector2Int.zero;
            if (currentTile != null) coords = currentTile.GetGridPosition();
            coordinatesText.text = $"Coordinates: {coords.x}, {coords.y}";
        }
    }

    private void RefreshRepairEntryState()
    {
        bool hasRepair   = currentBuilding && currentBuilding.GetComponent<BuildingRepair>();
        bool isDamaged   = currentStatus && currentStatus.CurrentState == BuildingState.Damaged;
        bool isRepairing = currentRepair && currentRepair.IsRepairing;

        if (openRepairButton)
        {
            openRepairButton.gameObject.SetActive(hasRepair && isDamaged);
            openRepairButton.interactable = (hasRepair && isDamaged && !isRepairing);
        }
    }

    private void RefreshRepairProgressUI()
    {
        if (!repairOb || !repairProgressSlider) return;

        bool repairing = currentRepair && currentRepair.IsRepairing;
        repairOb.SetActive(repairing);

        if (repairing)
        {
            repairProgressSlider.minValue     = 0;
            repairProgressSlider.maxValue     = Mathf.Max(1, currentRepair.TurnsRemaining);
            repairProgressSlider.value        = currentRepair.TurnsRemaining;
            repairProgressSlider.wholeNumbers = true;
            repairProgressSlider.direction    = Slider.Direction.RightToLeft;
            repairProgressSlider.interactable = false;
        }
    }

    private void RefreshDestroyButton()
    {
        if (!destroyButton) return;

        bool canShow = currentStatus != null;
        destroyButton.gameObject.SetActive(canShow);
        if (!canShow) return;

        bool canDestroy = currentStatus.CurrentState == BuildingState.Damaged;
        destroyButton.interactable = canDestroy;
    }

    private void OnClickOpenRepair()
    {
        if (!currentBuilding)
        {
            //Debug.LogWarning("[BuildingDamagedPanel] No currentBuilding when opening Repair.");
            return;
        }
        if (!repairPanel)
        {
            //Debug.LogError("[BuildingDamagedPanel] RepairPanel reference is missing (assign a SCENE instance, not a prefab).");
            return;
        }

        repairPanel.OpenFor(currentBuilding);
    }

    private void OnClickDestroyBuilding()
    {
        if (currentStatus == null) return;

        if (currentHealth != null)
        {
            currentHealth.ApplyDamage(currentHealth.CurrentHealth);
        }
        else
        {
            currentStatus.SetState(BuildingState.Destroyed);
        }

        if (repairPanel != null) repairPanel.Close();

        RefreshDestroyButton();
        RefreshRepairEntryState();
    }

    // -------- Event Handlers --------
    private void HandleStateChanged(BuildingState s)
    {
        if (s == BuildingState.Normal)
        {
            // swap to main panel
            var tile = currentTile;
            Hide();
            if (buildingPanel != null && currentBuilding != null)
                buildingPanel.Show(currentBuilding, tile);
            return;
        }

        if (s == BuildingState.Destroyed)
        {
            // swap to destroyed panel
            var tile = currentTile;      // stash before Hide() clears it
            Hide();
            if (destroyedPanel != null && currentBuilding != null)
                destroyedPanel.Show(currentBuilding, tile);
            return;
        }

        // still damaged → refresh interactivity
        RefreshDestroyButton();
        RefreshRepairEntryState();
        RefreshRepairProgressUI();
    }

    private void HandleRepairStarted(RepairOption opt, int totalTurns)
    {
        if (openRepairButton) openRepairButton.interactable = false;

        if (repairOb) repairOb.SetActive(true);
        if (repairProgressSlider)
        {
            repairProgressSlider.minValue     = 0;
            repairProgressSlider.maxValue     = totalTurns;
            repairProgressSlider.value        = totalTurns;
            repairProgressSlider.wholeNumbers = true;
            repairProgressSlider.direction    = Slider.Direction.RightToLeft;
            repairProgressSlider.interactable = false;
        }
    }

    private void HandleRepairProgress(int turnsLeft)
    {
        if (repairProgressSlider)
            repairProgressSlider.value = turnsLeft;
    }

    private void HandleRepairCompleted()
    {
        // fully repaired will flip to Normal → handled in HandleStateChanged
        if (repairOb) repairOb.SetActive(false);
        if (openRepairButton) openRepairButton.interactable = true;

        RefreshRepairEntryState();
        RefreshRepairProgressUI();
    }
}
