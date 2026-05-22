using System;
using System.Collections.Generic;
using UnityEngine;

public enum CivilizationStat
{
    Happiness,
    Health,
    Diversity,
    Integration,
    Order,
    Discovery,
    Knowledge,
    Faith
}

[Serializable]
public class CultureEffect
{
    [Tooltip("Which civilization stat this building passively affects.")]
    public CivilizationStat stat;

    [Tooltip("Amount added to the stat each turn. Negative values decrease the stat.")]
    [Range(-0.1f, 0.1f)]
    public float ratePerTurn = 0.01f;
}

[DisallowMultipleComponent]
public class CultureBuildingControl : MonoBehaviour
{
    [Header("Culture Effects (applied passively each turn)")]
    public List<CultureEffect> effects = new List<CultureEffect>();

    private BuildingStatus _buildingStatus;

    private static readonly List<CultureBuildingControl> s_all = new List<CultureBuildingControl>();

    private void Awake()
    {
        _buildingStatus = GetComponent<BuildingStatus>();
    }

    private void OnEnable()  { s_all.Add(this); }
    private void OnDisable() { s_all.Remove(this); }

    public void RunEndTurn()
    {
        if (!isActiveAndEnabled)
            return;

        if (_buildingStatus != null && _buildingStatus.CurrentState == BuildingState.Destroyed)
            return;

        CivilizationStateManager civ = CivilizationStateManager.Instance;
        if (civ == null || effects == null)
            return;

        for (int i = 0; i < effects.Count; i++)
        {
            CultureEffect effect = effects[i];
            if (effect == null || Mathf.Approximately(effect.ratePerTurn, 0f))
                continue;

            ApplyEffect(civ, effect.stat, effect.ratePerTurn);
        }
    }

    private static void ApplyEffect(CivilizationStateManager civ, CivilizationStat stat, float delta)
    {
        switch (stat)
        {
            case CivilizationStat.Happiness:   civ.AdjustHappiness(delta);   break;
            case CivilizationStat.Health:      civ.AdjustHealth(delta);      break;
            case CivilizationStat.Diversity:   civ.AdjustDiversity(delta);   break;
            case CivilizationStat.Integration: civ.AdjustIntegration(delta); break;
            case CivilizationStat.Order:       civ.AdjustOrder(delta);       break;
            case CivilizationStat.Discovery:   civ.AdjustDiscovery(delta);   break;
            case CivilizationStat.Knowledge:   civ.AdjustKnowledge(delta);   break;
            case CivilizationStat.Faith:       civ.AdjustFaith(delta);       break;
        }
    }

    public static List<CultureBuildingControl> GetAllSnapshot()
    {
        var result = new List<CultureBuildingControl>(s_all.Count);
        for (int i = 0; i < s_all.Count; i++)
        {
            if (s_all[i] != null)
                result.Add(s_all[i]);
        }
        return result;
    }
}
