using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central ScriptableObject for all environment task calculations.
/// Replaces the hardcoded static calculator classes with inspector-editable data.
///
/// Create via: Assets > Create > Game > Environment Calculations
/// Place in a Resources folder (name it "EnvironmentCalculations") for auto-loading,
/// or reference it from any MonoBehaviour to trigger self-registration.
/// </summary>
[CreateAssetMenu(fileName = "EnvironmentCalculations", menuName = "Game/Environment Calculations")]
public class EnvironmentCalculations : ScriptableObject
{
    // ------------------------------------------------------------------
    // Serializable data types
    // ------------------------------------------------------------------

    [Serializable]
    public class EnvironmentTypeSettings
    {
        public EnvironmentType environmentType;

        [Header("Discovery")]
        public int discoveryBaseTurns = 10;
        [Range(0, 100)] public int discoveryBaseFailureChance = 30;
        public float discoveryPopEnvMultiplier = 2f;
        public float discoveryPenaltyEnvMultiplier = 1f;
        public int discoveryPopPenaltyOnFailure = 2;

        [Header("Gathering")]
        public int gatheringBaseTurns = 8;
        [Range(0, 100)] public int gatheringBaseFailureChance = 0;
        public float gatheringPopEnvMultiplier = 1.5f;
        public int gatheringPopPenaltyOnFailure = 0;

        [Header("Survey")]
        public int surveyBaseTurns = 4;
        public float surveyPopEnvMultiplier = 1f;
        public int resurveyIntervalBase = 10;

        [Header("Fire")]
        public bool canCatchFire = true;
        public int baseBurnTurns = 4;
        [Range(0f, 1f)] public float baseDryness01 = 0.5f;
        [Range(0f, 1f)] public float maxDryness01 = 0.85f;
        [Range(0.05f, 3f)] public float fireIgnitionMultiplier = 1f;
        [Range(0.05f, 3f)] public float burnSpeedMultiplier = 1f;
    }

    [Serializable]
    public class TileTypeModifierEntry
    {
        public EnvironmentTileType tileType;

        [Header("Turn Multipliers")]
        public float discoveryTurnMultiplier = 1f;
        public float gatheringTurnMultiplier = 1f;
        public float surveyTurnMultiplier = 1f;
        public float resurveyTurnMultiplier = 1f;

        [Header("Failure & Population")]
        public int discoveryFailureModifier = 0;
        public float discoveryPopTileMultiplier = 1f;
        public float discoveryPenaltyTileMultiplier = 1f;
        public float gatheringPopTileMultiplier = 1f;

        [Header("Fire Override (overridesFire must be true)")]
        public bool overridesFire = false;
        public bool tileCanCatchFire = false;
        public int tileBaseBurnTurns = 0;
        [Range(0f, 1f)] public float tileBaseDryness01 = 0f;
        [Range(0f, 1f)] public float tileMaxDryness01 = 0f;
        [Range(0f, 3f)] public float tileFireIgnitionMultiplier = 0f;
        [Range(0f, 3f)] public float tileBurnSpeedMultiplier = 0f;
    }

    [Serializable]
    public class TileSizeEntry
    {
        public TileSize tileSize;

        [Header("Turn Multipliers")]
        public float discoveryTurnMultiplier = 1f;
        public float gatheringTurnMultiplier = 1f;
        public float surveyTurnMultiplier = 1f;
        public float resurveyTurnMultiplier = 1f;

        [Header("Failure Modifier (additive)")]
        public int discoveryFailureSizeModifier = 0;

        [Header("Population Base Values")]
        public int discoveryPopBase = 4;
        public int gatheringPopBase = 2;
        public int surveyPopBase = 3;
        public int discoveryPenaltyBase = 2;
    }

    // ------------------------------------------------------------------
    // Inspector fields
    // ------------------------------------------------------------------

    [Header("Global")]
    [Range(0f, 0.3f)]
    [Tooltip("±variance applied to all turn calculations (0.10 = ±10%).")]
    public float turnVariance = 0.10f;

    [Space]
    public List<EnvironmentTypeSettings> environmentSettings = new();
    public List<TileTypeModifierEntry> tileModifiers = new();
    public List<TileSizeEntry> sizeSettings = new();

    // ------------------------------------------------------------------
    // Singleton
    // ------------------------------------------------------------------

    private static EnvironmentCalculations _instance;
    public static EnvironmentCalculations Instance => _instance;

