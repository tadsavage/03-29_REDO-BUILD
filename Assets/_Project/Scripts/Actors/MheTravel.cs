using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace GameCore.Actors
{
    /// <summary>
    /// THE ONLY WAY A MACHINE TRAVELS (2026-11-20 rework: "strip everything, start from a clean slate").
    ///
    /// A plain NavMeshAgent leg: the agent plans the route on the one baked NavMeshSurface and steers round other agents (all machines
    /// avoidancePriority 50). No planner, no lanes, no sensor, no right of way, no holding lines. The transform is moved by copying the
    /// agent's position (the agent keeps updatePosition = false so it never fights the machine's Rigidbody; see AiNavigation.Awake).
    /// Scripted choreography (fork-in, lane drop, reverse-in) stays in the callers and is NOT done here.
    ///
    /// Contract: <c>onDone(true)</c> = arrived within <c>arrive</c>, <c>onDone(false)</c> = could not (no path, timed out, machine gone).
    /// The agent is left DISABLED afterwards (the machine is scripted again), AiNavigation is left as it was found.
    /// </summary>
    public static class MheTravel
    {
        /// <summary>Everyone equal for now (Tad, 2026-11-20); change this one number to experiment.</summary>
        public const int AvoidancePriority = 50;
        public const float DefaultArrive = 0.4f;
        /// <summary>A steered machine's centre stays this far from racks / walls (half the 1.27 m chassis + ~0.5 m).</summary>
        /// <summary>A scripted slide further than this off the machine's own forward AND backward axis turns the machine to face the way it goes first.</summary>
        private const float SlideAxisDeg = 25f;
        private const float RackClearance = 1.15f;
        /// <summary>Within this distance of the leg's goal the rack clamp is off (the caller's precise drive finishes the approach).</summary>
        private const float GoalNoClampMeters = 1.5f;
        public const float TimeoutReal = 180f;
        private const float TurnDegPerSec = 140f;
        private const int MaxReasserts = 12;
        /// <summary>Stall watchdog (game seconds): a leg that moved less than StallMoveMeters in StallSeconds re-rolls its avoidance priority and
        /// re-issues the destination; after StallGiveUpRounds such rounds in a row the leg fails cleanly instead of sitting out the timeout.</summary>
        private const float StallSeconds = 5f, StallMoveMeters = 0.3f;
        private const int StallGiveUpRounds = 3;
        /// <summary>Lane keep-clear zones (<see cref="MheLaneZone"/>): longest a machine waits outside one (game seconds), and how far ahead it looks.</summary>
        public const float ZoneWaitMax = 60f;
        private const float ZoneLookAhead = 2.2f;
        /// <summary>A straight scripted move stops this far (outline gap, m) short of another machine and waits at most <see cref="StraightWaitMax"/>.</summary>
        private const float StraightClearance = 0.3f, StraightWaitMax = 45f;
        /// <summary>Longest a turn on the spot may take (game seconds) before it is given up (a turn the clip guard keeps undoing).</summary>
        private const float TurnWaitMax = 30f;

        /// <summary>Per-machine avoidance priority: a stable value in 44..56 from the object's identity hash. With every machine at 50 three agents
        /// meeting at one spot yield equally and lock into a symmetric standoff (the jack triangle, 2026-11-22); distinct priorities let one go.</summary>
        public static int PriorityFor(Component c)
        {
            if (c == null) return AvoidancePriority;
            return 44 + (int)(unchecked((uint)System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(c.gameObject) * 2654435761u) % 13u);
        }

        private static readonly HashSet<Transform> s_active = new HashSet<Transform>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_active.Clear();

        /// <summary>True while a <see cref="Leg"/> owns this machine's agent: anything that parks / idles / disables the agent must wait.</summary>
        public static bool IsTravelling(Component c) => c != null && s_active.Contains(c.transform);

        public static IEnumerator Leg(Transform machine, Vector3 target, string phase, Action<bool> onDone, float arrive = DefaultArrive, Func<bool> abort = null)
        {
            if (machine == null) { onDone?.Invoke(false); yield break; }
            var agent = machine.GetComponent<NavMeshAgent>();
            var nav = machine.GetComponent<AiNavigation>();
            if (agent == null)
            {
                Debug.LogError($"[MheTravel] '{machine.name}' {phase}: no NavMeshAgent on this machine.");
                onDone?.Invoke(false);
                yield break;
            }

            s_active.Add(machine);
            bool navWas = nav != null && nav.enabled;
            if (nav != null) { nav.CancelSeekPosition(); nav.DriverOwnsMovement = true; nav.enabled = false; }
            float speedWas = agent.speed;
            agent.updatePosition = false;
            agent.updateRotation = false;
            agent.avoidancePriority = PriorityFor(machine);
            agent.areaMask = NavMesh.AllAreas;
            // Prefab stoppingDistance is 0.8-1.0 m: the agent halted that far short of every goal while the leg wanted 0.4 m, so legs
            // "stalled 0.9 m short" and failed (2026-11-22). The leg judges arrival itself.
            float stopWas = agent.stoppingDistance;
            agent.stoppingDistance = 0f;
            MheStandingObstacle.Suspend(machine);
            agent.enabled = true;

            bool ok = false;
            try
            {
                // Always re-seat the agent on the machine: while it was off the machine was moved by scripts / other legs, and a re-enabled
                // agent keeps its OLD internal position (isOnNavMesh is true there), which made every path from it PathInvalid (2026-11-20).
                // A lane mouth another machine is working is closed: wait (standing still) until it is free. Goal-in-zone only; a path
                // that runs into a zone is held below, in the loop.
                float zoneWaitFrom = Time.time;
                bool zoneLogged = false;
                while (machine != null && MheLaneZone.IsInsideForeign(new Vector3(target.x, machine.position.y, target.z), machine)
                       && Time.time - zoneWaitFrom < ZoneWaitMax && !(abort != null && abort()))
                {
                    if (!zoneLogged) { zoneLogged = true; Debug.Log($"[MheTravel] '{machine.name}' {phase}: goal is in a lane mouth another machine is working - waiting."); }
                    yield return null;
                }
                if (machine == null) { onDone?.Invoke(false); yield break; }

                agent.Warp(machine.position);
                if (!agent.isOnNavMesh)
                {
                    Debug.LogWarning($"[MheTravel] '{machine.name}' {phase}: not on the NavMesh at {machine.position}.");
                }
                else
                {
                    float y = machine.position.y;
                    Vector3 goal = new Vector3(target.x, y, target.z);
                    // Snap to the mesh of THIS agent's type (the plain overload samples agent type 0 = Rat, whose mesh differs from the MHE one:
                    // a goal on the Rat mesh is off the MHE mesh and SetDestination gave PathInvalid, 2026-11-20).
                    var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
                    // Rack aisles and lane slots have no MHE mesh: the leg ends at the nearest mesh point (the aisle mouth) and the caller's own
                    // precise drive covers the rest, as every old leg did.
                    Vector3 requested = goal;
                    if (NavMesh.SamplePosition(goal, out var hit, 8f, filter)) goal = hit.position;
                    // DIAGNOSTIC (2026-11-26): MHEBoundary kept dragging a jack back from a spot outside the building. Which leg sends it there?
                    if (!MHEBoundary.IsInside(goal))
                        Debug.LogWarning($"[MheTravel] '{machine.name}' {phase}: GOAL OUTSIDE BUILDING - asked for {requested}, snapped to {goal} ({Vector3.Distance(requested, goal):0.0} m), from {machine.position}.");
                    agent.isStopped = false;
                    agent.ResetPath();
                    if (!agent.SetDestination(goal))
                    {
                        Debug.LogWarning($"[MheTravel] '{machine.name}' {phase}: no path to {goal}.");
                    }
                    else
                    {
                        float started = Time.realtimeSinceStartup;
                        int reasserts = 0;
                        Vector3 stallPos = machine.position; float stallAt = Time.time; int stallRounds = 0;
                        float zoneHoldSince = -1f, zoneIgnoreUntil = 0f;
                        while (machine != null && Time.realtimeSinceStartup - started < TimeoutReal)
                        {
                            yield return null;
                            if (machine == null) break;
                            if (abort != null && abort()) break;
                            // The agent itself got there. A long frame (low fps / fast game speed: a reach truck covers 1-2 m a frame) can step
                            // the agent from outside the arrive radius straight onto the goal, where it consumes its path (hasPath false). The
                            // "lost" check below read that as a cleared path, re-seated the agent one frame BEHIND the machine and re-issued the
                            // destination, until the re-assert limit failed the leg 0.7-2.4 m short (block 74: 134 failures). Arrival is judged
                            // on the agent first, and the machine follows it.
                            if (agent.enabled && agent.isOnNavMesh && !agent.pathPending)
                            {
                                Vector3 an = agent.nextPosition;
                                if (new Vector2(an.x - goal.x, an.z - goal.z).magnitude <= arrive)
                                {
                                    machine.position = new Vector3(an.x, machine.position.y, an.z);
                                    ok = true;
                                    break;
                                }
                            }
                            // Other code sometimes switches this machine's agent off, or clears its path, in the middle of a leg (drive-mode
                            // switches, parking, watchdogs). Put it back and re-issue the destination instead of giving the leg up.
                            bool lost = !agent.enabled || !agent.isOnNavMesh
                                        || (!agent.pathPending && (agent.pathStatus == NavMeshPathStatus.PathInvalid || !agent.hasPath));
                            if (lost)
                            {
                                if (++reasserts > MaxReasserts) break;
                                MheStandingObstacle.Suspend(machine);
                                agent.enabled = true;
                                agent.updatePosition = false;
                                agent.updateRotation = false;
                                agent.Warp(machine.position);
                                agent.isStopped = false;
                                agent.SetDestination(goal);
                                continue;
                            }
                            if (agent.pathPending) continue;

                            // Stop short of a lane mouth another machine is working and wait for it to clear (a machine already inside keeps moving,
                            // so it can drive out). Uses the steering target, which stays valid while the agent is stopped.
                            if (Time.time >= zoneIgnoreUntil)
                            {
                                bool hold = false;
                                Vector3 toward = agent.steeringTarget - machine.position; toward.y = 0f;
                                if (toward.sqrMagnitude > 0.01f && !MheLaneZone.IsInsideForeign(machine.position, machine))
                                    hold = MheLaneZone.IsInsideForeign(machine.position + toward.normalized * ZoneLookAhead, machine);
                                if (hold)
                                {
                                    if (zoneHoldSince < 0f) zoneHoldSince = Time.time;
                                    if (Time.time - zoneHoldSince > ZoneWaitMax) { zoneIgnoreUntil = Time.time + 20f; zoneHoldSince = -1f; }
                                    else
                                    {
                                        agent.isStopped = true;
                                        agent.velocity = Vector3.zero;
                                        stallAt = Time.time; stallPos = machine.position; stallRounds = 0;
                                        continue;
                                    }
                                }
                                else zoneHoldSince = -1f;
                            }
                            if (agent.isStopped) agent.isStopped = false;

                            float mult = EquipmentBattery.SpeedMultiplierFor(machine);
                            agent.speed = speedWas * mult;
                            Vector3 next = agent.nextPosition;
                            Vector3 pos = machine.position;
                            machine.position = new Vector3(next.x, pos.y, next.z);
                            // Keep the centre RackClearance off racks / walls (agents hug rack legs). Near the goal the caller's own precise
                            // drive takes over, so a goal close to a rack is not fought.
                            if (new Vector2(machine.position.x - goal.x, machine.position.z - goal.z).sqrMagnitude > GoalNoClampMeters * GoalNoClampMeters)
                            {
                                Vector3 pushed = MachineClearance.KeepOffSolids(machine.position, RackClearance);
                                if ((pushed - machine.position).sqrMagnitude > 1e-6f)
                                {
                                    machine.position = pushed;
                                    agent.nextPosition = pushed;
                                }
                            }
                            Vector3 want = agent.desiredVelocity; want.y = 0f;
                            if (want.sqrMagnitude > 0.01f)
                                machine.rotation = Quaternion.RotateTowards(machine.rotation, Quaternion.LookRotation(want),
                                                                            TurnDegPerSec * mult * Time.deltaTime);

                            float left = new Vector2(machine.position.x - goal.x, machine.position.z - goal.z).magnitude;
                            if (left <= arrive || agent.remainingDistance <= arrive) { ok = true; break; }
                            if (agent.pathStatus == NavMeshPathStatus.PathPartial && !agent.hasPath) break;

                            // Stall watchdog (game time, so a paused game never trips it).
                            if ((machine.position - stallPos).sqrMagnitude > StallMoveMeters * StallMoveMeters) { stallPos = machine.position; stallAt = Time.time; stallRounds = 0; }
                            else if (Time.time - stallAt >= StallSeconds)
                            {
                                stallAt = Time.time;
                                if (++stallRounds >= StallGiveUpRounds)
                                {
                                    Debug.LogWarning($"[MheTravel] '{machine.name}' {phase}: stalled {StallSeconds * StallGiveUpRounds:0} s, {left:0.0} m short of {goal} - giving the leg up.");
                                    break;
                                }
                                agent.avoidancePriority = Mathf.Clamp(PriorityFor(machine) + UnityEngine.Random.Range(-6, 7), 1, 99);
                                agent.ResetPath();
                                agent.SetDestination(goal);
                            }
                        }
                        if (!ok && machine != null)
                        {
                            var diag = new NavMeshPath();
                            var filt = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = NavMesh.AllAreas };
                            bool startOn = NavMesh.SamplePosition(machine.position, out var sh, 1.5f, filt);
                            NavMesh.CalculatePath(startOn ? sh.position : machine.position, goal, filt, diag);
                            Debug.LogWarning($"[MheTravel] '{machine.name}' {phase}: did not arrive (status {agent.pathStatus}) from {machine.position} to {goal}; " +
                                             $"start on mesh: {startOn}, a static query says {diag.status}, agent.CalculatePath says {(agent.enabled && agent.isOnNavMesh ? AgentCalc(agent, goal) : "n/a")}, agent pos {agent.nextPosition}, destination {agent.destination}, agent on mesh: {agent.isOnNavMesh} enabled: {agent.enabled}.");
                        }
                    }
                }
            }
            finally
            {
                s_active.Remove(machine);
                if (machine != null)
                {
                    if (agent != null)
                    {
                        agent.speed = speedWas;
                        agent.stoppingDistance = stopWas;
                        if (agent.enabled && agent.isOnNavMesh) { agent.isStopped = true; agent.ResetPath(); }
                        agent.enabled = false;
                    }
                    if (nav != null) { nav.DriverOwnsMovement = false; nav.enabled = navWas; }
                }
            }
            onDone?.Invoke(ok);
        }

        private static string AgentCalc(NavMeshAgent a, Vector3 g) { var p = new NavMeshPath(); bool r = a.CalculatePath(g, p); return r + "/" + p.status; }

        /// <summary>Turn on the spot to face <paramref name="dir"/> (flat). No collision test: a turn in place is the caller's choice.</summary>
        public static IEnumerator Turn(Transform machine, Vector3 dir, float degPerSec = TurnDegPerSec)
        {
            dir.y = 0f;
            if (machine == null || dir.sqrMagnitude < 0.0001f) yield break;
            Quaternion want = Quaternion.LookRotation(dir.normalized);
            // MheClipGuard undoes a turn that would swing the outline into another machine, so a blocked turn makes no progress: give it
            // a limit (game seconds) and leave the machine as it stands rather than snapping it into the other machine.
            float startedAt = Time.time;
            while (machine != null && Quaternion.Angle(machine.rotation, want) > 0.5f)
            {
                if (Time.time - startedAt > TurnWaitMax)
                {
                    Debug.LogWarning($"[MheTravel] '{machine.name}': turn did not finish in {TurnWaitMax:0} s (blocked) - leaving it as it stands.");
                    yield break;
                }
                machine.rotation = Quaternion.RotateTowards(machine.rotation, want, degPerSec * EquipmentBattery.SpeedMultiplierFor(machine) * Time.deltaTime);
                yield return null;
            }
            if (machine != null) machine.rotation = want;
        }

        /// <summary>Straight line to <paramref name="target"/> (flat), keeping the heading. For short scripted moves (reverse into a lane).</summary>
        public static IEnumerator Straight(Transform machine, Vector3 target, float speed)
        {
            if (machine == null) yield break;
            // Never slide into another machine (Tad's rule): a step that would close within StraightClearance of one without moving away
            // waits instead. Backing out of a lane with something behind it therefore stops, it does not drive through.
            var slot = machine.GetComponent<MHEOperatorSlot>();
            float blockedSince = -1f;
            // NO SIDEWAYS SLIDING (Tad, 2026-11-22: reach trucks glided across the floor "as if on ice"): a slide that is neither forwards nor
            // backwards for the machine (more than SlideAxisDeg off both) first turns the machine to face the way it is going, drives, then
            // turns back to the heading it had, so the caller's pose contract still holds. Straight along the machine's own axis (reversing
            // out of a lane, forks-first moves) is untouched.
            Quaternion headingBefore = machine.rotation;
            bool turnedToTravel = false;
            {
                Vector3 first = new Vector3(target.x - machine.position.x, 0f, target.z - machine.position.z);
                if (first.magnitude > 0.6f)
                {
                    Vector3 fwd = machine.forward; fwd.y = 0f;
                    float a = Vector3.Angle(fwd, first);
                    if (a > SlideAxisDeg && 180f - a > SlideAxisDeg)
                    {
                        turnedToTravel = true;
                        yield return Turn(machine, first);
                    }
                }
            }
            while (machine != null)
            {
                Vector3 flat = new Vector3(target.x, machine.position.y, target.z);
                if ((flat - machine.position).magnitude <= 0.03f) { machine.position = flat; break; }
                Vector3 next = Vector3.MoveTowards(machine.position, flat, speed * EquipmentBattery.SpeedMultiplierFor(machine) * Time.deltaTime);
                if (slot != null && MachineClearance.VehicleSpacingBlocked(slot, next, machine.rotation, StraightClearance, out var blocker))
                {
                    if (blockedSince < 0f) blockedSince = Time.time;
                    if (Time.time - blockedSince > StraightWaitMax)
                    {
                        Debug.LogWarning($"[MheTravel] '{machine.name}': straight move blocked by '{(blocker != null ? blocker.name : "a machine")}' for {StraightWaitMax:0} s - stopping where it stands.");
                        yield break;
                    }
                    yield return null;
                    continue;
                }
                blockedSince = -1f;
                machine.position = next;
                yield return null;
            }
            if (turnedToTravel && machine != null)
                yield return Turn(machine, headingBefore * Vector3.forward);
        }
    }
}
