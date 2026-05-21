using System;
using UnityEngine;

public enum SolarStormSeverity
{
    None = 0,
    Low = 1,
    Moderate = 2,
    High = 3,
    Extreme = 4
}

/// <summary>
/// Applies temporary energy pressure to existing unstable systems (faults, volcanoes,
/// storm clouds, lightning) without directly triggering events.
/// Add this MonoBehaviour to a scene object and wire references in the Inspector,
/// or let it auto-resolve via FindObjectOfType.
/// </summary>
public class SolarStormSystem : MonoBehaviour
{
    public static SolarStormSystem Instance { get; private set; }

    [Header("References")]
    public EarthquakeSimulationSystem earthquakeSystem;
    public VolcanoManager volcanoManager;
    public StormSimulationSystem stormSystem;
    public LightningSimulationSystem lightningSystem;

    [Header("Lifecycle")]
    public bool applyOnEndOfTurn = true;

    [Header("Random Trigger")]
    public bool enableRandomTrigger = false;
    [Range(0f, 1f)] public float randomTriggerChancePerTurn = 0.01f;
    public SolarStormSeverity randomTriggerMaxSeverity = SolarStormSeverity.High;

    [Header("Duration (turns)")]
    [Min(1)] public int durationMinLow = 2;
    [Min(1)] public int durationMaxLow = 4;
    [Min(1)] public int durationMinModerate = 3;
    [Min(1)] public int durationMaxModerate = 5;
    [Min(1)] public int durationMinHigh = 4;
    [Min(1)] public int durationMaxHigh = 6;
    [Min(1)] public int durationMinExtreme = 5;
    [Min(1)] public int durationMaxExtreme = 8;

    [Header("Earthquake Energy Bonus Per Turn")]
    [Range(0f, 1f)] public float earthquakeBonusLow = 0.05f;
    [Range(0f, 1f)] public float earthquakeBonusModerate = 0.10f;
    [Range(0f, 1f)] public float earthquakeBonusHigh = 0.15f;
    [Range(0f, 1f)] public float earthquakeBonusExtreme = 0.25f;

    [Header("Volcano Energy Bonus Per Turn (Dormant/Erupting only)")]
    [Range(0f, 1f)] public float volcanoBonusLow = 0.03f;
    [Range(0f, 1f)] public float volcanoBonusModerate = 0.06f;
    [Range(0f, 1f)] public float volcanoBonusHigh = 0.10f;
    [Range(0f, 1f)] public float volcanoBonusExtreme = 0.15f;

    [Header("Storm Intensity Gain Bonus (additive to StormSimulationSystem)")]
    [Range(0f, 1f)] public float stormBonusLow = 0.02f;
    [Range(0f, 1f)] public float stormBonusModerate = 0.05f;
    [Range(0f, 1f)] public float stormBonusHigh = 0.10f;
    [Range(0f, 1f)] public float stormBonusExtreme = 0.15f;

    [Header("Lightning Charge Gain Bonus (additive to LightningSimulationSystem)")]
    [Range(0f, 1f)] public float lightningBonusLow = 0.02f;
    [Range(0f, 1f)] public float lightningBonusModerate = 0.05f;
    [Range(0f, 1f)] public float lightningBonusHigh = 0.10f;
    [Range(0f, 1f)] public float lightningBonusExtreme = 0.15f;

    [Header("Debug")]
    public bool debugLogging = false;

    public bool IsActive => _currentSeverity != SolarStormSeverity.None;
    public SolarStormSeverity CurrentSeverity => _currentSeverity;
    public int TurnsRemaining => _turnsRemaining;
    public int TotalTurns => _totalTurns;

    private SolarStormSeverity _currentSeverity = SolarStormSeverity.None;
    private int _turnsRemaining;
    private int _totalTurns;

