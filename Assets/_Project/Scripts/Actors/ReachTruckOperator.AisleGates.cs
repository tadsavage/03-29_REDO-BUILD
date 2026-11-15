using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameCore.Actors
{
    /// <summary>
    /// REACH TRUCK AISLE GATES (Tad, 2026-11-15).
    ///
    /// A reach truck ALWAYS enters and leaves an aisle through that end's gate: on the lane opposite the pallet jacks' lane, level with
    /// the end's scrimmage line (built with the jack turns, OrderSelectionTaskDriver.BuildAisleTurns; green spheres). A leg out of an
    /// aisle first drives to the gate at the end toward its target; a leg into an aisle drives to the gate at the end toward the
    /// truck. ONE TRUCK PER GATE: while another reach truck is within <see cref="AisleTurnSet.rtGateOccupancyRadius"/> (2 m) of the
    /// gate, this one waits (short of it) until that truck has moved out of it (two trucks met nose to nose at an aisle end and stuck).
    /// </summary>
    public partial class ReachTruckOperator
    {
        private const float RtGateHoldBack = 5f;   // waits this far short of a busy gate

        /// <summary>The gate legs (exit gate of the aisle the truck is in, entry gate of the target's aisle) before a leg to
        /// <paramref name="target"/>. A gate leg that fails is logged and skipped: the leg itself still runs.</summary>
        private IEnumerator RtThroughAisleGates(Vector3 target, string phase)
        {
            var set = OrderSelectionTaskDriver.EnsureAisleTurns();
            if (set == null || set.rtGates.Count == 0) yield break;

            Vector3 from = transform.position;
            int fromAisle = AisleNoAt(from), toAisle = AisleNoAt(target);
            if (fromAisle == toAisle) yield break;   // same aisle (or both on open floor): nothing to go through

            var gates = new List<RtAisleGate>(2);
            Vector3 next = target;
            RtAisleGate entry = null;
            if (toAisle > 0) entry = NearestGate(set, toAisle, from, target);
            if (entry != null) next = entry.point;
            if (fromAisle > 0)
            {
                var exit = NearestGate(set, fromAisle, from, next);
                // Already at or out past that gate (e.g. parked at the aisle end): no need to drive back to it.
                if (exit != null && Vector3.Dot(GateFlat(from - exit.point), exit.outward) < -0.5f) gates.Add(exit);
            }
            if (entry != null) gates.Add(entry);

            foreach (var g in gates)
            {
                yield return WaitForRtGate(g, set.rtGateOccupancyRadius, phase);
                bool ok = false;
                yield return RtLegCore(g.point, $"{phase} (via {g.Label})", r => ok = r);
                if (!ok) Debug.LogWarning($"[ReachTruckOperator] '{name}' {phase}: could not reach {g.Label} at {g.point:F1} - driving the leg from here.");
            }
        }

        /// <summary>ONE TRUCK PER GATE: while another reach truck is within <paramref name="radius"/> of the gate, wait - from
        /// RtGateHoldBack metres short of it if the truck is further away than that.</summary>
        private IEnumerator WaitForRtGate(RtAisleGate g, float radius, string phase)
        {
            if (!RtGateBusy(g, radius, out var who)) yield break;
            if (PlanarDist(transform.position, g.point) > RtGateHoldBack + 1f)
            {
                Vector3 back = transform.position - g.point; back.y = 0f;
                Vector3 hold = g.point + (back.sqrMagnitude > 0.01f ? back.normalized : -g.outward) * RtGateHoldBack;
                hold.y = transform.position.y;
                Debug.Log($"[ReachTruckOperator] '{name}': {who.name} is in {g.Label} - waiting {RtGateHoldBack:0} m short of it.");
                yield return RtLegCore(hold, $"{phase} (wait for {g.Label})", null);
            }
            bool logged = false;
            while (RtGateBusy(g, radius, out who))
            {
                if (!logged) { Debug.Log($"[ReachTruckOperator] '{name}': holding for {who.name} to leave {g.Label}."); logged = true; }
                yield return null;
            }
        }

        private bool RtGateBusy(RtAisleGate g, float radius, out MHEOperatorSlot who)
        {
            who = null;
            foreach (var m in MachineClearance.AllMachines())
            {
                if (m == null || m == _operatorSlot || !m.isActiveAndEnabled || m.GetComponent<ReachTruckOperator>() == null) continue;
                if (PlanarDist(m.transform.position, g.point) <= radius) { who = m; return true; }
            }
            return false;
        }

        /// <summary>The gate of <paramref name="aisle"/> on the shorter way from <paramref name="a"/> through it to <paramref name="b"/>.</summary>
        private static RtAisleGate NearestGate(AisleTurnSet set, int aisle, Vector3 a, Vector3 b)
        {
            RtAisleGate best = null; float bestCost = float.MaxValue;
            foreach (var g in set.rtGates)
            {
                if (g.aisle != aisle) continue;
                float cost = PlanarDist(a, g.point) + PlanarDist(g.point, b);
                if (cost < bestCost) { bestCost = cost; best = g; }
            }
            return best;
        }

        /// <summary>The number of the aisle a point stands in (between its first and last bay, 1.5 m slack), or 0.</summary>
        private static int AisleNoAt(Vector3 p)
        {
            if (!OrderSelectionTaskDriver.InAnyAisle(p, out string aisle) || aisle == null) return 0;
            return int.TryParse(aisle, out int n) ? n : 0;
        }

        private static Vector3 GateFlat(Vector3 v) { v.y = 0f; return v; }
    }
}
