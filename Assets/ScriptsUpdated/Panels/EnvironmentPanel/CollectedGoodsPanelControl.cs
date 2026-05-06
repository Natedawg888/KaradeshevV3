using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CollectedGoodsPanelControl : MonoBehaviour
{
    [Header("Root")]
    public GameObject root;
    public Button closeButton;
    public TMP_Text headerText;

    [Header("List")]
    public Transform contentParent;
    public CollectedItemEntry itemEntryPrefab;

    private EnvironmentControl currentEnv;
    private readonly List<CollectedItemEntry> pooled = new();
    private readonly List<(ResourceDefinition def, int amount)> _leftBehindBuffer = new();

    public event Action OnClose;

    public bool IsShowing => root != null ? root.activeInHierarchy : gameObject.activeInHierarchy;
    public EnvironmentControl CurrentEnvironment => currentEnv;

    private void Awake()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(() => Hide(true));
        }

        Hide(false);
    }

    public void Show(EnvironmentControl env)
    {
        currentEnv = env;
        if (currentEnv == null)
        {
            Hide(false);
            return;
        }

        if (root != null) root.SetActive(true);

        if (headerText != null)
            headerText.text = $"{env.environmentName} — Collected Goods";

        RefreshList();
    }

    public void Hide()
    {
        Hide(true);
    }

    private void Hide(bool countRemainingAsLeftBehind)
    {
        if (countRemainingAsLeftBehind && currentEnv != null)
        {
            var remaining = currentEnv.PeekPendingLoot();
            if (remaining != null && remaining.Count > 0)
            {
                _leftBehindBuffer.Clear();

                for (int i = 0; i < remaining.Count; i++)
                {
                    var p = remaining[i];
                    if (p.def == null || p.amount <= 0)
                        continue;

                    _leftBehindBuffer.Add((p.def, p.amount));
                }

                if (_leftBehindBuffer.Count > 0)
                {
                    PlayerReligionManager religion = PlayerReligionManager.Instance;
                    if (religion != null)
                        religion.NotifyGatheredLootLeftBehind(_leftBehindBuffer);
                }
            }
        }

        if (root != null)
            root.SetActive(false);

        if (currentEnv != null)
            currentEnv.ClearPendingLoot();

        ClearList();
        currentEnv = null;
        OnClose?.Invoke();
    }

    private void ClearList()
    {
        for (int i = 0; i < pooled.Count; i++)
        {
            if (pooled[i] != null && pooled[i].gameObject != null)
                pooled[i].gameObject.SetActive(false);
        }
    }

    private CollectedItemEntry GetEntry()
    {
        foreach (var e in pooled)
            if (e != null && !e.gameObject.activeSelf)
                return e;

        var inst = Instantiate(itemEntryPrefab, contentParent);
        pooled.Add(inst);
        return inst;
    }

    public void RefreshList()
    {
        if (currentEnv == null)
        {
            Hide(false);
            return;
        }

        ClearList();

        var loot = currentEnv.PeekPendingLoot();
        bool any = loot != null && loot.Count > 0;

        if (!any)
        {
            Hide(false);
            return;
        }

        foreach (var p in loot)
        {
            var entry = GetEntry();
            entry.gameObject.SetActive(true);
            entry.Bind(currentEnv, p.def, p.amount, this);
        }
    }

    public void OnEntryChanged()
    {
        if (currentEnv == null || !currentEnv.HasLootReady)
        {
            Hide(false);
            return;
        }

        RefreshList();
    }

    public void ApplyCollectedItemPrefab(GameObject prefab, bool repopulateIfOpen)
    {
        if (!prefab) return;

        var typed = prefab.GetComponent<CollectedItemEntry>();
        if (!typed)
        {
            Debug.LogWarning("[CollectedGoodsPanelControl] Provided prefab has no CollectedItemEntry component.");
            return;
        }

        itemEntryPrefab = typed;

        for (int i = pooled.Count - 1; i >= 0; i--)
        {
            if (pooled[i])
                Destroy(pooled[i].gameObject);
        }
        pooled.Clear();

        if (repopulateIfOpen && root && root.activeInHierarchy && currentEnv != null)
            RefreshList();
    }
}