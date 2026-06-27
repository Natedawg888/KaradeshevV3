using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StoragePanelControl : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject root;
    [SerializeField] private Button closeButton;

    [Header("Header")]
    [SerializeField] private TMP_Text titleText;

    [Header("Capacity UI")]
    [SerializeField] private TMP_Text capacityText;

    [Header("Content Areas")]
    [SerializeField] private Transform storedContentParent;    // left
    [SerializeField] private Transform inventoryContentParent; // right

    [Header("Prefabs")]
    [SerializeField] private StoredItemDisplay storedItemPrefab;
    [SerializeField] private StorageItemUI inventoryItemPrefab;

    [Header("Perf")]
    [Tooltip("If true, populates lists over multiple frames to avoid spikes.")]
    [SerializeField] private bool buildOverFrames = false;

    [Tooltip("How many rows to build per frame when buildOverFrames is enabled.")]
    [SerializeField] private int rowsPerFrame = 12;

    [Tooltip("Temporarily disable layout components while building lists.")]
    [SerializeField] private bool disableLayoutsWhileBuilding = true;

    // runtime
    private BuildingControl _building;
    private TileControl _tile;
    private BuildingPanelControl _parentPanel;
    private StorageBuildingControl _storage;

    // pools
    private readonly List<StoredItemDisplay> _storedPool = new();
    private readonly List<StorageItemUI> _invPool = new();

    // temp buffers (reused)
    private readonly List<StorageItem> _tmpStored = new(64);
    private readonly List<InventoryStack> _tmpInv = new(128);

    // layout caches
    private LayoutGroup _storedLayout;
    private LayoutGroup _invLayout;
    private ContentSizeFitter _storedFitter;
    private ContentSizeFitter _invFitter;

    private Coroutine _buildRoutine;

    // build state
    private int _storedBuildIndex = 0;
    private int _invBuildIndex = 0;
    private int _invActiveCount = 0;

    public bool IsShowing => root != null && root.activeInHierarchy;
    public event System.Action OnClose;

    private void Awake()
    {
        CacheLayoutComponents();
    }

    private void CacheLayoutComponents()
    {
        if (storedContentParent != null)
        {
            _storedLayout = storedContentParent.GetComponent<LayoutGroup>();
            _storedFitter = storedContentParent.GetComponent<ContentSizeFitter>();
        }

        if (inventoryContentParent != null)
        {
            _invLayout = inventoryContentParent.GetComponent<LayoutGroup>();
            _invFitter = inventoryContentParent.GetComponent<ContentSizeFitter>();
        }
    }

    public void OpenFor(BuildingControl building, BuildingPanelControl parentPanel, TileControl tile)
    {
        _building = building;
        _parentPanel = parentPanel;
        _tile = tile;

        _storage = _building ? _building.GetComponent<StorageBuildingControl>() : null;
        if (_storage == null)
        {
            //Debug.LogWarning("[StoragePanel] No StorageBuildingControl found on building.");
            return;
        }

        RefreshTitle();

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
        }

        if (root != null) root.SetActive(true);
        else gameObject.SetActive(true);

        Refresh();
    }

    public void Close()
    {
        StopBuild();

        if (root != null) root.SetActive(false);
        else gameObject.SetActive(false);

        if (_parentPanel != null)
            _parentPanel.SoftShowFromChild();

        OnClose?.Invoke();
    }

    public void Refresh()
    {
        if (_storage == null) return;

        StopBuild();

        RefreshTitle();
        _storage.RecalculateTotalStoredAmount();

        if (capacityText != null)
            capacityText.text = $"{_storage.GetTotalStoredAmount()}/{_storage.maxStorageCapacity}";

        if (buildOverFrames)
            _buildRoutine = StartCoroutine(BuildListsOverFrames());
        else
            BuildListsImmediate();
    }

    private void StopBuild()
    {
        if (_buildRoutine != null)
        {
            StopCoroutine(_buildRoutine);
            _buildRoutine = null;
        }
    }

    private void SetLayoutsEnabled(bool enabled)
    {
        if (!disableLayoutsWhileBuilding) return;

        if (_storedLayout != null) _storedLayout.enabled = enabled;
        if (_invLayout != null) _invLayout.enabled = enabled;
        if (_storedFitter != null) _storedFitter.enabled = enabled;
        if (_invFitter != null) _invFitter.enabled = enabled;
    }

    private void BuildListsImmediate()
    {
        SetLayoutsEnabled(false);

        BuildStoredList(int.MaxValue);
        BuildInventoryList(int.MaxValue);

        SetLayoutsEnabled(true);
        Canvas.ForceUpdateCanvases();
    }

    private IEnumerator BuildListsOverFrames()
    {
        SetLayoutsEnabled(false);

        int budget = Mathf.Max(1, rowsPerFrame);

        // Build stored list first
        bool doneStored;
        BuildStoredList(budget, out doneStored, resume: false);
        while (!doneStored)
        {
            yield return null;
            BuildStoredList(budget, out doneStored, resume: true);
        }

        // Then build inventory list
        bool doneInv;
        BuildInventoryList(budget, out doneInv, resume: false);
        while (!doneInv)
        {
            yield return null;
            BuildInventoryList(budget, out doneInv, resume: true);
        }

        SetLayoutsEnabled(true);
        Canvas.ForceUpdateCanvases();
        _buildRoutine = null;
    }

    // ---------- Stored list (LEFT) ----------

    private int BuildStoredList(int budget)
    {
        return BuildStoredList(budget, out _, resume: false);
    }

    private int BuildStoredList(int budget, out bool done, bool resume = false)
    {
        done = true;

        if (storedContentParent == null || storedItemPrefab == null || _storage == null)
        {
            DeactivateFrom(_storedPool, 0);
            return 0;
        }

        if (!resume)
        {
            _storedBuildIndex = 0;

            _tmpStored.Clear();
            var list = _storage.storedResources;
            if (list != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var it = list[i];
                    if (it == null || it.definition == null) continue;
                    if (it.amount <= 0) continue;
                    _tmpStored.Add(it);
                }
            }

            _tmpStored.Sort((a, b) =>
                string.Compare(a.definition.resourceName, b.definition.resourceName, System.StringComparison.Ordinal));
        }

        int built = 0;

        while (_storedBuildIndex < _tmpStored.Count && built < budget)
        {
            var item = _tmpStored[_storedBuildIndex];
            var row = GetStoredRow(_storedBuildIndex);
            row.SetDisplay(item, _storage, this);
            row.gameObject.SetActive(true);

            _storedBuildIndex++;
            built++;
        }

        if (_storedBuildIndex >= _tmpStored.Count)
        {
            DeactivateFrom(_storedPool, _tmpStored.Count);
            done = true;
        }
        else
        {
            done = false;
        }

        return built;
    }

    private StoredItemDisplay GetStoredRow(int index)
    {
        while (_storedPool.Count <= index)
        {
            var row = Instantiate(storedItemPrefab, storedContentParent);
            row.gameObject.SetActive(false);
            _storedPool.Add(row);
        }

        return _storedPool[index];
    }

    // ---------- Inventory list (RIGHT) ----------

    private void BuildInventoryList(int budget)
    {
        BuildInventoryList(budget, out _, resume: false);
    }

    private void BuildInventoryList(int budget, out bool done, bool resume = false)
    {
        done = true;

        if (inventoryContentParent == null || inventoryItemPrefab == null || _storage == null || PlayerInventoryManager.Instance == null)
        {
            DeactivateFrom(_invPool, 0);
            return;
        }

        if (!resume)
        {
            _invBuildIndex = 0;
            _invActiveCount = 0;

            if (_storage.GetAvailableSpace() <= 0)
            {
                DeactivateFrom(_invPool, 0);
                done = true;
                return;
            }

            _tmpInv.Clear();

            AddStacks(PlayerInventoryManager.Instance.GetStacks(ResourceType.Food));
            AddStacks(PlayerInventoryManager.Instance.GetStacks(ResourceType.Water));
            AddStacks(PlayerInventoryManager.Instance.GetStacks(ResourceType.Material));

            _tmpInv.Sort((a, b) =>
                string.Compare(a.definition.resourceName, b.definition.resourceName, System.StringComparison.Ordinal));

            // Clear previous visible rows before rebuilding.
            DeactivateFrom(_invPool, 0);
        }

        int built = 0;

        while (_invBuildIndex < _tmpInv.Count && built < budget)
        {
            var stack = _tmpInv[_invBuildIndex];
            _invBuildIndex++;

            if (stack == null || stack.definition == null) continue;
            if (stack.amount <= 0) continue;
            if (stack.definition.isGroup) continue;
            if (!_storage.CanStoreResource(stack.definition)) continue;

            var row = GetInvRow(_invActiveCount);
            row.SetResource(stack.definition, stack.amount, _storage, this);
            row.gameObject.SetActive(true);

            _invActiveCount++;
            built++;
        }

        if (_invBuildIndex >= _tmpInv.Count)
        {
            DeactivateFrom(_invPool, _invActiveCount);
            done = true;
        }
        else
        {
            done = false;
        }
    }

    private void AddStacks(IReadOnlyList<InventoryStack> stacks)
    {
        if (stacks == null) return;

        for (int i = 0; i < stacks.Count; i++)
        {
            var stack = stacks[i];
            if (stack == null || stack.definition == null) continue;
            _tmpInv.Add(stack);
        }
    }

    private StorageItemUI GetInvRow(int index)
    {
        while (_invPool.Count <= index)
        {
            var row = Instantiate(inventoryItemPrefab, inventoryContentParent);
            row.gameObject.SetActive(false);
            _invPool.Add(row);
        }

        return _invPool[index];
    }

    private static void DeactivateFrom<T>(List<T> pool, int startIndex) where T : Component
    {
        for (int i = startIndex; i < pool.Count; i++)
        {
            if (pool[i] != null)
                pool[i].gameObject.SetActive(false);
        }
    }

    private void RefreshTitle()
    {
        if (titleText == null || _building == null) return;

        string displayName = !string.IsNullOrWhiteSpace(_building.buildingName)
            ? _building.buildingName
            : (BuildingManager.Instance?.GetBuildingByID(_building.buildingID)?.buildingName ?? _building.buildingID);

        titleText.text = displayName;
    }
}
