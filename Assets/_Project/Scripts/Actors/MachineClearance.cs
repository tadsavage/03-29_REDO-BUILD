using UnityEngine;
using System.Collections.Generic;
using GameCore.Inventory;

namespace GameCore.Actors
{
    /// <summary>
    /// NEVER DRIVE THROUGH ANYTHING (Tad, 2026-11-07: "racks, equipment, walls, pallets - they shouldn't be driving through
    /// anything just because there's not a NavMesh").
    ///
    /// A NavMesh only says where a machine's CENTRE may go. The body and forks are not on it, the aisles have no MHE NavMesh at all
    /// (jacks are scripted in there), and scripted moves (turning in place, the last metre onto a stop, backing into a lane, the
    /// AiNavigation walk-out) ignored geometry entirely. This is the one footprint test every machine move should go through:
    /// the machine's real outline (body from its renderers, plus a pallet jack's fork load) against
    ///   - racks, walls, barriers, fences and furniture: solid placed objects in the PlacementGrid, tested against their real
    ///     outline inside their cell (rack colliders are only click triggers on the beams, so physics can't see racks);
    ///   - every other machine (RT / DS / pallet jack, manned or not);
    ///   - every pallet standing on a floor.
    /// Moving machines also keep a one-metre footprint gap from every other vehicle; an existing close gap can only be reduced by
    /// moving away from the other vehicle.
    /// A move is refused only if it pushes the outline into something it isn't already in, so a machine that was restored
    /// overlapping something, or that just set its own pallets down, can always back away.
    /// </summary>
    public static class MachineClearance
    {
        public struct Footprint { public float MinX, MaxX, MinZ, MaxZ; }

        public const float Skin = 0.10f;               // the outline is shrunk by this so grazing a neighbour lane's pallet doesn't count
        public const float VehicleSpacingMeters = 1f;
        // A pallet jack's fork load: both pallet positions (OrderSelectionTaskDriver.ForkPalletZ -2.00 / -0.95, +/- half a 40"
        // pallet), 48" across. Always part of the outline - the forks are there whether or not they carry anything.
        private const float ForkLoadMinZ = -2.51f, ForkLoadMaxZ = -0.44f, ForkLoadHalfX = 0.61f;
        private const float PalletHalf = 0.6f;

        private static readonly Dictionary<MHEOperatorSlot, Footprint> s_footprints = new();
        private static readonly Dictionary<MHEOperatorSlot, Vector3[]> s_samplePoints = new();
        private static readonly Dictionary<GameObject, (Bounds b, float at)> s_solidOutlines = new();
        private static readonly List<MHEOperatorSlot> s_allMachines = new();
        private static readonly List<Transform> s_floorPallets = new();
        private static float s_allMachinesAt = -10f, s_floorPalletsAt = -10f;
        private static PlacementGrid s_grid;

        /// <summary>Machines and floor pallets near the last <see cref="GatherNear"/> point.</summary>
        public static readonly List<MHEOperatorSlot> NearMachines = new();
        public static readonly List<Transform> NearPallets = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_footprints.Clear(); s_samplePoints.Clear(); s_solidOutlines.Clear(); s_allMachines.Clear(); s_floorPallets.Clear();
            NearMachines.Clear(); NearPallets.Clear();
            s_allMachinesAt = -10f; s_floorPalletsAt = -10f; s_grid = null;
        }

        // ── Outline ─────────────────────────────────────────────────────────────────────────────────

