using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace GameCore.Actors
{
    /// <summary>
    /// TRAFFIC TEST PROBE (MHE traffic rework, 2026-11-18). Watches every machine while the game runs and writes what went wrong to the
    /// log ("[MheProbe] ..."), so a test block can be judged from the log instead of by eye:
    ///   CLIP      two machines' outlines overlap (a machine drove into / through another)
    ///   TOO CLOSE two machines less than MheTraffic.Gap apart while one of them is moving
    ///   IN RACK   a machine's outline overlaps a rack / wall
    ///   ANGLE     a pallet jack inside an aisle more than MheTraffic.AisleMaxDeg off the aisle line
    ///   HELD      a machine waiting in traffic for 30+ real seconds (who it waits for)
    ///   FAILED    a motor leg that gave up (why)
    /// Every 30 real seconds a summary line with the counts. Always on in the Editor; MheTrafficProbe.Enabled turns it off.
    /// </summary>
    public sealed class MheTrafficProbe : MonoBehaviour
    {
        public static bool Enabled = true;
        private static MheTrafficProbe s_instance;

        public static int Clips, TooClose, InRack, Angle, Held, Failed;
        public static float MinGapSeen = 99f;

        private readonly Dictionary<string, float> _lastSaid = new();
        private readonly Dictionary<MheMotor, string> _lastFailure = new();
        private readonly Dictionary<MheMotor, float> _heldSince = new();
        private readonly Dictionary<string, bool> _pairClear = new();
        private readonly Dictionary<string, float> _pairGap = new();
        private readonly Dictionary<MheMotor, bool> _inStatic = new();
        private float _next, _nextSummary;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { s_instance = null; Clips = TooClose = InRack = Angle = Held = Failed = 0; MinGapSeen = 99f; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!Application.isEditor && !Debug.isDebugBuild) return;
            if (s_instance != null) return;
            var go = new GameObject("[MheTrafficProbe]") { hideFlags = HideFlags.DontSave };
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<MheTrafficProbe>();
        }

        public static void ResetCounts() { Clips = TooClose = InRack = Angle = Held = Failed = 0; MinGapSeen = 99f; }

        public static string Summary()
        {
            int prevented = 0;
            foreach (var m in MheTraffic.Motors) if (m != null) prevented += m.ClipsPrevented;
            return $"clips {Clips}, too close {TooClose}, in rack {InRack}, aisle angle {Angle}, held 30s+ {Held}, failed legs {Failed}, " +
                   $"smallest gap seen {(MinGapSeen > 50f ? "-" : MinGapSeen.ToString("0.00") + " m")}, moves undone by the anti-clip guard {prevented}";
        }

        private bool Say(string key, float every, string msg)
        {
            float now = Time.unscaledTime;
            if (_lastSaid.TryGetValue(key, out float t) && now - t < every && now >= t) return false;
            _lastSaid[key] = now;
            Debug.LogWarning("[MheProbe] " + msg);
            return true;
        }

        private void Update()
        {
            if (!Enabled || Time.timeScale <= 0f) return;
            float now = Time.unscaledTime;
            if (now < _next) return;
            _next = now + 0.25f;

            var motors = MheTraffic.Motors;
            // Pairs.
            for (int i = 0; i < motors.Count; i++)
            {
                var a = motors[i];
                if (a == null || !a.isActiveAndEnabled) continue;
                var oa = a.Outline;
                for (int j = i + 1; j < motors.Count; j++)
                {
                    var b = motors[j];
                    if (b == null || !b.isActiveAndEnabled) continue;
                    if (Vector3.Distance(a.Centre, b.Centre) > a.HalfLength + b.HalfLength + 2f) continue;
                    var ob = b.Outline;
                    bool moving = a.IsMovingNow || b.IsMovingNow;
                    string pair = a.GetInstanceIDSafe() + "-" + b.GetInstanceIDSafe();
                    if (oa.Overlaps(ob))
                    {
                        // only a NEW overlap is a clip (machines left overlapping in a save just part)
                        bool wasClear = _pairClear.TryGetValue(pair, out bool c) && c;
                        if (moving && wasClear && Say("clip" + pair, 8f,
                                $"CLIP: {Who(a)} and {Who(b)} overlap at {a.Centre:F1} / {b.Centre:F1} ({a.Phase} / {b.Phase})")) Clips++;
                        if (wasClear) MinGapSeen = 0f;
                        _pairClear[pair] = false;
                        continue;
                    }
                    _pairClear[pair] = true;
                    float gap = Gap(oa, ob);
                    // only machines that CLOSED IN count (a pair that started this close and is parting is not an incident)
                    bool wasFar = !_pairGap.TryGetValue(pair, out float prevGap) || prevGap >= MheTraffic.Gap - 0.05f || gap < prevGap - 0.01f;
                    _pairGap[pair] = gap;
                    if (moving && wasFar && gap < MinGapSeen) MinGapSeen = gap;
                    if (moving && wasFar && gap < MheTraffic.Gap - 0.05f && (a.IsDriving || b.IsDriving)
                        && Say("close" + a.GetInstanceIDSafe() + "-" + b.GetInstanceIDSafe(), 15f,
                               $"TOO CLOSE: {Who(a)} and {Who(b)} {gap:0.00} m apart at {a.Centre:F1} ({a.Phase} / {b.Phase})")) TooClose++;
                }

                // Racks / walls.
                // (motor legs only: a dock stocker inside a trailer, or a reach truck's forks in a rack slot, are meant to be there)
                bool inStatic = !a.PoseClearOfStatics(a.Centre, a.Forward, -0.12f);
                bool staticWasClear = !_inStatic.TryGetValue(a, out bool was) || !was;
                _inStatic[a] = inStatic;
                if (a.IsDriving && a.IsMovingNow && inStatic && staticWasClear)
                    if (Say("rack" + a.GetInstanceIDSafe(), 10f, $"IN RACK: {Who(a)} overlaps a rack / wall / pallet at {a.Centre:F1} ({a.Phase})")) InRack++;

                // Aisle angle (pallet jacks).
                if (a.Kind == MheKind.Jack && a.Slot != null && a.Slot.IsOccupied)
                {
                    var aisle = MheAisleMap.At(a.Centre, -0.5f, -0.3f);
                    if (aisle != null)
                    {
                        float ang = Vector3.Angle(a.Forward, aisle.Travel);
                        float off = Mathf.Min(ang, 180f - ang);
                        if (off > MheTraffic.AisleMaxDeg + 1f
                            && Say("angle" + a.GetInstanceIDSafe(), 10f, $"ANGLE: {Who(a)} is {off:0} degrees off the line of {aisle.Prefix.TrimEnd('-')} at {a.Centre:F1} ({a.Phase})")) Angle++;
                        if (ang > 150f && Say("wrongway" + a.GetInstanceIDSafe(), 10f, $"ANGLE: {Who(a)} is facing AGAINST the chevron of {aisle.Prefix.TrimEnd('-')} at {a.Centre:F1} ({a.Phase})")) Angle++;
                    }
                }

                // Held in traffic.
                bool held = a.IsDriving && a.Speed < 0.05f && (a.BlockedByMotor != null || a.WaitingForAisleOf != null);
                if (held)
                {
                    if (!_heldSince.TryGetValue(a, out float since)) _heldSince[a] = now;
                    else if (now - since > 30f && Say("held" + a.GetInstanceIDSafe(), 60f,
                                 $"HELD: {Who(a)} has waited {now - since:0} s at {a.Centre:F1} for {a.BlockedBy} ({a.Phase})")) Held++;
                }
                else _heldSince.Remove(a);

                // Failed legs.
                if (!string.IsNullOrEmpty(a.LastFailure) && (!_lastFailure.TryGetValue(a, out var lf) || lf != a.LastFailure))
                {
                    _lastFailure[a] = a.LastFailure;
                    Failed++;
                    Debug.LogWarning($"[MheProbe] FAILED: {Who(a)} - {a.LastFailure}");
                }
            }

            if (now >= _nextSummary)
            {
                _nextSummary = now + 30f;
                var sb = new StringBuilder("[MheProbe] summary: ").Append(Summary()).Append(" | ");
                foreach (var m in motors)
                {
                    if (m == null || !m.IsDriving) continue;
                    sb.Append(Who(m)).Append(": ").Append(m.Phase);
                    if (m.Speed < 0.05f && !string.IsNullOrEmpty(m.BlockedBy)) sb.Append(" (held: ").Append(m.BlockedBy).Append(')');
                    sb.Append("; ");
                }
                Debug.Log(sb.ToString());
            }
        }

        private static string Who(MheMotor m)
        {
            string kind = m.Kind == MheKind.Jack ? "jack" : m.Kind == MheKind.ReachTruck ? "reach truck" : "dock stocker";
            var op = m.Slot != null ? m.Slot.CurrentOperator : null;
            return op != null ? $"{kind} ({op.name.Replace("Employee_", "")})" : $"{kind} (no driver)";
        }

        /// <summary>Smallest distance between two rectangles (0 when they touch): corner-to-rectangle both ways.</summary>
        private static float Gap(MheObb a, MheObb b)
        {
            float best = float.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                Vector3 ca = a.Corner(i, 0f), cb = b.Corner(i, 0f);
                best = Mathf.Min(best, b.DistanceTo(new Vector2(ca.x, ca.z)));
                best = Mathf.Min(best, a.DistanceTo(new Vector2(cb.x, cb.z)));
            }
            return best;
        }
    }

    internal static class MheProbeExt
    {
        public static int GetInstanceIDSafe(this Object o) => o == null ? 0 : o.GetHashCode();
    }
}
