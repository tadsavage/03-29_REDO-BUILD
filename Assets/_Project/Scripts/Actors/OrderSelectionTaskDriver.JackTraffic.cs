using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using GameCore.Inventory;

namespace GameCore.Actors
{
    /// <summary>
    /// PALLET JACK TRAFFIC (Tad, 2026-11-07).
    ///
    /// 1. NEVER DRIVE THROUGH ANYTHING. Every jack move (turning in place, straight moves, the NavMesh legs) goes through
    ///    <see cref="MachineClearance"/>: a step that would push the jack's body or forks into a rack, wall, other machine or floor
    ///    pallet is not taken. A turn that can't be made either way round is given up (the jack keeps its heading); a NavMesh leg
    ///    stopped by a solid creeps straight on / back a little to swing wider, then gives up rather than drive through.
    ///
    /// 2. QUEUE, DON'T WEAVE. Pulled up behind another manned jack in its lane of travel (the other lane being free means it is off
    ///    to the side, and the jack just passes): wait, for as long as it takes. The old code waited 6 s and then drove anyway, which
    ///    is where the weaving, the turning round and the driving through came from. Two jacks that each block the other
    ///    (converging on one aisle mouth) no longer both freeze: the one with more room ahead goes, the other waits.
    ///
    /// 3. STEP OFF AND WALK. Queued for 5 REAL seconds on the way to a bay: the selector leaves the jack where it stands, steps off
    ///    and walks to the slot to pick, carrying each case back to the jack. Never further than 10 m of WALKING from the jack
    ///    (about 7-8 bays): keep picking rather than stand there, but don't hike from one end of an aisle to the other. The walk
    ///    follows the walking NavMesh (round racks and walls) and detours round other jacks and pallets. When the way clears, or the
    ///    next pick is beyond 10 m, the selector steps back on and drives.
    /// </summary>
    public partial class OrderSelectionTaskDriver
    {
        // ── Tunables ────────────────────────────────────────────────────────────────────────────────
        private const float QueueWaitRealSeconds = 5f;      // queued this long (REAL seconds, Tad) -> step off and walk to pick
        private const float QueueWalkMax = 10f;             // ...never further than this WALKING distance from the jack (~7-8 bays)
        private const float QueueExemptMaxGameMinutes = 120f;   // sanity cap on the no-progress exemption while queued
        private const float HeldResetRealSeconds = 0.5f;    // un-held this long before the queue timer resets (no flicker)
        private const float TurnBlockedGiveUpReal = 1.0f;   // a turn that would swing the forks into something: give up after this
        private const float SolidCreepAfterReal = 1.0f;     // NavMesh leg stopped by a solid: try a small straight creep after this
        private const float SolidGiveUpReal = 8f;           // ...and end the leg / move after this
        private const int   MaxCreepsPerLeg = 12;
        private const float CreepStep = 0.3f;
        private const float PersonRadius = 0.25f;           // a walking selector's clearance from jacks and pallets
        private const float QueueDropOff = 0.95f;           // where the selector stands beside the pallet to set a case down

        // ── Traffic state ───────────────────────────────────────────────────────────────────────────
        private MHEOperatorSlot _heldBehindJack;   // the manned jack this one is queued behind (null = not held by a jack)
        private string _heldBySolid;               // what the guard stopped this jack on (null = nothing)
        private float _heldRealSeconds, _heldGameMinutes, _notHeldReal;
        private Vector3 _travelDir;                // where this jack is driving right now (zero = parked / turning / picking)
        private LocationData _queueProbe;          // set for bay legs: the slot the selector would walk to if the jack queues
        private bool _jackLegQueued;               // the leg ended early: parked in a queue, the selector walks
        private bool _queueAbortBySolid;           // ...and it was a solid, not a jack, that held it
        private bool _queuedStop;                  // picking from a queue stop (walk reach QueueWalkMax)
        private bool _queueBySolid;
        private Vector3 _queuedAt;
        private float _nextQueueProbeReal;

