using UnityEngine;

public class TaskFailureStoryManager : MonoBehaviour
{
    public static TaskFailureStoryManager Instance { get; private set; }

    [Header("Config (ONE asset for the whole game)")]
    [SerializeField] private TaskFailureStoryDatabase database;

    private void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public string BuildStory(EnvironmentControl env, TaskFailureType type, int populationLost)
    {
        if (database == null) return "";
        return database.BuildStory(env, type, populationLost);
    }
}