using UnityEngine;

/// <summary>
/// Marks a navigation destination. Each waypoint has a bitmask of which agent
/// groups are allowed to use it — tick as many as you like in the Inspector.
///
/// Rats ignore this filter entirely (they roam everywhere).
/// Trucks use dedicated Truck-only waypoints that will drive the dock-backing system.
/// </summary>
public class Waypoint : MonoBehaviour
{
    [System.Flags]
    public enum WaypointGroup
    {
        Worker       = 1 << 0,   // warehouse floor staff (indoor + grounds, not truck yard)
        Boss         = 1 << 1,   // goes everywhere
        Security     = 1 << 2,   // patrol routes (indoor + outdoor perimeter)
        MHE          = 1 << 3,   // forklifts / reach trucks (indoor + dock area)
        Truck        = 1 << 4,   // delivery trucks (yard only)
        Exterminator = 1 << 5,   // primarily external / perimeter
        // Add new groups here as needed — existing waypoints are unaffected
    }

    [Tooltip("Which agent groups may navigate to this waypoint. " +
             "Tick multiple for shared waypoints. Rats roam freely without waypoints.")]
    public WaypointGroup allowedGroups = WaypointGroup.Worker | WaypointGroup.Boss;

    /// <summary>Returns true if the given group is allowed to use this waypoint.</summary>
    public bool AllowsGroup(WaypointGroup group) => (allowedGroups & group) != 0;

    // ── Editor visualisation ──────────────────────────────────────────────────
    private void OnDrawGizmos()
    {
        Gizmos.color = GizmoColor();
        Gizmos.DrawWireSphere(transform.position, 0.25f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 0.5f);
    }

    private Color GizmoColor()
    {
        // If multiple groups, white. Otherwise colour-coded by group.
        int bits = (int)allowedGroups;
        if (bits == 0) return Color.grey;
        // more than one bit set → shared waypoint
        if ((bits & (bits - 1)) != 0) return Color.white;

        if ((allowedGroups & WaypointGroup.Worker)       != 0) return Color.cyan;
        if ((allowedGroups & WaypointGroup.Boss)         != 0) return Color.yellow;
        if ((allowedGroups & WaypointGroup.Security)     != 0) return new Color(1f, 0.4f, 0f);   // orange
        if ((allowedGroups & WaypointGroup.MHE)          != 0) return Color.green;
        if ((allowedGroups & WaypointGroup.Truck)        != 0) return Color.red;
        if ((allowedGroups & WaypointGroup.Exterminator) != 0) return new Color(0.6f, 0f, 0.8f); // purple
        return Color.grey;
    }
}
