using UnityEngine;

[DisallowMultipleComponent]
public class BuildingInstance : MonoBehaviour
{
    [Tooltip("Definition used to spawn this building.")]
    public Building definition;

    [Tooltip("Generated unique id for this placed instance.")]
    public string instanceId;

    [Tooltip("Was this placed as the game’s starter shelter?")]
    public bool isStarter;

    private void Awake()
    {
        if (string.IsNullOrEmpty(instanceId))
            instanceId = System.Guid.NewGuid().ToString();
    }

    private void OnEnable()
    {
        PlayerBuildingManager.Instance?.Register(this);
    }

    private void OnDisable()
    {
        // Play-mode only: don’t unregister on prefab edits
        if (Application.isPlaying)
            PlayerBuildingManager.Instance?.Unregister(this);
    }
}