        private static float RealDt => Time.timeScale > 0f ? Time.unscaledDeltaTime : 0f;

        // ══════════════════════════════════════════════════════════════════════════════════════════════
        // Held / queued bookkeeping
        // ══════════════════════════════════════════════════════════════════════════════════════════════

        private void NoteHeld(bool held, MHEOperatorSlot behind, string solid)
        {
            var ta = JackTrafficAgent();
            if (ta != null) { if (held) ta.NoteHeld(behind, behind == null ? (solid ?? "something") : null); else ta.NoteClear(false); }
            if (held)
            {
                _notHeldReal = 0f;
                _heldRealSeconds += RealDt;
                _heldGameMinutes += GameClock.DeltaMinutes;
                _heldBehindJack = behind;
                _heldBySolid = behind == null ? (solid ?? "something") : null;
                return;
            }
            _notHeldReal += RealDt;
            if (_notHeldReal >= HeldResetRealSeconds) ClearHeld();
        }

        private void ClearHeld()
        {
            _heldBehindJack = null; _heldBySolid = null; _yieldingTo = null;
            _heldRealSeconds = 0f; _heldGameMinutes = 0f; _notHeldReal = 0f;
            _yieldTried = false;
        }

        // ══════════════════════════════════════════════════════════════════════════════════════════════
        // Equipment traffic (EquipmentTraffic.cs): reach drivers have right of way, aisle exits, jam yielding
        // ══════════════════════════════════════════════════════════════════════════════════════════════

        private const float YieldReverseAfterReal = 1.5f;   // a higher-priority machine has been stuck in front of us this long: back off if safe
        private const float AisleExitLook = 1.0f;           // "this step leaves the aisle" = the point this far ahead is outside it
        private bool _yieldTried;
        private string _exitCheckedPrefix;
        private float _exitCheckedAt, _nextExitPoll;

        private EquipmentTrafficAgent JackTrafficAgent() => _jack != null ? _jack.GetComponent<EquipmentTrafficAgent>() : null;

        /// <summary>Who holds this jack up, beyond other jacks: a reach truck / dock stocker in its lane (it always gives way to those,
        /// even before touching), or the machine the solid guard stopped it on. <paramref name="yieldHold"/>: a higher-priority machine is in the lane.</summary>
        private bool TrafficBlocker(Vector3 dir, bool solidHold, ref MHEOperatorSlot blocker, out bool yieldHold)
        {
            yieldHold = false;
            if (_jack == null) return false;
            if (blocker == null && EquipmentTraffic.FindInLane(_jack, dir, EquipmentTraffic.LaneAhead, EquipmentTraffic.LaneSide, true, out var hi))
            {
                blocker = hi; yieldHold = true;
            }
            else if (blocker == null && solidHold)
            {
                var m = MachineClearance.LastMachineHit;
                if (m != null && m != _jack && m.IsOccupied) blocker = m;
            }
            return blocker != null;
        }

        /// <summary>Selectors yield to reach drivers: after <see cref="YieldReverseAfterReal"/> in front of a stationary higher-priority machine,
        /// back up 0.5-1 m if that is safe (one maneuver at a time, MachineClearance + NavMesh checked); otherwise stay put and let it get round.</summary>
        private IEnumerator JackYieldReverse(Vector3 travelDir, MHEOperatorSlot to)
        {
            if (_jack == null || _yieldTried || to == null) yield break;
            if (_heldRealSeconds < YieldReverseAfterReal) yield break;
            var ta = to.GetComponent<EquipmentTrafficAgent>();
            if (ta != null && ta.IsMoving) yield break;   // it is going past: just wait
            if (!EquipmentTraffic.TryBeginManeuver(_jack)) yield break;
            _yieldTried = true;
            Vector3 back = -Flat(travelDir, 0f).normalized;
            float d = EquipmentTraffic.PickSafeReverse(_jack, back, EquipmentTraffic.SelectorReverse);
            if (d > 0f)
            {
                yield return EquipmentTraffic.ReverseStraight(_jack, back, d, JackReverseSpeed, _jackAgent);
            }
            else
            {
                ThrottledLog.Warn("jack-yield-" + name, $"[EquipmentTraffic] '{name}' pallet jack cannot reverse safely for {to.name} - staying put so it can get round.", 30f);
            }
            EquipmentTraffic.EndManeuver(_jack);
        }

