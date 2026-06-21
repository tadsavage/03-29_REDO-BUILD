using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using UnityEditor;
using System.Text;

/// <summary>
/// Run via Tools → Diagnose Dock Links (works in Play Mode).
/// Authoritatively reports, for every NavMeshLink in the scene:
///   • whether each endpoint sits on the baked NavMesh
///   • whether a NavMesh path actually crosses the link (ground ↔ dock)
/// and for every worker waypoint whether an agent can path to it.
///
/// This bypasses the live agent loop so the results are ground truth.
/// </summary>
public static class DockLinkDiagnostics
{
    [MenuItem("Tools/Diagnose Dock Links")]
    public static void Run()
    {
        var sb = new StringBuilder();
        sb.AppendLine("===== DOCK LINK DIAGNOSTICS =====");

        // ── Agent type id for Human ──────────────────────────────────────────
        int humanType = 0;
        for (int i = 0; i < NavMesh.GetSettingsCount(); i++)
        {
            var s = NavMesh.GetSettingsByIndex(i);
            if (NavMesh.GetSettingsNameFromID(s.agentTypeID) == "Human") { humanType = s.agentTypeID; break; }
        }
        sb.AppendLine($"Human agentTypeID = {humanType}");

        var filter = new NavMeshQueryFilter { agentTypeID = humanType, areaMask = NavMesh.AllAreas };

        // ── NavMeshLinks ─────────────────────────────────────────────────────
        var links = Object.FindObjectsByType<NavMeshLink>(FindObjectsSortMode.None);
        sb.AppendLine($"\nNavMeshLink count = {links.Length}");

        int connected = 0, startOff = 0, endOff = 0, noPath = 0;
        int sampled = 0;
        foreach (var link in links)
        {
            if (link == null) continue;
            // Skip non-ledge links (e.g. stair links) — only report ledge links here
            bool isLedge = link.GetComponent<LedgeLinkMarker>() != null;

            Vector3 wStart = link.transform.TransformPoint(link.startPoint);
            Vector3 wEnd   = link.transform.TransformPoint(link.endPoint);

            bool sOn = NavMesh.SamplePosition(wStart, out var sHit, 0.4f, filter);
            bool eOn = NavMesh.SamplePosition(wEnd,   out var eHit, 0.4f, filter);

            if (!sOn) startOff++;
            if (!eOn) endOff++;

            string pathInfo = "n/a";
            if (sOn && eOn)
            {
                var path = new NavMeshPath();
                bool ok = NavMesh.CalculatePath(eHit.position, sHit.position, filter, path);
                pathInfo = $"{path.status} corners={path.corners.Length}";
                if (ok && path.status == NavMeshPathStatus.PathComplete) connected++;
                else noPath++;
            }
            sampled++;

            // For the both-on-mesh links, print how FAR the endpoints really are from solid
            // navmesh (gap), and the sampled Y. A large gap = endpoint near an eroded edge,
            // which the link's own tighter snap tolerance rejects → no connection.
            if (sOn && eOn && sampled <= 18)
            {
                float sGap = Vector3.Distance(wStart, sHit.position);
                float eGap = Vector3.Distance(wEnd,   eHit.position);
                sb.AppendLine(
                    $"  [BOTH-ON] {link.name} startGap={sGap:F2} (sampledY={sHit.position.y:F2}) " +
                    $"endGap={eGap:F2} (sampledY={eHit.position.y:F2}) path={pathInfo}");
            }
        }

        sb.AppendLine($"\nLEDGE/LINK SUMMARY: total={links.Length} connected(path complete)={connected} " +
                      $"startEndpointOffMesh={startOff} endEndpointOffMesh={endOff} sampledBothOnButNoPath={noPath}");

        // ── Worker waypoints reachability ────────────────────────────────────
        var wps = Object.FindObjectsByType<Waypoint>(FindObjectsSortMode.None);
        sb.AppendLine($"\nWaypoint count = {wps.Length}");
        // Use a ground reference point: sample down at world origin-ish of the first ground waypoint
        Vector3 groundRef = Vector3.zero;
        bool haveGround = false;
        foreach (var wp in wps)
        {
            if (wp.transform.position.y < 0.3f)
            {
                if (NavMesh.SamplePosition(wp.transform.position, out var gHit, 1f, filter))
                { groundRef = gHit.position; haveGround = true; break; }
            }
        }
        sb.AppendLine($"groundRef = {groundRef:F2} (found={haveGround})");

        foreach (var wp in wps)
        {
            if (!wp.name.Contains("Worker")) continue;
            Vector3 p = wp.transform.position;
            bool onMesh = NavMesh.SamplePosition(p, out var wHit, 1f, filter);
            string path = "n/a";
            if (haveGround && onMesh)
            {
                var np = new NavMeshPath();
                NavMesh.CalculatePath(groundRef, wHit.position, filter, np);
                path = $"{np.status}";
            }
            sb.AppendLine($"  WP {wp.name} pos={p:F2} (Y={p.y:F2}) onMesh={onMesh} mappedY={(onMesh ? wHit.position.y.ToString("F2") : "-")} pathFromGround={path}");
        }

        // ── Dock-top internal connectivity ──────────────────────────────────
        // Are the two dock-top waypoints connected to EACH OTHER? If not, the dock
        // surface is fragmented (the injected boxes didn't merge into one mesh).
        Vector3? dockA = null, dockB = null;
        foreach (var wp in wps)
        {
            if (wp.transform.position.y > 0.5f &&
                NavMesh.SamplePosition(wp.transform.position, out var dHit, 1f, filter))
            {
                if (dockA == null) dockA = dHit.position;
                else if (dockB == null) dockB = dHit.position;
            }
        }
        if (dockA != null && dockB != null)
        {
            var dp = new NavMeshPath();
            NavMesh.CalculatePath(dockA.Value, dockB.Value, filter, dp);
            sb.AppendLine($"\nDOCK-TOP INTERNAL: pathBetweenTwoDockWaypoints = {dp.status} " +
                          $"(corners={dp.corners.Length})  A={dockA.Value:F2} B={dockB.Value:F2}");
        }
        else
        {
            sb.AppendLine("\nDOCK-TOP INTERNAL: fewer than 2 dock waypoints found on mesh.");
        }

        sb.AppendLine("===== END DIAGNOSTICS =====");
        Debug.Log(sb.ToString());
    }
}
