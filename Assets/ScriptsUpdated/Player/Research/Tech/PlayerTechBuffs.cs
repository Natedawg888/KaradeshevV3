using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PlayerTechBuffs : MonoBehaviour
{
    public static PlayerTechBuffs Instance { get; private set; }

    private EnvironmentTechEffectSO[] _allEnvEffects;   // cached assets

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        _allEnvEffects = Resources.LoadAll<EnvironmentTechEffectSO>("");
    }

    /// DISCOVERY: failure = (base * product(multipliers)) - sum(flatReductions);  turns = base - sum(flatReductions)
    public (float failurePct, int turns) GetDiscoveryEffective(EnvironmentControl env, float baseFailurePct, int baseTurns)
    {
        float clampedBaseFail  = Mathf.Clamp(baseFailurePct, 0f, 100f);
        int   clampedBaseTurns = Mathf.Max(1, baseTurns);

        if (_allEnvEffects == null || _allEnvEffects.Length == 0 || PlayerResearchManager.Instance == null)
            return (clampedBaseFail, clampedBaseTurns);

        float multFail = 1f;
        float flatFailReduction = 0f;   // <-- interpret as how many percentage points to SUBTRACT
        int   flatTurnsReduction = 0;   // <-- interpret as how many whole turns to SUBTRACT

        foreach (var so in _allEnvEffects)
        {
            if (so == null || string.IsNullOrWhiteSpace(so.techID)) continue;
            if (!PlayerResearchManager.Instance.IsResearched(so.techID)) continue;

            var list = so.environmentEffects;
            if (list == null) continue;

            for (int i = 0; i < list.Count; i++)
            {
                var eff = list[i];
                if (!eff.Matches(env)) continue;

                // Failure%: keep multiplicative stack, then subtract flat reduction
                if (eff.discoveryFailureMult > 0f)
                    multFail *= eff.discoveryFailureMult;

                // Treat positive delta as "reduce by that many percentage points"
                flatFailReduction += eff.discoveryFailureDeltaPct;

                // TURNS: flat only; positive means "reduce by N turns"
                flatTurnsReduction += eff.discoveryTurnsDelta;
            }
        }

        // Apply
        float failure = clampedBaseFail * multFail;
        failure = Mathf.Clamp(failure - flatFailReduction, 0f, 100f);

        int turns = Mathf.Max(1, clampedBaseTurns - flatTurnsReduction);

        return (failure, turns);
    }

    /// GATHERING: same rules as discovery
    public (float failurePct, int turns) GetGatheringEffective(EnvironmentControl env, float baseFailurePct, int baseTurns)
    {
        float clampedBaseFail  = Mathf.Clamp(baseFailurePct, 0f, 100f);
        int   clampedBaseTurns = Mathf.Max(1, baseTurns);

        if (_allEnvEffects == null || _allEnvEffects.Length == 0 || PlayerResearchManager.Instance == null)
            return (clampedBaseFail, clampedBaseTurns);

        float multFail = 1f;
        float flatFailReduction = 0f;
        int   flatTurnsReduction = 0;

        foreach (var so in _allEnvEffects)
        {
            if (so == null || string.IsNullOrWhiteSpace(so.techID)) continue;
            if (!PlayerResearchManager.Instance.IsResearched(so.techID)) continue;

            var list = so.environmentEffects;
            if (list == null) continue;

            for (int i = 0; i < list.Count; i++)
            {
                var eff = list[i];
                if (!eff.Matches(env)) continue;

                if (eff.gatheringFailureMult > 0f)
                    multFail *= eff.gatheringFailureMult;

                flatFailReduction += eff.gatheringFailureDeltaPct;
                flatTurnsReduction += eff.gatheringTurnsDelta;
            }
        }

        float failure = clampedBaseFail * multFail;
        failure = Mathf.Clamp(failure - flatFailReduction, 0f, 100f);

        int turns = Mathf.Max(1, clampedBaseTurns - flatTurnsReduction);

        return (failure, turns);
    }

    public int GetDiscoveryRequiredPopEffective(EnvironmentControl env, int baseRequiredPop)
    {
        int clampedBase = Mathf.Max(1, baseRequiredPop);

        if (_allEnvEffects == null || _allEnvEffects.Length == 0 || PlayerResearchManager.Instance == null)
            return clampedBase;

        float mult = 1f;
        int flatReduction = 0; // positive means "reduce by N"

        foreach (var so in _allEnvEffects)
        {
            if (so == null || string.IsNullOrWhiteSpace(so.techID)) continue;
            if (!PlayerResearchManager.Instance.IsResearched(so.techID)) continue;

            var list = so.environmentEffects;
            if (list == null) continue;

            for (int i = 0; i < list.Count; i++)
            {
                var eff = list[i];
                if (!eff.Matches(env)) continue;

                if (eff.discoveryRequiredPopMult > 0f)
                    mult *= eff.discoveryRequiredPopMult;

                flatReduction += eff.discoveryRequiredPopDelta;
            }
        }

        int scaled = Mathf.RoundToInt(clampedBase * mult);
        return Mathf.Max(1, scaled - flatReduction);
    }

    public int GetGatheringRequiredPopEffective(EnvironmentControl env, int baseRequiredPop)
    {
        int clampedBase = Mathf.Max(1, baseRequiredPop);

        if (_allEnvEffects == null || _allEnvEffects.Length == 0 || PlayerResearchManager.Instance == null)
            return clampedBase;

        float mult = 1f;
        int flatReduction = 0;

        foreach (var so in _allEnvEffects)
        {
            if (so == null || string.IsNullOrWhiteSpace(so.techID)) continue;
            if (!PlayerResearchManager.Instance.IsResearched(so.techID)) continue;

            var list = so.environmentEffects;
            if (list == null) continue;

            for (int i = 0; i < list.Count; i++)
            {
                var eff = list[i];
                if (!eff.Matches(env)) continue;

                if (eff.gatheringRequiredPopMult > 0f)
                    mult *= eff.gatheringRequiredPopMult;

                flatReduction += eff.gatheringRequiredPopDelta;
            }
        }

        int scaled = Mathf.RoundToInt(clampedBase * mult);
        return Mathf.Max(1, scaled - flatReduction);
    }

    public int GetDiscoveryPenaltyEffective(EnvironmentControl env, int basePenalty)
    {
        int clampedBase = Mathf.Max(0, basePenalty);

        if (_allEnvEffects == null || _allEnvEffects.Length == 0 || PlayerResearchManager.Instance == null)
            return clampedBase;

        float mult = 1f;
        int flatReduction = 0; // positive means "reduce by N"

        foreach (var so in _allEnvEffects)
        {
            if (so == null || string.IsNullOrWhiteSpace(so.techID)) continue;
            if (!PlayerResearchManager.Instance.IsResearched(so.techID)) continue;

            var list = so.environmentEffects;
            if (list == null) continue;

            for (int i = 0; i < list.Count; i++)
            {
                var eff = list[i];
                if (!eff.Matches(env)) continue;

                if (eff.discoveryPenaltyMult > 0f)
                    mult *= eff.discoveryPenaltyMult;

                flatReduction += eff.discoveryPenaltyDelta;
            }
        }

        int scaled = Mathf.RoundToInt(clampedBase * mult);
        return Mathf.Max(0, scaled - flatReduction);
    }

    public int GetGatheringPenaltyEffective(EnvironmentControl env, int basePenalty)
    {
        int clampedBase = Mathf.Max(0, basePenalty);

        if (_allEnvEffects == null || _allEnvEffects.Length == 0 || PlayerResearchManager.Instance == null)
            return clampedBase;

        float mult = 1f;
        int flatReduction = 0; // positive means "reduce by N"

        foreach (var so in _allEnvEffects)
        {
            if (so == null || string.IsNullOrWhiteSpace(so.techID)) continue;
            if (!PlayerResearchManager.Instance.IsResearched(so.techID)) continue;

            var list = so.environmentEffects;
            if (list == null) continue;

            for (int i = 0; i < list.Count; i++)
            {
                var eff = list[i];
                if (!eff.Matches(env)) continue;

                if (eff.gatheringPenaltyMult > 0f)
                    mult *= eff.gatheringPenaltyMult;

                flatReduction += eff.gatheringPenaltyDelta;
            }
        }

        int scaled = Mathf.RoundToInt(clampedBase * mult);
        return Mathf.Max(0, scaled - flatReduction);
    }
}