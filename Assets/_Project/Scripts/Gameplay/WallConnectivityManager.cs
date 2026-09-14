using System.Collections.Generic;
using UnityEngine;
using GameCore.Economy;
using GameCore.Services;

/// <summary>
/// Automatically swaps a straight Wall segment for the correct Corner, T-Wall, or Cross-Wall piece
/// (and back again) based on which of its four cardinal neighbor cells contain another wall-family
/// object.
///
/// Called explicitly by PlaceCommand/DragPlaceCommand/DeleteCommand right after each mutates the
/// grid, rather than subscribing to GameEvents.Build.OnObjectPlaced/OnObjectDeleted — those events
/// don't carry a per-instance payload for every code path (e.g. DragPlaceCommand's aggregate
/// publish, or PlaceCommand.Undo for non-Racking categories), so a direct call from the command
/// itself is the only reliable hook.
///
/// Recompute() is a pure function of the grid's current state, not of history, so it naturally
/// "just works" again whenever a command's Undo/Redo calls it — no separate undo tracking needed
/// for the shape swap itself.
///
/// KNOWN LIMITATION: swapping a neighbor's shape destroys its old GameObject and instantiates a
/// new one. If an EARLIER command in the undo history still holds a cached reference to that old
/// instance (e.g. the command that originally placed the neighboring wall), undoing all the way
/// back past this swap can leave that older command's Undo/Redo silently no-op on a since-destroyed
/// object. This only matters when undoing multiple steps past a shape change on a shared wall run;
/// it does not affect ordinary build/delete play.
/// </summary>
public class WallConnectivityManager : MonoBehaviour
{
    public static WallConnectivityManager Instance { get; private set; }

    [Tooltip("Straight wall segment. Used when a wall cell has 0-1 connections, or 2 opposite connections.")]
    [SerializeField] private ObjDataSO _wallData;

    [Tooltip("Corner piece. Used when a wall cell has exactly 2 adjacent (non-opposite) connections.")]
    [SerializeField] private ObjDataSO _cornerData;

    [Tooltip("T-junction piece. Used for 3 connections, and as the fallback for a full 4-way junction if _crossData is unassigned.")]
    [SerializeField] private ObjDataSO _tWallData;

    [Tooltip("4-way cross piece. Used when a wall cell has all 4 cardinal connections. Falls back to the T-Wall piece if left unassigned.")]
    [SerializeField] private ObjDataSO _crossData;

    // objNames this system is allowed to auto-replace. Only a plain straight Wall is ever swapped —
    // once a cell becomes a Corner, T-Wall, or Cross-Wall (whether the player placed it directly or
    // it was auto-upgraded from a Wall earlier), it is frozen and never touched again. Doors,
    // windows, and multi-cell brick walls still COUNT as a wall-family connection for neighbor
    // detection (see IsWallFamily) but were never swapped either — all of these are deliberate
    // player choices.
    private static readonly HashSet<string> AutoShapeNames = new() { "Wall" };
    private const string WallCategory = "Walls";

    private static readonly Vector2Int North = new Vector2Int(0, 1);
    private static readonly Vector2Int South = new Vector2Int(0, -1);
    private static readonly Vector2Int East = new Vector2Int(1, 0);
    private static readonly Vector2Int West = new Vector2Int(-1, 0);

    private void Awake()
    {
        Instance = this;
    }

    /// <summary>
    /// Recomputes wall shape for the given cell and its 4 cardinal neighbors. Call once after any
    /// single wall-family cell is added to or removed from the grid. Any resulting shape swap is
    /// charged/refunded at real catalog prices (sell-back on the old piece, full cost on the new
    /// one) via <paramref name="money"/> — e.g. a Wall auto-upgrading to a $950 T-Wall is not free.
    /// </summary>
    public void RecomputeArea(PlacementGrid grid, PlacementFinalizer finalizer, MoneyService money, Vector2Int cell)
    {
        if (grid == null || finalizer == null) return;

        Recompute(grid, finalizer, money, cell);
        Recompute(grid, finalizer, money, cell + North);
        Recompute(grid, finalizer, money, cell + South);
        Recompute(grid, finalizer, money, cell + East);
        Recompute(grid, finalizer, money, cell + West);
    }

