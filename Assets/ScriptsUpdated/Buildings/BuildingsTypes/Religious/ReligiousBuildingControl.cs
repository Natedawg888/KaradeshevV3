using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BuildingControl))]
public class ReligiousBuildingControl : MonoBehaviour, IBuildingTypeHandler, IBuildingTurnTickable
{
    [Serializable]
    private class RitualCooldownState
    {
        public string ritualID;
        public int readyOnTurn;
    }

    [Serializable]
    private class ActiveRitualRuntimeState
    {
        public ReligionRitualDefinitionSO ritual;
        public SpiritDefinitionSO targetSpirit;
        public int totalTurns;
        public int turnsRemaining;
        public int startedOnTurn;
        public string workerReservationId;
    }

    [Header("Ritual World Overlay")]
    [SerializeField] private TimerUI ritualTimerUI;

    [Header("Ritual Lighting")]
    [SerializeField] private Light ritualLight;
    [SerializeField] private bool restoreOriginalLightStateWhenIdle = true;

    private Color _ritualLightDefaultColor;
    private float _ritualLightDefaultIntensity;
    private float _ritualLightDefaultRange;
    private bool _ritualLightDefaultEnabled;
    private bool _ritualLightDefaultsCaptured;

    [Header("Building Spirit Capacity")]
    [Min(1)] public int maxAffiliatedSpirits = 1;

    [Header("Building Ritual Options")]
    [Tooltip("These are the rituals this building can perform. The known-ritual manager filters what is actually available.")]
    public List<ReligionRitualDefinitionSO> ritualOptions = new List<ReligionRitualDefinitionSO>();

    [Header("Runtime")]
    [SerializeField] private List<SpiritDefinitionSO> affiliatedSpirits = new List<SpiritDefinitionSO>();
    [SerializeField] private List<ReligionRitualDefinitionSO> completedNonRepeatableRituals = new List<ReligionRitualDefinitionSO>();
    [SerializeField] private List<RitualCooldownState> ritualCooldowns = new List<RitualCooldownState>();
    [SerializeField] private ActiveRitualRuntimeState activeRitual;

    private BuildingHealth _buildingHealth;
    private float _lastKnownHealthFraction = 1f;

    private readonly List<ReligionRitualDefinitionSO> _tmpKnownRituals = new List<ReligionRitualDefinitionSO>(32);

    private BuildingControl _buildingControl;
    private BuildingStatus _buildingStatus;

    public BuildingType HandledType => BuildingType.Religious;

    public IReadOnlyList<SpiritDefinitionSO> AffiliatedSpirits => affiliatedSpirits;
    public bool HasActiveRitual => activeRitual != null && activeRitual.ritual != null;
    public ReligionRitualDefinitionSO CurrentRitual => activeRitual != null ? activeRitual.ritual : null;
    public SpiritDefinitionSO CurrentRitualSpirit => activeRitual != null ? activeRitual.targetSpirit : null;
    public int CurrentRitualTurnsRemaining => activeRitual != null ? Mathf.Max(0, activeRitual.turnsRemaining) : 0;
    private readonly List<Individual> _tmpPopulationSacrificeCandidates = new List<Individual>(32);

    public int CurrentRitualMaxTurns =>
    activeRitual != null
        ? Mathf.Max(1, activeRitual.totalTurns)
        : 0;

    public event Action BuildingReligionChanged;

    private void Awake()
    {
        _buildingControl = GetComponent<BuildingControl>();
        _buildingStatus = GetComponent<BuildingStatus>();
        _buildingHealth = GetComponent<BuildingHealth>();

        if (_buildingHealth != null)
        {
            int max = Mathf.Max(1, _buildingHealth.maxHealth);
            _lastKnownHealthFraction = Mathf.Clamp01(_buildingHealth.CurrentHealth / (float)max);
        }

        CacheRitualLightDefaults();
    }

    private void OnEnable()
    {
        BuildingTickManager.Instance?.Register(this);

        if (PlayerReligionManager.Instance != null)
            PlayerReligionManager.Instance.ReligionChanged += HandleReligionChanged;

        if (_buildingHealth != null)
            _buildingHealth.OnHealthChanged += HandleBuildingHealthChanged;
    }

    private void OnDisable()
    {
        BuildingTickManager.Instance?.Unregister(this);

        if (PlayerReligionManager.Instance != null)
            PlayerReligionManager.Instance.ReligionChanged -= HandleReligionChanged;

        if (_buildingHealth != null)
            _buildingHealth.OnHealthChanged -= HandleBuildingHealthChanged;

        ResetRitualLight();
    }

    private void CacheRitualLightDefaults()
    {
        if (ritualLight == null || _ritualLightDefaultsCaptured)
            return;

        _ritualLightDefaultColor = ritualLight.color;
        _ritualLightDefaultIntensity = ritualLight.intensity;
        _ritualLightDefaultRange = ritualLight.range;
        _ritualLightDefaultEnabled = ritualLight.enabled;
        _ritualLightDefaultsCaptured = true;
    }

