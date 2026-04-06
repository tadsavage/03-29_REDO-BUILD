using UnityEngine;

public class PlacementValidator : MonoBehaviour
{
    [SerializeField] private PlacementGrid _grid;

    // ---------------------------------------------------------
    // VALIDATE FULL FOOTPRINT
    // ---------------------------------------------------------
    public bool IsValidPlacement(Vector2Int root, Vector2Int[] offsets, ObjDataSO data)
    {
        foreach (var offset in offsets)
        {
            Vector2Int cell = root + offset;

            // Out of bounds = invalid
            if (!_grid.IsInsideGrid(cell))
                return false;

            // ================================
            // STACKING RULE:
            // If object is NOT stackable, occupied cells are invalid.
            // If object IS stackable, occupied cells are allowed.
            // ================================
            if (!data.isStackable && _grid.IsOccupied(cell))
                return false;
        }

        return true;
    }

    // ---------------------------------------------------------
    // VALIDATE A SINGLE CELL (used for drag placement)
    // ---------------------------------------------------------
    public bool IsCellValid(Vector2Int cell, Vector2Int[] offsets, ObjDataSO data)
    {
        foreach (var offset in offsets)
        {
            Vector2Int c = cell + offset;

            if (!_grid.IsInsideGrid(c))
                return false;

            // ================================
            // STACKING RULE:
            // Allow occupied cells only if object is stackable.
            // ================================
            if (!data.isStackable && _grid.IsOccupied(c))
                return false;
        }

        return true;
    }
}
