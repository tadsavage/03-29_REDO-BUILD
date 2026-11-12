using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameCore.Navigation;

namespace GameCore.Actors
{
    /// <summary>
    /// THE REACH TRUCK'S TRAVEL LEG ON THE GRID PLANNER (2026-11-11, behind <see cref="NavGridService.UseForReachTrucks"/>, default OFF).
    ///
    /// <see cref="SeekViaNavMesh"/> hands the route to the truck's NavMeshAgent and then spends ~290 lines defending that agent: register
    /// on the mesh, snap the goal, claim it from the waypoint patrol, survive a rebake, hand a held leg to the pass / go-round scripts.
    /// This is the same contract with none of that: plan on the NavGrid, drive the smoothed route with scripted moves, keep every rule
    /// the NavMesh leg kept (1 m vehicle buffer, never into a rack / wall / pallet, stop and look at an aisle exit, back up out of a jam),
    /// and treat "go round the machine in front" as what it is: re-plan with that machine's cells blocked.
    ///
    /// Reach trucks may go either way in an aisle (no traffic rule), and never reverse on a leg (the callers' own precise drives do that).
    /// Contract kept: onDone(true) = arrived, onDone(false) = could not (the caller aborts / re-queues), leaves the truck in
    /// <see cref="DriveMode.Manual"/>, and there is NO straight-line fallback.
    /// </summary>
    public partial class ReachTruckOperator
    {
        private const int   RtMaxReplans     = 6;
        private const int   RtGoalRadius     = 2;      // cells: an unusable goal moves to the nearest cell the truck fits in
        private const float RtSolidGiveUpReal = 4f;    // held by something solid this long (real s): re-plan
        private const float RtPassInflate    = 0.5f;   // a machine driven round is blocked this much wider (half the 1 m vehicle buffer)

        private int _rtStep;                           // result of RtDriveTo: 0 arrived, 2 interrupted (re-plan), 3 failed
        private float _rtSolidFor;
        private MHEOperatorSlot _rtGoAround;           // the machine a hold asked to drive round
        private readonly List<MHEOperatorSlot> _rtAvoid = new List<MHEOperatorSlot>();
        private readonly HashSet<MHEOperatorSlot> _rtTried = new HashSet<MHEOperatorSlot>();

        /// <summary>Dispatch point: the flag decides which backend carries the leg. Same signature and contract as SeekViaNavMesh.</summary>
        private IEnumerator RtLeg(Vector3 target, string phase, System.Action<bool> onDone)
        {
            if (NavGridService.UseForReachTrucks) yield return RtGridLeg(target, phase, onDone);
            else yield return SeekViaNavMesh(target, phase, onDone);
        }

        private IEnumerator RtGridLeg(Vector3 target, string phase, System.Action<bool> onDone)
        {
            if (_operatorSlot == null)
            {
                Debug.LogError($"[GridNav] '{name}' {phase}: no MHEOperatorSlot on this truck - cannot plan a leg.");
                onDone?.Invoke(false);
                yield break;
            }

            // Same short-hop rule as the NavMesh leg: the caller's precise manual drive closes the last couple of metres.
            if (new Vector2(target.x - transform.position.x, target.z - transform.position.z).magnitude < 2f) { onDone?.Invoke(true); yield break; }

            // An open-floor leg is never inside a lane, so no lane lock may ride along (see SeekViaNavMesh).
            if (_heldLaneLock.HasValue)
            {
                Debug.LogWarning($"[ReachTruckOperator] '{name}' started a grid leg ({phase}) still holding lane {_heldLaneLock.Value.Door}{_heldLaneLock.Value.Lane} - releasing it.");
                ReleaseLaneLock();
            }
            SetDriveMode(DriveMode.Manual, $"grid leg → {phase}");   // the script owns the truck: its agent stays off

            // A rack anchor sits at its shelf's height; the truck drives on the floor.
            Vector3 driveTarget = new Vector3(target.x, transform.position.y, target.z);
            // Reversing is allowed (at double cost): a truck standing nose-along a wall has its fork tail against the wall and must back out
            // before it can turn (found in Play 2026-11-11, "no route" from the north edge). Reach trucks may reverse in aisles too.
            var profile = NavGridBuilder.ProfileOf(_operatorSlot, canReverse: true, followAisleTraffic: false);
            _rtAvoid.Clear();
            _rtTried.Clear();
            _rtGoAround = null;
            _jackWaitReal = 0f;

            int replans = 0;
            bool done = false, failed = false;
            while (replans <= RtMaxReplans && !failed)
            {
                var planner = NavGridService.EnsurePlanner();
                if (planner == null)
                {
                    Debug.LogError($"[GridNav] '{name}' {phase}: no PlacementGrid to plan on.");
                    onDone?.Invoke(false);
                    yield break;
                }
                float t0 = Time.realtimeSinceStartup;
                NavDynamicLayer.Refresh(planner.Grid, _operatorSlot, MachineClearance.VehicleSpacingMeters);   // keep the 1 m buffer the guard enforces
                foreach (var m in _rtAvoid) if (m != null) NavDynamicLayer.BlockMachine(planner.Grid, m, RtPassInflate);
                var res = planner.PlanNearest(profile, transform.position, NavGrid.HeadingOf(transform.forward), driveTarget, -1, RtGoalRadius, out float moved);
                if (!res.Ok && _rtAvoid.Count > 0)
                {
                    // No way round the machine in front: stop trying to go round it and wait for it, as the NavMesh leg did.
                    _rtAvoid.Clear();
                    NavDynamicLayer.Refresh(planner.Grid, _operatorSlot, MachineClearance.VehicleSpacingMeters);
                    res = planner.PlanNearest(profile, transform.position, NavGrid.HeadingOf(transform.forward), driveTarget, -1, RtGoalRadius, out moved);
                }
                if (!res.Ok)
                {
                    Debug.LogError($"[GridNav] '{name}' {phase}: no route from {transform.position} to {driveTarget} ({res.Status}, {res.Expanded} expanded) - the truck will not drive it.");
                    onDone?.Invoke(false);
                    yield break;
                }
                Debug.Log($"[GridNav] '{name}' '{phase}': {res.Smoothed.Count - 1} legs, cost {res.Cost:0.0}, goal moved {moved:0.0} m, avoiding {_rtAvoid.Count}, {(Time.realtimeSinceStartup - t0) * 1000f:0.0} ms.");

                bool interrupted = false;
                for (int i = 1; i < res.Smoothed.Count; i++)
                {
                    // The planner says which way the truck FACES on this leg; travel opposite to that is a reverse (hold the heading, back up).
                    Vector3 legTo = res.Smoothed[i] - transform.position; legTo.y = 0f;
                    Vector3 face = NavGrid.ForwardOf(res.LegHeading(i));
                    yield return RtDriveTo(res.Smoothed[i], Vector3.Dot(legTo, face) < -0.01f, face);
                    if (_rtStep == 3) { failed = true; break; }
                    if (_rtStep == 2) { interrupted = true; break; }
                }
                if (failed) break;
                if (!interrupted) { done = true; break; }

                if (_rtGoAround != null && !_rtAvoid.Contains(_rtGoAround)) _rtAvoid.Add(_rtGoAround);
                _rtGoAround = null;
                replans++;
                yield return null;
            }

            if (!done)
            {
                Debug.LogError($"[GridNav] '{name}' {phase}: leg FAILED " + (failed ? $"(held by another vehicle {JackWaitGiveUpReal:0}+ s)" : $"(interrupted {RtMaxReplans + 1} times)") + " - aborting this leg, no straight-line fallback.");
                onDone?.Invoke(false);
                yield break;
            }
            onDone?.Invoke(true);
        }

