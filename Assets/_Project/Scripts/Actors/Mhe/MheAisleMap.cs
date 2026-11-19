using System.Collections.Generic;
using UnityEngine;
using GameCore.Inventory;

namespace GameCore.Actors
{
    /// <summary>
    /// One rack aisle's geometry for machine traffic (MHE traffic rework, 2026-11-18). Built from the live pick locations, so it works for
    /// any layout the player builds. Frame: <see cref="Origin"/> sits on the aisle's centre line (half way between the two rack faces);
    /// <see cref="Travel"/> points toward higher bay numbers, which is the aisle's chevron direction (RackingSystem numbers bays along the
    /// chevron); <see cref="Right"/> = up x Travel. Along/Lateral are measured from Origin.
    /// </summary>
    public sealed class MheAisle
    {
        public string Prefix;          // "01-"
        public int Number;
        public Vector3 Origin, Travel, Right;
        public float Y;
        /// <summary>Rack face lines (lateral). LatMin &lt; 0 &lt; LatMax. A one-sided aisle's open side is the floor's edge (NavMesh) instead.</summary>
        public float LatMin, LatMax;
        /// <summary>Along the aisle: the rack ends (outer edges of the first and last bays).</summary>
        public float AlongStart, AlongEnd;
        public float BayPitch = 2.66f;
        public bool OneSided;
        public bool HasNegSide, HasPosSide;

        public float Half => (LatMax - LatMin) * 0.5f;
        public float Along(Vector3 p) { p -= Origin; p.y = 0f; return Vector3.Dot(p, Travel); }
        public float Lateral(Vector3 p) { p -= Origin; p.y = 0f; return Vector3.Dot(p, Right); }
        public Vector3 Point(float along, float lateral) { var p = Origin + Travel * along + Right * lateral; p.y = Y; return p; }

        /// <summary>Between the racks (rack ends +/- <paramref name="endSlack"/>, faces +/- <paramref name="sideSlack"/>).</summary>
        public bool Contains(Vector3 p, float endSlack = 0f, float sideSlack = 0f)
        {
            float a = Along(p), l = Lateral(p);
            return a >= AlongStart - endSlack && a <= AlongEnd + endSlack && l >= LatMin - sideSlack && l <= LatMax + sideSlack;
        }

        /// <summary>The rack face line a location faces onto (its side of the aisle).</summary>
        public float FaceLat(LocationData loc) => Lateral(loc.WorldPosition) < 0f ? LatMin : LatMax;
        /// <summary>+1 when the location's rack is on the +Right side, -1 on the other.</summary>
        public int SideOf(LocationData loc) => Lateral(loc.WorldPosition) < 0f ? -1 : 1;

        public override string ToString() => $"aisle {Prefix.TrimEnd('-')} along {AlongStart:0.0}..{AlongEnd:0.0} lat {LatMin:0.00}..{LatMax:0.00} travel {Travel:F2}{(OneSided ? " one-sided" : "")}";
    }

    /// <summary>Every aisle of the building, rebuilt when the pick locations change. Shared by pallet jacks, reach trucks and the NavMesh bake.</summary>
    public static class MheAisleMap
    {
        public const float DefaultFaceDepth = 0.65f;   // slot pivot (rack depth centre) to its aisle face, when it can't be measured
        public const float SlotHalfWidth = 0.665f;      // half a 1.33 m slot along the aisle

        private static readonly List<MheAisle> s_aisles = new();
        private static readonly Dictionary<string, MheAisle> s_byPrefix = new();
        private static int s_builtCount = -1;
        private static float s_builtAt = -100f;
        private static int s_version;

