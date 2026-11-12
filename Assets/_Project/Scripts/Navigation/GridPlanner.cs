using System.Collections.Generic;
using UnityEngine;

namespace GameCore.Navigation
{
    /// <summary>The machine's outline relative to its pivot, in metres. Front is ahead of the pivot, Back is behind it
    /// (a pallet jack drags its forks behind it, so its Back is long). Use <c>MachineProfile.FromClearance</c> to read the real
    /// outline from a machine instead of typing numbers in.</summary>
    public struct MachineProfile
    {
        public string Name;
        public float Front, Back, HalfWidth;
        /// <summary>May the planner back the machine up (jacks reversing out of a lane)? Reversing costs more.</summary>
        public bool CanReverse;
        /// <summary>Pallet jacks: inside an aisle the machine must face the aisle's travel direction (toward higher bay numbers) at every
        /// state, so it can never enter against the traffic, turn round or sit sideways in the aisle. Reach trucks leave this off:
        /// they may go either way. Judged on every aisle cell the OUTLINE touches, not just the pivot, so a jack cannot swing its tail
        /// into an aisle mouth sideways.</summary>
        public bool FollowAisleTraffic;
        /// <summary>With <see cref="FollowAisleTraffic"/>: may the machine BACK UP inside an aisle (against the traffic, still facing it)? Off by
        /// default, so a route never reverses up an aisle to reach a stop behind it; the jack driver sets it only for its short back-out.</summary>
        public bool AllowAisleReverse;
        /// <summary>Dock stockers stay out of the aisles (Tad, 2026-11-09): no state may touch an aisle cell. The planner then simply has no route
        /// that runs down an aisle, which is what AiNavigation's "refused to drive ... the only route runs down aisle 02" did after the fact.</summary>
        public bool AvoidAisles;
    }

    public enum PlanStatus { Ok, GoalBlocked, NoPath, OutOfBudget }

    public struct PlanStep
    {
        public int X, Z, Heading;
    }

    public sealed class PlanResult
    {
        public PlanStatus Status;
        /// <summary>Every lattice state from start to goal (one per cell moved or quarter-turn made).</summary>
        public readonly List<PlanStep> Steps = new List<PlanStep>();
        /// <summary>Only the points where the route turns, plus start and goal (world space, y = start y).</summary>
        public readonly List<Vector3> Waypoints = new List<Vector3>();
        /// <summary>For each entry of <see cref="Waypoints"/>, the index into <see cref="Steps"/> it came from.</summary>
        public readonly List<int> WaypointStep = new List<int>();
        /// <summary>The route a machine should actually drive: <see cref="Waypoints"/> with open-floor staircases cut into straight
        /// segments. Never cuts through a cost cell (the rack-clearance ring), a one-way cell, a reverse leg, or the final approach
        /// when an arrival heading was asked for, and every straight segment is swept with the machine's real outline at its real angle.</summary>
        public readonly List<Vector3> Smoothed = new List<Vector3>();
        /// <summary>For each entry of <see cref="Smoothed"/>, the index into <see cref="Steps"/> it came from. The heading the machine
        /// holds on the leg that LEAVES smoothed point i is <c>Steps[SmoothedStep[i]].Heading</c> (use <see cref="LegHeading"/>), which is how
        /// a follower tells a reverse leg (travel opposite the heading) from a forward one.</summary>
        public readonly List<int> SmoothedStep = new List<int>();
        /// <summary>Heading the machine holds while driving smoothed leg <paramref name="i"/> (from point i-1 to point i).</summary>
        public int LegHeading(int i) => Steps[SmoothedStep[i - 1]].Heading;
        public float Cost;
        public int Expanded;
        public int GridVersion;
        public bool Ok => Status == PlanStatus.Ok;
    }