        /// <summary>True (at most 5 times a second) when this step takes the jack out of the end of an aisle it has not just been checked at.</summary>
        private bool AisleExitDue(Vector3 dir, out string prefix, out Vector3 exitPoint)
        {
            prefix = null; exitPoint = default;
            if (_jack == null || Time.unscaledTime < _nextExitPoll) return false;
            _nextExitPoll = Time.unscaledTime + 0.2f;
            Vector3 c = JackCentre(_jack.transform);
            if (!IsAisleExitStep(c, dir, AisleExitLook, out prefix)) return false;
            if (prefix == _exitCheckedPrefix && Time.unscaledTime - _exitCheckedAt < 8f) return false;
            exitPoint = c;
            return true;
        }

        /// <summary>Stop at the aisle mouth, look both ways, wait until the cross aisle is clear.</summary>
        private IEnumerator JackAisleExitCheck(string prefix, Vector3 exitPoint, Vector3 dir)
        {
            _exitCheckedPrefix = prefix; _exitCheckedAt = Time.unscaledTime;
            yield return EquipmentTraffic.AisleExitCheck(_jack, exitPoint, dir);
            _exitCheckedAt = Time.unscaledTime;
        }

        /// <summary>A step from <paramref name="p"/> along <paramref name="dir"/> that leaves the aisle <paramref name="p"/> is in through one
        /// of its ends (not sideways). Shared with the reach trucks.</summary>
        internal static bool IsAisleExitStep(Vector3 p, Vector3 dir, float look, out string prefix)
        {
            prefix = null;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return false;
            dir.Normalize();
            if (!TryFindAisleAt(p, 0f, out var cur)) return false;
            if (TryFindAisleAt(p + dir * look, 0f, out _)) return false;
            if (Mathf.Abs(Vector3.Dot(dir, cur.Travel)) < 0.5f) return false;
            prefix = cur.Prefix;
            return true;
        }

        /// <summary>Called at the end of every jack leg / straight move.</summary>
        private void EndTravel()
        {
            _travelDir = Vector3.zero;
            _legTarget = Vector3.zero;
            ClearHeld();
        }

        /// <summary>Held long enough on the way to a bay, and the slot is within walking reach: end the leg, the selector walks.</summary>
        private bool ShouldStepOffAndWalk()
        {
            if (_queueProbe == null || _heldRealSeconds < QueueWaitRealSeconds) return false;
            if (Time.unscaledTime < _nextQueueProbeReal) return false;
            _nextQueueProbeReal = Time.unscaledTime + 1f;
            if (!TryPlanQueueWalk(_queueProbe, out _)) return false;
            _queueAbortBySolid = _heldBehindJack == null;
            return true;
        }

        /// <summary>No-progress watchdog exemption: standing in a queue behind a jack that is itself working is not "stuck".
        /// Not when the two wait on each other, and capped, so a real jam still gets freed.</summary>
        private bool QueuedBehindWorkingJack()
        {
            // Held by something solid: the leg creeps round it, walks from here (5 s) or gives up (SolidGiveUpReal) by itself.
            if (_heldBySolid != null) return _heldRealSeconds < SolidGiveUpReal + 1f;
            if (_heldBehindJack == null || _heldGameMinutes > QueueExemptMaxGameMinutes) return false;
            if (!s_jackClaims.TryGetValue(_heldBehindJack, out var d) || d == null) return true;   // driven by something else: let it move
            if (d._heldBehindJack == _jack) return false;                                         // waiting on each other
            return d._noProgressSeconds < NoProgressGameMinutes * 0.75f;
        }

