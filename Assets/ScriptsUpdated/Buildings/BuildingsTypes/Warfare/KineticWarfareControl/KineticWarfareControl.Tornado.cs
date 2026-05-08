using System;
using System.Collections.Generic;
using UnityEngine;

public partial class KineticWarfareControl
{
    private enum TrainingPauseReason
    {
        None,
        TornadoImpact,
        FireImpact
    }

    [Header("Tornado Training Impact")]
    public bool tornadoCanPauseTraining = true;

    [Range(0f, 1f)] public float tornadoTeenTraineeDeathChance = 0.08f;
    [Range(0f, 1f)] public float tornadoAdultTraineeDeathChance = 0.06f;
    [Range(0f, 1f)] public float tornadoElderTraineeDeathChance = 0.12f;

    [Tooltip("Extra multiplier applied to tornado trainee death chance.")]
    [Min(0f)] public float tornadoTrainingDeathChanceMultiplier = 1f;

    [Header("Fire Training Impact")]
    public bool fireCanPauseTraining = true;

    [Range(0f, 1f)] public float fireTeenTraineeDeathChance = 0.05f;
    [Range(0f, 1f)] public float fireAdultTraineeDeathChance = 0.03f;
    [Range(0f, 1f)] public float fireElderTraineeDeathChance = 0.07f;

    [Tooltip("Extra multiplier applied to fire trainee death chance.")]
    [Min(0f)] public float fireTrainingDeathChanceMultiplier = 1f;

    [Header("Post-Training Fatigue")]
    public bool trainingAppliesPostTrainingFatigue = true;

    [Range(0f, 1f)] public float trainedGroupStartingHealthFraction = 0.70f;

    [Range(0f, 1f)] public float trainingPowerDebuffFraction = 0.15f;
    [Range(0f, 1f)] public float trainingDefenseDebuffFraction = 0.12f;
    [Range(0f, 1f)] public float trainingAgilityDebuffFraction = 0.15f;
    [Range(0f, 1f)] public float trainingAccuracyDebuffFraction = 0.10f;
    [Range(0f, 1f)] public float trainingRangeDebuffFraction = 0.08f;
    [Range(0f, 1f)] public float trainingStealthDebuffFraction = 0.10f;
    [Range(0f, 1f)] public float trainingMovementDebuffFraction = 0.12f;

    private TrainingPauseReason _trainingPauseReason = TrainingPauseReason.None;
    private readonly HashSet<int> _activeTornadoSourceIds = new();
    private readonly HashSet<int> _activeFireSourceIds = new();

    public bool IsPausedForTornadoImpact => _trainingPauseReason == TrainingPauseReason.TornadoImpact;
    public bool IsPausedForFireImpact => _trainingPauseReason == TrainingPauseReason.FireImpact;

    public struct TornadoTrainingImpact
    {
        public bool paused;
        public int traineesRolled;
        public int traineesKilled;
        public int cancelledOrdersOnResume;
    }

    private float GetTornadoTraineeDeathChance(AgeGroup ageGroup)
    {
        return ageGroup switch
        {
            AgeGroup.Teen => tornadoTeenTraineeDeathChance,
            AgeGroup.Adult => tornadoAdultTraineeDeathChance,
            AgeGroup.Elder => tornadoElderTraineeDeathChance,
            _ => 0f
        };
    }

    public struct FireTrainingImpact
    {
        public bool paused;
        public int traineesRolled;
        public int traineesKilled;
        public int cancelledOrdersOnResume;
    }

    private float GetFireTraineeDeathChance(AgeGroup ageGroup)
    {
        return ageGroup switch
        {
            AgeGroup.Teen => fireTeenTraineeDeathChance,
            AgeGroup.Adult => fireAdultTraineeDeathChance,
            AgeGroup.Elder => fireElderTraineeDeathChance,
            _ => 0f
        };
    }

