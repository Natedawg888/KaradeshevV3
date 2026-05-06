using UnityEngine;

public enum TileEdge
{
    North = 0,
    East = 1,
    South = 2,
    West = 3,

    // corners (added at end to preserve existing serialized values)
    NorthEast = 4,
    NorthWest = 5,
    SouthEast = 6,
    SouthWest = 7,
}

[System.Serializable]
public class EnvironmentTileVariant
{
    public EnvironmentType environmentType;
    public GameObject prefab;

    [Header("Neighbour Env Filter (preferred)")]
    public EnvironmentNeighbourFilterSO neighborEnvFilter;

    [Header("Legacy (optional): only used if Neighbor Env Filter is NULL")]
    public EnvironmentType[] allowedNeighborEnvironmentTypes;
    public EnvironmentType[] disallowedNeighborEnvironmentTypes;
}

[CreateAssetMenu(menuName = "World/Environment Tile Option", fileName = "EnvTileOption_")]
public class EnvironmentTileOptionSO : ScriptableObject
{
    public string optionName;
    public EnvironmentTileType tileType;

    [Header("Adjacency Groups (OR) — drives which SHAPE + ROTATION wins")]
    public AdjacencyGroup[] adjacencyGroups;

    [Header("Variants — same adjacency/rotation, different prefabs by biome")]
    public EnvironmentTileVariant[] variants;
}
