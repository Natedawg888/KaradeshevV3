using UnityEngine;

[DisallowMultipleComponent]
public class EnvironmentTileRegistrar : MonoBehaviour
{
    private static GridManager cachedGridManager;

    private TileCoord _coord;
    private bool _hasCoord;
    private EnvironmentControl _currentEnv;

    private void Awake()
    {
        if (cachedGridManager == null)
            cachedGridManager = FindObjectOfType<GridManager>();

        if (cachedGridManager != null)
        {
            Vector2Int gridPos = cachedGridManager.GetGridPosition(transform.position);
            _coord = new TileCoord(gridPos.x, gridPos.y);
            _hasCoord = true;
        }
        else
        {
            Debug.LogWarning("[EnvTileRegistrar] No GridManager found; cannot compute tile coord.");
        }
    }

    private void OnEnable()
    {
        RefreshRegistration();
    }

    private void OnDisable()
    {
        if (!_hasCoord || MonoEnvironmentDataSource.Instance == null || _currentEnv == null)
            return;

        MonoEnvironmentDataSource.Instance.Unregister(_coord, _currentEnv);
    }

    // 🔹 THIS IS THE IMPORTANT NEW BIT
    private void OnTransformChildrenChanged()
    {
        // Called when the pooled environment prefab is attached / removed
        RefreshRegistration();
    }

    public void RefreshRegistration()
    {
        if (!_hasCoord || MonoEnvironmentDataSource.Instance == null)
            return;

        var env = GetComponentInChildren<EnvironmentControl>(true);

        // If we previously had an env but now don't, remove it
        if (env == null)
        {
            if (_currentEnv != null)
            {
                MonoEnvironmentDataSource.Instance.Unregister(_coord, _currentEnv);
                _currentEnv = null;
            }
            return;
        }

        _currentEnv = env;
        MonoEnvironmentDataSource.Instance.RegisterOrUpdate(_coord, env);
    }
}