using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;

public class ClearingDetailsPanelControl : MonoBehaviour
{
    [Header("UI References")]
    public GameObject root;
    public TMP_Text turnsText;
    public TMP_Text populationRequirementText;
    public Button closeButton;

    [Header("Blocked Clearing UI")]
    public GameObject cannotClearContainer;
    public TMP_Text cannotClearText;

    private EnvironmentControl currentEnv;
    public event Action OnClose;

    private void Start()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }

        if (cannotClearContainer != null)
            cannotClearContainer.SetActive(false);
            
        Hide();
    }

    public void ShowFor(EnvironmentControl env)
    {
        if (env == null) return;
        currentEnv = env;

        if (root != null) root.SetActive(true);
        else gameObject.SetActive(true);

        // Base requirements from calculators (same as EnvironmentClearingTask uses)
        int baseTurns = ClearingTurnCalculator.CalculateClearingTurns(
            env.environmentType,
            env.environmentTileType,
            env.tileSize
        );

        int requiredPop = ClearingPopulationRequirementCalculator.CalculateRequiredPopulation(
            env.environmentType,
            env.environmentTileType,
            env.tileSize
        );


        if (cannotClearContainer != null)
                cannotClearContainer.SetActive(false);

            // If this tile CANNOT be manually cleared, show the block + bail out of normal cost UI
            if (!env.canBeManuallyCleared)
            {
                if (cannotClearContainer != null)
                    cannotClearContainer.SetActive(true);

                if (cannotClearText != null)
                    cannotClearText.text = "This tile cannot be cleared.";

                return;
            }

        // Display
        if (turnsText != null)
            turnsText.text = $"{baseTurns}";

        if (populationRequirementText != null)
            populationRequirementText.text = $"{requiredPop}";

        // Colour population requirement green/red depending on availability
        bool hasEnough = false;
        if (PlayersPopulationManager.Instance != null)
        {
            int available = PlayersPopulationManager.Instance.GetAvailableTaskPopulation();
            hasEnough = available >= requiredPop;
        }
        if (populationRequirementText != null)
            populationRequirementText.color = hasEnough ? Color.green : Color.red;
    }

    public void Hide()
    {
        if (root != null) root.SetActive(false);
        else gameObject.SetActive(false);

        currentEnv = null;
        OnClose?.Invoke();
    }
}
