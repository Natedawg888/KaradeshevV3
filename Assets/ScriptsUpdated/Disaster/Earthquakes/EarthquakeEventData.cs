using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class EarthquakeEventData
{
    public Vector2Int epicentreBlock;
    public float magnitude;
    public float radiusBlocks;
    public bool forced;
    public bool epicentreWasOnFault;

    public List<Vector2Int> affectedBlocks = new List<Vector2Int>();
    public HashSet<Vector2Int> affectedBlockSet = new HashSet<Vector2Int>();

    public IReadOnlyCollection<Vector2Int> faultBlocks;
    public IReadOnlyCollection<Vector2Int> faultInfluenceBlocks;

    public bool ContainsBlock(Vector2Int block)
    {
        return affectedBlockSet != null && affectedBlockSet.Contains(block);
    }
}