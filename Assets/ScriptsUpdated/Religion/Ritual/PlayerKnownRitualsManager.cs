using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerKnownRitualsManager : MonoBehaviour
{
    public static PlayerKnownRitualsManager Instance { get; private set; }

    [Header("Database (optional)")]
    [Tooltip("Optional list of all ritual assets you want this manager to know about for knownByDefault bootstrapping.")]
    public List<ReligionRitualDefinitionSO> ritualDatabase = new List<ReligionRitualDefinitionSO>();

    [Header("Startup")]
    public List<ReligionRitualDefinitionSO> startingKnownRituals = new List<ReligionRitualDefinitionSO>();

    [Header("Runtime")]
    [SerializeField] private List<ReligionRitualDefinitionSO> knownRituals = new List<ReligionRitualDefinitionSO>();

    private readonly HashSet<string> _knownIds = new HashSet<string>(StringComparer.Ordinal);

    public IReadOnlyList<ReligionRitualDefinitionSO> KnownRituals => knownRituals;

    public event Action KnownRitualsChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        RebuildLookup();
    }

    private void Start()
    {
        EnsureStartingKnownRituals();
    }

    private void RebuildLookup()
    {
        _knownIds.Clear();

        for (int i = knownRituals.Count - 1; i >= 0; i--)
        {
            ReligionRitualDefinitionSO ritual = knownRituals[i];
            if (ritual == null || string.IsNullOrWhiteSpace(ritual.ritualID))
            {
                knownRituals.RemoveAt(i);
                continue;
            }

            if (!_knownIds.Add(ritual.ritualID))
                knownRituals.RemoveAt(i);
        }
    }

    private void EnsureStartingKnownRituals()
    {
        bool changed = false;

        for (int i = 0; i < ritualDatabase.Count; i++)
        {
            ReligionRitualDefinitionSO ritual = ritualDatabase[i];
            if (ritual == null || !ritual.knownByDefault)
                continue;

            changed |= LearnRitualInternal(ritual);
        }

        for (int i = 0; i < startingKnownRituals.Count; i++)
        {
            ReligionRitualDefinitionSO ritual = startingKnownRituals[i];
            if (ritual == null)
                continue;

            changed |= LearnRitualInternal(ritual);
        }

        if (changed)
            NotifyChanged();
    }

    private bool LearnRitualInternal(ReligionRitualDefinitionSO ritual)
    {
        if (ritual == null || string.IsNullOrWhiteSpace(ritual.ritualID))
            return false;

        if (_knownIds.Contains(ritual.ritualID))
            return false;

        _knownIds.Add(ritual.ritualID);
        knownRituals.Add(ritual);
        return true;
    }

    public bool IsKnown(ReligionRitualDefinitionSO ritual)
    {
        if (ritual == null || string.IsNullOrWhiteSpace(ritual.ritualID))
            return false;

        return _knownIds.Contains(ritual.ritualID);
    }

    public bool LearnRitual(ReligionRitualDefinitionSO ritual)
    {
        bool changed = LearnRitualInternal(ritual);
        if (changed)
            NotifyChanged();

        return changed;
    }

    public bool ForgetRitual(ReligionRitualDefinitionSO ritual)
    {
        if (ritual == null || string.IsNullOrWhiteSpace(ritual.ritualID))
            return false;

        if (!_knownIds.Remove(ritual.ritualID))
            return false;

        for (int i = knownRituals.Count - 1; i >= 0; i--)
        {
            if (knownRituals[i] == ritual)
                knownRituals.RemoveAt(i);
        }

        NotifyChanged();
        return true;
    }

    public void GetKnownRitualsForBeliefSystem(BeliefSystemType beliefSystem, List<ReligionRitualDefinitionSO> outList)
    {
        if (outList == null)
            return;

        outList.Clear();

        for (int i = 0; i < knownRituals.Count; i++)
        {
            ReligionRitualDefinitionSO ritual = knownRituals[i];
            if (ritual == null)
                continue;

            if (!ritual.MatchesBeliefSystem(beliefSystem))
                continue;

            outList.Add(ritual);
        }
    }

    public void GetKnownRitualsForSpirit(
        SpiritDefinitionSO spirit,
        BeliefSystemType beliefSystem,
        List<ReligionRitualDefinitionSO> outList,
        bool includeSelectableAtRuntime = true)
    {
        if (outList == null)
            return;

        outList.Clear();

        for (int i = 0; i < knownRituals.Count; i++)
        {
            ReligionRitualDefinitionSO ritual = knownRituals[i];
            if (ritual == null)
                continue;

            if (!ritual.MatchesBeliefSystem(beliefSystem))
                continue;

            if (ritual.spiritSelectionMode == RitualSpiritSelectionMode.SelectAtRuntime)
            {
                if (includeSelectableAtRuntime)
                    outList.Add(ritual);

                continue;
            }

            if (ritual.fixedSpirit == spirit)
                outList.Add(ritual);
        }
    }

    private void NotifyChanged()
    {
        KnownRitualsChanged?.Invoke();
    }

    private static Dictionary<string, ReligionRitualDefinitionSO> _ritualLookupById;

    private void MarkKnowledgeDirty()
    {
        SaveSystem.MarkSectionDirty(SaveSectionKeys.Knowledge);
    }

    public PlayerKnownRitualsSaveData SaveState()
    {
        PlayerKnownRitualsSaveData data = new PlayerKnownRitualsSaveData();

        foreach (string id in _knownIds)
        {
            if (!string.IsNullOrWhiteSpace(id))
                data.knownRitualIDs.Add(id);
        }

        return data;
    }

    public void LoadState(PlayerKnownRitualsSaveData data)
    {
        _knownIds.Clear();
        knownRituals.Clear();

        if (data == null || data.knownRitualIDs == null)
        {
            EnsureStartingKnownRituals();
            NotifyChanged();
            return;
        }

        for (int i = 0; i < data.knownRitualIDs.Count; i++)
        {
            string rawId = data.knownRitualIDs[i];
            string id = string.IsNullOrWhiteSpace(rawId) ? null : rawId.Trim();
            if (string.IsNullOrEmpty(id))
                continue;

            if (!_knownIds.Add(id))
                continue;

            ReligionRitualDefinitionSO ritual = ResolveRitualByID(id);
            if (ritual != null)
                knownRituals.Add(ritual);
        }

        NotifyChanged();
    }

    private static ReligionRitualDefinitionSO ResolveRitualByID(string ritualID)
    {
        if (string.IsNullOrWhiteSpace(ritualID))
            return null;

        string trimmedId = ritualID.Trim();

        if (_ritualLookupById == null)
        {
            _ritualLookupById = new Dictionary<string, ReligionRitualDefinitionSO>(StringComparer.Ordinal);
            ReligionRitualDefinitionSO[] allDefs = Resources.LoadAll<ReligionRitualDefinitionSO>(string.Empty);

            for (int i = 0; i < allDefs.Length; i++)
            {
                ReligionRitualDefinitionSO def = allDefs[i];
                if (def == null || string.IsNullOrWhiteSpace(def.ritualID))
                    continue;

                string id = def.ritualID.Trim();
                if (!_ritualLookupById.ContainsKey(id))
                    _ritualLookupById.Add(id, def);
            }
        }

        _ritualLookupById.TryGetValue(trimmedId, out ReligionRitualDefinitionSO result);
        return result;
    }
}