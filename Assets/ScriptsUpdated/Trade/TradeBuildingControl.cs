using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BuildingControl))]
public class TradeBuildingControl : MonoBehaviour, IBuildingTypeHandler
{
    [Header("Trade Settings")]
    [SerializeField] private bool enableTrade = true;
    [Tooltip("Roll for a trader once per season change. When false, uses per-def turn-based timing instead.")]
    [SerializeField] private bool traderArrivesOncePerSeason = true;

    [Header("Trader Pool")]
    [SerializeField] private List<TraderDefinitionSO> traderPool = new List<TraderDefinitionSO>();

    [Header("World Canvas Icon")]
    [Tooltip("Root GameObject containing the trader icon. Hidden when no trader is present.")]
    [SerializeField] private GameObject traderIconRoot;
    [Tooltip("Optional. Displays turns remaining while a trader is present.")]
    [SerializeField] private TMP_Text traderTurnsRemainingText;

    [Header("State")]
    [SerializeField] private bool hasActiveTrader;
    [SerializeField] private int traderTurnsRemaining;
    [SerializeField] private TravelingTraderOffer currentTraderOffer;

    private BuildingControl _buildingControl;
    private BuildingStatus _buildingStatus;
    private bool _turnSubscribed;
    private bool _seasonSubscribed;
    private string _lastSeasonID;
    private int _nextVisitTurn;

    public BuildingType HandledType => BuildingType.Trade;

    public event Action<TravelingTraderOffer> OnTraderArrived;
    public event Action<TradeEvaluationResult> OnNegotiationResolved;
    public event Action OnTraderLeft;

    // ──────────────────── Lifecycle ────────────────────

    private void Awake()
    {
        _buildingControl = GetComponent<BuildingControl>();
        _buildingStatus  = GetComponent<BuildingStatus>();
        RefreshWorldIcon();
    }

    private void OnEnable()
    {
        if (!_turnSubscribed)
        {
            TurnSystem.SubscribeToEndOfTurn(HandleEndOfTurn);
            _turnSubscribed = true;
        }

        if (!_seasonSubscribed && SeasonManager.Instance != null)
        {
            SeasonManager.Instance.OnSeasonChanged += HandleSeasonChanged;
            _seasonSubscribed = true;
            _lastSeasonID = SeasonManager.Instance.CurrentSeason?.seasonID;
        }
    }

    private void OnDisable()
    {
        if (_turnSubscribed)
        {
            TurnSystem.UnsubscribeFromEndOfTurn(HandleEndOfTurn);
            _turnSubscribed = false;
        }

        if (_seasonSubscribed && SeasonManager.Instance != null)
        {
            SeasonManager.Instance.OnSeasonChanged -= HandleSeasonChanged;
            _seasonSubscribed = false;
        }
    }

    // ──────────────────── IBuildingTypeHandler ────────────────────

    public void OnTypeEnabled()  { }
    public void OnTypeDisabled() { ClearTrader(); }

    public void OnBuildingStateChanged(BuildingState state)
    {
        if (state == BuildingState.Destroyed)
            ClearTrader();
    }

    // ──────────────────── Public API ────────────────────

    public bool HasActiveTrader()                        => hasActiveTrader && currentTraderOffer != null;
    public TravelingTraderOffer GetCurrentTraderOffer()  => currentTraderOffer;

