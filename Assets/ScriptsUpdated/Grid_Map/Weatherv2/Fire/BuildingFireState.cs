using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BuildingFireState : MonoBehaviour
{
    [Header("Fire Rules")]
    [SerializeField] private bool canCatchFire = true;

    [Header("Extinguish Cost")]
    [Tooltip("Resources the player must spend to start fighting this fire.")]
    public List<ResourceCost> extinguishCost = new();

    [Header("Firefighting")]
    [Tooltip("How many workers are tied up fighting this fire.")]
    public int populationRequired = 2;

    [Tooltip("Base turns needed to extinguish the fire with full population.")]
    public int baseFightTurns = 4;

    [Tooltip("Each turn while fighting, roll a value in [rollMin, rollMax] and subtract from turns remaining. " +
             "Positive = progress, negative = setback.")]
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

    // --- fire state ---
    public bool CanCatchFire    => canCatchFire;
    public bool IsOnFire        { get; private set; }
    public int  BurnTurnsRemaining { get; private set; }
    public int  BaseBurnTurns   { get; private set; }

    // --- fight state ---
    public bool  IsFighting            { get; private set; }
    public int   FightTurnsRemaining   { get; private set; }
    public int   LastRollResult        { get; private set; }
    public int   CasualtiesSoFar       { get; private set; }
    public float CurrentCasualtyChance { get; private set; }

    // --- events ---
    public event Action<BuildingFireState>           OnIgnited;
    public event Action<BuildingFireState, int>      OnFireDamageStep;
    public event Action<BuildingFireState>           OnExtinguished;
    public event Action<BuildingFireState, int, int> OnFightProgress;
    // state, total casualties so far
    public event Action<BuildingFireState, int>      OnFightCasualty;

    private string _populationReservationId;

    private void Awake()
    {
        CacheFireVisualsIfNeeded();
        RefreshVisuals();

        var worldIcon = GetComponentInChildren<BuildingFireWorldIcon>(true);
        if (worldIcon != null)
            worldIcon.Bind(this);
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
            CacheFireVisualsIfNeeded();

        RefreshVisuals();
    }

    private void OnDestroy()
    {
        ReleasePopulationReservation();
    }

    // ------------------------------------------------------------------
    // Fire lifecycle
    // ------------------------------------------------------------------

    public void SetCanCatchFire(bool value)
    {
        canCatchFire = value;
        if (!canCatchFire && IsOnFire)
            Extinguish();
    }

    public bool TryIgnite(float chance01, int burnTurns)
    {
        if (!canCatchFire || IsOnFire)
            return false;

        chance01  = Mathf.Clamp01(chance01);
        burnTurns = Mathf.Max(0, burnTurns);

        if (chance01 <= 0f || burnTurns <= 0)
            return false;

        if (UnityEngine.Random.value > chance01)
            return false;

        IsOnFire           = true;
        BaseBurnTurns      = burnTurns;
        BurnTurnsRemaining = burnTurns;

        RefreshVisuals();
        OnIgnited?.Invoke(this);
        PostFireNotification();

        return true;
    }

    public bool AdvanceBurnStep(int damageThisStep, float extinguishChance01)
    {
        if (!IsOnFire)
            return false;

        if (damageThisStep > 0)
            OnFireDamageStep?.Invoke(this, damageThisStep);

        extinguishChance01 = Mathf.Clamp01(extinguishChance01);
        if (extinguishChance01 > 0f && UnityEngine.Random.value < extinguishChance01)
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

        IsOnFire           = false;
        BurnTurnsRemaining = 0;

        RefreshVisuals();
        OnExtinguished?.Invoke(this);
    }

    // ------------------------------------------------------------------
    // Firefighting
    // ------------------------------------------------------------------

    /// <summary>
    /// Spend resources and reserve population to begin fighting the fire.
    /// Returns false if costs cannot be met or population is unavailable.
    /// </summary>
    public bool TryBeginFighting()
    {
        if (!IsOnFire || IsFighting)
            return false;

        // Spend resources
        if (extinguishCost != null && extinguishCost.Count > 0)
        {
            if (!ResourceDeduction.Deduct(extinguishCost))
                return false;
        }

        // Reserve population
        if (populationRequired > 0)
        {
            var pop = PlayersPopulationManager.Instance;
            if (pop == null || !pop.TryReservePopulation(populationRequired, out _populationReservationId))
            {
                // Refund resources on population failure
                RefundExtinguishCost();
                return false;
            }
        }

        IsFighting            = true;
        FightTurnsRemaining   = Mathf.Max(1, baseFightTurns);
        LastRollResult        = 0;
        CasualtiesSoFar       = 0;
        CurrentCasualtyChance = baseCasualtyChance;

        return true;
    }

    /// <summary>Releases reserved population and stops the fight without extinguishing.</summary>
    public void CancelFighting()
    {
        StopFighting();
    }

    public void TickFight()
    {
        if (!IsOnFire || !IsFighting) { StopFighting(); return; }

        // Progress roll
        int clampedMin = Mathf.Min(rollMin, rollMax);
        int clampedMax = Mathf.Max(rollMin, rollMax);
        LastRollResult      = UnityEngine.Random.Range(clampedMin, clampedMax + 1);
        FightTurnsRemaining = Mathf.Max(0, FightTurnsRemaining - LastRollResult);

        // Casualty roll — risk scales with remaining fire strength
        float fireStrength  = BaseBurnTurns > 0 ? (float)BurnTurnsRemaining / BaseBurnTurns : 0f;
        float effectiveRisk = Mathf.Clamp01(CurrentCasualtyChance * fireStrength);

        if (UnityEngine.Random.value < effectiveRisk)
        {
            CasualtiesSoFar++;
            OnFightCasualty?.Invoke(this, CasualtiesSoFar);
            PlayersPopulationManager.Instance?.ForceSyncUI();

            if (CasualtiesSoFar >= populationRequired)
            {
                // All workers lost — stop first so IsFighting is false when UI reacts
                StopFighting();
                OnFightProgress?.Invoke(this, LastRollResult, FightTurnsRemaining);
                PostFightOutcomeNotification(succeeded: false);
                ScoreManager.NotifyFirefightLost();
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
        {
            PostFightOutcomeNotification(succeeded: true);
            ScoreManager.NotifyFirefightVictory();
            Extinguish();
        }
    }

    private void StopFighting()
    {
        if (!IsFighting)
            return;

        IsFighting = false;
        ReleasePopulationReservation();
    }

    private void ReleasePopulationReservation()
    {
        if (string.IsNullOrEmpty(_populationReservationId))
            return;

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

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

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

    // ------------------------------------------------------------------
    // Visuals & notification
    // ------------------------------------------------------------------

    private void CacheFireVisualsIfNeeded()
    {
        if (!autoFindFireChildByName) return;
        if (fireVisualObjects != null && fireVisualObjects.Length > 0) return;

        Transform child = FindChildRecursive(transform, fireChildName);
        if (child != null)
            fireVisualObjects = new[] { child.gameObject };
    }

    private Transform FindChildRecursive(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrWhiteSpace(targetName)) return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (string.Equals(child.name, targetName, StringComparison.OrdinalIgnoreCase))
                return child;

            Transform nested = FindChildRecursive(child, targetName);
            if (nested != null) return nested;
        }

        return null;
    }

    private void RefreshVisuals()
    {
        if (fireVisualObjects == null) return;

        for (int i = 0; i < fireVisualObjects.Length; i++)
        {
            if (fireVisualObjects[i] != null)
                fireVisualObjects[i].SetActive(IsOnFire);
        }
    }

    private void PostFightOutcomeNotification(bool succeeded)
    {
        if (NotificationManager.Instance == null) return;

        var building = GetComponent<BuildingControl>();
        string name = building != null && !string.IsNullOrWhiteSpace(building.buildingName)
            ? building.buildingName
            : gameObject.name;

        NotificationType type = succeeded ? NotificationType.FireFightSucceeded : NotificationType.FireFightFailed;

        string title, message;
        if (NotificationMessageCrafterManager.Instance != null)
            (title, message) = NotificationMessageCrafterManager.Instance.CraftFireFight(type, name, CasualtiesSoFar);
        else
        {
            title   = succeeded ? "Fire Extinguished!" : "Fire Fight Failed";
            message = succeeded
                ? $"{name} fire put out. {CasualtiesSoFar} lost."
                : $"All workers at {name} were lost to the flames.";
        }

        NotificationManager.Instance.AddNotification(type, title, message, transform.position);
    }

    private void PostFireNotification()
    {
        if (NotificationManager.Instance == null) return;

        var building = GetComponent<BuildingControl>();
        string buildingName = building != null && !string.IsNullOrWhiteSpace(building.buildingName)
            ? building.buildingName
            : gameObject.name;

        string title, message;
        if (NotificationMessageCrafterManager.Instance != null)
            (title, message) = NotificationMessageCrafterManager.Instance.CraftBuilding(NotificationType.BuildingOnFire, buildingName);
        else
        {
            title   = "Building on Fire!";
            message = $"{buildingName} is on fire!";
        }

        NotificationManager.Instance.AddNotification(NotificationType.BuildingOnFire, title, message, transform.position);
    }
}
