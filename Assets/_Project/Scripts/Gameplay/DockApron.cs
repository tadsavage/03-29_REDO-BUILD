using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// DOCK APRON (Tad, 2026-11-15): "the 6 tiles across the front of the shipping door and 5 tiles out make it a no fly zone for pallet
/// jacks and reach trucks. Workers can cross here though - specifically receivers." Only dock stockers drive here.
///
/// Lives on the ShippingDoor prefab. The zone is the grid cells in front of the door (inside the building, the way the door faces):
/// <see cref="widthCells"/> across, centred on the door (an even width takes the extra cell on the door's left), <see cref="depthCells"/>
/// out from the wall. Staging-lane tiles are never part of it, so jacks still stage into lanes that start inside it. Doors on the same
/// wall have the gaps between their aprons filled (up to <see cref="MaxJoinGapCells"/>), so the whole dock is one apron.
/// Two ways it is kept: the NavMesh marks these cells as area <see cref="NavArea"/> ("Dock Apron", a modifier box per cell added by
/// NavMeshManager.AddDockApronSources), which the pallet jack and reach
/// truck agents cannot walk (dock stockers and people can), and MachineClearance refuses any scripted jack / reach-truck step into it.
/// </summary>
[DisallowMultipleComponent]
public class DockApron : MonoBehaviour
{
    public const int NavArea = 7;   // ProjectSettings/NavMeshAreas: "Dock Apron"

    [Tooltip("Tiles across the front of the door.")]
    public int widthCells = 6;
    [Tooltip("Tiles out from the door into the building.")]
    public int depthCells = 5;

    private static readonly List<DockApron> s_all = new List<DockApron>();
    private static readonly HashSet<Vector2Int> s_cells = new HashSet<Vector2Int>();
    private static float s_builtAt = -10f;
    private static bool s_dirty = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { s_all.Clear(); s_cells.Clear(); s_builtAt = -10f; s_dirty = true; s_grid = null; }

    private void OnEnable() { if (!s_all.Contains(this)) s_all.Add(this); s_dirty = true; }
    private void OnDisable() { s_all.Remove(this); s_dirty = true; }

    /// <summary>Any apron in the scene (cheap early-out for the per-step guard).</summary>
    public static bool Any => s_all.Count > 0;

    /// <summary>True when the grid cell is part of a dock apron.</summary>
    public static bool ContainsCell(Vector2Int cell)
    {
        Ensure();
        return s_cells.Contains(cell);
    }

    /// <summary>All apron cells (rebuilt at most once a second: doors move, lane tiles come and go).</summary>
    public static IReadOnlyCollection<Vector2Int> Cells { get { Ensure(); return s_cells; } }

    public static void MarkDirty() => s_dirty = true;

    private static void Ensure()
    {
        if (!s_dirty && Time.realtimeSinceStartup - s_builtAt < 1f) return;
        s_dirty = false;
        s_builtAt = Time.realtimeSinceStartup;
        s_cells.Clear();
        foreach (var a in s_all)
            if (a != null && a.isActiveAndEnabled) a.AddCells(s_cells);
        JoinAlongWalls(s_cells);
        TrimNearRacks(s_cells);
    }

    /// <summary>RACK TURNING STRIP (Tad, 2026-11-15, "trim apron near racks"): apron cells within this many tiles of a rack cell are
    /// left out, so a pallet jack (forks trailing 2.8 m) can turn into an aisle mouth that faces the dock. With the racks at cell
    /// x 35 and door 1's apron starting at cell 38, the old apron left only 2 free tiles (~3.3 m) in front of aisle 01.</summary>
    public const int RackClearanceCells = 5;   // 4 still let a jack's forks swing onto the apron corner turning into aisle 01

    private static readonly List<Vector2Int> s_trim = new List<Vector2Int>();

    private static void TrimNearRacks(HashSet<Vector2Int> cells)
    {
        var grid = Grid();
        if (grid == null || cells.Count == 0) return;
        s_trim.Clear();
        foreach (var c in cells)
            if (RackWithin(grid, c, RackClearanceCells)) s_trim.Add(c);
        foreach (var c in s_trim) cells.Remove(c);
    }

    private static bool RackWithin(PlacementGrid grid, Vector2Int c, int r)
    {
        for (int dx = -r; dx <= r; dx++)
            for (int dy = -r; dy <= r; dy++)
            {
                var list = grid.GetObjectsInCell(new Vector2Int(c.x + dx, c.y + dy));
                if (list == null) continue;
                foreach (var e in list)
                    if (e.instance != null && e.data != null && e.data.category == "Racking") return true;
            }
        return false;
    }

    /// <summary>Doors further apart than this many free tiles along a wall are separate docks: their aprons are not joined.</summary>
    public const int MaxJoinGapCells = 12;