    /// <summary>
    /// A* over a <see cref="NavGrid"/> on a 4-heading lattice: a state is (cell, heading); a move is one cell forward (or back, if the
    /// profile may reverse) or a quarter-turn in place. Warehouse aisles are axis-aligned, so this gives routes that look like
    /// something a forklift would drive, and it lets the machine's real length be tested at every state instead of just its centre.
    ///
    /// All the rules are data on the grid: blocked cells, per-cell cost, per-cell allowed exit directions. The planner has no rules
    /// of its own beyond turn / reverse cost. The START state is deliberately never validated: a machine restored overlapping
    /// something must still be able to plan its way out.
    /// </summary>
    public sealed class GridPlanner
    {
        public float TurnCost = 2f;
        public float ReverseMultiplier = 2f;
        /// <summary>Safety valve: a plan that expands this many states gives up as OutOfBudget.</summary>
        public int MaxExpansions = 400000;
        /// <summary>The outline is shrunk by this on each side so touching a neighbour cell's edge does not count as overlapping it.</summary>
        public float Skin = 0.05f;

        private readonly NavGrid _grid;
        private float[] _g;
        private int[] _parent;
        private int[] _stamp;
        private int _curStamp;
        private readonly Heap _open = new Heap();

        public GridPlanner(NavGrid grid)
        {
            _grid = grid;
        }

        public NavGrid Grid => _grid;

        // ── Footprint ───────────────────────────────────────────────────────────────────────────────

        /// <summary>Cell offsets (relative to the pivot cell) the outline covers when the machine faces <paramref name="heading"/>.</summary>
        public List<Vector2Int> FootprintOffsets(MachineProfile p, int heading)
        {
            heading &= 3;
            float cs = _grid.CellSize;
            // World-axis extents relative to the pivot, per heading (the outline is axis-aligned at every lattice heading).
            float minX, maxX, minZ, maxZ;
            switch (heading)
            {
                case NavGrid.North: minX = -p.HalfWidth; maxX = p.HalfWidth; minZ = -p.Back;  maxZ = p.Front; break;
                case NavGrid.South: minX = -p.HalfWidth; maxX = p.HalfWidth; minZ = -p.Front; maxZ = p.Back;  break;
                case NavGrid.East:  minX = -p.Back;  maxX = p.Front; minZ = -p.HalfWidth; maxZ = p.HalfWidth; break;
                default:            minX = -p.Front; maxX = p.Back;  minZ = -p.HalfWidth; maxZ = p.HalfWidth; break;
            }
            minX += Skin; minZ += Skin; maxX -= Skin; maxZ -= Skin;
            // Cell k spans ((k - 0.5) * cs, (k + 0.5) * cs) around the pivot cell centre.
            int kx0 = Mathf.FloorToInt(minX / cs - 0.5f) + 1, kx1 = Mathf.CeilToInt(maxX / cs + 0.5f) - 1;
            int kz0 = Mathf.FloorToInt(minZ / cs - 0.5f) + 1, kz1 = Mathf.CeilToInt(maxZ / cs + 0.5f) - 1;
            var list = new List<Vector2Int>();
            for (int x = kx0; x <= kx1; x++)
                for (int z = kz0; z <= kz1; z++)
                    list.Add(new Vector2Int(x, z));
            if (list.Count == 0) list.Add(Vector2Int.zero);
            return list;
        }

        private bool _follow, _aisleReverse, _avoidAisles;   // set per Plan from the profile

        private bool Fits(List<Vector2Int>[] fp, int x, int z, int h)
        {
            var offs = fp[h];
            for (int i = 0; i < offs.Count; i++)
            {
                int cx = x + offs[i].x, cz = z + offs[i].y;
                if (_grid.IsBlocked(cx, cz)) return false;
                if (_avoidAisles && _grid.IsAisle(cx, cz)) return false;
                if (_follow)
                {
                    int t = _grid.GetAisleTravel(cx, cz);
                    if (t >= 0 && t != h) return false;   // in an aisle, face the traffic or stay out
                }
            }
            return true;
        }

        // ── Plan ────────────────────────────────────────────────────────────────────────────────────

