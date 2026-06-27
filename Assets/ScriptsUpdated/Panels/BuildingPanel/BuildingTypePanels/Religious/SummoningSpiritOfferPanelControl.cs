using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SummoningSpiritOfferPanelControl : MonoBehaviour
{
    public event Action OnOpen;
    public event Action OnSpiritChosen;

    public bool IsShowing => RootObject != null && RootObject.activeSelf;

    [Header("Root")]
    public GameObject root;
    public TMP_Text titleText;
    public TMP_Text subtitleText;

    [Header("Choices")]
    public Transform contentRoot;
    public SummoningSpiritOfferItemUI itemPrefab;

    private readonly List<SummoningSpiritOfferItemUI> _spawned = new List<SummoningSpiritOfferItemUI>();
    private PlayerRitualManager.PendingSummoningChoice _current;

    private GameObject RootObject => root != null ? root : gameObject;

    private void Awake()
    {
        if (RootObject != null)
            RootObject.SetActive(false);
    }

    public void OpenFor(PlayerRitualManager.PendingSummoningChoice request)
    {
        _current = request;

        if (titleText != null)
            titleText.text = "Choose a Spirit";

        if (subtitleText != null)
            subtitleText.text = request != null
                ? $"The summoning has completed. Choose one {request.beliefSystem} spirit to follow."
                : string.Empty;

        RootObject.SetActive(true);
        Rebuild();
        OnOpen?.Invoke();
    }

    public void Hide()
    {
        ClearItems();
        _current = null;

        if (RootObject != null)
            RootObject.SetActive(false);
    }

    private void Rebuild()
    {
        ClearItems();

        if (_current == null || _current.offeredSpirits == null || itemPrefab == null || contentRoot == null)
            return;

        for (int i = 0; i < _current.offeredSpirits.Count; i++)
        {
            SpiritDefinitionSO spirit = _current.offeredSpirits[i];
            if (spirit == null)
                continue;

            SummoningSpiritOfferItemUI item = Instantiate(itemPrefab, contentRoot);
            item.Setup(spirit, this);
            _spawned.Add(item);
        }
    }

    private void ClearItems()
    {
        for (int i = 0; i < _spawned.Count; i++)
        {
            if (_spawned[i] != null)
                Destroy(_spawned[i].gameObject);
        }

        _spawned.Clear();
    }

    public void OnClickChooseSpirit(SpiritDefinitionSO spirit)
    {
        if (PlayerRitualManager.Instance == null)
            return;

        if (PlayerRitualManager.Instance.TryAcceptSummoningChoice(spirit, out string reason))
        {
            OnSpiritChosen?.Invoke();
        }
        //else Debug.LogWarning($"[SummoningSpiritOfferPanel] Failed to accept spirit choice: {reason}");
    }
}