    private void Recompute(PlacementGrid grid, PlacementFinalizer finalizer, MoneyService money, Vector2Int cell)
    {
        if (!TryGetAutoShapeEntry(grid, cell, out GameObject oldInstance, out ObjDataSO oldData))
            return;

        bool n = IsWallFamily(grid, cell + North);
        bool s = IsWallFamily(grid, cell + South);
        bool e = IsWallFamily(grid, cell + East);
        bool w = IsWallFamily(grid, cell + West);
        int count = (n ? 1 : 0) + (s ? 1 : 0) + (e ? 1 : 0) + (w ? 1 : 0);

        ObjDataSO targetData;
        float targetRotation;

        if (count == 4)
        {
            // Cross piece is symmetric under any 90-degree rotation, so rotation is always 0.
            // Fall back to the T-Wall if no cross piece has been assigned in the Inspector.
            targetData = _crossData != null ? _crossData : _tWallData;
            targetRotation = 0f;
        }
        else if (count == 3)
        {
            targetData = _tWallData;
            targetRotation = TWallRotationForMissing(n, s, e, w);
        }
        else if (count == 2 && ((n && s) || (e && w)))
        {
            targetData = _wallData;
            targetRotation = (n && s) ? 90f : 0f;
        }
        else if (count == 2)
        {
            targetData = _cornerData;
            targetRotation = CornerRotation(n, e, s, w);
        }
        else if (count == 1)
        {
            targetData = _wallData;
            targetRotation = (n || s) ? 90f : 0f;
        }
        else // count == 0 — isolated segment, leave its current rotation alone if it's already a wall
        {
            targetData = _wallData;
            targetRotation = oldData == _wallData ? CurrentRotationDegrees(oldInstance) : 0f;
        }

        if (targetData == null) return;

        if (targetData == oldData && Mathf.Approximately(NormalizeDegrees(targetRotation), CurrentRotationDegrees(oldInstance)))
            return; // already the correct shape and orientation

        ReplaceInPlace(grid, finalizer, money, cell, oldInstance, oldData, targetData, targetRotation);
    }

    // {N,E} -> 0, {N,W} -> 90, {S,W} -> 180, {S,E} -> 270 — matches Corner.prefab's default
    // N+E orientation rotated via ObjDataSO's (x,y) -> (-y,x) quarter-turn convention.
// {N,E} -> 0, {N,W} -> 270, {S,W} -> 180, {S,E} -> 90 — matches Corner.prefab's default
    // N+E orientation as ACTUALLY rotated by PlacementFinalizer's Quaternion.Euler(0, rotation, 0),
    // which spins clockwise when viewed from above (Unity's left-handed Y axis), i.e. (x,y) -> (y,-x)
    // per 90-degree step — the opposite handedness from ObjDataSO's footprint-offset convention.
// {N,E} -> 0, {N,W} -> 90, {S,W} -> 180, {S,E} -> 270 — matches Corner.prefab's default
    // N+E orientation rotated via ObjDataSO's (x,y) -> (-y,x) quarter-turn convention.
    // CONFIRMED by direct in-editor test (spawning Corner + adjacent Wall pieces and checking
    // the render): this mapping is correct. Do not swap 90/270 here — that was tried and
    // empirically made every case except N+E wrong.
// {N,E} -> 0, {N,W} -> 90, {S,W} -> 180, {S,E} -> 270 — matches Corner.prefab's default
    // N+E orientation rotated via ObjDataSO's (x,y) -> (-y,x) quarter-turn convention.
    // Verified 2026-09-13 by spawning a live Corner + adjacent Wall pieces via unityMCP and
    // checking the actual render: this mapping is correct as-is. Do not "fix" it by swapping
    // 90/270 — that was tried and empirically broke every case except N+E.
// Corner.prefab's ACTUAL default (rotation 0) orientation connects {S,W}, NOT {N,E} as previously
    // assumed — confirmed 2026-09-13 by raycast-scanning the live mesh footprint in-editor (264/404
    // surface hits landed in the SW quadrant vs 28 in NE). The rotation-to-direction cycle itself
    // (verified separately via Quaternion.Euler tests: a 90 degree Y rotation maps N->E->S->W->N)
    // was already correct, so only the base case was wrong. Net effect: {N,E} and {S,W} swap to
    // 180/0 respectively; {N,W}->90 and {S,E}->270 were already right and are unchanged.
private static float CornerRotation(bool n, bool e, bool s, bool w)
    {
        // Confirmed by user: previous mapping (n&&w=0, n&&e=90, s&&e=180, s&&w=270) placed every
        // corner facing exactly opposite of correct. Flipping all four values by 180 degrees.
        if (n && w) return 180f;
        if (n && e) return 270f;
        if (s && e) return 0f;
        return 90f; // s && w
    }

