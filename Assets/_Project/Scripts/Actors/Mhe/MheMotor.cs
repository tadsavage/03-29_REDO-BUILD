using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using GameCore.Inventory;

namespace GameCore.Actors
{
    public enum MheKind { Jack, ReachTruck, DockStocker }

    public struct MheResult
    {
        public bool Ok;
        public string Why;
        public static MheResult Done => new MheResult { Ok = true };
        public static MheResult Fail(string why) => new MheResult { Ok = false, Why = why };
    }

    /// <summary>
    /// THE MACHINE MOTOR (MHE traffic rework, 2026-11-18). Drives a reach truck, pallet jack or dock stocker along a route from
    /// <see cref="MhePlanner"/>: the NavMesh plans the way on open floor, the aisle's own lane is used inside rack aisles.
    ///
    /// No physics and no NavMeshAgent steering: the machine's CENTRE follows the route (pure pursuit), it turns about its centre, and it
    /// is moved by script. What keeps it out of everything is a sensor: every frame the machine's full outline (forks and carried pallets
    /// included) is swept along the route ahead, and it brakes so it always stops <see cref="MheTraffic.Gap"/> short of another machine,
    /// short of a person on foot, and short of racks, walls and floor pallets. A last check on every step refuses any move that would
    /// overlap something, so nothing is ever driven through.
    ///
    /// Right of way: reach trucks first, then dock stockers, then pallet jacks. A machine of lower rank treats the stretch a higher-ranked
    /// machine is about to drive as occupied, so it gives way at crossings and mouths. When two machines hold each other up, the lower rank
    /// (or, between equals, the later one) backs off out of the other's way and then carries on. A machine standing still (parked to pick,
    /// unmanned) is driven round: in an aisle on a line that clears it, on open floor by a detour.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10000)]   // after AiNavigation and every task script: the anti-clip guard sees each frame's final pose
    public sealed class MheMotor : MonoBehaviour
    {
        public static MheMotor Ensure(GameObject go)
        {
            if (go == null) return null;
            var m = go.GetComponent<MheMotor>();
            if (m == null) m = go.AddComponent<MheMotor>();
            return m;
        }

        public MHEOperatorSlot Slot { get; private set; }
        public MheKind Kind { get; private set; }
        public int Priority => Kind == MheKind.ReachTruck ? 3 : Kind == MheKind.DockStocker ? 2 : 1;

        // ── Geometry ─────────────────────────────────────────────────────────────────────────────────
        private float _cx, _cz, _hf = 1.9f, _hr = 0.61f;
        private bool _geom;
        private void Geom()
        {
            if (_geom) return;
            if (Slot == null) Slot = GetComponent<MHEOperatorSlot>();
            if (Slot == null) return;
            var f = MachineClearance.FootprintOf(Slot);
            Vector3 s = transform.lossyScale;
            _cx = (f.MinX + f.MaxX) * 0.5f * s.x;
            _cz = (f.MinZ + f.MaxZ) * 0.5f * s.z;
            _hr = (f.MaxX - f.MinX) * 0.5f * s.x;
            _hf = (f.MaxZ - f.MinZ) * 0.5f * s.z;
            _geom = true;
        }

        /// <summary>Half the machine's length / width, a carried pallet included.</summary>
        public float HalfLength { get { Geom(); UpdateLoad(); return _hf + Mathf.Max(0f, _loadF) + Mathf.Abs(_loadShift); } }
        public float HalfWidth { get { Geom(); UpdateLoad(); return _hr + Mathf.Max(0f, _loadR); } }
        /// <summary>Centre distance in front of an aisle's rack end where the whole machine is out of the aisle.</summary>
        public float EntryClear => HalfLength + 0.35f;

        private float BattMul => Slot != null ? Mathf.Max(1f, EquipmentBattery.SpeedMultiplierFor(Slot)) : 1f;
        public float OpenSpeed => (Kind == MheKind.Jack ? 2.0f : Kind == MheKind.ReachTruck ? 2.6f : 2.4f) * BattMul;
        public float AisleSpeed => (Kind == MheKind.Jack ? 1.3f : 1.8f) * BattMul;
        public float ReverseSpeed => (Kind == MheKind.Jack ? 0.8f : 1.1f) * BattMul;
        private const float Accel = 1.1f, Decel = 2.4f;
        private float YawRate => Kind == MheKind.Jack ? 75f : 90f;     // deg/s while moving
        private float PivotRate => Kind == MheKind.Jack ? 80f : 95f;   // deg/s on the spot

        public Vector3 Forward { get { var f = transform.forward; f.y = 0f; return f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward; } }
        public Vector3 Centre { get { Geom(); return transform.position + transform.rotation * new Vector3(_cx, 0f, _cz); } }
        public Vector3 CentreFromRoot(Vector3 root, Vector3 fwd) { Geom(); return root + Quaternion.LookRotation(FlatN(fwd)) * new Vector3(_cx, 0f, _cz); }
        public Vector3 RootFromCentre(Vector3 c, Vector3 fwd) { Geom(); return c - Quaternion.LookRotation(FlatN(fwd)) * new Vector3(_cx, 0f, _cz); }
        public MheObb Outline => OutlineAt(Centre, Forward);
        public MheObb OutlineAt(Vector3 c, Vector3 f) { Geom(); return MheObb.From(c, f, _hf + _loadF, _hr + _loadR).Shifted(f, _loadShift); }

        // Carried pallets stick out of the body (a reach truck's forks): grow the outline to cover them.
        private float _loadF, _loadR, _loadShift;
        private int _loadFrame = -1;
        private void UpdateLoad()
        {
            if (_loadFrame == Time.frameCount) return;
            _loadFrame = Time.frameCount;
            Geom();
            _loadF = _loadR = _loadShift = 0f;
            if (Kind == MheKind.Jack) return;   // a jack's fork load is already part of its outline
            float zMin = _cz - _hf, zMax = _cz + _hf, xMax = _hr;
            bool any = false;
            foreach (var pd in GetComponentsInChildren<PalletData>())
            {
                if (pd == null) continue;
                Vector3 l = transform.InverseTransformPoint(pd.transform.position);
                if (l.y > 2.5f) continue;   // up on the mast: above every other machine's body
                zMin = Mathf.Min(zMin, l.z - 0.6f); zMax = Mathf.Max(zMax, l.z + 0.6f); xMax = Mathf.Max(xMax, Mathf.Abs(l.x - _cx) + 0.6f);
                any = true;
            }
            if (!any) return;
            float newHf = (zMax - zMin) * 0.5f, newCz = (zMin + zMax) * 0.5f;
            _loadF = newHf - _hf; _loadShift = newCz - _cz; _loadR = xMax - _hr;
        }

        // ── Live state (read by other machines) ─────────────────────────────────────────────────────
        public bool IsDriving => Time.frameCount - _tickFrame <= 2;
        public float Speed { get; private set; }
        public bool Reversing { get; private set; }
        public Vector3 MoveDir { get; private set; }
        public bool IsMovingNow => Time.unscaledTime - _lastMovedAt < 0.6f;
        public float StoppedFor => Time.unscaledTime - _lastMovedAt;
        /// <summary>Parked / working / unmanned, not waiting in traffic: others drive round it.</summary>
        public bool IsStanding => (!IsDriving || _holdingForSpot) && StoppedFor > 1.5f;
        /// <summary>Stopped at a holding line short of another machine's work spot (others may drive round it).</summary>
        private bool _holdingForSpot;
        public MheMotor BlockedByMotor { get; private set; }
        public string BlockedBy { get; private set; }
        public MheMotor WaitingForAisleOf { get; private set; }
        public string Phase { get; private set; }
        public string LastRoute { get; private set; }
        public string LastFailure { get; private set; }

