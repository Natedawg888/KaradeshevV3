using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerKnownBuildingsManager : MonoBehaviour
{
    public static PlayerKnownBuildingsManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private BuildingManager buildingManager;

    [Header("Starting Known Buildings (IDs)")]
    [Tooltip("Populate with buildingIDs the player should know at game start.")]
    [SerializeField] private List<string> startingKnownBuildingIDs = new();

    private readonly HashSet<string> _known = new(StringComparer.Ordinal);

    public event Action OnKnownBuildingsChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        _known.Clear();

        for (int i = 0; i < startingKnownBuildingIDs.Count; i++)
        {
            string raw = startingKnownBuildingIDs[i];
            string trimmed = string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();

            if (!string.IsNullOrEmpty(trimmed))
                _known.Add(trimmed);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool IsKnown(string buildingID)
    {
        return !string.IsNullOrWhiteSpace(buildingID) && _known.Contains(buildingID);
    }

    public bool IsKnown(Building def)
    {
        return def != null && IsKnown(def.buildingID);
    }

    public IReadOnlyCollection<string> GetKnownIDs()
    {
        return _known;
    }

    public List<Building> GetKnownBuildings()
    {
        List<Building> result = new List<Building>();

        if (buildingManager == null)
            return result;

        foreach (string id in _known)
        {
            if (string.IsNullOrWhiteSpace(id))
                continue;

            Building b = buildingManager.GetBuildingByID(id);
            if (b != null)
                result.Add(b);
        }

        return result;
    }

    private void MarkKnowledgeDirty()
    {
        SaveSystem.MarkSectionDirty(SaveSectionKeys.Knowledge);
    }

    public bool Learn(string buildingID)
    {
        string trimmed = string.IsNullOrWhiteSpace(buildingID) ? null : buildingID.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return false;

        if (_known.Add(trimmed))
        {
            OnKnownBuildingsChanged?.Invoke();
            MarkKnowledgeDirty();
            return true;
        }

        return false;
    }

    public int LearnMany(IEnumerable<string> buildingIDs)
    {
        if (buildingIDs == null)
            return 0;

        int added = 0;

        foreach (string rawId in buildingIDs)
        {
            string trimmed = string.IsNullOrWhiteSpace(rawId) ? null : rawId.Trim();
            if (!string.IsNullOrEmpty(trimmed) && _known.Add(trimmed))
                added++;
        }

        if (added > 0)
            OnKnownBuildingsChanged?.Invoke();

        MarkKnowledgeDirty();
        return added;
    }

    public bool Forget(string buildingID)
    {
        string trimmed = string.IsNullOrWhiteSpace(buildingID) ? null : buildingID.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return false;

        if (_known.Remove(trimmed))
        {
            OnKnownBuildingsChanged?.Invoke();
            MarkKnowledgeDirty();
            return true;
        }

        return false;
    }

    public int ForgetMany(IEnumerable<string> buildingIDs)
    {
        if (buildingIDs == null)
            return 0;

        int removed = 0;

        foreach (string rawId in buildingIDs)
        {
            string trimmed = string.IsNullOrWhiteSpace(rawId) ? null : rawId.Trim();
            if (!string.IsNullOrEmpty(trimmed) && _known.Remove(trimmed))
                removed++;
        }

        if (removed > 0)
            OnKnownBuildingsChanged?.Invoke();
        MarkKnowledgeDirty();
        return removed;
    }

    public PlayerKnownBuildingsSaveData SaveState()
    {
        PlayerKnownBuildingsSaveData data = new PlayerKnownBuildingsSaveData();

        foreach (string id in _known)
        {
            if (!string.IsNullOrWhiteSpace(id))
                data.knownBuildingIDs.Add(id);
        }

        return data;
    }

    public void LoadState(PlayerKnownBuildingsSaveData data)
    {
        if (data == null)
            return;

        _known.Clear();

        if (data.knownBuildingIDs != null)
        {
            foreach (string rawId in data.knownBuildingIDs)
            {
                string id = string.IsNullOrWhiteSpace(rawId) ? null : rawId.Trim();
                if (!string.IsNullOrEmpty(id))
                    _known.Add(id);
            }
        }

        OnKnownBuildingsChanged?.Invoke();
    }

    public void SetBuildingManager(BuildingManager newBuildingManager)
    {
        if (newBuildingManager == null)
            return;

        buildingManager = newBuildingManager;
        OnKnownBuildingsChanged?.Invoke();
    }
}