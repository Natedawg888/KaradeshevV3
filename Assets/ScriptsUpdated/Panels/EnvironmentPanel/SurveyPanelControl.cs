using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SurveyPanelControl : MonoBehaviour
{
    [Serializable]
    public class TutorialSurveyEntry
    {
        public ResourceDefinition definition;
        public int amount;
    }

    [Header("Roots")]
    public GameObject root;
    public Button closeButton;

    [Header("Content")]
    public Transform resourceListParent;
    public GameObject resourceEntryPrefab;

    [Header("References")]
    [SerializeField] private PlayerKnownResourcesManager knownManager;

    private EnvironmentResourceNode currentNode;
    private bool _subscribedToKnown;

    private bool _showTutorialEntriesOnly;
    private readonly List<TutorialSurveyEntry> _tutorialEntries = new();

    public event Action OnOpen;
    public event Action OnClose;

    public bool IsShowing => root != null ? root.activeInHierarchy : gameObject.activeInHierarchy;

    private void Awake()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }

        Hide();
    }

    private void OnEnable()
    {
        SubscribeToKnownManager();
    }

    private void OnDisable()
    {
        UnsubscribeFromKnownManager();
    }

    private void OnDestroy()
    {
        UnsubscribeFromKnownManager();
    }

    private void SubscribeToKnownManager()
    {
        if (_subscribedToKnown)
            return;

        if (knownManager == null)
            return;

        knownManager.OnKnownChanged += HandleKnownChanged;
        _subscribedToKnown = true;
    }

    private void UnsubscribeFromKnownManager()
    {
        if (!_subscribedToKnown)
            return;

        if (knownManager != null)
            knownManager.OnKnownChanged -= HandleKnownChanged;

        _subscribedToKnown = false;
    }

    private void HandleKnownChanged()
    {
        if (root != null && root.activeSelf && (currentNode != null || _showTutorialEntriesOnly))
            PopulateResourceList();
    }

    public void Show(EnvironmentResourceNode node)
    {
        if (node == null)
            return;

        _showTutorialEntriesOnly = false;
        _tutorialEntries.Clear();
        currentNode = node;

        if (root != null)
            root.SetActive(true);

        PopulateResourceList();
        OnOpen?.Invoke();
    }

    public void ShowTutorialEntries(List<TutorialSurveyEntry> entries)
    {
        _showTutorialEntriesOnly = true;
        currentNode = null;
        _tutorialEntries.Clear();

        if (entries != null)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                TutorialSurveyEntry src = entries[i];
                if (src == null || src.definition == null || src.amount <= 0)
                    continue;

                _tutorialEntries.Add(new TutorialSurveyEntry
                {
                    definition = src.definition,
                    amount = src.amount
                });
            }
        }

        if (root != null)
            root.SetActive(true);

        PopulateResourceList();
        OnOpen?.Invoke();
    }

    public void Hide()
    {
        bool wasShowing = root != null && root.activeSelf;
        if (root != null)
            root.SetActive(false);

        currentNode = null;
        _showTutorialEntriesOnly = false;
        _tutorialEntries.Clear();
        ClearResourceList();

        if (wasShowing)
            OnClose?.Invoke();
    }

    private void PopulateResourceList()
    {
        ClearResourceList();

        if (resourceListParent == null || resourceEntryPrefab == null)
            return;

        if (_showTutorialEntriesOnly)
        {
            PopulateTutorialEntries();
            return;
        }

        if (currentNode == null)
            return;

        List<ResourceSpawnEntry> visibleEntries = new List<ResourceSpawnEntry>();
        IReadOnlyList<ResourceSpawnEntry> entries = currentNode.SpawnedResources;

        if (entries == null)
            return;

        for (int i = 0; i < entries.Count; i++)
        {
            ResourceSpawnEntry entry = entries[i];
            if (entry == null || entry.definition == null || entry.amount <= 0)
                continue;

            if (knownManager != null && !knownManager.IsKnown(entry.definition))
                continue;

            visibleEntries.Add(entry);
        }

        if (knownManager == null) {}
            //Debug.LogWarning("[SurveyPanel] knownManager is not assigned. Showing all resources.");

        visibleEntries.Sort(CompareEntriesByDisplayName);

        for (int i = 0; i < visibleEntries.Count; i++)
            SpawnResourceEntry(visibleEntries[i].definition, visibleEntries[i].amount);
    }

    private void PopulateTutorialEntries()
    {
        _tutorialEntries.Sort((a, b) =>
        {
            string aName = GetDisplayName(a != null ? a.definition : null);
            string bName = GetDisplayName(b != null ? b.definition : null);
            return string.Compare(aName, bName, StringComparison.OrdinalIgnoreCase);
        });

        for (int i = 0; i < _tutorialEntries.Count; i++)
        {
            TutorialSurveyEntry entry = _tutorialEntries[i];
            if (entry == null || entry.definition == null || entry.amount <= 0)
                continue;

            SpawnResourceEntry(entry.definition, entry.amount);
        }
    }

    private void SpawnResourceEntry(ResourceDefinition definition, int amount)
    {
        if (definition == null || amount <= 0 || resourceEntryPrefab == null || resourceListParent == null)
            return;

        ResourceSpawnEntry entry = new ResourceSpawnEntry
        {
            definition = definition,
            amount = amount
        };

        GameObject go = Instantiate(resourceEntryPrefab, resourceListParent);
        ResourceEntryUI ui = go.GetComponent<ResourceEntryUI>();
        if (ui != null)
            ui.Initialize(entry);
    }

    private static int CompareEntriesByDisplayName(ResourceSpawnEntry a, ResourceSpawnEntry b)
    {
        string aName = GetDisplayName(a != null ? a.definition : null);
        string bName = GetDisplayName(b != null ? b.definition : null);
        return string.Compare(aName, bName, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetDisplayName(ResourceDefinition def)
    {
        if (def == null)
            return "(null)";

        return string.IsNullOrWhiteSpace(def.resourceName) ? def.name : def.resourceName;
    }

    private void ClearResourceList()
    {
        if (resourceListParent == null)
            return;

        for (int i = resourceListParent.childCount - 1; i >= 0; i--)
            Destroy(resourceListParent.GetChild(i).gameObject);
    }

    public void ApplyResourceEntryPrefab(GameObject newPrefab, bool repopulateIfOpen = false)
    {
        if (!newPrefab)
            return;

        resourceEntryPrefab = newPrefab;

        if (repopulateIfOpen && root != null && root.activeSelf && (currentNode != null || _showTutorialEntriesOnly))
            PopulateResourceList();
    }

    public void SetKnownManager(PlayerKnownResourcesManager newKnownManager, bool refreshIfOpen = true)
    {
        if (knownManager == newKnownManager)
            return;

        UnsubscribeFromKnownManager();
        knownManager = newKnownManager;
        SubscribeToKnownManager();

        if (refreshIfOpen && root != null && root.activeSelf && (currentNode != null || _showTutorialEntriesOnly))
            PopulateResourceList();
    }
}
