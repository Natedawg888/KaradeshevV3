using UnityEngine;

public class SceneStartupProbe : MonoBehaviour
{
    private static float _sceneStartReference = -1f;

    private void Awake()
    {
        if (_sceneStartReference < 0f)
            _sceneStartReference = Time.realtimeSinceStartup;

        Debug.Log($"[SceneProbe] Awake on {name}: {Time.realtimeSinceStartup:0.000}s");
    }

    private void OnEnable()
    {
        Debug.Log($"[SceneProbe] OnEnable on {name}: {Time.realtimeSinceStartup:0.000}s");
    }

    private void Start()
    {
        Debug.Log($"[SceneProbe] Start on {name}: {Time.realtimeSinceStartup:0.000}s");
    }
}