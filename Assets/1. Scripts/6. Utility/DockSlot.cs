using UnityEngine;
using System.Collections.Generic;

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

    private void OnEnable()  => All.Add(this);
    private void OnDisable() => All.Remove(this);

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
        Vector3 rgt = flipYardSide ? -transform.right   : transform.right;

        Vector3 ap = pos + fwd * laneDepth;
        Vector3 pp = pos + fwd * laneDepth + rgt * (pullPastOffset * pullPastSide);
        Vector3 dt = pos + fwd * dockOffset;

        // Turn start point (1m past PullPastPoint)
        Vector3 tsp = pp + rgt * (1.0f * pullPastSide);

        // Align point (4m further than ApproachPoint)
        Vector3 ap_align = ap + fwd * 4.0f;

        // Departure pull-out point (15m out from DockPosition)
        Vector3 pop = dt + fwd * 15.0f;

        // Draw door location
        Gizmos.color = IsOccupied ? Color.red : Color.green;
        Gizmos.DrawWireCube(pos, Vector3.one * 0.4f);

        // Draw PullPastPoint (Yellow)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(pp, 0.35f);

        // Draw TurnStartPoint (Orange)
        Gizmos.color = new Color(1.0f, 0.5f, 0.0f);
        Gizmos.DrawWireSphere(tsp, 0.35f);
        Gizmos.DrawLine(pp, tsp);

        // Draw AlignPoint (Cyan)
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(ap_align, 0.35f);

        // Draw smooth 90-degree turn arc from tsp to ap_align (Blue)
        Gizmos.color = Color.blue;
        Vector3 p0 = tsp;
        Vector3 p1 = tsp + rgt * (pullPastOffset * 0.45f * pullPastSide);
        Vector3 p2 = ap_align + fwd * (pullPastOffset * 0.45f);
        Vector3 p3 = ap_align;
        Vector3 prevPoint = p0;
        for (int i = 1; i <= 10; i++)
        {
            float t = i / 10.0f;
            Vector3 point = EvalBezier(p0, p1, p2, p3, t);
            Gizmos.DrawLine(prevPoint, point);
            prevPoint = point;
        }

        // Draw DockPosition (Magenta)
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(dt, 0.35f);
        
        // Draw straight backing line from ap_align to dt (Magenta)
        Gizmos.DrawLine(ap_align, dt);

        // Draw straight pull-out line from dt to pop (Light Green)
        Gizmos.color = new Color(0.8f, 1.0f, 0.8f);
        Gizmos.DrawWireSphere(pop, 0.35f);
        Gizmos.DrawLine(dt, pop);
    }
}