    private void ApplyRitualLight(ReligionRitualDefinitionSO ritual)
    {
        if (ritualLight == null)
            return;

        CacheRitualLightDefaults();

        if (ritual == null)
        {
            ResetRitualLight();
            return;
        }

        if (ritual.useCustomRitualLight)
        {
            ritualLight.color = ritual.ritualLightColor;
            ritualLight.intensity = ritual.ritualLightIntensity;
            ritualLight.range = ritual.ritualLightRange;
            ritualLight.enabled = ritual.ritualLightIntensity > 0f;
        }
        else
        {
            ritualLight.enabled = true;
        }
    }

    private void ResetRitualLight()
    {
        if (ritualLight == null)
            return;

        CacheRitualLightDefaults();

        if (restoreOriginalLightStateWhenIdle)
        {
            ritualLight.color = _ritualLightDefaultColor;
            ritualLight.intensity = _ritualLightDefaultIntensity;
            ritualLight.range = _ritualLightDefaultRange;
            ritualLight.enabled = _ritualLightDefaultEnabled;
        }
        else
        {
            ritualLight.enabled = false;
        }
    }

    public void OnTypeEnabled()
    {
        PruneInvalidAffiliatedSpirits();
        NotifyChanged();
    }

    public void OnTypeDisabled()
    {
        CancelActiveRitual(releaseWorkers: true);
        NotifyChanged();
    }

    public void OnBuildingStateChanged(BuildingState state)
    {
        if (state == BuildingState.Destroyed)
            CancelActiveRitual(releaseWorkers: true);
    }

    private void HandleReligionChanged()
    {
        PruneInvalidAffiliatedSpirits();
        NotifyChanged();
    }

    public void TurnTick()
    {
        if (_buildingControl == null || _buildingControl.ActiveType != BuildingType.Religious)
            return;

        if (_buildingStatus != null && _buildingStatus.CurrentState == BuildingState.Destroyed)
            return;

        if (!HasActiveRitual)
            return;

        activeRitual.turnsRemaining--;

        if (activeRitual.turnsRemaining <= 0)
            CompleteActiveRitual();

        UpdateRitualTimerUI();
        NotifyChanged();
    }

    public bool IsSpiritAffiliated(SpiritDefinitionSO spirit)
    {
        if (spirit == null)
            return false;

        return affiliatedSpirits.Contains(spirit);
    }

    public bool CanAffiliateAnotherSpirit(SpiritDefinitionSO spirit)
    {
        if (spirit == null)
            return false;

        if (IsSpiritAffiliated(spirit))
            return true;

        return affiliatedSpirits.Count < Mathf.Max(1, maxAffiliatedSpirits);
    }

    public bool TryAffiliateSpirit(SpiritDefinitionSO spirit, out string reason)
    {
        reason = null;

        if (spirit == null)
        {
            reason = "Spirit is null.";
            return false;
        }

        if (IsSpiritAffiliated(spirit))
            return true;

        if (!CanAffiliateAnotherSpirit(spirit))
        {
            reason = "This building has no free spirit slots.";
            return false;
        }

        affiliatedSpirits.Add(spirit);
        MarkReligionDirty();
        NotifyChanged();
        return true;
    }

    public void RemoveAffiliatedSpirit(SpiritDefinitionSO spirit)
    {
        if (spirit == null)
            return;

        if (affiliatedSpirits.Remove(spirit))
            NotifyChanged();
        MarkReligionDirty();
    }

    public void GetAvailableKnownRituals(List<ReligionRitualDefinitionSO> outRituals)
    {
        if (outRituals == null)
            return;

        outRituals.Clear();

        PlayerReligionManager religion = PlayerReligionManager.Instance;
        if (religion == null)
            return;

        PlayerKnownRitualsManager knownMgr = PlayerKnownRitualsManager.Instance;

        for (int i = 0; i < ritualOptions.Count; i++)
        {
            ReligionRitualDefinitionSO ritual = ritualOptions[i];
            if (ritual == null)
                continue;

            if (!ritual.MatchesBeliefSystem(religion.currentBeliefSystem))
                continue;

            if (knownMgr != null && !knownMgr.IsKnown(ritual))
                continue;

            if (!ritual.repeatable && completedNonRepeatableRituals.Contains(ritual))
                continue;

            outRituals.Add(ritual);
        }
    }

    public bool IsRitualOnCooldown(ReligionRitualDefinitionSO ritual, out int turnsRemaining)
    {
        turnsRemaining = 0;

        if (ritual == null || string.IsNullOrWhiteSpace(ritual.ritualID))
            return false;

        int currentTurn = GetCurrentTurn();

        for (int i = 0; i < ritualCooldowns.Count; i++)
        {
            RitualCooldownState entry = ritualCooldowns[i];
            if (entry == null || entry.ritualID != ritual.ritualID)
                continue;

            if (entry.readyOnTurn <= currentTurn)
                return false;

            turnsRemaining = entry.readyOnTurn - currentTurn;
            return true;
        }

        return false;
    }

