using UnityEngine;

public class PlacementValidator : MonoBehaviour
{
    [SerializeField] private PlacementGrid _grid;

    // ---------------------------------------------------------
    // VALIDATE FULL FOOTPRINT
    // ---------------------------------------------------------
    public bool IsValidPlacement(
        Vector2Int root,
        Vector2Int[] offsets,
        ObjDataSO data,
        GameObject ignore = null)
    {
        foreach (var offset in offsets)
        {
            Vector2Int cell = root + offset;

            // Out of bounds = invalid
            if (!_grid.IsInsideGrid(cell))
                return false;

            var list = _grid.GetObjectsInCell(cell);

            if (list != null)
            {
                foreach (var entry in list)
                {
                    if (entry.instance == ignore)
                        continue;

                    if (!data.isStackable)
                        return false;
                }
            }
        }

        return true;
    }

    // ---------------------------------------------------------
    // VALIDATE A SINGLE CELL (used for drag placement)
    // ---------------------------------------------------------
    public bool IsCellValid(
        Vector2Int cell,
        Vector2Int[] offsets,
        ObjDataSO data,
        GameObject ignore = null)
    {
        foreach (var offset in offsets)
        {
            Vector2Int c = cell + offset;

            if (!_grid.IsInsideGrid(c))
                return false;

            var list = _grid.GetObjectsInCell(c);

            if (list != null)
            {
                foreach (var entry in list)
                {
                    if (entry.instance == ignore)
                        continue;

                    if (!data.isStackable)
                        return false;
                }
            }
        }

        return true;
    }
}