        /// <param name="goalHeading">-1 = arrive facing any way; 0..3 = arrive facing that way (e.g. into a rack bay).</param>
        public PlanResult Plan(MachineProfile profile, Vector3 startWorld, int startHeading, Vector3 goalWorld, int goalHeading = -1)
        {
            var res = new PlanResult { GridVersion = _grid.Version };
            int W = _grid.Width, H = _grid.Height;
            var fp = new List<Vector2Int>[4];
            for (int h = 0; h < 4; h++) fp[h] = FootprintOffsets(profile, h);

            Vector2Int sc = _grid.WorldToCell(startWorld);
            Vector2Int gc = _grid.WorldToCell(goalWorld);
            startHeading &= 3;
            _follow = profile.FollowAisleTraffic;
            _aisleReverse = profile.AllowAisleReverse;
            _avoidAisles = profile.AvoidAisles;

            // The goal must be reachable in at least one acceptable heading, or there is no point searching.
            bool goalOk = false;
            if (goalHeading >= 0) goalOk = Fits(fp, gc.x, gc.y, goalHeading & 3);
            else for (int h = 0; h < 4 && !goalOk; h++) goalOk = Fits(fp, gc.x, gc.y, h);
            if (!goalOk) { res.Status = PlanStatus.GoalBlocked; return res; }

            int n = W * H * 4;
            if (_g == null || _g.Length != n) { _g = new float[n]; _parent = new int[n]; _stamp = new int[n]; _curStamp = 0; }
            _curStamp++;
            _open.Clear();

            int start = State(sc.x, sc.y, startHeading, W);
            Visit(start, 0f, -1);
            _open.Push(start, Heuristic(sc.x, sc.y, gc.x, gc.y));

            int goalState = -1;
            int expanded = 0;
            while (_open.Count > 0)
            {
                _open.Pop(out int cur, out float fCur);
                int cx, cz, ch;
                Decode(cur, W, out cx, out cz, out ch);
                float gCur = _g[cur];
                // Lazy deletion: skip entries superseded by a cheaper route found later.
                if (fCur > gCur + Heuristic(cx, cz, gc.x, gc.y) + 1e-3f) continue;

                if (cx == gc.x && cz == gc.y && (goalHeading < 0 || ch == (goalHeading & 3))) { goalState = cur; break; }
                if (++expanded > MaxExpansions) { res.Status = PlanStatus.OutOfBudget; res.Expanded = expanded; return res; }

                // Forward.
                TryMove(fp, cur, cx, cz, ch, ch, 1f, gCur, gc, W);
                // Reverse.
                if (profile.CanReverse) TryMove(fp, cur, cx, cz, ch, (ch + 2) & 3, ReverseMultiplier, gCur, gc, W);
                // Quarter turns in place.
                for (int t = 1; t <= 3; t += 2)
                {
                    int nh = (ch + t) & 3;
                    if (!Fits(fp, cx, cz, nh)) continue;
                    int ns = State(cx, cz, nh, W);
                    float ng = gCur + TurnCost;
                    if (_stamp[ns] == _curStamp && _g[ns] <= ng) continue;
                    Visit(ns, ng, cur);
                    _open.Push(ns, ng + Heuristic(cx, cz, gc.x, gc.y));
                }
            }

            res.Expanded = expanded;
            if (goalState < 0) { res.Status = PlanStatus.NoPath; return res; }

            // Walk the parents back, then reverse.
            var rev = new List<int>();
            for (int s = goalState; s >= 0; s = _parent[s]) rev.Add(s);
            rev.Reverse();
            foreach (int s in rev)
            {
                Decode(s, W, out int x, out int z, out int h);
                res.Steps.Add(new PlanStep { X = x, Z = z, Heading = h });
            }
            res.Cost = _g[goalState];
            res.Status = PlanStatus.Ok;
            BuildWaypoints(res, startWorld.y);
            if (SmoothOpenFloor) Smooth(res, profile, goalHeading >= 0);
            else { res.Smoothed.AddRange(res.Waypoints); res.SmoothedStep.AddRange(res.WaypointStep); }
            return res;
        }