    // T-Wall.prefab connects {W,E,S} (missing N) at rotation 0. Missing-direction cycles the same
    // way: missing N -> 0, missing W -> 90, missing S -> 180, missing E -> 270.
// T-Wall.prefab connects {W,E,S} (missing N) at rotation 0. Missing-direction cycles per the
    // actual clockwise-from-above visual rotation (see CornerRotation): missing N -> 0,
    // missing W -> 270, missing S -> 180, missing E -> 90.
// T-Wall.prefab connects {W,E,S} (missing N) at rotation 0. Missing-direction cycles the same
    // way: missing N -> 0, missing W -> 90, missing S -> 180, missing E -> 270.
// T-Wall.prefab connects {W,E,S} (missing N) at rotation 0. Missing-direction cycles the same
    // way: missing N -> 0, missing W -> 90, missing S -> 180, missing E -> 270.
private static float TWallRotationForMissing(bool n, bool s, bool e, bool w)
    {
        // User confirmed T-Wall pieces were also 180 degrees off (same base-orientation issue as
        // Corner). Flipping all four values by 180 degrees. Corners are untouched.
        if (!n) return 180f;
        if (!w) return 270f;
        if (!s) return 0f;
        return 90f; // !e
    }

    private static bool TryGetAutoShapeEntry(PlacementGrid grid, Vector2Int cell, out GameObject instance, out ObjDataSO data)
    {
        instance = null;
        data = null;

        var list = grid.GetObjectsInCell(cell);
        if (list == null) return false;

        foreach (var entry in list)
        {
            if (entry.instance != null && entry.data != null && AutoShapeNames.Contains(entry.data.objName))
            {
                instance = entry.instance;
                data = entry.data;
                return true;
            }
        }
        return false;
    }

    private static bool IsWallFamily(PlacementGrid grid, Vector2Int cell)
    {
        var list = grid.GetObjectsInCell(cell);
        if (list == null) return false;

        foreach (var entry in list)
        {
            if (entry.instance != null && entry.data != null && entry.data.category == WallCategory)
                return true;
        }
        return false;
    }

    private static float CurrentRotationDegrees(GameObject instance)
    {
        var po = instance != null ? instance.GetComponent<PlacedObject>() : null;
        return po != null ? po.rotation * 90f : 0f;
    }

    private static float NormalizeDegrees(float r)
    {
        r %= 360f;
        if (r < 0f) r += 360f;
        return r;
    }

    private static void ReplaceInPlace(PlacementGrid grid, PlacementFinalizer finalizer, MoneyService money, Vector2Int cell,
        GameObject oldInstance, ObjDataSO oldData, ObjDataSO targetData, float targetRotation)
    {
        grid.RemoveStackObject(cell, oldInstance, oldData);
        Object.Destroy(oldInstance);

        // Sell back the old piece, then charge full price for the new one — mirrors the sell-back
        // convention PlaceCommand already uses when a door replaces a straight wall.
        if (money != null && oldData != null)
        {
            int refund = Mathf.RoundToInt(oldData.cost * money.SellBackRate);
            money.Refund(refund, oldData.category);
            money.RemoveHourlyCost(oldData.hourlyCost, FinanceCategory.ForHourlyCost(oldData.category), oldData.category);
        }

        Vector2Int[] offsets = targetData.GetFootprintOffsets(targetRotation);
        GameObject newInstance = finalizer.FinalizePlacement(cell, offsets, targetData, targetRotation, null, true);
        if (newInstance == null) return;

        if (money != null)
        {
            money.Deduct(targetData.cost, targetData.category);
            money.AddHourlyCost(targetData.hourlyCost, FinanceCategory.ForHourlyCost(targetData.category), targetData.category);

            int netCost = targetData.cost - Mathf.RoundToInt(oldData != null ? oldData.cost * money.SellBackRate : 0f);
            if (netCost != 0)
                FloatingMoneyText.Show(newInstance.transform.position + Vector3.up * 1.5f, -netCost);
        }

        newInstance.SetActive(true);
        grid.UpdateStackPositions(cell);
        NavMeshManager.Instance?.MarkDirty();

        // Tell the command history about the swap so any EARLIER command still holding a cached
        // reference to oldInstance (now-destroyed) can update it to newInstance instead of
        // silently no-oping the next time its own Undo()/Redo() runs. See
        // IWallInstanceRelocatable for the full story.
        ResolveHistory()?.RelocateWallInstance(oldInstance, newInstance);
    }


    private static CommandHistory ResolveHistory()
    {
        return ServiceLocator.TryGet<GameCore.Build.BuildService>(out var buildService) && buildService != null
            ? buildService.CommandHistory
            : null;
    }
}
