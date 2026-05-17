using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;

public class GatheringDetailsPanelControl : MonoBehaviour
{
    [Header("UI References")]
    public GameObject root;
    public TMP_Text turnsText;
    public TMP_Text failureChanceText;
    public TMP_Text populationRequirementText;
    public TMP_Text penaltyText;
    public GameObject noResourceBlock;
    public Button closeButton;

    private EnvironmentControl currentEnv;
    public event Action OnClose;

    private void Start()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }
        Hide();
    }

    private void OnEnable()
    {
        if (SeasonManager.Instance != null)
            SeasonManager.Instance.OnSeasonChanged += HandleSeasonChanged;
    }

    private void OnDisable()
    {
        if (SeasonManager.Instance != null)
            SeasonManager.Instance.OnSeasonChanged -= HandleSeasonChanged;
    }

    private void HandleSeasonChanged(SeasonDefinition _)
    {
        if (currentEnv != null && root != null && root.activeInHierarchy)
            ShowFor(currentEnv, noResourceBlock != null && noResourceBlock.activeSelf);
    }

    public void ShowFor(EnvironmentControl env, bool showNoResources = false)
    {
        if (env == null) return;
        currentEnv = env;

        if (root != null) root.SetActive(true);
        else gameObject.SetActive(true);

        if (noResourceBlock != null)
            noResourceBlock.SetActive(showNoResources);

        env.GetPreTechGathering(out int preTechTurns, out float preTechFail);
        env.GetEffectiveGathering(out int effTurns, out float effFail);

        turnsText.text = FormatWithDelta(effTurns, preTechTurns);
        failureChanceText.text = FormatPctWithDelta(effFail, preTechFail);

        int basePop = env.BaseGatheringRequiredPop;
        int effectiveRequiredPop = PlayerTechBuffs.Instance != null
            ? PlayerTechBuffs.Instance.GetGatheringRequiredPopEffective(env, basePop)
            : basePop;
        populationRequirementText.text = FormatWithDelta(effectiveRequiredPop, basePop);

        int basePenalty = env.GatheringPopPenaltyOnFailure;
        int effectivePenalty = PlayerTechBuffs.Instance != null
            ? PlayerTechBuffs.Instance.GetGatheringPenaltyEffective(env, basePenalty)
            : basePenalty;
        penaltyText.text = FormatWithDelta(effectivePenalty, basePenalty);

        bool hasEnough = false;
        if (PlayersPopulationManager.Instance != null)
        {
            int available = PlayersPopulationManager.Instance.GetAvailableTaskPopulation();
            hasEnough = available >= effectiveRequiredPop;
        }
        populationRequirementText.color = hasEnough ? Color.green : Color.red;
    }

    private static string FormatWithDelta(int effective, int preTech) => $"{effective}";
    private static string FormatPctWithDelta(float effective, float preTech) => $"{Mathf.RoundToInt(effective)}%";

    public void Hide()
    {
        if (root != null) root.SetActive(false);
        else gameObject.SetActive(false);

        if (noResourceBlock != null)
            noResourceBlock.SetActive(false);

        currentEnv = null;
        OnClose?.Invoke();
    }
}