        // ══════════════════════════════════════════════════════════════════════════════════════════════
        // Another jack in the way (queue) + right of way
        // ══════════════════════════════════════════════════════════════════════════════════════════════

        private static void RefreshJackList()
        {
            float age = Time.unscaledTime - s_allJacksAt;
            if (age >= 0f && age <= 1f) return;   // (a stamp from an earlier Play session reads negative: refresh)
            s_allJacksAt = Time.unscaledTime;
            s_allJacks.Clear();
            foreach (var s in FindObjectsByType<MHEOperatorSlot>())
                if (s != null && s.isActiveAndEnabled && s.IsPalletJack && s.GetComponent<PlacedObject>() != null) s_allJacks.Add(s);
        }

        /// <summary>A manned pallet jack sits in this jack's lane of travel for a move in <paramref name="moveDir"/>, and this jack
        /// doesn't have right of way over it. A jack off to the side (the other lane is free) doesn't count: pass it.</summary>
        private bool JackAhead(Vector3 moveDir, out MHEOperatorSlot blocker)
        {
            blocker = null;
            if (_jack == null) return false;
            moveDir.y = 0f;
            if (moveDir.sqrMagnitude < 0.0001f) return false;
            moveDir.Normalize();
            RefreshJackList();
            Vector3 me = JackCentre(_jack.transform);
            float best = float.MaxValue;
            foreach (var other in s_allJacks)
            {
                if (other == null || other == _jack) continue;
                // An unmanned jack (parked, charging, left where a driver gave up) never moves, so queueing behind it would wait for ever
                // (blocks 37+: jacks idle at (-18, 42)). MachineClearance keeps this jack out of it.
                if (other.CurrentOperator == null) continue;
                Vector3 rel = JackCentre(other.transform) - me; rel.y = 0f;
                float ahead = Vector3.Dot(rel, moveDir);
                if (ahead <= 0f || ahead > JackClearAhead) continue;
                if ((rel - moveDir * ahead).magnitude >= JackClearSide) continue;
                if (HasRightOfWayOver(other, me, ahead)) continue;
                if (ahead < best) { best = ahead; blocker = other; }
            }
            return blocker != null;
        }

        /// <summary>RIGHT OF WAY: two moving jacks each in the other's path (typically converging on one aisle mouth) used to hold each
        /// other for 6 s and then both drive. Now the one with more room ahead goes and the other waits; a tie goes to the lower id.
        /// A parked / picking jack never has to give way: whoever drives up to it waits.</summary>
        private bool HasRightOfWayOver(MHEOperatorSlot other, Vector3 me, float otherAheadOfMe)
        {
            if (!s_jackClaims.TryGetValue(other, out var od) || od == null || od == this) return false;
            Vector3 odDir = od._travelDir;
            if (odDir.sqrMagnitude < 0.0001f) return false;
            Vector3 back = me - JackCentre(other.transform); back.y = 0f;
            float meAheadOfIt = Vector3.Dot(back, odDir);
            if (meAheadOfIt <= 0f || meAheadOfIt > JackClearAhead) return false;          // not in its way: it isn't waiting on me
            if ((back - odDir * meAheadOfIt).magnitude >= JackClearSide) return false;
            if (Mathf.Abs(meAheadOfIt - otherAheadOfMe) > 0.05f) return otherAheadOfMe > meAheadOfIt;
            return _jack.GetHashCode() < other.GetHashCode();   // any stable per-session order will do
        }

        // ══════════════════════════════════════════════════════════════════════════════════════════════
        // Footprint guard (MachineClearance) for this driver's jack
        // ══════════════════════════════════════════════════════════════════════════════════════════════

        /// <summary>True when moving the jack from where it is to this pose would push it into something it isn't already in.</summary>
        private bool StepBlocked(Vector3 nextPos, Quaternion nextRot, out string what)
            => MachineClearance.StepBlocked(_jack, nextPos, nextRot, out what);