    public bool TryStartRitual(
        ReligionRitualDefinitionSO ritual,
        SpiritDefinitionSO selectedSpirit,
        out string reason)
    {
        reason = null;

        PlayerReligionManager religion = PlayerReligionManager.Instance;
        if (religion == null)
        {
            reason = "PlayerReligionManager is missing.";
            return false;
        }

        if (_buildingControl == null || _buildingControl.ActiveType != BuildingType.Religious)
        {
            reason = "This building is not currently in Religious mode.";
            return false;
        }

        if (_buildingStatus != null && _buildingStatus.CurrentState == BuildingState.Destroyed)
        {
            reason = "This building is destroyed.";
            return false;
        }

        if (ritual == null)
        {
            reason = "Ritual is null.";
            return false;
        }

        if (HasActiveRitual)
        {
            reason = "A ritual is already in progress.";
            return false;
        }

        if (!ritualOptions.Contains(ritual))
        {
            reason = "This building does not support that ritual.";
            return false;
        }

        PlayerKnownRitualsManager knownMgr = PlayerKnownRitualsManager.Instance;
        if (knownMgr != null && !knownMgr.IsKnown(ritual))
        {
            reason = "This ritual is not yet known.";
            return false;
        }

        if (!ritual.MatchesBeliefSystem(religion.currentBeliefSystem))
        {
            reason = "This ritual does not match the current belief system.";
            return false;
        }

        if (!ritual.repeatable && completedNonRepeatableRituals.Contains(ritual))
        {
            reason = "This ritual has already been completed and is not repeatable.";
            return false;
        }

        if (IsRitualOnCooldown(ritual, out int cooldownLeft))
        {
            reason = $"This ritual is on cooldown for {cooldownLeft} more turns.";
            return false;
        }

        SpiritDefinitionSO resolvedSpirit = ritual.IsSummoningRitual
            ? null
            : ritual.ResolveSpirit(selectedSpirit);
        if (!ValidateRitualTarget(ritual, resolvedSpirit, religion, out reason))
            return false;

        string workerReservationId = null;
        if (!TryReserveRitualWorkers(ritual, out workerReservationId, out reason))
            return false;

        int resolvedTurnsRequired = Mathf.Max(1, ritual.turnsRequired);

        if (ritual.IsBanishmentRitual)
        {
            if (resolvedSpirit == null)
            {
                reason = "No target spirit was selected for banishment.";
                return false;
            }

            if (!resolvedSpirit.TryGetBanishmentOption(out var banishment))
            {
                reason = "That spirit does not support banishment.";
                return false;
            }

            resolvedTurnsRequired = Mathf.Max(1, banishment.turnsRequired);
        }

        activeRitual = new ActiveRitualRuntimeState
        {
            ritual = ritual,
            targetSpirit = resolvedSpirit,
            totalTurns = resolvedTurnsRequired,
            turnsRemaining = resolvedTurnsRequired,
            startedOnTurn = GetCurrentTurn(),
            workerReservationId = workerReservationId
        };

        BeginRitualVisuals();
        MarkReligionDirty();
        NotifyChanged();
        return true;
    }

    public void CancelActiveRitual(bool releaseWorkers)
    {
        if (!HasActiveRitual)
            return;

        if (releaseWorkers && !string.IsNullOrWhiteSpace(activeRitual.workerReservationId))
            PlayersPopulationManager.Instance?.ReleaseReservation(activeRitual.workerReservationId);

        activeRitual = null;
        CompleteRitualVisuals();
        MarkReligionDirty();
        NotifyChanged();
    }

    private bool ValidateRitualTarget(
        ReligionRitualDefinitionSO ritual,
        SpiritDefinitionSO spirit,
        PlayerReligionManager religion,
        out string reason)
    {
        reason = null;

        if (ritual == null)
        {
            reason = "Ritual is null.";
            return false;
        }

        if (ritual.IsSummoningRitual)
        {
            if (!HasFreeSpiritSlot())
            {
                reason = "This building has no free spirit slots for another spirit.";
                return false;
            }

            return true;
        }

        if (spirit == null)
        {
            reason = "No target spirit was resolved for this ritual.";
            return false;
        }

        if (ritual.requiresAcceptedSpirit && !religion.IsAccepted(spirit))
        {
            reason = "That spirit is not currently accepted.";
            return false;
        }

        if (ritual.requiresSpiritAffiliationAtBuilding && !IsSpiritAffiliated(spirit))
        {
            reason = "That spirit is not affiliated with this building.";
            return false;
        }

        if (ritual.IsResourceOffering)
        {
            if (ritual.resourceDefinition == null)
            {
                reason = "This ritual has no resource offering defined.";
                return false;
            }

            if (!spirit.TryGetMatchingResourceOffering(ritual.resourceDefinition, ritual.resourceAmount, out _))
            {
                reason = "This ritual's resource offering does not match any offering rule on the selected spirit.";
                return false;
            }

            if (!TryValidateResourceOfferingCost(ritual, out reason))
                return false;
        }

        if (ritual.IsPopulationSacrifice)
        {
            if (!spirit.TryGetMatchingPopulationSacrifice(
                    ritual.sacrificeSexFilter,
                    ritual.sacrificeAgeFilter,
                    ritual.sacrificeCount,
                    out _))
            {
                reason = "This ritual's sacrifice does not match any sacrifice rule on the selected spirit.";
                return false;
            }

            if (!TryValidatePopulationSacrifice(ritual, out reason))
                return false;
        }

        if (ritual.IsBanishmentRitual)
        {
            if (spirit == null)
            {
                reason = "No target spirit was resolved for this banishment ritual.";
                return false;
            }

            if (!spirit.TryGetBanishmentOption(out var banishment))
            {
                reason = "That spirit does not have a banishment setup.";
                return false;
            }

            if (banishment.resourceDefinition == null || banishment.resourceAmount <= 0)
            {
                reason = "That spirit's banishment setup is missing its resource cost.";
                return false;
            }

            List<ResourceCost> costs = new List<ResourceCost>(1)
            {
                new ResourceCost
                {
                    resource = banishment.resourceDefinition,
                    amount = banishment.resourceAmount
                }
            };

            if (!InventoryQuery.CanAfford(costs))
            {
                reason = "Not enough resources to perform this banishment.";
                return false;
            }

            return true;
        }

        return true;
    }

