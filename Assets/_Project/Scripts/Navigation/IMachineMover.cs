using System;
using System.Collections;
using UnityEngine;

namespace GameCore.Navigation
{
    public enum MoveStatus { Arrived, NoPath, Blocked, TimedOut, Cancelled }

    public struct MoveRequest
    {
        /// <summary>The machine (or dock stocker) to move.</summary>
        public Transform Machine;
        public Vector3 Target;
        /// <summary>For the log line only: "jack to bay", "RT putaway approach"...</summary>
        public string Phase;
        /// <summary>Arrive facing this lattice heading (0 N, 1 E, 2 S, 3 W); -1 = any.</summary>
        public int FinalHeading;
        /// <summary>Counts as arrived within this many metres (planar). 0 = the backend's default.</summary>
        public float ArriveRadius;
        /// <summary>Ground speed (m/s) and turn rate (deg/s) the machine drives at, battery multiplier already applied.</summary>
        public float DriveSpeed, TurnSpeed;
        /// <summary>May the route back the machine up? (A machine standing nose-along a wall must, to turn.)</summary>
        public bool CanReverse;
        /// <summary>No part of the route may touch a rack aisle (dock stockers: Tad, 2026-11-09).</summary>
        public bool AvoidAisles;
        /// <summary>Plan this far (metres) clear of every unmanned machine; 0 = just not through it. Normally MachineClearance.VehicleSpacingMeters.</summary>
        public float VehicleBuffer;
    }

    public struct MoveResult
    {
        public MoveStatus Status;
        public string Detail;
        public bool Arrived => Status == MoveStatus.Arrived;
    }

    /// <summary>
    /// THE seam between the workflows (putaway, replenishment, selection, dock load/unload) and how a machine gets from A to B.
    /// A workflow says WHERE and gets back arrived / failed; it never touches a NavMeshAgent, a seek backstop or a lane-approach
    /// script move. Today that is four near-identical private copies of SeekViaNavMesh (ReachTruckOperator, TrailerLoadController,
    /// TrailerLoadController.DigOut, TrailerOffloadController) plus the jack's WalkTo / DriveToAisleMouth / LaneDriveTo, about 50 call
    /// sites. Each gets one implementation behind this interface, so the NavMesh backend and the grid backend can be swapped per role.
    ///
    /// Contract (taken from the existing copies, because every one of them learned it the hard way):
    ///  - NO straight-line fallback. A failed route is a loud failure (<see cref="MoveStatus.NoPath"/>); the caller re-queues the task.
    ///  - A move never drives through racks, walls, machines or pallets: the backend owns that, the caller never has to.
    ///  - On return the machine is stopped and the caller owns the transform again for its precision step (the final approach to a bay).
    ///  - Exactly one mover owns a machine at a time; nothing else may flip its agent.
    /// </summary>
    public interface IMachineMover
    {
        IEnumerator MoveTo(MoveRequest request, Action<MoveResult> onDone);
    }
}