        private MHEOperatorSlot _yieldingTo;

        public const float JackBuffer = 1.0f;       // jacks keep this far (outline to outline) from each other, except while passing
        private MHEOperatorSlot _bufferYieldTo;     // the jack this one is currently keeping its distance from

        /// <summary>True when this step would bring the jack inside <see cref="JackBuffer"/> of another manned jack (any angle, open floor
        /// or aisle). Not while passing (the pass goes 1.5 m out on purpose). Two jacks holding each other: the lower hash goes, so they
        /// never both freeze.</summary>
        // ── Deadlock breaker (Tad, 2026-11-10): two jacks stuck on each other near the same place (an aisle mouth) ──
        private const float YieldAfterReal   = 1.5f;   // held this long (REAL s) before the loser backs up
        private const float YieldBackUp      = 1.5f;   // the loser backs straight up this far...
        private const float YieldWaitReal    = 4f;     // ...and waits this long (REAL s) or until the other jack is out of its way
        private const float YieldNearRadius  = 5f;     // only jacks this close (centre to centre) count as "stuck on each other"
        private const float YieldClearRadius = 6f;     // "out of my way" = its centre this far from mine
        private Vector3 _legTarget;                    // where this jack is driving to right now (zero = not driving)
        private float _nextYieldReal;

        /// <summary>Held up by a nearby manned jack that is itself driving or held: the one FURTHER from where it is going (or with the more
        /// room behind it) is the loser and backs up. False for the winner, or when there is no room behind to back into.</summary>
        private bool ShouldBackUpFor(out MHEOperatorSlot other)
        {
            other = null;
            if (_jack == null || _passing || _heldRealSeconds < YieldAfterReal || Time.unscaledTime < _nextYieldReal) return false;
            Vector3 me = JackCentre(_jack.transform);
            float bestD = YieldNearRadius;
            foreach (var kv in s_jackClaims)
            {
                var o = kv.Key; var od = kv.Value;
                if (o == null || o == _jack || od == null || od == this || !o.IsOccupied) continue;
                if (od._travelDir.sqrMagnitude < 0.0001f && od._heldRealSeconds <= 0f) continue;   // parked / picking: passing or walking handles it
                float d = PlanarDist(JackCentre(o.transform), me);
                if (d < bestD) { bestD = d; other = o; }
            }
            if (other == null) return false;
            var odr = s_jackClaims[other];
            float myTo = PlanarDist(_jack.transform.position, _legTarget);
            float itsTo = odr._legTarget.sqrMagnitude > 0.0001f ? PlanarDist(other.transform.position, odr._legTarget) : 0f;
            bool loser = myTo > itsTo + 0.5f || (Mathf.Abs(myTo - itsTo) <= 0.5f && _jack.GetHashCode() > other.GetHashCode());
            if (!loser) return false;
            Vector3 back = _jack.transform.position - Flat(_jack.transform.forward, 0f).normalized * YieldBackUp;
            if (!MachineClearance.StraightSlideClear(_jack, back)) return false;   // nowhere to back into: the other one must give
            return true;
        }

        /// <summary>Back straight up YieldBackUp, then wait YieldWaitReal real seconds or until <paramref name="other"/> is out of the way.</summary>
        private IEnumerator BackUpAndWait(MHEOperatorSlot other)
        {
            if (_jack == null) yield break;
            Transform jt = _jack.transform;
            Debug.Log($"[OrderSelectionTaskDriver] '{name}': stuck with {(other != null ? other.name : "another jack")} - backing up {YieldBackUp:0.0}m to let it through, then waiting up to {YieldWaitReal:0}s.");
            _nextYieldReal = Time.unscaledTime + 8f;
            Vector3 fwd = Flat(jt.forward, 0f).normalized;
            float moved = 0f;
            while (_jack != null && moved < YieldBackUp)
            {
                float step = Mathf.Min(JackReverseSpeed * Time.deltaTime, YieldBackUp - moved);
                if (step <= 0f) { yield return null; continue; }
                Vector3 next = jt.position - fwd * step;
                if (StepBlocked(next, jt.rotation, out _)) break;
                jt.position = next;
                if (_jackAgent != null && _jackAgent.isActiveAndEnabled && _jackAgent.isOnNavMesh) _jackAgent.nextPosition = next;
                moved += step;
                yield return null;
            }
            float waited = 0f;
            while (_jack != null && other != null && waited < YieldWaitReal && other.IsOccupied
                   && PlanarDist(JackCentre(other.transform), JackCentre(jt)) < YieldClearRadius)
            {
                waited += RealDt;
                yield return null;
            }
            ClearHeld();
            _bufferYieldTo = null;
        }