    public TradeEvaluationResult SubmitPlayerOffer(TradeOffer playerOffer)
    {
        if (!HasActiveTrader())
            return MakeResult(TradeResultType.Declined, "No Active Trader", "There is no trader present.");
        if (playerOffer == null)
            return MakeResult(TradeResultType.Declined, "Invalid Offer", "No offer was provided.");

        if (!ValidatePlayerCanAffordOffer(playerOffer, out string affordReason))
            return MakeResult(TradeResultType.Declined, "Cannot Afford", affordReason);

        float traderValue = ComputeTraderOfferValue(currentTraderOffer);
        float playerValue = ComputePlayerOfferValue(playerOffer);
        float required    = traderValue * currentTraderOffer.greedMultiplier;

        var result = new TradeEvaluationResult
        {
            playerOfferValue = playerValue,
            traderOfferValue = traderValue,
            requiredValue    = required,
        };

        if (playerValue >= required)
        {
            ApplyTrade(playerOffer);
            result.resultType = TradeResultType.Accepted;
            result.title      = "Trade Accepted";
            result.message    = $"{currentTraderOffer.traderName} accepts your offer.";
            NotificationManager.Instance?.AddNotification(NotificationType.TradeAccepted, result.title, result.message);
            ClearTrader();
        }
        else if (playerValue >= traderValue * currentTraderOffer.counterOfferTolerance)
        {
            result.resultType = TradeResultType.CounterOffer;
            result.title      = "Counter Offer";
            result.message    = $"{currentTraderOffer.traderName} is interested but wants more.";
            result.preferredResourceHints = BuildPreferenceHints();
            NotificationManager.Instance?.AddNotification(NotificationType.TradeCounterOffered, result.title, result.message);
        }
        else
        {
            result.resultType = TradeResultType.Declined;
            result.title      = "Trade Declined";
            result.message    = $"{currentTraderOffer.traderName} shakes their head. Your offer isn't enough.";
            NotificationManager.Instance?.AddNotification(NotificationType.TradeDeclined, result.title, result.message);
            ClearTrader();
        }

        OnNegotiationResolved?.Invoke(result);
        MarkDirty();
        return result;
    }

    public void AcceptTrade(TradeOffer finalOffer)
    {
        if (!HasActiveTrader())
        {
            Debug.LogWarning("[TradeBuildingControl] AcceptTrade called but no trader is present.");
            return;
        }

        if (!ValidatePlayerCanAffordOffer(finalOffer, out string reason))
        {
            Debug.LogWarning($"[TradeBuildingControl] AcceptTrade blocked: {reason}");
            return;
        }

        ApplyTrade(finalOffer);
        NotificationManager.Instance?.AddNotification(NotificationType.TradeAccepted, "Trade Accepted", "Goods exchanged successfully.");
        ClearTrader();
        MarkDirty();
    }

    public void DeclineTrade()
    {
        if (!HasActiveTrader()) return;
        NotificationManager.Instance?.AddNotification(NotificationType.TradeDeclined, "Trade Declined", "You declined the trader's offer.");
        ClearTrader();
        MarkDirty();
    }

    public void GenerateTrader()
    {
        TraderDefinitionSO def = PickTraderDefinition();
        if (def == null)
        {
            Debug.LogWarning("[TradeBuildingControl] GenerateTrader: no valid trader definition available.");
            return;
        }
        GenerateTraderFromDef(def);
    }

    public void ClearTrader()
    {
        if (!hasActiveTrader) return;
        hasActiveTrader      = false;
        traderTurnsRemaining = 0;
        currentTraderOffer   = null;
        ScheduleNextVisit();
        RefreshWorldIcon();
        OnTraderLeft?.Invoke();
        MarkDirty();
    }

    // ──────────────────── Turn / Season Handling ────────────────────

    private void HandleEndOfTurn()
    {
        if (!enableTrade) return;
        if (_buildingControl != null && _buildingControl.ActiveType != BuildingType.Trade) return;
        if (_buildingStatus  != null && _buildingStatus.CurrentState == BuildingState.Destroyed) return;

        if (hasActiveTrader)
        {
            traderTurnsRemaining--;
            if (traderTurnsRemaining <= 0)
            {
                NotificationManager.Instance?.AddNotification(
                    NotificationType.TraderLeft, "Trader Departed",
                    $"{currentTraderOffer?.traderName ?? "The trader"} has moved on.");
                ClearTrader();
            }
            else
            {
                RefreshWorldIcon();
            }
            return;
        }

        // Fallback season detection when SeasonManager event not subscribed
        if (!_seasonSubscribed && SeasonManager.Instance != null)
        {
            string currentID = SeasonManager.Instance.CurrentSeason?.seasonID;
            if (currentID != _lastSeasonID)
            {
                _lastSeasonID = currentID;
                if (traderArrivesOncePerSeason) { RollForTraderArrival(); return; }
            }
        }

        if (!traderArrivesOncePerSeason && TurnSystem.GetCurrentTurn() >= _nextVisitTurn)
            RollForTraderArrival();
    }