        /// <summary>As <see cref="Plan"/>, but when the goal itself is unusable (a bay park a few cm inside a rack's cell, a cell the outline
        /// does not fit in) it plans to the nearest cell centre within <paramref name="radiusCells"/> that the machine does fit in, and says
        /// how far it moved the goal. This is the NavMesh's silent "snap the goal to the mesh", done openly and bounded: the caller sees
        /// <paramref name="movedMetres"/> and decides whether the final approach is a scripted move.</summary>
        public PlanResult PlanNearest(MachineProfile profile, Vector3 startWorld, int startHeading, Vector3 goalWorld, int goalHeading,
                                      int radiusCells, out float movedMetres)
        {
            movedMetres = 0f;
            var first = Plan(profile, startWorld, startHeading, goalWorld, goalHeading);
            if (first.Status != PlanStatus.GoalBlocked || radiusCells <= 0) return first;

            Vector2Int gc = _grid.WorldToCell(goalWorld);
            var cands = new List<KeyValuePair<float, Vector3>>();
            for (int dx = -radiusCells; dx <= radiusCells; dx++)
            {
                for (int dz = -radiusCells; dz <= radiusCells; dz++)
                {
                    if (_grid.IsBlocked(gc.x + dx, gc.y + dz)) continue;
                    Vector3 c = _grid.CellCenter(gc.x + dx, gc.y + dz, goalWorld.y);
                    float ddx = c.x - goalWorld.x, ddz = c.z - goalWorld.z;
                    cands.Add(new KeyValuePair<float, Vector3>(Mathf.Sqrt(ddx * ddx + ddz * ddz), c));
                }
            }
            cands.Sort((a, b) => a.Key.CompareTo(b.Key));
            PlanResult last = first;
            foreach (var c in cands)
            {
                last = Plan(profile, startWorld, startHeading, c.Value, goalHeading);
                // A blocked goal is rejected before any search, so walking the list is cheap. The first goal that is searched is the one used.
                if (last.Status == PlanStatus.GoalBlocked) continue;
                movedMetres = c.Key;
                return last;
            }
            return first;
        }

        // ── Open-floor smoothing ────────────────────────────────────────────────────────────────────

        /// <summary>Cut Manhattan staircases into straight segments where it is provably safe (see <see cref="PlanResult.Smoothed"/>).
        /// Aisles keep the lattice because they carry the direction rules (cost ring, one-way exits); smoothing only ever happens
        /// across plain open cells.</summary>
        public bool SmoothOpenFloor = true;
        /// <summary>Samples along a candidate straight segment are this fraction of a cell apart.</summary>
        public float SmoothSampleStep = 0.25f;
        /// <summary>A smoothed segment keeps the machine's outline at least this far (metres) from every blocked cell (Tad, 2026-11-05:
        /// "a fifth to a quarter of a meter"). It is measured to the cell edge, and a rack's real outline is thinner than its cell, so
        /// the true gap is larger. It may cross the rack-clearance cost ring; it may never enter an aisle or its mouth.</summary>
        public float ClearanceMargin = 0.25f;

        private void Smooth(PlanResult res, MachineProfile p, bool pinFinalApproach)
        {
            var pts = res.Waypoints;
            int n = pts.Count;
            if (n < 3) { res.Smoothed.AddRange(pts); res.SmoothedStep.AddRange(res.WaypointStep); return; }
            int anchor = 0;
            res.Smoothed.Add(pts[0]);
            res.SmoothedStep.Add(res.WaypointStep[0]);
            int limit = pinFinalApproach ? n - 2 : n - 1;
            while (anchor < n - 1)
            {
                int next = anchor + 1;
                for (int j = limit; j >= anchor + 2; j--)
                    if (CanShortcut(res, p, anchor, j)) { next = j; break; }
                res.Smoothed.Add(pts[next]);
                res.SmoothedStep.Add(res.WaypointStep[next]);
                anchor = next;
            }
        }