        private bool JackBufferHold(Vector3 nextPos, Quaternion nextRot, out MHEOperatorSlot other)
        {
            other = null;
            if (_jack == null || _passing) { _bufferYieldTo = null; return false; }
            if (!MachineClearance.JackBufferBlocked(_jack, nextPos, nextRot, JackBuffer, out other)) { _bufferYieldTo = null; return false; }
            if (s_jackClaims.TryGetValue(other, out var od) && od != null && od != this && od._bufferYieldTo == _jack
                && _jack.GetHashCode() < other.GetHashCode())
            { _bufferYieldTo = null; return false; }   // it is waiting for me and I win the tie: go
            _bufferYieldTo = other;
            return true;
        }

        /// <summary>One log line each time this jack starts giving way to a reach truck (rule 1: reach trucks have right of way, 1 m buffer).</summary>
        private void WarnYield(MHEOperatorSlot truck)
        {
            if (truck == null || truck == _yieldingTo) return;
            _yieldingTo = truck;
            Debug.Log($"[OrderSelectionTaskDriver] '{name}': giving way to reach truck {truck.name} (right of way, keeping {AisleTraffic.ReachTruckBuffer:0}m clear).");
        }

        private void WarnSolid(string phase, string what)
        {
            ThrottledLog.Warn("jack-solid-" + name + phase,
                $"[JackSolid] '{name}' pallet jack ({phase}) stopped at {(_jack != null ? _jack.transform.position : transform.position)} - " +
                $"the move would have driven it into {what ?? "something"}.", 30f);
        }

        /// <summary>A NavMesh leg held by a solid: nudge the jack straight forward (swings a turn wider) or back a little, if that
        /// is clear and still on its NavMesh. Returns false when neither is possible.</summary>
        private bool TryCreep(Vector3 wantDir)
        {
            if (_jack == null || _jackAgent == null || !_jackAgent.isActiveAndEnabled || !_jackAgent.isOnNavMesh) return false;
            Transform jt = _jack.transform;
            Vector3 fwd = jt.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) return false;
            fwd.Normalize();
            wantDir.y = 0f;
            bool forwardFirst = wantDir.sqrMagnitude < 0.0001f || Vector3.Dot(fwd, wantDir.normalized) >= -0.2f;
            for (int k = 0; k < 2; k++)
            {
                Vector3 d = (k == 0) == forwardFirst ? fwd : -fwd;
                Vector3 p = jt.position + d * CreepStep;
                if (!NavSample.OnAgentMesh(_jackAgent, p, 0.25f, out var hit) || Mathf.Abs(hit.position.y - jt.position.y) > 0.3f) continue;
                p = hit.position;
                if (StepBlocked(p, jt.rotation, out _)) continue;
                jt.position = p;
                _jackAgent.nextPosition = p;
                return true;
            }
            return false;
        }

        // ══════════════════════════════════════════════════════════════════════════════════════════════
        // Queue stop: walk up the aisle to pick
        // ══════════════════════════════════════════════════════════════════════════════════════════════

        private sealed class QueueWalk
        {
            public Vector3 Drop;                          // beside the pallet, where each case is set down
            public Vector3 Face;                          // the pick face
            public readonly List<Vector3> Route = new(); // Drop ... Face
            public float Length;
        }

