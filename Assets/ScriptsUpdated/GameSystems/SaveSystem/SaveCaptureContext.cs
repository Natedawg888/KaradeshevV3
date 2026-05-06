using UnityEngine;

public sealed class SaveCaptureContext
{
    public CameraControl CameraControl { get; }
    public PlayerPopulationStatistic PopulationStatistic { get; }
    public AnimalSimulationController AnimalController { get; }

    public SaveCaptureContext(
        CameraControl cameraControl,
        PlayerPopulationStatistic populationStatistic,
        AnimalSimulationController animalController)
    {
        CameraControl = cameraControl;
        PopulationStatistic = populationStatistic;
        AnimalController = animalController;
    }
}