using System;
using System.Collections.Generic;
using UnityEngine;

public class SeasonManager : MonoBehaviour
{
    public static SeasonManager Instance { get; private set; }

    [SerializeField] private int activeSeasonSetID = -1;
    [SerializeField] private int currentSeasonIndex = 0;
    [SerializeField] private int turnsIntoCurrentSeason = 0;

    private readonly List<SeasonDefinition> runtimeCycle = new();

    private bool hasLoadedSeasonState = false;
    private bool hasResolvedInitialSeason = false;

    public event Action<SeasonDefinition> OnSeasonChanged;

    public SeasonDefinition CurrentSeason => GetCurrentSeasonInternal();
    public int ActiveSeasonSetID => activeSeasonSetID;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        // New game/startup path.
        // If LoadState already ran before Start, do not randomize.
        if (!hasResolvedInitialSeason)
        {
            EnsureCycleResolved(
                forceRestart: true,
                allowPresetRandomStart: !hasLoadedSeasonState
            );

            hasResolvedInitialSeason = true;
            NotifySeasonChanged();
        }
    }

    private void OnEnable()
    {
        TurnSystem.SubscribeToEndOfTurn(HandleTurnEnd);

        if (EnvironmentPresetManager.Instance != null)
            EnvironmentPresetManager.Instance.OnPresetApplied += HandlePresetApplied;
    }

    private void OnDisable()
    {
        TurnSystem.UnsubscribeFromEndOfTurn(HandleTurnEnd);

        if (EnvironmentPresetManager.Instance != null)
            EnvironmentPresetManager.Instance.OnPresetApplied -= HandlePresetApplied;
    }

    private void HandlePresetApplied(EnvironmentPreset preset)
    {
        // Preset/new map path.
        // Randomize only if this is not restoring saved season data.
        EnsureCycleResolved(
            forceRestart: true,
            allowPresetRandomStart: !hasLoadedSeasonState
        );

        hasResolvedInitialSeason = true;

        NotifySeasonChanged();
        MarkCoreSystemsDirty();
    }

    private void EnsureCycleResolved(
        bool forceRestart = false,
        bool allowPresetRandomStart = false)
    {
        var presetMgr = EnvironmentPresetManager.Instance;
        if (presetMgr == null)
        {
            runtimeCycle.Clear();
            return;
        }

        PresetSeasonSet set = null;

        if (activeSeasonSetID >= 0)
            set = presetMgr.GetSeasonSet(activeSeasonSetID);

        if (set == null)
            set = presetMgr.GetDefaultSeasonSet();

        BuildRuntimeCycle(set);

        if (set != null)
            activeSeasonSetID = set.setID;

        if (forceRestart || currentSeasonIndex >= runtimeCycle.Count)
        {
            if (allowPresetRandomStart && ShouldRandomizeStartingSeasonFromPreset())
            {
                RandomizeStartingSeasonFromPreset();
            }
            else
            {
                currentSeasonIndex = 0;
                turnsIntoCurrentSeason = 0;
            }
        }

        ClampTurnCounterToCurrentSeason();
    }

    private bool ShouldRandomizeStartingSeasonFromPreset()
    {
        if (runtimeCycle.Count == 0)
            return false;

        var presetMgr = EnvironmentPresetManager.Instance;
        if (presetMgr == null)
            return false;

        EnvironmentPreset preset = presetMgr.GetCurrentPreset();
        if (preset == null)
            return false;

        return preset.randomizeStartingSeason;
    }

    private void RandomizeStartingSeasonFromPreset()
    {
        var presetMgr = EnvironmentPresetManager.Instance;
        EnvironmentPreset preset = presetMgr != null ? presetMgr.GetCurrentPreset() : null;

        if (runtimeCycle.Count == 0)
        {
            currentSeasonIndex = 0;
            turnsIntoCurrentSeason = 0;
            return;
        }

        currentSeasonIndex = UnityEngine.Random.Range(0, runtimeCycle.Count);

        if (preset != null && preset.randomizeTurnsIntoStartingSeason)
        {
            SeasonDefinition season = runtimeCycle[currentSeasonIndex];
            int seasonTurns = Mathf.Max(1, season != null ? season.turns : 1);

            // Random.Range int max is exclusive, so this gives 0 to seasonTurns - 1.
            turnsIntoCurrentSeason = UnityEngine.Random.Range(0, seasonTurns);
        }
        else
        {
            turnsIntoCurrentSeason = 0;
        }
    }

    private void BuildRuntimeCycle(PresetSeasonSet set)
    {
        runtimeCycle.Clear();

        if (set == null || set.cycle == null)
            return;

        for (int i = 0; i < set.cycle.Count; i++)
        {
            SeasonCycleEntry entry = set.cycle[i];
            if (entry == null || entry.season == null)
                continue;

            int repeats = Mathf.Max(1, entry.repeatCount);
            for (int r = 0; r < repeats; r++)
                runtimeCycle.Add(entry.season);
        }
    }

    public bool SwitchSeasonSet(int setID, bool restartCycle = true)
    {
        var presetMgr = EnvironmentPresetManager.Instance;
        if (presetMgr == null)
            return false;

        var set = presetMgr.GetSeasonSet(setID);
        if (set == null)
            return false;

        activeSeasonSetID = setID;
        BuildRuntimeCycle(set);

        if (restartCycle)
        {
            if (ShouldRandomizeStartingSeasonFromPreset())
                RandomizeStartingSeasonFromPreset();
            else
            {
                currentSeasonIndex = 0;
                turnsIntoCurrentSeason = 0;
            }
        }
        else
        {
            currentSeasonIndex = Mathf.Clamp(currentSeasonIndex, 0, Mathf.Max(0, runtimeCycle.Count - 1));
            ClampTurnCounterToCurrentSeason();
        }

        NotifySeasonChanged();
        MarkCoreSystemsDirty();
        return true;
    }

    private void HandleTurnEnd()
    {
        SeasonDefinition current = GetCurrentSeasonInternal();
        if (current == null || runtimeCycle.Count == 0)
            return;

        turnsIntoCurrentSeason++;

        if (turnsIntoCurrentSeason >= Mathf.Max(1, current.turns))
        {
            turnsIntoCurrentSeason = 0;
            currentSeasonIndex = (currentSeasonIndex + 1) % runtimeCycle.Count;
            NotifySeasonChanged();
        }

        MarkCoreSystemsDirty();
    }

    private void NotifySeasonChanged()
    {
        OnSeasonChanged?.Invoke(GetCurrentSeasonInternal());
    }

    private SeasonDefinition GetCurrentSeasonInternal()
    {
        if (runtimeCycle.Count == 0)
            return null;

        currentSeasonIndex = Mathf.Clamp(currentSeasonIndex, 0, runtimeCycle.Count - 1);
        return runtimeCycle[currentSeasonIndex];
    }

    private void ClampTurnCounterToCurrentSeason()
    {
        SeasonDefinition current = GetCurrentSeasonInternal();
        if (current == null)
        {
            turnsIntoCurrentSeason = 0;
            return;
        }

        turnsIntoCurrentSeason = Mathf.Clamp(turnsIntoCurrentSeason, 0, Mathf.Max(0, current.turns - 1));
    }

    private void MarkCoreSystemsDirty()
    {
        SaveSystem.MarkSectionDirty(SaveSectionKeys.CoreSystems);
    }

    public string GetSeasonName()
    {
        return CurrentSeason != null ? CurrentSeason.displayName : "No Season";
    }

    public int GetTurnsIntoCurrentSeason()
    {
        return turnsIntoCurrentSeason;
    }

    public int GetTurnsRemainingInSeason()
    {
        SeasonDefinition current = CurrentSeason;
        if (current == null) return 0;

        return Mathf.Max(1, current.turns) - turnsIntoCurrentSeason;
    }

    public float GetSeasonProgress()
    {
        SeasonDefinition current = CurrentSeason;
        if (current == null) return 0f;

        return (float)turnsIntoCurrentSeason / Mathf.Max(1, current.turns);
    }

    public SeasonManagerSaveData SaveState()
    {
        return new SeasonManagerSaveData
        {
            activeSeasonSetID = activeSeasonSetID,
            currentSeasonIndex = currentSeasonIndex,
            turnsIntoCurrentSeason = turnsIntoCurrentSeason
        };
    }

    public void LoadState(SeasonManagerSaveData data)
    {
        if (data == null)
            return;

        hasLoadedSeasonState = true;

        activeSeasonSetID = data.activeSeasonSetID;
        currentSeasonIndex = Mathf.Max(0, data.currentSeasonIndex);
        turnsIntoCurrentSeason = Mathf.Max(0, data.turnsIntoCurrentSeason);

        EnsureCycleResolved(forceRestart: false);
        hasResolvedInitialSeason = true;

        NotifySeasonChanged();
    }
}