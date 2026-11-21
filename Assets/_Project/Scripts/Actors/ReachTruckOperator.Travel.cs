using System.Collections;
using UnityEngine;

namespace GameCore.Actors
{
    public partial class ReachTruckOperator
    {
        /// <summary>Every reach truck travel leg (2026-11-20 rework): a plain NavMeshAgent leg via <see cref="MheTravel"/>. Contract as before:
        /// onDone(true) = arrived, onDone(false) = could not (the caller aborts / re-queues); the truck is left in Manual drive mode.</summary>
        /// <summary>True while <see cref="MheTravel"/> owns the truck's agent: the Manual-phase enforcement in Update must not switch it off.</summary>
        private bool _inTravelLeg;

        private IEnumerator RtLeg(Vector3 target, string phase, System.Action<bool> onDone)
        {
            if (_heldLaneLock.HasValue) ReleaseLaneLock();   // an open-floor leg never runs inside a lane
            SetDriveMode(DriveMode.Manual, $"leg → {phase}");
            if (_vehicleNav != null) _vehicleNav.SetTaskBusy(true);
            bool ok = false;
            _inTravelLeg = true;
            yield return MheTravel.Leg(transform, target, phase, r => ok = r, 0.5f);
            _inTravelLeg = false;
            if (_vehicleNav != null) _vehicleNav.SetTaskBusy(false);
            onDone?.Invoke(ok);
        }
    }
}
