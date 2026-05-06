using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class PlayerBuildingRulebook : MonoBehaviour
{
    public static PlayerBuildingRulebook Instance { get; private set; }

    [Header("References")]
    [SerializeField] private PlayerBuildingManager playerBuildingManager;

    [Serializable]
    public struct Mod
    {
        public int dMaxHealth;
        public int dDegAmount;
        public int dDegInterval;

        public Mod(int h, int a, int i)
        {
            dMaxHealth = h;
            dDegAmount = a;
            dDegInterval = i;
        }

        public static Mod operator +(Mod x, Mod y)
        {
            return new Mod(
                x.dMaxHealth + y.dMaxHealth,
                x.dDegAmount + y.dDegAmount,
                x.dDegInterval + y.dDegInterval
            );
        }
    }

    private readonly Dictionary<string, Mod> _mods = new(StringComparer.Ordinal);
    private bool _subscribed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void Start()
    {
        // In case the reference is assigned later during startup order.
        TrySubscribe();
    }

    private void OnDisable()
    {
        TryUnsubscribe();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        TryUnsubscribe();
    }

    private void TrySubscribe()
    {
        if (_subscribed)
            return;

        if (playerBuildingManager == null)
            return;

        playerBuildingManager.OnBuildingPlaced += ApplyToRecord;
        _subscribed = true;
    }

    private void TryUnsubscribe()
    {
        if (!_subscribed)
            return;

        if (playerBuildingManager != null)
            playerBuildingManager.OnBuildingPlaced -= ApplyToRecord;

        _subscribed = false;
    }

    public void AddDeltasFor(IReadOnlyList<string> targetIds, Mod delta)
    {
        if (targetIds == null || targetIds.Count == 0)
        {
            _mods[""] = _mods.TryGetValue("", out var cur) ? cur + delta : delta;
            return;
        }

        for (int i = 0; i < targetIds.Count; i++)
        {
            string id = targetIds[i];
            if (string.IsNullOrWhiteSpace(id))
                continue;

            _mods[id] = _mods.TryGetValue(id, out var cur) ? cur + delta : delta;
        }
    }

    public Mod GetTotalFor(string buildingID)
    {
        Mod m = default;

        if (_mods.TryGetValue("", out var all))
            m += all;

        if (!string.IsNullOrWhiteSpace(buildingID) && _mods.TryGetValue(buildingID, out var per))
            m += per;

        return m;
    }

    public void ApplyToAllExisting()
    {
        if (playerBuildingManager == null)
            return;

        IReadOnlyList<PlayerBuildingManager.Record> all = playerBuildingManager.GetAll();
        for (int i = 0; i < all.Count; i++)
            ApplyToRecord(all[i]);
    }

    private void ApplyToRecord(PlayerBuildingManager.Record rec)
    {
        if (rec == null || rec.definition == null || !rec.instance)
            return;

        BuildingHealth bh = rec.instance.GetComponent<BuildingHealth>();
        if (!bh)
            bh = rec.instance.GetComponentInChildren<BuildingHealth>(true);

        if (!bh)
            return;

        Mod mods = GetTotalFor(rec.definition.buildingID);

        int targetMaxH = Mathf.Max(1, rec.definition.defaultMaxHealth + mods.dMaxHealth);
        int targetDegAmt = Mathf.Max(0, rec.definition.defaultDegenerationAmount + mods.dDegAmount);
        int targetDegInt = Mathf.Max(1, rec.definition.defaultDegenerationIntervalTurns + mods.dDegInterval);

        bh.maxHealth = targetMaxH;
        bh.degenerationAmount = targetDegAmt;
        bh.degenerationIntervalTurns = targetDegInt;

        if (bh.CurrentHealth > bh.maxHealth)
            SetPrivateInt(bh, "currentHealth", bh.maxHealth);

        bh.ForceRefresh();
    }

    private static void SetPrivateInt(object obj, string field, int value)
    {
        if (obj == null)
            return;

        FieldInfo fi = obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
        if (fi != null && fi.FieldType == typeof(int))
            fi.SetValue(obj, value);
    }
}