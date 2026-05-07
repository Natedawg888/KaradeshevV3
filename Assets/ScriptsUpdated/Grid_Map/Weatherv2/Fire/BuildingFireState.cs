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
    public bool IsFighting          { get; private set; }
    public int  FightTurnsRemaining { get; private set; }
    public int  LastRollResult      { get; private set; }

    // --- events ---
    public event Action<BuildingFireState>           OnIgnited;
    public event Action<BuildingFireState, int>      OnFireDamageStep;
    public event Action<BuildingFireState>           OnExtinguished;
    // roll result + new fight turns remaining
    public event Action<BuildingFireState, int, int> OnFightProgress;

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
        TurnSystem.UnsubscribeFromEndOfTurn(OnEndTurn_FightFire);
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

        IsFighting          = true;
        FightTurnsRemaining = Mathf.Max(1, baseFightTurns);
        LastRollResult      = 0;

        TurnSystem.SubscribeToEndOfTurn(OnEndTurn_FightFire);
        return true;
    }

    /// <summary>Releases reserved population and stops the fight without extinguishing.</summary>
    public void CancelFighting()
    {
        StopFighting();
    }

    private void OnEndTurn_FightFire()
    {
        if (!IsOnFire || !IsFighting)
        {
            StopFighting();
            return;
        }

        int clampedMin = Mathf.Min(rollMin, rollMax);
        int clampedMax = Mathf.Max(rollMin, rollMax);
        LastRollResult       = UnityEngine.Random.Range(clampedMin, clampedMax + 1);
        FightTurnsRemaining  = Mathf.Max(0, FightTurnsRemaining - LastRollResult);

        OnFightProgress?.Invoke(this, LastRollResult, FightTurnsRemaining);

        if (FightTurnsRemaining <= 0)
            Extinguish();
    }

    private void StopFighting()
    {
        if (!IsFighting)
            return;

        IsFighting = false;
        TurnSystem.UnsubscribeFromEndOfTurn(OnEndTurn_FightFire);
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
