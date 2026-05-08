using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerTrainingManager : MonoBehaviour
{
    public static PlayerTrainingManager Instance { get; private set; }

    [Header("Batching")]
    [Tooltip("How many completed training orders to finalize per frame.")]
    [Min(1)] public int completionsPerFrame = 100;

    private readonly Queue<KineticWarfareControl.TrainingCompletion> _pending = new();
    private Coroutine _processCo;

    private static Dictionary<string, MilitiaUnit> _unitById;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
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

    private string GetTrainingReservationOwnerId(KineticWarfareControl source, string fallbackOwnerId = null)
    {
        if (source != null)
        {
            Saveable saveable = source.GetComponent<Saveable>();
            if (saveable == null)
                saveable = source.GetComponentInParent<Saveable>();

            if (saveable != null && !string.IsNullOrWhiteSpace(saveable.uniqueID))
                return saveable.uniqueID;

            return source.gameObject.GetInstanceID().ToString();
        }

        return fallbackOwnerId;
    }

    private void TagTrainingReservation(
        string reservationId,
        KineticWarfareControl source,
        string fallbackOwnerId = null)
    {
        if (string.IsNullOrWhiteSpace(reservationId))
            return;

        PlayersPopulationManager.Instance?.UpdateReservationMetadata(
            reservationId,
            PopulationReservationKind.Training,
            GetTrainingReservationOwnerId(source, fallbackOwnerId),
            nameof(KineticWarfareControl));
    }

    public void RetagAllPendingTrainingReservations()
    {
        if (_pending.Count == 0)
            return;

        var snapshot = _pending.ToArray();
        for (int i = 0; i < snapshot.Length; i++)
        {
            var tc = snapshot[i];
            TagTrainingReservation(tc.populationReservationId, tc.source);
        }
    }

    private void OnEndTurn()
    {
        EnqueueCompletionsFromAllBuildings();

        if (_processCo == null && _pending.Count > 0)
            _processCo = StartCoroutine(ProcessCompletions());
    }

    private void EnqueueCompletionsFromAllBuildings()
    {
        var pbm = PlayerBuildingManager.Instance;
        if (pbm == null) return;

        var all = pbm.GetAll();
        if (all == null || all.Count == 0) return;

        for (int i = 0; i < all.Count; i++)
        {
            var rec = all[i];
            if (rec == null || !rec.instance) continue;

            var kw = rec.instance.GetComponent<KineticWarfareControl>();
            if (kw == null || !kw.isActiveAndEnabled) continue;

            var tmp = ListPool<KineticWarfareControl.TrainingCompletion>.Get();
            try
            {
                int count = kw.AdvanceTurnAndCollectCompletions(tmp);
                for (int k = 0; k < count; k++)
                {
                    var tc = tmp[k];

                    if (!string.IsNullOrWhiteSpace(tc.populationReservationId))
                        TagTrainingReservation(tc.populationReservationId, kw);

                    _pending.Enqueue(tc);
                }
            }
            finally
            {
                ListPool<KineticWarfareControl.TrainingCompletion>.Release(tmp);
            }
        }
    }

    private static void ReleasePopulationReservation(string reservationId)
    {
        if (string.IsNullOrEmpty(reservationId)) return;
        PlayersPopulationManager.Instance?.ReleaseReservation(reservationId);
    }

    private IEnumerator ProcessCompletions()
    {
        while (_pending.Count > 0)
        {
            int toDo = Mathf.Min(completionsPerFrame, _pending.Count);

            for (int i = 0; i < toDo; i++)
            {
                var tc = _pending.Dequeue();

                if (!string.IsNullOrWhiteSpace(tc.populationReservationId))
                    TagTrainingReservation(tc.populationReservationId, tc.source);

                bool spawned = false;

                if (tc.tileGroupControl != null && tc.unit != null && tc.totalUnits > 0)
                {
                    var group = tc.tileGroupControl.AddGroup(
                        tc.unit,
                        tc.totalUnits,
                        tc.populationReservationId,
                        tc.reservedPopulation,
                        tc.expiryTurn);

                    if (group != null)
                    {
                        ApplyTrainingFatigueToSpawnedGroup(group, tc);
                        tc.tileGroupControl.RefreshMarker(group);
                    }

                    spawned = (group != null);

                    if (spawned)
                        PostUnitTrainingNotification(tc);
                }
                else
                {
                    //Debug.LogWarning("[PlayerTrainingManager] TrainingCompletion has missing data; skipping spawn.");
                }

                if (!spawned && !string.IsNullOrEmpty(tc.populationReservationId))
                {
                    ReleasePopulationReservation(tc.populationReservationId);
                }

                if (tc.source != null)
                    tc.source.OnOrderFinalizedExternally(tc.orderId);
            }

            yield return null;
        }

        _processCo = null;
    }

    public PlayerTrainingSaveData SaveState()
    {
        PlayerTrainingSaveData data = new PlayerTrainingSaveData();

        KineticWarfareControl[] buildings = FindObjectsOfType<KineticWarfareControl>(true);
        for (int i = 0; i < buildings.Length; i++)
        {
            KineticWarfareControl kw = buildings[i];
            if (kw == null)
                continue;

            Saveable saveable = kw.GetComponent<Saveable>();
            if (saveable == null)
                saveable = kw.GetComponentInParent<Saveable>();

            if (saveable == null || string.IsNullOrWhiteSpace(saveable.uniqueID))
                continue;

            List<ActiveTrainingOrderSaveData> orders = kw.CaptureActiveOrders(saveable.uniqueID);
            if (orders != null && orders.Count > 0)
                data.activeOrders.AddRange(orders);
        }

        KineticWarfareControl.TrainingCompletion[] queued = _pending.ToArray();
        for (int i = 0; i < queued.Length; i++)
        {
            var tc = queued[i];

            Saveable saveable = tc.source != null ? tc.source.GetComponent<Saveable>() : null;
            if (saveable == null && tc.source != null)
                saveable = tc.source.GetComponentInParent<Saveable>();

            data.pendingCompletions.Add(new PendingTrainingCompletionSaveData
            {
                sourceBuildingSaveableID = saveable != null ? saveable.uniqueID : null,
                orderId = tc.orderId,
                unitID = tc.unit != null ? tc.unit.unitID : null,
                totalUnits = tc.totalUnits,
                populationReservationId = tc.populationReservationId,
                reservedPopulation = tc.reservedPopulation,
                expiryTurn = tc.expiryTurn,

                startingHealthFraction = tc.startingHealthFraction,
                fatigueBonusPower = tc.fatigueBonusPower,
                fatigueBonusDefense = tc.fatigueBonusDefense,
                fatigueBonusAgility = tc.fatigueBonusAgility,
                fatigueBonusAccuracy = tc.fatigueBonusAccuracy,
                fatigueBonusRange = tc.fatigueBonusRange,
                fatigueBonusStealth = tc.fatigueBonusStealth,
                fatigueBonusMovementSpeed = tc.fatigueBonusMovementSpeed
            });
        }

        return data;
    }

    public void LoadState(PlayerTrainingSaveData data)
    {
        if (_processCo != null)
        {
            StopCoroutine(_processCo);
            _processCo = null;
        }

        _pending.Clear();

        KineticWarfareControl[] buildings = FindObjectsOfType<KineticWarfareControl>(true);
        Dictionary<string, KineticWarfareControl> bySaveableId = new Dictionary<string, KineticWarfareControl>(StringComparer.Ordinal);

        for (int i = 0; i < buildings.Length; i++)
        {
            KineticWarfareControl kw = buildings[i];
            if (kw == null)
                continue;

            kw.ClearTrainingOrdersForLoad();

            Saveable saveable = kw.GetComponent<Saveable>();
            if (saveable == null)
                saveable = kw.GetComponentInParent<Saveable>();

            if (saveable != null && !string.IsNullOrWhiteSpace(saveable.uniqueID) && !bySaveableId.ContainsKey(saveable.uniqueID))
                bySaveableId.Add(saveable.uniqueID, kw);
        }

        if (data != null && data.activeOrders != null)
        {
            for (int i = 0; i < data.activeOrders.Count; i++)
            {
                ActiveTrainingOrderSaveData saved = data.activeOrders[i];
                if (saved == null || string.IsNullOrWhiteSpace(saved.buildingSaveableID))
                    continue;

                if (!bySaveableId.TryGetValue(saved.buildingSaveableID, out KineticWarfareControl kw) || kw == null)
                {
                    //Debug.LogWarning($"[Training] Could not resolve building '{saved.buildingSaveableID}' for active training order '{saved.orderID}'.");
                    continue;
                }

                kw.AddLoadedTrainingOrder(saved, ResolveUnitByID);
            }
        }

        if (data != null && data.pendingCompletions != null)
        {
            for (int i = 0; i < data.pendingCompletions.Count; i++)
            {
                PendingTrainingCompletionSaveData saved = data.pendingCompletions[i];
                if (saved == null || string.IsNullOrWhiteSpace(saved.unitID))
                    continue;

                KineticWarfareControl source = null;
                if (!string.IsNullOrWhiteSpace(saved.sourceBuildingSaveableID))
                    bySaveableId.TryGetValue(saved.sourceBuildingSaveableID, out source);

                TileUnitGroupControl tileGroupControl = source != null
                    ? source.GetComponentInParent<TileUnitGroupControl>()
                    : null;

                MilitiaUnit unit = ResolveUnitByID(saved.unitID);
                if (unit == null)
                {
                    //Debug.LogWarning($"[Training] Could not resolve unit '{saved.unitID}' for pending completion '{saved.orderId}'.");
                    continue;
                }

                var completion = new KineticWarfareControl.TrainingCompletion
                {
                    source = source,
                    orderId = saved.orderId,
                    unit = unit,
                    totalUnits = Mathf.Max(0, saved.totalUnits),
                    tileGroupControl = tileGroupControl,
                    populationReservationId = saved.populationReservationId,
                    reservedPopulation = Mathf.Max(0, saved.reservedPopulation),
                    expiryTurn = saved.expiryTurn,

                    startingHealthFraction = saved.startingHealthFraction > 0f ? saved.startingHealthFraction : 1f,
                    fatigueBonusPower = saved.fatigueBonusPower,
                    fatigueBonusDefense = saved.fatigueBonusDefense,
                    fatigueBonusAgility = saved.fatigueBonusAgility,
                    fatigueBonusAccuracy = saved.fatigueBonusAccuracy,
                    fatigueBonusRange = saved.fatigueBonusRange,
                    fatigueBonusStealth = saved.fatigueBonusStealth,
                    fatigueBonusMovementSpeed = saved.fatigueBonusMovementSpeed,
                };

                if (!string.IsNullOrWhiteSpace(completion.populationReservationId))
                    TagTrainingReservation(
                        completion.populationReservationId,
                        source,
                        saved.sourceBuildingSaveableID);

                _pending.Enqueue(completion);
            }
        }

        if (_processCo == null && _pending.Count > 0)
            _processCo = StartCoroutine(ProcessCompletions());
    }

    private static MilitiaUnit ResolveUnitByID(string unitID)
    {
        if (string.IsNullOrWhiteSpace(unitID))
            return null;

        if (_unitById == null)
        {
            _unitById = new Dictionary<string, MilitiaUnit>(StringComparer.Ordinal);
            MilitiaUnit[] units = Resources.LoadAll<MilitiaUnit>(string.Empty);

            for (int i = 0; i < units.Length; i++)
            {
                MilitiaUnit unit = units[i];
                if (unit == null || string.IsNullOrWhiteSpace(unit.unitID))
                    continue;

                string id = unit.unitID.Trim();
                if (!_unitById.ContainsKey(id))
                    _unitById.Add(id, unit);
            }
        }

        _unitById.TryGetValue(unitID.Trim(), out MilitiaUnit result);
        return result;
    }

    private static void PostUnitTrainingNotification(KineticWarfareControl.TrainingCompletion tc)
    {
        if (NotificationManager.Instance == null) return;
        string unitName = !string.IsNullOrWhiteSpace(tc.unit?.unitName) ? tc.unit.unitName : "Unit";
        Vector3 pos = tc.tileGroupControl != null ? tc.tileGroupControl.transform.position : default;
        string title, message;
        if (NotificationMessageCrafterManager.Instance != null)
            (title, message) = NotificationMessageCrafterManager.Instance.CraftUnitTrainingCompleted(unitName, tc.totalUnits);
        else
            (title, message) = ("Training Complete", $"{tc.totalUnits} {unitName}(s) are ready for deployment.");
        NotificationManager.Instance.AddNotification(NotificationType.UnitTrainingCompleted, title, message, pos);
    }

    private void ApplyTrainingFatigueToSpawnedGroup(
    TileUnitGroupData group,
    KineticWarfareControl.TrainingCompletion tc)
    {
        if (group == null)
            return;

        group.bonusPower += tc.fatigueBonusPower;
        group.bonusDefense += tc.fatigueBonusDefense;
        group.bonusAgility += tc.fatigueBonusAgility;
        group.bonusAccuracy += tc.fatigueBonusAccuracy;
        group.bonusRange += tc.fatigueBonusRange;
        group.bonusStealth += tc.fatigueBonusStealth;
        group.bonusMovementSpeed += tc.fatigueBonusMovementSpeed;

        float startHealth01 = Mathf.Clamp01(tc.startingHealthFraction);
        if (startHealth01 <= 0f)
            startHealth01 = 1f;

        group.currentHealth = Mathf.Clamp(
            Mathf.RoundToInt(group.maxHealth * startHealth01),
            1,
            Mathf.Max(1, group.maxHealth));
    }
}