    private Individual FindTrainingIndividualById(string individualId)
    {
        if (string.IsNullOrWhiteSpace(individualId))
            return null;

        var familySim = PlayerFamilySimulationManager.Instance;
        if (familySim == null)
            return null;

        var people = familySim.GetIndividuals();
        if (people == null)
            return null;

        for (int i = 0; i < people.Count; i++)
        {
            var person = people[i];
            if (person != null && person.Id == individualId)
                return person;
        }

        return null;
    }

    private void RefreshTrainingPopulationUI()
    {
        PlayersPopulationManager.Instance?.ForceSyncUI();
    }

    private void UnbusyReservationButKeepTrainingOrder(string reservationId)
    {
        if (string.IsNullOrWhiteSpace(reservationId))
            return;

        var familySim = PlayerFamilySimulationManager.Instance;
        if (familySim == null)
            return;

        familySim.UnbusyReservationOnly(reservationId);
        TagTrainingReservation(reservationId);
    }

    private void RebusyTrainingReservation(string reservationId)
    {
        if (string.IsNullOrWhiteSpace(reservationId))
            return;

        var familySim = PlayerFamilySimulationManager.Instance;
        if (familySim == null)
            return;

        familySim.RebusyReservation(reservationId);
        TagTrainingReservation(reservationId);
    }

    private int GetRequiredPopulationForOrder(TrainingOrder order)
    {
        if (order == null)
            return 0;

        return Mathf.Max(
            Mathf.Max(0, order.reservedPopulation),
            Mathf.Max(0, order.PopulationRequired));
    }

    private void RefreshOrderExpiryFromReservation(TrainingOrder order)
    {
        if (order == null || order.unit == null || !order.unit.isHuman)
            return;

        if (string.IsNullOrWhiteSpace(order.populationReservationId))
        {
            order.expiryTurn = -1;
            return;
        }

        PlayersPopulationManager.Instance?.TryComputeAndStoreReservationExpiryTurn(
            order.populationReservationId,
            out order.expiryTurn);
    }

    private bool IsOrderReservationValid(TrainingOrder order)
    {
        if (order == null)
            return false;

        int required = GetRequiredPopulationForOrder(order);
        if (required <= 0)
            return true;

        if (string.IsNullOrWhiteSpace(order.populationReservationId))
            return false;

        var familySim = PlayerFamilySimulationManager.Instance;
        if (familySim == null)
            return false;

        return familySim.IsProductionReservationStillValid(order.populationReservationId, required);
    }

    private bool TryBackfillInvalidReservedWorkers(string reservationId)
    {
        if (string.IsNullOrWhiteSpace(reservationId))
            return false;

        var pop = PlayersPopulationManager.Instance;
        var familySim = PlayerFamilySimulationManager.Instance;

        if (pop == null || familySim == null)
            return false;

        if (!pop.TryGetReservedIndividualIds(reservationId, out var reservedIds) ||
            reservedIds == null ||
            reservedIds.Count == 0)
        {
            return false;
        }

        bool allReplaced = true;
        var snapshot = new List<string>(reservedIds);
        var people = familySim.GetIndividuals();

        for (int i = 0; i < snapshot.Count; i++)
        {
            string id = snapshot[i];
            Individual person = null;

            for (int j = 0; j < people.Count; j++)
            {
                var candidate = people[j];
                if (candidate != null && candidate.Id == id)
                {
                    person = candidate;
                    break;
                }
            }

            bool shouldReplace =
                person == null ||
                !person.IsAlive ||
                (person.AggregatedAgeGroup != AgeGroup.Teen &&
                 person.AggregatedAgeGroup != AgeGroup.Adult);

            if (!shouldReplace)
                continue;

            if (person == null || !person.IsAlive)
            {
                allReplaced = false;
                continue;
            }

            bool replaced;
            if (!pop.TryDetachIndividualFromExistingReservations(person.Id, out replaced) || !replaced)
                allReplaced = false;
        }

        return allReplaced;
    }