        /// <summary>A walk from beside the jack's active pallet to <paramref name="location"/>'s pick face that is no longer than
        /// QueueWalkMax, stays on the walking NavMesh (round racks and walls) and doesn't pass through another machine or a floor
        /// pallet (one detour round the first one in the way is tried).</summary>
        private bool TryPlanQueueWalk(LocationData location, out QueueWalk walk)
        {
            walk = null;
            if (_jack == null || location == null || _pallets.Count == 0) return false;
            var pallet = _pallets[_pallets.Count - 1];
            if (pallet == null) return false;
            if (!TryGetPickGeometry(location, out Vector3 slotFlat, out Vector3 toAisle)) return false;
            var self = GetComponent<NavMeshAgent>();
            if (self == null) return false;
            var filter = new NavMeshQueryFilter { agentTypeID = self.agentTypeID, areaMask = self.areaMask };

            Transform jt = _jack.transform;
            float y = jt.position.y;
            Vector3 face = Flat(slotFlat + toAisle * PickStandOff(location), y);
            // Quick reject before any path query: straight-line distance already over the limit.
            Vector3 pp = pallet.transform.position;
            if (PlanarDist(pp, face) > QueueWalkMax + 1f) return false;
            Vector3 right = jt.right; right.y = 0f; right.Normalize();
            Vector3 side = Vector3.Dot(face - pp, right) >= 0f ? right : -right;
            Vector3 drop = Flat(pp + side * QueueDropOff, y);

            if (!NavMesh.SamplePosition(drop, out var dHit, 1f, filter)) return false;
            if (!NavMesh.SamplePosition(face, out var fHit, 1f, filter)) return false;
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(dHit.position, fHit.position, filter, path) || path.status != NavMeshPathStatus.PathComplete) return false;

            var route = new List<Vector3> { drop };
            for (int i = 1; i < path.corners.Length - 1; i++) route.Add(Flat(path.corners[i], y));
            route.Add(face);
            if (RouteLength(route) > QueueWalkMax) return false;

            MachineClearance.GatherNear(Vector3.Lerp(drop, face, 0.5f), QueueWalkMax * 0.5f + 1f, _jack);
            if (FirstObstacleOnRoute(route, out Transform obs, out MachineClearance.Footprint obsF))
            {
                if (!TryDetour(drop, face, obs, obsF, filter, y, out route)) return false;
                if (RouteLength(route) > QueueWalkMax) return false;
            }

