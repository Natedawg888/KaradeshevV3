using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnvironmentResourceUpdater : MonoBehaviour
{
    [Tooltip("How many nodes to process per frame to spread cost.")]
    public int nodesPerFrame = 10;

    private List<EnvironmentResourceNode> allNodes = new();

    private void OnEnable()
    {
        TurnSystem.SubscribeToEndOfTurn(OnTurnEnded);

        if (SeasonManager.Instance != null)
            SeasonManager.Instance.OnSeasonChanged += OnSeasonChanged;
    }

    private void OnDisable()
    {
        TurnSystem.UnsubscribeFromEndOfTurn(OnTurnEnded);

        if (SeasonManager.Instance != null)
            SeasonManager.Instance.OnSeasonChanged -= OnSeasonChanged;
    }

    private void Start()
    {
        RefreshNodeList();
    }

    private void RefreshNodeList()
    {
        allNodes.Clear();
        allNodes.AddRange(FindObjectsOfType<EnvironmentResourceNode>());
    }

    private void OnTurnEnded()
    {
        if (allNodes.Count == 0)
            RefreshNodeList();

        StartCoroutine(ProcessNodeLifecycle());
    }

    private void OnSeasonChanged(SeasonDefinition newSeason)
    {
        if (allNodes.Count == 0)
            RefreshNodeList();

        StartCoroutine(GenerateSeasonResources());
    }

    private IEnumerator ProcessNodeLifecycle()
    {
        int processed = 0;
        for (int i = 0; i < allNodes.Count; i++)
        {
            var node = allNodes[i];
            if (node == null)
                continue;

            node.TickResourceLifecycle();

            processed++;
            if (processed >= nodesPerFrame)
            {
                processed = 0;
                yield return null;
            }
        }
    }

    private IEnumerator GenerateSeasonResources()
    {
        int processed = 0;
        for (int i = 0; i < allNodes.Count; i++)
        {
            var node = allNodes[i];
            if (node == null)
                continue;

            node.GenerateResources();

            processed++;
            if (processed >= nodesPerFrame)
            {
                processed = 0;
                yield return null;
            }
        }
    }
}