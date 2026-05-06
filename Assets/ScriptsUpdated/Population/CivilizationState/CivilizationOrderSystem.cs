using UnityEngine;
using System.Linq;

public class CivilizationOrderSystem : MonoBehaviour
{
    [Header("Tuning: delta-based gains/losses")]
    [Tooltip("Order gain per +1.0 change in Happiness from last turn.")]
    public float happinessUpGainPerUnit = 0.20f;
    [Tooltip("Order loss per +1.0 drop in Happiness from last turn.")]
    public float happinessDownLossPerUnit = 0.25f;

    [Tooltip("Order gain per +1.0 change in Integration from last turn.")]
    public float integrationUpGainPerUnit = 0.15f;
    [Tooltip("Order loss per +1.0 drop in Integration from last turn.")]
    public float integrationDownLossPerUnit = 0.20f;

    [Header("Tuning: absolute health level effects (this turn)")]
    [Tooltip("Above this health, Order gains linearly toward 1.0.")]
    public float healthGoodThreshold = 0.60f;
    [Tooltip("Below this health, Order loses linearly toward 0.0.")]
    public float healthBadThreshold  = 0.40f;

    [Tooltip("Order gain at full health (1.0), scaled linearly from healthGoodThreshold..1.0.")]
    public float healthGoodGainAtMax = 0.02f;
    [Tooltip("Order loss at zero health (0.0), scaled linearly from 0.0..healthBadThreshold.")]
    public float healthBadLossAtZero = 0.03f;

    [Header("Optional: gentle convergence to the trio’s mean each turn")]
    [Tooltip("Blend Order a little toward the mean(Happiness, Health, Integration).")]
    public float meanConvergence = 0.05f;

    private CivilizationStateManager civ;
    private PlayersPopulationManager pop;

    // Prev-turn snapshots
    private float _prevHappiness = -1f;
    private float _prevIntegration = -1f;

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
        pop = PlayersPopulationManager.Instance;

        // Seed snapshots
        if (civ != null)
        {
            _prevHappiness   = Mathf.Clamp01(civ.happiness01);
            _prevIntegration = Mathf.Clamp01(civ.integration01);
        }
    }

    private void OnEndTurn()
    {
        if (civ == null) return;

        // 1) Recompute overall health from population (keeps civ.health01 current)
        if (pop == null) pop = PlayersPopulationManager.Instance;
        float overallHealth = ComputeOverallHealth01(pop);
        civ.health01 = overallHealth;

        // 2) Delta-driven effects (Happiness + Integration)
        float h = Mathf.Clamp01(civ.happiness01);
        float i = Mathf.Clamp01(civ.integration01);

        if (_prevHappiness >= 0f)
        {
            float dh = h - _prevHappiness; // + up, - down
            if (dh > 0f) civ.AdjustOrder(+happinessUpGainPerUnit * dh);
            else if (dh < 0f) civ.AdjustOrder(-happinessDownLossPerUnit * -dh);
        }

        if (_prevIntegration >= 0f)
        {
            float di = i - _prevIntegration; // + up, - down
            if (di > 0f) civ.AdjustOrder(+integrationUpGainPerUnit * di);
            else if (di < 0f) civ.AdjustOrder(-integrationDownLossPerUnit * -di);
        }

        // 3) Absolute health-level effect this turn
        float health = overallHealth;
        if (health > healthGoodThreshold)
        {
            float t = Mathf.InverseLerp(healthGoodThreshold, 1f, health); // 0..1
            civ.AdjustOrder(+healthGoodGainAtMax * t);
        }
        else if (health < healthBadThreshold)
        {
            float t = Mathf.InverseLerp(0f, healthBadThreshold, health); // 0..1 (low health -> low t)
            float loss = Mathf.Lerp(healthBadLossAtZero, 0f, t);         // worst at 0 health
            civ.AdjustOrder(-loss);
        }

        // 4) Gentle convergence toward the trio mean (stability)
        if (meanConvergence > 0f)
        {
            float trioMean = (h + health + i) / 3f;
            float blend = Mathf.Clamp01(meanConvergence);
            float newOrder = Mathf.Lerp(civ.order01, trioMean, blend);
            civ.SetOrder01(newOrder);
        }

        // 5) Snapshot for next turn
        _prevHappiness   = h;
        _prevIntegration = i;
    }

    private static float ComputeOverallHealth01(PlayersPopulationManager pop)
    {
        if (pop == null) return 0f;

        var list = pop.AllPopulations;
        int sumCount = 0;
        float sumHealth = 0f;
        for (int k = 0; k < list.Count; k++)
        {
            var g = list[k];
            if (g == null || g.count <= 0) continue;
            sumCount  += g.count;
            sumHealth += g.averageHealth * g.count;
        }
        if (sumCount <= 0) return 0f;
        return Mathf.Clamp01(sumHealth / sumCount);
    }
}
