using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance;

    [Header("Grid Size")]
    public int columns = 10;
    public int rows = 10;
    public float cellSize = 2f;

    [Header("Draw Gizmo")]
    public Color gridColor = Color.green;

    private Vector3 origin = Vector3.zero;

    // Array to track whether a cell is occupied
    private bool[,] gridOccupied;

    private void Awake()
    {
        // Ensure there’s only one instance of GridManager
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        DontDestroyOnLoad(gameObject); // Optional: Keep across scenes if necessary

        // ✅ Initialize gridOccupied here
        gridOccupied = new bool[columns, rows];
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = gridColor;
        // draw the grid lines
        for (int x = 0; x <= columns; x++)
        {
            Gizmos.DrawLine(origin + new Vector3(x * cellSize, 0, 0),
                            origin + new Vector3(x * cellSize, 0, rows * cellSize));
        }
        for (int y = 0; y <= rows; y++)
        {
            Gizmos.DrawLine(origin + new Vector3(0, 0, y * cellSize),
                            origin + new Vector3(columns * cellSize, 0, y * cellSize));
        }

        // if the grid hasn't been initialized yet, skip
        if (gridOccupied == null) return;

        // // choose two contrasting colors
        // Color occColor   = new Color(1, 0, 0, 0.5f);   // semi-transparent red
        // Color freeColor  = new Color(0, 1, 0, 0.1f);   // very faint green

        // // draw a little cube in each cell center
        // for (int x = 0; x < columns; x++)
        // {
        //     for (int y = 0; y < rows; y++)
        //     {
        //         bool occ = gridOccupied[x, y];
        //         Gizmos.color = occ ? occColor : freeColor;

        //         // center of this cell
        //         Vector3 cellCenter = origin
        //             + new Vector3((x + 0.5f) * cellSize, 0.01f, (y + 0.5f) * cellSize);

        //         // draw a thin cube just above the ground
        //         Gizmos.DrawCube(cellCenter, new Vector3(cellSize * 0.9f, 0.02f, cellSize * 0.9f));
        //     }
        // }
    }

    public void InitializeGrid()
    {
        gridOccupied = new bool[columns, rows];
    }

    public Vector3 GetWorldPosition(int x, int y)
    {
        // Adjust world position to account for the base cell size
        return origin + new Vector3(x * cellSize, 0, y * cellSize);
    }

    public Vector2Int GetGridPosition(Vector3 worldPosition)
    {
        int x = Mathf.FloorToInt((worldPosition - origin).x / cellSize);
        int y = Mathf.FloorToInt((worldPosition - origin).z / cellSize);
        return new Vector2Int(x, y);
    }

    public TileScript GetTileAtPosition(Vector3 worldPosition)
    {
        Vector2Int gridPos = GetGridPosition(worldPosition);
        Collider[] hitColliders = Physics.OverlapBox(worldPosition, new Vector3(cellSize / 2, 1, cellSize / 2));
        foreach (var hitCollider in hitColliders)
        {
            TileScript tile = hitCollider.GetComponent<TileScript>();
            if (tile != null)
            {
                return tile;
            }
        }
        return null;
    }

    public bool IsPositionOccupied(Vector2Int gridPos)
    {
        if (gridPos.x >= 0 && gridPos.x < columns && gridPos.y >= 0 && gridPos.y < rows)
        {
            return gridOccupied[gridPos.x, gridPos.y];
        }
        return false;
    }

    public void MarkCellOccupied(int x, int y)
    {
        if (x >= 0 && x < columns && y >= 0 && y < rows)
        {
            gridOccupied[x, y] = true;
        }
    }

    public void MarkCellUnoccupied(int x, int y)
    {
        if (x >= 0 && x < columns && y >= 0 && y < rows)
        {
            gridOccupied[x, y] = false;
        }
    }

    public bool TryGetCell(Vector3 worldPos, out int x, out int y)
    {
        x = Mathf.FloorToInt(worldPos.x / cellSize);
        y = Mathf.FloorToInt(worldPos.z / cellSize);

        if (x < 0 || x >= columns || y < 0 || y >= rows)
            return false;

        return true;
    }

    public bool IsCellOccupied(int x, int y)
    {
        return gridOccupied[x, y];
    }
}