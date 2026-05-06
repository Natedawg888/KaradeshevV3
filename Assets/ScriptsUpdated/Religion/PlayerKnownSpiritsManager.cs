using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerKnownSpiritsManager : MonoBehaviour
{
    public static PlayerKnownSpiritsManager Instance { get; private set; }

    [Header("Starting Known Spirits")]
    [Tooltip("Drag the SpiritDefinitionSO assets the player knows from game start.")]
    [SerializeField] private List<SpiritDefinitionSO> startingKnown = new();

    private readonly HashSet<SpiritDefinitionSO> _known = new();
    private readonly HashSet<string> _knownIds = new(StringComparer.Ordinal);

    public event Action OnKnownChanged;

    private static Dictionary<string, SpiritDefinitionSO> _spiritLookupById;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        ResetToStarting();
    }

    private void MarkKnowledgeDirty()
    {
        SaveSystem.MarkSectionDirty(SaveSectionKeys.Knowledge);
    }

    public bool IsKnown(SpiritDefinitionSO def)
    {
        if (def == null)
            return false;

        if (_known.Contains(def))
            return true;

        string id = GetSpiritId(def);
        return !string.IsNullOrEmpty(id) && _knownIds.Contains(id);
    }

    public void Learn(SpiritDefinitionSO def)
    {
        if (def == null)
            return;

        bool added = _known.Add(def);

        string id = GetSpiritId(def);
        if (!string.IsNullOrEmpty(id))
            added |= _knownIds.Add(id);

        if (added)
            OnKnownChanged?.Invoke();

        MarkKnowledgeDirty();
    }

    public void LearnMany(IEnumerable<SpiritDefinitionSO> defs)
    {
        bool any = false;

        if (defs != null)
        {
            foreach (SpiritDefinitionSO d in defs)
            {
                if (d == null)
                    continue;

                if (_known.Add(d))
                    any = true;

                string id = GetSpiritId(d);
                if (!string.IsNullOrEmpty(id) && _knownIds.Add(id))
                    any = true;
            }
        }

        if (any)
            OnKnownChanged?.Invoke();

        MarkKnowledgeDirty();
    }

    public void Forget(SpiritDefinitionSO def)
    {
        if (def == null)
            return;

        bool removed = _known.Remove(def);

        string id = GetSpiritId(def);
        if (!string.IsNullOrEmpty(id))
            removed |= _knownIds.Remove(id);

        if (removed)
            OnKnownChanged?.Invoke();

        MarkKnowledgeDirty();
    }

    public void ForgetMany(IEnumerable<SpiritDefinitionSO> defs)
    {
        bool any = false;

        if (defs != null)
        {
            foreach (SpiritDefinitionSO d in defs)
            {
                if (d == null)
                    continue;

                if (_known.Remove(d))
                    any = true;

                string id = GetSpiritId(d);
                if (!string.IsNullOrEmpty(id) && _knownIds.Remove(id))
                    any = true;
            }
        }

        if (any)
            OnKnownChanged?.Invoke();

        MarkKnowledgeDirty();
    }

    [ContextMenu("Reset To Starting")]
    public void ResetToStarting()
    {
        _known.Clear();
        _knownIds.Clear();

        if (startingKnown != null)
        {
            foreach (SpiritDefinitionSO d in startingKnown)
            {
                if (d == null)
                    continue;

                _known.Add(d);

                string id = GetSpiritId(d);
                if (!string.IsNullOrEmpty(id))
                    _knownIds.Add(id);
            }
        }

        OnKnownChanged?.Invoke();
    }

    public IReadOnlyCollection<SpiritDefinitionSO> GetAllKnown() => _known;

    public PlayerKnownSpiritsSaveData SaveState()
    {
        PlayerKnownSpiritsSaveData data = new PlayerKnownSpiritsSaveData();

        foreach (string id in _knownIds)
        {
            if (!string.IsNullOrWhiteSpace(id))
                data.knownSpiritIDs.Add(id);
        }

        return data;
    }

    public void LoadState(PlayerKnownSpiritsSaveData data)
    {
        if (data == null)
            return;

        _known.Clear();
        _knownIds.Clear();

        if (data.knownSpiritIDs != null)
        {
            foreach (string rawId in data.knownSpiritIDs)
            {
                string id = string.IsNullOrWhiteSpace(rawId) ? null : rawId.Trim();
                if (string.IsNullOrEmpty(id))
                    continue;

                _knownIds.Add(id);

                SpiritDefinitionSO def = ResolveSpiritByID(id);
                if (def != null)
                    _known.Add(def);
            }
        }

        OnKnownChanged?.Invoke();
    }

    private static string GetSpiritId(SpiritDefinitionSO def)
    {
        if (def == null || string.IsNullOrWhiteSpace(def.spiritID))
            return null;

        return def.spiritID.Trim();
    }

    private static SpiritDefinitionSO ResolveSpiritByID(string spiritID)
    {
        if (string.IsNullOrWhiteSpace(spiritID))
            return null;

        string trimmedId = spiritID.Trim();

        if (ReligionManager.Instance != null)
        {
            SpiritDefinitionSO fromRegistry = ReligionManager.Instance.GetSpiritById(trimmedId);
            if (fromRegistry != null)
                return fromRegistry;
        }

        if (_spiritLookupById == null)
        {
            _spiritLookupById = new Dictionary<string, SpiritDefinitionSO>(StringComparer.Ordinal);

            SpiritDefinitionSO[] allDefs = Resources.LoadAll<SpiritDefinitionSO>(string.Empty);
            foreach (SpiritDefinitionSO def in allDefs)
            {
                if (def == null || string.IsNullOrWhiteSpace(def.spiritID))
                    continue;

                string id = def.spiritID.Trim();
                if (!_spiritLookupById.ContainsKey(id))
                    _spiritLookupById.Add(id, def);
            }
        }

        _spiritLookupById.TryGetValue(trimmedId, out SpiritDefinitionSO result);
        return result;
    }
}