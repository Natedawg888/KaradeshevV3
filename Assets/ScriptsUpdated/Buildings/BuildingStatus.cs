using System;
using UnityEngine;

public enum BuildingState
{
    Normal,
    Damaged,
    Destroyed
}

public class BuildingStatus : MonoBehaviour
{
    [Header("State Prefabs (optional)")]
    [Tooltip("If set, this object will be turned on/off for the Normal state. If null, falls back to MeshRenderer/SkinnedMeshRenderer on this GameObject.")]
    public GameObject normalRoot;

    [Tooltip("Prefab shown while Damaged (instantiated as a child).")]
    public GameObject damagedPrefab;

    [Tooltip("Prefab shown while Destroyed (instantiated as a child).")]
    public GameObject destroyedPrefab;

    public GameObject lights;

    [Header("UI refs")]
    public TimerUI manualClearTimerUIRef;

    [Header("Destroyed Cleanup")]
    public int autoClearAfterTurns = 3;
    [SerializeField] private bool readAutoClearFromDefinition = true;

    public BuildingState CurrentState { get; private set; } = BuildingState.Normal;
    public event Action<BuildingState> OnStateChanged;

    [Header("Restore-to-Environment")]
    [Tooltip("Prefab that contains TileControl + TileScript configured with environment tile options.")]
    public GameObject environmentTilePrefab;

    public int DestroyedTurnsElapsed => _destroyedTurns;
    public int AutoClearAfterTurns => autoClearAfterTurns;
    
    public int AutoClearTurnsRemaining
    {
        get
        {
            if (autoClearAfterTurns <= 0) return int.MaxValue;
            return Mathf.Max(0, autoClearAfterTurns - _destroyedTurns);
        }
    }

    private GameObject _damagedInstance;
    private GameObject _destroyedInstance;
    private int _destroyedTurns;
    

    private void Awake()
    {
        // Pre-instantiate variants for quick toggling
        if (damagedPrefab != null)
        {
            _damagedInstance = Instantiate(damagedPrefab, transform);
            _damagedInstance.transform.localPosition = Vector3.zero;
            _damagedInstance.transform.localRotation = Quaternion.identity;
            _damagedInstance.SetActive(false);
        }

        if (destroyedPrefab != null)
        {
            _destroyedInstance = Instantiate(destroyedPrefab, transform);
            _destroyedInstance.transform.localPosition = Vector3.zero;
            _destroyedInstance.transform.localRotation = Quaternion.identity;
            _destroyedInstance.SetActive(false);
        }

        var job = GetComponent<ManualClearJob>();
        if (!job) job = gameObject.AddComponent<ManualClearJob>();

        // Start in Normal state visually
        SetState(BuildingState.Normal);

        TurnSystem.SubscribeToEndOfTurn(OnEndTurn);
    }

    private void OnDestroy()
    {
        TurnSystem.UnsubscribeFromEndOfTurn(OnEndTurn);
    }

    private void Start()
    {
        // Pull per-building config once everything is spawned.
        if (readAutoClearFromDefinition)
            ApplyAutoClearFromDefinition();
    }

    private void ApplyAutoClearFromDefinition()
    {
        var bc  = GetComponent<BuildingControl>();
        var id  = bc != null ? bc.buildingID : null;
        if (string.IsNullOrEmpty(id)) return;

        var def = BuildingManager.Instance?.GetBuildingByID(id);
        if (def == null) return;

        autoClearAfterTurns = Mathf.Max(0, def.destroyedAutoClearAfterTurns);
        // Debug.Log($"[BuildingStatus] autoClearAfterTurns set from def '{id}' = {autoClearAfterTurns}");
    }

    private void OnEndTurn()
    {
        if (CurrentState == BuildingState.Destroyed && autoClearAfterTurns > 0)
        {
            _destroyedTurns++;
            if (_destroyedTurns >= autoClearAfterTurns)
            {
                TryClearToBaseTile();
            }
        }
    }
    
