using UnityEngine;
using System.Collections.Generic;

namespace GameCore.Actors
{
    /// <summary>
    /// AISLE TRAFFIC RULES shared by every machine (Tad, 2026-11-09). Pallet-jack-only rules live in
    /// OrderSelectionTaskDriver.PalletJack.cs / .JackTraffic.cs; this file holds what reach trucks and jacks both need.
    ///
    /// 1. Reach trucks always have right of way; a pallet jack keeps at least <see cref="ReachTruckBuffer"/> (1 m) from one
    ///    (MachineClearance.ReachTruckBufferBlocked).
    /// 3. A vehicle leaving an aisle stops at the end, pauses (<see cref="ExitPauseGame"/>) and only pulls out once no other machine is
    ///    moving through the cross-aisle in front of it (<see cref="ExitBlocked"/>). Two vehicles waiting at once: first come, first out.
    /// 5. A reach truck held behind a pallet jack in an aisle passes it, UNLESS another manned jack is within 2 bays in front of or
    ///    behind that jack - then it waits (<see cref="TryPlanReachPass"/>).
    /// </summary>
    public static class AisleTraffic
    {
        public const float ReachTruckBuffer = 0.5f;    // rule 1: jacks never come closer than this to a manned reach truck (was 1 m; Tad 2026-11-14)
        public const float ExitPauseGame = 1.0f;       // rule 3: stop this long (game seconds = Time.deltaTime units) at an aisle end
        public const float ExitWaitMaxReal = 20f;      // ...and never wait longer than this for the cross-aisle to clear (REAL seconds)
        public const float ExitClearRadius = 5f;       // a machine moving within this of the exit zone is cross traffic
        public const float ReachPassJackGapBays = 2f;  // rule 5: another jack this many bays from the jack ahead = wait, don't pass
        public const float PassAngleDeg = 25f;         // lane changes never turn more than this off the aisle line (Tad: 25 degrees)
        private const float MaxBackUp = 3f;            // a reach truck backs up at most this far to make room to pull out
        private const float MovingSampleReal = 0.4f;
        private const float MovingMinStep = 0.08f;

        private static readonly List<MHEOperatorSlot> s_machines = new();
        private static float s_machinesAt = -10f;
        private static readonly Dictionary<MHEOperatorSlot, (Vector3 p, float t, bool moving)> s_motion = new();
        private static readonly Dictionary<MHEOperatorSlot, float> s_exitWaitSince = new();
        private static readonly Vector3[] s_corners = new Vector3[4];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_machines.Clear(); s_machinesAt = -10f; s_motion.Clear(); s_exitWaitSince.Clear();
            s_motionDir.Clear(); s_entries.Clear(); s_againstWaiting.Clear();
        }

        // ── Rule 6 (nav review step 3b, 2026-11-14): ONE DIRECTION PER AISLE ───────────────────────────────────────
        // An aisle carries traffic one way at a time. Before driving into an aisle a machine asks MayEnterAisle at the mouth:
        // - it waits while any manned machine inside (or one just granted entry) is DRIVING the other way along the aisle;
        //   standing / working / sideways machines don't count (the queue, pass and buffer rules already handle those);
        // - a reach truck / dock stocker waiting to go in AGAINST the aisle's travel direction (jacks only ever drive with it) is
        //   served first: new jacks wait at the mouth until it is in, so the jacks inside drain and it is never starved;
        // - never a deadlock: if the machine in the way is itself held up by the one asking, the asker goes (waiting can't help,
        //   the old rules sort it out), and nobody waits longer than TokenWaitMaxReal.
        public const float TokenWaitMaxReal = 30f;      // longest wait at a mouth (REAL seconds) before going in anyway, logged
        public const float TokenGrantReal = 15f;        // an entry granted at the mouth counts as "inside" for this long
        private const float AgainstWaitFreshReal = 1.0f;

        private static readonly Dictionary<MHEOperatorSlot, Vector3> s_motionDir = new();
        private static readonly Dictionary<MHEOperatorSlot, (string prefix, int dir, float until)> s_entries = new();
        private static readonly Dictionary<string, (MHEOperatorSlot who, float at)> s_againstWaiting = new();

        /// <summary>+1 = driving with the aisle's travel direction, -1 = against it, 0 = standing still / sideways.</summary>
        public static int AisleDir(Vector3 dir, Vector3 travel)
        {
            dir.y = 0f; travel.y = 0f;
            if (dir.sqrMagnitude < 0.0001f || travel.sqrMagnitude < 0.0001f) return 0;
            float d = Vector3.Dot(dir.normalized, travel.normalized);
            return d > 0.5f ? 1 : d < -0.5f ? -1 : 0;
        }

