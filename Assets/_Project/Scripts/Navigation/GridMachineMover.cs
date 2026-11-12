using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameCore.Actors;

namespace GameCore.Navigation
{
    /// <summary>
    /// The generic grid backend for <see cref="IMachineMover"/>: plan on the NavGrid, drive the smoothed route with scripted moves, and
    /// keep the two guards every machine move in this project must keep (Tad, 2026-11-07 / 2026-11-09): never into a rack, wall, pallet
    /// or another machine, and always a 1 m gap from other vehicles. Both are <see cref="MachineClearance.StepBlocked"/>. Held behind a
    /// stationary machine, it drives ROUND it by re-planning with that machine's cells blocked wider (once per machine per move); held by
    /// something solid, it re-plans; held for good, it fails. No straight-line fallback, ever.
    ///
    /// The pallet jack and reach truck legs have their own versions with role-specific guards (queues, aisle-exit stops); this one is the
    /// plain case, first used by the dock stockers' cross-dock legs, and is what those two collapse into once their special rules are data.
    /// </summary>
    public static class GridMachineMover
    {
        private const int   MaxReplans      = 6;
        private const int   GoalRadiusCells = 2;
        private const float SolidGiveUpReal = 4f;
        private const float HeldGiveUpReal  = 30f;
        private const float PassAfterReal   = 1f;
        private const float PassInflate     = 0.5f;

        private sealed class LegState
        {
            public int Step;                       // 0 arrived, 2 interrupted (re-plan), 3 failed
            public float HeldReal, SolidReal;
            public MHEOperatorSlot GoAround;
            public readonly HashSet<MHEOperatorSlot> Tried = new HashSet<MHEOperatorSlot>();
            public readonly List<MHEOperatorSlot> Avoid = new List<MHEOperatorSlot>();
        }

        /// <summary>The dock stockers' cross-dock leg: same shape as their SeekViaNavMesh (a bool callback, no fallback), grid-planned.
        /// Dock stockers stay out of aisles, may back up, and keep the 1 m buffer.</summary>
        public static IEnumerator DockStockerLeg(Transform ds, Vector3 target, string phase, float driveSpeed, float turnSpeed, Action<bool> onDone)
        {
            MoveResult result = default;
            yield return MoveTo(new MoveRequest
            {
                Machine = ds, Target = target, Phase = phase, FinalHeading = -1,
                DriveSpeed = driveSpeed, TurnSpeed = turnSpeed,
                CanReverse = true, AvoidAisles = true, VehicleBuffer = GameCore.Actors.MachineClearance.VehicleSpacingMeters,
            }, r => result = r);
            onDone?.Invoke(result.Arrived);
        }

        public static IEnumerator MoveTo(MoveRequest req, Action<MoveResult> onDone)
        {
            Transform t = req.Machine;
            var slot = t != null ? t.GetComponent<MHEOperatorSlot>() : null;
            string who = t != null ? t.name : "?";
            if (slot == null)
            {
                Debug.LogError($"[GridNav] '{who}' {req.Phase}: no MHEOperatorSlot on this machine - cannot plan a leg.");
                onDone?.Invoke(new MoveResult { Status = MoveStatus.NoPath, Detail = "no slot" });
                yield break;
            }
            if (new Vector2(req.Target.x - t.position.x, req.Target.z - t.position.z).magnitude < 2f)
            {
                onDone?.Invoke(new MoveResult { Status = MoveStatus.Arrived });   // the caller's precise drive closes a hop this short
                yield break;
            }

            Vector3 driveTarget = new Vector3(req.Target.x, t.position.y, req.Target.z);
            var profile = NavGridBuilder.ProfileOf(slot, req.CanReverse, followAisleTraffic: false);
            profile.AvoidAisles = req.AvoidAisles;
            var st = new LegState();

            for (int replans = 0; replans <= MaxReplans; replans++)
            {
                var planner = NavGridService.EnsurePlanner();
                if (planner == null)
                {
                    Debug.LogError($"[GridNav] '{who}' {req.Phase}: no PlacementGrid to plan on.");
                    onDone?.Invoke(new MoveResult { Status = MoveStatus.NoPath, Detail = "no grid" });
                    yield break;
                }
                float t0 = Time.realtimeSinceStartup;
                NavDynamicLayer.Refresh(planner.Grid, slot, req.VehicleBuffer);
                foreach (var m in st.Avoid) if (m != null) NavDynamicLayer.BlockMachine(planner.Grid, m, PassInflate);
                var res = planner.PlanNearest(profile, t.position, NavGrid.HeadingOf(t.forward), driveTarget, -1, GoalRadiusCells, out float moved);
                if (!res.Ok && st.Avoid.Count > 0)
                {
                    st.Avoid.Clear();   // no way round it: wait for it instead
                    NavDynamicLayer.Refresh(planner.Grid, slot, req.VehicleBuffer);
                    res = planner.PlanNearest(profile, t.position, NavGrid.HeadingOf(t.forward), driveTarget, -1, GoalRadiusCells, out moved);
                }
                if (!res.Ok)
                {
                    Debug.LogError($"[GridNav] '{who}' {req.Phase}: no route from {t.position} to {driveTarget} ({res.Status}, {res.Expanded} expanded) - it will not drive it.");
                    onDone?.Invoke(new MoveResult { Status = MoveStatus.NoPath, Detail = res.Status.ToString() });
                    yield break;
                }
                Debug.Log($"[GridNav] '{who}' '{req.Phase}': {res.Smoothed.Count - 1} legs, cost {res.Cost:0.0}, goal moved {moved:0.0} m, avoiding {st.Avoid.Count}, {(Time.realtimeSinceStartup - t0) * 1000f:0.0} ms.");

                bool interrupted = false;
                for (int i = 1; i < res.Smoothed.Count; i++)
                {
                    Vector3 to = res.Smoothed[i] - t.position; to.y = 0f;
                    Vector3 face = NavGrid.ForwardOf(res.LegHeading(i));
                    yield return DriveLeg(st, req, t, slot, res.Smoothed[i], Vector3.Dot(to, face) < -0.01f, face);
                    if (t == null) { onDone?.Invoke(new MoveResult { Status = MoveStatus.Cancelled, Detail = "machine destroyed" }); yield break; }
                    if (st.Step == 3)
                    {
                        Debug.LogError($"[GridNav] '{who}' {req.Phase}: leg FAILED (held by another vehicle {HeldGiveUpReal:0}+ s) - aborting, no straight-line fallback.");
                        onDone?.Invoke(new MoveResult { Status = MoveStatus.Blocked, Detail = "held" });
                        yield break;
                    }
                    if (st.Step == 2) { interrupted = true; break; }
                }
                if (!interrupted) { onDone?.Invoke(new MoveResult { Status = MoveStatus.Arrived }); yield break; }

                if (st.GoAround != null && !st.Avoid.Contains(st.GoAround)) st.Avoid.Add(st.GoAround);
                st.GoAround = null;
                yield return null;
            }
            Debug.LogError($"[GridNav] '{who}' {req.Phase}: interrupted {MaxReplans + 1} times - giving the leg up.");
            onDone?.Invoke(new MoveResult { Status = MoveStatus.Blocked, Detail = "replans exhausted" });
        }

