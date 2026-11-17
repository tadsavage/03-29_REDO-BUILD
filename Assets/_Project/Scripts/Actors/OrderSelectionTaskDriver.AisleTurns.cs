using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using GameCore.Inventory;

namespace GameCore.Actors
{
    /// <summary>
    /// AISLE-TO-AISLE TURNS FOR PALLET JACKS (Tad, 2026-11-15).
    ///
    /// Every aisle end gets fixed turn points, built once from the rack geometry (and again whenever an aisle is set up or the
    /// locations change), held in an <see cref="AisleTurnSet"/> and shown as invisible marker transforms under "[AisleTurns]":
    /// 1. START, the scrimmage line: the centre of the last pick location on the side the jack turns toward + half its width (local X),
    ///    in the direction of travel.
    /// 2. MID: that pick centre + half its width + the midpoint offset (2 m) in the direction of travel, and half its depth (local Z)
    ///    back toward the back of the rack - the middle of the rack end, just past it.
    /// 3. END: the next aisle's right-lane line, level with the scrimmage line.
    /// A jack going from aisle A to aisle B drives A's lane to the scrimmage line, WAITS until no other machine is in the turn, curves
    /// to the midpoint, WAITS until the entry of aisle B is clear, curves onto B's right lane and carries on up B in its traffic
    /// direction. Pallet jacks only; every step still goes through the clearance guard (SteerToGuarded).
    /// </summary>
    public partial class OrderSelectionTaskDriver
    {
        private const string AisleTurnRootName = "[AisleTurns]";
        private const float TurnMaxBackUp = 7f;   // a jack this far past a turn's scrimmage line backs up to it rather than going round some other way

