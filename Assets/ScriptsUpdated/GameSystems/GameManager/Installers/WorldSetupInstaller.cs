using UnityEngine;
using UnityEngine.SceneManagement;

public class WorldSetupInstaller : MonoBehaviour
{
    [Header("World Core")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private EnvironmentPresetManager environmentPresetManager;
    [SerializeField] private MapGenerator mapGenerator;
    [SerializeField] private MapTilePlacer mapTilePlacer;
    [SerializeField] private TileActivator tileActivator;
    [SerializeField] private TileUIResolveCoordinator tileUIResolveCoordinator;
    [SerializeField] private SeasonManager seasonManager;

    [Header("World Load Helpers")]
    [SerializeField] private SavedTilePlacer savedTilePlacer;
    [SerializeField] private MonoEnvironmentDataSource envDataSource;

    public Scene LoadedScene => gameObject.scene;

    public GridManager GridManager => gridManager;
    public EnvironmentPresetManager EnvironmentPresetManager => environmentPresetManager;
    public MapGenerator MapGenerator => mapGenerator;
    public MapTilePlacer MapTilePlacer => mapTilePlacer;
    public TileActivator TileActivator => tileActivator;
    public TileUIResolveCoordinator TileUIResolveCoordinator => tileUIResolveCoordinator;
    public SeasonManager SeasonManager => seasonManager;
    public SavedTilePlacer SavedTilePlacer => savedTilePlacer;
    public MonoEnvironmentDataSource EnvDataSource => envDataSource;

    private void OnValidate()
    {
        if (mapGenerator != null && gridManager != null)
            mapGenerator.gridManager = gridManager;

        if (mapTilePlacer != null)
        {
            if (mapGenerator != null)
                mapTilePlacer.mapGenerator = mapGenerator;

            if (gridManager != null)
                mapTilePlacer.gridManager = gridManager;
        }

        if (tileActivator != null)
        {
            if (mapGenerator != null)
                tileActivator.mapGenerator = mapGenerator;

            if (gridManager != null)
                tileActivator.gridManager = gridManager;
        }
    }
}