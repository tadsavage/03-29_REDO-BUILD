using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace GameCore.Actors
{
    public enum MheSegKind { Path, Pivot }

    /// <summary>One step of a route. A Path is a polyline the machine's CENTRE follows (forward, or backward when <see cref="Reverse"/>);
    /// a Pivot turns the machine on the spot about its centre to <see cref="Heading"/>.</summary>
    public sealed class MheSeg
    {
        public enum SegResult { Ok, Replan, Fail, Cancelled }

        public MheSegKind Kind;
        public readonly List<Vector3> Pts = new();
        public readonly List<float> Cap = new();       // speed cap of the span starting at each point (m/s, 0 = none)
        public readonly List<bool> InAisle = new();    // span runs inside an aisle (heading held to the aisle line for jacks)
        public bool Reverse;
        public Vector3 Heading;
        public bool AboutRoot;   // a Pivot that turns about the machine's root (its own pivot), not its centre: lands exactly on a pad
        public string Label;
        /// <summary>Aisle mouths this path drives in through: arc length where the machine's front reaches the mouth, the aisle and the direction.</summary>
        public readonly List<(float s, MheAisle aisle, int dir)> Gates = new();

        private float[] _len;
        public float Length { get { EnsureLen(); return _len[_len.Length - 1]; } }

        public void Add(Vector3 p, float cap, bool inAisle)
        {
            if (Pts.Count > 0 && Flat(p - Pts[Pts.Count - 1]).sqrMagnitude < 0.0025f) { Cap[Cap.Count - 1] = cap; InAisle[InAisle.Count - 1] = inAisle; return; }
            Pts.Add(p); Cap.Add(cap); InAisle.Add(inAisle); _len = null;
        }

        private void EnsureLen()
        {
            if (_len != null && _len.Length == Pts.Count) return;
            _len = new float[Mathf.Max(1, Pts.Count)];
            for (int i = 1; i < Pts.Count; i++) _len[i] = _len[i - 1] + Flat(Pts[i] - Pts[i - 1]).magnitude;
        }

        public float LenAt(int i) { EnsureLen(); return _len[Mathf.Clamp(i, 0, _len.Length - 1)]; }

        public Vector3 PointAt(float s)
        {
            EnsureLen();
            if (Pts.Count == 0) return Vector3.zero;
            if (s <= 0f) return Pts[0];
            for (int i = 1; i < Pts.Count; i++)
                if (s <= _len[i]) { float seg = _len[i] - _len[i - 1]; return Vector3.Lerp(Pts[i - 1], Pts[i], seg > 1e-5f ? (s - _len[i - 1]) / seg : 1f); }
            return Pts[Pts.Count - 1];
        }

        public int SpanAt(float s)
        {
            EnsureLen();
            for (int i = 1; i < Pts.Count; i++) if (s < _len[i]) return i - 1;
            return Mathf.Max(0, Pts.Count - 2);
        }

        public Vector3 TangentAt(float s)
        {
            if (Pts.Count < 2) return Heading;
            int i = SpanAt(s);
            Vector3 d = Flat(Pts[i + 1] - Pts[i]);
            return d.sqrMagnitude > 1e-6f ? d.normalized : Heading;
        }

        /// <summary>Arc length of the point on the path nearest <paramref name="p"/>, searched from span <paramref name="fromSpan"/> on.</summary>
        public float Project(Vector3 p, int fromSpan, out int span)
        {
            EnsureLen();
            span = Mathf.Clamp(fromSpan, 0, Mathf.Max(0, Pts.Count - 2));
            if (Pts.Count < 2) return 0f;
            float best = float.MaxValue, bestS = _len[span];
            int bestSpan = span;
            int last = Mathf.Min(Pts.Count - 2, span + 4);
            for (int i = span; i <= last; i++)
            {
                Vector3 a = Flat(Pts[i]), b = Flat(Pts[i + 1]), q = Flat(p);
                Vector3 ab = b - a;
                float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(q - a, ab) / ab.sqrMagnitude) : 0f;
                float d = (a + ab * t - q).sqrMagnitude;
                if (d < best - 1e-5f) { best = d; bestS = _len[i] + ab.magnitude * t; bestSpan = i; }
            }
            span = bestSpan;
            return bestS;
        }

        public float CapAt(float s) { if (Cap.Count == 0) return 0f; return Cap[Mathf.Clamp(SpanAt(s), 0, Cap.Count - 1)]; }
        public bool InAisleAt(float s) { if (InAisle.Count == 0) return false; return InAisle[Mathf.Clamp(SpanAt(s), 0, InAisle.Count - 1)]; }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }

    public sealed class MheRoute
    {
        public readonly List<MheSeg> Segs = new();
        public string Summary;
    }

    /// <summary>Where a machine should end up. Positions are the machine's ROOT (its transform), like every caller already uses.</summary>
    public struct MheGoal
    {
        public Vector3 Root;
        /// <summary>Facing at the goal (zero = any).</summary>
        public Vector3 Heading;
        public string Label;
        /// <summary>A straight move from where the machine stands, no planning (forward or backward along the machine's own axis).</summary>
        public bool Straight;
        public bool Reverse;
        /// <summary>Only turn on the spot to <see cref="Heading"/>.</summary>
        public bool PivotOnly;
        /// <summary>Treat <see cref="Root"/> as the machine's CENTRE (used for pivot points: the machine turns about its centre there).</summary>
        public bool CentreAt;
        /// <summary>The goal is fine anywhere within this of the point (open floor only).</summary>
        public float Tolerance;
        public float SpeedCap;
        /// <summary>Pallets the sensor ignores (the one being picked up).</summary>
        public Transform IgnorePallet;

        public static MheGoal To(Vector3 root, Vector3 heading, string label) => new MheGoal { Root = root, Heading = heading, Label = label };
        public static MheGoal CentreTo(Vector3 centre, Vector3 heading, string label) => new MheGoal { Root = centre, Heading = heading, Label = label, CentreAt = true };
        public static MheGoal StraightTo(Vector3 root, bool reverse, string label) => new MheGoal { Root = root, Straight = true, Reverse = reverse, Label = label };
        public static MheGoal Turn(Vector3 heading, string label) => new MheGoal { Heading = heading, PivotOnly = true, Label = label };
        /// <summary>Turn on the spot about the machine's root (as a parking finish does), every step checked like any other turn.</summary>
        public static MheGoal TurnAboutRoot(Vector3 heading, string label) => new MheGoal { Heading = heading, PivotOnly = true, AboutRoot = true, Label = label };
        public bool AboutRoot;
    }

    /// <summary>
    /// Builds routes for <see cref="MheMotor"/> (MHE traffic rework, 2026-11-18).
    ///
    /// Open floor: the NavMesh (MHE agent, rack aisles excluded) plans the way; the path corners are pushed off walls and racks so the whole
    /// machine, not just its centre, clears them. Inside a rack aisle: the aisle's own lane, entered and left only by its ends. Pallet jacks
    /// always drive an aisle with its chevron (toward higher bays), enter at the low-bay end, leave by the far end, and hold the aisle line
    /// within 30 degrees, so they never turn round or lie across an aisle. Reach trucks may use an aisle either way. Lane changes in an aisle
    /// (pulling up to a pick face, passing a parked machine) are gentle diagonals.
    /// </summary>
    public static class MhePlanner
    {
        public const float ApproachLen = 2.6f;       // a machine lines up this far in front of an aisle mouth
        public const float FaceGap = 0.5f;           // a pallet jack parks this far from the pick face (Tad, 2026-11-18)
        public const float LaneChangeDeg = 18f;      // nominal lane-change angle in an aisle (never more than MheTraffic.AisleMaxDeg)
        public const float SettleLen = 0.9f;         // straight run before a stop, so the machine stops square to the aisle
        public const float JackReverseInAisleMax = 1.5f;   // a jack may back up this far in an aisle (a short re-park), never further

        public static MheRoute Plan(MheMotor m, MheGoal g, out string why)
        {
            why = null;
            var route = new MheRoute();
            Vector3 c = m.Centre;
            Vector3 h = m.Forward;
            float y = m.transform.position.y;

            if (g.PivotOnly)
            {
                route.Segs.Add(new MheSeg { Kind = MheSegKind.Pivot, Heading = Flat(g.Heading).normalized, Label = g.Label, AboutRoot = g.AboutRoot });
                return route;
            }

            Vector3 goalHeading = Flat(g.Heading).sqrMagnitude > 1e-4f ? Flat(g.Heading).normalized : Vector3.zero;

            if (g.Straight)
            {
                // Straight along the machine's own axis: the goal root's projection onto it.
                Vector3 gc = m.CentreFromRoot(Flat(g.Root, y), h);
                var seg = new MheSeg { Kind = MheSegKind.Path, Reverse = g.Reverse, Heading = h, Label = g.Label };
                seg.Add(Flat(c, y), g.SpeedCap, false);
                seg.Add(Flat(gc, y), g.SpeedCap, false);
                route.Segs.Add(seg);
                return route;
            }

            Vector3 goalC = g.CentreAt ? Flat(g.Root, y)
                          : (goalHeading != Vector3.zero ? m.CentreFromRoot(Flat(g.Root, y), goalHeading) : Flat(g.Root, y));

            MheAisle here = MheAisleMap.At(c, 0.6f, 0.2f);
            MheAisle there = MheAisleMap.At(goalC, 0.3f, 0.2f);
            // A goal right at a mouth (inside the slack) counts as the aisle only if it is really between the racks.
            if (there != null && !there.Contains(goalC, 0.05f, 0.2f)) there = null;

            var path = new MheSeg { Kind = MheSegKind.Path, Heading = h, Label = g.Label };
            path.Add(Flat(c, y), 0f, here != null);
            Vector3 curC = Flat(c, y), curH = h;
            var summary = new System.Text.StringBuilder();

            // 1. Standing in an aisle.
            if (here != null)
            {
                float a0 = here.Along(curC), a1 = there == here ? here.Along(goalC) : 0f;
                bool jack = m.Kind == MheKind.Jack;
                // A reach truck / dock stocker left at a rack face after working a slot (turned toward the rack, or tight against it): back
                // straight out toward the aisle's middle, then turn on the spot onto the aisle line - what a driver does after a put-away.
                if (!jack)
                {
                    float lat0 = here.Lateral(curC);
                    float axisDot = Vector3.Dot(curH, here.Travel);
                    float offDeg = Vector3.Angle(curH, axisDot >= 0f ? here.Travel : -here.Travel);
                    float band = Mathf.Max(0f, here.Half - m.HalfLength - MheTraffic.Gap);   // a turn on the spot fits inside this
                    if (offDeg > 10f || Mathf.Abs(lat0) > here.Half - m.HalfWidth - MheTraffic.Gap)
                    {
                        float across = Vector3.Dot(curH, here.Right);
                        if (Mathf.Abs(across) > 0.2f)
                        {
                            // move along its own axis toward the centre line
                            float s = -Mathf.Sign(lat0) * Mathf.Sign(across);   // +1 = forward, -1 = backward
                            float need = Mathf.Abs(lat0) / Mathf.Abs(across);   // all the way to the centre line: a loaded truck needs the whole aisle to turn
                            need = Mathf.Min(need, 3f);
                            if (need > 0.05f)
                            {
                                var sq = new MheSeg { Kind = MheSegKind.Path, Reverse = s < 0f, Heading = curH, Label = g.Label + " (out from the rack)" };
                                sq.Add(curC, m.ReverseSpeed, true);
                                curC = Flat(curC + curH * (s * need), y);
                                sq.Add(curC, m.ReverseSpeed, true);
                                route.Segs.Add(sq);
                            }
                        }
                        Vector3 axis = Vector3.Dot(curH, here.Travel) >= 0f ? here.Travel : -here.Travel;
                        route.Segs.Add(new MheSeg { Kind = MheSegKind.Pivot, Heading = axis, Label = g.Label + " (square up)" });
                        curH = axis;
                        path = new MheSeg { Kind = MheSegKind.Path, Heading = curH, Label = g.Label };
                        path.Add(curC, 0f, true);
                        summary.Append("backed out from the rack and squared up, ");
                    }
                }
                int dirHere = Vector3.Dot(curH, here.Travel) >= 0f ? 1 : -1;
                // A reach truck whose stop in this aisle means going against traffic coming the other way: out of the end it faces
                // and back in with the flow, rather than meeting it head-on in the aisle.
                bool sameAisle = there == here;
                if (sameAisle && !jack)
                {
                    int want = (there.Along(goalC) >= a0) ? 1 : -1;
                    if (OpposingTraffic(m, here, want, a0, there.Along(goalC))) { sameAisle = false; summary.Append("traffic coming the other way, going round; "); }
                }
                if (sameAisle)
                {
                    int dir = a1 >= a0 ? 1 : -1;
                    float back = a0 - a1;
                    if (jack && dir < 0 && back > JackReverseInAisleMax)
                    {
                        // Never back up or turn round in an aisle: out of the far end, round, and in again at the low-bay end.
                        AppendLane(m, path, here, curC, 1, here.AlongEnd + m.EntryClear, here.Lateral(curC), y, out curC);
                        curH = here.Travel;
                        summary.Append($"out of {Name(here)} (stop is behind), ");
                        here = null;
                    }
                    else if (dir < 0 && (jack || back <= 6f))
                    {
                        // A short backtrack: reverse straight along the aisle.
                        if (path.Pts.Count > 1) route.Segs.Add(path);
                        var rev = new MheSeg { Kind = MheSegKind.Path, Reverse = true, Heading = curH, Label = g.Label + " (back up)" };
                        rev.Add(curC, 0.8f, true);
                        // straight back on the line it is on: a jack never crosses the aisle while backing up (the 30 degree cap makes
                        // that impossible and it ends up pinned against the rack); a pick on the other face is walked to
                        rev.Add(here.Point(a1, jack ? here.Lateral(curC) : here.Lateral(goalC)), 0.8f, true);
                        route.Segs.Add(rev);
                        FinishHeading(route, goalHeading, g.Label);
                        route.Summary = summary.Append($"back up {back:0.0} m in {Name(here)}").ToString();
                        return route;
                    }
                    else
                    {
                        // NEVER turn round in an aisle (a reach truck with a load cannot, and it blocks the aisle while it tries): a stop
                        // behind it is reached driving in reverse along the lane.
                        bool rev = dir != dirHere;
                        if (rev) { if (path.Pts.Count > 1) route.Segs.Add(path); path = new MheSeg { Kind = MheSegKind.Path, Reverse = true, Heading = curH, Label = g.Label + " (reversing)" }; path.Add(curC, m.ReverseSpeed, true); }
                        AppendLane(m, path, here, curC, dir, a1, here.Lateral(goalC), y, out curC, goalC);
                        if (rev) for (int i = 0; i < path.Cap.Count; i++) path.Cap[i] = Mathf.Min(path.Cap[i] > 0f ? path.Cap[i] : m.ReverseSpeed, m.ReverseSpeed);
                        route.Segs.Add(path);
                        FinishHeading(route, goalHeading, g.Label);
                        route.Summary = summary.Append($"along {Name(here)}{(rev ? " in reverse" : "")} to {g.Label}").ToString();
                        return route;
                    }
                }
                else
                {
                    // Leave this aisle. A jack only by the far end. A reach truck by whichever end is cheaper - the end it faces, or backing
                    // out of the one behind it (never turning round inside the aisle).
                    int dir;
                    if (jack) dir = 1;
                    else
                    {
                        Vector3 eHi = here.Point(here.AlongEnd + m.EntryClear, 0f), eLo = here.Point(here.AlongStart - m.EntryClear, 0f);
                        float cHi = (here.AlongEnd - a0) + Flat(goalC - eHi).magnitude, cLo = (a0 - here.AlongStart) + Flat(goalC - eLo).magnitude;
                        // reversing costs double
                        if (dirHere > 0) cLo *= 1.6f; else cHi *= 1.6f;
                        dir = cHi <= cLo ? 1 : -1;
                        float outA = dir > 0 ? here.AlongEnd : here.AlongStart;
                        if (OpposingTraffic(m, here, dir, a0, outA)) dir = -dir;
                    }
                    bool rev = dir != dirHere;
                    if (rev) { if (path.Pts.Count > 1) route.Segs.Add(path); path = new MheSeg { Kind = MheSegKind.Path, Reverse = true, Heading = curH, Label = g.Label + " (backing out)" }; path.Add(curC, m.ReverseSpeed, true); }
                    float outAlong = dir > 0 ? here.AlongEnd + m.EntryClear : here.AlongStart - m.EntryClear;
                    // hold the lateral line it is on until it is out (a diagonal at the very end of an aisle swings the forks into the rack end)
                    AppendLane(m, path, here, curC, dir, outAlong, here.Lateral(curC), y, out curC);
                    if (rev)
                    {
                        for (int i = 0; i < path.Cap.Count; i++) path.Cap[i] = m.ReverseSpeed;
                        route.Segs.Add(path);
                        path = new MheSeg { Kind = MheSegKind.Path, Heading = curH, Label = g.Label };
                        path.Add(curC, 0f, false);
                        // facing back into the aisle now: the open-floor leg turns it round out here
                    }
                    else curH = here.Travel * dir;
                    summary.Append($"{(rev ? "backed" : "drove")} out of {Name(here)}, ");
                    here = null;
                }
            }

            // 1b. Starting somewhere tight on open floor (in a staging lane between pallets, against a rack end): straight out along the
            //     machine's own line first, the way a driver backs out of a lane before turning.
            if (here == null && path.Pts.Count == 1 && !m.PoseClearOfStatics(curC, curH, 0.35f))
            {
                bool done = false;
                foreach (float sign in new[] { -1f, 1f })
                {
                    for (float d = 0.5f; d <= 6f && !done; d += 0.5f)
                    {
                        Vector3 c2 = curC + curH * (sign * d);
                        if (!MheNav.Sample(c2, 0.5f, out _, false)) break;
                        if (!m.PoseClearOfStatics(c2, curH, 0f)) continue;    // still touching something: keep going
                        if (!m.PoseClearOfStatics(c2, curH, 0.35f)) continue; // not yet clear enough to turn
                        var esc = new MheSeg { Kind = MheSegKind.Path, Reverse = sign < 0f, Heading = curH, Label = g.Label + " (straight out)" };
                        esc.Add(curC, Mathf.Min(m.ReverseSpeed, m.AisleSpeed), false);
                        esc.Add(Flat(c2, y), Mathf.Min(m.ReverseSpeed, m.AisleSpeed), false);
                        route.Segs.Add(esc);
                        curC = Flat(c2, y);
                        path = new MheSeg { Kind = MheSegKind.Path, Heading = curH, Label = g.Label };
                        path.Add(curC, 0f, false);
                        summary.Append($"straight {(sign < 0f ? "back" : "on")} {d:0.0} m out of a tight spot, ");
                        done = true;
                    }
                    if (done) break;
                }
            }

            // 2. Open floor to the goal, or to the mouth of the goal's aisle and up it.
            if (there != null)
            {
                bool jack = m.Kind == MheKind.Jack;
                int dir = 1;
                float aGoal = there.Along(goalC);
                Vector3 entryLo = there.Point(there.AlongStart - m.EntryClear, 0f), entryHi = there.Point(there.AlongEnd + m.EntryClear, 0f);
                if (!jack)
                {
                    float lo = Flat(entryLo - curC).magnitude + (aGoal - there.AlongStart);
                    float hi = Flat(entryHi - curC).magnitude + (there.AlongEnd - aGoal);
                    dir = lo <= hi ? 1 : -1;
                    // not into traffic coming the other way along the stretch it would drive
                    if (OpposingTraffic(m, there, dir, dir > 0 ? there.AlongStart - 1f : there.AlongEnd + 1f, aGoal)
                        && !OpposingTraffic(m, there, -dir, -dir > 0 ? there.AlongStart - 1f : there.AlongEnd + 1f, aGoal)) dir = -dir;
                    if (!MheNav.Sample(dir > 0 ? entryLo : entryHi, 0.8f, out _)) dir = -dir;
                }
                // Line up on the side of the mouth that clears anything standing there (a machine holding back at the mouth).
                {
                    float eAlong = dir > 0 ? there.AlongStart - m.EntryClear : there.AlongEnd + m.EntryClear;
                    float hw = m.HalfWidth, minL = there.LatMin + hw + MheTraffic.Gap, maxL = there.LatMax - hw - MheTraffic.Gap;
                    if (minL < maxL)
                    {
                        float eLat = ChooseCruise(m, there, dir, eAlong, eAlong + dir * (m.EntryClear + 4f), 0f, minL, maxL);
                        entryLo = there.Point(there.AlongStart - m.EntryClear, eLat);
                        entryHi = there.Point(there.AlongEnd + m.EntryClear, eLat);
                    }
                }
                Vector3 entry = dir > 0 ? entryLo : entryHi;
                Vector3 entryHeading = there.Travel * dir;
                if (!MheNav.Sample(entry, 1.0f, out Vector3 entryOnMesh))
                { why = $"the mouth of {Name(there)} ({(dir > 0 ? "low-bay end" : "high-bay end")}) is not on the machine floor"; return null; }
                entry = Flat(entryOnMesh, y);
                if (!AppendOpenFloor(m, path, curC, curH, entry, entryHeading, y, out why)) return null;
                // in through the mouth
                path.Gates.Add((path.Length - m.HalfLength + m.EntryClear - 0.1f, there, dir));
                AppendLane(m, path, there, entry, dir, aGoal, there.Lateral(goalC), y, out curC, goalC);
                route.Segs.Add(path);
                FinishHeading(route, goalHeading, g.Label);
                route.Summary = summary.Append($"to the {(dir > 0 ? "low-bay" : "high-bay")} mouth of {Name(there)}, up it to {g.Label}").ToString();
                return route;
            }

            if (!AppendOpenFloor(m, path, curC, curH, goalC, goalHeading, y, out why, g.Tolerance)) return null;
            route.Segs.Add(path);
            FinishHeading(route, goalHeading, g.Label);
            route.Summary = summary.Append($"open floor to {g.Label}").ToString();
            return route;
        }

        private static string Name(MheAisle a) => "aisle " + a.Prefix.TrimEnd('-');

        /// <summary>Another machine in this aisle, near the stretch <paramref name="a0"/>..<paramref name="a1"/>, on a leg going the other way
        /// along it (driving or held). Driving into it head-on in an aisle is a deadlock waiting to happen.</summary>
        public static bool OpposingTraffic(MheMotor m, MheAisle a, int dir, float a0, float a1)
        {
            // only AHEAD: from where it is (a0) on toward its stop / exit (a1) and a little past it
            float lo = dir > 0 ? a0 + 0.1f : a1 - 4f, hi = dir > 0 ? a1 + 4f : a0 - 0.1f;
            if (lo > hi) { float t = lo; lo = hi; hi = t; }
            foreach (var o in MheTraffic.Motors)
            {
                if (o == null || o == m || !o.isActiveAndEnabled || !o.IsDriving) continue;
                if (!a.Contains(o.Centre, 2.5f, 0.3f)) continue;
                float al = a.Along(o.Centre);
                if (al < lo || al > hi) continue;
                Vector3 it = o.Intent;
                if (it == Vector3.zero) continue;
                if (Vector3.Dot(it, a.Travel) * dir < -0.5f) return true;
            }
            return false;
        }

        /// <summary>Where the machine's centre will be at the goal.</summary>
        public static Vector3 GoalCentre(MheMotor m, MheGoal g)
        {
            float y = m.transform.position.y;
            Vector3 h = Flat(g.Heading).sqrMagnitude > 1e-4f ? Flat(g.Heading).normalized : Vector3.zero;
            if (g.CentreAt) return Flat(g.Root, y);
            return h != Vector3.zero ? m.CentreFromRoot(Flat(g.Root, y), h) : Flat(g.Root, y);
        }

        /// <summary>The first stretch of <paramref name="route"/> cut short where it comes within <paramref name="keepAway"/> of
        /// <paramref name="point"/> (a holding point short of someone else's work). Null when the machine is already that close.</summary>
        public static MheSeg HoldSegment(MheRoute route, Vector3 point, float keepAway)
        {
            if (route == null || route.Segs.Count == 0 || route.Segs[0].Kind != MheSegKind.Path) return null;
            var seg = route.Segs[0];
            if (seg.Pts.Count < 2 || Flat(seg.Pts[0] - point).magnitude < keepAway) return null;
            float L = seg.Length, hold = -1f;
            for (float s = 0f; s <= L; s += 0.2f)
                if (Flat(seg.PointAt(s) - point).magnitude < keepAway) { hold = Mathf.Max(0f, s - 0.3f); break; }
            if (hold < 0f) hold = L;   // the whole first stretch stays clear: drive it
            if (hold < 0.5f) return null;
            var cut = new MheSeg { Kind = MheSegKind.Path, Reverse = seg.Reverse, Heading = seg.Heading, Label = seg.Label + " (holding back)" };
            for (int i = 0; i < seg.Pts.Count; i++)
            {
                if (seg.LenAt(i) >= hold - 0.01f) break;
                cut.Add(seg.Pts[i], seg.Cap[i], seg.InAisle[i]);
            }
            cut.Add(seg.PointAt(hold), seg.CapAt(hold), seg.InAisleAt(hold));
            foreach (var gt in seg.Gates) if (gt.s < hold) cut.Gates.Add(gt);
            return cut.Pts.Count >= 2 ? cut : null;
        }

        private static void FinishHeading(MheRoute route, Vector3 heading, string label)
        {
            if (heading != Vector3.zero) route.Segs.Add(new MheSeg { Kind = MheSegKind.Pivot, Heading = heading, Label = label + " (square up)" });
        }

        private static void FlushPivot(MheRoute route, ref MheSeg path, Vector3 curH, Vector3 newH, string label, float y, Vector3 curC)
        {
            if (path.Pts.Count > 1) route.Segs.Add(path);
            route.Segs.Add(new MheSeg { Kind = MheSegKind.Pivot, Heading = Flat(newH).normalized, Label = label + " (turn)" });
            path = new MheSeg { Kind = MheSegKind.Path, Heading = newH, Label = label };
            path.Add(curC, 0f, true);
        }

        // ── Inside an aisle ───────────────────────────────────────────────────────────────────────

        /// <summary>Appends the drive along <paramref name="a"/> from <paramref name="from"/> to along-position <paramref name="toAlong"/>,
        /// ending at lateral <paramref name="toLat"/>. Cruises on a line that clears every standing machine in the way (a pass), and makes
        /// every lane change as a gentle diagonal (<see cref="LaneChangeDeg"/>, never steeper than <see cref="MheTraffic.AisleMaxDeg"/>).</summary>
        private static void AppendLane(MheMotor m, MheSeg path, MheAisle a, Vector3 from, int dir, float toAlong, float toLat, float y, out Vector3 end, Vector3? exactGoal = null)
        {
            float a0 = a.Along(from), l0 = a.Lateral(from);
            float hw = m.HalfWidth, minLat = a.LatMin + hw + MheTraffic.Gap, maxLat = a.LatMax - hw - MheTraffic.Gap;
            if (minLat > maxLat) { minLat = maxLat = 0f; }
            float cap = m.AisleSpeed;
            // The centre line, except: leaving the aisle = stay on the line it is on (no diagonal at the rack end); traffic coming the
            // other way = keep right.
            float preferred = 0f;
            bool leaving = toAlong > a.AlongEnd || toAlong < a.AlongStart;
            if (leaving) preferred = l0;
            if (OpposingTraffic(m, a, dir, a0, toAlong)) preferred = dir > 0 ? maxLat : minLat;
            float cruise = Mathf.Clamp(preferred, minLat, maxLat);
            cruise = ChooseCruise(m, a, dir, a0, toAlong, cruise, minLat, maxLat);
            toLat = Mathf.Clamp(toLat, minLat, maxLat);

            float total = (toAlong - a0) * dir;
            if (total <= 0.02f)
            {
                Vector3 p = exactGoal.HasValue ? Flat(exactGoal.Value, y) : Flat(a.Point(toAlong, toLat), y);
                path.Add(p, cap, true);
                end = p;
                return;
            }
            float tanN = Mathf.Tan(LaneChangeDeg * Mathf.Deg2Rad), tanMax = Mathf.Tan((MheTraffic.AisleMaxDeg - 4f) * Mathf.Deg2Rad);

            // Lane change 1: from where it stands onto the cruise line.
            float d1 = Mathf.Abs(cruise - l0);
            float len1 = d1 / tanN;
            // Lane change 2: from the cruise line onto the stop line, then a short straight to settle.
            float d2 = Mathf.Abs(toLat - cruise);
            float len2 = d2 / tanN;
            float need = len1 + len2 + (d2 > 0.02f ? SettleLen : 0f);
            if (need > total)
            {
                // Not enough room at the nominal angle: steepen (never past the cap), then give up some of the final shift.
                float k = Mathf.Max(tanN, Mathf.Min(tanMax, (d1 + d2) / Mathf.Max(0.05f, total - (d2 > 0.02f ? SettleLen : 0f))));
                len1 = d1 / k; len2 = d2 / k;
                need = len1 + len2 + (d2 > 0.02f ? SettleLen : 0f);
                if (need > total)
                {
                    float room = Mathf.Max(0f, total - SettleLen);
                    // skip the cruise line: shift straight from l0 toward toLat as far as the room allows
                    float maxShift = room * tanMax;
                    float shift = Mathf.Clamp(toLat - l0, -maxShift, maxShift);
                    toLat = l0 + shift;
                    cruise = toLat;
                    len1 = Mathf.Abs(shift) / tanMax; d2 = 0f; len2 = 0f;
                    exactGoal = null;
                }
            }
            float s = a0;
            if (len1 > 0.02f) { s += dir * len1; path.Add(Flat(a.Point(s, cruise), y), cap, true); }
            float startChange2 = toAlong - dir * (len2 + (d2 > 0.02f ? SettleLen : 0f));
            if ((startChange2 - s) * dir > 0.05f) { s = startChange2; path.Add(Flat(a.Point(s, cruise), y), cap, true); }
            if (d2 > 0.02f) { s += dir * len2; path.Add(Flat(a.Point(s, toLat), y), cap, true); }
            Vector3 last = exactGoal.HasValue && Mathf.Abs(a.Lateral(exactGoal.Value) - toLat) < 0.05f ? Flat(exactGoal.Value, y) : Flat(a.Point(toAlong, toLat), y);
            path.Add(last, cap, true);
            end = last;
        }

        /// <summary>The lateral line to cruise on between <paramref name="a0"/> and <paramref name="a1"/>: the aisle centre unless a standing
        /// machine (parked to pick, a reach truck working) would be inside our clearance there, then the nearest line that clears all of them.</summary>
        private static float ChooseCruise(MheMotor m, MheAisle a, int dir, float a0, float a1, float preferred, float minLat, float maxLat)
        {
            var blocks = new List<(float lo, float hi)>();
            float lo = Mathf.Min(a0, a1) - m.HalfLength, hi = Mathf.Max(a0, a1) + m.HalfLength;
            foreach (var o in MheTraffic.Motors)
            {
                if (o == null || o == m || !o.isActiveAndEnabled || !o.IsStanding) continue;
                if (!a.Contains(o.Centre, 2f, 0.5f)) continue;
                var ob = o.Outline;
                float oLo = float.MaxValue, oHi = float.MinValue, oA0 = float.MaxValue, oA1 = float.MinValue;
                for (int i = 0; i < 4; i++)
                {
                    Vector3 cpt = ob.Corner(i, a.Y);
                    float l = a.Lateral(cpt), al = a.Along(cpt);
                    oLo = Mathf.Min(oLo, l); oHi = Mathf.Max(oHi, l); oA0 = Mathf.Min(oA0, al); oA1 = Mathf.Max(oA1, al);
                }
                if (oA1 < lo || oA0 > hi) continue;
                // The stop itself sits in front of / behind it: only machines strictly between start and stop matter for the cruise line.
                if (dir > 0 && oA0 > a1 + m.HalfLength) continue;
                if (dir < 0 && oA1 < a1 - m.HalfLength) continue;
                float pad = m.HalfWidth + MheTraffic.Gap + 0.05f;
                blocks.Add((oLo - pad, oHi + pad));
            }
            if (blocks.Count == 0) return preferred;
            bool Clear(float l) { foreach (var b in blocks) if (l > b.lo && l < b.hi) return false; return true; }
            if (Clear(preferred)) return preferred;
            float best = preferred, bestD = float.MaxValue;
            var cands = new List<float> { minLat, maxLat };
            foreach (var b in blocks) { cands.Add(b.lo - 0.01f); cands.Add(b.hi + 0.01f); }
            foreach (var cnd in cands)
            {
                float l = Mathf.Clamp(cnd, minLat, maxLat);
                if (!Clear(l)) continue;
                float d = Mathf.Abs(l - preferred);
                if (d < bestD) { bestD = d; best = l; }
            }
            return best;
        }

        // ── Open floor ────────────────────────────────────────────────────────────────────────────

        /// <summary>NavMesh route (aisles excluded) from <paramref name="from"/> to <paramref name="to"/>, lined up for the last
        /// <see cref="ApproachLen"/> when a heading is given, with each corner pushed off walls and racks for the machine's width.</summary>
        private static bool AppendOpenFloor(MheMotor m, MheSeg path, Vector3 from, Vector3 fromHeading, Vector3 to, Vector3 toHeading, float y, out string why, float tolerance = 0f)
        {
            why = null;
            var filter = MheNav.OpenFloorFilter(m.Kind == MheKind.DockStocker);
            float cap = m.OpenSpeed;

            Vector3 startOnMesh;
            if (NavMesh.SamplePosition(from, out var fromHit, 3f, filter)) startOnMesh = fromHit.position;
            else if (!MheNav.Sample(from, 3f, out startOnMesh, false)) { why = $"no machine floor near the start {from}"; return false; }
            if (!NavMesh.SamplePosition(to, out var toHit, 2.5f, filter)) { why = $"no open machine floor near {to}"; return false; }
            Vector3 goal = toHit.position;

            // Line up in front of a headed goal: the longest clear straight approach up to ApproachLen.
            Vector3 navTarget = goal;
            bool approach = false;
            if (toHeading != Vector3.zero)
            {
                // The whole machine must fit, lined up, at the approach point and on the way in from it (not just its centre).
                for (float L = ApproachLen; L >= 0.6f; L -= 0.4f)
                {
                    Vector3 ap = goal - toHeading * L;
                    if (!NavMesh.SamplePosition(ap, out var aph, 0.25f, filter)) continue;
                    if (!MheNav.SegmentClear(aph.position, goal, filter)) continue;
                    if (!m.PoseClearOfStatics(Flat(aph.position, y), toHeading, MheTraffic.Gap)) continue;
                    if (!m.PoseClearOfStatics(Flat((aph.position + goal) * 0.5f, y), toHeading, MheTraffic.Gap)) continue;
                    navTarget = aph.position; approach = true;
                    break;
                }
            }

            Vector3 navStart = startOnMesh;
            var nav = new NavMeshPath();
            if (!NavMesh.CalculatePath(navStart, navTarget, filter, nav) || nav.corners.Length == 0)
            { why = $"no machine route from {from} to {to}"; return false; }
            if (nav.status != NavMeshPathStatus.PathComplete)
            {
                Vector3 endP = nav.corners[nav.corners.Length - 1];
                if (Flat(endP - navTarget).magnitude > Mathf.Max(1.0f, tolerance)) { why = $"only a partial route to {to} (ends {Flat(endP - navTarget).magnitude:0.0} m short)"; return false; }
            }

            var corners = Densify(new List<Vector3>(nav.corners), 1.0f);
            // Keep the route off walls, racks and pallets by more than the NavMesh radius: every point (not just the corners) is pushed
            // toward the middle of the space it runs through, so the whole machine clears what is beside it, and a long machine turning
            // about its centre has room to swing its ends.
            float need = Mathf.Min(m.HalfWidth + MheTraffic.Gap + 0.6f, 0.5f * (m.HalfWidth + Mathf.Sqrt(m.HalfLength * m.HalfLength + m.HalfWidth * m.HalfWidth)) + MheTraffic.Gap) - MheNav.AgentRadius;
            for (int i = 1; i < corners.Count - 1; i++)
            {
                for (int pass = 0; pass < 4; pass++)
                {
                    float d = MheNav.EdgeDistance(corners[i], filter, out Vector3 n);
                    if (d < 0f || d >= need || n == Vector3.zero) break;
                    Vector3 pushed = corners[i] + n.normalized * (need - d + 0.02f);
                    if (!NavMesh.SamplePosition(pushed, out var ph, 0.2f, filter)) break;
                    if (!MheNav.SegmentClear(corners[i - 1], ph.position, filter) || !MheNav.SegmentClear(ph.position, corners[i + 1], filter)) break;
                    corners[i] = ph.position;
                }
                // The machine at the corner, facing along the turn, must be clear of racks / walls / pallets: if not, slide the corner
                // sideways (either way) a little until it is.
                Vector3 bis = Flat(corners[i + 1] - corners[i - 1]);
                if (bis.sqrMagnitude < 1e-4f) continue;
                bis.Normalize();
                Vector3 hin = Flat(corners[i] - corners[i - 1]), hout = Flat(corners[i + 1] - corners[i]);
                hin = hin.sqrMagnitude > 1e-4f ? hin.normalized : bis; hout = hout.sqrMagnitude > 1e-4f ? hout.normalized : bis;
                bool TurnFits(Vector3 p) => m.PoseClearOfStatics(Flat(p, y), bis, 0.05f) && m.PoseClearOfStatics(Flat(p, y), hin, 0.05f) && m.PoseClearOfStatics(Flat(p, y), hout, 0.05f);
                if (TurnFits(corners[i])) continue;
                Vector3 side = Vector3.Cross(Vector3.up, bis);
                foreach (float off in new[] { 0.3f, -0.3f, 0.6f, -0.6f, 0.9f, -0.9f, 1.2f, -1.2f })
                {
                    Vector3 cand = corners[i] + side * off;
                    if (!NavMesh.SamplePosition(cand, out var ch, 0.2f, filter)) continue;
                    if (!TurnFits(ch.position)) continue;
                    if (!MheNav.SegmentClear(corners[i - 1], ch.position, filter) || !MheNav.SegmentClear(ch.position, corners[i + 1], filter)) continue;
                    corners[i] = ch.position;
                    break;
                }
            }

            var pts = new List<Vector3> { Flat(from, y) };
            for (int i = 1; i < corners.Count; i++) pts.Add(Flat(corners[i], y));
            if (approach || Flat(goal - corners[corners.Count - 1]).magnitude > 0.05f) pts.Add(Flat(goal, y));
            // The NavMesh snapped the goal (a staging pivot point sits where the lane pallets carve the mesh): when the machine fits at the
            // exact point, end there. A pivot point 0.9 m off the lane line sent a jack reversing into the next lane's pallet.
            Vector3 exact = Flat(to, y);
            if (Flat(exact - pts[pts.Count - 1]).magnitude > 0.05f)
            {
                Vector3 dirIn = Flat(exact - pts[pts.Count - 1]).normalized;
                Vector3 hd = toHeading != Vector3.zero ? toHeading : dirIn;
                if (m.PoseClearOfStatics(exact, hd, 0f) && Flat(exact - pts[pts.Count - 1]).magnitude < 2.6f) pts.Add(exact);
            }
            InsertDetour(m, pts, filter, y);
            int approachFrom = approach ? pts.Count - 2 : int.MaxValue;
            for (int i = 1; i < pts.Count; i++) path.Add(pts[i], i > approachFrom ? Mathf.Min(cap, m.AisleSpeed) : cap, false);
            return true;
        }

        /// <summary>Adds points so no span is longer than <paramref name="step"/> (the end points stay exactly where they are).</summary>
        private static List<Vector3> Densify(List<Vector3> pts, float step)
        {
            if (pts.Count < 2) return pts;
            var o = new List<Vector3> { pts[0] };
            for (int i = 1; i < pts.Count; i++)
            {
                Vector3 a = pts[i - 1], b = pts[i];
                int n = Mathf.Max(1, Mathf.CeilToInt(Flat(b - a).magnitude / step));
                for (int k = 1; k <= n; k++) o.Add(Vector3.Lerp(a, b, k / (float)n));
            }
            return o;
        }

        // ── Driving round a standing machine ──────────────────────────────────────────────────────

        /// <summary>Is there room to drive round <paramref name="o"/>? In an aisle: a lane beside it wide enough for us plus the gap on
        /// both sides. On open floor: assumed yes (the detour is checked when it is built).</summary>
        public static bool CanDriveRound(MheMotor m, MheMotor o)
        {
            if (m == null || o == null) return false;
            var a = MheAisleMap.At(o.Centre, 0.5f, 0.3f);
            if (a == null) return true;
            var ob = o.Outline;
            float lo = float.MaxValue, hi = float.MinValue;
            for (int i = 0; i < 4; i++) { float l = a.Lateral(ob.Corner(i, a.Y)); lo = Mathf.Min(lo, l); hi = Mathf.Max(hi, l); }
            float need = 2f * m.HalfWidth + 2f * MheTraffic.Gap + 0.05f;
            return Mathf.Max(lo - a.LatMin, a.LatMax - hi) >= need;
        }

        /// <summary>The first standing machine the open-floor route runs into gets a detour round its freer side.</summary>
        private static void InsertDetour(MheMotor m, List<Vector3> pts, NavMeshQueryFilter filter, float y)
        {
            if (pts.Count < 2) return;
            var lens = new float[pts.Count];
            for (int i = 1; i < pts.Count; i++) lens[i] = lens[i - 1] + Flat(pts[i] - pts[i - 1]).magnitude;
            float total = lens[pts.Count - 1];
            Vector3 P(float s)
            {
                s = Mathf.Clamp(s, 0f, total);
                for (int i = 1; i < pts.Count; i++)
                    if (s <= lens[i]) { float seg = lens[i] - lens[i - 1]; return Vector3.Lerp(pts[i - 1], pts[i], seg > 1e-5f ? (s - lens[i - 1]) / seg : 1f); }
                return pts[pts.Count - 1];
            }
            Vector3 T(float s)
            {
                s = Mathf.Clamp(s, 0f, total);
                for (int i = 1; i < pts.Count; i++) if (s <= lens[i] || i == pts.Count - 1) { var d = Flat(pts[i] - pts[i - 1]); return d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.forward; }
                return Vector3.forward;
            }

            foreach (var slot in MachineClearance.AllMachines())
            {
                if (slot == null || slot == m.Slot) continue;
                var om = slot.GetComponent<MheMotor>();
                if (om != null && !om.IsStanding) continue;
                MheObb o = MheMotor.OutlineOf(slot);
                Vector3 oc = new Vector3(o.C.x, y, o.C.y);
                float s0 = -1f, s1 = -1f;
                for (float s = 0.6f; s <= total; s += 0.3f)
                {
                    bool hit = m.OutlineAt(P(s), T(s)).Inflated(MheTraffic.Gap).Overlaps(o);
                    if (hit) { if (s0 < 0f) s0 = s; s1 = s; }
                    else if (s0 >= 0f && s - s1 > 1.2f) break;
                }
                if (s0 < 0f) continue;
                // The goal itself is inside it: nothing to drive round (the caller waits or chooses another spot).
                if (s1 >= total - 0.35f) return;
                Vector3 t0 = T(s0), c0 = P(s0);
                Vector3 n = Vector3.Cross(Vector3.up, t0).normalized;
                float lo = float.MaxValue, hi = float.MinValue;
                for (int i = 0; i < 4; i++) { float l = Vector3.Dot(Flat(o.Corner(i, y) - c0), n); lo = Mathf.Min(lo, l); hi = Mathf.Max(hi, l); }
                float pad = m.HalfWidth + MheTraffic.Gap + 0.15f;
                float offR = hi + pad, offL = lo - pad;
                var sides = Mathf.Abs(offR) <= Mathf.Abs(offL) ? new[] { offR, offL } : new[] { offL, offR };
                foreach (float off in sides)
                {
                    Vector3 A = P(s0 - 1.8f), B = P(s0 - 0.2f) + n * off, Cc = P(s1 + 0.2f) + n * off, Dd = P(s1 + 1.8f);
                    if (!NavMesh.SamplePosition(B, out var bh, 0.25f, filter) || !NavMesh.SamplePosition(Cc, out var ch, 0.25f, filter)) continue;
                    if (!MheNav.SegmentClear(A, bh.position, filter) || !MheNav.SegmentClear(bh.position, ch.position, filter) || !MheNav.SegmentClear(ch.position, Dd, filter)) continue;
                    // the detour itself must clear the standing machine (and its sides must clear racks)
                    if (m.OutlineAt(Flat(B, y), Flat(Cc - B).normalized).Inflated(MheTraffic.Gap).Overlaps(o) && Flat(Cc - B).sqrMagnitude > 0.01f) continue;
                    var rebuilt = new List<Vector3>();
                    float sA = Mathf.Max(0f, s0 - 1.8f), sD = Mathf.Min(total, s1 + 1.8f);
                    for (int i = 0; i < pts.Count; i++) if (lens[i] < sA - 0.01f) rebuilt.Add(pts[i]);
                    if (sA > 0.01f) rebuilt.Add(Flat(A, y));
                    rebuilt.Add(Flat(bh.position, y));
                    rebuilt.Add(Flat(ch.position, y));
                    rebuilt.Add(Flat(Dd, y));
                    for (int i = 0; i < pts.Count; i++) if (lens[i] > sD + 0.01f) rebuilt.Add(pts[i]);
                    pts.Clear(); pts.AddRange(rebuilt);
                    return;
                }
                return;   // no way round it: the sensor waits behind it
            }
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
        private static Vector3 Flat(Vector3 v, float y) => new Vector3(v.x, y, v.z);
    }
}
