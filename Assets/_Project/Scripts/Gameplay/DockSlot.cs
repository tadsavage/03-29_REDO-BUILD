using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Marks a ShippingDoor as a dockable slot for trucks.
/// Waypoints are computed at runtime from this object's transform + serialized offsets —
/// no scene references needed, survives save/load cycles cleanly.
/// </summary>
public class DockSlot : MonoBehaviour
{
    public static readonly List<DockSlot> All = new();

    [Header("Lane offsets")]
    [Tooltip("How far into the yard the traffic lane sits (approach + pull-past depth).")]
    [SerializeField] private float laneDepth = 8f;

    [Tooltip("How far past the door along the wall the truck pulls (≈ truck length + clearance).")]
    [SerializeField] private float pullPastOffset = 6f;

    [Tooltip("+1 = pull past to the right (default),  -1 = pull past to the left.")]
    [SerializeField] private float pullPastSide = 1f;

    public float PullPastSide => pullPastSide;

    [Tooltip("How far out from the wall the truck center sits when fully docked.")]
    [SerializeField] private float dockOffset = 3.5f;

    [Tooltip("Flip if waypoints appear on the wrong side of the door (inside building instead of yard).")]
    [SerializeField] private bool flipYardSide = false;

    [Header("Truck maneuver points (drive-straight-then-flip route)")]
    [Tooltip("Xform 2 (_drApproach-DepartPoint): straight-out distance from the door into the yard.")]
    [SerializeField] private float approachDepartDepth = 9.5f;
    [Tooltip("Xform 2 sideways offset from the door center along the wall (0 = centered on the door).")]
    [SerializeField] private float approachDepartSide = 0f;

    // ── Computed waypoints ────────────────────────────────────────────────────

    private Vector3 YardForward => flipYardSide ? -transform.forward : transform.forward;
    private Vector3 YardRight   => flipYardSide ? -transform.right   : transform.right;

    public Vector3    ApproachPoint => transform.position + YardForward * laneDepth;
    public Vector3    PullPastPoint => transform.position
                                       + YardForward * laneDepth
                                       + YardRight   * (pullPastOffset * pullPastSide);
    public Vector3    DockPosition  => transform.position + YardForward * dockOffset;
    public Quaternion DockRotation  => flipYardSide
                                       ? transform.rotation * Quaternion.Euler(0, 180, 0)
                                       : transform.rotation;

    // Route points (per-door, computed from the serialized offsets above)
    public Vector3 ApproachDepartPoint => transform.position
                                          + YardForward * approachDepartDepth
                                          + YardRight   * approachDepartSide;   // Xform 2

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

        Vector3 x2 = ApproachDepartPoint;   // Xform 2
        Vector3 dt = DockPosition;          // door (Xform 4)

        // Door
        Gizmos.color = IsOccupied ? Color.red : Color.green;
        Gizmos.DrawWireCube(pos, Vector3.one * 0.4f);

        // Waypoint markers: Xform 2 (cyan), dock (magenta)
        Gizmos.color = Color.cyan;    Gizmos.DrawWireSphere(x2, 0.4f);
        Gizmos.color = Color.magenta; Gizmos.DrawWireSphere(dt, 0.35f);

        // Xform 2 → dock: straight drive-in, then an in-place flip to face away (no curve to draw).
        Gizmos.color = Color.red;
        Gizmos.DrawLine(x2, dt);
    }
}