    private void OnEnable()
    {
        _instance = this;
        BuildCaches();
    }

    private void OnDisable()
    {
        if (_instance == this) _instance = null;
    }

    private void OnValidate() => BuildCaches();

    // ------------------------------------------------------------------
    // Dictionary caches (built once, O(1) lookup at runtime)
    // ------------------------------------------------------------------

    private Dictionary<EnvironmentType, EnvironmentTypeSettings> _envCache;
    private Dictionary<EnvironmentTileType, TileTypeModifierEntry> _tileCache;
    private Dictionary<TileSize, TileSizeEntry> _sizeCache;

    private void BuildCaches()
    {
        _envCache = new Dictionary<EnvironmentType, EnvironmentTypeSettings>();
        if (environmentSettings != null)
            for (int i = 0; i < environmentSettings.Count; i++)
            {
                var e = environmentSettings[i];
                if (e != null) _envCache[e.environmentType] = e;
            }

        _tileCache = new Dictionary<EnvironmentTileType, TileTypeModifierEntry>();
        if (tileModifiers != null)
            for (int i = 0; i < tileModifiers.Count; i++)
            {
                var t = tileModifiers[i];
                if (t != null) _tileCache[t.tileType] = t;
            }

        _sizeCache = new Dictionary<TileSize, TileSizeEntry>();
        if (sizeSettings != null)
            for (int i = 0; i < sizeSettings.Count; i++)
            {
                var s = sizeSettings[i];
                if (s != null) _sizeCache[s.tileSize] = s;
            }
    }

    // ------------------------------------------------------------------
    // Lookup helpers
    // ------------------------------------------------------------------

    private EnvironmentTypeSettings Env(EnvironmentType t) =>
        _envCache != null && _envCache.TryGetValue(t, out var e) ? e : null;

    private TileTypeModifierEntry Tile(EnvironmentTileType t) =>
        _tileCache != null && _tileCache.TryGetValue(t, out var m) ? m : null;

    private TileSizeEntry Size(TileSize s) =>
        _sizeCache != null && _sizeCache.TryGetValue(s, out var sz) ? sz : null;

    private float Vary(float v) =>
        v * UnityEngine.Random.Range(1f - turnVariance, 1f + turnVariance);

    // ------------------------------------------------------------------
    // Discovery
    // ------------------------------------------------------------------

    public int GetDiscoveryTurns(EnvironmentType env, EnvironmentTileType tile, TileSize size)
    {
        var e = Env(env); var t = Tile(tile); var s = Size(size);
        float raw = (e?.discoveryBaseTurns ?? 10)
                  * (t?.discoveryTurnMultiplier ?? 1f)
                  * (s?.discoveryTurnMultiplier ?? 1f);
        return Mathf.Max(1, Mathf.CeilToInt(Vary(raw)));
    }

    public int GetDiscoveryFailureChance(EnvironmentType env, EnvironmentTileType tile, TileSize size)
    {
        var e = Env(env); var t = Tile(tile); var s = Size(size);
        int raw = (e?.discoveryBaseFailureChance ?? 30)
                + (t?.discoveryFailureModifier ?? 0)
                + (s?.discoveryFailureSizeModifier ?? 0);
        return Mathf.Clamp(raw, 1, 95);
    }

    public int GetDiscoveryRequiredPop(EnvironmentType env, EnvironmentTileType tile, TileSize size)
    {
        var e = Env(env); var t = Tile(tile); var s = Size(size);
        float raw = (s?.discoveryPopBase ?? 4)
                  * (e?.discoveryPopEnvMultiplier ?? 1f)
                  * (t?.discoveryPopTileMultiplier ?? 1f);
        return Mathf.Max(1, Mathf.CeilToInt(raw));
    }

    public int GetDiscoveryPopPenalty(EnvironmentType env, EnvironmentTileType tile, TileSize size)
    {
        var e = Env(env); var t = Tile(tile); var s = Size(size);
        float raw = (s?.discoveryPenaltyBase ?? 2)
                  * (e?.discoveryPenaltyEnvMultiplier ?? 1f)
                  * (t?.discoveryPenaltyTileMultiplier ?? 1f);
        return Mathf.Clamp(Mathf.CeilToInt(raw), 1, 10);
    }

    // ------------------------------------------------------------------
    // Gathering
    // ------------------------------------------------------------------