        /// <summary>The machine's outline in its own local space (x across, z along), from its renderers. Pallets, riders and
        /// labels are left out; a pallet jack's fork load zone is always added.</summary>
        public static Footprint FootprintOf(MHEOperatorSlot m)
        {
            if (m == null) return default;
            if (s_footprints.TryGetValue(m, out var cached)) return cached;
            Matrix4x4 toLocal = m.transform.worldToLocalMatrix;
            bool any = false;
            float minX = 0f, maxX = 0f, minZ = 0f, maxZ = 0f;
            foreach (var r in m.GetComponentsInChildren<Renderer>())
            {
                if (r == null || !r.enabled || r is ParticleSystemRenderer) continue;
                if (r.GetComponentInParent<OutboundPalletBuilder>() != null || r.GetComponentInParent<PalletData>() != null) continue;
                if (r.GetComponentInParent<EmployeeIdentity>() != null) continue;
                if (r.GetComponent<TMPro.TMP_Text>() != null) continue;
                // Floor shadow decals (VehicleShadow_Wide reaches 3.1 m behind a pallet jack and 1.8 m ahead) are not the machine.
                if (r.name.IndexOf("Shadow", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                Bounds lb = r.localBounds;
                Matrix4x4 toMachine = toLocal * r.localToWorldMatrix;
                Vector3 c = lb.center, e = lb.extents;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                    Vector3 p = toMachine.MultiplyPoint3x4(corner);
                    if (p.y > 2.5f) continue;   // masts, beacons, overhead guards: nothing at body height
                    if (!any) { minX = maxX = p.x; minZ = maxZ = p.z; any = true; }
                    else { minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z); }
                }
            }
            // Anything implausible (a stray world-space child): fall back to the measured pallet jack.
            if (!any || maxX - minX > 2.5f || maxZ - minZ > 6f || maxX - minX < 0.3f || maxZ - minZ < 0.3f)
            { minX = -0.6f; maxX = 0.6f; minZ = -2.55f; maxZ = 0.6f; }
            if (m.IsPalletJack)
            {
                minX = Mathf.Min(minX, -ForkLoadHalfX); maxX = Mathf.Max(maxX, ForkLoadHalfX);
                minZ = Mathf.Min(minZ, ForkLoadMinZ); maxZ = Mathf.Max(maxZ, ForkLoadMaxZ);
            }
            var f = new Footprint { MinX = minX, MaxX = maxX, MinZ = minZ, MaxZ = maxZ };
            s_footprints[m] = f;
            return f;
        }

        /// <summary>Points over the machine's outline (shrunk by Skin), in its local space.</summary>
        private static Vector3[] SamplePointsOf(MHEOperatorSlot m)
        {
            if (s_samplePoints.TryGetValue(m, out var pts)) return pts;
            var f = FootprintOf(m);
            float x0 = f.MinX + Skin, x1 = f.MaxX - Skin, z0 = f.MinZ + Skin, z1 = f.MaxZ - Skin;
            int nx = Mathf.Max(2, Mathf.CeilToInt((x1 - x0) / 0.4f) + 1);
            int nz = Mathf.Max(2, Mathf.CeilToInt((z1 - z0) / 0.4f) + 1);
            var list = new List<Vector3>(nx * nz);
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                    list.Add(new Vector3(Mathf.Lerp(x0, x1, i / (float)(nx - 1)), 0f, Mathf.Lerp(z0, z1, j / (float)(nz - 1))));
            pts = list.ToArray();
            s_samplePoints[m] = pts;
            return pts;
        }

        // ── Solid placed objects ────────────────────────────────────────────────────────────────────

        private static PlacementGrid Grid()
        {
            if (s_grid == null) s_grid = Object.FindAnyObjectByType<PlacementGrid>();
            return s_grid;
        }

        /// <summary>Placed objects a vehicle can never drive through: racks, walls (not doors), barriers, fences, furniture.
        /// Floors, grounds, lane tiles, doors, parking bays, vehicles, staff and waypoints are not solid.</summary>
        public static bool IsSolidData(ObjDataSO d)
        {
            if (d == null || d.isFloor || d.pathfindingClear || d.ignorePlacementRules || d.ClearsGridAfterPlacement || d.wallMounted) return false;
            switch (d.category)
            {
                case "Racking":
                case "Walls":
                case "Barrier":
                    return true;
                case "Loss Prevention":
                    return d.objName != "Truck";
                case "Flavor":
                {
                    string n = (d.objName ?? "").ToLowerInvariant();
                    return !(n.Contains("light") || n.Contains("rat") || n.Contains("betty") || n.Contains("grass") ||
                             n.Contains("reeds") || n.Contains("vines") || n.Contains("stalks"));
                }
                default:
                    return false;
            }
        }

