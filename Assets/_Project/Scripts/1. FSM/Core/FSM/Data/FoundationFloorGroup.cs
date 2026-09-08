using UnityEngine;

/// <summary>
/// Sits on a Foundation/Grounds root next to its BuildingData, holding explicit references to its 4
/// default floor-tile children (in canonical footprint-offset order: (0,0),(1,0),(0,1),(1,1)).
///
/// Exists so every system that needs "which of this cell's floor entries are MY OWN default tiles,
/// as opposed to a swapped-in walkway/staging tile" (placement, move, delete, save/load) has one
/// unambiguous O(1) answer instead of re-deriving it from grid-cell/position matching in five
/// different places — the root cause of most of the Foundation/tile sync bugs before this component
/// existed. A tile is "intact" only while it's both active and still parented here; once swapped out
/// (see PlacementFinalizer.DisableExistingFloors) it becomes an independent object again and every
/// consumer of this component must fall back to the old per-cell rider handling for that one slot.
/// </summary>
public class FoundationFloorGroup : MonoBehaviour
{
    [Tooltip("The 4 default Floor Tile children, in canonical footprint-offset order: (0,0),(1,0),(0,1),(1,1).")]
    [SerializeField] private PlacedObject[] defaultTiles = new PlacedObject[4];

    public PlacedObject[] DefaultTiles => defaultTiles;

    /// <summary>True only while every default tile is present, active, and still parented directly
    /// under this Foundation — i.e. none of the 4 cells have been swapped to a custom tile.</summary>
    public bool AllDefaultTilesIntact()
    {
        if (defaultTiles == null || defaultTiles.Length == 0) return false;
        foreach (var tile in defaultTiles)
        {
            if (tile == null) return false;
            if (!tile.gameObject.activeSelf) return false;
            if (tile.transform.parent != transform) return false;
        }
        return true;
    }

    /// <summary>True if the given PlacedObject is one of this foundation's own default tiles
    /// (regardless of whether it's currently intact/swapped-out) — used to distinguish "this floor
    /// entry belongs to me" from "this is an independent/swapped tile" in per-cell logic.</summary>
    public bool Owns(PlacedObject candidate)
    {
        if (candidate == null || defaultTiles == null) return false;
        foreach (var tile in defaultTiles)
            if (tile == candidate) return true;
        return false;
    }
}