        /// <summary>Builds (or rebuilds) every turn from the live pick locations: both ends of every aisle, toward the aisle across the
        /// rack on each side. Returns how many turns were built.</summary>
        public static int BuildAisleTurns()
        {
            var set = AisleTurns.Set;
            set.turns.Clear();

            // One lane per aisle, and the ground-plane centres of its pick locations.
            var lanes = new Dictionary<int, AisleLane>();
            var picks = new Dictionary<int, List<LocationData>>();
            int count = 0;
            foreach (var loc in LocationRegistry.All)
            {
                if (loc == null) continue;
                count++;
                if (!TryParseAddress(loc.Address, out string prefix, out _, out _) || !int.TryParse(prefix.Substring(0, 2), out int aisle)) continue;
                if (!picks.TryGetValue(aisle, out var list)) picks[aisle] = list = new List<LocationData>();
                list.Add(loc);
                if (!lanes.ContainsKey(aisle) && TryGetAisleLane(loc, out var lane) && lane.HasMin && lane.HasMax) lanes[aisle] = lane;
            }

            float w = set.pickWidth, d = set.pickDepth, off = set.turnMidpointOffset, y = set.groundY;
            foreach (var kv in lanes)
            {
                int a = kv.Key;
                AisleLane A = kv.Value;
                for (int endIdx = 0; endIdx < 2; endIdx++)
                {
                    bool high = endIdx == 0;
                    Vector3 h = high ? A.Travel : -A.Travel;
                    Vector3 left = -Vector3.Cross(Vector3.up, h).normalized;
                    for (int s = 0; s < 2; s++)
                    {
                        bool turnLeft = s == 0;
                        Vector3 side = turnLeft ? left : -left;

                        // The last pick on that side at this end (furthest along h).
                        LocationData last = null; float bestAlong = float.MinValue;
                        foreach (var loc in picks[a])
                        {
                            Vector3 rel = Flat(loc.transform.position - A.Centre, 0f);
                            if (Vector3.Dot(rel, side) < 0.5f) continue;
                            float al = Vector3.Dot(rel, h);
                            if (al > bestAlong + 0.05f) { bestAlong = al; last = loc; }
                        }
                        if (last == null) continue;
                        Vector3 pc = Flat(last.transform.position, y);

                        // The aisle across that rack: parallel, beyond the pick on this side, nearest.
                        int toAisle = -1; float bestLat = float.MaxValue; AisleLane B = default;
                        foreach (var kb in lanes)
                        {
                            if (kb.Key == a) continue;
                            AisleLane cand = kb.Value;
                            if (Mathf.Abs(Vector3.Dot(cand.Travel, A.Travel)) < 0.9f) continue;
                            float lat = Vector3.Dot(Flat(cand.Centre - pc, 0f), side);
                            if (lat <= d * 0.5f || lat > 12f) continue;
                            // its racks must reach this end too (to within a bay of the scrimmage line, or past it: in "three doors" the
                            // outer racks run 5.3 m past the middle double rack at the west end, so the turn is made inside aisle 02)
                            float candEnd = Mathf.Max(Vector3.Dot(Flat(cand.Lane(cand.AlongMin) - pc, 0f), h), Vector3.Dot(Flat(cand.Lane(cand.AlongMax) - pc, 0f), h));
                            if (candEnd < -(cand.BayPitch + 1f)) continue;
                            if (lat < bestLat) { bestLat = lat; toAisle = kb.Key; B = cand; }
                        }
                        if (toAisle < 0) continue;

                        // On the lane the jack drives out of A on (A's right lane for heading h), level with the scrimmage line - the
                        // mirror of END on B's lane (Tad, 2026-11-15: it sat on the pick row, against the middle rack).
                        Vector3 line = pc + h * (w * 0.5f);
                        float aLat = Vector3.Dot(A.Travel, h) > 0f ? A.Offset : -A.Offset;
                        Vector3 start = A.Centre + A.Right * aLat + A.Travel * A.Along(line);
                        Vector3 mid = pc + h * (w * 0.5f + off) + side * (d * 0.5f);
                        Vector3 into = -h;
                        bool withTraffic = Vector3.Dot(B.Travel, into) > 0.5f;
                        // B's right lane for a jack driving INTO it from here (its own lane if that is B's traffic direction).
                        float laneLat = withTraffic ? B.Offset : -B.Offset;
                        float alongB = B.Along(start);
                        Vector3 end = B.Centre + B.Right * laneLat + B.Travel * alongB; end.y = y;
                        start.y = y; mid.y = y;

                        set.turns.Add(new AisleTurn
                        {
                            fromAisle = a, toAisle = toAisle, atHighEnd = high, turnLeft = turnLeft, withTraffic = withTraffic,
                            lastPick = last.Address, start = start, mid = mid, end = end, heading = h, side = side,
                        });
                    }
                }
            }

            // REACH TRUCK GATES (Tad, 2026-11-15): one at each end of every aisle, on the lane opposite the jacks' lane, level with that
            // end's scrimmage line (a built turn's, else the end pick + half a pick width).
            set.rtGates.Clear();
            foreach (var kv in lanes)
            {
                AisleLane A = kv.Value;
                for (int endIdx = 0; endIdx < 2; endIdx++)
                {
                    bool high = endIdx == 0;
                    Vector3 h = high ? A.Travel : -A.Travel;
                    float lineAlong = float.NaN;
                    foreach (var t in set.turns)
                        if (t.fromAisle == kv.Key && t.atHighEnd == high) { lineAlong = A.Along(t.start); break; }
                    if (float.IsNaN(lineAlong)) lineAlong = high ? A.AlongMax + w * 0.5f : A.AlongMin - w * 0.5f;
                    Vector3 gp = A.Centre - A.Right * A.Offset + A.Travel * lineAlong; gp.y = y;
                    set.rtGates.Add(new RtAisleGate { aisle = kv.Key, atHighEnd = high, point = gp, outward = h });
                }
            }

            set.builtFromLocations = count;
            AisleTurns.MarkBuilt();
            BuildAisleTurnMarkers(set);
            foreach (var g in set.rtGates) Debug.Log($"[AisleTurns]   {g.Label}: {g.point:F2}");
            Debug.Log($"[AisleTurns] built {set.turns.Count} pallet-jack turns for {lanes.Count} aisles (midpoint offset {off:0.0} m).");
            foreach (var t in set.turns)
                Debug.Log($"[AisleTurns]   {t.Label}: last pick {t.lastPick}, start {t.start:F2}, mid {t.mid:F2}, end {t.end:F2}");
            return set.turns.Count;
        }