    private bool TryReserveRitualWorkers(
        ReligionRitualDefinitionSO ritual,
        out string reservationId,
        out string reason)
    {
        reservationId = null;
        reason = null;

        if (ritual == null || ritual.workerCount <= 0)
            return true;

        PlayersPopulationManager population = PlayersPopulationManager.Instance;
        if (population == null)
        {
            reason = "PlayersPopulationManager is missing.";
            return false;
        }

        string ownerId = _buildingControl != null
            ? _buildingControl.UniqueInstanceID
            : gameObject.GetInstanceID().ToString();

        if (!population.TryReservePopulation(
                ritual.workerCount,
                PopulationReservationKind.Religion ,
                ownerId,
                nameof(ReligiousBuildingControl),
                out reservationId))
        {
            reason = "Not enough available population to perform this ritual.";
            return false;
        }

        return true;
    }

    private void CompleteActiveRitual()
    {
        if (!HasActiveRitual)
            return;

        ReligionRitualDefinitionSO ritual = activeRitual.ritual;
        SpiritDefinitionSO spirit = activeRitual.targetSpirit;
        string workerReservationId = activeRitual.workerReservationId;

        bool success = ExecuteCompletedRitual(ritual, spirit, out string failureReason);

        if (!string.IsNullOrWhiteSpace(workerReservationId))
            PlayersPopulationManager.Instance?.ReleaseReservation(workerReservationId);

        if (success)
        {
            ApplyCooldown(ritual);

            if (!ritual.repeatable && !completedNonRepeatableRituals.Contains(ritual))
                completedNonRepeatableRituals.Add(ritual);

            if (ritual.faithRequired > 0f)
                CivilizationStateManager.Instance?.AdjustFaith(-(ritual.faithRequired * 0.5f));

            if (ritual.faithReward > 0f)
                CivilizationStateManager.Instance?.AdjustFaith(ritual.faithReward);
        }
        else
        {
            //Debug.LogWarning($"[ReligiousBuildingControl] Ritual '{ritual?.displayName}' failed on completion: {failureReason}");
            if (ritual != null && ritual.faithReward > 0f)
                CivilizationStateManager.Instance?.AdjustFaith(-ritual.faithReward);
        }

        activeRitual = null;
        CompleteRitualVisuals();
        MarkReligionDirty();
        NotifyChanged();
    }

    private bool ExecuteCompletedRitual(
        ReligionRitualDefinitionSO ritual,
        SpiritDefinitionSO spirit,
        out string reason)
    {
        reason = null;

        PlayerReligionManager religion = PlayerReligionManager.Instance;
        if (religion == null)
        {
            reason = "PlayerReligionManager is missing.";
            return false;
        }

        int currentTurn = GetCurrentTurn();

        if (ritual.IsSummoningRitual)
        {
            PlayerRitualManager ritualMgr = PlayerRitualManager.Instance;
            if (ritualMgr == null)
            {
                reason = "PlayerRitualManager is missing.";
                return false;
            }

            if (!ritualMgr.EnqueueSummoningChoice(ritual, this, out _))
            {
                reason = "Failed to create a summoning spirit choice.";
                return false;
            }

            return true;
        }

        if (ritual.IsResourceOffering)
        {
            if (!TryConsumeResourceOfferingCost(ritual, out reason))
                return false;

            return religion.TryOfferResource(
                spirit,
                ritual.resourceDefinition,
                ritual.resourceAmount,
                currentTurn,
                out _);
        }

        if (ritual.IsPopulationSacrifice)
        {
            if (!TryConsumePopulationSacrifice(ritual, out reason))
                return false;

            return religion.TryOfferPopulationSacrifice(
                spirit,
                ritual.sacrificeSexFilter,
                ritual.sacrificeAgeFilter,
                ritual.sacrificeCount,
                currentTurn,
                out _);
        }

        if (ritual.IsBanishmentRitual)
        {
            if (spirit == null)
            {
                reason = "No spirit was selected for banishment.";
                return false;
            }

            if (!spirit.TryGetBanishmentOption(out var banishment))
            {
                reason = "That spirit does not have a banishment setup.";
                return false;
            }

            PlayerInventoryManager inv = PlayerInventoryManager.Instance;
            if (inv == null)
            {
                reason = "PlayerInventoryManager is missing.";
                return false;
            }

            bool removed = banishment.resourceDefinition.isGroup
                ? inv.TryRemoveGroup(banishment.resourceDefinition, banishment.resourceAmount)
                : inv.TryRemove(banishment.resourceDefinition, banishment.resourceAmount);

            if (!removed)
            {
                reason = "Failed to consume the banishment cost.";
                return false;
            }

            bool failed = UnityEngine.Random.value < Mathf.Clamp01(banishment.failureChance);

            if (failed)
            {
                //Debug.Log($"[ReligiousBuildingControl] Banishment failed for spirit '{spirit.displayName}'.");
                return true; // ritual completed, but outcome failed
            }

            religion.RemoveSpirit(spirit);
            RemoveAffiliatedSpirit(spirit);

            //Debug.Log($"[ReligiousBuildingControl] Spirit '{spirit.displayName}' was banished.");
            return true;
        }

        reason = "Unsupported ritual kind.";
        return false;
    }