    /// <summary>ONE APRON ACROSS THE WHOLE DOCK (Tad, 2026-11-15): doors on the same wall facing the same way get the gaps between
    /// their aprons filled too (jacks were cutting through the 1-tile gap between door 1's and door 2's aprons).</summary>
    private static void JoinAlongWalls(HashSet<Vector2Int> cells)
    {
        var walls = new Dictionary<(Vector2Int fwd, int wall), List<(int latLo, int latHi, int depth, Vector2Int door)>>();
        foreach (var a in s_all)
        {
            if (a == null || !a.isActiveAndEnabled || !a.TryFrame(out var door, out var fwd, out var left, out int lo, out int hi)) continue;
            int wall = fwd.x != 0 ? door.x : door.y;
            int lat = Vector2IntDot(door, left);
            var key = (fwd, wall);
            if (!walls.TryGetValue(key, out var list)) walls[key] = list = new List<(int, int, int, Vector2Int)>();
            list.Add((lat - hi, lat - lo, Mathf.Max(1, a.depthCells), door));
        }
        var grid = Grid();
        foreach (var kv in walls)
        {
            var list = kv.Value;
            if (list.Count < 2) continue;
            list.Sort((x, y) => x.latLo.CompareTo(y.latLo));
            Vector2Int fwd = kv.Key.fwd, left = new Vector2Int(-fwd.y, fwd.x);
            for (int k = 1; k < list.Count; k++)
            {
                var prev = list[k - 1]; var next = list[k];
                int gapFrom = prev.latHi + 1, gapTo = next.latLo - 1;
                if (gapTo < gapFrom || gapTo - gapFrom + 1 > MaxJoinGapCells) continue;   // touching / overlapping, or a separate dock
                int depth = Mathf.Min(prev.depth, next.depth);
                Vector2Int door = prev.door;
                int lat0 = Vector2IntDot(door, left);
                for (int lat = gapFrom; lat <= gapTo; lat++)
                    for (int i = 1; i <= depth; i++)
                    {
                        Vector2Int c = door + fwd * i + left * (lat - lat0);
                        if (IsSpecialFloor(grid, c)) continue;
                        cells.Add(c);
                    }
            }
        }
    }

    private static int Vector2IntDot(Vector2Int a, Vector2Int b) => a.x * b.x + a.y * b.y;

    /// <summary>The door's grid cell, facing (into the building), its left, and the apron's lateral offsets (j) from the door.</summary>
    private bool TryFrame(out Vector2Int door, out Vector2Int fwd, out Vector2Int left, out int lo, out int hi)
    {
        door = fwd = left = default; lo = hi = 0;
        var po = GetComponent<PlacedObject>();
        if (po == null) return false;
        Vector3 f = transform.forward; f.y = 0f;
        if (f.sqrMagnitude < 0.01f) return false;
        // Grid directions: rotations are multiples of 90 degrees.
        fwd = Mathf.Abs(f.x) >= Mathf.Abs(f.z) ? new Vector2Int(f.x > 0f ? 1 : -1, 0) : new Vector2Int(0, f.z > 0f ? 1 : -1);
        left = new Vector2Int(-fwd.y, fwd.x);
        door = new Vector2Int(po.gridX, po.gridY);
        int w = Mathf.Max(1, widthCells);
        lo = -(w / 2); hi = lo + w - 1;   // 6 -> -3..2
        return true;
    }

    /// <summary>This door's apron cells.</summary>
    public void AddCells(ICollection<Vector2Int> into)
    {
        // The extra cell of an even width goes to the side "left" of the door's facing (6 -> j = -3..2).
        if (!TryFrame(out var door, out var fwd, out var left, out int lo, out int hi)) return;
        var grid = Grid();
        for (int i = 1; i <= Mathf.Max(1, depthCells); i++)
            for (int j = lo; j <= hi; j++)
            {
                Vector2Int c = door + fwd * i + left * (-j);
                if (IsSpecialFloor(grid, c)) continue;   // staging-lane tiles (and other special floors) stay as they are
                into.Add(c);
            }
    }

    private static PlacementGrid s_grid;
    private static PlacementGrid Grid()
    {
        if (s_grid == null) s_grid = FindAnyObjectByType<PlacementGrid>();
        return s_grid;
    }

    /// <summary>A lane tile (or any floor with its own NavMesh area) is in this cell. Read from the grid, not LaneNamingService: the
    /// NavMesh is baked on load before the lanes are named.</summary>
    private static bool IsSpecialFloor(PlacementGrid grid, Vector2Int c)
    {
        if (LaneNamingService.TryGetSlot(c, out _)) return true;
        if (grid == null) return false;
        var list = grid.GetObjectsInCell(c);
        if (list == null) return false;
        foreach (var e in list)
        {
            if (e.instance == null || e.data == null || !e.data.isFloor) continue;
            if (e.data.navArea != 0 || e.instance.transform.Find("LaneNo") != null) return true;
        }
        return false;
    }

    private void OnDrawGizmosSelected()
    {
        var grid = Grid();
        if (grid == null) return;
        var cells = new List<Vector2Int>();
        AddCells(cells);
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.35f);
        foreach (var c in cells)
            Gizmos.DrawCube(grid.GetCellCenter(c) + Vector3.up * 0.05f, new Vector3(grid.CellSize * 0.95f, 0.05f, grid.CellSize * 0.95f));
    }
}
