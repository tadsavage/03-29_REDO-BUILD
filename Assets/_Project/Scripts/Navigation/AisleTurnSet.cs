using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameCore.Actors
{
    /// <summary>One pallet-jack turn round a rack end, from one aisle into the next (Tad, 2026-11-15). Built from the rack geometry by
    /// <see cref="OrderSelectionTaskDriver.BuildAisleTurns"/>; all points are on the floor (y = <see cref="AisleTurnSet.groundY"/>).</summary>
    [Serializable]
    public class AisleTurn
    {
        public int fromAisle, toAisle;
        public bool atHighEnd;      // the end with the aisle's highest bay numbers (where its traffic leaves)
        public bool turnLeft;       // as seen by a jack driving out of fromAisle at this end
        public bool withTraffic;    // toAisle's traffic runs INTO the aisle from this end (only these turns are driven)
        public string lastPick;     // the last pick location on the turn side, the turn is measured from
        public Vector3 start;       // on fromAisle's right-lane line, at the scrimmage line (last pick centre + half its width along travel)
        public Vector3 mid;         // + the midpoint offset further on, and half the pick depth back toward the rack's back
        public Vector3 end;         // toAisle's right-lane line, level with the scrimmage line
        public Vector3 heading;     // direction of travel out of fromAisle at this end
        public Vector3 side;        // unit vector toward the rack the jack turns round (toward toAisle)

        public string Label => $"{fromAisle:00}->{toAisle:00} {(atHighEnd ? "high" : "low")} end, {(turnLeft ? "left" : "right")}{(withTraffic ? "" : " (against traffic)")}";
    }

    /// <summary>Where reach trucks enter and leave an aisle at one end (Tad, 2026-11-15): on the lane OPPOSITE the pallet jacks' lane,
    /// level with that end's scrimmage line. Every reach-truck leg into or out of an aisle drives through one.</summary>
    [Serializable]
    public class RtAisleGate
    {
        public int aisle;
        public bool atHighEnd;
        public Vector3 point;
        public Vector3 outward;   // out of the aisle at this end
        public string Label => $"RT gate {aisle:00} {(atHighEnd ? "high" : "low")} end";
    }

    /// <summary>Settings and the built turn points for pallet jacks going from aisle to aisle. The asset in Resources holds the settings;
    /// at run time a copy is filled with the turns of the warehouse that is loaded (never written back into the asset).</summary>
    [CreateAssetMenu(menuName = "Warehouse/Aisle Turn Set", fileName = "AisleTurnSet")]
    public class AisleTurnSet : ScriptableObject
    {
        [Tooltip("How far past the scrimmage line (last pick centre + half its width) the turn's midpoint is, in metres.")]
        public float turnMidpointOffset = 2.5f;   // Tad, 2026-11-15: 2 m -> 3.5 m; 2026-11-16: -1 m (turning too wide). Simulated: 2.5 m starts to brush the rack-end corner with the trailing forks, 1.5 m clips it.
        [Tooltip("Height of the ground the turn points sit on.")]
        public float groundY = 1.06f;
        [Tooltip("Width of one pick location along the rack (its local X).")]
        public float pickWidth = 1.325f;
        [Tooltip("Depth of one pick location into the rack (its local Z).")]
        public float pickDepth = 1.325f;

        [Tooltip("A reach truck waits while another reach truck is within this many metres of an aisle gate it is heading for.")]
        public float rtGateOccupancyRadius = 2f;

        [NonSerialized] public List<AisleTurn> turns = new List<AisleTurn>();
        [NonSerialized] public List<RtAisleGate> rtGates = new List<RtAisleGate>();
        [NonSerialized] public int builtFromLocations = -1;

        public bool TryGet(int from, int to, Vector3 travelDir, out AisleTurn turn)
        {
            turn = null;
            float best = 0.3f;
            foreach (var t in turns)
            {
                if (t.fromAisle != from || t.toAisle != to || !t.withTraffic) continue;
                float dot = Vector3.Dot(t.heading, travelDir);
                if (dot > best) { best = dot; turn = t; }
            }
            return turn != null;
        }
    }

    /// <summary>The live turn set and when it must be rebuilt.</summary>
    public static class AisleTurns
    {
        private static AisleTurnSet s_set;
        private static bool s_dirty = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { s_set = null; s_dirty = true; }

        public static AisleTurnSet Set
        {
            get
            {
                if (s_set == null)
                {
                    var asset = Resources.Load<AisleTurnSet>("AisleTurnSet");
                    s_set = asset != null ? UnityEngine.Object.Instantiate(asset) : ScriptableObject.CreateInstance<AisleTurnSet>();
                    s_set.name = "AisleTurnSet (live)";
                }
                return s_set;
            }
        }

        public static bool Dirty => s_dirty;
        /// <summary>Call after an aisle is set up, renamed or removed: the turns are rebuilt before the next one is driven.</summary>
        public static void MarkDirty() => s_dirty = true;
        internal static void MarkBuilt() => s_dirty = false;
    }

    /// <summary>Invisible marker for one turn (Start / Mid / End children); draws the turn in the Scene view.</summary>
    public class AisleTurnMarker : MonoBehaviour
    {
        public AisleTurn turn;

        private void OnDrawGizmos()
        {
            if (turn == null) return;
            Gizmos.color = turn.withTraffic ? Color.green : new Color(1f, 0.5f, 0f);
            Vector3 across = Vector3.Cross(Vector3.up, turn.heading).normalized;
            Gizmos.DrawLine(turn.start - across * 2f, turn.start + across * 2f);   // the scrimmage line
            Gizmos.DrawSphere(turn.start, 0.15f);
            Gizmos.DrawSphere(turn.mid, 0.2f);
            Gizmos.DrawSphere(turn.end, 0.15f);
            Gizmos.DrawLine(turn.start, turn.mid);
            Gizmos.DrawLine(turn.mid, turn.end);
        }
    }
}