        private readonly List<MheObb> _reservation = new();
        public IReadOnlyList<MheObb> Reservation => _reservation;

        private int _tickFrame = -100;
        private float _lastMovedAt;
        private Vector3 _lastPos;
        private int _token;
        private MheSeg _seg;           // the path being driven (for reservations / future poses)
        private float _segS;
        private MheSeg.SegResult _segResult;

        private static int s_nextSerial;
        private int _serial;

        private void Awake()
        {
            _serial = ++s_nextSerial;
            Slot = GetComponent<MHEOperatorSlot>();
            if (Slot != null && Slot.IsPalletJack) Kind = MheKind.Jack;
            else if (GetComponent<ReachTruckOperator>() != null) Kind = MheKind.ReachTruck;
            else Kind = MheKind.DockStocker;
            _lastPos = transform.position;
            _lastMovedAt = Time.unscaledTime - 10f;
        }

        private void OnEnable() => MheTraffic.Register(this);
        private void OnDisable() { MheTraffic.Unregister(this); _reservation.Clear(); }

        private Quaternion _lastRot = Quaternion.identity;
        private bool _havePose;

        private void LateUpdate()
        {
            Vector3 p = transform.position;
            Quaternion r = transform.rotation;
            Vector3 d = p - _lastPos; d.y = 0f;

            // ANTI-CLIP GUARD (2026-11-18): whatever moved this machine this frame (its NavMeshAgent, a task script's hand-driven move,
            // the motor), a move INTO another machine is undone; the mover simply finds itself where it was and waits. Moves that keep
            // or increase the separation are allowed (machines set down overlapping can still part). Teleports (loads, placement) pass.
            if (_havePose && Slot != null && Time.timeScale > 0f && (d.sqrMagnitude > 1e-6f || Quaternion.Angle(r, _lastRot) > 0.05f) && d.magnitude < 3f)
            {
                UpdateLoad();
                Vector3 fwdNow = FlatN(r * Vector3.forward), fwdWas = FlatN(_lastRot * Vector3.forward);
                if (fwdNow != Vector3.zero && fwdWas != Vector3.zero)
                {
                    var rawNow = OutlineAt(RootToCentre(p, fwdNow), fwdNow);
                    var rawWas = OutlineAt(RootToCentre(_lastPos, fwdWas), fwdWas);
                    var now = rawNow.Inflated(GuardBuffer);
                    foreach (var slot in MachineClearance.AllMachines())
                    {
                        if (slot == null || slot == Slot) continue;
                        var om = slot.GetComponent<MheMotor>();
                        MheObb o = om != null ? om.Outline : OutlineOf(slot);
                        if (!now.Overlaps(o)) continue;
                        // already this close before the move: allowed if it does not get closer (edge to edge; centres for a real overlap)
                        float gNow = rawNow.GapTo(o), gWas = rawWas.GapTo(o);
                        if (gWas < GuardBuffer)
                        {
                            if (gWas > 0f || gNow > 0f) { if (gNow >= gWas - 0.002f) continue; }
                            else if (Vector2.Distance(rawNow.C, o.C) >= Vector2.Distance(rawWas.C, o.C) - 0.002f) continue;
                        }
                        // undo
                        transform.SetPositionAndRotation(new Vector3(_lastPos.x, p.y, _lastPos.z), _lastRot);
                        var agent = GetComponent<NavMeshAgent>();
                        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh) { agent.nextPosition = transform.position; agent.velocity = Vector3.zero; }
                        _clipsPrevented++;
                        PushingInto = om; PushingAt = Time.unscaledTime;
                        p = transform.position; r = _lastRot; d = Vector3.zero;
                        break;
                    }
                }
            }

            if (d.magnitude > 0.003f) { _lastMovedAt = Time.unscaledTime; MoveDir = d.normalized; }
            _lastPos = p; _lastRot = r; _havePose = true;
            if (!IsDriving) { _reservation.Clear(); BlockedByMotor = null; WaitingForAisleOf = null; Speed = 0f; }
            MheTraffic.RefreshGrant(this);
            MheTraffic.RefreshSpot(this);
        }

        private int _clipsPrevented;
        private readonly List<(MheMotor who, Vector3 c, float r)> _spots = new();
        private int _spotsFrame = -1;
        /// <summary>The guard keeps every move, however it is driven, at least this far from another machine.</summary>
        private const float GuardBuffer = 0.1f;
        /// <summary>The machine this one last tried to move into (the guard undid it), and when. A machine held up by a hand-driven
        /// script or an agent that keeps pushing into it gets out of its way (MakeWay).</summary>
        public MheMotor PushingInto { get; private set; }
        public float PushingAt { get; private set; } = -100f;
        public bool IsPushing(MheMotor target) => target != null && PushingInto == target && Time.unscaledTime - PushingAt < 1.5f;
        /// <summary>How many moves the anti-clip guard has undone on this machine (a hand-driven script or agent drove into someone).</summary>
        public int ClipsPrevented => _clipsPrevented;

        private Vector3 RootToCentre(Vector3 root, Vector3 fwd) { Geom(); return root + Quaternion.LookRotation(fwd) * new Vector3(_cx, 0f, _cz); }

        /// <summary>One travel leg for a machine driven by a controller that has no motor code of its own (the dock stockers' load and
        /// offload legs): same contract as their old SeekViaNavMesh (bool callback, no straight-line fallback, navigation left off).</summary>
        public static IEnumerator Leg(Transform machine, Vector3 target, string phase, System.Action<bool> onDone, float tolerance = 0.5f)
        {
            var m = machine != null ? Ensure(machine.gameObject) : null;
            if (m == null) { onDone?.Invoke(false); yield break; }
            var g = MheGoal.To(new Vector3(target.x, machine.position.y, target.z), Vector3.zero, phase);
            g.Tolerance = tolerance;
            bool ok = false; string why = "interrupted";
            yield return m.Drive(g, r => { ok = r.Ok; why = r.Why; });
            if (!ok) Debug.LogWarning($"[MheMotor] '{machine.name}' {phase}: could not drive there - {why} (route: {m.LastRoute}).");
            onDone?.Invoke(ok);
        }

        /// <summary>Changes every time a leg starts or is stopped (a caller can tell its leg was replaced).</summary>
        public int LegToken => _token;

        /// <summary>Stops whatever leg is running (its coroutine ends on its next frame; the caller's callback is not called).</summary>
        public void Stop() { _token++; Speed = 0f; _reservation.Clear(); BlockedByMotor = null; WaitingForAisleOf = null; }

        // ══════════════════════════════════════════════════════════════════════════════════════════════
        // Driving
        // ══════════════════════════════════════════════════════════════════════════════════════════════

