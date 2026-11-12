using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace GameCore.Actors
{
    public enum EquipmentRole { Jack = 0, DockStocker = 1, ReachTruck = 2 }

    public enum TrafficState { Free, Waiting, Blocked }

    /// <summary>
    /// EQUIPMENT TRAFFIC COORDINATION: the shared rules every machine movement path (pallet jacks, reach trucks, dock stockers) uses.
    ///
    /// * RIGHT OF WAY: reach truck > dock stocker > pallet jack. A selector on a jack always yields to a reach driver (subject to
    ///   <see cref="CanReverse"/>: physical safety comes first).
    /// * ONE MANEUVER AT A TIME: a jam is resolved by backing up / routing round. <see cref="TryBeginManeuver"/> hands out a single
    ///   lease (highest priority first), so several jammed machines never all reverse at once.
    /// * NO SAFE MOVE = REPORT: <see cref="ReportBlockage"/> logs it (throttled) and marks the machine blocked; nothing is driven
    ///   through an obstacle and nothing retries for ever.
    /// * AISLE EXITS: <see cref="AisleExitCheck"/> stops the machine, looks both ways and waits until the cross aisle is clear.
    /// Collision safety itself stays in <see cref="MachineClearance"/>; this file only decides who goes, who waits and who backs up.
    /// </summary>
    public static class EquipmentTraffic
    {
        public const float LaneAhead = 3.6f;            // same reach as the jack queue (JackClearAhead)
        public const float LaneSide = 1.4f;
        public const float ExitLookSeconds = 0.5f;       // each "look left / look right" pause at an aisle exit
        public const float ExitZoneHalfLength = 7f;      // cross-traffic window either side of the aisle mouth
        public const float ExitZoneDepth = 3f;           // ...and how far either side of the mouth line along the aisle
        public const float ExitReportAfterReal = 30f;    // still not clear after this: report it (keeps waiting, never drives into it)
        public const float ManeuverLease = 6f;           // a maneuver that overruns this loses the lease
        public const float ManeuverGap = 0.75f;          // quiet gap before the next machine may start its maneuver
        public static readonly float[] SelectorReverse = { 1.0f, 0.75f, 0.5f };   // 0.5-1 m
        public static readonly float[] YieldToReachReverse = { 2.5f, 2.0f, 1.5f, 1.0f, 0.5f };   // Tad 2026-11-11: back off "a couple meters at least" for a reach truck
        public static readonly float[] ReachReverse = { 1.5f, 1.0f, 0.5f };

        /// <summary>How far a selector tries to back up for <paramref name="to"/>: a couple of metres when it is a reach truck.</summary>
        public static float[] YieldOptionsFor(MHEOperatorSlot to) => RoleOf(to) == EquipmentRole.ReachTruck ? YieldToReachReverse : SelectorReverse;

        private static readonly List<MHEOperatorSlot> s_manned = new();
        private static float s_mannedAt = -100f;

        private static MHEOperatorSlot s_holder;
        private static float s_leaseUntil;
        private static float s_nextFree;
        private static readonly Dictionary<MHEOperatorSlot, float> s_requests = new();
        private static readonly Dictionary<MHEOperatorSlot, float> s_firstRequest = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            s_manned.Clear(); s_mannedAt = -100f; s_holder = null; s_leaseUntil = 0f; s_nextFree = 0f;
            s_requests.Clear(); s_firstRequest.Clear();
        }

        // ── Roles / priority ────────────────────────────────────────────────────────────────────────

        public static EquipmentRole RoleOf(MHEOperatorSlot m)
        {
            if (m == null) return EquipmentRole.Jack;
            if (m.IsPalletJack) return EquipmentRole.Jack;
            return m.GetComponent<ReachTruckOperator>() != null ? EquipmentRole.ReachTruck : EquipmentRole.DockStocker;
        }

        public static int Priority(EquipmentRole r) => (int)r;

        public static int Priority(MHEOperatorSlot m) => Priority(RoleOf(m));

        /// <summary>True when <paramref name="self"/> must give way to <paramref name="other"/> (selectors always yield to reach drivers).</summary>
        public static bool Yields(MHEOperatorSlot self, MHEOperatorSlot other) => Priority(self) < Priority(other);

        // ── Who is out there ────────────────────────────────────────────────────────────────────────

        public static IReadOnlyList<MHEOperatorSlot> Manned()
        {
            float age = Time.unscaledTime - s_mannedAt;
            if (age >= 0f && age <= 0.5f) return s_manned;
            s_mannedAt = Time.unscaledTime;
            s_manned.Clear();
            foreach (var s in UnityEngine.Object.FindObjectsByType<MHEOperatorSlot>())
                if (s != null && s.isActiveAndEnabled && s.IsOccupied && s.GetComponent<PlacedObject>() != null) s_manned.Add(s);
            return s_manned;
        }

        /// <summary>A manned machine other than <paramref name="self"/> in the lane of travel for a move along <paramref name="dir"/>.
        /// <paramref name="higherOnly"/>: only machines <paramref name="self"/> has to yield to.</summary>
        public static bool FindInLane(MHEOperatorSlot self, Vector3 dir, float ahead, float side, bool higherOnly, out MHEOperatorSlot found)
        {
            found = null;
            if (self == null) return false;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return false;
            dir.Normalize();
            Vector3 me = self.transform.position;
            float best = float.MaxValue;
            foreach (var o in Manned())
            {
                if (o == null || o == self) continue;
                if (higherOnly && !Yields(self, o)) continue;
                Vector3 rel = o.transform.position - me; rel.y = 0f;
                float a = Vector3.Dot(rel, dir);
                if (a <= 0f || a > ahead) continue;
                if ((rel - dir * a).magnitude >= side) continue;
                if (a < best) { best = a; found = o; }
            }
            return found != null;
        }

        // ── Physical safety for reversing ───────────────────────────────────────────────────────────

        /// <summary>True when <paramref name="m"/> can slide <paramref name="dist"/> metres along <paramref name="dir"/> without pushing into
        /// a rack, wall, machine or pallet (MachineClearance), staying on its NavMesh and inside its boundary.</summary>
        public static bool CanReverse(MHEOperatorSlot m, Vector3 dir, float dist, out string why)
        {
            why = null;
            if (m == null) { why = "no machine"; return false; }
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) { why = "no direction"; return false; }
            dir.Normalize();
            Vector3 from = m.transform.position;
            Vector3 to = from + dir * dist;
            if (!MachineClearance.StraightSlideClear(m, to, 0.1f)) { why = "something solid behind it"; return false; }
            var agent = m.GetComponent<NavMeshAgent>();
            if (agent != null && agent.agentTypeID != -1)
            {
                if (!NavSample.OnAgentMesh(agent, to, 0.3f, out var hit) || Mathf.Abs(hit.position.y - from.y) > 0.3f) { why = "no floor behind it"; return false; }
            }
            if (MHEBoundary.Restricts(m.gameObject) && !MHEBoundary.IsInside(to)) { why = "outside its boundary"; return false; }
            return true;
        }

        /// <summary>Largest of <paramref name="options"/> (metres, tried in order) that is safe; 0 when none is.</summary>
        public static float PickSafeReverse(MHEOperatorSlot m, Vector3 dir, float[] options)
        {
            foreach (float d in options) if (CanReverse(m, dir, d, out _)) return d;
            return 0f;
        }

        /// <summary>Slides <paramref name="m"/> back <paramref name="dist"/> metres along <paramref name="dir"/>, one guarded step at a time.
        /// Stops where it is the moment a step would push into something.</summary>
        public static IEnumerator ReverseStraight(MHEOperatorSlot m, Vector3 dir, float dist, float speed, NavMeshAgent agent = null)
        {
            if (m == null) yield break;
            dir.y = 0f; dir.Normalize();
            Transform t = m.transform;
            Vector3 target = t.position + dir * dist;
            while (m != null && Vector3.Distance(Flat(t.position), Flat(target)) > 0.02f)
            {
                Vector3 next = Vector3.MoveTowards(t.position, new Vector3(target.x, t.position.y, target.z), Mathf.Max(0.2f, speed) * Time.deltaTime);
                if (MachineClearance.StepBlocked(m, next, t.rotation, out _)) yield break;
                t.position = next;
                if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh) agent.nextPosition = next;
                yield return null;
            }
        }

        // ── One maneuver at a time ──────────────────────────────────────────────────────────────────

        /// <summary>Asks for the (single) maneuver lease. True = go ahead; call <see cref="EndManeuver"/> afterwards. False = not yet:
        /// someone else is maneuvering, the quiet gap hasn't passed, or a higher-priority machine asked first. Ask again next frame.</summary>
        public static bool TryBeginManeuver(MHEOperatorSlot m)
        {
            if (m == null) return false;
            float now = Time.unscaledTime;
            if (s_holder != null && (s_holder == m)) { s_leaseUntil = now + ManeuverLease; return true; }
            if (s_holder != null && now < s_leaseUntil && s_holder.isActiveAndEnabled) { Register(m, now); return false; }
            if (now < s_nextFree) { Register(m, now); return false; }

            Register(m, now);
            MHEOperatorSlot best = null; int bestP = -1; float bestFirst = float.MaxValue;
            var stale = new List<MHEOperatorSlot>();
            foreach (var kv in s_requests)
            {
                if (kv.Key == null || now - kv.Value > 1.5f) { stale.Add(kv.Key); continue; }
                int p = Priority(kv.Key);
                float first = s_firstRequest.TryGetValue(kv.Key, out var f) ? f : now;
                bool better = p > bestP || (p == bestP && (first < bestFirst || (first == bestFirst && string.CompareOrdinal(kv.Key.name, best.name) < 0)));
                if (better) { best = kv.Key; bestP = p; bestFirst = first; }
            }
            foreach (var s in stale) { s_requests.Remove(s); if (s != null) s_firstRequest.Remove(s); else s_firstRequest.Clear(); }
            if (best != m) return false;
            s_holder = m;
            s_leaseUntil = now + ManeuverLease;
            s_requests.Remove(m); s_firstRequest.Remove(m);
            return true;
        }

        private static void Register(MHEOperatorSlot m, float now)
        {
            s_requests[m] = now;
            if (!s_firstRequest.ContainsKey(m)) s_firstRequest[m] = now;
        }

        public static void EndManeuver(MHEOperatorSlot m)
        {
            if (s_holder != m) return;
            s_holder = null;
            s_nextFree = Time.unscaledTime + ManeuverGap;
        }

        public static bool IsManeuvering(MHEOperatorSlot m) => m != null && s_holder == m;

        // ── Reporting ───────────────────────────────────────────────────────────────────────────────

        /// <summary>No safe movement exists: say so (throttled) and mark the machine blocked. The caller then stays put.</summary>
        public static void ReportBlockage(MHEOperatorSlot m, string reason)
        {
            string who = m != null ? m.name : "?";
            var agent = m != null ? m.GetComponent<EquipmentTrafficAgent>() : null;
            if (agent != null) agent.MarkReported(reason);
            ThrottledLog.Warn("traffic-blocked-" + who,
                $"[EquipmentTraffic] '{who}' is blocked at {(m != null ? m.transform.position : Vector3.zero)}: {reason}. No safe way to move - staying put.", 60f);
        }

        // ── Aisle exits ─────────────────────────────────────────────────────────────────────────────

        /// <summary>Moving machines in the cross aisle at an aisle mouth: <paramref name="exitPoint"/> is the aisle end, <paramref name="outDir"/>
        /// the way out. Machines parked there don't count (they never clear; collision safety handles them).</summary>
        public static bool CrossTrafficClear(MHEOperatorSlot self, Vector3 exitPoint, Vector3 outDir, out MHEOperatorSlot who)
        {
            who = null;
            outDir.y = 0f;
            if (outDir.sqrMagnitude < 0.0001f) return true;
            outDir.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, outDir);
            foreach (var o in Manned())
            {
                if (o == null || o == self) continue;
                Vector3 rel = o.transform.position - exitPoint; rel.y = 0f;
                float along = Vector3.Dot(rel, outDir), lat = Vector3.Dot(rel, right);
                if (Mathf.Abs(lat) > ExitZoneHalfLength || along < -ExitZoneDepth || along > ExitZoneDepth + 2f) continue;
                var ta = o.GetComponent<EquipmentTrafficAgent>();
                if (ta == null || !ta.IsMoving) continue;
                who = o;
                return false;
            }
            return true;
        }

        /// <summary>The stop-look-both-ways-wait-until-clear gate every aisle exit goes through. The caller has already decided this step
        /// leaves an aisle; it holds still here until clear. Waiting for cross traffic is NOT "blocked" (no honking): only a wait that never ends
        /// is reported, after <see cref="ExitReportAfterReal"/>.</summary>
        public static IEnumerator AisleExitCheck(MHEOperatorSlot self, Vector3 exitPoint, Vector3 outDir, Func<bool> keepWaiting = null)
        {
            if (self == null) yield break;
            var ta = self.GetComponent<EquipmentTrafficAgent>();
            if (ta != null) ta.Stop();
            for (float t = 0f; t < ExitLookSeconds * 2f; t += RealDt()) yield return null;   // look both ways
            float waited = 0f; bool reported = false;
            while (self != null && !CrossTrafficClear(self, exitPoint, outDir, out var who))
            {
                if (keepWaiting != null && !keepWaiting()) yield break;
                if (ta != null) ta.NoteWaiting();
                waited += RealDt();
                if (!reported && waited >= ExitReportAfterReal)
                {
                    reported = true;
                    ThrottledLog.Warn("traffic-exit-" + self.name, $"[EquipmentTraffic] '{self.name}' has waited {waited:0}s at an aisle exit for {(who != null ? who.name : "cross traffic")} - still waiting (not driving into it).", 60f);
                }
                yield return null;
            }
            if (ta != null) ta.NoteClear(true);
        }

        public static float RealDt() => Time.timeScale > 0f ? Time.unscaledDeltaTime : 0f;

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }

    /// <summary>
    /// Per-machine traffic state: speed tracking, waiting-vs-blocked classification, and the frustration honk + red lines above the vehicle.
    /// While BLOCKED, every <see cref="HonkRollInterval"/> real seconds it rolls <see cref="HonkChance"/> (default 25%, set per machine for
    /// future personality traits). Added at runtime next to MheAvoidanceObstacle (EquipmentBattery.Start).
    /// </summary>
    [DisallowMultipleComponent]
    public class EquipmentTrafficAgent : MonoBehaviour
    {
        public const float DefaultHonkChance = 0.25f;
        public const float HonkRollInterval = 5f;
        public const float BlockedGraceReal = 2f;        // held this long (and not behind something that is moving) = genuinely blocked
        public const float ClearGraceReal = 0.5f;
        public const float MovingSpeed = 0.2f;
        public const float FxSeconds = 1.6f;

        /// <summary>Test seam: replaced by a deterministic source in tests.</summary>
        public static Func<float> RandomSource = () => UnityEngine.Random.value;

        [Range(0f, 1f)] [SerializeField] private float _honkChance = DefaultHonkChance;
        public float HonkChance { get => _honkChance; set => _honkChance = Mathf.Clamp01(value); }

        public TrafficState State { get; private set; }
        public MHEOperatorSlot BlockedBy { get; private set; }
        public bool IsMoving { get; private set; }
        public float Speed { get; private set; }
        public float HeldReal { get; private set; }
        public int HonkCount { get; private set; }
        public int RollCount { get; private set; }
        public string ReportedReason { get; private set; }
        public bool FxShowing => _fx != null && _fx.activeSelf;
        public int FxLineCount => _lines != null ? _lines.Length : 0;

        private MHEOperatorSlot _slot;
        private Vector3 _lastPos;
        private float _clearReal, _blockedReal, _honkClock, _fxUntil;
        private EquipmentTrafficAgent _blockerAgent;
        private MHEOperatorSlot _blockerFor;
        private GameObject _fx;
        private LineRenderer[] _lines;
        private AudioSource _audio;

        public static EquipmentTrafficAgent Ensure(GameObject machine)
        {
            if (machine == null) return null;
            var a = machine.GetComponent<EquipmentTrafficAgent>();
            return a != null ? a : machine.AddComponent<EquipmentTrafficAgent>();
        }

        private void Awake()
        {
            _slot = GetComponent<MHEOperatorSlot>();
            _lastPos = transform.position;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt > 0f)
            {
                Speed = Vector3.Distance(Flat(transform.position), Flat(_lastPos)) / dt;
                IsMoving = Speed > MovingSpeed;
                _lastPos = transform.position;
            }
            if (_fx != null && _fx.activeSelf)
            {
                float left = _fxUntil - Time.unscaledTime;
                if (left <= 0f) _fx.SetActive(false);
                else AnimateFx(left);
            }
            // A held machine that nobody reports on this frame is no longer held.
            _clearReal += EquipmentTraffic.RealDt();
            if (State != TrafficState.Free && _clearReal >= ClearGraceReal) NoteClear(false);
        }

        // ── State from the movement code (call every frame while held / not held) ────────────────────

        /// <summary>Held by <paramref name="blocker"/> (another machine) or a solid. <paramref name="realDt"/> only for tests.</summary>
        public void NoteHeld(MHEOperatorSlot blocker, string solid, float realDt = -1f)
        {
            if (realDt < 0f) realDt = EquipmentTraffic.RealDt();
            _clearReal = 0f;
            HeldReal += realDt;
            BlockedBy = blocker;
            if (blocker != _blockerFor) { _blockerFor = blocker; _blockerAgent = blocker != null ? blocker.GetComponent<EquipmentTrafficAgent>() : null; }
            bool behindMover = _blockerAgent != null && _blockerAgent.IsMoving;
            if (HeldReal < BlockedGraceReal || behindMover) { State = TrafficState.Waiting; return; }
            State = TrafficState.Blocked;
            _blockedReal += realDt;
            if (blocker == null)
            {
                _honkClock = 0f;
                return;
            }
            _honkClock += realDt;
            while (_honkClock >= HonkRollInterval)
            {
                _honkClock -= HonkRollInterval;
                RollHonk();
            }
        }

        /// <summary>Waiting for a gap (aisle exit, crossing): never counts as blocked and never honks.</summary>
        public void NoteWaiting()
        {
            _clearReal = 0f;
            if (State == TrafficState.Free) State = TrafficState.Waiting;
        }

        /// <summary>Not held this frame (the movement code calls this every free frame; the state resets after a short grace).</summary>
        public void NoteClear(bool immediate)
        {
            if (!immediate && _clearReal < ClearGraceReal) return;
            State = TrafficState.Free;
            BlockedBy = null; _blockerFor = null; _blockerAgent = null;
            HeldReal = 0f; _blockedReal = 0f; _honkClock = 0f;
            ReportedReason = null;
        }

        public void MarkReported(string reason) { ReportedReason = reason; State = TrafficState.Blocked; _clearReal = 0f; }

        public void Stop()
        {
            var nav = GetComponent<NavMeshAgent>();
            if (nav != null && nav.isActiveAndEnabled && nav.isOnNavMesh) { nav.isStopped = true; nav.velocity = Vector3.zero; }
        }

        // ── Honk + red lines ──────────────────────────────────────────────────────────────────────────

        /// <summary>One 25% (configurable) roll. Public so a test can drive it.</summary>
        public bool RollHonk()
        {
            RollCount++;
            if (RandomSource() >= _honkChance) return false;
            Honk();
            return true;
        }

        public void Honk()
        {
            HonkCount++;
            ShowFrustration();
            PlayHonk();
        }

        private void PlayHonk()
        {
            AudioClip clip = VehicleThrottleAudio.SharedHonkClip;
            var own = GetComponent<VehicleThrottleAudio>();
            if (own != null && own.HonkClip != null) clip = own.HonkClip;
            if (clip == null) return;
            if (_audio == null)
            {
                _audio = gameObject.AddComponent<AudioSource>();
                _audio.playOnAwake = false;
                _audio.spatialBlend = 1f;
                _audio.rolloffMode = AudioRolloffMode.Logarithmic;
                _audio.minDistance = 5f;
                _audio.maxDistance = 30f;
            }
            _audio.PlayOneShot(clip, 0.7f * AudioManager.GameVolumeLevel);
        }

        private void ShowFrustration()
        {
            if (_fx == null) BuildFx();
            _fx.transform.position = TopOfMachine() + Vector3.up * 0.7f;
            _fxUntil = Time.unscaledTime + FxSeconds;
            _fx.SetActive(true);
            AnimateFx(FxSeconds);
        }

        private Vector3 TopOfMachine()
        {
            Bounds b = default; bool any = false;
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (r == null || !r.enabled || r is ParticleSystemRenderer || r is LineRenderer) continue;
                if (r.GetComponentInParent<EmployeeIdentity>() != null) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return any ? new Vector3(b.center.x, b.max.y, b.center.z) : transform.position + Vector3.up * 2.5f;
        }

        // Red strokes fanned above the vehicle (the "frustration lines" of the reference screenshot), turned to face the camera.
        private void BuildFx()
        {
            _fx = new GameObject("FrustrationLines");
            _fx.transform.SetParent(null, false);
            var mat = new Material(Shader.Find("Sprites/Default")) { color = new Color(0.9f, 0.08f, 0.06f, 1f) };
            const int strokes = 5;
            _lines = new LineRenderer[strokes];
            for (int i = 0; i < strokes; i++)
            {
                float ang = Mathf.Lerp(-65f, 65f, i / (strokes - 1f)) * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Sin(ang), Mathf.Cos(ang));
                var go = new GameObject("Stroke" + i);
                go.transform.SetParent(_fx.transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = false;
                lr.positionCount = 2;
                lr.SetPosition(0, new Vector3(dir.x * 0.35f, dir.y * 0.35f, 0f));
                lr.SetPosition(1, new Vector3(dir.x * 0.85f, dir.y * 0.85f, 0f));
                lr.startWidth = lr.endWidth = 0.09f;
                lr.numCapVertices = 3;
                lr.sharedMaterial = mat;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                _lines[i] = lr;
            }
            _fx.SetActive(false);
        }

        private void AnimateFx(float secondsLeft)
        {
            var cam = Camera.main;
            if (cam != null) _fx.transform.rotation = cam.transform.rotation;
            _fx.transform.position = TopOfMachine() + Vector3.up * 0.7f;
            float pulse = 1f + 0.2f * Mathf.Sin(Time.unscaledTime * 24f);
            float fade = Mathf.Clamp01(secondsLeft / 0.4f);
            _fx.transform.localScale = Vector3.one * pulse;
            foreach (var lr in _lines)
            {
                var c = new Color(0.9f, 0.08f, 0.06f, fade);
                lr.startColor = lr.endColor = c;
            }
        }

        private void OnDestroy()
        {
            if (_fx != null) Destroy(_fx);
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }
}