        /// <summary>The invisible transforms: [AisleTurns] / Turn ... / Start, Mid, End.</summary>
        private static void BuildAisleTurnMarkers(AisleTurnSet set)
        {
            var old = GameObject.Find(AisleTurnRootName);
            if (old != null) Destroy(old);
            var root = new GameObject(AisleTurnRootName);
            foreach (var t in set.turns)
            {
                var go = new GameObject("Turn " + t.Label);
                go.transform.SetParent(root.transform, false);
                go.transform.position = t.mid;
                go.transform.rotation = Quaternion.LookRotation(t.heading);
                go.AddComponent<AisleTurnMarker>().turn = t;
                AddMarkerPoint(go.transform, "Start", t.start, t.heading);
                AddMarkerPoint(go.transform, "Mid", t.mid, t.side);
                AddMarkerPoint(go.transform, "End", t.end, -t.heading);
            }
            foreach (var g in set.rtGates)
            {
                var go = new GameObject(g.Label);
                go.transform.SetParent(root.transform, false);
                AddMarkerPoint(go.transform, "Gate", g.point, g.outward, Color.green);
            }
        }

        private static void AddMarkerPoint(Transform parent, string name, Vector3 at, Vector3 facing) => AddMarkerPoint(parent, name, at, facing, Color.red);