    public event Action<SolarStormSeverity> OnSolarStormStarted;
    public event Action OnSolarStormEnded;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolveReferences();
    }

    private void OnEnable()
    {
        if (applyOnEndOfTurn)
            TurnSystem.SubscribeToEndOfTurn(HandleEndOfTurn);
    }

    private void OnDisable()
    {
        TurnSystem.UnsubscribeFromEndOfTurn(HandleEndOfTurn);
        ClearSystemBonuses();
    }

    private void OnDestroy()
    {
        ClearSystemBonuses();

        if (Instance == this)
            Instance = null;
    }

    private void HandleEndOfTurn()
    {
        if (!IsActive)
        {
            if (enableRandomTrigger && UnityEngine.Random.value < randomTriggerChancePerTurn)
                TriggerSolarStorm(RollRandomSeverity());
            return;
        }

        ApplyBonusesThisTurn();
        _turnsRemaining--;

        if (debugLogging)
            Debug.Log($"[SolarStormSystem] Turn applied. Severity={_currentSeverity}, TurnsLeft={_turnsRemaining}");

        if (_turnsRemaining <= 0)
            EndSolarStorm();
    }

    public void TriggerSolarStorm(SolarStormSeverity severity)
    {
        TriggerSolarStorm(severity, GetDurationForSeverity(severity));
    }

    public void TriggerSolarStorm(SolarStormSeverity severity, int durationTurns)
    {
        if (severity == SolarStormSeverity.None || durationTurns <= 0)
        {
            EndSolarStorm();
            return;
        }

        ResolveReferences();

        if (IsActive)
            ClearSystemBonuses();

        _currentSeverity = severity;
        _turnsRemaining = durationTurns;
        _totalTurns = durationTurns;

        ApplySystemBonuses(_currentSeverity);

        if (debugLogging)
            Debug.Log($"[SolarStormSystem] Storm started: Severity={severity}, Duration={durationTurns} turns.");

        OnSolarStormStarted?.Invoke(severity);
    }

    [ContextMenu("Trigger Low Solar Storm")]
    public void TriggerLow() => TriggerSolarStorm(SolarStormSeverity.Low);

    [ContextMenu("Trigger Moderate Solar Storm")]
    public void TriggerModerate() => TriggerSolarStorm(SolarStormSeverity.Moderate);

    [ContextMenu("Trigger High Solar Storm")]
    public void TriggerHigh() => TriggerSolarStorm(SolarStormSeverity.High);

    [ContextMenu("Trigger Extreme Solar Storm")]
    public void TriggerExtreme() => TriggerSolarStorm(SolarStormSeverity.Extreme);

    [ContextMenu("End Solar Storm")]
    public void ForceEndSolarStorm() => EndSolarStorm();

    private void EndSolarStorm()
    {
        if (!IsActive)
            return;

        _currentSeverity = SolarStormSeverity.None;
        _turnsRemaining = 0;
        _totalTurns = 0;

        ClearSystemBonuses();

        if (debugLogging)
            Debug.Log("[SolarStormSystem] Storm ended.");

        OnSolarStormEnded?.Invoke();
    }

    private void ApplyBonusesThisTurn()
    {
        float eqBonus  = GetEarthquakeBonusForSeverity(_currentSeverity);
        float volBonus = GetVolcanoBonusForSeverity(_currentSeverity);

        if (earthquakeSystem != null && eqBonus > 0f)
            earthquakeSystem.AddExternalTectonicEnergy(eqBonus);

        if (volcanoManager != null && volBonus > 0f)
            ApplyVolcanicEnergyBonus(volBonus);
    }

    private void ApplyVolcanicEnergyBonus(float bonus)
    {
        foreach (VolcanoTileState volcano in volcanoManager.RegisteredVolcanoes)
        {
            if (volcano == null || !volcano.IsVolcano)
                continue;

            volcano.energy01 = Mathf.Clamp01(volcano.energy01 + bonus);
        }
    }

    private void ApplySystemBonuses(SolarStormSeverity severity)
    {
        if (stormSystem != null)
            stormSystem.SolarStormIntensityGainBonus = GetStormBonusForSeverity(severity);

        if (lightningSystem != null)
            lightningSystem.SolarStormChargeGainBonus = GetLightningBonusForSeverity(severity);
    }

    private void ClearSystemBonuses()
    {
        if (stormSystem != null)
            stormSystem.SolarStormIntensityGainBonus = 0f;

        if (lightningSystem != null)
            lightningSystem.SolarStormChargeGainBonus = 0f;
    }

    private SolarStormSeverity RollRandomSeverity()
    {
        int max = Mathf.Clamp((int)randomTriggerMaxSeverity, 1, 4);
        return (SolarStormSeverity)UnityEngine.Random.Range(1, max + 1);
    }

    private int GetDurationForSeverity(SolarStormSeverity severity)
    {
        int min, max;

        if (severity == SolarStormSeverity.Low)
        {
            min = durationMinLow;
            max = durationMaxLow;
        }
        else if (severity == SolarStormSeverity.Moderate)
        {
            min = durationMinModerate;
            max = durationMaxModerate;
        }
        else if (severity == SolarStormSeverity.High)
        {
            min = durationMinHigh;
            max = durationMaxHigh;
        }
        else
        {
            min = durationMinExtreme;
            max = durationMaxExtreme;
        }

        min = Mathf.Max(1, min);
        max = Mathf.Max(min, max);
        return UnityEngine.Random.Range(min, max + 1);
    }

    private float GetEarthquakeBonusForSeverity(SolarStormSeverity severity)
    {
        if (severity == SolarStormSeverity.Low)      return earthquakeBonusLow;
        if (severity == SolarStormSeverity.Moderate) return earthquakeBonusModerate;
        if (severity == SolarStormSeverity.High)     return earthquakeBonusHigh;
        if (severity == SolarStormSeverity.Extreme)  return earthquakeBonusExtreme;
        return 0f;
    }

    private float GetVolcanoBonusForSeverity(SolarStormSeverity severity)
    {
        if (severity == SolarStormSeverity.Low)      return volcanoBonusLow;
        if (severity == SolarStormSeverity.Moderate) return volcanoBonusModerate;
        if (severity == SolarStormSeverity.High)     return volcanoBonusHigh;
        if (severity == SolarStormSeverity.Extreme)  return volcanoBonusExtreme;
        return 0f;
    }

    private float GetStormBonusForSeverity(SolarStormSeverity severity)
    {
        if (severity == SolarStormSeverity.Low)      return stormBonusLow;
        if (severity == SolarStormSeverity.Moderate) return stormBonusModerate;
        if (severity == SolarStormSeverity.High)     return stormBonusHigh;
        if (severity == SolarStormSeverity.Extreme)  return stormBonusExtreme;
        return 0f;
    }

    private float GetLightningBonusForSeverity(SolarStormSeverity severity)
    {
        if (severity == SolarStormSeverity.Low)      return lightningBonusLow;
        if (severity == SolarStormSeverity.Moderate) return lightningBonusModerate;
        if (severity == SolarStormSeverity.High)     return lightningBonusHigh;
        if (severity == SolarStormSeverity.Extreme)  return lightningBonusExtreme;
        return 0f;
    }

    private void ResolveReferences()
    {
        if (earthquakeSystem == null)
            earthquakeSystem = FindObjectOfType<EarthquakeSimulationSystem>();

        if (volcanoManager == null)
            volcanoManager = VolcanoManager.Instance != null
                ? VolcanoManager.Instance
                : FindObjectOfType<VolcanoManager>();

        if (stormSystem == null)
            stormSystem = StormSimulationSystem.Instance != null
                ? StormSimulationSystem.Instance
                : FindObjectOfType<StormSimulationSystem>();

        if (lightningSystem == null)
            lightningSystem = LightningSimulationSystem.Instance != null
                ? LightningSimulationSystem.Instance
                : FindObjectOfType<LightningSimulationSystem>();
    }

    public SolarStormSaveData SaveState()
    {
        return new SolarStormSaveData
        {
            isActive       = IsActive,
            severityValue  = (int)_currentSeverity,
            turnsRemaining = _turnsRemaining,
            totalTurns     = _totalTurns
        };
    }

    public void LoadState(SolarStormSaveData data)
    {
        if (data == null)
            return;

        ResolveReferences();
        ClearSystemBonuses();

        _currentSeverity = (SolarStormSeverity)data.severityValue;
        _turnsRemaining  = data.turnsRemaining;
        _totalTurns      = data.totalTurns;

        if (IsActive)
            ApplySystemBonuses(_currentSeverity);

        if (debugLogging)
            Debug.Log($"[SolarStormSystem] Loaded. Active={IsActive}, Severity={_currentSeverity}, TurnsLeft={_turnsRemaining}");
    }
}
