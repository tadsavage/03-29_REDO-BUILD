using UnityEngine;

public class PlacementValidator : MonoBehaviour
{
    [SerializeField] private PlacementGrid _grid;

    public bool IsValidPlacement(Vector2Int root, Vector2Int[] offsets)
    {
        foreach (var offset in offsets)
        {
            Vector2Int cell = root + offset;

            if (!_grid.IsInsideGrid(cell))
                return false;

            if (_grid.IsOccupied(cell))
                return false;
        }

        return true;
    }
}