        /// <summary>The way a machine already in an aisle is driving along it: its last movement (0 when it is not moving).</summary>
        private static int OccupantDir(MHEOperatorSlot m, Vector3 travel)
        {
            if (!IsMoving(m)) return 0;
            return s_motionDir.TryGetValue(m, out var d) ? AisleDir(d, travel) : 0;
        }

        /// <summary>RULE 6: may <paramref name="self"/> drive into aisle <paramref name="prefix"/> (travel direction <paramref name="travel"/>)
        /// going <paramref name="myDir"/>? True grants it (the entry counts as inside for <see cref="TokenGrantReal"/>). False names who
        /// it waits for. Call every frame while waiting at the mouth.</summary>
        public static bool MayEnterAisle(MHEOperatorSlot self, string prefix, Vector3 travel, int myDir, out MHEOperatorSlot waitFor)
        {
            waitFor = null;
            if (self == null || string.IsNullOrEmpty(prefix)) return true;
            float now = Time.unscaledTime;
            bool isJack = self.IsPalletJack;

            // Who is in there, and which way are they going?
            MHEOperatorSlot against = null;
            foreach (var m in Machines)
            {
                if (m == null || m == self || !m.IsOccupied) continue;
                bool granted = s_entries.TryGetValue(m, out var e) && e.prefix == prefix && e.until >= now;
                bool inside = OrderSelectionTaskDriver.TryFindAisleAt(OutlineCentre(m), 0.5f, out var a) && a.Prefix == prefix;
                if (!granted && !inside) continue;
                int od = inside && IsMoving(m) ? OccupantDir(m, travel) : granted ? e.dir : 0;
                if (od == 0 || myDir == 0 || od == myDir) continue;
                // It waits on me. A JACK asking goes (me waiting too would be a deadlock). A reach truck / dock stocker asking does NOT drive
                // into it (block 65: that sent a truck nose to nose with a jack driving out); it backs off instead (ReachAisleEntryGate).
                if (self.IsPalletJack && IsHeldBy(m, self)) continue;
                against = m;
                break;
            }
            if (against != null)
            {
                if (!isJack && myDir < 0) s_againstWaiting[prefix] = (self, now);
                waitFor = against;
                return false;
            }

            // A reach truck / dock stocker queued to go in against the traffic goes first.
            if (isJack && s_againstWaiting.TryGetValue(prefix, out var w) && w.who != null && w.who != self && now - w.at <= AgainstWaitFreshReal
                && !IsHeldBy(w.who, self))
            {
                waitFor = w.who;
                return false;
            }

            if (s_againstWaiting.TryGetValue(prefix, out var mine) && mine.who == self) s_againstWaiting.Remove(prefix);
            s_entries[self] = (prefix, myDir, now + TokenGrantReal);
            return true;
        }

        /// <summary>Forget a grant (the machine gave up on the aisle, or went in anyway after the cap).</summary>
        public static void ReleaseAisleEntry(MHEOperatorSlot self)
        {
            if (self == null) return;
            s_entries.Remove(self);
            string drop = null;
            foreach (var kv in s_againstWaiting) if (kv.Value.who == self) { drop = kv.Key; break; }
            if (drop != null) s_againstWaiting.Remove(drop);
        }

        private static Vector3 OutlineCentre(MHEOperatorSlot m)
        {
            MachineClearance.WorldCorners(m, s_corners);
            return (s_corners[0] + s_corners[1] + s_corners[2] + s_corners[3]) * 0.25f;
        }

        private static bool IsHeldBy(MHEOperatorSlot m, MHEOperatorSlot by)
        {
            var ta = m != null ? m.GetComponent<EquipmentTrafficAgent>() : null;
            return ta != null && ta.BlockedBy == by;
        }

        /// <summary>A step from <paramref name="p"/> along <paramref name="dir"/> that enters an aisle through one of its ends from outside.</summary>
        internal static bool IsAisleEntryStep(Vector3 p, Vector3 dir, float look, out OrderSelectionTaskDriver.AisleLane lane)
        {
            lane = default;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return false;
            dir.Normalize();
            if (OrderSelectionTaskDriver.TryFindAisleAt(p, 0f, out _)) return false;
            if (!OrderSelectionTaskDriver.TryFindAisleAt(p + dir * look, 0f, out lane)) return false;
            return Mathf.Abs(Vector3.Dot(dir, lane.Travel)) >= 0.5f;
        }

        public static IReadOnlyList<MHEOperatorSlot> Machines
        {
            get
            {
                float age = Time.unscaledTime - s_machinesAt;
                if (age < 0f || age > 1f)
                {
                    s_machinesAt = Time.unscaledTime;
                    s_machines.Clear();
                    foreach (var s in Object.FindObjectsByType<MHEOperatorSlot>())
                        if (s != null && s.isActiveAndEnabled && s.GetComponent<PlacedObject>() != null) s_machines.Add(s);
                }
                return s_machines;
            }
        }

