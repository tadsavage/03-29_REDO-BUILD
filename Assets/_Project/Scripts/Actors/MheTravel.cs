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
        public const float TimeoutReal = 180f;
        private const float TurnDegPerSec = 140f;
        private const int MaxReasserts = 12;

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
            agent.avoidancePriority = AvoidancePriority;
            agent.areaMask = NavMesh.AllAreas;
            agent.enabled = true;

            bool ok = false;
            try
            {
                // Always re-seat the agent on the machine: while it was off the machine was moved by scripts / other legs, and a re-enabled
                // agent keeps its OLD internal position (isOnNavMesh is true there), which made every path from it PathInvalid (2026-11-20).
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
                    if (NavMesh.SamplePosition(goal, out var hit, 8f, filter)) goal = hit.position;
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
                        while (machine != null && Time.realtimeSinceStartup - started < TimeoutReal)
                        {
                            yield return null;
                            if (machine == null) break;
                            if (abort != null && abort()) break;
                            // Other code sometimes switches this machine's agent off, or clears its path, in the middle of a leg (drive-mode
                            // switches, parking, watchdogs). Put it back and re-issue the destination instead of giving the leg up.
                            bool lost = !agent.enabled || !agent.isOnNavMesh
                                        || (!agent.pathPending && (agent.pathStatus == NavMeshPathStatus.PathInvalid || !agent.hasPath));
                            if (lost)
                            {
                                if (++reasserts > MaxReasserts) break;
                                agent.enabled = true;
                                agent.updatePosition = false;
                                agent.updateRotation = false;
                                agent.Warp(machine.position);
                                agent.isStopped = false;
                                agent.SetDestination(goal);
                                continue;
                            }
                            if (agent.pathPending) continue;

                            float mult = EquipmentBattery.SpeedMultiplierFor(machine);
                            agent.speed = speedWas * mult;
                            Vector3 next = agent.nextPosition;
                            Vector3 pos = machine.position;
                            machine.position = new Vector3(next.x, pos.y, next.z);
                            Vector3 want = agent.desiredVelocity; want.y = 0f;
                            if (want.sqrMagnitude > 0.01f)
                                machine.rotation = Quaternion.RotateTowards(machine.rotation, Quaternion.LookRotation(want),
                                                                            TurnDegPerSec * mult * Time.deltaTime);

                            float left = new Vector2(machine.position.x - goal.x, machine.position.z - goal.z).magnitude;
                            if (left <= arrive || agent.remainingDistance <= arrive) { ok = true; break; }
                            if (agent.pathStatus == NavMeshPathStatus.PathPartial && !agent.hasPath) break;
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
            while (machine != null && Quaternion.Angle(machine.rotation, want) > 0.5f)
            {
                machine.rotation = Quaternion.RotateTowards(machine.rotation, want, degPerSec * EquipmentBattery.SpeedMultiplierFor(machine) * Time.deltaTime);
                yield return null;
            }
            if (machine != null) machine.rotation = want;
        }

        /// <summary>Straight line to <paramref name="target"/> (flat), keeping the heading. For short scripted moves (reverse into a lane).</summary>
        public static IEnumerator Straight(Transform machine, Vector3 target, float speed)
        {
            if (machine == null) yield break;
            while (machine != null)
            {
                Vector3 flat = new Vector3(target.x, machine.position.y, target.z);
                if ((flat - machine.position).magnitude <= 0.03f) { machine.position = flat; yield break; }
                machine.position = Vector3.MoveTowards(machine.position, flat, speed * EquipmentBattery.SpeedMultiplierFor(machine) * Time.deltaTime);
                yield return null;
            }
        }
    }
}
