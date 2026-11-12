using System.Collections;
using UnityEngine;
using GameCore.Navigation;

namespace GameCore.Actors
{
    /// <summary>
    /// THE PALLET JACK'S OPEN-FLOOR LEG ON THE GRID PLANNER (2026-11-11, behind <see cref="NavGridService.UseForJacks"/>, default OFF).
    ///
    /// <see cref="JackNavLeg"/> hands the route to the jack's own NavMeshAgent. This replaces ONLY that: where the route goes is planned
    /// on the NavGrid (footprint-aware, aisles are one-way for jacks, never reversing up an aisle), and the jack is then driven along
    /// the smoothed route with the SAME scripted primitives every bay leg already uses (JackFace + JackMoveStraight). Everything
    /// those do stays exactly as it was: the queue behind a manned jack, the 1 m buffer, reach-truck right of way, the solid guard,
    /// stop-and-look at an aisle exit, step-off-and-walk. Nothing here owns a NavMeshAgent; the jack's agent stays off.
    ///
    /// Contract kept from JackNavLeg: sets _jackLegOk (true = arrived, or parked in a queue with _jackLegQueued), leaves the jack in
    /// manual mode, and fails LOUDLY with no straight-line fallback (a failed route is a failed leg, the caller abandons the jack as before).
    /// </summary>
    public partial class OrderSelectionTaskDriver
    {
        private const int   GridLegMaxReplans    = 6;     // an interrupted leg (held out, solid give-up, passed) is re-planned from where the jack stands
        private const int   GridLegGoalRadius    = 2;     // an unusable goal moves to the nearest fitting cell within this many cells
        private const float GridLegCloseMaxDist  = 4.5f;  // the last stretch from the route's end to the real target is a guarded straight move

        /// <summary>Dispatch point: the flag decides which backend carries the leg.</summary>
        private IEnumerator JackLeg(Vector3 target, string phase)
        {
            if (NavGridService.UseForJacks) yield return JackGridLeg(target, phase);
            else yield return JackNavLeg(target, phase);
        }

        private IEnumerator JackGridLeg(Vector3 target, string phase)
        {
            _jackLegOk = false;
            if (_jack == null) yield break;
            Transform jt = _jack.transform;

            // Same short-hop rule as the NavMesh leg: the scripted finish in DriveJackTo does it.
            if (PlanarDist(jt.position, target) < 2f) { _jackLegOk = true; yield break; }

            SetJackDrive(false, phase);   // the script owns the jack on the grid path: its agent and AiNavigation stay off
            // Reversing is allowed on OPEN FLOOR (a jack parked nose-along a wall has its tail against the wall and must back out before it can
            // turn: found in Play 2026-11-11), never up an aisle (AllowAisleReverse stays false: Tad, 2026-10-18 "never back up" in aisles).
            var profile = NavGridBuilder.ProfileOf(_jack, canReverse: true, followAisleTraffic: true);

            int replans = 0;
            bool completed = false;
            while (_jack != null && replans <= GridLegMaxReplans)
            {
                var planner = NavGridService.EnsurePlanner();
                if (planner == null)
                {
                    Debug.LogError($"[GridNav] '{name}': no PlacementGrid to plan '{phase}' on.");
                    yield break;
                }
                float t0 = Time.realtimeSinceStartup;
                // The floor as it is right now: parked machines and pallets standing on it. Taken at plan time, so a re-plan sees new things.
                int dynCells = NavDynamicLayer.Refresh(planner.Grid, _jack);
                var res = planner.PlanNearest(profile, jt.position, NavGrid.HeadingOf(jt.forward), target, -1, GridLegGoalRadius, out float moved);
                float planMs = (Time.realtimeSinceStartup - t0) * 1000f;
                if (!res.Ok)
                {
                    // Loud, and named: this is where the NavMesh used to say "partial path" or snap the goal somewhere else.
                    Debug.LogError($"[GridNav] '{name}': no route for '{phase}' from {jt.position} to {target} ({res.Status}, {res.Expanded} expanded) - the jack will not drive it.");
                    yield break;
                }
                Debug.Log($"[GridNav] '{name}' '{phase}': {res.Smoothed.Count - 1} legs, cost {res.Cost:0.0}, goal moved {moved:0.0} m, {dynCells} dynamic cells, {planMs:0.0} ms (grid v{res.GridVersion}).");
                _legTarget = Flat(target, jt.position.y);

                bool interrupted = false;
                for (int i = 1; i < res.Smoothed.Count; i++)
                {
                    Vector3 wp = Flat(res.Smoothed[i], jt.position.y);
                    Vector3 to = wp - jt.position; to.y = 0f;
                    if (to.magnitude < 0.05f) continue;
                    // The planner says which way the jack FACES on this leg. Travel opposite to that is a reverse: hold the heading and slide
                    // backwards (JackMoveStraight slides without turning), at reverse speed.
                    Vector3 face = NavGrid.ForwardOf(res.LegHeading(i));
                    bool reverse = Vector3.Dot(to, face) < -0.01f;
                    yield return JackFace(reverse ? face : to);
                    if (_jack == null) yield break;
                    yield return JackMoveStraight(wp, reverse ? JackReverseSpeed : JackDriveSpeed);
                    if (_jack == null) yield break;
                    if (_jackLegQueued) { _jackLegOk = true; yield break; }   // held in a queue: the caller has the selector walk, as for a NavMesh leg
                    if (PlanarDist(jt.position, wp) > 0.3f) { interrupted = true; break; }
                }
                if (!interrupted) { completed = true; break; }

                // Held out by something solid, or told to pass: whatever stopped the move is gone or has changed. Plan again from here.
                _overtakeBlocker = null;
                replans++;
                yield return null;
            }
            if (_jack == null) yield break;
            if (!completed)
            {
                Debug.LogError($"[GridNav] '{name}': '{phase}' was interrupted {GridLegMaxReplans + 1} times - giving the leg up.");
                yield break;
            }

            // The route ends on a cell centre; the real target may be a little way on (or the goal was moved). Close it with a guarded straight move.
            float left = PlanarDist(jt.position, target);
            if (left > 0.5f && left <= GridLegCloseMaxDist)
            {
                Vector3 to = Flat(target, jt.position.y) - jt.position; to.y = 0f;
                yield return JackFace(to);
                if (_jack == null) yield break;
                yield return JackMoveStraight(target, JackDriveSpeed);
                if (_jack == null) yield break;
                if (_jackLegQueued) { _jackLegOk = true; yield break; }
            }
            _jackLegOk = true;   // DriveJackTo verifies the distance itself (NavLegArriveTolerance) and re-issues the leg if it is short
        }
    }
}
