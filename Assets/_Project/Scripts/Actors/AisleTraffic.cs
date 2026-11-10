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
        public const float ReachTruckBuffer = 1.0f;    // rule 1: jacks never come closer than this to a manned reach truck
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
        private static void ResetStatics() { s_machines.Clear(); s_machinesAt = -10f; s_motion.Clear(); s_exitWaitSince.Clear(); }

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

        /// <summary>Waypoints for reach truck <paramref name="rt"/> to drive round pallet jack <paramref name="jack"/> in an aisle, on the side
        /// with more room, ending clear past it. False (with <paramref name="why"/> for the log) when rule 5 says wait: another manned jack
        /// within 2 bays of this one, no room beside it, or the truck's stop is beside the jack anyway. <paramref name="why"/> is null when
        /// the jack is not in an aisle (open floor: NavMesh avoidance handles it).</summary>
        public static bool TryPlanReachPass(MHEOperatorSlot rt, MHEOperatorSlot jack, Vector3 dest, out List<Vector3> route, out string why)
        {
            route = null; why = null;
            if (rt == null || jack == null) return false;
            Vector3 jc = jack.transform.position - jack.transform.forward * 1.0f;
            if (!OrderSelectionTaskDriver.TryFindAisleAt(jc, 0.5f, out var lane)) return false;

            float rtAlong = lane.Along(rt.transform.position);
            Span(lane, jack, out float jRear, out float jFront, out float jLatMin, out float jLatMax);
            float jMid = (jRear + jFront) * 0.5f;
            float dir = jMid >= rtAlong ? 1f : -1f;            // which way along the aisle the truck is driving past it
            float pitch = lane.BayPitch > 0.5f ? lane.BayPitch : 2.65f;

            // Rule 5: another jack within 2 bays (front or rear) of the one ahead -> wait for safety.
            float guard = ReachPassJackGapBays * pitch;
            foreach (var o in Machines)
            {
                if (o == null || o == jack || o == rt || !o.IsPalletJack || !o.IsOccupied) continue;
                Vector3 oc = o.transform.position - o.transform.forward * 1.0f;
                if (!OrderSelectionTaskDriver.TryFindAisleAt(oc, 1.5f, out var oa) || oa.Prefix != lane.Prefix) continue;
                Span(lane, o, out float oRear, out float oFront, out _, out _);
                if (oFront >= jRear - guard && oRear <= jFront + guard)
                {
                    why = $"another pallet jack ({o.name}) is within {ReachPassJackGapBays:0} bays of {jack.name} - waiting, not passing";
                    return false;
                }
            }

            // Room beside it: the aisle's clear band runs between the rack faces (slot centre lines minus the rack's half depth).
            var rf = MachineClearance.FootprintOf(rt);
            float rtHalf = Mathf.Max(-rf.MinX, rf.MaxX);
            float rtLen = rf.MaxZ - rf.MinZ;
            float band = lane.Half - 0.75f;
            const float SideGap = 0.25f;
            float roomLeft = jLatMin - (-band), roomRight = band - jLatMax;
            float need = 2f * rtHalf + SideGap;
            bool goLeft = roomLeft >= roomRight;
            float room = goLeft ? roomLeft : roomRight;
            if (room < need)
            {
                why = $"no room beside {jack.name} to pass ({room:0.00}m, needs {need:0.00}m) - waiting";
                return false;
            }
            float lat = goLeft ? jLatMin - SideGap - rtHalf : jLatMax + SideGap + rtHalf;

            // Along: out to the passing line before reaching the jack, past it by a truck length, then the NavMesh takes over.
            float near = dir > 0f ? jRear : jFront, far = dir > 0f ? jFront : jRear;
            float sDest = dir * lane.Along(dest), sFar = dir * far;
            if (sDest < sFar + 1f)
            {
                why = $"its stop is beside {jack.name} - waiting for the jack to move";
                return false;
            }
            // The lane change is finished (at no more than PassAngle off the aisle) by the time the truck's nose reaches the jack. Held
            // right behind it, there isn't that much room: back straight up first (reach trucks may drive either way).
            Span(lane, rt, out float rRear, out float rFront, out _, out _);
            float nose = dir > 0f ? rFront : rRear;
            float gap = dir * (near - nose) - 0.3f;
            float curLat = lane.Lateral(rt.transform.position);
            float runNeeded = Mathf.Abs(lat - curLat) / Mathf.Tan(PassAngleDeg * Mathf.Deg2Rad);
            float backUp = Mathf.Max(0f, runNeeded - Mathf.Max(0f, gap));
            if (backUp > MaxBackUp)
            {
                why = $"too close behind {jack.name} to pull out and no room to back up - waiting";
                return false;
            }
            float outAt = rtAlong + dir * Mathf.Max(0f, gap);
            float endAt = far + dir * (rtLen + 0.5f);
            if (dir * endAt > sDest) endAt = dir * sDest;
            float y = rt.transform.position.y;
            Vector3 P(float along, float l) { Vector3 p = lane.Centre + lane.Travel * along + lane.Right * l; p.y = y; return p; }
            route = new List<Vector3>();
            if (backUp > 0.05f) route.Add(P(rtAlong - dir * backUp, curLat));
            route.Add(P(outAt, lat));
            route.Add(P(endAt, lat));
            return true;
        }
    }
}
