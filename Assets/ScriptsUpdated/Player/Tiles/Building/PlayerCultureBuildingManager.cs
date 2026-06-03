using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerCultureBuildingManager : MonoBehaviour
{
    public static PlayerCultureBuildingManager Instance { get; private set; }

    [Header("Turn Processing")]
    [Min(1)] public int buildingsPerFrame = 4;

    private Coroutine _processCo;
    private bool _isBlockingTurn = false;
    private bool _isProcessing;

    private readonly List<CultureBuildingControl> _buffer = new List<CultureBuildingControl>();

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

    private void OnEnable()  { TurnSystem.SubscribeToEndOfTurn(HandleEndTurn); }
    private void OnDisable()
    {
        TurnSystem.UnsubscribeFromEndOfTurn(HandleEndTurn);
        if (_isBlockingTurn) { _isBlockingTurn = false; TurnSystem.UnblockTurnAdvance(); }
    }

    private void HandleEndTurn()
    {
        if (!isActiveAndEnabled)
            return;

        if (_isBlockingTurn) { _isBlockingTurn = false; TurnSystem.UnblockTurnAdvance(); }
        if (_processCo != null)
            StopCoroutine(_processCo);

        _processCo = StartCoroutine(ProcessCultureBuildingsCo());
    }

    private IEnumerator ProcessCultureBuildingsCo()
    {
        _isBlockingTurn = true;
        TurnSystem.BlockTurnAdvance();
        _isProcessing = true;

        _buffer.Clear();
        var snapshot = CultureBuildingControl.GetAllSnapshot();
        for (int i = 0; i < snapshot.Count; i++)
        {
            if (snapshot[i] != null && snapshot[i].isActiveAndEnabled)
                _buffer.Add(snapshot[i]);
        }

        int processedThisFrame = 0;

        for (int i = 0; i < _buffer.Count; i++)
        {
            var building = _buffer[i];
            if (building == null)
                continue;

            building.RunEndTurn();

            processedThisFrame++;
            if (processedThisFrame >= buildingsPerFrame)
            {
                processedThisFrame = 0;
                yield return null;
            }
        }

        _isProcessing = false;
        _isBlockingTurn = false;
        TurnSystem.UnblockTurnAdvance();
        _processCo = null;

        SaveSystem.MarkSectionDirty(SaveSectionKeys.CoreSystems);
    }
}
