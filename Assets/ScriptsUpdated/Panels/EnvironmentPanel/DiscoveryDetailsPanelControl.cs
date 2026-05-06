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

        // Base
        int baseTurns = Mathf.Max(1, env.discoveryTurnsRequired);
        float baseFail = Mathf.Clamp(env.DiscoveryFailureChance, 0f, 100f);

        var tile = env.GetComponentInParent<TileControl>();
        baseFail = Mathf.Clamp(baseFail + PredatorFailureBonus.GetBonusPercent(tile), 0f, 100f);

        env.GetEffectiveDiscovery(out int effTurns, out float effFail);

        // ✅ Show combined/effective values ONLY
        turnsText.text = $"{effTurns}";
        failureChanceText.text = $"{Mathf.Round(effFail)}%";

        int effectiveRequiredPop = env.requireDiscoveryPopulation;

        if (PlayerTechBuffs.Instance != null)
            effectiveRequiredPop = PlayerTechBuffs.Instance.GetDiscoveryRequiredPopEffective(env, env.requireDiscoveryPopulation);

        populationRequirementText.text = $"{effectiveRequiredPop}";
        int effectivePenalty = env.DiscoveryPopPenaltyOnFailure;
        if (PlayerTechBuffs.Instance != null)
            effectivePenalty = PlayerTechBuffs.Instance.GetDiscoveryPenaltyEffective(env, effectivePenalty);

        penaltyText.text = $"{effectivePenalty}";

        // Colour population requirement based on availability
        bool hasEnough = false;
        if (PlayersPopulationManager.Instance != null)
        {
            int available = PlayersPopulationManager.Instance.GetAvailableTaskPopulation();
            hasEnough = available >= effectiveRequiredPop;
        }
        populationRequirementText.color = hasEnough ? Color.green : Color.red;
    }

    public void Hide()
    {
        if (root != null) root.SetActive(false);
        currentEnv = null;
        OnClose?.Invoke();
    }
}