    private void ApplyCooldown(ReligionRitualDefinitionSO ritual)
    {
        if (ritual == null || ritual.cooldownTurns <= 0 || string.IsNullOrWhiteSpace(ritual.ritualID))
            return;

        int readyOnTurn = GetCurrentTurn() + ritual.cooldownTurns;

        for (int i = 0; i < ritualCooldowns.Count; i++)
        {
            RitualCooldownState entry = ritualCooldowns[i];
            if (entry == null || entry.ritualID != ritual.ritualID)
                continue;

            entry.readyOnTurn = readyOnTurn;
            return;
        }

        ritualCooldowns.Add(new RitualCooldownState
        {
            ritualID = ritual.ritualID,
            readyOnTurn = readyOnTurn
        });
        MarkReligionDirty();
    }

    private void PruneInvalidAffiliatedSpirits()
    {
        PlayerReligionManager religion = PlayerReligionManager.Instance;
        if (religion == null)
            return;

        bool changed = false;

        for (int i = affiliatedSpirits.Count - 1; i >= 0; i--)
        {
            SpiritDefinitionSO spirit = affiliatedSpirits[i];
            if (spirit != null && religion.IsAccepted(spirit))
                continue;

            affiliatedSpirits.RemoveAt(i);
            changed = true;
        }

        if (changed)
            NotifyChanged();
    }

    private int GetCurrentTurn()
    {
        return TurnSystem.Instance != null ? TurnSystem.GetCurrentTurn() : 0;
    }

    private void NotifyChanged()
    {
        BuildingReligionChanged?.Invoke();
    }

    // -------------------------------------------------
    // Ritual cost hooks
    // -------------------------------------------------

    private bool TryValidateResourceOfferingCost(ReligionRitualDefinitionSO ritual, out string reason)
    {
        reason = null;

        if (ritual == null || ritual.resourceDefinition == null || ritual.resourceAmount <= 0)
            return true;

        List<ResourceCost> costs = new List<ResourceCost>(1)
        {
            new ResourceCost
            {
                resource = ritual.resourceDefinition,
                amount = ritual.resourceAmount
            }
        };

        if (!InventoryQuery.CanAfford(costs))
        {
            reason = "Not enough resources for this offering.";
            return false;
        }

        return true;
    }

    private bool TryConsumeResourceOfferingCost(ReligionRitualDefinitionSO ritual, out string reason)
    {
        reason = null;

        if (!TryValidateResourceOfferingCost(ritual, out reason))
            return false;

        if (ritual == null || ritual.resourceDefinition == null || ritual.resourceAmount <= 0)
            return true;

        PlayerInventoryManager inv = PlayerInventoryManager.Instance;
        if (inv == null)
        {
            reason = "PlayerInventoryManager is missing.";
            return false;
        }

        ResourceDefinition resourceDef = ritual.resourceDefinition;
        int amount = ritual.resourceAmount;

        // Safety check before remove. This matters because TryRemove() returns true
        // if it removed anything, and for non-group resources it can remove less than requested
        // if you don't verify the amount first.
        if (inv.GetAmount(resourceDef) < amount)
        {
            reason = $"Not enough {resourceDef.resourceName} for this offering.";
            return false;
        }

        if (!inv.TryRemove(resourceDef, amount))
        {
            reason = $"Failed to remove {amount} {resourceDef.resourceName} for the offering.";
            return false;
        }

        inv.inventoryPanel?.Refresh();
        return true;
    }

    private bool TryValidatePopulationSacrifice(ReligionRitualDefinitionSO ritual, out string reason)
    {
        reason = null;

        if (ritual == null || ritual.sacrificeCount <= 0)
            return true;

        PlayerFamilySimulationManager sim = PlayerFamilySimulationManager.Instance;
        if (sim == null)
        {
            reason = "PlayerFamilySimulationManager is missing.";
            return false;
        }

        PlayersPopulationManager pop = PlayersPopulationManager.Instance;
        if (pop == null)
        {
            reason = "PlayersPopulationManager is missing.";
            return false;
        }

        CollectPopulationSacrificeCandidates(ritual, _tmpPopulationSacrificeCandidates);

        if (_tmpPopulationSacrificeCandidates.Count < ritual.sacrificeCount)
        {
            reason =
                $"Not enough eligible population for this sacrifice. " +
                $"Need {ritual.sacrificeCount}, found {_tmpPopulationSacrificeCandidates.Count}.";
            return false;
        }

        return true;
    }

