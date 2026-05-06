using System;
using System.Collections.Generic;
using UnityEngine;

public partial class KineticWarfareControl : MonoBehaviour
{
    public enum TempRegroupMode
    {
        WaitForAllReservedIds = 0,
        RemovePregnantReservedIdsAndRegroup = 1
    }

    [Serializable]
    public class TempRegroupEvaluation
    {
        public string reservationId;
        public int totalReservedCount;
        public int readyCount;
        public int busyCount;
        public int pregnantCount;
        public int missingOrDeadCount;

        public List<string> readyIds = new();
        public List<string> busyIds = new();
        public List<string> pregnantIds = new();
        public List<string> missingOrDeadIds = new();

        public bool CanRegroupExactly =>
            totalReservedCount > 0 &&
            busyCount == 0 &&
            pregnantCount == 0 &&
            missingOrDeadCount == 0 &&
            readyCount == totalReservedCount;

        public bool CanRegroupByRemovingPregnant =>
            totalReservedCount > 0 &&
            busyCount == 0 &&
            missingOrDeadCount == 0 &&
            pregnantCount > 0 &&
            (readyCount + pregnantCount) == totalReservedCount &&
            readyCount > 0;
    }

    [Serializable]
    public class TemporarilyDisbandedGroupRecord
    {
        public TileUnitGroupData group;
        public TileUnitGroupControl originalOwner;
        public int populationCost;

        public string originalReservationId;
        public int originalExpiryTurn;
    }

    [SerializeField]
    private List<TemporarilyDisbandedGroupRecord> _temporarilyDisbandedGroups = new();

    public IReadOnlyList<TemporarilyDisbandedGroupRecord> TemporarilyDisbandedGroups
        => _temporarilyDisbandedGroups;

    private int ComputePopulationCostForGroup(TileUnitGroupData group)
    {
        if (group == null) return 0;

        int popCost = Mathf.Max(0, group.reservedPopulation);
        var unit = group.unitType;

        if (popCost <= 0 && unit != null)
        {
            if (unit.populationToTrain > 0 && unit.outputUnits > 0)
            {
                float unitsPerPop = unit.outputUnits / (float)unit.populationToTrain;
                float denom = Mathf.Max(unitsPerPop, 0.0001f);
                popCost = Mathf.CeilToInt(group.unitCount / denom);
            }
            else
            {
                popCost = group.unitCount;
            }
        }

        return Mathf.Max(0, popCost);
    }

    private TemporarilyDisbandedGroupRecord FindTempDisbandRecord(TileUnitGroupData group, out int recordIndex)
    {
        recordIndex = -1;
        if (group == null) return null;

        for (int i = 0; i < _temporarilyDisbandedGroups.Count; i++)
        {
            var r = _temporarilyDisbandedGroups[i];
            if (r != null && r.group == group)
            {
                recordIndex = i;
                return r;
            }
        }

        return null;
    }

    private Individual FindIndividualById(PlayerFamilySimulationManager famSim, string individualId)
    {
        if (famSim == null || string.IsNullOrWhiteSpace(individualId))
            return null;

        var people = famSim.GetIndividuals();
        if (people == null)
            return null;

        for (int i = 0; i < people.Count; i++)
        {
            var p = people[i];
            if (p != null && p.Id == individualId)
                return p;
        }

        return null;
    }

    private int ComputeReducedUnitCount(TileUnitGroupData group, int newPopulationCount)
    {
        if (group == null)
            return 0;

        int originalPopulation = Mathf.Max(1, ComputePopulationCostForGroup(group));
        int originalUnits = Mathf.Max(0, group.unitCount);

        if (newPopulationCount >= originalPopulation)
            return originalUnits;

        float ratio = newPopulationCount / (float)originalPopulation;
        int reduced = Mathf.FloorToInt(originalUnits * ratio + 0.0001f);

        if (reduced <= 0 && newPopulationCount > 0)
            reduced = 1;

        return Mathf.Max(0, reduced);
    }

