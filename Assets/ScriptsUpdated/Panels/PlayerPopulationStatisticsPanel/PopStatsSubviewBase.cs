using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class PopStatsSubviewBase : MonoBehaviour
{
    [Header("Data Sources")]
    public PlayerPopulationStatistic stats;
    public PlayersPopulationManager populationManager;

    protected virtual void Awake()
    {
        if (!stats) stats = FindObjectOfType<PlayerPopulationStatistic>();
        if (!populationManager) populationManager = PlayersPopulationManager.Instance;
    }

    protected virtual void OnEnable()
    {
        SubscribeSources();

        if (isActiveAndEnabled && gameObject.activeInHierarchy)
            RefreshNow();
    }

    protected virtual void OnDisable()
    {
        UnsubscribeSources();
    }

    protected virtual void OnDestroy()
    {
        UnsubscribeSources();
    }

    public void SetDataSources(PlayerPopulationStatistic newStats, PlayersPopulationManager newPopulationManager, bool refreshNow = true)
    {
        bool wasActive = isActiveAndEnabled && gameObject.activeInHierarchy;

        UnsubscribeSources();

        stats = newStats;
        populationManager = newPopulationManager;

        if (wasActive)
            SubscribeSources();

        if (refreshNow && wasActive)
            RefreshNow();
    }

    private void SubscribeSources()
    {
        if (stats != null)
            stats.OnSnapshotRecorded += OnStatsEvent;

        if (populationManager != null)
            populationManager.OnPopulationChanged += OnPopEvent;
    }

    private void UnsubscribeSources()
    {
        if (stats != null)
            stats.OnSnapshotRecorded -= OnStatsEvent;

        if (populationManager != null)
            populationManager.OnPopulationChanged -= OnPopEvent;
    }

    private void OnStatsEvent(PopulationSnapshot _)
    {
        if (isActiveAndEnabled && gameObject.activeInHierarchy)
            RefreshNow();
    }

    private void OnPopEvent()
    {
        if (isActiveAndEnabled && gameObject.activeInHierarchy)
            RefreshNow();
    }

    public abstract void RefreshNow();
}