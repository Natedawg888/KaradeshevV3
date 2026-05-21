using System;
using System.Collections.Generic;
using UnityEngine;

public class EarthquakeSimulationSystem : MonoBehaviour
{
    [Header("References")]
    public MapGenerator mapGenerator;
    public GridManager gridManager;
    public EarthquakeFaultLineGenerator faultLineGenerator;

    [Header("Preset Settings")]
    [Tooltip("If true, preset values overwrite inspector values on Start.")]
    public bool usePresetSettings = true;

    [Header("Turn Rolling")]
    [Tooltip("If true, this system subscribes to TurnSystem end-of-turn and rolls automatically.")]
    public bool rollEarthquakeOnEndOfTurn = true;

    [Tooltip("Prevents accidental double rolls if another system also calls RollForEarthquake in the same turn.")]
    public bool preventMultipleRollsPerTurn = true;

    [Header("Base Earthquake Chance")]
    [Range(0f, 1f)]
    public float earthquakeChancePerTurn = 0.015f;

    [Header("Tectonic Energy")]
    [Tooltip("Current stored tectonic energy. Higher energy means higher quake chance and stronger magnitude.")]
    [Range(0f, 1f)]
    public float tectonicEnergy01 = 0f;

    [Tooltip("Minimum tectonic energy gained each turn.")]
    [Range(0f, 1f)]
    public float minEnergyGainPerTurn = 0.015f;

    [Tooltip("Maximum tectonic energy gained each turn.")]
    [Range(0f, 1f)]
    public float maxEnergyGainPerTurn = 0.045f;

    [Tooltip("Energy gain multiplier when this map has fault lines.")]
    [Min(0f)]
    public float faultLineEnergyGainMultiplier = 1.25f;

    [Tooltip("Small random stress spikes make earthquakes less predictable.")]
    [Range(0f, 1f)]
    public float stressSpikeChancePerTurn = 0.08f;

    public Vector2 stressSpikeEnergyGainRange = new Vector2(0.03f, 0.12f);

    [Header("Energy -> Chance")]
    [Tooltip("Extra earthquake chance added when tectonic energy reaches 1.")]
    [Range(0f, 1f)]
    public float maxEnergyChanceBonus = 0.35f;

