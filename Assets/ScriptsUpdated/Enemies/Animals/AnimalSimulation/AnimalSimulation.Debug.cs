public partial class AnimalSimulation
{
    // [Conditional] causes the compiler to remove ALL call sites (including their $"..." args)
    // when ANIMAL_DEBUG_ENABLED is not defined — zero GC alloc from logging in release builds.
    [System.Diagnostics.Conditional("ANIMAL_DEBUG_ENABLED")]
    private void LogAnimalEvent(string tag, AnimalGroupState group, string message)
    {
        // re-enable body and add the Debug.Log back if you define ANIMAL_DEBUG_ENABLED
    }

    [System.Diagnostics.Conditional("ANIMAL_DEBUG_ENABLED")]
    private void LogAnimalVsAnimal(string tag, AnimalGroupState actor, AnimalGroupState other, string message)
    {
    }
}
