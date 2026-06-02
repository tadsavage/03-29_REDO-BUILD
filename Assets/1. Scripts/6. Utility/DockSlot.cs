using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Marks a ShippingDoor as a dockable slot for trucks.
/// Place this on (or next to) the door object and assign the three child transforms
/// in the Inspector. Gizmos show the full path in the Scene View.
/// </summary>
public class DockSlot : MonoBehaviour
{
    public static readonly List<DockSlot> All = new();

    [Header("Waypoints — set these up once per door")]
    [Tooltip("Yard nav point the truck drives to first (before the pull-past).")]
    [SerializeField] private Transform approachWaypoint;

    [Tooltip("Point in the traffic lane, one truck-length PAST the door. Truck drives here, then reverses.")]
    [SerializeField] private Transform pullPastPoint;

    [Tooltip("Final truck pose when docked. Position = truck center, Forward = facing toward yard (away from building).")]
    [SerializeField] private Transform dockTarget;

    public Transform ApproachWaypoint => approachWaypoint;
    public Transform PullPastPoint    => pullPastPoint;
    public Transform DockTarget       => dockTarget;

    public bool IsOccupied { get; private set; }

    private void OnEnable()  => All.Add(this);
    private void OnDisable() => All.Remove(this);

    public void Claim()   => IsOccupied = true;
    public void Release() => IsOccupied = false;

    // ── Scene gizmos ──────────────────────────────────────────────────────────
    private void OnDrawGizmos()
    {
        Gizmos.color = IsOccupied ? Color.red : Color.green;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.4f);

        if (approachWaypoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(approachWaypoint.position, 0.35f);
            Gizmos.DrawLine(transform.position, approachWaypoint.position);
        }

        if (pullPastPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(pullPastPoint.position, 0.35f);
            if (approachWaypoint != null)
                Gizmos.DrawLine(approachWaypoint.position, pullPastPoint.position);
        }

        if (dockTarget != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(dockTarget.position, 0.35f);
            // Draw an arrow showing the truck's final facing direction
            Gizmos.DrawRay(dockTarget.position, dockTarget.forward * 1.5f);
            if (pullPastPoint != null)
                Gizmos.DrawLine(pullPastPoint.position, dockTarget.position);
        }
    }
}
