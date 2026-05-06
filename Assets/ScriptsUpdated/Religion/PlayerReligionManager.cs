using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerReligionManager : MonoBehaviour
{
    public static PlayerReligionManager Instance { get; private set; }

    [Header("Current Belief System")]
    public BeliefSystemType currentBeliefSystem = BeliefSystemType.Animism;

    [Header("Rules")]
    [Tooltip("If true, a spirit that conflicts with an already accepted spirit cannot be accepted.")]
    public bool preventConflictingSpiritAcceptance = true;

    public int likedSpiritCoexistenceBonus = 5;
    public int dislikedSpiritConflictPenalty = 15;

    [Header("Startup")]
    public List<SpiritDefinitionSO> startingAcceptedSpirits = new List<SpiritDefinitionSO>();

    [Header("Runtime")]
    [SerializeField] private List<SpiritRuntimeState> activeSpirits = new List<SpiritRuntimeState>();
    private readonly List<EnvironmentResourceNode> _tmpEligibleBarrenNodes = new List<EnvironmentResourceNode>(128);
    private readonly List<float> _tmpEligibleBarrenWeights = new List<float>(128);

    [Header("Sacred Animal Groups")]
    [SerializeField, Min(1)] private int sacredGroupRotationIntervalSeasons = 4;
    [SerializeField] private bool replaceSacredGroupImmediatelyOnRemoval = true;
    [SerializeField] private bool avoidSelectingSameGroupsOnRotation = true;
    [SerializeField] private int seasonsSinceSacredGroupRotation = 0;

    private readonly List<AnimalGroupState> _tmpLiveAnimalGroups = new List<AnimalGroupState>(256);
    private readonly List<AnimalGroupState> _tmpSacredCandidates = new List<AnimalGroupState>(64);
    private readonly HashSet<int> _tmpReservedSacredGroupIds = new HashSet<int>();

    private readonly List<SpiritSpoilageTabooRule> _tmpMatchingSpoilageTaboos = new List<SpiritSpoilageTabooRule>(8);
    private readonly List<SpiritLeftBehindGatheredLootTabooRule> _tmpMatchingLeftBehindLootTaboos = new List<SpiritLeftBehindGatheredLootTabooRule>(8);

    private readonly List<SpiritCombatRetreatTabooRule> _retreatTabooBuffer = new();

    private readonly List<SpiritLeftBehindUnitLootTabooRule> _tmpMatchingLeftBehindUnitLootTaboos =
        new List<SpiritLeftBehindUnitLootTabooRule>(8);

    private readonly List<SpiritReligiousBuildingHealthTabooRule> _tmpMatchingReligiousBuildingHealthTaboos =
        new List<SpiritReligiousBuildingHealthTabooRule>(8);

    public IReadOnlyList<SpiritRuntimeState> ActiveSpirits => activeSpirits;

    public event Action ReligionChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        EnsureStartingSpirits();
        RefreshSacredAnimalGroups(forceRotateAll: false, replaceMissingOnly: true);
    }

    private void OnEnable()
    {
        TurnSystem.SubscribeToEndOfTurn(HandleEndTurn);

        if (SeasonManager.Instance != null)
            SeasonManager.Instance.OnSeasonChanged += HandleSeasonChanged;
    }

    private void OnDisable()
    {
        TurnSystem.UnsubscribeFromEndOfTurn(HandleEndTurn);

        if (SeasonManager.Instance != null)
            SeasonManager.Instance.OnSeasonChanged -= HandleSeasonChanged;
    }

    private void HandleEndTurn()
    {
        ApplyEndTurnDecay();
        TryTriggerRandomBarrenNodeFromSpirits();
    }

    private void HandleSeasonChanged(SeasonDefinition _)
    {
        seasonsSinceSacredGroupRotation++;

        if (seasonsSinceSacredGroupRotation >= sacredGroupRotationIntervalSeasons)
        {
            seasonsSinceSacredGroupRotation = 0;
            RefreshSacredAnimalGroups(forceRotateAll: true, replaceMissingOnly: false);
        }
        else
        {
            RefreshSacredAnimalGroups(forceRotateAll: false, replaceMissingOnly: true);
        }
    }

    private void EnsureStartingSpirits()
    {
        if (activeSpirits.Count > 0)
            return;

        for (int i = 0; i < startingAcceptedSpirits.Count; i++)
        {
            SpiritDefinitionSO spirit = startingAcceptedSpirits[i];
            if (spirit == null)
                continue;

            if (spirit.beliefSystem != currentBeliefSystem)
                continue;

            SpiritRuntimeState state = GetOrCreateState(spirit);
            state.accepted = true;
            state.favor = spirit.ClampFavor(spirit.startingFavor + spirit.favorOnAcceptance);
        }

        NotifyReligionChanged();
    }

    public SpiritRuntimeState GetState(SpiritDefinitionSO spirit)
    {
        if (spirit == null)
            return null;

        for (int i = 0; i < activeSpirits.Count; i++)
        {
            SpiritRuntimeState state = activeSpirits[i];
            if (state != null && state.definition == spirit)
                return state;
        }

        return null;
    }

    public SpiritRuntimeState GetOrCreateState(SpiritDefinitionSO spirit)
    {
        if (spirit == null)
            return null;

        SpiritRuntimeState existing = GetState(spirit);
        if (existing != null)
            return existing;

        SpiritRuntimeState created = new SpiritRuntimeState(spirit);
        activeSpirits.Add(created);
        return created;
    }

    public bool IsAccepted(SpiritDefinitionSO spirit)
    {
        SpiritRuntimeState state = GetState(spirit);
        return state != null && state.accepted;
    }

    public bool TryAcceptSpirit(SpiritDefinitionSO spirit, out string reason)
    {
        reason = null;

        if (spirit == null)
        {
            reason = "Spirit is null.";
            return false;
        }

        if (spirit.beliefSystem != currentBeliefSystem)
        {
            reason = "Spirit does not belong to the current belief system.";
            return false;
        }

        PlayerKnownSpiritsManager knownMgr = PlayerKnownSpiritsManager.Instance;
        if (knownMgr != null && !knownMgr.IsKnown(spirit))
        {
            reason = "Spirit is not yet known.";
            return false;
        }

        for (int i = 0; i < activeSpirits.Count; i++)
        {
            SpiritRuntimeState other = activeSpirits[i];
            if (other == null || !other.accepted || other.definition == null || other.definition == spirit)
                continue;

            if (!spirit.ConflictsWith(other.definition))
                continue;

            if (preventConflictingSpiritAcceptance)
            {
                reason = "This spirit conflicts with an already accepted spirit.";
                return false;
            }
        }

        SpiritRuntimeState state = GetOrCreateState(spirit);
        state.accepted = true;
        state.favor = spirit.ClampFavor(Mathf.Max(state.favor, spirit.startingFavor) + spirit.favorOnAcceptance);

        ApplyRelationshipAdjustmentsOnAcceptance(spirit);
        RefreshSacredAnimalGroups(forceRotateAll: false, replaceMissingOnly: true);
        MarkKnowledgeDirty();
        NotifyReligionChanged();
        return true;
    }

    public void RemoveSpirit(SpiritDefinitionSO spirit)
    {
        SpiritRuntimeState state = GetState(spirit);
        if (state == null)
            return;

        state.accepted = false;
        state.ClearSacredAnimalGroups();
        MarkKnowledgeDirty();
        NotifyReligionChanged();
    }

    private void ApplyRelationshipAdjustmentsOnAcceptance(SpiritDefinitionSO acceptedSpirit)
    {
        if (acceptedSpirit == null)
            return;

        for (int i = 0; i < activeSpirits.Count; i++)
        {
            SpiritRuntimeState other = activeSpirits[i];
            if (other == null || !other.accepted || other.definition == null || other.definition == acceptedSpirit)
                continue;

            if (acceptedSpirit.Likes(other.definition))
            {
                other.favor = other.definition.ClampFavor(other.favor + likedSpiritCoexistenceBonus);
            }

            if (acceptedSpirit.ConflictsWith(other.definition))
            {
                other.favor = other.definition.ClampFavor(other.favor - dislikedSpiritConflictPenalty);

                SpiritRuntimeState acceptedState = GetState(acceptedSpirit);
                if (acceptedState != null)
                    acceptedState.favor = acceptedSpirit.ClampFavor(acceptedState.favor - dislikedSpiritConflictPenalty);
            }
        }
    }

    public void AddFavor(SpiritDefinitionSO spirit, int amount)
    {
        SpiritRuntimeState state = GetOrCreateState(spirit);
        if (state == null || state.definition == null)
            return;

        state.favor = state.definition.ClampFavor(state.favor + amount);
        MarkKnowledgeDirty();
        NotifyReligionChanged();
    }

    public SpiritMoodState GetMood(SpiritDefinitionSO spirit)
    {
        SpiritRuntimeState state = GetState(spirit);
        if (state == null || state.definition == null)
            return SpiritMoodState.Neutral;

        return state.definition.GetMoodForFavor(state.favor);
    }

    public void ApplyEndTurnDecay()
    {
        bool changed = false;

        for (int i = 0; i < activeSpirits.Count; i++)
        {
            SpiritRuntimeState state = activeSpirits[i];
            if (state == null || !state.accepted || state.definition == null)
                continue;

            int decay = Mathf.Max(0, state.definition.favorDecayPerTurn);
            if (decay <= 0)
                continue;

            int next = state.definition.ClampFavor(state.favor - decay);
            if (next != state.favor)
            {
                state.favor = next;
                changed = true;
            }
        }

        if (changed)
            NotifyReligionChanged();
        MarkKnowledgeDirty();
    }

    public bool TryOfferResource(
        SpiritDefinitionSO spirit,
        ScriptableObject resourceDefinition,
        int amount,
        int currentTurn,
        out SpiritResourceOfferingOption matchedOffering)
    {
        matchedOffering = null;

        if (spirit == null || resourceDefinition == null || amount <= 0)
            return false;

        if (!IsAccepted(spirit))
            return false;

        if (!spirit.TryGetMatchingResourceOffering(resourceDefinition, amount, out matchedOffering))
            return false;

        SpiritRuntimeState state = GetState(spirit);
        if (state == null)
            return false;

        state.totalOfferingsGiven++;
        state.lastOfferingTurn = currentTurn;
        state.favor = spirit.ClampFavor(state.favor + matchedOffering.favorChange);

        NotifyReligionChanged();
        return true;
    }

    public bool TryOfferPopulationSacrifice(
        SpiritDefinitionSO spirit,
        SpiritSacrificeSexFilter sex,
        SpiritSacrificeAgeFilter age,
        int count,
        int currentTurn,
        out SpiritPopulationSacrificeOption matchedOffering)
    {
        matchedOffering = null;

        if (spirit == null || count <= 0)
            return false;

        if (!IsAccepted(spirit))
            return false;

        if (!spirit.TryGetMatchingPopulationSacrifice(sex, age, count, out matchedOffering))
            return false;

        SpiritRuntimeState state = GetState(spirit);
        if (state == null)
            return false;

        state.totalOfferingsGiven++;
        state.lastOfferingTurn = currentTurn;
        state.favor = spirit.ClampFavor(state.favor + matchedOffering.favorChange);

        NotifyReligionChanged();
        return true;
    }

    public float GetAdditiveSum(SpiritEffectType effectType)
    {
        float total = 0f;

        for (int i = 0; i < activeSpirits.Count; i++)
        {
            SpiritRuntimeState state = activeSpirits[i];
            if (state == null || !state.accepted || state.definition == null)
                continue;

            SpiritMoodState mood = state.definition.GetMoodForFavor(state.favor);
            List<SpiritEffectEntry> effects = state.definition.effects;

            for (int j = 0; j < effects.Count; j++)
            {
                SpiritEffectEntry entry = effects[j];
                if (entry == null)
                    continue;

                if (entry.effectType != effectType)
                    continue;

                if (entry.modifierMode != SpiritModifierMode.Additive)
                    continue;

                total += entry.GetValue(mood);
            }
        }

        return total;
    }

    public float GetMultiplierProduct(SpiritEffectType effectType)
    {
        float total = 1f;

        for (int i = 0; i < activeSpirits.Count; i++)
        {
            SpiritRuntimeState state = activeSpirits[i];
            if (state == null || !state.accepted || state.definition == null)
                continue;

            SpiritMoodState mood = state.definition.GetMoodForFavor(state.favor);
            List<SpiritEffectEntry> effects = state.definition.effects;

            for (int j = 0; j < effects.Count; j++)
            {
                SpiritEffectEntry entry = effects[j];
                if (entry == null)
                    continue;

                if (entry.effectType != effectType)
                    continue;

                if (entry.modifierMode != SpiritModifierMode.Multiplier)
                    continue;

                total *= Mathf.Max(0f, entry.GetValue(mood));
            }
        }

        return total;
    }

    public void SwitchBeliefSystem(BeliefSystemType nextSystem)
    {
        if (currentBeliefSystem == nextSystem)
            return;

        currentBeliefSystem = nextSystem;

        for (int i = 0; i < activeSpirits.Count; i++)
        {
            SpiritRuntimeState state = activeSpirits[i];
            if (state == null || state.definition == null)
                continue;

            if (state.definition.beliefSystem != currentBeliefSystem)
            {
                state.accepted = false;
                state.ClearSacredAnimalGroups();
            }
        }

        NotifyReligionChanged();
        MarkKnowledgeDirty();
    }

    private void NotifyReligionChanged()
    {
        ReligionChanged?.Invoke();
    }

    private void TryTriggerRandomBarrenNodeFromSpirits()
    {
        float barrenChance = Mathf.Clamp01(GetAdditiveSum(SpiritEffectType.TileBarrenChanceAdd));

        if (barrenChance <= 0f)
            return;

        if (UnityEngine.Random.value > barrenChance)
            return;

        PlayerDiscoveryManager discovery = PlayerDiscoveryManager.Instance;
        if (discovery == null)
            return;

        IReadOnlyList<EnvironmentControl> discovered = discovery.GetDiscovered();
        if (discovered == null || discovered.Count == 0)
            return;

        _tmpEligibleBarrenNodes.Clear();
        _tmpEligibleBarrenWeights.Clear();

        for (int i = 0; i < discovered.Count; i++)
        {
            EnvironmentControl env = discovered[i];
            if (env == null)
                continue;

            if (!env.IsDiscovered)
                continue;

            EnvironmentResourceNode node = env.GetComponent<EnvironmentResourceNode>();
            if (node == null)
                node = env.GetComponentInChildren<EnvironmentResourceNode>(true);

            if (node == null)
                continue;

            if (node.IsBarren)
                continue;

            float weight = GetBarrenWeightForNode(env);
            if (weight <= 0f)
                continue;

            _tmpEligibleBarrenNodes.Add(node);
            _tmpEligibleBarrenWeights.Add(weight);
        }

        if (_tmpEligibleBarrenNodes.Count == 0)
            return;

        EnvironmentResourceNode chosen = PickWeightedBarrenNode();
        if (chosen == null)
            return;

        chosen.StartBarren();
    }

    private EnvironmentResourceNode PickWeightedBarrenNode()
    {
        if (_tmpEligibleBarrenNodes.Count == 0)
            return null;

        float totalWeight = 0f;
        for (int i = 0; i < _tmpEligibleBarrenWeights.Count; i++)
            totalWeight += Mathf.Max(0f, _tmpEligibleBarrenWeights[i]);

        if (totalWeight <= 0f)
        {
            int fallbackIndex = UnityEngine.Random.Range(0, _tmpEligibleBarrenNodes.Count);
            return _tmpEligibleBarrenNodes[fallbackIndex];
        }

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        float running = 0f;

        for (int i = 0; i < _tmpEligibleBarrenNodes.Count; i++)
        {
            running += Mathf.Max(0f, _tmpEligibleBarrenWeights[i]);
            if (roll <= running)
                return _tmpEligibleBarrenNodes[i];
        }

        return _tmpEligibleBarrenNodes[_tmpEligibleBarrenNodes.Count - 1];
    }

    private float GetBarrenWeightForNode(EnvironmentControl env)
    {
        if (env == null)
            return 0f;

        float weight = 1f;

        for (int i = 0; i < activeSpirits.Count; i++)
        {
            SpiritRuntimeState state = activeSpirits[i];
            if (!SpiritCurrentlyPushesBarren(state))
                continue;

            SpiritDefinitionSO spirit = state.definition;
            if (spirit == null || spirit.barrenPreferences == null || spirit.barrenPreferences.Count == 0)
                continue;

            for (int j = 0; j < spirit.barrenPreferences.Count; j++)
            {
                SpiritBarrenPreference pref = spirit.barrenPreferences[j];
                if (pref == null)
                    continue;

                if (!pref.Matches(env))
                    continue;

                weight += Mathf.Max(0f, pref.flatWeightBonus);
                weight *= Mathf.Max(0.01f, pref.weightMultiplier);
            }
        }

        return Mathf.Max(0.01f, weight);
    }

    private bool SpiritCurrentlyPushesBarren(SpiritRuntimeState state)
    {
        if (state == null || !state.accepted || state.definition == null)
            return false;

        SpiritDefinitionSO spirit = state.definition;
        SpiritMoodState mood = spirit.GetMoodForFavor(state.favor);

        if (spirit.effects == null || spirit.effects.Count == 0)
            return false;

        float total = 0f;

        for (int i = 0; i < spirit.effects.Count; i++)
        {
            SpiritEffectEntry entry = spirit.effects[i];
            if (entry == null)
                continue;

            if (entry.effectType != SpiritEffectType.TileBarrenChanceAdd)
                continue;

            if (entry.modifierMode != SpiritModifierMode.Additive)
                continue;

            total += entry.GetValue(mood);
        }

        return total > 0f;
    }

    public void TryFillMissingSacredAnimalGroups()
    {
        RefreshSacredAnimalGroups(forceRotateAll: false, replaceMissingOnly: true);
    }

    public void NotifyAnimalGroupRemoved(int groupId)
    {
        if (groupId <= 0)
            return;

        bool changed = false;

        for (int i = 0; i < activeSpirits.Count; i++)
        {
            SpiritRuntimeState state = activeSpirits[i];
            if (state == null)
                continue;

            if (state.RemoveSacredAnimalGroup(groupId))
                changed = true;
        }

        if (!changed)
            return;

        if (replaceSacredGroupImmediatelyOnRemoval)
            RefreshSacredAnimalGroups(forceRotateAll: false, replaceMissingOnly: true);
        else
            NotifyReligionChanged();
    }

    public bool IsAnimalGroupSacred(int groupId)
    {
        if (groupId <= 0)
            return false;

        for (int i = 0; i < activeSpirits.Count; i++)
        {
            SpiritRuntimeState state = activeSpirits[i];
            if (state == null || !state.accepted || state.definition == null)
                continue;

            if (state.ContainsSacredAnimalGroup(groupId))
                return true;
        }

        return false;
    }

    public bool TryGetSacredAnimalRuleForGroup(int groupId, AnimalDefinition animalDefinition, out SacredAnimalRule bestRule)
    {
        bestRule = null;

        if (groupId <= 0 || animalDefinition == null)
            return false;

        int bestScore = int.MinValue;

        for (int i = 0; i < activeSpirits.Count; i++)
        {
            SpiritRuntimeState state = activeSpirits[i];
            if (state == null || !state.accepted || state.definition == null)
                continue;

            if (!state.ContainsSacredAnimalGroup(groupId))
                continue;

            SacredAnimalRule rule;
            if (!state.definition.TryGetSacredAnimalRule(animalDefinition, out rule))
                continue;

            int score = state.favor;
            if (score > bestScore)
            {
                bestScore = score;
                bestRule = rule;
            }
        }

        return bestRule != null;
    }

    public bool TryGetSacredAnimalMarkerColor(int groupId, AnimalDefinition animalDefinition, out Color color)
    {
        color = Color.white;

        SacredAnimalRule rule;
        if (!TryGetSacredAnimalRuleForGroup(groupId, animalDefinition, out rule))
            return false;

        if (rule == null || !rule.overrideMarkerColor)
            return false;

        color = rule.markerColor;
        return true;
    }

    public void NotifySacredAnimalAttacked(int groupId, AnimalDefinition animalDefinition)
    {
        if (groupId <= 0 || animalDefinition == null)
            return;

        bool changed = false;

        for (int i = 0; i < activeSpirits.Count; i++)
        {
            SpiritRuntimeState state = activeSpirits[i];
            if (state == null || !state.accepted || state.definition == null)
                continue;

            if (!state.ContainsSacredAnimalGroup(groupId))
                continue;

            SacredAnimalRule rule;
            if (!state.definition.TryGetSacredAnimalRule(animalDefinition, out rule))
                continue;

            int next = state.definition.ClampFavor(state.favor - Mathf.Abs(rule.favorPenaltyOnAttack));
            if (next != state.favor)
            {
                state.favor = next;
                changed = true;
            }
        }

        if (changed)
            NotifyReligionChanged();
    }

    public void NotifySacredAnimalKilled(int groupId, AnimalDefinition animalDefinition)
    {
        if (groupId <= 0 || animalDefinition == null)
            return;

        bool changed = false;
        bool removedGroup = false;

        for (int i = 0; i < activeSpirits.Count; i++)
        {
            SpiritRuntimeState state = activeSpirits[i];
            if (state == null || !state.accepted || state.definition == null)
                continue;

            if (!state.ContainsSacredAnimalGroup(groupId))
                continue;

            SacredAnimalRule rule;
            if (!state.definition.TryGetSacredAnimalRule(animalDefinition, out rule))
                continue;

            int next = state.definition.ClampFavor(state.favor - Mathf.Abs(rule.favorPenaltyOnKill));
            if (next != state.favor)
            {
                state.favor = next;
                changed = true;
            }

            if (state.RemoveSacredAnimalGroup(groupId))
                removedGroup = true;
        }

        if (removedGroup && replaceSacredGroupImmediatelyOnRemoval)
            RefreshSacredAnimalGroups(forceRotateAll: false, replaceMissingOnly: true);
        else if (changed || removedGroup)
            NotifyReligionChanged();
        MarkKnowledgeDirty();
    }

    public void RefreshSacredAnimalGroups(bool forceRotateAll, bool replaceMissingOnly)
    {
        GetLiveAnimalGroups(_tmpLiveAnimalGroups);
        _tmpReservedSacredGroupIds.Clear();

        bool changed = false;

        for (int i = 0; i < activeSpirits.Count; i++)
        {
            SpiritRuntimeState state = activeSpirits[i];
            if (!StateCanHaveSacredGroups(state))
            {
                if (state != null && state.HasSacredAnimalGroups)
                {
                    state.ClearSacredAnimalGroups();
                    changed = true;
                }

                continue;
            }

            state.EnsureSacredGroupList();

            int desiredCount = GetDesiredSacredGroupCount(state);
            List<int> oldIds = new List<int>(state.currentSacredAnimalGroupIds);
            List<int> nextIds = new List<int>(desiredCount);

            if (!forceRotateAll)
            {
                for (int j = 0; j < oldIds.Count; j++)
                {
                    int groupId = oldIds[j];
                    if (groupId <= 0)
                        continue;

                    if (_tmpReservedSacredGroupIds.Contains(groupId))
                        continue;

                    if (!IsSacredGroupValidForState(state, groupId, _tmpLiveAnimalGroups))
                        continue;

                    nextIds.Add(groupId);
                    _tmpReservedSacredGroupIds.Add(groupId);

                    if (nextIds.Count >= desiredCount)
                        break;
                }
            }

            while (nextIds.Count < desiredCount)
            {
                int chosenId;
                bool picked = TryPickSacredGroupForState(
                    state,
                    _tmpLiveAnimalGroups,
                    _tmpReservedSacredGroupIds,
                    forceRotateAll && avoidSelectingSameGroupsOnRotation ? oldIds : null,
                    out chosenId);

                if (!picked)
                    break;

                nextIds.Add(chosenId);
                _tmpReservedSacredGroupIds.Add(chosenId);
            }

            if (!SacredGroupListsEqual(state.currentSacredAnimalGroupIds, nextIds))
            {
                state.currentSacredAnimalGroupIds.Clear();
                state.currentSacredAnimalGroupIds.AddRange(nextIds);
                changed = true;
            }
        }

        if (changed)
            NotifyReligionChanged();
        MarkKnowledgeDirty();
    }

    private bool StateCanHaveSacredGroups(SpiritRuntimeState state)
    {
        return state != null &&
               state.accepted &&
               state.definition != null &&
               state.definition.sacredAnimals != null &&
               state.definition.sacredAnimals.Count > 0 &&
               GetDesiredSacredGroupCount(state) > 0;
    }

    private int GetDesiredSacredGroupCount(SpiritRuntimeState state)
    {
        if (state == null || state.definition == null)
            return 0;

        return Mathf.Max(1, state.definition.activeSacredGroupCount);
    }

    private bool IsSacredGroupValidForState(
        SpiritRuntimeState state,
        int groupId,
        List<AnimalGroupState> liveGroups)
    {
        if (state == null || state.definition == null || groupId <= 0)
            return false;

        for (int i = 0; i < liveGroups.Count; i++)
        {
            AnimalGroupState group = liveGroups[i];
            if (group == null || group.id != groupId)
                continue;

            if (group.species == null)
                return false;

            SacredAnimalRule rule;
            return state.definition.TryGetSacredAnimalRule(group.species, out rule);
        }

        return false;
    }

    private bool TryPickSacredGroupForState(
        SpiritRuntimeState state,
        List<AnimalGroupState> liveGroups,
        HashSet<int> reservedGroupIds,
        List<int> avoidGroupIds,
        out int chosenGroupId)
    {
        chosenGroupId = -1;
        _tmpSacredCandidates.Clear();

        for (int i = 0; i < liveGroups.Count; i++)
        {
            AnimalGroupState group = liveGroups[i];
            if (!CanGroupBeChosenForSacredState(state, group, reservedGroupIds, avoidGroupIds))
                continue;

            _tmpSacredCandidates.Add(group);
        }

        if (_tmpSacredCandidates.Count == 0 && avoidGroupIds != null && avoidGroupIds.Count > 0)
        {
            for (int i = 0; i < liveGroups.Count; i++)
            {
                AnimalGroupState group = liveGroups[i];
                if (!CanGroupBeChosenForSacredState(state, group, reservedGroupIds, null))
                    continue;

                _tmpSacredCandidates.Add(group);
            }
        }

        if (_tmpSacredCandidates.Count == 0)
            return false;

        AnimalGroupState chosen = _tmpSacredCandidates[UnityEngine.Random.Range(0, _tmpSacredCandidates.Count)];
        chosenGroupId = chosen.id;
        return chosenGroupId > 0;
    }

    private bool CanGroupBeChosenForSacredState(
        SpiritRuntimeState state,
        AnimalGroupState group,
        HashSet<int> reservedGroupIds,
        List<int> avoidGroupIds)
    {
        if (state == null || state.definition == null || group == null || group.id <= 0 || group.species == null)
            return false;

        if (reservedGroupIds != null && reservedGroupIds.Contains(group.id))
            return false;

        if (avoidGroupIds != null && avoidGroupIds.Contains(group.id))
            return false;

        SacredAnimalRule rule;
        return state.definition.TryGetSacredAnimalRule(group.species, out rule);
    }

    private bool SacredGroupListsEqual(List<int> a, List<int> b)
    {
        if (ReferenceEquals(a, b))
            return true;

        if (a == null || b == null)
            return false;

        if (a.Count != b.Count)
            return false;

        for (int i = 0; i < a.Count; i++)
        {
            if (a[i] != b[i])
                return false;
        }

        return true;
    }

    private void GetLiveAnimalGroups(List<AnimalGroupState> results)
    {
        results.Clear();

        AnimalSimulation simulation = AnimalSimulationAccess.Current;

        if (simulation == null)
        {
            AnimalSimulationController controller = FindObjectOfType<AnimalSimulationController>();
            if (controller != null)
                simulation = controller.Simulation;
        }

        if (simulation == null)
            return;

        simulation.GetAllGroupsNonAlloc(results);
    }

    private void MarkKnowledgeDirty()
    {
        SaveSystem.MarkSectionDirty(SaveSectionKeys.Knowledge);
    }

    public PlayerReligionSaveData SaveState()
    {
        PlayerReligionSaveData data = new PlayerReligionSaveData
        {
            currentBeliefSystem = currentBeliefSystem,
            seasonsSinceSacredGroupRotation = seasonsSinceSacredGroupRotation
        };

        for (int i = 0; i < activeSpirits.Count; i++)
        {
            SpiritRuntimeState state = activeSpirits[i];
            if (state == null || state.definition == null || string.IsNullOrWhiteSpace(state.definition.spiritID))
                continue;

            data.activeSpirits.Add(new SpiritRuntimeStateSaveData
            {
                spiritID = state.definition.spiritID.Trim(),
                accepted = state.accepted,
                favor = state.favor,
                totalOfferingsGiven = state.totalOfferingsGiven,
                lastOfferingTurn = state.lastOfferingTurn,
                currentSacredAnimalGroupIds = state.currentSacredAnimalGroupIds != null
                    ? new List<int>(state.currentSacredAnimalGroupIds)
                    : new List<int>()
            });
        }

        return data;
    }

    public void LoadState(PlayerReligionSaveData data)
    {
        activeSpirits.Clear();

        if (data == null)
        {
            EnsureStartingSpirits();
            RefreshSacredAnimalGroups(forceRotateAll: false, replaceMissingOnly: true);
            NotifyReligionChanged();
            return;
        }

        currentBeliefSystem = data.currentBeliefSystem;
        seasonsSinceSacredGroupRotation = Mathf.Max(0, data.seasonsSinceSacredGroupRotation);

        if (data.activeSpirits != null)
        {
            for (int i = 0; i < data.activeSpirits.Count; i++)
            {
                SpiritRuntimeStateSaveData saved = data.activeSpirits[i];
                if (saved == null || string.IsNullOrWhiteSpace(saved.spiritID))
                    continue;

                SpiritDefinitionSO spirit = ResolveSpiritByID(saved.spiritID);
                if (spirit == null)
                    continue;

                SpiritRuntimeState state = new SpiritRuntimeState(spirit)
                {
                    accepted = saved.accepted,
                    favor = spirit.ClampFavor(saved.favor),
                    totalOfferingsGiven = Mathf.Max(0, saved.totalOfferingsGiven),
                    lastOfferingTurn = saved.lastOfferingTurn,
                    currentSacredAnimalGroupIds = saved.currentSacredAnimalGroupIds != null
                        ? new List<int>(saved.currentSacredAnimalGroupIds)
                        : new List<int>()
                };

                activeSpirits.Add(state);
            }
        }

        RefreshSacredAnimalGroups(forceRotateAll: false, replaceMissingOnly: true);
        NotifyReligionChanged();
    }

    private SpiritDefinitionSO ResolveSpiritByID(string spiritID)
    {
        if (string.IsNullOrWhiteSpace(spiritID))
            return null;

        string trimmed = spiritID.Trim();

        SpiritDefinitionSO[] allDefs = Resources.LoadAll<SpiritDefinitionSO>(string.Empty);
        for (int i = 0; i < allDefs.Length; i++)
        {
            SpiritDefinitionSO def = allDefs[i];
            if (def == null || string.IsNullOrWhiteSpace(def.spiritID))
                continue;

            if (string.Equals(def.spiritID.Trim(), trimmed, StringComparison.Ordinal))
                return def;
        }

        return null;
    }

    public void NotifyResourcesSpoiled(Dictionary<ResourceDefinition, int> spoiledAmounts)
    {
        if (spoiledAmounts == null || spoiledAmounts.Count == 0)
            return;

        bool changed = false;

        foreach (KeyValuePair<ResourceDefinition, int> pair in spoiledAmounts)
        {
            ResourceDefinition spoiledDef = pair.Key;
            int spoiledAmount = pair.Value;

            if (spoiledDef == null || spoiledAmount <= 0)
                continue;

            for (int i = 0; i < activeSpirits.Count; i++)
            {
                SpiritRuntimeState state = activeSpirits[i];
                if (state == null || !state.accepted || state.definition == null)
                    continue;

                state.definition.GetMatchingSpoilageTaboos(spoiledDef, spoiledAmount, _tmpMatchingSpoilageTaboos);

                if (_tmpMatchingSpoilageTaboos.Count == 0)
                    continue;

                int totalPenalty = 0;

                for (int j = 0; j < _tmpMatchingSpoilageTaboos.Count; j++)
                {
                    SpiritSpoilageTabooRule taboo = _tmpMatchingSpoilageTaboos[j];
                    if (taboo == null)
                        continue;

                    totalPenalty += taboo.GetPenalty(spoiledAmount);
                }

                if (totalPenalty <= 0)
                    continue;

                int nextFavor = state.definition.ClampFavor(state.favor - totalPenalty);
                if (nextFavor != state.favor)
                {
                    state.favor = nextFavor;
                    changed = true;
                }
            }
        }

        if (changed)
        {
            MarkKnowledgeDirty();
            NotifyReligionChanged();
        }
    }

    public void NotifyGatheredLootLeftBehind(List<(ResourceDefinition def, int amount)> leftBehindLoot)
    {
        if (leftBehindLoot == null || leftBehindLoot.Count == 0)
            return;

        bool changed = false;

        for (int k = 0; k < leftBehindLoot.Count; k++)
        {
            ResourceDefinition leftDef = leftBehindLoot[k].def;
            int leftAmount = leftBehindLoot[k].amount;

            if (leftDef == null || leftAmount <= 0)
                continue;

            for (int i = 0; i < activeSpirits.Count; i++)
            {
                SpiritRuntimeState state = activeSpirits[i];
                if (state == null || !state.accepted || state.definition == null)
                    continue;

                state.definition.GetMatchingLeftBehindGatheredLootTaboos(
                    leftDef,
                    leftAmount,
                    _tmpMatchingLeftBehindLootTaboos);

                if (_tmpMatchingLeftBehindLootTaboos.Count == 0)
                    continue;

                int totalPenalty = 0;

                for (int j = 0; j < _tmpMatchingLeftBehindLootTaboos.Count; j++)
                {
                    SpiritLeftBehindGatheredLootTabooRule taboo = _tmpMatchingLeftBehindLootTaboos[j];
                    if (taboo == null)
                        continue;

                    totalPenalty += taboo.GetPenalty(leftAmount);
                }

                if (totalPenalty <= 0)
                    continue;

                int nextFavor = state.definition.ClampFavor(state.favor - totalPenalty);
                if (nextFavor != state.favor)
                {
                    state.favor = nextFavor;
                    changed = true;
                }
            }
        }

        if (changed)
        {
            MarkKnowledgeDirty();
            NotifyReligionChanged();
        }
    }

    public void NotifyUnitRetreatedFromCombat(
        TileUnitGroupData group,
        bool againstUnit,
        bool againstAnimal,
        bool afterRetaliation,
        bool wasSurround)
    {
        if (group == null || activeSpirits == null || activeSpirits.Count == 0)
            return;

        for (int i = 0; i < activeSpirits.Count; i++)
        {
            SpiritRuntimeState runtime = activeSpirits[i];
            if (runtime == null || runtime.definition == null || !runtime.accepted)
                continue;

            var spirit = runtime.definition;
            spirit.GetMatchingCombatRetreatTaboos(
                againstUnit,
                againstAnimal,
                afterRetaliation,
                wasSurround,
                _retreatTabooBuffer);

            for (int j = 0; j < _retreatTabooBuffer.Count; j++)
            {
                var taboo = _retreatTabooBuffer[j];
                if (taboo == null)
                    continue;

                runtime.favor = spirit.ClampFavor(runtime.favor - taboo.GetPenalty());

                Debug.Log(
                    $"[Religion] Retreat taboo broken: spirit='{spirit.displayName}', taboo='{taboo.displayName}', " +
                    $"penalty={taboo.GetPenalty()}, newFavor={runtime.favor}");
            }
        }
    }

    public void NotifyUnitLootLeftBehind(List<(ResourceDefinition def, int amount)> leftBehindLoot)
    {
        if (leftBehindLoot == null || leftBehindLoot.Count == 0)
            return;

        bool changed = false;

        for (int k = 0; k < leftBehindLoot.Count; k++)
        {
            ResourceDefinition leftDef = leftBehindLoot[k].def;
            int leftAmount = leftBehindLoot[k].amount;

            if (leftDef == null || leftAmount <= 0)
                continue;

            for (int i = 0; i < activeSpirits.Count; i++)
            {
                SpiritRuntimeState state = activeSpirits[i];
                if (state == null || !state.accepted || state.definition == null)
                    continue;

                state.definition.GetMatchingLeftBehindUnitLootTaboos(
                    leftDef,
                    leftAmount,
                    _tmpMatchingLeftBehindUnitLootTaboos);

                if (_tmpMatchingLeftBehindUnitLootTaboos.Count == 0)
                    continue;

                int totalPenalty = 0;

                for (int j = 0; j < _tmpMatchingLeftBehindUnitLootTaboos.Count; j++)
                {
                    SpiritLeftBehindUnitLootTabooRule taboo = _tmpMatchingLeftBehindUnitLootTaboos[j];
                    if (taboo == null)
                        continue;

                    totalPenalty += taboo.GetPenalty(leftAmount);
                }

                if (totalPenalty <= 0)
                    continue;

                int nextFavor = state.definition.ClampFavor(state.favor - totalPenalty);
                if (nextFavor != state.favor)
                {
                    state.favor = nextFavor;
                    changed = true;
                }
            }
        }

        if (changed)
        {
            MarkKnowledgeDirty();
            NotifyReligionChanged();
        }
    }

    public void NotifyReligiousBuildingHealthDropped(
        ReligiousBuildingControl building,
        float previousFraction,
        float currentFraction)
    {
        if (building == null)
            return;

        bool changed = false;
        bool isReligiousType = true;

        IReadOnlyList<SpiritDefinitionSO> affiliated = building.AffiliatedSpirits;

        for (int i = 0; i < activeSpirits.Count; i++)
        {
            SpiritRuntimeState state = activeSpirits[i];
            if (state == null || !state.accepted || state.definition == null)
                continue;

            bool isAffiliatedAtBuilding = false;
            if (affiliated != null)
            {
                for (int j = 0; j < affiliated.Count; j++)
                {
                    if (affiliated[j] == state.definition)
                    {
                        isAffiliatedAtBuilding = true;
                        break;
                    }
                }
            }

            state.definition.GetMatchingReligiousBuildingHealthTaboos(
                previousFraction,
                currentFraction,
                isReligiousType,
                isAffiliatedAtBuilding,
                _tmpMatchingReligiousBuildingHealthTaboos);

            if (_tmpMatchingReligiousBuildingHealthTaboos.Count == 0)
                continue;

            int totalPenalty = 0;
            for (int j = 0; j < _tmpMatchingReligiousBuildingHealthTaboos.Count; j++)
            {
                SpiritReligiousBuildingHealthTabooRule taboo = _tmpMatchingReligiousBuildingHealthTaboos[j];
                if (taboo == null)
                    continue;

                totalPenalty += taboo.GetPenalty();
            }

            if (totalPenalty <= 0)
                continue;

            int nextFavor = state.definition.ClampFavor(state.favor - totalPenalty);
            if (nextFavor != state.favor)
            {
                state.favor = nextFavor;
                changed = true;
            }
        }

        if (changed)
        {
            MarkKnowledgeDirty();
            NotifyReligionChanged();
        }
    }
}