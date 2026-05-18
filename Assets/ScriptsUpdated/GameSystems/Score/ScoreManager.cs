using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [Header("Point Values")]
    [SerializeField, Min(0)] private int discoveryPoints      = 10;
    [SerializeField, Min(0)] private int gatheringPoints      = 5;
    [SerializeField, Min(0)] private int craftingPoints       = 15;
    [SerializeField, Min(0)] private int productionCyclePoints= 8;
    [SerializeField, Min(0)] private int birthPoints          = 20;
    [SerializeField, Min(0)] private int buildingCompletePoints = 25;
    [SerializeField, Min(0)] private int buildingRepairPoints = 10;
    [SerializeField, Min(0)] private int firefightVictoryPoints = 20;
    [SerializeField, Min(0)] private int trainingPoints       = 20;
    [SerializeField, Min(0)] private int combatVictoryPoints  = 15;
    [SerializeField, Min(0)] private int researchPoints       = 30;
    [SerializeField, Min(0)] private int populationAgedPoints = 5;
    [SerializeField, Min(0)] private int familyFormedPoints  = 30;
    [SerializeField, Min(0)] private int levelUpPoints       = 50;

    [Header("Penalties")]
    [SerializeField, Min(0)] private int buildingDestroyedPenalty = 25;
    [SerializeField, Min(0)] private int firefightLostPenalty     = 15;
    [SerializeField, Min(0)] private int discoveryPopLostPenalty  = 10;
    [SerializeField, Min(0)] private int gatheringPopLostPenalty  = 10;
    [SerializeField, Min(0)] private int birthFailurePenalty      = 20;

    public int CurrentScore { get; private set; }
    public event Action<int> OnScoreChanged;

    private bool _gameStarted;
    private string _leaderboardPath;

    private const string LeaderboardFileName = "scoreboard.json";
    private static string LeaderboardFilePath => Path.Combine(Application.persistentDataPath, LeaderboardFileName);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        _leaderboardPath = Path.Combine(Application.persistentDataPath, LeaderboardFileName);
    }


    public void OnGameStarted()
    {
        _gameStarted = true;
    }

    private void AddScore(int points)
    {
        if (!_gameStarted || points <= 0)
            return;

        CurrentScore += points;
        OnScoreChanged?.Invoke(CurrentScore);
        SaveSystem.MarkSectionDirty(SaveSectionKeys.CoreSystems);
    }

    private void SubtractScore(int penalty)
    {
        if (!_gameStarted || penalty <= 0)
            return;

        CurrentScore = Mathf.Max(0, CurrentScore - penalty);
        OnScoreChanged?.Invoke(CurrentScore);
        SaveSystem.MarkSectionDirty(SaveSectionKeys.CoreSystems);
    }

    public static void NotifyBuildingCompleted()  => Instance?.AddScore(Instance.buildingCompletePoints);

    // Direct-call hooks (called from within manager code)
    public static void NotifyDiscovery()          => Instance?.AddScore(Instance.discoveryPoints);
    public static void NotifyGathering()          => Instance?.AddScore(Instance.gatheringPoints);
    public static void NotifyCraftCompleted()     => Instance?.AddScore(Instance.craftingPoints);
    public static void NotifyProductionCycle()    => Instance?.AddScore(Instance.productionCyclePoints);
    public static void NotifyBirth()              => Instance?.AddScore(Instance.birthPoints);
    public static void NotifyBuildingRepaired()   => Instance?.AddScore(Instance.buildingRepairPoints);
    public static void NotifyFirefightVictory()   => Instance?.AddScore(Instance.firefightVictoryPoints);

    // Penalties
    public static void NotifyBuildingDestroyed()               => Instance?.SubtractScore(Instance.buildingDestroyedPenalty);
    public static void NotifyFirefightLost()                   => Instance?.SubtractScore(Instance.firefightLostPenalty);
    public static void NotifyDiscoveryPopLost(int count)       => Instance?.SubtractScore(Instance.discoveryPopLostPenalty * Mathf.Max(1, count));
    public static void NotifyGatheringPopLost(int count)       => Instance?.SubtractScore(Instance.gatheringPopLostPenalty * Mathf.Max(1, count));
    public static void NotifyBirthFailure()                    => Instance?.SubtractScore(Instance.birthFailurePenalty);
    public static void NotifyTrainingCompleted()  => Instance?.AddScore(Instance.trainingPoints);
    public static void NotifyCombatVictory()      => Instance?.AddScore(Instance.combatVictoryPoints);
    public static void NotifyResearchCompleted()  => Instance?.AddScore(Instance.researchPoints);
    public static void NotifyPopulationAged()     => Instance?.AddScore(Instance.populationAgedPoints);
    public static void NotifyFamilyFormed()       => Instance?.AddScore(Instance.familyFormedPoints);
    public static void NotifyLevelUp()            => Instance?.AddScore(Instance.levelUpPoints);

    // Save / Load (integrated with CoreSystems save section)
    public int SaveState() => CurrentScore;

    public void LoadState(int score)
    {
        CurrentScore = score;
        OnScoreChanged?.Invoke(CurrentScore);
    }

    // Leaderboard (persists across game sessions in a separate file)
    public void CommitScoreToLeaderboard(string playerName, string civName, string avatarName)
    {
        if (CurrentScore <= 0)
            return;

        ScoreboardData data = LoadLeaderboardFromDisk() ?? new ScoreboardData();

        data.entries.Add(new ScoreboardEntry
        {
            score            = CurrentScore,
            playerName       = playerName       ?? string.Empty,
            civilizationName = civName          ?? string.Empty,
            avatarName       = avatarName       ?? string.Empty
        });

        data.entries.Sort((a, b) => b.score.CompareTo(a.score));

        if (data.entries.Count > 5)
            data.entries.RemoveRange(5, data.entries.Count - 5);

        SaveLeaderboardToDisk(data);
    }

    public ScoreboardData GetLeaderboard()
    {
        return LoadLeaderboardFromDisk() ?? new ScoreboardData();
    }

    // Reads the leaderboard from disk without requiring a live instance — safe to call
    // at any point, including before ScoreManager has initialized in the current scene.
    public static ScoreboardData ReadLeaderboard()
    {
        try
        {
            string path = LeaderboardFilePath;
            if (!File.Exists(path))
                return new ScoreboardData();

            string json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<ScoreboardData>(json) ?? new ScoreboardData();
        }
        catch
        {
            return new ScoreboardData();
        }
    }

    private ScoreboardData LoadLeaderboardFromDisk()
    {
        try
        {
            if (!File.Exists(_leaderboardPath))
                return null;

            string json = File.ReadAllText(_leaderboardPath);
            return JsonConvert.DeserializeObject<ScoreboardData>(json);
        }
        catch
        {
            return null;
        }
    }

    private void SaveLeaderboardToDisk(ScoreboardData data)
    {
        try
        {
            string json = JsonConvert.SerializeObject(data, Formatting.Indented);
            File.WriteAllText(_leaderboardPath, json);
        }
        catch { }
    }
}
