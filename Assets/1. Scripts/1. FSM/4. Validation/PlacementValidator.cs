using UnityEngine;

public class PlacementValidator : MonoBehaviour
{
    [SerializeField] private PlacementGrid _grid;

    /// <summary>
    /// Validates whether a footprint (root + offsets) can be placed.
    /// Returns TRUE only if ALL footprint cells are:
    /// - inside the grid
    /// - unoccupied
    /// </summary>
    public bool IsValidPlacement(Vector2Int root, Vector2Int[] offsets)
    {
        // Validate each footprint cell
        foreach (var offset in offsets)
        {
            Vector2Int cell = root + offset;

            // Out of bounds = invalid
            if (!_grid.IsInsideGrid(cell))
                return false;

            // Occupied = invalid
            if (_grid.IsOccupied(cell))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Used by drag placement to check a single cell quickly.
    /// This version does NOT require all cells in the rectangle to be valid.
    /// It only checks the footprint for this one cell.
    /// </summary>
    public bool IsCellValid(Vector2Int cell, Vector2Int[] offsets)
    {
        foreach (var offset in offsets)
        {
            Vector2Int c = cell + offset;

            if (!_grid.IsInsideGrid(c))
                return false;

            if (_grid.IsOccupied(c))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Optional helper: checks if ANY cell in a rectangle is valid.
    /// Useful for drag placement if you ever want to preview only valid cells.
    /// </summary>
    public bool AnyValidInRectangle(Vector2Int start, Vector2Int end, Vector2Int[] offsets)
    {
        int minX = Mathf.Min(start.x, end.x);
        int maxX = Mathf.Max(start.x, end.x);
        int minY = Mathf.Min(start.y, end.y);
        int maxY = Mathf.Max(start.y, end.y);

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (IsCellValid(cell, offsets))
                    return true;
            }
        }

        return false;
    }
}