        public static bool IsReachTruck(MHEOperatorSlot m) => m != null && m.GetComponent<ReachTruckOperator>() != null;

        /// <summary>Has this machine moved in the last ~0.4 real seconds? (Sampled on demand; the first look says "not moving".)</summary>
        public static bool IsMoving(MHEOperatorSlot m)
        {
            if (m == null) return false;
            float now = Time.unscaledTime;
            Vector3 p = m.transform.position;
            if (!s_motion.TryGetValue(m, out var s) || now < s.t) { s_motion[m] = (p, now, false); return false; }
            if (now - s.t < MovingSampleReal) return s.moving;
            Vector3 d = p - s.p; d.y = 0f;
            bool mv = d.magnitude > MovingMinStep;
            s_motion[m] = (p, now, mv);
            if (mv) s_motionDir[m] = d;
            return mv;
        }

        // ── Rule 3: stop and look before pulling out of an aisle ──────────────────────────────────────────

        public static void BeginExitWait(MHEOperatorSlot self) { if (self != null) s_exitWaitSince[self] = Time.unscaledTime; }
        public static void EndExitWait(MHEOperatorSlot self) { if (self != null) s_exitWaitSince.Remove(self); }

        private static bool ExitWaitingSince(MHEOperatorSlot m, out float since)
        {
            if (m != null && s_exitWaitSince.TryGetValue(m, out since))
            {
                float age = Time.unscaledTime - since;
                if (age >= 0f && age <= ExitWaitMaxReal + 5f) return true;   // older = left over from a stopped coroutine
            }
            since = 0f;
            return false;
        }

        /// <summary>Another manned machine is moving through the area in front of this aisle end (<paramref name="zone"/>). Machines
        /// following <paramref name="self"/> out of its own aisle are not cross traffic. A machine that started waiting at an aisle end
        /// AFTER self did gives way to self (first come, first out), so two waiting vehicles never hold each other.</summary>
        public static bool ExitBlocked(MHEOperatorSlot self, Vector3 zone, string ownAislePrefix, out string who)
        {
            who = null;
            if (self == null) return false;
            ExitWaitingSince(self, out float mySince);
            foreach (var m in Machines)
            {
                if (m == null || m == self || !m.IsOccupied) continue;
                Vector3 c = m.transform.position;
                Vector3 d = c - zone; d.y = 0f;
                if (d.magnitude > ExitClearRadius) continue;
                if (ownAislePrefix != null && OrderSelectionTaskDriver.TryFindAisleAt(c, 0f, out var a) && a.Prefix == ownAislePrefix) continue;
                if (!IsMoving(m)) continue;
                if (ExitWaitingSince(m, out float since) && since > mySince) continue;
                who = m.name;
                return true;
            }
            return false;
        }

        // ── Rule 5: reach trucks pass pallet jacks ─────────────────────────────────────────────────────────

        /// <summary>Along / lateral extent of a machine's outline in an aisle's frame.</summary>
        internal static void Span(OrderSelectionTaskDriver.AisleLane lane, MHEOperatorSlot m, out float rear, out float front, out float latMin, out float latMax)
        {
            MachineClearance.WorldCorners(m, s_corners);
            rear = latMin = float.MaxValue; front = latMax = float.MinValue;
            foreach (var c in s_corners)
            {
                float a = lane.Along(c), l = lane.Lateral(c);
                rear = Mathf.Min(rear, a); front = Mathf.Max(front, a);
                latMin = Mathf.Min(latMin, l); latMax = Mathf.Max(latMax, l);
            }
        }

        /// <summary>The passer's root drives this far to the side of the passed vehicle's root (Tad, 2026-11-14).</summary>
        public const float PassOffsetFromVehicle = 1.5f;
        private const float PassRoomProbe = 4f;   // how far to each side of the passed vehicle the open space is measured

