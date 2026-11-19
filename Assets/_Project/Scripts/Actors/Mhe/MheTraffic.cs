using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace GameCore.Actors
{
    /// <summary>An oriented rectangle on the floor plane (x/z). Fwd/Right are unit vectors; HalfF/HalfR are the half lengths along them.</summary>
    public struct MheObb
    {
        public Vector2 C, Fwd, Right;
        public float HalfF, HalfR;

        public static MheObb From(Vector3 centre, Vector3 forward, float halfF, float halfR)
        {
            var f = new Vector2(forward.x, forward.z);
            if (f.sqrMagnitude < 1e-6f) f = Vector2.up;
            f.Normalize();
            return new MheObb { C = new Vector2(centre.x, centre.z), Fwd = f, Right = new Vector2(f.y, -f.x), HalfF = halfF, HalfR = halfR };
        }

        public MheObb Inflated(float d) { var o = this; o.HalfF = Mathf.Max(0.01f, HalfF + d); o.HalfR = Mathf.Max(0.01f, HalfR + d); return o; }

        /// <summary>Moved <paramref name="along"/> metres along <paramref name="forward"/>.</summary>
        public MheObb Shifted(Vector3 forward, float along)
        {
            if (Mathf.Abs(along) < 1e-5f) return this;
            var o = this;
            o.C += Fwd * along;
            return o;
        }

        private float Project(Vector2 axis) => HalfF * Mathf.Abs(Vector2.Dot(Fwd, axis)) + HalfR * Mathf.Abs(Vector2.Dot(Right, axis));

        public bool Overlaps(in MheObb o)
        {
            Vector2 d = o.C - C;
            if (Mathf.Abs(Vector2.Dot(d, Fwd)) > HalfF + o.Project(Fwd)) return false;
            if (Mathf.Abs(Vector2.Dot(d, Right)) > HalfR + o.Project(Right)) return false;
            if (Mathf.Abs(Vector2.Dot(d, o.Fwd)) > o.HalfF + Project(o.Fwd)) return false;
            if (Mathf.Abs(Vector2.Dot(d, o.Right)) > o.HalfR + Project(o.Right)) return false;
            return true;
        }

        /// <summary>Edge-to-edge distance to another rectangle (0 when they touch or overlap).</summary>
        public float GapTo(in MheObb o)
        {
            if (Overlaps(o)) return 0f;
            float best = float.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                Vector3 ca = Corner(i, 0f), cb = o.Corner(i, 0f);
                best = Mathf.Min(best, o.DistanceTo(new Vector2(ca.x, ca.z)));
                best = Mathf.Min(best, DistanceTo(new Vector2(cb.x, cb.z)));
            }
            return best;
        }

        /// <summary>Distance from a floor point to this rectangle (0 inside).</summary>
        public float DistanceTo(Vector2 p)
        {
            Vector2 d = p - C;
            float f = Mathf.Max(0f, Mathf.Abs(Vector2.Dot(d, Fwd)) - HalfF);
            float r = Mathf.Max(0f, Mathf.Abs(Vector2.Dot(d, Right)) - HalfR);
            return Mathf.Sqrt(f * f + r * r);
        }

        public Vector3 Corner(int i, float y)
        {
            float sf = (i == 0 || i == 1) ? 1f : -1f, sr = (i == 0 || i == 3) ? 1f : -1f;
            Vector2 p = C + Fwd * (HalfF * sf) + Right * (HalfR * sr);
            return new Vector3(p.x, y, p.y);
        }
    }

    /// <summary>NavMesh queries for machines (MHE agent type). Open-floor routes never use the rack aisles (area <see cref="AisleArea"/>):
    /// inside an aisle a machine follows the aisle's own lane (MhePlanner), entering and leaving only by its ends.</summary>
    public static class MheNav
    {
        public const int AisleArea = 8;                // ProjectSettings/NavMeshAreas: "Rack Aisle"
        public const float AgentRadius = 0.5f;          // the MHE agent type's baked radius
        private static int s_agentType = int.MinValue;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_agentType = int.MinValue;

        public static int AgentTypeId
        {
            get
            {
                if (s_agentType != int.MinValue) return s_agentType;
                for (int i = 0; i < NavMesh.GetSettingsCount(); i++)
                {
                    var s = NavMesh.GetSettingsByIndex(i);
                    if (NavMesh.GetSettingsNameFromID(s.agentTypeID) == "MHE") { s_agentType = s.agentTypeID; return s_agentType; }
                }
                s_agentType = 0;
                return s_agentType;
            }
        }

        /// <summary>Machine floor, aisles and the dock apron excluded (machines never route through either on open-floor legs).</summary>
        public static NavMeshQueryFilter OpenFloorFilter(bool allowApron = false)
        {
            int mask = NavMesh.AllAreas & ~(1 << AisleArea) & ~(1 << 1);
            if (!allowApron) mask &= ~(1 << DockApron.NavArea);
            var f = new NavMeshQueryFilter { agentTypeID = AgentTypeId, areaMask = mask };
            f.SetAreaCost(AiNavigation.StagingLaneArea, 6f);
            return f;
        }

        public static NavMeshQueryFilter AnyFloorFilter() => new NavMeshQueryFilter { agentTypeID = AgentTypeId, areaMask = NavMesh.AllAreas & ~(1 << 1) };

        public static bool Sample(Vector3 p, float radius, out Vector3 hit, bool openFloorOnly = true)
        {
            var f = openFloorOnly ? OpenFloorFilter(true) : AnyFloorFilter();
            if (NavMesh.SamplePosition(p, out var h, radius, f) && h.position.y > 0.9f) { hit = h.position; return true; }
            hit = p;
            return false;
        }

        /// <summary>Raycast along the machine floor; true + distance when it hits the mesh edge before <paramref name="to"/>.</summary>
        public static bool RaycastFloor(Vector3 from, Vector3 to, out float dist)
        {
            dist = 0f;
            var f = AnyFloorFilter();
            if (!NavMesh.SamplePosition(from, out var s, 1.0f, f)) return false;
            Vector3 t = to; t.y = s.position.y;
            if (NavMesh.Raycast(s.position, t, out var hit, f)) { dist = hit.distance; return true; }
            return false;
        }

        public static bool SegmentClear(Vector3 a, Vector3 b, NavMeshQueryFilter f)
        {
            if (!NavMesh.SamplePosition(a, out var s, 0.6f, f)) return false;
            Vector3 t = b; t.y = s.position.y;
            return !NavMesh.Raycast(s.position, t, out _, f);
        }

        public static float EdgeDistance(Vector3 p, NavMeshQueryFilter f, out Vector3 normal)
        {
            normal = Vector3.zero;
            if (!NavMesh.SamplePosition(p, out var s, 0.6f, f)) return -1f;
            if (!NavMesh.FindClosestEdge(s.position, out var e, f)) return 10f;
            normal = e.normal; normal.y = 0f;
            return e.distance;
        }
    }

    /// <summary>
    /// Shared state of every motor-driven machine (MHE traffic rework, 2026-11-18): the registry, the aisle entry rule (one direction of
    /// traffic at a time in an aisle, reach trucks first), and the people a machine must keep clear of.
    /// </summary>
    public static class MheTraffic
    {
        public const float Gap = 0.25f;           // every machine keeps this far from every other machine (Tad, 2026-11-18)
        public const float Headway = 0.6f;         // extra room kept behind / beside a machine that is driving (not standing)
        public const float PersonGap = 0.2f;       // ...and this far from a person on foot
        public const float PersonRadius = 0.28f;
        public const float AisleMaxDeg = 30f;      // a pallet jack inside an aisle is never more than this off the aisle line

        private static readonly List<MheMotor> s_motors = new();
        public static IReadOnlyList<MheMotor> Motors => s_motors;

        private static readonly List<Vector3> s_people = new();
        private static float s_peopleAt = -10f;

        // Aisle entry grants: who is (or is about to be) in which aisle, going which way.
        private struct Grant { public string Prefix; public int Dir; public float Until; }
        private static readonly Dictionary<MheMotor, Grant> s_grants = new();
        private static readonly Dictionary<string, (MheMotor who, float at)> s_againstWaiting = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { s_motors.Clear(); s_people.Clear(); s_peopleAt = -10f; s_grants.Clear(); s_againstWaiting.Clear(); s_spots.Clear(); }

        internal static void Register(MheMotor m) { if (m != null && !s_motors.Contains(m)) s_motors.Add(m); }
        internal static void Unregister(MheMotor m) { s_motors.Remove(m); s_grants.Remove(m); s_spots.Remove(m); }

        public static MheMotor MotorOf(MHEOperatorSlot s) => s != null ? s.GetComponent<MheMotor>() : null;

        /// <summary>People on foot (not riding a machine), refreshed every 0.25 real seconds.</summary>
        public static List<Vector3> People
        {
            get
            {
                float now = Time.unscaledTime;
                if (now - s_peopleAt < 0.25f && now >= s_peopleAt) return s_people;
                s_peopleAt = now;
                s_people.Clear();
                foreach (var e in Object.FindObjectsByType<EmployeeIdentity>())
                {
                    if (e == null || !e.isActiveAndEnabled) continue;
                    if (e.GetComponentInParent<MHEOperatorSlot>() != null) continue;
                    s_people.Add(e.transform.position);
                }
                return s_people;
            }
        }

        // ── Aisle entry ─────────────────────────────────────────────────────────────────────────────
        /// <summary>May <paramref name="self"/> drive into <paramref name="aisle"/> going <paramref name="dir"/> (+1 = with the chevron)?
        /// Rule: an aisle carries one direction of moving traffic at a time. Pallet jacks only ever go +1. A reach truck (or anything)
        /// going -1 waits while anything inside is DRIVING +1; while it waits, new jacks wait at the mouth so the aisle drains (reach trucks
        /// first). Jacks going +1 wait while a machine is inside (or granted) going -1. Standing / working machines never block entry: the
        /// sensors and the lane planner handle them. The caller re-asks every frame while it waits.</summary>
        public static bool MayEnterAisle(MheMotor self, MheAisle aisle, int dir, out MheMotor waitFor)
        {
            waitFor = null;
            if (self == null || aisle == null || dir == 0) return true;
            float now = Time.unscaledTime;
            foreach (var m in s_motors)
            {
                if (m == null || m == self || !m.isActiveAndEnabled) continue;
                bool granted = s_grants.TryGetValue(m, out var g) && g.Prefix == aisle.Prefix && g.Until >= now;
                bool inside = aisle.Contains(m.Centre, 0.3f, 0.2f);
                if (!granted && !inside) continue;
                int theirDir = granted ? g.Dir : 0;
                if (inside && m.IsMovingNow)
                {
                    Vector3 v = m.MoveDir;
                    float d = Vector3.Dot(v, aisle.Travel);
                    theirDir = d > 0.5f ? 1 : d < -0.5f ? -1 : 0;
                }
                if (theirDir == 0 || theirDir == dir) continue;
                // It is waiting for me (would deadlock): the lower priority one gives way, so if I outrank it I go.
                if (m.WaitingForAisleOf == self && self.Priority > m.Priority) continue;
                waitFor = m;
                if (dir < 0) s_againstWaiting[aisle.Prefix] = (self, now);
                return false;
            }
            if (dir > 0 && self.Kind == MheKind.Jack && s_againstWaiting.TryGetValue(aisle.Prefix, out var w) && w.who != null && w.who != self
                && now - w.at < 1.0f && w.who.Priority > self.Priority)
            {
                waitFor = w.who;
                return false;
            }
            if (s_againstWaiting.TryGetValue(aisle.Prefix, out var mine) && mine.who == self) s_againstWaiting.Remove(aisle.Prefix);
            s_grants[self] = new Grant { Prefix = aisle.Prefix, Dir = dir, Until = now + 20f };
            return true;
        }

        /// <summary>Keeps a grant alive while the machine is in the aisle; dropped once it is out.</summary>
        internal static void RefreshGrant(MheMotor self)
        {
            if (self == null || !s_grants.TryGetValue(self, out var g)) return;
            var a = MheAisleMap.ByPrefix(g.Prefix);
            if (a == null) { s_grants.Remove(self); return; }
            if (a.Contains(self.Centre, 0.5f, 0.3f)) { g.Until = Time.unscaledTime + 3f; s_grants[self] = g; }
            else if (Time.unscaledTime > g.Until) s_grants.Remove(self);
        }

        internal static void DropGrant(MheMotor self)
        {
            if (self == null) return;
            s_grants.Remove(self);
            string drop = null;
            foreach (var kv in s_againstWaiting) if (kv.Value.who == self) { drop = kv.Key; break; }
            if (drop != null) s_againstWaiting.Remove(drop);
        }

        // ── Work spots ──────────────────────────────────────────────────────────────────────────────
        // A machine's destination (a lane mouth, a pivot point, a pick stop, a rack face) is its own until it sets off somewhere else:
        // it often keeps working round it after its leg ends (a reach truck backing into the lane and out again, a jack reversing into a
        // staging lane). Another machine whose destination is within reach of it waits at a holding point a little way back instead of
        // driving into the middle of that work. This is the queue at the dock and the lane mouths.
        private static readonly Dictionary<MheMotor, (Vector3 c, string label, float at)> s_spots = new();

        public static void ReserveSpot(MheMotor m, Vector3 centre, string label) { if (m != null) s_spots[m] = (centre, label, Time.unscaledTime); }
        public static void ReleaseSpot(MheMotor m) { if (m != null) s_spots.Remove(m); }

        /// <summary>Spacing two machines' work spots need: both bodies, end to end, plus room to manoeuvre.</summary>
        public static float SpotSpacing(MheMotor a, MheMotor b) => a.HalfLength + b.HalfLength + 0.9f;

        public static bool SpotTaken(MheMotor self, Vector3 centre, out MheMotor who, out Vector3 theirs, out string label)
        {
            who = null; theirs = default; label = null;
            float best = float.MaxValue;
            foreach (var kv in s_spots)
            {
                var o = kv.Key;
                if (o == null || o == self || !o.isActiveAndEnabled) continue;
                Vector3 d = kv.Value.c - centre; d.y = 0f;
                float dist = d.magnitude;
                if (dist >= SpotSpacing(self, o) || dist >= best) continue;
                best = dist; who = o; theirs = kv.Value.c; label = kv.Value.label;
            }
            return who != null;
        }

        /// <summary>Another machine's work spot clashes with <paramref name="mySpot"/> AND it is theirs: they are already at it (within
        /// 1.5 m), or nearer their spot than <paramref name="self"/> is to its own, or (equal) they claimed it first. Then <paramref name="self"/>
        /// keeps <paramref name="keepAway"/> from their spot until they set off.</summary>
        public static bool SpotContest(MheMotor self, Vector3 mySpot, out MheMotor owner, out Vector3 theirs, out float keepAway, out string label)
        {
            owner = null; theirs = default; keepAway = 0f; label = null;
            if (self == null) return false;
            if (!s_spots.TryGetValue(self, out var mine)) return false;
            float myDist = Flat(self.Centre - mySpot).magnitude;
            foreach (var kv in s_spots)
            {
                var o = kv.Key;
                if (o == null || o == self || !o.isActiveAndEnabled) continue;
                float spacing = SpotSpacing(self, o);
                if (Flat(kv.Value.c - mySpot).magnitude >= spacing) continue;
                float theirDist = Flat(o.Centre - kv.Value.c).magnitude;
                bool theirs_ = false;
                if (myDist < 1.5f && theirDist >= 1.5f) theirs_ = false;
                else if (theirDist < 1.5f && myDist >= 1.5f) theirs_ = true;
                else if (Mathf.Abs(theirDist - myDist) > 0.5f) theirs_ = theirDist < myDist;
                else theirs_ = kv.Value.at < mine.at;
                if (!theirs_) continue;
                owner = o; theirs = kv.Value.c; label = kv.Value.label;
                keepAway = spacing + 0.8f;
                return true;
            }
            return false;
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        /// <summary>Machines working at their spot right now (within 1 m of it: a jack turning at a staging pivot, a reach truck at a lane
        /// mouth): the spot centre and the radius their body sweeps when they turn there. Others keep out of that circle.</summary>
        public static void WorkingSpots(List<(MheMotor who, Vector3 c, float r)> into)
        {
            into.Clear();
            foreach (var kv in s_spots)
            {
                var o = kv.Key;
                if (o == null || !o.isActiveAndEnabled) continue;
                if (!o.IsDriving) continue;   // a parked machine (picking) is passed by the normal gap rules, not kept clear of
                if (Flat(o.Centre - kv.Value.c).magnitude > 1.0f) continue;
                float r = Mathf.Sqrt(o.HalfLength * o.HalfLength + o.HalfWidth * o.HalfWidth) + Gap;
                into.Add((o, kv.Value.c, r));
            }
        }

        /// <summary>Drops a spot its machine has abandoned (left unmanned and standing).</summary>
        internal static void RefreshSpot(MheMotor m)
        {
            if (m == null || !s_spots.TryGetValue(m, out var s)) return;
            if (m.IsDriving) return;
            bool manned = m.Slot != null && m.Slot.IsOccupied;
            if (!manned && m.StoppedFor > 20f) s_spots.Remove(m);
        }

        /// <summary>How many manned pallet jacks are in aisle <paramref name="aisle"/> right now.</summary>
        public static int JacksIn(MheAisle aisle)
        {
            int n = 0;
            foreach (var m in s_motors)
                if (m != null && m.Kind == MheKind.Jack && m.Slot != null && m.Slot.IsOccupied && aisle.Contains(m.Centre, 1f, 0.2f)) n++;
            return n;
        }
    }
}
