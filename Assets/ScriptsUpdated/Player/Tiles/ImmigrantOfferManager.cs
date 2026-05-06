using System;
using System.Collections.Generic;
using UnityEngine;

public class ImmigrantOfferManager : MonoBehaviour
{
    public static ImmigrantOfferManager Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private ImmigrantOfferPanel panel; // assigned at runtime from UISetup

    private readonly Queue<ImmigrantOffer> _queue = new();
    private ImmigrantOffer _current;
    private bool _showing;

    [Serializable]
    public class ImmigrantOffer
    {
        public EnvironmentTaskKind kind;
        public EnvironmentControl env;
        public string envNameFallback;

        public TaskSuccessPopulationRewardConfig cfg;

        public bool isNewFamily;
        public int adults;
        public int children;
        public int individuals;
    }

    public ImmigrantOfferPanel Panel => panel;

    private void Awake()
    {
        if (Instance && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public int QueueCount => _queue.Count + (_showing ? 1 : 0);

    public void SetPanel(ImmigrantOfferPanel newPanel, bool refreshNow = true)
    {
        panel = newPanel;

        if (!refreshNow)
            return;

        if (_showing && _current != null && panel != null)
        {
            panel.Show(_current, OnAcceptCurrent, OnDeclineCurrent, QueueCount - 1);
            return;
        }

        TryShowNext();
    }

    public void EnqueueNewFamily(EnvironmentTaskKind kind, EnvironmentControl env, TaskSuccessPopulationRewardConfig cfg, int adults, int children)
    {
        if (cfg == null) return;

        _queue.Enqueue(new ImmigrantOffer
        {
            kind = kind,
            env = env,
            envNameFallback = env != null ? env.environmentName : "Unknown",
            cfg = cfg,
            isNewFamily = true,
            adults = Mathf.Max(1, adults),
            children = Mathf.Max(0, children),
            individuals = 0
        });

        TryShowNext();
    }

    public void EnqueueIndividuals(EnvironmentTaskKind kind, EnvironmentControl env, TaskSuccessPopulationRewardConfig cfg, int count)
    {
        if (cfg == null) return;

        _queue.Enqueue(new ImmigrantOffer
        {
            kind = kind,
            env = env,
            envNameFallback = env != null ? env.environmentName : "Unknown",
            cfg = cfg,
            isNewFamily = false,
            adults = 0,
            children = 0,
            individuals = Mathf.Max(1, count)
        });

        TryShowNext();
    }

    private void TryShowNext()
    {
        if (_showing) return;
        if (_queue.Count == 0) return;

        if (panel == null)
        {
            Debug.LogWarning("[ImmigrantOfferManager] No panel assigned.");
            return;
        }

        _current = _queue.Dequeue();
        _showing = true;

        panel.Show(_current, OnAcceptCurrent, OnDeclineCurrent, QueueCount - 1);
    }

    private void OnAcceptCurrent()
    {
        ApplyOffer(_current);
        _showing = false;
        TryShowNext();
        if (!_showing) panel?.Hide();
    }

    private void OnDeclineCurrent()
    {
        _showing = false;
        TryShowNext();
        if (!_showing) panel?.Hide();
    }

    private void ApplyOffer(ImmigrantOffer offer)
    {
        if (offer == null) return;

        var famSim = PlayerFamilySimulationManager.Instance;
        if (famSim == null)
        {
            Debug.LogWarning("[ImmigrantOfferManager] No PlayerFamilySimulationManager.");
            return;
        }

        int addedTotal = 0;
        string familyId = null;

        if (offer.isNewFamily)
        {
            if (!famSim.TryAddImmigrantFamily(offer.adults, offer.children, offer.cfg, out addedTotal, out familyId))
            {
                Debug.Log("[ImmigrantOfferManager] Accept failed (capacity / sim constraints).");
                return;
            }

            EnvironmentTaskRewardManager.Instance?.TryHouseFamilyPublic(familyId, famSim);
            Debug.Log($"[Immigrants] Accepted family {familyId} (+{addedTotal}).");
        }
        else
        {
            if (!famSim.TryAddImmigrantIndividuals(offer.individuals, offer.cfg, out addedTotal))
            {
                Debug.Log("[ImmigrantOfferManager] Accept failed (capacity / sim constraints).");
                return;
            }

            Debug.Log($"[Immigrants] Accepted individuals (+{addedTotal}).");
        }

        PlayersPopulationManager.Instance?.ForceSyncUI();
    }
}