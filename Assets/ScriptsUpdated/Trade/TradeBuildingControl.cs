using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BuildingControl))]
public class TradeBuildingControl : MonoBehaviour, IBuildingTypeHandler
{
    private static readonly string[] DefaultTraderNames =
    {
        "River Walker", "Hill Clan Trader", "Forest Forager",
        "Wandering Kin Group", "Stone Carrier", "Hide Trader"
    };

    [Header("Trade Timing")]
    [SerializeField] private bool enableTrade = true;
    [SerializeField] private bool traderArrivesOncePerSeason = true;
    [SerializeField] private int minTurnsBetweenTraders = 0;
    [SerializeField] private int maxTurnsBetweenTraders = 0;
    [SerializeField, Range(0f, 1f)] private float traderArrivalChancePerSeason = 1f;
    [SerializeField] private int traderAvailableTurns = 3;

    [Header("Trader Offer Generation")]
    [SerializeField] private List<ResourceAmount> possibleTraderResources = new List<ResourceAmount>();
    [SerializeField] private Vector2Int traderResourceAmountRange = new Vector2Int(1, 5);
    [SerializeField] private int minResourceTypesOffered = 1;
    [SerializeField] private int maxResourceTypesOffered = 3;

    [Header("Population Offered By Trader")]
    [SerializeField] private bool traderCanOfferPopulation = false;
    [SerializeField] private int minPopulationOffered = 0;
    [SerializeField] private int maxPopulationOffered = 2;
    [SerializeField] private bool canOfferChildren = false;
    [SerializeField] private bool canOfferTeens = true;
    [SerializeField] private bool canOfferAdults = true;
    [SerializeField] private bool canOfferElders = false;

    [Header("Trader Preferences")]
    [SerializeField] private List<TradeResourcePreference> resourcePreferences = new List<TradeResourcePreference>();
    [SerializeField] private bool acceptsPopulationFromPlayer = true;
    [SerializeField] private float childValue = 1f;
    [SerializeField] private float teenValue = 2f;
    [SerializeField] private float adultValue = 4f;
    [SerializeField] private float elderValue = 2f;
    [SerializeField] private float baseGreedMultiplier = 1.15f;
    [SerializeField] private float counterOfferTolerance = 0.75f;

    [Header("Trader Pool")]
    [Tooltip("Optional. When populated, a random definition is picked each visit instead of the inline settings above.")]
    [SerializeField] private List<TraderDefinitionSO> traderPool = new List<TraderDefinitionSO>();

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

    public bool HasActiveTrader()               => hasActiveTrader && currentTraderOffer != null;
    public TravelingTraderOffer GetCurrentTraderOffer() => currentTraderOffer;

    public TradeEvaluationResult SubmitPlayerOffer(TradeOffer playerOffer)
    {
        if (!HasActiveTrader())
            return MakeResult(TradeResultType.Declined, "No Active Trader", "There is no trader present.");
        if (playerOffer == null)
            return MakeResult(TradeResultType.Declined, "Invalid Offer", "No offer was provided.");

        if (!ValidatePlayerCanAffordOffer(playerOffer, out string affordReason))
            return MakeResult(TradeResultType.Declined, "Cannot Afford", affordReason);

        float traderValue  = ComputeTraderOfferValue(currentTraderOffer);
        float playerValue  = ComputePlayerOfferValue(playerOffer);
        float required     = traderValue * currentTraderOffer.greedMultiplier;

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

            NotificationManager.Instance?.AddNotification(
                NotificationType.TradeAccepted, result.title, result.message);

            ClearTrader();
        }
        else if (playerValue >= traderValue * currentTraderOffer.counterOfferTolerance)
        {
            result.resultType = TradeResultType.CounterOffer;
            result.title      = "Counter Offer";
            result.message    = $"{currentTraderOffer.traderName} is interested but wants more.";
            result.preferredResourceHints = BuildPreferenceHints();

            NotificationManager.Instance?.AddNotification(
                NotificationType.TradeCounterOffered, result.title, result.message);
        }
        else
        {
            result.resultType = TradeResultType.Declined;
            result.title      = "Trade Declined";
            result.message    = $"{currentTraderOffer.traderName} shakes their head. Your offer isn't enough.";

            NotificationManager.Instance?.AddNotification(
                NotificationType.TradeDeclined, result.title, result.message);

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
        NotificationManager.Instance?.AddNotification(
            NotificationType.TradeAccepted, "Trade Accepted", "Goods exchanged successfully.");

        ClearTrader();
        MarkDirty();
    }