    private bool TryEnsureOrderReservation(TrainingOrder order)
    {
        if (order == null)
            return false;

        int required = GetRequiredPopulationForOrder(order);
        if (required <= 0)
            return true;

        var pop = PlayersPopulationManager.Instance;
        var familySim = PlayerFamilySimulationManager.Instance;

        if (pop == null || familySim == null)
            return false;

        // Already valid -> just rebusy it.
        if (IsOrderReservationValid(order))
        {
            RebusyTrainingReservation(order.populationReservationId);
            RefreshOrderExpiryFromReservation(order);
            return true;
        }

        // If there is no reservation at all, try to make a fresh one.
        if (string.IsNullOrWhiteSpace(order.populationReservationId))
        {
            if (!pop.TryReservePopulation(
                    required,
                    PopulationReservationKind.Training,
                    GetTrainingReservationOwnerId(),
                    nameof(KineticWarfareControl),
                    out string newReservationId))
            {
                return false;
            }

            order.populationReservationId = newReservationId;
            order.reservedPopulation = required;

            TagTrainingReservation(order.populationReservationId);
            RebusyTrainingReservation(order.populationReservationId);
            RefreshOrderExpiryFromReservation(order);
            return true;
        }

        bool replacedInvalid = TryBackfillInvalidReservedWorkers(order.populationReservationId);
        bool toppedUp = pop.TryTopUpReservationToRequiredCount(order.populationReservationId, required);

        if ((replacedInvalid || toppedUp) && IsOrderReservationValid(order))
        {
            order.reservedPopulation = required;
            TagTrainingReservation(order.populationReservationId);
            RebusyTrainingReservation(order.populationReservationId);
            RefreshOrderExpiryFromReservation(order);
            return true;
        }

        return false;
    }

    private bool CancelTrainingOrderForTornado(TrainingOrder order, string reason = null)
    {
        if (order == null)
            return false;

        ReleasePopulationForOrder(order);
        RemoveWidget(order.orderID);

        bool removed = activeOrders.Remove(order);

        if (removed)
        {
            if (!string.IsNullOrWhiteSpace(reason))
                //Debug.Log(
                    //$"[KineticWarfare] Cancelled training order {order.orderID} " +
                    //$"({(order.unit != null ? order.unit.unitName : "Unknown Unit")}): {reason}");

            PostTrainingWeatherFailureNotification(order, reason);
        }

        return removed;
    }

    private void PostTrainingWeatherFailureNotification(TrainingOrder order, string reason)
    {
        if (NotificationManager.Instance == null) return;
        string unitName = order.unit != null && !string.IsNullOrWhiteSpace(order.unit.unitName)
            ? order.unit.unitName : "Unit";
        int count = order.TotalUnits;
        string cause = !string.IsNullOrWhiteSpace(reason) && reason.Contains("fire") ? "fire" : "tornado";
        string title, message;
        if (NotificationMessageCrafterManager.Instance != null)
            (title, message) = NotificationMessageCrafterManager.Instance.CraftUnitTrainingFailedWeather(unitName, count, cause);
        else
            (title, message) = ("Training Disrupted", $"{count} {unitName}(s) lost their training due to {cause}.");
        NotificationManager.Instance.AddNotification(NotificationType.UnitTrainingFailedWeather, title, message, transform.position);
    }

    private void PauseTrainingForTornado()
    {
        _trainingPauseReason = TrainingPauseReason.TornadoImpact;

        for (int i = 0; i < activeOrders.Count; i++)
        {
            var order = activeOrders[i];
            if (order == null || string.IsNullOrWhiteSpace(order.populationReservationId))
                continue;

            UnbusyReservationButKeepTrainingOrder(order.populationReservationId);
        }

        RefreshTrainingPopulationUI();
    }

