using System.Collections.Generic;
using UnityEngine;

public class BuildingTickManager : MonoBehaviour
{
    public static BuildingTickManager Instance { get; private set; }

    private readonly List<BuildingHealth>          _healths   = new List<BuildingHealth>();
    private readonly List<BuildingStatus>          _statuses  = new List<BuildingStatus>();
    private readonly List<BuildingRepair>          _repairs   = new List<BuildingRepair>();
    private readonly List<StorageBuildingControl>  _storage   = new List<StorageBuildingControl>();
    private readonly List<ReligiousBuildingControl> _religious = new List<ReligiousBuildingControl>();
    private readonly List<TradeBuildingControl>    _trade     = new List<TradeBuildingControl>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnEnable()  => TurnSystem.SubscribeToEndOfTurn(OnEndTurn);
    private void OnDisable() => TurnSystem.UnsubscribeFromEndOfTurn(OnEndTurn);

    public void Register(BuildingHealth h)            { if (h != null && !_healths.Contains(h))    _healths.Add(h); }
    public void Register(BuildingStatus s)            { if (s != null && !_statuses.Contains(s))   _statuses.Add(s); }
    public void Register(BuildingRepair r)            { if (r != null && !_repairs.Contains(r))    _repairs.Add(r); }
    public void Register(StorageBuildingControl s)    { if (s != null && !_storage.Contains(s))    _storage.Add(s); }
    public void Register(ReligiousBuildingControl r)  { if (r != null && !_religious.Contains(r))  _religious.Add(r); }
    public void Register(TradeBuildingControl t)      { if (t != null && !_trade.Contains(t))      _trade.Add(t); }

    public void Unregister(BuildingHealth h)            { _healths.Remove(h); }
    public void Unregister(BuildingStatus s)            { _statuses.Remove(s); }
    public void Unregister(BuildingRepair r)            { _repairs.Remove(r); }
    public void Unregister(StorageBuildingControl s)    { _storage.Remove(s); }
    public void Unregister(ReligiousBuildingControl r)  { _religious.Remove(r); }
    public void Unregister(TradeBuildingControl t)      { _trade.Remove(t); }

    private void OnEndTurn()
    {
        TickList(_healths);
        TickList(_statuses);
        TickList(_repairs);
        TickList(_storage);
        TickList(_religious);
        TickList(_trade);
    }

    private static void TickList<T>(List<T> list) where T : MonoBehaviour, IBuildingTurnTickable
    {
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var item = list[i];
            if (item == null) { list.RemoveAt(i); continue; }
            item.TurnTick();
        }
    }
}
