using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PlayerShelterManager : MonoBehaviour
{
    public static PlayerShelterManager Instance { get; private set; }

    [Header("Turn Processing")]
    [Min(1)] public int sheltersPerFrame = 2;

    [Tooltip("If true, shelters run maintenance first, then pairing in a second pass.")]
    public bool useTwoPhaseProcessing = true;

    private Coroutine _processCo;
    private bool _isProcessing;

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

        if (_processCo != null)
            StopCoroutine(_processCo);

        _processCo = StartCoroutine(ProcessSheltersEndTurnCo());
        MarkJobsDirty();
    }

    private IEnumerator ProcessSheltersEndTurnCo()
    {
        _isProcessing = true;

        var shelters = ShelterControl.GetAllSheltersSnapshot()
            .Where(s => s != null && s.isActiveAndEnabled)
            .OrderBy(s => s.shelterLevel)
            .ThenBy(s => s.name)
            .ToList();

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
        _processCo = null;
        MarkJobsDirty();
    }
}