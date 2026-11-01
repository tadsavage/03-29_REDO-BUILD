using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Keeps material-handling equipment (reach truck, dock stocker, pallet jack) INSIDE the building.
///
/// Tad's rule: forklifts, dock stockers and pallet jacks never drive out of the building. The NavMesh covers the
/// yard too, so nothing stopped a reach truck from routing out through a wall, around the outside and back in
/// through the foundation (2026-10-31). This is a boundary, not a wall: every few tenths of a second it checks the
/// grid cell the vehicle is standing on. A cell is "inside" only if a Foundation or Grounds piece sits under it.
/// If the vehicle is outside for two checks in a row, it logs an error and is put back on the last inside spot it
/// was seen at, so a missing wall location never matters.
///
/// Added by MHEOperatorSlot.Start, next to VehicleGrounding and EquipmentBattery.
/// </summary>
[DisallowMultipleComponent]
public class MHEBoundary : MonoBehaviour
{
    // Tuning -----------------------------------------------------------------------------------------------------
    private const float CheckInterval = 0.2f;   // seconds between checks
    private const int   OutsideChecksBeforeReturn = 2; // two in a row, so one bad frame never teleports anything
    // The dock apron: trailers park in the yard right outside each door, and a dock stocker has to drive out to them to
    // unload. Anything within this many metres of a dock door counts as inside. Further out is still outside.
    private const float DockApronMeters = 9f;
    // The building floor sits at y ~1.1. A machine measured lower than this has fallen through to the yard-level mesh beneath it.
    private const float MinFloorY = 0.6f;

    // A dock stocker under a dock routine (offload / load / dig-out) drives scripted legs to a trailer that can sit well past the
    // 9 m apron; the routine and this boundary fought, bouncing the machine back every second (2026-10-31).
    private const float DockWorkApronMeters = 20f;
    private const int   MaxBouncesBeforeGivingUp = 5;
    private const float BounceWindowSeconds = 10f;

    private MHEOperatorSlot _slot;
    private int _bounces;
    private float _bounceWindowStart;
    private bool _gaveUp;

    private PlacementGrid _grid;
    private Vector3 _lastInside;
    private bool _haveLastInside;
    private int _outsideStreak;
    private float _nextCheck;

    /// <summary>Adds the boundary to an operated vehicle once. Safe to call twice.</summary>
    public static void Attach(GameObject go)
    {
        if (go == null || go.GetComponent<MHEBoundary>() != null) return;
        go.AddComponent<MHEBoundary>();
    }

    private void Start()
    {
        _grid = FindAnyObjectByType<PlacementGrid>();
        _lastInside = transform.position;
        _haveLastInside = IsInside(transform.position) && transform.position.y >= MinFloorY;
    }

    private void Update()
    {
        if (_grid == null) { _grid = FindAnyObjectByType<PlacementGrid>(); return; }
        if (Time.time < _nextCheck) return;
        _nextCheck = Time.time + CheckInterval;

        Vector3 pos = transform.position;
        if (_slot == null) _slot = GetComponent<MHEOperatorSlot>();
        bool dockWork = GameCore.Labor.DockEquipmentCommandeerRegistry.IsCommandeered(_slot);
        bool sunk = pos.y < MinFloorY && _haveLastInside;
        if (!sunk && (IsInside(pos) || (dockWork && NearDoor(pos, DockWorkApronMeters))))
        {
            if (pos.y >= MinFloorY) { _lastInside = pos; _haveLastInside = true; }
            _outsideStreak = 0;
            _gaveUp = false;
            return;
        }

        _outsideStreak++;
        if (_outsideStreak < OutsideChecksBeforeReturn) return;
        _outsideStreak = 0;

        // Bouncing again and again means a scripted driver keeps heading outside: stop fighting it (one error, then leave it be).
        if (_gaveUp) return;
        if (Time.time - _bounceWindowStart > BounceWindowSeconds) { _bounceWindowStart = Time.time; _bounces = 0; }
        if (++_bounces > MaxBouncesBeforeGivingUp)
        {
            _gaveUp = true;
            Debug.LogError($"[MHEBoundary] '{name}' keeps driving out of the building ({_bounces} times in {BounceWindowSeconds:0}s, at {pos}). Not putting it back any more; something is steering it outside.");
            return;
        }

        Debug.LogError($"[MHEBoundary] '{name}' {(sunk ? "fell below the floor" : "drove OUT of the building")} at {pos} (cell {_grid.WorldToCell(pos)}). " +
                       $"Putting it back on its last inside spot {_lastInside}. Forklifts, dock stockers and pallet jacks must stay inside.");
        ReturnToLastInside();
    }

    private static bool NearDoor(Vector3 worldPos, float meters)
    {
        foreach (var door in DockSlot.All)
        {
            if (door == null) continue;
            Vector3 d = door.transform.position - worldPos;
            d.y = 0f;
            if (d.sqrMagnitude <= meters * meters) return true;
        }
        return false;
    }

    private static PlacementGrid s_grid;

    private static PlacementGrid Grid()
    {
        if (s_grid == null) s_grid = FindAnyObjectByType<PlacementGrid>();
        return s_grid;
    }

    private static bool CellIsBuilding(PlacementGrid grid, Vector3 worldPos)
    {
        Vector2Int cell = grid.WorldToCell(worldPos);
        if (!grid.IsInsideGrid(cell)) return false;
        var entries = grid.GetObjectsInCell(cell);
        if (entries == null) return false;
        foreach (var e in entries)
        {
            var cat = e.data != null ? e.data.category : null;
            if (cat == "Foundation" || cat == "Grounds") return true;
        }
        return false;
    }

    /// <summary>True for a vehicle this boundary restricts. AiNavigation's walk-out uses it so an escape never leads outside.</summary>
    public static bool Restricts(GameObject go) => go != null && go.GetComponent<MHEBoundary>() != null;

    /// <summary>True when the position is on the building floor (a Foundation or Grounds piece under it) or on the dock apron
    /// in front of a door. Callable from anywhere: the same test the vehicles use.</summary>
    public static bool IsInside(Vector3 worldPos)
    {
        var grid = Grid();
        if (grid == null) return true; // no grid yet: never block a vehicle on a missing reference
        if (CellIsBuilding(grid, worldPos)) return true;
        // A vehicle is ~2 m long: with its centre on the very edge of the last foundation tile it is still on the building.
        // Check a point a little way out in each direction before calling it outside.
        const float EdgeTolerance = 0.9f;
        if (CellIsBuilding(grid, worldPos + new Vector3(EdgeTolerance, 0f, 0f)) ||
            CellIsBuilding(grid, worldPos - new Vector3(EdgeTolerance, 0f, 0f)) ||
            CellIsBuilding(grid, worldPos + new Vector3(0f, 0f, EdgeTolerance)) ||
            CellIsBuilding(grid, worldPos - new Vector3(0f, 0f, EdgeTolerance))) return true;

        // Not on the building floor, but it may be on the dock apron in front of a door.
        foreach (var door in DockSlot.All)
        {
            if (door == null) continue;
            Vector3 d = door.transform.position - worldPos;
            d.y = 0f;
            if (d.sqrMagnitude <= DockApronMeters * DockApronMeters) return true;
        }
        return false;
    }

    private void ReturnToLastInside()
    {
        if (!_haveLastInside) return;
        Vector3 back = new Vector3(_lastInside.x, _lastInside.y, _lastInside.z);

        var agent = GetComponent<NavMeshAgent>();
        if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.Warp(back);
        else
            transform.position = back;
    }
}