    public void SetState(BuildingState newState)
    {
        if (newState == CurrentState) return;
        CurrentState = newState;

        // Build an exclusion list so normal toggling doesn't affect variant children
        Transform[] exclude = new Transform[]
        {
            _damagedInstance ? _damagedInstance.transform   : null,
            _destroyedInstance ? _destroyedInstance.transform : null
        };

        switch (newState)
        {
            case BuildingState.Normal:
                EnableNormalBuildingMesh(true, exclude);
                if (lights) lights.gameObject.SetActive(true);
                if (_damagedInstance)   _damagedInstance.SetActive(false);
                if (_destroyedInstance) _destroyedInstance.SetActive(false);
                _destroyedTurns = 0;
                break;

            case BuildingState.Damaged:
                // Hide normal visuals only (do not touch damaged/destroyed trees)
                EnableNormalBuildingMesh(false, exclude);

                if (lights) lights.gameObject.SetActive(true);

                if (_destroyedInstance) _destroyedInstance.SetActive(false);
                if (_damagedInstance)
                {
                    _damagedInstance.SetActive(true);
                    // Make sure its renderers are ON
                    ToggleRenderersIn(_damagedInstance.transform, true);
                }
                _destroyedTurns = 0;
                break;

            case BuildingState.Destroyed:
                EnableNormalBuildingMesh(false, exclude);

                if (lights) lights.gameObject.SetActive(false);

                if (_damagedInstance)   _damagedInstance.SetActive(false);
                if (_destroyedInstance)
                {
                    _destroyedInstance.SetActive(true);
                    // Make sure its renderers are ON
                    ToggleRenderersIn(_destroyedInstance.transform, true);
                }
                // count turns in OnEndTurn
                break;
        }

        // NEW: if this building can produce, cancel any active production plan when Damaged
        if (newState == BuildingState.Damaged)
        {
            var prodControls = GetComponents<ProductionBuildingControl>();
            for (int i = 0; i < prodControls.Length; i++)
            {
                var pc = prodControls[i];
                if (pc != null && pc.HasActivePlan)
                {
                    Debug.Log($"[BuildingStatus] Building '{name}' Damaged; cancelling production plan on {pc.name}.");
                    pc.CancelCurrentPlan();
                }
            }
        }

        PostBuildingStateNotification(newState);

        OnStateChanged?.Invoke(newState);
        BroadcastToTypeHandlers(newState);
    }

    private void PostBuildingStateNotification(BuildingState newState)
    {
        if (newState != BuildingState.Damaged && newState != BuildingState.Destroyed) return;
        if (NotificationManager.Instance == null) return;

        NotificationType notifType = newState == BuildingState.Damaged
            ? NotificationType.BuildingDamaged
            : NotificationType.BuildingDestroyed;

        var instance = GetComponent<BuildingInstance>();
        string buildingName = (instance != null && instance.definition != null)
            ? instance.definition.buildingName
            : gameObject.name;

        string title, message;
        if (NotificationMessageCrafterManager.Instance != null)
            (title, message) = NotificationMessageCrafterManager.Instance.CraftBuilding(notifType, buildingName);
        else
        {
            title   = newState == BuildingState.Damaged ? "Building Damaged"   : "Building Destroyed";
            message = newState == BuildingState.Damaged
                ? $"{buildingName} has been damaged."
                : $"{buildingName} has been destroyed.";
        }

        NotificationManager.Instance.AddNotification(notifType, title, message, transform.position);
    }

    private void EnableNormalBuildingMesh(bool on, Transform[] exclude = null)
    {
        // If a normalRoot was provided, toggle ONLY its renderers (not the GameObject)
        if (normalRoot != null)
        {
            // Toggle meshes under normalRoot, excluding variant roots (damaged/destroyed)
            ToggleRenderersIn(normalRoot.transform, on, exclude);
            return;
        }

        // Otherwise, toggle ONLY renderers under this object, EXCLUDING variant roots
        ToggleRenderersIn(transform, on, exclude);
    }

    private static void ToggleRenderersIn(Transform root, bool on, Transform[] exclude = null)
    {
        if (!root) return;

        bool Skip(Transform t)
        {
            if (exclude == null) return false;
            for (int i = 0; i < exclude.Length; i++)
            {
                var ex = exclude[i];
                if (!ex) continue;
                if (t == ex || t.IsChildOf(ex)) return true;
            }
            return false;
        }

        var mrs = root.GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < mrs.Length; i++)
        {
            var r = mrs[i];
            if (!r) continue;
            if (Skip(r.transform)) continue;
            r.enabled = on;
        }

