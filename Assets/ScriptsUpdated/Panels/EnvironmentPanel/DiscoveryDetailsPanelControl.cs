using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;

public class DiscoveryDetailsPanelControl : MonoBehaviour
{
    [Header("UI References")]
    public GameObject root; // panel root
    public TMP_Text turnsText;
    public TMP_Text failureChanceText;
    public TMP_Text populationRequirementText;
    public TMP_Text penaltyText;
    public Button closeButton;

    private EnvironmentControl currentEnv;

    public bool IsShowing => root != null ? root.activeInHierarchy : gameObject.activeInHierarchy;

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
            ShowFor(currentEnv);
    }

    public void ShowFor(EnvironmentControl env)
    {
        if (env == null) return;
        currentEnv = env;

        if (root != null) root.SetActive(true);
        else gameObject.SetActive(true);

        env.GetPreTechDiscovery(out int preTechTurns, out float preTechFail);
        env.GetEffectiveDiscovery(out int effTurns, out float effFail);

        turnsText.text = FormatWithDelta(effTurns, preTechTurns);
        failureChanceText.text = FormatPctWithDelta(effFail, preTechFail);

        int basePop = env.BaseDiscoveryRequiredPop;
        int effectiveRequiredPop = PlayerTechBuffs.Instance != null
            ? PlayerTechBuffs.Instance.GetDiscoveryRequiredPopEffective(env, basePop)
            : basePop;
        populationRequirementText.text = FormatWithDelta(effectiveRequiredPop, basePop);

        int basePenalty = env.DiscoveryPopPenaltyOnFailure;
        int effectivePenalty = PlayerTechBuffs.Instance != null
            ? PlayerTechBuffs.Instance.GetDiscoveryPenaltyEffective(env, basePenalty)
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
        currentEnv = null;
        OnClose?.Invoke();
    }
}