using System.Collections.Generic;
using UnityEngine;
public partial class EnvironmentResourceNode : MonoBehaviour
{

}

[System.Serializable]
public class ResourceSpawnEntry
{
    public ResourceDefinition definition;
    public int amount;
    public int maxAmount; // the original cap for this resource on the node

    [HideInInspector] public int turnsSinceLastRegeneration;
    [HideInInspector] public int turnsSinceLastSpoilage;

    public void Initialize(int assignedAmount)
    {
        maxAmount = assignedAmount;
        amount    = assignedAmount;
        turnsSinceLastRegeneration = 0;
        turnsSinceLastSpoilage     = 0;
    }
}