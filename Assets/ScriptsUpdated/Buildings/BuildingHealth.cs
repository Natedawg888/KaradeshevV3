using UnityEngine;

[RequireComponent(typeof(BuildingStatus))]
public class BuildingHealth : MonoBehaviour
{
    [Header("Health (per-instance, can be overridden by manager defaults)")]
    [Min(1)] public int maxHealth = 100;
    [SerializeField] private int currentHealth = 100;

    [Header("Natural Degeneration")]
    public int degenerationAmount = 5;
    [Min(1)] public int degenerationIntervalTurns = 3;

    [Header("Damage→State thresholds")]
    [Tooltip("Switch to Damaged at/below this fraction of max health.")]
    [Range(0f,1f)] public float damagedThreshold = 0.33f;

    [Header("Config Source")]
    [Tooltip("If true, pulls defaults from BuildingManager by buildingID on Awake.")]
    public bool useManagerDefaults = true;
    [Tooltip("Optional explicit buildingID (if BuildingControl is missing). If empty, tries BuildingControl.buildingID.")]
    public string buildingIDOverride;

    private int _degenerationPauseCounter = 0;
    public  bool IsDegenerationPaused => _degenerationPauseCounter > 0;

    private int _turnsSinceDegenerate;
    private BuildingStatus _status;

    public int CurrentHealth => currentHealth;

    public event System.Action<int,int> OnHealthChanged; // (current, max)

    // Call this helper whenever values change
    private void NotifyChanged() => OnHealthChanged?.Invoke(currentHealth, maxHealth);

    private void Awake()
    {
        _status = GetComponent<BuildingStatus>();

        if (useManagerDefaults)
            LoadDefaultsFromManager();

        string id = !string.IsNullOrEmpty(buildingIDOverride)
            ? buildingIDOverride
            : GetComponent<BuildingControl>()?.buildingID;

        var mgr = BuildingManager.Instance;
        if (mgr == null) return;

        var def = mgr.GetBuildingByID(id);
        if (def == null) return;

        maxHealth = Mathf.Max(1, def.defaultMaxHealth);

        currentHealth = def.defaultMaxHealth;
        ApplyThresholdState();
        NotifyChanged();

        TurnSystem.SubscribeToEndOfTurn(OnEndTurn);
    }

    private void OnDestroy()
    {
        TurnSystem.UnsubscribeFromEndOfTurn(OnEndTurn);
    }

    private void LoadDefaultsFromManager()
    {
        var mgr = BuildingManager.Instance;
        if (mgr == null) return;

        // Prefer explicit override, else read from BuildingControl
        string id = !string.IsNullOrEmpty(buildingIDOverride)
            ? buildingIDOverride
            : GetComponent<BuildingControl>()?.buildingID;

        if (string.IsNullOrEmpty(id)) return;

        var def = mgr.GetBuildingByID(id);
        if (def == null) return;

        maxHealth = Mathf.Max(1, def.defaultMaxHealth);
        degenerationAmount = Mathf.Max(0, def.defaultDegenerationAmount);
        degenerationIntervalTurns = Mathf.Max(1, def.defaultDegenerationIntervalTurns);
        damagedThreshold = Mathf.Clamp01(def.defaultDamagedThreshold);

        // If instance had an out-of-range current, snap to max by default
        currentHealth = def.defaultMaxHealth;

        NotifyChanged();
    }
    
    public void RefreshDefaultsFromManager(string idOverride = null)
    {
        if (!string.IsNullOrEmpty(idOverride))
            buildingIDOverride = idOverride;

        bool prevUse = useManagerDefaults;
        useManagerDefaults = true;
        // call the same logic used in Awake
        // (make LoadDefaultsFromManager internal or keep it private and duplicate the 4 assigns here)
        var mgr = BuildingManager.Instance;
        if (mgr == null) return;

        string id = !string.IsNullOrEmpty(buildingIDOverride)
            ? buildingIDOverride
            : GetComponent<BuildingControl>()?.buildingID;

        if (string.IsNullOrEmpty(id)) { useManagerDefaults = prevUse; return; }

        var def = mgr.GetBuildingByID(id);
        if (def == null) { useManagerDefaults = prevUse; return; }

        maxHealth                 = Mathf.Max(1, def.defaultMaxHealth);
        degenerationAmount        = Mathf.Max(0, def.defaultDegenerationAmount);
        degenerationIntervalTurns = Mathf.Max(1, def.defaultDegenerationIntervalTurns);
        damagedThreshold          = Mathf.Clamp01(def.defaultDamagedThreshold);

        ApplyThresholdState();
        useManagerDefaults = prevUse;
    }

