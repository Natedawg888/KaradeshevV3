using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VolcanicPrecipitationUnitEffectResolver : MonoBehaviour
{
    public static VolcanicPrecipitationUnitEffectResolver Instance { get; private set; }

    [Header("References")]
    [SerializeField] private RainSimulationSystem rainSimulationSystem;

    [Header("Timing")]
    [SerializeField] private bool applyEffectsOnEndOfTurn = true;

    [Header("Unit Damage")]
    [Min(0)][SerializeField] private int acidRainUnitGroupDamagePerTurn = 8;
    [Min(0)][SerializeField] private int ashFallUnitGroupDamagePerTurn = 4;

    [Tooltip("If true, acid/ash severity scales the damage. If false, full base damage is used.")]
    [SerializeField] private bool scaleDamageBySeverity = true;

    [Tooltip("If true, any acid/ash cell with severity > 0 deals at least 1 damage if base damage is above 0.")]
    [SerializeField] private bool minimumOneDamageWhenSeverityExists = true;

    [Tooltip("If true, each unit group is only damaged once per resolver pass.")]
    [SerializeField] private bool affectEachUnitGroupOnlyOncePerPass = true;

    [Header("Over-Frame Processing")]
    [SerializeField] private bool processOverFrames = true;

    [Min(1)]
    [SerializeField] private int cellsProcessedPerFrame = 8;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = false;

    private readonly List<RainSimulationSystem.VolcanicPrecipitationCell> activeCellsScratch =
        new List<RainSimulationSystem.VolcanicPrecipitationCell>(128);

    private readonly List<PlayerUnitManager.GroupInfo> trackedGroupsScratch =
        new List<PlayerUnitManager.GroupInfo>(128);

    private readonly List<TileUnitGroupData> tmpUnitGroupSnapshot =
        new List<TileUnitGroupData>(16);

    private readonly HashSet<string> processedUnitGroupsThisPass =
        new HashSet<string>();

    private readonly Dictionary<long, List<TileUnitGroupControl>> unitControlsByCell =
        new Dictionary<long, List<TileUnitGroupControl>>();

    private readonly List<List<TileUnitGroupControl>> pooledControlLists =
        new List<List<TileUnitGroupControl>>();

    private Coroutine processRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
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
        trackedGroupsScratch.Clear();
        tmpUnitGroupSnapshot.Clear();
        processedUnitGroupsThisPass.Clear();
        ClearUnitControlLookup();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void EnsureLinks()
    {
        if (rainSimulationSystem == null)
            rainSimulationSystem = RainSimulationSystem.Instance;
    }

    private void HandleEndOfTurn()
    {
        EnsureLinks();

        if (rainSimulationSystem == null)
            return;

        if (!rainSimulationSystem.CopyActiveVolcanicPrecipitationCells(activeCellsScratch))
            return;

        BuildUnitControlLookup();

        if (unitControlsByCell.Count == 0)
        {
            ClearUnitControlLookup();
            return;
        }

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
        processedUnitGroupsThisPass.Clear();

        int processed = 0;
        int maxPerFrame = Mathf.Max(1, cellsProcessedPerFrame);

        for (int i = 0; i < activeCellsScratch.Count; i++)
        {
            ApplyUnitEffectsAtCell(activeCellsScratch[i]);

            processed++;

            if (processed >= maxPerFrame)
            {
                processed = 0;
                yield return null;
            }
        }

        processedUnitGroupsThisPass.Clear();
        ClearUnitControlLookup();
        processRoutine = null;
    }

    private void ProcessImmediate()
    {
        processedUnitGroupsThisPass.Clear();

        for (int i = 0; i < activeCellsScratch.Count; i++)
            ApplyUnitEffectsAtCell(activeCellsScratch[i]);

        processedUnitGroupsThisPass.Clear();
        ClearUnitControlLookup();
    }

    private void BuildUnitControlLookup()
    {
        ClearUnitControlLookup();

        PlayerUnitManager unitManager = PlayerUnitManager.Instance;
        if (unitManager == null)
            return;

        trackedGroupsScratch.Clear();
        unitManager.GetAllGroups(trackedGroupsScratch);

        if (trackedGroupsScratch.Count == 0)
            return;

        for (int i = 0; i < trackedGroupsScratch.Count; i++)
        {
            PlayerUnitManager.GroupInfo info = trackedGroupsScratch[i];

            TileUnitGroupControl owner = info.owner;
            TileUnitGroupData data = info.data;

            if (owner == null || data == null)
                continue;

            if (!owner.HasAnyGroups)
                continue;

            if (!owner.TryGetOwningGridPosition(out Vector2Int ownerGrid))
                continue;

            long key = MakeGridKey(ownerGrid.x, ownerGrid.y);

            List<TileUnitGroupControl> list = GetOrCreateControlListForCell(key);

            if (!list.Contains(owner))
                list.Add(owner);
        }
    }

    private List<TileUnitGroupControl> GetOrCreateControlListForCell(long key)
    {
        if (unitControlsByCell.TryGetValue(key, out List<TileUnitGroupControl> existing))
            return existing;

        List<TileUnitGroupControl> list;

        if (pooledControlLists.Count > 0)
        {
            int last = pooledControlLists.Count - 1;
            list = pooledControlLists[last];
            pooledControlLists.RemoveAt(last);
            list.Clear();
        }
        else
        {
            list = new List<TileUnitGroupControl>(4);
        }

        unitControlsByCell.Add(key, list);
        return list;
    }

    private void ClearUnitControlLookup()
    {
        foreach (KeyValuePair<long, List<TileUnitGroupControl>> pair in unitControlsByCell)
        {
            if (pair.Value == null)
                continue;

            pair.Value.Clear();
            pooledControlLists.Add(pair.Value);
        }

        unitControlsByCell.Clear();
    }

    private void ApplyUnitEffectsAtCell(RainSimulationSystem.VolcanicPrecipitationCell cell)
    {
        int baseDamage = GetBaseDamage(cell.kind);

        if (baseDamage <= 0)
            return;

        float severity01 = Mathf.Clamp01(cell.severity01);

        if (severity01 <= 0f)
            return;

        int finalDamage = scaleDamageBySeverity
            ? Mathf.RoundToInt(baseDamage * severity01)
            : baseDamage;

        if (minimumOneDamageWhenSeverityExists && finalDamage <= 0)
            finalDamage = 1;

        if (finalDamage <= 0)
            return;

        long key = MakeGridKey(cell.x, cell.y);

        if (!unitControlsByCell.TryGetValue(key, out List<TileUnitGroupControl> controls) ||
            controls == null ||
            controls.Count == 0)
        {
            return;
        }

        for (int controlIndex = 0; controlIndex < controls.Count; controlIndex++)
        {
            TileUnitGroupControl unitControl = controls[controlIndex];

            if (unitControl == null || !unitControl.HasAnyGroups)
                continue;

            tmpUnitGroupSnapshot.Clear();

            IReadOnlyList<TileUnitGroupData> groups = unitControl.Groups;
            if (groups == null || groups.Count == 0)
                continue;

            for (int i = 0; i < groups.Count; i++)
            {
                TileUnitGroupData group = groups[i];

                if (group != null)
                    tmpUnitGroupSnapshot.Add(group);
            }

            for (int i = 0; i < tmpUnitGroupSnapshot.Count; i++)
            {
                TileUnitGroupData group = tmpUnitGroupSnapshot[i];

                if (group == null || string.IsNullOrWhiteSpace(group.groupId))
                    continue;

                if (affectEachUnitGroupOnlyOncePerPass &&
                    processedUnitGroupsThisPass.Contains(group.groupId))
                {
                    continue;
                }

                if (affectEachUnitGroupOnlyOncePerPass)
                    processedUnitGroupsThisPass.Add(group.groupId);

                ApplyDamageToUnitGroup(
                    unitControl,
                    group,
                    cell,
                    finalDamage,
                    severity01);
            }

            tmpUnitGroupSnapshot.Clear();
        }
    }

    private void ApplyDamageToUnitGroup(
        TileUnitGroupControl unitControl,
        TileUnitGroupData group,
        RainSimulationSystem.VolcanicPrecipitationCell cell,
        int finalDamage,
        float severity01)
    {
        if (unitControl == null || group == null)
            return;

        int oldUnitCount = Mathf.Max(0, group.unitCount);

        int unitsLost = group.ApplyDamageAndReturnUnitsLost(finalDamage);

        if (group.unitCount <= 0 || group.currentHealth <= 0)
        {
            if (debugLogging)
            {
                Debug.Log(
                    $"[VolcanicPrecipitationUnitEffectResolver] {cell.kind} destroyed unit group {group.groupId} " +
                    $"at ({cell.x},{cell.y}). damage={finalDamage} severity={severity01:0.00}");
            }

            unitControl.RemoveGroupDueToFatalities(group);
            ApplyPopulationLossFromUnitLoss(group, oldUnitCount, oldUnitCount);
            return;
        }

        if (unitsLost > 0)
            ApplyPopulationLossFromUnitLoss(group, oldUnitCount, unitsLost);

        unitControl.RefreshMarker(group);

        if (debugLogging)
        {
            Debug.Log(
                $"[VolcanicPrecipitationUnitEffectResolver] {cell.kind} damaged unit group {group.groupId} " +
                $"at ({cell.x},{cell.y}). damage={finalDamage} unitsLost={unitsLost} severity={severity01:0.00}");
        }
    }

    private int GetBaseDamage(RainSimulationSystem.RainVisualKind kind)
    {
        switch (kind)
        {
            case RainSimulationSystem.RainVisualKind.AcidRain:
                return acidRainUnitGroupDamagePerTurn;

            case RainSimulationSystem.RainVisualKind.AshFall:
                return ashFallUnitGroupDamagePerTurn;

            default:
                return 0;
        }
    }

    private void ApplyPopulationLossFromUnitLoss(TileUnitGroupData group, int oldUnitCount, int unitsLost)
    {
        if (group == null)
            return;

        if (unitsLost <= 0 || oldUnitCount <= 0)
            return;

        if (string.IsNullOrWhiteSpace(group.populationReservationId) || group.reservedPopulation <= 0)
            return;

        PlayersPopulationManager pop = PlayersPopulationManager.Instance;
        if (pop == null)
            return;

        int populationLoss = Mathf.Clamp(
            Mathf.RoundToInt(group.reservedPopulation * (unitsLost / (float)oldUnitCount)),
            0,
            group.reservedPopulation
        );

        if (populationLoss <= 0)
            return;

        pop.ApplyPenaltyFromReservation(group.populationReservationId, populationLoss);
        group.reservedPopulation = Mathf.Max(0, group.reservedPopulation - populationLoss);

        if (group.reservedPopulation <= 0)
        {
            pop.ReleaseReservation(group.populationReservationId);
            group.populationReservationId = null;
            group.reservedPopulation = 0;
        }
    }

    private static long MakeGridKey(int x, int y)
    {
        return ((long)x << 32) ^ (uint)y;
    }
}