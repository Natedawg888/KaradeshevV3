public interface IBuildingTypeHandler
{
    /// The building type this component handles.
    BuildingType HandledType { get; }

    /// Called when this type becomes active on the building.
    void OnTypeEnabled();

    /// Called when this type is no longer active on the building.
    void OnTypeDisabled();

    /// Notified any time the building state changes (Normal/Damaged/Destroyed).
    void OnBuildingStateChanged(BuildingState state);
}
