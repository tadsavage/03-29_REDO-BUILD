using UnityEngine;

public class PlacementValidator : MonoBehaviour
{
    [SerializeField] private PlacementGrid _grid;

    // =========================================================
    //  FULL FOOTPRINT VALIDATION (used for actual placement)
    // =========================================================
    public bool IsValidPlacement(
        Vector2Int root,
        Vector2Int[] offsets,
        ObjDataSO data,
        GameObject ignore = null)
    {
        // Bulldozer / special objects that ignore all rules
        if (data.ignorePlacementRules)
            return true;

        foreach (var offset in offsets)
        {
            Vector2Int cell = root + offset;

            if (!_grid.IsInsideGrid(cell))
                return false;

            if (!IsSingleCellValid(cell, data, ignore))
                return false;
        }

        return true;
    }

    // =========================================================
    //  PER-CELL VALIDATION (used for indicators)
    // =========================================================
    public bool IsCellValid(
        Vector2Int cell,
        ObjDataSO data,
        GameObject ignore = null)
    {
        return IsSingleCellValid(cell, data, ignore);
    }

    // =========================================================
    //  INTERNAL: VALIDATE ONE CELL ONLY
    // =========================================================
    private bool IsSingleCellValid(Vector2Int cell, ObjDataSO data, GameObject ignore)
    {
        // Bulldozer-type objects ignore all rules
        if (data.ignorePlacementRules)
            return true;

        if (!_grid.IsInsideGrid(cell))
            return false;

        var list = _grid.GetObjectsInCell(cell);
        if (list == null || list.Count == 0)
            return true;

        foreach (var entry in list)
        {
            if (entry.instance == ignore)
                continue;

            // Floors never block anything
            if (entry.data.isFloor)
                continue;

            // Objects that ignore rules never block anything
            if (entry.data.ignorePlacementRules)
                continue;

            // If either object is non-stackable → invalid
            if (!entry.data.isStackable || !data.isStackable)
                return false;

            // Both stackable → obey stack height
            if (!_grid.CanStack(cell, data))
                return false;
        }

        return true;
    }
}
