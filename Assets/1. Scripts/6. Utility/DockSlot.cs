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

    private void OnDrawGizmos()
    {
        Vector3 pos = transform.position;
        Vector3 fwd = flipYardSide ? -transform.forward : transform.forward;
        Vector3 rgt = flipYardSide ? -transform.right   : transform.right;

        Vector3 ap = pos + fwd * laneDepth;
        Vector3 pp = pos + fwd * laneDepth + rgt * (pullPastOffset * pullPastSide);
        Vector3 dt = pos + fwd * dockOffset;

        Gizmos.color = IsOccupied ? Color.red : Color.green;
        Gizmos.DrawWireCube(pos, Vector3.one * 0.4f);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(ap, 0.35f);
        Gizmos.DrawLine(pos, ap);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(pp, 0.35f);
        Gizmos.DrawLine(ap, pp);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(dt, 0.35f);
        Gizmos.DrawRay(dt, fwd * 1.5f);
        Gizmos.DrawLine(pp, dt);
    }
}