    public void DeclineTrade()
    {
        if (!HasActiveTrader()) return;

        NotificationManager.Instance?.AddNotification(
            NotificationType.TradeDeclined, "Trade Declined", "You declined the trader's offer.");

        ClearTrader();
        MarkDirty();
    }

    public void GenerateTrader()
    {
        if (hasActiveTrader) return;

        currentTraderOffer   = BuildTraderOffer();
        hasActiveTrader      = true;
        traderTurnsRemaining = traderAvailableTurns;

        string buildingName = _buildingControl != null ? _buildingControl.buildingName : "your building";
        NotificationManager.Instance?.AddNotification(
            NotificationType.TraderArrived,
            "Trader Arrived",
            $"{currentTraderOffer.traderName} has arrived at {buildingName}.");

        OnTraderArrived?.Invoke(currentTraderOffer);
        MarkDirty();
    }

    public void ClearTrader()
    {
        if (!hasActiveTrader) return;
        hasActiveTrader      = false;
        traderTurnsRemaining = 0;
        currentTraderOffer   = null;
        ScheduleNextVisit();
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
                string name = currentTraderOffer?.traderName ?? "The trader";
                NotificationManager.Instance?.AddNotification(
                    NotificationType.TraderLeft, "Trader Departed",
                    $"{name} has moved on.");
                ClearTrader();
            }
            return;
        }

        // Fallback season detection via turn system (when SeasonManager event not subscribed)
        if (!_seasonSubscribed && SeasonManager.Instance != null)
        {
            string currentID = SeasonManager.Instance.CurrentSeason?.seasonID;
            if (currentID != _lastSeasonID)
            {
                _lastSeasonID = currentID;
                if (traderArrivesOncePerSeason) { RollForTraderArrival(); return; }
            }
        }

        // Pure turn-based arrival (no SeasonManager or traderArrivesOncePerSeason = false)
        if (!traderArrivesOncePerSeason && maxTurnsBetweenTraders > 0)
        {
            if (TurnSystem.GetCurrentTurn() >= _nextVisitTurn)
                RollForTraderArrival();
        }
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
        if (UnityEngine.Random.value <= traderArrivalChancePerSeason)
            GenerateTrader();
        else
            ScheduleNextVisit();
    }

    private void ScheduleNextVisit()
    {
        if (maxTurnsBetweenTraders <= 0) return;
        int span = Mathf.Max(1, maxTurnsBetweenTraders - minTurnsBetweenTraders);
        _nextVisitTurn = TurnSystem.GetCurrentTurn() + minTurnsBetweenTraders + UnityEngine.Random.Range(0, span + 1);
    }

    // ──────────────────── Offer Generation ────────────────────

    private TravelingTraderOffer BuildTraderOffer()
    {
        TraderDefinitionSO def = PickTraderDefinition();
        return def != null ? BuildOfferFromDefinition(def) : BuildOfferFromInlineSettings();
    }

    private TraderDefinitionSO PickTraderDefinition()
    {
        if (traderPool == null || traderPool.Count == 0) return null;
        var valid = new List<TraderDefinitionSO>();
        for (int i = 0; i < traderPool.Count; i++)
            if (traderPool[i] != null) valid.Add(traderPool[i]);
        return valid.Count > 0 ? valid[UnityEngine.Random.Range(0, valid.Count)] : null;
    }

    private TravelingTraderOffer BuildOfferFromDefinition(TraderDefinitionSO def)
    {
        var offer = new TravelingTraderOffer
        {
            traderName                = string.IsNullOrWhiteSpace(def.traderName)
                                        ? DefaultTraderNames[UnityEngine.Random.Range(0, DefaultTraderNames.Length)]
                                        : def.traderName,
            greedMultiplier           = def.greedMultiplier,
            counterOfferTolerance     = def.counterOfferTolerance,
            turnsRemaining            = traderAvailableTurns,
            flavorDescription         = def.flavorDescription,
            preferences               = new List<TradeResourcePreference>(def.resourcePreferences),
            acceptsPopulationFromPlayer = def.acceptsPopulationFromPlayer,
            childValue                = def.childValue,
            teenValue                 = def.teenValue,
            adultValue                = def.adultValue,
            elderValue                = def.elderValue,
        };

        FillResources(offer, def.possibleResources, def.resourceAmountRange, def.minResourceTypes, def.maxResourceTypes);

        if (def.canOfferPopulation && def.maxPopulationOffered > 0)
        {
            int total = UnityEngine.Random.Range(def.minPopulationOffered, def.maxPopulationOffered + 1);
            if (total > 0)
                offer.offeredPopulation = BuildRandomPopulation(
                    total, def.canOfferChildren, def.canOfferTeens, def.canOfferAdults, def.canOfferElders);
        }

        return offer;
    }

    private TravelingTraderOffer BuildOfferFromInlineSettings()
    {
        var offer = new TravelingTraderOffer
        {
            traderName                = DefaultTraderNames[UnityEngine.Random.Range(0, DefaultTraderNames.Length)],
            greedMultiplier           = baseGreedMultiplier,
            counterOfferTolerance     = counterOfferTolerance,
            turnsRemaining            = traderAvailableTurns,
            flavorDescription         = "A wandering trader arrives bearing goods.",
            preferences               = new List<TradeResourcePreference>(resourcePreferences),
            acceptsPopulationFromPlayer = acceptsPopulationFromPlayer,
            childValue                = childValue,
            teenValue                 = teenValue,
            adultValue                = adultValue,
            elderValue                = elderValue,
        };

        FillResources(offer, possibleTraderResources, traderResourceAmountRange, minResourceTypesOffered, maxResourceTypesOffered);

        if (traderCanOfferPopulation && maxPopulationOffered > 0)
        {
            int total = UnityEngine.Random.Range(minPopulationOffered, maxPopulationOffered + 1);
            if (total > 0)
                offer.offeredPopulation = BuildRandomPopulation(
                    total, canOfferChildren, canOfferTeens, canOfferAdults, canOfferElders);
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

    private TradePopulationAmount BuildRandomPopulation(int total, bool children, bool teens, bool adults, bool elders)
    {
        var pop   = new TradePopulationAmount();
        var slots = new List<int>();
        if (children) slots.Add(0);
        if (teens)    slots.Add(1);
        if (adults)   slots.Add(2);
        if (elders)   slots.Add(3);

        if (slots.Count == 0) return pop;

        for (int i = 0; i < total; i++)
        {
            int slot = slots[UnityEngine.Random.Range(0, slots.Count)];
            if      (slot == 0) pop.children++;
            else if (slot == 1) pop.teens++;
            else if (slot == 2) pop.adults++;
            else                pop.elders++;
        }
        return pop;
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
        return pop.children * currentTraderOffer.childValue
             + pop.teens    * currentTraderOffer.teenValue
             + pop.adults   * currentTraderOffer.adultValue
             + pop.elders   * currentTraderOffer.elderValue;
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
        {
            foreach (var r in offer.playerGivesResources)
            {
                if (r?.resource == null) continue;
                if (inv.GetAmount(r.resource) < r.amount)
                {
                    reason = $"Not enough {r.resource.resourceName} (need {r.amount}).";
                    return false;
                }
            }
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

        // TODO: Replace with pop.GetAvailableByAgeGroup(AgeGroup) when that API is added.
        if (amount.children > GetAvailableByAge(pop, AgeGroup.Child))
            { reason = $"Not enough children available."; return false; }
        if (amount.teens > GetAvailableByAge(pop, AgeGroup.Teen))
            { reason = $"Not enough teens available."; return false; }
        if (amount.adults > GetAvailableByAge(pop, AgeGroup.Adult))
            { reason = $"Not enough adults available."; return false; }
        if (amount.elders > GetAvailableByAge(pop, AgeGroup.Elder))
            { reason = $"Not enough elders available."; return false; }

        return true;
    }

    private int GetAvailableByAge(PlayersPopulationManager pop, AgeGroup age)
    {
        int total = 0;
        var groups = pop.AllPopulations;
        if (groups == null) return 0;
        for (int i = 0; i < groups.Count; i++)
        {
            var g = groups[i];
            if (g != null && g.ageGroup == age)
                total += Mathf.Max(0, g.count - g.reservedCount);
        }
        return total;
    }

    // ──────────────────── Trade Execution ────────────────────

    private void ApplyTrade(TradeOffer offer)
    {
        var inv = PlayerInventoryManager.Instance;

        // Remove player resources first (validate already passed)
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

        // Add trader resources
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
        if (pop == null)
        {
            Debug.LogWarning("[TradeBuildingControl] RemovePopulationFromPlayer: PlayersPopulationManager missing.");
            return;
        }
        // TODO: Use pop.RemoveFromAgeGroup(AgeGroup, count) when that API is added.
        RemoveFromAge(pop, AgeGroup.Child,  amount.children);
        RemoveFromAge(pop, AgeGroup.Teen,   amount.teens);
        RemoveFromAge(pop, AgeGroup.Adult,  amount.adults);
        RemoveFromAge(pop, AgeGroup.Elder,  amount.elders);
        pop.MarkUIDirty();
        SaveSystem.MarkSectionDirty(SaveSectionKeys.Population);
    }

    public void AddPopulationToPlayer(TradePopulationAmount amount)
    {
        if (amount == null || amount.IsEmpty) return;
        var pop = PlayersPopulationManager.Instance;
        if (pop == null)
        {
            Debug.LogWarning("[TradeBuildingControl] AddPopulationToPlayer: PlayersPopulationManager missing.");
            return;
        }
        // TODO: Use pop.AddToAgeGroup(AgeGroup, count) when that API is added.
        AddToAge(pop, AgeGroup.Child,  amount.children);
        AddToAge(pop, AgeGroup.Teen,   amount.teens);
        AddToAge(pop, AgeGroup.Adult,  amount.adults);
        AddToAge(pop, AgeGroup.Elder,  amount.elders);
        pop.MarkUIDirty();
        SaveSystem.MarkSectionDirty(SaveSectionKeys.Population);
    }

    private void RemoveFromAge(PlayersPopulationManager pop, AgeGroup age, int count)
    {
        if (count <= 0) return;
        int remaining = count;
        var groups = pop.AllPopulations;
        if (groups == null) return;
        for (int i = 0; i < groups.Count && remaining > 0; i++)
        {
            var g = groups[i];
            if (g == null || g.ageGroup != age) continue;
            int take = Mathf.Min(remaining, Mathf.Max(0, g.count - g.reservedCount));
            g.count   -= take;
            remaining -= take;
        }
        if (remaining > 0)
            Debug.LogWarning($"[TradeBuildingControl] Could not remove all {age} population. {remaining} unresolved.");
    }

    private void AddToAge(PlayersPopulationManager pop, AgeGroup age, int count)
    {
        if (count <= 0) return;
        var groups = pop.AllPopulations;
        if (groups == null) return;
        for (int i = 0; i < groups.Count; i++)
        {
            var g = groups[i];
            if (g == null || g.ageGroup != age) continue;
            g.count += count;
            return;
        }
        Debug.LogWarning($"[TradeBuildingControl] No {age} population group found to receive {count} people.");
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
            data.traderName             = currentTraderOffer.traderName;
            data.traderGreedMultiplier  = currentTraderOffer.greedMultiplier;
            data.traderFlavorDescription = currentTraderOffer.flavorDescription;

            if (currentTraderOffer.offeredResources != null)
                foreach (var r in currentTraderOffer.offeredResources)
                    if (r?.resource != null)
                        data.traderOfferedResources.Add(new TradeItemSaveEntry
                            { resourceID = r.resource.resourceID, amount = r.amount });

            var pop = currentTraderOffer.offeredPopulation;
            if (pop != null)
            {
                data.traderOfferedChildren = pop.children;
                data.traderOfferedTeens    = pop.teens;
                data.traderOfferedAdults   = pop.adults;
                data.traderOfferedElders   = pop.elders;
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

        var offer = new TravelingTraderOffer
        {
            traderName                = data.traderName ?? "Trader",
            greedMultiplier           = data.traderGreedMultiplier > 0 ? data.traderGreedMultiplier : baseGreedMultiplier,
            counterOfferTolerance     = counterOfferTolerance,
            turnsRemaining            = data.traderTurnsRemaining,
            flavorDescription         = data.traderFlavorDescription,
            preferences               = new List<TradeResourcePreference>(resourcePreferences),
            acceptsPopulationFromPlayer = acceptsPopulationFromPlayer,
            childValue                = childValue,
            teenValue                 = teenValue,
            adultValue                = adultValue,
            elderValue                = elderValue,
        };

        if (data.traderOfferedResources != null && resolveResource != null)
            foreach (var entry in data.traderOfferedResources)
            {
                if (string.IsNullOrWhiteSpace(entry.resourceID)) continue;
                var def = resolveResource(entry.resourceID);
                if (def == null) continue;
                offer.offeredResources.Add(new ResourceAmount { resource = def, amount = entry.amount });
            }

        offer.offeredPopulation = new TradePopulationAmount
        {
            children = data.traderOfferedChildren,
            teens    = data.traderOfferedTeens,
            adults   = data.traderOfferedAdults,
            elders   = data.traderOfferedElders
        };

        currentTraderOffer   = offer;
        hasActiveTrader      = true;
        traderTurnsRemaining = data.traderTurnsRemaining;
    }
}