        var sks = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < sks.Length; i++)
        {
            var r = sks[i];
            if (!r) continue;
            if (Skip(r.transform)) continue;
            r.enabled = on;
        }
    }

    private void BroadcastToTypeHandlers(BuildingState state)
    {
        var handlers = GetComponents<IBuildingTypeHandler>();
        for (int i = 0; i < handlers.Length; i++)
            handlers[i]?.OnBuildingStateChanged(state);
    }

    public void TryClearToBaseTile()
    {
        // Use the parent TileControl's transform if available, but we won't add/touch TileControl on the new prefab.
        Vector3 pos = transform.position;
        Quaternion rot = transform.rotation;
        Transform parent = null;

        var currentTile = GetComponentInParent<TileControl>();
        if (currentTile != null)
        {
            pos    = currentTile.transform.position;
            parent = currentTile.transform.parent;
            // Keep rot = transform.rotation (building's rotation) so the replacement
            // environment tile spawns facing the same direction the building did.
        }

        if (environmentTilePrefab == null)
        {
            Debug.LogWarning("[BuildingStatus] environmentTilePrefab not set; destroying building only.");
            Destroy(gameObject);
            return;
        }

        // 1) Spawn the environment host (TileScript-only prefab is OK)
        var host = Instantiate(environmentTilePrefab, pos, rot, parent);

        // 2) Require TileScript on the prefab (no TileControl added)
        var ts = host.GetComponent<TileScript>();
        if (ts == null)
        {
            Debug.LogError("[BuildingStatus] environmentTilePrefab must contain TileScript.");
            Destroy(host);
            Destroy(gameObject);
            return;
        }

        // 3) Try to force-spawn from the Building definition (env + tile), with graceful fallbacks
        var bc = GetComponent<BuildingControl>();
        var def = (bc != null) ? BuildingManager.Instance?.GetBuildingByID(bc.buildingID) : null;

        bool spawned = false;

        if (def != null)
        {
            // Exact env+tile matches first
            if (def.requiredEnvironmentTypes != null && def.requiredEnvironmentTypes.Count > 0 &&
                def.requiredEnvironmentTileTypes != null && def.requiredEnvironmentTileTypes.Count > 0)
            {
                for (int i = 0; i < def.requiredEnvironmentTypes.Count && !spawned; i++)
                {
                    for (int j = 0; j < def.requiredEnvironmentTileTypes.Count && !spawned; j++)
                    {
                        spawned = ts.ForceSpawnSpecific(
                            def.requiredEnvironmentTypes[i],
                            def.requiredEnvironmentTileTypes[j]
                        );
                    }
                }
            }

            // Fallback: tile-type only
            if (!spawned && def.requiredEnvironmentTileTypes != null && def.requiredEnvironmentTileTypes.Count > 0)
            {
                for (int j = 0; j < def.requiredEnvironmentTileTypes.Count && !spawned; j++)
                {
                    spawned = ts.ForceSpawnSpecificTileType(def.requiredEnvironmentTileTypes[j]);
                }
            }
        }

        // Final fallback: let TileScript choose based on its own rules
        if (!spawned)
            ts.SpawnEnvironmentTile();

        // 4) Remove the building
        Destroy(gameObject);
    }

    public BuildingStatusRuntimeSaveData CaptureRuntimeSaveData()
    {
        return new BuildingStatusRuntimeSaveData
        {
            currentState = CurrentState,
            autoClearAfterTurns = autoClearAfterTurns,
            destroyedTurnsElapsed = _destroyedTurns
        };
    }

    public void ApplyRuntimeSaveData(BuildingStatusRuntimeSaveData data)
    {
        if (data == null)
            return;

        autoClearAfterTurns = Mathf.Max(0, data.autoClearAfterTurns);

        SetState(data.currentState);

        if (data.currentState == BuildingState.Destroyed)
            _destroyedTurns = Mathf.Max(0, data.destroyedTurnsElapsed);
        else
            _destroyedTurns = 0;
    }
}