using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SurveyDetailsPanelControl : MonoBehaviour
{
    [Header("UI Roots")]
    public GameObject root;
    public Button closeButton;

    [Header("Survey Progress")]
    public TMP_Text surveyTurnsText;          // e.g. "2 / 5 turns"
    public GameObject surveyOb;
    public Slider surveyProgressSlider;

    [Header("Population Requirement")]
    public TMP_Text populationRequirementText;

    [Header("Block UI")]
    public GameObject noResourceBlock;

    [Header("Re-survey Countdown")]
    public GameObject resurveyOb;
    public Slider resurveySlider;

    private EnvironmentControl currentEnv;
    public event Action OnClose;

    void Start()
    {
        closeButton.onClick.RemoveAllListeners();
        closeButton.onClick.AddListener(Hide);
        Hide();
    }

    /// Pass in the EnvironmentControl to show its survey stats.
    public void ShowFor(EnvironmentControl env, bool showNoResources = false) // ✅ add param
    {
        currentEnv = env;
        if (root != null) root.SetActive(true);

        if (noResourceBlock != null)
            noResourceBlock.SetActive(showNoResources); // ✅ toggle

        surveyTurnsText.text = $"{env.surveyTurnsRequired}";

        populationRequirementText.text = $"{env.requireSurveyPopulation}";
        int available = PlayersPopulationManager.Instance?.GetAvailableTaskPopulation() ?? 0;
        populationRequirementText.color = (available >= env.requireSurveyPopulation)
            ? Color.green
            : Color.red;

        if (env.isSurveying || !env.IsSurveyed)
        {
            surveyOb.gameObject.SetActive(true);
            surveyProgressSlider.gameObject.SetActive(true);

            surveyProgressSlider.minValue = 0;
            surveyProgressSlider.maxValue = env.surveyTurnsRequired;
            surveyProgressSlider.value = env.surveyTurnsLeft;

            resurveyOb.gameObject.SetActive(false);
        }
        else
        {
            surveyOb.gameObject.SetActive(false);

            resurveyOb.gameObject.SetActive(true);

            resurveySlider.minValue = 0;
            resurveySlider.maxValue = env.resurveyInterval;
            resurveySlider.value = env.resurveyTurnsLeft;
        }
    }

    public void Hide()
    {
        if (root != null) root.SetActive(false);

        if (noResourceBlock != null)
            noResourceBlock.SetActive(false); // ✅ reset

        currentEnv = null;
        OnClose?.Invoke();
    }
}