using System.Collections.Generic;
using UnityEngine;

namespace GameCore.Actors
{
    /// <summary>
    /// KEEP-CLEAR ZONES AT STAGING LANE MOUTHS (Tad, 2026-11-22).
    ///
    /// A jack or reach truck working a staging lane (driving to its pivot, reversing in, pulling out) holds a rectangle across the lane
    /// mouth. Every OTHER machine's <see cref="MheTravel.Leg"/> stays out of it: it will not start a leg whose goal is inside, and it
    /// stops short of one its path is about to enter, then sits and waits until the owner releases (capped by
    /// <see cref="MheTravel.ZoneWaitMax"/> so a stuck owner never freezes the floor). A machine already inside keeps moving, so it can drive out.
    ///
    /// The zone is a box on the floor: <c>back</c> metres behind the mouth point (into the lane), <c>front</c> metres out in front of it,
    /// <c>halfWidth</c> either side of the lane centre line. Zones expire after <see cref="ExpirySeconds"/> of game time if never released
    /// (coroutines do not run <c>finally</c> when stopped), and die with their owner.
    /// </summary>
    public static class MheLaneZone
    {
        public const float ExpirySeconds = 300f;

        private sealed class Zone
        {
            public Component Owner;
            public Vector3 Mouth, Outward, Side;
            public float Back, Front, HalfWidth, ExpiresAt;
        }

        private static readonly List<Zone> s_active = new List<Zone>();
        /// <summary>Every mouth that has ever been held (owner-less copy): where a machine must NOT be left parked.</summary>
        private static readonly List<Zone> s_known = new List<Zone>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { s_active.Clear(); s_known.Clear(); }

        /// <summary>Hold the zone for <paramref name="owner"/> (replaces its previous one). <paramref name="outward"/> points from the lane
        /// out onto the open floor.</summary>
        public static void Hold(Component owner, Vector3 mouth, Vector3 outward, float back = 1.5f, float front = 2.5f, float halfWidth = 2f)
        {
            if (owner == null) return;
            outward.y = 0f;
            if (outward.sqrMagnitude < 0.0001f) return;
            outward.Normalize();
            mouth.y = 0f;
            Release(owner);
            var z = new Zone
            {
                Owner = owner, Mouth = mouth, Outward = outward, Side = Vector3.Cross(Vector3.up, outward),
                Back = back, Front = front, HalfWidth = halfWidth, ExpiresAt = Time.time + ExpirySeconds
            };
            s_active.Add(z);
            for (int i = 0; i < s_known.Count; i++)
                if ((s_known[i].Mouth - mouth).sqrMagnitude < 0.25f) { s_known[i] = Copy(z); return; }
            s_known.Add(Copy(z));
        }

        private static Zone Copy(Zone z) => new Zone { Mouth = z.Mouth, Outward = z.Outward, Side = z.Side, Back = z.Back, Front = z.Front, HalfWidth = z.HalfWidth };

        public static void Release(Component owner)
        {
            if (owner == null) return;
            for (int i = s_active.Count - 1; i >= 0; i--)
                if (s_active[i].Owner == owner) s_active.RemoveAt(i);
        }

        private static bool Inside(Zone z, Vector3 p)
        {
            Vector3 d = p - z.Mouth; d.y = 0f;
            float along = Vector3.Dot(d, z.Outward);
            if (along < -z.Back || along > z.Front) return false;
            return Mathf.Abs(Vector3.Dot(d, z.Side)) <= z.HalfWidth;
        }

        /// <summary>True when <paramref name="p"/> lies in a zone held by anyone other than <paramref name="self"/>.</summary>
        public static bool IsInsideForeign(Vector3 p, Transform self)
        {
            for (int i = s_active.Count - 1; i >= 0; i--)
            {
                var z = s_active[i];
                if (z.Owner == null || Time.time > z.ExpiresAt) { s_active.RemoveAt(i); continue; }
                if (z.Owner.transform == self) continue;
                if (Inside(z, p)) return true;
            }
            return false;
        }

        /// <summary>True when <paramref name="p"/> is on ANY known lane mouth (held or not); <paramref name="clear"/> is a point just outside it.
        /// Used to move an abandoned machine out of the way instead of leaving it parked across a pivot.</summary>
        public static bool TryClearPoint(Vector3 p, out Vector3 clear)
        {
            clear = p;
            foreach (var z in s_known)
            {
                if (!Inside(z, p)) continue;
                Vector3 d = p - z.Mouth; d.y = 0f;
                float sign = Vector3.Dot(d, z.Side) >= 0f ? 1f : -1f;
                clear = z.Mouth + z.Outward * (z.Front + 1f) + z.Side * (sign * (z.HalfWidth + 1f));
                clear.y = p.y;
                return true;
            }
            return false;
        }
    }
}
