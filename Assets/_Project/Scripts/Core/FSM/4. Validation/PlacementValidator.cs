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
        // When placing a door that replaces walls, exclude replaceable-wall heights from
        // the comparison so that partial-wall coverage doesn't fail the check.
        bool doorReplacingWalls = data.replacesWalls;

        Vector2Int firstCell = root + offsets[0];
        float baseHeight = doorReplacingWalls
            ? _grid.GetStackHeightIgnoringWalls(firstCell, ignore)
            : _grid.GetStackHeight(firstCell, ignore);

        for (int i = 1; i < offsets.Length; i++)
        {
            Vector2Int cell = root + offsets[i];
            float h = doorReplacingWalls
                ? _grid.GetStackHeightIgnoringWalls(cell, ignore)
                : _grid.GetStackHeight(cell, ignore);

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

    private bool IsGround(ObjDataSO data)
    {
        if (data == null) return false;
        return data.category == "Foundation" || data.category == "Grounds";
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

        bool isPlacingGround = IsGround(data);

        foreach (var entry in list)
        {
            if (entry.instance == ignore)
                continue;

            // Skip destroyed or inactive entries (e.g. objects disabled by Undo)
            if (entry.instance == null || !entry.instance.activeSelf)
                continue;

            bool entryIsGround = IsGround(entry.data);

            // 1. Placing Ground: can replace other Grounds or sit on top of Floor tiles.
            // Anything else (normal objects, etc.) blocks ground placement.
            if (isPlacingGround)
            {
                if (!entryIsGround && !entry.data.isFloor) return false;
                continue;
            }

            // 2. Placing something else:
            // Allowed to overlap with Ground or Floor.
            if (entryIsGround || entry.data.isFloor)
                continue;

            // SPECIAL CASE: Floors are allowed to overlap with anything (they shuffle to the bottom)
            if (data.isFloor)
                continue;

            // Objects that ignore rules or clear grid never block anything
            if (entry.data.ignorePlacementRules || entry.data.ClearsGridAfterPlacement)
                continue;

            // Door ↔ wall mutual ghosting:
            //   • A door (replacesWalls) ghosts through replaceable walls
            //   • A wall (canBeReplacedByDoor) ghosts through doors
            if (data.replacesWalls && entry.data.canBeReplacedByDoor) continue;
            if (data.canBeReplacedByDoor && entry.data.replacesWalls) continue;

            // --- OVERLAP CHECK ---
            // If there is any non-floor/non-ground object here, and we aren't stacking, then we are overlapping.
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
