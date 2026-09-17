using UnityEngine;

/// <summary>
/// Spins a truck's visual wheel meshes to match its actual movement speed.
///
/// This rig's wheel meshes were exported with every Transform pivot sitting at the
/// vehicle's own origin (not at each wheel's hub) — the geometry is offset entirely
/// inside the baked mesh data. Rotating a wheel's Transform in place would therefore
/// make the mesh orbit the truck's center instead of spinning around its axle. Instead,
/// each wheel's true hub position is measured once (from its renderer bounds) and the
/// wheel is spun around that world-space point every frame with Transform.RotateAround,
/// which keeps the hub fixed while the mesh itself rotates.
///
/// Speed is derived from this object's own frame-to-frame displacement, so it tracks
/// whatever is actually moving the truck (state machine, waypoint follower, etc.)
/// without needing a direct reference to that logic, and naturally reverses spin
/// direction when the truck is backing up.
/// </summary>
public class WheelSpinAnimator : MonoBehaviour
{
    [System.Serializable]
    public struct Wheel
    {
        public Transform wheelTransform;

        [Tooltip("Wheel radius in world units. Leave at 0 to auto-measure from the wheel's renderer bounds on start.")]
        public float radius;
    }

    [Tooltip("Wheels to spin. Radius is auto-measured from renderer bounds when left at 0.")]
    [SerializeField] private Wheel[] wheels;

    [Tooltip("Axle direction in each wheel's own local space. Almost always local right (X).")]
    [SerializeField] private Vector3 localAxleAxis = Vector3.right;

    [Tooltip("Below this speed (world units/sec) wheels are treated as stopped, to avoid jitter from tiny per-frame drift while idle.")]
    [SerializeField] private float minSpeedThreshold = 0.02f;

    private Vector3[] _hubLocalOffsets;
    private Vector3 _lastPosition;
    private bool _hasLastPosition;

    private void Awake()
    {
        _hubLocalOffsets = new Vector3[wheels.Length];
        for (int i = 0; i < wheels.Length; i++)
        {
            var w = wheels[i];
            if (w.wheelTransform == null) continue;

            var renderer = w.wheelTransform.GetComponent<Renderer>();
            if (renderer != null)
            {
                // Renderer.bounds is world-space; every ancestor in this rig sits at local
                // (0,0,0) with identity rotation, so the wheel's own InverseTransformPoint
                // here gives us the correct hub offset to re-derive each frame at runtime,
                // regardless of where/how the truck root has since moved or turned.
                _hubLocalOffsets[i] = w.wheelTransform.InverseTransformPoint(renderer.bounds.center);

                if (wheels[i].radius <= 0f)
                {
                    Vector3 size = renderer.bounds.size;
                    wheels[i].radius = Mathf.Max(size.y, size.z) * 0.5f;
                }
            }
        }

        _lastPosition = transform.position;
        _hasLastPosition = true;
    }

    private void LateUpdate()
    {
        if (!_hasLastPosition)
        {
            _lastPosition = transform.position;
            _hasLastPosition = true;
            return;
        }

        Vector3 delta = transform.position - _lastPosition;
        _lastPosition = transform.position;

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        float speed = delta.magnitude / dt;
        if (speed < minSpeedThreshold) return;

        float dir = Mathf.Sign(Vector3.Dot(delta, transform.forward));
        if (dir == 0f) dir = 1f;

        for (int i = 0; i < wheels.Length; i++)
        {
            var w = wheels[i];
            if (w.wheelTransform == null || w.radius <= 0f) continue;

            float angleDegrees = (speed / w.radius) * Mathf.Rad2Deg * dt * dir;
            Vector3 hubWorld = w.wheelTransform.TransformPoint(_hubLocalOffsets[i]);
            Vector3 axisWorld = w.wheelTransform.TransformDirection(localAxleAxis);
            w.wheelTransform.RotateAround(hubWorld, axisWorld, angleDegrees);
        }
    }
}