    public int GetGatheringTurns(EnvironmentType env, EnvironmentTileType tile, TileSize size)
    {
        var e = Env(env); var t = Tile(tile); var s = Size(size);
        float raw = (e?.gatheringBaseTurns ?? 8)
                  * (t?.gatheringTurnMultiplier ?? 1f)
                  * (s?.gatheringTurnMultiplier ?? 1f);
        return Mathf.Max(1, Mathf.CeilToInt(Vary(raw)));
    }

    public int GetGatheringFailureChance(EnvironmentType env, EnvironmentTileType tile, TileSize size)
    {
        var e = Env(env);
        return Mathf.Clamp(e?.gatheringBaseFailureChance ?? 0, 0, 100);
    }

    public int GetGatheringRequiredPop(EnvironmentType env, EnvironmentTileType tile, TileSize size)
    {
        var e = Env(env); var t = Tile(tile); var s = Size(size);
        float raw = (s?.gatheringPopBase ?? 2)
                  * (e?.gatheringPopEnvMultiplier ?? 1f)
                  * (t?.gatheringPopTileMultiplier ?? 1f);
        return Mathf.Max(1, Mathf.CeilToInt(raw));
    }

    public int GetGatheringPopPenalty(EnvironmentType env, EnvironmentTileType tile, TileSize size)
    {
        var e = Env(env);
        return Mathf.Max(0, e?.gatheringPopPenaltyOnFailure ?? 0);
    }

    // ------------------------------------------------------------------
    // Survey
    // ------------------------------------------------------------------

    public int GetSurveyTurns(EnvironmentType env, EnvironmentTileType tile, TileSize size)
    {
        var e = Env(env); var t = Tile(tile); var s = Size(size);
        float raw = (e?.surveyBaseTurns ?? 4)
                  * (t?.surveyTurnMultiplier ?? 1f)
                  * (s?.surveyTurnMultiplier ?? 1f);
        return Mathf.Max(1, Mathf.CeilToInt(Vary(raw)));
    }

    public int GetSurveyRequiredPop(EnvironmentType env, EnvironmentTileType tile, TileSize size)
    {
        var e = Env(env); var s = Size(size);
        float raw = (s?.surveyPopBase ?? 3) * (e?.surveyPopEnvMultiplier ?? 1f);
        return Mathf.Max(1, Mathf.CeilToInt(raw));
    }

    public int GetResurveyInterval(EnvironmentType env, EnvironmentTileType tile, TileSize size)
    {
        var e = Env(env); var t = Tile(tile); var s = Size(size);
        float raw = (e?.resurveyIntervalBase ?? 10)
                  * (t?.resurveyTurnMultiplier ?? 1f)
                  * (s?.resurveyTurnMultiplier ?? 1f);
        return Mathf.Max(1, Mathf.CeilToInt(Vary(raw)));
    }

    // ------------------------------------------------------------------
    // Fire defaults
    // ------------------------------------------------------------------

    /// <summary>
    /// Writes fire parameters onto <paramref name="target"/>.
    /// Tile-type overrides take priority over environment-type defaults.
    /// </summary>
    public void ApplyFireDefaults(EnvironmentControl target, EnvironmentType envType, EnvironmentTileType tileType)
    {
        var e = Env(envType);
        bool canFire      = e?.canCatchFire ?? true;
        int burnTurns     = e?.baseBurnTurns ?? 4;
        float dryness     = e?.baseDryness01 ?? 0.5f;
        float maxDryness  = e?.maxDryness01 ?? 0.85f;
        float ignition    = e?.fireIgnitionMultiplier ?? 1f;
        float burnSpeed   = e?.burnSpeedMultiplier ?? 1f;

        var t = Tile(tileType);
        if (t != null && t.overridesFire)
        {
            canFire     = t.tileCanCatchFire;
            burnTurns   = t.tileBaseBurnTurns;
            dryness     = t.tileBaseDryness01;
            maxDryness  = t.tileMaxDryness01;
            ignition    = t.tileFireIgnitionMultiplier;
            burnSpeed   = t.tileBurnSpeedMultiplier;
        }

        target.canCatchFire           = canFire;
        target.baseBurnTurns          = burnTurns;
        target.baseDryness01          = dryness;
        target.maxDryness01           = maxDryness;
        target.fireIgnitionMultiplier = ignition;
        target.burnSpeedMultiplier    = burnSpeed;
    }
}