    private void HandleSeasonChanged(SeasonDefinition season)
    {
        if (!enableTrade || !traderArrivesOncePerSeason) return;
        if (hasActiveTrader) return;
        if (_buildingStatus != null && _buildingStatus.CurrentState == BuildingState.Destroyed) return;

        _lastSeasonID = season?.seasonID;
        RollForTraderArrival();
    }

    private void RollForTraderArrival()
    {
        TraderDefinitionSO def = PickTraderDefinition();
        if (def == null) return;

        if (UnityEngine.Random.value <= def.arrivalChance)
            GenerateTraderFromDef(def);
        else
            ScheduleNextVisit(def);
    }

    private void ScheduleNextVisit(TraderDefinitionSO hint = null)
    {
        TraderDefinitionSO def = hint ?? PickTraderDefinition();
        if (def == null || def.maxTurnsBetweenVisits <= 0) return;
        int span = Mathf.Max(1, def.maxTurnsBetweenVisits - def.minTurnsBetweenVisits);
        _nextVisitTurn = TurnSystem.GetCurrentTurn() + def.minTurnsBetweenVisits + UnityEngine.Random.Range(0, span + 1);
    }

    // ──────────────────── Offer Generation ────────────────────

    private void GenerateTraderFromDef(TraderDefinitionSO def)
    {
        if (hasActiveTrader) return;

        currentTraderOffer   = BuildOfferFromDefinition(def);
        hasActiveTrader      = true;
        traderTurnsRemaining = def.turnsAvailable;

        string buildingName = _buildingControl != null ? _buildingControl.buildingName : "your building";
        NotificationManager.Instance?.AddNotification(
            NotificationType.TraderArrived, "Trader Arrived",
            $"{currentTraderOffer.traderName} has arrived at {buildingName}.");

        RefreshWorldIcon();
        OnTraderArrived?.Invoke(currentTraderOffer);
        MarkDirty();
    }

    private TraderDefinitionSO PickTraderDefinition()
    {
        if (traderPool == null || traderPool.Count == 0) return null;
        SeasonDefinition currentSeason = SeasonManager.Instance?.CurrentSeason;
        var valid = new List<TraderDefinitionSO>();
        for (int i = 0; i < traderPool.Count; i++)
        {
            TraderDefinitionSO def = traderPool[i];
            if (def != null && def.IsAvailableInSeason(currentSeason))
                valid.Add(def);
        }
        return valid.Count > 0 ? valid[UnityEngine.Random.Range(0, valid.Count)] : null;
    }

    private TravelingTraderOffer BuildOfferFromDefinition(TraderDefinitionSO def)
    {
        var offer = new TravelingTraderOffer
        {
            traderName                  = def.traderName,
            greedMultiplier             = def.greedMultiplier,
            counterOfferTolerance       = def.counterOfferTolerance,
            turnsRemaining              = def.turnsAvailable,
            flavorDescription           = def.flavorDescription,
            preferences                 = new List<TradeResourcePreference>(def.resourcePreferences),
            acceptsPopulationFromPlayer = def.acceptsPopulationFromPlayer,
            childValue                  = def.childValue,
            teenValue                   = def.teenValue,
            adultValue                  = def.adultValue,
            elderValue                  = def.elderValue,
        };

        FillResources(offer, def.possibleResources, def.resourceAmountRange, def.minResourceTypes, def.maxResourceTypes);

        if (def.canOfferPopulation && def.maxPopulationOffered > 0 && def.offerablePopulation?.Count > 0)
        {
            int total = UnityEngine.Random.Range(def.minPopulationOffered, def.maxPopulationOffered + 1);
            if (total > 0)
                offer.offeredPopulation = BuildRandomPopulation(total, def.offerablePopulation);
        }

        return offer;
    }

