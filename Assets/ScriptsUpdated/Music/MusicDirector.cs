using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

[System.Serializable]
public class StageTracks
{
    public Stage stage;
    [Header("3 tracks per stage (early/mid/late)")]
    public AudioClip earlyTrack;
    public AudioClip midTrack;
    public AudioClip lateTrack;
}

[RequireComponent(typeof(AudioSource))]
public class MusicDirector : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerLevel playerLevel;
    [SerializeField] private LevelManager levelManager;

    [Header("Stage → Tracks")]
    public List<StageTracks> stageTracks = new();

    [Header("Playback")]
    [Tooltip("Loop the selected track.")]
    public bool loop = true;

    [Tooltip("Seconds to crossfade when changing tracks.")]
    [Range(0f, 5f)] public float crossfadeSeconds = 1.25f;

    private AudioSource _srcA;
    private AudioSource _srcB;
    private bool _usingA = true;

    private Coroutine _crossfadeRoutine;
    private bool _subscribedToPlayerLevel;

    public PlayerLevel PlayerLevel => playerLevel;
    public LevelManager LevelManager => levelManager;

    private void Awake()
    {
        _srcA = GetComponent<AudioSource>();
        _srcA.playOnAwake = false;
        _srcA.volume = 0f;

        _srcB = gameObject.AddComponent<AudioSource>();
        _srcB.playOnAwake = false;
        _srcB.volume = 0f;

        ApplyLoopFlag();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        TryResolveMissingRefs();
        RefreshBindingAndMusic();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnsubscribeFromPlayerLevel();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnsubscribeFromPlayerLevel();
    }

    private void OnValidate()
    {
        ApplyLoopFlag();
    }

    private void ApplyLoopFlag()
    {
        if (_srcA != null) _srcA.loop = loop;
        if (_srcB != null) _srcB.loop = loop;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryResolveMissingRefs();
        RefreshBindingAndMusic();
    }

    private void TryResolveMissingRefs()
    {
        if (levelManager == null)
            levelManager = FindFirstObjectByType<LevelManager>(FindObjectsInactive.Include);

        if (playerLevel == null)
            playerLevel = PlayerLevel.Instance != null
                ? PlayerLevel.Instance
                : FindFirstObjectByType<PlayerLevel>(FindObjectsInactive.Include);
    }

    private void RefreshBindingAndMusic()
    {
        if (playerLevel != null && !_subscribedToPlayerLevel)
        {
            playerLevel.OnLevelUp += HandlePlayerLevelUp;
            _subscribedToPlayerLevel = true;
        }

        if (playerLevel != null && levelManager != null)
            UpdateMusicImmediate();
    }

    private void HandlePlayerLevelUp(int newLevel)
    {
        UpdateMusicImmediate();
    }

    private void UnsubscribeFromPlayerLevel()
    {
        if (playerLevel != null && _subscribedToPlayerLevel)
        {
            playerLevel.OnLevelUp -= HandlePlayerLevelUp;
            _subscribedToPlayerLevel = false;
        }
    }

    public void SetPlayerLevel(PlayerLevel newPlayerLevel, bool refreshNow = true)
    {
        if (playerLevel == newPlayerLevel)
        {
            if (refreshNow)
                UpdateMusicImmediate();
            return;
        }

        UnsubscribeFromPlayerLevel();

        playerLevel = newPlayerLevel;

        if (playerLevel != null)
        {
            playerLevel.OnLevelUp += HandlePlayerLevelUp;
            _subscribedToPlayerLevel = true;
        }

        if (refreshNow)
            UpdateMusicImmediate();
    }

    public void SetLevelManager(LevelManager newLevelManager, bool refreshNow = true)
    {
        levelManager = newLevelManager;

        if (refreshNow)
            UpdateMusicImmediate();
    }

    public void UpdateMusicImmediate()
    {
        if (playerLevel == null || levelManager == null || stageTracks == null || stageTracks.Count == 0)
            return;

        int curLevel = playerLevel.GetCurrentLevel();
        Stage stage = levelManager.GetStageForLevel(curLevel);

        var stageLevels = levelManager.levels
            .Where(l => l != null && l.stage == stage)
            .OrderBy(l => l.level)
            .ToList();

        if (stageLevels.Count == 0)
            return;

        int firstLevel = stageLevels[0].level;
        int lastLevel = stageLevels[stageLevels.Count - 1].level;
        int pos = Mathf.Clamp(curLevel - firstLevel, 0, Mathf.Max(0, lastLevel - firstLevel));

        int count = stageLevels.Count;
        int thirdSize = Mathf.Max(1, Mathf.CeilToInt(count / 3f));
        int bucket = Mathf.Clamp(pos / thirdSize, 0, 2);

        StageTracks set = stageTracks.FirstOrDefault(s => s != null && s.stage == stage);
        if (set == null)
            return;

        AudioClip target = bucket switch
        {
            0 => set.earlyTrack,
            1 => set.midTrack,
            _ => set.lateTrack
        };

        if (target == null)
            return;

        AudioSource active = _usingA ? _srcA : _srcB;
        AudioSource inactive = _usingA ? _srcB : _srcA;

        if ((active != null && active.clip == target && active.isPlaying) ||
            (inactive != null && inactive.clip == target && inactive.isPlaying))
        {
            return;
        }

        if (_crossfadeRoutine != null)
            StopCoroutine(_crossfadeRoutine);

        _crossfadeRoutine = StartCoroutine(CrossfadeTo(target));
    }

    private IEnumerator CrossfadeTo(AudioClip next)
    {
        AudioSource from = _usingA ? _srcA : _srcB;
        AudioSource to = _usingA ? _srcB : _srcA;

        to.clip = next;
        to.loop = loop;
        to.volume = 0f;
        to.Play();

        float dur = Mathf.Max(0f, crossfadeSeconds);

        if (dur <= 0f)
        {
            if (from.isPlaying)
                from.Stop();

            from.clip = null;
            to.volume = 1f;
            _usingA = !_usingA;
            _crossfadeRoutine = null;
            yield break;
        }

        float t = 0f;
        float fromStart = from.volume;

        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);

            to.volume = Mathf.Lerp(0f, 1f, k);
            from.volume = Mathf.Lerp(fromStart, 0f, k);

            yield return null;
        }

        if (from.isPlaying)
            from.Stop();

        from.clip = null;
        from.volume = 0f;
        to.volume = 1f;

        _usingA = !_usingA;
        _crossfadeRoutine = null;
    }
}