            walk = new QueueWalk { Drop = drop, Face = face, Length = RouteLength(route) };
            walk.Route.AddRange(route);
            return true;
        }

        private static float RouteLength(List<Vector3> route)
        {
            float d = 0f;
            for (int i = 1; i < route.Count; i++) d += PlanarDist(route[i - 1], route[i]);
            return d;
        }

        /// <summary>The first machine or floor pallet (from the last MachineClearance.GatherNear) a walker on this route would pass through.</summary>
        private static bool FirstObstacleOnRoute(List<Vector3> route, out Transform obstacle, out MachineClearance.Footprint f)
        {
            obstacle = null; f = default;
            for (int i = 1; i < route.Count; i++)
            {
                Vector3 a = route[i - 1], b = route[i];
                int n = Mathf.Max(1, Mathf.CeilToInt(PlanarDist(a, b) / 0.25f));
                for (int k = 0; k <= n; k++)
                {
                    Vector3 p = Vector3.Lerp(a, b, k / (float)n);
                    foreach (var m in MachineClearance.NearMachines)
                    {
                        var mf = MachineClearance.FootprintOf(m);
                        if (MachineClearance.InsideFootprint(m.transform, mf, p, PersonRadius)) { obstacle = m.transform; f = mf; return true; }
                    }
                    foreach (var pl in MachineClearance.NearPallets)
                        if (MachineClearance.InsidePallet(pl, p, PersonRadius)) { obstacle = pl; f = MachineClearance.PalletFootprint; return true; }
                }
            }
            return false;
        }

        /// <summary>Walks round <paramref name="obs"/>: past both of its ends, on whichever side is clear (and shorter).</summary>
        private static bool TryDetour(Vector3 drop, Vector3 face, Transform obs, MachineClearance.Footprint f, NavMeshQueryFilter filter, float y, out List<Vector3> route)
        {
            route = null;
            float m = PersonRadius + 0.3f;
            float bestLen = float.MaxValue;
            foreach (float sx in new[] { f.MinX - m, f.MaxX + m })
            {
                Vector3 a = Flat(obs.TransformPoint(new Vector3(sx, 0f, f.MinZ - m)), y);
                Vector3 b = Flat(obs.TransformPoint(new Vector3(sx, 0f, f.MaxZ + m)), y);
                if (PlanarDist(drop, b) < PlanarDist(drop, a)) { var t = a; a = b; b = t; }
                var cand = new List<Vector3> { drop, a, b, face };
                bool ok = true;
                var onMesh = new Vector3[cand.Count];
                for (int i = 0; i < cand.Count && ok; i++)
                {
                    if (NavMesh.SamplePosition(cand[i], out var h, 0.3f, filter)) onMesh[i] = h.position;
                    else ok = false;
                }
                // NavMesh.Raycast returns true when the straight leg leaves the walkable floor (a rack, a wall in between).
                for (int i = 1; i < cand.Count && ok; i++)
                    if (NavMesh.Raycast(onMesh[i - 1], onMesh[i], out _, filter)) ok = false;
                if (!ok || FirstObstacleOnRoute(cand, out _, out _)) continue;
                float len = RouteLength(cand);
                if (len < bestLen) { bestLen = len; route = cand; }
            }
            return route != null;
        }

        /// <summary>Forward along <paramref name="route"/> (pallet side .. pick face), starting at the first point that is not behind the
        /// walker: a point is skipped when the walker is already closer to the next one than that point is.</summary>
        private IEnumerator WalkRouteFromHere(List<Vector3> route)
        {
            if (route == null || route.Count == 0) yield break;
            int start = 0;
            while (start < route.Count - 1 &&
                   PlanarDist(transform.position, route[start + 1]) <= PlanarDist(route[start], route[start + 1]))
                start++;
            for (int i = start; i < route.Count; i++) yield return WalkTo(route[i]);
        }

        private IEnumerator WalkRoute(List<Vector3> route, bool reverse)
        {
            if (route == null) yield break;
            if (!reverse) for (int i = 0; i < route.Count; i++) yield return WalkTo(route[i]);
            else for (int i = route.Count - 1; i >= 0; i--) yield return WalkTo(route[i]);
        }

        /// <summary>The jack stays where the queue stopped it; the selector picks on foot from here (see TryPlanQueueWalk).</summary>
        private void BeginQueueStop(string bayKey, LocationData location, QueueWalk walk)
        {
            _queuedStop = true;
            _queueBySolid = _queueAbortBySolid;
            _queuedAt = _jack != null ? _jack.transform.position : transform.position;
            Debug.Log($"[OrderSelectionTaskDriver] '{name}': pallet jack held up on the way to bay {bayKey} for {QueueWaitRealSeconds:0}s " +
                      $"({(_queueBySolid ? "something solid in the way" : "another jack ahead")}) - stepping off to walk {walk.Length:0.0}m to {location.Address}.");
        }

        /// <summary>A queue stop is still a queue: the jack ahead hasn't moved on (or, if a solid held this jack, it is still where it
        /// stopped - driving on would only hit the same thing).</summary>
        private bool StillQueued(Vector3 slotFlat)
        {
            if (_jack == null) return false;
            if (_queueBySolid) return PlanarDist(_jack.transform.position, _queuedAt) < 0.5f;
            Vector3 to = slotFlat - _jack.transform.position; to.y = 0f;
            return JackAhead(_jack.transform.forward, out _) || JackAhead(to, out _);
        }
    }
}
