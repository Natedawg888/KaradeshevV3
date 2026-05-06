// CivilizationDiscoverySystem.cs
using UnityEngine;

public class CivilizationDiscoverySystem : MonoBehaviour
{
    public static CivilizationDiscoverySystem Instance { get; private set; }

    [Header("Tuning: success / failure effects")]
    [Tooltip("Discovery gain per unit weight on success.")]
    public float successGainPerWeight = 0.02f;
    [Tooltip("Discovery loss per unit weight on failure.")]
    public float failureLossPerWeight = 0.03f;

    [Header("Natural recovery (each end of turn)")]
    [Tooltip("Additive recovery to Discovery each turn.")]
    public float passiveRecoveryPerTurn = 0.015f;

    [Header("Risk gating when Discovery is low")]
    [Tooltip("Below this discovery value, we start blocking risky attempts.")]
    public float lowDiscoveryThreshold = 0.30f;
    [Tooltip("If below threshold, any attempt with failureChance >= this is blocked.")]
    [Range(0f, 1f)] public float blockRiskThresholdAtLow = 0.50f;

    [Tooltip("Optional smoothing: lerp block threshold between low and high discovery.")]
    [Range(0f, 1f)] public float highDiscoveryForNoBlock = 0.80f;

    private CivilizationStateManager civ;

    private void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        TurnSystem.SubscribeToEndOfTurn(OnEndTurn);
    }

    private void OnDisable()
    {
        TurnSystem.UnsubscribeFromEndOfTurn(OnEndTurn);
    }

    private void Start()
    {
        civ = CivilizationStateManager.Instance;
    }

    private void OnEndTurn()
    {
        if (civ == null) civ = CivilizationStateManager.Instance;
        if (civ == null) return;

        // Natural recovery
        civ.AdjustDiscovery(+passiveRecoveryPerTurn);
    }

    // ─────────────── External hooks to call from your task systems ───────────────
    // success/failureChance in 0..1, weight default 1.0 (scale by task rarity/importance if you like)

    public void NotifyGatherResult(bool success, float failureChance01, float weight = 1f)
    {
        ApplyOutcome(success, failureChance01, weight);
    }

    public void NotifyDiscoveryTaskResult(bool success, float failureChance01, float weight = 1f)
    {
        ApplyOutcome(success, failureChance01, weight);
    }

    private void ApplyOutcome(bool success, float failureChance01, float weight)
    {
        if (civ == null) return;
        weight = Mathf.Max(0f, weight);
        float riskFactor = Mathf.Clamp01(failureChance01);

        // Slightly scale the magnitude by risk: high-risk success teaches more; high-risk failure hurts more.
        float scaledWeight = weight * Mathf.Lerp(0.8f, 1.2f, riskFactor);

        civ.AdjustDiscovery(success ? +successGainPerWeight * scaledWeight
                                    : -failureLossPerWeight * scaledWeight);
    }

    // ─────────────── Risk-gating API ───────────────
    // Call before executing a gather/discovery on an environment tile.
    // Returns true if the attempt should be blocked due to low discovery.
    public bool ShouldBlockForRisk(float failureChance01)
    {
        if (civ == null) return false;

        float d = Mathf.Clamp01(civ.discovery01);
        float risk = Mathf.Clamp01(failureChance01);

        // If discovery is high enough, never block
        if (d >= highDiscoveryForNoBlock) return false;

        // Interpolate a dynamic block threshold between:
        //  - at/under low threshold: block if risk >= blockRiskThresholdAtLow
        //  - at highDiscoveryForNoBlock: threshold becomes 1 (effectively never blocks)
        float t = Mathf.InverseLerp(lowDiscoveryThreshold, highDiscoveryForNoBlock, d); // 0..1
        float dynamicThreshold = Mathf.Lerp(blockRiskThresholdAtLow, 1f, Mathf.Clamp01(t));

        return risk >= dynamicThreshold;
    }
}
