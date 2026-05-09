using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class BuildingPanelControl : MonoBehaviour
{
    [Header("Mode Switching")]
    public Button prevModeButton;
    public Button nextModeButton;

    [Header("Destroy")]
    public Button destroyButton;

    [Header("Other Panels")]
    [Tooltip("Reference to the Damaged panel so we can swap to it when the building becomes Damaged.")]
    public BuildingDamagedPanelControl damagedPanel;

    [Header("Fire")]
    public BuildingFireOverlayControl fireOverlayPanel;

    [Header("Upgrade")]
    public Button openUpgradeButton;
    public BuildingUpgradePanelControl upgradePanel;

    [Header("Research")]
    public Button openResearchButton;
    public ResearchPanelControl researchPanel;

    [Header("Shelter")]
    public Button openShelterButton;
    public ShelterPanelControl shelterPanel;

    [Header("Crafting")]
    public Button openCraftingButton;
    public CraftingBuildingPanelControl craftingPanel;

    [Header("Production UI")]
    public Button openProductionButton;
    public ProductionBuildingPanelControl productionPanel;
    public ProductionRunningPanelControl producingPanel;

    [Header("Kinetic Warfare")]
    public Button openKineticWarfareButton;
    public KineticWarfarePanelControl kineticWarfarePanel;

    [Header("Storage")]
    public Button openStorageButton;
    public StoragePanelControl storagePanel;

    [Header("Religious")]
    public Button openReligiousButton;
    public ReligiousBuildingPanelControl religiousPanel;

    private void RefreshModeSpecificButtons()
    {
        if (!currentBuilding)
            return;

        bool destroyed = currentStatus && currentStatus.CurrentState == BuildingState.Destroyed;

        int switchableCount = currentBuilding.switchableTypes != null ? currentBuilding.switchableTypes.Count : 0;
        bool canSwitch = switchableCount > 1;

        if (prevModeButton)
            prevModeButton.gameObject.SetActive(canSwitch && !destroyed);
        if (nextModeButton)
            nextModeButton.gameObject.SetActive(canSwitch && !destroyed);

        BuildingType active = currentBuilding.ActiveType;

        bool hasShelter = currentShelterControl != null && currentShelterControl.enabled;
        bool hasCrafting = currentCraftingControl != null && currentCraftingControl.enabled;
        bool hasProduction = currentProductionControl != null && currentProductionControl.enabled;
        bool hasKinetic = currentKineticControl != null && currentKineticControl.enabled;
        bool hasStorage = currentStorageControl != null && currentStorageControl.enabled;
        bool hasReligious = currentReligiousControl != null && currentReligiousControl.enabled;

        if (openShelterButton)
        {
            bool showShelter = !destroyed && active == BuildingType.Shelter && hasShelter;
            openShelterButton.gameObject.SetActive(showShelter);
            openShelterButton.interactable = showShelter;
        }

        if (openCraftingButton)
        {
            bool showCraft = !destroyed && active == BuildingType.Crafting && hasCrafting;
            openCraftingButton.gameObject.SetActive(showCraft);
            openCraftingButton.interactable = showCraft;
        }

        if (openProductionButton)
        {
            bool showProd = !destroyed && active == BuildingType.Production && hasProduction;
            openProductionButton.gameObject.SetActive(showProd);
            openProductionButton.interactable = showProd;
        }

        if (openKineticWarfareButton)
        {
            bool showKW = !destroyed && active == BuildingType.KineticWarfare && hasKinetic;
            openKineticWarfareButton.gameObject.SetActive(showKW);
            openKineticWarfareButton.interactable = showKW;
        }

        if (openStorageButton)
        {
            bool showStorage = !destroyed && active == BuildingType.Storage && hasStorage;
            openStorageButton.gameObject.SetActive(showStorage);
            openStorageButton.interactable = showStorage;
        }

        if (openReligiousButton)
        {
            bool showReligious = !destroyed && active == BuildingType.Religious && hasReligious;
            openReligiousButton.gameObject.SetActive(showReligious);
            openReligiousButton.interactable = showReligious;
        }
    }

    public void DisplayStoredItems(StorageBuildingControl storage)
    {
        if (storagePanel != null)
            storagePanel.Refresh();
    }

    private bool HasAnyUpgradeOption(BuildingControl b)
    {
        if (!b || buildingManager == null)
            return false;

        Building fromDef = buildingManager.GetBuildingByID(b.buildingID);
        if (fromDef == null || fromDef.upgradeToIDs == null || fromDef.upgradeToIDs.Count == 0)
            return false;

        int currentPlayerLevel = playerLevel != null ? playerLevel.GetCurrentLevel() : int.MaxValue;

        foreach (string rawId in fromDef.upgradeToIDs)
        {
            if (string.IsNullOrWhiteSpace(rawId))
                continue;

            string id = rawId.Trim();
            Building cand = buildingManager.GetBuildingByID(id);
            if (cand == null)
                continue;

            if (knownBuildingsManager != null && !knownBuildingsManager.IsKnown(cand))
                continue;

            if (!cand.IsAvailableAtLevel(currentPlayerLevel))
                continue;

            return true;
        }

        return false;
    }

    private void RefreshUpgradeEntryState()
    {
        if (!openUpgradeButton)
            return;

        bool destroyed = currentStatus && currentStatus.CurrentState == BuildingState.Destroyed;
        bool hasUpgrade = !destroyed && HasAnyUpgradeOption(currentBuilding);

        openUpgradeButton.gameObject.SetActive(hasUpgrade);
        openUpgradeButton.interactable = hasUpgrade;
    }

    private void RefreshResearchEntryState()
    {
        if (!openResearchButton)
            return;

        bool destroyed = currentStatus && currentStatus.CurrentState == BuildingState.Destroyed;
        bool hasAvailable = false;

        if (!destroyed && currentTechnology != null)
        {
            var byLevel = currentTechnology.GetAvailableAtPlayerLevel();
            hasAvailable = byLevel != null && byLevel.Count > 0;
        }

        bool show = !destroyed && hasAvailable;
        openResearchButton.gameObject.SetActive(show);
        openResearchButton.interactable = show;
    }

    private void RefreshDestroyButton()
    {
        if (destroyButton == null)
            return;

        bool canShow = currentStatus != null;
        destroyButton.gameObject.SetActive(canShow);

        if (!canShow)
            return;

        bool canDestroy = currentStatus.CurrentState != BuildingState.Destroyed;
        destroyButton.interactable = canDestroy;
    }

    private void OnClickDestroyBuilding()
    {
        if (currentStatus == null)
            return;

        if (currentBuilding != null &&
            TutorialDestroyOverride != null &&
            TutorialDestroyOverride.Invoke(currentBuilding))
        {
            return;
        }

        if (currentHealth != null)
            currentHealth.ApplyDamage(currentHealth.CurrentHealth);
        else
            currentStatus.SetState(BuildingState.Destroyed);

        if (repairPanel != null)
            repairPanel.Close();

        RefreshDestroyButton();
        RefreshRepairEntryState();
        RefreshHealthUI();

        Hide();
    }

    public void OnClickOpenKineticWarfare()
    {
        if (!currentBuilding)
        {
            //Debug.LogWarning("[BuildingPanel] No currentBuilding when opening Kinetic Warfare.");
            return;
        }

        if (!kineticWarfarePanel)
        {
            //Debug.LogError("[BuildingPanel] KineticWarfarePanel reference is missing (assign a SCENE instance).");
            return;
        }

        var kw = currentKineticControl != null
            ? currentKineticControl
            : currentBuilding.GetComponent<KineticWarfareControl>();

        if (kw == null)
        {
            //Debug.LogWarning("[BuildingPanel] No KineticWarfareControl on this building.");
            return;
        }

        SoftHideForChild();
        kineticWarfarePanel.OpenFor(currentBuilding, this, currentTile);
    }

    public void OnClickOpenReligious()
    {
        if (!currentBuilding)
        {
            //Debug.LogWarning("[BuildingPanel] No currentBuilding when opening Religious.");
            return;
        }

        if (!religiousPanel)
        {
            //Debug.LogError("[BuildingPanel] ReligiousPanel reference is missing (assign a SCENE instance).");
            return;
        }

        var religious = currentReligiousControl != null
            ? currentReligiousControl
            : currentBuilding.GetComponent<ReligiousBuildingControl>();

        if (religious == null)
        {
            //Debug.LogWarning("[BuildingPanel] No ReligiousBuildingControl on this building.");
            return;
        }

        SoftHideForChild();
        religiousPanel.OpenFor(currentBuilding, this, currentTile);
    }
}
