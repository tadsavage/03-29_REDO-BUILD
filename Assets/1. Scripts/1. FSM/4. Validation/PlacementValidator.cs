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
        // Ignore rules? Always valid.
        if (data.ignorePlacementRules)
            return true;

        if (!_grid.IsInsideGrid(cell))
            return false;

        var list = _grid.GetObjectsInCell(cell);
        if (list == null || list.Count == 0)
            return true; // empty cell is always valid

        foreach (var entry in list)
        {
            if (entry.instance == ignore)
                continue;

            // Floors do not block placement, but they also should not make the cell auto-valid.
            if (entry.data.isFloor)
                continue;

            // Bulldozer-type objects ignore all rules
            if (entry.data.ignorePlacementRules)
                continue;

            // 🚫 If the existing object is non-stackable, nothing can go on top of it
            if (!entry.data.isStackable)
                return false;

            // 🚫 If the object we’re placing is non-stackable, it can’t go on anything
            if (!data.isStackable)
                return false;

            // ✅ Both are stackable → obey stack rules
            if (!_grid.CanStack(cell, data))
                return false;
        }

        return true;
    }
}
