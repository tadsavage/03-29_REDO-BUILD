using UnityEngine;

public static class PlacementMath
{
    public static float CellSize = 1.33f;   // 5 feet

    public static Vector3 SnapToGrid(Vector3 worldPos)
    {
        // Return nearest snapped position
        return Vector3.zero;
    }

    public static Vector2Int WorldToCell(Vector3 worldPos)
    {
        // Convert world position to cell coordinates
        return Vector2Int.zero;
    }
    public static Vector2Int GetRotatedFootprint(Vector2Int footprint, float rotation)
    {
        // Normalize rotation to 0, 90, 180, 270
        int r = Mathf.RoundToInt(rotation) % 360;
        // Odd rotations (90, 270) swap X and Y
        if (r == 90 || r == 270 )
            return new Vector2Int(footprint.y, footprint.x);
        return footprint;
    }

}