    private bool TryConsumePopulationSacrifice(ReligionRitualDefinitionSO ritual, out string reason)
    {
        reason = null;

        if (!TryValidatePopulationSacrifice(ritual, out reason))
            return false;

        if (ritual == null || ritual.sacrificeCount <= 0)
            return true;

        PlayerFamilySimulationManager sim = PlayerFamilySimulationManager.Instance;
        if (sim == null)
        {
            reason = "PlayerFamilySimulationManager is missing.";
            return false;
        }

        PlayersPopulationManager pop = PlayersPopulationManager.Instance;
        if (pop == null)
        {
            reason = "PlayersPopulationManager is missing.";
            return false;
        }

        CollectPopulationSacrificeCandidates(ritual, _tmpPopulationSacrificeCandidates);

        if (_tmpPopulationSacrificeCandidates.Count < ritual.sacrificeCount)
        {
            reason =
                $"Not enough eligible population for this sacrifice. " +
                $"Need {ritual.sacrificeCount}, found {_tmpPopulationSacrificeCandidates.Count}.";
            return false;
        }

        ShuffleIndividualsInPlace(_tmpPopulationSacrificeCandidates);

        List<string> idsToKill = new List<string>(ritual.sacrificeCount);
        List<Guid> affectedGroups = new List<Guid>(ritual.sacrificeCount);

        for (int i = 0; i < ritual.sacrificeCount; i++)
        {
            Individual person = _tmpPopulationSacrificeCandidates[i];
            if (person == null || !person.IsAlive)
                continue;

            idsToKill.Add(person.Id);
            affectedGroups.Add(person.AggregatedGroupGuid);
        }

        if (idsToKill.Count != ritual.sacrificeCount)
        {
            reason = "Failed to resolve enough valid sacrifice targets.";
            return false;
        }

        if (!sim.TryKillIndividualsById(idsToKill, out int killedCount))
        {
            reason = "Failed to apply the sacrifice in the family simulation.";
            return false;
        }

        if (killedCount != ritual.sacrificeCount)
        {
            reason =
                $"Sacrifice killed {killedCount} population, but expected {ritual.sacrificeCount}.";
            return false;
        }

        pop.MarkUIDirty();
        return true;
    }

    private bool MatchesSacrificeSexFilter(Individual person, SpiritSacrificeSexFilter filter)
    {
        if (person == null)
            return false;

        switch (filter)
        {
            case SpiritSacrificeSexFilter.Male:
                return person.Gender == Gender.Male;

            case SpiritSacrificeSexFilter.Female:
                return person.Gender == Gender.Female;

            default:
                return true;
        }
    }

    private bool MatchesSacrificeAgeFilter(Individual person, SpiritSacrificeAgeFilter filter)
    {
        if (person == null)
            return false;

        switch (filter)
        {
            case SpiritSacrificeAgeFilter.Child:
                return person.AggregatedAgeGroup == AgeGroup.Child;

            case SpiritSacrificeAgeFilter.Teen:
                return person.AggregatedAgeGroup == AgeGroup.Teen;

            case SpiritSacrificeAgeFilter.Adult:
                return person.AggregatedAgeGroup == AgeGroup.Adult;

            case SpiritSacrificeAgeFilter.Elder:
                return person.AggregatedAgeGroup == AgeGroup.Elder;

            default:
                return true;
        }
    }

    private void CollectPopulationSacrificeCandidates(
        ReligionRitualDefinitionSO ritual,
        List<Individual> outList)
    {
        outList.Clear();

        if (ritual == null)
            return;

        PlayerFamilySimulationManager sim = PlayerFamilySimulationManager.Instance;
        PlayersPopulationManager pop = PlayersPopulationManager.Instance;

        if (sim == null || pop == null)
            return;

        IReadOnlyList<Individual> people = sim.GetIndividuals();
        if (people == null)
            return;

        for (int i = 0; i < people.Count; i++)
        {
            Individual person = people[i];
            if (person == null || !person.IsAlive)
                continue;

            if (!MatchesSacrificeSexFilter(person, ritual.sacrificeSexFilter))
                continue;

            if (!MatchesSacrificeAgeFilter(person, ritual.sacrificeAgeFilter))
                continue;

            // Don't let sacrifices rip active jobs/reservations apart.
            if (person.IsBusy)
                continue;

            if (pop.IsIndividualReservedAnywhere(person.Id))
                continue;

            // Safer default: don't sacrifice currently pregnant mothers.
            if (sim.IsIndividualCurrentlyPregnant(person.Id))
                continue;

            outList.Add(person);
        }
    }

    private static void ShuffleIndividualsInPlace(List<Individual> list)
    {
        if (list == null || list.Count <= 1)
            return;

        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            Individual tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
    }

