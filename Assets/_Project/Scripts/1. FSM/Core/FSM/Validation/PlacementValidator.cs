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
        // Bulldozer / special objects that ignore all rules — EXCEPT a height-aware fixture (see
        // blockedByTallObstructions) still refuses a cell a Wall/Rack is tall enough to occupy.
        if (data.ignorePlacementRules)
        {
            if (data.blockedByTallObstructions)
            {
                foreach (var o in offsets)
                {
                    if (IsBlockedByTallObstruction(root + o, data, ignore))
                        return false;
                }
            }
            return true;
        }

        int coreCount = Mathf.Min(data.CoreFootprintCellCount, offsets.Length);

        // --- 1. Ensure the CORE footprint cells are inside the grid ---
        // Buffer cells (offsets[coreCount..]) are exempt: they're a placement-blocking reservation,
        // not real mesh area, and a dock door's truck apron routinely reaches past the grid's own
        // edge onto plain yard ground the grid array was never sized to cover (a dock built at the
        // building's perimeter, by definition, has its apron pointing OFF the buildable footprint).
        // Rejecting the whole placement just because that reservation fell outside the grid's bounds
        // is the same class of bug the level-surface exemption below already fixed for buffer cells —
        // this is the "inside grid" analogue of it. A buffer cell that IS inside the grid still goes
        // through the normal per-cell overlap check in step 3, so it still blocks real obstacles.
        for (int i = 0; i < coreCount; i++)
        {
            Vector2Int cell = root + offsets[i];
            if (!_grid.IsInsideGrid(cell))
                return false;
        }

        // --- 2. LEVEL SURFACE CHECK ---
        // When placing a door that replaces walls, exclude replaceable-wall heights from
        // the comparison so that partial-wall coverage doesn't fail the check.
        bool doorReplacingWalls = data.replacesWalls;

        // Only the object's OWN footprint (customShapeOffsets/footprint) needs a level surface --
        // bufferOffsets cells (e.g. a dock's truck apron reaching out onto plain yard ground) are
        // placement-blocking reservations, not real mesh area, and routinely span a height seam
        // (raised dock slab vs. yard at y=0). Checking them here rejected every dock-door placement
        // with a real apron the moment ObjDataSO.bufferOffsets started reserving ground past the door.

        Vector2Int firstCell = root + offsets[0];
        float baseHeight = doorReplacingWalls
            ? _grid.GetStackHeightIgnoringWalls(firstCell, ignore)
            : _grid.GetStackHeight(firstCell, ignore);

        for (int i = 1; i < coreCount; i++)
        {
            Vector2Int cell = root + offsets[i];
            float h = doorReplacingWalls
                ? _grid.GetStackHeightIgnoringWalls(cell, ignore)
                : _grid.GetStackHeight(cell, ignore);

            if (!Mathf.Approximately(h, baseHeight))
                return false;
        }

        // --- 3. PER-CELL VALIDATION ---
        // IsSingleCellValid itself rejects any cell outside the grid, so a buffer cell that falls off
        // the grid's edge (see step 1's comment) needs the same exemption here, or step 1 relaxing the
        // bounds check would just have this loop reject it a moment later. A buffer cell that's still
        // inside the grid isn't exempt from anything — it goes through the normal overlap check.
        for (int i = 0; i < offsets.Length; i++)
        {
            Vector2Int cell = root + offsets[i];
            if (i >= coreCount && !_grid.IsInsideGrid(cell))
                continue;
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

    /// <summary>
    /// For a height-placed fixture that otherwise ignores every placement rule (see
    /// ObjDataSO.blockedByTallObstructions — e.g. a ceiling light): the one thing that still blocks
    /// it is a Wall or Racking object physically tall enough to reach the fixture's own placement
    /// height. Compares REAL rendered geometry (the existing object's actual top, from its renderer
    /// bounds) against PlacementFinalizer.GetFloorTopY(cell) + data.worldYOffset — the exact same
    /// formula the finalizer uses to place the fixture — rather than trusting ObjDataSO.objHeight,
    /// which isn't reliably in the same units/scale as the real mesh (verified: Wall.asset's
    /// objHeight is 9, nowhere near a real 9-metre wall). A rack's real top height also varies by
    /// how many levels are stacked, which only the live renderer bounds can answer correctly.
    /// </summary>
    private bool IsBlockedByTallObstruction(Vector2Int cell, ObjDataSO data, GameObject ignore)
    {
        var list = _grid.GetObjectsInCell(cell);
        if (list == null || list.Count == 0) return false;

        float fixtureY = PlacementFinalizer.GetFloorTopY(_grid, cell) + data.worldYOffset;

        foreach (var entry in list)
        {
            if (entry.instance == ignore) continue;
            if (entry.instance == null || !entry.instance.activeSelf || entry.data == null) continue;
            if (entry.data.category != "Walls" && entry.data.category != "Racking") continue;

            float entryTopY = 0f;
            foreach (var r in entry.instance.GetComponentsInChildren<Renderer>())
                entryTopY = Mathf.Max(entryTopY, r.bounds.max.y);

            if (entryTopY >= fixtureY)
                return true;
        }
        return false;
    }

    // =========================================================
    //  INTERNAL: VALIDATE ONE CELL ONLY
    // =========================================================
    private bool IsSingleCellValid(Vector2Int cell, ObjDataSO data, GameObject ignore)
    {
        if (data.ignorePlacementRules)
        {
            if (data.blockedByTallObstructions && IsBlockedByTallObstruction(cell, data, ignore))
                return false;
            return true;
        }

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

            // 1. Placing Ground/Foundation: only allowed to sit on top of Floor tiles.
            // Another Foundation/Ground already here always blocks placement — letting
            // two ground-category objects share a cell corrupts the stack order (index 0
            // is reserved for exactly one Foundation/Ground) and is what caused placed
            // foundations to merge meshes and leave behind orphaned tiles on delete.
            if (isPlacingGround)
            {
                if (entryIsGround || !entry.data.isFloor) return false;
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
