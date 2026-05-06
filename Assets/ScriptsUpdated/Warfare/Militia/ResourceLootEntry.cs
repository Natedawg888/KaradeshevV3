using System;
using UnityEngine;

[Serializable]
public struct ResourceLootEntry
{
    public ResourceDefinition resource;

    [Min(0)]
    public int amountPerKill;
}
