using System.Collections.Generic;

public partial class AnimalSimulation
{
    private AnimalSimulationSaveData _cachedSaveState;
    private bool _cachedSaveStateValid;

    public void InvalidateSaveCache()
    {
        _cachedSaveStateValid = false;
    }

    public void RebuildCachedSaveState()
    {
        AnimalSimulationSaveData rebuilt = new AnimalSimulationSaveData
        {
            nextGroupId = _nextGroupId
        };

        foreach (var kvp in _groups)
        {
            AnimalGroupState g = kvp.Value;
            if (g == null || g.species == null)
                continue;

            rebuilt.groups.Add(BuildGroupSaveData(g));
        }

        PendingSpawn[] queued = _pendingSpawns.ToArray();
        for (int i = 0; i < queued.Length; i++)
        {
            AnimalGroupState template = queued[i].template;
            if (template == null || template.species == null)
                continue;

            rebuilt.pendingSpawnTemplates.Add(BuildGroupSaveData(template));
        }

        _cachedSaveState = rebuilt;
        _cachedSaveStateValid = true;
    }
}