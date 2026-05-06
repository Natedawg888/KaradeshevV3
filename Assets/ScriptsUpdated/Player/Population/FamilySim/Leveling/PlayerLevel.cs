using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerLevel : MonoBehaviour
{
    public static PlayerLevel Instance { get; private set; }

    [Header("References")]
    [SerializeField] private LevelManager levelManager;
    [SerializeField] private TurnSystem turnSystem;

    [Header("Progress")]
    public int currentXP = 0;
    public int currentLevel = 1;

    // Fired whenever the player levels up
    public delegate void OnLevelUpDelegate(int newLevel);
    public event OnLevelUpDelegate OnLevelUp;

    // Fired whenever XP changes
    public event Action<int, int> OnXPChanged; // (currentXP, xpToNextLevel)

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {

        RaiseXPChanged();
        ApplyTurnSecondsForLevel(currentLevel, keepProgress: false);
    }

    public void AddXP(int amount)
    {
        if (amount <= 0)
            return;

        currentXP += amount;

        CheckLevelUp();
        RaiseXPChanged();
        MarkCoreSystemsDirty();
    }

    private void MarkCoreSystemsDirty()
    {
        SaveSystem.MarkSectionDirty(SaveSectionKeys.CoreSystems);
    }

    private void CheckLevelUp()
    {
        while (HasNextLevel() && currentXP >= XPToNextLevel)
        {
            currentXP -= XPToNextLevel;
            currentLevel++;

            ApplyTurnSecondsForLevel(currentLevel, keepProgress: true);

            MarkCoreSystemsDirty();

            OnLevelUp?.Invoke(currentLevel);
        }

        if (!HasNextLevel())
            currentXP = 0;
    }

    private bool HasNextLevel()
    {
        return levelManager != null &&
               currentLevel < levelManager.levels.Count &&
               levelManager.GetLevelData(currentLevel) != null;
    }

    public int XPToNextLevel
    {
        get
        {
            if (!HasNextLevel())
                return 0;

            LevelData levelData = levelManager.GetLevelData(currentLevel);
            return levelData != null ? levelData.xpRequired : 0;
        }
    }

    public float Progress01
    {
        get
        {
            if (!HasNextLevel())
                return 1f;

            int req = XPToNextLevel;
            return req > 0 ? Mathf.Clamp01(currentXP / (float)req) : 0f;
        }
    }

    public int GetCurrentLevel()
    {
        return currentLevel;
    }

    private void RaiseXPChanged()
    {
        OnXPChanged?.Invoke(currentXP, XPToNextLevel);
    }

    private void ApplyTurnSecondsForLevel(int level, bool keepProgress = true)
    {
        if (levelManager == null || turnSystem == null)
            return;

        float target = levelManager.GetSecondsPerTurnForLevel(level);
        if (target <= 0f)
            return;

        float oldDuration = turnSystem.phaseDuration;
        float oldTimer = turnSystem.GetCurrentPhaseTimer();

        float remainingFrac = 1f;
        if (keepProgress && oldDuration > 0f)
            remainingFrac = Mathf.Clamp01(oldTimer / oldDuration);

        turnSystem.phaseDuration = Mathf.Max(0.1f, target);
        turnSystem.SetCurrentPhaseTimer(turnSystem.phaseDuration * remainingFrac);
    }

    public PlayerLevelSaveData SaveState()
    {
        return new PlayerLevelSaveData
        {
            currentXP = currentXP,
            currentLevel = currentLevel
        };
    }

    public void LoadState(PlayerLevelSaveData data)
    {
        if (data == null)
            return;

        if (levelManager == null)
        {
            Debug.LogError("PlayerLevel: LevelManager reference is missing while loading.");
            return;
        }

        if (turnSystem == null)
        {
            Debug.LogError("PlayerLevel: TurnSystem reference is missing while loading.");
            return;
        }

        currentLevel = data.currentLevel;

        if (HasNextLevel())
            currentXP = Mathf.Clamp(data.currentXP, 0, XPToNextLevel);
        else
            currentXP = 0;

        ApplyTurnSecondsForLoadedLevel(currentLevel);
        RaiseXPChanged();
    }

    private void ApplyTurnSecondsForLoadedLevel(int level)
    {
        if (levelManager == null || turnSystem == null)
            return;

        float target = levelManager.GetSecondsPerTurnForLevel(level);
        if (target <= 0f)
            return;

        float currentTimer = turnSystem.GetCurrentPhaseTimer();

        turnSystem.phaseDuration = Mathf.Max(0.1f, target);
        turnSystem.SetCurrentPhaseTimer(Mathf.Clamp(currentTimer, 0f, turnSystem.phaseDuration));
    }

    public void SetLevelManager(LevelManager newLevelManager)
    {
        if (newLevelManager == null)
            return;

        levelManager = newLevelManager;
    }
}