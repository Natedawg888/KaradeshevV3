using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SpiritAffiliatedPanelControl : MonoBehaviour
{
    [Header("Root")]
    public GameObject root;
    public Button closeButton;
    public TMP_Text titleText;
    public TMP_Text subtitleText;

    [Header("Content")]
    public Transform contentRoot;
    public SpiritAffiliatedItemUI itemPrefab;

    private readonly List<SpiritAffiliatedItemUI> _spawned = new List<SpiritAffiliatedItemUI>();

    private GameObject RootObject => root != null ? root : gameObject;

    private void Awake()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }

        if (RootObject != null)
            RootObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (PlayerReligionManager.Instance != null)
            PlayerReligionManager.Instance.ReligionChanged += HandleReligionChanged;
    }

    private void OnDisable()
    {
        if (PlayerReligionManager.Instance != null)
            PlayerReligionManager.Instance.ReligionChanged -= HandleReligionChanged;
    }

    private void HandleReligionChanged()
    {
        if (RootObject != null && RootObject.activeSelf)
            RefreshNow();
    }

    public void Show()
    {
        if (RootObject != null)
            RootObject.SetActive(true);

        RefreshNow();
    }

    public void Hide()
    {
        ClearItems();

        if (RootObject != null)
            RootObject.SetActive(false);
    }

    public void RefreshNow()
    {
        ClearItems();

        if (titleText != null)
            titleText.text = "Affiliated Spirits";

        PlayerReligionManager religion = PlayerReligionManager.Instance;
        if (religion == null)
        {
            if (subtitleText != null)
                subtitleText.text = "No religion manager found.";

            return;
        }

        int shownCount = 0;

        IReadOnlyList<SpiritRuntimeState> active = religion.ActiveSpirits;
        if (active != null && contentRoot != null && itemPrefab != null)
        {
            for (int i = 0; i < active.Count; i++)
            {
                SpiritRuntimeState state = active[i];
                if (state == null || !state.accepted || state.definition == null)
                    continue;

                SpiritAffiliatedItemUI item = Instantiate(itemPrefab, contentRoot);
                item.Bind(state);
                _spawned.Add(item);
                shownCount++;
            }
        }

        if (subtitleText != null)
            subtitleText.text = shownCount > 0
                ? $"Showing {shownCount} affiliated spirit{(shownCount == 1 ? string.Empty : "s")}."
                : "You do not currently have any affiliated spirits.";
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
}