        /// <summary>Bumped on every rebuild (route caches compare it).</summary>
        public static int Version { get { Refresh(); return s_version; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { s_aisles.Clear(); s_byPrefix.Clear(); s_builtCount = -1; s_builtAt = -100f; s_version = 0; }

        public static IReadOnlyList<MheAisle> All { get { Refresh(); return s_aisles; } }

        public static void Invalidate() => s_builtCount = -1;

        public static MheAisle ByPrefix(string prefix)
        {
            Refresh();
            if (string.IsNullOrEmpty(prefix)) return null;
            if (prefix.Length == 2) prefix += "-";
            return s_byPrefix.TryGetValue(prefix, out var a) ? a : null;
        }

        public static MheAisle Of(LocationData loc)
        {
            if (loc == null || string.IsNullOrEmpty(loc.Address) || loc.Address.Length < 3) return null;
            return ByPrefix(loc.Address.Substring(0, 3));
        }

        /// <summary>The aisle <paramref name="p"/> stands in (between its rack ends, +/- slack).</summary>
        public static MheAisle At(Vector3 p, float endSlack = 0f, float sideSlack = 0.2f)
        {
            Refresh();
            MheAisle best = null; float bestD = float.MaxValue;
            foreach (var a in s_aisles)
            {
                if (!a.Contains(p, endSlack, sideSlack)) continue;
                float d = Mathf.Abs(a.Lateral(p));
                if (d < bestD) { bestD = d; best = a; }
            }
            return best;
        }

        private static void Refresh()
        {
            int count = 0;
            foreach (var _ in LocationRegistry.All) count++;
            float now = Time.unscaledTime;
            if (count == s_builtCount && now - s_builtAt < 20f && now >= s_builtAt) return;
            s_builtCount = count;
            s_builtAt = now;
            Build();
            s_version++;
        }

        private struct Loc { public LocationData L; public Vector3 P; public int Bay; public Vector3 ToAisle; }

        private static void Build()
        {
            s_aisles.Clear();
            s_byPrefix.Clear();
            var groups = new Dictionary<string, List<Loc>>();
            foreach (var l in LocationRegistry.All)
            {
                if (l == null || string.IsNullOrEmpty(l.Address) || l.Address.Length < 8) continue;
                string a = l.Address;
                if (!int.TryParse(a.Substring(0, 2), out _) || !int.TryParse(a.Substring(3, 2), out int bay)) continue;
                Vector3 p = l.WorldPosition;
                var anchor = l.transform.Find("LocApproachAnchor");
                Vector3 to = anchor != null ? anchor.position - p : l.transform.forward;
                to.y = 0f;
                string pre = a.Substring(0, 3);
                if (!groups.TryGetValue(pre, out var list)) groups[pre] = list = new List<Loc>();
                list.Add(new Loc { L = l, P = p, Bay = bay, ToAisle = to.sqrMagnitude > 0.0001f ? to.normalized : Vector3.zero });
            }
            foreach (var kv in groups)
            {
                var aisle = BuildOne(kv.Key, kv.Value);
                if (aisle == null) continue;
                s_aisles.Add(aisle);
                s_byPrefix[aisle.Prefix] = aisle;
            }
        }

        private static MheAisle BuildOne(string prefix, List<Loc> locs)
        {
            if (locs.Count < 2) return null;
            // Ground plane only.
            float minY = float.MaxValue;
            foreach (var l in locs) minY = Mathf.Min(minY, l.P.y);
            Vector3 mean = Vector3.zero; float meanBay = 0f;
            foreach (var l in locs) { mean += new Vector3(l.P.x, 0f, l.P.z); meanBay += l.Bay; }
            mean /= locs.Count; meanBay /= locs.Count;

            // The aisle's long axis: the faces point ACROSS the aisle, so the axis is perpendicular to the average face direction.
            Vector3 across = Vector3.zero;
            foreach (var l in locs)
            {
                if (l.ToAisle == Vector3.zero) continue;
                // fold both sides onto one direction
                Vector3 d = l.ToAisle;
                if (across != Vector3.zero && Vector3.Dot(d, across) < 0f) d = -d;
                across += d;
            }
            Vector3 axis;
            if (across.sqrMagnitude > 0.01f) axis = Vector3.Cross(Vector3.up, across.normalized).normalized;
            else
            {
                // principal component of the positions
                float sxx = 0, szz = 0, sxz = 0;
                foreach (var l in locs) { float dx = l.P.x - mean.x, dz = l.P.z - mean.z; sxx += dx * dx; szz += dz * dz; sxz += dx * dz; }
                float ang = 0.5f * Mathf.Atan2(2f * sxz, sxx - szz);
                axis = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
            }
            // Travel = toward higher bay numbers (the chevron).
            float corr = 0f;
            foreach (var l in locs) corr += (l.Bay - meanBay) * Vector3.Dot(new Vector3(l.P.x - mean.x, 0f, l.P.z - mean.z), axis);
            Vector3 travel = corr >= 0f ? axis : -axis;
            Vector3 right = Vector3.Cross(Vector3.up, travel).normalized;

            // Sides: a location's rack is on the side opposite its face direction.
            float faceNeg = float.MinValue, facePos = float.MaxValue;
            bool hasNeg = false, hasPos = false;
            float depthNeg = -1f, depthPos = -1f;
            foreach (var l in locs)
            {
                float lat = Vector3.Dot(new Vector3(l.P.x - mean.x, 0f, l.P.z - mean.z), right);
                float faceDir = l.ToAisle != Vector3.zero ? Vector3.Dot(l.ToAisle, right) : -Mathf.Sign(lat);
                bool negSide = faceDir > 0f;   // faces toward +right, so the rack is on the - side
                float depth = negSide ? depthNeg : depthPos;
                if (depth < 0f && l.P.y <= minY + 0.3f) { depth = MeasureFaceDepth(l.P, faceDir > 0f ? right : -right); if (negSide) depthNeg = depth; else depthPos = depth; }
                float d = depth > 0f ? depth : DefaultFaceDepth;
                if (negSide) { hasNeg = true; faceNeg = Mathf.Max(faceNeg, lat + d); }
                else { hasPos = true; facePos = Mathf.Min(facePos, lat - d); }
            }
            if (!hasNeg && !hasPos) return null;
            bool oneSided = !(hasNeg && hasPos);
            if (oneSided)
            {
                // The open side ends at the floor's edge: measure it on the MHE NavMesh, capped at 6 m.
                float face = hasNeg ? faceNeg : facePos;
                Vector3 outDir = hasNeg ? right : -right;
                Vector3 probe = new Vector3(mean.x, minY, mean.z) + right * face + outDir * 0.8f;
                float width = 4.5f;
                if (MheNav.RaycastFloor(probe, probe + outDir * 6f, out float hitDist)) width = Mathf.Clamp(hitDist + 0.8f + MheNav.AgentRadius, 2.5f, 6.8f);
                if (hasNeg) facePos = faceNeg + width; else faceNeg = facePos - width;
            }
            if (facePos - faceNeg < 1.5f) return null;   // not an aisle a machine fits in

            float centreLat = (faceNeg + facePos) * 0.5f;
            Vector3 origin = mean + right * centreLat; origin.y = minY;

            var aisle = new MheAisle
            {
                Prefix = prefix, Origin = origin, Travel = travel, Right = right, Y = minY,
                LatMin = faceNeg - centreLat, LatMax = facePos - centreLat, OneSided = oneSided, HasNegSide = hasNeg, HasPosSide = hasPos,
            };
            int.TryParse(prefix.Substring(0, 2), out aisle.Number);
            float aMin = float.MaxValue, aMax = float.MinValue;
            int bMin = int.MaxValue, bMax = int.MinValue;
            foreach (var l in locs)
            {
                float a = aisle.Along(l.P);
                aMin = Mathf.Min(aMin, a); aMax = Mathf.Max(aMax, a);
                bMin = Mathf.Min(bMin, l.Bay); bMax = Mathf.Max(bMax, l.Bay);
            }
            aisle.AlongStart = aMin - SlotHalfWidth;
            aisle.AlongEnd = aMax + SlotHalfWidth;
            int sameSideSteps = Mathf.Max(1, (bMax - bMin) / 2);
            aisle.BayPitch = Mathf.Clamp((aMax - aMin) / sameSideSteps, 1.3f, 4f);
            // Floor-level y: the machines' floor (the slot pivots of level 0 sit ~at floor level in this project).
            aisle.Y = minY;
            return aisle;
        }

        /// <summary>Distance from a slot pivot to its rack's aisle face: step out toward the aisle until the solid rack outline ends.</summary>
        private static float MeasureFaceDepth(Vector3 pivot, Vector3 toAisle)
        {
            if (!MachineClearance.SolidAt(pivot, out _)) return DefaultFaceDepth;
            for (float d = 0.05f; d <= 1.6f; d += 0.05f)
                if (!MachineClearance.SolidAt(pivot + toAisle * d, out _)) return Mathf.Clamp(d, 0.3f, 1.5f);
            return DefaultFaceDepth;
        }
    }
}
