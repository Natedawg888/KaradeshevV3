using System.Collections.Generic;

public static class AnimalRepellerRegistry
{
    private static readonly HashSet<AnimalRepeller> _active = new HashSet<AnimalRepeller>();

    public static void Register(AnimalRepeller r)   { if (r != null) _active.Add(r); }
    public static void Unregister(AnimalRepeller r) { if (r != null) _active.Remove(r); }
    public static IReadOnlyCollection<AnimalRepeller> Active => _active;
}
