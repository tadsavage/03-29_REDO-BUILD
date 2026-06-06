using UnityEngine;
using UnityEngine.AI;

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
             "Tick multiple boxes to share a waypoint between types. " +
             "Rats roam freely and never use waypoints.")]
    public WaypointGroup allowedGroups = WaypointGroup.Worker;

    /// <summary>Returns true if the given group is allowed to use this waypoint.</summary>
    public bool AllowsGroup(WaypointGroup group) => (allowedGroups & group) != 0;

    // ── Surface snapping ──────────────────────────────────────────────────────

    private void Start()
    {
        // Snap to the walkable NavMesh surface so agents can always reach us.
        // Waypoints bypass the stacking system (ignorePlacementRules=true) and
        // land at raw grid height (Y=0). The floor tiles sit ~1.06m above that,
        // so without snapping agents cluster on top trying to reach underground points.
        if (NavMeshManager.IsReady)
            SnapToSurface();
        else
            NavMeshManager.OnNavMeshReady += OnNavMeshReady;
    }

    private void OnNavMeshReady()
    {
        NavMeshManager.OnNavMeshReady -= OnNavMeshReady;
        SnapToSurface();
    }

    private void OnDestroy()
    {
        NavMeshManager.OnNavMeshReady -= OnNavMeshReady;
    }

    private void SnapToSurface()
    {
        Vector3 pos = transform.position;

        // Search upward first (waypoint lands at y=0 from the grid; floor surface is ~1.06m up).
        // Radius 1f is tight enough to avoid snapping to a different foundation one tile over.
        // Dense offsets bridge the gap between y=0 and y=1.06 without missing the surface.
        float[] yOffsets = { 2f, 1.5f, 1.0f, 0.5f, 0f, -0.5f };
        foreach (float offset in yOffsets)
        {
            Vector3 sample = new Vector3(pos.x, pos.y + offset, pos.z);
            if (NavMesh.SamplePosition(sample, out NavMeshHit hit, 1f, NavMesh.AllAreas))
            {
                transform.position = new Vector3(pos.x, hit.position.y, pos.z);
                return;
            }
        }
        // No NavMesh found nearby — leave position unchanged
    }

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