    public TornadoTrainingImpact RegisterTornadoImpact(
        int tornadoSourceId,
        float externalChanceMultiplier = 1f,
        bool debugLogging = false)
    {
        TornadoTrainingImpact result = default;

        if (!tornadoCanPauseTraining || activeOrders == null || activeOrders.Count == 0)
            return result;

        // Only process once per unique tornado currently overlapping this training building.
        bool firstContactFromThisTornado = _activeTornadoSourceIds.Add(tornadoSourceId);
        if (!firstContactFromThisTornado)
            return result;

        PauseTrainingForTornado();
        result.paused = true;

        var pop = PlayersPopulationManager.Instance;
        var familySim = PlayerFamilySimulationManager.Instance;

        if (pop == null || familySim == null)
            return result;

        HashSet<string> killIds = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < activeOrders.Count; i++)
        {
            var order = activeOrders[i];
            if (order == null)
                continue;

            if (string.IsNullOrWhiteSpace(order.populationReservationId))
                continue;

            if (!pop.TryGetReservedIndividualIds(order.populationReservationId, out var reservedIds) ||
                reservedIds == null)
            {
                continue;
            }

            for (int j = 0; j < reservedIds.Count; j++)
            {
                string individualId = reservedIds[j];
                Individual person = FindTrainingIndividualById(individualId);

                if (person == null || !person.IsAlive)
                    continue;

                result.traineesRolled++;

                float chance = GetTornadoTraineeDeathChance(person.AggregatedAgeGroup);
                if (chance <= 0f)
                    continue;

                chance *= tornadoTrainingDeathChanceMultiplier;
                chance *= Mathf.Max(0f, externalChanceMultiplier);
                chance = Mathf.Clamp01(chance);

                if (UnityEngine.Random.value <= chance)
                    killIds.Add(person.Id);
            }
        }

        if (killIds.Count > 0)
            familySim.TryKillIndividualsById(killIds, out result.traineesKilled);

        RefreshTrainingPopulationUI();

        if (debugLogging)
        {
            //Debug.Log(
                //$"[KineticWarfare] Tornado impacted training on '{name}' | " +
                //$"Paused={result.paused} | " +
                //$"TraineesRolled={result.traineesRolled} | " +
                //$"TraineesKilled={result.traineesKilled} | " +
                //$"Orders={activeOrders.Count}"
            //);
        }

