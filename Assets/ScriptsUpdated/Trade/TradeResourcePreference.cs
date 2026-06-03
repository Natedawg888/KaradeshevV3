using System;
using UnityEngine;

[Serializable]
public class TradeResourcePreference
{
    [Tooltip("The resource this trader values.")]
    public ResourceDefinition resource;

    [Tooltip("Multiplier applied when evaluating player offers containing this resource. >1 means trader values it more.")]
    [Range(0.01f, 100f)]
    public float valueMultiplier = 1f;
}

