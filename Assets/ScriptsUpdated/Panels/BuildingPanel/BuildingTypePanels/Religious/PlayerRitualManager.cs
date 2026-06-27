using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerRitualManager : MonoBehaviour
{
    [Serializable]
    public class PendingSummoningChoice
    {
        public string requestId;
        public BeliefSystemType beliefSystem;
        public ReligionRitualDefinitionSO sourceRitual;
        public ReligiousBuildingControl sourceBuilding;
        public int maxChoices = 3;
        public List<SpiritDefinitionSO> offeredSpirits = new List<SpiritDefinitionSO>();
        public bool consumed;
    }

    public static PlayerRitualManager Instance { get; private set; }

    [Header("Spirit Database (optional fallback)")]
    [Tooltip("Optional spirit database. Used if known spirits do not produce enough choices.")]
    [SerializeField] private List<SpiritDefinitionSO> spiritDatabase = new List<SpiritDefinitionSO>();

    [Header("UI")]
    [SerializeField] private SummoningSpiritOfferPanelControl summoningOfferPanel;

    [Header("Rules")]
    [SerializeField, Min(1)] private int defaultSummoningChoiceCount = 3;
    [SerializeField] private bool avoidAlreadyAcceptedSpirits = true;
    [SerializeField] private bool avoidSpiritsAlreadyAffiliatedToBuilding = true;

    private readonly Queue<PendingSummoningChoice> _pendingQueue = new Queue<PendingSummoningChoice>();
    private readonly List<SpiritDefinitionSO> _tmpCandidates = new List<SpiritDefinitionSO>(32);
    private readonly HashSet<SpiritDefinitionSO> _tmpUnique = new HashSet<SpiritDefinitionSO>();

    private PendingSummoningChoice _activeChoice;

    public static bool TutorialBypassSpiritFilter = false;

    public PendingSummoningChoice ActiveChoice => _activeChoice;
    public bool HasActiveChoice => _activeChoice != null && !_activeChoice.consumed;

    public event Action PendingChoicesChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        TryShowNextPendingChoice();
    }

    public bool EnqueueSummoningChoice(
        ReligionRitualDefinitionSO ritual,
        ReligiousBuildingControl sourceBuilding,
        out PendingSummoningChoice created)
    {
        created = null;

        if (ritual == null)
            return false;

        PendingSummoningChoice request = new PendingSummoningChoice
        {
            requestId = Guid.NewGuid().ToString(),
            beliefSystem = ritual.beliefSystem,
            sourceRitual = ritual,
            sourceBuilding = sourceBuilding,
            maxChoices = Mathf.Max(1, defaultSummoningChoiceCount)
        };

        BuildSummoningChoices(request);

        if (request.offeredSpirits.Count <= 0)
            return false;

        _pendingQueue.Enqueue(request);
        created = request;

        PendingChoicesChanged?.Invoke();
        TryShowNextPendingChoice();
        return true;
    }

    public void TryShowNextPendingChoice()
    {
        if (HasActiveChoice)
            return;

        while (_pendingQueue.Count > 0)
        {
            PendingSummoningChoice next = _pendingQueue.Dequeue();
            if (next == null || next.consumed || next.offeredSpirits == null || next.offeredSpirits.Count == 0)
                continue;

            _activeChoice = next;
            PendingChoicesChanged?.Invoke();

            if (summoningOfferPanel != null)
                summoningOfferPanel.OpenFor(next);

            return;
        }

        _activeChoice = null;
        PendingChoicesChanged?.Invoke();
    }

    public bool TryAcceptSummoningChoice(SpiritDefinitionSO chosenSpirit, out string reason)
    {
        reason = null;

        if (!HasActiveChoice)
        {
            reason = "There is no active summoning choice.";
            return false;
        }

        if (chosenSpirit == null)
        {
            reason = "Chosen spirit is null.";
            return false;
        }

        if (_activeChoice.offeredSpirits == null || !_activeChoice.offeredSpirits.Contains(chosenSpirit))
        {
            reason = "That spirit is not part of the current summoning offer.";
            return false;
        }

        PlayerReligionManager religion = PlayerReligionManager.Instance;
        if (religion == null)
        {
            reason = "PlayerReligionManager is missing.";
            return false;
        }

        if (!religion.IsAccepted(chosenSpirit))
        {
            if (!religion.TryAcceptSpirit(chosenSpirit, out reason))
                return false;
        }

        if (_activeChoice.sourceBuilding != null)
        {
            if (!_activeChoice.sourceBuilding.TryAffiliateSpirit(chosenSpirit, out reason))
                return false;
        }

        _activeChoice.consumed = true;

        if (summoningOfferPanel != null)
            summoningOfferPanel.Hide();

        _activeChoice = null;
        PendingChoicesChanged?.Invoke();

        TryShowNextPendingChoice();
        return true;
    }

    public void DeclineActiveSummoningChoice()
    {
        if (!HasActiveChoice)
            return;

        _activeChoice.consumed = true;

        if (summoningOfferPanel != null)
            summoningOfferPanel.Hide();

        _activeChoice = null;
        PendingChoicesChanged?.Invoke();

        TryShowNextPendingChoice();
    }

    private void BuildSummoningChoices(PendingSummoningChoice request)
    {
        request.offeredSpirits.Clear();
        _tmpCandidates.Clear();
        _tmpUnique.Clear();

        if (TutorialBypassSpiritFilter && ReligionManager.Instance != null)
        {
            IReadOnlyList<SpiritDefinitionSO> all = ReligionManager.Instance.AllSpirits;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null)
                    _tmpCandidates.Add(all[i]);
            }
        }
        else
        {
            AddKnownSpiritCandidates(request.beliefSystem, request.sourceBuilding, _tmpCandidates);
            AddDatabaseCandidates(request.beliefSystem, request.sourceBuilding, _tmpCandidates);
        }

        ShuffleInPlace(_tmpCandidates);

        for (int i = 0; i < _tmpCandidates.Count; i++)
        {
            SpiritDefinitionSO spirit = _tmpCandidates[i];
            if (spirit == null)
                continue;

            if (!_tmpUnique.Add(spirit))
                continue;

            request.offeredSpirits.Add(spirit);

            if (request.offeredSpirits.Count >= request.maxChoices)
                break;
        }
    }

    private void AddKnownSpiritCandidates(
        BeliefSystemType beliefSystem,
        ReligiousBuildingControl sourceBuilding,
        List<SpiritDefinitionSO> outList)
    {
        PlayerKnownSpiritsManager known = PlayerKnownSpiritsManager.Instance;
        if (known == null || outList == null)
            return;

        foreach (SpiritDefinitionSO spirit in known.GetAllKnown())
        {
            if (IsValidSummoningCandidate(spirit, beliefSystem, sourceBuilding))
                outList.Add(spirit);
        }
    }

    private void AddDatabaseCandidates(
        BeliefSystemType beliefSystem,
        ReligiousBuildingControl sourceBuilding,
        List<SpiritDefinitionSO> outList)
    {
        if (outList == null)
            return;

        for (int i = 0; i < spiritDatabase.Count; i++)
        {
            SpiritDefinitionSO spirit = spiritDatabase[i];
            if (IsValidSummoningCandidate(spirit, beliefSystem, sourceBuilding))
                outList.Add(spirit);
        }
    }

    private bool IsValidSummoningCandidate(
        SpiritDefinitionSO spirit,
        BeliefSystemType beliefSystem,
        ReligiousBuildingControl sourceBuilding)
    {
        if (spirit == null)
            return false;

        if (TutorialBypassSpiritFilter)
            return true;

        if (spirit.beliefSystem != beliefSystem)
            return false;

        PlayerReligionManager religion = PlayerReligionManager.Instance;
        if (avoidAlreadyAcceptedSpirits && religion != null && religion.IsAccepted(spirit))
            return false;

        if (avoidSpiritsAlreadyAffiliatedToBuilding && sourceBuilding != null && sourceBuilding.IsSpiritAffiliated(spirit))
            return false;

        return true;
    }

    private static void ShuffleInPlace<T>(List<T> list)
    {
        if (list == null || list.Count <= 1)
            return;

        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            T tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
    }

    public void InstallRuntimeRefs(
    SummoningSpiritOfferPanelControl newSummoningOfferPanel,
    bool refreshIfShowing = true)
    {
        if (newSummoningOfferPanel != null)
            summoningOfferPanel = newSummoningOfferPanel;

        if (refreshIfShowing && HasActiveChoice && summoningOfferPanel != null)
            summoningOfferPanel.OpenFor(_activeChoice);
    }
}