        private bool CanShortcut(PlanResult res, MachineProfile p, int i, int j)
        {
            // A leg that was driven in reverse stays as planned: the straight line would face the other way.
            for (int k = res.WaypointStep[i] + 1; k <= res.WaypointStep[j]; k++)
            {
                var a = res.Steps[k - 1]; var b = res.Steps[k];
                int dx = b.X - a.X, dz = b.Z - a.Z;
                if ((dx != 0 || dz != 0) && (dx != NavGrid.DX[b.Heading] || dz != NavGrid.DZ[b.Heading])) return false;
            }
            Vector3 P = res.Waypoints[i], Q = res.Waypoints[j];
            float fx = Q.x - P.x, fz = Q.z - P.z;
            float len = Mathf.Sqrt(fx * fx + fz * fz);
            if (len < 1e-4f) return true;
            fx /= len; fz /= len;
            float step = Mathf.Max(0.05f, _grid.CellSize * SmoothSampleStep);
            for (float t = 0f; ; t += step)
            {
                float tt = t > len ? len : t;
                if (!OpenAt(p, P.x + fx * tt, P.z + fz * tt, fx, fz)) return false;
                if (tt >= len) break;
            }
            return true;
        }

        // The oriented outline at (cx, cz) facing (fx, fz), grown by ClearanceMargin, must touch no blocked cell, no restricted-exit cell
        // and no aisle / aisle-mouth cell. Cost cells (the rack-clearance ring) are fine: the margin is what enforces the distance.
        private bool OpenAt(MachineProfile p, float cx, float cz, float fx, float fz)
        {
            float rx = fz, rz = -fx;                       // right-hand vector
            float halfL = (p.Front + p.Back) * 0.5f + ClearanceMargin, halfW = p.HalfWidth + ClearanceMargin;
            float off = (p.Front - p.Back) * 0.5f;
            float ox = cx + fx * off, oz = cz + fz * off;  // outline centre
            float ex = Mathf.Abs(fx) * halfL + Mathf.Abs(rx) * halfW;
            float ez = Mathf.Abs(fz) * halfL + Mathf.Abs(rz) * halfW;
            float cs = _grid.CellSize;
            int x0 = Mathf.FloorToInt((ox - ex - _grid.Origin.x) / cs), x1 = Mathf.FloorToInt((ox + ex - _grid.Origin.x) / cs);
            int z0 = Mathf.FloorToInt((oz - ez - _grid.Origin.z) / cs), z1 = Mathf.FloorToInt((oz + ez - _grid.Origin.z) / cs);
            for (int x = x0; x <= x1; x++)
            {
                for (int z = z0; z <= z1; z++)
                {
                    // Separating-axis test of the rotated outline against this cell (the world axes are covered by the AABB loop).
                    float ccx = _grid.Origin.x + (x + 0.5f) * cs - ox, ccz = _grid.Origin.z + (z + 0.5f) * cs - oz;
                    float h = cs * 0.5f;
                    float dF = Mathf.Abs(ccx * fx + ccz * fz), dR = Mathf.Abs(ccx * rx + ccz * rz);
                    float cellOnF = h * (Mathf.Abs(fx) + Mathf.Abs(fz)), cellOnR = h * (Mathf.Abs(rx) + Mathf.Abs(rz));
                    if (dF > halfL + cellOnF || dR > halfW + cellOnR) continue;   // not touching this cell
                    if (_grid.IsBlocked(x, z) || _grid.IsNoSmooth(x, z) || _grid.GetExits(x, z) != NavGrid.AllExits) return false;
                }
            }
            return true;
        }

        // `moveDir` is the world direction of travel; `faceHeading` stays the machine's heading (a reverse keeps facing the same way).
        private void TryMove(List<Vector2Int>[] fp, int cur, int cx, int cz, int faceHeading, int moveDir, float stepMul, float gCur, Vector2Int gc, int W)
        {
            if (!_grid.CanExit(cx, cz, moveDir)) return;
            int nx = cx + NavGrid.DX[moveDir], nz = cz + NavGrid.DZ[moveDir];
            // Backing up against the traffic inside an aisle is not a route, it is a manoeuvre: only when the profile says so.
            if (_follow && !_aisleReverse && moveDir != faceHeading && (_grid.GetAisleTravel(cx, cz) >= 0 || _grid.GetAisleTravel(nx, nz) >= 0)) return;
            if (!Fits(fp, nx, nz, faceHeading)) return;
            int ns = State(nx, nz, faceHeading, W);
            float ng = gCur + (1f + _grid.GetCost(nx, nz)) * stepMul;
            if (_stamp[ns] == _curStamp && _g[ns] <= ng) return;
            Visit(ns, ng, cur);
            _open.Push(ns, ng + Heuristic(nx, nz, gc.x, gc.y));
        }