    private PopulationGroup FindPopulationGroupByGuid(Guid groupId)
    {
        PlayersPopulationManager pop = PlayersPopulationManager.Instance;
        if (pop == null || pop.AllPopulations == null)
            return null;

        for (int i = 0; i < pop.AllPopulations.Count; i++)
        {
            PopulationGroup group = pop.AllPopulations[i];
            if (group != null && group.GroupID == groupId)
                return group;
        }

        return null;
    }

    public void GetKnownRitualsForSelectedSpirit(
    SpiritDefinitionSO selectedSpirit,
    List<ReligionRitualDefinitionSO> outRituals)
    {
        if (outRituals == null)
            return;

        outRituals.Clear();

        List<ReligionRitualDefinitionSO> allAvailable = new List<ReligionRitualDefinitionSO>();
        GetAvailableKnownRituals(allAvailable);

        // If the player has no selected/followed spirit yet,
        // only show summoning rituals.
        if (selectedSpirit == null)
        {
            for (int i = 0; i < allAvailable.Count; i++)
            {
                ReligionRitualDefinitionSO ritual = allAvailable[i];
                if (ritual != null && ritual.IsSummoningRitual)
                    outRituals.Add(ritual);
            }

            SortRitualList(outRituals);
            return;
        }

        for (int i = 0; i < allAvailable.Count; i++)
        {
            ReligionRitualDefinitionSO ritual = allAvailable[i];
            if (ritual == null)
                continue;

            // Always keep summoning visible and first.
            if (ritual.IsSummoningRitual)
            {
                outRituals.Add(ritual);
                continue;
            }

            if (ritual.spiritSelectionMode == RitualSpiritSelectionMode.SelectAtRuntime)
            {
                outRituals.Add(ritual);
                continue;
            }

            if (ritual.fixedSpirit == selectedSpirit)
                outRituals.Add(ritual);
        }

        SortRitualList(outRituals);
    }

    private void SortRitualList(List<ReligionRitualDefinitionSO> rituals)
    {
        if (rituals == null || rituals.Count <= 1)
            return;

        rituals.Sort((a, b) =>
        {
            if (a == null && b == null) return 0;
            if (a == null) return 1;
            if (b == null) return -1;

            int aPriority = a.IsSummoningRitual ? 0 : 1;
            int bPriority = b.IsSummoningRitual ? 0 : 1;

            int cmp = aPriority.CompareTo(bPriority);
            if (cmp != 0)
                return cmp;

            return string.Compare(a.displayName, b.displayName, StringComparison.OrdinalIgnoreCase);
        });
    }

    public SpiritDefinitionSO GetDefaultSelectedSpirit()
    {
        for (int i = 0; i < affiliatedSpirits.Count; i++)
        {
            SpiritDefinitionSO spirit = affiliatedSpirits[i];
            if (spirit != null)
                return spirit;
        }

        return null;
    }

    private void BeginRitualVisuals()
    {
        if (ritualTimerUI != null && HasActiveRitual)
        {
            ritualTimerUI.gameObject.SetActive(true);
            ritualTimerUI.Initialize(Mathf.Max(1, activeRitual.totalTurns));
            ritualTimerUI.UpdateTimer(Mathf.Max(0, activeRitual.turnsRemaining));
        }

        ApplyRitualLight(activeRitual != null ? activeRitual.ritual : null);
    }


    private void UpdateRitualTimerUI()
    {
        if (ritualTimerUI == null)
            return;

        if (!HasActiveRitual || activeRitual.ritual == null)
        {
            ritualTimerUI.gameObject.SetActive(false);
            return;
        }

        if (!ritualTimerUI.gameObject.activeSelf)
            ritualTimerUI.gameObject.SetActive(true);

        ritualTimerUI.UpdateTimer(Mathf.Max(0, activeRitual.turnsRemaining));
    }

    private void CompleteRitualVisuals()
    {
        if (ritualTimerUI != null)
            ritualTimerUI.gameObject.SetActive(false);

        ResetRitualLight();
    }


    public bool HasFreeSpiritSlot()
    {
        return affiliatedSpirits.Count < Mathf.Max(1, maxAffiliatedSpirits);
    }

    public ReligiousBuildingRuntimeSaveData CaptureRuntimeSaveData(string buildingSaveableID)
    {
        ReligiousBuildingRuntimeSaveData data = new ReligiousBuildingRuntimeSaveData
        {
            buildingSaveableID = buildingSaveableID
        };

        for (int i = 0; i < affiliatedSpirits.Count; i++)
        {
            SpiritDefinitionSO spirit = affiliatedSpirits[i];
            if (spirit != null && !string.IsNullOrWhiteSpace(spirit.spiritID))
                data.affiliatedSpiritIDs.Add(spirit.spiritID.Trim());
        }

        for (int i = 0; i < completedNonRepeatableRituals.Count; i++)
        {
            ReligionRitualDefinitionSO ritual = completedNonRepeatableRituals[i];
            if (ritual != null && !string.IsNullOrWhiteSpace(ritual.ritualID))
                data.completedNonRepeatableRitualIDs.Add(ritual.ritualID.Trim());
        }

        for (int i = 0; i < ritualCooldowns.Count; i++)
        {
            RitualCooldownState entry = ritualCooldowns[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.ritualID))
                continue;

            data.ritualCooldowns.Add(new ReligiousBuildingRitualCooldownSaveData
            {
                ritualID = entry.ritualID,
                readyOnTurn = entry.readyOnTurn
            });
        }

