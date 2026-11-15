using System.Collections;
using UnityEngine;

namespace NavTest
{
    /// <summary>
    /// A box truck that drives in from the yard, swings round and BACKS into a dock door, waits, then pulls away and disappears.
    /// It lives outside the building (south of the dock wall), on the plain Ground, so it is never part of the NavMesh.
    /// Pivot = the middle of the trailer's rear doors; the truck's forward (+z) points at the cab.
    /// </summary>
    public class NavTestTruck : MonoBehaviour
    {
        private const float TrailerLength = 13f, TrailerWidth = 2.6f, TrailerHeight = 3.6f;
        private const float CabLength = 2.6f;

        private NavTestSim _sim;
        public int Door { get; private set; }
        public string State { get; private set; } = "arriving";

        public void Init(NavTestSim sim, int door)
        {
            _sim = sim; Door = door;
            NavTestSim.Box("Trailer", transform, new Vector3(0f, TrailerHeight * 0.5f + 0.4f, TrailerLength * 0.5f), new Vector3(TrailerWidth, TrailerHeight, TrailerLength), sim.trailerMat, false);
            NavTestSim.Box("Cab", transform, new Vector3(0f, 1.7f, TrailerLength + CabLength * 0.5f + 0.3f), new Vector3(TrailerWidth - 0.1f, 2.8f, CabLength), sim.cabMat, false);
            NavTestSim.Box("Windscreen", transform, new Vector3(0f, 2.4f, TrailerLength + CabLength + 0.31f), new Vector3(TrailerWidth - 0.4f, 0.9f, 0.05f), sim.wallMat, false);
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            float doorX = NavTestSim.DoorX[Door];
            float dockZ = NavTestSim.DockLineZ - 0.15f;        // rear doors against the wall
            Quaternion facingSouth = Quaternion.Euler(0f, 180f, 0f);   // cab south, rear doors north (at the dock)

            // Arrive: drive west along the yard, cab first, until the rear doors are just past the door.
            Vector3 yardStart = new Vector3(doorX + 45f, 0f, dockZ - 20f);
            Vector3 swingFrom = new Vector3(doorX - 6f,  0f, dockZ - 20f);
            Place(yardStart, Quaternion.Euler(0f, -90f, 0f));
            State = "arriving";
            yield return Drive(swingFrom, forward: true);

            // Swing: reverse in a quarter turn, the rear swinging round toward the door, ending lined up with it, cab south.
            State = "swinging";
            Vector3 lineUp = new Vector3(doorX, 0f, dockZ - 14f);
            yield return Arc(swingFrom, lineUp, Quaternion.Euler(0f, -90f, 0f), facingSouth);

            // Back in, slowly.
            State = "backing in";
            yield return Drive(new Vector3(doorX, 0f, dockZ), forward: false, speedScale: 0.4f);

            State = "at door";
            float dwell = Random.Range(_sim.truckDwellMin, Mathf.Max(_sim.truckDwellMin, _sim.truckDwellMax));
            for (float t = 0f; t < dwell; t += Time.deltaTime) yield return null;

            // Pull away south and leave.
            State = "leaving";
            yield return Drive(new Vector3(doorX, 0f, dockZ - 50f), forward: true);
            Destroy(gameObject);
        }

        private void Place(Vector3 local, Quaternion localRot)
        {
            transform.SetPositionAndRotation(_sim.W(local), _sim.transform.rotation * localRot);
        }

        private Vector3 LocalPos => _sim.L(transform.position);

        /// <summary>Straight drive to a local point keeping the current heading (forward or reversing).</summary>
        private IEnumerator Drive(Vector3 localTarget, bool forward, float speedScale = 1f)
        {
            while (true)
            {
                Vector3 p = LocalPos; p.y = 0f;
                Vector3 to = localTarget - p;
                float step = _sim.truckSpeed * speedScale * Time.deltaTime;
                if (to.magnitude <= step) { Place(localTarget, Quaternion.Inverse(_sim.transform.rotation) * transform.rotation); yield break; }
                transform.position = _sim.W(p + to.normalized * step);
                yield return null;
            }
        }

        /// <summary>A smooth quarter turn from a to b (quadratic curve through the corner), heading turning with it.</summary>
        private IEnumerator Arc(Vector3 a, Vector3 b, Quaternion fromRot, Quaternion toRot)
        {
            Vector3 corner = new Vector3(b.x, 0f, a.z);
            float length = (corner - a).magnitude + (b - corner).magnitude;
            float t = 0f;
            while (t < 1f)
            {
                t = Mathf.Min(1f, t + _sim.truckSpeed * 0.5f * Time.deltaTime / Mathf.Max(0.1f, length));
                Vector3 p = (1 - t) * (1 - t) * a + 2 * (1 - t) * t * corner + t * t * b;
                Place(p, Quaternion.Slerp(fromRot, toRot, t));
                yield return null;
            }
        }
    }
}
