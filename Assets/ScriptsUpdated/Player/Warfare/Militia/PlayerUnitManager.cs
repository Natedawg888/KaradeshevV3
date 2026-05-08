using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerUnitManager : MonoBehaviour
{
    public static PlayerUnitManager Instance { get; private set; }

    [Header("Upkeep")]
    [Tooltip("How often upkeep is charged for units, in turns (e.g. 4 = every 4 turns).")]
    public int upkeepIntervalTurns = 4;

    [Header("Debug (read-only)")]
    [SerializeField] private List<TileUnitGroupData> debugGroups = new();

    private class TrackedGroup
    {
        public TileUnitGroupData data;
        public TileUnitGroupControl owner;
    }

    private readonly List<TrackedGroup> _trackedGroups = new();
    private readonly Dictionary<ResourceDefinition, int> _upkeepBuffer = new();
    private readonly List<TrackedGroup> _expiredBuffer = new();   // age expiry
    private readonly List<TrackedGroup> _upkeepDeathBuffer = new();   // missed-upkeep expiry
    private readonly List<TrackedGroup> _dueGroupsBuffer = new();   // upkeep due this turn
    private readonly HashSet<string> _playerGroupIds = new();

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

    private string GetReservationOwnerId(TileUnitGroupControl owner, TileUnitGroupData data)
    {
        if (owner != null)
        {
            Saveable saveable = owner.GetComponent<Saveable>();
            if (saveable == null)
                saveable = owner.GetComponentInParent<Saveable>();

            if (saveable != null && !string.IsNullOrWhiteSpace(saveable.uniqueID))
                return saveable.uniqueID;

            return owner.gameObject.GetInstanceID().ToString();
        }

        return data != null ? data.groupId : null;
    }

    private void TagGroupReservation(TileUnitGroupData data, TileUnitGroupControl owner)
    {
        if (data == null || string.IsNullOrWhiteSpace(data.populationReservationId))
            return;

        var popMgr = PlayersPopulationManager.Instance;
        if (popMgr == null)
            return;

        popMgr.UpdateReservationMetadata(
            data.populationReservationId,
            PopulationReservationKind.UnitGroup,
            GetReservationOwnerId(owner, data),
            nameof(TileUnitGroupControl));
    }

    public void RetagAllTrackedGroupReservations()
    {
        for (int i = 0; i < _trackedGroups.Count; i++)
        {
            var t = _trackedGroups[i];
            if (t == null || t.data == null)
                continue;

            TagGroupReservation(t.data, t.owner);
        }
    }

    public void RegisterGroup(TileUnitGroupData data, TileUnitGroupControl owner)
    {
        if (data == null || owner == null) return;

        // If already tracked, just update the owner (group has moved).
        for (int i = 0; i < _trackedGroups.Count; i++)
        {
            var t = _trackedGroups[i];
            if (t != null && t.data != null && t.data.groupId == data.groupId)
            {
                t.owner = owner;

                // keep the fast lookup in sync
                _playerGroupIds.Add(data.groupId);

                TagGroupReservation(data, owner);
                RefreshDebugList();
                return;
            }
        }

        // Otherwise, add a new tracked entry
        _trackedGroups.Add(new TrackedGroup
        {
            data = data,
            owner = owner
        });

        // fast lookup
        _playerGroupIds.Add(data.groupId);

        TagGroupReservation(data, owner);
        RefreshDebugList();
    }

    public void UnregisterGroup(TileUnitGroupData data)
    {
        if (data == null) return;

        // fast lookup
        _playerGroupIds.Remove(data.groupId);

        for (int i = _trackedGroups.Count - 1; i >= 0; i--)
        {
            var t = _trackedGroups[i];
            if (t == null || t.data == null)
            {
                _trackedGroups.RemoveAt(i);
                continue;
            }

            if (t.data.groupId == data.groupId)
                _trackedGroups.RemoveAt(i);
        }

        RefreshDebugList();
    }

    private void RefreshDebugList()
    {
        debugGroups.Clear();
        for (int i = 0; i < _trackedGroups.Count; i++)
        {
            var t = _trackedGroups[i];
            if (t != null && t.data != null)
                debugGroups.Add(t.data);
        }
    }

    // ---------------- Turn system ----------------

    private void OnEndTurn()
    {
        // Make sure any live group reservations are tagged as UnitGroup
        // before upkeep / expiry / action processing.
        RetagAllTrackedGroupReservations();

        // 1) Upkeep & markers
        OnEndOfTurnUpkeep();               // pay upkeep every N turns per group
        RefreshMarkersForTrackedGroups();  // tick all UI (health, expiry, upkeep timer)

        // 2) Age-based expiry (may remove some groups entirely)
        HandleExpiry();

        // 3) Tick multi-turn movement for all groups that have a planned route.
        var moveMgr = UnitGroupMovementManager.Instance;
        if (moveMgr != null)
        {
            moveMgr.ProcessMovementForAllGroupsBatched();
        }
        else
        {
            //Debug.LogWarning("[PlayerUnitManager] No UnitGroupMovementManager in scene; cannot tick group movement.");
        }

        var actionMgr = UnitGroupActionManager.Instance;
        if (actionMgr != null)
        {
            actionMgr.ProcessActionsForAllGroupsBatched();
        }
        else
        {
            //Debug.LogWarning("[PlayerUnitManager] No UnitGroupActionManager in scene; cannot tick group actions.");
        }
    }

    private void RefreshMarkersForTrackedGroups()
    {
        for (int i = 0; i < _trackedGroups.Count; i++)
        {
            var t = _trackedGroups[i];
            if (t == null || t.data == null || t.owner == null) continue;

            t.owner.RefreshMarker(t.data);
        }
    }

    // Helper: is this group due for an upkeep charge this turn?
    private bool IsUpkeepDue(TileUnitGroupData data, int currentTurn, int interval)
    {
        if (data == null) return false;
        if (interval <= 0) return true; // fallback to "every turn"
        if (data.upkeepStartTurn < 0) return false;
        if (currentTurn < data.upkeepStartTurn) return false;

        return ((currentTurn - data.upkeepStartTurn) % interval) == 0;
    }

    private void OnEndOfTurnUpkeep()
    {
        if (_trackedGroups.Count == 0) return;
        if (TurnSystem.Instance == null) return;

        var inventory = PlayerInventoryManager.Instance;
        if (inventory == null)
        {
            //Debug.LogWarning("[PlayerUnitManager] No PlayerInventoryManager found; cannot pay unit upkeep.");
            return;
        }

        int currentTurn = TurnSystem.GetCurrentTurn();
        int interval = Mathf.Max(1, upkeepIntervalTurns);

        _upkeepBuffer.Clear();
        _dueGroupsBuffer.Clear();

        // 1) Collect ONLY groups whose upkeep is due this turn.
        for (int i = 0; i < _trackedGroups.Count; i++)
        {
            var t = _trackedGroups[i];
            if (t == null || t.data == null) continue;

            var data = t.data;
            var unit = data.unitType;
            if (unit == null) continue;

            if (!IsUpkeepDue(data, currentTurn, interval))
                continue;

            _dueGroupsBuffer.Add(t);

            var costs = unit.upkeepPerTurn;
            if (costs == null || costs.Count == 0) continue;

            int batchSize = Mathf.Max(1, unit.outputUnits);
            int batches = Mathf.Max(1, Mathf.CeilToInt(data.unitCount / (float)batchSize));

            for (int c = 0; c < costs.Count; c++)
            {
                var cost = costs[c];

                var defField = cost.GetType().GetField("resource")
                              ?? cost.GetType().GetField("definition")
                              ?? cost.GetType().GetField("resourceDef");

                var amountField = cost.GetType().GetField("amount")
                                 ?? cost.GetType().GetField("value")
                                 ?? cost.GetType().GetField("count");

                if (defField == null || amountField == null)
                {
                    //Debug.LogWarning("[PlayerUnitManager] Could not reflect ResourceCost fields; skipping upkeep entry.");
                    continue;
                }

                var defObj = defField.GetValue(cost) as ResourceDefinition;
                if (defObj == null) continue;

                object amtObj = amountField.GetValue(cost);
                int amountPerBatch = (amtObj is int iAmt) ? iAmt :
                                     (amtObj is float fAmt) ? Mathf.RoundToInt(fAmt) : 0;
                if (amountPerBatch <= 0) continue;

                int totalForGroup = amountPerBatch * batches;
                if (totalForGroup <= 0) continue;

                if (_upkeepBuffer.TryGetValue(defObj, out var existing))
                    _upkeepBuffer[defObj] = existing + totalForGroup;
                else
                    _upkeepBuffer.Add(defObj, totalForGroup);
            }
        }

        // No groups due, nothing to do.
        if (_dueGroupsBuffer.Count == 0 || _upkeepBuffer.Count == 0)
            return;

        // 2) Check if we can fully pay all due upkeep this turn.
        bool fullPaymentPossible = true;
        foreach (var kv in _upkeepBuffer)
        {
            var def = kv.Key;
            int want = kv.Value;
            if (def == null || want <= 0) continue;

            int have = inventory.GetAmount(def);
            if (have < want)
            {
                fullPaymentPossible = false;
            }
        }

        // 3) Pay upkeep (even if partial).
        foreach (var kv in _upkeepBuffer)
        {
            var def = kv.Key;
            int want = kv.Value;
            if (def == null || want <= 0) continue;

            int before = inventory.GetAmount(def);
            if (before <= 0)
            {
                //Debug.LogWarning($"[PlayerUnitManager] Upkeep: no '{def.name}' available (needed {want}).");
                continue;
            }

            inventory.TryRemove(def, want);
            int after = inventory.GetAmount(def);
            int paid = Mathf.Max(0, before - after);

            if (paid < want)
            {
                //Debug.LogWarning(
                    //$"[PlayerUnitManager] Partial upkeep payment for '{def.name}': paid {paid}/{want}. " +
                    //"You can add morale/attrition penalties here later.");
            }
        }

        // 4) Update miss counters + auto-disband, but ONLY for groups that were due this turn.
        if (fullPaymentPossible)
        {
            ResetUpkeepMissesForGroups(_dueGroupsBuffer);
        }
        else
        {
            ApplyUpkeepMissesAndAutoDisband(_dueGroupsBuffer);
        }
    }

    private void ResetUpkeepMissesForGroups(List<TrackedGroup> groups)
    {
        if (groups == null) return;

        for (int i = 0; i < groups.Count; i++)
        {
            var t = groups[i];
            if (t == null || t.data == null) continue;

            if (t.data.missedUpkeepTurns != 0)
                t.data.missedUpkeepTurns = 0;
        }
    }

    private void ApplyUpkeepMissesAndAutoDisband(List<TrackedGroup> dueGroups)
    {
        if (dueGroups == null || dueGroups.Count == 0) return;

        _upkeepDeathBuffer.Clear();

        // Increment missed-upkeep counters and collect any that hit the limit.
        for (int i = 0; i < dueGroups.Count; i++)
        {
            var t = dueGroups[i];
            if (t == null || t.data == null) continue;

            var data = t.data;
            var unit = data.unitType;
            if (unit == null) continue;

            int maxMisses = Mathf.Max(0, unit.maxMissedUpkeepTurns);
            if (maxMisses <= 0) continue; // this unit type never auto-disbands from upkeep

            data.missedUpkeepTurns = Mathf.Min(maxMisses, data.missedUpkeepTurns + 1);

            if (data.missedUpkeepTurns >= maxMisses)
                _upkeepDeathBuffer.Add(t);
        }

        if (_upkeepDeathBuffer.Count == 0) return;

        // Disband the ones that exhausted their tolerance.
        for (int i = 0; i < _upkeepDeathBuffer.Count; i++)
        {
            var t = _upkeepDeathBuffer[i];
            if (t == null || t.data == null || t.owner == null) continue;

            //Debug.Log(
                //$"[PlayerUnitManager] Group {t.data.groupId} ({t.data.unitType?.unitName}) " +
                //$"disbanded due to missed upkeep (misses={t.data.missedUpkeepTurns}, max={t.data.unitType?.maxMissedUpkeepTurns}).");

            // This will release population and unregister from PlayerUnitManager.
            t.owner.RemoveGroup(t.data.groupId);
        }

        _upkeepDeathBuffer.Clear();
    }

    private void HandleExpiry()
    {
        if (_trackedGroups.Count == 0) return;
        if (TurnSystem.Instance == null) return;

        int currentTurn = TurnSystem.GetCurrentTurn();
        _expiredBuffer.Clear();

        for (int i = 0; i < _trackedGroups.Count; i++)
        {
            var t = _trackedGroups[i];
            if (t == null || t.data == null) continue;

            var data = t.data;
            var unit = data.unitType;

            if (unit == null) continue;
            if (!unit.isHuman) continue;      // only humans expire by ageing
            if (!data.HasExpiry) continue;    // expiryTurn not set

            if (currentTurn >= data.expiryTurn)
                _expiredBuffer.Add(t);
        }

        if (_expiredBuffer.Count == 0) return;

        for (int i = 0; i < _expiredBuffer.Count; i++)
        {
            var t = _expiredBuffer[i];
            if (t == null || t.data == null || t.owner == null) continue;

            //Debug.Log(
                //$"[PlayerUnitManager] Group {t.data.groupId} ({t.data.unitType?.unitName}) " +
                //$"expired on turn {currentTurn} (expiry={t.data.expiryTurn}).");

            // This will also release population and unregister from PlayerUnitManager.
            t.owner.RemoveGroup(t.data.groupId);
        }

        _expiredBuffer.Clear();
    }

    public struct GroupInfo
    {
        public TileUnitGroupData data;
        public TileUnitGroupControl owner;
    }

    public void GetAllGroups(List<GroupInfo> outList)
    {
        if (outList == null) return;
        outList.Clear();

        for (int i = 0; i < _trackedGroups.Count; i++)
        {
            var t = _trackedGroups[i];
            if (t == null || t.data == null || t.owner == null) continue;

            outList.Add(new GroupInfo
            {
                data = t.data,
                owner = t.owner
            });
        }
    }

    public bool IsPlayerUnitGroupId(string groupId)
    {
        if (string.IsNullOrEmpty(groupId)) return false;
        return _playerGroupIds.Contains(groupId);
    }
}
