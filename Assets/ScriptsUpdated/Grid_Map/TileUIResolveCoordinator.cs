using System.Collections;
using UnityEngine;

public class TileUIResolveCoordinator : MonoBehaviour
{
    [SerializeField] private TileActivator tileActivator;

    private void OnEnable()
    {
        if (tileActivator != null)
            tileActivator.OnTilesActivated += HandleTilesActivated;

        TileLifecycleEvents.OnTileEnvironmentSpawned += HandleTileEnvEvent;
        TileLifecycleEvents.OnTileEnvironmentChanged += HandleTileEnvEvent;
    }

    private void OnDisable()
    {
        if (tileActivator != null)
            tileActivator.OnTilesActivated -= HandleTilesActivated;

        TileLifecycleEvents.OnTileEnvironmentSpawned -= HandleTileEnvEvent;
        TileLifecycleEvents.OnTileEnvironmentChanged -= HandleTileEnvEvent;
    }

    private void HandleTilesActivated()
    {
        // wait 1 frame so hierarchy/parents are final
        StartCoroutine(ResolveAllNextFrame());
    }

    private IEnumerator ResolveAllNextFrame()
    {
        yield return null;

        foreach (var ui in FindObjectsOfType<TileMovementUI>(true))
            ui.ResolveNow();

        foreach (var ui in FindObjectsOfType<TileAnimalUI>(true))
            ui.ResolveNow();
    }

    private void HandleTileEnvEvent(TileScript tile)
    {
        if (tile == null) return;
        StartCoroutine(ResolveThisTileNextFrame(tile));
    }

    private IEnumerator ResolveThisTileNextFrame(TileScript tile)
    {
        yield return null;

        foreach (var ui in tile.GetComponentsInChildren<TileMovementUI>(true))
            ui.ResolveNow();

        foreach (var ui in tile.GetComponentsInChildren<TileAnimalUI>(true))
            ui.ResolveNow();
    }
}
