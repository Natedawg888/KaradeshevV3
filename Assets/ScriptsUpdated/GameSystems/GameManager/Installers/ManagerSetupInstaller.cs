using UnityEngine;
using UnityEngine.SceneManagement;

public class ManagerSetupInstaller : MonoBehaviour
{
    [Header("Manager Core")]
    [SerializeField] private LevelManager levelManager;
    [SerializeField] private BuildingManager buildingManager;
    [SerializeField] private GeneralPopulationManager generalPopulationManager;
    [SerializeField] private TechnologyManager technologyManager;
    [SerializeField] private CraftingRecipeManager craftingRecipeManager;
    [SerializeField] private ProductionPlanManager productionPlanManager;

    public Scene LoadedScene => gameObject.scene;

    public LevelManager LevelManager => levelManager;
    public BuildingManager BuildingManager => buildingManager;
    public GeneralPopulationManager GeneralPopulationManager => generalPopulationManager;
    public TechnologyManager TechnologyManager => technologyManager;
    public CraftingRecipeManager CraftingRecipeManager => craftingRecipeManager;
    public ProductionPlanManager ProductionPlanManager => productionPlanManager;
}