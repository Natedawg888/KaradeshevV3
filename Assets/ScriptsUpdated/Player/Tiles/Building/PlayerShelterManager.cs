using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerShelterManager : MonoBehaviour
{
    public static PlayerShelterManager Instance { get; private set; }

    [Header("Turn Processing")]
    [Min(1)] public int sheltersPerFrame = 2;

    [Tooltip("If true, shelters run maintenance first, then pairing in a second pass.")]
    public bool useTwoPhaseProcessing = true;

    private Coroutine _processCo;
    private bool _isBlockingTurn = false;
    private bool _isProcessing;

    // Pre-allocated buffer and comparer — avoids LINQ allocation every turn
    private readonly List<ShelterControl> _shelterBuffer = new();
    private static readonly ShelterLevelComparer _shelterComparer = new();

    private sealed class ShelterLevelComparer : IComparer<ShelterControl>
    {
        public int Compare(ShelterControl x, ShelterControl y)
        {
            if (x == null || y == null) return 0;
            int cmp = x.shelterLevel.CompareTo(y.shelterLevel);
            return cmp != 0 ? cmp : string.Compare(x.name, y.name, System.StringComparison.Ordinal);
        }
    }

    public bool IsProcessing => _isProcessing;

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
        TurnSystem.SubscribeToEndOfTurn(HandleEndTurn);
    }

    private void OnDisable()
    {
        TurnSystem.UnsubscribeFromEndOfTurn(HandleEndTurn);
        if (_isBlockingTurn) { _isBlockingTurn = false; TurnSystem.UnblockTurnAdvance(); }
    }

    private void MarkJobsDirty()
    {
        SaveSystem.MarkSectionDirty(SaveSectionKeys.Jobs);
        SaveSystem.MarkSectionDirty(SaveSectionKeys.WorldObjects);
    }

    private void HandleEndTurn()
    {
        if (!isActiveAndEnabled)
            return;

        if (_isBlockingTurn) { _isBlockingTurn = false; TurnSystem.UnblockTurnAdvance(); }
        if (_processCo != null)
            StopCoroutine(_processCo);

        _processCo = StartCoroutine(ProcessSheltersEndTurnCo());
        MarkJobsDirty();
    }

    private IEnumerator ProcessSheltersEndTurnCo()
    {
        _isBlockingTurn = true;
        TurnSystem.BlockTurnAdvance();
        _isProcessing = true;

        // Build sorted shelter list without LINQ allocation
        _shelterBuffer.Clear();
        var snapshot = ShelterControl.GetAllSheltersSnapshot();
        foreach (var s in snapshot)
        {
            if (s != null && s.isActiveAndEnabled)
                _shelterBuffer.Add(s);
        }
        _shelterBuffer.Sort(_shelterComparer);
        var shelters = _shelterBuffer;

        int processedThisFrame = 0;

        if (useTwoPhaseProcessing)
        {
            for (int i = 0; i < shelters.Count; i++)
            {
                var shelter = shelters[i];
                if (shelter == null) continue;

                shelter.RunEndTurnMaintenance();

                processedThisFrame++;
                if (processedThisFrame >= sheltersPerFrame)
                {
                    processedThisFrame = 0;
                    yield return null;
                }
            }

            for (int i = 0; i < shelters.Count; i++)
            {
                var shelter = shelters[i];
                if (shelter == null) continue;

                shelter.RunEndTurnPairingStep();

                processedThisFrame++;
                if (processedThisFrame >= sheltersPerFrame)
                {
                    processedThisFrame = 0;
                    yield return null;
                }
            }
        }
        else
        {
            for (int i = 0; i < shelters.Count; i++)
            {
                var shelter = shelters[i];
                if (shelter == null) continue;

                shelter.RunEndTurnMaintenance();
                shelter.RunEndTurnPairingStep();

                processedThisFrame++;
                if (processedThisFrame >= sheltersPerFrame)
                {
                    processedThisFrame = 0;
                    yield return null;
                }
            }
        }

        _isProcessing = false;
        _isBlockingTurn = false;
        TurnSystem.UnblockTurnAdvance();
        _processCo = null;
        MarkJobsDirty();
    }
}