    private void OnEndTurn()
    {
        if (_status.CurrentState == BuildingState.Destroyed) return;
        if (IsDegenerationPaused) return; // <<< NEW: paused while repairing

        _turnsSinceDegenerate++;
        if (_turnsSinceDegenerate >= degenerationIntervalTurns)
        {
            _turnsSinceDegenerate = 0;
            ApplyDamage(degenerationAmount);
        }
    }

    public void SetDegenerationPaused(bool on)
    {
        if (on) _degenerationPauseCounter++;
        else    _degenerationPauseCounter = Mathf.Max(0, _degenerationPauseCounter - 1);
    }

    public void ApplyDamage(int amount)
    {
        if (amount <= 0 || _status.CurrentState == BuildingState.Destroyed) return;

        currentHealth = Mathf.Max(0, currentHealth - amount);
        ApplyThresholdState();
        NotifyChanged();
    }

    public void RepairAbsolute(int amount)
    {
        if (amount <= 0) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        ApplyThresholdState();
        NotifyChanged();
    }

    public void RepairPercent(float pct01)
    {
        if (pct01 <= 0f) return;
        int amount = Mathf.CeilToInt(maxHealth * Mathf.Clamp01(pct01));
        RepairAbsolute(amount);
        NotifyChanged();
    }

    private void ApplyThresholdState()
    {
        if (currentHealth <= 0)
        {
            _status.SetState(BuildingState.Destroyed);
            return;
        }

        float frac = currentHealth / (float)maxHealth;
        if (frac <= damagedThreshold)
            _status.SetState(BuildingState.Damaged);
        else
            _status.SetState(BuildingState.Normal);
    }

    public void ForceRefresh()
    {
        // Re-run thresholds and fire the UI event without changing health
        var method = typeof(BuildingHealth).GetMethod("ApplyThresholdState", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        method?.Invoke(this, null);
        var notify = typeof(BuildingHealth).GetMethod("NotifyChanged", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        notify?.Invoke(this, null);
    }

    public BuildingHealthRuntimeSaveData CaptureRuntimeSaveData()
    {
        return new BuildingHealthRuntimeSaveData
        {
            maxHealth = maxHealth,
            currentHealth = currentHealth,
            degenerationAmount = degenerationAmount,
            degenerationIntervalTurns = degenerationIntervalTurns,
            damagedThreshold = damagedThreshold,
            useManagerDefaults = useManagerDefaults,
            buildingIDOverride = buildingIDOverride,
            turnsSinceDegenerate = _turnsSinceDegenerate,
            degenerationPauseCounter = _degenerationPauseCounter
        };
    }

    public void ApplyRuntimeSaveData(BuildingHealthRuntimeSaveData data)
    {
        if (data == null)
            return;

        maxHealth = Mathf.Max(1, data.maxHealth);
        currentHealth = Mathf.Clamp(data.currentHealth, 0, maxHealth);
        degenerationAmount = Mathf.Max(0, data.degenerationAmount);
        degenerationIntervalTurns = Mathf.Max(1, data.degenerationIntervalTurns);
        damagedThreshold = Mathf.Clamp01(data.damagedThreshold);
        useManagerDefaults = data.useManagerDefaults;
        buildingIDOverride = data.buildingIDOverride;
        _turnsSinceDegenerate = Mathf.Max(0, data.turnsSinceDegenerate);
        _degenerationPauseCounter = Mathf.Max(0, data.degenerationPauseCounter);

        ApplyThresholdState();
        NotifyChanged();
    }
}