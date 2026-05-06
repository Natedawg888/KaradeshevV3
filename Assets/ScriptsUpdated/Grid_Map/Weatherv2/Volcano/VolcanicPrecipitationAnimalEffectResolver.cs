using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VolcanicPrecipitationAnimalEffectResolver : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RainSimulationSystem rainSimulationSystem;
    [SerializeField] private AnimalSimulation animalSimulation;

    [Header("Timing")]
    [SerializeField] private bool applyEffectsOnEndOfTurn = true;

    [Header("Animal Health Damage")]
    [Min(0)][SerializeField] private int acidRainAnimalDamagePerTurn = 6;
    [Min(0)][SerializeField] private int ashFallAnimalDamagePerTurn = 3;

    [Tooltip("If true, each animal group is only damaged once per resolver pass.")]
    [SerializeField] private bool affectEachGroupOnlyOncePerPass = true;

    [Header("Over-Frame Processing")]
    [SerializeField] private bool processOverFrames = true;

    [Min(1)]
    [SerializeField] private int cellsProcessedPerFrame = 8;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = false;

    private readonly List<RainSimulationSystem.VolcanicPrecipitationCell> activeCellsScratch =
        new List<RainSimulationSystem.VolcanicPrecipitationCell>(128);

    private readonly List<int> groupIdsScratch = new List<int>(16);
    private readonly HashSet<int> processedGroupsThisPass = new HashSet<int>();

    private Coroutine processRoutine;

    private void Awake()
    {
        EnsureLinks();
    }

    private void OnEnable()
    {
        EnsureLinks();

        if (applyEffectsOnEndOfTurn)
            TurnSystem.SubscribeToEndOfTurn(HandleEndOfTurn);
    }

    private void Start()
    {
        EnsureLinks();
    }

    private void OnDisable()
    {
        TurnSystem.UnsubscribeFromEndOfTurn(HandleEndOfTurn);

        if (processRoutine != null)
        {
            StopCoroutine(processRoutine);
            processRoutine = null;
        }

        activeCellsScratch.Clear();
        groupIdsScratch.Clear();
        processedGroupsThisPass.Clear();
    }

    private void EnsureLinks()
    {
        if (rainSimulationSystem == null)
            rainSimulationSystem = RainSimulationSystem.Instance;

        if (animalSimulation == null)
            animalSimulation = AnimalSimulationAccess.Current;
    }

    private void HandleEndOfTurn()
    {
        EnsureLinks();

        if (rainSimulationSystem == null || animalSimulation == null)
            return;

        if (!rainSimulationSystem.CopyActiveVolcanicPrecipitationCells(activeCellsScratch))
            return;

        if (processOverFrames)
        {
            if (processRoutine == null)
                processRoutine = StartCoroutine(ProcessRoutine());
        }
        else
        {
            ProcessImmediate();
        }
    }

    private IEnumerator ProcessRoutine()
    {
        processedGroupsThisPass.Clear();

        int processed = 0;
        int maxPerFrame = Mathf.Max(1, cellsProcessedPerFrame);

        for (int i = 0; i < activeCellsScratch.Count; i++)
        {
            ApplyAnimalEffectsAtCell(activeCellsScratch[i]);

            processed++;

            if (processed >= maxPerFrame)
            {
                processed = 0;
                yield return null;
            }
        }

        processedGroupsThisPass.Clear();
        processRoutine = null;
    }

    private void ProcessImmediate()
    {
        processedGroupsThisPass.Clear();

        for (int i = 0; i < activeCellsScratch.Count; i++)
            ApplyAnimalEffectsAtCell(activeCellsScratch[i]);

        processedGroupsThisPass.Clear();
    }

    private void ApplyAnimalEffectsAtCell(RainSimulationSystem.VolcanicPrecipitationCell cell)
    {
        EnsureLinks();

        if (animalSimulation == null)
            return;

        TileCoord coord = new TileCoord(cell.x, cell.y);

        if (!animalSimulation.HasGroupsAtTile(coord))
            return;

        groupIdsScratch.Clear();
        int count = animalSimulation.GetGroupIdsAtTileNonAlloc(coord, groupIdsScratch);

        if (count <= 0)
            return;

        int baseDamage = GetBaseDamage(cell.kind);

        for (int i = 0; i < groupIdsScratch.Count; i++)
        {
            int groupId = groupIdsScratch[i];

            if (affectEachGroupOnlyOncePerPass && processedGroupsThisPass.Contains(groupId))
                continue;

            if (affectEachGroupOnlyOncePerPass)
                processedGroupsThisPass.Add(groupId);

            animalSimulation.TryApplyVolcanicPrecipitationDamageToGroup(
                groupId,
                cell.kind,
                baseDamage,
                cell.severity01,
                debugLogging);
        }
    }

    private int GetBaseDamage(RainSimulationSystem.RainVisualKind kind)
    {
        switch (kind)
        {
            case RainSimulationSystem.RainVisualKind.AcidRain:
                return acidRainAnimalDamagePerTurn;

            case RainSimulationSystem.RainVisualKind.AshFall:
                return ashFallAnimalDamagePerTurn;

            default:
                return 0;
        }
    }
}