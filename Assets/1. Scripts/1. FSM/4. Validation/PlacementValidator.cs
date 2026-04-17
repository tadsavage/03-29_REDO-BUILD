using UnityEngine;

public class PlacementValidator : MonoBehaviour
{
    [SerializeField] private PlacementGrid _grid;

    public bool IsValidPlacement(
        Vector2Int root,
        Vector2Int[] offsets,
        ObjDataSO data,
        GameObject ignore = null)
    {
        // ---------------------------------------------------------
        // FLOOR TILES / RECEIVING LANES IGNORE ALL RULES
        // ---------------------------------------------------------
        if (data.ignorePlacementRules)
            return true;

        // ---------------------------------------------------------
        // NORMAL OBJECT VALIDATION
        // ---------------------------------------------------------
        foreach (var offset in offsets)
        {
            Vector2Int cell = root + offset;

            if (!_grid.IsInsideGrid(cell))
                return false;

            var list = _grid.GetObjectsInCell(cell);
            if (list == null)
                continue;

            foreach (var entry in list)
            {
                if (entry.instance == ignore)
                    continue;

                // Floor tiles do not block anything
                if (entry.data.ignorePlacementRules)
                    continue;

                // Non-stackable objects cannot be placed on anything
                if (!data.isStackable)
                    return false;

                // Stackable objects must obey stack height rules
                if (!_grid.CanStack(cell, data))
                    return false;
            }
        }

        return true;
    }

    public bool IsCellValid(
        Vector2Int cell,
        Vector2Int[] offsets,
        ObjDataSO data,
        GameObject ignore = null)
    {
        return IsValidPlacement(cell, offsets, data, ignore);
    }
}
