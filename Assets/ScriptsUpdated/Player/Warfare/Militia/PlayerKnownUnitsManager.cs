using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks which militia unit types the *player* knows about / can see in UI.
/// Very similar to PlayerKnownResourcesManager.
/// </summary>
public class PlayerKnownUnitsManager : MonoBehaviour
{
    public static PlayerKnownUnitsManager Instance { get; private set; }

    [Header("Starting Known Units")]
    [Tooltip("Units the player knows from game start.")]
    [SerializeField] private List<MilitiaUnit> startingKnown = new();

    private readonly HashSet<MilitiaUnit> _knownUnits = new();
    private readonly HashSet<string> _knownUnitIds = new();

    public event Action OnKnownChanged;

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

    // ---------- Public API ----------

    public bool IsKnown(MilitiaUnit unit)
    {
        if (unit == null) return false;
        if (_knownUnits.Contains(unit)) return true;
        if (!string.IsNullOrEmpty(unit.unitID) && _knownUnitIds.Contains(unit.unitID)) return true;
        return false;
    }

    public bool IsKnown(string unitID)
    {
        if (string.IsNullOrEmpty(unitID)) return false;
        return _knownUnitIds.Contains(unitID);
    }

    public void Learn(MilitiaUnit unit)
    {
        if (unit == null) return;
        bool changed = false;

        if (_knownUnits.Add(unit)) changed = true;
        if (!string.IsNullOrEmpty(unit.unitID) && _knownUnitIds.Add(unit.unitID)) changed = true;

        if (changed) OnKnownChanged?.Invoke();
        MarkKnowledgeDirty();
    }

    public void LearnMany(IEnumerable<MilitiaUnit> units)
    {
        if (units == null) return;
        bool changed = false;

        foreach (var u in units)
        {
            if (u == null) continue;
            if (_knownUnits.Add(u)) changed = true;
            if (!string.IsNullOrEmpty(u.unitID) && _knownUnitIds.Add(u.unitID)) changed = true;
        }

        if (changed) OnKnownChanged?.Invoke();
        MarkKnowledgeDirty();
    }

    public void Forget(MilitiaUnit unit)
    {
        if (unit == null) return;
        bool changed = false;

        if (_knownUnits.Remove(unit)) changed = true;
        if (!string.IsNullOrEmpty(unit.unitID) && _knownUnitIds.Remove(unit.unitID)) changed = true;

        if (changed) OnKnownChanged?.Invoke();
        MarkKnowledgeDirty();
    }

    public void ForgetMany(IEnumerable<MilitiaUnit> units)
    {
        if (units == null) return;
        bool changed = false;

        foreach (var u in units)
        {
            if (u == null) continue;
            if (_knownUnits.Remove(u)) changed = true;
            if (!string.IsNullOrEmpty(u.unitID) && _knownUnitIds.Remove(u.unitID)) changed = true;
        }

        if (changed) OnKnownChanged?.Invoke();
        MarkKnowledgeDirty();
    }

    [ContextMenu("Reset To Starting")]
    public void ResetToStarting()
    {
        _knownUnits.Clear();
        _knownUnitIds.Clear();

        if (startingKnown != null)
        {
            foreach (var u in startingKnown)
            {
                if (u == null) continue;
                _knownUnits.Add(u);
                if (!string.IsNullOrEmpty(u.unitID))
                    _knownUnitIds.Add(u.unitID);
            }
        }

        OnKnownChanged?.Invoke();
    }

    public IReadOnlyCollection<MilitiaUnit> GetAllKnown() => _knownUnits;

    public PlayerKnownUnitsSaveData SaveState()
    {
        PlayerKnownUnitsSaveData data = new PlayerKnownUnitsSaveData();

        foreach (string id in _knownUnitIds)
        {
            if (!string.IsNullOrWhiteSpace(id))
                data.knownUnitIDs.Add(id);
        }

        return data;
    }

    public void LoadState(PlayerKnownUnitsSaveData data)
    {
        if (data == null)
            return;

        _knownUnits.Clear();
        _knownUnitIds.Clear();

        if (data.knownUnitIDs != null)
        {
            foreach (string rawId in data.knownUnitIDs)
            {
                string id = string.IsNullOrWhiteSpace(rawId) ? null : rawId.Trim();
                if (string.IsNullOrEmpty(id))
                    continue;

                _knownUnitIds.Add(id);

                MilitiaUnit unit = ResolveUnitByID(id);
                if (unit != null)
                    _knownUnits.Add(unit);
            }
        }

        OnKnownChanged?.Invoke();
    }

    private static Dictionary<string, MilitiaUnit> _unitLookupById;

    private static MilitiaUnit ResolveUnitByID(string unitID)
    {
        if (string.IsNullOrWhiteSpace(unitID))
            return null;

        if (_unitLookupById == null)
        {
            _unitLookupById = new Dictionary<string, MilitiaUnit>(StringComparer.Ordinal);

            MilitiaUnit[] allUnits = Resources.LoadAll<MilitiaUnit>(string.Empty);
            foreach (MilitiaUnit unit in allUnits)
            {
                if (unit == null || string.IsNullOrWhiteSpace(unit.unitID))
                    continue;

                string id = unit.unitID.Trim();
                if (!_unitLookupById.ContainsKey(id))
                    _unitLookupById.Add(id, unit);
            }
        }

        _unitLookupById.TryGetValue(unitID.Trim(), out MilitiaUnit result);
        return result;
    }
}