        return result;
    }

    public bool NotifyTornadoCleared(int tornadoSourceId, bool debugLogging = false)
    {
        if (!_activeTornadoSourceIds.Remove(tornadoSourceId))
            return false;

        // Another tornado is still overlapping this building.
        if (_activeTornadoSourceIds.Count > 0)
            return false;

        // Fire still active on this building, so do not resume yet.
        if (_activeFireSourceIds.Count > 0)
            return false;

        if (_trainingPauseReason != TrainingPauseReason.TornadoImpact)
            return false;

        _trainingPauseReason = TrainingPauseReason.None;

        int cancelled = 0;

        for (int i = activeOrders.Count - 1; i >= 0; i--)
        {
            var order = activeOrders[i];
            if (order == null)
            {
                activeOrders.RemoveAt(i);
                continue;
            }

            if (!TryEnsureOrderReservation(order))
            {
                if (CancelTrainingOrderForTornado(order, "could not restore trainees after tornado"))
                    cancelled++;
            }
        }

        RefreshTrainingPopulationUI();

        if (cancelled > 0)
            OnTrainingQueueChanged?.Invoke();

        if (debugLogging)
        {
            //Debug.Log(
                //$"[KineticWarfare] Tornado cleared for '{name}' | " +
                //$"CancelledOrdersOnResume={cancelled} | " +
                //$"RemainingOrders={activeOrders.Count}"
            //);
        }

        return true;
    }

    private void PauseTrainingForFire()
    {
        _trainingPauseReason = TrainingPauseReason.FireImpact;

        for (int i = 0; i < activeOrders.Count; i++)
        {
            var order = activeOrders[i];
            if (order == null || string.IsNullOrWhiteSpace(order.populationReservationId))
                continue;

            UnbusyReservationButKeepTrainingOrder(order.populationReservationId);
        }

        RefreshTrainingPopulationUI();
    }

    public FireTrainingImpact RegisterFireImpact(
    int fireSourceId,
    float externalChanceMultiplier = 1f,
    bool debugLogging = false)
    {
        FireTrainingImpact result = default;

        if (!fireCanPauseTraining || activeOrders == null || activeOrders.Count == 0)
            return result;

        // Only process once per unique fire currently affecting this training building.
        bool firstContactFromThisFire = _activeFireSourceIds.Add(fireSourceId);
        if (!firstContactFromThisFire)
            return result;

        PauseTrainingForFire();
        result.paused = true;

        var pop = PlayersPopulationManager.Instance;
        var familySim = PlayerFamilySimulationManager.Instance;

        if (pop == null || familySim == null)
            return result;

        HashSet<string> killIds = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < activeOrders.Count; i++)
        {
            var order = activeOrders[i];
            if (order == null)
                continue;

            if (string.IsNullOrWhiteSpace(order.populationReservationId))
                continue;

            if (!pop.TryGetReservedIndividualIds(order.populationReservationId, out var reservedIds) ||
                reservedIds == null)
            {
                continue;
            }

            for (int j = 0; j < reservedIds.Count; j++)
            {
                string individualId = reservedIds[j];
                Individual person = FindTrainingIndividualById(individualId);

                if (person == null || !person.IsAlive)
                    continue;

                result.traineesRolled++;

                float chance = GetFireTraineeDeathChance(person.AggregatedAgeGroup);
                if (chance <= 0f)
                    continue;

                chance *= fireTrainingDeathChanceMultiplier;
                chance *= Mathf.Max(0f, externalChanceMultiplier);
                chance = Mathf.Clamp01(chance);

                if (UnityEngine.Random.value <= chance)
                    killIds.Add(person.Id);
            }
        }

        if (killIds.Count > 0)
            familySim.TryKillIndividualsById(killIds, out result.traineesKilled);

        RefreshTrainingPopulationUI();

        if (debugLogging)
        {
            //Debug.Log(
                //$"[KineticWarfare] Fire impacted training on '{name}' | " +
                //$"Paused={result.paused} | " +
                //$"TraineesRolled={result.traineesRolled} | " +
                //$"TraineesKilled={result.traineesKilled} | " +
                //$"Orders={activeOrders.Count}"
            //);
        }

        return result;
    }

    public bool NotifyFireCleared(int fireSourceId, bool debugLogging = false)
    {
        if (!_activeFireSourceIds.Remove(fireSourceId))
            return false;

        // Another fire is still affecting this building.
        if (_activeFireSourceIds.Count > 0)
            return false;

        // Tornado still active on this building, so do not resume yet.
        if (_activeTornadoSourceIds.Count > 0)
            return false;

        if (_trainingPauseReason != TrainingPauseReason.FireImpact)
            return false;

        _trainingPauseReason = TrainingPauseReason.None;

        int cancelled = 0;

        for (int i = activeOrders.Count - 1; i >= 0; i--)
        {
            var order = activeOrders[i];
            if (order == null)
            {
                activeOrders.RemoveAt(i);
                continue;
            }

            if (!TryEnsureOrderReservation(order))
            {
                if (CancelTrainingOrderForTornado(order, "could not restore trainees after fire"))
                    cancelled++;
            }
        }

        RefreshTrainingPopulationUI();

        if (cancelled > 0)
            OnTrainingQueueChanged?.Invoke();

        if (debugLogging)
        {
            //Debug.Log(
                //$"[KineticWarfare] Fire cleared for '{name}' | " +
                //$"CancelledOrdersOnResume={cancelled} | " +
                //$"RemainingOrders={activeOrders.Count}"
            //);
        }

        return true;
    }
}