        /// <summary>Drives to <paramref name="goal"/>. Yield on it from the caller's coroutine. <paramref name="done"/> gets the result
        /// (not called if the leg is replaced by another <see cref="Drive"/> or <see cref="Stop"/>).</summary>
        public IEnumerator Drive(MheGoal goal, System.Action<MheResult> done = null)
        {
            int token = ++_token;
            Geom();
            UpdateLoad();
            TakeOverFromAgent();
            Phase = goal.Label;
            _tickFrame = Time.frameCount;
            _legTrafficReal = 0f;

            var route = MhePlanner.Plan(this, goal, out string why);
            if (route == null) { Finish(token, MheResult.Fail(why ?? "no route"), done); yield break; }
            LastRoute = route.Summary;

            // WORK SPOTS: someone else is working where this leg ends (their spot is within reach of ours): hold back a little way short
            // of it, out of their way, until they set off; then go. Local moves (straight, turn on the spot) don't take part.
            _legSpotValid = false;
            if (!goal.Straight && !goal.PivotOnly)
            {
                _legSpot = MhePlanner.GoalCentre(this, goal);
                _legSpotValid = true;
                _legSpotSince = Time.unscaledTime;
                MheTraffic.ReserveSpot(this, _legSpot, goal.Label);
            }
            int replans = 0;
            MheResult result = MheResult.Done;
            int segIndex = 0;
            while (segIndex < route.Segs.Count)
            {
                if (token != _token) yield break;
                var seg = route.Segs[segIndex];
                if (seg.Kind == MheSegKind.Pivot) yield return RunPivot(seg, token);
                else yield return RunPath(seg, token, goal);
                if (token != _token) yield break;
                if (_segResult == MheSeg.SegResult.Ok) { segIndex++; continue; }
                if (_segResult == MheSeg.SegResult.Replan)
                {
                    replans++;
                    var again = MhePlanner.Plan(this, goal, out why);
                    if (again == null) { result = MheResult.Fail(why ?? "no route"); break; }
                    route = again; segIndex = 0;
                    LastRoute = route.Summary + $" (replan {replans})";
                    continue;
                }
                result = MheResult.Fail(_failWhy ?? "could not finish the leg");
                break;
            }
            Finish(token, result, done);
        }

        private void Finish(int token, MheResult r, System.Action<MheResult> done)
        {
            Speed = 0f;
            _seg = null;
            _reservation.Clear();
            BlockedByMotor = null;
            WaitingForAisleOf = null;
            if (!r.Ok) LastFailure = $"{Phase}: {r.Why}";
            if (token == _token) { _tickFrame = -100; done?.Invoke(r); }
        }

