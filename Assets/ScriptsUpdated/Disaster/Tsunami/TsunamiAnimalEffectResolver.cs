using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TsunamiAnimalEffectResolver : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TsunamiSimulationSystem simulationSystem;
    [SerializeField] private GridManager gridManager;
    [SerializeField] private AnimalSimulation animalSimulation;

    [Header("Energy")]
    [Tooltip("Tsunami energy01 must be at or above this before animals are affected.")]
    [Range(0f, 1f)]
    [SerializeField] private float animalEffectsStartAtEnergy01 = 0.10f;

    [Tooltip("Energy01 at or above this uses strongest animal effects.")]
    [Range(0f, 1f)]
    [SerializeField] private float severeAnimalEffectsEnergy01 = 1f;

    [Header("Animal Flee")]
    [Range(0f, 1f)]
    [SerializeField] private float minFleeChance = 0.20f;

    [Range(0f, 1f)]
    [SerializeField] private float maxFleeChance = 0.90f;

    [Min(1)]
    [SerializeField] private int fleeSearchDistance = 3;

    [Header("If Flee Fails")]
    [SerializeField] private bool instantKillIfFleeFails = false;

    [Min(0)]
    [SerializeField] private int minDamageIfFleeFails = 3;

    [Min(0)]
    [SerializeField] private int maxDamageIfFleeFails = 30;

    [Header("Filtering")]
    [Range(0f, 1f)]
    [SerializeField] private float minThreatSeverityToAffect = 0.05f;

    [Tooltip("If true, flee tiles cannot be active tsunami cells.")]
    [SerializeField] private bool avoidActiveTsunamiCellsWhenFleeing = true;

    [Tooltip("If true, flee tiles cannot be any tile visited by this tsunami step pass. For now this matches active tsunami cells only.")]
    [SerializeField] private bool avoidCurrentDangerCellWhenFleeing = true;

    [Header("Over-Frame Processing")]
    [SerializeField] private bool processOverFrames = true;

    [Min(1)]
    [SerializeField] private int cellsProcessedPerFrame = 32;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = true;

    private TsunamiSimulationSystem subscribedSimulationSystem;
    private Coroutine processRoutine;

    private readonly HashSet<TileCoord> activeTsunamiCells = new HashSet<TileCoord>();
    private readonly List<int> groupIdsScratch = new List<int>(16);
    private readonly HashSet<int> processedGroupsThisPass = new HashSet<int>();

    private TileCoord currentTsunamiCell;
    private bool hasCurrentTsunamiCell;

    private void Awake()
    {
        EnsureLinks();
    }

    private void OnEnable()
    {
        EnsureLinks();
        RebindTsunamiEvents();
    }

    private void Start()
    {
        EnsureLinks();
        RebindTsunamiEvents();
    }

    private void OnDisable()
    {
        UnbindTsunamiEvents();

        if (processRoutine != null)
        {
            StopCoroutine(processRoutine);
            processRoutine = null;
        }

        activeTsunamiCells.Clear();
        groupIdsScratch.Clear();
        processedGroupsThisPass.Clear();

        hasCurrentTsunamiCell = false;
    }

    public void InstallRuntimeRefs(
        TsunamiSimulationSystem newSimulationSystem,
        GridManager newGridManager,
        AnimalSimulation newAnimalSimulation)
    {
        if (newSimulationSystem != null)
            simulationSystem = newSimulationSystem;

        if (newGridManager != null)
            gridManager = newGridManager;

        if (newAnimalSimulation != null)
            animalSimulation = newAnimalSimulation;

        RebindTsunamiEvents();

        if (debugLogging)
        {
            //Debug.Log(
                //$"[TsunamiAnimalEffectResolver] Installed refs. " +
                //$"Simulation={(simulationSystem != null ? simulationSystem.name : "NULL")}, " +
                //$"GridManager={(gridManager != null ? gridManager.name : "NULL")} "
            //);
        }
    }

    public void SetAnimalSimulation(AnimalSimulation newAnimalSimulation)
    {
        animalSimulation = newAnimalSimulation;
    }

    private void EnsureLinks()
    {
        if (simulationSystem == null)
            simulationSystem = TsunamiSimulationSystem.Instance;

        if (simulationSystem == null)
            simulationSystem = FindObjectOfType<TsunamiSimulationSystem>();

        if (gridManager == null)
            gridManager = GridManager.Instance;

        if (animalSimulation == null)
            animalSimulation = AnimalSimulationAccess.Current;
    }

    private void RebindTsunamiEvents()
    {
        if (subscribedSimulationSystem == simulationSystem)
            return;

        UnbindTsunamiEvents();

        subscribedSimulationSystem = simulationSystem;

        if (subscribedSimulationSystem != null)
            subscribedSimulationSystem.OnTsunamiAdvanced += HandleTsunamiAdvanced;
    }

    private void UnbindTsunamiEvents()
    {
        if (subscribedSimulationSystem == null)
            return;

        subscribedSimulationSystem.OnTsunamiAdvanced -= HandleTsunamiAdvanced;
        subscribedSimulationSystem = null;
    }

    private void HandleTsunamiAdvanced(TsunamiAdvancedEventData data)
    {
        EnsureLinks();

        if (data == null || data.activeCells == null || data.activeCells.Count == 0)
            return;

        if (animalSimulation == null || gridManager == null)
        {
            if (debugLogging) {}
                //Debug.LogWarning("[TsunamiAnimalEffectResolver] Missing references.");

            return;
        }

        if (processRoutine != null)
            StopCoroutine(processRoutine);

        activeTsunamiCells.Clear();

        for (int i = 0; i < data.activeCells.Count; i++)
            activeTsunamiCells.Add(data.activeCells[i]);

        if (processOverFrames)
            processRoutine = StartCoroutine(ProcessTsunamiAnimalEffectsRoutine(data));
        else
            ProcessTsunamiAnimalEffectsImmediate(data);
    }

    private IEnumerator ProcessTsunamiAnimalEffectsRoutine(TsunamiAdvancedEventData data)
    {
        processedGroupsThisPass.Clear();

        int processedCells = 0;
        int maxPerFrame = Mathf.Max(1, cellsProcessedPerFrame);
        int affectedGroups = 0;

        float severity01 = GetEnergySeverity01(data.energy01);

        if (severity01 <= 0f)
        {
            if (debugLogging)
            {
                //Debug.Log(
                    //$"[TsunamiAnimalEffectResolver] Energy too low for animal effects. " +
                    //$"TsunamiId={data.tsunamiId}, Energy01={data.energy01:0.00}");
            }

            processRoutine = null;
            yield break;
        }

        for (int i = 0; i < data.activeCells.Count; i++)
        {
            TileCoord cell = data.activeCells[i];
            affectedGroups += ApplyAnimalTsunamiThreatAtCell(cell, data, severity01);

            processedCells++;

            if (processedCells >= maxPerFrame)
            {
                processedCells = 0;
                yield return null;
            }
        }

        if (debugLogging)
        {
            //Debug.Log(
                //$"[TsunamiAnimalEffectResolver] Complete. " +
                //$"TsunamiId={data.tsunamiId}, Step={data.stepCount}, " +
                //$"AffectedGroups={affectedGroups}, ActiveCells={activeTsunamiCells.Count}");
        }

        processedGroupsThisPass.Clear();
        processRoutine = null;
    }

    private void ProcessTsunamiAnimalEffectsImmediate(TsunamiAdvancedEventData data)
    {
        processedGroupsThisPass.Clear();

        int affectedGroups = 0;
        float severity01 = GetEnergySeverity01(data.energy01);

        if (severity01 <= 0f)
        {
            if (debugLogging)
            {
                //Debug.Log(
                    //$"[TsunamiAnimalEffectResolver] Energy too low for animal effects. " +
                    //$"TsunamiId={data.tsunamiId}, Energy01={data.energy01:0.00}");
            }

            return;
        }

        for (int i = 0; i < data.activeCells.Count; i++)
            affectedGroups += ApplyAnimalTsunamiThreatAtCell(data.activeCells[i], data, severity01);

        if (debugLogging)
        {
            //Debug.Log(
                //$"[TsunamiAnimalEffectResolver] Complete. " +
                //$"TsunamiId={data.tsunamiId}, Step={data.stepCount}, " +
                //$"AffectedGroups={affectedGroups}, ActiveCells={activeTsunamiCells.Count}");
        }

        processedGroupsThisPass.Clear();
    }

    private int ApplyAnimalTsunamiThreatAtCell(
        TileCoord coord,
        TsunamiAdvancedEventData data,
        float severity01)
    {
        if (IsOutsideGrid(coord))
            return 0;

        if (!animalSimulation.HasGroupsAtTile(coord))
            return 0;

        if (severity01 < minThreatSeverityToAffect)
            return 0;

        groupIdsScratch.Clear();

        int count = animalSimulation.GetGroupIdsAtTileNonAlloc(coord, groupIdsScratch);

        if (count <= 0)
            return 0;

        hasCurrentTsunamiCell = true;
        currentTsunamiCell = coord;

        int affected = 0;

        float fleeChance = Mathf.Lerp(minFleeChance, maxFleeChance, severity01);
        int damageIfFleeFails = Mathf.RoundToInt(
            Mathf.Lerp(minDamageIfFleeFails, maxDamageIfFleeFails, severity01)
        );

        for (int i = 0; i < groupIdsScratch.Count; i++)
        {
            int groupId = groupIdsScratch[i];

            if (processedGroupsThisPass.Contains(groupId))
                continue;

            processedGroupsThisPass.Add(groupId);

            bool changed = animalSimulation.TryApplyTsunamiThreatToGroup(
                groupId,
                fleeChance,
                instantKillIfFleeFails,
                damageIfFleeFails,
                fleeSearchDistance,
                IsDangerousTsunamiTile,
                IsValidFleeTile,
                debugLogging
            );

            if (changed)
                affected++;

            if (debugLogging)
            {
                //Debug.Log(
                    //$"[TsunamiAnimalEffectResolver] Tsunami animal group {groupId} at {coord}. " +
                    //$"TsunamiId={data.tsunamiId}, Step={data.stepCount}, " +
                    //$"Energy01={data.energy01:0.00}, Severity={severity01:0.00}, " +
                    //$"FleeChance={fleeChance:0.00}, DamageIfFail={damageIfFleeFails}"
                //);
            }
        }

        hasCurrentTsunamiCell = false;

        return affected;
    }

    private float GetEnergySeverity01(float energy01)
    {
        float min = Mathf.Min(animalEffectsStartAtEnergy01, severeAnimalEffectsEnergy01);
        float max = Mathf.Max(animalEffectsStartAtEnergy01, severeAnimalEffectsEnergy01);

        return Mathf.Clamp01(Mathf.InverseLerp(min, max, energy01));
    }

    private bool IsDangerousTsunamiTile(TileCoord coord)
    {
        if (hasCurrentTsunamiCell && avoidCurrentDangerCellWhenFleeing && coord.Equals(currentTsunamiCell))
            return true;

        if (avoidActiveTsunamiCellsWhenFleeing && activeTsunamiCells.Contains(coord))
            return true;

        return false;
    }

    private bool IsValidFleeTile(TileCoord coord)
    {
        if (IsOutsideGrid(coord))
            return false;

        if (IsDangerousTsunamiTile(coord))
            return false;

        return true;
    }

    private bool IsOutsideGrid(TileCoord coord)
    {
        return gridManager == null ||
               coord.x < 0 ||
               coord.y < 0 ||
               coord.x >= gridManager.columns ||
               coord.y >= gridManager.rows;
    }
}