    public bool TryEvaluateTemporarilyDisbandedGroupRegroup(
        TileUnitGroupData group,
        out TempRegroupEvaluation evaluation,
        out string failReason)
    {
        evaluation = null;
        failReason = string.Empty;

        if (group == null)
        {
            failReason = "Missing group.";
            return false;
        }

        int recordIndex;
        var record = FindTempDisbandRecord(group, out recordIndex);
        if (record == null)
        {
            failReason = "Group is not marked as temporarily disbanded.";
            return false;
        }

        string reservationId = !string.IsNullOrWhiteSpace(record.originalReservationId)
            ? record.originalReservationId
            : group.populationReservationId;

        evaluation = new TempRegroupEvaluation
        {
            reservationId = reservationId
        };

        if (string.IsNullOrWhiteSpace(reservationId))
        {
            evaluation.totalReservedCount = 0;
            return true;
        }

        var popMgr = PlayersPopulationManager.Instance;
        var famSim = PlayerFamilySimulationManager.Instance;

        if (popMgr == null || famSim == null)
        {
            failReason = "Population or family simulation system not found.";
            return false;
        }

        if (!popMgr.TryGetReservedIndividualIds(reservationId, out var reservedIds) || reservedIds == null)
        {
            failReason = "Stored trained individual IDs were not found.";
            return false;
        }

        evaluation.totalReservedCount = reservedIds.Count;

        for (int i = 0; i < reservedIds.Count; i++)
        {
            string id = reservedIds[i];
            var person = FindIndividualById(famSim, id);

            if (person == null || !person.IsAlive)
            {
                evaluation.missingOrDeadCount++;
                evaluation.missingOrDeadIds.Add(id);
                continue;
            }

            bool pregnant = person.Gender == Gender.Female &&
                            famSim.IsIndividualCurrentlyPregnant(person.Id);

            if (pregnant)
            {
                evaluation.pregnantCount++;
                evaluation.pregnantIds.Add(person.Id);
                continue;
            }

            if (person.IsBusy)
            {
                evaluation.busyCount++;
                evaluation.busyIds.Add(person.Id);
                continue;
            }

            evaluation.readyCount++;
            evaluation.readyIds.Add(person.Id);
        }

        return true;
    }

    public bool TryTemporarilyDisbandGroup(
        TileUnitGroupControl owner,
        TileUnitGroupData group,
        out string failReason)
    {
        failReason = string.Empty;

        if (owner == null || group == null)
        {
            failReason = "Missing group or owner.";
            return false;
        }

        var buildingTile = GetComponentInParent<TileControl>();
        var ownerTile = owner.GetComponentInParent<TileControl>();
        if (buildingTile != null && ownerTile != null && buildingTile != ownerTile)
        {
            failReason = "Group must be on this building's tile to disband.";
            return false;
        }

        for (int i = 0; i < _temporarilyDisbandedGroups.Count; i++)
        {
            if (_temporarilyDisbandedGroups[i].group == group)
            {
                failReason = "Group is already temporarily disbanded.";
                return false;
            }
        }

        int popCost = ComputePopulationCostForGroup(group);
        string originalReservationId = group.populationReservationId;
        int originalExpiryTurn = group.expiryTurn;

        var record = new TemporarilyDisbandedGroupRecord
        {
            group = group,
            originalOwner = owner,
            populationCost = Mathf.Max(0, popCost),
            originalReservationId = originalReservationId,
            originalExpiryTurn = originalExpiryTurn
        };

        _temporarilyDisbandedGroups.Add(record);

        if (!string.IsNullOrWhiteSpace(originalReservationId))
            PlayersPopulationManager.Instance?.UnbusyReservationOnly(originalReservationId);

        // IMPORTANT:
        // this overload must NOT release the reservation for temporary disband
        owner.RemoveGroup(group.groupId, releasePopulationReservation: false);

        // Preserve original backing reservation on the stored group
        group.populationReservationId = originalReservationId;
        group.reservedPopulation = Mathf.Max(0, popCost);
        group.expiryTurn = originalExpiryTurn;

        return true;
    }

