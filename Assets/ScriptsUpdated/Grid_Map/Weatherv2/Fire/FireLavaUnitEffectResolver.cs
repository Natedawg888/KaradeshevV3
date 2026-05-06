using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FireLavaUnitEffectResolver : MonoBehaviour
{
    public static FireLavaUnitEffectResolver Instance { get; private set; }

    private enum UnitHazardKind
    {
        FireIgnition,
        FireOngoing,
        LavaActivation,
        LavaOngoing
    }

    [Header("References")]
    [SerializeField] private WeatherFireSystem weatherFireSystem;
    [SerializeField] private LavaOverlayManager lavaOverlayManager;

    [Header("Fire Timing")]
    [SerializeField] private bool applyWhenFireIgnites = true;
    [SerializeField] private bool applyOngoingFireEachTurn = true;

    [Header("Lava Timing")]
    [Tooltip("Best option. Units are damaged before lava officially occupies the cell.")]
    [SerializeField] private bool applyBeforeLavaCellActivates = true;

    [Tooltip("Usually keep false if Apply Before Lava Cell Activates is true, or units may take activation damage twice.")]
    [SerializeField] private bool applyWhenLavaCellActivates = false;

    [SerializeField] private bool applyOngoingLavaEachTurn = true;

    [Header("Fire Unit Damage")]
    [Min(0)][SerializeField] private int fireIgnitionUnitDamage = 6;
    [Min(0)][SerializeField] private int ongoingFireUnitDamagePerTurn = 4;

    [Header("Lava Unit Damage")]
    [Min(0)][SerializeField] private int lavaActivationUnitDamage = 40;
    [Min(0)][SerializeField] private int ongoingLavaUnitDamagePerTurn = 18;

    [Tooltip("If true, cooling lava deals less ongoing damage.")]
    [SerializeField] private bool scaleOngoingLavaDamageByHeat = true;

    [Tooltip("If true, active cooling lava with heat above 0 can still deal at least 1 damage.")]
    [SerializeField] private bool minimumOneLavaDamageWhileActive = true;

    [Header("Pass Rules")]
    [SerializeField] private bool affectEachUnitGroupOnlyOncePerFirePass = true;
    [SerializeField] private bool affectEachUnitGroupOnlyOncePerLavaPass = true;

    [Header("Population")]
    [SerializeField] private bool applyPopulationLossFromUnitLoss = true;

    [Header("Over-Frame Processing")]
    [SerializeField] private bool processOngoingOverFrames = true;

    [Min(1)]
    [SerializeField] private int hazardCellsProcessedPerFrame = 8;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = false;

    private readonly List<TileCoord> activeFireCellsScratch = new List<TileCoord>(128);
    private readonly List<TileCoord> activeLavaCellsScratch = new List<TileCoord>(128);

    private readonly List<PlayerUnitManager.GroupInfo> trackedGroupsScratch =
        new List<PlayerUnitManager.GroupInfo>(128);

    private readonly List<TileUnitGroupData> tmpUnitGroupSnapshot =
        new List<TileUnitGroupData>(16);

    private readonly List<TileUnitGroupControl> unitControlsAtTileScratch =
        new List<TileUnitGroupControl>(8);

    private readonly HashSet<TileUnitGroupControl> uniqueUnitControlsAtTileScratch =
        new HashSet<TileUnitGroupControl>();

    private readonly HashSet<string> processedFireGroupsThisPass =
        new HashSet<string>();

    private readonly HashSet<string> processedLavaGroupsThisPass =
        new HashSet<string>();

    private readonly Dictionary<long, List<TileUnitGroupControl>> unitControlsByCell =
        new Dictionary<long, List<TileUnitGroupControl>>();

    private readonly List<List<TileUnitGroupControl>> pooledControlLists =
        new List<List<TileUnitGroupControl>>();

    private WeatherFireSystem subscribedWeatherFireSystem;
    private LavaOverlayManager subscribedLavaOverlayManager;

    private Coroutine ongoingRoutine;

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
        RebindHazardEvents();

        if (applyOngoingFireEachTurn || applyOngoingLavaEachTurn)
            TurnSystem.SubscribeToEndOfTurn(HandleEndOfTurn);
    }

    private void Start()
    {
        EnsureLinks();
        RebindHazardEvents();
    }

    private void OnDisable()
    {
        UnbindHazardEvents();
        TurnSystem.UnsubscribeFromEndOfTurn(HandleEndOfTurn);

        if (ongoingRoutine != null)
        {
            StopCoroutine(ongoingRoutine);
            ongoingRoutine = null;
        }

        activeFireCellsScratch.Clear();
        activeLavaCellsScratch.Clear();
        trackedGroupsScratch.Clear();
        tmpUnitGroupSnapshot.Clear();
        unitControlsAtTileScratch.Clear();
        uniqueUnitControlsAtTileScratch.Clear();
        processedFireGroupsThisPass.Clear();
        processedLavaGroupsThisPass.Clear();

        ClearUnitControlLookup();
    }

    private void OnDestroy()
    {
        UnbindHazardEvents();

        if (Instance == this)
            Instance = null;
    }

    private void EnsureLinks()
    {
        if (weatherFireSystem == null)
            weatherFireSystem = WeatherFireSystem.Instance;

        if (lavaOverlayManager == null)
            lavaOverlayManager = LavaOverlayManager.Instance;
    }

    private void RebindHazardEvents()
    {
        RebindFireEvents();
        RebindLavaEvents();
    }

    private void RebindFireEvents()
    {
        if (subscribedWeatherFireSystem == weatherFireSystem)
            return;

        if (subscribedWeatherFireSystem != null)
            subscribedWeatherFireSystem.OnFireCellIgnited -= HandleFireCellIgnited;

        subscribedWeatherFireSystem = weatherFireSystem;

        if (subscribedWeatherFireSystem != null && applyWhenFireIgnites)
            subscribedWeatherFireSystem.OnFireCellIgnited += HandleFireCellIgnited;
    }

    private void RebindLavaEvents()
    {
        if (subscribedLavaOverlayManager == lavaOverlayManager)
            return;

        if (subscribedLavaOverlayManager != null)
        {
            subscribedLavaOverlayManager.OnBeforeLavaCellActivated -= HandleBeforeLavaCellActivated;
            subscribedLavaOverlayManager.OnLavaCellActivated -= HandleLavaCellActivated;
        }

        subscribedLavaOverlayManager = lavaOverlayManager;

        if (subscribedLavaOverlayManager == null)
            return;

        if (applyBeforeLavaCellActivates)
            subscribedLavaOverlayManager.OnBeforeLavaCellActivated += HandleBeforeLavaCellActivated;

        if (applyWhenLavaCellActivates)
            subscribedLavaOverlayManager.OnLavaCellActivated += HandleLavaCellActivated;
    }

    private void UnbindHazardEvents()
    {
        if (subscribedWeatherFireSystem != null)
            subscribedWeatherFireSystem.OnFireCellIgnited -= HandleFireCellIgnited;

        if (subscribedLavaOverlayManager != null)
        {
            subscribedLavaOverlayManager.OnBeforeLavaCellActivated -= HandleBeforeLavaCellActivated;
            subscribedLavaOverlayManager.OnLavaCellActivated -= HandleLavaCellActivated;
        }

        subscribedWeatherFireSystem = null;
        subscribedLavaOverlayManager = null;
    }

    private void HandleFireCellIgnited(TileCoord coord)
    {
        if (!applyWhenFireIgnites)
            return;

        processedFireGroupsThisPass.Clear();

        ApplyImmediateUnitEffectsAtCell(
            coord,
            fireIgnitionUnitDamage,
            UnitHazardKind.FireIgnition,
            damageMultiplier: 1f,
            processedFireGroupsThisPass,
            affectEachUnitGroupOnlyOncePerFirePass);

        processedFireGroupsThisPass.Clear();
    }

    private void HandleBeforeLavaCellActivated(TileCoord coord)
    {
        if (!applyBeforeLavaCellActivates)
            return;

        processedLavaGroupsThisPass.Clear();

        ApplyImmediateUnitEffectsAtCell(
            coord,
            lavaActivationUnitDamage,
            UnitHazardKind.LavaActivation,
            damageMultiplier: 1f,
            processedLavaGroupsThisPass,
            affectEachUnitGroupOnlyOncePerLavaPass);

        processedLavaGroupsThisPass.Clear();
    }

    private void HandleLavaCellActivated(TileCoord coord)
    {
        if (!applyWhenLavaCellActivates)
            return;

        processedLavaGroupsThisPass.Clear();

        ApplyImmediateUnitEffectsAtCell(
            coord,
            lavaActivationUnitDamage,
            UnitHazardKind.LavaActivation,
            damageMultiplier: 1f,
            processedLavaGroupsThisPass,
            affectEachUnitGroupOnlyOncePerLavaPass);

        processedLavaGroupsThisPass.Clear();
    }

    private void HandleEndOfTurn()
    {
        EnsureLinks();

        activeFireCellsScratch.Clear();
        activeLavaCellsScratch.Clear();

        bool hasFire = applyOngoingFireEachTurn &&
                       weatherFireSystem != null &&
                       weatherFireSystem.CopyActiveFireCells(activeFireCellsScratch);

        bool hasLava = applyOngoingLavaEachTurn &&
                       lavaOverlayManager != null &&
                       lavaOverlayManager.CopyActiveLavaCells(activeLavaCellsScratch);

        if (!hasFire && !hasLava)
            return;

        BuildUnitControlLookup();

        if (unitControlsByCell.Count == 0)
        {
            ClearUnitControlLookup();
            return;
        }

        if (processOngoingOverFrames)
        {
            if (ongoingRoutine == null)
                ongoingRoutine = StartCoroutine(ProcessOngoingRoutine(hasFire, hasLava));
        }
        else
        {
            ProcessOngoingImmediate(hasFire, hasLava);
        }
    }

    private IEnumerator ProcessOngoingRoutine(bool processFire, bool processLava)
    {
        processedFireGroupsThisPass.Clear();
        processedLavaGroupsThisPass.Clear();

        int processedCells = 0;
        int maxPerFrame = Mathf.Max(1, hazardCellsProcessedPerFrame);

        if (processFire)
        {
            for (int i = 0; i < activeFireCellsScratch.Count; i++)
            {
                ApplyLookupUnitEffectsAtCell(
                    activeFireCellsScratch[i],
                    ongoingFireUnitDamagePerTurn,
                    UnitHazardKind.FireOngoing,
                    damageMultiplier: 1f,
                    processedFireGroupsThisPass,
                    affectEachUnitGroupOnlyOncePerFirePass);

                processedCells++;

                if (processedCells >= maxPerFrame)
                {
                    processedCells = 0;
                    yield return null;
                }
            }
        }

        if (processLava)
        {
            for (int i = 0; i < activeLavaCellsScratch.Count; i++)
            {
                TileCoord coord = activeLavaCellsScratch[i];

                float heat01 = GetLavaHeat01(coord);
                float lavaDamageMultiplier = scaleOngoingLavaDamageByHeat
                    ? Mathf.Clamp01(heat01)
                    : 1f;

                ApplyLookupUnitEffectsAtCell(
                    coord,
                    ongoingLavaUnitDamagePerTurn,
                    UnitHazardKind.LavaOngoing,
                    lavaDamageMultiplier,
                    processedLavaGroupsThisPass,
                    affectEachUnitGroupOnlyOncePerLavaPass);

                processedCells++;

                if (processedCells >= maxPerFrame)
                {
                    processedCells = 0;
                    yield return null;
                }
            }
        }

        processedFireGroupsThisPass.Clear();
        processedLavaGroupsThisPass.Clear();
        ClearUnitControlLookup();

        ongoingRoutine = null;
    }

    private void ProcessOngoingImmediate(bool processFire, bool processLava)
    {
        processedFireGroupsThisPass.Clear();
        processedLavaGroupsThisPass.Clear();

        if (processFire)
        {
            for (int i = 0; i < activeFireCellsScratch.Count; i++)
            {
                ApplyLookupUnitEffectsAtCell(
                    activeFireCellsScratch[i],
                    ongoingFireUnitDamagePerTurn,
                    UnitHazardKind.FireOngoing,
                    damageMultiplier: 1f,
                    processedFireGroupsThisPass,
                    affectEachUnitGroupOnlyOncePerFirePass);
            }
        }

        if (processLava)
        {
            for (int i = 0; i < activeLavaCellsScratch.Count; i++)
            {
                TileCoord coord = activeLavaCellsScratch[i];

                float heat01 = GetLavaHeat01(coord);
                float lavaDamageMultiplier = scaleOngoingLavaDamageByHeat
                    ? Mathf.Clamp01(heat01)
                    : 1f;

                ApplyLookupUnitEffectsAtCell(
                    coord,
                    ongoingLavaUnitDamagePerTurn,
                    UnitHazardKind.LavaOngoing,
                    lavaDamageMultiplier,
                    processedLavaGroupsThisPass,
                    affectEachUnitGroupOnlyOncePerLavaPass);
            }
        }

        processedFireGroupsThisPass.Clear();
        processedLavaGroupsThisPass.Clear();
        ClearUnitControlLookup();
    }

    private void ApplyImmediateUnitEffectsAtCell(
        TileCoord coord,
        int baseDamage,
        UnitHazardKind hazard,
        float damageMultiplier,
        HashSet<string> processedSet,
        bool affectOnce)
    {
        if (baseDamage <= 0)
            return;

        if (!CollectUnitControlsAtTile(coord, unitControlsAtTileScratch))
            return;

        for (int i = 0; i < unitControlsAtTileScratch.Count; i++)
        {
            ApplyEffectsToUnitControl(
                unitControlsAtTileScratch[i],
                coord,
                baseDamage,
                hazard,
                damageMultiplier,
                processedSet,
                affectOnce);
        }
    }

    private void ApplyLookupUnitEffectsAtCell(
        TileCoord coord,
        int baseDamage,
        UnitHazardKind hazard,
        float damageMultiplier,
        HashSet<string> processedSet,
        bool affectOnce)
    {
        if (baseDamage <= 0)
            return;

        long key = MakeGridKey(coord.x, coord.y);

        if (!unitControlsByCell.TryGetValue(key, out List<TileUnitGroupControl> controls) ||
            controls == null ||
            controls.Count == 0)
        {
            return;
        }

        for (int i = 0; i < controls.Count; i++)
        {
            ApplyEffectsToUnitControl(
                controls[i],
                coord,
                baseDamage,
                hazard,
                damageMultiplier,
                processedSet,
                affectOnce);
        }
    }

    private void ApplyEffectsToUnitControl(
        TileUnitGroupControl unitControl,
        TileCoord coord,
        int baseDamage,
        UnitHazardKind hazard,
        float damageMultiplier,
        HashSet<string> processedSet,
        bool affectOnce)
    {
        if (unitControl == null || !unitControl.HasAnyGroups)
            return;

        int finalDamage = CalculateFinalDamage(baseDamage, hazard, damageMultiplier);

        if (finalDamage <= 0)
            return;

        tmpUnitGroupSnapshot.Clear();

        IReadOnlyList<TileUnitGroupData> groups = unitControl.Groups;

        if (groups == null || groups.Count == 0)
            return;

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

            if (affectOnce && processedSet != null && processedSet.Contains(group.groupId))
                continue;

            if (affectOnce && processedSet != null)
                processedSet.Add(group.groupId);

            ApplyDamageToUnitGroup(
                unitControl,
                group,
                coord,
                finalDamage,
                hazard);
        }

        tmpUnitGroupSnapshot.Clear();
    }

    private int CalculateFinalDamage(
        int baseDamage,
        UnitHazardKind hazard,
        float damageMultiplier)
    {
        baseDamage = Mathf.Max(0, baseDamage);

        if (baseDamage <= 0)
            return 0;

        damageMultiplier = Mathf.Clamp01(damageMultiplier);

        int finalDamage = Mathf.RoundToInt(baseDamage * damageMultiplier);

        if (hazard == UnitHazardKind.LavaOngoing &&
            minimumOneLavaDamageWhileActive &&
            damageMultiplier > 0f &&
            finalDamage <= 0)
        {
            finalDamage = 1;
        }

        return Mathf.Max(0, finalDamage);
    }

    private void ApplyDamageToUnitGroup(
        TileUnitGroupControl unitControl,
        TileUnitGroupData group,
        TileCoord coord,
        int finalDamage,
        UnitHazardKind hazard)
    {
        if (unitControl == null || group == null || finalDamage <= 0)
            return;

        int oldUnitCount = Mathf.Max(0, group.unitCount);

        int unitsLost = group.ApplyDamageAndReturnUnitsLost(finalDamage);

        if (group.unitCount <= 0 || group.currentHealth <= 0)
        {
            ApplyPopulationLossFromUnitLoss(group, oldUnitCount, oldUnitCount);

            if (debugLogging)
            {
                Debug.Log(
                    $"[FireLavaUnitEffectResolver] {hazard} destroyed unit group {group.groupId} " +
                    $"at ({coord.x},{coord.y}). Damage={finalDamage}");
            }

            unitControl.RemoveGroupDueToFatalities(group);
            return;
        }

        if (unitsLost > 0)
            ApplyPopulationLossFromUnitLoss(group, oldUnitCount, unitsLost);

        unitControl.RefreshMarker(group);

        if (debugLogging)
        {
            Debug.Log(
                $"[FireLavaUnitEffectResolver] {hazard} damaged unit group {group.groupId} " +
                $"at ({coord.x},{coord.y}). Damage={finalDamage} UnitsLost={unitsLost}");
        }
    }

    private bool CollectUnitControlsAtTile(TileCoord coord, List<TileUnitGroupControl> results)
    {
        if (results == null)
            return false;

        results.Clear();
        uniqueUnitControlsAtTileScratch.Clear();
        trackedGroupsScratch.Clear();

        PlayerUnitManager unitManager = PlayerUnitManager.Instance;
        if (unitManager == null)
            return false;

        unitManager.GetAllGroups(trackedGroupsScratch);

        if (trackedGroupsScratch.Count == 0)
            return false;

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

            if (ownerGrid.x != coord.x || ownerGrid.y != coord.y)
                continue;

            if (uniqueUnitControlsAtTileScratch.Add(owner))
                results.Add(owner);
        }

        return results.Count > 0;
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

    private float GetLavaHeat01(TileCoord coord)
    {
        if (lavaOverlayManager == null)
            return 1f;

        return Mathf.Clamp01(lavaOverlayManager.GetLavaHeat01AtCell(coord));
    }

    private void ApplyPopulationLossFromUnitLoss(
        TileUnitGroupData group,
        int oldUnitCount,
        int unitsLost)
    {
        if (!applyPopulationLossFromUnitLoss)
            return;

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