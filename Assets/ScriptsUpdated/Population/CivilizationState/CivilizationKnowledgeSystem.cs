using UnityEngine;

public class CivilizationKnowledgeSystem : MonoBehaviour
{
    [Header("Inputs")]
    [Tooltip("0..1 external hunger input (0 = fed, 1 = starving). Set from your food system each turn or live.")]
    [Range(0f,1f)] public float externalHunger01 = 0f;

    [Header("Loss From Hunger")]
    [Tooltip("Knowledge loss per turn at full hunger (1.0). Scales linearly with hunger.")]
    public float hungerLossPerTurn01 = 0.04f; // 4% per turn at worst hunger

    [Header("Loss From Low Diversity")]
    [Tooltip("Below this diversity, knowledge loses each turn.")]
    [Range(0f,1f)] public float diversityGoodThreshold = 0.50f;
    [Tooltip("Knowledge loss per turn when diversity is 0.0; scales up as diversity falls below threshold.")]
    public float diversityLossAtZero01 = 0.02f;

    [Header("Low Knowledge → Order Penalty")]
    [Tooltip("Below this knowledge, Order is penalized each turn.")]
    [Range(0f, 1f)] public float knowledgeLowThreshold = 0.40f;
    
    [Tooltip("Order loss per turn at 0 knowledge; scales up as knowledge drops below threshold.")]
    public float orderLossAtZeroKnowledgePerTurn = 0.02f;

    private CivilizationStateManager civ;

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

    /// <summary>Call this from your food/hunger system.</summary>
    public void SetExternalHunger01(float v) => externalHunger01 = Mathf.Clamp01(v);

    private void OnEndTurn()
    {
        if (civ == null) civ = CivilizationStateManager.Instance;
        if (civ == null) return;

        // 1) Hunger-driven knowledge loss
        if (externalHunger01 > 0f && hungerLossPerTurn01 > 0f)
        {
            float hungerLoss = hungerLossPerTurn01 * Mathf.Clamp01(externalHunger01);
            civ.AdjustKnowledge(-hungerLoss);
        }

        // 2) Diversity-driven knowledge loss
        float diversity = Mathf.Clamp01(civ.diversity01);
        if (diversity < diversityGoodThreshold && diversityLossAtZero01 > 0f)
        {
            // Map diversity in [0..threshold] → [1..0] factor (worse at 0)
            float t = Mathf.InverseLerp(0f, diversityGoodThreshold, diversity); // 0 at 0 diversity, 1 at threshold
            float factor = 1f - t; // 1 at 0 diversity, 0 at threshold
            float loss = diversityLossAtZero01 * factor;
            civ.AdjustKnowledge(-loss);
        }

        // 3) Low knowledge penalizes Order
        float knowledge = Mathf.Clamp01(civ.knowledge01);
        if (knowledge < knowledgeLowThreshold && orderLossAtZeroKnowledgePerTurn > 0f)
        {
            // Map knowledge in [0..threshold] → [1..0] factor (worse at 0)
            float t = Mathf.InverseLerp(0f, knowledgeLowThreshold, knowledge);
            float factor = 1f - t; // 1 at 0 knowledge, 0 at threshold
            float orderLoss = orderLossAtZeroKnowledgePerTurn * factor;
            civ.AdjustOrder(-orderLoss);
        }
    }
}