        /// <summary>PASSING, ANYWHERE (Tad, 2026-11-14): "1.5 from the center of the VEHICLE we are passing, on the side of the vehicle that
        /// has the most space, then we turn off rules or other avoidance and just pass through." Nothing here depends on aisles, so it works on
        /// any map: the side with more open floor (racks / walls / solids only; machines don't count) is chosen, the passer's root drives the
        /// line PassOffsetFromVehicle from the passed vehicle's root on that side, from where it swings over (at most PassAngleDeg) to a
        /// passer length past the vehicle's far end (or the destination, if that comes first). In an aisle the pass runs parallel to it.
        /// The caller drives this route with every clearance / traffic check OFF (and the passed vehicle ignored).</summary>
        public static bool PlanPassAround(MHEOperatorSlot passer, MHEOperatorSlot blocker, Vector3 dest, out List<Vector3> route)
        {
            route = null;
            if (passer == null || blocker == null) return false;
            Vector3 pc = passer.transform.position, bc = blocker.transform.position;
            Vector3 f = bc - pc; f.y = 0f;
            if (f.sqrMagnitude < 0.01f) { f = passer.transform.forward; f.y = 0f; }
            f.Normalize();
            // In an aisle, pass parallel to it (whichever way along it the blocker lies).
            if (OrderSelectionTaskDriver.TryFindAisleAt(bc, 1.5f, out var lane))
                f = Vector3.Dot(lane.Travel, f) >= 0f ? lane.Travel : -lane.Travel;
            Vector3 right = Vector3.Cross(Vector3.up, f);

            float side = FreeRoom(bc, right) >= FreeRoom(bc, -right) ? 1f : -1f;
            Vector3 offset = right * side * PassOffsetFromVehicle;

            // The passed vehicle's far end along the pass, and the passer's length.
            MachineClearance.WorldCorners(blocker, s_corners);
            float bFar = float.MinValue;
            foreach (var c in s_corners) bFar = Mathf.Max(bFar, Vector3.Dot(c - bc, f));
            var pf = MachineClearance.FootprintOf(passer);
            float pLen = pf.MaxZ - pf.MinZ;

            float pAlong = Vector3.Dot(pc - bc, f);
            float pLat = Vector3.Dot(pc - bc, right * side);
            float run = Mathf.Max(1.5f, Mathf.Abs(PassOffsetFromVehicle - pLat) / Mathf.Tan(PassAngleDeg * Mathf.Deg2Rad));
            float a1 = pAlong + run;                       // on the pass line
            // Clear past it - and past any vehicle lined up right after it on the same line (a jack pulling back in behind the first one
            // drove straight into the second: one pass for the whole queue).
            for (int g = 0; g < 6; g++)
            {
                bool grew = false;
                foreach (var o in Machines)
                {
                    if (o == null || o == passer || o == blocker) continue;
                    Vector3 oc = o.transform.position - bc;
                    if (Mathf.Abs(Vector3.Dot(oc, right)) > 1.0f) continue;          // not on the passed vehicle's line
                    if (Vector3.Dot(oc, f) <= 0f) continue;                         // behind it
                    // Its NEAREST end (a jack's forks trail 2.8 m behind its root): a gap shorter than a passer length + margin is no room
                    // to pull back in, so it is part of the same queue.
                    MachineClearance.WorldCorners(o, s_corners);
                    float oNear = float.MaxValue, oFar = float.MinValue;
                    foreach (var c in s_corners) { float a = Vector3.Dot(c - bc, f); oNear = Mathf.Min(oNear, a); oFar = Mathf.Max(oFar, a); }
                    if (oNear > bFar + pLen + 1.5f) continue;                       // a clear gap: pull back in before it
                    if (oFar > bFar) { bFar = oFar; grew = true; }
                }
                if (!grew) break;
            }
            float a2 = bFar + pLen + 0.5f;                 // clear past it (them)
            float dAlong = Vector3.Dot(dest - bc, f);
            if (dAlong > pAlong && dAlong < a2) a2 = Mathf.Max(dAlong, a1);   // the destination comes first: pass only that far
            float y = pc.y;
            Vector3 P(float along) { Vector3 p = bc + f * along + offset; p.y = y; return p; }
            route = new List<Vector3>();
            if (a2 - a1 > 0.3f) route.Add(P(a1));
            route.Add(P(a2));
            return true;
        }

        /// <summary>Open floor from <paramref name="from"/> in <paramref name="dir"/> before the first solid (rack, wall, ...), up to PassRoomProbe.</summary>
        private static float FreeRoom(Vector3 from, Vector3 dir)
        {
            for (float d = 0.5f; d <= PassRoomProbe; d += 0.25f)
                if (MachineClearance.SolidAt(from + dir * d, out _)) return d;
            return PassRoomProbe;
        }

        /// <summary>Reach trucks: the same pass (kept as the old entry point). <paramref name="why"/> is always null now: nothing refuses.</summary>
        public static bool TryPlanReachPass(MHEOperatorSlot rt, MHEOperatorSlot blocker, Vector3 dest, out List<Vector3> route, out string why)
        {
            why = null;
            return PlanPassAround(rt, blocker, dest, out route);
        }

        private static Vector3 FootprintCentre(MHEOperatorSlot m)
        {
            var f = MachineClearance.FootprintOf(m);
            return m.transform.TransformPoint(new Vector3((f.MinX + f.MaxX) * 0.5f, 0f, (f.MinZ + f.MaxZ) * 0.5f));
        }
    }
}
