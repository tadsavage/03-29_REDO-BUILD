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

        // --- 1. Ensure all footprint cells are inside the grid ---
        foreach (var offset in offsets)
        {
            Vector2Int cell = root + offset;
            if (!_grid.IsInsideGrid(cell))
                return false;
        }

        // --- 2. LEVEL SURFACE CHECK ---
        // Determine the base height under the first footprint cell
        Vector2Int firstCell = root + offsets[0];
        float baseHeight = _grid.GetStackHeight(firstCell, ignore);

        // All other footprint cells must match this height
        for (int i = 1; i < offsets.Length; i++)
        {
            Vector2Int cell = root + offsets[i];
            float h = _grid.GetStackHeight(cell, ignore);

            if (!Mathf.Approximately(h, baseHeight))
                return false;
        }

        // --- 3. PER-CELL VALIDATION ---
        foreach (var offset in offsets)
        {
            Vector2Int cell = root + offset;
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
        if (data.ignorePlacementRules)
            return true;

        if (!_grid.IsInsideGrid(cell))
            return false;

        var list = _grid.GetObjectsInCell(cell);
        if (list == null || list.Count == 0)
            return true;

        float existingHeight = _grid.GetStackHeight(cell, ignore);

        foreach (var entry in list)
        {
            if (entry.instance == ignore)
                continue;

            // Floors never block anything
            if (entry.data.isFloor)
                continue;

            // Objects that ignore rules or clear grid never block anything
            if (entry.data.ignorePlacementRules || entry.data.ClearsGridAfterPlacement)
                continue;

            // --- OVERLAP CHECK ---
            // If there is any non-floor object here, and we aren't stacking, then we are overlapping.
            if (!data.isStackable)
                return false;

            // --- VERTICAL STACK CHECK ---
            // If the existing object is not stackable, we cannot place anything on top of it.
            if (!entry.data.isStackable)
                return false;

            // Finally check if we've reached the maximum stack height
            if (!_grid.CanStack(cell, data))
                return false;
        }

        return true;
    }
}
