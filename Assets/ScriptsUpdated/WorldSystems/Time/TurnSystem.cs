using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[System.Serializable]
public enum DayPhase
{
    Day,
    Dusk,
    Night,
    Dawn
}

public class TurnSystem : MonoBehaviour
{
    public static TurnSystem Instance { get; private set; }

    [Header("Light")]
    public Light directionalLight;

    public static event Action OnStartOfTurn;

    // Sorted end-of-turn invocation list (lower priority number fires first)
    private static readonly List<(int priority, Action handler)> _endOfTurnHandlers = new List<(int, Action)>();
    private static bool _endOfTurnNeedsSort;
    private static readonly List<Action> _endOfTurnFireBuffer = new List<Action>();

    [Header("Day-Night Cycle Settings")]
    public LightSettings daySettings;
    public LightSettings duskSettings;
    public LightSettings nightSettings;
    public LightSettings dawnSettings;

    [Header("UI Settings")]
    public Image phaseImage;
    public Image phaseFillImage;
    public Sprite daySprite;
    public Sprite duskSprite;
    public Sprite nightSprite;
    public Sprite dawnSprite;

    [Header("Turn UI")]
    public TMP_Text turnText;

    [Header("Control Buttons")]
    public Image pauseButtonImage;
    public Sprite pauseSprite;
    public Sprite playSprite;

    public Image speedButtonImage;
    public Sprite normalSpeedSprite;
    public Sprite fastSpeedSprite;

    private Button _pauseButton;
    private Button _speedButton;

    [Header("Timer Speed")]
    public float normalSpeedMultiplier = 1f;
    public float fastSpeedMultiplier = 2f;

    public DayPhase currentPhase = DayPhase.Day;
    public float phaseDuration = 5f;
    private float phaseTimer;

    public static int CurrentTurn { get; private set; } = 1;

    private bool isPaused = false;
    private bool isSpeedingUp = false;
    private float currentSpeedMultiplier = 1f;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        PauseTurnTimer();
        phaseTimer = phaseDuration;
        currentSpeedMultiplier = normalSpeedMultiplier;

