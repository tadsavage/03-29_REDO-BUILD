using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Marks a ShippingDoor as a dockable slot for trucks.
/// The old drive-straight-then-flip waypoint/maneuver system (ApproachPoint, PullPastPoint,
/// DockPosition, DockRotation, ApproachDepartPoint) has been stripped out — TruckController's
/// references to those members need a replacement before it will compile again. Only
/// flipYardSide survives from that system, kept for whatever draws the yard-side facing.
/// </summary>
public class DockSlot : MonoBehaviour
{
    public static readonly List<DockSlot> All = new();

    [Tooltip("Flip if waypoints appear on the wrong side of the door (inside building instead of yard).")]
    [SerializeField] private bool flipYardSide = false;

    public bool FlipYardSide => flipYardSide;

    public bool IsOccupied { get; private set; }

    // ── Cached child components ───────────────────────────────────────────────

    private DoorNumberDisplay      _numberDisplay;
    private DockLightController    _lightController;
    private PlacedObject           _placed;
    private RollupDoorController[] _rollupDoors;

    public DockLightController LightController => _lightController;

    /// <summary>Holds this door's rollup panel(s) open (or releases them) for the duration a truck is
    /// docked here — see RollupDoorController.SetForcedOpen for why this can't just be left to the
    /// trigger collider alone. ShippingDoor can carry more than one RollupDoorController.</summary>
    public void SetDoorForcedOpen(bool forced)
    {
        if (_rollupDoors == null) return;
        foreach (var door in _rollupDoors)
            door?.SetForcedOpen(forced);
    }

    private int _doorNumber;
    public int DoorNumber
    {
        get => _doorNumber;
        private set
        {
            _doorNumber = value;
            if (_numberDisplay != null) _numberDisplay.Number = value;
        }
    }

    // A door's number is persisted in its PlacedObject.customData (doors don't use customData for
    // anything else) so it survives save/load and never changes once assigned. 0 = none yet.
    private int PersistedNumber =>
        (_placed != null && int.TryParse(_placed.customData, out int n) && n > 0) ? n : 0;

    private void SetNumber(int n, bool persist)
    {
        DoorNumber = n;
        if (persist && _placed != null) _placed.customData = n.ToString();
    }

    private void Awake()
    {
        _numberDisplay   = GetComponentInChildren<DoorNumberDisplay>(true);
        _lightController = GetComponentInChildren<DockLightController>(true);
        _placed          = GetComponent<PlacedObject>();
        _rollupDoors     = GetComponentsInChildren<RollupDoorController>(true);
    }

    private void OnEnable()
    {
        All.Add(this);
        AssignDoorNumbers();
    }

    private void OnDisable()
    {
        All.Remove(this);
        AssignDoorNumbers();
    }

    /// <summary>
    /// Assigns door numbers WITHOUT ever renumbering existing doors. A door that already has a
    /// number — persisted in customData, whether restored from a save or assigned earlier this
    /// session — keeps it; only doors with no number yet get one, taking the LOWEST currently
    /// unused number. So adding a 5th door to 1-4 gives it "5", and deleting door 2 frees the
    /// number 2 for the next door placed to reuse (same recycling as aisle numbers). This is
    /// critical: trucks, shipping-lane names, and employee/inventory destinations all reference
    /// door numbers and must stay stable mid-game — we only ever add or remove, never rename (a
    /// manual rename UI can come later). Self-contained (no guard shack needed); also runs from
    /// DockNumberingService on placement/load so ghost-placed and save-restored doors (whose
    /// customData is only set after OnEnable) get numbered once everything is settled.
    /// </summary>
    public static void AssignDoorNumbers()
    {
        var used = new HashSet<int>();
        var unnumbered = new List<DockSlot>();
        foreach (var d in All)
        {
            if (d == null) continue;
            int p = d.PersistedNumber;
            // used.Contains(p) here means a SECOND door in `All` claims the same persisted number as
            // one already accepted this pass — normally impossible, but a reload can briefly leave a
            // stale (about-to-be-destroyed) DockSlot and its freshly-restored replacement both in `All`
            // at once with identical customData. Blindly keeping both would show two physical doors as
            // the same door number (and downstream, LaneNamingService would fold their lanes together
            // too). Treat the second claimant as unnumbered instead, so it falls through to the gap-fill
            // loop below and gets its own number rather than colliding.
            if (p > 0 && !used.Contains(p)) { d.SetNumber(p, false); used.Add(p); }
            else unnumbered.Add(d);
        }

        // Deterministic order for doors that don't have a number yet (fresh placements, plus the
        // one-time bootstrap of a save made before numbers were persisted): by position, X then Z —
        // matching the original numbering order so existing docks keep the numbers they had.
        unnumbered.Sort((a, b) =>
        {
            int cx = a.transform.position.x.CompareTo(b.transform.position.x);
            return cx != 0 ? cx : a.transform.position.z.CompareTo(b.transform.position.z);
        });

        // Each new door claims the lowest free number (gap-fill = reuse deleted doors' numbers).
        foreach (var d in unnumbered)
        {
            int n = 1;
            while (used.Contains(n)) n++;
            used.Add(n);
            d.SetNumber(n, true);
        }
    }

    public void Claim()   => IsOccupied = true;
    public void Release() => IsOccupied = false;

    // ── Scene gizmos ──────────────────────────────────────────────────────────

    private void OnDrawGizmos()
    {
        Vector3 pos = transform.position;

        // Door
        Gizmos.color = IsOccupied ? Color.red : Color.green;
        Gizmos.DrawWireCube(pos, Vector3.one * 0.4f);

        // Facing indicator, respecting flipYardSide.
        Vector3 facing = flipYardSide ? -transform.forward : transform.forward;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(pos, pos + facing * 2f);
    }
}
