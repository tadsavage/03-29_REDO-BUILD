using UnityEngine;

namespace GameCore.Navigation
{
    /// <summary>
    /// Scene-view overlay and test harness for the grid planner. Nothing in the game uses the planner yet; this is how it is proven.
    /// Add to any GameObject, press Play, then use the component's context menu: "Rebuild Nav Grid", then "Plan Route".
    /// Gizmos: red = blocked, yellow = extra cost, cyan = dynamic reservation, green line = the last route, white dots = its corners.
    /// Selecting the object is needed to see the gizmos (drawn in OnDrawGizmosSelected, so an unselected overlay costs nothing).
    /// </summary>
    public class NavGridDebug : MonoBehaviour
    {
        [Header("Route (drag any two transforms in)")]
        public Transform from;
        public Transform to;
        [Tooltip("-1 any, 0 N, 1 E, 2 S, 3 W")] public int finalHeading = -1;

        [Header("Machine outline (metres from the pivot) - defaults are a pallet jack with forks trailing")]
        public float front = 0.6f;
        public float back = 2.55f;
        public float halfWidth = 0.7f;
        public bool canReverse = true;

        [Header("Planner")]
        public float turnCost = 2f;
        public float clearanceCost = 0.75f;

        [Header("Overlay")]
        public bool drawBlocked = true;
        public float drawY = 0.15f;

        private PlanResult _last;

        [ContextMenu("Rebuild Nav Grid")]
        public void RebuildGrid()
        {
            var placement = FindFirstObjectByType<PlacementGrid>();
            if (placement == null) { Debug.LogWarning("[NavGrid] no PlacementGrid in the scene."); return; }
            var t0 = Time.realtimeSinceStartup;
            var g = NavGridService.Rebuild(placement, new NavGridBuilder.Options { ClearanceCost = clearanceCost, RequireFloor = true });
            int blocked = 0;
            for (int x = 0; x < g.Width; x++) for (int z = 0; z < g.Height; z++) if (g.IsStaticBlocked(x, z)) blocked++;
            Debug.LogWarning($"[NavGrid] built {g.Width}x{g.Height} @ {g.CellSize:0.###} m, {blocked} blocked cells, {(Time.realtimeSinceStartup - t0) * 1000f:0.0} ms.");
        }

        [ContextMenu("Plan Route")]
        public void PlanRoute()
        {
            if (NavGridService.Planner == null) RebuildGrid();
            if (NavGridService.Planner == null || from == null || to == null) { Debug.LogWarning("[NavGrid] need a built grid plus 'from' and 'to'."); return; }
            var planner = NavGridService.Planner;
            planner.TurnCost = turnCost;
            var profile = new MachineProfile { Name = "debug", Front = front, Back = back, HalfWidth = halfWidth, CanReverse = canReverse };
            int h = NavGrid.HeadingOf(from.forward);
            var t0 = Time.realtimeSinceStartup;
            _last = planner.Plan(profile, from.position, h, to.position, finalHeading);
            float ms = (Time.realtimeSinceStartup - t0) * 1000f;
            Debug.LogWarning(_last.Ok
                ? $"[NavGrid] route OK: {_last.Steps.Count} steps, {_last.Waypoints.Count} corners, cost {_last.Cost:0.0}, {_last.Expanded} expanded, {ms:0.0} ms."
                : $"[NavGrid] route {_last.Status} after {_last.Expanded} expanded, {ms:0.0} ms.");
        }

        private void OnDrawGizmosSelected()
        {
            var g = NavGridService.Grid;
            if (g == null) return;
            Vector3 size = new Vector3(g.CellSize * 0.95f, 0.05f, g.CellSize * 0.95f);
            for (int x = 0; x < g.Width; x++)
            {
                for (int z = 0; z < g.Height; z++)
                {
                    Vector3 c = g.CellCenter(x, z, drawY);
                    if (drawBlocked && g.IsStaticBlocked(x, z)) { Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.6f); Gizmos.DrawCube(c, size); }
                    else if (g.IsDynamicBlocked(x, z)) { Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.6f); Gizmos.DrawCube(c, size); }
                    else if (g.GetCost(x, z) > 0f) { Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.25f); Gizmos.DrawCube(c, size); }
                }
            }
            if (_last == null || !_last.Ok) return;
            Gizmos.color = Color.green;
            for (int i = 1; i < _last.Steps.Count; i++)
            {
                var a = _last.Steps[i - 1]; var b = _last.Steps[i];
                Gizmos.DrawLine(g.CellCenter(a.X, a.Z, drawY + 0.1f), g.CellCenter(b.X, b.Z, drawY + 0.1f));
            }
            Gizmos.color = Color.white;
            foreach (var w in _last.Waypoints) Gizmos.DrawSphere(new Vector3(w.x, drawY + 0.15f, w.z), 0.15f);
        }
    }
}