        /// <summary>A solid placed object in this point's grid cell whose real outline (renderer bounds, planar) covers the point.
        /// The cell keeps stray renderers from reaching into the aisle; the outline keeps a thin wall panel from blocking its whole cell.</summary>
        public static bool SolidAt(Vector3 wp, out string what)
        {
            what = null;
            var grid = Grid();
            if (grid == null) return false;
            var list = grid.GetObjectsInCell(grid.WorldToCell(wp));
            if (list == null) return false;
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (e.isBuffer || e.instance == null || !e.instance.activeInHierarchy || !IsSolidData(e.data)) continue;
                if (!InsideSolidOutline(e.instance, wp, grid.CellSize)) continue;
                what = e.data.objName;
                return true;
            }
            return false;
        }

        private static bool InsideSolidOutline(GameObject go, Vector3 wp, float cellSize)
        {
            if (!s_solidOutlines.TryGetValue(go, out var c) || (Time.unscaledTime - c.at > 30f || Time.unscaledTime < c.at))   // re-read now and then: the player can move things
            {
                bool any = false;
                Bounds b = default;
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                {
                    if (r == null || !r.enabled || r is ParticleSystemRenderer) continue;
                    if (r.GetComponent<TMPro.TMP_Text>() != null) continue;
                    if (r.GetComponentInParent<PalletData>() != null) continue;   // stock in a rack is not the rack
                    if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
                }
                if (!any) b = new Bounds(go.transform.position, new Vector3(cellSize, 10f, cellSize));
                c = (b, Time.unscaledTime);
                s_solidOutlines[go] = c;
            }
            Bounds o = c.b;
            return wp.x >= o.min.x && wp.x <= o.max.x && wp.z >= o.min.z && wp.z <= o.max.z;   // planar: solid from the floor up
        }

        // ── Machines and floor pallets ──────────────────────────────────────────────────────────────

        private static void RefreshMachines()
        {
            float age = Time.unscaledTime - s_allMachinesAt;
            if (age >= 0f && age <= 0.1f) return;
            s_allMachinesAt = Time.unscaledTime;
            s_allMachines.Clear();
            foreach (var s in Object.FindObjectsByType<MHEOperatorSlot>())
                if (s != null && s.isActiveAndEnabled && s.GetComponent<PlacedObject>() != null) s_allMachines.Add(s);
        }

        /// <summary>Every active placed machine (RT, DS, pallet jack), manned or not. Cached for ~0.1 s. Read only.</summary>
        public static List<MHEOperatorSlot> AllMachines() { RefreshMachines(); return s_allMachines; }

        /// <summary>Every pallet standing on a floor (not riding a machine or person, not in a rack). Cached for ~1 s. Read only.</summary>
        public static List<Transform> AllFloorPallets() { RefreshFloorPallets(); return s_floorPallets; }

        /// <summary>Half the side of a floor pallet's square footprint, metres.</summary>
        public static float PalletHalfSize => PalletHalf;

        /// <summary>Pallets standing on a floor (lanes, dock, aisles). Pallets riding a machine or a person are part of that machine;
        /// pallets in a rack are inside a solid already.</summary>
        private static void RefreshFloorPallets()
        {
            float age = Time.unscaledTime - s_floorPalletsAt;
            if (age >= 0f && age <= 1f) return;
            s_floorPalletsAt = Time.unscaledTime;
            s_floorPallets.Clear();
            // Received stock carries PalletData; a staged outbound pallet may only carry OutboundPalletBuilder.
            foreach (var pd in Object.FindObjectsByType<PalletData>())
                if (pd != null && pd.isActiveAndEnabled) AddFloorPallet(pd.transform);
            foreach (var ob in Object.FindObjectsByType<OutboundPalletBuilder>())
                if (ob != null && ob.isActiveAndEnabled && ob.GetComponent<PalletData>() == null) AddFloorPallet(ob.transform);
        }

        private static void AddFloorPallet(Transform t)
        {
            if (t.GetComponentInParent<MHEOperatorSlot>() != null || t.GetComponentInParent<EmployeeIdentity>() != null) return;   // riding something
            if (SolidAt(t.position, out _)) return;   // in a rack
            s_floorPallets.Add(t);
        }

        public static bool InsideFootprint(Transform t, Footprint f, Vector3 wp, float inflate)
        {
            Vector3 l = t.InverseTransformPoint(wp);
            return l.x >= f.MinX - inflate && l.x <= f.MaxX + inflate && l.z >= f.MinZ - inflate && l.z <= f.MaxZ + inflate;
        }

        public static bool InsidePallet(Transform p, Vector3 wp, float inflate)
        {
            Vector3 l = Quaternion.Inverse(p.rotation) * (wp - p.position);
            float h = PalletHalf + inflate;
            return Mathf.Abs(l.x) <= h && Mathf.Abs(l.z) <= h;
        }

        public static Footprint PalletFootprint => new Footprint { MinX = -PalletHalf, MaxX = PalletHalf, MinZ = -PalletHalf, MaxZ = PalletHalf };

        /// <summary>Fills NearMachines / NearPallets with what is within <paramref name="radius"/> of <paramref name="pos"/>
        /// (other than <paramref name="self"/>).</summary>
        public static void GatherNear(Vector3 pos, float radius, MHEOperatorSlot self)
        {
            RefreshMachines();
            RefreshFloorPallets();
            NearMachines.Clear();
            NearPallets.Clear();
            foreach (var m in s_allMachines)
                if (m != null && m != self && Flat(m.transform.position - pos).magnitude < radius + 3f) NearMachines.Add(m);
            foreach (var p in s_floorPallets)
                if (p != null && Mathf.Abs(p.position.y - pos.y) < 0.8f && Flat(p.position - pos).magnitude < radius + 1f) NearPallets.Add(p);
        }

        // ── The test ────────────────────────────────────────────────────────────────────────────────

        /// <summary>The machine the most recent <see cref="PenetrationAt"/> / <see cref="JackPenetrationAt"/> hit (null if it hit something else).
        /// Lets the traffic code say WHO is in the way.</summary>
        public static MHEOperatorSlot LastMachineHit { get; private set; }

        /// <summary>How many of the machine's outline points are inside something solid with the machine at this pose.</summary>
        public static int PenetrationAt(MHEOperatorSlot m, Vector3 pos, Quaternion rot, out string what)
        {
            what = null;
            LastMachineHit = null;
            if (m == null) return 0;
            var pts = SamplePointsOf(m);
            Vector3 scale = m.transform.lossyScale;
            GatherNear(pos, 3.5f, m);
            int hits = 0;
            foreach (var lp in pts)
            {
                Vector3 wp = pos + rot * Vector3.Scale(lp, scale);
                if (SolidAt(wp, out string w)) { hits++; what ??= w; continue; }
                bool hit = false;
                foreach (var o in NearMachines)
                    if (InsideFootprint(o.transform, FootprintOf(o), wp, 0f)) { hits++; what ??= o.name; LastMachineHit ??= o; hit = true; break; }
                if (hit) continue;
                foreach (var p in NearPallets)
                    if (InsidePallet(p, wp, 0f)) { hits++; what ??= "a pallet (" + p.name + ")"; break; }
            }
            return hits;
        }

        /// <summary>How many of the machine's outline points are inside another VEHICLE at this pose: any pallet jack (body or fork load) or any
        /// manned machine (reach truck, dock stocker). Racks, walls, pallets and parked, unmanned non-jack machines are ignored.</summary>
        public static int JackPenetrationAt(MHEOperatorSlot m, Vector3 pos, Quaternion rot, float inflate, out string what)
        {
            what = null;
            LastMachineHit = null;
            if (m == null) return 0;
            var pts = SamplePointsOf(m);
            Vector3 scale = m.transform.lossyScale;
            GatherNear(pos, 3.5f, m);
            int hits = 0;
            foreach (var lp in pts)
            {
                Vector3 wp = pos + rot * Vector3.Scale(lp, scale);
                foreach (var o in NearMachines)
                    if ((o.IsPalletJack || o.IsOccupied) && InsideFootprint(o.transform, FootprintOf(o), wp, inflate)) { hits++; what ??= o.name; LastMachineHit ??= o; break; }
            }
            return hits;
        }

        /// <summary>Reach-truck clearance: blocks clipping other vehicles, and enforces the shared one-metre vehicle buffer.</summary>
        public static bool JackStepBlocked(MHEOperatorSlot m, Vector3 nextPos, Quaternion nextRot, out string what)
            => JackStepBlocked(m, nextPos, nextRot, out what, out _);

        /// <summary>As above, also naming the jack in the way.</summary>
        public static bool JackStepBlocked(MHEOperatorSlot m, Vector3 nextPos, Quaternion nextRot, out string what, out MHEOperatorSlot jack)
        {
            what = null; jack = null;
            if (m == null) return false;
            const float Margin = 0.15f;
            int next = PenetrationOfKind(m, nextPos, nextRot, Margin, o => o.IsPalletJack, out what, out jack);
            if (next > 0 && next > PenetrationOfKind(m, m.transform.position, m.transform.rotation, Margin, o => o.IsPalletJack, out _, out _))
            {
                LastMachineHit = jack;
                return true;
            }
            if (!VehicleSpacingBlocked(m, nextPos, nextRot, VehicleSpacingMeters, out var blocker)) return false;
            SetMachineBlocker(blocker, out what);
            return true;
        }

        /// <summary>REACH TRUCKS HAVE RIGHT OF WAY (Tad, 2026-11-09): "pallet jacks maintain at least a 1 meter distance from making
        /// contact". True when this step would bring the jack <paramref name="m"/> closer than <paramref name="buffer"/> to a MANNED reach
        /// truck than it already is (an unmanned one never moves; the ordinary clearance test keeps the jack off it). Moving away is
        /// always allowed, so a jack that ends up inside the buffer can still back off.</summary>
        public static bool ReachTruckBufferBlocked(MHEOperatorSlot m, Vector3 nextPos, Quaternion nextRot, float buffer, out MHEOperatorSlot truck)
        {
            truck = null;
            if (m == null) return false;
            System.Func<MHEOperatorSlot, bool> isRt = o => o.IsOccupied && o.GetComponent<ReachTruckOperator>() != null;
            int next = PenetrationOfKind(m, nextPos, nextRot, buffer, isRt, out _, out truck);
            if (next == 0) return false;
            return next > PenetrationOfKind(m, m.transform.position, m.transform.rotation, buffer, isRt, out _, out _);
        }

        /// <summary>PALLET JACKS KEEP THEIR DISTANCE (Tad, 2026-11-10: they "stick to each other like glue"). True when this step would bring
        /// the machine <paramref name="m"/> closer than <paramref name="buffer"/> to a MANNED pallet jack than it already is. Outline to
        /// outline (forks included), so it works at any angle on open floor. Moving away is always allowed.</summary>
        public static bool JackBufferBlocked(MHEOperatorSlot m, Vector3 nextPos, Quaternion nextRot, float buffer, out MHEOperatorSlot jack)
        {
            jack = null;
            if (m == null) return false;
            System.Func<MHEOperatorSlot, bool> manned = o => o.IsPalletJack && o.IsOccupied;
            int next = PenetrationOfKind(m, nextPos, nextRot, buffer, manned, out _, out jack);
            if (next == 0) return false;
            return next > PenetrationOfKind(m, m.transform.position, m.transform.rotation, buffer, manned, out _, out _);
        }

        /// <summary>How many of the machine's outline points are inside an inflated outline of a nearby machine matching <paramref name="kind"/>.</summary>
        private static int PenetrationOfKind(MHEOperatorSlot m, Vector3 pos, Quaternion rot, float inflate, System.Func<MHEOperatorSlot, bool> kind,
                                             out string what, out MHEOperatorSlot first)
        {
            what = null; first = null;
            var pts = SamplePointsOf(m);
            Vector3 scale = m.transform.lossyScale;
            GatherNear(pos, 3.5f + inflate, m);
            int hits = 0;
            foreach (var lp in pts)
            {
                Vector3 wp = pos + rot * Vector3.Scale(lp, scale);
                foreach (var o in NearMachines)
                    if (kind(o) && InsideFootprint(o.transform, FootprintOf(o), wp, inflate)) { hits++; what ??= o.name; first ??= o; break; }
            }
            return hits;
        }

        /// <summary>A machine's outline corners on the floor, in world space (for lane / aisle span tests).</summary>
        public static void WorldCorners(MHEOperatorSlot m, Vector3[] four)
        {
            var f = FootprintOf(m);
            Transform t = m.transform;
            four[0] = t.TransformPoint(new Vector3(f.MinX, 0f, f.MinZ));
            four[1] = t.TransformPoint(new Vector3(f.MaxX, 0f, f.MinZ));
            four[2] = t.TransformPoint(new Vector3(f.MinX, 0f, f.MaxZ));
            four[3] = t.TransformPoint(new Vector3(f.MaxX, 0f, f.MaxZ));
        }

        /// <summary>True when moving the machine from where it is to this pose would push it into something it isn't already in,
        /// or within <see cref="VehicleSpacingMeters"/> of another vehicle without moving away.</summary>
        public static bool StepBlocked(MHEOperatorSlot m, Vector3 nextPos, Quaternion nextRot, out string what)
        {
            what = null;
            if (m == null) return false;
            int next = PenetrationAt(m, nextPos, nextRot, out what);
            var hitMachine = LastMachineHit;
            int now = PenetrationAt(m, m.transform.position, m.transform.rotation, out _);
            LastMachineHit = hitMachine;
            if (next > now) return true;
            return VehicleSpacingBlocked(m, nextPos, nextRot, VehicleSpacingMeters, out hitMachine)
                   && SetMachineBlocker(hitMachine, out what);
        }

        /// <summary>True when a proposed vehicle pose would reduce its footprint gap below <paramref name="clearance"/> without
        /// increasing that gap. This lets a vehicle already inside the buffer back away, but prevents it approaching or tracking
        /// alongside another vehicle closer than the required safe distance.</summary>
        public static bool VehicleSpacingBlocked(MHEOperatorSlot m, Vector3 nextPos, Quaternion nextRot, float clearance, out MHEOperatorSlot blocker)
        {
            blocker = null;
            if (m == null || clearance <= 0f) return false;
            RefreshMachines();
            Footprint selfFootprint = FootprintOf(m);
            Vector3 scale = m.transform.lossyScale;
            float selfRadius = FootprintRadius(selfFootprint, scale);
            foreach (var other in s_allMachines)
            {
                if (other == null || other == m) continue;
                Footprint otherFootprint = FootprintOf(other);
                float otherRadius = FootprintRadius(otherFootprint, other.transform.lossyScale);
                Vector3 centerDelta = other.transform.position - nextPos;
                centerDelta.y = 0f;
                if (centerDelta.sqrMagnitude > Mathf.Pow(selfRadius + otherRadius + clearance, 2f)) continue;

                float nextGap = FootprintGap(m, nextPos, nextRot, selfFootprint, other, otherFootprint);
                if (nextGap >= clearance) continue;
                float currentGap = FootprintGap(m, m.transform.position, m.transform.rotation, selfFootprint, other, otherFootprint);
                if (nextGap <= currentGap + 0.01f)
                {
                    blocker = other;
                    LastMachineHit = other;
                    return true;
                }
            }
            return false;
        }

        private static bool SetMachineBlocker(MHEOperatorSlot machine, out string what)
        {
            what = machine != null ? machine.name : "another vehicle";
            if (machine == null) return false;
            LastMachineHit = machine;
            return true;
        }

        private static float FootprintRadius(Footprint f, Vector3 scale)
        {
            float x = Mathf.Max(Mathf.Abs(f.MinX), Mathf.Abs(f.MaxX)) * Mathf.Abs(scale.x);
            float z = Mathf.Max(Mathf.Abs(f.MinZ), Mathf.Abs(f.MaxZ)) * Mathf.Abs(scale.z);
            return Mathf.Sqrt(x * x + z * z);
        }

        private static float FootprintGap(MHEOperatorSlot a, Vector3 aPos, Quaternion aRot, Footprint aFootprint,
            MHEOperatorSlot b, Footprint bFootprint)
        {
            float best = float.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                Vector3 a0 = CandidateCorner(a, aPos, aRot, aFootprint, i);
                Vector3 a1 = CandidateCorner(a, aPos, aRot, aFootprint, (i + 1) & 3);
                for (int j = 0; j < 4; j++)
                {
                    Vector3 b0 = CurrentCorner(b, bFootprint, j);
                    Vector3 b1 = CurrentCorner(b, bFootprint, (j + 1) & 3);
                    if (SegmentsIntersect(a0, a1, b0, b1)) return 0f;
                    best = Mathf.Min(best, PointSegmentDistance(a0, b0, b1));
                    best = Mathf.Min(best, PointSegmentDistance(a1, b0, b1));
                    best = Mathf.Min(best, PointSegmentDistance(b0, a0, a1));
                    best = Mathf.Min(best, PointSegmentDistance(b1, a0, a1));
                }
            }
            return best;
        }

        private static Vector3 CandidateCorner(MHEOperatorSlot m, Vector3 pos, Quaternion rot, Footprint f, int index)
        {
            float x = index == 1 || index == 2 ? f.MaxX : f.MinX;
            float z = index >= 2 ? f.MaxZ : f.MinZ;
            Vector3 point = pos + rot * Vector3.Scale(new Vector3(x, 0f, z), m.transform.lossyScale);
            point.y = 0f;
            return point;
        }

        private static Vector3 CurrentCorner(MHEOperatorSlot m, Footprint f, int index)
        {
            float x = index == 1 || index == 2 ? f.MaxX : f.MinX;
            float z = index >= 2 ? f.MaxZ : f.MinZ;
            Vector3 point = m.transform.TransformPoint(new Vector3(x, 0f, z));
            point.y = 0f;
            return point;
        }

        private static float PointSegmentDistance(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 0.000001f ? Mathf.Clamp01(Vector3.Dot(point - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector3.Distance(point, a + ab * t);
        }

        private static bool SegmentsIntersect(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            float abC = Cross(b - a, c - a), abD = Cross(b - a, d - a);
            float cdA = Cross(d - c, a - c), cdB = Cross(d - c, b - c);
            return abC * abD <= 0f && cdA * cdB <= 0f
                   && Mathf.Max(Mathf.Min(a.x, b.x), Mathf.Min(c.x, d.x)) <= Mathf.Min(Mathf.Max(a.x, b.x), Mathf.Max(c.x, d.x)) + 0.0001f
                   && Mathf.Max(Mathf.Min(a.z, b.z), Mathf.Min(c.z, d.z)) <= Mathf.Min(Mathf.Max(a.z, b.z), Mathf.Max(c.z, d.z)) + 0.0001f;
        }

        private static float Cross(Vector3 a, Vector3 b) => a.x * b.z - a.z * b.x;

        /// <summary>True when sliding the machine in a straight line (no turning) from where it stands to <paramref name="to"/> never
        /// pushes it further into anything - the same per-step rule the moves themselves use.</summary>
        public static bool StraightSlideClear(MHEOperatorSlot m, Vector3 to, float step = 0.25f)
        {
            if (m == null) return true;
            Vector3 from = m.transform.position;
            Quaternion rot = m.transform.rotation;
            int prev = PenetrationAt(m, from, rot, out _);
            int n = Mathf.Max(1, Mathf.CeilToInt(Flat(to - from).magnitude / step));
            for (int i = 1; i <= n; i++)
            {
                Vector3 p = Vector3.Lerp(from, to, i / (float)n);
                p.y = from.y;
                int c = PenetrationAt(m, p, rot, out _);
                if (c > prev) return false;
                prev = c;
            }
            return true;
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }
}
