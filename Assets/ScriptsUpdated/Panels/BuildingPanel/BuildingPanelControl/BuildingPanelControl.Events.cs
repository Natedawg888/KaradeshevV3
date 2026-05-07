using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class BuildingPanelControl : MonoBehaviour
{
    // ---- runtime ----
    private BuildingControl currentBuilding;
    private BuildingHealth currentHealth;
    private BuildingStatus currentStatus;
    private BuildingRepair currentRepair;
    private BuildingFireState currentFireState;
    private TileControl currentTile;

    // Cached building-type components
    private ShelterControl currentShelterControl;
    private CraftingBuildingControl currentCraftingControl;
    private ProductionBuildingControl currentProductionControl;
    private KineticWarfareControl currentKineticControl;
    private StorageBuildingControl currentStorageControl;
    private ReligiousBuildingControl currentReligiousControl;
    private BuildingTechnology currentTechnology;

    public event Action OnClose;

    [Header("References")]
    [SerializeField] private CameraControl cameraControl;
    [SerializeField] private BuildingManager buildingManager;
    [SerializeField] private PlayerKnownBuildingsManager knownBuildingsManager;
    [SerializeField] private PlayerLevel playerLevel;

    public bool IsShowing => root != null && root.activeInHierarchy;
    public BuildingControl CurrentBuilding => currentBuilding;
    public Func<BuildingControl, bool> TutorialDestroyOverride;

    private void Start()
    {
        // Header / rename wiring
        if (renameButton != null)
        {
            renameButton.onClick.RemoveAllListeners();
            renameButton.onClick.AddListener(BeginRename);
        }

        if (saveRenameButton != null)
        {
            saveRenameButton.onClick.RemoveAllListeners();
            saveRenameButton.onClick.AddListener(SubmitRename);
        }

        if (cancelRenameButton != null)
        {
            cancelRenameButton.onClick.RemoveAllListeners();
            cancelRenameButton.onClick.AddListener(CancelRename);
        }

        if (renameContainer != null)
            renameContainer.SetActive(false);

        // Close
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }

        if (prevModeButton)
        {
            prevModeButton.onClick.RemoveAllListeners();
            prevModeButton.onClick.AddListener(() =>
            {
                if (!currentBuilding) return;

                currentBuilding.CyclePrevType();
                RefreshModeSpecificButtons();
                RefreshResearchEntryState();
                RefreshUpgradeEntryState();
            });
        }

        if (nextModeButton)
        {
            nextModeButton.onClick.RemoveAllListeners();
            nextModeButton.onClick.AddListener(() =>
            {
                if (!currentBuilding) return;

                currentBuilding.CycleNextType();
                RefreshModeSpecificButtons();
                RefreshResearchEntryState();
                RefreshUpgradeEntryState();
            });
        }

        // Repair button
        if (openRepairButton)
        {
            openRepairButton.onClick.RemoveAllListeners();
            openRepairButton.onClick.AddListener(() =>
            {
                if (!currentBuilding)
                {
                    Debug.LogWarning("[BuildingPanel] No currentBuilding when opening Repair.");
                    return;
                }

                if (!repairPanel)
                {
                    Debug.LogError("[BuildingPanel] RepairPanel reference is missing (assign a SCENE instance).");
                    return;
                }

                repairPanel.OpenFor(currentBuilding);
            });
        }

        // Destroy button
        if (destroyButton != null)
        {
            destroyButton.onClick.RemoveAllListeners();
            destroyButton.onClick.AddListener(OnClickDestroyBuilding);
        }

        // Upgrade
        if (openUpgradeButton)
        {
            openUpgradeButton.onClick.RemoveAllListeners();
            openUpgradeButton.onClick.AddListener(() =>
            {
                if (!currentBuilding)
                {
                    Debug.LogWarning("[BuildingPanel] No currentBuilding when opening Upgrade.");
                    return;
                }

                if (!upgradePanel)
                {
                    Debug.LogError("[BuildingPanel] UpgradePanel reference is missing (assign a SCENE instance).");
                    return;
                }

                upgradePanel.OpenFor(currentBuilding);
            });
        }

        // Research
        if (openResearchButton)
        {
            openResearchButton.onClick.RemoveAllListeners();
            openResearchButton.onClick.AddListener(() =>
            {
                if (!currentBuilding)
                {
                    Debug.LogWarning("[BuildingPanel] No currentBuilding when opening Research.");
                    return;
                }

                if (!researchPanel)
                {
                    Debug.LogError("[BuildingPanel] ResearchPanel reference is missing (assign a SCENE instance).");
                    return;
                }

                researchPanel.OpenFor(currentBuilding);
            });
        }

        // Shelter
        if (openShelterButton)
        {
            openShelterButton.onClick.RemoveAllListeners();
            openShelterButton.onClick.AddListener(() =>
            {
                if (!currentBuilding)
                {
                    Debug.LogWarning("[BuildingPanel] No currentBuilding when opening Shelter.");
                    return;
                }

                if (!shelterPanel)
                {
                    Debug.LogError("[BuildingPanel] ShelterPanel reference is missing (assign a SCENE instance).");
                    return;
                }

                SoftHideForChild();
                shelterPanel.OpenFor(currentBuilding, this, currentTile);
            });
        }

        // Crafting
        if (openCraftingButton)
        {
            openCraftingButton.onClick.RemoveAllListeners();
            openCraftingButton.onClick.AddListener(() =>
            {
                if (!currentBuilding)
                {
                    Debug.LogWarning("[BuildingPanel] No currentBuilding when opening Crafting.");
                    return;
                }

                if (!craftingPanel)
                {
                    Debug.LogError("[BuildingPanel] CraftingPanel reference is missing (assign a SCENE instance).");
                    return;
                }

                SoftHideForChild();
                craftingPanel.OpenFor(currentBuilding, this, currentTile);
            });
        }

        if (openProductionButton)
        {
            openProductionButton.onClick.RemoveAllListeners();
            openProductionButton.onClick.AddListener(() =>
            {
                if (!currentBuilding)
                {
                    Debug.LogWarning("[BuildingPanel] No currentBuilding when opening Production.");
                    return;
                }

                var prod = currentProductionControl != null
                    ? currentProductionControl
                    : currentBuilding.GetComponent<ProductionBuildingControl>();

                if (!prod)
                {
                    Debug.LogError("[BuildingPanel] No ProductionBuildingControl found on currentBuilding.");
                    return;
                }

                bool hasActivePlan = prod.HasActivePlan;

                if (hasActivePlan && producingPanel != null)
                {
                    SoftHideForChild();
                    producingPanel.OpenFor(currentBuilding, this, currentTile);
                }
                else
                {
                    if (productionPanel == null)
                    {
                        Debug.LogError("[BuildingPanel] ProductionPanel reference is missing (assign a SCENE instance).");
                        return;
                    }

                    SoftHideForChild();
                    productionPanel.OpenFor(currentBuilding, this, currentTile);
                }
            });
        }

        ProductionSelectionController.OnSelectionCompleted -= HandleSelectionCompleted;
        ProductionSelectionController.OnSelectionCompleted += HandleSelectionCompleted;

        if (openKineticWarfareButton)
        {
            openKineticWarfareButton.onClick.RemoveAllListeners();
            openKineticWarfareButton.onClick.AddListener(OnClickOpenKineticWarfare);
        }

        if (openStorageButton)
        {
            openStorageButton.onClick.RemoveAllListeners();
            openStorageButton.onClick.AddListener(OnClickOpenStorage);
        }

        if (openReligiousButton)
        {
            openReligiousButton.onClick.RemoveAllListeners();
            openReligiousButton.onClick.AddListener(OnClickOpenReligious);
        }
    }

    private void OnEnable()
    {
        SubscribeKnownBuildings();
    }

    private void OnDisable()
    {
        Unsubscribe();
        ProductionSelectionController.OnSelectionCompleted -= HandleSelectionCompleted;
        UnsubscribeKnownBuildings();
    }

    private void OnDestroy()
    {
        Unsubscribe();
        ProductionSelectionController.OnSelectionCompleted -= HandleSelectionCompleted;
        UnsubscribeKnownBuildings();
    }

    private void HandleKnownBuildingsChanged()
    {
        RefreshUpgradeEntryState();
    }

    // -------- API --------
    public void Show(BuildingControl building, TileControl tile = null)
    {
        if (building == null)
            return;

        TileInteraction.SetSelectionEnabled(false);

        Unsubscribe();

        currentBuilding = building;
        currentHealth = building.GetComponent<BuildingHealth>();
        currentStatus = building.GetComponent<BuildingStatus>();
        currentRepair = building.GetComponent<BuildingRepair>();
        currentFireState = building.GetComponent<BuildingFireState>();
        currentTile = tile != null ? tile : building.GetComponentInParent<TileControl>();

        // Cache optional building-type components once
        currentShelterControl = building.GetComponent<ShelterControl>();
        currentCraftingControl = building.GetComponent<CraftingBuildingControl>();
        currentProductionControl = building.GetComponent<ProductionBuildingControl>();
        currentKineticControl = building.GetComponent<KineticWarfareControl>();
        currentStorageControl = building.GetComponent<StorageBuildingControl>();
        currentReligiousControl = building.GetComponent<ReligiousBuildingControl>();
        currentTechnology = building.GetComponent<BuildingTechnology>();

        if (currentBuilding != null)
            currentBuilding.OnTypeApplied += HandleTypeApplied;

        if (currentStatus != null && currentStatus.CurrentState == BuildingState.Damaged)
        {
            if (damagedPanel != null)
            {
                damagedPanel.Show(currentBuilding, currentTile);
                return;
            }
        }

        if (currentHealth != null)
            currentHealth.OnHealthChanged += HandleHealthChanged;
        if (currentStatus != null)
            currentStatus.OnStateChanged += HandleStateChanged;

        if (currentRepair != null)
        {
            currentRepair.OnRepairStarted += HandleRepairStarted;
            currentRepair.OnRepairProgress += HandleRepairProgress;
            currentRepair.OnRepairCompleted += HandleRepairCompleted;
        }

        if (currentFireState != null)
            currentFireState.OnIgnited += HandleFireIgnited;

        if (fireOverlayPanel != null)
        {
            if (currentFireState != null && currentFireState.IsOnFire)
                fireOverlayPanel.ShowFor(currentBuilding);
            else
                fireOverlayPanel.Hide();
        }

        SoftShowFromChild();

        RefreshHeader();
        RefreshHealthUI();
        RefreshRepairEntryState();
        RefreshRepairProgressUI();
        RefreshDestroyButton();
        RefreshUpgradeEntryState();
        RefreshResearchEntryState();
        RefreshModeSpecificButtons();

        cameraControl?.PushInputLock();

        if (renameContainer != null)
            renameContainer.SetActive(false);
        if (renameButton != null)
            renameButton.gameObject.SetActive(true);
    }

    public void ReopenForCurrent()
    {
        if (!currentBuilding)
            return;

        Show(currentBuilding, currentTile);
    }

    // -------- internal helpers --------
    private void Unsubscribe()
    {
        if (currentBuilding != null)
            currentBuilding.OnTypeApplied -= HandleTypeApplied;

        if (currentHealth != null)
            currentHealth.OnHealthChanged -= HandleHealthChanged;
        if (currentStatus != null)
            currentStatus.OnStateChanged -= HandleStateChanged;
        if (currentRepair != null)
        {
            currentRepair.OnRepairStarted -= HandleRepairStarted;
            currentRepair.OnRepairProgress -= HandleRepairProgress;
            currentRepair.OnRepairCompleted -= HandleRepairCompleted;
        }

        if (currentFireState != null)
            currentFireState.OnIgnited -= HandleFireIgnited;

        currentHealth = null;
        currentStatus = null;
        currentRepair = null;
        currentFireState = null;
        currentShelterControl = null;
        currentCraftingControl = null;
        currentProductionControl = null;
        currentKineticControl = null;
        currentStorageControl = null;
        currentReligiousControl = null;
        currentTechnology = null;
    }

    // -------- event handlers --------
    private void HandleHealthChanged(int current, int max)
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = max;
            healthSlider.value = current;
        }

        if (healthValueText != null)
            healthValueText.text = $"{current}/{max}";

        RefreshRepairEntryState();
        RefreshRepairProgressUI();
    }

    private void HandleStateChanged(BuildingState s)
    {
        if (s == BuildingState.Damaged)
        {
            Hide();
            if (damagedPanel != null && currentBuilding != null)
                damagedPanel.Show(currentBuilding, currentTile);
            return;
        }

        RefreshDestroyButton();
        RefreshRepairEntryState();
        RefreshRepairProgressUI();
        RefreshHealthUI();
        RefreshUpgradeEntryState();
        RefreshModeSpecificButtons();
    }

    private void HandleFireIgnited(BuildingFireState state)
    {
        if (fireOverlayPanel != null && currentBuilding != null)
            fireOverlayPanel.ShowFor(currentBuilding);
    }

    private void HandleTypeApplied(BuildingType t)
    {
        // Refresh cached typed components in case mode/application changed behavior
        if (currentBuilding != null)
        {
            currentShelterControl = currentBuilding.GetComponent<ShelterControl>();
            currentCraftingControl = currentBuilding.GetComponent<CraftingBuildingControl>();
            currentProductionControl = currentBuilding.GetComponent<ProductionBuildingControl>();
            currentKineticControl = currentBuilding.GetComponent<KineticWarfareControl>();
            currentStorageControl = currentBuilding.GetComponent<StorageBuildingControl>();
            currentReligiousControl = currentBuilding.GetComponent<ReligiousBuildingControl>();
            currentTechnology = currentBuilding.GetComponent<BuildingTechnology>();
        }

        RefreshModeSpecificButtons();
        RefreshResearchEntryState();
        RefreshUpgradeEntryState();
    }

    private void HandleSelectionCompleted(ProductionBuildingControl building, ProductionPlan plan)
    {
        if (!currentBuilding || building == null)
            return;

        if (building.gameObject != currentBuilding.gameObject)
            return;

        if (producingPanel == null)
        {
            Debug.LogWarning("[BuildingPanel] Selection completed, but producingPanel is not assigned.");
            return;
        }

        if (productionPanel != null)
            productionPanel.Hide();

        SoftHideForChild();
        producingPanel.OpenFor(currentBuilding, this, currentTile);
    }

    public void OnClickOpenStorage()
    {
        if (!currentBuilding)
        {
            Debug.LogWarning("[BuildingPanel] No currentBuilding when opening Storage.");
            return;
        }

        if (!storagePanel)
        {
            Debug.LogError("[BuildingPanel] StoragePanel reference is missing (assign a SCENE instance).");
            return;
        }

        var storage = currentStorageControl != null
            ? currentStorageControl
            : currentBuilding.GetComponent<StorageBuildingControl>();

        if (!storage)
        {
            Debug.LogWarning("[BuildingPanel] No StorageBuildingControl on this building.");
            return;
        }

        SoftHideForChild();
        storagePanel.OpenFor(currentBuilding, this, currentTile);
    }

    private void SubscribeKnownBuildings()
    {
        if (knownBuildingsManager != null)
            knownBuildingsManager.OnKnownBuildingsChanged += HandleKnownBuildingsChanged;
    }

    private void UnsubscribeKnownBuildings()
    {
        if (knownBuildingsManager != null)
            knownBuildingsManager.OnKnownBuildingsChanged -= HandleKnownBuildingsChanged;
    }

    public void InstallRuntimeRefs(
        CameraControl newCameraControl = null,
        BuildingManager newBuildingManager = null,
        PlayerKnownBuildingsManager newKnownBuildingsManager = null,
        PlayerLevel newPlayerLevel = null,
        bool refreshIfShowing = true)
    {
        bool knownManagerChanged =
            newKnownBuildingsManager != null &&
            knownBuildingsManager != newKnownBuildingsManager;

        if (knownManagerChanged)
            UnsubscribeKnownBuildings();

        if (newCameraControl != null)
            cameraControl = newCameraControl;

        if (newBuildingManager != null)
            buildingManager = newBuildingManager;

        if (newKnownBuildingsManager != null)
            knownBuildingsManager = newKnownBuildingsManager;

        if (newPlayerLevel != null)
            playerLevel = newPlayerLevel;

        if (isActiveAndEnabled && knownManagerChanged)
            SubscribeKnownBuildings();

        if (refreshIfShowing && currentBuilding != null)
        {
            RefreshHeader();
            RefreshDestroyButton();
            RefreshUpgradeEntryState();
            RefreshResearchEntryState();
            RefreshModeSpecificButtons();
        }
    }
}