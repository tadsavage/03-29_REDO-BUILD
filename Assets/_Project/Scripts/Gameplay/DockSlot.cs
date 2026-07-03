using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Marks a ShippingDoor as a dockable slot for trucks.
/// All three waypoints are computed at runtime from this object's transform + serialized
/// offsets — no scene references needed, survives save/load cycles cleanly.
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

    [Header("Truck maneuver points (4-step route)")]
    [Tooltip("Xform 2 (_drApproach-DepartPoint): straight-out distance from the door into the yard.")]
    [SerializeField] private float approachDepartDepth = 9.5f;
    [Tooltip("Xform 2 sideways offset from the door center along the wall (0 = centered on the door).")]
    [SerializeField] private float approachDepartSide = 0f;
    [Tooltip("Xform 3 (_drBackup): depth out from the door (same as approach by default).")]
    [SerializeField] private float backupDepth = 9.5f;
    [Tooltip("Xform 3 sideways offset from the door center along the wall (negative = left). Flip the sign if it lands on the wrong side.")]
    [SerializeField] private float backupSide = -9f;

    [Tooltip("beginBackupTurn (between Xform 3 and the dock): same spot as Xform 2 but pulled this many units toward the door. The reverse turns here, then backs straight into the dock.")]
    [SerializeField] private float beginBackupTurnZ = 3f;

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

    // 4-step route points (per-door, computed from the serialized offsets above)
    public Vector3 ApproachDepartPoint => transform.position
                                          + YardForward * approachDepartDepth
                                          + YardRight   * approachDepartSide;   // Xform 2
    public Vector3 BackupPoint         => transform.position
                                          + YardForward * backupDepth
                                          + YardRight   * backupSide;           // Xform 3
    public Vector3 BeginBackupTurnPoint => ApproachDepartPoint - YardForward * beginBackupTurnZ; // between 3 and dock

    public bool IsOccupied { get; private set; }

    // ── Cached child components ───────────────────────────────────────────────

    private DoorNumberDisplay   _numberDisplay;
    private DockLightController _lightController;

    public DockLightController LightController => _lightController;

    private int _doorNumber;
    public int DoorNumber
    {
        get => _doorNumber;
        set
        {
            _doorNumber = value;
            if (_numberDisplay != null) _numberDisplay.Number = value;
        }
    }

    private void Awake()
    {
        _numberDisplay   = GetComponentInChildren<DoorNumberDisplay>(true);
        _lightController = GetComponentInChildren<DockLightController>(true);
    }

    private void OnEnable()
    {
        All.Add(this);
        RenumberAll();
    }

    private void OnDisable()
    {
        All.Remove(this);
        RenumberAll();
    }

    /// <summary>
    /// Assigns sequential door numbers (1..N) to every registered dock, ordered by yard
    /// position (X, then Z). Self-contained: does NOT require a TruckYardManager / guard
    /// shack in the scene, so doors number themselves as soon as they're placed or loaded.
    /// Called on every dock register/unregister and by DockNumberingService on placement
    /// events (the latter catches ghost-placed doors whose final position is only settled
    /// after OnEnable has already run).
    /// </summary>
    public static void RenumberAll()
    {
        int n = 1;
        foreach (var dock in All.OrderBy(d => d.transform.position.x)
                                .ThenBy(d => d.transform.position.z))
        {
            dock.DoorNumber = n++;
        }
    }

    public void Claim()   => IsOccupied = true;
    public void Release() => IsOccupied = false;

    // ── Scene gizmos ──────────────────────────────────────────────────────────

    private static Vector3 EvalBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float u = 1f - t;
        return u*u*u*p0 + 3f*u*u*t*p1 + 3f*u*t*t*p2 + t*t*t*p3;
    }

    private void OnDrawGizmos()
    {
        Vector3 pos = transform.position;
        Vector3 fwd = flipYardSide ? -transform.forward : transform.forward;

        Vector3 x2  = ApproachDepartPoint;   // Xform 2
        Vector3 x3  = BackupPoint;           // Xform 3
        Vector3 bbt = BeginBackupTurnPoint;  // between 3 and dock
        Vector3 dt  = DockPosition;          // door (Xform 4)

        // Door
        Gizmos.color = IsOccupied ? Color.red : Color.green;
        Gizmos.DrawWireCube(pos, Vector3.one * 0.4f);

        // Waypoint markers: Xform 2 (cyan), Xform 3 (orange), beginBackupTurn (yellow), dock (magenta)
        Gizmos.color = Color.cyan;              Gizmos.DrawWireSphere(x2, 0.4f);
        Gizmos.color = new Color(1f, 0.5f, 0f); Gizmos.DrawWireSphere(x3, 0.4f);
        Gizmos.color = Color.yellow;            Gizmos.DrawWireSphere(bbt, 0.35f);
        Gizmos.color = Color.magenta;           Gizmos.DrawWireSphere(dt, 0.35f);

        // Xform 2 → Xform 3: rounded corner (RED), control at (Xform2.x, Xform3.z)
        Gizmos.color = Color.red;
        Vector3 corner = new Vector3(x2.x, x2.y, x3.z);
        Vector3 prev = x2;
        for (int i = 1; i <= 14; i++)
        {
            float t = i / 14.0f;
            Vector3 point = EvalBezier(x2, corner, corner, x3, t);
            Gizmos.DrawLine(prev, point);
            prev = point;
        }

        // Xform 3 → beginBackupTurn: straight reverse (gray)
        Gizmos.color = Color.gray;
        Gizmos.DrawLine(x3, bbt);

        // beginBackupTurn → dock: final reverse Bézier (blue, approximate)
        Gizmos.color = Color.blue;
        float tension = Vector3.Distance(bbt, dt) * 0.45f;
        Vector3 q1 = bbt + (dt - bbt).normalized * tension;
        Vector3 q2 = dt + fwd * tension;
        Vector3 prevQ = bbt;
        for (int i = 1; i <= 12; i++)
        {
            float t = i / 12.0f;
            Vector3 point = EvalBezier(bbt, q1, q2, dt, t);
            Gizmos.DrawLine(prevQ, point);
            prevQ = point;
        }
    }
}