    public bool CanRebandTemporarilyDisbandedGroup(TileUnitGroupData group)
    {
        if (!TryEvaluateTemporarilyDisbandedGroupRegroup(group, out var eval, out _))
            return false;

        return eval == null || eval.CanRegroupExactly;
    }

    public bool TryRebandTemporarilyDisbandedGroup(
        TileUnitGroupData group,
        TileUnitGroupControl owner,
        out string failReason)
    {
        return TryRebandTemporarilyDisbandedGroup(
            group,
            owner,
            TempRegroupMode.WaitForAllReservedIds,
            out failReason);
    }

    public bool TryRebandTemporarilyDisbandedGroup(
        TileUnitGroupData group,
        TileUnitGroupControl owner,
        TempRegroupMode regroupMode,
        out string failReason)
    {
        failReason = string.Empty;

        if (group == null || owner == null)
        {
            failReason = "Missing group or owner.";
            return false;
        }

        int recordIndex;
        var record = FindTempDisbandRecord(group, out recordIndex);
        if (record == null)
        {
            failReason = "Group is not marked as temporarily disbanded.";
            return false;
        }

        if (!TryEvaluateTemporarilyDisbandedGroupRegroup(group, out var eval, out failReason))
            return false;

        var popMgr = PlayersPopulationManager.Instance;
        if (popMgr == null)
        {
            failReason = "Population system not found.";
            return false;
        }

        string reservationId = eval != null ? eval.reservationId : null;

        if (!string.IsNullOrWhiteSpace(reservationId))
        {
            if (regroupMode == TempRegroupMode.WaitForAllReservedIds)
            {
                if (eval == null || !eval.CanRegroupExactly)
                {
                    if (eval != null && eval.pregnantCount > 0 && eval.busyCount == 0 && eval.missingOrDeadCount == 0)
                        failReason = "A trained female is now pregnant. Wait, or regroup without her.";
                    else
                        failReason = "The original trained individuals are not all ready yet.";

                    return false;
                }
            }
            else
            {
                if (eval == null || !eval.CanRegroupByRemovingPregnant)
                {
                    failReason = "This group cannot regroup in reduced form right now.";
                    return false;
                }

                int removed = popMgr.RemoveIndividualsFromReservation(
                    reservationId,
                    eval.pregnantIds,
                    unbusyRemovedIndividuals: false);

                if (removed != eval.pregnantIds.Count)
                {
                    failReason = "Failed to remove the pregnant individual(s) from the regrouping unit.";
                    return false;
                }

                if (!popMgr.TryGetReservedIndividualIds(reservationId, out var remainingIds) ||
                    remainingIds == null || remainingIds.Count == 0)
                {
                    failReason = "No trained individuals remain available to regroup.";
                    return false;
                }

                int newPopulationCount = remainingIds.Count;
                int newUnitCount = ComputeReducedUnitCount(group, newPopulationCount);

                if (newUnitCount <= 0)
                {
                    failReason = "Regrouping without the pregnant individual(s) would leave no unit strength.";
                    return false;
                }

                group.reservedPopulation = newPopulationCount;
                group.unitCount = newUnitCount;
            }

            popMgr.RebusyReservation(reservationId);

            group.populationReservationId = reservationId;

            if (group.unitType != null && group.unitType.isHuman)
            {
                if (popMgr.TryComputeAndStoreReservationExpiryTurn(reservationId, out int newExpiryTurn))
                    group.expiryTurn = newExpiryTurn;
            }
        }

        owner.AddExistingGroup(group);

        if (recordIndex >= 0)
            _temporarilyDisbandedGroups.RemoveAt(recordIndex);
        else
            _temporarilyDisbandedGroups.Remove(record);

        return true;
    }
}