        private void Visit(int s, float g, int parent) { _stamp[s] = _curStamp; _g[s] = g; _parent[s] = parent; }

        // Manhattan distance: every step costs at least 1 (cost layers only add), so this never overestimates.
        private static float Heuristic(int x, int z, int gx, int gz) => Mathf.Abs(x - gx) + Mathf.Abs(z - gz);

        private static int State(int x, int z, int h, int W) => (z * W + x) * 4 + h;
        private static void Decode(int s, int W, out int x, out int z, out int h)
        {
            h = s & 3; int cell = s >> 2; x = cell % W; z = cell / W;
        }

        private void BuildWaypoints(PlanResult res, float y)
        {
            var steps = res.Steps;
            if (steps.Count == 0) return;
            res.Waypoints.Add(_grid.CellCenter(steps[0].X, steps[0].Z, y));
            res.WaypointStep.Add(0);
            // A corner is a cell where the machine pivots in place, or where its direction of travel flips or changes (a reverse after a
            // forward move is a cusp even though the heading is the same).
            int lastDx = 0, lastDz = 0;
            for (int i = 1; i < steps.Count; i++)
            {
                var a = steps[i - 1]; var b = steps[i];
                int dx = b.X - a.X, dz = b.Z - a.Z;
                if (dx == 0 && dz == 0) { AddCorner(res, b.X, b.Z, y, i); continue; }
                if ((lastDx != 0 || lastDz != 0) && (dx != lastDx || dz != lastDz)) AddCorner(res, a.X, a.Z, y, i - 1);
                lastDx = dx; lastDz = dz;
            }
            var last = steps[steps.Count - 1];
            AddCorner(res, last.X, last.Z, y, steps.Count - 1);
        }

        private void AddCorner(PlanResult res, int x, int z, float y, int stepIndex)
        {
            Vector3 c = _grid.CellCenter(x, z, y);
            if (res.Waypoints.Count == 0 || (res.Waypoints[res.Waypoints.Count - 1] - c).sqrMagnitude > 1e-6f)
            {
                res.Waypoints.Add(c);
                res.WaypointStep.Add(stepIndex);
            }
            else
            {
                // Same pivot cell as the previous corner: the later step is where the machine finishes turning, so a shortcut ending here must include it.
                res.WaypointStep[res.WaypointStep.Count - 1] = stepIndex;
            }
        }

        // ── Binary min-heap (Unity's .NET profile has no PriorityQueue) ─────────────────────────────

        private sealed class Heap
        {
            private int[] _id = new int[1024];
            private float[] _f = new float[1024];
            public int Count { get; private set; }
            public void Clear() { Count = 0; }
            public void Push(int id, float f)
            {
                if (Count == _id.Length) { System.Array.Resize(ref _id, Count * 2); System.Array.Resize(ref _f, Count * 2); }
                int i = Count++;
                while (i > 0)
                {
                    int p = (i - 1) >> 1;
                    if (_f[p] <= f) break;
                    _id[i] = _id[p]; _f[i] = _f[p]; i = p;
                }
                _id[i] = id; _f[i] = f;
            }
            public void Pop(out int id, out float f)
            {
                id = _id[0]; f = _f[0];
                Count--;
                if (Count == 0) return;
                int lastId = _id[Count]; float lastF = _f[Count];
                int i = 0;
                while (true)
                {
                    int l = 2 * i + 1;
                    if (l >= Count) break;
                    int r = l + 1;
                    int m = (r < Count && _f[r] < _f[l]) ? r : l;
                    if (_f[m] >= lastF) break;
                    _id[i] = _id[m]; _f[i] = _f[m]; i = m;
                }
                _id[i] = lastId; _f[i] = lastF;
            }
        }
    }
}