    private void FillResources(TravelingTraderOffer offer, List<ResourceAmount> pool, Vector2Int amountRange, int minTypes, int maxTypes)
    {
        if (pool == null || pool.Count == 0) return;
        int typeCount = Mathf.Clamp(UnityEngine.Random.Range(minTypes, maxTypes + 1), 1, pool.Count);
        var shuffled = new List<ResourceAmount>(pool);
        ShuffleList(shuffled);
        for (int i = 0; i < typeCount; i++)
        {
            var entry = shuffled[i];
            if (entry?.resource == null) continue;
            offer.offeredResources.Add(new ResourceAmount
            {
                resource = entry.resource,
                amount   = UnityEngine.Random.Range(Mathf.Max(1, amountRange.x), Mathf.Max(1, amountRange.y) + 1)
            });
        }
    }

    private TradePopulationAmount BuildRandomPopulation(int total, List<TradePopulationSlot> slots)
    {
        var result = new TradePopulationAmount();
        if (slots == null || slots.Count == 0) return result;
        for (int i = 0; i < total; i++)
        {
            var slot = slots[UnityEngine.Random.Range(0, slots.Count)];
            if (slot != null)
                result.Add(slot.ageGroup, slot.gender, 1);
        }
        return result;
    }

    private static void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            T tmp = list[i]; list[i] = list[j]; list[j] = tmp;
        }
    }

    // ──────────────────── Value Computation ────────────────────

    private float ComputeTraderOfferValue(TravelingTraderOffer offer)
    {
        float total = 0f;
        if (offer.offeredResources != null)
            foreach (var r in offer.offeredResources)
                if (r?.resource != null) total += r.amount * GetBaseValue(r.resource);
        total += PopValue(offer.offeredPopulation);
        return total;
    }

    private float ComputePlayerOfferValue(TradeOffer offer)
    {
        float total = 0f;
        if (offer.playerGivesResources != null)
            foreach (var r in offer.playerGivesResources)
                if (r?.resource != null) total += r.amount * GetBaseValue(r.resource) * GetPreferenceMult(r.resource);
        if (currentTraderOffer != null && currentTraderOffer.acceptsPopulationFromPlayer)
            total += PopValue(offer.playerGivesPopulation);
        return total;
    }

    private float PopValue(TradePopulationAmount pop)
    {
        if (pop == null || currentTraderOffer == null) return 0f;
        float total = 0f;
        for (int i = 0; i < pop.entries.Count; i++)
        {
            var e = pop.entries[i];
            if (e == null || e.count <= 0) continue;
            total += e.count * GetAgeValue(e.ageGroup);
        }
        return total;
    }

    private float GetAgeValue(AgeGroup age)
    {
        if (currentTraderOffer == null) return 1f;
        switch (age)
        {
            case AgeGroup.Child: return currentTraderOffer.childValue;
            case AgeGroup.Teen:  return currentTraderOffer.teenValue;
            case AgeGroup.Adult: return currentTraderOffer.adultValue;
            case AgeGroup.Elder: return currentTraderOffer.elderValue;
            default: return 1f;
        }
    }

    // Extension point: read ResourceDefinition.tradeValue here when the field is added.
    private float GetBaseValue(ResourceDefinition def) => 1f;

    private float GetPreferenceMult(ResourceDefinition def)
    {
        if (currentTraderOffer?.preferences == null) return 1f;
        foreach (var p in currentTraderOffer.preferences)
            if (p?.resource == def) return p.valueMultiplier;
        return 1f;
    }

    // ──────────────────── Validation ────────────────────

    private bool ValidatePlayerCanAffordOffer(TradeOffer offer, out string reason)
    {
        reason = null;
        var inv = PlayerInventoryManager.Instance;
        if (inv == null) { reason = "Inventory manager unavailable."; return false; }

        if (offer.playerGivesResources != null)
            foreach (var r in offer.playerGivesResources)
            {
                if (r?.resource == null) continue;
                if (inv.GetAmount(r.resource) < r.amount)
                    { reason = $"Not enough {r.resource.resourceName} (need {r.amount})."; return false; }
            }

        if (!CanPlayerAffordPopulation(offer.playerGivesPopulation, out reason))
            return false;

        return true;
    }

    public bool CanPlayerAffordPopulation(TradePopulationAmount amount, out string reason)
    {
        reason = null;
        if (amount == null || amount.IsEmpty) return true;

        var pop = PlayersPopulationManager.Instance;
        if (pop == null)
        {
            Debug.LogWarning("[TradeBuildingControl] PlayersPopulationManager not found.");
            reason = "Population manager unavailable.";
            return false;
        }

        // TODO: Replace with pop.GetAvailableByAgeAndGender(AgeGroup, Gender) when that API is added.
        for (int i = 0; i < amount.entries.Count; i++)
        {
            var e = amount.entries[i];
            if (e == null || e.count <= 0) continue;
            int available = GetAvailableByAgeAndGender(pop, e.ageGroup, e.gender);
            if (e.count > available)
            {
                reason = $"Not enough {e.gender} {e.ageGroup} available (need {e.count}, have {available}).";
                return false;
            }
        }

        return true;
    }

    private int GetAvailableByAgeAndGender(PlayersPopulationManager pop, AgeGroup age, Gender gender)
    {
        int total = 0;
        var groups = pop.AllPopulations;
        if (groups == null) return 0;
        for (int i = 0; i < groups.Count; i++)
        {
            var g = groups[i];
            if (g != null && g.ageGroup == age && g.gender == gender)
                total += Mathf.Max(0, g.count - g.reservedCount);
        }
        return total;
    }

    // ──────────────────── Trade Execution ────────────────────

    private void ApplyTrade(TradeOffer offer)
    {
        var inv = PlayerInventoryManager.Instance;

        if (inv != null && offer.playerGivesResources != null)
        {
            foreach (var r in offer.playerGivesResources)
            {
                if (r?.resource == null || r.amount <= 0) continue;
                if (!inv.TryRemove(r.resource, r.amount))
                    Debug.LogWarning($"[TradeBuildingControl] Failed to remove {r.amount}x {r.resource.resourceName}.");
            }
            inv.inventoryPanel?.Refresh();
            SaveSystem.MarkSectionDirty(SaveSectionKeys.Knowledge);
        }

        RemovePopulationFromPlayer(offer.playerGivesPopulation);

        if (inv != null && offer.traderGivesResources != null)
        {
            foreach (var r in offer.traderGivesResources)
            {
                if (r?.resource == null || r.amount <= 0) continue;
                if (!inv.TryAdd(r.resource, r.amount))
                    Debug.LogWarning($"[TradeBuildingControl] Failed to add {r.amount}x {r.resource.resourceName}.");
            }
            inv.inventoryPanel?.Refresh();
            SaveSystem.MarkSectionDirty(SaveSectionKeys.Knowledge);
        }

        AddPopulationToPlayer(offer.traderGivesPopulation);
    }

    public void RemovePopulationFromPlayer(TradePopulationAmount amount)
    {
        if (amount == null || amount.IsEmpty) return;
        var pop = PlayersPopulationManager.Instance;
        if (pop == null) { Debug.LogWarning("[TradeBuildingControl] RemovePopulationFromPlayer: PlayersPopulationManager missing."); return; }
        // TODO: Use pop.RemoveByAgeAndGender(AgeGroup, Gender, count) when that API is added.
        for (int i = 0; i < amount.entries.Count; i++)
        {
            var e = amount.entries[i];
            if (e == null || e.count <= 0) continue;
            RemovePopEntry(pop, e.ageGroup, e.gender, e.count);
        }
        pop.MarkUIDirty();
        SaveSystem.MarkSectionDirty(SaveSectionKeys.Population);
    }

    public void AddPopulationToPlayer(TradePopulationAmount amount)
    {
        if (amount == null || amount.IsEmpty) return;
        var pop = PlayersPopulationManager.Instance;
        if (pop == null) { Debug.LogWarning("[TradeBuildingControl] AddPopulationToPlayer: PlayersPopulationManager missing."); return; }
        // TODO: Use pop.AddByAgeAndGender(AgeGroup, Gender, count) when that API is added.
        for (int i = 0; i < amount.entries.Count; i++)
        {
            var e = amount.entries[i];
            if (e == null || e.count <= 0) continue;
            AddPopEntry(pop, e.ageGroup, e.gender, e.count);
        }
        pop.MarkUIDirty();
        SaveSystem.MarkSectionDirty(SaveSectionKeys.Population);
    }

    private void RemovePopEntry(PlayersPopulationManager pop, AgeGroup age, Gender gender, int count)
    {
        if (count <= 0) return;
        int remaining = count;
        var groups = pop.AllPopulations;
        if (groups == null) return;
        for (int i = 0; i < groups.Count && remaining > 0; i++)
        {
            var g = groups[i];
            if (g == null || g.ageGroup != age || g.gender != gender) continue;
            int take = Mathf.Min(remaining, Mathf.Max(0, g.count - g.reservedCount));
            g.count -= take; remaining -= take;
        }
        if (remaining > 0)
            Debug.LogWarning($"[TradeBuildingControl] Could not remove all {gender} {age} population. {remaining} unresolved.");
    }

    private void AddPopEntry(PlayersPopulationManager pop, AgeGroup age, Gender gender, int count)
    {
        if (count <= 0) return;
        var groups = pop.AllPopulations;
        if (groups == null) return;
        for (int i = 0; i < groups.Count; i++)
        {
            var g = groups[i];
            if (g == null || g.ageGroup != age || g.gender != gender) continue;
            g.count += count;
            return;
        }
        Debug.LogWarning($"[TradeBuildingControl] No {gender} {age} population group found to receive {count} people.");
    }

    // ──────────────────── Preference Hints ────────────────────

    private List<string> BuildPreferenceHints()
    {
        var hints = new List<string>();
        if (currentTraderOffer?.preferences != null)
            foreach (var p in currentTraderOffer.preferences)
                if (p?.resource != null && p.valueMultiplier > 1f)
                    hints.Add($"They want more {p.resource.resourceName}.");

        if (currentTraderOffer != null
            && currentTraderOffer.acceptsPopulationFromPlayer
            && currentTraderOffer.adultValue >= currentTraderOffer.teenValue
            && currentTraderOffer.adultValue >= currentTraderOffer.childValue)
            hints.Add("They are looking for adults who can work.");

        if (hints.Count == 0)
            hints.Add("They want more in return.");
        return hints;
    }

    // ──────────────────── Helpers ────────────────────

    private TradeEvaluationResult MakeResult(TradeResultType type, string title, string msg)
        => new TradeEvaluationResult { resultType = type, title = title, message = msg };

    private void MarkDirty()
        => SaveSystem.MarkSectionDirty(SaveSectionKeys.WorldObjects);

    private void RefreshWorldIcon()
    {
        if (traderIconRoot != null)
            traderIconRoot.SetActive(hasActiveTrader);

        if (traderTurnsRemainingText != null)
            traderTurnsRemainingText.text = hasActiveTrader ? traderTurnsRemaining.ToString() : string.Empty;
    }

    private TraderDefinitionSO FindDefByName(string traderName)
    {
        if (traderPool == null || string.IsNullOrEmpty(traderName)) return null;
        for (int i = 0; i < traderPool.Count; i++)
            if (traderPool[i] != null && traderPool[i].traderName == traderName)
                return traderPool[i];
        return null;
    }

    // ──────────────────── Save / Load ────────────────────

    public TradeBuildingSaveData CaptureRuntimeSaveData(string saveableID)
    {
        var data = new TradeBuildingSaveData
        {
            buildingInstanceId   = saveableID,
            hasActiveTrader      = hasActiveTrader,
            traderTurnsRemaining = traderTurnsRemaining,
            nextVisitTurn        = _nextVisitTurn,
        };

        if (hasActiveTrader && currentTraderOffer != null)
        {
            data.traderName              = currentTraderOffer.traderName;
            data.traderGreedMultiplier   = currentTraderOffer.greedMultiplier;
            data.traderFlavorDescription = currentTraderOffer.flavorDescription;

            if (currentTraderOffer.offeredResources != null)
                foreach (var r in currentTraderOffer.offeredResources)
                    if (r?.resource != null)
                        data.traderOfferedResources.Add(new TradeItemSaveEntry
                            { resourceID = r.resource.resourceID, amount = r.amount });

            var pop = currentTraderOffer.offeredPopulation;
            if (pop?.entries != null)
                for (int i = 0; i < pop.entries.Count; i++)
                {
                    var e = pop.entries[i];
                    if (e == null || e.count <= 0) continue;
                    data.traderOfferedPopulation.Add(new TradePopulationSaveEntry
                        { ageGroup = e.ageGroup.ToString(), gender = e.gender.ToString(), count = e.count });
                }
        }

        return data;
    }

    public void ApplyRuntimeSaveData(TradeBuildingSaveData data, Func<string, ResourceDefinition> resolveResource)
    {
        if (data == null) return;

        _nextVisitTurn = data.nextVisitTurn;

        if (!data.hasActiveTrader)
        {
            hasActiveTrader      = false;
            traderTurnsRemaining = 0;
            currentTraderOffer   = null;
            return;
        }

        // Try to restore negotiation values from the original SO definition by name.
        TraderDefinitionSO sourceDef = FindDefByName(data.traderName);

        var offer = new TravelingTraderOffer
        {
            traderName                  = data.traderName ?? "Trader",
            greedMultiplier             = data.traderGreedMultiplier > 0 ? data.traderGreedMultiplier : 1.15f,
            counterOfferTolerance       = sourceDef?.counterOfferTolerance ?? 0.75f,
            turnsRemaining              = data.traderTurnsRemaining,
            flavorDescription           = data.traderFlavorDescription,
            preferences                 = sourceDef != null
                                          ? new List<TradeResourcePreference>(sourceDef.resourcePreferences)
                                          : new List<TradeResourcePreference>(),
            acceptsPopulationFromPlayer = sourceDef?.acceptsPopulationFromPlayer ?? true,
            childValue                  = sourceDef?.childValue ?? 1f,
            teenValue                   = sourceDef?.teenValue  ?? 2f,
            adultValue                  = sourceDef?.adultValue ?? 4f,
            elderValue                  = sourceDef?.elderValue ?? 2f,
        };

        if (data.traderOfferedResources != null && resolveResource != null)
            foreach (var entry in data.traderOfferedResources)
            {
                if (string.IsNullOrWhiteSpace(entry.resourceID)) continue;
                var def = resolveResource(entry.resourceID);
                if (def == null) continue;
                offer.offeredResources.Add(new ResourceAmount { resource = def, amount = entry.amount });
            }

        offer.offeredPopulation = new TradePopulationAmount();
        if (data.traderOfferedPopulation != null)
            for (int i = 0; i < data.traderOfferedPopulation.Count; i++)
            {
                var e = data.traderOfferedPopulation[i];
                if (e == null || e.count <= 0) continue;
                if (System.Enum.TryParse(e.ageGroup, out AgeGroup age) && System.Enum.TryParse(e.gender, out Gender gender))
                    offer.offeredPopulation.Add(age, gender, e.count);
            }

        currentTraderOffer   = offer;
        hasActiveTrader      = true;
        traderTurnsRemaining = data.traderTurnsRemaining;
    }
}