        /// <summary>Face a point, then drive to it, with every guard the NavMesh leg and DriveToPoint had. Sets <see cref="_rtStep"/>.</summary>
        private IEnumerator RtDriveTo(Vector3 wp, bool reverse, Vector3 reverseFace)
        {
            _rtStep = 0;
            Transform t = transform;
            Vector3 flat = new Vector3(wp.x, t.position.y, wp.z);
            Vector3 to = flat - t.position; to.y = 0f;
            if (to.magnitude <= 0.05f) yield break;

            // Forward: turn to face the point. Reverse: turn to the planned heading, then slide straight back (half speed, as PassLeg does).
            Quaternion want = Quaternion.LookRotation((reverse ? reverseFace : to).normalized);
            while (Quaternion.Angle(t.rotation, want) > FaceThreshold)
            {
                Quaternion next = Quaternion.RotateTowards(t.rotation, want, TurnSpeed * Time.deltaTime);
                int h = RtHold(t.position, next);
                if (h >= 2) { _rtStep = h; yield break; }
                if (h == 1) { yield return null; continue; }
                t.rotation = next;
                yield return null;
            }
            t.rotation = want;

            while (PlanarDist(t.position, flat) > 0.05f)
            {
                Vector3 next = Vector3.MoveTowards(t.position, flat, (reverse ? DriveSpeed * 0.5f : DriveSpeed) * Time.deltaTime);
                int h = RtHold(next, t.rotation);
                if (h >= 2) { _rtStep = h; yield break; }
                if (h == 1)
                {
                    yield return ReachJamRecovery(next - t.position);   // held behind a stationary machine a while: back up (capped)
                    yield return null;
                    continue;
                }
                yield return ReachAisleExitGate(next - t.position);     // stop and look before leaving an aisle
                t.position = next;
                yield return null;
            }
            t.position = flat;
        }

        /// <summary>0 = free to move, 1 = hold this frame, 2 = give up on this plan and re-plan, 3 = give the leg up.</summary>
        private int RtHold(Vector3 nextPos, Quaternion nextRot)
        {
            bool vehicleHold = WaitForJack(transform, nextPos, nextRot);   // 1 m vehicle buffer; sets _jackWaitReal / _lastBlocker
            bool solidHold = !vehicleHold && MachineClearance.StepBlocked(_operatorSlot, nextPos, nextRot, out _);
            if (!vehicleHold && !solidHold) { _rtSolidFor = 0f; return 0; }

            if (solidHold)
            {
                // A rack, wall or pallet in this step's way that the grid did not know about: re-plan, then give up if it keeps happening.
                if (Time.timeScale > 0f) _rtSolidFor += Time.unscaledDeltaTime;
                return _rtSolidFor >= RtSolidGiveUpReal ? 2 : 1;
            }

            if (_jackWaitReal >= JackWaitGiveUpReal) return 3;
            // RIGHT OF WAY: held behind a STATIONARY machine, drive round it (once per machine per leg) by planning with its cells blocked.
            var blocker = _lastBlocker;
            if (_jackWaitReal >= RtPassAfterReal && blocker != null && !_rtTried.Contains(blocker))
            {
                var ta = blocker.GetComponent<EquipmentTrafficAgent>();
                if (ta == null || !ta.IsMoving)
                {
                    _rtTried.Add(blocker);
                    _rtGoAround = blocker;
                    return 2;
                }
            }
            return 1;
        }
    }
}
