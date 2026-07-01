using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared helpers for reasoning about racks in grid-cell space.
/// Collection detection and chevron placement both need to know exactly which
/// tile cells a rack occupies and which axis it runs on — this keeps that logic
/// in one place and consistent with PlacementGrid.RebuildFromRegistry().
/// </summary>
public static class RackGridUtil
{
    /// <summary>
    /// Rotation of a rack expressed in 90° steps (0..3), derived from its transform.
    /// Independent of PlacedObject.rotation being set, so it works the instant a
    /// rack is placed as well as for objects restored from a save.
    /// </summary>
    public static int RotationStep(GameObject rack)
    {
        int step = Mathf.RoundToInt(rack.transform.eulerAngles.y / 90f);
        return ((step % 4) + 4) % 4;
    }

    /// <summary>
    /// True when two racks run on the same axis (both "vertical" or both "horizontal").
    /// A 180° flip still counts as the same direction; a 90° turn does not.
    /// </summary>
    public static bool SameAxis(GameObject a, GameObject b)
    {
        return (RotationStep(a) % 2) == (RotationStep(b) % 2);
    }

    /// <summary>
    /// The set of grid cells this rack occupies. Uses world position → root cell
    /// (the authoritative path RebuildFromRegistry relies on) plus the rotated
    /// footprint offsets, so multi-cell bays are represented correctly.
    /// </summary>
    public static List<Vector2Int> GetCells(GameObject rack, PlacementGrid grid)
    {
        var cells = new List<Vector2Int>();
        if (rack == null || grid == null)
            return cells;

        Vector2Int root = grid.WorldToCell(rack.transform.position);

        var po = rack.GetComponent<PlacedObject>();
        if (po == null || po.data == null)
        {
            cells.Add(root);
            return cells;
        }

        float rotDeg = RotationStep(rack) * 90f;
        Vector2Int[] offsets = po.data.GetFootprintOffsets(-rotDeg);
        if (offsets == null || offsets.Length == 0)
        {
            cells.Add(root);
            return cells;
        }

        foreach (var o in offsets)
            cells.Add(root + o);

        return cells;
    }
}