    [Tooltip("Curve for how energy increases earthquake chance.")]
    public AnimationCurve energyChanceCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("If energy reaches this value, the quake chance is forced to at least Guaranteed Chance At High Energy.")]
    [Range(0f, 1f)]
    public float guaranteedCheckEnergy01 = 1f;

    [Tooltip("Minimum chance when energy is at or above Guaranteed Check Energy.")]
    [Range(0f, 1f)]
    public float guaranteedChanceAtHighEnergy = 1f;

    [Header("Magnitude")]
    public Vector2 magnitudeRange = new Vector2(2.5f, 8.5f);

    [Tooltip("0 = magnitude is random. 1 = magnitude is mostly based on stored energy.")]
    [Range(0f, 1f)]
    public float energyMagnitudeWeight = 0.75f;

    [Tooltip("Curve for how energy increases magnitude.")]
    public AnimationCurve energyMagnitudeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("If true, forced earthquakes use full energy for magnitude. Useful for testing.")]
    public bool forcedEarthquakeUsesFullEnergyForMagnitude = false;

    [Header("Energy Release")]
    public bool releaseEnergyAfterEarthquake = true;

    [Tooltip("Minimum energy released by a weak earthquake.")]
    [Range(0f, 1f)]
    public float minEnergyReleaseOnEarthquake = 0.20f;

    [Tooltip("Maximum energy released by a severe earthquake.")]
    [Range(0f, 1f)]
    public float maxEnergyReleaseOnEarthquake = 0.90f;

    [Tooltip("Small energy left behind after a quake, so pressure can rebuild from a non-zero value.")]
    [Range(0f, 1f)]
    public float minimumEnergyAfterRelease = 0f;

    [Header("Affected Radius")]
    [Tooltip("Radius is measured in terrain blocks, not grid cells.")]
    public float minRadiusBlocks = 1.5f;

    [Tooltip("Radius is measured in terrain blocks, not grid cells.")]
    public float maxRadiusBlocks = 7f;

    [Header("Epicentre")]
    [Tooltip("If true, natural earthquakes require generated fault lines.")]
    public bool requireFaultForNaturalEarthquakes = true;

    [Tooltip("If no fault exists and the earthquake is forced, pick any valid block.")]
    public bool forcedCanUseRandomBlockWithoutFault = true;

    [Header("Debug")]
    public bool debugLogging = true;
    public bool rollWithSpaceKey = false;

    public event Action<EarthquakeEventData> OnEarthquake;

    public float CurrentTectonicEnergy01 => tectonicEnergy01;
    public float LastCalculatedChance01 => lastCalculatedChance01;

    private readonly List<Vector2Int> affectedScratch = new List<Vector2Int>();
    private readonly HashSet<Vector2Int> affectedSetScratch = new HashSet<Vector2Int>();

    private int lastProcessedTurn = int.MinValue;
    private float lastCalculatedChance01;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        if (rollEarthquakeOnEndOfTurn)
            TurnSystem.SubscribeToEndOfTurn(HandleEndOfTurn);
    }

    private void OnDisable()
    {
        TurnSystem.UnsubscribeFromEndOfTurn(HandleEndOfTurn);
    }

    private void Start()
    {
        ResolveReferences();

        if (usePresetSettings)
            ApplyPresetSettings();
    }

    private void Update()
    {
        if (rollWithSpaceKey && Input.GetKeyDown(KeyCode.Space))
            ForceEarthquake();
    }

    private void HandleEndOfTurn()
    {
        if (!rollEarthquakeOnEndOfTurn)
            return;

        ProcessEarthquakeTurnRoll();
    }

    public void RollForEarthquake()
    {
        ProcessEarthquakeTurnRoll();
    }

    private void ProcessEarthquakeTurnRoll()
    {
        ResolveReferences();

        int currentTurn = TurnSystem.GetCurrentTurn();

        if (preventMultipleRollsPerTurn && lastProcessedTurn == currentTurn)
        {
            if (debugLogging) {}
                //Debug.Log($"EarthquakeSimulationSystem: Already processed turn {currentTurn}, skipping duplicate roll.");

            return;
        }

        lastProcessedTurn = currentTurn;

        MarkEarthquakeSaveDirty();

        AddTectonicEnergyForTurn();

        float chance = CalculateCurrentEarthquakeChance();
        lastCalculatedChance01 = chance;

        float roll = UnityEngine.Random.value;

        if (debugLogging)
        {
            //Debug.Log(
                //$"Earthquake turn roll. Turn={currentTurn}, " +
                //$"Energy={tectonicEnergy01:0.000}, Chance={chance:0.000}, Roll={roll:0.000}"
            //);
        }

        if (roll > chance)
            return;

        TriggerEarthquake(false);
    }

    [ContextMenu("Force Earthquake")]
    public void ForceEarthquake()
    {
        ResolveReferences();
        TriggerEarthquake(true);
    }

    [ContextMenu("Add Test Energy")]
    public void AddTestEnergy()
    {
        tectonicEnergy01 = Mathf.Clamp01(tectonicEnergy01 + 0.25f);

        MarkEarthquakeSaveDirty();

        if (debugLogging) {}
            //Debug.Log($"EarthquakeSimulationSystem: Added test energy. Energy={tectonicEnergy01:0.000}");
    }

    [ContextMenu("Clear Tectonic Energy")]
    public void ClearTectonicEnergy()
    {
        tectonicEnergy01 = 0f;

        MarkEarthquakeSaveDirty();

        if (debugLogging) {}
            //Debug.Log("EarthquakeSimulationSystem: Cleared tectonic energy.");
    }

    public void AddExternalTectonicEnergy(float amount)
    {
        if (amount <= 0f)
            return;

        tectonicEnergy01 = Mathf.Clamp01(tectonicEnergy01 + amount);
        MarkEarthquakeSaveDirty();
    }

    private void AddTectonicEnergyForTurn()
    {
        float minGain = Mathf.Min(minEnergyGainPerTurn, maxEnergyGainPerTurn);
        float maxGain = Mathf.Max(minEnergyGainPerTurn, maxEnergyGainPerTurn);

        float gain = UnityEngine.Random.Range(minGain, maxGain);

        if (faultLineGenerator != null && faultLineGenerator.HasFaults)
            gain *= Mathf.Max(0f, faultLineEnergyGainMultiplier);

        float spike = 0f;

        if (UnityEngine.Random.value <= stressSpikeChancePerTurn)
        {
            float minSpike = Mathf.Min(stressSpikeEnergyGainRange.x, stressSpikeEnergyGainRange.y);
            float maxSpike = Mathf.Max(stressSpikeEnergyGainRange.x, stressSpikeEnergyGainRange.y);

            spike = UnityEngine.Random.Range(minSpike, maxSpike);
        }

        tectonicEnergy01 = Mathf.Clamp01(tectonicEnergy01 + gain + spike);

        if (debugLogging && spike > 0f)
        {
            //Debug.Log(
                //$"EarthquakeSimulationSystem: Stress spike added {spike:0.000} energy. " +
                //$"Energy={tectonicEnergy01:0.000}"
            //);
        }

        MarkEarthquakeSaveDirty();
    }

    private float CalculateCurrentEarthquakeChance()
    {
        float energyT = Mathf.Clamp01(tectonicEnergy01);

        if (energyChanceCurve != null)
            energyT = Mathf.Clamp01(energyChanceCurve.Evaluate(energyT));

        float chance = earthquakeChancePerTurn + maxEnergyChanceBonus * energyT;

        if (tectonicEnergy01 >= guaranteedCheckEnergy01)
            chance = Mathf.Max(chance, guaranteedChanceAtHighEnergy);

        return Mathf.Clamp01(chance);
    }

    private void TriggerEarthquake(bool forced)
    {
        if (!IsMapReady())
        {
            //Debug.LogWarning("EarthquakeSimulationSystem: Map is not ready for earthquakes.");
            return;
        }

        if (faultLineGenerator == null)
        {
            //Debug.LogWarning("EarthquakeSimulationSystem: Missing EarthquakeFaultLineGenerator.");
            return;
        }

        bool hasFaults = faultLineGenerator.HasFaults;

        if (!hasFaults)
        {
            if (!forced && requireFaultForNaturalEarthquakes)
            {
                if (debugLogging) {}
                    //Debug.Log("EarthquakeSimulationSystem: No fault lines, natural earthquake skipped. Energy remains stored.");

                return;
            }

            if (forced && !forcedCanUseRandomBlockWithoutFault)
            {
                if (debugLogging) {}
                    //Debug.Log("EarthquakeSimulationSystem: Forced earthquake skipped because no faults exist.");

                return;
            }
        }

        Vector2Int epicentre = hasFaults
            ? faultLineGenerator.GetRandomEpicentre()
            : GetRandomValidBlock();

        if (!mapGenerator.IsValidBlock(epicentre))
        {
            //Debug.LogWarning($"EarthquakeSimulationSystem: Invalid epicentre block {epicentre}.");
            return;
        }

        float minMag = Mathf.Min(magnitudeRange.x, magnitudeRange.y);
        float maxMag = Mathf.Max(magnitudeRange.x, magnitudeRange.y);

        float magnitude = CalculateMagnitude(minMag, maxMag, forced);
        float magT = Mathf.InverseLerp(minMag, maxMag, magnitude);

        float minRadius = Mathf.Min(minRadiusBlocks, maxRadiusBlocks);
        float maxRadius = Mathf.Max(minRadiusBlocks, maxRadiusBlocks);

        float radiusBlocks = Mathf.Lerp(minRadius, maxRadius, magT);

        GetBlocksInRadius(
            epicentre,
            radiusBlocks,
            affectedScratch,
            affectedSetScratch
        );

        EarthquakeEventData data = new EarthquakeEventData
        {
            epicentreBlock = epicentre,
            magnitude = magnitude,
            radiusBlocks = radiusBlocks,
            forced = forced,
            epicentreWasOnFault = hasFaults && faultLineGenerator.FaultBlocks.Contains(epicentre),

            affectedBlocks = new List<Vector2Int>(affectedScratch),
            affectedBlockSet = new HashSet<Vector2Int>(affectedSetScratch),

            faultBlocks = hasFaults ? faultLineGenerator.FaultBlocks : null,
            faultInfluenceBlocks = hasFaults ? faultLineGenerator.FaultInfluenceBlocks : null
        };

        if (debugLogging)
        {
            //Debug.Log(
                //$"Earthquake: magnitude={magnitude:0.0}, " +
                //$"energyBeforeRelease={tectonicEnergy01:0.000}, " +
                //$"epicentreBlock={epicentre}, " +
                //$"radiusBlocks={radiusBlocks:0.0}, " +
                //$"affectedBlocks={data.affectedBlocks.Count}, " +
                //$"epicentreWasOnFault={data.epicentreWasOnFault}, " +
                //$"forced={forced}"
            //);
        }

        OnEarthquake?.Invoke(data);

        ReleaseTectonicEnergyAfterEarthquake(magT);
    }

    private float CalculateMagnitude(float minMag, float maxMag, bool forced)
    {
        float energyInput = forced && forcedEarthquakeUsesFullEnergyForMagnitude
            ? 1f
            : tectonicEnergy01;

        float energyT = Mathf.Clamp01(energyInput);

        if (energyMagnitudeCurve != null)
            energyT = Mathf.Clamp01(energyMagnitudeCurve.Evaluate(energyT));

        float randomT = UnityEngine.Random.value;

        float finalT = Mathf.Lerp(randomT, energyT, energyMagnitudeWeight);
        finalT = Mathf.Clamp01(finalT);

        return Mathf.Lerp(minMag, maxMag, finalT);
    }

    private void ReleaseTectonicEnergyAfterEarthquake(float magnitudeT)
    {
        if (!releaseEnergyAfterEarthquake)
            return;

        float release = Mathf.Lerp(
            minEnergyReleaseOnEarthquake,
            maxEnergyReleaseOnEarthquake,
            Mathf.Clamp01(magnitudeT)
        );

        float oldEnergy = tectonicEnergy01;

        tectonicEnergy01 = Mathf.Max(
            minimumEnergyAfterRelease,
            tectonicEnergy01 - release
        );

        tectonicEnergy01 = Mathf.Clamp01(tectonicEnergy01);

        if (debugLogging)
        {
            //Debug.Log(
                //$"EarthquakeSimulationSystem: Released tectonic energy. " +
                //$"Old={oldEnergy:0.000}, Release={release:0.000}, New={tectonicEnergy01:0.000}"
            //);
        }

        MarkEarthquakeSaveDirty();
    }

    private bool IsMapReady()
    {
        ResolveReferences();

        if (mapGenerator == null || gridManager == null)
            return false;

        if (gridManager.columns <= 0 || gridManager.rows <= 0)
            return false;

        if (mapGenerator.blockSize <= 0)
            return false;

        if (mapGenerator.IsGenerating)
            return false;

        if (!mapGenerator.HasBlockTerrainData)
            return false;

        if (mapGenerator.BlockColumns <= 0 || mapGenerator.BlockRows <= 0)
            return false;

        return true;
    }

    private Vector2Int GetRandomValidBlock()
    {
        List<Vector2Int> blocks = new List<Vector2Int>();

        if (mapGenerator == null)
            return Vector2Int.zero;

        for (int bx = 0; bx < mapGenerator.BlockColumns; bx++)
        {
            for (int by = 0; by < mapGenerator.BlockRows; by++)
            {
                Vector2Int block = new Vector2Int(bx, by);

                if (mapGenerator.IsValidBlock(block))
                    blocks.Add(block);
            }
        }

        if (blocks.Count == 0)
            return Vector2Int.zero;

        return blocks[UnityEngine.Random.Range(0, blocks.Count)];
    }

    private void GetBlocksInRadius(
        Vector2Int epicentreBlock,
        float radiusBlocks,
        List<Vector2Int> results,
        HashSet<Vector2Int> resultSet)
    {
        results.Clear();
        resultSet.Clear();

        if (mapGenerator == null)
            return;

        float safeRadius = Mathf.Max(0f, radiusBlocks);

        for (int bx = 0; bx < mapGenerator.BlockColumns; bx++)
        {
            for (int by = 0; by < mapGenerator.BlockRows; by++)
            {
                Vector2Int block = new Vector2Int(bx, by);

                if (!mapGenerator.IsValidBlock(block))
                    continue;

                float distanceBlocks = Vector2Int.Distance(epicentreBlock, block);

                if (distanceBlocks > safeRadius)
                    continue;

                results.Add(block);
                resultSet.Add(block);
            }
        }
    }

    private void ResolveReferences()
    {
        if (gridManager == null)
            gridManager = GridManager.Instance;

        if (mapGenerator == null)
            mapGenerator = FindObjectOfType<MapGenerator>();

        if (faultLineGenerator == null)
            faultLineGenerator = FindObjectOfType<EarthquakeFaultLineGenerator>();
    }

    public EarthquakeSimulationSaveData SaveState()
    {
        return new EarthquakeSimulationSaveData
        {
            tectonicEnergy01 = Mathf.Clamp01(tectonicEnergy01),
            lastCalculatedChance01 = Mathf.Clamp01(lastCalculatedChance01),
            lastProcessedTurn = lastProcessedTurn
        };
    }

    public void LoadState(EarthquakeSimulationSaveData data)
    {
        if (data == null)
            return;

        ResolveReferences();

        tectonicEnergy01 = Mathf.Clamp01(data.tectonicEnergy01);
        lastCalculatedChance01 = Mathf.Clamp01(data.lastCalculatedChance01);
        lastProcessedTurn = data.lastProcessedTurn;

        if (debugLogging)
        {
            //Debug.Log(
                //$"EarthquakeSimulationSystem: Loaded state. " +
                //$"Energy={tectonicEnergy01:0.000}, " +
                //$"LastChance={lastCalculatedChance01:0.000}, " +
                //$"LastProcessedTurn={lastProcessedTurn}");
        }
    }

    private void MarkEarthquakeSaveDirty()
    {
        SaveSystem.MarkSectionDirty(SaveSectionKeys.WorldSim);
    }

    public void ApplyPresetSettings(EarthquakeWeatherPresetSettings settings)
    {
        if (settings == null || !settings.overrideEarthquakes)
            return;

        earthquakeChancePerTurn = settings.earthquakeChancePerTurn;

        minEnergyGainPerTurn = settings.minEnergyGainPerTurn;
        maxEnergyGainPerTurn = settings.maxEnergyGainPerTurn;
        faultLineEnergyGainMultiplier = settings.faultLineEnergyGainMultiplier;
        stressSpikeChancePerTurn = settings.stressSpikeChancePerTurn;
        stressSpikeEnergyGainRange = settings.stressSpikeEnergyGainRange;

        maxEnergyChanceBonus = settings.maxEnergyChanceBonus;
        magnitudeRange = settings.magnitudeRange;
        energyMagnitudeWeight = settings.energyMagnitudeWeight;

        minRadiusBlocks = settings.minRadiusBlocks;
        maxRadiusBlocks = settings.maxRadiusBlocks;

        requireFaultForNaturalEarthquakes = settings.requireFaultForNaturalEarthquakes;

        if (debugLogging) {}
            //Debug.Log("[EarthquakeSimulationSystem] Applied earthquake preset settings.");
    }

    private void ApplyPresetSettings()
    {
        var preset = EnvironmentPresetManager.Instance != null
            ? EnvironmentPresetManager.Instance.GetCurrentPreset()
            : null;

        if (preset == null)
            return;

        if (preset.weatherSettings != null && preset.weatherSettings.earthquakes != null)
        {
            ApplyPresetSettings(preset.weatherSettings.earthquakes);
            return;
        }

        // Fallback for your old setup where earthquake settings were inside PlanetarySectionSettings.
        var section = preset.planetarySection;

        if (section == null)
            return;

        earthquakeChancePerTurn = section.earthquakeChancePerTurn;
        magnitudeRange = section.earthquakeMagnitudeRange;
    }
}