        private static void AddMarkerPoint(Transform parent, string name, Vector3 at, Vector3 facing, Color colour)
        {
            var p = new GameObject(name).transform;
            p.SetParent(parent, false);
            p.position = at;
            p.rotation = Quaternion.LookRotation(facing);

            // A small red sphere so the point can be seen in the Game view (Tad, 2026-11-15). Debug only: no collider.
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = name + " (marker)";
            var col = ball.GetComponent<Collider>();
            if (col != null) Destroy(col);
            ball.transform.SetParent(p, false);
            ball.transform.localScale = Vector3.one * 0.3f;
            var r = ball.GetComponent<Renderer>();
            if (r != null)
            {
                r.sharedMaterial = MarkerMaterial(colour);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        private static readonly Dictionary<Color, Material> s_markerMats = new Dictionary<Color, Material>();
        private static Material MarkerMaterial(Color colour)
        {
            if (s_markerMats.TryGetValue(colour, out var m) && m != null) return m;
            var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            m = new Material(sh) { name = "AisleTurnMarker " + colour };
            m.color = colour;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", colour);
            s_markerMats[colour] = m;
            return m;
        }

        /// <summary>The turn point a jack is in the middle of: past a turn's scrimmage line, out beyond the rack end, short of the next aisle.
        /// Selectors never pick from here (Tad, 2026-11-15) - only before the exit line or after crossing the entry line.</summary>
        internal static bool InTurnZone(Vector3 p)
        {
            foreach (var t in AisleTurns.Set.turns)
            {
                Vector3 q = Flat(p - t.start, 0f);
                float al = Vector3.Dot(q, t.heading), la = Vector3.Dot(q, t.side), span = Vector3.Dot(Flat(t.end - t.start, 0f), t.side);
                float depth = Vector3.Dot(Flat(t.mid - t.start, 0f), t.heading);
                // Between the two lane lines (off both aisles' lanes, where the midpoint is). On a lane line past the scrimmage line is
                // still that aisle (the outer rack's last bays run past the middle rack's end at the west end of "three doors").
                if (al > 0.2f && al < depth + 2f && la > 1.0f && la < span - 1.0f) return true;
            }
            return false;
        }

        /// <summary>The live turn set, rebuilt first when an aisle changed or the number of locations did.</summary>
        internal static AisleTurnSet EnsureAisleTurns()
        {
            var set = AisleTurns.Set;
            int count = 0;
            foreach (var loc in LocationRegistry.All) if (loc != null) count++;
            if (AisleTurns.Dirty || set.builtFromLocations != count) BuildAisleTurns();
            return set;
        }

        /// <summary>The jack is in another aisle, driving toward the end that has a built turn into <paramref name="target"/>.</summary>
        private bool TryGetTurnInto(AisleLane target, out AisleLane from, out AisleTurn turn)
        {
            from = default; turn = null;
            if (_jack == null || target.Prefix == null || !int.TryParse(target.Prefix.Substring(0, 2), out int toNo)) return false;
            Transform jt = _jack.transform;
            if (TryFindAisleAt(JackCentre(jt), AisleEntryLead, out from) && from.Prefix != target.Prefix
                && TryGetTurn(from, target, Flat(jt.forward, 0f).normalized, out turn)) return true;
            turn = null;

            // JUST OUT OF THE AISLE'S END (Tad, 2026-11-16: the turn went wide of the red markers). A jack that has driven on past the far end of
            // the aisle it was in (beyond AisleEntryLead, so it no longer counts as "in" it) used to fall through to the old rack-end hop, which
            // ignores the markers and steers waypoint to waypoint. If it stands on open floor just past a built turn's scrimmage line, on that
            // aisle's exit lane, into the aisle it wants, it takes THE TURN: DriveAisleTurn backs it up to the line if it is a little past it.
            foreach (var t in EnsureAisleTurns().turns)
            {
                if (t.toAisle != toNo || !t.withTraffic) continue;
                Vector3 q = Flat(jt.position - t.start, 0f);
                float al = Vector3.Dot(q, t.heading);
                if (al < -0.3f || al >= TurnMaxBackUp) continue;
                if ((q - t.heading * al).magnitude > 2f) continue;     // on the exit lane of the aisle being left
                if (!TryLaneOfAisle(t.fromAisle, out from)) continue;
                turn = t;
                return true;
            }
            return false;
        }

        /// <summary>The built turn from <paramref name="from"/> into <paramref name="to"/> at the end this jack is heading for (no check of
        /// where in <paramref name="from"/> it stands: a jack at the aisle's mouth, ~4 m before its first bay, counts - the loop-in case).</summary>
        private bool TryGetTurn(AisleLane from, AisleLane to, Vector3 fwd, out AisleTurn turn)
        {
            turn = null;
            if (_jack == null || from.Prefix == null || to.Prefix == null || from.Prefix == to.Prefix) return false;
            if (!int.TryParse(from.Prefix.Substring(0, 2), out int fromNo) || !int.TryParse(to.Prefix.Substring(0, 2), out int toNo)) return false;
            if (!EnsureAisleTurns().TryGet(fromNo, toNo, fwd, out turn)) return false;
            // Past the scrimmage line (picking the outer rack's last bays, or after a pass): DriveAisleTurn backs it up to the line first.
            // Further than this and it is not this turn's business.
            return Vector3.Dot(Flat(_jack.transform.position - turn.start, 0f), turn.heading) < TurnMaxBackUp;
        }

        /// <summary>The far end of <paramref name="lane"/> (its traffic direction) runs into something solid within a few metres - a wall -
        /// so a jack can't drive out of it (Tad, 2026-11-15: jacks leaving aisle 01 for staging drove "forward, out of its far end" into the
        /// corner by the west wall).</summary>
        private static bool DeadEndAhead(AisleLane lane)
        {
            if (!lane.HasMax) return false;
            for (float d = 0.5f; d <= AisleEntryLead + 3f; d += 0.5f)
                if (MachineClearance.SolidAt(lane.Lane(lane.AlongMax + d), out _)) return true;
            return false;
        }

        /// <summary>A built turn out of the far end of <paramref name="from"/> (with its traffic) into the aisle next door.</summary>
        private bool TryTurnOutOf(AisleLane from, out AisleLane to, out AisleTurn turn)
        {
            to = default; turn = null;
            if (from.Prefix == null || !int.TryParse(from.Prefix.Substring(0, 2), out int fromNo)) return false;
            foreach (var t in EnsureAisleTurns().turns)
            {
                if (t.fromAisle != fromNo || !t.withTraffic || Vector3.Dot(t.heading, from.Travel) < 0.5f) continue;
                if (!TryLaneOfAisle(t.toAisle, out to)) continue;
                turn = t;
                return Vector3.Dot(Flat(_jack.transform.position - t.start, 0f), t.heading) < TurnMaxBackUp;
            }
            return false;
        }

        private static bool TryLaneOfAisle(int aisle, out AisleLane lane)
        {
            lane = default;
            string prefix = aisle.ToString("00") + "-";
            foreach (var loc in LocationRegistry.All)
                if (loc != null && loc.Address != null && loc.Address.StartsWith(prefix) && TryGetAisleLane(loc, out lane) && lane.HasMin && lane.HasMax)
                    return true;
            return false;
        }

        /// <summary>Drives one built turn: to the scrimmage line, wait for the turn to be clear, curve to the midpoint, wait for the next
        /// aisle's entry to be clear, curve onto its right lane and into it. Sets _jackLegOk; false = fall back to the old way in.</summary>
        private IEnumerator DriveAisleTurn(AisleLane from, AisleLane to, AisleTurn t)
        {
            _jackLegOk = false;
            _inAisleTurn = false;
            if (_jack == null) yield break;
            Transform jt = _jack.transform;
            float y = jt.position.y;
            Vector3 h = t.heading, side = t.side;
            float off = Vector3.Dot(Flat(t.mid - t.start, 0f), h);
            Debug.Log($"[AisleTurns] '{name}': turning {t.Label} (round {t.lastPick}).");

            // 1. Up the lane to the scrimmage line.
            Vector3 S = jt.position + h * Vector3.Dot(Flat(t.start - jt.position, 0f), h); S.y = y;
            if (Vector3.Dot(Flat(S - jt.position, 0f), h) < -(off + 1.5f))
            {
                // Already past the line (Tad, 2026-11-15: Christopher Lewis passed a jack at the west end of aisle 01, ended up by the wall
                // past the line, the old hop hit the wall and he ended up in aisle 02 facing against its traffic). Back straight up to it:
                // there is no room to turn out there.
                Debug.Log($"[AisleTurns] '{name}': {-Vector3.Dot(Flat(S - jt.position, 0f), h):0.0}m past the scrimmage line of {t.Label} - backing up to it.");
                yield return JackFace(h);
                if (_jack == null) yield break;
                yield return JackMoveStraight(S, JackReverseSpeed);
                if (_jack == null || _jackLegQueued) yield break;
                if (Vector3.Dot(Flat(S - jt.position, 0f), h) < -0.8f) { Debug.Log($"[AisleTurns] '{name}': could not back up to the scrimmage line - old way in."); yield break; }
            }
            else if (Vector3.Dot(Flat(S - jt.position, 0f), h) > 0.3f)   // short of the line: drive up to it (a jack already past it, within the turn's depth, turns from where it stands)
            {
                yield return LaneDriveCore(from, S);
                if (_jack == null || _jackLegQueued) yield break;
                if (Vector3.Dot(Flat(S - jt.position, 0f), h) > 0.8f) { Debug.Log($"[AisleTurns] '{name}': did not reach the scrimmage line - old way in."); yield break; }
            }
            S = jt.position;
            Vector3 M = Flat(t.mid, y), E = Flat(t.end, y);
            _inAisleTurn = true;   // over the scrimmage line: no picking until it is into the next aisle (cleared at the end / next bay leg)

            // 2. Hold until nobody is in the first half of the turn (scrimmage line to midpoint). A machine at the far end, in the next
            // aisle's entry, does not stop it starting: it drives to the midpoint and waits there (Tad, 2026-11-15).
            float latMid = Vector3.Dot(M - S, side);
            yield return WaitTurnClear(S, h, side, -0.2f, off + 1.5f, -1.2f, latMid + 1.0f, "the turn into aisle " + to.Prefix.TrimEnd('-'));
            if (_jack == null) yield break;

            // 3-5. PIVOT TURN (Tad, 2026-11-16: "why can't we just turn until we match the angle that the racks are at and have it drive forward?").
            // Every attempt to steer a curve ran wide, because the load wheels trail the heading. A pallet jack pivots on the spot, and that is
            // exactly how a real one takes a rack end: drive straight out to the apex line, pivot 90 degrees to face across the rack end,
            // drive straight across (through the Mid marker), pivot 90 degrees to face along the next lane, drive straight in. The root only
            // ever travels on straight lines between the markers' lines, so it cannot run wide or cross the lane line, and it is exactly parallel
            // when it enters. Each straight move and each pivot goes through the clearance guard (JackMoveStraight / JackFace).
            Vector3 into = -h;
            // Depth is measured from the scrimmage line. A jack that already drove a little past it (leaving the aisle) pivots at the depth it is
            // at, if that is beyond the Mid marker's depth, instead of backing up; otherwise it drives out to the Mid marker's depth.
            float depthNow = Vector3.Dot(Flat(jt.position - t.start, 0f), h);
            float apex = Mathf.Max(off, depthNow);
            Vector3 Q1 = jt.position + h * (apex - depthNow); Q1.y = y;   // straight out of the aisle to the pivot depth
            Vector3 Q2 = E + h * apex; Q2.y = y;                          // across the rack end (the Mid marker is on this line) to the next lane's line
            Vector3 Ein = E + into * 2.4f; Ein.y = y;     // and in along the next lane

            yield return JackMoveStraight(Q1, JackDriveSpeed);
            if (_jack == null || _jackLegQueued) yield break;
            if (PlanarDist(jt.position, Q1) > 0.6f) { Debug.Log($"[AisleTurns] '{name}': could not drive out to the turn - old way in."); yield break; }

            yield return JackPivotTo(side);
            if (_jack == null) yield break;
            if (Vector3.Angle(Flat(jt.forward, 0f), side) > 25f) { Debug.Log($"[AisleTurns] '{name}': could not pivot across the rack end - old way in."); yield break; }

            // 4. Hold at the apex until the entry of the next aisle is clear.
            yield return WaitTurnClear(E, into, side, -1.5f, 5f, -1.3f, 1.3f, "the entry of aisle " + to.Prefix.TrimEnd('-'));
            if (_jack == null) yield break;

            yield return JackMoveStraight(Q2, JackDriveSpeed);
            if (_jack == null || _jackLegQueued) yield break;
            if (PlanarDist(jt.position, Q2) > 0.6f) { Debug.Log($"[AisleTurns] '{name}': blocked crossing the rack end - old way in."); yield break; }

            yield return JackPivotTo(into);
            if (_jack == null) yield break;
            yield return JackMoveStraight(Ein, JackDriveSpeed);
            if (_jack == null) yield break;
            _inAisleTurn = false;   // past the entry line: into the next aisle
            if (PlanarDist(jt.position, Ein) > 0.8f) yield break;
            yield return JackFaceTraffic(to.Travel);
            _jackLegOk = _jack != null;
        }

        private const float TurnExitHandle = 1.2f;    // cubic turn: out-handle from the scrimmage line, x the Mid marker depth (apex lands on the marker)
        private const float TurnEntryHandle = 1.57f;   // ...and the handle along the next lane line, further out, so the jack reaches the line already straight
        private const int TurnCurveSteps = 16;   // points per quarter-turn (~0.3 m apart): the jack follows the curve between the red markers

        private bool _inAisleTurn;   // between a turn's two scrimmage lines (DriveAisleTurn): the selector may not step off to pick

        /// <summary>Waits (no time limit, Tad: "just hold until everyone is out of the way") while any other machine's outline is inside the
        /// rectangle [along a0..a1 on <paramref name="ax"/>] x [lat l0..l1 on <paramref name="lat"/>] from <paramref name="o"/>.</summary>
        private IEnumerator WaitTurnClear(Vector3 o, Vector3 ax, Vector3 lat, float a0, float a1, float l0, float l1, string what)
        {
            bool logged = false;
            while (_jack != null && TurnZoneBusy(o, ax, lat, a0, a1, l0, l1, out var who))
            {
                NoteHeld(true, who, null);
                if (!logged) { Debug.Log($"[AisleTurns] '{name}': holding for {who.name} to clear {what}."); logged = true; }
                yield return null;
            }
            NoteHeld(false, null, null);
        }

        private bool TurnZoneBusy(Vector3 o, Vector3 ax, Vector3 lat, float a0, float a1, float l0, float l1, out MHEOperatorSlot who)
        {
            who = null;
            foreach (var m in MachineClearance.AllMachines())
            {
                if (m == null || m == _jack || !m.isActiveAndEnabled) continue;
                var f = MachineClearance.FootprintOf(m);
                Transform mt = m.transform;
                for (int i = 0; i < 5; i++)
                {
                    Vector3 local = i == 4 ? new Vector3((f.MinX + f.MaxX) * 0.5f, 0f, (f.MinZ + f.MaxZ) * 0.5f)
                                           : new Vector3((i & 1) == 0 ? f.MinX : f.MaxX, 0f, (i & 2) == 0 ? f.MinZ : f.MaxZ);
                    Vector3 q = Flat(mt.TransformPoint(local) - o, 0f);
                    float al = Vector3.Dot(q, ax), la = Vector3.Dot(q, lat);
                    if (al >= a0 && al <= a1 && la >= Mathf.Min(l0, l1) && la <= Mathf.Max(l0, l1)) { who = m; return true; }
                }
            }
            return false;
        }
    }
}
