using System;
using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class EnvironmentFireState : MonoBehaviour
{
    [Header("Fire Rules")]
    [SerializeField] private bool canCatchFire = true;

    [Tooltip("Dryness starts here when ignited if current dryness is lower.")]
    [Range(0f, 1f)][SerializeField] private float drynessWhenIgnited = 0.80f;

    [Tooltip("How quickly the tile dries out when not being rained on.")]
    [Range(0f, 1f)][SerializeField] private float drynessRecoveryPerStep = 0.08f;

    [Tooltip("How strongly rain reduces dryness each step.")]
    [Range(0f, 1f)][SerializeField] private float rainDrynessReductionPerStep = 0.35f;

    [Header("Extinguish Cost")]
    public List<ResourceCost> extinguishCost = new();

    [Header("Firefighting")]
    public int populationRequired = 2;
    public int baseFightTurns = 4;
    public int rollMin = -1;
    public int rollMax = 3;

    [Header("Casualty Risk")]
    [Tooltip("Base chance per turn of losing a worker at full fire strength.")]
    [Range(0f, 1f)] public float baseCasualtyChance = 0.30f;
    [Tooltip("How much the casualty chance drops per safe turn (no casualty).")]
    [Range(0f, 1f)] public float casualtyReductionPerSafeRoll = 0.05f;

    [Header("Fire Visuals")]
    [SerializeField] private GameObject[] fireVisualObjects;
    [SerializeField] private bool autoFindFireChildByName = true;
    [SerializeField] private string fireChildName = "Fire";

    public bool CanCatchFire => canCatchFire;
    public bool IsOnFire { get; private set; }
    public int BurnTurnsRemaining { get; private set; }
    public int BaseBurnTurns { get; private set; }
    public float CurrentDryness01 { get; private set; } = 0.5f;

    public bool  IsFighting           { get; private set; }
    public int   FightTurnsRemaining  { get; private set; }
    public int   LastRollResult       { get; private set; }
    public int   CasualtiesSoFar      { get; private set; }
    public float CurrentCasualtyChance { get; private set; }

    public event Action<EnvironmentFireState>           OnIgnited;
    public event Action<EnvironmentFireState>           OnExtinguished;
    public event Action<EnvironmentFireState, int, int> OnFightProgress;
    // state, total casualties so far
    public event Action<EnvironmentFireState, int>      OnFightCasualty;

    private string _populationReservationId;

    private readonly Dictionary<Renderer, Material[]> originalFireMaterials = new();

    private void Awake()
    {
        CacheFireVisualsIfNeeded();
        CacheOriginalFireMaterials();
        RefreshVisuals();
    }

    private void OnDestroy()
    {
        ReleasePopulationReservation();
        TurnSystem.UnsubscribeFromEndOfTurn(OnEndTurn_FightFire);
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
            CacheFireVisualsIfNeeded();

        RefreshVisuals();
    }

    public void SetCanCatchFire(bool value)
    {
        canCatchFire = value;

        if (!canCatchFire && IsOnFire)
            Extinguish();
    }

    public void RefreshDrynessFromWeather(float rain01)
    {
        rain01 = Mathf.Clamp01(rain01);

        if (rain01 > 0f)
        {
            CurrentDryness01 = Mathf.Clamp01(
                CurrentDryness01 - rain01 * rainDrynessReductionPerStep);
        }
        else
        {
            CurrentDryness01 = Mathf.Clamp01(
                CurrentDryness01 + drynessRecoveryPerStep);
        }
    }

    public bool TryIgnite(float chance01, int burnTurns)
    {
        if (!canCatchFire)
            return false;

        if (IsOnFire)
            return false;

        chance01 = Mathf.Clamp01(chance01);
        burnTurns = Mathf.Max(0, burnTurns);

        if (chance01 <= 0f || burnTurns <= 0)
            return false;

        if (UnityEngine.Random.value > chance01)
            return false;

        IsOnFire = true;
        BaseBurnTurns = burnTurns;
        BurnTurnsRemaining = burnTurns;
        CurrentDryness01 = Mathf.Max(CurrentDryness01, drynessWhenIgnited);

        RefreshVisuals();
        OnIgnited?.Invoke(this);

        return true;
    }

    private void CacheOriginalFireMaterials()
    {
        originalFireMaterials.Clear();

        if (fireVisualObjects == null)
            return;

        for (int i = 0; i < fireVisualObjects.Length; i++)
        {
            GameObject fireObj = fireVisualObjects[i];
            if (fireObj == null)
                continue;

            Renderer[] renderers = fireObj.GetComponentsInChildren<Renderer>(includeInactive: true);

            for (int r = 0; r < renderers.Length; r++)
            {
                Renderer renderer = renderers[r];
                if (renderer == null)
                    continue;

                originalFireMaterials[renderer] = renderer.sharedMaterials;
            }
        }
    }

    public bool AdvanceBurnStep(float rain01, float extinguishChanceAtFullRain)
    {
        if (!IsOnFire)
            return false;

        rain01 = Mathf.Clamp01(rain01);
        RefreshDrynessFromWeather(rain01);

        float extinguishChance = Mathf.Clamp01(extinguishChanceAtFullRain * rain01);
        if (extinguishChance > 0f && UnityEngine.Random.value < extinguishChance)
        {
            Extinguish();
            return false;
        }

        BurnTurnsRemaining--;

        if (BurnTurnsRemaining <= 0)
        {
            Extinguish();
            return false;
        }

        RefreshVisuals();
        return true;
    }

    public void Extinguish()
    {
        if (!IsOnFire)
            return;

        StopFighting();

        IsOnFire = false;
        BurnTurnsRemaining = 0;

        RefreshVisuals();
        OnExtinguished?.Invoke(this);
    }

    // ------------------------------------------------------------------
    // Firefighting
    // ------------------------------------------------------------------

    public bool TryBeginFighting()
    {
        if (!IsOnFire || IsFighting) return false;

        if (extinguishCost != null && extinguishCost.Count > 0)
            if (!ResourceDeduction.Deduct(extinguishCost)) return false;

        if (populationRequired > 0)
        {
            var pop = PlayersPopulationManager.Instance;
            if (pop == null || !pop.TryReservePopulation(populationRequired, out _populationReservationId))
            {
                RefundExtinguishCost();
                return false;
            }
        }

        IsFighting            = true;
        FightTurnsRemaining   = Mathf.Max(1, baseFightTurns);
        LastRollResult        = 0;
        CasualtiesSoFar       = 0;
        CurrentCasualtyChance = baseCasualtyChance;

        TurnSystem.SubscribeToEndOfTurn(OnEndTurn_FightFire);
        return true;
    }

    public void CancelFighting() => StopFighting();

    public bool CanAffordFight()
    {
        if (extinguishCost == null || extinguishCost.Count == 0) return true;
        return InventoryQuery.CanAfford(extinguishCost);
    }

    public bool HasEnoughPopulation()
    {
        if (populationRequired <= 0) return true;
        var pop = PlayersPopulationManager.Instance;
        return pop != null && pop.GetAvailableTaskPopulation() >= populationRequired;
    }

    private void OnEndTurn_FightFire()
    {
        if (!IsOnFire || !IsFighting) { StopFighting(); return; }

        // Progress roll
        int lo = Mathf.Min(rollMin, rollMax);
        int hi = Mathf.Max(rollMin, rollMax);
        LastRollResult      = UnityEngine.Random.Range(lo, hi + 1);
        FightTurnsRemaining = Mathf.Max(0, FightTurnsRemaining - LastRollResult);

        // Casualty roll — risk scales with remaining fire strength
        float fireStrength   = BaseBurnTurns > 0 ? (float)BurnTurnsRemaining / BaseBurnTurns : 0f;
        float effectiveRisk  = Mathf.Clamp01(CurrentCasualtyChance * fireStrength);

        if (UnityEngine.Random.value < effectiveRisk)
        {
            CasualtiesSoFar++;
            OnFightCasualty?.Invoke(this, CasualtiesSoFar);

            if (CasualtiesSoFar >= populationRequired)
            {
                // All workers lost — fight collapses
                OnFightProgress?.Invoke(this, LastRollResult, FightTurnsRemaining);
                StopFighting();
                return;
            }
        }
        else
        {
            // Safe roll — reduce future risk as fight progresses
            CurrentCasualtyChance = Mathf.Max(0f, CurrentCasualtyChance - casualtyReductionPerSafeRoll);
        }

        OnFightProgress?.Invoke(this, LastRollResult, FightTurnsRemaining);

        if (FightTurnsRemaining <= 0)
            Extinguish();
    }

    private void StopFighting()
    {
        if (!IsFighting) return;
        IsFighting = false;
        TurnSystem.UnsubscribeFromEndOfTurn(OnEndTurn_FightFire);
        ReleasePopulationReservation();
    }

    private void ReleasePopulationReservation()
    {
        if (string.IsNullOrEmpty(_populationReservationId)) return;
        PlayersPopulationManager.Instance?.ReleaseReservation(_populationReservationId);
        _populationReservationId = null;
    }

    private void RefundExtinguishCost()
    {
        if (extinguishCost == null) return;
        var inv = PlayerInventoryManager.Instance;
        if (inv == null) return;
        for (int i = 0; i < extinguishCost.Count; i++)
        {
            var c = extinguishCost[i];
            if (c?.resource != null && c.amount > 0)
                inv.TryAdd(c.resource, c.amount);
        }
    }

    private void CacheFireVisualsIfNeeded()
    {
        if (!autoFindFireChildByName)
            return;

        if (fireVisualObjects != null && fireVisualObjects.Length > 0)
            return;

        Transform child = FindChildRecursive(transform, fireChildName);
        if (child != null)
            fireVisualObjects = new[] { child.gameObject };
    }

    private Transform FindChildRecursive(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrWhiteSpace(targetName))
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);

            if (string.Equals(child.name, targetName, StringComparison.OrdinalIgnoreCase))
                return child;

            Transform nested = FindChildRecursive(child, targetName);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private void RefreshVisuals()
    {
        if (fireVisualObjects == null)
            return;

        if (IsOnFire)
            RestoreOriginalFireMaterials();

        for (int i = 0; i < fireVisualObjects.Length; i++)
        {
            if (fireVisualObjects[i] != null)
                fireVisualObjects[i].SetActive(IsOnFire);
        }
    }

    private void RestoreOriginalFireMaterials()
    {
        foreach (var kvp in originalFireMaterials)
        {
            Renderer renderer = kvp.Key;
            Material[] materials = kvp.Value;

            if (renderer == null || materials == null || materials.Length == 0)
                continue;

            renderer.sharedMaterials = materials;
        }
    }
}