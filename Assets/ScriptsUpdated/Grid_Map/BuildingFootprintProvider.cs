using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BuildingFootprintProvider : MonoBehaviour
{
    public enum AnchorMode
    {
        OriginIsBottomLeft = 0,
        OriginIsCenter = 1
    }

    [Header("Grid Anchor")]
    [SerializeField] private Transform origin;
    [SerializeField] private AnchorMode anchorMode = AnchorMode.OriginIsBottomLeft;

    [Header("Rectangular Footprint")]
    [Min(1)][SerializeField] private int widthCells = 1;
    [Min(1)][SerializeField] private int heightCells = 1;

    [Header("Optional Extra Shape Offsets")]
    [SerializeField] private bool useExtraOffsets = false;
    [SerializeField] private List<Vector2Int> extraLocalCellOffsets = new List<Vector2Int>();

    public bool TryGetCoveredCells(GridManager gridManager, List<TileCoord> results)
    {
        results.Clear();

        if (gridManager == null)
            return false;

        Transform anchor = origin != null ? origin : transform;
        Vector2Int anchorCell = gridManager.GetGridPosition(anchor.position);

        int startX = anchorCell.x;
        int startY = anchorCell.y;

        if (anchorMode == AnchorMode.OriginIsCenter)
        {
            startX = anchorCell.x - (widthCells / 2);
            startY = anchorCell.y - (heightCells / 2);
        }

        for (int x = 0; x < widthCells; x++)
        {
            for (int y = 0; y < heightCells; y++)
            {
                int cellX = startX + x;
                int cellY = startY + y;

                if (cellX < 0 || cellX >= gridManager.columns || cellY < 0 || cellY >= gridManager.rows)
                    continue;

                TileCoord coord = new TileCoord(cellX, cellY);
                if (!results.Contains(coord))
                    results.Add(coord);
            }
        }

        if (useExtraOffsets && extraLocalCellOffsets != null)
        {
            for (int i = 0; i < extraLocalCellOffsets.Count; i++)
            {
                Vector2Int offset = extraLocalCellOffsets[i];

                int cellX = startX + offset.x;
                int cellY = startY + offset.y;

                if (cellX < 0 || cellX >= gridManager.columns || cellY < 0 || cellY >= gridManager.rows)
                    continue;

                TileCoord coord = new TileCoord(cellX, cellY);
                if (!results.Contains(coord))
                    results.Add(coord);
            }
        }

        return results.Count > 0;
    }
}