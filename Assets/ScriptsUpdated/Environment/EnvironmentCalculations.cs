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

        public override string ToString() => environmentType.ToString();
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

        public override string ToString() => tileType.ToString();
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

        public override string ToString() => tileSize.ToString();
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
    // Preset population (right-click SO in Inspector > "Populate Defaults")
    // ------------------------------------------------------------------

    private void Reset() => PopulateDefaults();

    [ContextMenu("Populate Defaults")]
    public void PopulateDefaults()
    {
        environmentSettings = new List<EnvironmentTypeSettings>
        {
            Env(EnvironmentType.Desert,          12, 60, 2.3f, 1.3f, 2,  8, 0, 1.6f, 0, 4, 12,  true, 1, 0.95f, 1.00f, 0.15f, 0.40f),
            Env(EnvironmentType.Grassland,       11, 20, 2.0f, 1.0f, 2,  7, 0, 1.1f, 0, 2,  7,  true, 3, 0.80f, 1.00f, 1.35f, 1.45f),
            Env(EnvironmentType.Savanna,         11, 25, 2.0f, 1.0f, 2,  7, 0, 1.2f, 0, 3,  8,  true, 3, 0.80f, 1.00f, 1.35f, 1.45f),
            Env(EnvironmentType.TemperateForest, 13, 30, 2.9f, 0.9f, 2,  9, 0, 1.3f, 0, 3,  9,  true, 5, 0.50f, 0.80f, 1.00f, 1.00f),
            Env(EnvironmentType.BorealForest,    13, 40, 2.9f, 0.85f,2,  7, 0, 1.5f, 0, 5, 14,  true, 5, 0.50f, 0.80f, 1.00f, 1.00f),
            Env(EnvironmentType.TropicalForest,  15, 60, 2.8f, 0.8f, 2, 10, 0, 1.8f, 0, 6, 16,  true, 6, 0.55f, 0.75f, 1.20f, 1.10f),
            Env(EnvironmentType.SubTropical,     15, 50, 2.3f, 1.4f, 2, 10, 0, 1.6f, 0, 5, 13,  true, 5, 0.50f, 0.80f, 1.00f, 1.00f),
            Env(EnvironmentType.Lake,            16, 60, 2.2f, 1.2f, 2, 10, 0, 1.4f, 0, 4, 12,  true, 4, 0.50f, 0.85f, 1.00f, 1.00f),
            Env(EnvironmentType.Tundra,          22, 60, 2.3f, 1.2f, 2, 15, 0, 1.4f, 0, 5, 14,  true, 2, 0.30f, 0.55f, 0.40f, 0.60f),
            Env(EnvironmentType.Mountain,        30, 80, 2.4f, 1.2f, 2, 20, 0, 1.5f, 0, 5, 15,  true, 4, 0.50f, 0.85f, 1.00f, 1.00f),
            Env(EnvironmentType.Ocean,           17, 60, 3.0f, 1.5f, 2, 30, 0, 1.9f, 0, 7, 18,  true, 4, 0.50f, 0.85f, 1.00f, 1.00f),
            Env(EnvironmentType.SaltLake,        10, 20, 2.25f,0.75f,2, 12, 0, 1.25f,0, 8, 10,  true, 4, 0.50f, 0.85f, 1.00f, 1.00f),
        };

        // Tile type modifiers — turn multipliers for discovery/gathering are identical in the original calculators.
        // Water tiles: overridesFire=true, canCatchFire=false.
        // Beach/Mountain-type tiles: overridesFire=true with low ignition values.
        tileModifiers = new List<TileTypeModifierEntry>
        {
            //                              tileType              discTrn gthTrn  srvTrn  rsvTrn  discFail discPop  discPen  gthPop  override  canFire  brn  dry    maxD   ign    spd
            Tile(EnvironmentTileType.Land,             0.80f, 0.80f, 1.00f, 1.00f,   0,  1.0f,  1.0f,  1.0f,   false, false, 0, 0f,   0f,   0f,   0f),
            Tile(EnvironmentTileType.River,            0.90f, 0.90f, 1.20f, 1.10f,  -2,  1.2f,  1.1f,  1.1f,   true,  false, 0, 0f,   0f,   0f,   0f),
            Tile(EnvironmentTileType.RiverCorner,      0.95f, 0.95f, 1.20f, 1.10f,  -1,  1.2f,  1.1f,  1.1f,   true,  false, 0, 0f,   0f,   0f,   0f),
            Tile(EnvironmentTileType.RiverSplit,       1.00f, 1.00f, 1.30f, 1.20f,   1,  1.3f,  1.2f,  1.2f,   true,  false, 0, 0f,   0f,   0f,   0f),
            Tile(EnvironmentTileType.RiverCross,       1.10f, 1.10f, 1.30f, 1.20f,   1,  1.5f,  1.4f,  1.3f,   true,  false, 0, 0f,   0f,   0f,   0f),
            Tile(EnvironmentTileType.RiverEnd,         0.95f, 0.95f, 1.10f, 1.10f,  -1,  1.3f,  1.2f,  1.1f,   true,  false, 0, 0f,   0f,   0f,   0f),
            Tile(EnvironmentTileType.RiverMouth,       1.00f, 1.00f, 1.30f, 1.20f,   0,  1.3f,  1.2f,  1.2f,   true,  false, 0, 0f,   0f,   0f,   0f),
            Tile(EnvironmentTileType.Water,            0.75f, 0.75f, 1.10f, 1.10f,  -2,  1.5f,  1.2f,  1.2f,   true,  false, 0, 0f,   0f,   0f,   0f),
            Tile(EnvironmentTileType.Lake,             1.15f, 1.15f, 1.30f, 1.30f,   2,  1.7f,  1.25f, 1.25f,  true,  false, 0, 0f,   0f,   0f,   0f),
            Tile(EnvironmentTileType.LakeEdge,         1.00f, 1.00f, 1.30f, 1.30f,  -1,  1.1f,  1.1f,  1.1f,   true,  false, 0, 0f,   0f,   0f,   0f),
            Tile(EnvironmentTileType.LakeCorner,       1.05f, 1.05f, 1.30f, 1.30f,  -1,  1.1f,  1.1f,  1.1f,   true,  false, 0, 0f,   0f,   0f,   0f),
            Tile(EnvironmentTileType.LakeMouth,        1.00f, 1.00f, 1.30f, 1.20f,   0,  1.3f,  1.2f,  1.2f,   true,  false, 0, 0f,   0f,   0f,   0f),
            Tile(EnvironmentTileType.Ocean,            1.40f, 1.40f, 2.50f, 1.60f,   5,  2.0f,  1.5f,  1.5f,   true,  false, 0, 0f,   0f,   0f,   0f),
            Tile(EnvironmentTileType.Coastline,        1.10f, 1.10f, 1.10f, 1.20f,  -1,  1.1f,  1.0f,  1.2f,   false, false, 0, 0f,   0f,   0f,   0f),
            Tile(EnvironmentTileType.CoastlineCorner,  1.10f, 1.10f, 1.20f, 1.20f,   0,  1.1f,  1.0f,  1.2f,   false, false, 0, 0f,   0f,   0f,   0f),
            Tile(EnvironmentTileType.Mountain,         1.45f, 1.45f, 1.50f, 1.40f,   3,  2.0f,  1.3f,  1.5f,   true,  true,  1, 0.70f,0.95f,0.20f,0.50f),
            Tile(EnvironmentTileType.Cave,             0.80f, 0.80f, 2.00f, 1.50f,  -5,  1.1f,  2.0f,  1.6f,   false, false, 0, 0f,   0f,   0f,   0f),
            Tile(EnvironmentTileType.SaltLake,         1.15f, 1.15f, 1.75f, 1.20f,   2,  1.5f,  1.25f, 1.25f,  false, false, 0, 0f,   0f,   0f,   0f),
        };

        sizeSettings = new List<TileSizeEntry>
        {
            //                  size              discTrn  gthTrn  srvTrn  rsvTrn  discFail  discPop  gthPop  srvPop  penBase
            Size(TileSize.Tiny,    0.75f, 0.75f, 0.50f, 2.50f,  2,   2,  1,  2,  1),
            Size(TileSize.Small,   1.50f, 1.50f, 0.75f, 2.00f,  3,   4,  2,  3,  1),
            Size(TileSize.Medium,  3.00f, 3.00f, 1.00f, 1.50f,  4,   8,  4,  5,  2),
            Size(TileSize.Large,   6.00f, 6.00f, 1.25f, 1.25f,  5,  16,  8,  8,  3),
            Size(TileSize.Giant,  12.00f,12.00f, 1.50f, 0.75f,  6,  32, 10, 12,  4),
            Size(TileSize.Massive,24.00f,24.00f, 2.00f, 0.50f,  7,  64, 20, 20,  5),
        };

        BuildCaches();

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    // Builder helpers used by PopulateDefaults only.
    private static EnvironmentTypeSettings Env(
        EnvironmentType type,
        int discTurns, int discFail, float discPopMult, float discPenMult, int discPenOnFail,
        int gthTurns,  int gthFail,  float gthPopMult,  int gthPenOnFail,
        int srvTurns,  int resurveyBase,
        bool canFire, int burnTurns, float dryness, float maxDry, float ignition, float burnSpd)
    {
        return new EnvironmentTypeSettings
        {
            environmentType              = type,
            discoveryBaseTurns           = discTurns,
            discoveryBaseFailureChance   = discFail,
            discoveryPopEnvMultiplier    = discPopMult,
            discoveryPenaltyEnvMultiplier= discPenMult,
            discoveryPopPenaltyOnFailure = discPenOnFail,
            gatheringBaseTurns           = gthTurns,
            gatheringBaseFailureChance   = gthFail,
            gatheringPopEnvMultiplier    = gthPopMult,
            gatheringPopPenaltyOnFailure = gthPenOnFail,
            surveyBaseTurns              = srvTurns,
            resurveyIntervalBase         = resurveyBase,
            canCatchFire                 = canFire,
            baseBurnTurns                = burnTurns,
            baseDryness01                = dryness,
            maxDryness01                 = maxDry,
            fireIgnitionMultiplier       = ignition,
            burnSpeedMultiplier          = burnSpd,
        };
    }

    private static TileTypeModifierEntry Tile(
        EnvironmentTileType type,
        float discTrn, float gthTrn, float srvTrn, float rsvTrn,
        int discFail,
        float discPop, float discPen, float gthPop,
        bool overFire, bool canFire, int burnTurns,
        float dryness, float maxDry, float ignition, float burnSpd)
    {
        return new TileTypeModifierEntry
        {
            tileType                    = type,
            discoveryTurnMultiplier     = discTrn,
            gatheringTurnMultiplier     = gthTrn,
            surveyTurnMultiplier        = srvTrn,
            resurveyTurnMultiplier      = rsvTrn,
            discoveryFailureModifier    = discFail,
            discoveryPopTileMultiplier  = discPop,
            discoveryPenaltyTileMultiplier = discPen,
            gatheringPopTileMultiplier  = gthPop,
            overridesFire               = overFire,
            tileCanCatchFire            = canFire,
            tileBaseBurnTurns           = burnTurns,
            tileBaseDryness01           = dryness,
            tileMaxDryness01            = maxDry,
            tileFireIgnitionMultiplier  = ignition,
            tileBurnSpeedMultiplier     = burnSpd,
        };
    }

    private static TileSizeEntry Size(
        TileSize size,
        float discTrn, float gthTrn, float srvTrn, float rsvTrn,
        int discFail,
        int discPop, int gthPop, int srvPop, int penBase)
    {
        return new TileSizeEntry
        {
            tileSize                    = size,
            discoveryTurnMultiplier     = discTrn,
            gatheringTurnMultiplier     = gthTrn,
            surveyTurnMultiplier        = srvTrn,
            resurveyTurnMultiplier      = rsvTrn,
            discoveryFailureSizeModifier= discFail,
            discoveryPopBase            = discPop,
            gatheringPopBase            = gthPop,
            surveyPopBase               = srvPop,
            discoveryPenaltyBase        = penBase,
        };
    }

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
