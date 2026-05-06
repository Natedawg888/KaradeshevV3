using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class BuildingPanelControl : MonoBehaviour
{
    [Header("Repair Panel")]
    public Button openRepairButton;
    public RepairPanelControl repairPanel;

    [Header("Health")]
    public Slider healthSlider;
    public TMP_Text healthValueText;

    [Header("Repair (Progress UI)")]
    public GameObject repairOb;
    public Slider repairProgressSlider;

    private void RefreshHealthUI()
    {
        if (currentHealth == null) return;

        if (healthSlider != null)
        {
            healthSlider.minValue = 0;
            healthSlider.maxValue = currentHealth.maxHealth;
            healthSlider.wholeNumbers = true;
            healthSlider.value = currentHealth.CurrentHealth;
            healthSlider.interactable = false;
        }

        if (healthValueText != null)
            healthValueText.text = $"{currentHealth.CurrentHealth}/{currentHealth.maxHealth}";
    }

    private void RefreshRepairEntryState()
    {
        bool hasHealth   = currentBuilding && currentBuilding.GetComponent<BuildingHealth>();
        bool hasRepair   = currentBuilding && currentBuilding.GetComponent<BuildingRepair>();
        bool isDestroyed = currentStatus && currentStatus.CurrentState == BuildingState.Destroyed;

        if (openRepairButton)
        {
            openRepairButton.gameObject.SetActive(hasHealth && hasRepair && !isDestroyed);
            bool isRepairing = currentRepair && currentRepair.IsRepairing;
            openRepairButton.interactable = (hasHealth && hasRepair) && !isRepairing && !isDestroyed;
        }

        if (!(hasHealth && hasRepair) && repairPanel)
            repairPanel.Close();

        RefreshModeSpecificButtons(); // destroy state can alter visibility
    }

    private void RefreshRepairProgressUI()
    {
        if (repairOb == null || repairProgressSlider == null) return;

        bool destroyed = currentStatus && currentStatus.CurrentState == BuildingState.Destroyed;
        bool repairing = currentRepair && currentRepair.IsRepairing && !destroyed;

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

    private void HandleRepairStarted(RepairOption opt, int totalTurns)
    {
        if (openRepairButton) openRepairButton.interactable = false;

        if (repairOb != null) repairOb.SetActive(true);
        if (repairProgressSlider != null)
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
        if (repairProgressSlider != null)
            repairProgressSlider.value = turnsLeft;
    }

    private void HandleRepairCompleted()
    {
        if (repairOb != null) repairOb.SetActive(false);
        if (openRepairButton) openRepairButton.interactable = true;

        RefreshHealthUI();
        RefreshRepairEntryState();
    }
}