        if (activeRitual != null && activeRitual.ritual != null && !string.IsNullOrWhiteSpace(activeRitual.ritual.ritualID))
        {
            data.activeRitual = new ReligiousBuildingActiveRitualSaveData
            {
                ritualID = activeRitual.ritual.ritualID.Trim(),
                targetSpiritID = activeRitual.targetSpirit != null && !string.IsNullOrWhiteSpace(activeRitual.targetSpirit.spiritID)
                ? activeRitual.targetSpirit.spiritID.Trim()
                : null,
                totalTurns = activeRitual.totalTurns,
                turnsRemaining = activeRitual.turnsRemaining,
                startedOnTurn = activeRitual.startedOnTurn,
                workerReservationId = activeRitual.workerReservationId
            };
        }

        return data;
    }

    public void ApplyRuntimeSaveData(
        ReligiousBuildingRuntimeSaveData data,
        Func<string, SpiritDefinitionSO> resolveSpiritByID,
        Func<string, ReligionRitualDefinitionSO> resolveRitualByID)
    {
        affiliatedSpirits.Clear();
        completedNonRepeatableRituals.Clear();
        ritualCooldowns.Clear();

        if (HasActiveRitual)
            CancelActiveRitual(releaseWorkers: false);

        activeRitual = null;

        if (data == null)
        {
            NotifyChanged();
            return;
        }

        if (data.affiliatedSpiritIDs != null)
        {
            for (int i = 0; i < data.affiliatedSpiritIDs.Count; i++)
            {
                SpiritDefinitionSO spirit = resolveSpiritByID?.Invoke(data.affiliatedSpiritIDs[i]);
                if (spirit != null && !affiliatedSpirits.Contains(spirit))
                    affiliatedSpirits.Add(spirit);
            }
        }

        if (data.completedNonRepeatableRitualIDs != null)
        {
            for (int i = 0; i < data.completedNonRepeatableRitualIDs.Count; i++)
            {
                ReligionRitualDefinitionSO ritual = resolveRitualByID?.Invoke(data.completedNonRepeatableRitualIDs[i]);
                if (ritual != null && !completedNonRepeatableRituals.Contains(ritual))
                    completedNonRepeatableRituals.Add(ritual);
            }
        }

        if (data.ritualCooldowns != null)
        {
            for (int i = 0; i < data.ritualCooldowns.Count; i++)
            {
                ReligiousBuildingRitualCooldownSaveData saved = data.ritualCooldowns[i];
                if (saved == null || string.IsNullOrWhiteSpace(saved.ritualID))
                    continue;

                ritualCooldowns.Add(new RitualCooldownState
                {
                    ritualID = saved.ritualID.Trim(),
                    readyOnTurn = saved.readyOnTurn
                });
            }
        }

        if (data.activeRitual != null)
        {
            ReligionRitualDefinitionSO ritual = resolveRitualByID?.Invoke(data.activeRitual.ritualID);
            SpiritDefinitionSO targetSpirit = resolveSpiritByID?.Invoke(data.activeRitual.targetSpiritID);

            if (ritual != null)
            {
                activeRitual = new ActiveRitualRuntimeState
                {
                    ritual = ritual,
                    targetSpirit = targetSpirit,
                    totalTurns = Mathf.Max(1, data.activeRitual.totalTurns),
                    turnsRemaining = Mathf.Max(0, data.activeRitual.turnsRemaining),
                    startedOnTurn = data.activeRitual.startedOnTurn,
                    workerReservationId = data.activeRitual.workerReservationId
                };

                BeginRitualVisuals();
                UpdateRitualTimerUI();
            }
        }

        NotifyChanged();
    }

    private void MarkReligionDirty()
    {
        SaveSystem.MarkSectionDirty(SaveSectionKeys.WorldObjects);
    }

    private void HandleBuildingHealthChanged(int currentHealth, int maxHealth)
    {
        if (_buildingControl == null)
            return;

        if (_buildingControl.ActiveType != BuildingType.Religious)
        {
            _lastKnownHealthFraction = Mathf.Clamp01(currentHealth / (float)Mathf.Max(1, maxHealth));
            return;
        }

        if (_buildingStatus != null && _buildingStatus.CurrentState == BuildingState.Destroyed)
        {
            _lastKnownHealthFraction = 0f;
            return;
        }

        float currentFraction = Mathf.Clamp01(currentHealth / (float)Mathf.Max(1, maxHealth));
        float previousFraction = _lastKnownHealthFraction;

        if (currentFraction < previousFraction)
        {
            PlayerReligionManager.Instance?.NotifyReligiousBuildingHealthDropped(
                this,
                previousFraction,
                currentFraction);
        }

        _lastKnownHealthFraction = currentFraction;
    }
}