        private static IEnumerator DriveLeg(LegState st, MoveRequest req, Transform t, MHEOperatorSlot slot, Vector3 wp, bool reverse, Vector3 reverseFace)
        {
            st.Step = 0;
            Vector3 flat = new Vector3(wp.x, t.position.y, wp.z);
            Vector3 to = flat - t.position; to.y = 0f;
            if (to.magnitude <= 0.05f) yield break;

            float turn = req.TurnSpeed > 0f ? req.TurnSpeed : 140f;
            float speed = req.DriveSpeed > 0f ? req.DriveSpeed : 3f;
            Quaternion want = Quaternion.LookRotation((reverse ? reverseFace : to).normalized);
            while (t != null && Quaternion.Angle(t.rotation, want) > 2f)
            {
                Quaternion next = Quaternion.RotateTowards(t.rotation, want, turn * Time.deltaTime);
                int h = Hold(st, slot, t.position, next);
                if (h >= 2) { st.Step = h; yield break; }
                if (h == 1) { yield return null; continue; }
                t.rotation = next;
                yield return null;
            }
            if (t == null) yield break;
            t.rotation = want;

            while (t != null && PlanarDist(t.position, flat) > 0.05f)
            {
                Vector3 next = Vector3.MoveTowards(t.position, flat, (reverse ? speed * 0.5f : speed) * Time.deltaTime);
                int h = Hold(st, slot, next, t.rotation);
                if (h >= 2) { st.Step = h; yield break; }
                if (h == 1) { yield return null; continue; }
                t.position = next;
                yield return null;
            }
            if (t != null) t.position = flat;
        }

        /// <summary>0 free, 1 hold this frame, 2 re-plan, 3 give up.</summary>
        private static int Hold(LegState st, MHEOperatorSlot slot, Vector3 nextPos, Quaternion nextRot)
        {
            if (!GameCore.Actors.MachineClearance.StepBlocked(slot, nextPos, nextRot, out _)) { st.HeldReal = 0f; st.SolidReal = 0f; return 0; }
            float dt = Time.timeScale > 0f ? Time.unscaledDeltaTime : 0f;
            var blocker = GameCore.Actors.MachineClearance.LastMachineHit;

            if (blocker == null)
            {
                st.SolidReal += dt;   // a rack, wall or pallet the grid did not know about: re-plan, then give up if it keeps happening
                return st.SolidReal >= SolidGiveUpReal ? 2 : 1;
            }

            st.HeldReal += dt;
            if (st.HeldReal >= HeldGiveUpReal) return 3;
            if (st.HeldReal >= PassAfterReal && !st.Tried.Contains(blocker))
            {
                var ta = blocker.GetComponent<EquipmentTrafficAgent>();
                if (ta == null || !ta.IsMoving) { st.Tried.Add(blocker); st.GoAround = blocker; return 2; }   // right of way: drive round it
            }
            return 1;
        }

        private static float PlanarDist(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }
    }
}