        UpdateLighting(currentPhase);
    }

    private bool isAdvancingTurn = false;

    // Turn-advance blocking: systems doing multi-frame work call Block/Unblock.
    // The turn queues at timer=0 until all blockers release.
    private static int _turnBlockers = 0;
    private bool _pendingTurnAdvance = false;

    public static void BlockTurnAdvance()
    {
        _turnBlockers++;
    }

    public static void UnblockTurnAdvance()
    {
        if (_turnBlockers > 0)
            _turnBlockers--;
    }

    void Update()
    {
        if (isPaused || isAdvancingTurn)
            return;

        if (_pendingTurnAdvance)
        {
            if (_turnBlockers <= 0)
            {
                _pendingTurnAdvance = false;
                StartCoroutine(AdvanceTurnRoutine());
            }
            return;
        }

        phaseTimer -= Time.deltaTime * currentSpeedMultiplier;

        if (phaseTimer <= 0f)
        {
            phaseTimer = 0f;
            UpdatePhaseTimer();

            if (_turnBlockers > 0)
                _pendingTurnAdvance = true;
            else
                StartCoroutine(AdvanceTurnRoutine());
            return;
        }

        UpdatePhaseTimer();
    }

    private IEnumerator AdvanceTurnRoutine()
    {
        isAdvancingTurn = true;
        _pendingTurnAdvance = false;

        // Clamp visually to zero first
        phaseTimer = 0f;
        UpdatePhaseTimer();

        // Frame 1: end-of-turn work
        EndOfTurn();
        yield return null;

        // Frame 2: phase change / lighting / icon / text update
        NextPhase();
        yield return null;

        // Frame 3: reset timer for the new phase
        phaseTimer = phaseDuration;
        UpdatePhaseTimer();
        yield return null;

        // Frame 4: start-of-turn work
        StartOfTurn();

        isAdvancingTurn = false;
    }

    void NextPhase()
    {
        currentPhase = (DayPhase)(((int)currentPhase + 1) % 4);
        UpdateLighting(currentPhase);
        UpdatePhaseImage(currentPhase);
        UpdateTurnText();
    }

    public void UpdateLighting(DayPhase phase)
    {
        switch (phase)
        {
            case DayPhase.Day:
                ApplyLightSettings(daySettings);
                break;
            case DayPhase.Dusk:
                ApplyLightSettings(duskSettings);
                break;
            case DayPhase.Night:
                ApplyLightSettings(nightSettings);
                break;
            case DayPhase.Dawn:
                ApplyLightSettings(dawnSettings);
                break;
        }
    }

    void ApplyLightSettings(LightSettings settings)
    {
        directionalLight.color = settings.lightColor;
        directionalLight.intensity = settings.lightIntensity;
    }

    public void UpdatePhaseImage(DayPhase phase)
    {
        switch (phase)
        {
            case DayPhase.Day:
                phaseImage.sprite = daySprite;
                break;
            case DayPhase.Dusk:
                phaseImage.sprite = duskSprite;
                break;
            case DayPhase.Night:
                phaseImage.sprite = nightSprite;
                break;
            case DayPhase.Dawn:
                phaseImage.sprite = dawnSprite;
                break;
        }
    }

    public void StartOfTurn()
    {
        OnStartOfTurn?.Invoke();
    }

    public static void SubscribeToStartOfTurn(Action handler)
    {
        OnStartOfTurn += handler;
        //Debug.Log($"[TurnSystem] SubscribeToStartOfTurn -> {handler.Method.Name}");
    }

    public static void UnsubscribeFromStartOfTurn(Action handler)
    {
        OnStartOfTurn -= handler;
        //Debug.Log($"[TurnSystem] UnsubscribeFromStartOfTurn -> {handler.Method.Name}");
    }

    public void EndOfTurn()
    {
        IncrementTurnCount();
        if (_endOfTurnNeedsSort)
        {
            _endOfTurnHandlers.Sort((a, b) => a.priority.CompareTo(b.priority));
            _endOfTurnNeedsSort = false;
        }
        _endOfTurnFireBuffer.Clear();
        for (int i = 0; i < _endOfTurnHandlers.Count; i++)
            _endOfTurnFireBuffer.Add(_endOfTurnHandlers[i].handler);
        for (int i = 0; i < _endOfTurnFireBuffer.Count; i++)
            _endOfTurnFireBuffer[i]?.Invoke();
    }

    public static void SubscribeToEndOfTurn(Action handler, int priority = 0)
    {
        _endOfTurnHandlers.Add((priority, handler));
        _endOfTurnNeedsSort = true;
    }

    public static void UnsubscribeFromEndOfTurn(Action handler)
    {
        for (int i = _endOfTurnHandlers.Count - 1; i >= 0; i--)
        {
            if (_endOfTurnHandlers[i].handler == handler)
            {
                _endOfTurnHandlers.RemoveAt(i);
                break;
            }
        }
    }

    public void UpdatePhaseTimer()
    {
        if (phaseFillImage != null)
        {
            phaseFillImage.fillAmount = 1 - (phaseTimer / phaseDuration);
        }
    }

    public static int GetCurrentTurn()
    {
        return CurrentTurn;
    }

    void IncrementTurnCount()
    {
        CurrentTurn++;
        UpdateTurnText();
        MarkCoreSystemsDirty();
    }

    public void PauseTurnTimer()
    {
        isPaused = true;
        isSpeedingUp = false;
        currentSpeedMultiplier = normalSpeedMultiplier;
        UpdateButtonIcons();
    }

    public void ResumeTurnTimer()
    {
        isPaused = false;
        UpdateButtonIcons();
    }

    public void TogglePauseTimer()
    {
        isPaused = !isPaused;

        if (isPaused)
        {
            isSpeedingUp = false;
            currentSpeedMultiplier = normalSpeedMultiplier;
        }

        UpdateButtonIcons();
    }

    public void ToggleSpeedUp()
    {
        if (isPaused)
        {
            isPaused = false;
        }

        isSpeedingUp = !isSpeedingUp;
        currentSpeedMultiplier = isSpeedingUp ? fastSpeedMultiplier : normalSpeedMultiplier;

        UpdateButtonIcons();
    }

    private void UpdateButtonIcons()
    {
        if (pauseButtonImage != null)
        {
            pauseButtonImage.sprite = isPaused ? playSprite : pauseSprite;
        }

        if (speedButtonImage != null)
        {
            speedButtonImage.sprite = isSpeedingUp ? normalSpeedSprite : fastSpeedSprite;
        }
    }

    private void SetDirectionalLightRotation(float xRotation)
    {
        if (directionalLight != null)
        {
            directionalLight.transform.rotation = Quaternion.Euler(
                xRotation,
                directionalLight.transform.eulerAngles.y,
                directionalLight.transform.eulerAngles.z
            );
        }
    }

    public void SetCurrentPhaseTimer(float timerValue)
    {
        phaseTimer = timerValue;
        MarkCoreSystemsDirty();
    }

    public float GetCurrentPhaseTimer()
    {
        return phaseTimer;
    }

    public static void SetCurrentTurn(int turn)
    {
        CurrentTurn = turn;

        if (Instance != null)
            Instance.UpdateTurnText();
    }

    private void UpdateTurnText()
    {
        if (turnText == null)
            return;

        string seasonName = "No Season";

        if (SeasonManager.Instance != null && SeasonManager.Instance.CurrentSeason != null)
            seasonName = string.IsNullOrWhiteSpace(SeasonManager.Instance.CurrentSeason.displayName)
                ? SeasonManager.Instance.CurrentSeason.seasonID
                : SeasonManager.Instance.CurrentSeason.displayName;

        turnText.text = $"{CurrentTurn} - {currentPhase} - {seasonName}";
    }

    private void MarkCoreSystemsDirty()
    {
        SaveSystem.MarkSectionDirty(SaveSectionKeys.CoreSystems);
    }

    public TurnSystemSaveData SaveState()
    {
        return new TurnSystemSaveData
        {
            currentTurn = CurrentTurn,
            currentPhase = currentPhase,
            phaseTimer = phaseTimer
        };
    }

    public void LoadState(TurnSystemSaveData data)
    {
        if (data == null)
            return;

        currentPhase = data.currentPhase;
        phaseTimer = Mathf.Clamp(data.phaseTimer, 0f, Mathf.Max(0.01f, phaseDuration));

        SetCurrentTurn(data.currentTurn);

        UpdateLighting(currentPhase);
        UpdatePhaseImage(currentPhase);
        UpdatePhaseTimer();
        UpdateTurnText();
        ResumeTurnTimer();
    }

    public void RefreshResolvedUI()
    {
        ResolveAndBindControlButtons();
        UpdateLighting(currentPhase);
        UpdatePhaseImage(currentPhase);
        UpdateTurnText();
        UpdateButtonIcons();
        UpdatePhaseTimer();
    }

    private void ResolveAndBindControlButtons()
    {
        Button newPauseButton = pauseButtonImage != null
            ? pauseButtonImage.GetComponentInParent<Button>()
            : null;

        Button newSpeedButton = speedButtonImage != null
            ? speedButtonImage.GetComponentInParent<Button>()
            : null;

        if (_pauseButton != newPauseButton)
        {
            if (_pauseButton != null)
                _pauseButton.onClick.RemoveListener(TogglePauseTimer);

            _pauseButton = newPauseButton;

            if (_pauseButton != null)
            {
                _pauseButton.onClick.RemoveListener(TogglePauseTimer);
                _pauseButton.onClick.AddListener(TogglePauseTimer);
            }
        }

        if (_speedButton != newSpeedButton)
        {
            if (_speedButton != null)
                _speedButton.onClick.RemoveListener(ToggleSpeedUp);

            _speedButton = newSpeedButton;

            if (_speedButton != null)
            {
                _speedButton.onClick.RemoveListener(ToggleSpeedUp);
                _speedButton.onClick.AddListener(ToggleSpeedUp);
            }
        }
    }

    private void OnDestroy()
    {
        if (_pauseButton != null)
            _pauseButton.onClick.RemoveListener(TogglePauseTimer);

        if (_speedButton != null)
            _speedButton.onClick.RemoveListener(ToggleSpeedUp);

        if (Instance == this)
        {
            Instance = null;
            _endOfTurnHandlers.Clear();
            _endOfTurnFireBuffer.Clear();
            _turnBlockers = 0;
        }
    }

    public IEnumerator RunGhostPhaseAdvance(Action onGhostTick = null)
    {
        bool wasPaused = isPaused;

        isPaused = true;
        isAdvancingTurn = true;

        // Frame 1: visually empty the phase timer
        phaseTimer = 0f;
        UpdatePhaseTimer();
        yield return null;

        // Frame 2: apply the tutorial-only tile tick
        onGhostTick?.Invoke();
        yield return null;

        // Frame 3: advance phase visuals only
        NextPhase();
        yield return null;

        // Frame 4: refill timer for the next phase
        phaseTimer = phaseDuration;
        UpdatePhaseTimer();
        yield return null;

        isAdvancingTurn = false;

        // Stay paused during tutorials
        isPaused = true;
        UpdateButtonIcons();
    }
}

[System.Serializable]
public class LightSettings
{
    public Color lightColor = Color.white;
    public float lightIntensity = 1.0f;
}