        private void TakeOverFromAgent()
        {
            var agent = GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled)
            {
                if (agent.isActiveAndEnabled && agent.isOnNavMesh) { agent.isStopped = true; agent.ResetPath(); }
                agent.enabled = false;
            }
            var nav = GetComponent<AiNavigation>();
            if (nav != null && nav.enabled) { nav.CancelSeekPosition(); nav.enabled = false; }
        }

        private string _failWhy;
        private float _legTrafficReal;
        private Vector3 _legSpot;
        private bool _legSpotValid;
        private float _legSpotSince;
        /// <summary>A leg held up by traffic this long (REAL seconds, summed) gives up so the caller can decide (retry, another lane...).</summary>
        public float MaxTrafficWaitReal = 120f;
        /// <summary>Longest wait at a holding point for another machine's work spot (REAL seconds).</summary>
        public float MaxSpotWaitReal = 150f;

        // ── Path ──────────────────────────────────────────────────────────────────────────────────

        private IEnumerator RunPath(MheSeg seg, int token, MheGoal goal)
        {
            _segResult = MheSeg.SegResult.Ok;
            _failWhy = null;
            _seg = seg;
            int span = 0;
            float stallReal = 0f, trafficReal = 0f, pivotBlockedReal = 0f;
            float nextStandoff = Time.unscaledTime + 1.5f;
            Vector3 stallAnchor = Centre;
            var granted = new HashSet<MheAisle>();
            Reversing = seg.Reverse;
            if (seg.Pts.Count < 2) yield break;
            Vector3 end = seg.Pts[seg.Pts.Count - 1];
            float L = seg.Length;

            while (true)
            {
                if (token != _token) { _segResult = MheSeg.SegResult.Cancelled; yield break; }
                _tickFrame = Time.frameCount;
                float dt = Time.deltaTime;
                float rdt = Time.timeScale > 0f ? Time.unscaledDeltaTime : 0f;
                if (dt <= 0f) { yield return null; continue; }
                UpdateLoad();

                Vector3 C = Centre, H = Forward;
                float s = seg.Project(C, span, out span);
                _segS = s;
                float remaining = L - s;
                Vector3 motionDir = seg.Reverse ? -H : H;
                Vector3 toEnd = end - C; toEnd.y = 0f;
                if (remaining <= 0.03f || (remaining < 0.2f && Vector3.Dot(toEnd, motionDir) <= 0.005f))
                {
                    if (toEnd.magnitude < 0.12f && !HardBlocked(end, H, goal.IgnorePallet, out _)) Apply(end, H);
                    Speed = 0f;
                    yield break;
                }

                BuildReservation(seg, s);

                // Sense the way ahead.
                float range = Speed * Speed / (2f * Decel) + 0.9f;
                float free = Sense(seg, s, range, goal.IgnorePallet, granted, out MheMotor blkMotor, out string blkWhat, out MheMotor gateFor);
                BlockedByMotor = blkMotor; BlockedBy = blkWhat; WaitingForAisleOf = gateFor;

                // Steer: pure pursuit on the centre.
                bool inAisleSpan = seg.InAisleAt(s);
                float ld = seg.Reverse ? 1.0f : inAisleSpan ? 1.5f : Mathf.Clamp(0.7f + 0.5f * Speed, 0.7f, 1.6f);
                Vector3 look = seg.PointAt(Mathf.Min(s + ld, L));
                Vector3 D = look - C; D.y = 0f;
                if (D.sqrMagnitude < 0.0004f) D = seg.TangentAt(s);
                Vector3 want = seg.Reverse ? -D.normalized : D.normalized;
                _intent = D.normalized;
                MheAisle aisle = Kind == MheKind.Jack ? MheAisleMap.At(C, 0.2f, 0.2f) : null;
                if (aisle != null) want = ClampToAisle(want, aisle);
                float err = Vector3.SignedAngle(H, want, Vector3.up);
                float pivotAt = aisle != null ? 34f : 55f;
                bool pivot = Mathf.Abs(err) > pivotAt;

                float vmax = seg.CapAt(s) > 0f ? seg.CapAt(s) : OpenSpeed;
                if (seg.Reverse) vmax = Mathf.Min(vmax, ReverseSpeed);
                if (goal.SpeedCap > 0f) vmax = Mathf.Min(vmax, goal.SpeedCap);
                float vCurve = vmax * Mathf.Clamp(Mathf.Cos(Mathf.Min(89f, Mathf.Abs(err) * 1.5f) * Mathf.Deg2Rad), 0.15f, 1f);
                float vEnd = Mathf.Sqrt(2f * Decel * Mathf.Max(0f, remaining - 0.01f)) * 0.8f + 0.06f;
                float vSense = free <= 0.01f ? 0f : Mathf.Sqrt(2f * Decel * Mathf.Max(0f, free - 0.01f));
                float vTarget = Mathf.Min(Mathf.Min(vmax, vCurve), Mathf.Min(vEnd, vSense));
                if (pivot) vTarget = 0f;
                if (vSense < Speed) Speed = vSense;                       // never run into what the sensor sees
                else if (vTarget < Speed) Speed = Mathf.Max(vTarget, Speed - Decel * dt);
                else Speed = Mathf.Min(vTarget, Speed + Accel * dt);

                // Move in small steps; every step is checked.
                float yawRate = (pivot || Speed < 0.2f) ? PivotRate : YawRate;
                float yawTotal = Mathf.Clamp(err, -yawRate * dt, yawRate * dt);
                if (!pivot && Speed < 0.05f && Mathf.Abs(err) < 3f) yawTotal = 0f;   // standing in traffic: don't fidget
                float moveTotal = Speed * dt;
                int n = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(moveTotal / 0.06f, Mathf.Abs(yawTotal) / 3f)), 1, 10);
                bool stepBlocked = false, stepMachine = false;
                string stepWhat = null;
                for (int i = 0; i < n; i++)
                {
                    Vector3 nh = Quaternion.AngleAxis(yawTotal / n, Vector3.up) * H;
                    Vector3 nc = C + (seg.Reverse ? -nh : nh) * (moveTotal / n);
                    // a turn swings the ends: keep the 0.25 m gap through the swing too (the sweep ahead only follows the route)
                    if (Mathf.Abs(yawTotal / n) > 0.01f && SoftBlockedPose(nc, nh, C, null, out var swingWho, out string swingWhat, true))
                    {
                        stepBlocked = true; stepMachine = swingWho != null || swingWhat != null; stepWhat = swingWhat;
                        if (swingWho != null) { blkMotor = swingWho; BlockedByMotor = swingWho; BlockedBy = swingWhat; }
                        Speed = 0f;
                        break;
                    }
                    if (HardBlocked(nc, nh, goal.IgnorePallet, out stepWhat))
                    {
                        stepMachine = _lastHardWasMachine;
                        if (_lastHardMotor != null) { blkMotor = _lastHardMotor; BlockedByMotor = _lastHardMotor; BlockedBy = stepWhat; }
                        // try the turn alone, then the move alone
                        if (moveTotal > 0f && !HardBlocked(C, nh, goal.IgnorePallet, out _)) { H = nh; }
                        stepBlocked = true;
                        Speed = 0f;
                        break;
                    }
                    C = nc; H = nh;
                }
                Apply(C, H);

                // Bookkeeping: progress, traffic waits, stalls.
                bool waitingTraffic = (vSense < 0.05f && (blkMotor != null || gateFor != null || blkWhat != null)) || (stepBlocked && stepMachine);
                if (Flat(Centre - stallAnchor).magnitude > 0.08f || Mathf.Abs(yawTotal) > 0.5f || waitingTraffic) { stallAnchor = Centre; stallReal = 0f; }
                else stallReal += rdt;
                trafficReal = waitingTraffic ? trafficReal + rdt : 0f;
                if (waitingTraffic) _legTrafficReal += rdt; else if (Speed > 0.2f) _legTrafficReal = Mathf.Max(0f, _legTrafficReal - rdt * 2f);
                if (pivot && stepBlocked) pivotBlockedReal += rdt; else if (!stepBlocked) pivotBlockedReal = 0f;

                // A turn on the spot held by a rack or wall: ease forward or back a little and try again.
                if (pivotBlockedReal > 1.2f)
                {
                    pivotBlockedReal = 0f;
                    yield return Nudge(token, goal.IgnorePallet);
                    if (token != _token) { _segResult = MheSeg.SegResult.Cancelled; yield break; }
                    stallAnchor = Centre;
                }

                // Held by a rack / wall / pallet on the route itself (a tight corner): bend the next few metres of the route sideways
                // until the whole machine fits.
                bool staticHold = vSense < 0.05f && blkMotor == null && gateFor == null && blkWhat != null && blkWhat != "a person on foot";
                // Close enough: a leg that allows some slack (a park spot against a wall) ends here when only a wall / rack / pallet stops it.
                if (staticHold && goal.Tolerance > 0f && remaining <= goal.Tolerance && !seg.Reverse)
                {
                    Speed = 0f;
                    yield break;
                }
                _staticHoldReal = staticHold ? _staticHoldReal + rdt : 0f;
                if (_staticHoldReal > 0.8f)
                {
                    _staticHoldReal = 0f;
                    if (TryBendAhead(seg, s, goal.IgnorePallet)) { L = seg.Length; end = seg.Pts[seg.Pts.Count - 1]; span = 0; }
                    else if (Time.unscaledTime >= _nextStaticReplan)
                    {
                        // no bend clears it: plan afresh from where the machine now stands (the old route was made from somewhere else)
                        _nextStaticReplan = Time.unscaledTime + 3f;
                        _segResult = MheSeg.SegResult.Replan;
                        yield break;
                    }
                }

                // Standoffs and machines to drive round.
                // Something not driven by a motor (a hand-driven task move, a dock stocker's agent) keeps trying to move into this machine
                // while it waits: it cannot re-route, so this one gets out of its way, whatever the rank.
                if (trafficReal > 1.0f && Time.unscaledTime >= nextStandoff)
                {
                    MheMotor pusher = null;
                    foreach (var om in MheTraffic.Motors) if (om != null && om != this && !om.IsDriving && om.IsPushing(this)) { pusher = om; break; }
                    if (pusher != null)
                    {
                        nextStandoff = Time.unscaledTime + 2f;
                        string phaseWas = Phase;
                        yield return MakeWay(pusher, token);
                        Phase = phaseWas;
                        if (token != _token) { _segResult = MheSeg.SegResult.Cancelled; yield break; }
                        _segResult = MheSeg.SegResult.Replan;
                        yield break;
                    }
                }

                if (trafficReal > 1.2f && blkMotor != null && !_holdingForSpot && Time.unscaledTime >= nextStandoff)
                {
                    nextStandoff = Time.unscaledTime + 2f;
                    if (blkMotor.IsStanding && blkMotor.StoppedFor > 2.5f && MhePlanner.CanDriveRound(this, blkMotor))
                    {
                        _segResult = MheSeg.SegResult.Replan;   // the planner picks a line that clears the standing machine
                        yield break;
                    }
                    bool mutual = blkMotor.BlockedByMotor == this || blkMotor.WaitingForAisleOf == this || InReservationOf(blkMotor);
                    // GRIDLOCK (block 14: five machines at the dock junction, each waiting on the next): held a long time by a machine that is
                    // itself held up = a chain; in every link the lower one makes way, which opens the chain somewhere.
                    bool chain = _legTrafficReal > 12f && blkMotor.IsDriving && blkMotor.Speed < 0.05f && blkMotor.BlockedByMotor != null;
                    if ((mutual || chain) && YieldsTo(blkMotor))
                    {
                        string phaseWas = Phase;
                        yield return MakeWay(blkMotor, token);
                        Phase = phaseWas;
                        if (token != _token) { _segResult = MheSeg.SegResult.Cancelled; yield break; }
                        _segResult = MheSeg.SegResult.Replan;
                        yield break;
                    }
                }

                if (!waitingTraffic && stallReal > 7f)
                {
                    _failWhy = $"stuck {stallReal:0} s ({stepWhat ?? blkWhat ?? "no progress"})";
                    _segResult = MheSeg.SegResult.Fail;
                    yield break;
                }
                if (_legTrafficReal > MaxTrafficWaitReal)
                {
                    _failWhy = $"held up by {blkWhat ?? blkMotor?.name ?? "traffic"} for 2 minutes";
                    _segResult = MheSeg.SegResult.Fail;
                    yield break;
                }
                yield return null;
            }
        }

        private float _staticHoldReal, _nextStaticReplan;

        /// <summary>Debug: the path being driven, as "(x,z) (x,z) ...".</summary>
        public string DebugPath()
        {
            var sg = _seg;
            if (sg == null || !IsDriving) return "(not driving)";
            var sb = new System.Text.StringBuilder($"s={_segS:0.0}/{sg.Length:0.0}{(sg.Reverse ? " reverse" : "")}: ");
            foreach (var p in sg.Pts) sb.Append('(').Append(p.x.ToString("0.0")).Append(',').Append(p.z.ToString("0.0")).Append(") ");
            return sb.ToString();
        }

        /// <summary>Shifts the route sideways from here for the next few metres (fading back in) so the machine's outline clears the static
        /// thing in the way. Tries small shifts first, either side. True if a clear version was found (the segment is edited in place).</summary>
        private bool TryBendAhead(MheSeg seg, float s, Transform ignorePallet)
        {
            float L = seg.Length;
            float span = Mathf.Min(5f, L - s);
            if (span < 0.3f) return false;
            // sample the original route ahead
            var orig = new List<(Vector3 p, Vector3 t, float s)>();
            for (float d = 0f; d <= span + 0.001f; d += 0.25f)
            {
                float sd = Mathf.Min(s + d, L);
                orig.Add((seg.PointAt(sd), seg.TangentAt(sd), sd));
            }
            foreach (float off in new[] { 0.25f, -0.25f, 0.5f, -0.5f, 0.8f, -0.8f })
            {
                var pts = new List<Vector3>();
                bool ok = true;
                for (int i = 0; i < orig.Count; i++)
                {
                    float d = orig[i].s - s;
                    // in over the first metre, hold, back out over the last metre (if the route continues)
                    float w = Mathf.Clamp01(d / 1.0f);
                    if (s + span < L - 0.05f) w = Mathf.Min(w, Mathf.Clamp01((span - d) / 1.0f));
                    Vector3 n = Vector3.Cross(Vector3.up, orig[i].t);
                    Vector3 p = orig[i].p + n * (off * w);
                    Vector3 h = seg.Reverse ? -orig[i].t : orig[i].t;
                    if (i > 0 && StaticBlocked(p, h, ignorePallet, out _)) { ok = false; break; }
                    var ma = Kind == MheKind.Jack ? MheAisleMap.At(p, 0.2f, 0.2f) : null;
                    if (ma != null && (ma.Lateral(p) - HalfWidth < ma.LatMin + 0.05f || ma.Lateral(p) + HalfWidth > ma.LatMax - 0.05f)) { ok = false; break; }
                    if (!MheNav.Sample(p, 0.4f, out _, false)) { ok = false; break; }
                    pts.Add(p);
                }
                if (!ok || pts.Count < 2) continue;
                // splice: route up to s, the bent stretch, the rest after s+span
                var newPts = new List<Vector3>(); var caps = new List<float>(); var inA = new List<bool>();
                for (int i = 0; i < seg.Pts.Count; i++) if (seg.LenAt(i) < s - 0.01f) { newPts.Add(seg.Pts[i]); caps.Add(seg.Cap[i]); inA.Add(seg.InAisle[i]); }
                foreach (var p in pts) { newPts.Add(p); caps.Add(seg.CapAt(s)); inA.Add(seg.InAisleAt(s)); }
                for (int i = 0; i < seg.Pts.Count; i++) if (seg.LenAt(i) > s + span + 0.01f) { newPts.Add(seg.Pts[i]); caps.Add(seg.Cap[i]); inA.Add(seg.InAisle[i]); }
                // keep gate distances right: shift them by the change in length before them (small); simplest is to keep them
                seg.Pts.Clear(); seg.Cap.Clear(); seg.InAisle.Clear();
                for (int i = 0; i < newPts.Count; i++) seg.Add(newPts[i], caps[i], inA[i]);
                return true;
            }
            return false;
        }

        // ── Pivot ─────────────────────────────────────────────────────────────────────────────────

        private IEnumerator RunPivot(MheSeg seg, int token)
        {
            _segResult = MheSeg.SegResult.Ok;
            _failWhy = null;
            _seg = null;
            Vector3 goalH = FlatN(seg.Heading);
            if (goalH == Vector3.zero) yield break;
            float sign = 0f, blockedReal = 0f, trafficReal = 0f;
            bool flipped = false;
            float startErr = Vector3.SignedAngle(Forward, goalH, Vector3.up);
            if (Mathf.Abs(startErr) < 0.3f) yield break;
            // A machine in an aisle squares up only a little; a big turn there is the planner's to make, and a jack never makes it.
            while (true)
            {
                if (token != _token) { _segResult = MheSeg.SegResult.Cancelled; yield break; }
                _tickFrame = Time.frameCount;
                float dt = Time.deltaTime, rdt = Time.timeScale > 0f ? Time.unscaledDeltaTime : 0f;
                if (dt <= 0f) { yield return null; continue; }
                UpdateLoad();
                Vector3 C = Centre, H = Forward;
                Vector3 root = transform.position;
                float err = Vector3.SignedAngle(H, goalH, Vector3.up);
                if (Mathf.Abs(err) <= 0.3f) { if (seg.AboutRoot) transform.SetPositionAndRotation(root, Quaternion.LookRotation(goalH)); else Apply(C, goalH); yield break; }
                if (sign == 0f) sign = Mathf.Sign(err);
                float left = sign > 0f ? (err >= 0f ? err : err + 360f) : (err <= 0f ? -err : 360f - err);
                float step = Mathf.Min(PivotRate * dt, left);
                Vector3 nh = Quaternion.AngleAxis(sign * step, Vector3.up) * H;
                // Sweep ahead a little further than this frame so the forks never swing into someone.
                Vector3 ahead = Quaternion.AngleAxis(sign * Mathf.Min(left, step + 12f), Vector3.up) * H;
                // about the root: the centre swings round the root with the heading
                Vector3 Cn = seg.AboutRoot ? RootToCentre(root, nh) : C, Ca = seg.AboutRoot ? RootToCentre(root, ahead) : C;
                _reservation.Clear(); _reservation.Add(OutlineAt(Ca, ahead).Inflated(MheTraffic.Gap)); _reservation.Add(OutlineAt(C, H).Inflated(MheTraffic.Gap));
                bool soft = SoftBlockedPose(Ca, ahead, C, null, out var bm, out string bw, false);
                bool hard = HardBlocked(Cn, nh, null, out string hw);
                if (hard || soft)
                {
                    BlockedByMotor = bm; BlockedBy = hw ?? bw;
                    if (hard && bm == null)
                    {
                        // A rack or wall in the swing: the other way round once (only for a near U-turn), then give up.
                        blockedReal += rdt;
                        if (!flipped && Mathf.Abs(startErr) > 150f) { flipped = true; sign = -sign; blockedReal = 0f; }
                        else if (blockedReal > 2.5f) { _failWhy = $"turn blocked by {hw}"; _segResult = MheSeg.SegResult.Fail; yield break; }
                    }
                    else
                    {
                        trafficReal += rdt;
                        if (trafficReal > 60f) { _failWhy = $"turn held up by {bw ?? hw} for a minute"; _segResult = MheSeg.SegResult.Fail; yield break; }
                    }
                    yield return null;
                    continue;
                }
                blockedReal = 0f; trafficReal = 0f; BlockedByMotor = null; BlockedBy = null;
                if (seg.AboutRoot)
                {
                    bool last = step >= left - 0.01f;
                    transform.SetPositionAndRotation(root, Quaternion.LookRotation(last ? goalH : nh));
                    if (last) yield break;
                    yield return null;
                    continue;
                }
                Apply(C, nh);
                if (step >= left - 0.01f) { Apply(C, goalH); yield break; }
                yield return null;
            }
        }

        // ── Getting out of someone's way ─────────────────────────────────────────────────────────

        /// <summary>Who gives way in a standoff: the lower rank; between equals, the one that is driving INTO the other (a follower whose
        /// leader's forks swing back at it in a turn backs off, not the leader); then the later machine.</summary>
        private bool YieldsTo(MheMotor other)
        {
            if (other == null) return false;
            if (Priority != other.Priority) return Priority < other.Priority;
            Vector3 toOther = Flat(other.Centre - Centre);
            if (toOther.sqrMagnitude > 1e-4f)
            {
                toOther.Normalize();
                float mine = Vector3.Dot(_intent, toOther), theirs = Vector3.Dot(other._intent, -toOther);
                if (Mathf.Abs(mine - theirs) > 0.25f) return mine > theirs;
            }
            return _serial > other._serial;
        }

        /// <summary>The way the machine wants to move this frame (its route's direction at its position).</summary>
        private Vector3 _intent;
        public Vector3 Intent => IsDriving ? _intent : Vector3.zero;

        private bool InReservationOf(MheMotor other)
        {
            if (other == null || other.Priority <= Priority) return false;
            var me = Outline;
            foreach (var r in other.Reservation) if (r.Overlaps(me)) return true;
            return false;
        }

        /// <summary>Backs off (or pulls forward) along its own axis until it is out of <paramref name="other"/>'s way, waits for it to go by,
        /// then hands back to the leg (which re-plans from where it now stands).</summary>
        private IEnumerator MakeWay(MheMotor other, int token)
        {
            Vector3 C = Centre, H = Forward;
            var future = other.FuturePoses(9f);
            float awaySign = Vector3.Dot(C - other.Centre, H) >= 0f ? 1f : -1f;
            float[] dists = { 1.2f, 2.0f, 3.0f };
            bool inAisle = MheAisleMap.At(C, 0.2f, 0.2f) != null;
            Vector3 chosen = C; bool found = false;
            Vector3 moveHeading = H;
            foreach (float sgn in new[] { awaySign, -awaySign })
            {
                foreach (float d in dists)
                {
                    if (inAisle && Kind == MheKind.Jack && sgn < 0f && d > MhePlanner.JackReverseInAisleMax + 1.5f) continue;
                    Vector3 c2 = C + H * (sgn * d);
                    if (!StraightClear(C, c2, H, other)) break;
                    var o2 = OutlineAt(c2, H).Inflated(MheTraffic.Gap);
                    bool inTheWay = false;
                    foreach (var f in future) if (f.Overlaps(o2)) { inTheWay = true; break; }
                    if (inTheWay) continue;
                    chosen = c2; found = true; break;
                }
                if (found) break;
            }
            // Open floor: pull aside, off the other machine's line (a diagonal forward or back, either side).
            if (!found && !inAisle)
            {
                Vector3 R = Vector3.Cross(Vector3.up, H);
                foreach (float side in new[] { 1f, -1f })
                {
                    foreach (float fw in new[] { -awaySign * -1.4f, awaySign * -1.4f })
                    {
                        foreach (float lat in new[] { 1.8f, 2.6f })
                        {
                            Vector3 c2 = C + H * fw + R * (side * lat);
                            Vector3 dir = Flat(c2 - C).normalized;
                            Vector3 faceDuring = fw < 0f ? -dir : dir;
                            if (!StraightClear(C, c2, faceDuring, other)) continue;
                            var o2 = OutlineAt(c2, faceDuring).Inflated(MheTraffic.Gap);
                            bool inTheWay = false;
                            foreach (var f in future) if (f.Overlaps(o2)) { inTheWay = true; break; }
                            if (inTheWay) continue;
                            chosen = c2; moveHeading = faceDuring; found = true; break;
                        }
                        if (found) break;
                    }
                    if (found) break;
                }
            }
            Phase = $"making way for {other.name}";
            if (!found)
            {
                for (float w = 0f; w < 0.6f && token == _token; w += Time.unscaledDeltaTime) { _tickFrame = Time.frameCount; yield return null; }
                yield break;
            }
            bool reverse = Vector3.Dot(chosen - C, moveHeading) < 0f;
            var seg = new MheSeg { Kind = MheSegKind.Path, Reverse = reverse, Heading = moveHeading, Label = Phase };
            seg.Add(C, ReverseSpeed, inAisle);
            seg.Add(chosen, ReverseSpeed, inAisle);
            yield return RunPath(seg, token, default);
            if (token != _token) yield break;
            // Wait for it to go by (or give up after a while: it may be waiting on something else).
            for (float w = 0f; w < 15f && token == _token; w += Time.timeScale > 0f ? Time.unscaledDeltaTime : 0f)
            {
                _tickFrame = Time.frameCount;
                if (other == null) break;
                if (!other.IsDriving)
                {
                    // a hand-driven / agent move: give it time to get past (it is moving, or still pushing toward where we were)
                    if (w > 1.5f && !other.IsMovingNow && !other.IsPushing(this)) break;
                    yield return null;
                    continue;
                }
                var me = Outline.Inflated(MheTraffic.Gap);
                bool still = false;
                foreach (var f in other.FuturePoses(7f)) if (f.Overlaps(me)) { still = true; break; }
                if (!still && Flat(other.Centre - Centre).magnitude > HalfLength + other.HalfLength + 0.5f) break;
                yield return null;
            }
        }

        /// <summary>A turn on the spot is held by a rack or wall: move a little along the machine's own axis (forward first, then back).</summary>
        private IEnumerator Nudge(int token, Transform ignorePallet)
        {
            Vector3 C = Centre, H = Forward;
            foreach (float d in new[] { 0.6f, -0.6f, 1.2f, -1.2f })
            {
                Vector3 c2 = C + H * d;
                if (!StraightClear(C, c2, H, null)) continue;
                var seg = new MheSeg { Kind = MheSegKind.Path, Reverse = d < 0f, Heading = H, Label = Phase + " (easing)" };
                seg.Add(C, 0.6f, false);
                seg.Add(c2, 0.6f, false);
                yield return RunPath(seg, token, new MheGoal { IgnorePallet = ignorePallet });
                yield break;
            }
        }

        private bool StraightClear(Vector3 from, Vector3 to, Vector3 h, MheMotor exempt)
        {
            float len = Flat(to - from).magnitude;
            for (float t = 0.2f; t <= len + 0.001f; t += 0.2f)
            {
                Vector3 c = Vector3.Lerp(from, to, Mathf.Clamp01(t / Mathf.Max(0.001f, len)));
                if (HardBlocked(c, h, null, out _)) return false;
                if (SoftBlockedPose(c, h, from, exempt, out _, out _, true)) return false;
            }
            return MheNav.Sample(to, 0.6f, out _, false);
        }

        /// <summary>The poses this machine is about to drive through (outline + gap), up to <paramref name="len"/> metres along its route.</summary>
        public List<MheObb> FuturePoses(float len)
        {
            var list = new List<MheObb>();
            list.Add(Outline.Inflated(MheTraffic.Gap));
            if (!IsDriving || _seg == null) return list;
            float L = _seg.Length;
            for (float d = 0.5f; d <= len && _segS + d <= L + 0.01f; d += 0.5f)
            {
                float sd = Mathf.Min(_segS + d, L);
                Vector3 t = _seg.TangentAt(sd);
                list.Add(OutlineAt(_seg.PointAt(sd), _seg.Reverse ? -t : t).Inflated(MheTraffic.Gap));
            }
            return list;
        }

        private void BuildReservation(MheSeg seg, float s)
        {
            _reservation.Clear();
            _reservation.Add(Outline.Inflated(MheTraffic.Gap));
            float len = 1.2f + Speed * 1.6f;
            float L = seg.Length;
            for (float d = 0.5f; d <= len && s + d <= L + 0.01f; d += 0.5f)
            {
                float sd = Mathf.Min(s + d, L);
                Vector3 t = seg.TangentAt(sd);
                _reservation.Add(OutlineAt(seg.PointAt(sd), seg.Reverse ? -t : t).Inflated(MheTraffic.Gap));
            }
        }

        // ══════════════════════════════════════════════════════════════════════════════════════════════
        // Sensing
        // ══════════════════════════════════════════════════════════════════════════════════════════════

        /// <summary>Free distance along the route before the machine's outline (+gap) would touch anything. Also holds the machine at an
        /// aisle mouth it may not drive into yet.</summary>
        private float Sense(MheSeg seg, float s, float range, Transform ignorePallet, HashSet<MheAisle> granted,
                            out MheMotor blkMotor, out string blkWhat, out MheMotor gateFor)
        {
            blkMotor = null; blkWhat = null; gateFor = null;
            Vector3 C = Centre, H = Forward;
            float L = seg.Length;
            float free = range;
            const float Step = 0.2f;

            // Aisle mouths ahead.
            foreach (var g in seg.Gates)
            {
                if (g.aisle == null || granted.Contains(g.aisle)) continue;
                float toGate = g.s - s;
                if (toGate < -0.5f || toGate > range + 1.5f) continue;
                if (MheTraffic.MayEnterAisle(this, g.aisle, g.dir, out var waitFor)) { granted.Add(g.aisle); continue; }
                float f = Mathf.Max(0f, toGate);
                if (f < free) { free = f; gateFor = waitFor; blkMotor = waitFor; blkWhat = $"waiting to enter {g.aisle.Prefix.TrimEnd('-')} ({waitFor?.name})"; }
            }

            bool spotHold = false;
            // Someone else's work spot overlaps where this leg ends and it is theirs (they are there, or nearer): a stop line short of it.
            // (no time limit: going in anyway while the owner is still working there is how a reach truck put a pallet down on a jack;
            //  a leg held too long fails on MaxTrafficWaitReal instead and the task goes back on the queue)
            if (_legSpotValid && MheTraffic.SpotContest(this, _legSpot, out var owner, out Vector3 theirs, out float keepAway, out string theirLabel))
            {
                float line = -1f;
                if (Flat(C - theirs).magnitude < keepAway) line = 0f;
                else
                    for (float d = 0f; d <= free + keepAway && s + d <= L + 0.001f; d += 0.25f)
                        if (Flat(seg.PointAt(Mathf.Min(s + d, L)) - theirs).magnitude < keepAway) { line = Mathf.Max(0f, d - 0.25f); break; }
                if (line >= 0f && line < free)
                {
                    free = line; blkMotor = owner; gateFor = null;
                    blkWhat = $"holding back for {owner.name} at {theirLabel}";
                    spotHold = line < 0.05f;
                }
            }
            _holdingForSpot = spotHold;

            for (float d = 0f; d <= free + 0.001f; d += Step)
            {
                float sd = Mathf.Min(s + d, L);
                Vector3 c = d <= 0.001f ? C : seg.PointAt(sd);
                Vector3 t = seg.TangentAt(sd);
                Vector3 h = d <= 0.001f ? H : (seg.Reverse ? -t : t);
                if (SoftBlockedPose(c, h, C, null, out var m, out string what, false) || (d > 0.001f && StaticBlocked(c, h, ignorePallet, out what, true)))
                {
                    float f = Mathf.Max(0f, d - Step);
                    if (f < free || blkWhat == null) { free = Mathf.Min(free, f); blkMotor = m; blkWhat = what; }
                    break;
                }
                if (s + d >= L) break;
            }
            return free;
        }

        /// <summary>Another machine (+gap), a higher-ranked machine's reserved way, or a person, at this pose. Things already that close at
        /// <paramref name="now"/> only count if this pose is closer to them still.</summary>
        private bool SoftBlockedPose(Vector3 c, Vector3 h, Vector3 now, MheMotor exempt, out MheMotor who, out string what, bool ignoreReservations)
        {
            who = null; what = null;
            var mineBase = OutlineAt(c, h).Inflated(MheTraffic.Gap);
            var mineNowBase = OutlineAt(now, Forward).Inflated(MheTraffic.Gap);
            foreach (var slot in MachineClearance.AllMachines())
            {
                if (slot == null || slot == Slot) continue;
                var om = slot.GetComponent<MheMotor>();
                if (om != null && om == exempt) continue;
                MheObb o = om != null ? om.Outline : OutlineOf(slot);
                // Following distance: a machine that is driving gets more room than one standing, so a leader has space to swing its
                // forks round a corner and nobody has to back out of a nose-to-tail jam.
                float extra = om != null && om.IsDriving && om.IsMovingNow ? MheTraffic.Headway : 0f;
                // and its next step: both machines move each frame, and at high game speed a frame's step is a good part of the gap
                if (om != null && om.IsMovingNow) extra += om.Speed * Time.deltaTime * 2f;
                var mine = extra > 0f ? mineBase.Inflated(extra) : mineBase;
                var mineNow = extra > 0f ? mineNowBase.Inflated(extra) : mineNowBase;
                if (Flat(new Vector3(o.C.x, 0f, o.C.y) - c).magnitude > _hf + o.HalfF + 2f) continue;
                if (!mine.Overlaps(o)) continue;
                if (mineNow.Overlaps(o))
                {
                    // already this close: only getting closer counts (the real edge-to-edge gap, not the centres: a long machine
                    // sliding sideways toward another can move its centre away while its side closes in)
                    if (OutlineAt(c, h).GapTo(o) >= OutlineAt(now, Forward).GapTo(o) - 0.005f) continue;
                }
                who = om; what = slot.name;
                return true;
            }
            if (!ignoreReservations)
            {
                foreach (var om in MheTraffic.Motors)
                {
                    if (om == null || om == this || om == exempt || om.Priority <= Priority || !om.IsDriving) continue;
                    var res = om.Reservation;
                    bool insideNow = false;
                    for (int i = 0; i < res.Count; i++) if (res[i].Overlaps(OutlineAt(now, Forward))) { insideNow = true; break; }
                    if (insideNow) continue;   // already in its way: keep going and get out of it
                    var raw = OutlineAt(c, h);
                    for (int i = 0; i < res.Count; i++)
                        if (res[i].Overlaps(raw)) { who = om; what = $"giving way to {om.name}"; return true; }
                }
            }
            var mp = OutlineAt(c, h);
            var mpNow = OutlineAt(now, Forward);
            if (!ignoreReservations)
            {
                // Keep out of the circle a machine working at its spot sweeps when it turns there (block 16: a jack drove beside one
                // turning at a staging pivot and the two locked for 150 s).
                if (_spotsFrame != Time.frameCount) { _spotsFrame = Time.frameCount; MheTraffic.WorkingSpots(_spots); }
                foreach (var sp in _spots)
                {
                    if (sp.who == this || sp.who == exempt) continue;
                    if (_legSpotValid && Flat(_legSpot - sp.c).magnitude < 0.5f) continue;   // it is my spot too (the contest rule handles that)
                    Vector2 sc = new Vector2(sp.c.x, sp.c.z);
                    if (mp.DistanceTo(sc) >= sp.r) continue;
                    if (mpNow.DistanceTo(sc) < sp.r) continue;   // already inside: get out of it
                    who = sp.who; what = $"keeping clear of {sp.who.name} working at its spot";
                    return true;
                }
            }
            foreach (var p in MheTraffic.People)
            {
                Vector2 pp = new Vector2(p.x, p.z);
                if (Mathf.Abs(p.y - c.y) > 1.5f) continue;
                if (mp.DistanceTo(pp) >= MheTraffic.PersonRadius + MheTraffic.PersonGap) continue;
                if (mpNow.DistanceTo(pp) < MheTraffic.PersonRadius + MheTraffic.PersonGap) continue;   // boarding / stepping off
                what = "a person on foot";
                return true;
            }
            return false;
        }

        /// <summary>For the planner: the machine's outline at this pose is clear of racks, walls and floor pallets (grown by <paramref name="margin"/>).</summary>
        public bool PoseClearOfStatics(Vector3 centre, Vector3 heading, float margin = 0f)
        {
            Geom();
            float keep = _extraMargin;
            _extraMargin = margin;
            bool blocked = StaticBlocked(centre, FlatN(heading) == Vector3.zero ? Forward : FlatN(heading), null, out _);
            _extraMargin = keep;
            return !blocked;
        }
        private float _extraMargin;

        /// <summary>Racks, walls and floor pallets at this pose (the machine's real outline, slightly shrunk).</summary>
        private bool StaticBlocked(Vector3 c, Vector3 h, Transform ignorePallet, out string what, bool exemptTouching = false)
        {
            what = null;
            if (exemptTouching) RefreshTouching();
            var o = OutlineAt(c, h).Inflated(_extraMargin);
            float y = transform.position.y;
            // Racks and walls: points round the outline.
            if (!(exemptTouching && _touchSolid))
            {
                var shrunk = o.Inflated(-0.08f);
                for (int i = 0; i < 4; i++)
                {
                    Vector3 a = shrunk.Corner(i, y), b = shrunk.Corner((i + 1) % 4, y);
                    float len = Flat(b - a).magnitude;
                    int n = Mathf.Max(1, Mathf.CeilToInt(len / 0.45f));
                    for (int k = 0; k < n; k++)
                    {
                        Vector3 p = Vector3.Lerp(a, b, k / (float)n);
                        if (MachineClearance.SolidAt(p, out string w)) { what = w; return true; }
                    }
                }
            }
            var po = o.Inflated(-0.05f);
            foreach (var p in MachineClearance.AllFloorPallets())
            {
                if (p == null || p == ignorePallet || p.IsChildOf(transform)) continue;
                if (exemptTouching && _touchPallets.Contains(p)) continue;
                if (Mathf.Abs(p.position.y - y) > 1.2f) continue;
                Vector3 d = p.position - c; d.y = 0f;
                if (d.magnitude > _hf + 1.5f) continue;
                var pb = MheObb.From(p.position, p.forward, MachineClearance.PalletHalfSize, MachineClearance.PalletHalfSize);
                if (po.Overlaps(pb)) { what = $"a pallet ({p.name}) at ({p.position.x:0.0},{p.position.z:0.0}) [me at ({c.x:0.0},{c.z:0.0}) facing {Mathf.RoundToInt(Vector3.SignedAngle(Vector3.forward, h, Vector3.up))}]"; return true; }
            }
            return false;
        }

        // What the machine is touching where it stands now (a reach truck with its forks in a lane, a dock stocker set down against a
        // pallet): it may drive out of those, so the sensor ignores them; everything else still counts.
        private readonly HashSet<Transform> _touchPallets = new();
        private bool _touchSolid;
        private int _touchFrame = -1;
        private void RefreshTouching()
        {
            if (_touchFrame == Time.frameCount) return;
            _touchFrame = Time.frameCount;
            _touchPallets.Clear();
            _touchSolid = false;
            var o = Outline;
            float y = transform.position.y;
            var shrunk = o.Inflated(-0.08f);
            for (int i = 0; i < 4 && !_touchSolid; i++)
            {
                Vector3 a = shrunk.Corner(i, y), b = shrunk.Corner((i + 1) % 4, y);
                int n = Mathf.Max(1, Mathf.CeilToInt(Flat(b - a).magnitude / 0.45f));
                for (int k = 0; k < n; k++) if (MachineClearance.SolidAt(Vector3.Lerp(a, b, k / (float)n), out _)) { _touchSolid = true; break; }
            }
            var po = o.Inflated(-0.05f);
            foreach (var p in MachineClearance.AllFloorPallets())
            {
                if (p == null || p.IsChildOf(transform)) continue;
                Vector3 d = p.position - o.Corner(0, y); d.y = 0f;
                if (d.magnitude > _hf * 2f + 2f) continue;
                if (po.Overlaps(MheObb.From(p.position, p.forward, MachineClearance.PalletHalfSize, MachineClearance.PalletHalfSize))) _touchPallets.Add(p);
            }
        }

        /// <summary>The last check before every step: would the machine at this pose overlap another machine (no gap here), a rack, a wall
        /// or a floor pallet? Moving away from something it already overlaps is allowed.</summary>
        private bool _lastHardWasMachine;
        private MheMotor _lastHardMotor;

        private bool HardBlocked(Vector3 c, Vector3 h, Transform ignorePallet, out string what)
        {
            what = null;
            _lastHardWasMachine = false;
            _lastHardMotor = null;
            var mine = OutlineAt(c, h);
            var now = Outline;
            foreach (var slot in MachineClearance.AllMachines())
            {
                if (slot == null || slot == Slot) continue;
                var om = slot.GetComponent<MheMotor>();
                MheObb o = om != null ? om.Outline : OutlineOf(slot);
                if (!mine.Overlaps(o)) continue;
                if (now.Overlaps(o) && Vector2.Distance(new Vector2(c.x, c.z), o.C) >= Vector2.Distance(now.C, o.C) - 0.002f) continue;
                what = slot.name;
                _lastHardWasMachine = true;
                _lastHardMotor = om;
                return true;
            }
            // Statics: only what the machine is not already touching (a machine set down overlapping a rack can still drive out).
            return StaticBlocked(c, h, ignorePallet, out what, true);
        }

        /// <summary>Outline of any machine, motor or not (footprint from its renderers, carried pallets included).</summary>
        public static MheObb OutlineOf(MHEOperatorSlot slot)
        {
            var m = slot.GetComponent<MheMotor>();
            if (m != null) { m.UpdateLoad(); return m.Outline; }
            var f = MachineClearance.FootprintOf(slot);
            Vector3 s = slot.transform.lossyScale;
            Vector3 c = slot.transform.TransformPoint(new Vector3((f.MinX + f.MaxX) * 0.5f, 0f, (f.MinZ + f.MaxZ) * 0.5f));
            Vector3 fwd = slot.transform.forward; fwd.y = 0f;
            return MheObb.From(c, fwd, (f.MaxZ - f.MinZ) * 0.5f * s.z, (f.MaxX - f.MinX) * 0.5f * s.x);
        }

        // ══════════════════════════════════════════════════════════════════════════════════════════════

        private static Vector3 ClampToAisle(Vector3 want, MheAisle a)
        {
            Vector3 axis = Vector3.Dot(want, a.Travel) >= 0f ? a.Travel : -a.Travel;
            float ang = Vector3.SignedAngle(axis, want, Vector3.up);
            float cap = MheTraffic.AisleMaxDeg;
            if (Mathf.Abs(ang) > cap) want = Quaternion.AngleAxis(Mathf.Sign(ang) * cap, Vector3.up) * axis;
            return want;
        }

        private void Apply(Vector3 centre, Vector3 heading)
        {
            heading = FlatN(heading);
            if (heading == Vector3.zero) heading = Forward;
            float y = transform.position.y;
            Vector3 root = RootFromCentre(centre, heading);
            root.y = y;
            transform.SetPositionAndRotation(root, Quaternion.LookRotation(heading, Vector3.up));
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
        private static Vector3 FlatN(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.zero; }
    }
}
